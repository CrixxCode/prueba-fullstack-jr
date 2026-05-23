import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  HostListener,
  OnInit,
  inject
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterOutlet } from '@angular/router';

import { User } from '../../core/models/user.model';
import { UserService } from '../../core/services/user.service';

@Component({
  selector: 'app-user-list',
  imports: [CommonModule, FormsModule, RouterOutlet],
  templateUrl: './user-list.html',
  styleUrl: './user-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserList implements OnInit {
  private readonly destroyRef = inject(DestroyRef);
  private readonly userService = inject(UserService);
  private readonly router = inject(Router);
  private toastTimer: ReturnType<typeof setTimeout> | null = null;
  private searchTimer: ReturnType<typeof setTimeout> | null = null;

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
    this.destroyRef.onDestroy(() => {
      this.clearToastTimer();
      this.clearSearchTimer();
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

  loadUsers(): void {
    this.loading = true;
    this.errorMessage = '';

    this.userService.getUsers(this.search, this.page, this.size).subscribe({
      next: (response) => {
        this.users = response.items;
        this.page = response.page;
        this.size = response.size;
        this.totalItems = response.totalItems;
        this.totalPages = response.totalPages;

        this.syncSelectedUser();
      },
      error: (error) => {
        this.errorMessage = error?.error?.message || 'No se pudieron cargar los usuarios.';
        this.loading = false;
      },
      complete: () => {
        this.loading = false;
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
    this.detailLoading = true;
    this.detailErrorMessage = '';

    this.userService.getUserById(id).subscribe({
      next: (user) => {
        if (this.selectedUserId === id) {
          this.selectedUser = user;
        }
      },
      error: (error) => {
        if (this.selectedUserId === id) {
          this.detailErrorMessage = error?.error?.message || 'No se pudo cargar el detalle del usuario.';
        }
      },
      complete: () => {
        if (this.selectedUserId === id) {
          this.detailLoading = false;
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
    this.router.navigate(['/users/new']);
  }

  goToEdit(id: string): void {
    this.router.navigate(['/users', id, 'edit']);
  }

  requestDeleteUser(user: User): void {
    this.userToDelete = user;
  }

  cancelDelete(): void {
    if (this.deleting) {
      return;
    }

    this.userToDelete = null;
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
      },
      complete: () => {
        this.deleting = false;
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
  }

  trackByUserId(_: number, user: User): string {
    return user.id;
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

    this.toastTimer = setTimeout(() => {
      this.toastVisible = false;
      this.toastTimer = null;
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
}
