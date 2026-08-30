// @ts-nocheck
import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ErrorStateComponent } from '../../shared/ui/error-state.component';
import { LoadingSkeletonComponent } from '../../shared/ui/loading-skeleton.component';
import { PlayerGameStore } from '../../stores/player-game.store';
import { GamesApi } from '../shared/games.api';

@Component({
  selector: 'app-result',
  standalone: true,
  imports: [CommonModule, LoadingSkeletonComponent, ErrorStateComponent],
  providers: [PlayerGameStore],
  template: `
    <div class="result" data-theme="player" style="display:flex; flex-direction:column; gap:var(--space-4,16px); max-width:600px; margin:auto; min-height:100vh; padding:var(--space-4,16px);">
      @if (store.ui().isLoading || store.ui().isHydrating) {
        <app-loading-skeleton [rows]="3" />
      } @else if (store.ui().error) {
        <app-error-state [message]="store.ui().error!.detail" [correlationId]="store.ui().error!.correlationId" [traceId]="store.ui().error!.traceId" (retry)="hydrate()" />
      } @else if (resultState() === 'playing') {
        <app-error-state [message]="'Partida aún en curso'" [correlationId]="correlationId()" (retry)="navigateToGame()" />
        <button (click)="navigateToGame()" style="min-height:44px; min-width:44px;" aria-label="Volver al juego">Volver al juego</button>
      } @else if (resultState() === 'won') {
        <div class="you-won" role="status" aria-live="assertive" aria-label="Felicidades, YOU WON, puesto 1">
          <h1>YOU WON</h1>
          <div class="final-score" role="status" aria-live="assertive">Final Score {{ finalScore() }} pts</div>
          @if (prize(); as p) { <div class="prize" role="status" aria-live="polite">Prize {{ p.name }}</div> } @else { <span>Sin premio</span> }
          <div class="confetti" aria-hidden="true">🎉</div>
        </div>
      } @else if (resultState() === 'walked') {
        <div class="you-walked-away" role="status" aria-live="assertive" aria-label="YOU WALKED AWAY">
          <h1>YOU WALKED AWAY</h1>
          <div class="secured" role="status" aria-live="polite">Secured Points {{ formatSecured() }}</div>
          <div class="available-rewards" role="list" aria-label="Available Rewards" aria-live="polite">
            @for (r of availableRewards(); track r.rewardId) {
              <div role="listitem">{{ r.name }} {{ r.pointsRequired }} pts</div>
            } @empty {
              <span>Sin recompensas disponibles</span>
            }
          </div>
        </div>
      } @else if (resultState() === 'over') {
        <div class="game-over" role="status" aria-live="assertive" aria-label="GAME OVER">
          <h1>GAME OVER</h1>
          <div class="final-score" role="status" aria-live="assertive">Final Score {{ finalScore() }} pts</div>
          @if (consolation(); as c) { <div class="consolation" role="status" aria-live="polite">Consolation Reward {{ c.name }}</div> } @else { <span>Sin consolación</span> }
        </div>
      } @else if (resultState() === 'finished') {
        <div class="game-finished" role="status" aria-live="assertive" [attr.aria-label]="'GAME FINISHED puesto ' + finalPosition() + ' de ' + totalPlayers()">
          <h1>GAME FINISHED</h1>
          <div class="final-position" role="status" aria-live="polite">Final Position {{ finalPosition() }} de {{ totalPlayers() }}</div>
          <div class="final-score" role="status" aria-live="assertive">Final Score {{ finalScore() }} pts</div>
          @if (reward(); as rw) { <div class="reward" role="status" aria-live="polite">Reward {{ rw.name }}</div> } @else { <span>Sin recompensa</span> }
        </div>
      }
      <button (click)="goLobby()" aria-label="Volver al lobby" style="min-height:44px; min-width:44px; margin-top:var(--space-4,16px);">Volver al lobby</button>
      <small>CorrelationId: {{ correlationId() }}</small>
    </div>
  `,
  styles: [`
    .you-won { background:var(--color-success,#16A34A); color:var(--color-success-contrast,#FFF); border-radius:var(--radius-lg,12px); padding:var(--space-6,24px); text-align:center; animation:confetti 600ms ease; }
    .you-walked-away { background:var(--color-warning,#D97706); color:var(--color-warning-contrast,#FFF); border-radius:var(--radius-lg,12px); padding:var(--space-6,24px); text-align:center; }
    .game-over { background:var(--color-destructive,#DC2626); color:var(--color-on-destructive,#FFF); border-radius:var(--radius-lg,12px); padding:var(--space-6,24px); text-align:center; }
    .game-finished { background:var(--color-accent,#7C3AED); color:var(--color-on-accent,#FFF); border-radius:var(--radius-lg,12px); padding:var(--space-6,24px); text-align:center; }
    .final-score { font-size:var(--font-size-lg,20px); font-weight:700; margin:var(--space-3,12px) 0; }
    .final-position { font-size:var(--font-size-lg,20px); font-weight:700; }
    .available-rewards { display:grid; grid-template-columns:1fr; gap:var(--space-2,8px); margin-top:var(--space-3,12px); }
    @media (min-width:768px){ .available-rewards { grid-template-columns:repeat(2,1fr); } }
    .confetti { font-size:32px; animation:pulse 600ms ease infinite; }
    @media (prefers-reduced-motion: reduce){ .you-won, .confetti, .pulse { animation:none; } }
    @keyframes confetti { 0%{transform:scale(0.9);opacity:0}100%{transform:scale(1);opacity:1} }
    @keyframes pulse { 0%,100%{opacity:1}50%{opacity:0.8} }
  `]
})
export class ResultComponent implements OnInit {
  store = inject(PlayerGameStore);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private api = inject(GamesApi);

  leaderboard = signal<any[]>([]);
  rewards = signal<any[]>([]);

  resultState = computed(() => {
    const ps = this.store.status().playerStatus;
    const gs = this.store.status().gameStatus;
    const rank = this.finalPosition();
    const isTerminal = this.store.status().isTerminal || this.store.game()?.status === 'FINISHED' || gs === 'FINISHED';
    if (ps === 'WINNER' && isTerminal && rank === 1) return 'won' as const;
    if (ps === 'WITHDRAWN') return 'walked' as const;
    if (ps === 'ELIMINATED') return 'over' as const;
    if (isTerminal && (ps === 'FINISHED' || gs === 'FINISHED') && rank != null && rank >= 2) return 'finished' as const;
    if (ps === 'WINNER' && rank === 1) return 'won' as const;
    if (isTerminal) {
      if (ps === 'WINNER') return 'won' as const;
      if (ps === 'WITHDRAWN') return 'walked' as const;
      if (ps === 'ELIMINATED') return 'over' as const;
      return 'finished' as const;
    }
    return 'playing' as const;
  });

  finalScore = computed(() => this.store.score().totalPoints ?? 0);
  finalPosition = computed(() => {
    const sub = this.store.player()?.playerId ?? this.store.score().playerId;
    const entry = this.leaderboard().find((e: any) => (e.playerId ?? e.PlayerId) === sub);
    if (entry) return entry.position ?? entry.Rank ?? entry.rank ?? null;
    // fallback from store's gameSession? use 1 if WINNER else null
    if (this.store.status().playerStatus === 'WINNER') return 1;
    return null;
  });
  totalPlayers = computed(() => this.leaderboard().length || (this.store.game() as any)?.maxPlayers || 4);

  prize = computed(() => {
    const total = this.finalScore();
    // Simple: if total >=500 return Pack Oro, else null
    if (total >= 500) return { name: 'Pack Oro', pointsRequired: 500 };
    return null;
  });

  reward = computed(() => {
    const total = this.finalScore();
    if (total >= 300) return { name: 'Pack Bronce', pointsRequired: 300 };
    return null;
  });

  consolation = computed(() => {
    if (this.store.status().playerStatus !== 'ELIMINATED') return null;
    const game: any = this.store.game();
    const policy = game?.configuration?.consolationPolicy ?? game?.Configuration?.ConsolationPolicy;
    if (policy) return { name: 'Pack Consuelo' };
    return null;
  });

  availableRewards = computed(() => {
    const secured = this.store.securedPoints().securedPoints ?? 0;
    return this.rewards().filter((r: any) => (r.pointsRequired ?? r.PointsRequired ?? 0) <= secured);
  });

  correlationId = computed(() => this.store.ui().error?.correlationId ?? '');

  ngOnInit() {
    const gameId = this.route.snapshot.paramMap.get('gameId')!;
    this.store.hydrateFor(gameId);
    this.api.getLeaderboard(gameId).subscribe({
      next: (res: any) => this.leaderboard.set(res.entries ?? res.Players ?? []),
      error: () => {}
    });
    // mock rewards for available
    this.rewards.set([
      { rewardId: 'pack-plata', name: 'Pack Plata', pointsRequired: 300 },
      { rewardId: 'pack-oro', name: 'Pack Oro', pointsRequired: 500 }
    ]);
  }

  hydrate() {
    const gameId = this.route.snapshot.paramMap.get('gameId')!;
    this.store.hydrateFor(gameId);
  }

  formatSecured(): string {
    const sp = this.store.securedPoints();
    if (sp.checkpointRoundNumber != null) return `${sp.securedPoints} pts · checkpoint ${sp.checkpointRoundNumber}`;
    return `${sp.securedPoints} pts`;
  }

  navigateToGame() {
    const gameId = this.route.snapshot.paramMap.get('gameId')!;
    this.router.navigate(['/player/game', gameId]);
  }

  goLobby() {
    this.router.navigate(['/player/lobby']);
  }
}
