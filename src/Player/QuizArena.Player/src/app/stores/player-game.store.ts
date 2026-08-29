import { computed, inject } from '@angular/core';
import { signalStore, withState, withComputed, withMethods, withProps, patchState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap, tap, interval, map } from 'rxjs';
import { tapResponse } from '@ngrx/operators';
import { GamesApi } from '../features/shared/games.api';
import { GameRealtimeService } from '../core/realtime/game-realtime.service';
import { Player, Game, GameSession, Round, Question, Answer, Score, SecuredPoints, Timer, PlayerGameStatus } from '../features/shared/models/player.models';
import { ProblemDetails } from '../core/interceptors/error.interceptor';

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
};

export const PlayerGameStore = signalStore(
  withState(initialState),

  withProps(() => ({
    _api: inject(GamesApi),
    _realtime: inject(GameRealtimeService),
  })),

  withComputed(({ timer, _now, status, round, answer }) => ({
    remainingSeconds: computed(() => Math.max(0, Math.floor((new Date(timer().expiresAt).getTime() - _now()) / 1000))),
    isExpired: computed(() => timer().state === 'EXPIRED' || (timer().state === 'RUNNING' && new Date(timer().expiresAt).getTime() <= _now())),
    isTerminal: computed(() => status().isTerminal),
    canAnswer: computed(() => status().canAnswer && round()?.status === 'IN_PROGRESS' && answer()?.state === 'PENDING'),
    displayScore: computed(() => `${status().isTerminal ? 'Final' : ''} ${status().gameStatus}`),
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
      switchMap(() => (store as any)._api.withdraw(store.game()!.gameId)),
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
        if (['QuestionAvailable', 'ScoreUpdated', 'RoundCompleted', 'GameFinished'].includes(evt.type)) {
          (store as any).hydrateFor(gameId);
        }
        if (evt.type === 'Reconnected') (store as any).hydrateFor(gameId);
      });
    },

    clearError() { patchState(store, { ui: { ...store.ui(), error: null } }); },
  }))
);
