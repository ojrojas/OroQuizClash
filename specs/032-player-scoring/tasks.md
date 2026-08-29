# Tasks: Player Scoring (032)

**Input**: Design documents from `/specs/032-player-scoring/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md
**Branch**: `032-player-scoring` | **Constitution**: v1.1.0 (I-VI, A-J)

**Organization**: Tasks grouped by user story — each story independently testable and deliverable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify existing Player SPA (027+029+030+031) + modular monolith and prepare scoring scaffolding

- [x] T001 Verify existing project structure per `specs/032-player-scoring/plan.md` (`src/Player/QuizArena.Player` Angular 22 standalone, `src/OroQuizClash.Domain`, `Application`, `Infrastructure`, `Api`, `OroQuizClash.AppHost` in `OroQuizClash.slnx`) and `dotnet build OroQuizClash.slnx` passes
- [x] T002 Verify Player SPA dependencies in `src/Player/QuizArena.Player/package.json` (`@angular/core 22`, `@ngrx/signals 22`, `angular-auth-oidc-client`, `@microsoft/signalr`, `rxjs`) and `design-system/tokens/design-tokens.css` is imported via `angular.json` styles with `data-theme="player"` per SPEC-016
- [x] T003 Verify `BuildingBlocks` dependencies and `Directory.Packages.props` central versions for `net10.0` (`LangVersion latest`, `Nullable enable`) and `src/BuildingBlocks/` references per plan.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared scoring infrastructure MUST complete before ANY user story — `GetMyPlayerState` 5 métricas, `PlayerGameStore` 10 elementos, `GameRealtimeService`, interceptors, shared UI states

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Verify `GamesApi` client in `src/Player/QuizArena.Player/src/app/features/shared/games.api.ts` has `getMyState(gameId)` (`GET /players/me` with `Score` 5 métricas `Current/Secured/Potential/Round/Total` + `SecuredPoints` + `GameConfiguration`) and `getGame(gameId)` per `contracts/api-contracts.md` §1
- [x] T005 [P] Verify `GameRealtimeService` in `src/Player/QuizArena.Player/src/app/core/realtime/game-realtime.service.ts` (`HubConnectionBuilder withUrl gameHubUrl?gameId accessTokenFactory`, `withAutomaticReconnect [0,2000,5000,10000,30000]`, events `ScoreUpdated/RoundCompleted/RoundStarted/GameFinished/Reconnected` → `hydrate`) per research.md
- [x] T006 [P] Verify interceptors in `src/Player/QuizArena.Player/src/app/core/interceptors/` — `correlation-id.interceptor.ts` (`X-Correlation-Id` UUID per `GET /players/me`), `auth.interceptor.ts` (`Authorization: Bearer` only `apiUrl` secureRoutes), `error.interceptor.ts` (RFC 7807 `ProblemDetails` mapping, 401 silentRenew, 429 RetryAfter) per plan.md Constraints H/I
- [x] T007 [P] Verify shared UI states in `src/Player/QuizArena.Player/src/app/shared/ui/` — `loading-skeleton.component.ts` (`aria-live="polite"` `aria-busy`), `empty-state.component.ts`, `error-state.component.ts` (CorrelationId/TraceId `Retry` 44px, `aria-live="assertive"`) per `data-model.md` UI States
- [x] T008 Verify `GetMyPlayerState` slice in `src/OroQuizClash.Application/Features/Games/GetMyPlayerState.cs` (returns `Score` `TotalPoints= sum(PointTransaction)` autoritativo, `SecuredPoints` `checkpointRoundNumber` nullable, `GameConfiguration.PointsPerRound` para `Potential`, `RowVersion`) per `data-model.md`
- [x] T009 Verify `PlayerGameStore` intake in `src/Player/QuizArena.Player/src/app/stores/player-game.store.ts` (`signalStore withState {game, gameSession, round, question, answer, score, securedPoints, timer, status, _now}`, `computed potentialReward/roundPoints/totalPoints`, `hydrate` via `GET /players/me`, `serverNow` correction, `bindRealtime` `ScoreUpdated`) is intact from 029/031 to avoid regression before scoring extension
- [x] T010 Verify `PointTransaction` ledger invariant in `src/OroQuizClash.Domain/Games/Game.cs` (`AwardPoints/RemovePoints/SecurePoints` generan `PointTransaction` append-only, `CHECK` ledger `sum=total`, `Game` RowVersion) per Constitution D and `data-model.md` §3

**Checkpoint**: Foundation ready — `dotnet build` passes, `GET /players/me` returns 5 métricas ledger + `Score`/`Secured` + `Potential` fallback "—", realtime → hydrate, UI states ready

---

## Phase 3: User Story 1 — Visualizar cinco puntuaciones autoritativas (Priority: P1) 🎯 MVP

**Goal**: Pantalla muestra exactamente 5 métricas autoritativas `Current Points` `Secured Points` `Potential Points` `Round Points` `Total Points` con `Score.totalPoints` `SecuredPoints` `Potential` `Round` derivados de `GET /players/me` sin cálculo cliente, formato `"{n} pts"` `aria-label` descriptivo, placeholder "—" si Potential no configurado, `aria-live polite`

**Independent Test**: Con `ROUND_IN_PROGRESS` `PointTransaction` ledger `Current 350 Secured 200 checkpoint 3 Potential 100 Round 50 Total 850`, abrir `/player/game/:id` → 5 valores coinciden con `GET /players/me` (0% cálculo cliente), `Potential` "—" si no configurado (spec US1, quickstart V1, SC-001)

### Tests for User Story 1

- [x] T011 [P] [US1] Contract test for `GET /players/me` 5 métricas in `tests/OroQuizClash.Api.Tests/Contracts/PlayerScoringContractTests.cs` (WebApplicationFactory JWT `PLAYER`, assert `score.totalPoints` `securedPoints.securedPoints` `checkpoint` `roundPoints` match ledger `sum(PointTransaction)` `X-Correlation-Id` echo, `PlayerNotInGame` 403)
- [x] T012 [P] [US1] Score panel component unit test for 5 métricas in `src/Player/QuizArena.Player/src/app/features/game/score-panel.component.spec.ts` (TestBed `ScorePanelComponent` mock `PlayerGameStore` with `score 350 secured 200 checkpoint 3 potential 100 round 50 total 850`, verify 5 `role="status"` `aria-live="polite"` `aria-label` "Current Points 350" etc., `Potential "—"` when null, `Secured checkpoint 3` vs null no badge, `Round Points` "en juego")
- [x] T013 [P] [US1] Integration test for 5 métricas rendering in `src/Player/QuizArena.Player/tests/integration/player-scoring-metrics.spec.ts` (mock `getMyState` 5 métricas, render `GameComponent`+`ScorePanelComponent` → assert 5 values visible, `data-theme="player"`, no client calc, `Potential "—"` fallback)

### Implementation for User Story 1

- [x] T014 [P] [US1] Create `ScoringDisplayState` types in `src/Player/QuizArena.Player/src/app/features/game/scoring-display.model.ts` (export `ScoringDisplayState {currentPoints, securedPoints, checkpointRoundNumber, potentialPoints, potentialDisplay, roundPoints, totalPoints, isLoading, errorDetail, correlationId}`, helper `formatPoints(n) => \`\${n} pts\``, `formatSecured(secured, checkpoint)` → `"{n} pts · checkpoint {m}"` or `"{n} pts"`, `Potential` fallback "—") per `data-model.md` §6
- [x] T015 [US1] Extend `ScorePanelComponent` to render 5 métricas autoritativas in `src/Player/QuizArena.Player/src/app/features/game/score-panel.component.ts` (standalone `selector app-score-panel`, inject `PlayerGameStore`, template `div scoring-grid role="group" aria-label="Puntuaciones"` 5 `div metric role="status" aria-live="polite"` with `Current Points {{store.score().totalPoints}} pts`, `Secured Points {{store.securedPoints().securedPoints}} pts` + `checkpoint` badge, `Potential Points {{store.potentialReward()}}`, `Round Points {{roundPoints()}} pts en juego`, `Total Points {{store.score().totalPoints}} pts`, per `contracts/ui-contracts.md` §1) (depends on T014)
- [x] T016 [US1] Add scoring styles with tokens in `src/Player/QuizArena.Player/src/app/features/game/score-panel.component.css` (create/extend CSS `scoring-grid {display:grid; grid-template-columns:1fr; gap:var(--space-3)} @media(min-width:768px){grid-template-columns:repeat(5,1fr)}` 1col 375 / 5col ≥768, `.metric {display:flex flex-direction:column gap:var(--space-1) padding:var(--space-3) min-height:44px border-radius:var(--radius-md) border:1px solid var(--color-border) background:var(--color-surface)}` `.metric.current .value {color:var(--color-primary); font-weight:700}` `.metric.secured .value {color:var(--color-success)}` `.metric.total .value.total-bold {color:var(--color-primary); font-weight:700; font-size:var(--font-size-lg)}` tokens only) (depends on T015)
- [x] T017 [US1] Wire ScorePanel validation in `src/Player/QuizArena.Player/src/app/stores/player-game.store.ts` (ensure `hydrate` restores `score` + `securedPoints` + `game.configuration` from `GET /players/me`, `computed potentialReward` fallback "—" when no `RewardRules`, `roundPoints` computed `score().roundPoints ?? 0` or `totalPoints - securedPoints` per `data-model.md`, no client `Total` sum) (depends on T014)

**Checkpoint**: US1 fully functional — `npm test` `score-panel.component.spec` 5 `role="status"` `aria-live polite` + `Potential "—"` + `Secured checkpoint` passes, contract `GET /players/me` 5 métricas 0% cliente SC-001, `axe` `group` passes, `/player/game/:id` shows 5 métricas 375 1col / 768 5col no scroll (quickstart V1 SC-001)

---

## Phase 4: User Story 2 — Evolución en tiempo real vía SPEC-012 (Priority: P1)

**Goal**: Cinco métricas se actualizan automáticamente en <1s tras `ScoreUpdated`/`RoundCompleted`/`Reconnected` vía `GameRealtimeService` `withAutomaticReconnect` → `hydrate` `GET /players/me` (no payload trust), animación `pulse 600ms` en `Current Points` tras `ScoreUpdated`, cliente nunca modifica `Current Points` localmente (server truth)

**Independent Test**: Con `Score 350 Round 50`, emitir `ScoreUpdated` + `ANSWER_CORRECT +100` → UI `Current 450 Round 150 Total 450` en <1s sin recarga, solo vía `hydrate` disparado por evento; cliente intento `Current Points +100` local → descartado en siguiente `hydrate` (US2, quickstart V2, SC-002/SC-003)

### Tests for User Story 2

- [x] T018 [P] [US2] Score panel store unit test for realtime hydrate in `src/Player/QuizArena.Player/src/app/stores/player-game.store.spec.ts` (verify `hydrateFor(gameId)` mock `getMyState` with `score 450 secured 200` → `score().totalPoints 450`, `ScoreUpdated` event triggers `hydrate`, `Reconnected` triggers `hydrate`, `isScorePulse` true 600ms then false)
- [x] T019 [P] [US2] Score panel component test for ScoreUpdated pulse in `src/Player/QuizArena.Player/src/app/features/game/score-panel.component.spec.ts` (mock `PlayerGameStore` with `score 350`, emit `ScoreUpdated` → `hydrate` → `Current Points 450` visible `pulse` class, `prefers-reduced-motion` reduce none, no client calc)

### Implementation for User Story 2

- [x] T020 [US2] Implement `PlayerGameStore` realtime scoring hydrate in `src/Player/QuizArena.Player/src/app/stores/player-game.store.ts` (extend `bindRealtime` to listen `ScoreUpdated/RoundCompleted/RoundStarted/GameFinished/Reconnected` → `hydrateFor(gameId)` `GET /players/me`, `computed isScorePulse` `signal` true on `ScoreUpdated` then `setTimeout 600ms` false, `patchState({score, securedPoints, game})` per `research.md` D2) (depends on T015)
- [x] T021 [US2] Enhance `ScorePanelComponent` for realtime pulse in `src/Player/QuizArena.Player/src/app/features/game/score-panel.component.ts` (add `pulse` class on `Current Points` when `store.isScorePulse()` true, `aria-live="polite"` per metric, ensure `GameComponent` `ngOnInit` calls `store.bindRealtime` already includes `ScoreUpdated`) (depends on T020)
- [x] T022 [US2] Add realtime pulse styles in `src/Player/QuizArena.Player/src/app/features/game/score-panel.component.css` (add `.pulse {animation: pulse 600ms ease}` `@keyframes pulse 0%,100% opacity1 50% opacity0.8` `@media prefers-reduced-motion reduce animation none` tokens only) (depends on T021)

**Checkpoint**: US1+US2 work — 5 métricas autoritativas 100% SC-001, realtime <1s 100% SC-002 0% cliente mutación SC-003, `hydrate` tras `ScoreUpdated`/`Reconnected`, quickstart V2 green

---

## Phase 5: User Story 3 — Distinguir políticas y estados de puntuación (Priority: P2)

**Goal**: Distinguir visualmente `Secured Points` ("asegurado" badge `var(--color-success)` con `checkpoint 3`) vs `Round Points` ("en juego" `var(--color-warning)`) y `Potential Points` ("Próximo: Pack Oro 500 pts" o "—") según `LossPolicy`/`WithdrawalPolicy`/`RewardRules`, sin cálculo cliente

**Independent Test**: Con `Secured 200 checkpoint 3` `Round 80` `LossPolicy LOSE_UNSECURED_POINTS` `RewardRules` próximo 500 → UI `Secured` "200 · checkpoint 3 asegurado", `Round` "80 en juego", `Potential` "Próximo: Pack Oro 500 pts"; sin `checkpoint` → "200 pts" sin badge; sin `RewardRules` → "—" `aria-label` "Potential no disponible" (US3, quickstart V3, SC-004/SC-005)

### Tests for User Story 3

- [x] T023 [P] [US3] Score panel test for Secured checkpoint & Potential in `src/Player/QuizArena.Player/src/app/features/game/score-panel.component.spec.ts` (mock `securedPoints 200 checkpoint 3` → `Secured` "200 · checkpoint 3" badge `asegurado`, mock `checkpoint null` → "200 pts" no badge, mock `potentialReward "—"` → "—" `aria-label` "Potential no disponible", `Round Points` "80 en juego" `var(--color-warning)`)
- [x] T024 [P] [US3] PlayerGameStore test for Potential & RoundPoints in `src/Player/QuizArena.Player/src/app/stores/player-game.store.spec.ts` (mock `game.configuration.rewardRules` → `potentialReward()` next threshold, mock no `RewardRules` → "—", mock `score.roundPoints 80` → `roundPoints()` 80, `LossPolicy` no affect display)

### Implementation for User Story 3

- [x] T025 [US3] Implement `ScorePanelComponent` Secured/Potential/Round distinguir in `src/Player/QuizArena.Player/src/app/features/game/score-panel.component.ts` (add `formatSecured()` helper `"{secured} pts · checkpoint {n}"` vs `"{secured} pts"`, badge `asegurado` when `store.isSecured()` or `securedPoints().securedPoints>0 && checkpoint!=null`, `Potential` display via `store.potentialReward()` already from 029, `Round Points` via `roundPoints()` computed, per `contracts/ui-contracts.md` §1) (depends on T015)
- [x] T026 [US3] Add Secured/Potential/Round styles in `src/Player/QuizArena.Player/src/app/features/game/score-panel.component.css` (add `.badge.asegurado {background:var(--color-success-subtle); color:var(--color-success); padding:var(--space-1) var(--space-2); border-radius:var(--radius-sm)}` `.metric.round .value {color:var(--color-warning)}` `.metric.potential .value {color:var(--color-accent)}` tokens only) (depends on T025)

**Checkpoint**: US1+US2+US3 work — `Secured checkpoint 3` badge 100% SC-004, `Potential "—"` 100% SC-005 no break layout, `Round` "en juego" `var(--color-warning)`, quickstart V3 green

---

## Phase 6: User Story 4 — Responsive, accesible y premium del bloque de puntuaciones (Priority: P2)

**Goal**: Bloque 5 métricas `Cinematic` premium `data-theme="player"` tokens sin literales, responsive 375 1 col / 768 5 col `gap var(--space-3)` targets ≥44px footer competitivo, WCAG 2.2 AA `role="group"` `aria-live polite` `aria-label` por métrica foco `outline:2px` `axe 0` `prefers-reduced-motion` reduce

**Independent Test**: Resize 375px → 1 col no scroll, 768/1280/1536 → 5 col `gap var(--space-3)` targets ≥44px 100%; inspect CSS `data-theme="player"` 0 literales `var(--space-*) var(--color-*)`; `axe` 0 violations `group` `aria-live` `aria-label`; `Tab` navega métricas 100% (US4, quickstart V4, SC-006/SC-007/SC-008)

### Tests for User Story 4

- [x] T027 [P] [US4] Responsive and a11y test for scoring in `src/Player/QuizArena.Player/src/app/features/game/score-panel.component.spec.ts` (set viewport 375/768/1536 → verify 375px `grid-template-columns 1fr` no horizontal scroll, 768px `repeat(5,1fr)` 5col, `data-theme="player"` exists, scan CSS for `var(--)` presence 0 literals, `axe` 0 violations `role="group"` `aria-live polite` `aria-label`, keyboard `Tab` navigates metrics, targets ≥44px via `getBoundingClientRect`, `prefers-reduced-motion` reduce)
- [x] T028 [P] [US4] Quickstart V4 validation placeholder in `specs/032-player-scoring/quickstart.md` (verify `X-Correlation-Id` header per `GET /players/me` + JWT required 401, `data-theme` tokens, documented for manual run, will be executed in T034)

### Implementation for User Story 4

- [x] T029 [US4] Polish responsive scoring layout in `src/Player/QuizArena.Player/src/app/features/game/score-panel.component.ts` and `score-panel.component.css` (ensure `scoring-grid` 1fr 375 + `repeat(5,1fr)` ≥768 `gap var(--space-3)` `min-height:44px min-width:44px` per metric, `total-bold` `font-size var(--font-size-lg)` `gap` tokens, verify `GameComponent` footer still `280px 1fr` with ladder sidebar (030) + center question 031 + footer 5 métricas, no scroll horizontal 375-1536, `OroQuizClash.AppHost` still mounts `design-system/tokens` via `angular.json`) (depends on T015)
- [x] T030 [US4] Harden scoring a11y and tokens in `src/Player/QuizArena.Player/src/app/features/game/score-panel.component.ts` (verify `role="group" aria-label="Puntuaciones"` + métricas `role="status" aria-live="polite"` `aria-label` per metric "Current Points 350", foco `outline:2px solid var(--color-primary)`, `aria-live` `polite` por métrica, `prefers-reduced-motion` `pulse` none, verify no inline style literals) (depends on T025)

**Checkpoint**: All 4 stories functional — 5 métricas autoritativas 100% SC-001, realtime <1s 100% SC-002 0% cliente SC-003, `Secured checkpoint` 100% SC-004, `Potential "—"` 100% SC-005, responsive 375-1536 100% SC-006, WCAG AA 100% SC-007 `axe 0` + `prefers-reduced-motion` 100% SC-008, quickstart V4 green

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Hardening, docs, observability, final validation

- [x] T031 [P] Add ProblemDetails mapping test for scoring errors in `tests/OroQuizClash.Api.Tests/Contracts/PlayerScoringErrorsMappingTests.cs` (assert `PlayerNotInGame 403` `GameNotFound 404` `InvalidGameState 400` map via `GlobalExceptionHandler` → `Result.ToHttpResult()` RFC 7807 with `CorrelationId/TraceId`, `X-Correlation-Id` echo)
- [x] T032 [P] Verify `X-Correlation-Id` + JWT gating test in `src/Player/QuizArena.Player/tests/integration/player-scoring-correlation.spec.ts` (mock `GamesApi.getMyState` → assert header `X-Correlation-Id` UUID + `Authorization Bearer` per `GET /players/me`, no JWT → 401 redirect OIDC, `ErrorState` displays `CorrelationId/TraceId` from `ProblemDetails`, `must_change_password` gating redirect)
- [x] T033 [P] Verify ledger/TotalPoints edge cases in `src/Player/QuizArena.Player/src/app/stores/player-game.store.spec.ts` (test `PointTransaction` 0 → 5 métricas 0 sin NaN, `checkpoint null` → sin badge, `Potential null` → "—", `RoundPoints` > `CurrentPoints` por corrección → muestra ledger tal cual, `ScoreUpdated` durante `Evaluating` → no bloquea scoring, `TotalPoints == sum(PointTransaction)` server truth)
- [x] T034 [P] Update design-system and Player README in `src/Player/QuizArena.Player/README.md` and `specs/032-player-scoring/spec.md` Status (add `Player Scoring` section: 5 métricas `Current/Secured/Potential/Round/Total` autoritativas `PlayerGameStore` `ScoreUpdated→hydrate` server truth `data-theme="player"` WCAG responsive, 5col `var(--space-3)` 44px)
- [x] T035 [P] Run quickstart validation in `specs/032-player-scoring/quickstart.md` (execute V1-V4: 5 métricas ledger sin cliente calc, realtime <1s ScoreUpdated→hydrate, Secured checkpoint/Potential "—", responsive 375-1536 axe, X-Correlation-Id + JWT, fix gaps if any)
- [x] T036 Add architecture test for scoring isolation in `tests/OroQuizClash.Architecture.Tests/PlayerScoringIsolationTests.cs` (verify `ScorePanelComponent`/`PlayerGameStore` not in `OroQuizClash.Domain` (Domain ↛ Angular), `GetMyPlayerState` uses `sub` not body, no client `TotalPoints` calc (Domain `sum(PointTransaction)`), BuildingBlocks `IRepository` not leaked)
- [x] T037 Security hardening verify in `src/Player/QuizArena.Player/src/app/core/interceptors/error.interceptor.ts` (hide sensitive details, propagate `traceId`, handle 429 `GamePlayLimiter` `Retry-After` already from 027/030, ensure `Score` not leaked cross-Player (F), `PlayerIdentityMismatch` audit logged, verify `getMyState` `Bearer` only `apiUrl`)
- [x] T038 Final `dotnet build OroQuizClash.slnx && dotnet test` green and `npm run lint` with `@ngrx/eslint-plugin` clean and `npm test -- --watch=false` `score-panel.component` + `player-game.store` pass, update `specs/032-player-scoring/spec.md` Status to `Ready for Review`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately (verifies Angular 22 SPA + monolith BuildingBlocks)
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (GamesApi `getMyState` 5 métricas, `PlayerGameStore` 10 elementos, `GameRealtimeService` `ScoreUpdated→hydrate`, interceptors, `PointTransaction` ledger invariant)
- **User Stories (Phase 3+)**: All depend on Foundational
  - **US1 (P1) 5 métricas autoritativas**: No other story dependency — MVP (5 métricas sin cliente calc)
  - **US2 (P1) Realtime ScoreUpdated→hydrate**: Depends on US1 `score-panel` 5 métricas + `PlayerGameStore` (needs T014/T015) but independently testable with mocked `GET /players/me`
  - **US3 (P2) Secured/Potential/Round distinguir**: Depends on US1 `Secured/Potential` (needs T015) but testable with mocked ledger
  - **US4 (P2) Responsive/a11y premium**: Depends on US1/US2/US3 `score-panel.component.ts/.css` layout (needs T015/T021/T025) — polish parallel with US2 if staffed
- **Polish (Final)**: Depends on all desired stories (US1+US2 for MVP, US3+US4 for completeness)

### User Story Dependencies

- **US1 (P1)**: After Foundational — no deps
- **US2 (P1)**: After Foundational + US1 types/component (integrates with US1's 5 métricas but testable with mocked `getMyState`)
- **US3 (P2)**: After Foundational — depends on US1 `Secured/Potential` but can start after US1 `ScoringDisplayState`
- **US4 (P2)**: After Foundational — depends on US1/US2/US3 layout but can start after US1 Idle

### Within Each User Story

- Tests (if included) written before implementation (T011 before T014, T018 before T020, T023 before T025, T027 before T029)
- Types/helper (`scoring-display.model.ts` T014) before component (T015) before `GameComponent` integration
- Store before component UI, component before `GameComponent` integration
- Core implementation before realtime pulse before responsive polish

### Parallel Opportunities

- Phase 1: T002 + T003 parallel (different files)
- Phase 2: T005 + T006 + T007 parallel (different files); T004 later but independent
- Phase 3: T011 + T012 + T013 parallel (contract test / component spec / integration test different files); T014 parallel with T011 tests start (different files)
- Phase 4: T018 + T019 parallel (store spec vs component spec); T021 needs T020 sequential same file `score-panel.component.ts`
- Phase 5: T023 + T024 parallel (component spec vs store spec different files); T025 needs T023 T024 contracts; T026 needs T025
- Phase 6: T027 + T028 parallel (component spec vs quickstart placeholder); T029/T030 sequential same file `score-panel.component.css/ts`
- Phase 7: T031 + T032 + T033 + T034 + T035 parallel (different files); T036 after all
- Different stories can start in parallel after Foundational if staffed (US2 needs only `Score` interface agreed, US3 needs only `Secured/Potential` signature)

### Parallel Example: User Story 1 (5 métricas autoritativas)

```bash
# Launch tests for US1 together:
Task T011: Contract test in tests/OroQuizClash.Api.Tests/Contracts/PlayerScoringContractTests.cs
Task T012: Score panel unit test in src/Player/QuizArena.Player/src/app/features/game/score-panel.component.spec.ts
Task T013: Integration test in src/Player/QuizArena.Player/tests/integration/player-scoring-metrics.spec.ts

# Launch types + component after tests:
Task T014: ScoringDisplayState types in src/Player/QuizArena.Player/src/app/features/game/scoring-display.model.ts
Task T015: ScorePanelComponent 5 métricas in src/Player/QuizArena.Player/src/app/features/game/score-panel.component.ts
```

### Parallel Example: User Story 2 (Realtime ScoreUpdated→hydrate)

```bash
# Launch tests:
Task T018: Store test in src/Player/QuizArena.Player/src/app/stores/player-game.store.spec.ts
Task T019: Component test in src/Player/QuizArena.Player/src/app/features/game/score-panel.component.spec.ts

# Launch implementation:
Task T020: PlayerGameStore realtime hydrate in src/Player/QuizArena.Player/src/app/stores/player-game.store.ts
Task T021: ScorePanelComponent pulse in src/Player/QuizArena.Player/src/app/features/game/score-panel.component.ts
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (verify SPA/monolith)
2. Complete Phase 2: Foundational (GamesApi `getMyState` 5 métricas, `PlayerGameStore` 10 elementos ledger, `GameRealtimeService` `ScoreUpdated→hydrate`, `PointTransaction` invariant)
3. Complete Phase 3: US1 (5 métricas `Current/Secured/Potential/Round/Total` sin cliente calc, `Secured checkpoint 3` vs null, `Potential "—"`, `roundPoints` `aria-live`)
4. **STOP and VALIDATE**: `GET /players/me` shows 5 métricas sin cliente calc 0% SC-001, `ScorePanel` 5 `role="status"` `aria-live polite` + `Potential "—"` passes, `axe` `group` passes, quickstart V1 SC-001
5. Deploy/demo MVP (5 métricas works end-to-end)

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 → Test independently → Demo MVP (5 métricas autoritativas)
3. Add US2 → Test independently → Demo (realtime <1s `ScoreUpdated→hydrate` `pulse` + `Reconnected` 0% cliente mutación)
4. Add US3 → Test independently → Demo (Secured checkpoint badge + Potential "—" + Round "en juego" `var(--color-warning)`)
5. Add US4 → Test independently → Demo (Responsive 375 1col / 768 5col + WCAG AA axe + `X-Correlation-Id`)
6. Polish → final validation V1-V4, SC-001..008

### Parallel Team Strategy

With 3 developers after Foundational:

1. Team completes Setup + Foundational together
2. Once Foundational done:
   - Developer A: US1 (5 métricas `ScoringDisplayState` + `ScorePanelComponent` skeleton)
   - Developer B: US2 (Realtime `ScoreUpdated→hydrate` `isScorePulse` + `pulse` `prefers-reduced-motion`) (US2 and US3 share `score-panel.component.css` but no same-line conflict)
   - Developer C: US3 (Secured checkpoint badge + Potential "—" + Round "en juego") + US4 (Responsive premium polish)
3. Polish by A/B/C after stories done

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability (US1..US4)
- Each user story independently completable and testable per spec Independent Test + quickstart V1-V4
- Verify tests fail before implementing (red → green)
- Commit after each task or logical group; push to branch `032-player-scoring`
- Avoid: vague tasks, same file conflicts, cross-story state leakage (e.g., `Total Points` calculated client instead of `score.totalPoints`)
- Do not edit `.aspire/modules` — wire via AppHost.cs only
- Tokens: `design-system/tokens` + `data-theme="player"` per SPEC-016 cinematic premium + `prefers-reduced-motion`
