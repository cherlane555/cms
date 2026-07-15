import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { environment } from '@env';
import { AppUserEdit } from './app-user-edit';

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

function signIn(roles: string[]): void {
  sessionStorage.setItem('accessToken', makeJwt(roles));
  sessionStorage.setItem('userName', 'Tester');
}

async function setup() {
  await TestBed.configureTestingModule({
    imports: [AppUserEdit],
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideNoopAnimations(),
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: convertToParamMap({ id: 'someone@else.com' }) } },
      },
    ],
  }).compileComponents();

  const httpMock = TestBed.inject(HttpTestingController);
  const fixture = TestBed.createComponent(AppUserEdit);
  fixture.detectChanges();

  // Component resolves the display name from the AppUser lookup on init.
  httpMock.expectOne(`${environment.apiBaseUrl}/api/lookups/app-users`).flush([
    { userId: 'someone@else.com', userName: 'Someone Else', label: 'Someone Else (someone@else.com)' },
  ]);
  fixture.detectChanges();

  return { fixture, httpMock };
}

describe('AppUserEdit', () => {
  beforeEach(() => sessionStorage.clear());
  afterEach(() => {
    sessionStorage.clear();
    TestBed.inject(HttpTestingController).verify();
  });

  it('shows the Reset Password button for an Admin user', async () => {
    signIn(['Admin', 'User']);
    const { fixture } = await setup();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('[data-testid="reset-block"]')).not.toBeNull();
    expect(el.textContent).toContain('重設密碼為預設值');
  });

  it('hides the Reset Password button for a non-Admin user', async () => {
    signIn(['User']);
    const { fixture } = await setup();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('[data-testid="reset-block"]')).toBeNull();
    expect(el.textContent).not.toContain('重設密碼為預設值');
  });
});
