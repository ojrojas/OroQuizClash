import { Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { take } from 'rxjs';

@Component({
  selector: 'app-callback',
  standalone: true,
  template: `
    <p>Autenticando...</p>
    <p style="font-size:0.8rem; opacity:0.6;">Si quedas aquí más de 3s, abre F12 → Console → [callback]</p>
  `,
})
export class CallbackComponent implements OnInit {
  private oidc = inject(OidcSecurityService);
  private router = inject(Router);
  ngOnInit() {
    console.log('[callback] ngOnInit, url', window.location.href);
    // Solo un checkAuth; AppComponent ya hizo uno global, pero este asegura el ?code= de este navigation
    // Si AppComponent ya consumió el code, este segundo checkAuth simplemente restaurará sesión
    this.oidc.checkAuth().subscribe({
      next: ({ isAuthenticated, userData }) => {
        console.log('[callback] checkAuth result', isAuthenticated, userData);
        // Dar tiempo a que isAuthenticated$ se propague
        setTimeout(() => {
          this.router.navigateByUrl(isAuthenticated ? '/lobby' : '/');
        }, 300);
      },
      error: (err) => {
        console.error('[callback] checkAuth error', err);
        this.router.navigateByUrl('/');
      }
    });

    // Fallback por si checkAuth no emite (ej. storage bloqueado)
    setTimeout(() => {
      console.warn('[callback] fallback timeout, checking isAuthenticated$');
      this.oidc.isAuthenticated$.pipe(take(1)).subscribe(({ isAuthenticated }) => {
        console.log('[callback] fallback isAuthenticated', isAuthenticated);
        if (window.location.pathname.includes('/auth/callback')) {
          this.router.navigateByUrl(isAuthenticated ? '/lobby' : '/');
        }
      });
    }, 4000);
  }
}
