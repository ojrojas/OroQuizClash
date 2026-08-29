# Tasks: Player Results (034)

**Input**: Design documents from `/specs/034-player-results/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md
**Branch**: `034-player-results` | **Constitution**: v1.1.0 (I-VI, A-J)

**Organization**: Tasks grouped by user story — each story independently testable and deliverable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify existing Player SPA (027+029+032+033) + modular monolith and prepare results scaffolding

- [x] T001 Verify existing project structure per `specs/034-player-results/plan.md` (`src/Player/QuizArena.Player` Angular 22 standalone, `src/OroQuizClash.Domain`, `Application`, `Infrastructure`, `Api`, `OroQuizClash.AppHost` in `OroQuizClash.slnx`) and `dotnet build OroQuizClash.slnx` passes
- [x] T002 Verify Player SPA dependencies in `src/Player/QuizArena.Player/package.json` (`@angular/core 22`, `@ngrx/signals 22`, `angular-auth-oidc-client`, `@microsoft/signalr`, `rxjs`) and `design-system/tokens/design-tokens.css` is imported via `angular.json` styles with `data-theme="player"` per SPEC-016
- [x] T003 Verify `BuildingBlocks` dependencies and `Directory.Packages.props` central versions for `net10.0` (`LangVersion latest`, `Nullable enable`) and `src/BuildingBlocks/` references per plan.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared results infrastructure MUST complete before ANY user story — `GetMyPlayerState` + `GetLeaderboard` per `sub`, `PlayerGameStore` 10 elementos + `ResultState`, `GameRealtimeService` `GameFinished`, interceptors, shared UI states

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Verify `GamesApi` client in `src/Player/QuizArena.Player/src/app/features/shared/games.api.ts` has `getMyState(gameId)` (`GET /players/me` `Final Score` `Secured` per `sub`), `getLeaderboard(gameId)` (`GET /leaderboard` `Rank` `Prize`), and `getGame(gameId)` per `contracts/api-contracts.md` §1-2
- [x] T005 [P] Verify `GameRealtimeService` in `src/Player/QuizArena.Player/src/app/core/realtime/game-realtime.service.ts` (`HubConnectionBuilder withUrl gameHubUrl?gameId accessTokenFactory`, `withAutomaticReconnect [0,2000,5000,10000,30000]`, events `GameFinished/ScoreUpdated/LeaderboardUpdated/Reconnected` → `hydrate` per `sub`) per research.md
- [x] T006 [P] Verify interceptors in `src/Player/QuizArena.Player/src/app/core/interceptors/` — `correlation-id.interceptor.ts` (`X-Correlation-Id` UUID per `GET /players/me` + `GET /leaderboard`), `auth.interceptor.ts` (`Authorization: Bearer` only `apiUrl` secureRoutes), `error.interceptor.ts` (RFC 7807 `ProblemDetails` mapping, 401 silentRenew, 429 RetryAfter) per plan.md Constraints H/I
- [x] T007 [P] Verify shared UI states in `src/Player/QuizArena.Player/src/app/shared/ui/` — `loading-skeleton.component.ts` (`aria-live="polite"` `aria-busy`), `empty-state.component.ts`, `error-state.component.ts` (CorrelationId/TraceId `Retry` 44px, `aria-live="assertive"`) per `data-model.md` UI States
- [x] T008 Verify `GetMyPlayerState` + `GetLeaderboard` + `GetGame` slices in `src/OroQuizClash.Application/Features/Games/GetMyPlayerState.cs`, `GetLeaderboard.cs`, `GetGame.cs` (ResultState `WINNER/WITHDRAWN/ELIMINATED/FINISHED` `Rank` 1..N `Final Score` `sum(PointTransaction)` per `sub`, `Prize`/`Consolation` per `RewardRules`/`ConsolationPolicy`, `GameStatus` `IsTerminal`) per `data-model.md`
- [x] T009 Verify `PlayerGameStore` intake in `src/Player/QuizArena.Player/src/app/stores/player-game.store.ts` (`signalStore withState {game, gameSession, round, question, answer, score, securedPoints, timer, status, _now}`, `computed resultState/WINNER/WITHDRAWN/ELIMINATED/FINISHED` + `Rank` + `finalScore`, `hydrate` via `GET /players/me` per `sub` + `GET /leaderboard` `Rank`, `serverNow` correction, `bindRealtime` `GameFinished`) is intact from 029/033 to avoid regression before results extension
- [x] T010 Verify `Game` finish invariants in `src/OroQuizClash.Domain/Games/Game.cs` (`Game.Finish()` `LeaderboardBuilder.Build(game)` `Rank` 1..N `Winner` + `ConsolationPolicy` `RowVersion` per `GamePlayer`, `PointTransaction` ledger `sum=total`) per Constitution A/C/D and `data-model.md` §1-2

**Checkpoint**: Foundation ready — `dotnet build` passes, `GET /players/me` per `sub` `Final Score` + `Secured` + `Leaderboard` `Rank` `Prize`, realtime `GameFinished→hydrate` per `sub`, UI states ready, `ResultState` computed

---

## Phase 3: User Story 1 — Victoria YOU WON (Priority: P1) 🎯 MVP

**Goal**: Pantalla `YOU WON` para `WINNER` `Rank 1` `GameStatus==FINISHED` con `Final Score` `850 pts` ledger `sum(PointTransaction)` + `Prize Pack Oro` confetti `pulse` `aria-live assertive` `data-theme="player"` sin `YOU WALKED AWAY`

**Independent Test**: Con `Game` `FINISHED` `Player` `WINNER` `Rank 1` `Score 850` `Reward Pack Oro`, abrir `/player/game/:id/result` → `YOU WON` `Final Score 850 pts` `Prize Pack Oro` `aria-live assertive` sin `YOU WALKED AWAY` (spec US1, quickstart V1, SC-001)

### Tests for User Story 1

- [x] T011 [P] [US1] Contract test for `GET /players/me` + `GET /leaderboard` YOU WON in `tests/OroQuizClash.Api.Tests/Contracts/PlayerResultsWonContractTests.cs` (WebApplicationFactory JWT `PLAYER` `WINNER` `Rank 1`, assert `score.totalPoints` `Rank 1` `Prize` `Reward` `GameStatus FINISHED` `X-Correlation-Id` echo, `PlayerNotInGame` 403)
- [x] T012 [P] [US1] Result component unit test for YOU WON in `src/Player/QuizArena.Player/src/app/features/result/result.component.spec.ts` (TestBed `ResultComponent` mock `PlayerGameStore` `WINNER` `Rank 1` `score 850` `Prize Pack Oro`, verify `YOU WON` `aria-live assertive` `Final Score 850 pts` `Prize Pack Oro` `confetti` `prefers-reduced-motion` none, `YOU WALKED AWAY`/`GAME OVER` not in DOM)
- [x] T013 [P] [US1] Integration test for YOU WON rendering in `src/Player/QuizArena.Player/tests/integration/player-results-won.spec.ts` (mock `getMyState WINNER` + `getLeaderboard Rank 1`, render `ResultComponent` → assert `YOU WON` visible, `data-theme="player"`, `Final Score` ledger, `Prize` if `totalPoints >= pointsRequired`)

### Implementation for User Story 1

- [x] T014 [P] [US1] Create `ResultState` types in `src/Player/QuizArena.Player/src/app/features/result/result-state.model.ts` (export `ResultState = 'won'|'walked'|'over'|'finished'|'playing'`, `ResultDisplay {state, finalScore, finalPosition, totalPlayers, prize, consolation, availableRewards, isTerminal}`, helper `mapResultState(playerStatus, gameStatus, rank)` → `ResultState` per `data-model.md` §1) (depends on T010)
- [x] T015 [US1] Extend `ResultComponent` to render YOU WON in `src/Player/QuizArena.Player/src/app/features/result/result.component.ts` (standalone `selector app-result`, `route /player/game/:gameId/result` `canActivate: [authGuard, mustChangePasswordGuard]`, inject `PlayerGameStore` + `GamesApi`, template `@if resultState()==='won' YOU WON` `h1 YOU WON` `div final-score role="status" aria-live="assertive"` `Final Score {{finalScore()}} pts` + `@if prize() Prize {{prize().name}}` `confetti` `aria-live polite`, redirect si `resultState()==='playing'` → `ErrorState` "Partida aún en curso" + `navigateToGame()`, per `contracts/ui-contracts.md` §1) (depends on T014)
- [x] T016 [US1] Add YOU WON styles with tokens in `src/Player/QuizArena.Player/src/app/features/result/result.component.css` (create/extend CSS `.you-won {background:var(--color-success); color:var(--color-success-contrast); border-radius:var(--radius-lg); padding:var(--space-6); text-align:center; animation:confetti 600ms ease}` `.final-score {font-size:var(--font-size-lg); font-weight:700}` `@keyframes confetti` `@media prefers-reduced-motion reduce animation none` tokens only) (depends on T015)
- [x] T017 [US1] Wire Result YOU WON validation in `src/Player/QuizArena.Player/src/app/stores/player-game.store.ts` (ensure `computed resultState` `WINNER` `Rank 1` `GameStatus FINISHED` + `finalScore` `score().totalPoints` ledger `sum(PointTransaction)` + `prize` via `Leaderboard` `totalPoints >= pointsRequired` or `RewardRules`, `hydrate` restores `Game` + `Leaderboard` per `sub`, no client `Winner` calc) (depends on T014)

**Checkpoint**: US1 fully functional — `npm test` `result.component.spec` `YOU WON` `Final Score 850 pts` `Prize Pack Oro` `aria-live assertive` passes, contract `GET /players/me` `WINNER` `Rank 1` SC-001, `axe` `status` passes, `/player/game/:id/result` shows `YOU WON` 375 1col / 768 centered no scroll (quickstart V1 SC-001)

---

## Phase 4: User Story 2 — Retiro YOU WALKED AWAY (Priority: P1)

**Goal**: Pantalla `YOU WALKED AWAY` para `WITHDRAWN` con `Secured Points` `"{n} pts · checkpoint {m}"` per `sub` + `Available Rewards` filtrable `pointsRequired <= Secured` `role="list"` `aria-live polite` `var(--color-warning)` sin `YOU WON`

**Independent Test**: Con `Player` `WITHDRAWN` `Secured 200 checkpoint 2` `Available Rewards [Pack Plata 300]`, abrir `/player/game/:id/result` → `YOU WALKED AWAY` `Secured Points 200 pts · checkpoint 2` `Available Rewards` `Pack Plata` sin `YOU WON` (spec US2, quickstart V2, SC-002)

### Tests for User Story 2

- [x] T018 [P] [US2] Result component test for YOU WALKED AWAY in `src/Player/QuizArena.Player/src/app/features/result/result.component.spec.ts` (mock `PlayerGameStore` `WITHDRAWN` `Secured 200 checkpoint 2` `Available Rewards [Pack Plata 300]`, verify `YOU WALKED AWAY` `aria-live assertive` `Secured Points 200 pts · checkpoint 2` `Available Rewards` `Pack Plata` `role="list"` `aria-live polite`, `WITHDRAWN` `LOSE_ALL 0` → "Sin recompensas disponibles")
- [x] T019 [P] [US2] PlayerGameStore test for Secured/AvailableRewards in `src/Player/QuizArena.Player/src/app/stores/player-game.store.spec.ts` (verify `hydrate` per `sub` `WITHDRAWN` `Secured 200 checkpoint 2` → `securedPoints().securedPoints 200` `checkpoint 2` `availableRewards()` filtrable `pointsRequired <= Secured` `Pack Plata 300` not `Pack Oro 500`)

### Implementation for User Story 2

- [x] T020 [US2] Extend `ResultComponent` for YOU WALKED AWAY in `src/Player/QuizArena.Player/src/app/features/result/result.component.ts` (add `@if resultState()==='walked' YOU WALKED AWAY` `h1 YOU WALKED AWAY` `div secured role="status" aria-live="polite"` `Secured Points {{formatSecured()}}` + `div available-rewards role="list" aria-label="Available Rewards"` `@for reward of availableRewards() track rewardId` `{{reward.name}} {{reward.pointsRequired}} pts` `@empty Sin recompensas disponibles`, per `contracts/ui-contracts.md` §1) (depends on T015)
- [x] T021 [US2] Add YOU WALKED AWAY styles in `src/Player/QuizArena.Player/src/app/features/result/result.component.css` (add `.you-walked-away {background:var(--color-warning); color:var(--color-warning-contrast); border-radius:var(--radius-lg); padding:var(--space-6); text-align:center}` `.available-rewards {display:grid; grid-template-columns:1fr; gap:var(--space-2)} @media(min-width:768px){grid-template-columns:repeat(2,1fr)}` tokens only) (depends on T020)
- [x] T022 [US2] Wire Available Rewards filtrable in `src/Player/QuizArena.Player/src/app/stores/player-game.store.ts` or `src/Player/QuizArena.Player/src/app/features/result/result.component.ts` (add `computed availableRewards` filtrable `reward.pointsRequired <= securedPoints().securedPoints` via `GET /api/rewards` or `GameConfiguration.RewardRules`, `formatSecured()` helper `"{n} pts · checkpoint {m}"` vs `"{n} pts"` per `data-model.md` §4-5) (depends on T020)

**Checkpoint**: US1+US2 work — `YOU WON` 100% SC-001 + `YOU WALKED AWAY` `Secured checkpoint 2` `Available Rewards` filtrable 100% SC-002, quickstart V2 green

---

## Phase 5: User Story 3 — Eliminación GAME OVER (Priority: P1)

**Goal**: Pantalla `GAME OVER` para `ELIMINATED` con `Final Score` `120 pts` ledger + `Consolation Reward` `CONSOLATION` si `ConsolationPolicy` cumple `FixedPoints`/`ParticipationBased` per `sub`, `var(--color-destructive)` `aria-live assertive` "Sin consolación" si no aplica

**Independent Test**: Con `Player` `ELIMINATED` `Final Score 120` `ConsolationReward Pack Consuelo` si `ConsolationPolicy` cumple, abrir `/player/game/:id/result` → `GAME OVER` `Final Score 120 pts` `Consolation Reward Pack Consuelo` o "Sin consolación" (spec US3, quickstart V3, SC-003)

### Tests for User Story 3

- [x] T023 [P] [US3] Result component test for GAME OVER in `src/Player/QuizArena.Player/src/app/features/result/result.component.spec.ts` (mock `PlayerGameStore` `ELIMINATED` `Final Score 0` `Consolation FixedPoints 50`, verify `GAME OVER` `aria-live assertive` `Final Score 0 pts` `Consolation Reward 50 pts` `aria-live polite`, no `Consolation` → "Sin consolación")
- [x] T024 [P] [US3] PlayerGameStore test for Consolation in `src/Player/QuizArena.Player/src/app/stores/player-game.store.spec.ts` (mock `Game` `ELIMINATED` `ConsolationPolicy FixedPoints 50` → `consolation()` `50 pts` `Consolation Reward`, mock `ParticipationBased` 80 pts → `Consolation 80 pts`)

### Implementation for User Story 3

- [x] T025 [US3] Extend `ResultComponent` for GAME OVER in `src/Player/QuizArena.Player/src/app/features/result/result.component.ts` (add `@if resultState()==='over' GAME OVER` `h1 GAME OVER` `div final-score role="status" aria-live="assertive"` `Final Score {{finalScore()}} pts` + `@if consolation() Consolation Reward {{consolation().name}}` `@else Sin consolación` per `contracts/ui-contracts.md` §1) (depends on T015)
- [x] T026 [US3] Add GAME OVER styles in `src/Player/QuizArena.Player/src/app/features/result/result.component.css` (add `.game-over {background:var(--color-destructive); color:var(--color-on-destructive); border-radius:var(--radius-lg); padding:var(--space-6); text-align:center}` tokens only) (depends on T025)

**Checkpoint**: US1+US2+US3 work — `YOU WON` SC-001 + `YOU WALKED AWAY` SC-002 + `GAME OVER` `Consolation` SC-003 100% `var(--color-destructive)`, quickstart V3 green

---

## Phase 6: User Story 4 — Juego finalizado GAME FINISHED (Priority: P2)

**Goal**: Pantalla `GAME FINISHED` para `FINISHED` posición 2..N `Final Position` `1..N` `aria-label` "Puesto X de N" + `Final Score` + `Reward` si `totalPoints >= pointsRequired` `var(--color-accent)` sin `YOU WON`/`GAME OVER`, responsive 375 1col / 768 centered

**Independent Test**: Con `Game` `FINISHED` `Player` `FINISHED` posición 3 `Final Score 400` `Reward Pack Bronce` si threshold 300 alcanzado, abrir `/player/game/:id/result` → `GAME FINISHED` `Final Position 3` `Final Score 400 pts` `Reward Pack Bronce` o "Sin recompensa" (spec US4, quickstart V4, SC-004)

### Tests for User Story 4

- [x] T027 [P] [US4] Result component test for GAME FINISHED in `src/Player/QuizArena.Player/src/app/features/result/result.component.spec.ts` (mock `PlayerGameStore` `FINISHED` `Rank 3` `Final Score 400` `Reward Pack Bronce`, verify `GAME FINISHED` `aria-live assertive` `Final Position 3 de 4` `aria-label` "Puesto 3 de 4" `Final Score 400 pts` `Reward Pack Bronce` `aria-live polite`, no `Reward` → "Sin recompensa")
- [x] T028 [P] [US4] Leaderboard Rank test for Final Position in `src/Player/QuizArena.Player/src/app/stores/player-game.store.spec.ts` (mock `Leaderboard` `Rank` per `sub` `position 3` `totalPlayers 4` `Rank` 1..N, verify `finalPosition()` 3 `totalPlayers()` 4 `Rank` 2..N vs `YOU WON` Rank 1)

### Implementation for User Story 4

- [x] T029 [US4] Extend `ResultComponent` for GAME FINISHED in `src/Player/QuizArena.Player/src/app/features/result/result.component.ts` (add `@if resultState()==='finished' GAME FINISHED` `h1 GAME FINISHED` `div final-position role="status" aria-live="polite"` `Final Position {{finalPosition()}} de {{totalPlayers()}}` `aria-label` "Puesto X de N" + `div final-score` `Final Score {{finalScore()}} pts` + `@if reward() Reward {{reward().name}}` `@else Sin recompensa`, per `contracts/ui-contracts.md` §1) (depends on T015)
- [x] T030 [US4] Add GAME FINISHED styles in `src/Player/QuizArena.Player/src/app/features/result/result.component.css` (add `.game-finished {background:var(--color-accent); color:var(--color-on-accent); border-radius:var(--radius-lg); padding:var(--space-6); text-align:center}` `.final-position {font-size:var(--font-size-lg); font-weight:700}` tokens only) (depends on T029)
- [x] T031 [US4] Wire Result redirect si !IsTerminal in `src/Player/QuizArena.Player/src/app/features/result/result.component.ts` (add `if resultState()==='playing'` → `ErrorState` "Partida aún en curso" + `retry` `router.navigate(['/player/game', gameId])`, `canActivate` `authGuard` already in `app.routes.ts` `path: 'player/game/:gameId/result'`, verify `GameComponent` already has `isTerminal` check, per `contracts/ui-contracts.md` §1) (depends on T029)

**Checkpoint**: All 4 stories functional — `YOU WON` SC-001 + `YOU WALKED AWAY` SC-002 + `GAME OVER` SC-003 + `GAME FINISHED` `Final Position 3` SC-004 100% `aria-label` "Puesto X de N", quickstart V4 green

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Hardening, docs, observability, final validation

- [x] T032 [P] Add ProblemDetails mapping test for results errors in `tests/OroQuizClash.Api.Tests/Contracts/PlayerResultsErrorsMappingTests.cs` (assert `PlayerNotInGame 403` `GameNotFound 404` `InvalidGameState 400` `PlayerIdentityMismatch 403` audit map via `GlobalExceptionHandler` → `Result.ToHttpResult()` RFC 7807 with `CorrelationId/TraceId`, `X-Correlation-Id` echo)
- [x] T033 [P] Verify `X-Correlation-Id` + JWT gating test in `src/Player/QuizArena.Player/tests/integration/player-results-correlation.spec.ts` (mock `GamesApi.getMyState` + `getLeaderboard` → assert header `X-Correlation-Id` UUID + `Authorization Bearer` per `GET /players/me` + `GET /leaderboard`, no JWT → 401 redirect OIDC, `ErrorState` displays `CorrelationId/TraceId` from `ProblemDetails`, `must_change_password` gating redirect)
- [x] T034 [P] Verify ResultState edge cases in `src/Player/QuizArena.Player/src/app/features/result/result.component.spec.ts` (test `Game` no terminal `!IsTerminal` → redirect "Partida aún en curso", `Prize` null → sin bloque roto, `Secured checkpoint null` → "200 pts" sin badge, `Consolation` null → "Sin consolación", `Final Position` 10 `aria-posinset`/`aria-setsize` 10, `Prize` threshold 500 not reached → "Sin recompensa")
- [x] T035 [P] Update design-system and Player README in `src/Player/QuizArena.Player/README.md` and `specs/034-player-results/spec.md` Status (add `Player Results` section: 4 pantallas `YOU WON` `YOU WALKED AWAY` `GAME OVER` `GAME FINISHED` `Final Score` `Final Position` `Prize`/`Consolation`/`Available Rewards` per `sub` `Leaderboard Rank` `data-theme="player"` WCAG responsive, 1col 375 / centered `max-width:600px`)
- [x] T036 [P] Run quickstart validation in `specs/034-player-results/quickstart.md` (execute V1-V4: YOU WON Rank1 + Prize, YOU WALKED AWAY Secured checkpoint + Available Rewards, GAME OVER Consolation, GAME FINISHED posición 2..N + Reward, fix gaps if any)
- [x] T037 Add architecture test for results isolation in `tests/OroQuizClash.Architecture.Tests/PlayerResultsIsolationTests.cs` (verify `ResultComponent`/`PlayerGameStore` not in `OroQuizClash.Domain` (Domain ↛ Angular), `GetMyPlayerState` uses `sub` not body, no `Leaderboard` `IsCorrect` leak (Domain `sum` not exposed), `ResultState` `WINNER/WITHDRAWN/ELIMINATED/FINISHED` derived per `sub` `Rank` 1..N, BuildingBlocks `IRepository` not leaked)
- [x] T038 Security hardening verify in `src/Player/QuizArena.Player/src/app/core/interceptors/error.interceptor.ts` (hide sensitive details, propagate `traceId`, handle 429 `GamePlayLimiter` `Retry-After`, ensure `Result` never leaks `Answer` de otro, `PlayerIdentityMismatch` audit logged, verify `getMyState`/`getLeaderboard` `Bearer` only `apiUrl`)
- [x] T039 Final `dotnet build OroQuizClash.slnx && dotnet test` green and `npm run lint` with `@ngrx/eslint-plugin` clean and `npm test -- --watch=false` `result.component` + `player-game.store` pass, update `specs/034-player-results/spec.md` Status to `Ready for Review`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately (verifies Angular 22 SPA + monolith BuildingBlocks)
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (GamesApi `getMyState` per `sub` `Final Score` + `getLeaderboard` `Rank` `Prize`, `PlayerGameStore` `ResultState` + `Leaderboard`, `GameRealtimeService` `GameFinished`, `Game` `Rank`/`Consolation`)
- **User Stories (Phase 3+)**: All depend on Foundational
  - **US1 (P1) YOU WON Rank1**: No other story dependency — MVP (YOU WON con Prize)
  - **US2 (P1) YOU WALKED AWAY Secured**: Depends on US1 `ResultState` `WINNER` vs `WITHDRAWN` but independently testable with mocked `GameSession` `WITHDRAWN`
  - **US3 (P1) GAME OVER Consolation**: Depends on US1 `ResultState` `WINNER` vs `ELIMINATED` but testable with mocked `ELIMINATED`
  - **US4 (P2) GAME FINISHED 2..N**: Depends on US1 `ResultState` `WINNER Rank1` vs `FINISHED 2..N` + `Leaderboard` `Rank` (needs T014/T015) — polish parallel with US1 if staffed
- **Polish (Final)**: Depends on all desired stories (US1+US2 for MVP, US3+US4 for completeness)

### User Story Dependencies

- **US1 (P1)**: After Foundational — no deps
- **US2 (P1)**: After Foundational — depends on US1 `ResultState` `WINNER` vs `WITHDRAWN` but testable with mocked `WITHDRAWN`
- **US3 (P1)**: After Foundational — depends on US1 `ResultState` `WINNER` vs `ELIMINATED` but testable with mocked `ELIMINATED`
- **US4 (P2)**: After Foundational — depends on US1 `ResultState` `WINNER Rank1` vs `FINISHED 2..N` + `Leaderboard` `Rank` per `sub`

### Within Each User Story

- Tests (if included) written before implementation (T011 before T014, T018 before T020, T023 before T025, T027 before T029)
- Types/helper (`result-state.model.ts` T014) before store (T015) before component (T029)
- Store before component UI, component before `GameComponent` integration
- Core implementation before `GAME OVER`/`GAME FINISHED` before responsive polish

### Parallel Opportunities

- Phase 1: T002 + T003 parallel (different files)
- Phase 2: T005 + T006 + T007 parallel (different files); T004 later but independent
- Phase 3: T011 + T012 + T013 parallel (contract test / component spec / integration test different files); T014 parallel with T011 tests start (different files)
- Phase 4: T018 + T019 parallel (component spec vs store spec); T020 needs T018 T019; T022 needs T020
- Phase 5: T023 + T024 parallel (component spec vs store spec); T025 needs T023 T024 contracts; T026 needs T025
- Phase 6: T027 + T028 parallel (component spec vs store spec); T029/T030/T031 sequential same file `result.component.ts/.css`
- Phase 7: T032 + T033 + T034 + T035 + T036 parallel (different files); T037 after all
- Different stories can start in parallel after Foundational if staffed (US2 needs only `Secured` interface agreed, US3 needs only `Consolation` signature)

### Parallel Example: User Story 1 (YOU WON Rank1)

```bash
# Launch tests for US1 together:
Task T011: Contract test in tests/OroQuizClash.Api.Tests/Contracts/PlayerResultsWonContractTests.cs
Task T012: Result component unit test in src/Player/QuizArena.Player/src/app/features/result/result.component.spec.ts
Task T013: Integration test in src/Player/QuizArena.Player/tests/integration/player-results-won.spec.ts

# Launch types + component after tests:
Task T014: ResultState types in src/Player/QuizArena.Player/src/app/features/result/result-state.model.ts
Task T015: ResultComponent YOU WON in src/Player/QuizArena.Player/src/app/features/result/result.component.ts
```

### Parallel Example: User Story 2 (YOU WALKED AWAY Secured)

```bash
# Launch tests:
Task T018: Result component test in src/Player/QuizArena.Player/src/app/features/result/result.component.spec.ts
Task T019: PlayerGameStore test in src/Player/QuizArena.Player/src/app/stores/player-game.store.spec.ts

# Launch implementation:
Task T020: ResultComponent YOU WALKED AWAY in src/Player/QuizArena.Player/src/app/features/result/result.component.ts
Task T022: Available Rewards filtrable in src/Player/QuizArena.Player/src/app/stores/player-game.store.ts
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (verify SPA/monolith)
2. Complete Phase 2: Foundational (GamesApi `getMyState` per `sub` `Final Score` + `getLeaderboard` `Rank` `Prize`, `PlayerGameStore` `ResultState` + `Leaderboard`, `GameRealtimeService` `GameFinished`, `Game` `Rank`/`Consolation`)
3. Complete Phase 3: US1 (YOU WON `WINNER` `Rank1` `Final Score` ledger + `Prize` `confetti` `aria-live assertive`)
4. **STOP and VALIDATE**: `GET /players/me` `WINNER` `Rank1` `Final Score` `Prize` SC-001, `ResultComponent` `YOU WON` `aria-live assertive` passes, `axe` `status` passes, quickstart V1 SC-001
5. Deploy/demo MVP (YOU WON works end-to-end)

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 → Test independently → Demo MVP (YOU WON Rank1 + Prize)
3. Add US2 → Test independently → Demo (YOU WALKED AWAY Secured checkpoint + Available Rewards)
4. Add US3 → Test independently → Demo (GAME OVER Consolation `CONSOLATION` si policy cumple)
5. Add US4 → Test independently → Demo (GAME FINISHED 2..N `Final Position` `Rank` + `Reward` si threshold)
6. Polish → final validation V1-V4, SC-001..009

### Parallel Team Strategy

With 3 developers after Foundational:

1. Team completes Setup + Foundational together
2. Once Foundational done:
   - Developer A: US1 (YOU WON `Rank1` `ResultState` + `ResultComponent` skeleton)
   - Developer B: US2 (YOU WALKED AWAY `Secured`·checkpoint + `Available Rewards` filtrable) + US3 (GAME OVER `Consolation` `FixedPoints`/`ParticipationBased`)
   - Developer C: US4 (GAME FINISHED `Final Position` `Rank` 2..N + `Reward` + redirect `!IsTerminal` + `withAutomaticReconnect` per `sub`)
3. Polish by A/B/C after stories done

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability (US1..US4)
- Each user story independently completable and testable per spec Independent Test + quickstart V1-V4
- Verify tests fail before implementing (red → green)
- Commit after each task or logical group; push to branch `034-player-results`
- Avoid: vague tasks, same file conflicts, cross-story state leakage (e.g., `Final Position` `Rank` de otro filtrado)
- Do not edit `.aspire/modules` — wire via AppHost.cs only
- Tokens: `design-system/tokens` + `data-theme="player"` per SPEC-016 cinematic premium + `prefers-reduced-motion`
