import { Component, inject, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute } from '@angular/router';
import { PlayerRewardsStore } from '../../stores/player-rewards.store';
import { LoadingSkeletonComponent } from '../../shared/ui/loading-skeleton.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { ErrorStateComponent } from '../../shared/ui/error-state.component';
import { deriveRewardStatus, formatRemaining } from './rewards-display.model';

@Component({
  selector: 'app-rewards-catalog',
  standalone: true,
  imports: [CommonModule, LoadingSkeletonComponent, EmptyStateComponent, ErrorStateComponent],
  template: `
    <div class="rewards" data-theme="player">
      <header class="wallet" role="status" aria-live="polite" [attr.aria-label]="'Available Points ' + (store.wallet().availablePoints ?? 0)">
        <div>
          <h2>Points Wallet</h2>
          <div class="available" aria-live="polite">Available Points {{ store.wallet().availablePoints ?? 0 }} pts</div>
        </div>
      </header>

      @if (store.isHydrating) {
        <app-loading-skeleton [rows]="6" />
      } @else if (store.error) {
        <app-error-state [message]="store.error!.detail" [correlationId]="store.error!.correlationId" [traceId]="store.error!.traceId" (retry)="hydrate()" />
      } @else if (store.catalog().length === 0) {
        <app-empty-state message="No hay recompensas disponibles">
          <a routerLink="/player" style="min-height:44px; display:inline-flex; align-items:center;">Explorar juego</a>
        </app-empty-state>
      } @else {
        <div class="grid" role="group" aria-label="Catálogo de recompensas">
          @for (r of store.catalog(); track r.id) {
            <div class="card" role="group" [attr.aria-label]="r.name">
              <h3>{{ r.name }}</h3>
              <p>{{ r.description }}</p>
              <div role="status" aria-live="polite" [attr.aria-label]="'Required Points ' + r.pointsRequired">Required Points {{ r.pointsRequired }} pts</div>
              <div class="badge" [class.canjeable]="deriveStatus(r) === 'Canjeable'" [class.insuficiente]="deriveStatus(r) === 'Puntos insuficientes'" [class.agotada]="deriveStatus(r) === 'Agotada'" role="status" [attr.aria-label]="deriveStatus(r)">{{ deriveStatus(r) }}</div>
              <div role="status" aria-live="polite" [attr.aria-label]="'Remaining Points ' + formatRemain(r)">{{ formatRemain(r) }}</div>
              <button (click)="goDetail(r.id)" style="min-height:44px; min-width:44px;" [attr.aria-label]="'Ver detalle ' + r.name">Ver detalle</button>
            </div>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .wallet { background:var(--color-surface,#FFF); border:1px solid var(--color-border,#DBEAFE); border-radius:var(--radius-lg,12px); padding:var(--space-6,24px); display:flex; justify-content:space-between; margin-bottom:var(--space-4,16px); }
    .available { font-size:var(--text-2xl,24px); color:var(--color-primary,#6c4ef6); font-weight:700; }
    .grid { display:grid; grid-template-columns:1fr; gap:var(--space-3,12px); }
    @media (min-width:768px){ .grid{ grid-template-columns:1fr 1fr; } }
    @media (min-width:1536px){ .grid{ grid-template-columns:1fr 1fr 1fr 1fr; } }
    .card { padding:var(--space-3,12px); min-height:160px; border-radius:var(--radius-md,8px); border:1px solid var(--color-border,#DBEAFE); background:var(--color-surface,#FFF); display:flex; flex-direction:column; gap:var(--space-2,8px); }
    .badge { padding:4px 8px; border-radius:var(--radius-full,999px); font-size:var(--text-sm,14px); font-weight:600; display:inline-block; width:fit-content; }
    .badge.canjeable { background:var(--color-success,#22c55e); color:white; }
    .badge.insuficiente { background:var(--color-warning,#D97706); color:white; }
    .badge.agotada { background:var(--color-destructive,#DC2626); color:white; }
    button { min-height:44px; min-width:44px; }
    @media (prefers-reduced-motion: reduce){ *{ animation:none !important; } }
  `]
})
export class RewardsCatalogComponent implements OnInit {
  store = inject(PlayerRewardsStore);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  ngOnInit() {
    const gameId = this.route.snapshot.queryParamMap.get('gameId') ?? undefined;
    if (gameId) this.store.hydrateFor(gameId);
    this.store.hydrate(gameId as any);
  }

  hydrate() {
    const gameId = this.route.snapshot.queryParamMap.get('gameId') ?? undefined;
    this.store.hydrate(gameId as any);
  }

  deriveStatus(r: any): string {
    const available = this.store.wallet().availablePoints;
    return deriveRewardStatus(available, r.pointsRequired, r.available, r.stock);
  }

  formatRemain(r: any): string {
    const available = this.store.wallet().availablePoints;
    const status = this.deriveStatus(r);
    if (status === 'Canjeable') {
      const diff = (available ?? 0) - r.pointsRequired;
      return `Quedan ${diff} pts`;
    }
    if (available != null) return `Te faltan ${Math.abs((available ?? 0) - r.pointsRequired)} pts`;
    return formatRemaining(available, r.pointsRequired, r.available);
  }

  goDetail(rewardId: string) {
    const gameId = this.route.snapshot.queryParamMap.get('gameId');
    this.router.navigate(['/rewards', rewardId], { queryParams: gameId ? { gameId } : {} });
  }
}
