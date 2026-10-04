import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { tap } from 'rxjs';

import { isUnreachable } from './api-errors';

/**
 * Logs connection problems and server failures to the console (M1-60), so they show up in the desktop app's
 * dev tools. Validation errors (400) are normal and aren't logged.
 */
export const apiErrorLoggingInterceptor: HttpInterceptorFn = (request, next) =>
  next(request).pipe(
    tap({
      error: (error: unknown) => {
        if (!(error instanceof HttpErrorResponse)) return;
        if (isUnreachable(error)) {
          console.error(`[api] Can't reach the local service: ${request.method} ${request.url} (${error.status})`);
        } else if (error.status >= 500) {
          console.error(`[api] Server error: ${request.method} ${request.url} (${error.status})`, error.error);
        }
      },
    }),
  );
