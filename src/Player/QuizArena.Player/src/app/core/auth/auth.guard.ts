import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { map, take, tap } from 'rxjs';

let authorizeTriggered = false;

export const authGuard: CanActivateFn = () => {
  const oidc = inject(OidcSecurityService);
  const router = inject(Router);

  // Evitar bucle si ya estamos en callback/logout-callback
  if (router.url.includes('/auth/callback') || router.url.includes('/auth/logout-callback')) {
    return true;
  }

  return oidc.isAuthenticated$.pipe(
    take(1),
    tap(({ isAuthenticated }) => {
      console.log('[authGuard] isAuthenticated', isAuthenticated, 'url', router.url, 'authorizeTriggered', authorizeTriggered);
      if (!isAuthenticated && !authorizeTriggered) {
        authorizeTriggered = true;
        // Pequeño delay para evitar múltiples authorize en la misma navegación
        setTimeout(() => {
          console.log('[authGuard] calling authorize()');
          oidc.authorize();
          // Resetear flag después de 5s por si el flujo falla y el usuario vuelve
          setTimeout(() => (authorizeTriggered = false), 5000);
        }, 100);
      }
    }),
    map(({ isAuthenticated }) => isAuthenticated)
  );
};

export const mustChangePasswordGuard: CanActivateFn = () => {
  const oidc = inject(OidcSecurityService);
  return oidc.getPayloadFromIdToken().pipe(
    take(1),
    map((payload: unknown) => {
      const p = payload as Record<string, unknown> | null;
      if (p?.['must_change_password']) {
        const iss = (p?.['iss'] as string) ?? 'https://localhost:5086';
        window.location.href = `${iss}/auth/change-password`;
        return false;
      }
      return true;
    })
  );
};

// Guard real de must_change_password se evalúa vía payload, pero ahora de forma segura
export const mustChangePasswordGuardStrict: CanActivateFn = () => {
  const oidc = inject(OidcSecurityService);
  return oidc.getPayloadFromIdToken().pipe(
    take(1),
    map((payload: any) => {
      console.log('[mustChangePasswordGuard] payload', payload);
      if (payload?.must_change_password) {
        const iss = payload?.iss ?? 'https://localhost:5086';
        console.warn('[mustChangePasswordGuard] redirecting to change-password');
        window.location.href = `${iss}/auth/change-password`;
        return false;
      }
      return true;
    })
  );
};
