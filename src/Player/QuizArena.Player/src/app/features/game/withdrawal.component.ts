import { Component, inject, signal, computed } from '@angular/core';
import { PlayerGameStore } from '../../stores/player-game.store';
import { CommonModule } from '@angular/common';
import { formatSecured } from './withdrawal-display.model';

@Component({
  selector: 'app-withdrawal',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div>
      <button (click)="open()" [disabled]="store.isTerminal() || !store.status().canAnswer" style="min-height:44px; min-width:44px;" aria-label="Retirarse">Withdrawal Action</button>
      @if (showConfirm()) {
        <div role="dialog" aria-modal="true" aria-label="Confirmar retiro" style="position:fixed; inset:0; background:rgba(0,0,0,0.5); display:flex; align-items:center; justify-content:center;" (click)="showConfirm.set(false)">
          <div style="background:var(--color-surface,#FFF); padding:var(--space-6,24px); border-radius:var(--radius-lg,12px); max-width:400px; display:flex; flex-direction:column; gap:var(--space-3,12px); border:1px solid var(--color-border,#DBEAFE);" (click)="$event.stopPropagation()" role="document">
            <h2>Confirmar retiro</h2>
            <div class="metrics" role="group" aria-label="Puntuaciones" style="display:flex; flex-direction:column; gap:var(--space-2,8px);">
              <div role="status" aria-live="polite" [attr.aria-label]="'Current Points ' + store.score().totalPoints">Current Points {{ store.score().totalPoints }} pts</div>
              <div role="status" aria-live="polite" [attr.aria-label]="'Secured Points ' + store.securedPoints().securedPoints">Secured Points {{ formatSecured(store.securedPoints().securedPoints, store.securedPoints().checkpointRoundNumber) }}</div>
              <div role="status" aria-live="polite" [attr.aria-label]="store.potentialReward() === '—' ? 'Potential no disponible' : 'Potential Points ' + store.potentialReward()">Potential Points {{ store.potentialReward() }}</div>
            </div>
            <div role="alert" aria-live="assertive" class="warning" style="color:var(--color-destructive,#DC2626); font-weight:600;">
              If you continue and answer incorrectly, you may lose your accumulated points.
            </div>
            <div role="alert" aria-live="assertive" class="withdraw-secure" style="color:var(--color-warning,#D97706); font-weight:600;">
              Withdraw now and secure {{ store.securedPoints().securedPoints }} points?
            </div>
            <div style="display:flex; gap:var(--space-3,12px); justify-content:flex-end;">
              <button (click)="confirm()" style="min-height:44px; min-width:44px;" aria-label="Confirmar retiro">Confirmar</button>
              <button (click)="showConfirm.set(false)" style="min-height:44px; min-width:44px;" aria-label="Cancelar">Cancelar</button>
            </div>
            @if (store.ui().error) {
              <div role="alert" aria-live="assertive" style="color:var(--color-destructive,#DC2626);">Error: {{ store.ui().error!.detail }} CorrelationId: {{ store.ui().error!.correlationId }}</div>
            }
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .warning { color:var(--color-destructive,#DC2626); font-weight:600; }
    .withdraw-secure { color:var(--color-warning,#D97706); font-weight:600; }
    @media (prefers-reduced-motion: reduce){ *{ animation:none; } }
  `]
})
export class WithdrawalComponent {
  store = inject(PlayerGameStore);
  showConfirm = signal(false);

  open() { if (!this.store.isTerminal()) this.showConfirm.set(true); }

  formatSecured(secured: number, checkpoint: number | null): string {
    return formatSecured(secured, checkpoint);
  }

  confirm() {
    const gameId = this.store.game()?.gameId ?? '';
    const key = sessionStorage.getItem(`idemp-withdraw-${gameId}`) ?? crypto.randomUUID();
    sessionStorage.setItem(`idemp-withdraw-${gameId}`, key);
    this.showConfirm.set(false);
    this.store.withdraw();
  }
}
