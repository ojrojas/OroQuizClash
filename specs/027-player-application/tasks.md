# Tasks: Player Application (027)

**Input**: Design documents from `/specs/027-player-application/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included where宪法/testing requires (store unit tests, isolation tests, integration rehydrate). Tests are optional only if explicitly requested — here included because spec SC-001..SC-009 are measurable and require automated verification.

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Initialize Angular 22 Player SPA and baseline tooling

- [ ] T001 Create Angular 22 standalone SPA skeleton in src/Player/QuizArena.Player per plan.md (ng new --standalone --routing --style=css, angular.json builder @angular/build:application, proxy.conf.json /api and /hubs)
- [ ] T002 Initialize package.json dependencies in src/Player/QuizArena.Player/package.json (add @angular/core 22.x, @angular/router, @angular/common, @angular/forms, rxjs 7.x, @microsoft/signalr 8.x, angular-auth-oidc-client 17+)
- [ ] T003 Install NgRx SignalStore libraries in src/Player/QuizArena.Player (npm install @ngrx/signals @ngrx/signals/entities @ngrx/signals/rxjs-interop) and add @ngrx/eslint-plugin --save-dev per research R3 and skill ngrx-signal-store
- [ ] T004 Configure design-system tokens in src/Player/QuizArena.Player/angular.json (add styles: design-system/tokens/design-tokens.css) and set data-theme="player" in src/Player/QuizArena.Player/src/app/app.component.ts per plan R6 and overrides/player.md
- [ ] T005 Create environment config in src/Player/QuizArena.Player/src/environments/environment.ts and src/environments/environment.example.ts (apiUrl, identityAuthority, gameHubUrl) per quickstart.md
- [ ] T006 Configure AppHost integration in OroQuizClash.AppHost/AppHost.cs (AddNpmApp quizarena-player with reference to oroclash-api and identity-api, or conditional AddProject for QuizArena.Player.Host if BFF)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core cross-cutting infrastructure MUST complete before ANY user story. Blocks all US phases.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T007 Setup Angular app shell routing and guards in src/Player/QuizArena.Player/src/app/app.routes.ts and src/app/app.component.ts (routes: /lobby, /game/:gameId, /result/:gameId, /auth/callback, /auth/logout-callback, guards: authGuard, mustChangePasswordGuard)
- [ ] T008 Implement OIDC PKCE auth configuration in src/Player/QuizArena.Player/src/app/app.config.ts (provideAuth with authority, clientId quizarena-player, scope openid profile email offline_access api, useRefreshToken true, silentRenew, secureRoutes [apiUrl]) per contracts/auth-contracts.md R1
- [ ] T009 Implement auth interceptors in src/Player/QuizArena.Player/src/app/core/interceptors/auth.interceptor.ts (attach Bearer only to apiUrl) and src/app/core/interceptors/correlation-id.interceptor.ts (X-Correlation-Id UUID per request) and src/app/core/interceptors/error.interceptor.ts (RFC7807 ProblemDetails mapping, 401 silentRenew, 429 retry)
- [ ] T010 Create shared DTO models in src/Player/QuizArena.Player/src/app/features/shared/models/player.models.ts (interfaces Player, Game, GameSession, Round, Question, AnswerOption, Question, Answer, Score, PointTransaction, SecuredPoints, Timer, PlayerGameStatus per data-model.md)
- [ ] T011 Create API client services in src/Player/QuizArena.Player/src/app/features/shared/games.api.ts (methods: joinGame(gameId), getMyState(gameId), getGame(gameId), getCurrentRound(gameId), submitAnswer(gameId, dto), withdraw(gameId), getLeaderboard(gameId) with HttpClient and X-Idempotency-Key header) per contracts/api-contracts.md
- [ ] T012 Create base UI states and layout components in src/Player/QuizArena.Player/src/app/shared/ui/ (loading-skeleton.component.ts, empty-state.component.ts, error-state.component.ts with CorrelationId display, responsive 375-1536, aria-live, 44px targets, data-theme player tokens per FR-020/FR-021)
- [ ] T013 Verify backend endpoint GET /api/games/{gameId}/players/me exists or create Vertical Slice GetMyPlayerState in src/OroQuizClash.Application/Features/Games/GetMyPlayerState.cs (IQuery<PlayerGameStateDto>, Handler, Validator, Endpoint IEndpoint) per research R2 and contracts/api-contracts.md §2

**Checkpoint**: Foundation ready — user story implementation can now begin (in parallel if staffed)

---

## Phase 3: User Story 1 — Experiencia privada e independiente por jugador (Priority: P1) 🎯 MVP

**Goal**: Cada instancia mantiene contexto privado aislado de 10 elementos (Player/Game/GameSession/Round/Question/Answer/Score/SecuredPoints/Timer/Status) sin compartir estado mutable — FR-001/FR-002/FR-003.

**Independent Test**: Dos navegadores (A y B) join mismo juego → cada uno ve su Player/Score/SecuredPoints propios, seleccionar Answer en A no afecta B, reload A rehidrata sin afectar B (spec US1 Independent Test, quickstart V1).

### Tests for User Story 1

- [ ] T014 [P] [US1] Create SignalStore unit test for isolated private context in src/Player/QuizArena.Player/src/app/stores/player-game.store.spec.ts (TestBed provide PlayerGameStore, verify initial 10 elements, patchState isolation, two TestBed instances do not share _now/answer per R8)
- [ ] T015 [P] [US1] Create isolation integration test for cross-player visibility in src/Player/QuizArena.Player/tests/integration/isolation.spec.ts (mock two getMyState responses A/B, assert FR-002: score/answer not leaked)

### Implementation for User Story 1

- [ ] T016 [US1] Implement PlayerGameStore with 10-element private context in src/Player/QuizArena.Player/src/app/stores/player-game.store.ts (signalStore withState initialState, withComputed remainingSeconds/isExpired/isTerminal/canAnswer, withProps _api/_realtime, withMethods hydrate rxMethod via getMyState + tapResponse patchState, clearError) per contracts/signal-stores.md
- [ ] T017 [US1] Provide scoped store and hydrate flow in src/Player/QuizArena.Player/src/app/features/lobby/lobby.component.ts (providers: [PlayerGameStore], onInit hydrate(gameId), JoinGame button with idempotencyKey sessionStorage, display Player/Game/GameSession/Status)
- [ ] T018 [US1] Implement GameSession-scoped isolation wiring in src/Player/QuizArena.Player/src/app/features/game/game.component.ts (providers: [PlayerGameStore], route param gameId → store.bindRealtime + hydrate, expose store signals to template via store.player(), store.score() etc., no shared localStorage)
- [ ] T019 [P] [US1] Implement sessionStorage idempotency helper in src/Player/QuizArena.Player/src/app/core/idempotency.service.ts (generate/persist per roundId, retrieve on reload, never localStorage) per FR-003 edge case
- [ ] T020 [US1] Add SC-001/SC-003 validation logging and audit forwarding in src/Player/QuizArena.Player/src/app/core/interceptors/error.interceptor.ts (log CorrelationId/TraceId/GameId/PlayerId/RoundId on 403 impersonation attempt)

**Checkpoint**: US1 fully functional — private context isolated, rehydratable, independently testable as MVP

---

## Phase 4: User Story 2 — Participación simultánea con aislamiento total (Priority: P1)

**Goal**: N jugadores simultáneos mismo juego, eventos server-driven, identidad verificada, sin interferencia — FR-004/FR-005 + isolation FR-002.

**Independent Test**: N=5 join + RoundStarted/QuestionAvailable broadcast → todos reciben misma Question pero Answer/Score independientes; envíos simultáneos evaluados sin pérdida; suplantación PlayerId rechazada 403 (spec US2, quickstart V2/V6).

### Implementation for User Story 2

- [ ] T021 [US2] Implement GameRealtimeService SignalR with rehydrate policy in src/Player/QuizArena.Player/src/app/core/realtime/game-realtime.service.ts (HubConnectionBuilder withUrl gameHubUrl?gameId + accessTokenFactory, withAutomaticReconnect [0,2000,5000,10000,30000], handlers RoundStarted/QuestionAvailable/ScoreUpdated/RoundCompleted/GameFinished filtering by playerId, events$ Subject, connect/disconnect, Reconnected → hydrate trigger) per contracts/realtime-contracts.md
- [ ] T022 [US2] Wire realtime rehydrate into PlayerGameStore in src/Player/QuizArena.Player/src/app/stores/player-game.store.ts (method bindRealtime(gameId) subscribing to GameRealtimeService.events$ and calling hydrate() on relevant events, never direct score patch from event — rehydrate REST per Constitution V)
- [ ] T023 [US2] Implement simultaneous answer submission UI in src/Player/QuizArena.Player/src/app/features/game/question.component.ts (display 4 options from Question.answerOptions, selectedOptionId local signal, submitAnswer rxMethod with idempotencyKey, disable when !canAnswer(), show isCorrect only after EVALUATED, aria-live for result)
- [ ] T024 [P] [US2] Add identity isolation guard in src/Player/QuizArena.Player/src/app/core/auth/player-identity.guard.ts (assert sub claim === store.player().playerId before submit/withdraw, audit attempt on mismatch)
- [ ] T025 [US2] Add concurrency verification test in src/Player/QuizArena.Player/tests/integration/simultaneous-submit.spec.ts (mock N simultaneous submitAnswer calls with distinct idempotencyKeys, verify each 200 EVALUATED and ledger sum)

**Checkpoint**: US1+US2 both work — single-player isolation plus multi-player simultaneous isolation verified

---

## Phase 5: User Story 3 — Ciclo de vida de la sesión de juego (Priority: P2)

**Goal**: Flujo completo WAITING_FOR_PLAYERS → IN_PROGRESS/ROUND_IN_PROGRESS → ScoreUpdated → ROUND_COMPLETED loop → FINISHED/withdrawn/eliminated/winner con bloqueo terminal — FR-006/FR-007/FR-008.

**Independent Test**: End-to-end lobby → 5 rounds →Withdraw en ronda 3 → GameFinished; verificar CurrentRound congelado, terminal bloquea SubmitAnswer 403 (spec US3, quickstart V3).

### Implementation for User Story 3

- [ ] T026 [P] [US3] Implement lobby/waiting UI with GameSession status in src/Player/QuizArena.Player/src/app/features/lobby/waiting-room.component.ts (display Game.status, player count, JoinGame / waiting skeleton, Empty when no game, handle GameFull/GameNotWaitingForPlayers ProblemDetails per FR-020)
- [ ] T027 [US3] Implement round lifecycle display in src/Player/QuizArena.Player/src/app/features/game/round.component.ts (show roundNumber/level/status, QuestionAvailable triggers round update, RoundCompleted → timer STOPPED, GameFinished → terminal view)
- [ ] T028 [US3] Implement result/terminal view in src/Player/QuizArena.Player/src/app/features/result/result.component.ts (show final Score/SecuredPoints/PlayerStatus WINNER/WITHDRAWN/ELIMINATED, block interaction when isTerminal, display audit CorrelationId)
- [ ] T029 [US3] Implement withdraw action in src/Player/QuizArena.Player/src/app/features/game/game.component.ts (Withdraw button → store.withdraw() rxMethod, handle WITHDRAWN status, disable answer, show SecuredPoints per policy KEEP_SECURED_SCORE/KEEP_CHECKPOINT_SCORE)
- [ ] T030 [US3] Verify backend lifecycle integration in src/Player/QuizArena.Player/tests/integration/lifecycle.spec.ts (mock game status transitions WAITING→ROUND_IN_PROGRESS→ROUND_COMPLETED→FINISHED, assert store.status.canAnswer toggles, terminal blocks submit)

**Checkpoint**: Lifecycle end-to-end works independently (can be demoed without US4 timer nuances)

---

## Phase 6: User Story 4 — Timer autoritativo y puntos asegurados visibles (Priority: P2)

**Goal**: Timer regresivo derivado de expiresAt con corrección drift <1s (SC-004), SecuredPoints diferenciado y por política — FR-011/FR-012/FR-013, SC-005.

**Independent Test**: timeLimit 30s countdown sin saltos >1s, envío a 5s evaluado vs expirado por server, checkpoint 5 → SecuredPoints 0→200 y FALLBACK tras fallo (spec US4, quickstart V4).

### Tests for User Story 4

- [ ] T031 [P] [US4] Create timer drift unit test in src/Player/QuizArena.Player/src/app/stores/player-game.store.spec.ts (remainingSeconds computed from expiresAt and _now, interval tick test, isExpired true when expiresAt <= _now)

### Implementation for User Story 4

- [ ] T032 [US4] Implement timer tick and drift correction in src/Player/QuizArena.Player/src/app/stores/player-game.store.ts (add _now signal updated by interval(1000), computed remainingSeconds = max(0, floor((expiresAt - _now)/1000)), correct _now on each hydrate/QuestionAvailable serverNow, method startTimerTick/stopTimerTick per contracts/signal-stores.md)
- [ ] T033 [US4] Implement timer UI with aria-live in src/Player/QuizArena.Player/src/app/features/game/timer.component.ts (bind store.timer() and remainingSeconds, states RUNNING/STOPPED/EXPIRED per FR-013, visual countdown, aria-live polite, color tokens from design-system)
- [ ] T034 [US4] Implement score/secured-points display in src/Player/QuizArena.Player/src/app/features/game/score-panel.component.ts (show Score.totalPoints + SecuredPoints.securedPoints as "500 pts · 200 asegurados", checkpointRoundNumber, policy badge, derived from ledger, never input)
- [ ] T035 [US4] Enforce server-time expiry handling in src/Player/QuizArena.Player/src/app/features/game/question.component.ts (on submit error AnswerWindowExpired show expired state, disable resubmit with same idempotencyKey replay returns same result without duplicate ledger per FR-009/FR-010)

**Checkpoint**: Timer precise <1s drift and SecuredPoints policy-correct display verified

---

## Phase 7: User Story 5 — Estado en tiempo real y rehidratación resiliente (Priority: P3)

**Goal**: Reconexión automática, rehidratación 10 elementos, token refresh, must_change_password gating — FR-017/FR-018/FR-019, SC-007.

**Independent Test**: Disconnect 10s durante ROUND_IN_PROGRESS → withAutomaticReconnect → Reconnected hydrate → Timer corregido sin duplicate; revoke token → OIDC redirect; FR-017/018/019 (spec US5, quickstart V5).

### Implementation for User Story 5

- [ ] T036 [US5] Implement automatic reconnect and rehydrate handler in src/Player/QuizArena.Player/src/app/core/realtime/game-realtime.service.ts (onreconnected callback emits Reconnected, onclose fallback to manual reconnect, keepAliveInterval 15s, integration with PlayerGameStore.bindRealtime) per contracts/realtime-contracts.md
- [ ] T037 [US5] Implement token refresh and must_change_password gating in src/Player/QuizArena.Player/src/app/core/auth/auth.service.ts (silentRenew via angular-auth-oidc-client useRefreshToken, renewTimeBeforeTokenExpiresInSeconds 30, handle must_change_password claim → redirect to identity-api /auth/change-password, post-logout via /connect/logout)
- [ ] T038 [US5] Add resilient error and Loading/Empty/Error/Expired/Terminal states in src/Player/QuizArena.Player/src/app/features/game/game.component.ts (show skeleton while isHydrating, Empty when no round, Error with retry + CorrelationId, Expired when timer EXPIRED, Terminal when isTerminal per FR-020, audit append-only events via API)
- [ ] T039 [US5] Create resilience integration test in src/Player/QuizArena.Player/tests/integration/rehydrate.spec.ts (mock HubConnection disconnect 10s, verify hydrate() called, Timer serverNow corrected, Answer idempotencyKey reused without duplicate)

**Checkpoint**: Resilience fully functional — all 10 elements rehydrate after disconnect, auth recovered

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Improvements affecting multiple stories

- [ ] T040 Audit trail verification for player events in src/OroQuizClash.Infrastructure/Persistence/Configurations/PlayerAuditConfiguration.cs or verify existing AuditEntry append-only (union, answer, score/secured update, status change, impersonation attempt with GameId/PlayerId/RoundId/QuestionId/CorrelationId/TraceId per FR-019)
- [ ] T041 Apply WCAG 2.2 AA and responsive polish across all player components in src/Player/QuizArena.Player/src/app/ (verify contrast via tokens, focus visible, keyboard Tab/Space/Enter for options, aria-live Timer/Score/Status, 375-1536 no scroll, 44px targets, data-theme player per FR-021/SC-008)
- [ ] T042 Run quickstart validation and fix gaps in specs/027-player-application/quickstart.md (execute V1-V7, record results, fix any SC not met)
- [ ] T043 Add architecture test for isolation in tests/OroQuizClash.Architecture.Tests/PlayerIsolationTests.cs (verify Domain does not reference Angular/Player, BuildingBlocks constraints, and that Player SPA never writes Score/isCorrect client-side)
- [ ] T044 Security hardening: rate limit verification and ProblemDetails hardening in src/Player/QuizArena.Player/src/app/core/interceptors/error.interceptor.ts (hide sensitive details, propagate traceId, handle 429 GamePlayLimiter with RetryAfter header)
- [ ] T045 Update placeholder README in src/Player/QuizArena.Player/README.md (replace placeholder per tasks.md T004 with actual Angular 22 + SignalStore documentation, design-system override reference)
- [ ] T046 Run ESLint with @ngrx/eslint-plugin in src/Player/QuizArena.Player (ng lint, fix withState/withComputed/withMethods ordering violations)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phase 3-7)**: All depend on Foundational completion
  - US1 (P1) → MVP — no dependencies on other stories
  - US2 (P1) → depends on US1 store shape but independently testable (can start after US1 store T016, or in parallel with mock store)
  - US3 (P2) → depends on Foundational + US1/US2 status signals (sequential preferred)
  - US4 (P2) → depends on US1 store + US3 round lifecycle (can start after T032 timer logic)
  - US5 (P3) → depends on US2 realtime + US1 hydrate (can start after T021/T016)
- **Polish (Phase 8)**: Depends on all desired user stories

### User Story Dependencies

- **US1 (P1)**: After Foundational — no other story dependency
- **US2 (P1)**: After Foundational — integrates with US1 store but independently testable with mocked store
- **US3 (P2)**: After Foundational — may integrate US1/US2 but independently testable via mocked status transitions
- **US4 (P2)**: After Foundational — depends on US1 timer/score signals
- **US5 (P3)**: After Foundational — depends on US2 realtime connectivity

### Within Each User Story

- Tests (if included) written before implementation (T014/T015 before T016, T031 before T032)
- Models/store before components
- Store before services before UI
- Core implementation before integration

### Parallel Opportunities

- Phase 1: T002 + T003 + T004 + T005 can run in parallel (different files); T006 after T001
- Phase 2: T007 + T010 + T011 can run in parallel; T008 + T009 (auth) can run parallel with T012 (UI)
- Once Foundational done: US1 and US2 can be worked by different devs in parallel (US2 needs only store interface)
- Within US1: T014 and T015 parallel; within US4: T031 parallel with T032 prep
- US3: T026 + T027 + T028 parallel (different components)
- Polish: T041 + T043 + T044 + T046 parallel

---

## Parallel Example: User Story 1

```bash
# Launch tests for US1 together:
Task: "Create SignalStore unit test for isolated private context in src/Player/QuizArena.Player/src/app/stores/player-game.store.spec.ts"
Task: "Create isolation integration test in src/Player/QuizArena.Player/tests/integration/isolation.spec.ts"

# Launch store + helpers together after tests:
Task: "Implement PlayerGameStore with 10-element private context in src/Player/QuizArena.Player/src/app/stores/player-game.store.ts"
Task: "Implement sessionStorage idempotency helper in src/Player/QuizArena.Player/src/app/core/idempotency.service.ts"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T006) — Angular 22 + SignalStore + tokens + AppHost
2. Complete Phase 2: Foundational (T007-T013) — routing, OIDC PKCE, interceptors, DTOs, API client, UI states, GetMyPlayerState
3. Complete Phase 3: US1 (T014-T020) — private context store, lobby, game scoping, idempotency
4. **STOP and VALIDATE**: Run quickstart V1 — two browsers join same game, verify isolation 100% (SC-001/SC-003), reload rehydrate
5. Deploy/demo MVP (isolated private context works end-to-end)

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 → Test independently → Demo MVP (private context)
3. Add US2 → Test independently → Demo (simultaneous N players without interference)
4. Add US3 → Test independently → Demo (full lifecycle lobby→result)
5. Add US4 → Test independently → Demo (timer <1s drift, secured points policy)
6. Add US5 → Test independently → Demo (resilience 10s disconnect rehydrate)
7. Polish → Final validation V1-V7

### Parallel Team Strategy

With 3 developers after Foundational:

1. Team completes Setup + Foundational together
2. Once Foundational done:
   - Developer A: US1 (store + lobby/game scoping)
   - Developer B: US2 (realtime + question + isolation)
   - Developer C: US3 (lifecycle + result) — starts after US1 store interface agreed
3. US4 timer can be picked by A or C after US1 done
4. US5 resilience by B after US2 realtime done
5. All stories integrate via PlayerGameStore contract without blocking

---

## Notes

- [P] tasks = different files, no dependencies — safe to parallelize
- [Story] label maps task to user story for traceability (US1..US5)
- Each user story independently completable and testable per spec Independent Test
- Verify store tests fail before implementing (red → green)
- Commit after each task or logical group; push to branch 027-player-application
- Avoid: vague tasks, same-file conflicts, cross-story state leakage (FR-003)
- Do not edit .aspire/modules — wire via AppHost.cs only
- Tokens: use memory only (PKCE) or BFF cookie — never localStorage (FR-002 edge case)

