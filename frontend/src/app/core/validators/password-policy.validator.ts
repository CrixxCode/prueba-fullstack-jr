import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

const DEFAULT_MIN_LENGTH = 8;

export const PASSWORD_POLICY_MESSAGE =
  'La contrasena debe tener minimo 8 caracteres, una mayuscula, una minuscula, un numero y un simbolo.';

export function strongPasswordValidator(
  minLength = DEFAULT_MIN_LENGTH
): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = normalizeValue(control.value);

    if (!value) {
      return null;
    }

    const errors: ValidationErrors = {};

    if (value.length < minLength) {
      errors['passwordMinLength'] = true;
    }

    if (!/[A-Z]/.test(value)) {
      errors['passwordUppercase'] = true;
    }

    if (!/[a-z]/.test(value)) {
      errors['passwordLowercase'] = true;
    }

    if (!/[0-9]/.test(value)) {
      errors['passwordDigit'] = true;
    }

    if (!/[^A-Za-z0-9]/.test(value)) {
      errors['passwordSymbol'] = true;
    }

    return Object.keys(errors).length > 0 ? errors : null;
  };
}

export function optionalStrongPasswordValidator(
  minLength = DEFAULT_MIN_LENGTH
): ValidatorFn {
  const validator = strongPasswordValidator(minLength);

  return (control: AbstractControl): ValidationErrors | null => {
    const value = normalizeValue(control.value);

    if (!value) {
      return null;
    }

    return validator(control);
  };
}

function normalizeValue(value: unknown): string {
  if (value == null) {
    return '';
  }

  return String(value).trim();
}
