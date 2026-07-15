import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { environment } from '@env';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let messageSpy: jasmine.SpyObj<MessageService>;
  const navigate = jasmine.createSpy('navigate');
  const url = `${environment.apiBaseUrl}/api/app-roles`;

  beforeEach(() => {
    sessionStorage.clear();
    navigate.calls.reset();
    messageSpy = jasmine.createSpyObj('MessageService', ['add']);
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: Router, useValue: { navigate } },
        { provide: MessageService, useValue: messageSpy },
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
    expect(messageSpy.add).not.toHaveBeenCalled(); // 401 redirects, it does not toast
  });

  it('shows a friendly toast with the safe body message on a 500', () => {
    http.get(url).subscribe({ next: () => {}, error: () => {} });

    httpMock
      .expectOne(url)
      .flush({ message: 'An unexpected error occurred.' }, { status: 500, statusText: 'Server Error' });

    expect(messageSpy.add).toHaveBeenCalledWith(
      jasmine.objectContaining({ severity: 'error', detail: 'An unexpected error occurred.' }),
    );
    expect(navigate).not.toHaveBeenCalled(); // no login redirect on 500
  });

  it('falls back to a generic toast message when the 500 has no body message', () => {
    http.get(url).subscribe({ next: () => {}, error: () => {} });

    httpMock.expectOne(url).flush(null, { status: 503, statusText: 'Service Unavailable' });

    expect(messageSpy.add).toHaveBeenCalledWith(
      jasmine.objectContaining({
        severity: 'error',
        detail: '系統發生錯誤，請稍後再試。An unexpected error occurred.',
      }),
    );
  });

  it('does not toast on validation (400) errors — the form handles those', () => {
    http.get(url).subscribe({ next: () => {}, error: () => {} });

    httpMock
      .expectOne(url)
      .flush({ errors: { Pkid: ['required'] } }, { status: 400, statusText: 'Bad Request' });

    expect(messageSpy.add).not.toHaveBeenCalled();
    expect(navigate).not.toHaveBeenCalled();
  });
});
