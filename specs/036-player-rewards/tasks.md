# Tasks: Player Rewards (036)

**Input**: Design documents from `/specs/036-player-rewards/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md
**Branch**: `036-player-rewards` | **Constitution**: v1.1.0 (I-VI, A-J)

**Organization**: Tasks grouped by user story — each story independently testable and deliverable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify existing Player SPA (027+029+032+035) + modular monolith and prepare rewards scaffolding

- [x] T001 Verify existing project structure per `specs/036-player-rewards/plan.md` (`src/Player/QuizArena.Player` Angular 22 standalone, `src/OroQuizClash.Domain`, `Application`, `Infrastructure`, `Api`, `OroQuizClash.AppHost` in `OroQuizClash.slnx`) and `dotnet build OroQuizClash.slnx` passes
- [x] T002 Verify Player SPA dependencies in `src/Player/QuizArena.Player/package.json` (`@angular/core 22`, `@ngrx/signals 22`, `angular-auth-oidc-client`, `@microsoft/signalr`, `rxjs`) and `design-system/tokens/design-tokens.css` is imported via `angular.json` styles with `data-theme="player"` per SPEC-016
- [x] T003 Verify `BuildingBlocks` dependencies and `Directory.Packages.props` central versions for `net10.0` (`LangVersion latest`, `Nullable enable`) and `src/BuildingBlocks/` references per plan.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared rewards infrastructure MUST complete before ANY user story — `RedeemReward` `POST /rewards/{id}/redeem` `X-Idempotency-Key` per `rewardId`, `GetRewards` `Available/Required/Remaining/Status`, `GetPlayerRedemptions`, `PlayerRewardsStore` `redeem()` `rxMethod`, `RewardsApi`, interceptors, shared UI states

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Verify `RewardsApi` client in `src/Player/QuizArena.Player/src/app/features/shared/rewards.api.ts` has `getRewards(gameId?)`, `getMyRedemptions()` and `redeem(rewardId, idempotencyKey, gameId)` (`POST /rewards/{id}/redeem` `X-Idempotency-Key` `X-Correlation-Id` + `Authorization Bearer`) and `getWallet(gameId)` per `contracts/api-contracts.md` §1-3
- [x] T005 [P] Verify interceptors in `src/Player/QuizArena.Player/src/app/core/interceptors/` — `correlation-id.interceptor.ts` (`X-Correlation-Id` UUID per `POST /redeem`), `auth.interceptor.ts` (`Authorization: Bearer` only `apiUrl` secureRoutes), `error.interceptor.ts` (RFC 7807 `ProblemDetails` mapping `RewardUnavailable 409`/`InsufficientPoints 409`/`RewardNotFound 404`, 401 silentRenew, 429 RetryAfter) per plan.md Constraints H/I
- [x] T006 [P] Verify shared UI states in `src/Player/QuizArena.Player/src/app/shared/ui/` — `loading-skeleton.component.ts` (`aria-live="polite"` `aria-busy`), `empty-state.component.ts`, `error-state.component.ts` (CorrelationId/TraceId `Retry` 44px, `aria-live="assertive"`) per `data-model.md`
- [x] T007 [P] Verify `RedeemReward` + `GetRewards` + `GetPlayerRedemptions` slices in `src/OroQuizClash.Application/Features/Rewards/RedeemReward.cs`, `GetRewards.cs`, `GetPlayerRedemptions.cs` (idempotent `X-Idempotency-Key` `idemp-redeem-{rewardId}` `UNIQUE (PlayerId,IdempotencyKey)` `RowVersion` per `Reward` + per `GamePlayer`, `ReserveStock` + `Game.ConsumePoints` → `REWARD_REDEMPTION` ledger `deduction` per `SufficientBalanceRule`, `RewardUnavailable 409` `InsufficientPoints 409` `RewardNotFound 404`) per `data-model.md`
- [x] T008 Verify `PlayerRewardsStore` scaffolding in `src/Player/QuizArena.Player/src/app/stores/player-rewards.store.ts` (`signalStore withState {wallet, catalog, selectedReward, history, redeemStatus, consolation}`, `computed isRedeemable/remainingPoints`, `redeem()` `rxMethod` `X-Idempotency-Key` `idemp-redeem-{rewardId}` `sessionStorage` per `rewardId`, `hydrate` via `GET /rewards` + `GET /redemptions` 4 métricas, `remaining = Available - Required`) is ready to extend without regression to `PlayerGameStore`
- [x] T009 Verify `Reward` + `RewardRedemption` + `Game.ConsumePoints` invariants in `src/OroQuizClash.Domain/Rewards/Reward.cs` (`Reward.ReserveStock` `RewardAvailableRule` decrement `Stock` `RowVersion`) and `RewardRedemption.cs` (`Create` `REQUESTED` `UNIQUE (PlayerId,IdempotencyKey)` `RedemptionTransitionRule`) and `src/OroQuizClash.Domain/Games/Game.cs` (`Game.ConsumePoints` `SufficientBalanceRule` → `PointTransaction` `REWARD_REDEMPTION`) per Constitution C/D/F and `data-model.md` §2-3

**Checkpoint**: Foundation ready — `dotnet build` passes, `POST /rewards/{id}/redeem` `X-Idempotency-Key` per `rewardId` idempotente + `GET /rewards` `Available/Required/Status` + `GET /redemptions` `History` + `PlayerRewardsStore` `redeem()` `rxMethod` `RowVersion` per `Reward`/`GamePlayer`

---

## Phase 3: User Story 1 — Points Wallet y Rewards Catalog (Priority: P1) 🎯 MVP

**Goal**: Wallet header muestra `Available Points` autoritativo + Catalog grid muestra cada recompensa `Required Points` + `Reward Status` `Canjeable/Puntos insuficientes/Agotada/No disponible` + `Remaining Points` `Quedan 400` o `Te faltan 700` derivados de `GET /rewards` ledger 0% cálculo cliente, `aria-label` descriptivo, responsive 1/2/4 col targets ≥44px

**Independent Test**: Con `Available 1200` y rewards `Required 800` + `1500`, abrir `/rewards` → verify `Available 1200` header + card `Required 800 Canjeable Quedan 400` y `Required 1500 Puntos insuficientes Te faltan 300` coincidentes con `GET /rewards` ledger (0% cliente) (spec US1, quickstart V1, SC-001)

### Tests for User Story 1

- [x] T010 [P] [US1] Contract test for `GET /rewards` 4 métricas in `tests/OroQuizClash.Api.Tests/Contracts/PlayerRewardsCatalogContractTests.cs` (WebApplicationFactory JWT `PLAYER`, assert `availablePoints 1200` + `rewards[].pointsRequired` `status` `available` match `GamePlayer.Score` + `Reward` ledger, `X-Correlation-Id` echo, `PlayerNotInGame` 403)
- [x] T011 [P] [US1] Rewards catalog component unit test for 4 métricas in `src/Player/QuizArena.Player/src/app/features/rewards/rewards-catalog.component.spec.ts` (TestBed `RewardsCatalogComponent` mock `PlayerRewardsStore` with `available 1200 rewards [{800 ACTIVE Stock10}, {1500 ACTIVE Stock5}]`, verify `Available Points 1200` `Required Points 800 Canjeable Quedan 400` `Required 1500 Puntos insuficientes Te faltan 300` `aria-live polite` `aria-label` descriptivo, `Required 800 Agotada` when Stock 0)
- [x] T012 [P] [US1] Integration test for catalog rendering in `src/Player/QuizArena.Player/tests/integration/player-rewards-catalog.spec.ts` (mock `getRewards` 4 métricas, render `RewardsCatalogComponent` → assert Available header + 2 cards `Canjeable`/`Puntos insuficientes` visible, `data-theme="player"` 0 literales, responsive grid 1col 375 no scroll)

### Implementation for User Story 1

- [x] T013 [P] [US1] Create `RewardsDisplay` types in `src/Player/QuizArena.Player/src/app/features/rewards/rewards-display.model.ts` (export `RewardsDisplay {availablePoints, requiredPoints, remainingPoints, remainingDisplay, rewardStatus}`, helper `formatPoints(n) => \`\${n} pts\``, `deriveRewardStatus(available, required, isAvailable)` → `Canjeable|Puntos insuficientes|Agotada|No disponible` + `formatRemaining(available, required, isAvailable)` per `data-model.md` §2) (depends on T009)
- [x] T014 [US1] Extend `RewardsApi` for wallet+catalog in `src/Player/QuizArena.Player/src/app/features/shared/rewards.api.ts` (add `getRewards(gameId?: string)` `GET /api/rewards?gameId` + `X-Correlation-Id` + `Authorization Bearer`, map `availablePoints` + `rewards[]` `RewardView`, per `contracts/api-contracts.md` §1) (depends on T004)
- [x] T015 [US1] Implement `PlayerRewardsStore` wallet+catalog in `src/Player/QuizArena.Player/src/app/stores/player-rewards.store.ts` (extend with `wallet: {availablePoints, lastUpdated}` `catalog: RewardView[]` `computed availablePoints/remainingPointsFor(id)` `isRedeemable(id) => available >= required && isAvailable`, `hydrate()` `switchMap` `RewardsApi.getRewards(gameId)` `patchState({wallet, catalog})`, per `data-model.md` §1-2) (depends on T013)
- [x] T016 [US1] Create `RewardsCatalogComponent` with 4 métricas autoritativas in `src/Player/QuizArena.Player/src/app/features/rewards/rewards-catalog.component.ts` (standalone `selector app-rewards-catalog`, inject `PlayerRewardsStore`, template `Available Points {{store.wallet().availablePoints}} pts` header `role="status" aria-live polite` + `@for catalog` card `Required {{r.pointsRequired}} pts` `Reward Status {{deriveRewardStatus()}}` badge `Remaining {{formatRemaining()}}` per `contracts/ui-contracts.md` §1) (depends on T015)
- [x] T017 [US1] Add rewards catalog styles with tokens in `src/Player/QuizArena.Player/src/app/features/rewards/rewards-catalog.component.css` (create CSS `wallet {background:var(--color-surface); border:1px solid var(--color-border); border-radius:var(--radius-lg); padding:var(--space-6)}` `grid {display:grid; grid-template-columns:1fr; @media 768 1fr 1fr; 1536 1fr 1fr 1fr 1fr; gap:var(--space-3)}` `.card {padding:var(--space-3); min-height:160px; border-radius:var(--radius-md); border:1px solid var(--color-border); background:var(--color-surface)}` `.badge.canjeable {background:var(--color-success)}` tokens only) (depends on T016)
- [x] T018 [US1] Wire Rewards routes in `src/Player/QuizArena.Player/src/app/app.routes.ts` (add `{path:'rewards', component:RewardsCatalogComponent, canActivate:[authGuard,mustChangePasswordGuard]}` `data-theme="player"`, lazy standalone, `redirectTo` login if !JWT) (depends on T016)

**Checkpoint**: US1 fully functional — `npm test` `rewards-catalog.component.spec` 4 métricas `Available/Required/Remaining/Status` `aria-live polite` passes, contract `GET /rewards` 4 métricas 0% cliente SC-001, `axe` `group` passes, catalog muestra 1200 → Canjeable Quedan 400 / Puntos insuficientes Te faltan 300, responsive 375 1col no scroll (quickstart V1 SC-001)

---

## Phase 4: User Story 2 — Reward Detail y canjear con confirmación (Priority: P1)

**Goal**: Reward Detail muestra `Available/Required/Remaining/Reward Status` + descripción con `Remaining = Available - Required` (0 si exacto) + botón `Canjear` habilitado solo si `Canjeable` (≥44px) → diálogo 2 pasos `role="dialog"` `aria-modal` con resumen `Required/Remaining` + `X-Idempotency-Key` per `rewardId` → `POST /rewards/{id}/redeem` → `Confirmation` `Canjeada` `Remaining` actualizado `Reference` idempotente sin duplicar `REWARD_REDEMPTION`

**Independent Test**: Con `Available 1200 Required 800 Canjeable`, abrir `/rewards/:id` → `Canjear` habilitado → `Canjear` → diálogo `Confirmar canje` con `Required 800 Remaining 400` → `Confirmar` → `POST /redeem` 200 `REQUESTED` `Remaining 400` idempotente; con `Available 800 Required 1500` → `Canjear` deshabilitado `Puntos insuficientes` (spec US2, quickstart V2-V3, SC-002/SC-003/SC-004/SC-005)

### Tests for User Story 2

- [x] T019 [P] [US2] Reward detail component test for 4 métricas + 2 pasos in `src/Player/QuizArena.Player/src/app/features/rewards/reward-detail.component.spec.ts` (mock `PlayerRewardsStore` `Available 1200 Required 800 Canjeable Remaining 400`, verify `Available 1200 pts` `Required 800 pts` `Remaining 400 pts` `aria-live polite` `Canjear` `min-height:44px` `aria-label="Canjear recompensa"` habilitado, `Canjeable 800 vs 1500 deshabilitado Te faltan 700`, `Canjear` → `Confirmar canje` `role="dialog"` `aria-modal` 2 pasos, `Cancelar`/`Escape` no `POST /redeem`)
- [x] T020 [P] [US2] PlayerRewardsStore idempotency test in `src/Player/QuizArena.Player/src/app/stores/player-rewards.store.spec.ts` (verify `redeem(rewardId)` `X-Idempotency-Key` `idemp-redeem-{rewardId}` `sessionStorage` per `rewardId`, `Confirmar` envía `POST /redeem` con header `X-Idempotency-Key` + `X-Correlation-Id` + `Authorization Bearer`, `Cancelar` no envía, reintento misma key idempotente sin duplicar)

### Implementation for User Story 2

- [x] T021 [US2] Create `RewardDetailComponent` with 4 métricas + redeem 2 pasos in `src/Player/QuizArena.Player/src/app/features/rewards/reward-detail.component.ts` (standalone `selector app-reward-detail`, inject `PlayerRewardsStore`, template métricas `Available/Required/Remaining/Status` `role="group" aria-label="Puntuaciones"` + `Canjear` button `min-height:44px` disabled `!store.isRedeemable()` + diálogo `showConfirm` `role="dialog" aria-modal="true" aria-label="Confirmar canje"` `Confirmar` 44px → `store.redeem(rewardId)` `X-Idempotency-Key` `idemp-redeem-{rewardId}` + `Cancelar`/`Escape` `showConfirm=false` sin llamada, per `contracts/ui-contracts.md` §2) (depends on T016)
- [x] T022 [US2] Implement `PlayerRewardsStore.redeem` with idempotency in `src/Player/QuizArena.Player/src/app/stores/player-rewards.store.ts` (extend `redeem: rxMethod<string>(pipe(switchMap((rewardId)=>{const key=sessionStorage.getItem(\`idemp-redeem-\${rewardId}\`)??crypto.randomUUID(); sessionStorage.setItem(\`idemp-redeem-\${rewardId}\`,key); return _api.redeem(rewardId,key, gameId).pipe(tapResponse({next:(r)=>patchState({history:[r,...history()], wallet:{...wallet(), availablePoints: wallet().availablePoints - r.points}, redeemStatus:'SUCCESS'}), error:(err)=>patchState({redeemStatus:err})}))}))`, `hydrate` restores `Available` tras redeem, per `research.md` D3) (depends on T015)
- [x] T023 [US2] Add reward detail + dialog styles with tokens in `src/Player/QuizArena.Player/src/app/features/rewards/reward-detail.component.css` (add `detail {max-width:600px; padding:var(--space-6); gap:var(--space-4)}` `dialog {max-width:400px; background:var(--color-surface); border:1px solid var(--color-border); border-radius:var(--radius-lg); padding:var(--space-6); gap:var(--space-3)}` `.warning {color:var(--color-warning)}` tokens only) (depends on T021)
- [x] T024 [US2] Enhance `RewardsCatalogComponent` navigation to detail in `src/Player/QuizArena.Player/src/app/features/rewards/rewards-catalog.component.ts` (add `Ver detalle` button `min-height:44px` per card → `router.navigate(['/rewards', rewardId], {queryParams:{gameId}})` `aria-label="Ver detalle {{name}}"`) (depends on T016)

**Checkpoint**: US1+US2 work — 4 métricas autoritativas 100% SC-001 + flujo Detail→Confirmation <90s SC-002 + 0% canje cliente SC-003 + 2 pasos 0% accidental SC-004 + idempotente 100% SC-005 sin duplicar `REWARD_REDEMPTION`, quickstart V2-V3 green

---

## Phase 5: User Story 3 — Redemption History (Priority: P2)

**Goal**: `Redemption History` `/rewards/history` muestra lista paginada por `RequestedAt` desc con cada fila `rewardName` `Required Points` `Remaining/Reference` `Reward Status` `Canjeada/Consolation` + `requestedAt`, empty-state "Aún no has canjeado recompensas" + CTA a `/rewards`, actualizado tras `Redeem` sin recarga completa

**Independent Test**: Con 3 canjes previos abrir `/rewards/history` → 3 filas `Pack Oro 800 Canjeada` orden desc; sin canjes → empty-state + CTA (spec US3, quickstart V4, SC-006)

### Tests for User Story 3

- [x] T025 [P] [US3] Redemption history component test in `src/Player/QuizArena.Player/src/app/features/rewards/redemption-history.component.spec.ts` (mock `PlayerRewardsStore` `history:[{Pack Oro 800 REQUESTED}, {Poción 1500 APPROVED}, {Consolation 0 APPROVED isConsolation}]`, verify `role="list"` `aria-label="Historial de canjes"` 3 `role="listitem"` `Pack Oro 800 pts Canjeada` orden desc `RequestedAt`, verify empty → `empty-state` "Aún no has canjeado recompensas" + CTA `/rewards`)
- [x] T026 [P] [US3] History contract test for `GET /redemptions` in `tests/OroQuizClash.Api.Tests/Contracts/PlayerRewardsHistoryContractTests.cs` (WebApplicationFactory JWT `PLAYER`, seed 3 redemptions, assert `redemptions[]` `id` `rewardId` `points` `status` `requestedAt` desc + `Consolation` `points 0` `APPROVED`, `X-Correlation-Id` echo)

### Implementation for User Story 3

- [x] T027 [US3] Implement `RedemptionHistoryComponent` in `src/Player/QuizArena.Player/src/app/features/rewards/redemption-history.component.ts` (standalone `selector app-redemption-history`, inject `PlayerRewardsStore`, template `@if history().length===0` `app-empty-state` CTA else `role="list"` `@for history` `role="listitem"` `rewardName` `points pts` badge `status` `Canjeada`/`Consolation` `requestedAt` `reference`, `@if hasNext` `Cargar más` 44px paginated, per `contracts/ui-contracts.md` §3) (depends on T015)
- [x] T028 [US3] Wire history hydrate in `src/Player/QuizArena.Player/src/app/stores/player-rewards.store.ts` (extend `hydrateHistory: rxMethod<void>(pipe(switchMap(()=>_api.getMyRedemptions().pipe(tapResponse({next:(h)=>patchState({history:h.redemptions}), error:(err)=>patchState({redeemStatus:err})}))))`, called on `/rewards/history` init y tras `redeem` success, per `contracts/api-contracts.md` §3) (depends on T022)
- [x] T029 [US3] Add history styles with tokens in `src/Player/QuizArena.Player/src/app/features/rewards/redemption-history.component.css` (add `history {max-width:800px; gap:var(--space-3)}` `.row {padding:var(--space-3); min-height:44px; border:1px solid var(--color-border); border-radius:var(--radius-md); display:flex; justify-content:space-between}` empty-state tokens only) (depends on T027)
- [x] T030 [US3] Add history route in `src/Player/QuizArena.Player/src/app/app.routes.ts` (add `{path:'rewards/history', component:RedemptionHistoryComponent, canActivate:[authGuard]}`, before `:rewardId` para evitar conflicto) (depends on T027)

**Checkpoint**: US1+US2+US3 work — 4 métricas SC-001 + Redeem 2 pasos SC-004/005 + History 100% SC-006 `RequestedAt` desc + empty CTA, quickstart V4 green

---

## Phase 6: User Story 4 — Consolation Reward (Priority: P2)

**Goal**: `Consolation Reward` otorgada automática por backend (`RewardRedemption.CreateAsConsolation` `APPROVED` `points 0`) al finalizar partida si elegible `ConsolationPolicy` aparece en `Wallet` (crédito si `FixedPoints`) y en `Redemption History` con badge `Consolation` `background:var(--color-info)` y motivo `eligibilityReason`, no listada como `Canjeable` en Catalog, no desconta `Stock`, no duplicada si ya recompensa estándar

**Independent Test**: Finalizar partida con puntaje bajo umbral pero elegible consolación → verify `Available` actualizado si `FixedPoints` + History fila `Consolation` `APPROVED` badge diferenciado + Catalog no muestra `Consolation` como canjeable (spec US4, quickstart V5, SC-007)

### Tests for User Story 4

- [x] T031 [P] [US4] Consolation badge test in `src/Player/QuizArena.Player/src/app/features/rewards/consolation-badge.component.spec.ts` (create `ConsolationBadgeComponent` `input isConsolation` verify badge `background:var(--color-info)` `aria-label="Recompensa de consolación"` visible solo si `isConsolation true`, no visible si false, `axe` 0)
- [x] T032 [P] [US4] Consolation history integration in `src/Player/QuizArena.Player/src/app/features/rewards/redemption-history.component.spec.ts` (extend mock `history` with `Consolation 0 APPROVED sourceGameId` verify row badge `Consolation` `color:var(--color-info)` + tooltip `eligibilityReason`, `RewardRedemption.CreateAsConsolation` not appear in `catalog` `isRedeemable`)

### Implementation for User Story 4

- [x] T033 [US4] Create `ConsolationBadgeComponent` in `src/Player/QuizArena.Player/src/app/features/rewards/consolation-badge.component.ts` (standalone `selector app-consolation-badge`, `input isConsolation: boolean`, template `@if isConsolation` `span` `Consolation` `background:var(--color-info,#3B82F6)` `border-radius:var(--radius-full)` `padding:var(--space-1) var(--space-2)` `aria-label="Recompensa de consolación"`) (depends on T027)
- [x] T034 [US4] Enhance `RedemptionHistoryComponent` for consolation in `src/Player/QuizArena.Player/src/app/features/rewards/redemption-history.component.ts` (add `app-consolation-badge` per row when `item.isConsolation` or `item.points===0 && status APPROVED`, tooltip `eligibilityReason`, filtrar `Consolation` de `Catalog` `isRedeemable` logic already excludes `isConsolation`) (depends on T033)
- [x] T035 [US4] Verify backend `CreateAsConsolation` exposure in `src/OroQuizClash.Application/Features/Rewards/GetPlayerRedemptions.cs` (ensure `GetPlayerRedemptionsResponse` incluye `isConsolation` derived `points===0 && status APPROVED` o `transitions` con system actor, no canje manual en `GetRewards` filter) (depends on T007)

**Checkpoint**: All 4 stories functional — 4 métricas autoritativas 100% SC-001, flujo <90s SC-002, 0% cliente SC-003, 2 pasos 0% accidental SC-004, idempotente 100% SC-005, History 100% SC-006, Consolation 100% SC-007 badge diferenciado sin duplicar estándar, quickstart V5 green

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Hardening, docs, observability, final validation

- [x] T036 [P] Add ProblemDetails mapping test for rewards errors in `tests/OroQuizClash.Api.Tests/Contracts/PlayerRewardsErrorsMappingTests.cs` (assert `RewardUnavailable 409` `InsufficientPoints 409` `RewardNotFound 404` `PlayerNotInGame 403` map via `GlobalExceptionHandler` → `Result.ToHttpResult()` RFC 7807 with `CorrelationId/TraceId`, `X-Correlation-Id` echo, `X-Idempotency-Key` idempotente)
- [x] T037 [P] Verify `X-Correlation-Id` + JWT gating test in `src/Player/QuizArena.Player/tests/integration/player-rewards-correlation.spec.ts` (mock `RewardsApi.redeem` → assert header `X-Correlation-Id` UUID + `Authorization Bearer` per `POST /redeem`, no JWT → 401 redirect OIDC, `ErrorState` displays `CorrelationId/TraceId` from `ProblemDetails`, `must_change_password` gating redirect)
- [x] T038 [P] Verify Remaining 0 / Available 0 / Stock 0 edge cases in `src/Player/QuizArena.Player/src/app/features/rewards/reward-detail.component.spec.ts` (test `Available 800 Required 800 Remaining 0 Canjeable`, `Available 0 Required 800 Te faltan 800` deshabilitado, `Stock 0 Agotada` deshabilitado, `Expired` `No disponible`, `Available` manipulado DevTools → ledger autoritativo 400)
- [x] T039 [P] Update Player README in `src/Player/QuizArena.Player/README.md` and `specs/036-player-rewards/spec.md` Status (add `Player Rewards` section: `Points Wallet` `Available Points` → `Rewards Catalog` 4 métricas `Available/Required/Remaining/Reward Status` → `Reward Detail` → `Redeem` 2 pasos `X-Idempotency-Key` per `rewardId` `Confirmation` `Canjeada` `Redemption History` paginado `Consolation` badge `data-theme="player"` WCAG responsive, `max-width:400px`)
- [x] T040 [P] Run quickstart validation in `specs/036-player-rewards/quickstart.md` (execute V1-V7: Wallet/Catalog 4 métricas ledger, Detail 2 pasos `X-Idempotency-Key`, Confirmation ledger idempotente, History paginado, Consolation badge, responsive 375-1536 axe, X-Correlation-Id + JWT, fix gaps if any)
- [x] T041 Add architecture test for rewards isolation in `tests/OroQuizClash.Architecture.Tests/PlayerRewardsIsolationTests.cs` (verify `PlayerRewardsStore`/`RewardsCatalogComponent` not in `OroQuizClash.Domain` (Domain ↛ Angular), `RedeemReward` uses `sub` not body, no client `deduction` calc (Domain `Reward.ReserveStock`/`Game.ConsumePoints`), BuildingBlocks `IRepository` not leaked, `Redeem` per `RewardId` not global)
- [x] T042 Security hardening verify in `src/Player/QuizArena.Player/src/app/core/interceptors/error.interceptor.ts` (hide sensitive details, propagate `traceId`, handle 429 `RateLimiter` `Retry-After`, ensure `Redeem` never leaks `Available` de otro, `NotRedemptionOwner 403` audit logged, verify `redeem` `Bearer` only `apiUrl`)
- [x] T043 Final `dotnet build OroQuizClash.slnx && dotnet test` green and `npm run lint` with `@ngrx/eslint-plugin` clean and `npm test -- --watch=false` `rewards-catalog` + `reward-detail` + `redemption-history` + `player-rewards.store` pass, update `specs/036-player-rewards/spec.md` Status to `Ready for Review`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately (verifies Angular 22 SPA + monolith BuildingBlocks)
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (RewardsApi `redeem` `X-Idempotency-Key` per `rewardId` + `GetRewards` 4 métricas, `PlayerRewardsStore` `redeem()` `rxMethod` `isRedeemable`, `Reward` `RowVersion` + `Game.ConsumePoints` ledger)
- **User Stories (Phase 3+)**: All depend on Foundational
  - **US1 (P1) Wallet+Catalog 4 métricas**: No other story dependency — MVP (4 métricas sin cálculo cliente)
  - **US2 (P1) Detail+Canjear 2 pasos**: Depends on US1 `Catalog` 4 métricas + `Available` (needs T013/T016) but independently testable with mocked `RewardsApi`
  - **US3 (P2) Redemption History**: Depends on US2 `Redeem` `REQUESTED` (needs T022) but testable with mocked `getMyRedemptions` sin canje real
  - **US4 (P2) Consolation**: Depends on US3 `History` display (needs T027) — polish parallel with US3 si staffed
- **Polish (Final)**: Depends on all desired stories (US1+US2 para MVP, US3+US4 para completitud)

### User Story Dependencies

- **US1 (P1)**: After Foundational — no deps
- **US2 (P1)**: After Foundational — depends on US1 `RewardsCatalog` 4 métricas (needs T013/T016) pero testable con mocked `Wallet`
- **US3 (P2)**: After Foundational + US2 `Redeem` (needs T022) pero testable con mocked `History`
- **US4 (P2)**: After Foundational — depende de US3 `History` layout pero puede iniciar tras US1 `wallet` si `Consolation` mockeada

### Within Each User Story

- Tests (if included) written before implementation (T010 before T013, T019 before T021, T025 before T027, T031 before T033)
- Types/helper (`rewards-display.model.ts` T013) before store (T015) before component (T021)
- Store before component UI, component before `app.routes.ts` integration
- Core implementation before `Redemption History` antes de `Consolation` polish

### Parallel Opportunities

- Phase 1: T002 + T003 parallel (different files)
- Phase 2: T005 + T006 + T007 parallel (different files); T004 luego T008
- Phase 3: T010 + T011 + T012 parallel (contract test / component spec / integration test different files); T013 parallel con T010 tests start (different files)
- Phase 4: T019 + T020 parallel (component spec vs store spec); T021 needs T019 T020; T022 parallel con T021 si staffed (different files store vs component)
- Phase 5: T025 + T026 parallel (component spec vs contract test); T027 needs T025; T028 needs T027
- Phase 6: T031 + T032 parallel (consolation badge vs history integration); T033 needs T031
- Phase 7: T036 + T037 + T038 + T039 + T040 parallel (different files); T041 after all; T042 serial error.interceptor
- Different stories can start in parallel after Foundational if staffed (US2 needs only `Available` interface agreed, US3 needs only `getMyRedemptions` signature)

### Parallel Example: User Story 1 (Wallet+Catalog 4 métricas)

```bash
# Launch tests for US1 together:
Task T010: Contract test in tests/OroQuizClash.Api.Tests/Contracts/PlayerRewardsCatalogContractTests.cs
Task T011: Rewards catalog unit test in src/Player/QuizArena.Player/src/app/features/rewards/rewards-catalog.component.spec.ts
Task T012: Integration test in src/Player/QuizArena.Player/tests/integration/player-rewards-catalog.spec.ts

# Launch types + store + component after tests:
Task T013: RewardsDisplay types in src/Player/QuizArena.Player/src/app/features/rewards/rewards-display.model.ts
Task T015: PlayerRewardsStore wallet+catalog in src/Player/QuizArena.Player/src/app/stores/player-rewards.store.ts
Task T016: RewardsCatalogComponent 4 métricas in src/Player/QuizArena.Player/src/app/features/rewards/rewards-catalog.component.ts
```

### Parallel Example: User Story 2 (Detail+Canjear 2 pasos)

```bash
# Launch tests:
Task T019: Reward detail component test in src/Player/QuizArena.Player/src/app/features/rewards/reward-detail.component.spec.ts
Task T020: PlayerRewardsStore redeem test in src/Player/QuizArena.Player/src/app/stores/player-rewards.store.spec.ts

# Launch implementation:
Task T021: RewardDetailComponent 2 pasos in src/Player/QuizArena.Player/src/app/features/rewards/reward-detail.component.ts
Task T022: PlayerRewardsStore.redeem idempotente in src/Player/QuizArena.Player/src/app/stores/player-rewards.store.ts
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (verify SPA/monolith)
2. Complete Phase 2: Foundational (RewardsApi `redeem` `X-Idempotency-Key` per `rewardId` + `GetRewards` 4 métricas, `PlayerRewardsStore` `redeem()` `rxMethod` `isRedeemable`, `Reward` `RowVersion` + `Game.ConsumePoints` ledger)
3. Complete Phase 3: US1 (Wallet+Catalog `Available/Required/Remaining/Status` sin cálculo cliente, `Te faltan 700` fallback, `Agotada` badge, `aria-live polite`)
4. **STOP and VALIDATE**: `GET /rewards` muestra 4 métricas sin cálculo cliente 0% SC-001, `RewardsCatalogComponent` 4 métricas `aria-live polite` passes, `axe` `group` passes, quickstart V1 SC-001
5. Deploy/demo MVP (Wallet+Catalog works end-to-end)

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 → Test independently → Demo MVP (Wallet+Catalog autoritativo)
3. Add US2 → Test independently → Demo (Detail+Canjear 2 pasos `X-Idempotency-Key` per `rewardId` + Confirmation idempotente `Remaining 400` `Reference`)
4. Add US3 → Test independently → Demo (Redemption History `RequestedAt` desc paginado)
5. Add US4 → Test independently → Demo (Consolation `APPROVED` `points 0` badge diferenciado)
6. Polish → final validation V1-V7, SC-001..008

### Parallel Team Strategy

With 3 developers after Foundational:

1. Team completes Setup + Foundational together
2. Once Foundational done:
   - Developer A: US1 (Wallet+Catalog `RewardsDisplay` + `RewardsCatalogComponent` skeleton)
   - Developer B: US2 (Detail 4 métricas + 2 pasos `X-Idempotency-Key` per `rewardId` + Confirmation) + US3 (History `RequestedAt` desc paginado)
   - Developer C: US4 (Consolation badge diferenciado `var(--color-info)` + `GetPlayerRedemptions` `isConsolation`)
3. Polish by A/B/C after stories done

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability (US1..US4)
- Each user story independently completable and testable per spec Independent Test + quickstart V1-V5
- Verify tests fail before implementing (red → green)
- Commit after each task or logical group; push to branch `036-player-rewards`
- Avoid: vague tasks, same file conflicts, cross-story state leakage (e.g., `Remaining` calculado cliente en vez de derivado de `Available` autoritativo)
- Do not edit `.aspire/modules` — wire via AppHost.cs only
- Tokens: `design-system/tokens` + `data-theme="player"` per SPEC-016 cinematic premium + `prefers-reduced-motion`

