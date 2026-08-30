// @ts-nocheck
import { Component, inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { PlayerGameStore } from '../../stores/player-game.store';
import { PlayerRoundsStore } from '../../stores/player-rounds.store';
import { AnswerInteractionStore } from '../../stores/answer-interaction.store';
import { PlayerRoundsComponent } from './player-rounds.component';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { LoadingSkeletonComponent } from '../../shared/ui/loading-skeleton.component';
import { ErrorStateComponent } from '../../shared/ui/error-state.component';
import { QuestionComponent } from './question.component';
import { TimerComponent } from './timer.component';
import { ScorePanelComponent } from './score-panel.component';
import { LeaderboardComponent } from './leaderboard.component';

@Component({
  selector: 'app-game',
  standalone: true,
  imports: [CommonModule, LoadingSkeletonComponent, ErrorStateComponent, QuestionComponent, TimerComponent, ScorePanelComponent, PlayerRoundsComponent, LeaderboardComponent],
  providers: [PlayerGameStore, PlayerRoundsStore, AnswerInteractionStore],
  template: `
    <div class="game-cinematic" data-theme="player" style="display:grid; grid-template-areas:'header header' 'sidebar center' 'footer footer'; grid-template-columns:280px 1fr; gap:var(--space-4,16px); padding:16px; min-height:100vh;">
      <style>
        @media (max-width:1023px) {
          .game-cinematic { grid-template-areas:'header' 'sidebar' 'center' 'footer' !important; grid-template-columns:1fr !important; }
          .game-sidebar { position:static !important; }
        }
      </style>
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
        <header style="grid-area:header; display:flex; justify-content:space-between; align-items:center; background: var(--player-gradient-premium, linear-gradient(135deg,#6c4ef6,#9b59ff)); padding:var(--space-4,16px); border-radius:12px; color:white; flex-wrap:wrap; gap:var(--space-3,12px);">
          <div>
            <div>Current Round: {{ store.currentRoundDisplay() }}</div>
            <div>Current Level: {{ store.score().currentLevel }} ({{ store.round()?.level }})</div>
            <div>Player Status: {{ store.status().playerStatus }}</div>
          </div>
          <app-leaderboard />
          <app-timer />
        </header>

        <aside style="grid-area:sidebar;" class="game-sidebar">
          <app-player-rounds [gameId]="gameId" />
        </aside>

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
          <div role="dialog" aria-modal="true" aria-label="Confirmar retiro" style="position:fixed; inset:0; background:rgba(0,0,0,0.5); display:flex; align-items:center; justify-content:center;" (click)="showWithdrawConfirm=false">
            <div style="background:var(--color-surface,#FFF); padding:var(--space-6,24px); border-radius:var(--radius-lg,12px); max-width:400px; display:flex; flex-direction:column; gap:var(--space-3,12px); border:1px solid var(--color-border,#DBEAFE);" (click)="$event.stopPropagation()" role="document">
              <h2>Confirmar retiro</h2>
              <div class="metrics" role="group" aria-label="Puntuaciones" style="display:flex; flex-direction:column; gap:var(--space-2,8px);">
                <div role="status" aria-live="polite" [attr.aria-label]="'Current Points ' + store.score().totalPoints">Current Points {{ store.score().totalPoints }} pts</div>
                <div role="status" aria-live="polite" [attr.aria-label]="'Secured Points ' + store.securedPoints().securedPoints">Secured Points {{ store.securedPoints().securedPoints }} pts @if (store.securedPoints().checkpointRoundNumber) { <span>· checkpoint {{ store.securedPoints().checkpointRoundNumber }}</span> }</div>
                <div role="status" aria-live="polite" [attr.aria-label]="store.potentialReward() === '—' ? 'Potential no disponible' : 'Potential Points ' + store.potentialReward()">Potential Points {{ store.potentialReward() }}</div>
              </div>
              <div role="alert" aria-live="assertive" style="color:var(--color-destructive,#DC2626); font-weight:600;">If you continue and answer incorrectly, you may lose your accumulated points.</div>
              <div role="alert" aria-live="assertive" style="color:var(--color-warning,#D97706); font-weight:600;">Withdraw now and secure {{ store.securedPoints().securedPoints }} points?</div>
              <div style="display:flex; gap:var(--space-3,12px); justify-content:flex-end;">
                <button (click)="confirmWithdraw()" style="min-height:44px; min-width:44px;" aria-label="Confirmar retiro">Confirmar</button>
                <button (click)="showWithdrawConfirm=false" style="min-height:44px; min-width:44px;" aria-label="Cancelar">Cancelar</button>
              </div>
              @if (store.ui().error) {
                <div role="alert" aria-live="assertive" style="color:var(--color-destructive,#DC2626);">Error: {{ store.ui().error!.detail }} CorrelationId: {{ store.ui().error!.correlationId }}</div>
              }
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
  roundsStore = inject(PlayerRoundsStore);
  answerStore = inject(AnswerInteractionStore);
  private route = inject(ActivatedRoute);
  private oidc = inject(OidcSecurityService);
  showWithdrawConfirm = false;
  gameId = '';

  ngOnInit() {
    this.gameId = this.route.snapshot.paramMap.get('gameId')!;
    this.store.hydrateFor(this.gameId);
    this.store.startTimerTick();
    this.store.bindRealtime(this.gameId, () => {
      let token = '';
      this.oidc.getAccessToken().subscribe((t: string) => (token = t));
      return token;
    });
    this.roundsStore.hydrateLadder(this.gameId);
    this.roundsStore.bindRealtimeLadder(this.gameId);
    this.answerStore.hydrateAnswer(this.gameId);
    // hydrate answer on realtime events
    (this.store as any)._realtime.events$?.subscribe?.((evt: any) => {
      if (['QuestionAvailable', 'ScoreUpdated', 'RoundCompleted', 'Reconnected'].includes(evt.type)) {
        this.answerStore.hydrateAnswer(this.gameId);
      }
    });
  }

  ngOnDestroy() {
    this.store.stopTimerTick();
  }

  hydrate() {
    const gameId = this.route.snapshot.paramMap.get('gameId')!;
    this.store.hydrateFor(gameId);
    this.roundsStore.hydrateLadder(gameId);
    this.answerStore.hydrateAnswer(gameId);
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
