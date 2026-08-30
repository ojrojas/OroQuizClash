import { computed, inject } from '@angular/core';
import { tapResponse } from '@ngrx/operators';
import { patchState, signalStore, withComputed, withMethods, withProps, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { EMPTY, interval, map, pipe, Subject, Subscription, switchMap, tap } from 'rxjs';
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
  _isPulse: false,
};

function safeUUID(): string {
  try { if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') return crypto.randomUUID(); } catch {}
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => { const r = (Math.random() * 16) | 0; return (c === 'x' ? r : (r & 0x3) | 0x8).toString(16); });
}

function safeSessionGet(key: string): string | null { try { return sessionStorage.getItem(key); } catch { return null; } }
function safeSessionSet(key: string, value: string): void { try { sessionStorage.setItem(key, value); } catch {} }

export const PlayerGameStore = signalStore(
  withState(initialState),

  withProps(() => ({
    _api: inject(GamesApi),
    _realtime: inject(GameRealtimeService),
    _tickSub: null as Subscription | null,
    _realtimeSub: null as Subscription | null,
    _pendingGameId: null as string | null,
    _hydrateTrigger: new Subject<string | void>(),
  })),

  withComputed(({ timer, _now, status, round, answer, score, securedPoints, game, _isPulse }) => ({
    remainingSeconds: computed(() => Math.max(0, Math.floor((new Date(timer().expiresAt).getTime() - _now()) / 1000))),
    isExpired: computed(() => timer().state === 'EXPIRED' || (timer().state === 'RUNNING' && new Date(timer().expiresAt).getTime() <= _now())),
    isTerminal: computed(() => status().isTerminal),
    canAnswer: computed(() => status().canAnswer && (round()?.status === 'IN_PROGRESS' || round()?.status === 'ROUND_IN_PROGRESS') && (answer()?.state === 'PENDING' || answer()?.state === 'NOT_ANSWERED' || !answer()?.state)),
    displayScore: computed(() => `${status().isTerminal ? 'Final' : ''} ${status().gameStatus}`),
    potentialReward: computed(() => {
      const g = game() as any;
      const cfg = g?.configuration as Record<string, any> | undefined;
      const rulesRaw = (cfg?.['rewardRules'] ?? cfg?.['RewardRules']) as Array<any> | undefined;
      if (Array.isArray(rulesRaw) && rulesRaw.length > 0) {
        const curPoints = score().totalPoints;
        const next = [...rulesRaw].sort((a: any, b: any) => (a.pointsRequired ?? a.PointsRequired ?? 0) - (b.pointsRequired ?? b.PointsRequired ?? 0)).find((r: any) => (r.pointsRequired ?? r.PointsRequired ?? 0) > curPoints);
        if (next) return `Próximo: ${next.name ?? next.Name ?? 'Pack'} ${next.pointsRequired ?? next.PointsRequired ?? 0} pts`;
        return '¡Recompensa alcanzada!';
      }
      const ppr = (cfg?.['pointsPerRound'] ?? cfg?.['PointsPerRound']) as number | undefined;
      if (ppr) { const roundNum = g?.currentRoundNumber ?? round()?.roundNumber ?? 1; return `Próximo: ${ppr * (roundNum + 1)} pts`; }
      return '—';
    }),
    currentRoundDisplay: computed(() => {
      const g = game() as any;
      const max = g?.configuration?.maxRounds ?? g?.configuration?.MaxRounds ?? 10;
      const cur = g?.currentRoundNumber ?? round()?.roundNumber ?? 0;
      return `Ronda ${cur}/${max}`;
    }),
    isScorePulse: computed(() => _isPulse()),
    isSecured: computed(() => securedPoints().securedPoints > 0 && securedPoints().checkpointRoundNumber != null),
  })),

  withMethods((store) => {
    const hydrateSub = store._hydrateTrigger.pipe(
      tap(() => patchState(store, { ui: { isLoading: true, error: null, isHydrating: true } })),
      switchMap((input) => {
        const gameId = (typeof input === 'string' && input) ? input : store._pendingGameId ?? store.game()?.gameId ?? '';
        if (!gameId) return EMPTY;
        return store._api.getMyState(gameId);
      }),
      tapResponse({
        next: (state: any) => {
          const s = state as Record<string, any>;
          const timerRaw = s['timer'];
          const cfg = s['game']?.['configuration'] as Record<string, any> | undefined;
          let timer: Timer = timerRaw ?? store.timer();
          if (cfg) {
            const tlp = cfg['timeLimitPerQuestion'] ?? cfg['TimeLimitPerQuestion'] ?? cfg['timeLimitPerQuestionSeconds'] ?? cfg['TimeLimitPerQuestionSeconds'];
            if (typeof tlp === 'number') timer = { ...timer, timeLimitSeconds: tlp };
          }
          const serverNow = timerRaw?.serverNow ? new Date(timerRaw.serverNow).getTime() : Date.now();
          patchState(store, {
            player: s['player'] as Player, game: s['game'] as Game, gameSession: s['gameSession'] as GameSession,
            round: s['round'] as Round, question: s['question'] as Question, answer: s['answer'] as Answer,
            score: s['score'] as Score, securedPoints: s['securedPoints'] as SecuredPoints,
            timer, status: s['status'] as PlayerGameStatus, _now: serverNow,
            ui: { isLoading: false, error: null, isHydrating: false }
          } as any);
        },
        error: (err: any) => patchState(store, { ui: { isLoading: false, error: err, isHydrating: false } }),
      })
    ).subscribe();

    return {
      hydrate(gameId?: string) { store._hydrateTrigger.next(gameId); },
      hydrateFor(gameId: string) { store._pendingGameId = gameId; store._hydrateTrigger.next(gameId); },

      submitAnswer: rxMethod<string>(pipe(
        switchMap((selectedOptionId: string) => {
          const game = store.game(), round = store.round(), question = store.question();
          if (!game?.gameId || !round?.roundId || !question?.questionId || !store.canAnswer()) return EMPTY;
          const key = safeSessionGet(`idemp-${round.roundId}`) ?? safeUUID();
          safeSessionSet(`idemp-${round.roundId}`, key);
          return store._api.submitAnswer(game.gameId, { roundId: round.roundId, questionId: question.questionId, selectedOptionId, idempotencyKey: key });
        }),
        tapResponse({
          next: (answer: Answer) => patchState(store, { answer, timer: { ...store.timer(), state: 'STOPPED' as const } }),
          error: (err: any) => patchState(store, { ui: { ...store.ui(), error: err } }),
        })
      )),

      withdraw: rxMethod<void>(pipe(
        switchMap(() => {
          const gameId = store.game()?.gameId ?? store._pendingGameId ?? '';
          if (!gameId) return EMPTY;
          const key = safeSessionGet(`idemp-withdraw-${gameId}`) ?? safeUUID();
          safeSessionSet(`idemp-withdraw-${gameId}`, key);
          return store._api.withdraw(gameId, key);
        }),
        tapResponse({
          next: (gs: GameSession) => patchState(store, {
            gameSession: gs, status: { ...store.status(), playerStatus: 'WITHDRAWN', isTerminal: true, canAnswer: false },
            score: { ...store.score(), totalPoints: store.securedPoints().securedPoints || store.score().totalPoints }
          }),
          error: (err: any) => patchState(store, { ui: { ...store.ui(), error: err } }),
        })
      )),

      startTimerTick() { if (store._tickSub) return; store._tickSub = interval(1000).pipe(map(() => Date.now())).subscribe(now => patchState(store, { _now: now })); },
      stopTimerTick() { if (store._tickSub) { store._tickSub.unsubscribe(); store._tickSub = null; } },
      clearError() { patchState(store, { ui: { ...store.ui(), error: null } }); },
    };
  }),

  withMethods((store) => ({
    bindRealtime(gameId: string, accessTokenFactory: () => string | Promise<string>) {
      if (store._realtimeSub) { store._realtimeSub.unsubscribe(); }
      store._realtime.connect(gameId, accessTokenFactory);
      store._realtimeSub = store._realtime.events$.subscribe((evt) => {
        const type = (evt as { type: string }).type;
        if (['GameStarted', 'PlayerJoined', 'RoundStarted', 'QuestionAvailable', 'QuestionPresented', 'ScoreUpdated', 'LeaderboardUpdated', 'PlayerAnswered', 'RoundCompleted', 'GameFinished', 'PlayerWithdrawn', 'PlayerStatusChanged'].includes(type)) {
          if (type === 'ScoreUpdated') { patchState(store, { _isPulse: true } as any); setTimeout(() => patchState(store, { _isPulse: false } as any), 600); }
          const payload = (evt as any).payload;
          if (payload?.serverNow) patchState(store, { _now: new Date(payload.serverNow).getTime() } as any);
          store.hydrateFor(gameId);
        }
        if (type === 'Reconnected') {
          const payload = (evt as any).payload;
          patchState(store, { _now: payload?.serverNow ? new Date(payload.serverNow).getTime() : Date.now() } as any);
          store.hydrateFor(gameId);
        }
      });
    },
    disconnectRealtime() { if (store._realtimeSub) { store._realtimeSub.unsubscribe(); store._realtimeSub = null; } try { store._realtime.disconnect(); } catch {} }
  }))
);
