import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

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
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly userService = inject(UserService);

  currentUser: User | null = null;

  loading = false;
  saving = false;
  editMode = false;

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
      },
      complete: () => {
        this.loading = false;
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
        this.currentUser = updatedUser;
        this.authService.updateCurrentUser(updatedUser);

        this.form.patchValue({
          name: updatedUser.name,
          email: updatedUser.email,
          password: ''
        });

        this.successMessage = 'Perfil actualizado correctamente.';
        this.editMode = false;
      },
      error: (error) => {
        this.errorMessage = error?.error?.message || 'No se pudo actualizar el perfil.';
        this.saving = false;
      },
      complete: () => {
        this.saving = false;
      }
    });
  }

  get avatarLetter(): string {
    return this.currentUser?.name?.charAt(0)?.toUpperCase() || 'U';
  }
}
