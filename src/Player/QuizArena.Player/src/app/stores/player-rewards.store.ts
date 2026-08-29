import { computed, inject } from '@angular/core';
import { signalStore, withState, withComputed, withMethods, withProps, patchState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap } from 'rxjs';
import { tapResponse } from '@ngrx/operators';
import { RewardsApi, RewardView, RedemptionItem } from '../features/shared/rewards.api';
import { ProblemDetails } from '../core/interceptors/error.interceptor';

export type WalletState = {
  availablePoints: number | null;
  lastUpdated: string | null;
  gameId: string | null;
};

export type PlayerRewardsState = {
  wallet: WalletState;
  catalog: RewardView[];
  selectedReward: RewardView | null;
  history: RedemptionItem[];
  redeemStatus: 'IDLE' | 'LOADING' | 'SUCCESS' | ProblemDetails | null;
  isHydrating: boolean;
  error: ProblemDetails | null;
  _pendingGameId: string | null;
};

const initialState: PlayerRewardsState = {
  wallet: { availablePoints: null, lastUpdated: null, gameId: null },
  catalog: [],
  selectedReward: null,
  history: [],
  redeemStatus: 'IDLE',
  isHydrating: false,
  error: null,
  _pendingGameId: null,
};

export const PlayerRewardsStore = signalStore(
  withState(initialState),
  withProps(() => ({
    _api: inject(RewardsApi),
  })),
  withComputed(({ wallet, catalog }) => ({
    availablePoints: computed(() => wallet().availablePoints),
    isRedeemable: computed(() => {
      return (rewardId: string) => {
        const reward = catalog().find(r => r.id === rewardId);
        if (!reward) return false;
        const available = wallet().availablePoints ?? 0;
        return reward.available && available >= reward.pointsRequired;
      };
    }),
    remainingPointsFor: computed(() => {
      return (rewardId: string) => {
        const reward = catalog().find(r => r.id === rewardId);
        if (!reward) return null;
        const available = wallet().availablePoints ?? 0;
        return available - reward.pointsRequired;
      };
    }),
    remainingDisplay: computed(() => {
      return (rewardId: string) => {
        const reward = catalog().find(r => r.id === rewardId);
        if (!reward) return '—';
        const available = wallet().availablePoints ?? 0;
        const diff = available - reward.pointsRequired;
        if (reward.available && available >= reward.pointsRequired) return `${diff} pts`;
        return `Te faltan ${Math.abs(diff)} pts`;
      };
    }),
    rewardStatus: computed(() => {
      return (rewardId: string) => {
        const reward = catalog().find(r => r.id === rewardId);
        if (!reward) return 'No disponible';
        if (!reward.available && reward.stock === 0) return 'Agotada';
        if (!reward.available) return 'No disponible';
        const available = wallet().availablePoints ?? 0;
        if (available >= reward.pointsRequired) return 'Canjeable';
        return 'Puntos insuficientes';
      };
    }),
  })),
  withMethods((store) => ({
    hydrate: rxMethod<string | void>(pipe(
      switchMap((gameId) => {
        const gid = (typeof gameId === 'string' ? gameId : null) ?? store._pendingGameId ?? undefined;
        patchState(store, { isHydrating: true, error: null } as any);
        return (store as any)._api.getRewards(gid);
      }),
      tapResponse({
        next: (res: any) => {
          patchState(store, {
            catalog: res.rewards ?? [],
            wallet: { availablePoints: res.availablePoints ?? null, lastUpdated: new Date().toISOString(), gameId: res.gameId ?? null },
            isHydrating: false,
            error: null,
          } as any);
        },
        error: (err: ProblemDetails) => patchState(store, { isHydrating: false, error: err } as any),
      })
    )),
    hydrateFor(gameId: string) {
      patchState(store, { _pendingGameId: gameId } as any);
      (store as any).hydrate(gameId);
    },
    hydrateHistory: rxMethod<void>(pipe(
      switchMap(() => (store as any)._api.getMyRedemptions()),
      tapResponse({
        next: (res: any) => patchState(store, { history: res.redemptions ?? [] } as any),
        error: (err: ProblemDetails) => patchState(store, { error: err } as any),
      })
    )),
    redeem: rxMethod<string>(pipe(
      switchMap((rewardId: string) => {
        const gameId = store.wallet().gameId ?? store._pendingGameId ?? '';
        const storageKey = `idemp-redeem-${rewardId}`;
        const key = sessionStorage.getItem(storageKey) ?? crypto.randomUUID();
        sessionStorage.setItem(storageKey, key);
        patchState(store, { redeemStatus: 'LOADING' } as any);
        return (store as any)._api.redeem(rewardId, key, gameId);
      }),
      tapResponse({
        next: (res: any) => {
          const currentWallet = store.wallet();
          const newAvailable = currentWallet.availablePoints != null ? currentWallet.availablePoints - res.points : null;
          patchState(store, {
            redeemStatus: 'SUCCESS' as any,
            wallet: { ...currentWallet, availablePoints: newAvailable, lastUpdated: new Date().toISOString() },
            history: [ { id: res.redemptionId, rewardId: res.rewardId, gameId: res.gameId, points: res.points, status: res.status, requestedAt: res.requestedAt, deliveredAt: null }, ...store.history()],
          } as any);
        },
        error: (err: ProblemDetails) => patchState(store, { redeemStatus: err } as any),
      })
    )),
    clearError() { patchState(store, { error: null, redeemStatus: 'IDLE' } as any); },
  }))
);
