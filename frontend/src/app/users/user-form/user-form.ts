import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, HostListener, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { UserService } from '../../core/services/user.service';
import { UserRole } from '../../core/models/user.model';
import {
  PASSWORD_POLICY_MESSAGE,
  optionalStrongPasswordValidator,
  strongPasswordValidator
} from '../../core/validators/password-policy.validator';

@Component({
  selector: 'app-user-form',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './user-form.html',
  styleUrl: './user-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserForm implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly userService = inject(UserService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  userId: string | null = null;
  isEditMode = false;

  loading = false;
  saving = false;
  errorMessage = '';
  successMessage = '';
  readonly passwordPolicyMessage = PASSWORD_POLICY_MESSAGE;

  roles: UserRole[] = ['admin', 'user'];

  form = this.fb.group({
    name: ['', [Validators.required, Validators.minLength(3)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [optionalStrongPasswordValidator()]],
    role: ['user' as UserRole, [Validators.required]],
    isActive: [true]
  });

  ngOnInit(): void {
    this.userId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.userId;

    if (this.isEditMode && this.userId) {
      this.loadUser(this.userId);
    } else {
      this.form.controls.password.setValidators([
        Validators.required,
        strongPasswordValidator()
      ]);
      this.form.controls.password.updateValueAndValidity();
    }
  }

  loadUser(id: string): void {
    this.loading = true;
    this.errorMessage = '';

    this.userService.getUserById(id).subscribe({
      next: (user) => {
        this.form.patchValue({
          name: user.name,
          email: user.email,
          password: '',
          role: user.role,
          isActive: user.isActive
        });

        this.form.controls.email.disable();
      },
      error: (error) => {
        this.errorMessage = error?.error?.message || 'No se pudo cargar el usuario.';
        this.loading = false;
      },
      complete: () => {
        this.loading = false;
      }
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.errorMessage = '';
    this.successMessage = '';

    if (this.isEditMode && this.userId) {
      this.updateUser(this.userId);
    } else {
      this.createUser();
    }
  }

  createUser(): void {
    const value = this.form.getRawValue();

    this.userService.createUser({
      name: value.name ?? '',
      email: value.email ?? '',
      password: value.password ?? '',
      role: value.role ?? 'user',
      isActive: value.isActive ?? true
    }).subscribe({
      next: () => {
        this.successMessage = 'Usuario creado correctamente.';
        this.userService.notifyUsersChanged();
        this.userService.notifyUserAction({
          type: 'created',
          message: 'Usuario creado correctamente.'
        });
        this.router.navigate(['/users']);
      },
      error: (error) => {
        this.errorMessage = error?.error?.message || 'No se pudo crear el usuario.';
        this.saving = false;
      },
      complete: () => {
        this.saving = false;
      }
    });
  }

  updateUser(id: string): void {
    const value = this.form.getRawValue();

    this.userService.updateUser(id, {
      name: value.name ?? '',
      password: value.password?.trim() ? value.password : null,
      role: value.role ?? 'user',
      isActive: value.isActive ?? true
    }).subscribe({
      next: () => {
        this.successMessage = 'Usuario actualizado correctamente.';
        this.userService.notifyUsersChanged();
        this.router.navigate(['/users']);
      },
      error: (error) => {
        this.errorMessage = error?.error?.message || 'No se pudo actualizar el usuario.';
        this.saving = false;
      },
      complete: () => {
        this.saving = false;
      }
    });
  }

  goBack(): void {
    this.router.navigate(['/users']);
  }

  trackByRole(_: number, role: UserRole): UserRole {
    return role;
  }

  @HostListener('document:keydown.escape')
  handleEscape(): void {
    if (this.saving) {
      return;
    }

    this.goBack();
  }
}
