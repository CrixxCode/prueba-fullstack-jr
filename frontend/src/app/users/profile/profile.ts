import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  HostListener,
  OnInit,
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
    email: [{ value: '', disabled: true }, [Validators.required, Validators.email]],
    password: ['', [optionalStrongPasswordValidator()]]
  });

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

  openAvatarModal(): void {
    if (this.uploadingAvatar) {
      return;
    }

    this.avatarModalOpen = true;
    this.avatarModalErrorMessage = '';
    this.selectedAvatarFile = null;
  }

  closeAvatarModal(): void {
    if (this.uploadingAvatar) {
      return;
    }

    this.avatarModalOpen = false;
    this.avatarModalErrorMessage = '';
    this.selectedAvatarFile = null;
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
          this.avatarModalOpen = false;
          this.selectedAvatarFile = null;
        },
        error: (error) => {
          this.avatarModalErrorMessage = error?.error?.message || 'No se pudo cargar el avatar.';
          this.cdr.markForCheck();
        }
      });
  }

  requestRemoveAvatar(): void {
    if (!this.currentUser || this.uploadingAvatar) {
      return;
    }

    this.deleteAvatarDialogOpen = true;
  }

  cancelRemoveAvatar(): void {
    if (this.uploadingAvatar) {
      return;
    }

    this.deleteAvatarDialogOpen = false;
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
          this.deleteAvatarDialogOpen = false;
        },
        error: (error) => {
          this.errorMessage = error?.error?.message || 'No se pudo eliminar el avatar.';
          this.cdr.markForCheck();
        }
      });
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
