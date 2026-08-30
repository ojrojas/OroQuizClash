import { Routes } from '@angular/router';
import { authGuard, mustChangePasswordGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'player/lobby', pathMatch: 'full' },

  // Legacy redirects to canonical
  { path: 'lobby', redirectTo: 'player/lobby', pathMatch: 'full' },
  { path: 'game/:gameId', redirectTo: 'player/game/:gameId', pathMatch: 'full' },
  { path: 'result/:gameId', redirectTo: 'player/game/:gameId/result', pathMatch: 'full' },
  { path: 'rewards', redirectTo: 'player/rewards', pathMatch: 'full' },
  { path: 'rewards/history', redirectTo: 'player/rewards/history', pathMatch: 'full' },
  { path: 'rewards/:rewardId', redirectTo: 'player/rewards/:rewardId', pathMatch: 'full' },
  { path: 'lobby/:gameId', redirectTo: 'player/lobby/:gameId', pathMatch: 'full' },

  {
    path: 'player/lobby',
    loadComponent: () => import('./features/lobby/lobby.component').then(m => m.LobbyComponent),
    canActivate: [authGuard, mustChangePasswordGuard],
  },
  {
    path: 'player/lobby/:gameId',
    loadComponent: () => import('./features/lobby/game-detail.component').then(m => m.GameDetailComponent),
    canActivate: [authGuard, mustChangePasswordGuard],
  },
  {
    path: 'player/game/:gameId',
    loadComponent: () => import('./features/game/game.component').then(m => m.GameComponent),
    canActivate: [authGuard, mustChangePasswordGuard],
  },
  {
    path: 'player/game/:gameId/result',
    loadComponent: () => import('./features/result/result.component').then(m => m.ResultComponent),
    canActivate: [authGuard, mustChangePasswordGuard],
  },
  {
    path: 'player/rewards',
    loadComponent: () => import('./features/rewards/rewards-catalog.component').then(m => m.RewardsCatalogComponent),
    canActivate: [authGuard, mustChangePasswordGuard],
  },
  {
    path: 'player/rewards/history',
    loadComponent: () => import('./features/rewards/redemption-history.component').then(m => m.RedemptionHistoryComponent),
    canActivate: [authGuard, mustChangePasswordGuard],
  },
  {
    path: 'player/rewards/:rewardId',
    loadComponent: () => import('./features/rewards/reward-detail.component').then(m => m.RewardDetailComponent),
    canActivate: [authGuard, mustChangePasswordGuard],
  },
  {
    path: 'auth/callback',
    loadComponent: () => import('./core/auth/callback.component').then(m => m.CallbackComponent),
  },
  {
    path: 'auth/logout-callback',
    loadComponent: () => import('./core/auth/logout-callback.component').then(m => m.LogoutCallbackComponent),
  },
  { path: '**', redirectTo: 'player/lobby' },
];
