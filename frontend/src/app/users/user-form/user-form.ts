import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  OnInit,
  ViewChild,
  inject
} from '@angular/core';
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
export class UserForm implements OnInit, AfterViewInit {
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly fb = inject(FormBuilder);
  private readonly userService = inject(UserService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private returnFocusTarget: string | null = null;

  @ViewChild('userFormDialog')
  private userFormDialogRef?: ElementRef<HTMLElement>;

  @ViewChild('userFormCloseButton')
  private userFormCloseButtonRef?: ElementRef<HTMLButtonElement>;

  @ViewChild('userFormNameInput')
  private userFormNameInputRef?: ElementRef<HTMLInputElement>;

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
    this.returnFocusTarget = this.readReturnFocusTarget();
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

  ngAfterViewInit(): void {
    this.focusInitialControl();
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
      },
      error: (error) => {
        this.errorMessage = error?.error?.message || 'No se pudo cargar el usuario.';
        this.loading = false;
        this.cdr.markForCheck();
      },
      complete: () => {
        this.loading = false;
        this.cdr.markForCheck();
        this.focusNameInput();
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
        this.navigateBackWithFocusRestore();
      },
      error: (error) => {
        this.errorMessage = error?.error?.message || 'No se pudo crear el usuario.';
        this.saving = false;
        this.cdr.markForCheck();
      },
      complete: () => {
        this.saving = false;
        this.cdr.markForCheck();
      }
    });
  }

  updateUser(id: string): void {
    const value = this.form.getRawValue();

    this.userService.updateUser(id, {
      name: value.name ?? '',
      email: value.email ?? '',
      password: value.password?.trim() ? value.password : null,
      role: value.role ?? 'user',
      isActive: value.isActive ?? true
    }).subscribe({
      next: () => {
        this.successMessage = 'Usuario actualizado correctamente.';
        this.userService.notifyUsersChanged();
        this.navigateBackWithFocusRestore();
      },
      error: (error) => {
        this.errorMessage = error?.error?.message || 'No se pudo actualizar el usuario.';
        this.saving = false;
        this.cdr.markForCheck();
      },
      complete: () => {
        this.saving = false;
        this.cdr.markForCheck();
      }
    });
  }

  goBack(): void {
    this.navigateBackWithFocusRestore();
  }

  trackByRole(_: number, role: UserRole): UserRole {
    return role;
  }

  onDialogKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      if (this.saving) {
        return;
      }

      event.preventDefault();
      event.stopPropagation();
      this.goBack();
      return;
    }

    if (event.key === 'Tab') {
      this.trapFocus(event, this.userFormDialogRef?.nativeElement);
    }
  }

  private navigateBackWithFocusRestore(): void {
    const state = this.returnFocusTarget
      ? { returnFocusTarget: this.returnFocusTarget }
      : undefined;

    this.router.navigate(['/users'], state ? { state } : undefined);
  }

  private focusInitialControl(): void {
    setTimeout(() => {
      if (this.loading) {
        this.userFormCloseButtonRef?.nativeElement.focus();
        return;
      }

      this.focusNameInput();
    });
  }

  private focusNameInput(): void {
    setTimeout(() => {
      this.userFormNameInputRef?.nativeElement.focus();
    });
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
