import { Component, inject, OnInit, signal, computed, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { PlayerRewardsStore } from '../../stores/player-rewards.store';
import { deriveRewardStatus, formatRemaining } from './rewards-display.model';
import { LoadingSkeletonComponent } from '../../shared/ui/loading-skeleton.component';
import { ErrorStateComponent } from '../../shared/ui/error-state.component';

@Component({
  selector: 'app-reward-detail',
  standalone: true,
  imports: [CommonModule, LoadingSkeletonComponent, ErrorStateComponent],
  template: `
    <div class="detail" data-theme="player">
      @if (store.isHydrating()) {
        <app-loading-skeleton [rows]="3" />
      } @else if (store.error()) {
        <app-error-state [message]="store.error()!.detail" [correlationId]="store.error()!.correlationId" [traceId]="store.error()!.traceId" (retry)="hydrate()" />
      } @else if (!selected()) {
        <div role="status" aria-live="polite">Recompensa no encontrada</div>
      } @else {
        <h2>{{ selected()!.name }}</h2>
        <p>{{ selected()!.description }}</p>
        <div class="metrics" role="group" aria-label="Puntuaciones" style="display:flex; flex-direction:column; gap:var(--space-2,8px);">
          <div role="status" aria-live="polite" [attr.aria-label]="'Available Points ' + (store.wallet().availablePoints ?? 0)">Available Points {{ store.wallet().availablePoints ?? 0 }} pts</div>
          <div role="status" aria-live="polite" [attr.aria-label]="'Required Points ' + selected()!.pointsRequired">Required Points {{ selected()!.pointsRequired }} pts</div>
          <div role="status" aria-live="polite" [attr.aria-label]="'Remaining Points ' + remainingDisplay()">{{ remainingDisplay() }}</div>
          <div class="badge" [class.canjeable]="rewardStatus() === 'Canjeable'" [class.insuficiente]="rewardStatus() === 'Puntos insuficientes'" [class.agotada]="rewardStatus() === 'Agotada'" role="status" [attr.aria-label]="rewardStatus()">{{ rewardStatus() }}</div>
        </div>

        @if (showSuccess()) {
          <div role="status" aria-live="assertive" style="padding:var(--space-3,12px); border:1px solid var(--color-success,#22c55e); border-radius:var(--radius-md,8px); background:var(--color-surface,#FFF);">
            <h3>¡Canje realizado!</h3>
            <div>Consumidos {{ selected()!.pointsRequired }} pts</div>
            <div>Restantes {{ store.wallet().availablePoints ?? 0 }} pts</div>
            <div>Referencia {{ lastRedemptionId() }}</div>
            <div>Estado Canjeada</div>
            <div style="display:flex; gap:var(--space-3,12px); margin-top:var(--space-3,12px);">
              <button (click)="goHistory()" style="min-height:44px; min-width:44px;">Ver historial</button>
              <button (click)="goCatalog()" style="min-height:44px; min-width:44px;">Seguir explorando</button>
            </div>
          </div>
        } @else {
          <button (click)="openConfirm()" [disabled]="!isRedeemable()" style="min-height:44px; min-width:44px;" aria-label="Canjear recompensa">
            Canjear
          </button>
          @if (!isRedeemable()) {
            <small role="status" aria-live="polite">Necesitas {{ missing() }} puntos más</small>
          }
        }

        @if (showConfirm()) {
          <div role="dialog" aria-modal="true" aria-label="Confirmar canje" style="position:fixed; inset:0; background:rgba(0,0,0,0.5); display:flex; align-items:center; justify-content:center;" (click)="showConfirm.set(false)">
            <div style="background:var(--color-surface,#FFF); padding:var(--space-6,24px); border-radius:var(--radius-lg,12px); max-width:400px; display:flex; flex-direction:column; gap:var(--space-3,12px); border:1px solid var(--color-border,#DBEAFE);" (click)="$event.stopPropagation()" role="document">
              <h2>Confirmar canje</h2>
              <div role="group" aria-label="Resumen canje" style="display:flex; flex-direction:column; gap:var(--space-2,8px);">
                <div>Disponible {{ store.wallet().availablePoints ?? 0 }} pts</div>
                <div>Required {{ selected()!.pointsRequired }} pts</div>
                <div>Restantes {{ remainingDisplay() }}</div>
              </div>
              <div role="alert" aria-live="assertive" style="color:var(--color-warning,#D97706); font-weight:600;">¿Confirmar canje de {{ selected()!.name }} por {{ selected()!.pointsRequired }} puntos?</div>
              <div style="display:flex; gap:var(--space-3,12px); justify-content:flex-end;">
                <button (click)="confirm()" style="min-height:44px; min-width:44px;" aria-label="Confirmar canje">Confirmar</button>
                <button (click)="showConfirm.set(false)" style="min-height:44px; min-width:44px;" aria-label="Cancelar">Cancelar</button>
              </div>
              @if (store.redeemStatus() && store.redeemStatus() !== 'IDLE' && store.redeemStatus() !== 'LOADING' && store.redeemStatus() !== 'SUCCESS') {
                <div role="alert" aria-live="assertive" style="color:var(--color-destructive,#DC2626);">Error: {{ (store.redeemStatus() | json) }} CorrelationId: {{ store.error()?.correlationId }}</div>
              }
            </div>
          </div>
        }

        @if (isError()) {
          <div role="alert" aria-live="assertive" style="color:var(--color-destructive,#DC2626);">Error: {{ errorDetail() }} CorrelationId: {{ store.error()?.correlationId }}</div>
        }
      }
    </div>
  `,
  styles: [`
    .detail { max-width:600px; margin:0 auto; padding:var(--space-6,24px); display:flex; flex-direction:column; gap:var(--space-4,16px); }
    .badge { padding:4px 8px; border-radius:var(--radius-full,999px); font-size:var(--text-sm,14px); font-weight:600; width:fit-content; }
    .badge.canjeable { background:var(--color-success,#22c55e); color:white; }
    .badge.insuficiente { background:var(--color-warning,#D97706); color:white; }
    .badge.agotada { background:var(--color-destructive,#DC2626); color:white; }
    button { min-height:44px; min-width:44px; }
    @media (prefers-reduced-motion: reduce){ *{ animation:none !important; } }
  `]
})
export class RewardDetailComponent implements OnInit {
  store = inject(PlayerRewardsStore);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  showConfirm = signal(false);
  showSuccess = signal(false);
  lastRedemptionId = signal<string>('');

  constructor() {
    effect(() => {
      const status = this.store.redeemStatus();
      if (status === 'SUCCESS' && !this.showSuccess()) {
        const last = this.store.history()[0];
        if (last) this.lastRedemptionId.set(last.id);
        this.showSuccess.set(true);
      }
    });
  }

  selected = computed(() => {
    const id = this.route.snapshot.paramMap.get('rewardId');
    return this.store.catalog().find(r => r.id === id) ?? null;
  });

  isRedeemable = computed(() => {
    const sel = this.selected();
    if (!sel) return false;
    const available = this.store.wallet().availablePoints;
    return sel.available && (available ?? 0) >= sel.pointsRequired;
  });

  rewardStatus = computed(() => {
    const sel = this.selected();
    if (!sel) return 'No disponible';
    return deriveRewardStatus(this.store.wallet().availablePoints, sel.pointsRequired, sel.available, sel.stock);
  });

  remainingDisplay = computed(() => {
    const sel = this.selected();
    if (!sel) return '—';
    const available = this.store.wallet().availablePoints;
    if (this.rewardStatus() === 'Canjeable') {
      const diff = (available ?? 0) - sel.pointsRequired;
      return `${diff} pts`;
    }
    if (available != null) return `Te faltan ${Math.abs((available ?? 0) - sel.pointsRequired)} pts`;
    return formatRemaining(available, sel.pointsRequired, sel.available);
  });

  missing = computed(() => {
    const sel = this.selected();
    if (!sel) return 0;
    return Math.abs((this.store.wallet().availablePoints ?? 0) - sel.pointsRequired);
  });

  isError = computed(() => {
    const s = this.store.redeemStatus() as any;
    return s && s !== 'IDLE' && s !== 'LOADING' && s !== 'SUCCESS' && typeof s === 'object';
  });

  errorDetail = computed(() => {
    const s = this.store.redeemStatus() as any;
    return s?.detail ?? s?.title ?? 'Error';
  });

  ngOnInit() {
    const gameId = this.route.snapshot.queryParamMap.get('gameId') ?? undefined;
    if (gameId) this.store.hydrateFor(gameId);
    else this.store.hydrate(undefined);
  }

  hydrate() {
    const gameId = this.route.snapshot.queryParamMap.get('gameId') ?? undefined;
    if (gameId) this.store.hydrateFor(gameId);
    else this.store.hydrate(undefined);
  }

  openConfirm() {
    if (!this.isRedeemable()) return;
    this.showConfirm.set(true);
  }

  confirm() {
    const sel = this.selected();
    if (!sel) return;
    this.showConfirm.set(false);
    this.showSuccess.set(false);
    this.store.redeem(sel.id);
  }

  goHistory() { this.router.navigate(['/player/rewards/history']); }
  goCatalog() { this.router.navigate(['/player/rewards']); }
}
