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
    <div class="game" data-theme="player">
      @if (store.ui().isHydrating) {
        <app-loading-skeleton [rows]="4" />
      } @else if (store.ui().error) {
        <app-error-state [message]="store.ui().error!.detail" [correlationId]="store.ui().error!.correlationId" (retry)="hydrate()" />
      } @else if (store.isTerminal()) {
        <div role="status" aria-live="polite">Estado terminal: {{ store.status().playerStatus }} - {{ store.status().gameStatus }}</div>
        <app-score-panel />
      } @else if (!store.round()) {
        <div role="status">Esperando ronda...</div>
        <p>Juego: {{ store.game()?.name }} | Jugador: {{ store.player()?.displayName }}</p>
      } @else {
        <app-timer />
        <app-score-panel />
        <p>Ronda {{ store.round()!.roundNumber }} - Nivel {{ store.round()!.level }} - {{ store.round()!.status }}</p>
        @if (store.status().isTerminal) {
          <div aria-live="assertive">Juego terminado</div>
        } @else {
          <app-question />
          <button (click)="withdraw()" [disabled]="store.isTerminal()" style="min-height:44px;">Retirarse</button>
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

  withdraw() {
    this.store.withdraw();
  }
}
