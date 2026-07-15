import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { environment } from '@env';
import { AuthService } from './auth.service';

/** Build an unsigned JWT whose payload carries the given roles (as the API emits them). */
function makeJwt(roles: string[]): string {
  const b64url = (obj: unknown) =>
    btoa(JSON.stringify(obj)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  const payload = {
    userId: 'miles@uuu.com.tw',
    userName: 'Miles Sun',
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role':
      roles.length === 1 ? roles[0] : roles,
  };
  return `${b64url({ alg: 'HS256', typ: 'JWT' })}.${b64url(payload)}.sig`;
}

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  const loginUrl = `${environment.apiBaseUrl}/api/Auth/login`;

  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [AuthService, provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('login POSTs credentials and stores the profile in session storage', () => {
    const token = makeJwt(['Admin', 'User']);
    service.login('miles@uuu.com.tw', 'CMS4fun#').subscribe();

    const req = httpMock.expectOne(loginUrl);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ userId: 'miles@uuu.com.tw', password: 'CMS4fun#' });
    req.flush({ userId: 'miles@uuu.com.tw', userName: 'Miles Sun', accessToken: token });

    expect(sessionStorage.getItem('userId')).toBe('miles@uuu.com.tw');
    expect(sessionStorage.getItem('userName')).toBe('Miles Sun');
    expect(sessionStorage.getItem('accessToken')).toBe(token);
    expect(service.isAuthenticated()).toBe(true);
    expect(service.userName()).toBe('Miles Sun');
    expect(service.isAdmin()).toBe(true);
  });

  it('does not grant admin for a non-Admin token', () => {
    service.login('reg', 'pw').subscribe();
    httpMock
      .expectOne(loginUrl)
      .flush({ userId: 'reg', userName: 'Reg User', accessToken: makeJwt(['User']) });

    expect(service.isAuthenticated()).toBe(true);
    expect(service.isAdmin()).toBe(false);
    expect(service.roles()).toEqual(['User']);
  });

  it('clear removes stored auth and resets state', () => {
    service.login('miles@uuu.com.tw', 'CMS4fun#').subscribe();
    httpMock
      .expectOne(loginUrl)
      .flush({ userId: 'miles@uuu.com.tw', userName: 'Miles Sun', accessToken: makeJwt(['Admin']) });

    service.clear();

    expect(sessionStorage.getItem('accessToken')).toBeNull();
    expect(sessionStorage.getItem('userId')).toBeNull();
    expect(sessionStorage.getItem('userName')).toBeNull();
    expect(service.isAuthenticated()).toBe(false);
    expect(service.token).toBeNull();
  });
});
