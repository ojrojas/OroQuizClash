# Tasks: Multiplayer

**Input**: Design documents from `/specs/011-multiplayer/`

**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/multiplayer.openapi.yaml, contracts/gamehub.md, quickstart.md

**Tests**: Included — automated tests are MANDATORY per constitution Testing Strategy (Domain Unit Tests, Application Tests, Integration Tests, API Tests, Architecture Tests).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4, US5)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Baseline verification before any change. No new projects/packages are needed (SignalR ships in the ASP.NET Core shared framework already referenced).

- [X] T001 Verify solution builds and existing tests pass: run `dotnet build OroQuizClash.slnx` and `dotnet test tests/OroQuizClash.Domain.Tests/`, `dotnet test tests/OroQuizClash.Application.Tests/`, `dotnet test tests/OroQuizClash.Infrastructure.Tests/`, `dotnet test tests/OroQuizClash.Architecture.Tests/` from repo root (record baseline counts) — baseline: build 0 errors; Domain 245 ✅, Application 49 ✅, Architecture 41 ✅; Infrastructure had 20 pre-existing failures (broken EF field mapping in `GameTypeConfiguration` + stale 1–2 char names in `CategoryFilterSpecificationTests`) — fixed within T005 scope, Infrastructure 23 ✅

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Extend the `Game` aggregate with the per-player participation state shared by multiple stories (`CurrentRoundNumber` serves US1 and US4/`CurrentLevel`; `GetPlayerAnswerState` serves US1; `PlayerIdentityMismatch` error serves US3) plus its EF mapping. Per research.md R1–R3: explicit `CurrentRoundNumber` field, derived `AnswerState`, aggregate-level `RowVersion` as concurrency token.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T002 Extend `GamePlayer` entity: add `CurrentRoundNumber` (int, default 0, private set), `internal void AdvanceToRound(int roundNumber)` (sets `CurrentRoundNumber`, only meaningful for active players), and freeze semantics — `MarkWithdrawn()`/`MarkEliminated()` keep the last value (no further advances once `ParticipationStatus` is terminal) in `src/OroQuizClash.Domain/Games/GamePlayer.cs`
- [X] T003 Extend `Game` aggregate: (1) in `StartRound(Guid questionId, int difficulty, int? timeLimitOverride)` advance `CurrentRoundNumber` of every `Active` player to the new `RoundNumber` after the round is created; (2) add `public AnswerStatus GetPlayerAnswerState(Guid playerId)` returning the `Answer.Status` of the player's `Answer` for `CurrentRound`, or `AnswerStatus.NotAnswered` when none exists (or no active round) in `src/OroQuizClash.Domain/Games/Game.cs`
- [X] T004 [P] Add error `PlayerIdentityMismatch` (`Error.Forbidden("PlayerIdentityMismatch", "Authenticated user cannot act on behalf of another player.")`) to `src/OroQuizClash.Domain/Shared/Errors/GameErrors.cs`
- [X] T005 [P] Map the new column in `GamePlayerTypeConfiguration`: `CurrentRoundNumber` (int, required, default 0) in `src/OroQuizClash.Infrastructure/Persistence/Configurations/GamePlayerTypeConfiguration.cs`
- [X] T006 [P] Domain unit tests for participation state: `CurrentRoundNumber` starts at 0, advances for active players on each `StartRound`, does NOT advance for withdrawn/eliminated players (frozen at last reached round), never decreases; `GetPlayerAnswerState` returns `NOT_ANSWERED` before submitting, `EVALUATED` after a correct/incorrect evaluated answer, `EXPIRED` after a late submission, and `NOT_ANSWERED` when no active round — in `tests/OroQuizClash.Domain.Tests/Games/MultiplayerParticipationTests.cs`
- [X] T007 Verify foundational changes: `dotnet build OroQuizClash.slnx` compiles with 0 errors and `dotnet test tests/OroQuizClash.Domain.Tests/ --filter "FullyQualifiedName~Multiplayer"` plus the full domain suite pass

**Checkpoint**: Foundation ready — user story implementation can now begin

---

## Phase 3: User Story 1 - Participación concurrente con estado individual aislado (Priority: P1) 🎯 MVP

**Goal**: Cada jugador tiene su propio estado de participación (`PlayerId`, `GameId`, `Status`, `Score`, `CurrentRound`, `AnswerState`) que evoluciona independientemente, consultable en cualquier momento vía `GetPlayerState` (FR-001, FR-009, FR-010, FR-015).

**Independent Test**: Unir 3 jugadores, iniciar juego y ronda 1; verificar que el estado de cada jugador (`GET /api/games/{gameId}/players/{playerId}/state`) muestra `status=ACTIVE`, `currentRound=1`, `answerState` según su propia respuesta (uno correcto, uno incorrecto, uno sin responder → tres estados distintos), y que tras retirar a uno su `currentRound` queda congelado mientras los demás avanzan a la ronda 2.

### Implementation for User Story 1

> **NOTE**: `GetPlayerState` es un slice nuevo (sus tests referencian tipos nuevos), por lo que la implementación precede a los tests; el comportamiento de dominio que cubre ya fue testeado en T006.

- [X] T008 [US1] Implement `GetPlayerState` vertical slice: `GetPlayerStateQuery(GameId, PlayerId)` (`IQuery<Result<PlayerStateResponse>>`), `PlayerStateResponse(GameId, PlayerId, DisplayName, Status, CurrentPoints, SecuredPoints, RoundPoints, PotentialPoints, TotalPoints, CurrentRound, AnswerState, CorrectAnswers, IncorrectAnswers, ExitedAt)`, handler loading `GameByIdWithAnswersSpecification` (404 `GameNotFound`/`PlayerNotInGame` when missing), and endpoint `GET /api/games/{gameId}/players/{playerId}/state` with `RequireAuthorization()`; visibility: JWT `sub == playerId` or organizer role (ADMIN/GAME_MANAGER), otherwise `GameErrors.PlayerIdentityMismatch` (resolve `sub` in the endpoint as in `JoinGameEndpoint`) in `src/OroQuizClash.Application/Features/Games/GetPlayerState.cs`

### Tests for User Story 1

- [X] T009 [US1] Application tests for `GetPlayerStateHandler`: returns full state (status, score breakdown, `CurrentRound`, `AnswerState`, correct/incorrect counters) for a player mid-game; withdrawn player shows frozen `CurrentRound` + `ExitedAt`; unknown game → `GameNotFound`; unknown player → `PlayerNotInGame` — using NSubstitute repository mocks per existing handler-test conventions in `tests/OroQuizClash.Application.Tests/Features/Games/GetPlayerStateHandlerTests.cs`
- [X] T010 [US1] Run US1 validation: `dotnet test tests/OroQuizClash.Domain.Tests/ --filter "FullyQualifiedName~Multiplayer"` and `dotnet test tests/OroQuizClash.Application.Tests/ --filter "FullyQualifiedName~GetPlayerState"` all green

**Checkpoint**: User Story 1 fully functional — per-player state exists, evolves independently, and is queryable (MVP)

---

## Phase 4: User Story 2 - Respuestas simultáneas sin interferencia (Priority: P1)

**Goal**: A, B y C pueden responder simultáneamente la misma ronda; cada envío se evalúa de forma independiente y atómica, sin actualizaciones perdidas ni degradación mutua (FR-005, SC-001).

**Independent Test**: Con una ronda activa y 2–3 jugadores, enviar respuestas concurrentes (dos contextos EF guardando en secuencia sobre el mismo juego); verificar que todos los `Answer` + `PointTransaction` se persisten correctamente y que un conflicto de versión se reporta como `ConcurrencyConflict` recuperable.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation** (reemplazan el stub actual de `GameConcurrencyTests`)

- [X] T011 [P] [US2] Infrastructure concurrency tests (EF Core Sqlite): (a) two DbContext instances load the same game, each submits an answer for a DIFFERENT player in the active round, both save — all `Answer` rows and their `PointTransaction` rows persist with correct per-player balances (no lost/duplicated updates); (b) verify both players' `Score.CurrentPoints` match their ledger sums — replacing the current stub in `tests/OroQuizClash.Infrastructure.Tests/Persistence/GameConcurrencyTests.cs`

### Implementation for User Story 2

- [X] T012 [P] [US2] Wrap `unitOfWork.SaveChangesAsync` in `SubmitAnswerHandler` with `try/catch (DbUpdateConcurrencyException)` returning `Result.Failure(GameErrors.ConcurrencyConflict)` (409), matching the pattern already used in `JoinGameHandler`/`StartGameHandler`, in `src/OroQuizClash.Application/Features/Games/SubmitAnswer.cs`
- [X] T013 [US2] Run US2 validation: `dotnet test tests/OroQuizClash.Infrastructure.Tests/ --filter "FullyQualifiedName~GameConcurrency"` and `dotnet test tests/OroQuizClash.Application.Tests/` green

**Checkpoint**: User Stories 1 AND 2 work independently — simultaneous answers are processed without interference

---

## Phase 5: User Story 3 - Aislamiento entre jugadores (Priority: P1)

**Goal**: Ningún jugador puede modificar ni suplantar el estado de otro: el `PlayerId` de los comandos de jugador proviene del claim JWT `sub`, y actuar sobre otro jugador retorna 403 `PlayerIdentityMismatch` (FR-003, FR-004, SC-003). Corrige el bug actual de `SubmitAnswerHandler` (`playerId = Guid.Empty`).

**Independent Test**: Como jugador A, enviar una respuesta (funciona, se evalúa para A); intentar `withdraw` con el `playerId` de B → 403 `PlayerIdentityMismatch` y el estado de B permanece intacto; como organizador, retirar a B funciona.

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T014 [P] [US3] Application tests for player identity enforcement: `SubmitAnswerHandler` uses the command's authenticated `PlayerId` (never `Guid.Empty`); a command where the authenticated player is not in the game fails `PlayerNotInGame`; `WithdrawPlayerHandler` with `PlayerId != authenticated sub` (non-organizer) fails `PlayerIdentityMismatch`; organizer bypasses the check — NSubstitute mocks per existing conventions in `tests/OroQuizClash.Application.Tests/Features/Games/SubmitAnswerIdentityTests.cs`

### Implementation for User Story 3

- [X] T015 [US3] Wire JWT identity into `SubmitAnswer`: endpoint resolves `sub` from `HttpContext.User` (pattern from `JoinGameEndpoint`) and sets it as the command's player id — remove the `Guid.Empty` placeholder and fallback comment from the handler; the handler must use ONLY the authenticated player id (body cannot override it) in `src/OroQuizClash.Application/Features/Games/SubmitAnswer.cs` (must run after T012 — same file)
- [X] T016 [P] [US3] Enforce identity in `WithdrawPlayer`: endpoint resolves JWT `sub`; if the body-supplied `PlayerId` differs from `sub` and the caller is not ADMIN/GAME_MANAGER, return `GameErrors.PlayerIdentityMismatch` (403); default `PlayerId` to `sub` when body is empty; also wrap `SaveChangesAsync` in `try/catch (DbUpdateConcurrencyException)` → `ConcurrencyConflict` in `src/OroQuizClash.Application/Features/Games/WithdrawPlayer.cs`
- [X] T017 [US3] Run US3 validation: `dotnet test tests/OroQuizClash.Application.Tests/ --filter "FullyQualifiedName~SubmitAnswerIdentity"` green plus full application suite

**Checkpoint**: User Stories 1, 2 AND 3 work independently — cross-player mutation attempts are rejected and audited

---

## Phase 6: User Story 4 - Leaderboard en vivo del juego (Priority: P2)

**Goal**: Leaderboard por juego con `Rank`, `Player`, `Points`, `CorrectAnswers`, `CurrentLevel`, `Status`, orden determinista (Points desc → CorrectAnswers desc → consecución más temprana → join order), solo datos evaluados, estable tras `FINISHED` (FR-011, FR-012); notificaciones server-driven `PlayerJoined`/`ScoreUpdated`/`LeaderboardUpdated`/`PlayerStatusChanged` vía SignalR como hints best-effort (FR-014).

**Independent Test**: Tras varias rondas evaluadas con puntajes distintos y un empate, `GET /api/games/{gameId}/leaderboard` retorna las 6 columnas por jugador en orden determinista (verificable en consultas repetidas); un cliente SignalR conectado a `/hubs/game` recibe `LeaderboardUpdated` tras cada evaluación.

### Implementation for User Story 4

> **NOTE**: La extensión de `GetLeaderboard` precede a sus tests (los tests referencian los nuevos campos del response).

- [X] T018 [US4] Extend `GetLeaderboard`: add `CorrectAnswers` (count of the player's `Answer` with `Correct == true`), `CurrentLevel` (difficulty of the round whose `RoundNumber == player.CurrentRoundNumber`; null when 0), `Status` (`ParticipationStatus.Name`) to `LeaderboardEntryResponse` (keep `SecuredPoints` for backward compatibility); deterministic ordering: `CurrentPoints` desc → `CorrectAnswers` desc → earliest achievement of current balance (min `CreatedAt` of the `PointTransaction` whose `ResultingBalance` equals `CurrentPoints`, fallback `JoinedAt`) → `JoinedAt` asc; assign `Rank` 1-based from that order in `src/OroQuizClash.Application/Features/Games/GetLeaderboard.cs`
- [X] T019 [US4] Application tests for leaderboard ranking: ordering by points desc; tie broken by `CorrectAnswers` desc; persistent tie broken by earliest balance achievement then join order; withdrawn/eliminated players keep their entry with final `Status` and score; `CurrentLevel` reflects the player's current/frozen round difficulty; identical game state produces identical ranking across repeated queries in `tests/OroQuizClash.Application.Tests/Features/Games/LeaderboardRankingTests.cs`
- [X] T020 [P] [US4] Define notifications port `IGameNotificationsBroadcaster` (plain C# interface, no framework types): `PlayerJoinedAsync(gameId, playerId, displayName)`, `ScoreUpdatedAsync(gameId, playerId, points, totalPoints, reason)`, `LeaderboardUpdatedAsync(gameId, entries)`, `PlayerStatusChangedAsync(gameId, playerId, status, finalScore)` — all `Task`, `CancellationToken`-aware, in `src/OroQuizClash.Application/Features/Games/IGameNotificationsBroadcaster.cs`
- [X] T021 [US4] Implement `GameEventBroadcastHandlers`: `IDomainEventHandler<>` implementations that map existing domain events to the broadcaster port — `PlayerJoinedDomainEvent` → `PlayerJoinedAsync`; `ScoreUpdatedDomainEvent` → `ScoreUpdatedAsync`; `AnswerEvaluatedDomainEvent` + `RoundCompletedDomainEvent` → `LeaderboardUpdatedAsync` (rebuild entries from the loaded game); `PlayerWithdrawnDomainEvent`/`PlayerEliminatedDomainEvent`/`GameFinishedDomainEvent` → `PlayerStatusChangedAsync`; handlers must be resilient (never throw into the SaveChanges transaction on broadcast failure — catch and log) in `src/OroQuizClash.Application/Features/Games/Notifications/GameEventBroadcastHandlers.cs`
- [X] T022 [P] [US4] Implement `GameHub : Hub` — broadcast-only: single client method `JoinGameGroup(Guid gameId)` that validates the caller (JWT `sub`) is a player of the game or ADMIN/GAME_MANAGER (via `IRepository<Game, GameId>`) and adds the connection to group `game-{gameId}`; no game-command methods; hub mapped with `RequireAuthorization()` in `src/OroQuizClash.Api/Hubs/GameHub.cs`
- [X] T023 [US4] Implement `SignalRGameNotificationsBroadcaster : IGameNotificationsBroadcaster` using `IHubContext<GameHub>`, sending to group `game-{gameId}` with the payload shapes defined in `specs/011-multiplayer/contracts/gamehub.md`, in `src/OroQuizClash.Api/Hubs/SignalRGameNotificationsBroadcaster.cs`
- [X] T024 [US4] Wire SignalR in the API host: `builder.Services.AddSignalR()`, register `IGameNotificationsBroadcaster` → `SignalRGameNotificationsBroadcaster`, and `app.MapHub<GameHub>("/hubs/game").RequireAuthorization()` in `src/OroQuizClash.Api/Program.cs`
- [X] T025 [P] [US4] Application tests for broadcast handlers: with an NSubstitute `IGameNotificationsBroadcaster`, verify each domain event produces the expected broadcaster call (payload fields) and that a broadcaster exception is swallowed (does not propagate) in `tests/OroQuizClash.Application.Tests/Features/Games/NotificationsBroadcastTests.cs`
- [X] T026 [P] [US4] API contract tests for the multiplayer read contract: `GET /api/games/{gameId}/leaderboard` response shape matches `LeaderboardResponse`/`LeaderboardEntryResponse` (rank, points, correctAnswers, currentLevel, status) and `GET /api/games/{gameId}/players/{playerId}/state` matches `PlayerStateResponse`, per `specs/011-multiplayer/contracts/multiplayer.openapi.yaml`, in `tests/OroQuizClash.Api.Tests/Contracts/MultiplayerContractTests.cs`
- [X] T027 [US4] Run US4 validation: `dotnet test tests/OroQuizClash.Application.Tests/ --filter "FullyQualifiedName~Leaderboard|FullyQualifiedName~NotificationsBroadcast"` and `dotnet test tests/OroQuizClash.Api.Tests/ --filter "FullyQualifiedName~Multiplayer"` green

**Checkpoint**: User Stories 1–4 work — deterministic live leaderboard with server-driven notifications

---

## Phase 7: User Story 5 - Protección de integridad: concurrencia, idempotencia y atomicidad (Priority: P2)

**Goal**: Bajo conflictos de versión, duplicados y fallos, el estado resultante es siempre consistente: el perdedor de un conflicto recibe 409 recuperable, los duplicados no duplican efectos, y no hay puntos sin transacción ni transacción sin evaluación (FR-006, FR-007, FR-008, SC-002, SC-006).

**Independent Test**: (a) Dos contextos mutan el mismo estado del mismo jugador → el segundo `SaveChanges` lanza `DbUpdateConcurrencyException` (mapeado a 409); (b) envío duplicado mismo jugador+ronda → un solo `Answer` y una sola `PointTransaction`; (c) todo `Answer` EVALUATED tiene exactamente una `PointTransaction` (índice único `(GameId, AnswerId)`).

### Tests for User Story 5

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T028 [US5] Extend infrastructure concurrency/idempotency tests (same file as T011 — run after it): (a) stale-version conflict — two contexts load the same game, both mutate the SAME player's state (e.g., withdraw + answer), second `SaveChangesAsync` throws `DbUpdateConcurrencyException`; (b) duplicate submission — same player+round submitted twice (domain path returns the existing answer; direct duplicate insert violates the unique index `(GameId, PlayerId, RoundId)`), resulting in exactly one `Answer` and one `PointTransaction`; (c) atomicity — every EVALUATED `Answer` has exactly one linked `PointTransaction` and balances equal ledger sums in `tests/OroQuizClash.Infrastructure.Tests/Persistence/GameConcurrencyTests.cs`

### Implementation for User Story 5

- [X] T029 [P] [US5] Wrap `unitOfWork.SaveChangesAsync` in `AdjustScoreHandler` with `try/catch (DbUpdateConcurrencyException)` → `Result.Failure(GameErrors.ConcurrencyConflict)`, matching the pattern from T012, in `src/OroQuizClash.Application/Features/Games/AdjustScore.cs`
- [X] T030 [US5] Run US5 validation: `dotnet test tests/OroQuizClash.Infrastructure.Tests/ --filter "FullyQualifiedName~GameConcurrency"` and full application suite green (SC-002/SC-006 verified)

**Checkpoint**: All five user stories are independently functional — integrity holds under concurrency, retries, and failures

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Architecture compliance, full-suite verification, and end-to-end validation

- [X] T031 [P] Add architecture tests for the multiplayer slice: Domain (`GamePlayer`/`Game`) references no AspNetCore/EF/RabbitMQ/MediatR/MassTransit/AutoMapper; `GamePlayer` exposes no public setters; Application's `IGameNotificationsBroadcaster` port has no SignalR/AspNetCore types; Api hub implements (not redefines) the port — following the existing per-feature pattern in `tests/OroQuizClash.Architecture.Tests/MultiplayerDependenciesTests.cs`
- [X] T032 Run the complete test suite: `dotnet test OroQuizClash.slnx` — all projects green (Domain, Application, Infrastructure, Api, Architecture)
- [X] T033 Run the quickstart end-to-end validation per `specs/011-multiplayer/quickstart.md` §2–§4 (aspire start, 3-player concurrent scenario, SC-001…SC-008 acceptance table)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Stories (Phases 3–7)**: All depend on Foundational phase completion
  - US1, US2, US3 (P1) can proceed in parallel with the file-sequencing caveat below
  - US4, US5 (P2) can also start after Foundational; US4 integrates US1's `CurrentRoundNumber` (already foundational)
- **Polish (Phase 8)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (P1)**: After Foundational — no dependencies on other stories (MVP)
- **US2 (P1)**: After Foundational — independent; touches `SubmitAnswer.cs`
- **US3 (P1)**: After Foundational — **T015 must run after T012** (both edit `src/OroQuizClash.Application/Features/Games/SubmitAnswer.cs`); otherwise independent of US1/US2
- **US4 (P2)**: After Foundational — consumes `CurrentRoundNumber`/`GetPlayerAnswerState` from Foundational; independently testable
- **US5 (P2)**: After Foundational — **T028 must run after T011** (same test file); otherwise independent

### Within Each User Story

- Tests written first and failing before implementation, EXCEPT where tests reference brand-new types (T008→T009, T018→T019: implementation first, noted in each phase)
- Domain before Application; Application before Api wiring
- Story complete (validation task green) before moving to next priority

### Parallel Opportunities

- T004, T005, T006 run in parallel after T002+T003 (different files)
- T011 ∥ T012 (US2 tests ∥ handler fix, different files)
- T014 ∥ T016 (US3 tests ∥ WithdrawPlayer fix); T015 sequential after T012
- T020 ∥ T022 ∥ T018 (port ∥ hub ∥ leaderboard, different files/projects); T025 ∥ T026 after their targets exist
- T029 ∥ T028 (different files)
- T031 parallel to any Phase 8 suite run

---

## Parallel Example: User Story 2

```bash
# Launch US2 tasks together (different files, no dependencies):
Task: "Infrastructure concurrency tests — simultaneous submissions in tests/OroQuizClash.Infrastructure.Tests/Persistence/GameConcurrencyTests.cs"
Task: "ConcurrencyConflict catch in SubmitAnswerHandler in src/OroQuizClash.Application/Features/Games/SubmitAnswer.cs"
```

## Parallel Example: User Story 4

```bash
# After Foundational, launch in parallel (different files/projects):
Task: "Extend GetLeaderboard in src/OroQuizClash.Application/Features/Games/GetLeaderboard.cs"
Task: "Define IGameNotificationsBroadcaster port in src/OroQuizClash.Application/Features/Games/IGameNotificationsBroadcaster.cs"
Task: "Implement GameHub in src/OroQuizClash.Api/Hubs/GameHub.cs"
# Then sequentially: handlers (T021) → broadcaster impl (T023) → Program.cs wiring (T024)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: Foundational (T002–T007) — CRITICAL, blocks all stories
3. Complete Phase 3: User Story 1 (T008–T010)
4. **STOP and VALIDATE**: per-player state queryable and evolving independently (quickstart §3 steps 2–3, 6)
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. Add US1 → per-player state (MVP)
3. Add US2 → simultaneous answers safe → demo
4. Add US3 → isolation/anti-cheat enforced → demo
5. Add US4 → deterministic live leaderboard + SignalR notifications → demo
6. Add US5 → integrity proofs under conflict/duplicates → demo
7. Polish: architecture tests, full suite, E2E quickstart

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: US1 (state) then US5 (integrity tests)
   - Developer B: US2 (concurrency) then US3 (isolation) — sequential on `SubmitAnswer.cs`
   - Developer C: US4 (leaderboard + notifications)
3. Stories integrate independently; only file-level sequencing constraints apply (T012→T015, T011→T028)

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Tests are mandatory (constitution Testing Strategy); concurrency/idempotency verified as integration tests with EF Core Sqlite (research.md R10)
- SignalR notifications are best-effort hints dispatched pre-commit inside the SaveChanges transaction — never the source of truth (research.md R7, contracts/gamehub.md)
- Commit after each task or logical group; stop at any checkpoint to validate the story independently
- Avoid: vague tasks, same-file conflicts (respect T012→T015 and T011→T028 ordering), cross-story dependencies that break independence
