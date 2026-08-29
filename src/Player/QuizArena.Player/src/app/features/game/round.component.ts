import { Component, inject } from '@angular/core';
import { PlayerGameStore } from '../../stores/player-game.store';

@Component({
  selector: 'app-round',
  standalone: true,
  template: `
    <div class="round">
      @if (store.round(); as r) {
        <h4>Ronda {{ r.roundNumber }} - Nivel {{ r.level }}</h4>
        <p>Estado: {{ r.status }}</p>
        @if (r.status === 'COMPLETED') {
          <p aria-live="polite">Ronda completada</p>
        }
        @if (store.status().gameStatus === 'FINISHED') {
          <p aria-live="assertive">Juego terminado</p>
        }
      } @else {
        <p>Sin ronda activa</p>
      }
    </div>
  `
})
export class RoundComponent {
  store = inject(PlayerGameStore);
}
