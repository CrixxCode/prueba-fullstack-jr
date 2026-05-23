import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, ChangeDetectorRef, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { AuthService } from '../../core/services/auth.service';
import {
  PASSWORD_POLICY_MESSAGE,
  strongPasswordValidator
} from '../../core/validators/password-policy.validator';

@Component({
  selector: 'app-register',
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './register.html',
  styleUrl: './register.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class Register {
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  loading = false;
  errorMessage = '';
  readonly passwordPolicyMessage = PASSWORD_POLICY_MESSAGE;

  form = this.fb.group({
    name: ['', [Validators.required, Validators.minLength(3)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, strongPasswordValidator()]]
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading = true;
    this.errorMessage = '';

    const value = this.form.getRawValue();

    this.authService.register({
      name: value.name ?? '',
      email: value.email ?? '',
      password: value.password ?? ''
    }).subscribe({
      next: () => {
        if (this.authService.isAdmin()) {
          this.router.navigate(['/users']);
        } else {
          this.router.navigate(['/profile']);
        }
      },
      error: (error) => {
        this.errorMessage = error?.error?.message || 'No se pudo registrar el usuario.';
        this.loading = false;
        this.cdr.markForCheck();
      },
      complete: () => {
        this.loading = false;
        this.cdr.markForCheck();
      }
    });
  }
}
