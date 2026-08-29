# Tasks: Player Game (029)

**Input**: Design documents from `/specs/029-player-game/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md
**Branch**: `029-player-game` | **Constitution**: v1.1.0 (I-VI, A-J)

**Organization**: Tasks grouped by user story — each story independently testable and deliverable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify existing Player SPA (027+028) + modular monolith and prepare game screen scaffolding

- [x] T001 Verify existing project structure per `specs/029-player-game/plan.md` (`src/Player/QuizArena.Player` Angular 22 standalone, `src/OroQuizClash.Domain`, `Application`, `Infrastructure`, `Api`, `OroQuizClash.AppHost` in `OroQuizClash.slnx`) and `dotnet build OroQuizClash.slnx` passes
- [x] T002 Verify Player SPA dependencies `src/Player/QuizArena.Player/package.json` (`@angular/core 22`, `@ngrx/signals`, `angular-auth-oidc-client`, `@microsoft/signalr`, `rxjs`) and `design-system/tokens/design-tokens.css` is imported via `angular.json` styles with `data-theme="player"` per SPEC-016
- [x] T003 Verify `BuildingBlocks` dependencies and `Directory.Packages.props` central versions for `net10.0` (`LangVersion latest`, `Nullable enable`) and `src/BuildingBlocks/` references per plan.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared game screen infrastructure MUST complete before ANY user story — 10-element hydrato, Timer computed, realtime, interceptors, UI states

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Verify `PlayerGameStore` 10-element state in `src/Player/QuizArena.Player/src/app/stores/player-game.store.ts` (`signalStore withState {player, game, gameSession, round, question, answer, score, securedPoints, timer, status, ui, _now}`, `withComputed remainingSeconds/isExpired/isTerminal/canAnswer`, `withMethods hydrate rxMethod GET /players/me + tapResponse patchState + interval _now correction`, `submitAnswer/withdraw/bindRealtime`) per `contracts/api-contracts.md` §1
- [x] T005 [P] Verify `GamesApi` client in `src/Player/QuizArena.Player/src/app/features/shared/games.api.ts` has `getMyState(gameId)` (`GET /players/me` 10 elementos), `submitAnswer(gameId,dto)` (`POST /answers` `X-Idempotency-Key`), `withdraw(gameId)` (`POST /withdraw`), `getGame(gameId)` per `contracts/api-contracts.md` (verify `getGames` from 028 still present)
- [x] T006 [P] Verify `GameRealtimeService` in `src/Player/QuizArena.Player/src/app/core/realtime/game-realtime.service.ts` (`HubConnectionBuilder withUrl gameHubUrl?gameId accessTokenFactory`, `withAutomaticReconnect [0,2000,5000,10000,30000]`, `events$ Subject` `QuestionAvailable/ScoreUpdated/RoundCompleted/GameFinished/Reconnected` → `hydrate`) per research.md R5
- [x] T007 [P] Verify interceptors in `src/Player/QuizArena.Player/src/app/core/interceptors/` — `correlation-id.interceptor.ts` (`X-Correlation-Id` UUID), `auth.interceptor.ts` (`Authorization: Bearer` only `apiUrl` secureRoutes), `error.interceptor.ts` (RFC 7807 `ProblemDetails` mapping, 401 silentRenew, 429 RetryAfter) per `contracts/api-contracts.md` Interceptors
- [x] T008 [P] Verify shared UI states in `src/Player/QuizArena.Player/src/app/shared/ui/` — `loading-skeleton.component.ts` cinematic `aria-live="polite"`, `empty-state.component.ts`, `error-state.component.ts` (CorrelationId/TraceId Retry 44px, `aria-live="assertive"`) per FR-016
- [x] T009 Verify `GetMyPlayerState`/`SubmitAnswer`/`WithdrawPlayer` slices `GET /players/me` `POST /answers` `POST /withdraw` with `X-Idempotency-Key` + `AnswerWindowExpired` + `QuestionAlreadyAnswered` + `PlayerIdentityMismatch` + `RowVersion` in `src/OroQuizClash.Application/Features/Games/` (already from 027, verify idempotent 200) per research.md

**Checkpoint**: Foundation ready — `dotnet build` passes, game screen can hydrate 10 elementos, Timer computed, realtime → hydrate, UI states ready

---

## Phase 3: User Story 1 — Visualizar pantalla principal de juego (Priority: P1) 🎯 MVP

**Goal**: Pantalla cinematic 3 áreas muestra 10 elementos (Current Round "Ronda 3/10", Current Level, Question, Four Answers, Timer RUNNING, Current Score, Secured Points, Potential Reward "—" o nombre, Player Status, Withdrawal Action) proyectados de `GET /players/me`, responsive 375-1536 sin scroll, WCAG AA, `data-theme="player"` tokens

**Independent Test**: Con `Game` `ROUND_IN_PROGRESS` `Round 3/10` `Level Intermediate` `Question 4 opts` `Timer 12s RUNNING` `Score 250 Secured 100 Potential Pack Oro Status ACTIVE`, abrir `/player/game/:id` → 10 elementos visibles coincidentes con `GET /players/me` `X-Correlation-Id` en error (spec US1, quickstart V1, SC-001/SC-008)

### Tests for User Story 1

- [x] T010 [P] [US1] Contract test for `GET /api/games/{id}/players/me` 10 elements in `tests/OroQuizClash.Api.Tests/Contracts/PlayerGameScreenContractTests.cs` (WebApplicationFactory, JWT `PLAYER`, assert `player/game/gameSession/round/question/answer/score/securedPoints/timer/status` 10 elementos SC-001, `Question` 4 opts `isCorrect` null before EVALUATED SC-002, `PotentialReward` "—" when null)
- [x] T011 [P] [US1] Game store unit test for hydrate 10 elements in `src/Player/QuizArena.Player/src/app/stores/player-game.store.spec.ts` (TestBed provide `PlayerGameStore` mock `GamesApi.getMyState` → `gameSession currentRoundNumber 3`, verify `player/game/round/question/timer/score/secured/status` patchState, `remainingSeconds` computed, Empty when no round)
- [x] T012 [P] [US1] Game screen integration test for 10 elements rendering in `src/Player/QuizArena.Player/tests/integration/player-game-screen.spec.ts` (mock `getMyState` 10 elementos, render `GameComponent` → assert `Current Round "Ronda 3/10"`, `Current Level`, `Question text`, `Four Answers` count 4, `Timer` 12s, `Score` "250 pts", `Secured` "100", `Potential`, `Status ACTIVE`, `Withdrawal Action` visible)

### Implementation for User Story 1

- [x] T013 [P] [US1] Enhance `PlayerGameStore` with `PotentialReward` projection in `src/Player/QuizArena.Player/src/app/stores/player-game.store.ts` (add `potentialReward: computed(() => score/policy → rewardName or "—")` derived from `score/securedPoints` ledger, keep existing `remainingSeconds/isTerminal/canAnswer` per `data-model.md` Potential Reward)
- [x] T014 [US1] Implement cinematic game screen shell in `src/Player/QuizArena.Player/src/app/features/game/game.component.ts` (replace/extend existing, imports `LoadingSkeletonComponent`, `EmptyStateComponent`, `ErrorStateComponent`, `QuestionComponent`, `TimerComponent`, `ScorePanelComponent`, template `display:grid grid-template-areas "header" "center" "footer"` Header `Current Round "Ronda {{current}}/{{max}}" + Current Level + TimerComponent` `background: var(--player-gradient-premium)`, Center `QuestionComponent` `Four Answers` premium, Footer `ScorePanel` `Potential Reward` `Player Status` `Withdrawal Action` `min-height 44px` `data-theme="player"` `design-system/tokens` per `contracts/ui-contracts.md` Layout) (depends on T013)
- [x] T015 [US1] Wire game route and hydrato with realtime in `src/Player/QuizArena.Player/src/app/app.routes.ts` (verify `/player/game/:gameId` and `/game/:gameId` `canActivate: [authGuard, mustChangePasswordGuard]` `providers: [PlayerGameStore]` already) and `src/Player/QuizArena.Player/src/app/features/game/game.component.ts` `OnInit` → `store.hydrateFor(gameId)` + `store.startTimerTick()` + `store.bindRealtime(gameId, ()=>oidc.getAccessToken())` + `OnDestroy stopTimerTick` (verify no existing lobby conflict, ensure `gameId` from `ActivatedRoute.paramMap`)

**Checkpoint**: US1 fully functional — `dotnet test --filter PlayerGame` + `npm test` store/screen passes, `/player/game/:id` shows 10 elementos cinematic 375-1536 no scroll <500ms p95, Empty/Loading/Error/Expired/Terminal states, WCAG AA axe PASS (quickstart V1, SC-001/SC-008)

---

## Phase 4: User Story 2 — Responder y ver progresión de nivel/premio (Priority: P1)

**Goal**: Selección `Four Answers` `radiogroup` `aria-checked` `Tab/Space/Enter`, envío `POST /answers` `X-Idempotency-Key` per `roundId` `sessionStorage`, `EVALUATED` → `Current Score` + `Level Bonus` + `Secured` `KEEP_SECURED_SCORE` + `Potential Reward` next threshold updated <1s, reintento idempotente sin duplicar ledger, `canAnswer` bloquea re-envío

**Independent Test**: Select opt `o2` → Send → `POST /answers` `selectedOptionId o2` `X-Idempotency-Key uuid-round3` → `EVALUATED isCorrect true` → `Score 250→350` `Secured 100→?` `Level Intermediate→Advanced` `Potential` next; same key retry → same 200 no ledger duplicate (US2, quickstart V2, SC-003/SC-005)

### Tests for User Story 2

- [x] T016 [P] [US2] Contract test for `POST /api/games/{id}/answers` idempotent and scoring in `tests/OroQuizClash.Api.Tests/Contracts/PlayerAnswerProgressionContractTests.cs` (first submit 200 EVALUATED `isCorrect`, second same `X-Idempotency-Key` 200 same `answerId` no duplicate `PointTransaction`, `Current Score` + `PointsPerRound` verified via `GetMyPlayerState`)
- [x] T017 [P] [US2] Question component unit test for Four Answers radiogroup in `src/Player/QuizArena.Player/src/app/features/game/question.component.spec.ts` (TestBed `QuestionComponent` mock `PlayerGameStore`, verify 4 buttons `role=radio` `aria-checked`, `selectedOptionId` signal, `Submit` disabled when `!canAnswer || !selected`, `isCorrect` not rendered before `EVALUATED`)

### Implementation for User Story 2

- [x] T018 [US2] Verify `QuestionComponent` radiogroup and submit in `src/Player/QuizArena.Player/src/app/features/game/question.component.ts` (must have `role="radiogroup"` `aria-label="Opciones de respuesta"`, each option `role="radio"` `aria-checked/aria-selected` `tabIndex 0` `Space/Enter` via click, `selectedOptionId = signal<string|null>(null)`, `submit()` → `if (!id) return; store.submitAnswer(id)` `disabled !canAnswer || !selected`, `EVALUATED` shows `¡Correcto!/Incorrecto` `aria-live="assertive"`, `EXPIRED` shows `Tiempo expirado`, `isCorrect` never before `EVALUATED` per FR-003/FR-009) (depends on T014)
- [x] T019 [US2] Verify `PlayerGameStore.submitAnswer` idempotence in `src/Player/QuizArena.Player/src/app/stores/player-game.store.ts` (method `submitAnswer: rxMethod<string>` → `sessionStorage idemp-{roundId} ?? crypto.randomUUID()` → `GamesApi.submitAnswer(gameId,{roundId,questionId,selectedOptionId,idempotencyKey})` `tapResponse` → `patchState({answer, timer:STOPPED})` else error `ProblemDetails`, verify not patching `isCorrect` from event payload per FR-011)

**Checkpoint**: US1+US2 work — display plus Answer progression <1s 95% SC-003, `Potential Reward` `Secured` ledger SC-005, quickstart V2 green

---

## Phase 5: User Story 3 — Gestión de tiempo y estado (Priority: P2)

**Goal**: `Timer` `RUNNING` `remainingSeconds` `max(0,floor((expiresAt-_now)/1000))` with `interval(1000)` + `serverNow` correction on `hydrate`/`QuestionAvailable`, warning <10s `aria-live="polite"`, `EXPIRED` blocks send `400 AnswerWindowExpired` with `CorrelationId`, `Player Status` `ACTIVE→WITHDRAWN/ELIMINATED` `isTerminal` blocks

**Independent Test**: Timer 30→0 1/s no jumps >1s 30s warning <10s; `EXPIRED` without send → `EXPIRED` `assertive` blocked; `submittedAt>expiresAt` → `400 AnswerWindowExpired` (US3, quickstart V3, SC-004)

### Tests for User Story 3

- [x] T020 [P] [US3] Timer computed unit test in `src/Player/QuizArena.Player/src/app/stores/player-game.store.spec.ts` (verify `remainingSeconds` computed `max(0,floor((expiresAt-_now)/1000))`, `isExpired` when `expiresAt<=_now`, `interval tick` updates `_now`, `startTimerTick/stopTimerTick` lifecycle, correction `_now = serverNow` on hydrate)
- [x] T021 [P] [US3] Timer component test in `src/Player/QuizArena.Player/src/app/features/game/timer.component.spec.ts` (render `TimerComponent` with `LobbyStore` mock `timer RUNNING 12s` → `12s` `aria-live` `warning` when <10s, `EXPIRED` shows "Expirado" `assertive`)

### Implementation for User Story 3

- [x] T022 [US3] Verify `TimerComponent` and store tick in `src/Player/QuizArena.Player/src/app/features/game/timer.component.ts` and `src/Player/QuizArena.Player/src/app/stores/player-game.store.ts` (must have `computed remainingSeconds/isExpired`, `interval(1000) map Date.now → _now patchState`, `startTimerTick` called on `QuestionAvailable`/`hydrate` RUNNING, `stopTimerTick` on `EVALUATED/EXPIRED/terminal`, template `role="timer"` `data-state` `remainingSeconds+"s"` warning color `var(--color-warning)` when <10s) per `contracts/ui-contracts.md` (depends on T014)

**Checkpoint**: Timer precise <1s drift SC-004, quickstart V3 green

---

## Phase 6: User Story 4 — Retirarse de la partida (Priority: P2)

**Goal**: `Withdrawal Action` button only if `!isTerminal && canAnswer` → confirm modal `aria-modal=true` → `POST /withdraw` `X-Idempotency-Key` `sessionStorage idemp-withdraw-{gameId}` → `PlayerStatus WITHDRAWN` `canAnswer=false` `isTerminal` block `403 PlayerNotActive` on next answer, second withdraw idempotente no new ledger, isolation other players `ACTIVE`

**Independent Test**: Click Withdraw → confirm → `POST /withdraw` 200 `WITHDRAWN` `Secured` KEEP_SECURED_SCORE → `canAnswer=false` 403 on answer; second withdraw same key → same 200 no new `WITHDRAWAL` ledger; other player still ACTIVE (US4, quickstart V4, SC-006)

### Tests for User Story 4

- [x] T023 [P] [US4] Contract test for `POST /api/games/{id}/withdraw` idempotent in `tests/OroQuizClash.Api.Tests/Contracts/PlayerWithdrawContractTests.cs` (first withdraw 200 WITHDRAWN `SecuredPoints` per policy, second same `X-Idempotency-Key` 200 same `GameSessionId` no duplicate ledger, `PlayerIdentityMismatch` 403 when sub mismatch, terminal AlreadyTerminal 409)
- [x] T024 [P] [US4] Withdrawal component unit test in `src/Player/QuizArena.Player/src/app/features/game/withdrawal.component.spec.ts` or `game.component.spec.ts` (verify button disabled when `isTerminal`, modal `role="dialog"` `aria-modal` Confirm/Cancel 44px, `confirmWithdraw` calls `store.withdraw()` idemp key persisted, second call no new ledger, other player isolation)

### Implementation for User Story 4

- [x] T025 [US4] Create `WithdrawalComponent` or extend `GameComponent` with withdrawal modal in `src/Player/QuizArena.Player/src/app/features/game/withdrawal.component.ts` (standalone `role="dialog"` `aria-modal="true"` `aria-label="Confirmar retiro"` text "¿Confirmar retiro? Perderás puntos no asegurados según {{policy}}" Confirm/Cancel buttons 44px, `confirm()` → `sessionStorage idemp-withdraw-{gameId} ?? crypto.randomUUID()` → `store.withdraw()` → on success patch `isTerminal:true`) or integrate directly into `game.component.ts` footer Withdrawal Action per `contracts/ui-contracts.md` Withdrawal Modal (depends on T014)
- [x] T026 [US4] Wire Withdrawal Action in `src/Player/QuizArena.Player/src/app/features/game/game.component.ts` (template footer button `(click)="openWithdraw()" [disabled]="store.isTerminal()"` `aria-label="Retirarse"` `showWithdrawConfirm` signal, modal Confirm → `store.withdraw()` via `GamesApi.withdraw`, verify server `POST /withdraw` `X-Idempotency-Key` + `RowVersion` already in `WithdrawPlayer.cs` idempotent 200)

**Checkpoint**: Withdrawal <1s 100% SC-006, idempotence, quickstart V4 green

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Hardening, cinematic polish, WCAG, observability, docs, quickstart validation

- [x] T027 [P] Add ProblemDetails mapping test for game screen errors in `tests/OroQuizClash.Api.Tests/Contracts/PlayerGameErrorsMappingTests.cs` (assert `AnswerWindowExpired 400`, `QuestionAlreadyAnswered 409`, `PlayerNotActive 403` `Withdrawn`, `PlayerIdentityMismatch 403`, `GameNotFound 404` map via `GlobalExceptionHandler` → `Result.ToHttpResult()` RFC 7807 with `CorrelationId/TraceId`)
- [x] T028 [P] Verify cinematic `data-theme="player"` and responsive in `src/Player/QuizArena.Player/src/app/features/game/game.component.ts` and `question/timer/score-panel/withdrawal.component.ts` (grid 3 áreas `data-theme="player"` `design-tokens.css` no literals, cinematic Header gradient premium, Center Question immersive, Footer Score competitive, 375-1536 no horizontal scroll, `interval` + `serverNow` correction, WCAG AA `aria-live polite` Timer/Score `assertive` EXPIRED, focus visible `outline:2px`, targets ≥44px, axe pass) per `contracts/ui-contracts.md`
- [x] T029 [P] Verify `X-Correlation-Id` propagation test in `src/Player/QuizArena.Player/tests/integration/player-game-correlation.spec.ts` (mock `getMyState/submitAnswer/withdraw` → assert header `X-Correlation-Id` UUID sent, `ErrorState` displays `CorrelationId/TraceId` from `ProblemDetails`)
- [x] T030 [P] Update design-system reference in `src/Player/QuizArena.Player/README.md` (add `Player Game` section: 10 elements cinematic 3 áreas, `PlayerGameStore`, Timer `remainingSeconds`, Withdrawal idempotent, `data-theme="player"` WCAG)
- [x] T031 [P] Run quickstart validation in `specs/029-player-game/quickstart.md` (execute V1-V6: 10 elementos cinematic, Answer progression, Timer drift <1s, Withdraw idempotente, Responsive 375-1536 axe, 401/CorrelationId, fix gaps if any)
- [x] T032 Add architecture test for game screen isolation in `tests/OroQuizClash.Architecture.Tests/PlayerGameIsolationTests.cs` (verify Domain not references Angular/Player game, BuildingBlocks constraints, `SubmitAnswer`/`Withdraw` uses `sub` not body, no client `isCorrect` trust, `isTerminal` computed not stored)
- [x] T033 Security hardening verify in `src/Player/QuizArena.Player/src/app/core/interceptors/error.interceptor.ts` (hide sensitive details, propagate `traceId`, handle 429 `GamePlayLimiter` RetryAfter already from SPEC-027+028, ensure 403 `PlayerIdentityMismatch` audit)
- [x] T034 Final `dotnet build OroQuizClash.slnx && dotnet test` green and `npm run lint` with `@ngrx/eslint-plugin` clean, update `specs/029-player-game/spec.md` Status to `Ready for Review`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately (verifies 027 SPA + 028 lobby + monolith)
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (10-element hydrate, Timer computed, realtime, interceptors, UI states, `GetMyPlayerState`/`SubmitAnswer`/`Withdraw` slices verified)
- **User Stories (Phase 3+)**: All depend on Foundational
  - **US1 (P1) Visualizar pantalla principal**: No other story dependency — MVP (10 elementos cinematic)
  - **US2 (P1) Responder y ver progresión**: Depends on US1 screen shell (needs `question`/`canAnswer` from hydrate) but independently testable with mocked `GET /players/me`
  - **US3 (P2) Gestión tiempo y estado**: Depends on US1 `Timer` computed + `PlayerGameStore` tick (can start after US1 tick)
  - **US4 (P2) Retirarse**: Depends on US1 `isTerminal` + `WithdrawPlayer` slice (can start after Foundational US1 store interface agreed)
- **Polish (Final)**: Depends on all desired stories (US1+US2 for MVP, US3+US4 for completeness)

### User Story Dependencies

- **US1 (P1)**: After Foundational — no deps
- **US2 (P1)**: After Foundational — integrates with US1's `question/canAnswer` but testable with mocked `getMyState`
- **US3 (P2)**: After Foundational — depends on US1 Timer tick
- **US4 (P2)**: After Foundational — depends on US1 `isTerminal` + `Withdraw` slice

### Within Each User Story

- Tests (if included) written before implementation (T010 before T014, T016 before T018, T020 before T022)
- Store/computed before component
- Component before integration wiring
- Core implementation before error states

### Parallel Opportunities

- Phase 1: T002 + T003 parallel (different files)
- Phase 2: T005 + T006 + T007 + T008 parallel (different files)
- Phase 3: T010 + T011 + T012 parallel (contract/store/integration tests different files); T013 parallel with T010 tests start
- Phase 4: T016 + T017 parallel (contract test vs component spec)
- Phase 5: T020 + T021 parallel (store spec vs timer component spec)
- Phase 6: T023 + T024 parallel (contract test vs component spec)
- Phase 7: T027 + T028 + T029 + T030 + T031 parallel (different files)
- Different stories can start in parallel after Foundational if staffed (US2 needs only `canAnswer` interface)

### Parallel Example: User Story 1 (Visualizar)

```bash
# Launch tests for US1 together:
Task T010: Contract test in tests/OroQuizClash.Api.Tests/Contracts/PlayerGameScreenContractTests.cs
Task T011: Game store unit test in src/Player/QuizArena.Player/src/app/stores/player-game.store.spec.ts
Task T012: Integration test in src/Player/QuizArena.Player/tests/integration/player-game-screen.spec.ts

# Launch store + UI together after tests:
Task T013: Enhance PlayerGameStore with PotentialReward in src/Player/QuizArena.Player/src/app/stores/player-game.store.ts
Task T014: GameComponent cinematic shell in src/Player/QuizArena.Player/src/app/features/game/game.component.ts
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (verify SPA/monolith)
2. Complete Phase 2: Foundational (10-element hydrate, Timer computed, realtime, interceptors, UI states verified)
3. Complete Phase 3: US1 (10 elementos cinematic)
4. **STOP and VALIDATE**: `GET /players/me` shows 10 elementos cinematic `data-theme="player"` 375-1536 no scroll, Timer 12s, Empty/Loading/Error/Expired/Terminal, WCAG, quickstart V1 SC-001/SC-008
5. Deploy/demo MVP (visualize works end-to-end)

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 → Test independently → Demo MVP (10 elementos)
3. Add US2 → Test independently → Demo (Answer progression + Potential)
4. Add US3 → Test independently → Demo (Timer <1s drift)
5. Add US4 → Test independently → Demo (Withdraw idempotente)
6. Polish → final validation V1-V6, SC-001..009

### Parallel Team Strategy

With 3 developers after Foundational:

1. Team completes Setup + Foundational together
2. Once Foundational done:
   - Developer A: US1 (10 elementos cinematic shell)
   - Developer B: US2 (Four Answers radiogroup + Submit progression)
   - Developer C: US3 (Timer drift) + US4 (Withdraw modal) (no overlap, both use store)
3. Polish by A/B/C after stories done

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability (US1..US4)
- Each user story independently completable and testable per spec Independent Test + quickstart V1-V4
- Verify tests fail before implementing (red → green)
- Commit after each task or logical group; push to branch `029-player-game`
- Avoid: vague tasks, same file conflicts, cross-story state leakage (e.g., `answer` leaking across rounds)
- Do not edit `.aspire/modules` — wire via AppHost.cs only
- Tokens: `design-system/tokens` + `data-theme="player"` per SPEC-016 cinematic premium

