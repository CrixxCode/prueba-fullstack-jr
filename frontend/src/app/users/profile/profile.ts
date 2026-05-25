import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  HostListener,
  OnInit,
  ViewChild,
  inject
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';

import { AuthService } from '../../core/services/auth.service';
import { UserService } from '../../core/services/user.service';
import { User } from '../../core/models/user.model';
import {
  PASSWORD_POLICY_MESSAGE,
  optionalStrongPasswordValidator
} from '../../core/validators/password-policy.validator';

@Component({
  selector: 'app-profile',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './profile.html',
  styleUrl: './profile.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class Profile implements OnInit {
  private static readonly maxAvatarSizeBytes = 2 * 1024 * 1024;
  private static readonly allowedAvatarTypes = new Set([
    'image/jpeg',
    'image/png',
    'image/webp'
  ]);

  private readonly fb = inject(FormBuilder);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly authService = inject(AuthService);
  private readonly userService = inject(UserService);
  private lastFocusedElementBeforeAvatarModal: HTMLElement | null = null;
  private lastFocusedElementBeforeDeleteAvatarDialog: HTMLElement | null = null;

  currentUser: User | null = null;

  loading = false;
  saving = false;
  uploadingAvatar = false;
  editMode = false;
  avatarModalOpen = false;
  deleteAvatarDialogOpen = false;
  selectedAvatarFile: File | null = null;
  avatarModalErrorMessage = '';

  errorMessage = '';
  successMessage = '';
  readonly passwordPolicyMessage = PASSWORD_POLICY_MESSAGE;

  form = this.fb.group({
    name: ['', [Validators.required, Validators.minLength(3)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [optionalStrongPasswordValidator()]]
  });

  @ViewChild('avatarDialog')
  private avatarDialogRef?: ElementRef<HTMLElement>;

  @ViewChild('avatarFileInput')
  private avatarFileInputRef?: ElementRef<HTMLInputElement>;

  @ViewChild('avatarCancelButton')
  private avatarCancelButtonRef?: ElementRef<HTMLButtonElement>;

  @ViewChild('deleteAvatarDialog')
  private deleteAvatarDialogRef?: ElementRef<HTMLElement>;

  @ViewChild('deleteAvatarCancelButton')
  private deleteAvatarCancelButtonRef?: ElementRef<HTMLButtonElement>;

  ngOnInit(): void {
    const storedUser = this.authService.getCurrentUser();

    if (!storedUser) {
      this.errorMessage = 'No se encontro informacion del usuario autenticado.';
      this.cdr.markForCheck();
      return;
    }

    this.currentUser = storedUser;
    this.loadProfile(storedUser.id);
  }

  loadProfile(id: string): void {
    this.loading = true;
    this.errorMessage = '';

    this.userService.getUserById(id).subscribe({
      next: (user) => {
        this.currentUser = user;

        this.form.patchValue({
          name: user.name,
          email: user.email,
          password: ''
        });

        this.authService.updateCurrentUser(user);
      },
      error: (error) => {
        this.errorMessage = error?.error?.message || 'No se pudo cargar el perfil.';
        this.loading = false;
        this.cdr.markForCheck();
      },
      complete: () => {
        this.loading = false;
        this.cdr.markForCheck();
      }
    });
  }

  enableEdit(): void {
    this.editMode = true;
    this.successMessage = '';
    this.errorMessage = '';
  }

  cancelEdit(): void {
    this.editMode = false;
    this.form.patchValue({
      name: this.currentUser?.name ?? '',
      email: this.currentUser?.email ?? '',
      password: ''
    });
  }

  submit(): void {
    if (!this.currentUser) {
      this.errorMessage = 'No hay usuario autenticado.';
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();

    this.saving = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.userService.updateUser(this.currentUser.id, {
      name: value.name ?? '',
      email: value.email ?? '',
      password: value.password?.trim() ? value.password : null
    }).subscribe({
      next: (updatedUser) => {
        this.applyUpdatedUser(updatedUser);
        this.successMessage = 'Perfil actualizado correctamente.';
        this.editMode = false;
      },
      error: (error) => {
        this.errorMessage = error?.error?.message || 'No se pudo actualizar el perfil.';
        this.saving = false;
        this.cdr.markForCheck();
      },
      complete: () => {
        this.saving = false;
        this.cdr.markForCheck();
      }
    });
  }

  handleAvatarSelected(event: Event): void {
    const input = event.target as HTMLInputElement | null;
    const file = input?.files?.[0];

    if (!file) {
      this.selectedAvatarFile = null;
      this.avatarModalErrorMessage = '';
      return;
    }

    if (!Profile.allowedAvatarTypes.has(file.type)) {
      this.avatarModalErrorMessage = 'Formato no valido. Usa JPG, PNG o WEBP.';
      this.selectedAvatarFile = null;
      input.value = '';
      this.cdr.markForCheck();
      return;
    }

    if (file.size > Profile.maxAvatarSizeBytes) {
      this.avatarModalErrorMessage = 'El avatar supera el tamano maximo permitido de 2 MB.';
      this.selectedAvatarFile = null;
      input.value = '';
      this.cdr.markForCheck();
      return;
    }

    this.avatarModalErrorMessage = '';
    this.selectedAvatarFile = file;
    this.cdr.markForCheck();
  }

  openAvatarModal(event?: Event): void {
    if (this.uploadingAvatar) {
      return;
    }

    this.lastFocusedElementBeforeAvatarModal = this.resolveEventFocusSource(event);
    this.avatarModalOpen = true;
    this.avatarModalErrorMessage = '';
    this.selectedAvatarFile = null;

    setTimeout(() => {
      const initialTarget =
        this.avatarFileInputRef?.nativeElement ??
        this.avatarCancelButtonRef?.nativeElement;
      initialTarget?.focus();
    });
  }

  closeAvatarModal(options?: { restoreFocus?: boolean; force?: boolean }): void {
    if (this.uploadingAvatar && !options?.force) {
      return;
    }

    this.avatarModalOpen = false;
    this.avatarModalErrorMessage = '';
    this.selectedAvatarFile = null;

    if (options?.restoreFocus ?? true) {
      this.restoreFocusAfterAvatarModal();
    }
  }

  submitAvatarUpload(): void {
    if (!this.currentUser) {
      this.errorMessage = 'No hay usuario autenticado.';
      this.cdr.markForCheck();
      return;
    }

    if (!this.selectedAvatarFile) {
      this.avatarModalErrorMessage = 'Debes seleccionar una imagen.';
      this.cdr.markForCheck();
      return;
    }

    this.uploadingAvatar = true;
    this.errorMessage = '';
    this.successMessage = '';
    this.avatarModalErrorMessage = '';

    this.userService
      .uploadAvatar(this.currentUser.id, this.selectedAvatarFile)
      .pipe(
        finalize(() => {
          this.uploadingAvatar = false;
          this.cdr.markForCheck();
        })
      )
      .subscribe({
        next: (updatedUser) => {
          this.applyUpdatedUser(updatedUser);
          this.successMessage = 'Avatar actualizado correctamente.';
          this.closeAvatarModal({ force: true });
        },
        error: (error) => {
          this.avatarModalErrorMessage = error?.error?.message || 'No se pudo cargar el avatar.';
          this.cdr.markForCheck();
        }
      });
  }

  requestRemoveAvatar(event?: Event): void {
    if (!this.currentUser || this.uploadingAvatar) {
      return;
    }

    this.lastFocusedElementBeforeDeleteAvatarDialog = this.resolveEventFocusSource(event);
    this.deleteAvatarDialogOpen = true;

    setTimeout(() => {
      this.deleteAvatarCancelButtonRef?.nativeElement.focus();
    });
  }

  cancelRemoveAvatar(options?: { restoreFocus?: boolean; force?: boolean }): void {
    if (this.uploadingAvatar && !options?.force) {
      return;
    }

    this.deleteAvatarDialogOpen = false;

    if (options?.restoreFocus ?? true) {
      this.restoreFocusAfterDeleteAvatarDialog();
    }
  }

  removeAvatar(): void {
    if (!this.currentUser || this.uploadingAvatar) {
      return;
    }

    this.uploadingAvatar = true;
    this.errorMessage = '';
    this.successMessage = '';

    this.userService
      .deleteAvatar(this.currentUser.id)
      .pipe(
        finalize(() => {
          this.uploadingAvatar = false;
          this.cdr.markForCheck();
        })
      )
      .subscribe({
        next: (updatedUser) => {
          this.applyUpdatedUser(updatedUser);
          this.successMessage = 'Avatar eliminado correctamente.';
          this.cancelRemoveAvatar({ force: true });
        },
        error: (error) => {
          this.errorMessage = error?.error?.message || 'No se pudo eliminar el avatar.';
          this.cdr.markForCheck();
        }
      });
  }

  onAvatarModalKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      if (this.uploadingAvatar) {
        return;
      }

      event.preventDefault();
      event.stopPropagation();
      this.closeAvatarModal();
      return;
    }

    if (event.key === 'Tab') {
      this.trapFocus(event, this.avatarDialogRef?.nativeElement);
    }
  }

  onDeleteAvatarDialogKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      if (this.uploadingAvatar) {
        return;
      }

      event.preventDefault();
      event.stopPropagation();
      this.cancelRemoveAvatar();
      return;
    }

    if (event.key === 'Tab') {
      this.trapFocus(event, this.deleteAvatarDialogRef?.nativeElement);
    }
  }

  get avatarLetter(): string {
    return this.currentUser?.name?.charAt(0)?.toUpperCase() || 'U';
  }

  @HostListener('document:keydown.escape')
  handleEscape(): void {
    if (this.uploadingAvatar) {
      return;
    }

    if (this.deleteAvatarDialogOpen) {
      this.cancelRemoveAvatar();
      this.cdr.markForCheck();
      return;
    }

    if (this.avatarModalOpen) {
      this.closeAvatarModal();
      this.cdr.markForCheck();
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

  private restoreFocusAfterAvatarModal(): void {
    const previousFocus = this.lastFocusedElementBeforeAvatarModal;
    this.lastFocusedElementBeforeAvatarModal = null;

    setTimeout(() => {
      if (previousFocus && document.contains(previousFocus)) {
        previousFocus.focus();
        return;
      }

      const fallback = document.querySelector<HTMLElement>('[data-focus-id="open-avatar-modal"]');
      fallback?.focus();
    });
  }

  private restoreFocusAfterDeleteAvatarDialog(): void {
    const previousFocus = this.lastFocusedElementBeforeDeleteAvatarDialog;
    this.lastFocusedElementBeforeDeleteAvatarDialog = null;

    setTimeout(() => {
      if (previousFocus && document.contains(previousFocus)) {
        previousFocus.focus();
        return;
      }

      const fallback = document.querySelector<HTMLElement>('[data-focus-id="open-remove-avatar-dialog"]');
      if (fallback) {
        fallback.focus();
        return;
      }

      const avatarFallback = document.querySelector<HTMLElement>('[data-focus-id="open-avatar-modal"]');
      avatarFallback?.focus();
    });
  }

  private applyUpdatedUser(updatedUser: User): void {
    this.currentUser = updatedUser;
    this.authService.updateCurrentUser(updatedUser);

    this.form.patchValue({
      name: updatedUser.name,
      email: updatedUser.email,
      password: ''
    });
    this.cdr.markForCheck();
  }
}
