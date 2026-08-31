import { Component, inject, OnInit, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { PlayerRewardsStore } from '../../stores/player-rewards.store';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingSkeletonComponent } from '../../shared/ui/loading-skeleton.component';
import { ErrorStateComponent } from '../../shared/ui/error-state.component';
import { ConsolationBadgeComponent } from './consolation-badge.component';

@Component({
  selector: 'app-redemption-history',
  standalone: true,
  imports: [CommonModule, EmptyStateComponent, LoadingSkeletonComponent, ErrorStateComponent, ConsolationBadgeComponent],
  providers: [PlayerRewardsStore],
  template: `
    <div class="history" data-theme="player" role="region" aria-label="Historial de canjes">
      <h2>Redemption History</h2>
      @if (store.isHydrating()) {
        <app-loading-skeleton [rows]="3" />
      } @else if (store.error()) {
        <app-error-state [message]="store.error()!.detail" [correlationId]="store.error()!.correlationId" [traceId]="store.error()!.traceId" (retry)="hydrate()" />
      } @else if (store.history().length === 0) {
        <app-empty-state message="Aún no has canjeado recompensas">
          <button (click)="goCatalog()" style="min-height:44px; min-width:44px;">Explorar recompensas</button>
        </app-empty-state>
      } @else {
        <div role="list" aria-label="Historial de canjes" style="display:flex; flex-direction:column; gap:var(--space-3,12px);">
          @for (item of sortedHistory(); track item.id) {
            <div role="listitem" class="row" style="padding:var(--space-3,12px); min-height:44px; border:1px solid var(--color-border,#DBEAFE); border-radius:var(--radius-md,8px); background:var(--color-surface,#FFF); display:flex; justify-content:space-between; align-items:center; gap:var(--space-2,8px); flex-wrap:wrap;">
              <div>
                <div>{{ item.rewardId }}</div>
                <div>{{ item.points }} pts</div>
                <div class="badge" [style.background]="item.status === 'APPROVED' && item.points === 0 ? 'var(--color-info,#3B82F6)' : 'var(--color-success,#22c55e)'" style="color:white; padding:2px 8px; border-radius:var(--radius-full,999px); font-size:var(--text-xs,12px);">{{ isConsolation(item) ? 'Consolation' : 'Canjeada' }} {{ item.status }}</div>
                @if (isConsolation(item)) {
                  <app-consolation-badge [isConsolation]="true" />
                }
              </div>
              <div style="text-align:right;">
                <div>{{ item.requestedAt }}</div>
                <small>{{ item.id }}</small>
              </div>
            </div>
          }
          @if (hasNext()) {
            <button (click)="loadMore()" style="min-height:44px; min-width:44px;">Cargar más</button>
          }
        </div>
      }
    </div>
  `,
  styles: [`
    .history { max-width:800px; margin:0 auto; padding:var(--space-4,16px); display:flex; flex-direction:column; gap:var(--space-3,12px); }
    .row { min-height:44px; }
    @media (prefers-reduced-motion: reduce){ *{ animation:none !important; } }
  `]
})
export class RedemptionHistoryComponent implements OnInit {
  store = inject(PlayerRewardsStore);
  private router = inject(Router);
  page = 1;
  pageSize = 20;

  sortedHistory = computed(() => {
    return [...this.store.history()].sort((a, b) => new Date(b.requestedAt).getTime() - new Date(a.requestedAt).getTime());
  });

  hasNext = computed(() => this.sortedHistory().length >= this.page * this.pageSize);

  ngOnInit() { this.hydrate(); }

  hydrate() { (this.store as unknown as { hydrateHistory: (arg: { page:number; pageSize:number })=>void }).hydrateHistory({ page: this.page, pageSize: this.pageSize }); }

  isConsolation(item: { points: number; status: string }): boolean {
    return item.points === 0 && item.status === 'APPROVED';
  }

  loadMore() {
    this.page++;
    (this.store as unknown as { hydrateHistory: (arg: { page:number; pageSize:number })=>void }).hydrateHistory({ page: this.page, pageSize: this.pageSize });
  }

  goCatalog() { this.router.navigate(['/player/rewards']); }
}
