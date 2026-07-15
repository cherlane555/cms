import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { environment } from '@env';
import { AuthService } from '@core/auth/auth.service';
import { Profile } from './profile';

/** Unsigned JWT carrying the given roles, as the API emits them. */
function makeJwt(roles: string[]): string {
  const b64url = (obj: unknown) =>
    btoa(JSON.stringify(obj)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  const payload = {
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role':
      roles.length === 1 ? roles[0] : roles,
  };
  return `${b64url({ alg: 'HS256', typ: 'JWT' })}.${b64url(payload)}.sig`;
}

describe('Profile', () => {
  let httpMock: HttpTestingController;
  let auth: AuthService;
  const profileUrl = `${environment.apiBaseUrl}/api/Auth/profile`;

  beforeEach(async () => {
    sessionStorage.clear();
    sessionStorage.setItem('userId', 'miles@uuu.com.tw');
    sessionStorage.setItem('userName', 'Miles Sun');
    sessionStorage.setItem('accessToken', makeJwt(['Admin', 'User']));

    await TestBed.configureTestingModule({
      imports: [Profile],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideNoopAnimations()],
    }).compileComponents();

    httpMock = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('shows UserId and roles read-only and UserName editable', () => {
    const fixture = TestBed.createComponent(Profile);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    // UserId shown in a disabled/read-only input (not editable).
    const userIdInput = el.querySelector('input.readonly') as HTMLInputElement;
    expect(userIdInput).toBeTruthy();
    expect(userIdInput.value).toBe('miles@uuu.com.tw');
    expect(userIdInput.disabled).toBe(true);

    // Roles shown read-only (no form control for them).
    const roles = el.querySelector('[data-testid="roles"]') as HTMLElement;
    expect(roles.textContent).toContain('Admin');
    expect(roles.textContent).toContain('User');

    // UserName is an editable control seeded from the current name.
    const userNameInput = el.querySelector('#userName') as HTMLInputElement;
    expect(userNameInput).toBeTruthy();
    expect(userNameInput.disabled).toBe(false);
    expect(userNameInput.value).toBe('Miles Sun');
  });

  it('saving a new UserName PUTs to /api/Auth/profile and refreshes the shell', () => {
    const fixture = TestBed.createComponent(Profile);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;

    const userNameInput = el.querySelector('#userName') as HTMLInputElement;
    userNameInput.value = 'New Display Name';
    userNameInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    (el.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

    const req = httpMock.expectOne(profileUrl);
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ userName: 'New Display Name' });
    req.flush({ userId: 'miles@uuu.com.tw', userName: 'New Display Name' });

    // The shell reads userName from the service signal — it must now reflect the new name.
    expect(auth.userName()).toBe('New Display Name');
    expect(sessionStorage.getItem('userName')).toBe('New Display Name');
  });

  // ---- Change password (client-side validation + submit) ----

  it('marks the change-password form invalid for a weak new password', () => {
    const fixture = TestBed.createComponent(Profile);
    fixture.detectChanges();
    const form = (fixture.componentInstance as any).passwordForm;

    form.setValue({ currentPassword: 'CMS4fun#', newPassword: 'weak', confirmPassword: 'weak' });

    expect(form.controls.newPassword.hasError('complexity')).toBe(true);
    expect(form.invalid).toBe(true);
  });

  it('marks the change-password form invalid when new and confirm differ', () => {
    const fixture = TestBed.createComponent(Profile);
    fixture.detectChanges();
    const form = (fixture.componentInstance as any).passwordForm;

    form.setValue({
      currentPassword: 'CMS4fun#',
      newPassword: 'Str0ng!Pwd',
      confirmPassword: 'Other1!Pwd',
    });

    expect(form.hasError('mismatch')).toBe(true);
    expect(form.invalid).toBe(true);
  });

  it('does not call the API when the change-password form is invalid', () => {
    const fixture = TestBed.createComponent(Profile);
    fixture.detectChanges();
    (fixture.componentInstance as any).changePassword();
    httpMock.expectNone(`${environment.apiBaseUrl}/api/Auth/change-password`);
  });

  it('POSTs current/new/confirm when the password change is valid', () => {
    const fixture = TestBed.createComponent(Profile);
    fixture.detectChanges();
    const component = fixture.componentInstance as any;

    component.passwordForm.setValue({
      currentPassword: 'CMS4fun#',
      newPassword: 'Str0ng!Pwd',
      confirmPassword: 'Str0ng!Pwd',
    });
    expect(component.passwordForm.valid).toBe(true);

    component.changePassword();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/Auth/change-password`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      currentPassword: 'CMS4fun#',
      newPassword: 'Str0ng!Pwd',
      confirmPassword: 'Str0ng!Pwd',
    });
    req.flush(null);
  });
});
