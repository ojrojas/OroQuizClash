import { Component, OnInit, inject } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { Router } from '@angular/router';

@Component({
  selector: 'app-logout-callback',
  standalone: true,
  template: `
    <div style="padding:1.5rem; text-align:center;">
      <p role="status" aria-live="polite">Sesión cerrada correctamente.</p>
      <p style="opacity:0.7; font-size:0.9rem;">IdentityServer + Api + App limpiadas.</p>
      <p><a routerLink="/">Volver al inicio</a> · <a (click)="login()" style="cursor:pointer; text-decoration:underline;">Iniciar sesión de nuevo</a></p>
      @if (error) { <p role="alert" style="color: var(--color-destructive-text, #DC2626);">{{ error }}</p> }
    </div>
  `,
})
export class LogoutCallbackComponent implements OnInit {
  private oidc = inject(OidcSecurityService);
  private router = inject(Router);
  error = '';

  ngOnInit(): void {
    // Logout integral: App (session/local) + Api (tokens ya revocados en AuthService) + IdentityServer (end_session ya visitado)
    // Aquí solo garantizamos que no quede sesión local residual.
    try { sessionStorage.clear(); } catch {}
    try { localStorage.clear(); } catch {}
    // Limpiar posibles estados de juego
    try { sessionStorage.removeItem('idemp-withdraw'); } catch {}
    this.oidc.checkAuth().subscribe({
      next: ({ isAuthenticated }) => {
        if (isAuthenticated) {
          console.warn('[logout-callback] aún autenticado -> forzar logoffLocal');
          try { (this.oidc as any).logoffLocal?.(); } catch {}
          try { sessionStorage.clear(); localStorage.clear(); } catch {}
        }
        // Navegar al inicio donde se ve "Iniciar sesión" (estado deslogueado en los 3 layers)
        setTimeout(() => this.router.navigateByUrl('/'), 600);
      },
      error: (e) => {
        console.warn('[logout-callback] checkAuth error', e);
        try { (this.oidc as any).logoffLocal?.(); } catch {}
        this.router.navigateByUrl('/');
      },
    });
    // Fallback si checkAuth no emite (IdP caído)
    setTimeout(() => {
      if (window.location.pathname.includes('/auth/logout-callback')) this.router.navigateByUrl('/');
    }, 2500);
  }

  login() { (this.oidc as any).authorize?.(); }
}
