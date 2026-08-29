import { Component, inject } from '@angular/core';
import { PlayerGameStore } from '../../stores/player-game.store';

@Component({
  selector: 'app-score-panel',
  standalone: true,
  template: `
    <div class="score-panel" aria-live="polite">
      <span>{{ store.score().totalPoints }} pts</span>
      <small>· {{ store.securedPoints().securedPoints }} asegurados</small>
      @if (store.securedPoints().checkpointRoundNumber) {
        <small> checkpoint ronda {{ store.securedPoints().checkpointRoundNumber }}</small>
      }
      <span class="badge">{{ store.securedPoints().policy }}</span>
      <small>Correctas: {{ store.score().correctAnswers }}</small>
    </div>
  `,
  styles: [`
    .score-panel { display:flex; gap:8px; align-items:center; padding:8px; flex-wrap:wrap; }
    .badge { background: var(--color-primary); color:white; padding:2px 8px; border-radius:12px; font-size:0.75rem; }
  `]
})
export class ScorePanelComponent {
  store = inject(PlayerGameStore);
}
