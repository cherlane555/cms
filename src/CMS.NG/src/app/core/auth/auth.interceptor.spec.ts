import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { environment } from '@env';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  const navigate = jasmine.createSpy('navigate');
  const url = `${environment.apiBaseUrl}/api/app-roles`;

  beforeEach(() => {
    sessionStorage.clear();
    navigate.calls.reset();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: Router, useValue: { navigate } },
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    sessionStorage.clear();
  });

  it('attaches Authorization: Bearer when a token is in session storage', () => {
    sessionStorage.setItem('accessToken', 'tok123');

    http.get(url).subscribe();

    const req = httpMock.expectOne(url);
    expect(req.request.headers.get('Authorization')).toBe('Bearer tok123');
    req.flush([]);
  });

  it('sends no Authorization header when there is no token', () => {
    http.get(url).subscribe();

    const req = httpMock.expectOne(url);
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush([]);
  });

  it('clears session storage and redirects to /login on 401', () => {
    sessionStorage.setItem('accessToken', 'tok123');
    sessionStorage.setItem('userName', 'Miles Sun');

    http.get(url).subscribe({ next: () => {}, error: () => {} });

    httpMock.expectOne(url).flush('nope', { status: 401, statusText: 'Unauthorized' });

    expect(sessionStorage.getItem('accessToken')).toBeNull();
    expect(sessionStorage.getItem('userName')).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });
});
