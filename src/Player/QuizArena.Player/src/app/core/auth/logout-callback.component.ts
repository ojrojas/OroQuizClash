import { Component, OnInit, inject } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { Router } from '@angular/router';

@Component({
  selector: 'app-logout-callback',
  standalone: true,
  template: `
    <div style="padding:1.5rem; text-align:center;">
      <p role="status" aria-live="polite">Sesión cerrada.</p>
      <p><a href="/">Volver al inicio</a></p>
      @if (error) { <p role="alert" style="color: var(--color-destructive-text, #DC2626);">{{ error }}</p> }
    </div>
  `,
})
export class LogoutCallbackComponent implements OnInit {
  private oidc = inject(OidcSecurityService);
  private router = inject(Router);
  error = '';

  ngOnInit(): void {
    // Asegurar limpieza local aunque el IdP no haya redirigido con `logoffAndRevokeTokens`
    try { sessionStorage.clear(); } catch {}
    this.oidc.checkAuth().subscribe({
      next: ({ isAuthenticated }) => {
        if (isAuthenticated) {
          // Aún hay sesión local -> forzar logoffLocal
          try { (this.oidc as any).logoffLocal?.(); } catch {}
          try { sessionStorage.clear(); } catch {}
        }
        // Dar feedback y volver al lobby (que mostrará "Iniciar sesión")
        setTimeout(() => this.router.navigateByUrl('/'), 600);
      },
      error: () => {
        try { (this.oidc as any).logoffLocal?.(); } catch {}
        this.router.navigateByUrl('/');
      },
    });
    // Fallback si checkAuth no emite
    setTimeout(() => {
      if (window.location.pathname.includes('/auth/logout-callback')) this.router.navigateByUrl('/');
    }, 2500);
  }
}
