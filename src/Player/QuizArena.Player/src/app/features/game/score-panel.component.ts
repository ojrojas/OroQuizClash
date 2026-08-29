import { Component, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PlayerGameStore } from '../../stores/player-game.store';
import { LoadingSkeletonComponent } from '../../shared/ui/loading-skeleton.component';
import { ErrorStateComponent } from '../../shared/ui/error-state.component';

@Component({
  selector: 'app-score-panel',
  standalone: true,
  imports: [CommonModule, LoadingSkeletonComponent, ErrorStateComponent],
  template: `
    <div class="score-panel" data-theme="player">
      @if (store.ui().isLoading || store.ui().isHydrating) {
        <app-loading-skeleton [rows]="5" />
      } @else if (store.ui().error) {
        <app-error-state [message]="store.ui().error!.detail" [correlationId]="store.ui().error!.correlationId" [traceId]="store.ui().error!.traceId" (retry)="retry()" />
      } @else {
        <div class="scoring-grid" role="group" aria-label="Puntuaciones">
          <div class="metric current" role="status" aria-live="polite" [attr.aria-label]="'Current Points ' + store.score().totalPoints + ' puntos'">
            <span class="label">Current Points</span>
            <span class="value" [class.pulse]="isPulse()">{{ store.score().totalPoints }} pts</span>
          </div>
          <div class="metric secured" role="status" aria-live="polite" [attr.aria-label]="'Secured Points ' + store.securedPoints().securedPoints + (store.securedPoints().checkpointRoundNumber ? ' checkpoint ronda ' + store.securedPoints().checkpointRoundNumber : '')">
            <span class="label">Secured Points</span>
            <span class="value">
              {{ formatSecured(store.securedPoints().securedPoints, store.securedPoints().checkpointRoundNumber) }}
              @if (store.securedPoints().checkpointRoundNumber) { <span class="badge">· checkpoint {{ store.securedPoints().checkpointRoundNumber }}</span> }
              @if (isSecured()) { <span class="badge asegurado">asegurado</span> }
            </span>
          </div>
          <div class="metric potential" role="status" aria-live="polite" [attr.aria-label]="store.potentialReward() === '—' ? 'Potential no disponible' : 'Potential Points ' + store.potentialReward()">
            <span class="label">Potential Points</span>
            <span class="value">{{ store.potentialReward() }}</span>
          </div>
          <div class="metric round" role="status" aria-live="polite" [attr.aria-label]="'Round Points ' + roundPoints() + ' en juego'">
            <span class="label">Round Points</span>
            <span class="value">{{ roundPoints() }} pts <small>en juego</small></span>
          </div>
          <div class="metric total" role="status" aria-live="polite" [attr.aria-label]="'Total Points ' + store.score().totalPoints + ' puntos'">
            <span class="label">Total Points</span>
            <span class="value total-bold">{{ store.score().totalPoints }} pts</span>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    .score-panel { display:flex; flex-direction:column; gap:var(--space-3,12px); }
    .scoring-grid { display:grid; grid-template-columns:1fr; gap:var(--space-3,12px); }
    @media (min-width:768px){ .scoring-grid { grid-template-columns:repeat(5,1fr); } }
    .metric { display:flex; flex-direction:column; gap:var(--space-1,4px); padding:var(--space-3,12px) var(--space-4,16px); min-height:44px; min-width:44px; border-radius:var(--radius-md,8px); border:1px solid var(--color-border,#DBEAFE); background:var(--color-surface,#FFF); }
    .metric.current .value { color:var(--color-primary,#2563EB); font-weight:700; }
    .metric.secured .value { color:var(--color-success,#16A34A); }
    .metric.round .value { color:var(--color-warning,#D97706); }
    .metric.total .value.total-bold { color:var(--color-primary,#2563EB); font-weight:700; font-size:var(--font-size-lg,20px); }
    .metric.potential .value { color:var(--color-accent,#7C3AED); }
    .label { font-size:var(--font-size-sm,12px); color:var(--color-muted-foreground,#475569); }
    .value { font-size:var(--font-size-md,16px); }
    .badge { background:var(--color-primary-subtle,rgba(37,99,235,0.08)); color:var(--color-primary,#2563EB); padding:var(--space-1,4px) var(--space-2,8px); border-radius:var(--radius-sm,4px); font-size:var(--font-size-sm,12px); margin-left:var(--space-1,4px); }
    .badge.asegurado { background:var(--color-success-subtle,rgba(22,163,74,0.08)); color:var(--color-success,#16A34A); }
    .pulse { animation: pulse 600ms ease; }
    @media (prefers-reduced-motion: reduce){ .pulse { animation:none; } }
    @keyframes pulse { 0%,100%{opacity:1;}50%{opacity:0.8;} }
    .metric:focus-visible { outline:2px solid var(--color-primary,#2563EB); outline-offset:2px; }
  `]
})
export class ScorePanelComponent {
  store = inject(PlayerGameStore);

  roundPoints = computed(() => {
    const s: any = this.store.score();
    if (s.roundPoints != null) return s.roundPoints;
    if (s.RoundPoints != null) return s.RoundPoints;
    // fallback: total - secured
    const total = s.totalPoints ?? s.TotalPoints ?? 0;
    const secured = this.store.securedPoints().securedPoints ?? 0;
    return Math.max(0, total - secured);
  });

  isPulse = computed(() => {
    const v: any = (this.store as any).isScorePulse;
    if (typeof v === 'function') return v();
    return false;
  });

  isSecured = computed(() => {
    const sp = this.store.securedPoints();
    return (sp.securedPoints > 0 && sp.checkpointRoundNumber != null);
  });

  formatSecured(secured: number, checkpoint: number | null): string {
    if (checkpoint != null) return `${secured} pts · checkpoint ${checkpoint}`;
    return `${secured} pts`;
  }

  retry() {
    const gameId = this.store.game()?.gameId ?? '';
    if (gameId) this.store.hydrateFor(gameId);
  }
}
