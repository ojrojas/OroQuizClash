// @ts-nocheck
import { computed, inject } from '@angular/core';
import { signalStore, withState, withComputed, withMethods, withProps, patchState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap, tap, debounceTime } from 'rxjs';
import { tapResponse } from '@ngrx/operators';
import { GamesApi } from '../features/shared/games.api';
import { GameRealtimeService } from '../core/realtime/game-realtime.service';
import { buildLadder, LadderState, LadderRow, RewardRule, SecuredPoints } from '../features/game/ladder.model';

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
        // keep gameId for retry
        patchState(store, { gameId, status: 'loading' as const, correlationId: crypto.randomUUID() });
      }),
      switchMap((gameId: string) => (store as any)._api.getMyState(gameId).pipe(
        // no explicit correlation header here; interceptor adds it
        tapResponse({
          next: (state: any) => {
            const game: any = state.game;
            const gs: any = state.gameSession;
            const secured: any = state.securedPoints;
            const rounds: any[] = state.rounds ?? (state.round ? [state.round] : []);
            // maxRounds from Configuration (object) or maxPlayers fallback
            const cfg: any = game?.configuration ?? game?.Configuration ?? {};
            const maxRounds: number = cfg.maxRounds ?? cfg.MaxRounds ?? game?.maxRounds ?? 10;
            const current: number | null = gs?.currentRoundNumber ?? gs?.CurrentRoundNumber ?? null;
            const pointsPerRound: number | undefined = cfg.pointsPerRound ?? cfg.PointsPerRound;
            // rewardRules from cfg.rewardRules if exists
            const rawRules: any[] = cfg.rewardRules ?? cfg.RewardRules ?? [];
            const rewardRules: RewardRule[] = rawRules.map((r: any) => ({
              rewardId: r.rewardId ?? r.RewardId,
              roundThreshold: r.roundThreshold ?? r.RoundThreshold ?? 0,
              name: r.name ?? r.Name ?? '',
              pointsRequired: r.pointsRequired ?? r.PointsRequired ?? r.points ?? 0,
            })).filter((r: RewardRule) => r.roundThreshold > 0);

            const securedPoints: SecuredPoints | null = secured ? {
              playerId: secured.playerId ?? secured.PlayerId ?? '',
              gameId: secured.gameId ?? secured.GameId ?? gameId,
              securedPoints: secured.securedPoints ?? secured.SecuredPoints ?? 0,
              checkpointRoundNumber: secured.checkpointRoundNumber ?? secured.CheckpointRoundNumber ?? null,
              policy: secured.policy ?? secured.Policy ?? 'KEEP_SECURED_SCORE',
            } : null;

            // isTerminal from status
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

            // determine status
            let ladderStatus: LadderState['status'] = 'ready';
            if (current == null) ladderStatus = 'empty';
            else if (isTerminal) ladderStatus = 'terminal';
            else ladderStatus = 'ready';

            // handle animating round with jump detection
            let animating: number | null = null;
            if (previous != null && current != null && previous !== current) {
              if (Math.abs(current - previous) > 1) {
                // reconnect jump: direct, no intermediate anim but still show current as animating briefly
                animating = current;
              } else if (current > previous) {
                animating = current;
              } else {
                // rollback correction
                animating = current;
              }
              // auto clear after 350ms
              setTimeout(() => patchState(store, { _animatingRound: null }), 350);
              if (animating != null) {
                // set immediately, will be cleared
                // patch will be done below with animating
              }
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
            // if animating was set, ensure timeout clears
            if (animating != null) {
              // already scheduled
            }
          },
          error: (err: any) => {
            const detail = err?.error?.detail ?? err?.error?.title ?? err?.message ?? 'Error al cargar progresión';
            const corr = err?.error?.correlationId ?? err?.error?.traceId ?? store.correlationId();
            patchState(store, { status: 'error' as const, errorDetail: detail, correlationId: corr, _animatingRound: null });
          },
        })
      ))
    )),

    hydrateFor(gameId: string) {
      (store as any).hydrateLadder(gameId);
    },

    bindRealtimeLadder(gameId: string) {
      // Reuse GameRealtimeService events$ ; hydrate on relevant events
      // debounce rapid events to avoid duplicate hydrates
      (store as any)._realtime.events$
        .pipe(debounceTime(100))
        .subscribe((evt: any) => {
          if (['RoundCompleted', 'QuestionAvailable', 'ScoreUpdated', 'GameFinished', 'RoundStarted', 'Reconnected'].includes(evt?.type)) {
            (store as any).hydrateLadder(gameId);
          }
        });
      // Also trigger initial hydrate if needed
    },

    clearError() {
      patchState(store, { status: store.currentRoundNumber() == null ? 'empty' as const : 'ready' as const, errorDetail: undefined });
    },

    // for tests to set state directly
    _setState(patch: Partial<LadderState>) {
      patchState(store, patch as any);
    }
  }))
);
