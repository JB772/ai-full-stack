import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/** Shown when a 5xx response carries no usable message of its own. */
const FALLBACK_ERROR_MESSAGE = 'An unexpected error occurred.';

/**
 * Attaches `Authorization: Bearer <token>` to every outgoing request when a token is present, and
 * centralises HTTP error handling:
 *  - 401 clears the session and sends the user back to the login page (unchanged);
 *  - a 500-class error surfaces a friendly toast built from the safe message in the response body;
 *  - every other status (e.g. validation 400) is left for the caller to handle on the form.
 * The error is always re-thrown so component-level handling still runs.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const messages = inject(MessageService);

  const token = auth.token;
  const authorized = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(authorized).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        auth.clearSession();
        router.navigate(['/login']);
      } else if (error.status >= 500) {
        const safeMessage =
          typeof error.error?.message === 'string' && error.error.message.trim()
            ? error.error.message
            : FALLBACK_ERROR_MESSAGE;
        messages.add({
          severity: 'error',
          summary: '系統錯誤 Error',
          detail: safeMessage,
          life: 5000
        });
      }
      return throwError(() => error);
    })
  );
};
