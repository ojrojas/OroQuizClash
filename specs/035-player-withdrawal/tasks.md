# Tasks: Player Withdrawal (035)

**Input**: Design documents from `/specs/035-player-withdrawal/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md
**Branch**: `035-player-withdrawal` | **Constitution**: v1.1.0 (I-VI, A-J)

**Organization**: Tasks grouped by user story — each story independently testable and deliverable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify existing Player SPA (027+029+032+033) + modular monolith and prepare withdrawal scaffolding

- [x] T001 Verify existing project structure per `specs/035-player-withdrawal/plan.md` (`src/Player/QuizArena.Player` Angular 22 standalone, `src/OroQuizClash.Domain`, `Application`, `Infrastructure`, `Api`, `OroQuizClash.AppHost` in `OroQuizClash.slnx`) and `dotnet build OroQuizClash.slnx` passes
- [x] T002 Verify Player SPA dependencies in `src/Player/QuizArena.Player/package.json` (`@angular/core 22`, `@ngrx/signals 22`, `angular-auth-oidc-client`, `@microsoft/signalr`, `rxjs`) and `design-system/tokens/design-tokens.css` is imported via `angular.json` styles with `data-theme="player"` per SPEC-016
- [x] T003 Verify `BuildingBlocks` dependencies and `Directory.Packages.props` central versions for `net10.0` (`LangVersion latest`, `Nullable enable`) and `src/BuildingBlocks/` references per plan.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared withdrawal infrastructure MUST complete before ANY user story — `WithdrawPlayer` `POST /withdraw` `X-Idempotency-Key` per `gameId`, `GetMyPlayerState` 3 métricas, `PlayerGameStore` `withdraw()` `rxMethod`, `GameRealtimeService`, interceptors, shared UI states

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Verify `GamesApi` client in `src/Player/QuizArena.Player/src/app/features/shared/games.api.ts` has `withdraw(gameId, idempotencyKey)` (`POST /withdraw` `X-Idempotency-Key` `X-Correlation-Id`) and `getMyState(gameId)` (`GET /players/me` with `Score` `Current` `Secured` `Potential` 3 métricas + `GameSession` `WITHDRAWN`) per `contracts/api-contracts.md` §1-2
- [x] T005 [P] Verify `GameRealtimeService` in `src/Player/QuizArena.Player/src/app/core/realtime/game-realtime.service.ts` (`HubConnectionBuilder withUrl gameHubUrl?gameId accessTokenFactory`, `withAutomaticReconnect [0,2000,5000,10000,30000]`, events `PlayerWithdrawn/GameFinished/Reconnected` → `hydrate` per `sub`) per research.md
- [x] T006 [P] Verify interceptors in `src/Player/QuizArena.Player/src/app/core/interceptors/` — `correlation-id.interceptor.ts` (`X-Correlation-Id` UUID per `POST /withdraw`), `auth.interceptor.ts` (`Authorization: Bearer` only `apiUrl` secureRoutes), `error.interceptor.ts` (RFC 7807 `ProblemDetails` mapping `PlayerAlreadyWithdrawn 403`/`PlayerAlreadyEliminated 403`/`InvalidGameState 400`, 401 silentRenew, 429 RetryAfter) per plan.md Constraints H/I
- [x] T007 [P] Verify shared UI states in `src/Player/QuizArena.Player/src/app/shared/ui/` — `loading-skeleton.component.ts` (`aria-live="polite"` `aria-busy`), `empty-state.component.ts`, `error-state.component.ts` (CorrelationId/TraceId `Retry` 44px, `aria-live="assertive"`) per `data-model.md` UI States
- [x] T008 Verify `WithdrawPlayer` + `GetMyPlayerState` slices in `src/OroQuizClash.Application/Features/Games/WithdrawPlayer.cs` and `GetMyPlayerState.cs` (idempotent `X-Idempotency-Key` `idemp-withdraw-{gameId}` `UNIQUE` per `GamePlayerId` `RowVersion` per `GamePlayerId`, `WITHDRAWAL` ledger `deduction` per `WithdrawalPolicy` `KEEP_SECURED_SCORE` → `Current=Secured`, `PlayerAlreadyWithdrawn 403` `PlayerAlreadyEliminated 403` `InvalidGameState 400`) per `data-model.md`
- [x] T009 Verify `PlayerGameStore` intake in `src/Player/QuizArena.Player/src/app/stores/player-game.store.ts` (`signalStore withState {game, gameSession, round, question, answer, score, securedPoints, timer, status, _now}`, `computed isTerminal/canAnswer`, `withdraw()` `rxMethod` `X-Idempotency-Key` `idemp-withdraw-{gameId}` `isTerminal true` `canAnswer false` `Current=Secured`, `hydrate` via `GET /players/me` 3 métricas, `bindRealtime` `PlayerWithdrawn`) is intact from 029/032 to avoid regression before withdrawal extension
- [x] T010 Verify `Game` withdrawal invariants in `src/OroQuizClash.Domain/Games/Game.cs` (`Game.WithdrawPlayer(playerId)` `IBusinessRule` `PlayerNotWithdrawn` + `PlayerAlreadyEliminated` + `IsActive` + `WithdrawalPolicy` `KEEP_SECURED_SCORE` → `Score` `Current=Secured` + `PointTransaction` `WITHDRAWAL` `RowVersion` per `GamePlayerId`) per Constitution C/D/F and `data-model.md` §1

**Checkpoint**: Foundation ready — `dotnet build` passes, `POST /withdraw` `X-Idempotency-Key` per `gameId` idempotente + `Current/Secured/Potential` 3 métricas + `PlayerWithdrawn` `isTerminal`, `Game` `RowVersion` per `GamePlayerId`

---

## Phase 3: User Story 1 — Visualizar puntuaciones antes de retirarse (Priority: P1) 🎯 MVP

**Goal**: Diálogo de retiro muestra exactamente `Current Points` `Secured Points` `Potential Points` con `Score.totalPoints` `SecuredPoints` `PotentialReward` de `GET /players/me` sin cálculo cliente, formato `"{n} pts"` `aria-label` descriptivo, placeholder "—" si Potential no configurado, `aria-live polite`

**Independent Test**: Con `ROUND_IN_PROGRESS` `Score` `Current 400 Secured 200 Potential 100`, abrir `Withdrawal Action` → diálogo `Current 400 pts` `Secured 200 pts · checkpoint 2` `Potential 100 pts` coincidentes con `GET /players/me` ledger (0% cálculo cliente) (spec US1, quickstart V1, SC-001)

### Tests for User Story 1

- [x] T011 [P] [US1] Contract test for `GET /players/me` 3 métricas in `tests/OroQuizClash.Api.Tests/Contracts/PlayerWithdrawalMetricsContractTests.cs` (WebApplicationFactory JWT `PLAYER`, assert `score.totalPoints` `securedPoints.securedPoints` `checkpoint` `potentialReward` match ledger `Score` `SecuredPoints` `GameConfiguration`, `X-Correlation-Id` echo, `PlayerNotInGame` 403)
- [x] T012 [P] [US1] Withdrawal component unit test for 3 métricas in `src/Player/QuizArena.Player/src/app/features/game/withdrawal.component.spec.ts` (TestBed `WithdrawalComponent` mock `PlayerGameStore` with `score 400 secured 200 checkpoint 2 potential 100`, verify 3 métricas `Current` `Secured` `Potential` `aria-live polite` `aria-label` "Current Points 400" etc., `Potential "—"` when null, `Secured checkpoint 2` vs null no badge)
- [x] T013 [P] [US1] Integration test for 3 métricas rendering in `src/Player/QuizArena.Player/tests/integration/player-withdrawal-metrics.spec.ts` (mock `getMyState` 3 métricas, render `GameComponent`+`WithdrawalComponent` → assert 3 values visible, `data-theme="player"`, no client calc, `Potential "—"` fallback)

### Implementation for User Story 1

- [x] T014 [P] [US1] Create `WithdrawalDisplay` types in `src/Player/QuizArena.Player/src/app/features/game/withdrawal-display.model.ts` (export `WithdrawalDisplay {currentPoints, securedPoints, checkpointRoundNumber, potentialPoints, potentialDisplay}`, helper `formatPoints(n) => \`\${n} pts\``, `formatSecured(secured, checkpoint)` → `"{n} pts · checkpoint {m}"` or `"{n} pts"` per `data-model.md` §2) (depends on T010)
- [x] T015 [US1] Extend `WithdrawalComponent` to render 3 métricas autoritativas in `src/Player/QuizArena.Player/src/app/features/game/withdrawal.component.ts` (standalone `selector app-withdrawal`, inject `PlayerGameStore`, template `div metrics role="group" aria-label="Puntuaciones"` `Current Points {{store.score().totalPoints}} pts` `Secured Points {{formatSecured()}}` + `checkpoint` badge `Potential Points {{store.potentialReward()}}` per `contracts/ui-contracts.md` §1) (depends on T014)
- [x] T016 [US1] Add withdrawal metrics styles with tokens in `src/Player/QuizArena.Player/src/app/features/game/withdrawal.component.css` (create/extend CSS `metrics {display:flex; flex-direction:column; gap:var(--space-2)}` `.metric {padding:var(--space-3) min-height:44px border-radius:var(--radius-md) border:1px solid var(--color-border) background:var(--color-surface)}` tokens only) (depends on T015)
- [x] T017 [US1] Wire Withdrawal validation in `src/Player/QuizArena.Player/src/app/stores/player-game.store.ts` (ensure `hydrate` restores `score` + `securedPoints` + `game.configuration` from `GET /players/me` for diálogo 3 métricas, `computed potentialReward` fallback "—" when no `RewardRules`, no client `Current` calc per `data-model.md`) (depends on T014)

**Checkpoint**: US1 fully functional — `npm test` `withdrawal.component.spec` 3 métricas `Current/Secured/Potential` `aria-live polite` passes, contract `GET /players/me` 3 métricas 0% cliente SC-001, `axe` `group` passes, diálogo muestra 3 métricas 375 1col no scroll (quickstart V1 SC-001)

---

## Phase 4: User Story 2 — Confirmación con warnings de riesgo (Priority: P1)

**Goal**: Diálogo muestra 2 warnings exactos `"If you continue and answer incorrectly, you may lose your accumulated points."` `role="alert" aria-live assertive` + `"Withdraw now and secure X points?"` donde X=`SecuredPoints.securedPoints` dinámico, retiro requiere confirmación explícita 2 pasos `Withdrawal Action` → `Confirmar` `X-Idempotency-Key` per `gameId` `Cancel`/`Escape` no envía `POST /withdraw`

**Independent Test**: Abrir diálogo → verify warnings exactos `If you continue...` + `Withdraw now and secure 200 points?` X=`Secured`, `Confirmar` `min-height:44px` deshabilitado hasta interacción o requiere 2 pasos, `Cancelar` cierra sin `POST /withdraw` (spec US2, quickstart V2, SC-002/SC-003)

### Tests for User Story 2

- [x] T018 [P] [US2] Withdrawal component test for warnings + 2 pasos in `src/Player/QuizArena.Player/src/app/features/game/withdrawal.component.spec.ts` (mock `PlayerGameStore` `Secured 200`, verify warning1 `If you continue and answer incorrectly...` `role="alert"` `aria-live assertive`, warning2 `Withdraw now and secure 200 points?` dinámico X=`Secured`, `Confirmar` `aria-label="Confirmar retiro"` `min-height:44px`, `Cancelar` click `Escape` no `POST /withdraw`)
- [x] T019 [P] [US2] Withdrawal idempotency test in `src/Player/QuizArena.Player/src/app/stores/player-game.store.spec.ts` (verify `withdraw()` `X-Idempotency-Key` `idemp-withdraw-{gameId}` `sessionStorage` per `gameId`, `Confirmar` envía `POST /withdraw` con header `X-Idempotency-Key` + `X-Correlation-Id` + `Authorization Bearer`, `Cancelar` no envía)

### Implementation for User Story 2

- [x] T020 [US2] Extend `WithdrawalComponent` for warnings + 2 pasos in `src/Player/QuizArena.Player/src/app/features/game/withdrawal.component.ts` (add `div role="alert" aria-live="assertive"` warning1 exact `"If you continue and answer incorrectly, you may lose your accumulated points."` `var(--color-destructive)` + warning2 `Withdraw now and secure {{store.securedPoints().securedPoints}} points?` dinámico X=`Secured`, `Withdrawal Action` botón `min-height:44px` abre `showWithdrawConfirm=true` `role="dialog"` `aria-modal="true"` `aria-label="Confirmar retiro"`, `Confirmar` `min-height:44px` `aria-label="Confirmar retiro"` → `store.withdraw()` `X-Idempotency-Key` `idemp-withdraw-{gameId}`, `Cancelar` `Escape` `showWithdrawConfirm=false` sin llamada, per `contracts/ui-contracts.md` §1) (depends on T015)
- [x] T021 [US2] Add warnings + dialog styles with tokens in `src/Player/QuizArena.Player/src/app/features/game/withdrawal.component.css` (add `dialog {max-width:400px; background:var(--color-surface); border:1px solid var(--color-border); border-radius:var(--radius-lg); padding:var(--space-6); gap:var(--space-3)}` `warning {color:var(--color-destructive); font-weight:600}` `withdraw-secure {color:var(--color-warning); font-weight:600}` tokens only) (depends on T020)
- [x] T022 [US2] Wire Withdrawal 2 pasos in `src/Player/QuizArena.Player/src/app/features/game/game.component.ts` or `src/Player/QuizArena.Player/src/app/stores/player-game.store.ts` (ensure `GameComponent` `showWithdrawConfirm` boolean per `GameComponent` `providers` not global, `openWithdraw()` sets true, `confirmWithdraw()` reads `sessionStorage idemp-withdraw-{gameId} ?? crypto.randomUUID()` + `store.withdraw()` `X-Idempotency-Key` header, `Cancel`/`Escape` sets false without `withdraw` call, per `research.md` D2) (depends on T020)

**Checkpoint**: US1+US2 work — 3 métricas autoritativas 100% SC-001 + warnings exactos 100% SC-002 + 2 pasos 100% SC-003 `Cancel` no envía, `Confirmar` `X-Idempotency-Key` per `gameId`, quickstart V2 green

---

## Phase 5: User Story 3 — Retiro confirmado PlayerWithdrawn terminal (Priority: P1)

**Goal**: Confirmación `POST /withdraw` `X-Idempotency-Key` per `gameId` → `GameSession` `WITHDRAWN` `RowVersion` per `GamePlayerId` `isTerminal true` `canAnswer false` `Current` → `Secured` `KEEP_SECURED_SCORE` ledger idempotente sin duplicar `WITHDRAWAL`, bloquear `POST /answers` 403 `PlayerAlreadyWithdrawn` y `Question` `aria-disabled`

**Independent Test**: Con `Current 400 Secured 200`, abrir diálogo → `Confirmar` → `POST /withdraw` 200 `WITHDRAWN` `RowVersion++`, `isTerminal true` `canAnswer false`, `Current` 200 tras `hydrate`, `Question` bloqueado `aria-disabled`, reintento `POST /withdraw` misma key idempotente sin nuevo ledger, `POST /answers` 403 `PlayerAlreadyWithdrawn` (spec US3, quickstart V3, SC-004/SC-005/SC-006)

### Tests for User Story 3

- [x] T023 [P] [US3] Withdrawal terminal test in `src/Player/QuizArena.Player/src/app/stores/player-game.store.spec.ts` (mock `GamesApi.withdraw` `X-Idempotency-Key` `idemp-withdraw-gameId` → `WITHDRAWN` `RowVersion++` `Score` `Current 200 Secured 200` `Status isTerminal true canAnswer false`, verify `Question` `aria-disabled` via `store.isTerminal()` `canAnswer()`, reintento misma key idempotente no duplicar `WITHDRAWAL`, `POST /answers` tras `WITHDRAWN` 403)
- [x] T024 [P] [US3] Withdrawal component terminal test in `src/Player/QuizArena.Player/src/app/features/game/withdrawal.component.spec.ts` (mock `PlayerGameStore` `WITHDRAWN` `Secured 200` `Current 200`, verify `WITHDRAWN` `Player Status WITHDRAWN` `isTerminal true` `canAnswer false` `Question` `aria-disabled`, `Withdrawal Action` `disabled` si `isTerminal`, `ErrorState` `PlayerAlreadyWithdrawn` 403 `CorrelationId`)

### Implementation for User Story 3

- [x] T025 [US3] Implement `PlayerGameStore.withdraw` terminal in `src/Player/QuizArena.Player/src/app/stores/player-game.store.ts` (extend `withdraw: rxMethod<void>(pipe(switchMap(()=>{const key=sessionStorage.getItem(`idemp-withdraw-${gameId}`)??crypto.randomUUID(); sessionStorage.setItem(`idemp-withdraw-${gameId}`,key); return _api.withdraw(gameId,key).pipe(tapResponse({next:(gs)=>patchState({gameSession:gs, status:{...status(), playerStatus:'WITHDRAWN', isTerminal:true, canAnswer:false}}), error:(err)=>patchState({ui:{...ui(), error:err}})}))}))`, `hydrate` restores `WITHDRAWN` `isTerminal` `canAnswer false` `Score Current=Secured` per `WithdrawalPolicy` `KEEP_SECURED_SCORE`, idempotente same key per `research.md` D3) (depends on T020)
- [x] T026 [US3] Enhance `WithdrawalComponent` for PlayerWithdrawn terminal in `src/Player/QuizArena.Player/src/app/features/game/withdrawal.component.ts` or `src/Player/QuizArena.Player/src/app/features/game/game.component.ts` (add `Withdrawal Action` `disabled` if `store.isTerminal()` || `!store.status().canAnswer`, `QuestionComponent` `aria-disabled` when `store.isTerminal()`, `ErrorState` `PlayerAlreadyWithdrawn 403` `CorrelationId` + `Retry` reuses same `X-Idempotency-Key` per `gameId`, per `contracts/ui-contracts.md` §1) (depends on T025)

**Checkpoint**: US1+US2+US3 work — 3 métricas SC-001 + warnings SC-002 + 2 pasos SC-003 + idempotente 100% SC-004 + `WITHDRAWN` `isTerminal` `canAnswer false` 100% SC-005 + `Current=Secured` 100% SC-006 `KEEP_SECURED_SCORE`, quickstart V3 green

---

## Phase 6: User Story 4 — Responsive, accesible y premium del flujo de retiro (Priority: P2)

**Goal**: Diálogo `Cinematic` premium `data-theme="player"` tokens sin literales, responsive 375 1col / 768 `max-width:400px` centrado `gap var(--space-3)` targets ≥44px, WCAG 2.2 AA `role="dialog"` `aria-modal` `aria-label` "Confirmar retiro" `aria-live` warnings `outline:2px` `axe 0` `prefers-reduced-motion` reduce

**Independent Test**: Abrir diálogo en 375px → 3 métricas + 2 warnings apilados 1col `gap var(--space-3)` targets ≥44px sin scroll horizontal, en ≥768px centrado `max-width:400px` `padding:var(--space-6)` sin scroll; verificar `data-theme="player"` 0 literales `var(--space-*)`, `axe` 0 violations `role="dialog"` `aria-modal`, `Tab` navega `Cancelar`/`Confirmar` 100%, `Escape` cierra (spec US4, quickstart V4, SC-007)

### Tests for User Story 4

- [x] T027 [P] [US4] Responsive and a11y test for withdrawal dialog in `src/Player/QuizArena.Player/src/app/features/game/withdrawal.component.spec.ts` (set viewport 375/768/1536 → verify 375px `max-width:400px` centered no horizontal scroll, 768px `padding:var(--space-6)` `gap var(--space-3)` targets ≥44px via `getBoundingClientRect`, `data-theme="player"` exists, scan CSS for `var(--)` presence 0 literals, `axe` 0 violations `role="dialog"` `aria-modal` `aria-label` `aria-live` warnings `outline:2px`, `Escape` closes, `prefers-reduced-motion` reduce)
- [x] T028 [P] [US4] Quickstart V4 validation placeholder in `specs/035-player-withdrawal/quickstart.md` (verify `X-Correlation-Id` + `Authorization Bearer` per `POST /withdraw`, `data-theme` tokens, documented for manual run, will be executed in T035)

### Implementation for User Story 4

- [x] T029 [US4] Polish responsive withdrawal layout in `src/Player/QuizArena.Player/src/app/features/game/withdrawal.component.ts` and `withdrawal.component.css` (ensure `dialog` `max-width:400px` `position:fixed` `inset:0` `background:rgba(0,0,0,0.5)` `display:flex; align-items:center; justify-content:center;` `gap var(--space-3)` `min-height:44px` per button, `OroQuizClash.AppHost` still mounts `design-system/tokens` via `angular.json`) (depends on T015)
- [x] T030 [US4] Harden withdrawal a11y and tokens in `src/Player/QuizArena.Player/src/app/features/game/withdrawal.component.ts` (verify `role="dialog" aria-modal="true" aria-label="Confirmar retiro"` `role="alert"` warnings `aria-live assertive` `role="group"` métricas `aria-live polite`, foco `outline:2px solid var(--color-primary)` `Tab`/`Escape`/`Enter`, verify no inline style literals beyond `position:fixed` `inset:0` backdrop) (depends on T020)

**Checkpoint**: All 4 stories functional — 3 métricas autoritativas 100% SC-001, warnings exactos 100% SC-002, 2 pasos 100% SC-003, idempotente 100% SC-004, `WITHDRAWN` `isTerminal` 100% SC-005 `Current=Secured` 100% SC-006, responsive 375-1536 100% SC-007 `axe 0` + `prefers-reduced-motion`, quickstart V4 green

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Hardening, docs, observability, final validation

- [x] T031 [P] Add ProblemDetails mapping test for withdrawal errors in `tests/OroQuizClash.Api.Tests/Contracts/PlayerWithdrawalErrorsMappingTests.cs` (assert `PlayerAlreadyWithdrawn 403` `PlayerAlreadyEliminated 403` `InvalidGameState 400` `GameNotFound 404` map via `GlobalExceptionHandler` → `Result.ToHttpResult()` RFC 7807 with `CorrelationId/TraceId`, `X-Correlation-Id` echo, `X-Idempotency-Key` idempotente)
- [x] T032 [P] Verify `X-Correlation-Id` + JWT gating test in `src/Player/QuizArena.Player/tests/integration/player-withdrawal-correlation.spec.ts` (mock `GamesApi.withdraw` → assert header `X-Correlation-Id` UUID + `Authorization Bearer` per `POST /withdraw`, no JWT → 401 redirect OIDC, `ErrorState` displays `CorrelationId/TraceId` from `ProblemDetails`, `must_change_password` gating redirect)
- [x] T033 [P] Verify Secured 0 / Potential "—" edge cases in `src/Player/QuizArena.Player/src/app/features/game/withdrawal.component.spec.ts` (test `Secured 0 LOSE_ALL` → "Withdraw now and secure 0 points?" no break, `Potential "—"` → no NaN, `Game FINISHED 400` `InvalidGameState` `ErrorState` `CorrelationId`, `Secured` modificado DevTools → ledger autoritativo 200)
- [x] T034 [P] Update design-system and Player README in `src/Player/QuizArena.Player/README.md` and `specs/035-player-withdrawal/spec.md` Status (add `Player Withdrawal` section: `Withdrawal Action` → diálogo 3 métricas `Current/Secured/Potential` + 2 warnings `If you continue` `Withdraw now and secure X` `X-Idempotency-Key` per `gameId` `PlayerWithdrawn` `isTerminal` `canAnswer false` `Current=Secured` `data-theme="player"` WCAG responsive, `max-width:400px`)
- [x] T035 [P] Run quickstart validation in `specs/035-player-withdrawal/quickstart.md` (execute V1-V4: 3 métricas ledger, warnings exactos + 2 pasos `X-Idempotency-Key`, PlayerWithdrawn `isTerminal` `Current=Secured`, responsive 375-1536 axe, X-Correlation-Id + JWT, fix gaps if any)
- [x] T036 Add architecture test for withdrawal isolation in `tests/OroQuizClash.Architecture.Tests/PlayerWithdrawalIsolationTests.cs` (verify `WithdrawalComponent`/`PlayerGameStore` not in `OroQuizClash.Domain` (Domain ↛ Angular), `WithdrawPlayer` uses `sub` not body, no client `deduction` calc (Domain `WithdrawalPolicy`), BuildingBlocks `IRepository` not leaked, `WithdrawalAction` per `GamePlayer` not global)
- [x] T037 Security hardening verify in `src/Player/QuizArena.Player/src/app/core/interceptors/error.interceptor.ts` (hide sensitive details, propagate `traceId`, handle 429 `GamePlayLimiter` `Retry-After`, ensure `Withdraw` never leaks `Secured` de otro, `PlayerAlreadyWithdrawn` audit logged, verify `withdraw` `Bearer` only `apiUrl`)
- [x] T038 Final `dotnet build OroQuizClash.slnx && dotnet test` green and `npm run lint` with `@ngrx/eslint-plugin` clean and `npm test -- --watch=false` `withdrawal.component` + `player-game.store` pass, update `specs/035-player-withdrawal/spec.md` Status to `Ready for Review`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately (verifies Angular 22 SPA + monolith BuildingBlocks)
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (GamesApi `withdraw` `X-Idempotency-Key` per `gameId` + `GetMyPlayerState` 3 métricas, `PlayerGameStore` `withdraw()` `rxMethod` `isTerminal`, `GameRealtimeService` `isTerminal`, `Game` `RowVersion` per `GamePlayer`)
- **User Stories (Phase 3+)**: All depend on Foundational
  - **US1 (P1) 3 métricas Current/Secured/Potential**: No other story dependency — MVP (3 métricas sin cálculo cliente)
  - **US2 (P1) Confirmación warnings**: Depends on US1 `Withdrawal Action` diálogo 3 métricas (needs T014/T015) but independently testable with mocked `GameSession`
  - **US3 (P1) PlayerWithdrawn terminal**: Depends on US1 `Current=Secured` + `withdraw()` `X-Idempotency-Key` (needs T015/T025) but testable with mocked `withdraw` 200
  - **US4 (P2) Responsive/a11y premium**: Depends on US1/US2/US3 `withdrawal.component.ts/.css` layout (needs T015/T020/T025) — polish parallel with US2 if staffed
- **Polish (Final)**: Depends on all desired stories (US1+US2 for MVP, US3 for completeness)

### User Story Dependencies

- **US1 (P1)**: After Foundational — no deps
- **US2 (P1)**: After Foundational — depends on US1 `Withdrawal Action` diálogo 3 métricas (needs T014/T015) but testable with mocked `GameSession`
- **US3 (P1)**: After Foundational + US1 `Current=Secured` + `withdraw()` `X-Idempotency-Key` (needs T015/T025) but testable with mocked `withdraw` 200
- **US4 (P2)**: After Foundational — depends on US1/US2/US3 layout but can start after US1 metrics

### Within Each User Story

- Tests (if included) written before implementation (T011 before T014, T018 before T020, T023 before T025, T027 before T029)
- Types/helper (`withdrawal-display.model.ts` T014) before store (T015) before component (T020)
- Store before component UI, component before `GameComponent` integration
- Core implementation before `PlayerWithdrawn` terminal before responsive polish

### Parallel Opportunities

- Phase 1: T002 + T003 parallel (different files)
- Phase 2: T005 + T006 + T007 parallel (different files); T004 later but independent
- Phase 3: T011 + T012 + T013 parallel (contract test / component spec / integration test different files); T014 parallel with T011 tests start (different files)
- Phase 4: T018 + T019 parallel (component spec vs store spec); T020 needs T018 T019; T021 needs T020
- Phase 5: T023 + T024 parallel (component spec vs store spec); T025 needs T023 T024 contracts; T026 needs T025
- Phase 6: T027 + T028 parallel (component spec vs quickstart placeholder); T029/T030 sequential same file `withdrawal.component.css/ts`
- Phase 7: T031 + T032 + T033 + T034 + T035 parallel (different files); T036 after all
- Different stories can start in parallel after Foundational if staffed (US2 needs only `Secured` interface agreed, US3 needs only `withdraw` signature)

### Parallel Example: User Story 1 (3 métricas Current/Secured/Potential)

```bash
# Launch tests for US1 together:
Task T011: Contract test in tests/OroQuizClash.Api.Tests/Contracts/PlayerWithdrawalMetricsContractTests.cs
Task T012: Withdrawal component unit test in src/Player/QuizArena.Player/src/app/features/game/withdrawal.component.spec.ts
Task T013: Integration test in src/Player/QuizArena.Player/tests/integration/player-withdrawal-metrics.spec.ts

# Launch types + component after tests:
Task T014: WithdrawalDisplay types in src/Player/QuizArena.Player/src/app/features/game/withdrawal-display.model.ts
Task T015: WithdrawalComponent 3 métricas in src/Player/QuizArena.Player/src/app/features/game/withdrawal.component.ts
```

### Parallel Example: User Story 2 (Confirmación warnings)

```bash
# Launch tests:
Task T018: Withdrawal component test in src/Player/QuizArena.Player/src/app/features/game/withdrawal.component.spec.ts
Task T019: PlayerGameStore test in src/Player/QuizArena.Player/src/app/stores/player-game.store.spec.ts

# Launch implementation:
Task T020: WithdrawalComponent warnings in src/Player/QuizArena.Player/src/app/features/game/withdrawal.component.ts
Task T022: Withdrawal 2 pasos in src/Player/QuizArena.Player/src/app/features/game/game.component.ts
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (verify SPA/monolith)
2. Complete Phase 2: Foundational (GamesApi `withdraw` `X-Idempotency-Key` per `gameId` + `GetMyPlayerState` 3 métricas, `PlayerGameStore` `withdraw()` `rxMethod` `isTerminal`, `GameRealtimeService` `isTerminal`, `Game` `RowVersion` per `GamePlayer`)
3. Complete Phase 3: US1 (3 métricas `Current/Secured/Potential` sin cálculo cliente, `Potential "—"` fallback, `Secured checkpoint 2` vs null, `aria-live polite`)
4. **STOP and VALIDATE**: `GET /players/me` shows 3 métricas sin cálculo cliente 0% SC-001, `WithdrawalComponent` 3 métricas `aria-live polite` passes, `axe` `group` passes, quickstart V1 SC-001
5. Deploy/demo MVP (3 métricas works end-to-end)

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 → Test independently → Demo MVP (3 métricas autoritativas)
3. Add US2 → Test independently → Demo (warnings exactos + 2 pasos `X-Idempotency-Key` per `gameId`)
4. Add US3 → Test independently → Demo (PlayerWithdrawn `isTerminal` `canAnswer false` `Current=Secured` `WITHDRAWAL` ledger idempotente)
5. Add US4 → Test independently → Demo (Responsive 375 1col / 768 `max-width:400px` + WCAG AA axe + `X-Correlation-Id`)
6. Polish → final validation V1-V4, SC-001..008

### Parallel Team Strategy

With 3 developers after Foundational:

1. Team completes Setup + Foundational together
2. Once Foundational done:
   - Developer A: US1 (3 métricas `WithdrawalDisplay` + `WithdrawalComponent` skeleton)
   - Developer B: US2 (warnings exactos + 2 pasos `X-Idempotency-Key` per `gameId`) + US3 (PlayerWithdrawn `isTerminal` `canAnswer false` `Current=Secured` `WITHDRAWAL` ledger)
   - Developer C: US4 (Responsive premium polish `max-width:400px` `prefers-reduced-motion` + `withAutomaticReconnect` per `sub`)
3. Polish by A/B/C after stories done

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability (US1..US4)
- Each user story independently completable and testable per spec Independent Test + quickstart V1-V4
- Verify tests fail before implementing (red → green)
- Commit after each task or logical group; push to branch `035-player-withdrawal`
- Avoid: vague tasks, same file conflicts, cross-story state leakage (e.g., `Secured` X calculated cliente instead of `securedPoints.securedPoints`)
- Do not edit `.aspire/modules` — wire via AppHost.cs only
- Tokens: `design-system/tokens` + `data-theme="player"` per SPEC-016 cinematic premium + `prefers-reduced-motion`
