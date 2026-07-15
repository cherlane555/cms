import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { environment } from '@env';
import { AuthService } from './auth.service';

const LOGIN_PATH = '/api/Auth/login';

/**
 * Attaches `Authorization: Bearer <token>` to outgoing API requests, and on a 401
 * clears the session and bounces the user back to the login page.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const token = auth.token;
  const isApiCall = req.url.startsWith(environment.apiBaseUrl);
  const authReq =
    token && isApiCall
      ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : req;

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !req.url.includes(LOGIN_PATH)) {
        auth.clear();
        void router.navigate(['/login']);
      }
      return throwError(() => error);
    }),
  );
};
