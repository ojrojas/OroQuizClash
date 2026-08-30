import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { AuthService } from './core/auth/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet],
  template: `
    <div data-theme="player" class="app-shell">
      <header class="app-header">
        <h1>QuizArena Player</h1>
        <nav class="auth-nav" aria-label="Autenticación">
          @if (isAuthenticated) {
            <span class="user-info" aria-live="polite">
              {{ userName }} ({{ userEmail }})
            </span>
            <button type="button" (click)="logout()" class="btn btn-secondary" aria-label="Cerrar sesión" style="min-height:44px; min-width:44px;">
              Cerrar sesión
            </button>
          } @else {
            <button type="button" (click)="login()" class="btn btn-primary" aria-label="Iniciar sesión" style="min-height:44px; min-width:44px;">
              Iniciar sesión
            </button>
          }
        </nav>
      </header>
      <main>
        @if (isCheckingAuth) {
          <p role="status" aria-live="polite" style="padding:1rem;">Verificando sesión...</p>
        }
        <router-outlet />
        @if (!isCheckingAuth && !isAuthenticated) {
          <p style="padding:1rem; text-align:center; opacity:0.7;">No autenticado — usa “Iniciar sesión” arriba. Si quedas en blanco, abre la consola (F12) y revisa [Auth] logs. Authority: {{authority}}</p>
        }
      </main>
    </div>
  `,
  styles: [`
    .app-shell { min-height: 100vh; display:flex; flex-direction:column; background: var(--color-background, #0F172A); color: var(--color-foreground, #F8FAFC); }
    .app-header { padding: var(--space-4, 1rem); background: var(--color-primary, #2563EB); color: white; display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap; gap:0.5rem; }
    .auth-nav { display:flex; align-items:center; gap:0.75rem; }
    .user-info { font-size:0.9rem; opacity:0.9; color: white; }
    .btn { padding:0.5rem 1rem; border-radius:var(--radius-md,8px); border:none; cursor:pointer; font-weight:600; }
    .btn-primary { background:white; color:var(--color-primary, #2563EB); }
    .btn-secondary { background:transparent; color:white; border:1px solid white; }
    main { flex:1; padding: var(--space-4, 1rem); }
  `]
})
export class AppComponent implements OnInit {
  private oidc = inject(OidcSecurityService);
  private auth = inject(AuthService);

  isAuthenticated = false;
  isCheckingAuth = true;
  userName = '';
  userEmail = '';
  authority = 'https://localhost:5086';

  ngOnInit(): void {
    // No ejecutar checkAuth doble en callback — el CallbackComponent ya lo hace y consumiría el code dos veces (400)
    if (window.location.pathname.includes('/auth/callback') || window.location.pathname.includes('/auth/logout-callback')) {
      this.isCheckingAuth = false;
      // Aun así suscribirse a isAuthenticated$ para reflejar estado si ya hay sesión
      this.oidc.isAuthenticated$.subscribe(({ isAuthenticated }) => {
        this.isAuthenticated = isAuthenticated;
        if (!isAuthenticated) {
          this.userName = '';
          this.userEmail = '';
        }
      });
      this.oidc.userData$.subscribe((userData: any) => {
        if (userData) {
          this.userName = userData?.name ?? userData?.preferred_username ?? '';
          this.userEmail = userData?.email ?? '';
        }
      });
      return;
    }
    // Timeout de seguridad: si checkAuth no responde (CORS/cert), libera la UI a los 3s
    const fallback = setTimeout(() => {
      if (this.isCheckingAuth) {
        console.warn('[Auth] checkAuth timeout - mostrando login');
        this.isCheckingAuth = false;
      }
    }, 3000);

    this.oidc.checkAuth().subscribe({
      next: ({ isAuthenticated, userData }) => {
        clearTimeout(fallback);
        this.isAuthenticated = isAuthenticated;
        this.isCheckingAuth = false;
        console.log('[Auth] checkAuth', isAuthenticated, userData);
        if (isAuthenticated && userData) {
          const payload: any = userData;
          this.userName = payload?.name ?? payload?.preferred_username ?? '';
          this.userEmail = payload?.email ?? '';
        }
      },
      error: (err) => {
        clearTimeout(fallback);
        console.error('[Auth] checkAuth error', err);
        this.isCheckingAuth = false;
      }
    });

    this.oidc.isAuthenticated$.subscribe(({ isAuthenticated }) => {
      this.isAuthenticated = isAuthenticated;
      if (!isAuthenticated) {
        this.userName = '';
        this.userEmail = '';
      }
    });

    this.oidc.userData$.subscribe((userData: any) => {
      if (userData) {
        this.userName = userData?.name ?? userData?.preferred_username ?? '';
        this.userEmail = userData?.email ?? '';
      } else {
        this.userName = '';
        this.userEmail = '';
      }
    });
  }

  login(): void { this.auth.login(); }
  logout(): void { this.auth.logout(); }
}
