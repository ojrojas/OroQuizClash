import { Injectable, inject } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private oidc = inject(OidcSecurityService);

  isAuthenticated$ = this.oidc.isAuthenticated$;
  userData$ = this.oidc.userData$;

  login() { this.oidc.authorize(); }

  /**
   * Cierra sesión de forma robusta:
   * 1) intenta `logoffAndRevokeTokens` (revoca access/refresh en IdP y limpia storage)
   * 2) si el IdP no tiene `post_logout_redirect_uri` registrado o hay error de red/CORS,
   *    hace fallback a `logoffLocal` (limpieza local + redirect a postLogoutRedirectUri)
   *    de modo que el botón nunca queda "sin hacer nada".
   */
  logout(): void {
    // Limpiar posibles estados colgados de signalR/idempotencia
    try { sessionStorage.clear(); } catch {}
    const doLocalFallback = () => {
      try {
        (this.oidc as any).logoffLocal?.();
      } catch {}
      // Fallback final: forzar navegación a logout-callback aunque la lib no redirija
      const fallbackUrl = window.location.origin + '/auth/logout-callback';
      setTimeout(() => {
        if (!window.location.pathname.includes('/auth/logout-callback')) {
          window.location.href = fallbackUrl;
        }
      }, 400);
    };

    try {
      const obs: any = (this.oidc as any).logoffAndRevokeTokens?.();
      if (obs && typeof obs.subscribe === 'function') {
        obs.subscribe({
          next: () => {},
          error: () => doLocalFallback(),
        });
        // Si en 1.5s no hubo navegación (ej. post_logout_redirect_uri no registrado), hacer fallback local
        setTimeout(() => {
          if (!window.location.pathname.includes('/auth/logout-callback') && window.location.pathname !== '/auth/callback') {
            // aún en la misma página -> forzar local
            // no sobreescribir si ya estamos navegando al IdP (href cambió)
            if (window.location.href.includes(window.location.origin)) doLocalFallback();
          }
        }, 1500);
        return;
      }
    } catch {}
    try { this.oidc.logoff(); } catch { doLocalFallback(); }
    // fallback por si `logoff()` no redirige (ej. IdP sin end_session_endpoint o post_logout mal registrado)
    setTimeout(() => {
      if (window.location.pathname === '/' || window.location.pathname.startsWith('/player')) doLocalFallback();
    }, 1500);
  }

  logoffLocal(): void {
    try { (this.oidc as any).logoffLocal?.(); } catch {}
    try { sessionStorage.clear(); } catch {}
    window.location.href = window.location.origin + '/auth/logout-callback';
  }

  getAccessToken(): import('rxjs').Observable<string> { return this.oidc.getAccessToken(); }
  getPayload(): import('rxjs').Observable<any> { return this.oidc.getPayloadFromIdToken(); }
}
