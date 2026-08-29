# Tasks: Player Multiplayer (033)

**Input**: Design documents from `/specs/033-player-multiplayer/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md
**Branch**: `033-player-multiplayer` | **Constitution**: v1.1.0 (I-VI, A-J)

**Organization**: Tasks grouped by user story — each story independently testable and deliverable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify existing Player SPA (027+029+030+031+032) + modular monolith and prepare multiplayer scaffolding

- [x] T001 Verify existing project structure per `specs/033-player-multiplayer/plan.md` (`src/Player/QuizArena.Player` Angular 22 standalone, `src/OroQuizClash.Domain`, `Application`, `Infrastructure`, `Api`, `OroQuizClash.AppHost` in `OroQuizClash.slnx`) and `dotnet build OroQuizClash.slnx` passes
- [x] T002 Verify Player SPA dependencies in `src/Player/QuizArena.Player/package.json` (`@angular/core 22`, `@ngrx/signals 22`, `angular-auth-oidc-client`, `@microsoft/signalr`, `rxjs`) and `design-system/tokens/design-tokens.css` is imported via `angular.json` styles with `data-theme="player"` per SPEC-016
- [x] T003 Verify `BuildingBlocks` dependencies and `Directory.Packages.props` central versions for `net10.0` (`LangVersion latest`, `Nullable enable`) and `src/BuildingBlocks/` references per plan.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared multiplayer infrastructure MUST complete before ANY user story — `GetMyPlayerState` privado per `sub`, `GetLeaderboard`/`GetGamePlayers` públicos, `PlayerGameStore` scoped per `GameComponent`, `GameRealtimeService`, interceptors, shared UI states

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Verify `GamesApi` client in `src/Player/QuizArena.Player/src/app/features/shared/games.api.ts` has `getMyState(gameId)` (`GET /players/me` privado per `sub` 5 privados + 5 métricas), `getLeaderboard(gameId)` (`GET /leaderboard` público `totalPoints/level` sin `isCorrect`), and `getPlayers(gameId)` (`GET /players` `PlayersRemaining` count `IsActive`) per `contracts/api-contracts.md` §1-3
- [x] T005 [P] Verify `GameRealtimeService` in `src/Player/QuizArena.Player/src/app/core/realtime/game-realtime.service.ts` (`HubConnectionBuilder withUrl gameHubUrl?gameId accessTokenFactory per sub`, `withAutomaticReconnect [0,2000,5000,10000,30000]`, events `ScoreUpdated/LeaderboardUpdated/RoundCompleted/Reconnected` → `hydrate` `GET /players/me` privado per `sub`) per research.md
- [x] T006 [P] Verify interceptors in `src/Player/QuizArena.Player/src/app/core/interceptors/` — `correlation-id.interceptor.ts` (`X-Correlation-Id` UUID per `GET /players/me` + `GET /leaderboard`), `auth.interceptor.ts` (`Authorization: Bearer` only `apiUrl` secureRoutes), `error.interceptor.ts` (RFC 7807 `ProblemDetails` mapping, 401 silentRenew, 429 RetryAfter) per plan.md Constraints H/I
- [x] T007 [P] Verify shared UI states in `src/Player/QuizArena.Player/src/app/shared/ui/` — `loading-skeleton.component.ts` (`aria-live="polite"` `aria-busy`), `empty-state.component.ts`, `error-state.component.ts` (CorrelationId/TraceId `Retry` 44px, `aria-live="assertive"`) per `data-model.md` UI States
- [x] T008 Verify `GetMyPlayerState` + `GetLeaderboard` slices in `src/OroQuizClash.Application/Features/Games/GetMyPlayerState.cs` and `GetLeaderboard.cs` (privado per `sub` `GameClaims.GetSub` `UNIQUE (GameId,RoundId,PlayerId)` + `RowVersion` per `GamePlayerId`, público `Leaderboard` sin `SelectedOptionId/isCorrect/Timer` `totalPoints/level` orden desc) per `data-model.md`
- [x] T009 Verify `PlayerGameStore` intake in `src/Player/QuizArena.Player/src/app/stores/player-game.store.ts` (`signalStore withState {game, gameSession, round, question, answer, score, securedPoints, timer, status, _now}`, `computed potentialReward/roundPoints/totalPoints`, `hydrate` via `GET /players/me` privado per `sub`, `serverNow` correction, `bindRealtime` `ScoreUpdated`/`LeaderboardUpdated`, `providers: [PlayerGameStore]` per `GameComponent` scoped not root) is intact from 029/032 to avoid regression before multiplayer isolation
- [x] T010 Verify `Game` multiplayer invariants in `src/OroQuizClash.Domain/Games/Game.cs` (`UNIQUE (GameId,RoundId,PlayerId)` `Answer` per jugador, `GamePlayer` `RowVersion` per `GamePlayerId` no global, `PointTransaction` ledger per `playerId`, `PlayersRemaining = count IsActive`) per Constitution F and `data-model.md` §1-5

**Checkpoint**: Foundation ready — `dotnet build` passes, `GET /players/me` privado per `sub` 0% leak + `GET /leaderboard` público sin privados, realtime → hydrate per `sub`, UI states ready, Store scoped per `GameComponent`

---

## Phase 3: User Story 1 — Estado privado aislado por jugador (Priority: P1) 🎯 MVP

**Goal**: Cinco estados privados `Private Game State` `Private Answer State` `Private Score State` `Private Timer` `Private Session` per `sub=PlayerId+GameId/RoundId` aislados vía `GET /players/me` `sub` + `UNIQUE (GameId,RoundId,PlayerId)` + `RowVersion` per `GamePlayer`, nunca exponer `Answer/Score/Timer` de otro jugador (0% leak)

**Independent Test**: Con 4 jugadores A-D en `ROUND_IN_PROGRESS` mismo `GameId`, abrir `/player/game/:id` como A y B (2 browsers JWT `sub=A` vs `sub=B`) → `GET /players/me` de A retorna `Answer opt-A` `Score 100` de A, B retorna `Answer opt-C` `Score 250` de B, payload de A no contiene `Answer` de B (spec US1, quickstart V1, SC-001)

### Tests for User Story 1

- [x] T011 [P] [US1] Contract test for `GET /players/me` privado per sub in `tests/OroQuizClash.Api.Tests/Contracts/PlayerMultiplayerPrivateContractTests.cs` (WebApplicationFactory 2 JWTs `sub=A` `sub=B` paralelo mismo `GameId`, assert `answer.selectedOptionId` de A != de B, `score.totalPoints` de A != de B, `isCorrect` de B no en payload de A, `gameSession.playerId == sub`, `X-Correlation-Id` echo, `PlayerNotInGame` 403)
- [x] T012 [P] [US1] Isolation store unit test for Private State per sub in `src/Player/QuizArena.Player/tests/integration/isolation.spec.ts` (create 4 `TestBed` per `GameComponent` scoped `providers: [PlayerGameStore]` with `GamesApi` mock per `sub` `score 100 vs 250` `answer opt-A vs opt-C` `Timer expiresAt` per sub, verify `storeA.answer().selectedOptionId !== storeB.answer().selectedOptionId` no contaminación `Answer/Score/Timer/Session`)
- [x] T013 [P] [US1] Integration test for Private State rendering in `src/Player/QuizArena.Player/tests/integration/player-multiplayer-private.spec.ts` (mock `getMyState` per `sub=A` with `Answer opt-A` `Score 100` `Timer 12s`, render `GameComponent` → assert `Private Game State` `Private Answer` `Private Score` `Private Timer` `Private Session` visible solo de A, no `Answer` de B en DOM, `data-theme="player"`)

### Implementation for User Story 1

- [x] T014 [P] [US1] Create `MultiplayerIsolation` types in `src/Player/QuizArena.Player/src/app/features/game/multiplayer-isolation.model.ts` (export `PrivateGameState {game, gameSession}`, `PrivateAnswerState {answer}`, `PrivateScoreState {score, securedPoints}`, `PrivateTimer {timer}`, `PrivateSession {gameSession}`, helper `isPrivateForSub(payload, sub)` → boolean, `assertNoLeak(privatePayload, otherSub)` per `data-model.md` §1-5) (depends on T010)
- [x] T015 [US1] Verify `PlayerGameStore` scoped per `GameComponent` for Private State in `src/Player/QuizArena.Player/src/app/stores/player-game.store.ts` (ensure `signalStore` with `providers: [PlayerGameStore]` per `GameComponent` not `providedIn: 'root'`, `hydrate` uses `GameClaims.GetSub` `sub` via `GET /players/me` privado per `sub`, `Answer` `UNIQUE` per `sub`, `Score` per `sub`, `Timer` per `sub`, `GameSession` `RowVersion` per `GamePlayerId` isolated) (depends on T014)
- [x] T016 [US1] Verify `GamesApi` private state filtering in `src/Player/QuizArena.Player/src/app/features/shared/games.api.ts` (ensure `getMyState` uses `Authorization Bearer` `sub` and server filters `Answer` where `PlayerId==sub` + `isCorrect` null if `!EVALUATED`, no `Answer` de B leaked, per `contracts/api-contracts.md` §1) (depends on T015)
- [x] T017 [US1] Verify `GetMyPlayerState` server isolation in `src/OroQuizClash.Application/Features/Games/GetMyPlayerState.cs` (ensure `IsCorrect` filtrado null si `state != EVALUATED` para `PLAYER` per sub, `Answer` `UNIQUE` per `playerId+roundId`, `Score` per `sub` `sum(PointTransaction)` per `playerId`, `GameSession` per `sub` `RowVersion` per `GamePlayerId`) (depends on T014)

**Checkpoint**: US1 fully functional — `GET /players/me` per `sub` 0% leak SC-001 private isolation, `isolation.spec.ts` 4 instancias sin contaminación, `Answer` per `sub` `UNIQUE` + `isCorrect` filtrado, quickstart V1 SC-001

---

## Phase 4: User Story 2 — Visualizar información pública sin fuga (Priority: P1)

**Goal**: Mostrar 4 vistas públicas `Players` `Players Remaining` `Leaderboard` `Current Round` sin `SelectedOptionId/isCorrect/Timer/Secured` de otros, vía `GET /leaderboard` `GET /players` `GET /rounds/current` públicos con `totalPoints/level` `displayName/status` + `PlayersRemaining count IsActive` `aria-live polite`

**Independent Test**: Con 4 jugadores `ACTIVE`, abrir como A → `GET /leaderboard` retorna `Players` `displayName`+`level`+`totalPoints` sin `IsCorrect`, `Players Remaining` 4, `Current Round` 3/10 sin `Answer` privado (US2, quickstart V2, SC-002)

### Tests for User Story 2

- [x] T018 [P] [US2] Contract test for `GET /leaderboard` público sin privados in `tests/OroQuizClash.Api.Tests/Contracts/PlayerMultiplayerPublicContractTests.cs` (WebApplicationFactory JWT `PLAYER` 2 jugadores, assert `leaderboard.entries` orden `totalPoints` desc sin `selectedOptionId` `isCorrect` `Timer` `securedPoints` de otros, `PlayersRemaining = count IsActive`, `CurrentRound` 3/10, `X-Correlation-Id` echo)
- [x] T019 [P] [US2] Leaderboard component unit test for públicos sin fuga in `src/Player/QuizArena.Player/src/app/features/game/leaderboard.component.spec.ts` (TestBed `LeaderboardComponent` mock `GamesApi.getLeaderboard` with `entries 2` `totalPoints 100/250` `level Intermediate`, verify `role="list"` `aria-live polite` `totalPoints` visible sin `isCorrect`, `Players Remaining` count IsActive, `Current Round` 3/10)

### Implementation for User Story 2

- [x] T020 [US2] Create `LeaderboardComponent` for públicos sin fuga in `src/Player/QuizArena.Player/src/app/features/game/leaderboard.component.ts` (new standalone `selector app-leaderboard`, inject `GamesApi`, template `div players role="list"` `playersRemaining role="status" aria-live polite` `leaderboard role="list" aria-label="Leaderboard"` `entries` `playerId/displayName/totalPoints/level/position` sin `IsCorrect`, `current-round role="status" aria-live polite` "Ronda 3/10", per `contracts/ui-contracts.md` §2) (depends on T014)
- [x] T021 [US2] Add Leaderboard public styles with tokens in `src/Player/QuizArena.Player/src/app/features/game/leaderboard.component.css` (create CSS `players {display:flex flex-wrap gap:var(--space-2)}` `leaderboard {display:grid; grid-template-columns:1fr; gap:var(--space-2)} @media(min-width:768px){grid-template-columns:repeat(4,1fr)}` 1col 375 / 4col ≥768, `.metric {min-height:44px border-radius:var(--radius-md) border:1px solid var(--color-border) background:var(--color-surface)}` tokens only) (depends on T020)
- [x] T022 [US2] Wire Leaderboard public hydrate in `src/Player/QuizArena.Player/src/app/features/shared/games.api.ts` and `src/Player/QuizArena.Player/src/app/features/game/leaderboard.component.ts` (add `getLeaderboard(gameId)` `GET /leaderboard` público + `getPlayers(gameId)` `GET /players` `PlayersRemaining count IsActive`, `hydrateLeaderboard(gameId)` via `rxMethod` `X-Correlation-Id`, ensure `GameComponent` header/sidebar integration `Players/Leaderboard/CurrentRound` per `data-model.md` §6-8) (depends on T020)

**Checkpoint**: US1+US2 work — privados per `sub` 0% leak SC-001 + públicos sin `IsCorrect` 0% fuga SC-002 `Leaderboard` `totalPoints/level` + `Players Remaining` count `IsActive`, `axe` `list` passes, quickstart V2 green

---

## Phase 5: User Story 3 — Sesiones privadas y timers por jugador (Priority: P2)

**Goal**: `Private Session` `GameSession` `RowVersion` per `GamePlayerId` y `Private Timer` `expiresAt` per `GameRound` per `playerId` aislados, `Withdraw` de A no afecta `GameSession` de B, `Reconnected → hydrate` per `sub` sin reset cross-player

**Independent Test**: Con A y B en misma `Game` misma ronda, `GET /players/me` de A `GameSession RowVersion AAA=` `Timer 12:00:30Z` vs B `RowVersion BBB=` distinto; A `POST /withdraw` → `WITHDRAWN` `RowVersion++`, B sigue `ACTIVE` sin interferencia (US3, quickstart V3, SC-004)

### Tests for User Story 3

- [x] T023 [P] [US3] Session/Timer isolation test in `src/Player/QuizArena.Player/tests/integration/player-multiplayer-session.spec.ts` (mock `getMyState` per `sub=A` `GameSession RowVersion AAA=` `Timer 12:00:30Z` vs `sub=B` `BBB=` `12:00:32Z`, verify `RowVersion` per `GamePlayerId` distinto, `Timer expiresAt` per `sub` no compartido, `Withdraw` de A `RowVersion++` no afecta B)
- [x] T024 [P] [US3] Game session store test for RowVersion per GamePlayer in `src/Player/QuizArena.Player/src/app/stores/player-game.store.spec.ts` (verify `hydrateFor` per `sub` `gameSession.rowVersion` per `GamePlayerId`, `Withdraw` uses `RowVersion` per `GamePlayerId` not global `Game`)

### Implementation for User Story 3

- [x] T025 [US3] Verify `Game` per `GamePlayer` RowVersion isolation in `src/OroQuizClash.Domain/Games/Game.cs` (ensure `GamePlayer` `RowVersion` per `GamePlayerId` + `UNIQUE (GameId,RoundId,PlayerId)` `Answer` per jugador, `PlayersRemaining = count IsActive` per `Game`, not `Game` global `RowVersion` for `Withdraw`) (depends on T010)
- [x] T026 [US3] Enhance `PlayerGameStore` Session/Timer per sub in `src/Player/QuizArena.Player/src/app/stores/player-game.store.ts` (ensure `hydrate` restores `gameSession` `RowVersion` per `sub` + `timer` `expiresAt` per `sub` with `serverNow` correction per `playerId+roundId`, `bindRealtime` `Reconnected` → `hydrate` per `sub` without cross-player reset) (depends on T025)

**Checkpoint**: US1+US2+US3 work — `Private Session` `RowVersion` per `GamePlayerId` 100% SC-004, `Private Timer` per `sub` sin interferencia, `Withdraw` per `sub` no afecta otro, quickstart V3 green

---

## Phase 6: User Story 4 — Concurrencia multiplayer sin interferencia (Priority: P2)

**Goal**: 4 instancias `Angular A/B/C/D` concurrentes con `PlayerGameStore` scoped `providers: [PlayerGameStore]` per `GameComponent` + `GameHub` per `gameId+sub` `withAutomaticReconnect` → `hydrate` privado per `sub` sin mezclar `Answer/Score` en memoria, escalar a `MaxPlayers` 10

**Independent Test**: Simular 4 browsers A-D cada uno `PlayerGameStore` scoped, enviar `SubmitAnswer` simultáneo A `opt-A` y B `opt-C` → `storeA.answer().selectedOptionId==opt-A` y `storeB==opt-C` sin contaminación; `ScoreUpdated` para A → B hace `hydrate` y ve su propio `Score` no el de A, `Leaderboard` público sí actualiza (US4, quickstart V4, SC-003/SC-005)

### Tests for User Story 4

- [x] T027 [P] [US4] Concurrency isolation test for 4 instances in `src/Player/QuizArena.Player/tests/integration/isolation-concurrency.spec.ts` (4 `TestBed` scoped `providers: [PlayerGameStore]` per `GameComponent` with `GamesApi` mock per `sub` concurrent `SubmitAnswer` A `opt-A` B `opt-C`, verify `storeA.answer` != `storeB.answer` no contaminación, `Score` per `sub` isolated)
- [x] T028 [P] [US4] Realtime per sub hydrate test in `src/Player/QuizArena.Player/src/app/core/realtime/game-realtime.service.spec.ts` (mock `HubConnectionBuilder` per `gameId+sub` `withAutomaticReconnect`, emit `ScoreUpdated` for A → verify `PlayerGameStore` A `hydrate` per `sub` privado + B `hydrate` per `sub` privado, `LeaderboardUpdated` → `GET /leaderboard` público)

### Implementation for User Story 4

- [x] T029 [US4] Harden `PlayerGameStore` concurrency isolation in `src/Player/QuizArena.Player/src/app/stores/player-game.store.ts` (ensure `providers: [PlayerGameStore]` per `GameComponent` not `providedIn: 'root'`, `signalStore` `isolation.spec.ts` 4 instancias A-D sin contaminación, `GameRealtimeService` `accessTokenFactory` per `sub` HubConnection per `gameId+sub`, `LeaderboardUpdated` → `GET /leaderboard` público not `GET /players/me` privado) (depends on T025)
- [x] T030 [US4] Verify `GameComponent` providers scoping in `src/Player/QuizArena.Player/src/app/features/game/game.component.ts` (ensure `providers: [PlayerGameStore, PlayerRoundsStore]` per `GameComponent` scoped, `ngOnInit` `bindRealtime(gameId, () => oidc.getAccessToken())` per `sub`, `isolation.spec.ts` passes, `Leaderboard` público via `GamesApi.getLeaderboard` not `PlayerGameStore` privado) (depends on T029)

**Checkpoint**: All 4 stories functional — privados per `sub` 0% leak SC-001, públicos sin `IsCorrect` 0% fuga SC-002, stores A-D aislados 100% SC-003, `Session/Timer` per `sub` 100% SC-004, `SubmitAnswer` per `sub` 100% SC-005, `ScoreUpdated→hydrate` <1s 100% SC-006, quickstart V4 green

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Hardening, docs, observability, final validation

- [x] T031 [P] Add ProblemDetails mapping test for multiplayer errors in `tests/OroQuizClash.Api.Tests/Contracts/PlayerMultiplayerErrorsMappingTests.cs` (assert `PlayerNotInGame 403` `PlayerIdentityMismatch 403` audit `GameNotFound 404` `InvalidGameState 400` map via `GlobalExceptionHandler` → `Result.ToHttpResult()` RFC 7807 with `CorrelationId/TraceId`, `X-Correlation-Id` echo)
- [x] T032 [P] Verify `X-Correlation-Id` + JWT gating test in `src/Player/QuizArena.Player/tests/integration/player-multiplayer-correlation.spec.ts` (mock `GamesApi.getMyState` + `getLeaderboard` → assert header `X-Correlation-Id` UUID + `Authorization Bearer` per `GET /players/me` + `GET /leaderboard`, no JWT → 401 redirect OIDC, `ErrorState` displays `CorrelationId/TraceId` from `ProblemDetails`, `must_change_password` gating redirect)
- [x] T033 [P] Verify PlayersRemaining/CurrentRound edge cases in `src/Player/QuizArena.Player/src/app/features/game/leaderboard.component.spec.ts` (test `PlayersRemaining = count IsActive` 4→3 after `WITHDRAWN`, `ELIMINATED` `LOSE_ALL` count, `CurrentRound` 3/10 genérico sin `Answer` privado, `Leaderboard` orden `totalPoints` desc)
- [x] T034 [P] Update design-system and Player README in `src/Player/QuizArena.Player/README.md` and `specs/033-player-multiplayer/spec.md` Status (add `Player Multiplayer` section: 5 privados per `sub` `UNIQUE`+`RowVersion` per `GamePlayer` `Leaderboard` público sin `IsCorrect` `Players Remaining` `Current Round` scoped `providers: [PlayerGameStore]` `isolation.spec.ts` 4 instancias)
- [x] T035 [P] Run quickstart validation in `specs/033-player-multiplayer/quickstart.md` (execute V1-V4: privados per `sub` 0% leak, públicos sin `IsCorrect`, Session/Timer per `sub`, 4 instancias concurrentes sin interferencia, fix gaps if any)
- [x] T036 Add architecture test for multiplayer isolation in `tests/OroQuizClash.Architecture.Tests/PlayerMultiplayerIsolationTests.cs` (verify `PlayerGameStore`/`LeaderboardComponent` not in `OroQuizClash.Domain` (Domain ↛ Angular), `GetMyPlayerState` uses `sub` not body, no `Leaderboard` `IsCorrect` leak (Domain `sum` not exposed), BuildingBlocks `IRepository` not leaked, `providedIn: 'root'` not used for `PlayerGameStore` (checked via `isolation.spec.ts`))
- [x] T037 Security hardening verify in `src/Player/QuizArena.Player/src/app/core/interceptors/error.interceptor.ts` (hide sensitive details, propagate `traceId`, handle 429 `GamePlayLimiter` `Retry-After`, ensure `Leaderboard` `IsCorrect` never leaked, `PlayerIdentityMismatch` audit logged, verify `getMyState`/`getLeaderboard` `Bearer` only `apiUrl`)
- [x] T038 Final `dotnet build OroQuizClash.slnx && dotnet test` green and `npm run lint` with `@ngrx/eslint-plugin` clean and `npm test -- --watch=false` `isolation.spec.ts` + `player-game.store` pass, update `specs/033-player-multiplayer/spec.md` Status to `Ready for Review`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately (verifies Angular 22 SPA + monolith BuildingBlocks)
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (GamesApi `getMyState` privado per `sub` + `getLeaderboard`/`getPlayers` públicos, `PlayerGameStore` scoped per `GameComponent`, `GameRealtimeService` `ScoreUpdated→hydrate` per `sub`, `Game` `UNIQUE`+`RowVersion` per `GamePlayer`)
- **User Stories (Phase 3+)**: All depend on Foundational
  - **US1 (P1) Privados aislados per sub**: No other story dependency — MVP (5 privados sin leak)
  - **US2 (P1) Públicos sin fuga**: Depends on US1 `Players/Leaderboard` but independently testable with mocked `GET /leaderboard`
  - **US3 (P2) Session/Timer per jugador**: Depends on US1 `Private Session/Timer` (needs T015) but testable with mocked `getMyState` per `sub`
  - **US4 (P2) Concurrencia 4 instancias**: Depends on US1/US3 `PlayerGameStore` scoped (needs T025/T029) — polish parallel with US2 if staffed
- **Polish (Final)**: Depends on all desired stories (US1+US2 for MVP, US3+US4 for completeness)

### User Story Dependencies

- **US1 (P1)**: After Foundational — no deps
- **US2 (P1)**: After Foundational + US1 `Players/Leaderboard` but testable with mocked `GET /leaderboard` público sin privados
- **US3 (P2)**: After Foundational — depends on US1 `Private Session/Timer` per `sub` but can start after `Private Game State`
- **US4 (P2)**: After Foundational — depends on US1/US3 `Store` scoped per `GameComponent` for isolation

### Within Each User Story

- Tests (if included) written before implementation (T011 before T014, T018 before T020, T023 before T025, T027 before T029)
- Types/helper (`multiplayer-isolation.model.ts` T014) before store (T015) before component (T020)
- Store before component UI, component before `GameComponent` integration
- Core implementation before realtime per `sub` before responsive polish

### Parallel Opportunities

- Phase 1: T002 + T003 parallel (different files)
- Phase 2: T005 + T006 + T007 parallel (different files); T004 later but independent
- Phase 3: T011 + T012 + T013 parallel (contract test / component spec / integration test different files); T014 parallel with T011 tests start (different files)
- Phase 4: T018 + T019 parallel (contract test / component spec different files); T020 needs T018 T019 contracts; T021 needs T020
- Phase 5: T023 + T024 parallel (component spec vs store spec different files); T025 needs T023 T024 contracts; T026 needs T025
- Phase 6: T027 + T028 parallel (component spec vs realtime spec different files); T029/T030 sequential same file `player-game.store.ts`/`game.component.ts`
- Phase 7: T031 + T032 + T033 + T034 + T035 parallel (different files); T036 after all
- Different stories can start in parallel after Foundational if staffed (US2 needs only `Leaderboard` interface agreed, US3 needs only `Session/Timer` signature)

### Parallel Example: User Story 1 (Privados aislados per sub)

```bash
# Launch tests for US1 together:
Task T011: Contract test in tests/OroQuizClash.Api.Tests/Contracts/PlayerMultiplayerPrivateContractTests.cs
Task T012: Isolation store test in src/Player/QuizArena.Player/tests/integration/isolation.spec.ts
Task T013: Integration test in src/Player/QuizArena.Player/tests/integration/player-multiplayer-private.spec.ts

# Launch types + component after tests:
Task T014: MultiplayerIsolation types in src/Player/QuizArena.Player/src/app/features/game/multiplayer-isolation.model.ts
Task T015: PlayerGameStore scoped per GameComponent in src/Player/QuizArena.Player/src/app/stores/player-game.store.ts
```

### Parallel Example: User Story 2 (Públicos sin fuga)

```bash
# Launch tests:
Task T018: Contract test in tests/OroQuizClash.Api.Tests/Contracts/PlayerMultiplayerPublicContractTests.cs
Task T019: Component test in src/Player/QuizArena.Player/src/app/features/game/leaderboard.component.spec.ts

# Launch implementation:
Task T020: LeaderboardComponent públicos in src/Player/QuizArena.Player/src/app/features/game/leaderboard.component.ts
Task T021: Leaderboard styles in src/Player/QuizArena.Player/src/app/features/game/leaderboard.component.css
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (verify SPA/monolith)
2. Complete Phase 2: Foundational (GamesApi `getMyState` privado per `sub` + `getLeaderboard`/`getPlayers` públicos, `PlayerGameStore` scoped per `GameComponent`, `GameRealtimeService` `ScoreUpdated→hydrate` per `sub`, `Game` `UNIQUE`+`RowVersion` per `GamePlayer`)
3. Complete Phase 3: US1 (5 privados per `sub` `UNIQUE`+`RowVersion` 0% leak, `isolation.spec.ts` 4 instancias sin contaminación, `Answer` per `sub` `isCorrect` filtrado)
4. **STOP and VALIDATE**: `GET /players/me` per `sub` 0% leak SC-001, `isolation.spec.ts` 4 instancias sin contaminación SC-003, `Answer` per `sub` `UNIQUE` + `isCorrect` filtrado, quickstart V1 SC-001
5. Deploy/demo MVP (privados aislados works end-to-end)

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 → Test independently → Demo MVP (5 privados aislados per `sub`)
3. Add US2 → Test independently → Demo (públicos sin `IsCorrect` `Players Remaining` `Current Round` `Leaderboard` sin fuga)
4. Add US3 → Test independently → Demo (Session/Timer per `sub` `RowVersion` per `GamePlayerId` sin interferencia)
5. Add US4 → Test independently → Demo (4 instancias concurrentes `isolation.spec.ts` sin contaminación + `withAutomaticReconnect` per `sub`)
6. Polish → final validation V1-V4, SC-001..008

### Parallel Team Strategy

With 3 developers after Foundational:

1. Team completes Setup + Foundational together
2. Once Foundational done:
   - Developer A: US1 (5 privados per `sub` `MultiplayerIsolation` + `PlayerGameStore` scoped)
   - Developer B: US2 (Públicos `LeaderboardComponent` `getLeaderboard`/`getPlayers` sin `IsCorrect`)
   - Developer C: US3 (Session/Timer per `sub` `RowVersion` per `GamePlayerId`) + US4 (Concurrencia 4 instancias `isolation.spec.ts` + `withAutomaticReconnect` per `sub`)
3. Polish by A/B/C after stories done

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability (US1..US4)
- Each user story independently completable and testable per spec Independent Test + quickstart V1-V4
- Verify tests fail before implementing (red → green)
- Commit after each task or logical group; push to branch `033-player-multiplayer`
- Avoid: vague tasks, same file conflicts, cross-story state leakage (e.g., `Answer` de B leaked to `Leaderboard` de A)
- Do not edit `.aspire/modules` — wire via AppHost.cs only
- Tokens: `design-system/tokens` + `data-theme="player"` per SPEC-016 cinematic premium + `prefers-reduced-motion`
