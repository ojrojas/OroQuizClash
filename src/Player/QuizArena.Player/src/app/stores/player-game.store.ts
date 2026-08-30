// @ts-nocheck
import { computed, inject } from '@angular/core';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStore, withComputed, withMethods, withProps, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { interval, map, pipe, switchMap, tap } from 'rxjs';
import { ProblemDetails } from '../core/interceptors/error.interceptor';
import { GameRealtimeService } from '../core/realtime/game-realtime.service';
import { GamesApi } from '../features/shared/games.api';
import { Answer, Game, GameSession, Player, PlayerGameStatus, Question, Round, Score, SecuredPoints, Timer } from '../features/shared/models/player.models';

type PlayerGameState = {
  player: Player | null;
  game: Game | null;
  gameSession: GameSession | null;
  round: Round | null;
  question: Question | null;
  answer: Answer | null;
  score: Score;
  securedPoints: SecuredPoints;
  timer: Timer;
  status: PlayerGameStatus;
  ui: { isLoading: boolean; error: ProblemDetails | null; isHydrating: boolean };
  _now: number;
  _tickSub: any;
  _isPulse: boolean;
};

const initialState: PlayerGameState = {
  player: null,
  game: null,
  gameSession: null,
  round: null,
  question: null,
  answer: null,
  score: { playerId: '', gameId: '', totalPoints: 0, correctAnswers: 0, currentLevel: 'Basic' },
  securedPoints: { playerId: '', gameId: '', securedPoints: 0, checkpointRoundNumber: null, policy: 'KEEP_SECURED_SCORE' },
  timer: { timeLimitSeconds: 30, expiresAt: new Date().toISOString(), remainingSeconds: 0, state: 'STOPPED', serverNow: new Date().toISOString() },
  status: { gameStatus: 'WAITING_FOR_PLAYERS', playerStatus: 'ACTIVE', isTerminal: false, canAnswer: false },
  ui: { isLoading: false, error: null, isHydrating: false },
  _now: Date.now(),
  _tickSub: null,
  _isPulse: false,
};

export const PlayerGameStore = signalStore(
  withState(initialState),

  withProps(() => ({
    _api: inject(GamesApi),
    _realtime: inject(GameRealtimeService),
  })),

  withComputed(({ timer, _now, status, round, answer, score, securedPoints, game, _isPulse }) => ({
    remainingSeconds: computed(() => Math.max(0, Math.floor((new Date(timer().expiresAt).getTime() - _now()) / 1000))),
    isExpired: computed(() => timer().state === 'EXPIRED' || (timer().state === 'RUNNING' && new Date(timer().expiresAt).getTime() <= _now())),
    isTerminal: computed(() => status().isTerminal),
    canAnswer: computed(() => status().canAnswer && round()?.status === 'IN_PROGRESS' && answer()?.state === 'PENDING'),
    displayScore: computed(() => `${status().isTerminal ? 'Final' : ''} ${status().gameStatus}`),
    potentialReward: computed(() => {
      const g: any = game();
      const rewardName = g?.configuration?.rewardRules?.rewardId ? 'Pack Oro' : null;
      if (!rewardName) return '—';
      const nextThreshold = 500;
      const current = score().totalPoints;
      return current >= nextThreshold ? '¡Recompensa alcanzada!' : `Próximo: ${rewardName} ${nextThreshold} pts`;
    }),
    currentRoundDisplay: computed(() => {
      const gs: any = status();
      const g: any = game();
      const max = g?.configuration?.maxRounds ?? 10;
      const cur = (g as any)?.currentRoundNumber ?? round()?.roundNumber ?? 0;
      return `Ronda ${cur}/${max}`;
    }),
    isScorePulse: computed(() => _isPulse()),
    isSecured: computed(() => {
      const sp = securedPoints();
      return sp.securedPoints > 0 && sp.checkpointRoundNumber != null;
    }),
  })),

  withMethods((store) => ({
    hydrate: rxMethod<void>(pipe(
      tap(() => patchState(store, { ui: { isLoading: true, error: null, isHydrating: true } })),
      switchMap(() => {
        const gameId = store.game()?.gameId ?? (store as any)._pendingGameId ?? '';
        return (store as any)._api.getMyState(gameId);
      }),
      tapResponse({
        next: (state: any) => {
          // correct _now on each hydrate to fix drift (R5)
          const serverNow = state.timer?.serverNow ? new Date(state.timer.serverNow).getTime() : Date.now();
          patchState(store, {
            player: state.player,
            game: state.game,
            gameSession: state.gameSession,
            round: state.round,
            question: state.question,
            answer: state.answer,
            score: state.score,
            securedPoints: state.securedPoints,
            timer: state.timer,
            status: state.status,
            _now: serverNow,
            ui: { isLoading: false, error: null, isHydrating: false }
          } as any);
        },
        error: (err: ProblemDetails) => patchState(store, { ui: { isLoading: false, error: err, isHydrating: false } }),
      })
    )),

    hydrateFor(gameId: string) {
      (store as any)._pendingGameId = gameId;
      (store as any).hydrate();
    },

    submitAnswer: rxMethod<string>(pipe(
      switchMap((selectedOptionId: string) => {
        const roundId = store.round()?.roundId ?? '';
        const key = sessionStorage.getItem(`idemp-${roundId}`) ?? crypto.randomUUID();
        sessionStorage.setItem(`idemp-${roundId}`, key);
        return (store as any)._api.submitAnswer(store.game()!.gameId, {
          roundId: store.round()!.roundId,
          questionId: store.question()!.questionId,
          selectedOptionId,
          idempotencyKey: key
        });
      }),
      tapResponse({
        next: (answer: Answer) => patchState(store, { answer, timer: { ...store.timer(), state: 'STOPPED' as const } }),
        error: (err: ProblemDetails) => patchState(store, { ui: { ...store.ui(), error: err } }),
      })
    )),

    withdraw: rxMethod<void>(pipe(
      switchMap(() => {
        const gameId = store.game()?.gameId ?? (store as any)._pendingGameId ?? '';
        const storageKey = `idemp-withdraw-${gameId}`;
        const key = sessionStorage.getItem(storageKey) ?? crypto.randomUUID();
        sessionStorage.setItem(storageKey, key);
        return (store as any)._api.withdraw(gameId, key);
      }),
      tapResponse({
        next: (gs: GameSession) => patchState(store, { gameSession: gs, status: { ...store.status(), playerStatus: 'WITHDRAWN', isTerminal: true, canAnswer: false } }),
        error: (err: ProblemDetails) => patchState(store, { ui: { ...store.ui(), error: err } }),
      })
    )),

    startTimerTick() {
      if ((store as any)._tickSub) return;
      const sub = interval(1000).pipe(map(() => Date.now())).subscribe(now => patchState(store, { _now: now }));
      patchState(store, { _tickSub: sub } as any);
      return sub;
    },

    stopTimerTick() {
      const sub = (store as any)._tickSub;
      if (sub) { sub.unsubscribe(); patchState(store, { _tickSub: null } as any); }
    },

    bindRealtime(gameId: string, accessTokenFactory: () => string) {
      (store as any)._realtime.connect(gameId, accessTokenFactory);
      (store as any)._realtime.events$.subscribe((evt: any) => {
        if (['QuestionAvailable', 'ScoreUpdated', 'RoundCompleted', 'GameFinished', 'PlayerWithdrawn'].includes(evt.type)) {
          if (evt.type === 'ScoreUpdated') {
            patchState(store, { _isPulse: true } as any);
            setTimeout(() => patchState(store, { _isPulse: false } as any), 600);
          }
          (store as any).hydrateFor(gameId);
        }
        if (evt.type === 'Reconnected') (store as any).hydrateFor(gameId);
      });
    },

    clearError() { patchState(store, { ui: { ...store.ui(), error: null } }); },
  }))
);
