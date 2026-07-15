import { FormControl, FormGroup } from '@angular/forms';
import {
  PASSWORD_COMPLEXITY_MESSAGE,
  passwordComplexityValidator,
  passwordsMatchValidator,
} from './password.validators';

describe('passwordComplexityValidator', () => {
  const check = (value: string) => passwordComplexityValidator(new FormControl(value));

  it('accepts 8+ chars with at least 3 of 4 classes', () => {
    expect(check('Str0ng!Pwd')).toBeNull(); // 4 classes
    expect(check('Abcdefg1')).toBeNull(); // upper+lower+digit
    expect(check('abcdefg1!')).toBeNull(); // lower+digit+symbol
  });

  it('rejects passwords shorter than 8', () => {
    expect(check('Ab1!')).toEqual({ complexity: true });
  });

  it('rejects passwords with fewer than 3 character classes', () => {
    expect(check('abcdefghij')).toEqual({ complexity: true }); // 1 class
    expect(check('abcdefgh1')).toEqual({ complexity: true }); // 2 classes
  });

  it('defers empty values to the required validator', () => {
    expect(check('')).toBeNull();
  });

  it('exposes the bilingual message', () => {
    expect(PASSWORD_COMPLEXITY_MESSAGE).toContain('大寫英文');
    expect(PASSWORD_COMPLEXITY_MESSAGE).toContain('符號');
  });
});

describe('passwordsMatchValidator', () => {
  const group = (newPassword: string, confirmPassword: string) =>
    new FormGroup({
      newPassword: new FormControl(newPassword),
      confirmPassword: new FormControl(confirmPassword),
    });

  it('passes when new and confirm match', () => {
    expect(passwordsMatchValidator(group('Str0ng!Pwd', 'Str0ng!Pwd'))).toBeNull();
  });

  it('fails when new and confirm differ', () => {
    expect(passwordsMatchValidator(group('Str0ng!Pwd', 'Other1!Pwd'))).toEqual({ mismatch: true });
  });
});
