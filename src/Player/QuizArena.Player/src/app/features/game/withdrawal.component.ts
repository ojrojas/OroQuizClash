import { Component, inject, signal } from '@angular/core';
import { PlayerGameStore } from '../../stores/player-game.store';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-withdrawal',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div>
      <button (click)="open()" [disabled]="store.isTerminal()" style="min-height:44px; min-width:44px;" aria-label="Retirarse">Withdrawal Action</button>
      @if (showConfirm()) {
        <div role="dialog" aria-modal="true" aria-label="Confirmar retiro" style="position:fixed; inset:0; background:rgba(0,0,0,0.5); display:flex; align-items:center; justify-content:center;">
          <div style="background:white; padding:24px; border-radius:12px; max-width:400px;">
            <p>¿Confirmar retiro? Perderás puntos no asegurados según {{ store.securedPoints().policy }}</p>
            <div style="display:flex; gap:8px; justify-content:flex-end;">
              <button (click)="confirm()" style="min-height:44px; min-width:44px;">Confirmar</button>
              <button (click)="showConfirm.set(false)" style="min-height:44px; min-width:44px;">Cancelar</button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class WithdrawalComponent {
  store = inject(PlayerGameStore);
  showConfirm = signal(false);

  open() { this.showConfirm.set(true); }

  confirm() {
    const gameId = this.store.game()?.gameId ?? '';
    const key = sessionStorage.getItem(`idemp-withdraw-${gameId}`) ?? crypto.randomUUID();
    sessionStorage.setItem(`idemp-withdraw-${gameId}`, key);
    this.showConfirm.set(false);
    this.store.withdraw();
  }
}
