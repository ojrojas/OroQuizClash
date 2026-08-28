# Contracts: NgRx SignalStores for Player (027)

**Branch**: `027-player-application` | **Date**: 2026-08-28

Skill: `ngrx-signal-store` (`@ngrx/signals`, `withState`, `withComputed`, `withMethods`, `withProps`, `patchState`, `rxMethod`, `tapResponse`, `withEntities`)

## Store topology

**Un store raíz por GameSession** (scoped, no `providedIn: 'root'` global) — `PlayerGameStore` provee los 10 elementos. Sub-slices diferenciados por prefijo para granularidad pero co-localizados en un solo `signalStore` (evita sincronización inter-store). Colecciones opcionales (`history`, `leaderboard`) usan `withEntities`.

```
PlayerGameStore (scoped per gameId)
├── player: Player
├── game: Game
├── gameSession: GameSession
├── round: Round | null
├── question: Question | null
├── answer: Answer | null
├── score: Score
├── securedPoints: SecuredPoints
├── timer: Timer
├── status: PlayerGameStatus
├── ui: { isLoading, error: ProblemDetails | null, isHydrating }
└── computed: { remainingSeconds, isExpired, isTerminal, canAnswer, displayScore }
```

## File layout

```
src/app/stores/
├── player-game.store.ts        # PlayerGameStore (10 elementos + ui + computed + methods)
├── player-game.store.spec.ts   # unit tests (TestBed + inject)
└── features/
    └── timer.feature.ts        # custom feature withTimer (interval + server correction) — opcional
```

## Store definition (contract — TypeScript sketch)

```ts
import { computed, inject } from '@angular/core';
import { signalStore, withState, withComputed, withMethods, withProps, patchState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap, tap, interval, map } from 'rxjs';
import { tapResponse } from '@ngrx/operators';
import { GamesApi } from '../features/shared/games.api';
import { GameRealtimeService } from '../core/realtime/game-realtime.service';

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
  // internal now signal for timer tick
  _now: number;
};

const initialState: PlayerGameState = {
  player: null,
  game: null,
  gameSession: null,
  round: null,
  question: null,
  answer: null,
  score: { playerId: '', gameId: '', totalPoints: 0, correctAnswers: 0, currentLevel: 'Basic', transactions: [] },
  securedPoints: { playerId: '', gameId: '', securedPoints: 0, checkpointRoundNumber: null, policy: 'KEEP_SECURED_SCORE' },
  timer: { timeLimitSeconds: 30, expiresAt: new Date().toISOString(), remainingSeconds: 0, state: 'STOPPED', serverNow: new Date().toISOString() },
  status: { gameStatus: 'WAITING_FOR_PLAYERS', playerStatus: 'ACTIVE', isTerminal: false, canAnswer: false },
  ui: { isLoading: false, error: null, isHydrating: false },
  _now: Date.now(),
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
    displayScore: computed(() => `${status().isTerminal ? 'Final' : ''} ${/** derived */ ''}`),
  })),

  withMethods((store) => ({
    // ── Hydrate: load authoritative 10-element state
    hydrate: rxMethod<void>(pipe(
      tap(() => patchState(store, { ui: { isLoading: true, error: null, isHydrating: true } })),
      switchMap(() => store._api.getMyState(store.game()?.gameId ?? '')),
      tapResponse({
        next: (state) => patchState(store, {
          player: state.player, game: state.game, gameSession: state.gameSession,
          round: state.round, question: state.question, answer: state.answer,
          score: state.score, securedPoints: state.securedPoints, timer: state.timer, status: state.status,
          ui: { isLoading: false, error: null, isHydrating: false }
        }),
        error: (err: ProblemDetails) => patchState(store, { ui: { isLoading: false, error: err, isHydrating: false } }),
      })
    )),

    // ── Submit answer (idempotent)
    submitAnswer: rxMethod<string>(pipe(
      switchMap((selectedOptionId) => {
        const key = crypto.randomUUID(); // or persisted per round
        sessionStorage.setItem(`idemp-${store.round()?.roundId}`, key);
        return store._api.submitAnswer(store.game()!.gameId, { roundId: store.round()!.roundId, questionId: store.question()!.questionId, selectedOptionId, idempotencyKey: key });
      }),
      tapResponse({
        next: (answer) => patchState(store, { answer, timer: { ...store.timer(), state: 'STOPPED' as const } }),
        error: (err: ProblemDetails) => patchState(store, { ui: { ...store.ui(), error: err } }),
      })
    )),

    // ── Withdraw
    withdraw: rxMethod<void>(pipe(
      switchMap(() => store._api.withdraw(store.game()!.gameId)),
      tapResponse({
        next: (gs) => patchState(store, { gameSession: gs, status: { ...store.status(), playerStatus: 'WITHDRAWN', isTerminal: true, canAnswer: false } }),
        error: (err: ProblemDetails) => patchState(store, { ui: { ...store.ui(), error: err } }),
      })
    )),

    // ── Timer tick (corrects against server time on each event)
    startTimerTick() {
      // called once per QuestionAvailable; interval updates _now
      const sub = interval(1000).pipe(map(() => Date.now())).subscribe(now => patchState(store, { _now: now }));
      // store in withProps subscription cleanup via onDestroy if needed
      return sub;
    },

    // ── Realtime wiring
    bindRealtime(gameId: string) {
      store._realtime.connect(gameId);
      store._realtime.events$.subscribe(evt => {
        if (evt.type === 'QuestionAvailable' || evt.type === 'ScoreUpdated' || evt.type === 'RoundCompleted' || evt.type === 'GameFinished') {
          // rehydrate authoritative
          (store as any).hydrate();
        }
        if (evt.type === 'Reconnected') (store as any).hydrate();
      });
    },

    clearError() { patchState(store, { ui: { ...store.ui(), error: null } }); },
  }))
);
```

## Key invariants (FR-002/FR-003)

- Store instancia = `providers: [PlayerGameStore]` en `GameComponent` (scoped). No singleton global → aislamiento entre pestañas/juegos (dos `TestBed` con stores distintos no comparten `_now`/`answer`).
- Todo `patchState` proviene de REST rehydrate (`getMyState`) o de `rxMethod` que consume API. Event payloads nunca escriben `score`/`securedPoints`/`answer.isCorrect` directamente — solo disparan `hydrate()`.
- `Answer.idempotencyKey` generado una vez por `roundId` y guardado en `sessionStorage`; reintento usa misma key → idempotencia server-side (FR-009).

## Custom features (opcional)

```ts
export function withTimer() {
  return signalStoreFeature(
    withState({ _now: Date.now() }),
    withMethods((store) => ({
      _startTick() { /* interval → patchState({ _now }) */ },
      _stopTick() { /* unsubscribe */ },
    }))
  );
}
```

## Testing contract

```ts
describe('PlayerGameStore', () => {
  it('hydrates 10 elements from getMyState', async () => {
    TestBed.configureTestingModule({ providers: [PlayerGameStore, provideHttpClient(), provideHttpClientTesting()] });
    const store = TestBed.inject(PlayerGameStore);
    // mock GamesApi.getMyState → fake PlayerGameState
    store.hydrate();
    await flushMicrotasks();
    expect(store.player()).toBeTruthy();
    expect(store.score().totalPoints).toBe(250);
  });

  it('isolates instances: two stores do not share answer', () => {
    // create two TestBed instances with distinct providers
  });

  it('remainingSeconds is computed from expiresAt and _now', () => { /* ... */ });
});
```

Commands:

```bash
npm install @ngrx/signals @ngrx/signals/entities @ngrx/signals/rxjs-interop
ng test --include="**/player-game.store.spec.ts"
```
