import { Routes } from '@angular/router';
import { authGuard, mustChangePasswordGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'lobby', pathMatch: 'full' },
  {
    path: 'lobby',
    loadComponent: () => import('./features/lobby/lobby.component').then(m => m.LobbyComponent),
    canActivate: [authGuard, mustChangePasswordGuard],
  },
  {
    path: 'game/:gameId',
    loadComponent: () => import('./features/game/game.component').then(m => m.GameComponent),
    canActivate: [authGuard, mustChangePasswordGuard],
  },
  {
    path: 'result/:gameId',
    loadComponent: () => import('./features/result/result.component').then(m => m.ResultComponent),
    canActivate: [authGuard],
  },
  {
    path: 'auth/callback',
    loadComponent: () => import('./core/auth/callback.component').then(m => m.CallbackComponent),
  },
  {
    path: 'auth/logout-callback',
    loadComponent: () => import('./core/auth/logout-callback.component').then(m => m.LogoutCallbackComponent),
  },
  { path: '**', redirectTo: 'lobby' },
];
