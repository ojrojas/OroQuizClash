import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { LobbyStore } from './lobby.store';
import { GamesApi } from '../shared/games.api';
import { LoadingSkeletonComponent } from '../../shared/ui/loading-skeleton.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { ErrorStateComponent } from '../../shared/ui/error-state.component';

@Component({
  selector: 'app-lobby',
  standalone: true,
  imports: [CommonModule, LoadingSkeletonComponent, EmptyStateComponent, ErrorStateComponent],
  providers: [LobbyStore],
  template: `
    <div class="lobby" data-theme="player" style="padding:16px;">
      <header style="display:flex; justify-content:space-between; align-items:center;">
        <h2>Available Games</h2>
        <div style="display:flex; gap:8px;">
          <button (click)="refresh()" style="min-height:44px; min-width:44px;" aria-label="Refrescar">Refrescar</button>
          <button (click)="leave()" style="min-height:44px; min-width:44px;" aria-label="Salir del lobby">Leave Lobby</button>
        </div>
      </header>

      @if (store.isLoading()) {
        <app-loading-skeleton [rows]="3" />
      } @else if (store.error()) {
        <app-error-state [message]="store.error()!.detail" [correlationId]="store.error()!.correlationId" [traceId]="store.error()!.traceId" (retry)="refresh()" />
      } @else if (store.isEmpty()) {
        <app-empty-state message="No hay partidas disponibles" />
      } @else {
        <!-- Table >=1024px -->
        <div class="table-wrapper" style="overflow-x:auto;">
          <table aria-live="polite" aria-label="Available Games" style="width:100%; border-collapse:collapse;">
            <thead>
              <tr>
                <th scope="col">Game Name</th>
                <th scope="col">Category</th>
                <th scope="col">Difficulty</th>
                <th scope="col">Number of Rounds</th>
                <th scope="col">Players</th>
                <th scope="col">Start Time</th>
                <th scope="col">Prize</th>
                <th scope="col">Status</th>
                <th scope="col">Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (g of store.games(); track g.gameId) {
                <tr>
                  <td>{{ g.name }}</td>
                  <td>{{ g.categoryName }}</td>
                  <td>{{ g.difficultyName }}</td>
                  <td>{{ g.numberOfRoundsDisplay }}</td>
                  <td>{{ g.players.display }}</td>
                  <td>{{ g.startTime | date:'short' }}</td>
                  <td>{{ g.prize }}</td>
                  <td><span class="badge">{{ g.status }}</span></td>
                  <td style="display:flex; gap:8px;">
                    <button (click)="view(g.gameId)" style="min-height:44px; min-width:44px;" [attr.aria-label]="'Ver información ' + g.name">View Game Information</button>
                    <button (click)="join(g.gameId)" [disabled]="g.players.current >= g.players.max || g.status !== 'WAITING_FOR_PLAYERS'" style="min-height:44px; min-width:44px;" [attr.aria-label]="'Unirse a ' + g.name">Join Game</button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        <!-- Cards <=768px stacked same 8 fields -->
        <div class="cards" aria-live="polite" style="display:none;">
          @for (g of store.games(); track g.gameId) {
            <article class="card" style="border:1px solid var(--color-border, #ddd); padding:16px; margin:8px 0; border-radius:8px;">
              <h3>{{ g.name }}</h3>
              <dl>
                <dt>Category</dt><dd>{{ g.categoryName }}</dd>
                <dt>Difficulty</dt><dd>{{ g.difficultyName }}</dd>
                <dt>Number of Rounds</dt><dd>{{ g.numberOfRoundsDisplay }}</dd>
                <dt>Players</dt><dd>{{ g.players.display }}</dd>
                <dt>Start Time</dt><dd>{{ g.startTime | date:'short' }}</dd>
                <dt>Prize</dt><dd>{{ g.prize }}</dd>
                <dt>Status</dt><dd>{{ g.status }}</dd>
              </dl>
              <button (click)="view(g.gameId)" style="min-height:44px;">View Game Information</button>
              <button (click)="join(g.gameId)" [disabled]="g.players.current >= g.players.max" style="min-height:44px;">Join Game</button>
            </article>
          }
        </div>

        <div style="display:flex; justify-content:center; gap:8px; margin-top:16px;">
          <button (click)="prev()" [disabled]="store.page() <= 1" style="min-height:44px;">Anterior</button>
          <span>Página {{ store.page() }} de {{ totalPages }} ({{ store.totalCount() }} juegos)</span>
          <button (click)="next()" [disabled]="store.page() >= totalPages" style="min-height:44px;">Siguiente</button>
        </div>
      }
    </div>
  `,
  styles: [`
    @media (max-width: 768px) {
      .table-wrapper { display: none !important; }
      .cards { display: block !important; }
    }
    @media (min-width: 769px) {
      .cards { display: none !important; }
    }
    th, td { padding:8px; text-align:left; border-bottom:1px solid #eee; }
    th { background: var(--color-primary); color: white; }
    button:focus-visible { outline: 2px solid var(--color-primary); outline-offset:2px; }
    .badge { background: var(--color-primary); color:white; padding:2px 8px; border-radius:12px; }
  `]
})
export class LobbyComponent implements OnInit {
  store = inject(LobbyStore);
  private api = inject(GamesApi);
  private router = inject(Router);

  get totalPages() { return Math.ceil(this.store.totalCount() / this.store.pageSize()) || 1; }

  ngOnInit() {
    this.refresh();
  }

  refresh() {
    this.store.load({ page: 1 });
  }

  prev() {
    const p = Math.max(1, this.store.page() - 1);
    this.store.load({ page: p });
  }

  next() {
    const p = Math.min(this.totalPages, this.store.page() + 1);
    this.store.load({ page: p });
  }

  join(gameId: string) {
    const key = sessionStorage.getItem(`idemp-join-${gameId}`) ?? crypto.randomUUID();
    sessionStorage.setItem(`idemp-join-${gameId}`, key);
    this.api.joinGame(gameId, key).subscribe({
      next: () => this.router.navigate(['/game', gameId]),
      error: (err: any) => {
        // Error handled via LobbyStore error not here, but show via store error
        console.error(err);
      }
    });
  }

  view(gameId: string) {
    this.router.navigate(['/lobby', gameId]);
  }

  leave() {
    this.router.navigate(['/']);
  }
}
