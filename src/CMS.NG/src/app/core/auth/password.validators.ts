import { AbstractControl, ValidationErrors } from '@angular/forms';

/** Bilingual complexity message — must match the backend PasswordPolicy.ComplexityMessage. */
export const PASSWORD_COMPLEXITY_MESSAGE =
  '密碼長度至少需 8 碼，且內容須至少包含四種字元的其中三種：大寫英文／小寫英文／數字／符號';

/**
 * New-password complexity: length >= 8 AND at least 3 of the 4 character classes
 * (uppercase, lowercase, digit, symbol). Mirrors the server-side rule.
 */
export function passwordComplexityValidator(control: AbstractControl): ValidationErrors | null {
  const value = (control.value ?? '') as string;
  if (!value) {
    return null; // let `required` report emptiness
  }

  let classes = 0;
  if (/[A-Z]/.test(value)) classes++;
  if (/[a-z]/.test(value)) classes++;
  if (/[0-9]/.test(value)) classes++;
  if (/[^A-Za-z0-9]/.test(value)) classes++;

  return value.length >= 8 && classes >= 3 ? null : { complexity: true };
}

/** Group validator: `newPassword` and `confirmPassword` controls must be equal. */
export function passwordsMatchValidator(group: AbstractControl): ValidationErrors | null {
  const newPassword = group.get('newPassword')?.value;
  const confirmPassword = group.get('confirmPassword')?.value;
  if (!newPassword || !confirmPassword) {
    return null;
  }
  return newPassword === confirmPassword ? null : { mismatch: true };
}
