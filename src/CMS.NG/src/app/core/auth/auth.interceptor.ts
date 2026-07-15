import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { catchError, throwError } from 'rxjs';
import { environment } from '@env';
import { AuthService } from './auth.service';

const LOGIN_PATH = '/api/Auth/login';

/** Fallback when a 500-class response carries no safe message body. */
const GENERIC_SERVER_ERROR = '系統發生錯誤，請稍後再試。An unexpected error occurred.';

/**
 * Attaches `Authorization: Bearer <token>` to outgoing API requests; on a 401 clears the
 * session and bounces the user back to the login page; on a 500-class error shows a
 * friendly global toast with the safe message from the response body.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const messages = inject(MessageService);

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
      } else if (error.status >= 500) {
        // The backend's exception middleware guarantees a safe { message } body on 500.
        const body = error.error as { message?: unknown } | null;
        const detail =
          typeof body?.message === 'string' && body.message ? body.message : GENERIC_SERVER_ERROR;
        messages.add({ severity: 'error', summary: '系統錯誤 Server Error', detail });
      }
      return throwError(() => error);
    }),
  );
};
