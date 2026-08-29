import { Component, inject } from '@angular/core';
import { PlayerGameStore } from '../../stores/player-game.store';

@Component({
  selector: 'app-timer',
  standalone: true,
  template: `
    <div class="timer" aria-live="polite" [attr.data-state]="store.timer().state" role="timer">
      <span>{{ store.remainingSeconds() }}s</span>
      <small>{{ store.timer().state }}</small>
      @if (store.isExpired()) {
        <span aria-live="assertive">Expirado</span>
      }
    </div>
  `,
  styles: [`
    .timer { font-size: var(--font-size-xl, 1.5rem); font-weight:700; padding:8px; }
    .timer[data-state="EXPIRED"] { color: var(--color-error, red); }
    .timer[data-state="RUNNING"] { color: var(--color-primary); }
  `]
})
export class TimerComponent {
  store = inject(PlayerGameStore);
}
