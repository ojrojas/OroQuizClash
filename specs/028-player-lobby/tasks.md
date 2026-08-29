# Tasks: Player Lobby (028)

**Input**: Design documents from `/specs/028-player-lobby/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md
**Branch**: `028-player-lobby` | **Constitution**: v1.1.0 (I-VI, A-J)

**Organization**: Tasks grouped by user story — each story independently testable and deliverable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify existing Player SPA (027) + modular monolith and prepare lobbying scaffolding

- [x] T001 Verify existing project structure per `specs/028-player-lobby/plan.md` (`src/Player/QuizArena.Player` Angular 22 standalone, `src/OroQuizClash.Domain`, `Application`, `Infrastructure`, `Api`, `OroQuizClash.AppHost` in `OroQuizClash.slnx`) and `dotnet build OroQuizClash.slnx` passes
- [x] T002 Verify Player SPA dependencies `src/Player/QuizArena.Player/package.json` (`@angular/core 22`, `@ngrx/signals`, `angular-auth-oidc-client`, `@microsoft/signalr`, `rxjs`) and `design-system/tokens/design-tokens.css` is imported via `angular.json` styles with `data-theme="player"` per SPEC-016
- [x] T003 Verify `BuildingBlocks` dependencies and `Directory.Packages.props` central versions for `net10.0` (`LangVersion latest`, `Nullable enable`) and `src/BuildingBlocks/` references per plan.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared lobby infrastructure MUST complete before ANY user story — paginated Available Games query, interceptors, base UI states, prize proyección

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Extend `GamesApi` client in `src/Player/QuizArena.Player/src/app/features/shared/games.api.ts` to add `getGames(status, page, pageSize, search)` (`GET /api/games?status=WAITING_FOR_PLAYERS&page=&pageSize=`) and `getGame(gameId)` (`GET /api/games/{id}`) methods with `X-Correlation-Id` via interceptors per `contracts/api-contracts.md` §1-2 (verify existing `joinGame/getMyState` present)
- [x] T005 [P] Verify `GameFilterSpecification` paginated query in `src/OroQuizClash.Application/Features/Games/GetGame.cs` (`Where Status==WAITING_FOR_PLAYERS`, `OrderBy CreatedAt desc`, `Include Players`, `Skip/Take`, `AsNoTracking`, `HasIndex Status/CreatedAt` in `src/OroQuizClash.Infrastructure/Persistence/Configurations/GameTypeConfiguration.cs`) for lobby pagination per research.md R1
- [x] T006 [P] Verify interceptors in `src/Player/QuizArena.Player/src/app/core/interceptors/` — `correlation-id.interceptor.ts` (`X-Correlation-Id` UUID), `auth.interceptor.ts` (`Authorization: Bearer` only `apiUrl` secureRoutes), `error.interceptor.ts` (RFC 7807 `ProblemDetails` mapping, 401 silentRenew, 429 RetryAfter) per `contracts/api-contracts.md` Interceptors
- [x] T007 [P] Verify shared UI states in `src/Player/QuizArena.Player/src/app/shared/ui/` — `loading-skeleton.component.ts` (role=status aria-live polite), `empty-state.component.ts`, `error-state.component.ts` (CorrelationId/TraceId display, Retry, 44px targets) per `contracts/ui-contracts.md` States and FR-012
- [x] T008 Verify `JoinGame` slice `POST /api/games/{id}/players` with `X-Idempotency-Key` header and `UNIQUE (GameId,UserId)` + `RowVersion` in `src/OroQuizClash.Infrastructure/Persistence/Configurations/GamePlayerTypeConfiguration.cs` (already from 027, verify idempotent 200 on duplicate) per research.md R2
- [x] T009 Verify `GetGame` detail slice `GET /api/games/{id}` projection with 8 fields + extended (`TimeLimitPerQuestion, PointsPerRound, WithdrawalPolicy, LossPolicy, PlayersList`) and `Prize` resolution via `IRepository<Reward,RewardId>` optional fallback "—" in `src/OroQuizClash.Application/Features/Games/GetGame.cs` per research.md R3

**Checkpoint**: Foundation ready — `dotnet build` passes, lobby can call `GET /games?status=WAITING_FOR_PLAYERS` paginated and `POST /players` idempotent, UI states ready

---

## Phase 3: User Story 1 — Descubrir partidas disponibles en el lobby (Priority: P1) 🎯 MVP

**Goal**: Lobby muestra Available Games con 8 campos (Game Name, Category, Difficulty, Number of Rounds, Players, Start Time, Prize, Status) paginado, orden StartTime desc, estados Loading/Empty/Error/Ready, responsive table/cards sin scroll, WCAG AA

**Independent Test**: Con 3 juegos `WAITING_FOR_PLAYERS` y 2 `IN_PROGRESS/FINISHED`, abrir `/player/lobby` → solo 3 visibles con 8 columnas correctas, orden `CreatedAt desc`, `GET /api/games?status=WAITING_FOR_PLAYERS&page=1&pageSize=20` 200 con `totalCount=3`; Empty cuando 0; paginación con 25 juegos (pageSize 20 → 20 + 5) sin pérdida (spec US1, quickstart V1)

### Tests for User Story 1

- [x] T010 [P] [US1] Contract test for `GET /api/games?status=WAITING_FOR_PLAYERS` paginated 8 fields in `tests/OroQuizClash.Api.Tests/Contracts/LobbyAvailableGamesContractTests.cs` (WebApplicationFactory, JWT `PLAYER`, assert `status==WAITING_FOR_PLAYERS` 100% SC-001, 8 campos per item SC-002, `Prize` placeholder "—" when null)
- [x] T011 [P] [US1] Lobby store unit test for paginated load in `src/Player/QuizArena.Player/src/app/features/lobby/lobby.store.spec.ts` (TestBed provide store with `GamesApi` mock, verify `load()` → `games` 8 fields, `totalCount`, `page`, Empty when 0, Table vs Cards responsive)
- [x] T012 [P] [US1] API integration test for Available Games filtering + pagination in `src/Player/QuizArena.Player/tests/integration/lobby-available-games.spec.ts` (mock `getGames` WAITING vs FINISHED, assert only WAITING rendered, pagination pageSize 20)

### Implementation for User Story 1

- [x] T013 [P] [US1] Create lobby state management `LobbyStore` or extend `PlayerGameStore` with `LobbyStore` in `src/Player/QuizArena.Player/src/app/features/lobby/lobby.store.ts` (`signalStore withState { games: GameSummary[], totalCount, page, pageSize, isLoading, error } withComputed numberOfRoundsDisplay/prizeDisplay, withMethods load rxMethod via `GamesApi.getGames` + tapResponse patchState`) per `data-model.md` `PaginatedGames` and `contracts/ui-contracts.md` (scoped per lobby)
- [x] T014 [US1] Implement Available Games table/cards UI in `src/Player/QuizArena.Player/src/app/features/lobby/lobby.component.ts` (imports `LoadingSkeletonComponent`, `EmptyStateComponent`, `ErrorStateComponent`, `CommonModule`, template table ≥1024px 8 cols `<th>` + cards ≤768px stacked same 8 fields `Game Name/Category/Difficulty/NumberOfRounds/Players/StartTime/Prize/Status` with `aria-live="polite"`, `players.display "current/max"`, `prize "—"` fallback, pagination component with `totalCount/page/pageSize`, `Join/View` buttons `min-height 44px`, responsive `design-system/tokens` `data-theme="player"`, per `contracts/ui-contracts.md` Layout) (depends on T013)
- [x] T015 [US1] Wire lobby route and data fetch in `src/Player/QuizArena.Player/src/app/app.routes.ts` (verify `/player/lobby` or `/lobby` route `canActivate: [authGuard, mustChangePasswordGuard]` lazy `loadComponent: LobbyComponent` already from 027, add `providers: [LobbyStore]` if store per component) and `src/Player/QuizArena.Player/src/app/features/lobby/lobby.component.ts` `OnInit load(1)` + `Refresh` button → reload (verify no existing lobby conflict)

**Checkpoint**: US1 fully functional — `dotnet test --filter Lobby` + `npm test` lobby spec passes, `/player/lobby` shows only WAITING with 8 campos <500ms p95, Empty/Error/Loading/Pagination/RWCAG AA green (quickstart V1, SC-001/SC-002/SC-008)

---

## Phase 4: User Story 2 — Unirse a una partida desde el lobby (Priority: P1)

**Goal**: `Join Game` por fila solo si `Players<Max && WAITING`, idempotente con `X-Idempotency-Key` `sessionStorage` per `gameId`, server revalida `WAITING + RowVersion + UNIQUE`, 200 → redirect `/player/game/:id`, 400/409 → `ProblemDetails` with `CorrelationId`, 401 → OIDC login retry

**Independent Test**: Click `Join Game` on available game → `POST /api/games/{id}/players` with `X-Idempotency-Key` → 200 `GameSession ACTIVE` → redirect; double-click same key → same 200 no duplicate `GamePlayer`; second player join same game succeeds without leaking score; full game → `409 GameFull` friendly; unauth → redirect login (US2, quickstart V2, SC-003/SC-004/SC-005)

### Tests for User Story 2

- [x] T016 [P] [US2] Contract test for `POST /api/games/{id}/players` idempotent in `tests/OroQuizClash.Api.Tests/Contracts/LobbyJoinGameContractTests.cs` (JWT `PLAYER`, first Join 200 ACTIVE, second same `X-Idempotency-Key` 200 same `GameSessionId` count unchanged, `GameFull` 409, `GameNotWaitingForPlayers` 400, `PlayerIdentityMismatch` 403 when sub mismatch)
- [x] T017 [P] [US2] Lobby Join unit test for idempotency guard in `src/Player/QuizArena.Player/src/app/features/lobby/lobby.component.spec.ts` (TestBed LobbyComponent + LobbyStore mock `GamesApi`, simulate `join(gameId)` → verify `sessionStorage idemp-join-{gameId}` UUID persisted and sent as `X-Idempotency-Key`, button disabled when `current>=max` or `status!=WAITING`)

### Implementation for User Story 2

- [x] T018 [US2] Implement Join Game handler in `src/Player/QuizArena.Player/src/app/features/lobby/lobby.component.ts` (method `join(gameId: string)` → `const key = sessionStorage.getItem('idemp-join-'+gameId) ?? crypto.randomUUID(); sessionStorage.setItem(...)` → `GamesApi.joinGame(gameId, key)` `tapResponse` → on success `router.navigate(['/player/game', gameId])` + Players count optimistic update, on error map `ProblemDetails` `code==GameFull/GameNotWaitingForPlayers` → show `ErrorState` with `detail` + `CorrelationId` + CTA volver lobby, `code==PlayerIdentityMismatch` → audit log) per `contracts/api-contracts.md` §3 (depends on T014)
- [x] T019 [US2] Enforce server-side Join validation reuse in `src/OroQuizClash.Application/Features/Games/JoinGame.cs` (verify `JoinGameHandler` checks `GameStatus==WAITING_FOR_PLAYERS` else `GameNotWaitingForPlayers`, `Players.Count >= MaxPlayers` else `GameFull`, `UNIQUE (GameId,UserId)` already via `Game.JoinPlayer` + `GamePlayerTypeConfiguration` → idempotent 200, `PlayerId = sub` from `GameClaims.GetSub(http.User)` not body, `RowVersion` concurrency) — no new slice needed, just verify already covers SC-004/SC-005

**Checkpoint**: US1+US2 work — lobby discovery plus Join <1s 95%, idempotent double-click no duplicate 100% SC-004, full/waiting error 100% SC-005, quickstart V2 green

---

## Phase 5: User Story 3 — Ver información detallada de la partida (Priority: P2)

**Goal**: `View Game Information` por fila abre detalle modal/página con 8 campos + extendidos (`TimeLimit, PointsPerRound, WithdrawalPolicy, LossPolicy, PlayersList`, `StartTime` local) consistente con `GET /api/games/{id}`, server truth on refresh, 404 with CorrelationId

**Independent Test**: Click View on row → detail shows 8+extended matching `GET /games/{id}` JSON `StartTime` local; while open change game to IN_PROGRESS → reopen shows updated Status; manipulated id → 404 `GameNotFound` ErrorState with CorrelationId (US3, quickstart V3, SC-007)

### Tests for User Story 3

- [x] T020 [P] [US3] Contract test for `GET /api/games/{id}` detail in `tests/OroQuizClash.Api.Tests/Contracts/LobbyGameDetailContractTests.cs` (assert 8 campos + extended `timeLimitPerQuestionSeconds/pointsPerRound/withdrawalPolicy/lossPolicy` present, no `Answer/Score` leak FR-013, 404 GameNotFound with ProblemDetails)

### Implementation for User Story 3

- [x] T021 [P] [US3] Create GameDetail UI in `src/Player/QuizArena.Player/src/app/features/lobby/game-detail.component.ts` (standalone, `input gameId`, `OnInit` → `GamesApi.getGame(gameId)` `tapResponse`, template modal/page with 8 fields + extended config `TimeLimit/Points/Policies` + `PlayersList` names, `StartTime` `date:'short'` local, `Prize` "—" placeholder, `LoadingSkeleton`/`ErrorState` with CorrelationId, `Close` button 44px, `data-theme="player"`, aria attributes) per `contracts/ui-contracts.md` Actions
- [x] T022 [US3] Wire View action in `src/Player/QuizArena.Player/src/app/features/lobby/lobby.component.ts` (method `view(gameId)` → `router.navigate` detail or `MatDialog.open(GameDetailComponent, {data: gameId})` or `dialog` via `inject`, ensure no stale cache: re-fetch on open, verify server truth) (depends on T021)

**Checkpoint**: View Information 100% consistent SC-007, quickstart V3 green

---

## Phase 6: User Story 4 — Salir del lobby sin participar (Priority: P2)

**Goal**: `Leave Lobby` navigates away without API write, no `GameSession` mutation, no Withdraw, keyboard/aria accessible, preserves OIDC session

**Independent Test**: From lobby not joined click Leave → navigated to `/` with no `fetch` to `/players` POST, OIDC session preserved; after Join then back to lobby Leave → still ACTIVE in previous game `GET /players/me` (US4, quickstart V4, SC-006)

### Implementation for User Story 4

- [x] T023 [US4] Implement Leave Lobby action in `src/Player/QuizArena.Player/src/app/features/lobby/lobby.component.ts` (template `<button (click)="leave()" aria-label="Salir del lobby" style="min-height:44px">Leave Lobby</button>` method `leave()` → `router.navigate(['/'])` or `location.back()` without any `HttpClient` call, no `GamesApi` write, verify no `fetch` triggered) per FR-007/FR-008
- [x] T024 [US4] Verify Leave does not trigger Withdraw in `src/Player/QuizArena.Player/src/app/features/lobby/lobby.component.spec.ts` (unit test: spy `GamesApi` no call to `withdraw`, spy `Router` navigates, second spec: after mock Joined ACTIVE, Leave → `getMyState` still ACTIVE)

**Checkpoint**: Leave Lobby instant <500ms 100% SC-006, no side-effect verified

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Hardening, WCAG, observability, docs, quickstart validation

- [x] T025 [P] Add ProblemDetails mapping test for lobby errors in `tests/OroQuizClash.Api.Tests/Contracts/LobbyErrorsMappingTests.cs` (assert `GameFull 409`, `GameNotWaitingForPlayers 400`, `GameNotFound 404`, `PlayerIdentityMismatch 403` map via `GlobalExceptionHandler` → `Result.ToHttpResult()` RFC 7807 with `CorrelationId/TraceId`)
- [x] T026 [P] Verify responsive & WCAG `data-theme="player"` in `src/Player/QuizArena.Player/src/app/features/lobby/lobby.component.ts` and `game-detail.component.ts` (table ≥1024px / cards ≤768px 375px same 8 fields, no horizontal scroll, targets ≥44px, contrast via `design-tokens.css`, `aria-live="polite"` list `assertive` error, focus `outline` visible, Tab/Enter keyboard, axe pass) per `contracts/ui-contracts.md` A11y/Tokens
- [x] T027 [P] Verify `X-Correlation-Id` propagation test in `src/Player/QuizArena.Player/tests/integration/lobby-correlation.spec.ts` (mock `getGames`/`joinGame` → assert header `X-Correlation-Id` UUID sent, `ErrorState` displays `CorrelationId/TraceId`)
- [x] T028 [P] Update design-system reference in `src/Player/QuizArena.Player/README.md` (add `Player Lobby` section: Available Games 8 cols lobby.component, pagination, Join `X-Idempotency-Key`, View detail, Leave, `data-theme="player"`)
- [x] T029 [P] Run quickstart validation in `specs/028-player-lobby/quickstart.md` (execute V1-V6: discover Available 8 campos paginación, Join idempotente, View detail, Leave, 401/CorrelationId, axe 375-1536, fix gaps if any)
- [x] T030 Add architecture test for lobby isolation in `tests/OroQuizClash.Architecture.Tests/LobbyIsolationTests.cs` (verify Domain not references Angular/Player lobby, BuildingBlocks constraints, `JoinGame` uses `sub` not body, no client score trust)
- [x] T031 Security hardening verify in `src/Player/QuizArena.Player/src/app/core/interceptors/error.interceptor.ts` (hide sensitive details, propagate `traceId`, handle 429 `GamePlayLimiter` RetryAfter already from SPEC-027)
- [x] T032 Final `dotnet build OroQuizClash.slnx && dotnet test` green and `npm run lint` with `@ngrx/eslint-plugin` clean, update `specs/028-player-lobby/spec.md` Status to `Ready for Review`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately (verifies 027 SPA + monolith)
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (paginated `GetGames`, interceptors, UI states, `JoinGame` idempotence verified)
- **User Stories (Phase 3+)**: All depend on Foundational
  - **US1 (P1) Discover Available Games**: No other story dependency — MVP (8 campos paginado)
  - **US2 (P1) Join Game**: Depends on US1 list (needs `gameId` from lobby) but independently testable with mocked `GET /games`; adds idempotent Join
  - **US3 (P2) View Information**: Depends on `GetGame` slice (reuse), independently testable via direct `GET /games/{id}`
  - **US4 (P2) Leave Lobby**: Depends only on lobby route, independently testable (no API)
- **Polish (Final)**: Depends on all desired stories (US1+US2 for MVP lobby, US3+US4 for completeness)

### User Story Dependencies

- **US1 (P1)**: After Foundational — no deps
- **US2 (P1)**: After Foundational — integrates with US1's `Game.GameId` but testable with mocked games
- **US3 (P2)**: After Foundational — reuses `GetGame` slice, testable independently
- **US4 (P2)**: After Foundational — navigation only, no API deps

### Within Each User Story

- Tests (if included) written before implementation (T010 before T013, T016 before T018)
- Lobby state/store before component
- Component before integration wiring
- Core implementation before error states

### Parallel Opportunities

- Phase 1: T002 + T003 parallel (different files)
- Phase 2: T005 + T006 + T007 + T008 + T009 parallel (different files)
- Phase 3: T010 + T011 + T012 parallel (contract/store/integration tests different files)
- Phase 4: T016 + T017 parallel (contract test vs component spec)
- Phase 5: T020 parallel with T021 (contract test vs UI component different files)
- Phase 7: T025 + T026 + T027 + T028 + T029 parallel (different files)
- Different stories can start in parallel after Foundational if staffed (US2 needs only `gameId` interface)

### Parallel Example: User Story 1 (Discover)

```bash
# Launch tests for US1 together:
Task T010: Contract test in tests/OroQuizClash.Api.Tests/Contracts/LobbyAvailableGamesContractTests.cs
Task T011: Lobby store unit test in src/Player/QuizArena.Player/src/app/features/lobby/lobby.store.spec.ts
Task T012: Integration test in src/Player/QuizArena.Player/tests/integration/lobby-available-games.spec.ts

# Launch store + UI together after tests:
Task T013: LobbyStore in src/Player/QuizArena.Player/src/app/features/lobby/lobby.store.ts
Task T015: wiring in app.routes.ts provides LobbyStore
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (verify SPA/monolith)
2. Complete Phase 2: Foundational (paginated `GetGames`, interceptors, UI states, `JoinGame` idempotence verified)
3. Complete Phase 3: US1 (Available Games 8 campos paginado)
4. **STOP and VALIDATE**: `GET /api/games?status=WAITING_FOR_PLAYERS` shows only WAITING with 8 campos, Empty/pagination, WCAG, quickstart V1 SC-001/SC-002
5. Deploy/demo MVP (discover works end-to-end)

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 → Test independently → Demo MVP (Available Games)
3. Add US2 → Test independently → Demo (Join idempotente redirects to game)
4. Add US3 → Test independently → Demo (View detail)
5. Add US4 → Test independently → Demo (Leave navigation)
6. Polish → final validation V1-V6, SC-001..009

### Parallel Team Strategy

With 3 developers after Foundational:

1. Team completes Setup + Foundational together
2. Once Foundational done:
   - Developer A: US1 (Available Games table/cards + pagination)
   - Developer B: US2 (Join Game idempotence + redirect)
   - Developer C: US3 (View Information detail) + US4 (Leave) (no API conflict)
3. Polish by A/B/C after stories done

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability (US1..US4)
- Each user story independently completable and testable per spec Independent Test + quickstart V1-V4
- Verify tests fail before implementing (red → green)
- Commit after each task or logical group; push to branch `028-player-lobby`
- Avoid: vague tasks, same file conflicts, cross-story state leakage (e.g., lobby mutating game state)
- Do not edit `.aspire/modules` — wire via AppHost.cs only
- Tokens: memory + `sessionStorage` per `gameId` (never `localStorage`) per FR-004/H

