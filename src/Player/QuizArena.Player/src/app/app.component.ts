import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { AuthService } from './core/auth/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div data-theme="player" class="app-shell">
      <header class="app-header">
        <h1>QuizArena Player</h1>
        <nav class="main-nav" aria-label="Navegación principal" style="display:flex; gap:0.5rem; flex-wrap:wrap; align-items:center;">
          <a routerLink="/player/lobby" routerLinkActive="active" aria-label="Ir al Lobby" style="color:white; text-decoration:none; padding:0.4rem 0.6rem; border-radius:6px; min-height:44px; display:inline-flex; align-items:center;" [style.background]="isActive('/player/lobby') ? 'rgba(255,255,255,0.2)' : 'transparent'">Lobby</a>
          <a routerLink="/player/rewards" routerLinkActive="active" aria-label="Ver recompensas y canjear" style="color:white; text-decoration:none; padding:0.4rem 0.6rem; border-radius:6px; min-height:44px; display:inline-flex; align-items:center; background:var(--color-warning, #F59E0B); color:#111;" title="Canjear recompensas">🎁 Recompensas</a>
          <a routerLink="/player/rewards/history" routerLinkActive="active" aria-label="Historial de canjes" style="color:white; text-decoration:none; padding:0.4rem 0.6rem; border-radius:6px; min-height:44px; display:inline-flex; align-items:center;" [style.background]="isActive('/player/rewards/history') ? 'rgba(255,255,255,0.2)' : 'transparent'">Historial</a>
        </nav>
        <nav class="auth-nav" aria-label="Autenticación">
          @if (isAuthenticated) {
            <span class="user-info" aria-live="polite" title="{{ userName }} {{ userEmail }}">
              {{ userName }}@if (userEmail) { <small style="opacity:0.8;">({{ userEmail }})</small> }
            </span>
            <button type="button" (click)="logout()" class="btn btn-secondary" aria-label="Cerrar sesión - limpia App, Api e IdentityServer" title="Cierra sesión en App, Api e IdentityServer" style="min-height:44px; min-width:44px;">
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
    .app-header { padding: var(--space-4, 1rem); background: var(--color-primary, #2563EB); color: white; display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap; gap:0.75rem; }
    .main-nav a.active, .main-nav a:hover { background: rgba(255,255,255,0.15) !important; }
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

  private mapUserInfo(userData: any) {
    if (!userData) return;
    const d = userData;
    const emailRaw = (d.email && String(d.email).trim()) || (d.upn && String(d.upn).trim()) || '';
    // OroIdentityServer puede devolver name vacío: hacer fallback amplio, usando prefijo de email como nombre si existe
    let name =
      (d.name && String(d.name).trim()) ||
      (d.preferred_username && String(d.preferred_username).trim()) ||
      (d.username && String(d.username).trim()) ||
      (d.nickname && String(d.nickname).trim()) ||
      (d.given_name && String(d.given_name).trim()) ||
      (d.upn && String(d.upn).trim()) ||
      (d.unique_name && String(d.unique_name).trim()) ||
      '';
    if (!name && emailRaw) {
      // Usar prefijo de email como nombre (ej. player1@example.com -> player1)
      name = emailRaw.split('@')[0];
    }
    if (!name) {
      name = d.sub ? `Jugador ${String(d.sub).slice(0, 8)}` : 'Jugador';
    }
    this.userName = name;
    this.userEmail = emailRaw;
    console.log('[Auth] mapUserInfo', { name, email: emailRaw, raw: d });
  }

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
        if (userData) this.mapUserInfo(userData);
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
        if (isAuthenticated && userData) this.mapUserInfo(userData);
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
      if (userData) this.mapUserInfo(userData);
      else if (!this.isAuthenticated) {
        this.userName = '';
        this.userEmail = '';
      }
    });
  }

  isActive(path: string): boolean {
    try { return window.location.pathname.startsWith(path); } catch { return false; }
  }
  login(): void { this.auth.login(); }
  logout(): void {
    console.log('[Auth] logout clicked -> App + Api + IdentityServer');
    this.auth.logout();
  }
}
