import { Injectable, inject } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private oidc = inject(OidcSecurityService);

  isAuthenticated$ = this.oidc.isAuthenticated$;
  userData$ = this.oidc.userData$;

  login() { this.oidc.authorize(); }

  /**
   * Logout integral: limpia App (storage + SignalR) + Api (revocación tokens)
   * + IdentityServer (end_session). Garantiza que el botón siempre navegue,
   * incluso si el IdP no tiene post_logout_redirect_uri registrado.
   */
  logout(): void {
    // 1) App: limpiar storage de juego/idempotencia + desconectar SignalR si existe
    try { sessionStorage.clear(); } catch {}
    try { localStorage.removeItem('game-cache'); } catch {}
    // Intentar desconectar SignalR global si está activo (evita reconexión tras logout)
    try {
      const anyWindow = window as any;
      if (anyWindow.__gameRealtimeDisconnect) anyWindow.__gameRealtimeDisconnect();
    } catch {}

    // 2) Api es stateless (JWT) pero revocamos refresh/access en IdP
    //    y limpiamos cualquier token en memoria del OIDC client
    const doLocalCleanup = () => {
      try { (this.oidc as any).logoffLocal?.(); } catch {}
      try { sessionStorage.clear(); localStorage.clear(); } catch {}
    };

    const doFallbackRedirect = (reason: string) => {
      doLocalCleanup();
      console.warn(`[Auth] logout fallback (${reason}) -> /auth/logout-callback`);
      const fallbackUrl = window.location.origin + '/auth/logout-callback';
      if (!window.location.pathname.includes('/auth/logout-callback')) {
        // Pequeño delay para dar tiempo a que logoffLocal limpie
        setTimeout(() => (window.location.href = fallbackUrl), 200);
      }
    };

    // Intentar obtener id_token para id_token_hint (mejora logout en IdentityServer)
    let idToken: string | null = null;
    try {
      // angular-auth-oidc-client expone getIdToken como Observable
      (this.oidc as any).getIdToken?.().subscribe?.((t: string) => (idToken = t));
    } catch {}

    // 3) IdentityServer: end_session + revocación
    // Primero intentar el flujo de la librería (revoca y hace redirect a end_session)
    try {
      const obs: any = (this.oidc as any).logoffAndRevokeTokens?.();
      if (obs && typeof obs.subscribe === 'function') {
        console.log('[Auth] logoffAndRevokeTokens -> IdP');
        obs.subscribe({
          next: () => {
            console.log('[Auth] logoffAndRevokeTokens success');
            doLocalCleanup();
          },
          error: (e: any) => {
            console.warn('[Auth] logoffAndRevokeTokens error', e);
            // Fallback manual a end_session si la librería falla
            this.manualIdpLogout(idToken);
          },
        });
        // Si en 1.8s no hubo navegación, forzar manual
        setTimeout(() => {
          if (window.location.pathname.startsWith('/player') || window.location.pathname === '/') {
            if (window.location.href.includes(window.location.origin) && !window.location.href.includes('connect/logout')) {
              console.warn('[Auth] logoffAndRevokeTokens no navegó -> manual');
              this.manualIdpLogout(idToken);
            }
          }
        }, 1800);
        return;
      }
    } catch (e) {
      console.warn('[Auth] logoffAndRevokeTokens threw', e);
    }

    try {
      console.log('[Auth] logoff() -> IdP end_session');
      this.oidc.logoff();
      setTimeout(() => {
        if (!window.location.href.includes('connect/logout') && !window.location.pathname.includes('/auth/logout-callback')) {
          this.manualIdpLogout(idToken);
        }
      }, 1500);
      return;
    } catch (e) {
      console.warn('[Auth] logoff() threw', e);
    }
    doFallbackRedirect('exception');
  }

  /** Fallback manual: navega directo a IdP /connect/logout con id_token_hint */
  private manualIdpLogout(idToken: string | null) {
    try {
      const postLogout = encodeURIComponent(window.location.origin + '/auth/logout-callback');
      const authority = (environment as any).identityAuthority?.replace(/\/$/, '') ?? 'https://localhost:5086';
      // Intentar revocar refresh token manualmente antes de salir
      try { (this.oidc as any).revokeRefreshToken?.().subscribe?.(() => {}); } catch {}
      try { (this.oidc as any).revokeAccessToken?.().subscribe?.(() => {}); } catch {}
      // Limpiar local antes de salir
      try { (this.oidc as any).logoffLocal?.(); } catch {}
      try { sessionStorage.clear(); } catch {}
      let url = `${authority}/connect/logout?post_logout_redirect_uri=${postLogout}`;
      if (idToken) url += `&id_token_hint=${encodeURIComponent(idToken)}`;
      console.log('[Auth] manualIdpLogout ->', url);
      window.location.href = url;
      // Si el IdP no responde (cert/CORS), el navegador mostrará error; fallback a local en 2s
      setTimeout(() => {
        if (!window.location.pathname.includes('/auth/logout-callback')) {
          window.location.href = window.location.origin + '/auth/logout-callback';
        }
      }, 2000);
    } catch {
      window.location.href = window.location.origin + '/auth/logout-callback';
    }
  }

  logoffLocal(): void {
    try { (this.oidc as any).logoffLocal?.(); } catch {}
    try { sessionStorage.clear(); localStorage.clear(); } catch {}
    window.location.href = window.location.origin + '/auth/logout-callback';
  }

  getAccessToken(): import('rxjs').Observable<string> { return this.oidc.getAccessToken(); }
  getPayload(): import('rxjs').Observable<any> { return this.oidc.getPayloadFromIdToken(); }
}
