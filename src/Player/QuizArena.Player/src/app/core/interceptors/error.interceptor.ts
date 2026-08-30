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

let last401 = 0;

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const oidc = inject(OidcSecurityService);
  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401) {
        const now = Date.now();
        if (now - last401 > 2000) {
          last401 = now;
          try { oidc.authorize(); } catch {}
        }
      }
      if (err.status === 429) {
        const retry = err.headers.get('Retry-After');
        // propagate but keep header for UI
      }
      const problem: ProblemDetails = (err.error && typeof err.error === 'object' && (err.error as ProblemDetails).title ? (err.error as ProblemDetails) : {
        type: (err.error as ProblemDetails)?.type ?? 'about:blank',
        title: (err.error as ProblemDetails)?.title ?? err.error?.title ?? err.message ?? 'Error',
        status: err.status,
        detail: (err.error as ProblemDetails)?.detail ?? err.error?.detail ?? err.message ?? 'Ha ocurrido un error',
        code: (err.error as ProblemDetails)?.code ?? (err.error as { code?: string })?.code ?? String(err.status),
        traceId: (err.error as ProblemDetails)?.traceId ?? err.headers.get('traceId') ?? err.headers.get('TraceId') ?? '',
        correlationId: (err.error as ProblemDetails)?.correlationId ?? err.headers.get('X-Correlation-Id') ?? err.headers.get('x-correlation-id') ?? '',
      }) as ProblemDetails;
      // ensure correlationId/traceId from headers if missing
      if (!problem.correlationId) problem.correlationId = err.headers.get('X-Correlation-Id') ?? '';
      if (!problem.traceId) problem.traceId = err.headers.get('traceId') ?? '';
      return throwError(() => problem);
    })
  );
};
