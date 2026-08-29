# Tasks: Player Answering (031)

**Input**: Design documents from `/specs/031-player-answering/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md
**Branch**: `031-player-answering` | **Constitution**: v1.1.0 (I-VI, A-J)

**Organization**: Tasks grouped by user story — each story independently testable and deliverable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify existing Player SPA (027+029+030) + modular monolith and prepare answering scaffolding

- [ ] T001 Verify existing project structure per `specs/031-player-answering/plan.md` (`src/Player/QuizArena.Player` Angular 22 standalone, `src/OroQuizClash.Domain`, `Application`, `Infrastructure`, `Api`, `OroQuizClash.AppHost` in `OroQuizClash.slnx`) and `dotnet build OroQuizClash.slnx` passes
- [ ] T002 Verify Player SPA dependencies in `src/Player/QuizArena.Player/package.json` (`@angular/core 22`, `@ngrx/signals`, `angular-auth-oidc-client`, `@microsoft/signalr`, `rxjs`) and `design-system/tokens/design-tokens.css` is imported via `angular.json` styles with `data-theme="player"` per SPEC-016
- [ ] T003 Verify `BuildingBlocks` dependencies and `Directory.Packages.props` central versions for `net10.0` (`LangVersion latest`, `Nullable enable`) and `src/BuildingBlocks/` references per plan.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared answering infrastructure MUST complete before ANY user story — GamesApi submitAnswer/getMyState, Question 4/1 invariant, PlayerGameStore, GameRealtimeService, interceptors, shared UI states

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T004 Verify `GamesApi` client in `src/Player/QuizArena.Player/src/app/features/shared/games.api.ts` has `submitAnswer(gameId, dto {roundId, questionId, selectedOptionId, idempotencyKey})` (`POST /api/games/{id}/answers` `X-Idempotency-Key` + `X-Correlation-Id`) and `getMyState(gameId)` (`GET /players/me` with `question` 4 options + `answer` + `timer` + `status.canAnswer`) per `contracts/api-contracts.md` §1
- [ ] T005 [P] Verify `GameRealtimeService` in `src/Player/QuizArena.Player/src/app/core/realtime/game-realtime.service.ts` (`HubConnectionBuilder withUrl gameHubUrl?gameId accessTokenFactory`, `withAutomaticReconnect [0,2000,5000,10000,30000]`, events `QuestionAvailable/ScoreUpdated/RoundCompleted/Reconnected` → `hydrate`) per research.md
- [ ] T006 [P] Verify interceptors in `src/Player/QuizArena.Player/src/app/core/interceptors/` — `correlation-id.interceptor.ts` (`X-Correlation-Id` UUID per `POST /answers`), `auth.interceptor.ts` (`Authorization: Bearer` only `apiUrl` secureRoutes), `error.interceptor.ts` (RFC 7807 `ProblemDetails` mapping `AnswerWindowExpired 400`/`QuestionAlreadyAnswered 409`/`InvalidAnswer 400`, 401 silentRenew, 429 RetryAfter) per plan.md Constraints H/I
- [ ] T007 [P] Verify shared UI states in `src/Player/QuizArena.Player/src/app/shared/ui/` — `loading-skeleton.component.ts` (`aria-live="polite"` `aria-busy`), `empty-state.component.ts`, `error-state.component.ts` (CorrelationId/TraceId `Retry` 44px, `aria-live="assertive"`) per `data-model.md` UI States
- [ ] T008 Verify `SubmitAnswer` + `GetMyPlayerState` slices in `src/OroQuizClash.Application/Features/Games/SubmitAnswer.cs` and `GetMyPlayerState.cs` (idempotent `X-Idempotency-Key` `UNIQUE IdempotencyKey` + `UNIQUE (GameId,RoundId,PlayerId)` → `QuestionAlreadyAnswered` 409 idempotente 200, `AnswerWindowExpired` 400 `submittedAt<=expiresAt` server, `isCorrect` filtrado para `PLAYER` antes de `EVALUATED`, `RowVersion`) per `data-model.md`
- [ ] T009 Verify `PlayerGameStore`/`PlayerRoundsStore` intake in `src/Player/QuizArena.Player/src/app/stores/player-game.store.ts` and `player-rounds.store.ts` (`signalStore withState {game, gameSession, round, question, answer, score, securedPoints, timer, status, _now}`, `computed canAnswer/isTerminal/remainingSeconds`, `hydrate` via `GET /players/me`, `startTimerTick` + `serverNow` correction, `bindRealtime`) is intact from 029/030 to avoid regression before answering extension
- [ ] T010 Verify `Question` 4/1 invariant in `src/OroQuizClash.Domain/Questions/Question.cs` (`Question.Create` `IBusinessRule ExactlyFourOptions` + `ExactlyOneCorrect`, DB `CHECK exactly one correct`, publish requires ≥5 por `Category`) per Constitution B and `data-model.md` §1

**Checkpoint**: Foundation ready — `dotnet build` passes, `POST /answers` idempotente + 4/1 filtered, `GET /players/me` returns `question[4]` + `answer` + `timer` + `canAnswer`, realtime → hydrate, UI states ready

---

## Phase 3: User Story 1 — Presentar exactamente cuatro opciones en estado Idle/Hover (Priority: P1) 🎯 MVP

**Goal**: Selector muestra exactamente 4 opciones ordenadas por `displayOrder` con `optionId`+`text` en `Idle` (sin `isCorrect` leak) y `Hover`/`focus` premium (`border var(--color-primary)` `scale 1.01` `prefers-reduced-motion` none), valida `<4` o `>4` → `ErrorState` `CorrelationId`, `role="radiogroup"` `aria-posinset/aria-setsize`

**Independent Test**: Con `ROUND_IN_PROGRESS` `Question` 4 opciones A-D `text` distinto, abrir `/player/game/:id` → 4 `role="radio"` `aria-checked="false"` `aria-posinset 1..4` orden A-D `Idle`, hover → `Hover` `var(--shadow-hover)`, `GET /players/me` `answerOptions` sin `isCorrect` (0% leak); 3 opciones → `ErrorState` `CorrelationId` bloquea selección (spec US1, quickstart V1, SC-001/SC-002)

### Tests for User Story 1

- [ ] T011 [P] [US1] Contract test for `GET /players/me` 4 options without leak in `tests/OroQuizClash.Api.Tests/Contracts/PlayerAnsweringIdleContractTests.cs` (WebApplicationFactory JWT `PLAYER`, assert `question.answerOptions.length===4` ordered `displayOrder 0..3`, no `isCorrect` field for `PLAYER` when `answer.state!=EVALUATED`, `X-Correlation-Id` echo, `PlayerNotInGame` 403, `Question` 3 options → `ErrorState` 400)
- [ ] T012 [P] [US1] Question component unit test for Idle/Hover in `src/Player/QuizArena.Player/src/app/features/game/question.component.spec.ts` (TestBed `QuestionComponent` mock `question` 4 opts, verify 4 `role="radio"` `aria-checked="false"` `aria-posinset` 1..4 `aria-setsize 4`, `Idle` class, hover `Hover` triggers `border var(--color-primary)`, `prefers-reduced-motion` reduce none, text vacío placeholder "Opción sin texto")
- [ ] T013 [P] [US1] Integration test for 4 options rendering in `src/Player/QuizArena.Player/tests/integration/player-answering-idle.spec.ts` (mock `getMyState` 4 options, render `GameComponent`+`QuestionComponent` → assert 4 cards visible, `data-theme="player"`, no `isCorrect` in DOM, `ErrorState` when 3 options)

### Implementation for User Story 1

- [ ] T014 [P] [US1] Create `AnswerInteractionState` types in `src/Player/QuizArena.Player/src/app/features/game/answer-interaction.model.ts` (export `AnswerOptionState = 'Idle'|'Hover'|'Selected'|'Locked'|'Evaluating'|'Correct'|'Incorrect'|'Timeout'` + `AnswerPhase`, `AnswerInteractionState {selectedOptionId, lockedOptionId, phase, isEvaluating, canSelect, errorDetail, correlationId}`, helper `mapOptionState(optionId, interaction, answer, timer)` → `AnswerOptionState` per `data-model.md` §3, placeholder fallback for empty text)
- [ ] T015 [US1] Extend `QuestionComponent` to render 4 opciones Idle/Hover in `src/Player/QuizArena.Player/src/app/features/game/question.component.ts` (standalone `selector app-question`, inputs `question: QuestionView|null` + `interaction: Signal<AnswerInteractionState>` + `question`, outputs `select/output`, template `div options-grid role="radiogroup" aria-label="Opciones de respuesta" [attr.aria-busy]="isEvaluating"` `*ngFor answerOptions track optionId` `button role="radio"` `class idle/hover` `attr.aria-checked` `aria-posinset` `aria-setsize` `aria-disabled` when `Locked/Evaluating/Correct/Incorrect/Timeout`, `Idle` default, `Hover` via `:hover` + `(mouseenter)`, ordered `displayOrder`, text `|| 'Opción sin texto'`, per `contracts/ui-contracts.md` §1) (depends on T014)
- [ ] T016 [US1] Add Idle/Hover styles with tokens in `src/Player/QuizArena.Player/src/app/features/game/question.component.css` (create/extend CSS `options-grid {display:grid; grid-template-columns:1fr; gap:var(--space-3)} @media(min-width:768px){grid-template-columns:1fr 1fr}` 2x2, `.answer-option {display:flex align:center gap:var(--space-2) padding:var(--space-3) min-height:44px min-width:44px border-radius:var(--radius-md) border:1px solid var(--color-border) background:var(--color-surface)}` `.hover {border-color:var(--color-primary); box-shadow:var(--shadow-hover); transform:scale(1.01)}` `:focus-visible outline:2px solid var(--color-primary)` `@media prefers-reduced-motion reduce transition none` no literales, tokens only) (depends on T015)
- [ ] T017 [US1] Wire Question Idle validation in `src/Player/QuizArena.Player/src/app/stores/answer-interaction.store.ts` (or extend `PlayerGameStore`) (validate `answerOptions.length===4` else `patchState({errorDetail:"Pregunta inválida (se requieren 4 opciones)", canSelect:false})` with `CorrelationId`, ensure `hydrateAnswer` restores `question` from `GET /players/me` and filters `isCorrect` not exposed client) (depends on T014)

**Checkpoint**: US1 fully functional — `npm test` `question.component.spec` 4 `role="radio"` `Idle/Hover` + `aria-posinset` + placeholder passes, contract `GET /players/me` 4 without `isCorrect` leak 0% SC-001, `axe` `radiogroup` passes, `/player/game/:id` shows 4 opciones `Idle` 375 1 col / 768 2x2 no scroll (quickstart V1 SC-001/SC-002)

---

## Phase 4: User Story 2 — Seleccionar una única respuesta y bloquearla (Selected → Locked) (Priority: P1)

**Goal**: Selección única (`Selected` `aria-checked="true"` solo una, mover `Selected` entre opciones antes de lock, debounce 150ms), confirmar (`Confirmar` 44px) → `Locked` (`aria-disabled` `isLocked` inmutable, deshabilita otras, persistencia `hydrate` restores `Locked` tras recarga, local `Locked` no reversible, servidor `409 QuestionAlreadyAnswered` idempotente sin duplicar ledger)

**Independent Test**: Click B → `Selected` solo B, click C antes lock → `Selected` mueve B→C; sin selección `Confirmar` → validación local "Selecciona una opción" sin llamada; con `Selected` `Confirmar` → `Locked` (B `disabled` `aria-disabled`, otras `aria-disabled`), intentar seleccionar otra → ignorado; recarga `hydrate` → mismo `Locked`; reenvío distinta opción → `409` sin nuevo ledger (US2, quickstart V2, SC-003/SC-004)

### Tests for User Story 2

- [ ] T018 [P] [US2] Answer interaction store unit test for single selection + Locked in `src/Player/QuizArena.Player/src/app/stores/answer-interaction.store.spec.ts` (verify `selectOption(B)` → `selectedOptionId B phase selected canSelect true`, `selectOption(C)` moves `Selected` B→C único, `confirmLock()` with B → `lockedOptionId B phase locked isLocked true canSelect false`, second `selectOption(C)` after locked ignored, `confirmLock()` without selected → no lock no call; `hydrateAnswer` with `answer.selectedOptionId` restores `Locked`)
- [ ] T019 [P] [US2] Question component test for Selected→Locked interaction in `src/Player/QuizArena.Player/src/app/features/game/question.component.spec.ts` (click B → `aria-checked true` only B, click C before lock moves Selected, click Confirmar → `Locked` disabled others `aria-disabled`, double-click debounce 150ms first prevails, recarga mock `getMyState` restores `Locked`, Confirmar without selection shows `role="alert"` "Selecciona una opción")

### Implementation for User Story 2

- [ ] T020 [US2] Implement `AnswerInteractionStore` selection + lock in `src/Player/QuizArena.Player/src/app/stores/answer-interaction.store.ts` (create `signalStore withState<AnswerInteractionState> {selectedOptionId:null, lockedOptionId:null, phase:'idle', isEvaluating:false, canSelect:computed, errorDetail:null}`, `withComputed canSelect/isLocked/isSelected`, `withMethods selectOption: rxMethod<string>(pipe(debounceTime(150), tap(optionId => {if(isLocked||isEvaluating||!canAnswer) return; patchState({selectedOptionId: optionId, phase:'selected'})}))`, `confirmLock: rxMethod<void>(pipe(tap(()=>{if(!selected) {patchState({errorDetail:"Selecciona una opción"}); return;} patchState({lockedOptionId:selected, phase:'locked', isLocked:true})}))`, `hydrateAnswer(gameId)` via `GamesApi.getMyState → answer.selectedOptionId/state` restores `locked/phase`) per `research.md` D1) (depends on T014)
- [ ] T021 [US2] Enhance `QuestionComponent` for Selected→Locked in `src/Player/QuizArena.Player/src/app/features/game/question.component.ts` (add `onSelect(optionId)` → `store.selectOption(optionId)` with `isLocked/isEvaluating` guard, `onConfirm()` → `store.confirmLock()` + then `store.submitAnswer(gameId, roundId, questionId)` if locked, template `button class selected/locked [attr.aria-checked]="isSelected"` ` [attr.aria-disabled]="isLocked"` ` [disabled]="isLocked||isEvaluating||!canSelect"` ` (click) onSelect` `(keydown.space/enter) onSelect`, `Confirmar` button `min-height:44px` ` [disabled]="!selected||isLocked||isEvaluating"` `aria-label="Confirmar respuesta"`, validation `role="alert"` when `showValidation && !selected`) (depends on T020)
- [ ] T022 [US2] Add Selected/Locked styles in `src/Player/QuizArena.Player/src/app/features/game/question.component.css` (add `.answer-option.selected {background:var(--color-primary-subtle); border-color:var(--color-primary); box-shadow:var(--shadow-selected)}` `.answer-option.locked {opacity:0.9; cursor:not-allowed}` no multi-select highlight, tokens only, `@media prefers-reduced-motion reduce`) (depends on T021)

**Checkpoint**: US1+US2 work — single `Selected` unique 100% SC-003, `Locked` inmutable 0% local modify + server `409` idempotente SC-004, debounce 150ms coalesce, `hydrate` restores `Locked` after reload, quickstart V2 green

---

## Phase 5: User Story 3 — Evaluar y mostrar resultado autoritativo del backend (Evaluating → Correct/Incorrect/Timeout) (Priority: P1)

**Goal**: `Locked` → `POST /answers` `X-Idempotency-Key` UUID per `roundId` `sessionStorage` → `Evaluating` (`aria-busy spinner` `aria-live="polite"` deshabilita) → `Correct` (`var(--color-success)` check anim <300ms `assertive` "+X pts" + ledger) / `Incorrect` (`var(--color-error)` cross + correcta secondary resaltada) / `Timeout` (`var(--color-warning)` "Tiempo agotado" `AnswerWindowExpired`/`EXPIRED` `submittedAt<=expiresAt` server), idempotente reintento misma key sin duplicar ledger, `isCorrect` solo tras `EVALUATED`

**Independent Test**: `Locked` B dentro `TimeLimit` `POST /answers` B + `X-Idempotency-Key` → `Evaluating` spinner, `200 EVALUATED isCorrect true` → `Correct` verde + `score` ledger; `false` → `Incorrect` rojo + correcta B secondary verde; fuera ventana → `Timeout` warning sin `Correct`; doble `POST` misma key concurrente → `200` mismo `answerId` no duplicar `COUNT` ledger (US3, quickstart V3, SC-005/SC-006/SC-007)

### Tests for User Story 3

- [ ] T023 [P] [US3] Contract test for `POST /answers` evaluating authoritative in `tests/OroQuizClash.Api.Tests/Contracts/PlayerAnsweringEvaluatingContractTests.cs` (first `POST /answers` 200 `EVALUATED isCorrect` filtered for `PLAYER` before EVALUATED 0% leak, second same `X-Idempotency-Key` 200 same `answerId` no duplicate `PointTransaction` `COUNT`, `AnswerWindowExpired 400` outside `expiresAt` → `Timeout`, `QuestionAlreadyAnswered 409` after `Locked` distinct option without same key)
- [ ] T024 [P] [US3] Answer interaction store test for Evaluating→Correct/Incorrect/Timeout in `src/Player/QuizArena.Player/src/app/stores/answer-interaction.store.spec.ts` (mock `GamesApi.submitAnswer` 200 `EVALUATED isCorrect true` → `phase correct isEvaluating false`, false → `phase incorrect` + correctOption resalta, 400 `AnswerWindowExpired` → `phase timeout`, 500 → `errorDetail + correlationId` `phase evaluating` stays then `ErrorState` Retry reuses same `X-Idempotency-Key`, `submittedAt<=expiresAt` server truth)
- [ ] T025 [P] [US3] Question component evaluating test in `src/Player/QuizArena.Player/src/app/features/game/question.component.spec.ts` (`Locked` → `Evaluating` shows spinner `aria-busy true` `aria-live="polite"` "Evaluando…", button Confirmar disabled others `aria-disabled`, `Correct` green check `aria-live="assertive"` "+X pts", `Incorrect` red cross + secondary `Correct` on isCorrect option, `Timeout` warning "Tiempo agotado" `assertive`)

### Implementation for User Story 3

- [ ] T026 [US3] Implement `AnswerInteractionStore.submitAnswer` authoritative in `src/Player/QuizArena.Player/src/app/stores/answer-interaction.store.ts` (extend `withMethods submitAnswer: rxMethod<void>(pipe(switchMap(()=>{const key=sessionStorage.getItem(`idemp-${roundId}`)??crypto.randomUUID(); sessionStorage.setItem(`idemp-${roundId}`,key); patchState({isEvaluating:true, phase:'evaluating'}); return _api.submitAnswer(gameId,{roundId,questionId,selectedOptionId: lockedOptionId, idempotencyKey:key}).pipe(tapResponse({next:(answer:any)=>{const isCorrect=answer.isCorrect; if(answer.state==='EVALUATED') patchState({phase: isCorrect?'correct': isCorrect===false?'incorrect':'timeout', isEvaluating:false}); else if(answer.state==='EXPIRED') patchState({phase:'timeout', isEvaluating:false}); patchState({canSelect:false});}, error:(err:any)=>{if(err.code==='AnswerWindowExpired'){patchState({phase:'timeout', isEvaluating:false});} else if(err.status===409){patchState({phase:'locked'});} else {patchState({errorDetail: err.detail, correlationId: err.correlationId, isEvaluating:false});}}))}))`, `X-Correlation-Id` via interceptor, idempotente same key) (depends on T020)
- [ ] T027 [US3] Enhance `QuestionComponent` for Evaluating→Correct/Incorrect/Timeout in `src/Player/QuizArena.Player/src/app/features/game/question.component.ts` (add `@if phase==='evaluating' spinner aria-busy` `@if phase==='correct' result correct aria-live="assertive"` `@if incorrect` + secondary `Correct` highlight via `correctOptionId` from `GET /players/me` post-EVALUATED `isCorrect` already exposed, `@if timeout` warning, `@if errorDetail app-error-state [correlationId] (retry)` reuses same `X-Idempotency-Key`, disable Confirmar during `isEvaluating`, per `contracts/ui-contracts.md` §1) (depends on T026)
- [ ] T028 [US3] Add Evaluating/Correct/Incorrect/Timeout styles in `src/Player/QuizArena.Player/src/app/features/game/question.component.css` (add `.answer-option.evaluating {background:var(--color-primary-subtle); animation: pulse 600ms infinite}` `.answer-option.correct {background:var(--color-success); color:var(--color-success-contrast); border-color:var(--color-success)}` `.answer-option.incorrect {background:var(--color-error); color:var(--color-error-contrast)}` `.answer-option.timeout {background:var(--color-warning)}` `.spinner {border-top-color:var(--color-primary); animation: spin 600ms linear infinite}` `@keyframes pulse/spin` + `@media prefers-reduced-motion reduce animation none` tokens only) (depends on T027)
- [ ] T029 [US3] Verify `GetMyPlayerState` post-EVALUATED correct exposure in `src/OroQuizClash.Application/Features/Games/GetMyPlayerState.cs` (ensure `isCorrect` filtered null for `PLAYER` when `answer.state != EVALUATED`, exposed only when `EVALUATED` for secondary Correct highlight, per `contracts/api-contracts.md` §2)

**Checkpoint**: US1+US2+US3 work — 100% veredicto backend `Correct/Incorrect/Timeout` SC-005, `Evaluating` until server 100%, 409 idempotente SC-006, <1s `Correct` SC-007 95% + 100% `Timeout` fuera ventana, quickstart V3 green

---

## Phase 6: User Story 4 — Accesibilidad, responsive y premium del selector de respuestas (Priority: P2)

**Goal**: Selector `Cinematic` premium `data-theme="player"` tokens sin literales, responsive 375 1 col / 768 2x2 grid `gap var(--space-3)` targets ≥44px scrolleable interna, WCAG 2.2 AA `role="radiogroup"` `aria-checked/posinset/setsize/disabled/busy` `aria-live polite/assertive` foco `outline:2px` `axe 0` `prefers-reduced-motion` reduce

**Independent Test**: Resize 375px → 1 col no scroll, 768/1280/1536 → 2x2 `gap var(--space-3)` targets ≥44px 100%; inspect CSS `data-theme="player"` 0 literales `var(--space-*) var(--color-*)`; `axe` 0 violations `radiogroup`/`aria-checked`/`posinset`/`aria-disabled`/`aria-live` foco visible; `Tab/Shift+Tab` + `Space/Enter` selecciona 100% (US4, quickstart V5, SC-008/SC-009/SC-010)

### Tests for User Story 4

- [ ] T030 [P] [US4] Responsive and a11y test for answering in `src/Player/QuizArena.Player/src/app/features/game/question.component.spec.ts` (set viewport 375/768/1536 → verify 375px `grid-template-columns 1fr` no horizontal scroll, 768px `1fr 1fr` 2x2, `data-theme="player"` exists, scan CSS for `var(--)` presence 0 literals, `axe` 0 violations `role="radiogroup"` `aria-checked` `posinset/setsize` `aria-disabled` `aria-live` `aria-busy`, keyboard `Tab` + `Space` selects, targets ≥44px via `getBoundingClientRect`, `prefers-reduced-motion` reduce)
- [ ] T031 [P] [US4] Quickstart V5 validation placeholder in `specs/031-player-answering/quickstart.md` (verify `X-Correlation-Id` header per `POST /answers` + JWT required 401 `isCorrect` leak check, documented for manual run, will be executed in T036)

### Implementation for User Story 4

- [ ] T032 [US4] Polish responsive answering layout in `src/Player/QuizArena.Player/src/app/features/game/question.component.ts` and `question.component.css` (ensure `options-grid` 1fr 375 + 1fr 1fr ≥768 `gap var(--space-3)` `min-height:44px min-width:44px` per option, `question-text` `font-size var(--font-size-lg)` `gap` tokens, verify `GameComponent` grid still `280px 1fr` with ladder sidebar (030) + center question answering, no scroll horizontal 375-1536, `OroQuizClash.AppHost` still mounts `design-system/tokens` via `angular.json`) (depends on T015)
- [ ] T033 [US4] Harden answering a11y and tokens in `src/Player/QuizArena.Player/src/app/features/game/question.component.ts` (verify `role="radiogroup" aria-label="Opciones de respuesta"` `aria-busy` Evaluating, each `role="radio"` `aria-checked` single `Selected/Locked/Correct` `aria-posinset 1..4` `aria-setsize 4` `aria-disabled` terminales, `aria-live="polite"` Evaluating + `assertive` Correct/Incorrect/Timeout, foco `outline:2px solid var(--color-primary)`, question `aria-live="polite"` text, `InvalidAnswer` 400 `detail` + `CorrelationId` in `ErrorState`, verify no inline style literals) (depends on T027)

**Checkpoint**: All 4 stories functional — 8 estados 100% SC-002, responsive 375-1536 100% SC-008, WCAG AA 100% SC-009 `axe 0` + `X-Correlation-Id` SC-010, quickstart V5 green

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Hardening, docs, observability, final validation

- [ ] T034 [P] Add ProblemDetails mapping test for answering errors in `tests/OroQuizClash.Api.Tests/Contracts/PlayerAnsweringErrorsMappingTests.cs` (assert `InvalidAnswer 400` text empty, `AnswerWindowExpired 400` `submittedAt>expiresAt` with `CorrelationId`, `QuestionAlreadyAnswered 409` distinct option without same key no ledger duplicate, `GameNotFound 404` `PlayerIdentityMismatch 403` map via `GlobalExceptionHandler` → `Result.ToHttpResult()` RFC 7807 with `CorrelationId/TraceId`, `X-Correlation-Id` echo, `X-Idempotency-Key` idempotente)
- [ ] T035 [P] Verify `X-Correlation-Id` + JWT gating test in `src/Player/QuizArena.Player/tests/integration/player-answering-correlation.spec.ts` (mock `GamesApi.submitAnswer` → assert header `X-Correlation-Id` UUID + `Authorization Bearer` per `POST /answers`, no JWT → 401 redirect OIDC, `ErrorState` displays `CorrelationId/TraceId` from `ProblemDetails`, retry reuses same `X-Idempotency-Key` per `roundId` sessionStorage, `must_change_password` gating redirect)
- [ ] T036 [P] Verify empty/text fallback edge cases in `src/Player/QuizArena.Player/src/app/stores/answer-interaction.store.spec.ts` (test `answerOptions` 3/5 → `ErrorState` "Pregunta inválida" `canSelect false`, text vacío → placeholder "Opción sin texto" no break grid, `Timer EXPIRED` while `Selected` → forced `Timeout` local, `isCorrect` leak check `answerOptions` no `isCorrect` pre-EVALUATED)
- [ ] T037 [P] Update design-system and Player README in `src/Player/QuizArena.Player/README.md` and `specs/031-player-answering/spec.md` Status (add `Player Answering` section: 4 opciones 8 estados `Idle→Timeout` single `Selected→Locked` inmutable `AnswerInteractionStore` `X-Idempotency-Key` `Evaluating→Correct/Incorrect/Timeout` server truth `data-theme="player"` WCAG responsive, 2x2 grid `var(--space-3)` 44px)
- [ ] T038 [P] Run quickstart validation in `specs/031-player-answering/quickstart.md` (execute V1-V5: 4 opciones Idle/Hover sin leak, single Selected→Locked inmutable + 409, Evaluating→Correct/Incorrect/Timeout + idempotencia misma key, responsive 375-1536 axe, X-Correlation-Id + JWT, fix gaps if any)
- [ ] T039 Add architecture test for answering isolation in `tests/OroQuizClash.Architecture.Tests/PlayerAnsweringIsolationTests.cs` (verify `QuestionComponent`/`AnswerInteractionStore` not in `OroQuizClash.Domain` (Domain ↛ Angular), `SubmitAnswer` uses `sub` not body, no client `isCorrect` trust (Domain computes `Correct/Incorrect`), `Answer` `CHECK exactly one correct` + `UNIQUE IdempotencyKey` not leaked, BuildingBlocks `IRepository` not leaked)
- [ ] T040 Security hardening verify in `src/Player/QuizArena.Player/src/app/core/interceptors/error.interceptor.ts` (hide sensitive details, propagate `traceId`, handle 429 `GamePlayLimiter` `Retry-After` already from 027+030, ensure `AnswerWindowExpired` 400 `isCorrect` not leaked, `PlayerIdentityMismatch` audit logged, verify `submitAnswer` `Bearer` only `apiUrl`)
- [ ] T041 Final `dotnet build OroQuizClash.slnx && dotnet test` green and `npm run lint` with `@ngrx/eslint-plugin` clean and `npm test -- --watch=false` `question.component` + `answer-interaction.store` pass, update `specs/031-player-answering/spec.md` Status to `Ready for Review`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately (verifies Angular 22 SPA + monolith BuildingBlocks)
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (GamesApi `submitAnswer`/`getMyState` with `X-Correlation-Id`, `SubmitAnswer`/`GetMyPlayerState` idempotent + 4/1 filtered + `AnswerWindowExpired`, interceptors, `PlayerGameStore`/`PlayerRoundsStore` intact, `Question` 4/1 invariant)
- **User Stories (Phase 3+)**: All depend on Foundational
  - **US1 (P1) Idle/Hover 4 opciones**: No other story dependency — MVP (4 opciones sin leak)
  - **US2 (P1) Selected→Locked single inmutable**: Depends on US1 `question.component` 4 opciones + `AnswerInteractionState` types (needs T014/T015) but independently testable with mocked `GET /players/me`
  - **US3 (P1) Evaluating→Correct/Incorrect/Timeout**: Depends on US2 `selected/locked` + `submitAnswer` `X-Idempotency-Key` (needs T020/T021) but testable with mocked `submitAnswer` 200/400/409
  - **US4 (P2) Responsive/a11y premium**: Depends on US1/US2/US3 `question.component.ts/.css` layout (needs T015/T021/T027) — polish parallel with US3 if staffed
- **Polish (Final)**: Depends on all desired stories (US1+US2+US3 for MVP, US4 for completeness)

### User Story Dependencies

- **US1 (P1)**: After Foundational — no deps
- **US2 (P1)**: After Foundational + US1 types/component (integrates with US1's 4 opciones but testable with mocked `getMyState`)
- **US3 (P1)**: After Foundational + US2 selection/lock (depends on T020 `AnswerInteractionStore`)
- **US4 (P2)**: After Foundational — depends on US1/US2/US3 layout but can start after US1 Idle

### Within Each User Story

- Tests (if included) written before implementation (T011 before T014, T018 before T020, T023 before T026, T030 before T032)
- Types/helper (`answer-interaction.model.ts` T014) before store (T020) before component (T015/T021/T027)
- Store before component UI, component before `GameComponent` integration
- Core implementation before `Evaluating` terminal mapping before responsive polish

### Parallel Opportunities

- Phase 1: T002 + T003 parallel (different files)
- Phase 2: T005 + T006 + T007 parallel (different files); T004 later but independent
- Phase 3: T011 + T012 + T013 parallel (contract test / component spec / integration test different files); T014 parallel with T011 tests start (different files)
- Phase 4: T018 + T019 parallel (store spec vs component spec); T021 needs T020 sequential same file `question.component.ts`
- Phase 5: T023 + T024 + T025 parallel (contract / store spec / component spec different files); T026 needs T023 T024 contracts; T028 needs T027
- Phase 6: T030 + T031 parallel (component spec vs quickstart placeholder); T032/T033 sequential same file `question.component.css/ts`
- Phase 7: T034 + T035 + T036 + T037 + T038 parallel (different files); T039 after all
- Different stories can start in parallel after Foundational if staffed (US2 needs only `selectedOptionId` interface agreed, US3 needs only `submitAnswer` signature)

### Parallel Example: User Story 1 (4 opciones Idle/Hover)

```bash
# Launch tests for US1 together:
Task T011: Contract test in tests/OroQuizClash.Api.Tests/Contracts/PlayerAnsweringIdleContractTests.cs
Task T012: Question component unit test in src/Player/QuizArena.Player/src/app/features/game/question.component.spec.ts
Task T013: Integration test in src/Player/QuizArena.Player/tests/integration/player-answering-idle.spec.ts

# Launch types + component after tests:
Task T014: AnswerInteractionState types in src/Player/QuizArena.Player/src/app/features/game/answer-interaction.model.ts
Task T015: QuestionComponent 4 opciones Idle/Hover in src/Player/QuizArena.Player/src/app/features/game/question.component.ts
```

### Parallel Example: User Story 3 (Evaluating → Correct/Incorrect/Timeout)

```bash
# Launch tests:
Task T023: Contract test in tests/OroQuizClash.Api.Tests/Contracts/PlayerAnsweringEvaluatingContractTests.cs
Task T024: Store test in src/Player/QuizArena.Player/src/app/stores/answer-interaction.store.spec.ts
Task T025: Component test in src/Player/QuizArena.Player/src/app/features/game/question.component.spec.ts

# Launch implementation:
Task T026: AnswerInteractionStore.submitAnswer in src/Player/QuizArena.Player/src/app/stores/answer-interaction.store.ts
Task T027: QuestionComponent Evaluating→Correct/Incorrect/Timeout in src/Player/QuizArena.Player/src/app/features/game/question.component.ts
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (verify SPA/monolith)
2. Complete Phase 2: Foundational (GamesApi `submitAnswer`/`getMyState` with `X-Correlation-Id`, `SubmitAnswer` idempotent 4/1 filtered + `AnswerWindowExpired`, `Question` 4/1 invariant, `PlayerGameStore` intact)
3. Complete Phase 3: US1 (4 opciones `Idle/Hover` sin `isCorrect` leak, `ErrorState` 3 opciones, `radiogroup` a11y)
4. **STOP and VALIDATE**: `GET /players/me` shows 4 ordered `Idle` without `isCorrect` leak 0% SC-001, Hover `var(--shadow-hover)` premium, `ErrorState` 3 opciones, `axe` `radiogroup` passes, quickstart V1 SC-001/SC-002
5. Deploy/demo MVP (4 opciones works end-to-end)

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 → Test independently → Demo MVP (4 opciones Idle/Hover)
3. Add US2 → Test independently → Demo (single `Selected→Locked` inmutable + 409 no ledger)
4. Add US3 → Test independently → Demo (Evaluating→Correct/Incorrect/Timeout <1s + idempotencia misma key)
5. Add US4 → Test independently → Demo (Responsive 375 1 col / 768 2x2 + WCAG AA axe + `X-Correlation-Id`)
6. Polish → final validation V1-V5, SC-001..010

### Parallel Team Strategy

With 3 developers after Foundational:

1. Team completes Setup + Foundational together
2. Once Foundational done:
   - Developer A: US1 (4 opciones `Idle/Hover` `answer-interaction.model` + `QuestionComponent` skeleton)
   - Developer B: US2 (Selected→Locked single inmutable `AnswerInteractionStore` `selectOption`/`confirmLock` + debounce)
   - Developer C: US3 (Evaluating→Correct/Incorrect/Timeout `submitAnswer` `X-Idempotency-Key` + `ErrorState` Retry) + US4 (Responsive premium polish) (US3 and US4 share `question.component.css` but no same-line conflict)
3. Polish by A/B/C after stories done

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability (US1..US4)
- Each user story independently completable and testable per spec Independent Test + quickstart V1-V4
- Verify tests fail before implementing (red → green)
- Commit after each task or logical group; push to branch `031-player-answering`
- Avoid: vague tasks, same file conflicts, cross-story state leakage (e.g., `selectedOptionId` leaking across rounds)
- Do not edit `.aspire/modules` — wire via AppHost.cs only
- Tokens: `design-system/tokens` + `data-theme="player"` per SPEC-016 cinematic premium + `prefers-reduced-motion`
