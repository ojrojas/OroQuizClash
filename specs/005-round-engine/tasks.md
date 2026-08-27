# Tasks: Round Engine

**Input**: Design documents from `/specs/005-round-engine/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/
**Feature Branch**: `005-round-engine` | **Constitution**: v1.1.0 (I-VI)

**Organization**: Tasks grouped by user story — each story independently testable and deliverable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify existing modular monolith (001+002+003+004) and prepare Round Engine scaffolding

- [x] T001 Verify existing project structure per `specs/005-round-engine/plan.md` (`src/OroQuizClash.Domain`, `src/OroQuizClash.Application`, `src/OroQuizClash.Infrastructure`, `src/OroQuizClash.Api` in `OroQuizClash.slnx`) and `dotnet build OroQuizClash.slnx` passes
- [x] T002 Create Round Engine folder structure `src/OroQuizClash.Domain/Games/Strategies` and `src/OroQuizClash.Application/Features/Games` and `src/OroQuizClash.Infrastructure/{Strategies,Specifications}` via `mkdir -p src/OroQuizClash.Domain/Games/Strategies src/OroQuizClash.Application/Features/Games src/OroQuizClash.Infrastructure/Strategies`
- [x] T003 Verify `BuildingBlocks` dependencies and `Directory.Packages.props` central versions for `net10.0` (`LangVersion latest`, `Nullable enable`) in `Directory.Build.props` and `src/BuildingBlocks/` references

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared domain/infrastructure that MUST be complete before ANY user story — GameRound 5 fields, Difficulty progression port, Question selection wiring, rowversion/UNIQUE

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Extend `GameRound` Entity with `Difficulty : DifficultyLevel (1..5)`, `TimeLimit : int` (5–300), ensure `RoundNumber` unique per `GameId`, `Status` (`ROUND_IN_PROGRESS/COMPLETED`) in `src/OroQuizClash.Domain/Games/GameRound.cs` (verify existing from 004, add Difficulty/TimeLimit if missing)
- [x] T005 [P] Create `GameRoundId : StronglyTypedId<Guid>` in `src/OroQuizClash.Domain/Games/GameRoundId.cs` (verify exists from 004)
- [x] T006 [P] Create `IDifficultyProgressionStrategy` port `DifficultyLevel NextDifficulty(Game game, int completedRounds)` in `src/OroQuizClash.Domain/Games/Strategies/IDifficultyProgressionStrategy.cs`
- [x] T007 [P] Implement `LinearDifficultyStrategy` (`clamp(InitialDifficulty + completedRounds, 1,5)` → 1→2→3→4→5) in `src/OroQuizClash.Domain/Games/Strategies/LinearDifficultyStrategy.cs`
- [x] T008 [P] Implement `ProgressiveDifficultyStrategy` (1,1,2,3,5) and `AdaptiveDifficultyStrategy` (based on PointTransaction avg) in `src/OroQuizClash.Domain/Games/Strategies/ProgressiveDifficultyStrategy.cs` and `AdaptiveDifficultyStrategy.cs` (at least `Linear` + 2 registered)
- [x] T009 Extend `Game` AggregateRoot to delegate `StartRound(IQuestionSelectionStrategy, IDifficultyProgressionStrategy)` with `PreviousQuestionIds` exclusion and `Difficulty=NextDifficulty` in `src/OroQuizClash.Domain/Games/Game.cs` (keep existing `Create/MarkReady/Start/CompleteRound`, add `Difficulty`/`TimeLimit` assignment from `Configuration` + strategy)
- [x] T010 Create EF configuration `GameRoundTypeConfiguration : IEntityTypeConfiguration<GameRound>` update with `Difficulty` conversion (`HasConversion(d=>d.Id, id=>DifficultyLevel.FromId(id))`), `TimeLimit` IsRequired, `HasIndex(GameId,RoundNumber).IsUnique()`, `HasIndex(GameId,QuestionId).IsUnique()` optional in `src/OroQuizClash.Infrastructure/Persistence/Configurations/GameRoundTypeConfiguration.cs`
- [x] T011 Update `GameTypeConfiguration` to ensure `HasMany(g=>g.Rounds).WithOne().HasForeignKey("GameId")` via field `_rounds` and `Property(RowVersion).IsRowVersion()` protects `StartRound/CompleteRound` in `src/OroQuizClash.Infrastructure/Persistence/Configurations/GameTypeConfiguration.cs`
- [x] T012 Register `IDifficultyProgressionStrategy` default `LinearDifficultyStrategy` in `src/OroQuizClash.Api/Program.cs` (`AddScoped<IDifficultyProgressionStrategy, LinearDifficultyStrategy>` + config `Game:DifficultyStrategy` from `appsettings.json`)
- [x] T013 Create `GameErrors` helpers for Round Engine `DuplicateRoundNumber`, `DuplicateQuestion`, `NoAvailableQuestion`, `InvalidRoundFields` in `src/OroQuizClash.Domain/Shared/Errors/GameErrors.cs` (extend existing)
- [x] T014 Create `GameRound` domain events `RoundStartedDomainEvent`/`RoundCompletedDomainEvent` already exist from 004 — verify and extend with `Difficulty`/`TimeLimit` payload if missing in `src/OroQuizClash.Domain/Games/Events/RoundStartedDomainEvent.cs`
- [ ] T015 Add architecture test verifying `Games` Strategies do not reference Infrastructure/Web and `GameRound` composition in `tests/OroQuizClash.Architecture.Tests/RoundEngineDependenciesTests.cs`

**Checkpoint**: Foundation ready — `dotnet build OroQuizClash.slnx` passes, `GameRound` 5 fields `RoundNumber/Difficulty/QuestionId/TimeLimit/Status` persist via `EnsureCreated` with `UNIQUE (GameId,RoundNumber)` and `rowversion` on `Game`, `IDifficultyProgressionStrategy` injectable, `IQuestionSelectionStrategy` reused from 003

---

## Phase 3: User Story 1 — Iniciar ronda y seleccionar pregunta impredecible y no repetida (Priority: P1) 🎯 MVP

**Goal**: `StartRound` selecciona 1 `Question` PUBLISHED aleatoria server-side `ORDER BY NEWID()` no correlacionada por `RoundNumber`, excluye `PreviousQuestionIds` del mismo `Game`, y filtra `Category/Difficulty/Academic/Age` del round

**Independent Test**: Con `Game` en `IN_PROGRESS` con config `Category X, Difficulty 2, Secundaria 13-17`, banco 10 PUBLISHED que cumplen y 5 que no, `POST /api/games/{id}/rounds/start` crea `GameRound` con `RoundNumber` incremental, `Difficulty` según progresión, `QuestionId` PUBLISHED no usada, `TimeLimit` copiado, `Status=ROUND_IN_PROGRESS`; segunda `StartRound` excluye Q1, dos juegos distintos no correlacionan, concurrent `StartRound` →409, `GET /api/games/{id}` con `Previous` la pregunta fuera no elegible, impredecible verificado con distribución 1k

### Tests for User Story 1 (write FIRST, ensure FAIL before implementation)

- [ ] T016 [P] [US1] Contract test for `POST /api/games/{id}/rounds/start` success/failure (200 with 5 fields, 409 `RoundAlreadyInProgress`/`NoAvailableQuestion`/`DuplicateRoundNumber`, 400 `InvalidGameState`) in `tests/OroQuizClash.Api.Tests/Contracts/RoundStartContractTests.cs` (WebApplicationFactory, JWT mock `ADMIN`, asserts per `contracts/round-engine.openapi.yaml`)
- [ ] T017 [P] [US1] Domain unit tests for `Game.StartRound` with random+non-repeated+filters in `tests/OroQuizClash.Domain.Tests/Games/RoundSelectionTests.cs` (Arrange `Game` IN_PROGRESS with `MinRounds=5`, bank 10 PUBLISHED 5 no, Act `StartRound(selector stub, Linear)` with `Previous=[Q1]`, Assert `QuestionId` not in Previous and Category/Difficulty match; second call excludes Q1; bank agotado → NoAvailableQuestion)
- [ ] T018 [P] [US1] Strategy unit tests for `RandomQuestionSelectionStrategy` vs `DifficultyAware` with `Category/Difficulty/Academic/Age` + `Previous` in `tests/OroQuizClash.Application.Tests/Features/Games/QuestionSelectionTests.cs` (NSubstitute `IRepository<Question,QuestionId>` mock returns 10, assert `SelectAsync` random not correlated, Difficulty filter 100%)
- [ ] T019 [P] [US1] Concurrency test for duplicate `StartRound` with stale `RowVersion` in `tests/OroQuizClash.Infrastructure.Tests/Persistence/RoundStartConcurrencyTests.cs` (two `OroQuizClashDbContext` instances, second `SaveChangesAsync` → `DbUpdateConcurrencyException` →409 `RoundAlreadyInProgress`)

### Implementation for User Story 1

- [ ] T020 [P] [US1] Implement `IBusinessRule` classes `RoundAlreadyInProgressRule`, `PreviousQuestionNotRepeatedRule`, `CategoryMustMatchRule`, `DifficultyMustMatchRule` in `src/OroQuizClash.Domain/Games/Rules/` (one file per rule, e.g., `PreviousQuestionNotRepeatedRule.cs`)
- [x] T021 [US1] Implement `Game.StartRound` with random selection wiring in `src/OroQuizClash.Domain/Games/Game.cs` (validate `Status==IN_PROGRESS` or `ROUND_COMPLETED` + no active `CurrentRound`, `Rounds.Count < MaxRounds`, `RoundNumber = Rounds.Count+1`, `Difficulty = difficultyStrategy.NextDifficulty(this, completedRounds)`, `TimeLimit = Configuration.TimeLimitPerQuestionSeconds`, build `QuestionSelectionCriteria` with `CategoryId, Difficulty, AcademicLevel, AgeRange, PreviousQuestionIds, GameId, RoundNumber`, call `selector.SelectAsync(criteria)` → on `Failure` return `NoAvailableQuestion` without creating round, on success create `GameRound` with 5 fields, set `Game.Status=ROUND_IN_PROGRESS`, `RaiseDomainEvent(new RoundStartedDomainEvent(Id.Value, roundId.Value, roundNumber, questionId.Value))`) (depends on T004-T009)
- [x] T022 [US1] Implement Vertical Slice `StartRound` in `src/OroQuizClash.Application/Features/Games/StartRound.cs` with `StartRoundCommand(GameId) : ICommand<Result<GameRoundResponse>>`, `StartRoundValidator` (GameId required), `StartRoundHandler` (`IRepository<Game,GameId>.GetByIdAsync` with `Include(Rounds)`, `IQuestionSelectionStrategy`, `IDifficultyProgressionStrategy`, `game.StartRound(selector, difficultyStrategy)`, `IUnitOfWork.SaveChangesAsync` handling `DbUpdateConcurrencyException`→`ConcurrencyConflict`), `StartRoundEndpoint : IEndpoint` (`POST /api/games/{id}/rounds/start` `AdminOrGameManager`)
- [x] T023 [P] [US1] Ensure `IQuestionSelectionStrategy` already registered from 003 (`RandomQuestionSelectionStrategy` default) is reused in `src/OroQuizClash.Api/Program.cs` (verify `AddScoped<IQuestionSelectionStrategy, RandomQuestionSelectionStrategy>` present)

**Checkpoint**: US1 fully functional — `dotnet test --filter RoundSelection` passes, `POST /api/games/{id}/rounds/start` valid→200 with 5 fields <500ms with 1k, second call excludes Previous 100%, 2 games not correlated, concurrent →409, `quickstart.md` P1 scenario green

---

## Phase 4: User Story 2 — Presentar pregunta, esperar respuestas, evaluar y calcular puntajes, completar ronda (Priority: P1)

**Goal**: Tras `SelectQuestion`, `PresentQuestion` expone pregunta sin `IsCorrect` a `PLAYER`, `WaitForAnswers` ventana `TimeLimit`, `EvaluateAnswers` server-side (`IsCorrect` ledger), `CalculateScores` crea `PointTransaction`, `CompleteRound` → `ROUND_COMPLETED` bloqueando más respuestas

**Independent Test**: Iniciar ronda → `GET /api/games/{id}/rounds/{roundId}/question` como `PLAYER` retorna `Question` con 4 `AnswerOptions` sin `IsCorrect`, como `ADMIN` sí con `IsCorrect`; `SubmitAnswer` dentro `TimeLimit` con correcto→`correct=true` + `PointTransaction` ledger, incorrecto→`false`, fuera `TimeLimit`→`AnswerTimeout`, duplicado `IdempotencyKey` idempotente no duplica, `CompleteRound`→`ROUND_COMPLETED` con `CompletedAt`, luego `SubmitAnswer` bloqueado `NoActiveRound`

### Tests for User Story 2 (write FIRST)

- [ ] T024 [P] [US2] Contract tests for `GET /api/games/{id}/rounds/{roundId}/question` (PLAYER filtered vs ADMIN full) + `POST /api/games/{id}/answers` (correct vs incorrect vs timeout vs duplicate) in `tests/OroQuizClash.Api.Tests/Contracts/RoundPresentAnswerContractTests.cs` (asserts per `contracts/round-engine.openapi.yaml`, `IsCorrect` not in PLAYER payload, `AnswerTimeout` 400)
- [ ] T025 [P] [US2] Domain unit tests for `PresentQuestion` filtering + `EvaluateAnswers`/`CalculateScores` helper in `tests/OroQuizClash.Domain.Tests/Games/RoundEvaluateTests.cs` (Arrange `Question` PUBLISHED 4/1, Act `GetRoundQuestion` as PLAYER → AnswerOptions without IsCorrect, Act `SubmitAnswer` with correct/incorrect → ledger `ANSWER_CORRECT` 10 points)
- [ ] T026 [P] [US2] Application handler test for `SubmitAnswerHandler` idempotency in `tests/OroQuizClash.Application.Tests/Features/Games/SubmitAnswerHandlerTests.cs` (NSubstitute `IRepository<Game,GameId>` + `IRepository<Question,QuestionId>`, send duplicate `IdempotencyKey` → second returns same without second `PointTransaction`)
- [ ] T027 [P] [US2] Contract test for `POST /api/games/{id}/rounds/{roundId}/complete` success/failure in `tests/OroQuizClash.Api.Tests/Contracts/RoundCompleteContractTests.cs` (asserts `ROUND_IN_PROGRESS→ROUND_COMPLETED` 200, `NoActiveRound` 400)

### Implementation for User Story 2

- [ ] T028 [US2] Implement Vertical Slice `GetRoundQuestion` in `src/OroQuizClash.Application/Features/Games/GetRoundQuestion.cs` with `GetRoundQuestionQuery(GameId, RoundId) : IQuery<Result<PresentQuestionResponse>>`, `GetRoundQuestionHandler` (`IRepository<Game,GameId>.FirstOrDefaultAsync(GameByIdSpecification)` with `Include(Rounds)` + `IRepository<Question,QuestionId>.GetByIdAsync`, map `Question` to `PresentQuestionResponse` filtering `IsCorrect` if `User.IsInRole("PLAYER")` → only `Id/Text/DisplayOrder`, else include `IsCorrect`), `GetRoundQuestionEndpoint : IEndpoint` (`GET /api/games/{id}/rounds/{roundId}/question` `RequireAuthorization`)
- [ ] T029 [US2] Implement `CompleteRound` already exists from 004 — verify it sets `Status=ROUND_COMPLETED`, `CompletedAt=UtcNow`, raises `RoundCompletedDomainEvent` in `src/OroQuizClash.Domain/Games/Game.cs` (ensure `Game.CompleteRound(RoundId)` validates `Status==ROUND_IN_PROGRESS` and `Round.Status==ROUND_IN_PROGRESS`)
- [ ] T030 [US2] Ensure `SubmitAnswer` slice already exists from 004 — verify it checks `game.CanSubmitAnswer()` (`Status==ROUND_IN_PROGRESS`), validates `ServerTimestamp - Round.StartedAt ≤ TimeLimit` else `AnswerTimeout`, compares `AnswerOption.IsCorrect` server-side, creates `PointTransaction` ledger (`Type=ANSWER_CORRECT/ANSWER_INCORRECT`, `Points=PointsPerRound` adjusted by `Difficulty` if `ScoringSystem` defines), `IdempotencyKey` check (`PlayerId+RoundId`) in `src/OroQuizClash.Application/Features/Games/SubmitAnswer.cs` (no need to recreate, just verify filtering and ledger)
- [ ] T031 [US2] Add `PointTransaction` ledger wiring if missing: ensure `Score`/`PointTransaction` aggregate or `Game` creates `PointTransaction` via `IUnitOfWork` in `src/OroQuizClash.Domain/Games/Game.cs` or via `SubmitAnswerHandler` (if not already, add `PointTransaction` entity with `GameId, PlayerId, RoundId, QuestionId, Type, Points, CreatedAt` and `EfRepository` wiring)

**Checkpoint**: US2 green — `GET /rounds/{roundId}/question` as PLAYER without `IsCorrect` 100%, as ADMIN with `IsCorrect` 100%, `SubmitAnswer` correct→`correct=true` + ledger 10 points, incorrect→`false`, timeout→400, duplicate idempotent no double, `CompleteRound`→`ROUND_COMPLETED` + `CompletedAt`, `quickstart.md` P1 present/evaluate green

---

## Phase 5: User Story 3 — Progresión de dificultad configurable por ronda (Priority: P1)

**Goal**: `Round.Difficulty` incrementa por ronda según `IDifficultyProgressionStrategy` configurable (`Linear` 1→2→3→4→5 default, `Progressive` 1,1,2,3,5, `Adaptive` desempeño, `CategorySpecific`) con clamp 1..5, sin exponer `IncreaseDifficulty` como endpoint separado

**Independent Test**: Configurar `Game` con `MinRounds=5, MaxRounds=5, InitialDifficulty=1, Linear`. Iniciar 5 rondas `Start→Complete` loop y verificar `GameRound.Difficulty` == 1,2,3,4,5 y cada `SelectQuestion` filtró por esa dificultad; cambiar a `Progressive` y verificar secuencia 1,1,2,3,5 sin cambiar contrato `StartRound`; `InitialDifficulty=5` Linear → Round1 5, Round2 5 (clamp no 6)

### Tests for User Story 3 (write FIRST)

- [ ] T032 [P] [US3] Domain unit tests for `LinearDifficultyStrategy`, `Progressive`, `Adaptive` in `tests/OroQuizClash.Domain.Tests/Games/DifficultyProgressionTests.cs` (Arrange `Game` MinRounds=5 Initial=1, Act `NextDifficulty(completed=0)`→1, completed=1→2, ... completed=4→5; clamp 5+1→5; Progressive 1,1,2,3,5; Adaptive with mock PointTransaction avg)
- [ ] T033 [P] [US3] Contract tests for progression `POST /api/games/{id}/rounds/start` ×5 verifying `difficulty` field in `tests/OroQuizClash.Api.Tests/Contracts/RoundProgressionContractTests.cs` (asserts `GameRound.Difficulty` sequence matches strategy, `Difficulty` 1..5 only, clamp at 5)
- [ ] T034 [P] [US3] Integration test for strategy interchangeability in `tests/OroQuizClash.Application.Tests/Features/Games/RoundProgressionHandlerTests.cs` (NSubstitute `IDifficultyProgressionStrategy` mock returns 3 for round 2, assert `StartRound` uses mock without changing flow, verify `Game.Rounds` count+1)

### Implementation for User Story 3

- [ ] T035 [US3] Ensure `LinearDifficultyStrategy` correctly implements `NextDifficulty` with clamp in `src/OroQuizClash.Domain/Games/Strategies/LinearDifficultyStrategy.cs` (already from T007, verify `return DifficultyLevel.FromId(Math.Clamp(InitialDifficulty + completedRounds, 1, 5))`)
- [ ] T036 [P] [US3] Verify `ProgressiveDifficultyStrategy` and `AdaptiveDifficultyStrategy` implement `IDifficultyProgressionStrategy` with correct curves and `AddScoped<IDifficultyProgressionStrategy>` registration already in `Program.cs` (T012) — if missing, add `CategorySpecificDifficultyStrategy` in `src/OroQuizClash.Domain/Games/Strategies/CategorySpecificDifficultyStrategy.cs` (maps `CategoryId` to curve)
- [ ] T037 [US3] Wire `IncreaseDifficulty` as calculation inside `Game.StartRound` (not separate endpoint): ensure `Game.StartRound` calls `difficultyStrategy.NextDifficulty(this, _rounds.Count(r=>r.Status==ROUND_COMPLETED))` before creating `GameRound` and that `Round.Difficulty` is set to that value, verify no `POST /api/games/{id}/rounds/{roundId}/increase-difficulty` endpoint exists (intentionally not created)

**Checkpoint**: US3 green — `Linear` 1→2→3→4→5 with Initial=1 Min=5 100%, `Progressive` 1,1,2,3,5 100%, `Adaptive` based on score 100%, clamp 5→5 100%, strategy change does not break `StartRound` contract, `quickstart.md` P1 progression green

---

## Phase 6: User Story 4 — Invariantes de ronda y flujo completo de 8 pasos (Priority: P2)

**Goal**: Cada `GameRound` tiene 5 campos no nulos (`RoundNumber` único sin huecos, `Difficulty` 1..5, `QuestionId` PUBLISHED, `TimeLimit` 5–300, `Status`), `MinRounds≥5` gate para `Finish`, y flujo 8 pasos auditable `StartRound→SelectQuestion→PresentQuestion→WaitForAnswers→EvaluateAnswers→CalculateScores→CompleteRound→IncreaseDifficulty` como orquestación transaccional

**Independent Test**: Crear juego `MinRounds=5`, generar 5 rondas completas con 8 pasos, verificar cada `GameRound` 5 campos no nulos, `RoundNumber` 1..5 sin duplicados `UNIQUE (GameId,RoundNumber)`, `TimeLimit` == `GameConfiguration.TimeLimitPerQuestion`, `Status` `ROUND_IN_PROGRESS→ROUND_COMPLETED` en `CompleteRound`, audit `RoundStarted/RoundCompleted` con `RoundId`, intentar `FinishGame` con 3 rondas →400 `NotEnoughRounds`, concurrent `StartRound` →409

### Tests for User Story 4 (write FIRST)

- [ ] T038 [P] [US4] Domain unit tests for 5 fields invariants + `MinRounds≥5` gate + flow 8 steps in `tests/OroQuizClash.Domain.Tests/Games/RoundInvariantsTests.cs` (Arrange `Game` Min=5, Act `Create` with Min=4 → Failure `MinRoundsTooLow`; Act `StartRound` → Assert 5 fields not null; `RoundNumber` unique; `Finish` with 3 completed → Failure `NotEnoughRounds`)
- [ ] T039 [P] [US4] Contract tests for `GET /api/games/{id}/rounds` list with 5 fields + `POST /api/games/{id}/finish` gate `MinRounds` in `tests/OroQuizClash.Api.Tests/Contracts/RoundInvariantsContractTests.cs` (asserts `GET /rounds` returns 5 campos per round, `Finish` with 3→400, with 5→200 `FINISHED`)
- [ ] T040 [P] [US4] Concurrency test for `StartRound` duplicate `RoundNumber` via `UNIQUE (GameId,RoundNumber)` in `tests/OroQuizClash.Infrastructure.Tests/Persistence/RoundNumberConcurrencyTests.cs` (two `GameRound` with same `GameId,RoundNumber` → second `DbUpdateException` →409)
- [ ] T041 [P] [US4] Specification test for `GameByIdSpecification` with `Include(Rounds)` + `Include(Players)` rehydration in `tests/OroQuizClash.Infrastructure.Tests/Specifications/GameRehydrationSpecificationTests.cs` (seed `Game` with 5 rounds, assert `FirstOrDefaultAsync` returns `Rounds.Count==5` with `AsNoTracking` false for write)

### Implementation for User Story 4

- [ ] T042 [US4] Verify `Game.Finish()` already enforces `completedRounds≥MinRounds` gate (from 004) — ensure it checks `Rounds.Count(r=>r.Status==ROUND_COMPLETED) < Configuration.MinRounds` → `Failure(NotEnoughRounds)` in `src/OroQuizClash.Domain/Games/Game.cs` (add check if missing)
- [ ] T043 [US4] Ensure `GameRound` 5 fields are `IsRequired()` in `GameRoundTypeConfiguration` and `UNIQUE (GameId,RoundNumber)` + `RowVersion` on `Game` protects flow 8 steps transactionally in `src/OroQuizClash.Infrastructure/Persistence/Configurations/GameRoundTypeConfiguration.cs` (verify `Property(RoundNumber).IsRequired()`, `Property(Difficulty).IsRequired()`, `Property(QuestionId).IsRequired()`, `Property(TimeLimit).IsRequired()`, `Property(Status).IsRequired()`)
- [ ] T044 [US4] Add audit logging for 8 steps via `GameLifecycleAuditWriter` or inline `ILogger` in handlers `StartRound`, `GetRoundQuestion`, `SubmitAnswer`, `CompleteRound` with `CorrelationId/GameId/RoundId/RoundNumber/QuestionId/Difficulty/TimeLimit/FromStatus/ToStatus` in `src/OroQuizClash.Application/Features/Games/StartRound.cs` and `CompleteRound.cs` (ensure `LoggingBehavior` already via `BuildingBlocks.CQRS`)
- [ ] T045 [US4] Verify `GetRounds` query `GET /api/games/{id}/rounds` returns paginated `GameRoundResponse` with 5 fields via `GameByIdSpecification` + `ApplyAsNoTracking` in `src/OroQuizClash.Application/Features/Games/GetRoundQuestion.cs` or `GetRounds.cs` (ensure `GetRounds` already via `GetGame` → `Game.Rounds`, or create `GetRounds` slice)

**Checkpoint**: US4 green — `MinRounds<5` creation 100% 400, each round 5 fields not null + unique `RoundNumber` 1..5 100%, `TimeLimit` copied 30s 100%, `Status` transitions `ROUND_IN_PROGRESS→ROUND_COMPLETED` 100%, `Finish` with 3→400 vs 5→200, concurrent `StartRound` duplicate→409, `quickstart.md` P2 invariants green

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Hardening, docs, quickstart validation, DoD checklist, performance & security

- [ ] T046 [P] Add `ProblemDetails` mapping tests for all `GameErrors` Round codes (`MinRoundsTooLow`, `DuplicateRoundNumber`, `DuplicateQuestion`, `CategoryMismatch`, `DifficultyMismatch`, `NoAvailableQuestion`, `RoundAlreadyInProgress`, `InvalidRoundFields`, `ConcurrencyConflict`) in `tests/OroQuizClash.Api.Tests/Errors/RoundEngineErrorsMappingTests.cs` (assert `400` vs `409` per `GlobalExceptionHandler` → `Result.ToHttpResult()`)
- [ ] T047 [P] Run `quickstart.md` end-to-end validation (`Create Min5→StartRound×5 with 5 fields→PresentQuestion PLAYER filtered→SubmitAnswer correct/incorrect/timeout/idempotent→CompleteRound×5→Finish with MinRounds gate→concurrencia 409→progression Linear 1→5`) and fix gaps in `specs/005-round-engine/quickstart.md` (execute curl scenarios, confirm <1s Create, <500ms StartRound with 1k, 0% duplicate, 0% fuera categoría/dificultad)
- [ ] T048 [P] Add structured logging fields `GameId/RoundId/RoundNumber/QuestionId/Difficulty/TimeLimit/FromStatus/ToStatus/Command/Duration` via `LoggingBehavior` verification in `tests/OroQuizClash.Application.Tests/Pipeline/RoundEngineLoggingBehaviorTests.cs` (uses `ILogger` NSubstitute, asserts `LogInformation` with `RoundId`)
- [ ] T049 [P] Update `docs/adr/ADR-014-round-engine.md` documenting decisions from `research.md` (5 campos, 8 pasos, impredecible `ORDER BY NEWID()`, `UNIQUE (GameId,RoundNumber)`, `Linear` default + 3 strategies)
- [ ] T050 [P] Update `docs/adr/ADR-008-question-selection-strategy.md` if needed to reference `PreviousQuestionIds` exclusión already covered (provide reference, no duplicate)
- [ ] T051 Security hardening: verify `POST /rounds/start`/`POST /rounds/{id}/complete` require `ADMIN/GAME_MANAGER`, `GET /rounds/{id}/question` requires `PLAYER` (filtered) vs `ADMIN` (full), `POST /answers` requires `PLAYER` (JWT `sub`), rate limiting via `BuildingBlocks.ServiceDefaults`, `correlationId` propagation, no `IsCorrect` leak to `PLAYER` in `src/OroQuizClash.Application/Features/Games/GetRoundQuestion.cs` (ensure `[Authorize(Policy="Player")]` vs `AdminOrGameManager`)
- [ ] T052 Performance smoke: `dotnet test --filter SC-002` timing assert `StartRound` <500ms with 1k seeded, selection distribution aleatoria p-value, `GET` rounds <200ms in `tests/OroQuizClash.Api.Tests/Performance/RoundEnginePerformanceTests.cs` (measures `StartRoundHandler` + `RandomQuestionSelectionStrategy` + `GetRoundQuestion` with 1k)
- [ ] T053 Add audit append-only verification for 8 steps (StartRound/SelectQuestion/PresentQuestion/WaitForAnswers/EvaluateAnswers/CalculateScores/CompleteRound/IncreaseDifficulty) with `CorrelationId/PerformedBy sub` in `tests/OroQuizClash.Infrastructure.Tests/Audit/RoundEngineAuditTests.cs` (assert after `SaveChanges` audit row exists, OTel `TraceId` logged, `OutboxMessages` for `RoundCompleted`)
- [ ] T054 Final `dotnet build OroQuizClash.slnx && dotnet test` green, `dotnet format` clean, update `specs/005-round-engine/spec.md` Status to `Ready for Review` and sync `specs/005-round-engine/checklists/requirements.md` (re-run quality checklist, confirm SC-001..009)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately (reuses `001`+`004` `Game` aggregate)
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (5 fields, `IDifficultyProgressionStrategy`, `GameRound` EF `UNIQUE`+`RowVersion`)
- **User Stories (Phase 3+)**: Depend on Foundational
  - **US1 (P1) StartRound+Select impredecible**: No dependencies on other stories — delivers MVP (primera ronda con 5 campos + no repetida + filtros)
  - **US2 (P1) Present+Wait+Evaluate+Complete**: Depends on US1's `Game.StartRound` (needs `ROUND_IN_PROGRESS` + `QuestionId` PUBLISHED) — adds ciclo completo de 1 ronda, independently testable after US1 (Present filtered → Complete)
  - **US3 (P1) Progresión Linear/Progressive**: Depends on US1's `Game.StartRound` + `IDifficultyProgressionStrategy` — adds `Difficulty` 1→5 sin cambiar flujo, independently testable (create 5 rounds and check difficulty sequence)
  - **US4 (P2) Invariantes 5 campos + flujo 8 pasos + MinRounds≥5**: Depends on US1's `GameRound` 5 fields + US2's `CompleteRound` + US3's progresión — adds gate `Finish` con `MinRounds` y `UNIQUE` audit, independently testable (create Min4 → Failure, 5 rounds → Finish success)
- **Polish (Final Phase)**: Depends on all desired stories complete (US1+US2 for MVP 1 ronda, US3 for progresión, US4 for integridad)

### User Story Dependencies

- **US1 (P1)**: Can start after Foundational — no deps on other stories; delivers MVP (StartRound con 5 campos + selección no repetida)
- **US2 (P1)**: Depends on US1's `GameRound` (needs `ROUND_IN_PROGRESS` + `QuestionId`) and `SubmitAnswer` ledger — adds `PresentQuestion` filtered + `CompleteRound`; independently testable after US1
- **US3 (P1)**: Depends on US1's `StartRound` + `IDifficultyProgressionStrategy` — adds `Linear` 1→5 sin cambiar flujo; independently testable after US1 (create 5 rounds, check difficulty)
- **US4 (P2)**: Depends on US1's `GameRound` 5 fields + US2's `CompleteRound` + US3's progresión — adds `MinRounds≥5` gate + `UNIQUE` audit; independently testable (Min4 → Failure, 5→Finish)

### Within Each User Story

- Tests FIRST → FAIL → Implementation → PASS (TDD per constitution Testing Strategy)
- Models/Strategies → Rules → Aggregate (`Game.StartRound` + `CompleteRound`) → EF Config (`GameRoundTypeConfiguration` + `UNIQUE`) → Slice (Validator → Handler → Endpoint) → Specification (`GameByIdSpecification` with `Include(Rounds)`) → Integration tests (`rowversion` + `UNIQUE` concurrency)
- `IQuestionSelectionStrategy` stub seeded (10 PUBLISHED with/without filters) before `StartRound` tests; `IDifficultyProgressionStrategy` mock returns 2 for round 2 before progression tests; `rowversion` concurrency tests with two `DbContext` instances
- Verify `Result.Failure` codes map to `ProblemDetails` (400/409) after each story

### Parallel Opportunities

- Phase 2: T004+T005 (GameRound verification) parallel; T006+T007+T008 (IDifficultyProgressionStrategy + Linear + Progressive) parallel; T010+T011 (GameRound Config + Game Config) parallel; T013 errors + T014 events parallel
- Phase 3: T016+T017+T018+T019 (contract/domain/strategy/concurrency tests) parallel; T020 Rule classes parallel; T023 GetRounds slices parallel
- Phase 4: T024+T025+T026+T027 (contract/domain/handler/round tests) parallel; T028 GetRoundQuestion parallel with T030 SubmitAnswer (different files)
- Phase 5: T032+T033+T034 (domain/contract/integration tests) parallel; T035+T036 strategies parallel
- Phase 6: T038+T039+T040+T041 (domain/contract/concurrency/spec tests) parallel
- Phase 7: T046+T047+T048+T049+T050 (errors/quickstart/logging/ADRs/security) parallel; T051 security parallel with T052 perf
- Different user stories can be worked on in parallel by different developers after Foundational if `Game.cs` is shared (coordinate merges on `Game.StartRound` changes)

### Parallel Example: User Story 1 (StartRound Select)

```bash
# Tests in parallel (different files):
Task T016: Contract test in tests/OroQuizClash.Api.Tests/Contracts/RoundStartContractTests.cs
Task T017: Domain unit tests in tests/OroQuizClash.Domain.Tests/Games/RoundSelectionTests.cs
Task T018: Strategy unit tests in tests/OroQuizClash.Application.Tests/Features/Games/QuestionSelectionTests.cs
Task T019: Concurrency test in tests/OroQuizClash.Infrastructure.Tests/Persistence/RoundStartConcurrencyTests.cs

# Models/Strategies in parallel:
Task T006: Strategy in src/OroQuizClash.Domain/Games/Strategies/IDifficultyProgressionStrategy.cs
Task T007: Linear in src/OroQuizClash.Domain/Games/Strategies/LinearDifficultyStrategy.cs
Task T022: Slice StartRound in src/OroQuizClash.Application/Features/Games/StartRound.cs
```

### Parallel Example: User Story 3 (Progression)

```bash
# Tests in parallel:
Task T032: Domain in tests/OroQuizClash.Domain.Tests/Games/DifficultyProgressionTests.cs
Task T033: Contract in tests/OroQuizClash.Api.Tests/Contracts/RoundProgressionContractTests.cs
Task T034: Integration in tests/OroQuizClash.Application.Tests/Features/Games/RoundProgressionHandlerTests.cs

# Strategies in parallel:
Task T007: Linear in src/OroQuizClash.Domain/Games/Strategies/LinearDifficultyStrategy.cs
Task T008: Progressive in src/OroQuizClash.Domain/Games/Strategies/ProgressiveDifficultyStrategy.cs
Task T036: Wire in src/OroQuizClash.Api/Program.cs (IDifficultyProgressionStrategy)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup + Phase 2: Foundational (GameRound 5 fields, `IDifficultyProgressionStrategy` Linear, `GameRound` EF `UNIQUE`+`RowVersion`)
2. Complete Phase 3: US1 (`StartRound` with 5 fields + impredecible + no repetida + Category/Difficulty/Academic/Age) — delivers MVP: primera ronda jugable con 5 campos
3. **STOP and VALIDATE**: `dotnet test --filter RoundSelection` + `quickstart.md` P1 StartRound (valid→200 with 5 fields <500ms, second excludes Previous 100%, concurrent→409, distribution aleatoria) + `GET /api/games/{id}` with Previous
4. Deploy/demo if ready — `POST /api/games/{id}/rounds/start` sin PresentQuestion aún

### Incremental Delivery

1. Setup + Foundational → foundation ready (`GameRound` 5 fields, `Linear` + `UNIQUE`, `IQuestionSelectionStrategy` reused)
2. Add US1 → Test StartRound impredecible no repetida → Demo MVP (1 ronda con 5 campos)
3. Add US2 → Test Present filtered + SubmitAnswer idempotente + CompleteRound → Demo ciclo 8 pasos (1 ronda completa)
4. Add US3 → Test Linear 1→5 + Progressive/Adaptive → Demo progresión configurable
5. Add US4 → Test MinRounds≥5 gate + UNIQUE audit + flujo 8 pasos → Demo integridad
6. Polish → quickstart E2E 5 rounds Linear 1→5 + perf (<500ms) + security (`PLAYER` filtrado) + ADRs + audit + `dotnet build && dotnet test` green

### Parallel Team Strategy

With multiple developers after Foundational:
- Developer A: US1 (`StartRound` + selección no repetida)
- Developer B: US2 (`GetRoundQuestion` + `SubmitAnswer` + `CompleteRound`) — after A's `Game.StartRound` merges (needs `ROUND_IN_PROGRESS`)
- Developer C: US3 (`Linear/Progressive/Adaptive` + progression) — after A's `Game.StartRound` + `IDifficultyProgressionStrategy`
- Developer D: US4 (invariantes 5 campos + `Finish` MinRounds gate + `UNIQUE` audit) — after A's `GameRound` 5 fields + B's `CompleteRound`
- All stories integrate via same `Game` aggregate without conflicts if `Game.cs` changes are coordinated (rules → aggregate → slice order; use feature branch review for `Game.StartRound` merges)

---

## Notes

- [P] tasks = different files, no dependencies — can run in parallel
- [Story] label maps task to specific user story for traceability to `spec.md` US1..US4
- Each user story independently completable and testable via `quickstart.md` curl + `dotnet test --filter USx`
- Verify tests FAIL before implementation (TDD), commit after each task or logical group
- QST/FR traced: MinRounds≥5→T004/T013/T038/T042 (Create gate), 5 campos→T004/T010/T013/T023, 8 pasos→T009/T021/T029/T044, impredecible→T016/T021/T052 (ORDER BY NEWID()), no repetida→T016/T021/T040, Category→T016/T021/T046, Difficulty→T016/T021/T033, Academic/Age→T016/T021, Linear 1→5→T006/T007/T032/T035, IncreaseDifficulty→T037/T044
- Avoid: vague tasks, same-file conflicts, cross-story dependencies that break independence; prefer explicit file paths per task
- Constitution gates: Domain First (T004 Rules in Domain), Clean Arch (T006 ports), BuildingBlocks reuse (T013 errors, T010 RowVersion), Vertical Slice (T022 slices), Authoritative (T021 server-side random + T028 filtering), OroIdentityServer JWT (T016 filtered IsCorrect), A (5 campos + rowversion T009/T010), B (MinRounds≥5 T004), C (configurable Linear T006), E/F (rowversion + Specification T010/T013), G (Outbox T044), H (delegated T051), I/F (validation 3-level + idempotency T026/T041)

