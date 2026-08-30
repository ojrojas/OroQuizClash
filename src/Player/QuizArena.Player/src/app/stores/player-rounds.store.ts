import { computed, inject } from '@angular/core';
import { signalStore, withState, withComputed, withMethods, withProps, patchState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap, tap, debounceTime, Subscription } from 'rxjs';
import { tapResponse } from '@ngrx/operators';
import { GamesApi } from '../features/shared/games.api';
import { GameRealtimeService } from '../core/realtime/game-realtime.service';
import { buildLadder, LadderState, LadderRow, RewardRule, SecuredPoints } from '../features/game/ladder.model';

function safeUUID(): string {
  try { if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') return crypto.randomUUID(); } catch {}
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => { const r=(Math.random()*16)|0; const v=c==='x'?r:(r&0x3)|0x8; return v.toString(16); });
}

const initialState: LadderState = {
  gameId: null,
  maxRounds: 10,
  currentRoundNumber: null,
  ladder: [],
  secured: null,
  rewardRules: [],
  status: 'loading',
  correlationId: undefined,
  errorDetail: undefined,
  _animatingRound: null,
  previousRoundNumber: null,
};

export const PlayerRoundsStore = signalStore(
  withState<LadderState>(initialState),

  withProps(() => ({
    _api: inject(GamesApi),
    _realtime: inject(GameRealtimeService),
  })),

  withComputed(({ ladder, currentRoundNumber, secured, rewardRules, maxRounds, _animatingRound, status }) => ({
    currentLevel: computed<LadderRow | null>(() => ladder().find(r => r.roundNumber === currentRoundNumber()) ?? null),
    previousLevels: computed<LadderRow[]>(() => {
      const cur = currentRoundNumber();
      if (cur == null) return [];
      return ladder().filter(r => r.roundNumber < cur);
    }),
    nextReward: computed<RewardRule | null>(() => {
      const cur = currentRoundNumber();
      if (cur == null) return null;
      return rewardRules().find(r => r.roundThreshold === cur + 1) ?? null;
    }),
    securedReward: computed<SecuredPoints | null>(() => secured()),
    finalReward: computed<RewardRule | null>(() => rewardRules().find(r => r.roundThreshold === maxRounds()) ?? null),
    finalRow: computed<LadderRow | null>(() => ladder()[ladder().length - 1] ?? null),
    announcement: computed<string>(() => {
      const cur = currentRoundNumber();
      if (cur == null) return '';
      const row = ladder().find(r => r.roundNumber === cur);
      if (!row) return '';
      const curReward = row.currentReward ?? '—';
      const securedPts = secured()?.securedPoints ?? 0;
      const chk = secured()?.checkpointRoundNumber;
      if (status() === 'terminal') return `Juego terminado en ronda ${cur}. Asegurado ${securedPts} pts.`;
      if (_animatingRound() === cur) return `Avanzaste a ronda ${cur}. Recompensa actual ${curReward}. ${chk ? `Asegurado en ronda ${chk}.` : ''}`;
      return `Ronda actual ${cur} de ${maxRounds()}, nivel ${row.level}`;
    }),
    isEmpty: computed(() => status() === 'empty'),
    isError: computed(() => status() === 'error'),
    isTerminal: computed(() => status() === 'terminal'),
  })),

  withMethods((store) => ({
    hydrateLadder: rxMethod<string>(pipe(
      tap((gameId: string) => {
        patchState(store, { gameId, status: 'loading' as const, correlationId: safeUUID() });
      }),
      switchMap((gameId: string) => (store as any)._api.getMyState(gameId).pipe(
        tapResponse({
          next: (state: any) => {
            const game: any = state.game;
            const gs: any = state.gameSession;
            const secured: any = state.securedPoints;
            const rounds: any[] = state.rounds ?? (state.round ? [state.round] : []);
            const cfg: any = game?.configuration ?? game?.Configuration ?? {};
            const maxRounds: number = cfg.maxRounds ?? cfg.MaxRounds ?? game?.maxRounds ?? 10;
            const current: number | null = gs?.currentRoundNumber ?? gs?.CurrentRoundNumber ?? null;
            const pointsPerRound: number | undefined = cfg.pointsPerRound ?? cfg.PointsPerRound;
            const rawRulesInput: any = cfg.rewardRules ?? cfg.RewardRules ?? cfg.reward ?? cfg.Reward ?? [];
            const rawArray: any[] = Array.isArray(rawRulesInput) ? rawRulesInput : (rawRulesInput && typeof rawRulesInput === 'object' && rawRulesInput.type ? [rawRulesInput] : []);
            const rewardRules: RewardRule[] = rawArray.map((r: any) => ({
              rewardId: r.rewardId ?? r.RewardId ?? r.id ?? '',
              roundThreshold: r.roundThreshold ?? r.RoundThreshold ?? r.threshold ?? r.Threshold ?? 0,
              name: r.name ?? r.Name ?? r.type ?? r.Type ?? '',
              pointsRequired: r.pointsRequired ?? r.PointsRequired ?? r.points ?? r.threshold ?? r.Threshold ?? 0,
            })).filter((r: RewardRule) => r.roundThreshold > 0);

            const securedPoints: SecuredPoints | null = secured ? {
              playerId: secured.playerId ?? secured.PlayerId ?? '',
              gameId: secured.gameId ?? secured.GameId ?? gameId,
              securedPoints: secured.securedPoints ?? secured.SecuredPoints ?? 0,
              checkpointRoundNumber: secured.checkpointRoundNumber ?? secured.CheckpointRoundNumber ?? null,
              policy: secured.policy ?? secured.Policy ?? 'KEEP_SECURED_SCORE',
            } : null;

            const statusDto: any = state.status;
            const isTerminal = (statusDto?.isTerminal ?? statusDto?.IsTerminal ?? (gs?.status === 'WITHDRAWN' || gs?.status === 'ELIMINATED')) as boolean;

            const roundLites = (rounds as any[]).map((r: any) => ({
              roundId: r.roundId ?? r.RoundId ?? null,
              roundNumber: r.roundNumber ?? r.RoundNumber ?? 0,
              level: r.level ?? r.Level ?? '',
              difficulty: r.difficulty ?? r.Difficulty ?? null,
              status: r.status ?? r.Status ?? null,
            })).filter(r => r.roundNumber > 0);

            const previous = store.currentRoundNumber();
            const ladder = buildLadder(maxRounds, roundLites, rewardRules, securedPoints, current, pointsPerRound);

            let ladderStatus: LadderState['status'] = 'ready';
            if (current == null) ladderStatus = 'empty';
            else if (isTerminal) ladderStatus = 'terminal';
            else ladderStatus = 'ready';

            let animating: number | null = null;
            if (previous != null && current != null && previous !== current) {
              if (Math.abs(current - previous) > 1) {
                animating = current;
              } else if (current > previous) {
                animating = current;
              } else {
                animating = current;
              }
              setTimeout(() => patchState(store, { _animatingRound: null }), 350);
            }

            patchState(store, {
              maxRounds,
              currentRoundNumber: current,
              ladder,
              secured: securedPoints,
              rewardRules,
              status: ladderStatus,
              previousRoundNumber: previous,
              _animatingRound: animating,
              errorDetail: undefined,
            });
          },
          error: (err: unknown) => {
            const e = err as { detail?: string; title?: string; correlationId?: string; traceId?: string; message?: string; error?: { detail?: string; title?: string; correlationId?: string; traceId?: string } };
            const detail = e?.detail ?? e?.title ?? (e as unknown as { error?: { detail?: string; title?: string } })?.error?.detail ?? (e as unknown as { error?: { detail?: string; title?: string } })?.error?.title ?? e?.message ?? 'Error al cargar progresión';
            const corr = e?.correlationId ?? e?.traceId ?? (e as unknown as { error?: { correlationId?: string; traceId?: string } })?.error?.correlationId ?? ((store as unknown as { correlationId?: () => string }).correlationId?.() ?? '') as string;
            patchState(store, { status: 'error' as const, errorDetail: detail, correlationId: corr, _animatingRound: null });
          },
        })
      ))
    )),

    hydrateFor(gameId: string) {
      (store as any).hydrateLadder(gameId);
    },

    clearError() {
      patchState(store, { status: store.currentRoundNumber() == null ? 'empty' as const : 'ready' as const, errorDetail: undefined });
    },

    _setState(patch: Partial<LadderState>) {
      patchState(store, patch as any);
    }
  })),

  withMethods((store) => ({
    bindRealtimeLadder(gameId: string) {
      const prev = (store as unknown as { _ladderSub?: Subscription })._ladderSub;
      if (prev) try { prev.unsubscribe(); } catch {}
      const sub = (store as unknown as { _realtime: GameRealtimeService })._realtime.events$
        .pipe(debounceTime(100))
        .subscribe((evt: unknown) => {
          const t = (evt as { type?: string })?.type;
          if (['GameStarted', 'PlayerJoined', 'RoundStarted', 'QuestionAvailable', 'QuestionPresented', 'PlayerAnswered', 'ScoreUpdated', 'LeaderboardUpdated', 'RoundCompleted', 'GameFinished', 'PlayerWithdrawn', 'PlayerStatusChanged', 'Reconnected'].includes(t as string)) {
            // Use hydrateFor which delegates to hydrateLadder safely after store is fully built
            try { (store as unknown as { hydrateFor: (id: string)=>void }).hydrateFor(gameId); } catch {}
          }
        });
      (store as unknown as { _ladderSub: Subscription })._ladderSub = sub;
      return sub;
    },
  }))
);
