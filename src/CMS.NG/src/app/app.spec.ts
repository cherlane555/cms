import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { App } from './app';

/** Unsigned JWT carrying the given roles, as the API emits them. */
function makeJwt(roles: string[]): string {
  const b64url = (obj: unknown) =>
    btoa(JSON.stringify(obj)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  const payload = {
    userName: 'Miles Sun',
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role':
      roles.length === 1 ? roles[0] : roles,
  };
  return `${b64url({ alg: 'HS256', typ: 'JWT' })}.${b64url(payload)}.sig`;
}

function signIn(roles: string[], userName = 'Miles Sun'): void {
  sessionStorage.setItem('accessToken', makeJwt(roles));
  sessionStorage.setItem('userName', userName);
}

describe('App', () => {
  beforeEach(async () => {
    sessionStorage.clear();
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
      ],
    }).compileComponents();
  });

  afterEach(() => sessionStorage.clear());

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders only the router-outlet (no sidebar) when signed out', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.sidebar')).toBeNull();
  });

  it('shows the shell, the signed-in user, and the Admin menu for an Admin user', () => {
    signIn(['Admin', 'User']);
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.brand')?.textContent).toContain('UWA');
    expect(el.textContent).toContain('Miles Sun');
    expect(el.textContent).toContain('系統管理 Admin');
    expect(el.textContent).toContain('角色 AppRole');
  });

  it('hides the Admin menu for a non-Admin user but still shows the shell', () => {
    signIn(['User'], 'Reg User');
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('課程管理 Course');
    expect(el.textContent).not.toContain('系統管理 Admin');
  });
});
