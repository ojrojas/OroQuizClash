# Tasks: Game Lifecycle

**Input**: Design documents from `/specs/004-game-lifecycle/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/
**Feature Branch**: `004-game-lifecycle` | **Constitution**: v1.1.0 (I-VI)

**Organization**: Tasks grouped by user story — each story independently testable and deliverable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify existing modular monolith (001+002+003) and prepare Game Lifecycle scaffolding

- [x] T001 Verify existing project structure per `specs/004-game-lifecycle/plan.md` (`src/OroQuizClash.Domain`, `src/OroQuizClash.Application`, `src/OroQuizClash.Infrastructure`, `src/OroQuizClash.Api` in `OroQuizClash.slnx`) and `dotnet build OroQuizClash.slnx` passes
- [x] T002 Create Game Lifecycle folder structure `src/OroQuizClash.Domain/Games/{Rules,Events,ValueObjects}` (verify exists) and `src/OroQuizClash.Application/Features/Games/` and `src/OroQuizClash.Infrastructure/{Specifications,Services}` via `mkdir -p src/OroQuizClash.Domain/Games/Rules src/OroQuizClash.Domain/Games/Events src/OroQuizClash.Application/Features/Games src/OroQuizClash.Infrastructure/Specifications`
- [x] T003 Verify `BuildingBlocks` dependencies and `Directory.Packages.props` central versions for `net10.0` (`LangVersion latest`, `Nullable enable`) in `Directory.Build.props` and `src/BuildingBlocks/` references

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared domain/infrastructure that MUST be complete before ANY user story — extend GameStatus to 9 states, create GameRound/GamePlayer composition, persistence, specifications, ports

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Extend `GameStatus` Enumeration to 9 valores `DRAFT(1), READY(2), WAITING_FOR_PLAYERS(3), IN_PROGRESS(4), ROUND_IN_PROGRESS(5), ROUND_COMPLETED(6), FINISHED(7), CANCELLED(8), FORCED_FINISHED(9)` with helpers `IsTerminal`, `IsStarted`, `IsRoundActive`, `CanTransitionTo`, `IsValidTransition(from,to)` in `src/OroQuizClash.Domain/Games/Enumerations/GameStatus.cs`
- [x] T005 [P] Create `GameRound` Entity `GameRoundId : StronglyTypedId<Guid>` + `GameRound : Entity<GameRoundId>` with `GameId`, `RoundNumber` 1..MaxRounds, `QuestionId`, `Status` (ROUND_IN_PROGRESS/COMPLETED), `StartedAt`, `CompletedAt` in `src/OroQuizClash.Domain/Games/GameRound.cs` and `src/OroQuizClash.Domain/Games/GameRoundId.cs`
- [x] T006 [P] Create `GamePlayer` Entity `GamePlayerId : StronglyTypedId<Guid>` + `GamePlayer : Entity<GamePlayerId>` with `GameId`, `UserId` (sub), `JoinedAt`, `DisplayName` in `src/OroQuizClash.Domain/Games/GamePlayer.cs` and `src/OroQuizClash.Domain/Games/GamePlayerId.cs`
- [x] T007 [P] Create `GameConfiguration` ValueObject extension (if missing) to ensure `CategoryId`, `MinRounds`≥5, `MaxRounds`, `InitialDifficulty` 1..5, `DifficultyStrategy`, `TimeLimitPerQuestionSeconds` 5–300, `PointsPerRound`, `MinPlayers`≥1, `MaxPlayers`, `WithdrawalPolicy`, `LossPolicy`, `ConsolationPolicy`, `ScoringSystem`, `RewardRules` in `src/OroQuizClash.Domain/Games/ValueObjects/GameConfiguration.cs` (verify existing from 001)
- [x] T008 Extend `Game` AggregateRoot to include `List<GamePlayer> _players` and `List<GameRound> _rounds`, `RowVersion`, `ReadyAt`, `StartedAt`, `FinishedAt`, `CreatedBy`, backing fields and `IReadOnlyList` accessors in `src/OroQuizClash.Domain/Games/Game.cs` (keep `Create` from 001, add private ctor handling for new collections)
- [x] T009 Create EF configuration `GameTypeConfiguration : IEntityTypeConfiguration<Game>` update with `OwnsOne(Configuration)`, `Property(RowVersion).IsRowVersion().IsConcurrencyToken()`, `HasMany(g=>g.Players).WithOne().HasForeignKey("GameId").OnDelete(Cascade)`, `HasMany(g=>g.Rounds)` , `HasIndex(Status)`, `HasIndex(Configuration.CategoryId)` in `src/OroQuizClash.Infrastructure/Persistence/Configurations/GameTypeConfiguration.cs`
- [x] T010 Create EF configurations `GameRoundTypeConfiguration` and `GamePlayerTypeConfiguration` with `HasKey`, `StronglyTypedId` converter, `HasIndex(GameId, RoundNumber).IsUnique()`, `HasIndex(GameId, UserId).IsUnique()` in `src/OroQuizClash.Infrastructure/Persistence/Configurations/GameRoundTypeConfiguration.cs` and `GamePlayerTypeConfiguration.cs`
- [x] T011 Create domain events `GameReadyDomainEvent`, `PlayerJoinedDomainEvent`, `RoundStartedDomainEvent`, `RoundCompletedDomainEvent`, `GameFinishedDomainEvent`, `GameCancelledDomainEvent`, `GameForcedFinishedDomainEvent` in `src/OroQuizClash.Domain/Games/Events/` (one file per event, e.g., `GameReadyDomainEvent.cs` : `DomainEvent`)
- [x] T012 Create `GameErrors` factory helpers `NotEnoughPlayers`, `PlayerAlreadyJoined`, `GameFull`, `RoundAlreadyInProgress`, `PreviousRoundNotCompleted`, `NoActiveRound`, `ConfigurationImmutable`, `InvalidGameState`, `ConcurrencyConflict`, `CategoryNotReady`, `NoAvailableQuestion`, `InvalidReason` in `src/OroQuizClash.Domain/Games/GameErrors.cs` (extend `GameErrors` or `src/OroQuizClash.Domain/Shared/Errors/GameErrors.cs`)
- [x] T013 Create `GameByIdSpecification` and `GameFilterSpecification` (`Status`, `CategoryId`, `CreatedBy`, `Search`, `Pagination` + `AsNoTracking`) in `src/OroQuizClash.Infrastructure/Specifications/GameSpecifications.cs`
- [x] T014 Extend `OroQuizClashDbContext` to ensure `DbSet<Game>` includes `GameRound`/`GamePlayer` via `ApplyConfigurationsFromAssembly` and verify `AddOutbox` + `IUnitOfWork` wiring in `src/OroQuizClash.Api/Program.cs` (`AddScoped<IRepository<Game,GameId>>` with `EfRepository<Game,GameId>`)
- [ ] T015 Add architecture test verifying `OroQuizClash.Domain/Games` does not reference Infrastructure/Web and no MediatR/MassTransit/AutoMapper in `tests/OroQuizClash.Architecture.Tests/GameLifecycleDependenciesTests.cs`

**Checkpoint**: Foundation ready — `dotnet build OroQuizClash.slnx` passes, `GameStatus` 9 valores reachable via `GameStatus.FromId(9)`, `Game` with `Players`/`Rounds` composition `EnsureCreated` creates `Games`+`GameRounds`+`GamePlayers`+`OutboxMessages` with `rowversion` and `UNIQUE` indexes

---

## Phase 3: User Story 1 — Crear y preparar partida hasta sala de espera (Priority: P1) 🎯 MVP

**Goal**: Organizador crea `Game` en `DRAFT`, lo lleva a `READY` solo si config válida + categoría ≥5 válidas, y abre `WAITING_FOR_PLAYERS` para `JoinGame` idempotente hasta `MinPlayers`

**Independent Test**: `POST /api/games` con config válida →201 `DRAFT` + `GameCreated`; `POST /api/games/{id}/ready` con categoría ≥5 →200 `READY` + `GameReady`, con <5 →400 `CategoryNotReady` permanece `DRAFT`; `POST /api/games/{id}/open-lobby` →200 `WAITING_FOR_PLAYERS`; `POST /api/games/{id}/players` ×2 →200 `PlayerJoined` each, 3rd duplicate →409 `PlayerAlreadyJoined`, concurrent `MarkReady` →409

### Tests for User Story 1 (write FIRST, ensure FAIL before implementation)

- [ ] T016 [P] [US1] Contract test for `POST /api/games` create valid/invalid + `POST /api/games/{id}/ready` gate in `tests/OroQuizClash.Api.Tests/Contracts/GameCreateReadyContractTests.cs` (WebApplicationFactory, JWT mock `ADMIN`, asserts 201 vs 400 `InvalidGameConfiguration`/`CategoryNotReady` per `contracts/game-lifecycle.openapi.yaml`)
- [ ] T017 [P] [US1] Contract test for `POST /api/games/{id}/open-lobby` + `POST /api/games/{id}/players` idempotente in `tests/OroQuizClash.Api.Tests/Contracts/GameLobbyJoinContractTests.cs` (asserts `READY→WAITING_FOR_PLAYERS` 200, `PlayerJoined` 200, duplicate 409, `GameFull` 409)
- [ ] T018 [P] [US1] Domain unit tests for `Game.Create` + `MarkReady` + `OpenLobby` + `JoinPlayer` in `tests/OroQuizClash.Domain.Tests/Games/GameLifecycleCreateTests.cs` (Arrange config válida MinRounds=5 category published 5 válidas → MarkReady success + GameReady; <5 → Failure CategoryNotReady; OpenLobby from READY success; JoinPlayer in WAITING success, duplicate → PlayerAlreadyJoined, full → GameFull)
- [ ] T019 [P] [US1] Concurrency test for duplicate `MarkReady` with stale `RowVersion` in `tests/OroQuizClash.Infrastructure.Tests/Persistence/GameLifecycleConcurrencyTests.cs` (two `OroQuizClashDbContext` instances, second `SaveChangesAsync` → `DbUpdateConcurrencyException` →409)

### Implementation for User Story 1

- [x] T020 [P] [US1] Implement `IBusinessRule` classes `GameConfigurationValidRule`, `CategoryMustBeReadyRule`, `CanTransitionToReadyRule` in `src/OroQuizClash.Domain/Games/Rules/` (one file per rule, e.g., `CategoryMustBeReadyRule.cs` checks `IQuestionCounter.CountValidAsync≥5`)
- [x] T021 [US1] Extend `Game.Create` to validate via `GameConfigurationValidRule` and keep `DRAFT` (verify existing 001 already does) and ensure `Game.MarkReady(ICategoryValidator, IQuestionCounter)` implements `DRAFT→READY` gate (`Check Category published + CountValid≥5`, `IsValidTransition`, set `Status=READY`, `ReadyAt=UtcNow`, `RaiseDomainEvent(new GameReadyDomainEvent(Id.Value))`) in `src/OroQuizClash.Domain/Games/Game.cs` (depends on T004, T008, T020)
- [x] T022 [US1] Implement `Game.OpenLobby()` (`READY→WAITING_FOR_PLAYERS`, `IsValidTransition`, set `Status=WAITING_FOR_PLAYERS`, no event or `LobbyOpenedDomainEvent`) and `Game.JoinPlayer(Guid userId)` (only `WAITING_FOR_PLAYERS`, check `Players.Count < MaxPlayers` else `GameFull`, `!Players.Any(p=>p.UserId==userId)` else `PlayerAlreadyJoined`, add `GamePlayer`, `RaiseDomainEvent(new PlayerJoinedDomainEvent(Id.Value, userId))`) in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T023 [P] [US1] Create domain events `GameReadyDomainEvent` + `PlayerJoinedDomainEvent` + `LobbyOpenedDomainEvent` (if needed) in `src/OroQuizClash.Domain/Games/Events/` (one file per event)
- [x] T024 [US1] Implement Vertical Slices `MarkReady` in `src/OroQuizClash.Application/Features/Games/MarkReady.cs` with `MarkReadyCommand(GameId) : ICommand<Result<GameResponse>>`, `MarkReadyHandler` (`IRepository<Game,GameId>.GetByIdAsync`, `ICategoryValidator` + `IQuestionCounter`, `game.MarkReady`, `SaveChangesAsync` handling `DbUpdateConcurrencyException`→`ConcurrencyConflict`), `MarkReadyEndpoint : IEndpoint` (`POST /api/games/{id}/ready` `AdminOrGameManager`)
- [x] T025 [US1] Implement Vertical Slices `OpenLobby` in `src/OroQuizClash.Application/Features/Games/OpenLobby.cs` (`OpenLobbyCommand(GameId)`, `Handler` loads `Game`, `game.OpenLobby()`, `SaveChanges`, `Endpoint POST /api/games/{id}/open-lobby`)
- [x] T026 [US1] Implement Vertical Slice `JoinGame` in `src/OroQuizClash.Application/Features/Games/JoinGame.cs` with `JoinGameCommand(GameId, UserId) : ICommand<Result<GameResponse>>`, `JoinGameValidator` (GameId required, UserId required), `JoinGameHandler` (`IRepository<Game,GameId>`, extract `sub` from JWT if UserId empty, `game.JoinPlayer(userId)`, `SaveChanges`), `JoinGameEndpoint : IEndpoint` (`POST /api/games/{id}/players` `RequireAuthorization` `PLAYER`+`Admin`)
- [x] T027 [US1] Implement Vertical Slices `GetGame` + `GetGames` queries in `src/OroQuizClash.Application/Features/Games/GetGame.cs` (`GetGameQuery(GameId) : IQuery<Result<GameResponse>>`, `GetGamesQuery` with filter `Status`/`CategoryId`/`CreatedBy` via `GameFilterSpecification`, `Handler` `IRepository<Game,GameId>.ListAsync`) and `GetGameEndpoint` `GET /api/games/{id}` + `GET /api/games`

**Checkpoint**: US1 fully functional — `dotnet test --filter GameLifecycleCreate` passes, `POST /api/games` valid→201 DRAFT <1s, `POST /ready` with ≥5→READY <2s vs <5→400, `open-lobby`→WAITING 200, `join`×2→PlayerJoined 200, duplicate→409, `quickstart.md` P1 scenario green

---

## Phase 4: User Story 2 — Iniciar partida y ciclo de rondas (Priority: P1)

**Goal**: `WAITING_FOR_PLAYERS` con `players≥MinPlayers` → `IN_PROGRESS` → loop `ROUND_IN_PROGRESS ↔ ROUND_COMPLETED` (vía `StartRound` con selección PUBLISHED no usada y `CompleteRound`) hasta agotar rondas y poder `Finish` a `FINISHED`

**Independent Test**: `WAITING_FOR_PLAYERS` + 2 players (Min=2) → `POST /start` → `IN_PROGRESS` + `GameStarted`; con 1 player →400 `NotEnoughPlayers`; `IN_PROGRESS` → `POST /rounds/start` → `ROUND_IN_PROGRESS` roundNumber 1 + questionId; 2nd `start` sin `complete` →400 `RoundAlreadyInProgress`; `POST /rounds/{id}/complete` → `ROUND_COMPLETED`; next `start` → `ROUND_IN_PROGRESS` 2; tras MaxRounds `POST /finish` → `FINISHED`; concurrent `StartGame` →409

### Tests for User Story 2 (write FIRST)

- [ ] T028 [P] [US2] Contract tests for `POST /api/games/{id}/start` + `POST /api/games/{id}/rounds/start` + `POST /api/games/{id}/rounds/{roundId}/complete` + `POST /api/games/{id}/finish` in `tests/OroQuizClash.Api.Tests/Contracts/GameRoundLifecycleContractTests.cs` (asserts `WAITING→IN_PROGRESS` 200 vs `NotEnoughPlayers` 400, `RoundAlreadyInProgress` 400, `NoAvailableQuestion` 409, `Finish` valid 200 vs invalid from DRAFT 400)
- [ ] T029 [P] [US2] Domain unit tests for `Game.Start` + `StartRound` + `CompleteRound` + `Finish` in `tests/OroQuizClash.Domain.Tests/Games/GameRoundLifecycleTests.cs` (Arrange `WAITING` 2 players → Start success + GameStarted; 1 player → NotEnoughPlayers; IN_PROGRESS → StartRound success + RoundStarted + QuestionId PUBLISHED not in Previous; duplicate StartRound without Complete → RoundAlreadyInProgress; CompleteRound → ROUND_COMPLETED; Finish from ROUND_COMPLETED → FINISHED; Finish from DRAFT → InvalidGameState)
- [ ] T030 [P] [US2] Strategy integration test for `StartRound` selection with `IQuestionSelectionStrategy` stub in `tests/OroQuizClash.Application.Tests/Features/Games/StartRoundHandlerTests.cs` (NSubstitute `IRepository<Question,QuestionId>` mock returns PUBLISHED list, assert `StartRound` picks not in PreviousQuestionIds, <500ms with 1k)
- [ ] T031 [P] [US2] Concurrency test for duplicate `StartGame` and `StartRound` with stale `RowVersion` in `tests/OroQuizClash.Infrastructure.Tests/Persistence/GameStartRoundConcurrencyTests.cs` (two contexts, second → `DbUpdateConcurrencyException` →409)

### Implementation for User Story 2

- [x] T032 [P] [US2] Implement `IBusinessRule` `NotEnoughPlayersRule`, `PreviousRoundNotCompletedRule`, `CanFinishFromStateRule` in `src/OroQuizClash.Domain/Games/Rules/` (e.g., `NotEnoughPlayersRule.cs` checks `players.Count < MinPlayers`)
- [x] T033 [US2] Implement `Game.Start()` (`WAITING_FOR_PLAYERS→IN_PROGRESS`, gate `players.Count≥MinPlayers && ≤MaxPlayers`, `IsValidTransition`, set `Status=IN_PROGRESS`, `StartedAt=UtcNow`, `RaiseDomainEvent(new GameStartedDomainEvent(Id.Value))`, make `Configuration` immutable) and `Game.CompleteRound(Guid roundId)` (`ROUND_IN_PROGRESS→ROUND_COMPLETED`, find `GameRound` by id, set `Status=ROUND_COMPLETED`, `CompletedAt=UtcNow`, `RaiseDomainEvent(new RoundCompletedDomainEvent(Id.Value, roundId))`) in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T034 [US2] Implement `Game.StartRound(IQuestionSelectionStrategy selector, QuestionSelectionCriteria criteria, Guid questionId)` logic in `src/OroQuizClash.Domain/Games/Game.cs` (`IN_PROGRESS` or `ROUND_COMPLETED` allowed, no active `ROUND_IN_PROGRESS`, `rounds.Count < MaxRounds`, create `GameRound` with `RoundNumber = Rounds.Count+1`, `QuestionId`, `Status=ROUND_IN_PROGRESS`, `StartedAt=UtcNow`, add to `_rounds`, set `Game.Status=ROUND_IN_PROGRESS`, `RaiseDomainEvent(new RoundStartedDomainEvent(Id.Value, roundId, roundNumber, questionId))`; if no question available → return `NoAvailableQuestion` without creating round)
- [x] T035 [P] [US2] Create domain events `GameStartedDomainEvent`, `RoundStartedDomainEvent`, `RoundCompletedDomainEvent`, `GameFinishedDomainEvent` in `src/OroQuizClash.Domain/Games/Events/` (one file per event)
- [x] T036 [US2] Implement Vertical Slices `StartGame` in `src/OroQuizClash.Application/Features/Games/StartGame.cs` (`StartGameCommand(GameId)`, `Handler` loads `Game`, `game.Start()`, `SaveChanges`, `Endpoint POST /api/games/{id}/start`)
- [x] T037 [US2] Implement Vertical Slice `StartRound` in `src/OroQuizClash.Application/Features/Games/StartRound.cs` with `StartRoundCommand(GameId) : ICommand<Result<GameRoundResponse>>`, `StartRoundHandler` (`IRepository<Game,GameId>`, `IQuestionSelectionStrategy`, build `QuestionSelectionCriteria` from `Game.Configuration.CategoryId` + `Difficulty` progressive + `Players` + `PreviousQuestionIds=Rounds.Select(r=>r.QuestionId)`, `selector.SelectAsync`, on success `game.StartRound(selector, criteria, questionId)`, `SaveChanges`, handle `DbUpdateConcurrencyException`→`ConcurrencyConflict`), `StartRoundEndpoint : IEndpoint` (`POST /api/games/{id}/rounds/start`)
- [x] T038 [US2] Implement Vertical Slices `CompleteRound` in `src/OroQuizClash.Application/Features/Games/CompleteRound.cs` (`CompleteRoundCommand(GameId, RoundId)`, `Handler` `game.CompleteRound(roundId)`, `Endpoint POST /api/games/{id}/rounds/{roundId}/complete`) and `FinishGame` in `src/OroQuizClash.Application/Features/Games/FinishGame.cs` (`FinishGameCommand(GameId)`, `game.Finish()`→`FINISHED`, `Endpoint POST /api/games/{id}/finish`)

**Checkpoint**: US2 green — `WAITING→IN_PROGRESS` 100% with `players≥Min` + GameStarted <500ms, `NotEnoughPlayers` 400, `StartRound` exclusive `ROUND_IN_PROGRESS` + PUBLISHED not used <500ms 1k, `RoundAlreadyInProgress` 400, `CompleteRound`→`ROUND_COMPLETED`, next `StartRound`→`ROUND_IN_PROGRESS`, `Finish` from `ROUND_COMPLETED`→`FINISHED` + GameFinished, concurrent `StartGame/StartRound`→409, `quickstart.md` P1 round loop green

---

## Phase 5: User Story 3 — Defensa de invariantes durante el juego (Priority: P1)

**Goal**: Rechazar `UpdateGame` después de iniciar (`ConfigurationImmutable`), `SubmitAnswer` solo en `ROUND_IN_PROGRESS` (`NoActiveRound` server timestamp), y `Finish/Cancel` solo desde estados válidos (`InvalidGameState`), con `rowversion` 409 y idempotencia

**Independent Test**: `UpdateGame` en `WAITING_FOR_PLAYERS`/`IN_PROGRESS`→400 `ConfigurationImmutable`; `SubmitAnswer` en `IN_PROGRESS` sin ronda→400 `NoActiveRound`, en `ROUND_IN_PROGRESS`→200 con `PointTransaction` + idempotente duplicado no duplica puntos; `FinishGame` desde `DRAFT`→400 `InvalidGameState`; `FINISHED→StartGame`→400; segundo `Finish` concurrente→409

### Tests for User Story 3 (write FIRST)

- [ ] T039 [P] [US3] Domain unit tests for `Game.UpdateConfiguration` after start + `CanSubmitAnswer` guard + `CanFinishFromState` in `tests/OroQuizClash.Domain.Tests/Games/GameInvariantTests.cs` (Arrange `IN_PROGRESS` → Update → ConfigurationImmutable; `IN_PROGRESS` without round → SubmitAnswer → NoActiveRound; `DRAFT→Finish` → InvalidGameState; `FINISHED→Start` → InvalidGameState)
- [ ] T040 [P] [US3] Contract tests for `PUT /api/games/{id}` after start + `POST /api/games/{id}/answers` outside round + `POST /api/games/{id}/finish` invalid in `tests/OroQuizClash.Api.Tests/Contracts/GameInvariantContractTests.cs` (asserts `ConfigurationImmutable` 400, `NoActiveRound` 400, `AnswerTimeout` if >TimeLimit, `InvalidGameState` 400, `ConcurrencyConflict` 409)
- [ ] T041 [P] [US3] Idempotency test for `SubmitAnswer` with `IdempotencyKey`/`PlayerId+RoundId` in `tests/OroQuizClash.Infrastructure.Tests/Persistence/SubmitAnswerIdempotencyTests.cs` (send duplicate with same key → second returns same `PointTransaction` count==1, no duplicate ledger entry)

### Implementation for User Story 3

- [x] T042 [P] [US3] Implement `IBusinessRule` `ConfigurationImmutableRule`, `NoActiveRoundRule`, `CanFinishFromStateRule`, `AnswerTimeoutRule` in `src/OroQuizClash.Domain/Games/Rules/` (e.g., `ConfigurationImmutableRule.cs` checks `Status.IsStarted`)
- [x] T043 [US3] Extend `Game.UpdateConfiguration(GameConfiguration newConfig)` guard (already in 001, verify `Status.IsStarted` → `ConfigurationImmutable`, re-validate via `Create`, `Name= newConfig.Name`) and add `Game.CanSubmitAnswer(Guid playerId)` helper (`Status==ROUND_IN_PROGRESS` else `NoActiveRound`) and `Game.Finish()` strict matrix (`IsValidTransition` to `FINISHED` only from `IN_PROGRESS/ROUND_COMPLETED/ROUND_IN_PROGRESS` per policy) in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T044 [US3] Implement `SubmitAnswer` slice `SubmitAnswer.cs` with `SubmitAnswerCommand(GameId, QuestionId, AnswerOptionId, RoundId, IdempotencyKey) : ICommand<Result<SubmitAnswerResponse>>`, `SubmitAnswerValidator` (GameId/QuestionId/AnswerOptionId required, IdempotencyKey required), `SubmitAnswerHandler` (`IRepository<Game,GameId>`, `IRepository<Question,QuestionId>`, check `game.Status==ROUND_IN_PROGRESS` else `NoActiveRound`, find `GameRound` by `RoundId`, verify `Round.QuestionId==QuestionId`, evaluate `IsCorrect` server-side from `Question.AnswerOptions`, check `ServerTimestamp - Round.StartedAt ≤ TimeLimit`, idempotency via `IdempotencyStore` or `PointTransaction` lookup, create `PointTransaction` ledger, `RaiseDomainEvent`, `SaveChanges`), `SubmitAnswerEndpoint : IEndpoint` (`POST /api/games/{id}/answers`)
- [ ] T045 [US3] Add `PointTransaction` ledger check (verify existing `Score`/`PointTransaction` aggregate not needed for this story, but guard `SubmitAnswer` creates `PointTransaction` with `Type=ANSWER_CORRECT/ANSWER_INCORRECT` and does not trust client `Score`) and ensure `ConfigurationImmutable` mapped to `ProblemDetails` 400 via `GameErrors` in `src/OroQuizClash.Domain/Shared/Errors/GameErrors.cs`

**Checkpoint**: US3 green — `UpdateGame` after `IN_PROGRESS` 100% 400, `SubmitAnswer` outside `ROUND_IN_PROGRESS` 100% 400, inside 100% server-evaluated + ledger, duplicate idempotent no double points, `Finish` from `DRAFT` 100% 400, `rowversion` stale →409, `quickstart.md` P1 defense scenario green

---

## Phase 6: User Story 4 — Finalización y cancelación controlada (Priority: P2)

**Goal**: `FinishGame` desde `ROUND_COMPLETED`/`IN_PROGRESS` → `FINISHED` + `GameFinished` (bloquea todo), `CancelGame` desde `DRAFT/READY/WAITING/IN_PROGRESS/ROUND_*` → `CANCELLED`, `ForceFinishGame` desde `IN_PROGRESS/ROUND_*` → `FORCED_FINISHED` con `Reason` 3–500, terminales rechazan toda transición posterior + `409` concurrencia

**Independent Test**: `ROUND_COMPLETED` con `completed≥MinRounds` → `POST /finish` →200 `FINISHED` + `GameFinished` + `FinishedAt`, siguiente `StartRound/SubmitAnswer/Join`→400 `InvalidGameState`; `WAITING_FOR_PLAYERS` → `POST /cancel` with reason →200 `CANCELLED` + `GameCancelled`; `IN_PROGRESS` timeout → `POST /force-finish` →200 `FORCED_FINISHED` + `GameForcedFinished`; `FINISHED→Cancel`→400, concurrent `Cancel`×2→409

### Tests for User Story 4 (write FIRST)

- [ ] T046 [P] [US4] Contract tests for `POST /api/games/{id}/cancel` + `POST /api/games/{id}/force-finish` + `GET /api/games/{id}` terminal in `tests/OroQuizClash.Api.Tests/Contracts/GameCancellationContractTests.cs` (asserts `CANCELLED` 200 valid reason, `InvalidGameState` from `FINISHED`, `FORCED_FINISHED` 200, `InvalidReason` 400 for empty, `409` concurrent cancel)
- [ ] T047 [P] [US4] Domain unit tests for `Game.Cancel` + `ForceFinish` + terminal guards in `tests/OroQuizClash.Domain.Tests/Games/GameCancellationTests.cs` (Arrange `DRAFT→Cancel` success + GameCancelled; `IN_PROGRESS→ForceFinish` success; `FINISHED→Cancel` → InvalidGameState; `Cancel` with reason <3 → InvalidReason; terminal `Finish→Start` → InvalidGameState)
- [ ] T048 [P] [US4] Specification + query tests for `GameFilterSpecification` (Status, CategoryId, CreatedBy, Search) + `GetGames` pagination in `tests/OroQuizClash.Infrastructure.Tests/Specifications/GameFilterSpecificationTests.cs` (seed 20 games mixed statuses, assert filter `FINISHED` returns only finished, paginated `page/pageSize`)

### Implementation for User Story 4

- [x] T049 [P] [US4] Implement `IBusinessRule` `CanCancelFromStateRule`, `CanForceFinishFromStateRule`, `ReasonRequiredRule` in `src/OroQuizClash.Domain/Games/Rules/` (e.g., `ReasonRequiredRule.cs` validates 3–500)
- [x] T050 [US4] Implement `Game.Cancel(string reason)` (`!IsTerminal` else `InvalidGameState`, `ReasonRequiredRule` else `InvalidReason`, set `Status=CANCELLED`, `FinishedAt=UtcNow`, `RaiseDomainEvent(new GameCancelledDomainEvent(Id.Value, reason))`) and `Game.ForceFinish(string reason)` (`IN_PROGRESS`/`ROUND_*` only, else `InvalidGameState`, set `FORCED_FINISHED`, `RaiseDomainEvent(new GameForcedFinishedDomainEvent(Id.Value, reason))`) and ensure `Finish()` already terminal-guarded in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T051 [P] [US4] Create domain events `GameCancelledDomainEvent`, `GameForcedFinishedDomainEvent` in `src/OroQuizClash.Domain/Games/Events/` (one file per event)
- [x] T052 [US4] Implement Vertical Slices `CancelGame` in `src/OroQuizClash.Application/Features/Games/CancelGame.cs` (`CancelGameCommand(GameId, Reason)`, `CancelGameValidator` (Reason 3–500), `CancelGameHandler` `game.Cancel(reason)`, `Endpoint POST /api/games/{id}/cancel` `AdminOrGameManager`) and `ForceFinishGame` in `src/OroQuizClash.Application/Features/Games/ForceFinishGame.cs` (`ForceFinishGameCommand(GameId, Reason)`, `Endpoint POST /api/games/{id}/force-finish`)
- [x] T053 [US4] Implement `GetGame`/`GetGames` queries already in `GetGame.cs` (verify `GameByIdSpecification` includes `Players`/`Rounds` + `AsNoTracking`, `GameFilterSpecification` with `Where(Status==)`, `Where(CategoryId==)`, pagination) and ensure `GET /api/games` + `GET /api/games/{id}` return `RowVersion` for `If-Match`
- [ ] T054 [US4] Add audit append-only `GameLifecycleAudit` writer `IUnitOfWork` + `Outbox` handling for `GameFinished/Cancelled/ForcedFinished` integration (if publishing `GameFinishedIntegrationEvent` via `IOutboxWriter`) and ensure `AuditLog` table with `CorrelationId, GameId, PlayerId, RoundId, FromState, ToState, Command, PerformedBy, Timestamp` in `src/OroQuizClash.Infrastructure/Services/GameLifecycleAuditWriter.cs` (or inline in handlers via `ILogger`)

**Checkpoint**: US4 green — `Cancel` from `DRAFT→CANCELLED` 100%, `ForceFinish` from `IN_PROGRESS`→`FORCED_FINISHED` 100%, `FINISHED` terminal blocks 100%, `Reason <3`→400, `GET /api/games/{id}` reflects terminal, concurrent `Cancel`→409, `quickstart.md` P2 finish/cancel/force scenarios green

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Hardening, docs, quickstart validation, DoD checklist, performance & security

- [ ] T055 [P] Add `ProblemDetails` mapping tests for all `GameErrors` lifecycle codes (`InvalidGameConfiguration`, `CategoryNotReady`, `NotEnoughPlayers`, `PlayerAlreadyJoined`, `GameFull`, `RoundAlreadyInProgress`, `NoActiveRound`, `ConfigurationImmutable`, `InvalidGameState`, `ConcurrencyConflict`, `NoAvailableQuestion`, `InvalidReason`) in `tests/OroQuizClash.Api.Tests/Errors/GameLifecycleErrorsMappingTests.cs` (assert `400` vs `404` vs `409` per `GlobalExceptionHandler` → `Result.ToHttpResult()`)
- [ ] T056 [P] Run `quickstart.md` end-to-end validation (`Create→Ready→Wait→Join×2→Start→StartRound×5→CompleteRound→Finish→Cancel→ForceFinish→concurrencia 409`) and fix gaps in `specs/004-game-lifecycle/quickstart.md` (execute curl scenarios, confirm <1s Create, <2s Ready, <500ms Start/StartRound, 409 on concurrent, audit logs, `GET` reflects terminal)
- [ ] T057 [P] Add structured logging fields `GameId`/`RoundId`/`PlayerId`/`FromState`/`ToState`/`Command`/`Duration` via `LoggingBehavior` verification in `tests/OroQuizClash.Application.Tests/Pipeline/GameLifecycleLoggingBehaviorTests.cs` (uses `ILogger` NSubstitute, asserts `LogInformation` with `GameId`)
- [ ] T058 [P] Update `docs/adr/ADR-013-game-lifecycle.md` documenting decisions from `research.md` (9-state Enumeration, rowversion, 9 DomainEvents + Outbox, `IQuestionSelectionStrategy` delegation, idempotency `Join/SubmitAnswer`)
- [ ] T059 [P] Update `docs/adr/ADR-001-modular-monolith.md` if needed to reference `GameRound`/`GamePlayer` composition decision (vs separate aggregates)
- [ ] T060 Security hardening: verify `POST /ready /open-lobby /start /rounds/start /finish /cancel /force-finish` require `ADMIN/GAME_MANAGER` (no anonymous), `POST /players`/`POST /answers` require `PLAYER` (JWT `sub`), `GET /api/games` requires authenticated, rate limiting via `BuildingBlocks.ServiceDefaults`, `correlationId` propagation, no sensitive data (answer `IsCorrect` not leaked before Round) in `src/OroQuizClash.Api/Program.cs` and `GameEndpoints` (`[Authorize(Policy="AdminOrGameManager")]` vs `[Authorize(Policy="Player")]`)
- [ ] T061 Performance smoke: `dotnet test --filter SC-001` timing assert Create <1s, Ready <2s, Start <500ms, StartRound <500ms with 1k seeded, SubmitAnswer idempotent <500ms in `tests/OroQuizClash.Api.Tests/Performance/GameLifecyclePerformanceTests.cs` (measures `MarkReadyHandler` + `RandomQuestionSelectionStrategy` with 1k seeded)
- [ ] T062 Add audit append-only verification for lifecycle transitions (MarkReady/Join/Start/StartRound/CompleteRound/Finish/Cancel/ForceFinish) with `CorrelationId`/`PerformedBy sub` in `tests/OroQuizClash.Infrastructure.Tests/Audit/GameLifecycleAuditTests.cs` (assert after `SaveChanges` audit row exists, OTel `TraceId` logged, `OutboxMessages` for `GameFinishedIntegrationEvent` if publishing)
- [ ] T063 Final `dotnet build OroQuizClash.slnx && dotnet test` green, `dotnet format` clean, update `specs/004-game-lifecycle/spec.md` Status to `Ready for Review` and sync `specs/004-game-lifecycle/checklists/requirements.md` (re-run quality checklist, confirm SC-001..008)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately (reuses `001` foundation `Game.Create`)
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (9-state `GameStatus`, `GameRound`/`GamePlayer` composition, `RowVersion`, specs, ports, events)
- **User Stories (Phase 3+)**: Depend on Foundational
  - **US1 (P1) Create+Ready+Lobby+Join**: No dependencies on other stories — delivers MVP preparación
  - **US2 (P1) Start+Round loop+Finish**: Depends on US1's `Game` (needs `WAITING_FOR_PLAYERS` with players) + `IQuestionSelectionStrategy` — adds motor jugable, independently testable after US1 (Start with NotEnoughPlayers vs success)
  - **US3 (P1) Defensa**: Depends on US1's `Game` + US2's `Start`/`StartRound` (needs `IN_PROGRESS`/`ROUND_IN_PROGRESS`) — adds guards `ConfigurationImmutable`/`NoActiveRound`/`InvalidGameState`, independently testable (Update after Start → 400)
  - **US4 (P2) Cancel/ForceFinish/Get**: Depends on US1's `Game` + US2's `Finish` (needs terminal matrix) — adds cierre auditable, independently testable (Cancel from `DRAFT` vs `FINISHED`)
- **Polish (Final Phase)**: Depends on all desired stories complete (US1+US2 for MVP jugable, US3 required for integridad, US4 for cierre)

### User Story Dependencies

- **US1 (P1)**: Can start after Foundational — no deps on other stories; delivers MVP preparación (creación hasta `WAITING_FOR_PLAYERS` + `Join` idempotente)
- **US2 (P1)**: Depends on US1's `Game` (needs `WAITING_FOR_PLAYERS` with `players≥MinPlayers`) and `IQuestionSelectionStrategy` — adds `IN_PROGRESS` + round loop + `FINISHED`; independently testable after US1
- **US3 (P1)**: Depends on US1's `Game` + US2's `Start`/`StartRound` — adds defensa `ConfigurationImmutable`/`NoActiveRound`/`InvalidGameState`; independently testable after US1 (Update after `Start` → 400)
- **US4 (P2)**: Depends on US1's `Game` + US2's `Finish` — adds `CANCELLED`/`FORCED_FINISHED` terminals + `GET` filtros; independently testable (Cancel from `DRAFT` vs `FINISHED`)

### Within Each User Story

- Tests FIRST → FAIL → Implementation → PASS (TDD per constitution Testing Strategy)
- Models/VOs/Enumerations → Rules → Aggregate (`Game`) → EF Config → Slice (Validator → Handler → Endpoint) → Specification → Integration tests
- `IQuestionCounter` seeded (≥5 válidas) before `MarkReady` tests; `IQuestionSelectionStrategy` stub (PUBLISHED not in Previous) before `StartRound` tests; `rowversion` concurrency tests with two `DbContext` instances
- Verify `Result.Failure` codes map to `ProblemDetails` (400/404/409) after each story

### Parallel Opportunities

- Phase 2: T005+T006 (GameRound/GamePlayer) parallel; T009+T010 (GameTypeConfiguration + Round/Player configs) parallel; T011 creation of 7 domain events parallel; T013 specs `GameById` + `GameFilter` parallel
- Phase 3: T016+T017+T018+T019 (contract/domain/concurrency tests) parallel; T020 Rule classes parallel; T023 events parallel; T024+T025+T026 slices `MarkReady/OpenLobby/JoinGame` parallel (different files)
- Phase 4: T028+T029+T030+T031 (contract/domain/strategy/concurrency tests) parallel; T032 rule classes parallel; T035 events parallel; T036+T037+T038 slices `StartGame/StartRound/CompleteRound/FinishGame` can be worked in parallel after `Game` methods ready
- Phase 5: T039+T040+T041 (domain/contract/idempotency tests) parallel; T042 rule classes parallel
- Phase 6: T046+T047+T048 (contract/domain/spec tests) parallel
- Phase 7: T055+T056+T057+T058+T059 (errors/quickstart/logging/ADRs/security) parallel; T060 perf parallel with audit T062
- Different user stories can be worked on in parallel by different developers after Foundational if `Game.cs` is shared (coordinate merges on `Game.cs` changes)

### Parallel Example: User Story 1 (Create+Ready+Join)

```bash
# Tests in parallel (different files):
Task T016: Contract test in tests/OroQuizClash.Api.Tests/Contracts/GameCreateReadyContractTests.cs
Task T017: Contract test in tests/OroQuizClash.Api.Tests/Contracts/GameLobbyJoinContractTests.cs
Task T018: Domain unit tests in tests/OroQuizClash.Domain.Tests/Games/GameLifecycleCreateTests.cs
Task T019: Concurrency test in tests/OroQuizClash.Infrastructure.Tests/Persistence/GameLifecycleConcurrencyTests.cs

# Models/Rules in parallel:
Task T020: Rules in src/OroQuizClash.Domain/Games/Rules/CategoryMustBeReadyRule.cs
Task T023: Events in src/OroQuizClash.Domain/Games/Events/GameReadyDomainEvent.cs
Task T024: Slice MarkReady in src/OroQuizClash.Application/Features/Games/MarkReady.cs
```

### Parallel Example: User Story 2 (Round Loop)

```bash
# Tests in parallel:
Task T028: Contract in tests/OroQuizClash.Api.Tests/Contracts/GameRoundLifecycleContractTests.cs
Task T029: Domain in tests/OroQuizClash.Domain.Tests/Games/GameRoundLifecycleTests.cs
Task T030: Handler in tests/OroQuizClash.Application.Tests/Features/Games/StartRoundHandlerTests.cs
Task T031: Concurrency in tests/OroQuizClash.Infrastructure.Tests/Persistence/GameStartRoundConcurrencyTests.cs

# Slices in parallel (after Game methods):
Task T036: StartGame in src/OroQuizClash.Application/Features/Games/StartGame.cs
Task T037: StartRound in src/OroQuizClash.Application/Features/Games/StartRound.cs
Task T038: CompleteRound in src/OroQuizClash.Application/Features/Games/CompleteRound.cs
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup + Phase 2: Foundational (9-state `GameStatus`, `GameRound`/`GamePlayer` composition, `RowVersion`, specs, ports)
2. Complete Phase 3: US1 (`CreateGame` already DRAFT, `MarkReady` gate ≥5 + `OpenLobby` + `JoinGame` idempotente + `GetGame/GetGames`) — delivers MVP: preparación hasta `WAITING_FOR_PLAYERS`
3. **STOP and VALIDATE**: `dotnet test --filter GameLifecycleCreate` + `quickstart.md` P1 create/ready/join (valid→201/200, invalid→400, duplicate→409, concurrent→409) + `GET /api/games/{id}` con `RowVersion`
4. Deploy/demo if ready — `POST /api/games` + `ready` + `open-lobby` + `players` sin iniciar aún

### Incremental Delivery

1. Setup + Foundational → foundation ready (`GameStatus` 9, `GameRound`/`GamePlayer`, EF `RowVersion`, specs)
2. Add US1 → Test independently → Demo MVP preparación (DRAFT→READY→WAITING + Join)
3. Add US2 → Test `StartGame` + `StartRound` loop + `CompleteRound` + `Finish` → Demo motor jugable (9 estados, selección PUBLISHED <500ms)
4. Add US3 → Test `ConfigurationImmutable` + `NoActiveRound` + `InvalidGameState` + idempotencia `SubmitAnswer` → Demo defensa (authoritative, 400/409)
5. Add US4 → Test `Cancel`/`ForceFinish` terminals + `GET` filtros → Demo cierre auditable
6. Polish → quickstart E2E `Create→Finish` <5s + perf (<1s/<2s/<500ms) + security (`ADMIN`/`PLAYER`) + ADRs + audit + `dotnet build && dotnet test` green

### Parallel Team Strategy

With multiple developers after Foundational:
- Developer A: US1 (`MarkReady/OpenLobby/JoinGame` + `GetGame`)
- Developer B: US2 (`StartGame/StartRound/CompleteRound/FinishGame`) — after A's `Game.cs` merges (needs `WAITING_FOR_PLAYERS`)
- Developer C: US3 (`UpdateGame` guard + `SubmitAnswer` guard + `Finish` matrix) — after A's `Game.cs` + B's `Start`
- Developer D: US4 (`CancelGame/ForceFinishGame` + `GetGames` filter) — after A's `Game.cs` + B's `Finish`
- All stories integrate via same `Game` aggregate without conflicts if `Game.cs` changes are coordinated (rules → aggregate → slice order; use feature branch review for `Game.cs` merges)

---

## Notes

- [P] tasks = different files, no dependencies — can run in parallel
- [Story] label maps task to specific user story for traceability to `spec.md` US1..US4
- Each user story independently completable and testable via `quickstart.md` curl + `dotnet test --filter USx`
- Verify tests FAIL before implementation (TDD), commit after each task or logical group
- QST/FR traced: Rule 1 config válida→T020/T021/T024 (MarkReady gate), Rule 2 players≥Min→T032/T033/T036, Rule 3 previous round→T032/T037, Rule 4 NoActiveRound→T042/T044, Rule 5 ConfigurationImmutable→T042/T043, Rule 6 valid finish→T032/T038/T050 + FR-001→T004, FR-005→T013/T037
- Selection 7 params (SPEC-003) traced to `StartRound` T037 via `IQuestionSelectionStrategy`
- Avoid: vague tasks, same-file conflicts, cross-story dependencies that break independence; prefer explicit file paths per task
- Constitution gates: Domain First (T020-T022 rules in Domain), Clean Arch (T011 ports), BuildingBlocks reuse (T012 errors, T009 RowVersion), Vertical Slice (T024 slices), Authoritative (T037 server-side selection + T044 server timestamp), OroIdentityServer JWT (T014/T060), A (9 states + rowversion T004/T009), B (≥5 valid T020), C (configurable T007), E/F (rowversion + Specification T009/T013), G (Outbox T054), H (delegated T060), I/F (validation 3-level + idempotency T041/T044)

