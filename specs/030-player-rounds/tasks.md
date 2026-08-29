# Tasks: Player Rounds (030)

**Input**: Design documents from `/specs/030-player-rounds/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md
**Branch**: `030-player-rounds` | **Constitution**: v1.1.0 (I-VI, A-J)

**Organization**: Tasks grouped by user story — each story independently testable and deliverable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify existing Player SPA (027+028+029) + modular monolith and prepare ladder scaffolding

- [x] T001 Verify existing project structure per `specs/030-player-rounds/plan.md` (`src/Player/QuizArena.Player` Angular 22 standalone, `src/OroQuizClash.Domain`, `Application`, `Infrastructure`, `Api`, `OroQuizClash.AppHost` in `OroQuizClash.slnx`) and `dotnet build OroQuizClash.slnx` passes
- [x] T002 Verify Player SPA dependencies in `src/Player/QuizArena.Player/package.json` (`@angular/core 22`, `@ngrx/signals`, `angular-auth-oidc-client`, `@microsoft/signalr`, `rxjs`) and `design-system/tokens/design-tokens.css` is imported via `angular.json` styles with `data-theme="player"` per SPEC-016
- [x] T003 Verify `BuildingBlocks` dependencies and `Directory.Packages.props` central versions for `net10.0` (`LangVersion latest`, `Nullable enable`) and `src/BuildingBlocks/` references per plan.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared ladder infrastructure MUST complete before ANY user story — GamesApi, PlayerGameStore, GameRealtimeService, interceptors, shared UI states, and `GetMyPlayerState` projection reuse verified

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Verify `GamesApi` client in `src/Player/QuizArena.Player/src/app/features/shared/games.api.ts` has `getMyState(gameId)` (`GET /api/games/{id}/players/me` with `game.maxRounds`, `gameSession.currentRoundNumber`, `rounds[] {roundNumber, level}`, `rewardRules`, `securedPoints`) and `X-Correlation-Id` per `contracts/api-contracts.md` §1 (verify `getGames` from 028 still present)
- [x] T005 [P] Verify `GameRealtimeService` in `src/Player/QuizArena.Player/src/app/core/realtime/game-realtime.service.ts` (`HubConnectionBuilder withUrl gameHubUrl?gameId accessTokenFactory`, `withAutomaticReconnect [0,2000,5000,10000,30000]`, events `RoundCompleted/QuestionAvailable/ScoreUpdated/GameFinished/Reconnected` → `hydrate`) per research.md D3
- [x] T006 [P] Verify interceptors in `src/Player/QuizArena.Player/src/app/core/interceptors/` — `correlation-id.interceptor.ts` (`X-Correlation-Id` UUID per hydrate), `auth.interceptor.ts` (`Authorization: Bearer` only `apiUrl` secureRoutes), `error.interceptor.ts` (RFC 7807 `ProblemDetails` mapping, 401 silentRenew, 429 RetryAfter) per plan.md Constraints H/I
- [x] T007 [P] Verify shared UI states in `src/Player/QuizArena.Player/src/app/shared/ui/` — `loading-skeleton.component.ts` (`aria-live="polite"` `aria-busy`), `empty-state.component.ts`, `error-state.component.ts` (CorrelationId/TraceId Retry 44px, `aria-live="assertive"`) per `data-model.md` UI States
- [x] T008 Verify `GetMyPlayerState` slice in `src/OroQuizClash.Application/Features/Games/GetMyPlayerState.cs` returns `maxRounds`, `currentRoundNumber`, `rounds[].level`, `rewardRules`, `securedPoints {securedPoints, checkpointRoundNumber}` (if missing field add DTO projection without changing `Game` aggregate) and `GetGame.cs` already per plan.md
- [x] T009 Verify `PlayerGameStore` 10-element state in `src/Player/QuizArena.Player/src/app/stores/player-game.store.ts` (`signalStore withState {game, gameSession, round, question, answer, score, securedPoints, timer, status}`) is intact from 029 to avoid regression before ladder extension

**Checkpoint**: Foundation ready — `dotnet build` passes, `GET /players/me` returns N + current + rewards + secured, realtime → hydrate, UI states ready for ladder

---

## Phase 3: User Story 1 — Visualizar escalera de rondas Round 1..N con progresión de dificultad (Priority: P1) 🎯 MVP

**Goal**: Ladder vertical `role="list"` muestra Round 1..N (`N=maxRounds` ≥5 dinámico sin hardcodear 10) cada fila con `RoundNumber` + `Level/Difficulty` (Basic..Expert o CategorySpecific via `IDifficultyProgressionStrategy`), estados `completed` (<current check), `current` (`aria-current="step"` premium glow), `upcoming` (muted), coincidente con `GET /players/me` `currentRoundNumber` + `rounds[]` autoritativo

**Independent Test**: Con `MaxRounds=10`, `currentRoundNumber=4`, `Linear` (Level 1..5), abrir ladder → 10 filas "Round 1".. "Round 10" con Current=4 premium, Previous 1-3 completed check, 5-10 upcoming muted, cada fila `Level` correcto; cambiar a `MaxRounds=15 CategorySpecific` → 15 filas con "Geografía — Hard" (spec US1, quickstart V1, SC-001/SC-002/SC-010)

### Tests for User Story 1

- [x] T010 [P] [US1] Contract test for `GET /api/games/{id}/players/me` ladder fields in `tests/OroQuizClash.Api.Tests/Contracts/PlayerRoundsLadderContractTests.cs` (WebApplicationFactory JWT `PLAYER`, assert `game.maxRounds 5..15`, `gameSession.currentRoundNumber 1..N`, `rounds[]` length ≤ N with `level` Basic..Expert, `X-Correlation-Id` header echo, `PlayerNotInGame` 403)
- [x] T011 [P] [US1] Ladder store unit test for `buildLadder` and states in `src/Player/QuizArena.Player/src/app/stores/player-rounds.store.spec.ts` (TestBed `PlayerRoundsStore` mock `GamesApi.getMyState` with `maxRounds 10 current 4 rounds levels Linear`, verify `ladder.length===10` without gaps 1..N, `ladder[3].state==='current' aria-current`, `ladder[0..2].state==='completed'`, `ladder[4..9].state==='upcoming'`, `maxRounds 15` builds 15 rows)
- [x] T012 [P] [US1] Ladder component integration test for rendering in `src/Player/QuizArena.Player/tests/integration/player-rounds-ladder.spec.ts` (mock `getMyState` N=10 current 4, render `GameComponent`+`PlayerRoundsComponent` → assert `role="list"` exists, 10 `role="listitem"`, `aria-current="step"` only row 4 "Ronda 4 de 10, nivel Intermediate", completed rows have ✓, upcoming muted)

### Implementation for User Story 1

- [x] T013 [P] [US1] Create `LadderRow`/`LadderState` types and `buildLadder` helper in `src/Player/QuizArena.Player/src/app/features/game/ladder.model.ts` (export `LadderRow {roundNumber, level, difficulty, state:completed|current|upcoming, isSecured, isFinal, currentReward, nextRewardFlag, securedFlag, isCurrentReward, ariaLabel}` and `LadderState`, pure function `buildLadder(maxRounds: number, rounds: Round[], rewardRules: RewardRule[], secured: SecuredPoints|null, current: number|null, pointsPerRound?: number): LadderRow[]` mapping 1..N, state logic `<current completed` `===current current` `>current upcoming`, `isFinal n===maxRounds`, per `data-model.md` §6)
- [x] T014 [US1] Implement `PlayerRoundsStore` with ladder state in `src/Player/QuizArena.Player/src/app/stores/player-rounds.store.ts` (create `signalStore withState<LadderState> {gameId:null, maxRounds:0, currentRoundNumber:null, ladder:[], secured:null, rewardRules:[], status:'loading', correlationId:undefined, errorDetail:undefined, _animatingRound:null, previousRoundNumber:null}`, `withComputed {currentLevel:()=>ladder.find(r=>r.roundNumber===current), previousLevels:()=>filter <current, finalRow:()=>ladder[N-1]}`, `withMethods {hydrateLadder: rxMethod<string>(pipe(switchMap(id=>gamesApi.getMyState(id).pipe(tapResponse(...patchState buildLadder...), catchError(...status:'error'...))))) , bindRealtimeLadder:()=>gameRealtimeService.events$.subscribe(e=>hydrateLadder)}`, store scoped `providers: [PlayerRoundsStore]` per `research.md` D1) (depends on T013)
- [x] T015 [US1] Implement `PlayerRoundsComponent` ladder skeleton in `src/Player/QuizArena.Player/src/app/features/game/player-rounds.component.ts` (standalone `selector app-player-rounds`, `input gameId`, inject `PlayerRoundsStore`, template `section role="region" aria-label="Progresión de rondas"` with `@if status==='loading' skeleton aria-busy` `@if status==='empty' role=status "Aún no inicia — N rondas por jugar"` `@if status==='error' app-error-state [correlationId]` `@else ol role=list > li role=listitem *ngFor ladder track roundNumber [class.completed/current/upcoming] [attr.aria-current]="current?step:null" [attr.aria-label]="ariaLabel"`, roundLabel `Round {{n}}` + `level` + `difficulty-indicator`, use `LadderRow` from T013 per `contracts/ui-contracts.md` §1) (depends on T014)
- [x] T016 [US1] Add ladder styles with tokens in `src/Player/QuizArena.Player/src/app/features/game/player-rounds.component.css` (create CSS `display:flex flex-direction:column gap:var(--space-2) max-height:60vh overflow-y:auto`, `.ladder-row {display:flex align:center gap:var(--space-2) padding:var(--space-3) border-radius:var(--radius-md) min-height:44px}` `.current {border-color:var(--color-primary) box-shadow:var(--shadow-premium) transform:scale(1.02) transition:300ms}` `.completed{opacity:0.7}` no literales hardcodeados, `@media prefers-reduced-motion` reduce transition none, per `contracts/ui-contracts.md` §1) (depends on T015)
- [x] T017 [US1] Wire ladder into `GameComponent` in `src/Player/QuizArena.Player/src/app/features/game/game.component.ts` (extend 029 shell: import `PlayerRoundsComponent` and `PlayerRoundsStore` to `providers: [PlayerGameStore, PlayerRoundsStore]`, template `div.game-body grid 280px 1fr ≥1024px else flex column 375px` with `<app-player-rounds [gameId]="gameId()" class="game-sidebar" />` alongside `app-question/timer/score-panel`, `OnInit` → `playerRoundsStore.hydrateLadder(gameId)` + `playerRoundsStore.bindRealtimeLadder(gameId)` + reuse `PlayerGameStore` hydrate, verify both hydrates share same `GET /players/me` debounce 300ms if needed) (depends on T015)

**Checkpoint**: US1 fully functional — `npm test` `player-rounds.store.spec` N=10 ladder exact + `aria-current` + completed/upcoming + `MaxRounds 15` passes, component 10 rows `role=list` passes, `/player/game/:id` shows ladder vertical 375-1536 no horizontal scroll (quickstart V1, SC-001/SC-002/SC-010)

---

## Phase 4: User Story 2 — Visualizar recompensas: Current, Next, Secured y Final (Priority: P1)

**Goal**: Sobre la misma ladder: Current Reward badge en `currentRoundNumber` (RewardRules[threshold] o `pointsPerRound*round` fallback, "—" si no config), Next Reward badge en `current+1` muted upcoming con flecha, Secured Reward escudo + filas ≤ checkpoint overlay `success-subtle` según `SecuredPoints {securedPoints, checkpointRoundNumber}` ledger `KEEP_SECURED_SCORE` (0 si `LOSE_ALL`), Final Reward corona gradiente siempre en fila N (Round N) incluso antes de llegar

**Independent Test**: `RewardRules 5→500 10→5000` `Secured 500 checkpoint 5` `current 6` `Score 700` → fila 6 badge `Current Reward: 600 pts`, fila 7 `Next: 800 pts`, fila 5 escudo `Asegurado 500 pts` + filas 1-5 `class="secured"`, fila 10 corona `Final Reward: 5000 pts` 100% ledger-reconstructable; `RewardRules=[]` → "—" `aria-label="Sin recompensa"` sin layout break (US2, quickstart V2, SC-003/SC-004)

### Tests for User Story 2

- [x] T018 [P] [US2] Ladder rewards store unit test in `src/Player/QuizArena.Player/src/app/stores/player-rounds.store.spec.ts` (extend spec: mock `rewardRules [{roundThreshold:5 points:500},{10 points:5000}] secured {securedPoints:500 checkpoint:5} current 6`, verify `ladder[5].currentReward` contains 600, `ladder[6].nextRewardFlag===true` muted, `ladder[4].securedFlag===true` + `isSecured` for 1-5 true, `ladder[9].isFinal===true` final badge; `LOSE_ALL secured 0 checkpoint null` → `isSecured` all false + summary "Sin monto asegurado"; empty rules → "—")
- [x] T019 [P] [US2] Ladder rewards component test in `src/Player/QuizArena.Player/src/app/features/game/player-rounds.component.spec.ts` (render `PlayerRoundsComponent` with `secured` mock, verify escudo `aria-label="Asegurado"` on checkpoint row, `class="secured"` for ≤ checkpoint, corona `aria-label="Recompensa final"` on final row N, badges `Current Reward`/`Next` visible, placeholder "—" when no rules, `axe` 0 violations)

### Implementation for User Story 2

- [x] T020 [US2] Enhance `PlayerRoundsStore` with reward derivation in `src/Player/QuizArena.Player/src/app/stores/player-rounds.store.ts` (extend `buildLadder` to compute `currentReward` from `rewardRules.find(r=>threshold===roundNumber)?.pointsRequired ?? pointsPerRound*roundNumber ?? null → "—" if null` and `nextRewardFlag`/`securedFlag`/`isSecured`/`isFinal` per `data-model.md` Validation + `withComputed {currentReward:()=>ladder.find(...), nextReward:()=>rewardRules.find(threshold===current+1), securedReward:()=>secured, finalReward:()=>rewardRules.find(threshold===maxRounds), announcement:()=>...}`, verify placeholder "—" and `LOSE_ALL` → `securedPoints 0` logic) (depends on T014)
- [x] T021 [US2] Enhance `PlayerRoundsComponent` with reward badges in `src/Player/QuizArena.Player/src/app/features/game/player-rounds.component.ts` (template add `@if row.isCurrentReward badge class="current-reward" aria-label="Recompensa actual" {{currentReward??'—'}}` `@if row.nextRewardFlag badge class="next-reward" muted {{nextReward}} · próximo` `@if row.securedFlag badge secured-reward <svg shield aria-hidden> Asegurado {{secured.securedPoints}} pts` `@if row.isFinal badge final-reward <svg crown> Final {{finalReward}}` + `secured-summary` div `aria-live="polite"` showing "Asegurado: X pts en ronda Y" or "Sin monto asegurado" muted, per `contracts/ui-contracts.md` §1) (depends on T020)
- [x] T022 [US2] Polish reward styles in `src/Player/QuizArena.Player/src/app/features/game/player-rounds.component.css` (add `.secured {background:var(--color-success-subtle)}` `.final {background:var(--player-gradient-final) color:var(--color-final-contrast)}` `.badge {font-size:var(--font-size-sm) padding:var(--space-1) var(--space-2)}` `.next-reward {opacity:0.6}` tokens only, no literals) (depends on T021)

**Checkpoint**: US1+US2 work — Current/Next/Secured/Final badges 100% ledger SC-003, Secured escudo `KEEP_SECURED_SCORE` 100% + `LOSE_ALL` 0 SC-004, placeholder "—" no break, quickstart V2 green

---

## Phase 5: User Story 3 — Transición de ronda sincronizada con servidor y visualmente clara (Priority: P1)

**Goal**: Cambio `Current Level k → k+1` y `Previous` marcando `k` como `completed` solo tras `hydrate` exitoso `GET /players/me` disparado por `RoundCompleted`/`QuestionAvailable`/`ScoreUpdated`/`Reconnected` (payload evento nunca fuente de rewards/level/isCorrect), animación premium <400ms (scale 1.02 glow `var(--shadow-premium)` 300ms + check 200ms) + `prefers-reduced-motion` instantáneo + `aria-live="polite"` "Avanzaste a ronda X", mantener `current` si hydrate falla y mostrar `ErrorState` con `CorrelationId` `Retry` + `exponential backoff`, salto >1 (reconnect perdió 2 rondas) → hydrate directo a `current` autoritativo sin animar intermedios falsos

**Independent Test**: `ROUND_IN_PROGRESS` 4 `EVALUATED` → `RoundCompleted {4}` + `QuestionAvailable {5}` → hydrate 200 → `previousTransition 4` check completed, `current 5` animating 300ms + aria-live <500ms; hydrate 500 → stays 4 + ErrorState `CorrelationId` Retry; disconnect 4→6 offline reconnect → hydrate current 6 jump direct (US3, quickstart V3, SC-005/SC-006/SC-007)

### Tests for User Story 3

- [x] T023 [P] [US3] Ladder transition store unit test for hydrate gate in `src/Player/QuizArena.Player/src/app/stores/player-rounds.store.spec.ts` (mock `GamesApi.getMyState` 200 current 4→5 after `RoundCompleted` event trigger `bindRealtimeLadder`, verify `_animatingRound===5` 350ms then null, `previousRoundNumber` updated, `aria-live` announcement "Avanzaste a ronda 5"; hydrate 500 → `_animatingRound null` stays 4 `status==='error'` `correlationId` set, no advance; reconnect jump 4→6 diff>1 → direct 6 without 5 intermediate checked)
- [x] T024 [P] [US3] Ladder transition component test in `src/Player/QuizArena.Player/src/app/features/game/player-rounds.component.spec.ts` (verify `class="animating"` added to current row then removed after 350ms, `@media prefers-reduced-motion: reduce` transition none mocked, `aria-live="polite"` div announces advancement, error state shows `app-error-state` with Retry; verify event payload not used → change `Round.level` only after hydrate)

### Implementation for User Story 3

- [x] T025 [US3] Implement transition animation logic in `src/Player/QuizArena.Player/src/app/stores/player-rounds.store.ts` (add `previousRoundNumber` tracking + `_animatingRound` signal, in `hydrateLadder` `tapResponse` after `patchState({ladder,...currentRoundNumber, previousRoundNumber: oldCurrent})` set `_animatingRound=current` + `setTimeout 350ms → _animatingRound=null` via `effect`, detect jump `if Math.abs(current - previous)>1` → no intermediate animation; on `catchError` keep `current` previous + `status:'error'` + `correlationId` from `HttpErrorResponse.error.correlationId`, add `exponential backoff` optional for retry; ensure `bindRealtimeLadder` never patches ladder directly without hydrate) (depends on T014)
- [x] T026 [US3] Implement transition UI and error handling in `src/Player/QuizArena.Player/src/app/features/game/player-rounds.component.ts` and `.css` (template `ol.ladder` `li [class.animating]="_animatingRound()===roundNumber"` + `<div aria-live="polite" class="sr-only">{{announcement()}}</div>` announcement computed "Avanzaste a ronda {{current}}..." + `error` branch `app-error-state (retry)="hydrateLadder(gameId())"`, CSS `.animating {animation: ladderPulse 300ms}` `@keyframes ladderPulse` + `@media prefers-reduced-motion: reduce {transition:none; transform:none; animation:none;}` per research.md D4) (depends on T025)
- [x] T027 [US3] Verify `GameRealtimeService` binding for ladder in `src/Player/QuizArena.Player/src/app/core/realtime/game-realtime.service.ts` (verify `withAutomaticReconnect` already, add method `bindLadderHydrate(gameId: string, hydrateFn: ()=>void)` or reuse existing `events$` so `PlayerRoundsStore.bindRealtimeLadder` subscribes to `RoundCompleted|QuestionAvailable|ScoreUpdated|GameFinished|Reconnected` and calls `hydrateLadder`, debounce 300ms for rapid events to avoid duplicate hydrates, ensure `accessTokenFactory` uses `oidc.getAccessToken()`)

**Checkpoint**: US1+US2+US3 work — 100% transitions only after hydrate SC-005 0% payload trust SC-007, anim <400ms + aria-live 100% + reduced-motion 100% SC-006, quickstart V3 green

---

## Phase 6: User Story 4 — Experiencia responsive, accesible y premium en tema Player (Priority: P2)

**Goal**: Ladder `Cinematic Immersive Premium Competitive` con tokens `data-theme="player"` sin literales (`var(--space-*) var(--color-*) gradiente final`), responsive 375–1536 sin scroll horizontal (ladder `max-height:40vh overflow-y:auto` scrolleable interna si N=15, sidebar sticky ≥1024px 280px else apilada 375px), targets ≥44px, WCAG 2.2 AA (contraste tokens, foco `outline:2px solid var(--color-primary)` `role="list"`/`listitem` `aria-current` único `aria-live` `aria-label` per fila "Ronda 4 de 10, nivel Intermediate" `Tab/Shift+Tab` escudo/corona `aria-hidden`)

**Independent Test**: Resize 375px → Header? Ladder stacked no horizontal scroll, N=15 scrolleable interna 40vh; ≥1024px sidebar sticky; 0 literales CSS var(--*); axe/Lighthouse 0 violations AA `aria-current`/`aria-live`/`list` foco visible targets ≥44px 100%; qualitative 80% Cinematic/Premium (US4, quickstart V4, SC-008/SC-009)

### Tests for User Story 4

- [x] T028 [P] [US4] Responsive and a11y test for ladder in `src/Player/QuizArena.Player/src/app/features/game/player-rounds.component.spec.ts` (set viewport 375/1024/1536 → verify `player-rounds` `data-theme="player"` exists, computed style has no literal color (scan CSS for `var(--)` presence), axe run 0 violations `aria-current` single `role=list`, keyboard Tab through listitems focus visible, targets ≥44px via `getBoundingClientRect`, 375px `overflow-y:auto` when N=15)
- [x] T029 [P] [US4] Premium perception placeholder test in `specs/030-player-rounds/checklists/requirements.md` or docs (verify `player-rounds.component.css` uses `var(--player-gradient-final)` for final + `var(--shadow-premium)` for current glow + `var(--color-success-subtle)` for secured, qualitative SC-009 80% rating documented)

### Implementation for User Story 4

- [x] T030 [US4] Polish responsive layout in `src/Player/QuizArena.Player/src/app/features/game/game.component.ts` and `src/Player/QuizArena.Player/src/app/features/game/player-rounds.component.css` (ensure `game-body {display:grid grid-template-columns:280px 1fr gap:var(--space-4)} @media max-width:1023px {display:flex flex-direction:column}` sidebar `position:sticky top:var(--space-4)` desktop, ladder `max-height:40vh overflow-y:auto` mobile 375px `max-height:60vh`, verify `OroQuizClash.AppHost/AppHost.cs` `quizarena-player` container styles still mount `design-system/tokens` via `angular.json`) (depends on T015)
- [x] T031 [US4] Harden a11y and tokens in `src/Player/QuizArena.Player/src/app/features/game/player-rounds.component.ts` (verify `section role="region" aria-label`, `ol role="list" aria-label`, `li role="listitem" aria-label per row + aria-current step single`, `aria-live="polite"` announcement div `class="sr-only"`, shield/crown `aria-hidden="true"`, foco `outline:2px solid var(--color-primary)` in CSS, targets `min-height:44px`, `empty` `role="status"` WAITING, `terminal isTerminal` blocks animation via store, verify no inline styles literals) (depends on T021)

**Checkpoint**: All 4 stories functional — ladder N exact + rewards + sync transition + responsive WCAG 100% SC-008 premium 80% SC-009, quickstart V4 green

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Hardening, docs, observability, final validation

- [x] T032 [P] Add ProblemDetails mapping test for ladder errors in `tests/OroQuizClash.Api.Tests/Contracts/PlayerRoundsErrorsMappingTests.cs` (assert `GameNotFound 404`, `PlayerNotInGame 403` `PlayerIdentityMismatch`, `InvalidGameState 400` when `currentRoundNumber` exceeds `maxRounds` map via `GlobalExceptionHandler` → `Result.ToHttpResult()` RFC 7807 with `CorrelationId/TraceId`, verify `X-Correlation-Id` echo)
- [x] T033 [P] Verify `X-Correlation-Id` propagation test in `src/Player/QuizArena.Player/tests/integration/player-rounds-correlation.spec.ts` (mock `GamesApi.getMyState` → assert header `X-Correlation-Id` UUID sent per hydrate, `ErrorState` displays `CorrelationId/TraceId` from `ProblemDetails`, retry re-sends new UUID, verify `correlation-id.interceptor` already covers ladder hydrate)
- [x] T034 [P] Verify empty/terminal states edge cases in `src/Player/QuizArena.Player/src/app/stores/player-rounds.store.spec.ts` (test `currentRoundNumber null WAITING` → status `empty` "Aún no inicia", `isTerminal WITHDRAWN/ELIMINATED/FINISHED` → `_animatingRound null` no transition, rollback `currentRoundNumber` decreasing hydrate corrected via `aria-live` "Corrección: ronda X", `N=15` builds 15 rows without hardcode)
- [x] T035 [P] Update design-system and Player README in `src/Player/QuizArena.Player/README.md` and `specs/030-player-rounds/spec.md` Status (add `Player Rounds` section: ladder Round 1..N `PlayerRoundsStore` `LadderRow[]` 6 states Current/Previous/Current Reward/Next/Secured/Final, sync hydrate `withAutomaticReconnect`, `data-theme="player"` WCAG, transition <400ms)
- [x] T036 [P] Run quickstart validation in `specs/030-player-rounds/quickstart.md` (execute V1-V5: N filas exactas + Difficulty, rewards ledger 4 types + LOSE_ALL, transition sync hydrate/error/reconnect jump, responsive 375-1536 axe, empty/terminal, fix gaps if any)
- [x] T037 Add architecture test for rounds isolation in `tests/OroQuizClash.Architecture.Tests/PlayerRoundsIsolationTests.cs` (verify `PlayerRoundsStore` not in `OroQuizClash.Domain` (Domain ↛ Angular), `GetMyPlayerState` uses `sub` not body, no client `isCorrect`/`points` trust, `LadderRow` view-model not stored, BuildingBlocks constraints `IRepository` not leaked)
- [x] T038 Security hardening verify in `src/Player/QuizArena.Player/src/app/core/interceptors/error.interceptor.ts` (hide sensitive details, propagate `traceId`, handle 429 `GamePlayLimiter` RetryAfter already from 027+028+029, ensure 403 `PlayerIdentityMismatch` audit logged, verify ladder hydrate `Bearer` only `apiUrl`)
- [x] T039 Final `dotnet build OroQuizClash.slnx && dotnet test` green and `npm run lint` with `@ngrx/eslint-plugin` clean and `npm test -- --watch=false` `player-rounds.store` + `player-rounds.component` pass, update `specs/030-player-rounds/spec.md` Status to `Ready for Review`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately (verifies 027+028+029 SPA + monolith)
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (GamesApi `getMyState` with rewards/secured, GameRealtimeService `withAutomaticReconnect`, interceptors `X-Correlation-Id`, shared UI states, `GetMyPlayerState` DTO verified)
- **User Stories (Phase 3+)**: All depend on Foundational
  - **US1 (P1) Ladder N + Difficulty**: No other story dependency — MVP (N filas exactas `aria-current`)
  - **US2 (P1) Current/Next/Secured/Final**: Depends on US1 ladder store/component `LadderRow[]` N + `buildLadder` (needs T013/T014) but independently testable with mocked `GET /players/me`
  - **US3 (P1) Transition sync server**: Depends on US1 `PlayerRoundsStore hydrateLadder/bindRealtimeLadder` + `GameRealtimeService` (needs T014/T015) but testable with mocked hydrate
  - **US4 (P2) Responsive/A11y premium**: Depends on US1/US2 component `player-rounds.component.ts/.css` layout (needs T015/T021) — polish parallel with US3 if staffed
- **Polish (Final)**: Depends on all desired stories (US1+US2+US3 for MVP, US4 for completeness)

### User Story Dependencies

- **US1 (P1)**: After Foundational — no deps
- **US2 (P1)**: After Foundational + US1 `buildLadder`/`PlayerRoundsStore` (integrates with US1's ladder but testable with mocked `getMyState`)
- **US3 (P1)**: After Foundational + US1 store realtime hook (depends on T014 `PlayerRoundsStore`)
- **US4 (P2)**: After Foundational — depends on US1/US2 component CSS but can start after US1 layout

### Within Each User Story

- Tests (if included) written before implementation (T010 before T013, T018 before T020, T023 before T025, T028 before T030)
- Types/helper (`ladder.model.ts` T013) before store (T014) before component (T015) before wiring (T017)
- Store before component UI, component before integration with `GameComponent`
- Core implementation before animation polish

### Parallel Opportunities

- Phase 1: T002 + T003 parallel (different files)
- Phase 2: T005 + T006 + T007 parallel (different files); T004 later but independent of T005/6/7 except `games.api.ts` vs interceptors
- Phase 3: T010 + T011 + T012 parallel (contract/store/integration tests different files); T013 parallel with T010 tests start (different files)
- Phase 4: T018 + T019 parallel (store spec vs component spec); T020 then T021 sequential same file
- Phase 5: T023 + T024 parallel (store spec vs component spec); T027 independent file parallel with tests
- Phase 6: T028 + T029 parallel (component spec vs doc checklist)
- Phase 7: T032 + T033 + T034 + T035 + T036 parallel (different files); T037 after T032 same project but separable
- Different stories can start in parallel after Foundational if staffed (US2 needs only `buildLadder` interface agreed, US3 needs only `hydrateLadder` signature)

### Parallel Example: User Story 1 (Ladder N + Difficulty)

```bash
# Launch tests for US1 together:
Task T010: Contract test in tests/OroQuizClash.Api.Tests/Contracts/PlayerRoundsLadderContractTests.cs
Task T011: Ladder store unit test in src/Player/QuizArena.Player/src/app/stores/player-rounds.store.spec.ts
Task T012: Integration test in src/Player/QuizArena.Player/tests/integration/player-rounds-ladder.spec.ts

# Launch types + store after tests:
Task T013: LadderRow types + buildLadder helper in src/Player/QuizArena.Player/src/app/features/game/ladder.model.ts
Task T014: PlayerRoundsStore with ladder state in src/Player/QuizArena.Player/src/app/stores/player-rounds.store.ts
```

### Parallel Example: User Story 3 (Transition sync)

```bash
# Launch tests:
Task T023: Store hydrate gate test in src/Player/QuizArena.Player/src/app/stores/player-rounds.store.spec.ts
Task T024: Component transition test in src/Player/QuizArena.Player/src/app/features/game/player-rounds.component.spec.ts

# Launch implementation:
Task T025: Transition logic in src/Player/QuizArena.Player/src/app/stores/player-rounds.store.ts
Task T026: Transition UI in src/Player/QuizArena.Player/src/app/features/game/player-rounds.component.ts
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (verify SPA/monolith)
2. Complete Phase 2: Foundational (GamesApi with rewards/secured, realtime, interceptors, UI states, `GetMyPlayerState` DTO verified)
3. Complete Phase 3: US1 (Ladder N filas exactas `aria-current` + `Level` + `completed/current/upcoming`)
4. **STOP and VALIDATE**: `GET /players/me` shows ladder N exact without gaps, Current `aria-current="step"` premium, Previous completed check, upcoming muted, `MaxRounds 15` 15 rows, `data-theme="player"` 375-1536 no scroll, quickstart V1 SC-001/SC-002/SC-010
5. Deploy/demo MVP (ladder core works end-to-end)

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 → Test independently → Demo MVP (N filas + Difficulty)
3. Add US2 → Test independently → Demo (Current/Next/Secured/Final 100% ledger + Final corona + placeholder "—")
4. Add US3 → Test independently → Demo (Transition sync hydrate <400ms + aria-live + error/reconnect jump)
5. Add US4 → Test independently → Demo (Responsive 375-1536 axe + premium 80%)
6. Polish → final validation V1-V5, SC-001..010

### Parallel Team Strategy

With 3 developers after Foundational:

1. Team completes Setup + Foundational together
2. Once Foundational done:
   - Developer A: US1 (Ladder N + Difficulty `ladder.model` + `PlayerRoundsStore` + `PlayerRoundsComponent` skeleton)
   - Developer B: US2 (Rewards Current/Next/Secured/Final `buildLadder` extension + badges + escudo/corona)
   - Developer C: US3 (Transition sync `hydrate` gate + animation + error/reconnect) + US4 (Responsive/a11y premium polish) (US3 and US4 share component CSS but no same-line conflict)
3. Polish by A/B/C after stories done

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability (US1..US4)
- Each user story independently completable and testable per spec Independent Test + quickstart V1-V4
- Verify tests fail before implementing (red → green)
- Commit after each task or logical group; push to branch `030-player-rounds`
- Avoid: vague tasks, same file conflicts, cross-story state leakage (e.g., `ladder` leaking across gameId)
- Do not edit `.aspire/modules` — wire via AppHost.cs only
- Tokens: `design-system/tokens` + `data-theme="player"` per SPEC-016 cinematic premium + `prefers-reduced-motion`
