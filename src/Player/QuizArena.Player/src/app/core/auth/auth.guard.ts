import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { map } from 'rxjs';

export const authGuard: CanActivateFn = () => {
  const oidc = inject(OidcSecurityService);
  const router = inject(Router);
  return oidc.isAuthenticated$.pipe(
    map(({ isAuthenticated }) => isAuthenticated ? true : router.createUrlTree(['/auth/callback']))
  );
};

export const mustChangePasswordGuard: CanActivateFn = () => {
  const oidc = inject(OidcSecurityService);
  const router = inject(Router);
  const payload = oidc.getPayloadFromIdToken() as any;
  if (payload?.must_change_password) {
    window.location.href = `${(payload as any).iss ?? ''}/auth/change-password`;
    return false;
  }
  return true;
};
