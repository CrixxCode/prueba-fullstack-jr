import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectorRef,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  HostListener,
  OnInit,
  ViewChild,
  inject
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterOutlet } from '@angular/router';
import { finalize } from 'rxjs';

import { User } from '../../core/models/user.model';
import { UserService } from '../../core/services/user.service';

@Component({
  selector: 'app-user-list',
  imports: [CommonModule, FormsModule, RouterOutlet],
  templateUrl: './user-list.html',
  styleUrl: './user-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserList implements OnInit, AfterViewInit {
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);
  private readonly userService = inject(UserService);
  private readonly router = inject(Router);
  private toastTimer: ReturnType<typeof setTimeout> | null = null;
  private searchTimer: ReturnType<typeof setTimeout> | null = null;
  private focusRetryTimer: ReturnType<typeof setTimeout> | null = null;
  private usersRequestSeq = 0;
  private detailRequestSeq = 0;
  private pendingReturnFocusTarget: string | null = null;
  private pendingReturnFocusAttempts = 0;
  private readonly maxPendingReturnFocusAttempts = 20;
  private lastFocusedElementBeforeDeleteModal: HTMLElement | null = null;

  @ViewChild('deleteDialog')
  private deleteDialogRef?: ElementRef<HTMLElement>;

  @ViewChild('deleteCancelButton')
  private deleteCancelButtonRef?: ElementRef<HTMLButtonElement>;

  users: User[] = [];
  selectedUser: User | null = null;
  selectedUserId: string | null = null;
  userToDelete: User | null = null;

  search = '';
  page = 1;
  size = 10;
  totalItems = 0;
  totalPages = 0;

  loading = false;
  detailLoading = false;
  deleting = false;
  toastVisible = false;
  toastMessage = '';
  toastType: 'created' | 'deleted' = 'created';
  errorMessage = '';
  detailErrorMessage = '';

  ngOnInit(): void {
    this.pendingReturnFocusTarget = this.readReturnFocusTarget();

    this.destroyRef.onDestroy(() => {
      this.clearToastTimer();
      this.clearSearchTimer();
      this.clearFocusRetryTimer();
    });

    this.userService.usersChanged$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.loadUsers();
      });

    this.userService.userAction$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        if (event.type === 'created' || event.type === 'deleted') {
          this.showToast(event.message, event.type);
        }
      });

    this.loadUsers();
  }

  ngAfterViewInit(): void {
    this.focusPendingReturnTarget();
  }

  loadUsers(): void {
    const requestId = ++this.usersRequestSeq;
    this.loading = true;
    this.errorMessage = '';

    this.userService
      .getUsers(this.search, this.page, this.size)
      .pipe(
        finalize(() => {
          if (requestId !== this.usersRequestSeq) {
            return;
          }

          this.loading = false;
          this.cdr.markForCheck();
        })
      )
      .subscribe({
      next: (response) => {
        if (requestId !== this.usersRequestSeq) {
          return;
        }

        this.users = response.items;
        this.page = response.page;
        this.size = response.size;
        this.totalItems = response.totalItems;
        this.totalPages = response.totalPages;

        try {
          this.syncSelectedUser();
        } catch {
          this.detailLoading = false;
          this.detailErrorMessage = 'No se pudo sincronizar el detalle del usuario.';
        }

        this.cdr.markForCheck();
        this.focusPendingReturnTarget();
      },
      error: (error) => {
        if (requestId !== this.usersRequestSeq) {
          return;
        }

        this.errorMessage = error?.error?.message || 'No se pudieron cargar los usuarios.';
        this.cdr.markForCheck();
      }
    });
  }

  private syncSelectedUser(): void {
    if (!this.users.length) {
      this.selectedUser = null;
      this.selectedUserId = null;
      this.detailErrorMessage = '';
      this.detailLoading = false;
      return;
    }

    const selectedStillVisible = this.selectedUserId
      ? this.users.find((user) => user.id === this.selectedUserId)
      : null;

    if (selectedStillVisible) {
      this.selectedUser = selectedStillVisible;
      this.loadSelectedUser(selectedStillVisible.id);
      return;
    }

    this.selectUser(this.users[0]);
  }

  selectUser(user: User): void {
    this.selectedUserId = user.id;
    this.selectedUser = user;
    this.loadSelectedUser(user.id);
  }

  loadSelectedUser(id: string): void {
    const requestId = ++this.detailRequestSeq;
    this.detailLoading = true;
    this.detailErrorMessage = '';

    this.userService
      .getUserById(id)
      .pipe(
        finalize(() => {
          if (requestId !== this.detailRequestSeq || this.selectedUserId !== id) {
            return;
          }

          this.detailLoading = false;
          this.cdr.markForCheck();
        })
      )
      .subscribe({
      next: (user) => {
        if (requestId === this.detailRequestSeq && this.selectedUserId === id) {
          this.selectedUser = user;
          this.cdr.markForCheck();
        }
      },
      error: (error) => {
        if (requestId === this.detailRequestSeq && this.selectedUserId === id) {
          this.detailErrorMessage = error?.error?.message || 'No se pudo cargar el detalle del usuario.';
          this.cdr.markForCheck();
        }
      }
    });
  }

  searchUsers(): void {
    this.clearSearchTimer();
    this.page = 1;
    this.loadUsers();
  }

  onSearchChange(): void {
    this.page = 1;
    this.clearSearchTimer();

    this.searchTimer = setTimeout(() => {
      this.loadUsers();
      this.searchTimer = null;
    }, 350);
  }

  clearSearch(): void {
    this.search = '';
    this.clearSearchTimer();
    this.page = 1;
    this.loadUsers();
  }

  goToCreate(): void {
    this.router.navigate(['/users/new'], {
      state: {
        returnFocusTarget: 'open-create-user'
      }
    });
  }

  goToEdit(id: string): void {
    this.router.navigate(['/users', id, 'edit'], {
      state: {
        returnFocusTarget: `user-row-${id}`
      }
    });
  }

  requestDeleteUser(user: User, event?: Event): void {
    this.lastFocusedElementBeforeDeleteModal = this.resolveEventFocusSource(event);
    this.userToDelete = user;

    setTimeout(() => {
      this.deleteCancelButtonRef?.nativeElement.focus();
    });
  }

  cancelDelete(): void {
    if (this.deleting) {
      return;
    }

    this.userToDelete = null;
    this.restoreFocusAfterDeleteModal();
  }

  confirmDelete(): void {
    if (!this.userToDelete || this.deleting) {
      return;
    }

    const user = this.userToDelete;
    this.deleting = true;
    this.errorMessage = '';

    this.userService.deleteUser(user.id).subscribe({
      next: () => {
        this.userToDelete = null;
        this.restoreFocusAfterDeleteModal();

        if (this.selectedUserId === user.id) {
          this.selectedUser = null;
          this.selectedUserId = null;
        }

        this.userService.notifyUsersChanged();
        this.userService.notifyUserAction({
          type: 'deleted',
          message: 'Usuario eliminado correctamente.'
        });
      },
      error: (error) => {
        this.errorMessage = error?.error?.message || 'No se pudo eliminar el usuario.';
        this.cdr.markForCheck();
      },
      complete: () => {
        this.deleting = false;
        this.cdr.markForCheck();
      }
    });
  }

  previousPage(): void {
    if (this.page <= 1) {
      return;
    }

    this.page--;
    this.loadUsers();
  }

  nextPage(): void {
    if (this.page >= this.totalPages) {
      return;
    }

    this.page++;
    this.loadUsers();
  }

  dismissToast(): void {
    this.toastVisible = false;
    this.clearToastTimer();
    this.cdr.markForCheck();
  }

  trackByUserId(_: number, user: User): string {
    return user.id;
  }

  onDeleteDialogKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      if (this.deleting) {
        return;
      }

      event.preventDefault();
      event.stopPropagation();
      this.cancelDelete();
      return;
    }

    if (event.key === 'Tab') {
      this.trapFocus(event, this.deleteDialogRef?.nativeElement);
    }
  }

  @HostListener('document:keydown.escape')
  handleEscape(): void {
    if (this.userToDelete && !this.deleting) {
      this.cancelDelete();
      return;
    }

    if (this.toastVisible) {
      this.dismissToast();
    }
  }

  private showToast(message: string, type: 'created' | 'deleted'): void {
    this.toastMessage = message;
    this.toastType = type;
    this.toastVisible = true;
    this.clearToastTimer();
    this.cdr.markForCheck();

    this.toastTimer = setTimeout(() => {
      this.toastVisible = false;
      this.toastTimer = null;
      this.cdr.markForCheck();
    }, 3500);
  }

  private clearToastTimer(): void {
    if (this.toastTimer) {
      clearTimeout(this.toastTimer);
      this.toastTimer = null;
    }
  }

  private clearSearchTimer(): void {
    if (this.searchTimer) {
      clearTimeout(this.searchTimer);
      this.searchTimer = null;
    }
  }

  private trapFocus(event: KeyboardEvent, container: HTMLElement | undefined): void {
    if (!container) {
      return;
    }

    const focusableElements = this.getFocusableElements(container);

    if (!focusableElements.length) {
      event.preventDefault();
      container.focus();
      return;
    }

    const firstElement = focusableElements[0];
    const lastElement = focusableElements[focusableElements.length - 1];
    const activeElement = document.activeElement as HTMLElement | null;

    if (event.shiftKey && activeElement === firstElement) {
      event.preventDefault();
      lastElement.focus();
      return;
    }

    if (!event.shiftKey && activeElement === lastElement) {
      event.preventDefault();
      firstElement.focus();
    }
  }

  private getFocusableElements(container: HTMLElement): HTMLElement[] {
    const focusableSelectors = [
      'a[href]',
      'button:not([disabled])',
      'input:not([disabled])',
      'select:not([disabled])',
      'textarea:not([disabled])',
      '[tabindex]:not([tabindex="-1"])'
    ];

    return Array.from(
      container.querySelectorAll<HTMLElement>(focusableSelectors.join(','))
    ).filter((element) =>
      !element.hasAttribute('disabled') &&
      element.getAttribute('aria-hidden') !== 'true'
    );
  }

  private resolveEventFocusSource(event?: Event): HTMLElement | null {
    const target = event?.currentTarget;

    if (target instanceof HTMLElement) {
      return target;
    }

    const activeElement = document.activeElement;
    return activeElement instanceof HTMLElement ? activeElement : null;
  }

  private restoreFocusAfterDeleteModal(): void {
    const previousFocus = this.lastFocusedElementBeforeDeleteModal;
    this.lastFocusedElementBeforeDeleteModal = null;

    setTimeout(() => {
      if (previousFocus && document.contains(previousFocus)) {
        previousFocus.focus();
        return;
      }

      const fallback = document.getElementById('user-search');
      if (fallback instanceof HTMLElement) {
        fallback.focus();
      }
    });
  }

  private focusPendingReturnTarget(): void {
    if (!this.pendingReturnFocusTarget) {
      return;
    }

    const selector = `[data-focus-id="${this.pendingReturnFocusTarget}"]`;
    const target = document.querySelector(selector);

    if (target instanceof HTMLElement) {
      target.focus();
      this.pendingReturnFocusTarget = null;
      this.pendingReturnFocusAttempts = 0;
      this.clearFocusRetryTimer();
      return;
    }

    if (this.pendingReturnFocusAttempts >= this.maxPendingReturnFocusAttempts) {
      this.pendingReturnFocusTarget = null;
      this.pendingReturnFocusAttempts = 0;
      this.clearFocusRetryTimer();

      const fallback = document.getElementById('user-search');
      if (fallback instanceof HTMLElement) {
        fallback.focus();
      }

      return;
    }

    this.pendingReturnFocusAttempts++;

    this.clearFocusRetryTimer();
    this.focusRetryTimer = setTimeout(() => {
      this.focusRetryTimer = null;
      this.focusPendingReturnTarget();
    }, 60);
  }

  private clearFocusRetryTimer(): void {
    if (this.focusRetryTimer) {
      clearTimeout(this.focusRetryTimer);
      this.focusRetryTimer = null;
    }
  }

  private readReturnFocusTarget(): string | null {
    const state = window.history.state as { returnFocusTarget?: unknown } | null;
    const candidate = state?.returnFocusTarget;

    if (typeof candidate !== 'string') {
      return null;
    }

    const trimmed = candidate.trim();
    return trimmed ? trimmed : null;
  }
}
