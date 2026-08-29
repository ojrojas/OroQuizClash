import { Component, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { PlayerGameStore } from '../../stores/player-game.store';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { LoadingSkeletonComponent } from '../../shared/ui/loading-skeleton.component';
import { ErrorStateComponent } from '../../shared/ui/error-state.component';
import { QuestionComponent } from './question.component';
import { TimerComponent } from './timer.component';
import { ScorePanelComponent } from './score-panel.component';

@Component({
  selector: 'app-game',
  standalone: true,
  imports: [CommonModule, LoadingSkeletonComponent, ErrorStateComponent, QuestionComponent, TimerComponent, ScorePanelComponent],
  providers: [PlayerGameStore],
  template: `
    <div class="game-cinematic" data-theme="player" style="display:grid; grid-template-areas:'header' 'center' 'footer'; gap:var(--space-4,16px); padding:16px; min-height:100vh;">
      @if (store.ui().isHydrating) {
        <app-loading-skeleton [rows]="4" />
      } @else if (store.ui().error) {
        <app-error-state [message]="store.ui().error!.detail" [correlationId]="store.ui().error!.correlationId" [traceId]="store.ui().error!.traceId" (retry)="hydrate()" />
      } @else if (!store.round()) {
        <div role="status" aria-live="polite" style="grid-area:center; text-align:center; padding:32px;">Esperando ronda... Juego: {{ store.game()?.name }} | Jugador: {{ store.player()?.displayName }}</div>
      } @else if (store.isTerminal()) {
        <div role="status" aria-live="assertive" style="grid-area:center; text-align:center;">Estado terminal: {{ store.status().playerStatus }} - {{ store.status().gameStatus }}</div>
        <app-score-panel />
      } @else {
        <header style="grid-area:header; display:flex; justify-content:space-between; align-items:center; background: var(--player-gradient-premium, linear-gradient(135deg,#6c4ef6,#9b59ff)); padding:var(--space-4,16px); border-radius:12px; color:white;">
          <div>
            <div>Current Round: {{ store.currentRoundDisplay() }}</div>
            <div>Current Level: {{ store.score().currentLevel }} ({{ store.round()?.level }})</div>
            <div>Player Status: {{ store.status().playerStatus }}</div>
          </div>
          <app-timer />
        </header>

        <main style="grid-area:center;">
          <h2 aria-live="polite">{{ store.question()?.text }}</h2>
          <app-question />
          <div aria-live="polite">Potential Reward: {{ store.potentialReward() }}</div>
        </main>

        <footer style="grid-area:footer; display:flex; gap:var(--space-3,12px); flex-wrap:wrap; align-items:center; justify-content:space-between;">
          <app-score-panel />
          <div>
            <span>Secured Points: {{ store.securedPoints().securedPoints }}</span>
            @if (store.securedPoints().checkpointRoundNumber) { <small> checkpoint ronda {{ store.securedPoints().checkpointRoundNumber }}</small> }
          </div>
          <button (click)="openWithdraw()" [disabled]="store.isTerminal() || !store.status().canAnswer" style="min-height:44px; min-width:44px;" aria-label="Retirarse">Withdrawal Action</button>
        </footer>

        @if (showWithdrawConfirm) {
          <div role="dialog" aria-modal="true" aria-label="Confirmar retiro" style="position:fixed; inset:0; background:rgba(0,0,0,0.5); display:flex; align-items:center; justify-content:center;">
            <div style="background:white; padding:24px; border-radius:12px; max-width:400px;">
              <p>¿Confirmar retiro? Perderás puntos no asegurados según {{ store.securedPoints().policy }}</p>
              <div style="display:flex; gap:8px; justify-content:flex-end;">
                <button (click)="confirmWithdraw()" style="min-height:44px; min-width:44px;">Confirmar</button>
                <button (click)="showWithdrawConfirm=false" style="min-height:44px; min-width:44px;">Cancelar</button>
              </div>
            </div>
          </div>
        }
      }
      <small>Correlation: {{ store.ui().error?.correlationId }}</small>
    </div>
  `

})
export class GameComponent implements OnInit, OnDestroy {
  store = inject(PlayerGameStore);
  private route = inject(ActivatedRoute);
  private oidc = inject(OidcSecurityService);
  showWithdrawConfirm = false;

  ngOnInit() {
    const gameId = this.route.snapshot.paramMap.get('gameId')!;
    this.store.hydrateFor(gameId);
    this.store.startTimerTick();
    this.store.bindRealtime(gameId, () => this.oidc.getAccessToken());
  }

  ngOnDestroy() {
    this.store.stopTimerTick();
  }

  hydrate() {
    const gameId = this.route.snapshot.paramMap.get('gameId')!;
    this.store.hydrateFor(gameId);
  }

  openWithdraw() { this.showWithdrawConfirm = true; }

  confirmWithdraw() {
    const gameId = this.route.snapshot.paramMap.get('gameId')!;
    const key = sessionStorage.getItem(`idemp-withdraw-${gameId}`) ?? crypto.randomUUID();
    sessionStorage.setItem(`idemp-withdraw-${gameId}`, key);
    this.showWithdrawConfirm = false;
    this.store.withdraw();
  }

  withdraw() { this.openWithdraw(); }
}
