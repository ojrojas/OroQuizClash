import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PlayerGameStore } from '../../stores/player-game.store';
import { LoadingSkeletonComponent } from '../../shared/ui/loading-skeleton.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';

@Component({
  selector: 'app-waiting-room',
  standalone: true,
  imports: [CommonModule, LoadingSkeletonComponent, EmptyStateComponent],
  template: `
    <div class="waiting" data-theme="player">
      @if (store.ui().isLoading) {
        <app-loading-skeleton [rows]="2" />
      } @else if (store.status().gameStatus === 'WAITING_FOR_PLAYERS') {
        <h3>Esperando jugadores</h3>
        <p>Estado: {{ store.game()?.status }} | Jugadores max {{ store.game()?.maxPlayers }}</p>
        <app-empty-state message="Esperando a que el organizador inicie el juego" />
      } @else {
        <p>Juego listo: {{ store.game()?.name }}</p>
      }
      @if (store.ui().error?.code === 'GameFull') {
        <div role="alert">Juego lleno</div>
      }
      @if (store.ui().error?.code === 'GameNotWaitingForPlayers') {
        <div role="alert">El juego ya no acepta jugadores</div>
      }
    </div>
  `
})
export class WaitingRoomComponent {
  store = inject(PlayerGameStore);
}
