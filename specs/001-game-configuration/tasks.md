# Tasks: Game Configuration

**Input**: Design documents from `/specs/001-game-configuration/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/create-game.openapi.yaml, quickstart.md
**Feature Branch**: `001-game-configuration` | **Constitution**: v1.1.0 (I-VI)

**Organization**: Tasks grouped by user story — each story independently testable and deliverable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Exact file paths required; `src/` prefix per plan.md modular monolith

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Scaffold modular monolith projects and wire BuildingBlocks — no domain logic yet

- [x] T001 Create modular monolith solution structure per plan.md (`src/OroQuizClash.Domain`, `src/OroQuizClash.Application`, `src/OroQuizClash.Infrastructure`, `src/OroQuizClash.Api`, `tests/OroQuizClash.*`) and add to `OroQuizClash.slnx` in `OroQuizClash.slnx`
- [x] T002 Initialize `src/OroQuizClash.Domain/OroQuizClash.Domain.csproj` (net10.0) with ProjectReference to `src/BuildingBlocks/BuildingBlocks.Kernel.Domain/BuildingBlocks.Kernel.Domain.csproj` in `src/OroQuizClash.Domain/OroQuizClash.Domain.csproj`
- [x] T003 Initialize `src/OroQuizClash.Application/OroQuizClash.Application.csproj` with references to `OroQuizClash.Domain` and `src/BuildingBlocks/BuildingBlocks.CQRS/BuildingBlocks.CQRS.csproj` in `src/OroQuizClash.Application/OroQuizClash.Application.csproj`
- [x] T004 Initialize `src/OroQuizClash.Infrastructure/OroQuizClash.Infrastructure.csproj` with references to `OroQuizClash.Domain`, `src/BuildingBlocks/BuildingBlocks.Kernel.Infrastructure/BuildingBlocks.Kernel.Infrastructure.csproj`, `src/BuildingBlocks/BuildingBlocks.EventBus.RabbitMQ/BuildingBlocks.EventBus.RabbitMQ.csproj` in `src/OroQuizClash.Infrastructure/OroQuizClash.Infrastructure.csproj`
- [x] T005 Initialize `src/OroQuizClash.Api/OroQuizClash.Api.csproj` (web) with references to `OroQuizClash.Application`, `OroQuizClash.Infrastructure`, `src/BuildingBlocks/BuildingBlocks.ServiceDefaults/BuildingBlocks.ServiceDefaults.csproj`, `Microsoft.AspNetCore.Authentication.JwtBearer` in `src/OroQuizClash.Api/OroQuizClash.Api.csproj`
- [x] T006 Create test projects `tests/OroQuizClash.Domain.Tests/OroQuizClash.Domain.Tests.csproj`, `tests/OroQuizClash.Application.Tests/OroQuizClash.Application.Tests.csproj`, `tests/OroQuizClash.Infrastructure.Tests/OroQuizClash.Infrastructure.Tests.csproj`, `tests/OroQuizClash.Api.Tests/OroQuizClash.Api.Tests.csproj`, `tests/OroQuizClash.Architecture.Tests/OroQuizClash.Architecture.Tests.csproj` with xUnit v3 + NSubstitute + Testcontainers/Aspire references in `tests/` (one task)
- [x] T007 Configure `Directory.Packages.props` central versions already present; verify `TargetFramework net10.0` and `Nullable enable` in new csprojs in `Directory.Build.props`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story — CQRS pipeline, persistence, auth, error handling

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T008 Implement `src/OroQuizClash.Infrastructure/Persistence/OroQuizClashDbContext.cs` deriving from `AppDbContextBase` with `DbSet<Game>` and `ApplyConfiguration(new OutboxEntityTypeConfiguration())` in `src/OroQuizClash.Infrastructure/Persistence/OroQuizClashDbContext.cs`
- [x] T009 Configure `src/OroQuizClash.Api/Program.cs` to call `AddServiceDefaults()`, `AddCqrs(c => c.RegisterHandlersFromAssemblyContaining<Program>().AddOpenBehavior(typeof(LoggingBehavior<,>)).AddOpenBehavior(typeof(ValidationBehavior<,>)))`, `AddDbContext<OroQuizClashDbContext>()`, `AddUnitOfWork<OroQuizClashDbContext>()`, `AddOutbox<OroQuizClashDbContext>()`, `AddEndpoints()`, `AddExceptionHandler<GlobalExceptionHandler>()` + `MapDefaultEndpoints()`/`MapEndpoints()` in `src/OroQuizClash.Api/Program.cs`
- [x] T010 Configure JWT bearer authentication in `src/OroQuizClash.Api/Program.cs` against OroIdentityServer discovery (`Authority=http://identity:5080`, `RequireHttpsMetadata=false`, `MapInboundClaims=false`) and authorization policies `RequireAdminOrGameManager` (claim `roles` contains `ADMIN`/`GAME_MANAGER`) in `src/OroQuizClash.Api/Program.cs`
- [x] T011 Update `OroQuizClash.AppHost/AppHost.cs` to orchestrate SQL Server (or PostgreSQL for local) + `OroQuizClash.Api` + Podman container `oroidentityserver:latest` (`AddContainer("identity-server","oroidentityserver:latest").WithEndpoint(5080).WithEnvironment("SymmetricSecurityKey",...)`) + volume `identity-dp-keys` in `OroQuizClash.AppHost/AppHost.cs`
- [x] T012 Create placeholder `src/OroQuizClash.Domain/Shared/Errors/GameErrors.cs` with `Error` factory helpers (`InvalidGameConfiguration`, `CategoryNotReady`, `InvalidGameState`, etc.) using `BuildingBlocks.Kernel.Domain.Result/Error` in `src/OroQuizClash.Domain/Shared/Errors/GameErrors.cs`
- [x] T013 Add architecture test baseline in `tests/OroQuizClash.Architecture.Tests/DomainDependenciesTests.cs` asserting Domain does not reference `Microsoft.AspNetCore`, `Microsoft.EntityFrameworkCore`, `RabbitMQ.Client`, `MediatR`, `MassTransit`, `AutoMapper` in `tests/OroQuizClash.Architecture.Tests/DomainDependenciesTests.cs`

**Checkpoint**: Foundation ready — `dotnet build OroQuizClash.slnx` passes, `dotnet test --filter Architecture` passes, AppHost can start with identity discovery reachable

---

## Phase 3: User Story 1 — Crear juego con configuración válida (Priority: P1) 🎯 MVP

**Goal**: Administrador crea un `Game` solo si los 12 campos de configuración son válidos (CFG-001,002,004,005,006,007); retorna `201` con `gameId` y persiste agregado en `DRAFT/READY`.

**Independent Test**: `POST /api/games` con payload válido (minRondas=5, categoría Published, estrategia, 30s, políticas) → `201` + `Location` + `GET /api/games/{id}` idéntico; payload sin nombre/categoría/minRondas=3/sin estrategia/sin tiempo/sin políticas → `400` `ProblemDetails.code` tipificado y cero juegos persistidos (SC-001, SC-002, SC-006).

### Tests for User Story 1 (write FIRST, ensure FAIL before implementation)

- [x] T014 [P] [US1] Contract test for `POST /api/games` valid/invalid payloads in `tests/OroQuizClash.Api.Tests/Contracts/CreateGameContractTests.cs` (WebApplicationFactory, JWT mock with ADMIN role, asserts 201 vs 400 per contract `contracts/create-game.openapi.yaml`)
- [x] T015 [P] [US1] Domain unit tests for `Game.Create` valid/invalid configurations in `tests/OroQuizClash.Domain.Tests/Games/GameCreateTests.cs` (Arrange/Act/Assert per CFG-001..007, minRondas 5 vs 4, time 0 vs 30, policies null)
- [x] T016 [P] [US1] Application handler test for `CreateGameHandler` with NSubstitute `IRepository<Game,GameId>` in `tests/OroQuizClash.Application.Tests/Features/Games/CreateGameHandlerTests.cs` (mocks category validation, asserts `Result.IsSuccess` vs `Error`)

### Implementation for User Story 1

- [x] T017 [P] [US1] Create `GameId` StronglyTypedId in `src/OroQuizClash.Domain/Games/GameId.cs` (`sealed record GameId(Guid Value) : StronglyTypedId<Guid>(Value)`)
- [x] T018 [P] [US1] Create enumerations `GameStatus`, `DifficultyProgressionStrategy`, `LossPolicy`, `WithdrawalPolicy`, `ConsolationPolicy`, `ScoringSystem` (Enumeration) in `src/OroQuizClash.Domain/Games/Enumerations/` (one file per enum, e.g., `LossPolicy.cs`)
- [x] T019 [P] [US1] Create ValueObjects `GameConfiguration`, `RewardRules` and `CategoryId` in `src/OroQuizClash.Domain/Games/ValueObjects/` (GameConfiguration as ValueObject with all fields, equality by value, private setters)
- [x] T020 [P] [US1] Implement `IBusinessRule` classes `MinRoundsAtLeastFiveRule`, `RoundsRangeCoherenceRule`, `TimeLimitPositiveRule`, `TimeLimitRangeRule`, `PoliciesRequiredRule`, `GameNameNotEmptyRule`, `DifficultyStrategyRequiredRule` in `src/OroQuizClash.Domain/Games/Rules/` (one file per rule)
- [x] T021 [US1] Implement `Game` AggregateRoot in `src/OroQuizClash.Domain/Games/Game.cs` with `static Result<Game> Create(GameConfiguration config)`, `RowVersion`, `Status=DRAFT`, `GameCreatedDomainEvent`, `CheckRule` calls (depends on T017-T020)
- [x] T022 [P] [US1] Create `GameCreatedDomainEvent : IDomainEvent` in `src/OroQuizClash.Domain/Games/Events/GameCreatedDomainEvent.cs`
- [x] T023 [US1] Create EF configuration `GameTypeConfiguration : IEntityTypeConfiguration<Game>` with `HasKey`, `StronglyTypedId` converter, `OwnsOne(Configuration)`, `IsRowVersion`, `HasConversion` for enumerations, indexes in `src/OroQuizClash.Infrastructure/Persistence/Configurations/GameTypeConfiguration.cs` (depends on T021)
- [x] T024 [US1] Implement Vertical Slice `CreateGame` in `src/OroQuizClash.Application/Features/Games/CreateGame.cs` containing `CreateGameCommand : ICommand<Result<CreateGameResponse>>`, `CreateGameValidator : Validator<CreateGameCommand>` (name 3-100, Guid non-empty, minRounds≥5, time 5-300, enums valid), `CreateGameHandler : ICommandHandler<CreateGameCommand,Result<CreateGameResponse>>` (loads category via `IRepository<Category,CategoryId>`/stub, calls `Game.Create`, `AddAsync`, `SaveChangesAsync`), `CreateGameResponse` DTO, `CreateGameEndpoint : IEndpoint` (thin, `ISender.SendAsync` → `ToCreatedResult`), explicit mapping (depends on T021)
- [x] T025 [US1] Add `GameByIdSpecification : Specification<Game>` in `src/OroQuizClash.Infrastructure/Specifications/GameByIdSpecification.cs` for `SC-006` reconstruction test
- [x] T026 [US1] Wire integration test for persistence `EfRepository` + `OroQuizClashDbContext` + `Specification` + `rowversion` in `tests/OroQuizClash.Infrastructure.Tests/Persistence/GamePersistenceTests.cs` (Testcontainers/Aspire, asserts owned type columns and `RowVersion` concurrency token)
- [x] T027 [US1] Add observability: ensure `CreateGameHandler` logs `CorrelationId`/`GameId`/`CategoryId` via `LoggingBehavior` and `GlobalExceptionHandler` maps `InvalidGameConfiguration` to `400` ProblemDetails with `code` in `src/OroQuizClash.Api/Program.cs` (verify existing ServiceDefaults wiring)

**Checkpoint**: US1 fully functional — `dotnet test --filter US1` passes, `POST /api/games` valid → 201 in <2s, invalid → 400 with `code` per CFG, `GET` returns identical config, `quickstart.md` P1 scenario green

---

## Phase 4: User Story 2 — Inmutabilidad de configuración tras iniciar (Priority: P1)

**Goal**: Una vez iniciado el juego (`StartGame` → `WAITING_FOR_PLAYERS`/`IN_PROGRESS`), cualquier intento de mutar configuración es rechazado con `InvalidGameState.ConfigurationImmutable` (CFG-003, SC-003).

**Independent Test**: Crear juego válido → `POST /api/games/{id}/start` → `PUT /api/games/{id}` (o intento de `UpdateConfiguration`) → `400/409` con `ConfigurationImmutable`, agregado no mutado; segundo `StartGame` concurrente con `rowversion` desalinea → `409`.

### Tests for User Story 2

- [x] T028 [P] [US2] Domain unit test for `Game.Start()` and immutability guard in `tests/OroQuizClash.Domain.Tests/Games/GameStartTests.cs` (Arrange DRAFT → Act Start → Assert Status WAITING_FOR_PLAYERS; Act UpdateConfiguration after Start → Assert Fail `ConfigurationImmutable`)
- [x] T029 [P] [US2] API integration test for `POST /api/games/{id}/start` + immutability rejection in `tests/OroQuizClash.Api.Tests/Contracts/GameStartAndImmutabilityTests.cs` (WebApplicationFactory, JWT ADMIN, asserts 200 on Start, 400 on subsequent PUT)

### Implementation for User Story 2

- [x] T030 [US2] Implement `Game.Start()` and `GuardConfigurationImmutable()` in `src/OroQuizClash.Domain/Games/Game.cs` (validates `Status ∈ {DRAFT,READY}`, transitions to `READY`/`WAITING_FOR_PLAYERS`, sets `RowVersion` check; any config setter throws `Error ConfigurationImmutable` if `Status ≥ WAITING_FOR_PLAYERS`) (extends T021)
- [x] T031 [US2] Implement Vertical Slice `StartGame` in `src/OroQuizClash.Application/Features/Games/StartGame.cs` containing `StartGameCommand(GameId) : ICommand<Result<Unit>>`, `StartGameHandler` (loads via `IRepository`, calls `Game.Start()`, `SaveChangesAsync` with optimistic concurrency), `StartGameEndpoint : IEndpoint` (`POST /api/games/{gameId}/start` with `[Authorize(Policy=AdminOrGameManager)]`)
- [x] T032 [US2] Add domain event `GameStartedDomainEvent` in `src/OroQuizClash.Domain/Games/Events/GameStartedDomainEvent.cs` and ensure dispatch via `AppDbContextBase.SaveChanges` (Outbox optional)
- [x] T033 [US2] Add concurrency test for duplicate `StartGame` with stale `RowVersion` in `tests/OroQuizClash.Infrastructure.Tests/Persistence/GameConcurrencyTests.cs` (two concurrent loads, second `SaveChanges` → `DbUpdateConcurrencyException` mapped to `409`)

**Checkpoint**: US2 green — inicio bloquea configuración, `dotnet test --filter GameStart` passes, `quickstart.md` inmutabilidad scenario green, both P1 stories independently testable

---

## Phase 5: User Story 3 — Validar dependencias de categoría y configuración de jugadores (Priority: P2)

**Goal**: Validar categoría existe y está `Published` con ≥5 preguntas válidas (CFG-004, SPEC-002/003) y coherencia `minRounds≤maxRounds`, `minPlayers 1..maxPlayers` (FR-009), rechazando `CategoryNotFound`/`CategoryNotReady`/`InvalidRange` (SC-004).

**Independent Test**: `POST /api/games` con `categoryId` inexistente/no publicada → `CategoryNotFound`/`CategoryNotReady`; con `minRounds=10 maxRounds=5` o `minPlayers=5 maxPlayers=2` o `minPlayers=0` → `400 InvalidRange`; con `rewardRules` null o `scoringSystem` vacío → `400 InvalidGameConfiguration`.

### Tests for User Story 3

- [x] T034 [P] [US3] Domain tests for `CategoryMustBeValidRule` and `PlayersRangeCoherenceRule` in `tests/OroQuizClash.Domain.Tests/Games/CategoryAndPlayersRulesTests.cs` (mocks category Published vs Draft, min>max cases)
- [x] T035 [P] [US3] Contract tests for category/players range failures in `tests/OroQuizClash.Api.Tests/Contracts/CategoryAndPlayersContractTests.cs` (asserts `400` with `code` = `CategoryNotFound`, `CategoryNotReady`, `InvalidGameConfiguration.InvalidRange`)

### Implementation for User Story 3

- [x] T036 [P] [US3] Create stub `Category` aggregate or `ICategoryValidator` abstraction in `src/OroQuizClash.Domain/Categories/CategoryId.cs` and `src/OroQuizClash.Domain/Categories/ICategoryValidator.cs` (or reuse `IRepository<Category,CategoryId>` if SPEC-002 exists) for CFG-004 validation
- [x] T037 [US3] Implement `CategoryMustBeValidRule` and `PlayersRangeCoherenceRule`/`RewardsRequiredRule` integration in `src/OroQuizClash.Domain/Games/Game.cs` `Create` (inject `ICategoryValidator` result, check `minPlayers≤maxPlayers`, `rewardRules != null`, `scoringSystem` valid) (extends T021)
- [x] T038 [US3] Extend `CreateGameValidator` in `src/OroQuizClash.Application/Features/Games/CreateGame.cs` to validate `minRounds≤maxRounds`, `minPlayers≥1 && minPlayers≤maxPlayers`, `categoryId != Empty`, `rewardRules.Type` valid (adds to T024)
- [x] T039 [US3] Add `CategoryByIdSpecification` or `CategoryPublishedSpecification` in `src/OroQuizClash.Infrastructure/Specifications/CategorySpecifications.cs` for existence/published check (used by `CreateGameHandler`)
- [x] T040 [US3] Update `CreateGameHandler` to query category via `IRepository<Category,CategoryId>` + Specification and return `Error CategoryNotReady` if `ValidQuestionsCount <5` or `Status != Published` in `src/OroQuizClash.Application/Features/Games/CreateGame.cs`

**Checkpoint**: US3 green — categorías y rangos validados, `SC-004` 100% rechazo, no regresión en US1/US2

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Hardening, docs, quickstart validation, DoD checklist

- [x] T041 [P] Add `ProblemDetails` mapping tests for all `GameErrors` codes in `tests/OroQuizClash.Api.Tests/Errors/GameErrorsMappingTests.cs`
- [x] T042 [P] Run `quickstart.md` end-to-end validation (valid + 7 invalid CFG cases + immutability + ranges) and fix gaps in `specs/001-game-configuration/quickstart.md` (execute curl scenarios, confirm <2s creation, 0% mutation post-start)
- [x] T043 [P] Add structured logging fields `GameId`/`CategoryId`/`Command`/`Duration` via `LoggingBehavior` verification in `tests/OroQuizClash.Application.Tests/Pipeline/LoggingBehaviorTests.cs`
- [x] T044 [P] Update `docs/adr/ADR-010-game-configuration.md` documenting decisions from `research.md` (ValueObject owned, Enumerations, two-level validation, Podman identity)
- [x] T045 Security hardening: verify no anonymous `POST /api/games`, rate limiting via ServiceDefaults, `correlationId` propagation, no sensitive data logged in `src/OroQuizClash.Api/Program.cs`
- [x] T046 Performance smoke: `dotnet test --filter SC-002` timing assert <2s for `CreateGame` with valid payload (95% p95) in `tests/OroQuizClash.Api.Tests/Performance/CreateGamePerformanceTests.cs`
- [x] T047 Final `dotnet build OroQuizClash.slnx && dotnet test` green, `dotnet format` clean, update `specs/001-game-configuration/spec.md` Status to `Ready for Review`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phase 3+)**: Depend on Foundational; US1 and US2 both P1 but US2 extends `Game.cs` from US1 — implement US1 first; US3 (P2) can start after US1
- **Polish (Final Phase)**: Depends on all desired stories complete

### User Story Dependencies

- **US1 (P1)**: Can start after Foundational — no dependencies on other stories; delivers MVP (agregado creable solo si válido)
- **US2 (P1)**: Depends on US1's `Game.cs` — extends immutability; independently testable after US1
- **US3 (P2)**: Depends on US1's `Game.cs`/`CreateGame` — adds category/players validation; independently testable

### Within Each User Story

- Tests FIRST → FAIL → Implementation → PASS
- ValueObjects/Enumerations → Rules → Aggregate → EF Config → Slice (Validator → Handler → Endpoint) → Persistence/Spec → Integration tests

### Parallel Opportunities

- Phase 1: T002-T006 can run in parallel after T001
- Phase 2: T008 (DbContext) + T012 (Errors) + T013 (Architecture tests) can run in parallel; T009/T010 (Api wiring) sequential after T008
- Phase 3: T014-T016 (tests) parallel; T017-T020 (VOs/Rules) parallel; T022+T025 parallel
- Phase 4: T028-T029 parallel
- Phase 5: T034-T035 parallel; T036+T039 parallel
- Phase 6: T041-T044 parallel

### Parallel Example: User Story 1

```bash
# Tests in parallel:
Task T014: Contract test in tests/OroQuizClash.Api.Tests/Contracts/CreateGameContractTests.cs
Task T015: Domain unit tests in tests/OroQuizClash.Domain.Tests/Games/GameCreateTests.cs
Task T016: Handler test in tests/OroQuizClash.Application.Tests/Features/Games/CreateGameHandlerTests.cs

# Models in parallel:
Task T017: GameId in src/OroQuizClash.Domain/Games/GameId.cs
Task T018: Enumerations in src/OroQuizClash.Domain/Games/Enumerations/
Task T019: ValueObjects in src/OroQuizClash.Domain/Games/ValueObjects/
Task T020: IBusinessRules in src/OroQuizClash.Domain/Games/Rules/
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup + Phase 2: Foundational
2. Complete Phase 3: US1 (CreateGame valid/invalid) — delivers MVP: agregado creable solo si válido
3. **STOP and VALIDATE**: `dotnet test --filter US1` + `quickstart.md` P1 curl (201 vs 400 per CFG)
4. Deploy/demo if ready — game cannot be misconfigured

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. Add US1 → Test independently → Demo MVP
3. Add US2 → Test immutability → Demo (CFG-003)
4. Add US3 → Test category/players → Demo (CFG-004 + ranges)
5. Polish → quickstart + performance + ADR + security hardening

### Parallel Team Strategy

With multiple developers after Foundational:
- Developer A: US1 (Game + CreateGame slice)
- Developer B: US2 (StartGame + immutability) — after A's Game.cs merges
- Developer C: US3 (Category/players) — after A's Game.cs merges
- All stories integrate via same `Game` aggregate without conflicts if `Game.cs` changes are coordinated

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story independently completable and testable per spec `Independent Test`
- Verify tests fail before implementing (TDD per constitution Testing Strategy)
- Commit after each task or logical group; stop at any checkpoint to validate story
- File paths are absolute per plan.md; adjust if `src/` prefix changes
- `BuildingBlocks` must not be modified; only referenced via ProjectReference
- `OroIdentityServer` is Podman container — tests use JWT mock or Testcontainers with real `oroidentityserver:latest` and `admin/Admin@123456`
