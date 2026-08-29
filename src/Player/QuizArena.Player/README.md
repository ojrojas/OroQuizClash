# QuizArena.Player — Angular 22 + NgRx SignalStore

Player Experience (Game Show) — consumes `design-system/tokens/design-tokens.css` via CSS variables (`data-theme="player"`).

## Stack
- Angular 22 standalone (`input()`/`output()`, `@if`/`@for`, `provideRouter`, `withFetch`)
- NgRx Signals `@ngrx/signals` (`signalStore`, `withState`, `withComputed`, `withMethods`, `withProps`, `patchState`, `rxMethod`, `tapResponse`) — private context por GameSession (FR-003)
- `angular-auth-oidc-client` 17+ PKCE `authorization_code` + `refresh_token` contra OroIdentityServer discovery
- `@microsoft/signalr` 8.x GameHub `RoundStarted/QuestionAvailable/ScoreUpdated/RoundCompleted/GameFinished` + `withAutomaticReconnect` + rehydrate REST (Constitución V)
- Design System `design-system/tokens/design-tokens.css` + `overrides/player.md` (`angular.json` styles, `data-theme="player"`)

## Design System Override
Ver `design-system/MASTER.md` + `design-system/overrides/player.md` — tokens CSS sin literales, `data-theme="player"`, WCAG 2.2 AA, 375-1536 responsive, 44px targets, `aria-live` Timer/Score/Status.

## Quickstart
```bash
cd src/Player/QuizArena.Player
npm install
npm install @ngrx/signals @ngrx/signals/entities @ngrx/signals/rxjs-interop
npm install @ngrx/eslint-plugin --save-dev
cp src/environments/environment.example.ts src/environments/environment.ts
npm start # http://localhost:4200 proxy /api → oroclash-api
```

## Stores
`src/app/stores/player-game.store.ts` — `PlayerGameStore` 10 elementos (`Player/Game/GameSession/Round/Question/Answer/Score/SecuredPoints/Timer/Status`) scoped por `gameId` (`providers: [PlayerGameStore]`), `hydrate` via `GET /api/games/{id}/players/me`, `remainingSeconds` computed + `interval(1000)` + drift correction, `sessionStorage` idempotencyKey por round.

## Tests
```bash
npm test -- --watch=false
ng lint # @ngrx/eslint-plugin withState/withComputed/withMethods ordering
```

Spec: `specs/027-player-application/` (US1-US5, FR-001..021, SC-001..009)

## Player Lobby (028)
`src/app/features/lobby/` — Available Games 8 cols (`Game Name, Category, Difficulty, Number of Rounds, Players, Start Time, Prize, Status`) via `GET /api/games?status=WAITING_FOR_PLAYERS` paginado (`LobbyStore` `signalStore` `games/totalCount/page/pageSize` `load` rxMethod + `GamesApi.getGames`), table ≥1024px / cards ≤768px stacked 375px same 8 fields, `players.display "current/max"` `prize "—"` fallback, paginator `totalCount/page/pageSize`, `Join Game` per row `sessionStorage idemp-join-{gameId}` `X-Idempotency-Key` → `POST /api/games/{id}/players` idempotente `UNIQUE (GameId,UserId)` → redirect `/player/game/:id`, `View Game Information` → `GET /api/games/{id}` modal/page `game-detail.component.ts` 8+extended `TimeLimit/Points/Policies` `StartTime` local, `Leave Lobby` → `router.navigate(['/'])` no API (FR-007), `data-theme="player"` `design-tokens.css` WCAG 2.2 AA `aria-live` 44px, `X-Correlation-Id` per request `ErrorState` `CorrelationId/TraceId`.

## Player Game (029)
`src/app/features/game/` — Cinematic 3 áreas `game.component.ts` `display:grid grid-template-areas "header" "center" "footer"` Header `Current Round "Ronda 3/10" + Current Level + TimerComponent` `background: var(--player-gradient-premium)`, Center `QuestionComponent` `Four Answers radiogroup aria-checked` `selectedOptionId signal` `Submit` `isCorrect` solo `EVALUATED`, Footer `ScorePanel` `Potential Reward` computed `PlayerGameStore.potentialReward` + `SecuredPoints` + `Withdrawal Action` modal `withdrawal.component.ts` `sessionStorage idemp-withdraw-{gameId}` `X-Idempotency-Key` → `POST /withdraw` idempotente, `PlayerGameStore` 10 elementos `remainingSeconds/isExpired/isTerminal/canAnswer/potentialReward/currentRoundDisplay` `hydrate` `submitAnswer/withdraw/startTimerTick/bindRealtime` `interval+serverNow` drift <1s, `data-theme="player"` tokens premium responsive 375-1536 no scroll `aria-live` `role=timer/dialog` 44px WCAG AA.
