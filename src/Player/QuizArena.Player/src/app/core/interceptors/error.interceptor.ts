import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { catchError, throwError } from 'rxjs';

export interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  detail: string;
  code: string;
  traceId: string;
  correlationId: string;
}

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const oidc = inject(OidcSecurityService);
  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401) {
        // attempt silent renew
        oidc.authorize();
      }
      if (err.status === 429) {
        console.warn('[RateLimit]', err.headers.get('Retry-After'));
      }
      const problem: ProblemDetails = err.error ?? {
        title: err.message,
        status: err.status,
        traceId: err.headers.get('traceId') ?? '',
        correlationId: err.headers.get('X-Correlation-Id') ?? '',
      } as ProblemDetails;
      // audit traceId/correlationId already propagated via headers
      return throwError(() => problem);
    })
  );
};
