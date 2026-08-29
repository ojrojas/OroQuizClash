import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { PlayerGameStore } from '../../stores/player-game.store';
import { GamesApi } from '../shared/games.api';
import { LoadingSkeletonComponent } from '../../shared/ui/loading-skeleton.component';
import { ErrorStateComponent } from '../../shared/ui/error-state.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';

@Component({
  selector: 'app-lobby',
  standalone: true,
  imports: [CommonModule, LoadingSkeletonComponent, ErrorStateComponent, EmptyStateComponent],
  providers: [PlayerGameStore],
  template: `
    <div class="lobby" data-theme="player">
      @if (store.ui().isLoading) {
        <app-loading-skeleton [rows]="3" />
      } @else if (store.ui().error) {
        <app-error-state [message]="store.ui().error!.detail" [correlationId]="store.ui().error!.correlationId" [traceId]="store.ui().error!.traceId" (retry)="store.clearError()" />
      } @else if (!store.game()) {
        <app-empty-state message="No hay juego seleccionado. Únete desde el lobby." />
      } @else {
        <h2>{{ store.game()!.name }}</h2>
        <p>Estado: {{ store.status().gameStatus }} | Sesión: {{ store.status().playerStatus }}</p>
        <p>Jugador: {{ store.player()?.displayName }} | Score: {{ store.score().totalPoints }} pts · {{ store.securedPoints().securedPoints }} asegurados</p>
        <p>CanAnswer: {{ store.canAnswer() }} | Terminal: {{ store.isTerminal() }}</p>
        <button (click)="join()" style="min-height:44px; min-width:44px;" aria-label="Unirse al juego">Unirse al juego</button>
        <a [href]="'/game/' + store.game()!.gameId">Ir al juego</a>
      }
    </div>
  `
})
export class LobbyComponent implements OnInit {
  store = inject(PlayerGameStore);
  private api = inject(GamesApi);
  private route = inject(ActivatedRoute);

  ngOnInit() {
    const gameId = this.route.snapshot.paramMap.get('gameId') ?? this.route.snapshot.queryParamMap.get('gameId');
    if (gameId) {
      this.store.hydrateFor(gameId);
    }
  }

  join() {
    const gameId = this.store.game()?.gameId;
    if (!gameId) return;
    const key = crypto.randomUUID();
    sessionStorage.setItem(`idemp-join-${gameId}`, key);
    this.api.joinGame(gameId, key).subscribe({
      next: () => this.store.hydrateFor(gameId),
      error: (err) => console.error(err)
    });
  }
}
