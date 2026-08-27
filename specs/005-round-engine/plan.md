# Implementation Plan: Round Engine

**Branch**: `005-round-engine` | **Date**: 2026-08-27 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/005-round-engine/spec.md`

## Summary

Extender el motor de `Game` para orquestar las 5 invariantes de ronda (RoundNumber/Difficulty/Question/TimeLimit/Status) con el flujo canónico de 8 pasos (`StartRound→SelectQuestion→PresentQuestion→WaitForAnswers→EvaluateAnswers→CalculateScores→CompleteRound→IncreaseDifficulty`) sobre el agregado `Game : AggregateRoot<GameId>` con `GameRound : Entity<GameRoundId>` composición, validando `minimumRounds≥5` (SPEC-001 gate), selección impredecible server-side vía `IQuestionSelectionStrategy` (SPEC-003) con `PreviousQuestionIds` exclusión y filtros `Category/Difficulty/AcademicLevel/AgeRange`, y progresión `IDifficultyProgressionStrategy` configurable (`Linear` 1→2→3→4→5 por defecto + `Progressive/Adaptive/CategorySpecific` intercambiables, clamp 1..5). Implementación como Vertical Slices `BuildingBlocks.CQRS` + `AppDbContextBase` + `EfRepository<Game>` + `Specification<Game/Question>` + `rowversion` + `IOutboxWriter` para `RoundStarted/Completed` audit, autenticado vía OroIdentityServer (`PLAYER` para SubmitAnswer/PresentQuestion filtrado `IsCorrect`), idempotencia `SubmitAnswer` por `IdempotencyKey`.

## Technical Context

**Language/Version**: C# 12 / .NET 10.0 (`net10.0`, `LangVersion latest`, `Nullable enable`, `ImplicitUsings enable`)

**Primary Dependencies**: `BuildingBlocks.Kernel.Domain` (AggregateRoot, Entity, ValueObject, StronglyTypedId, Enumeration, IBusinessRule, Result/Error, IRepository, Specification, IDomainEvent, DomainEvent), `BuildingBlocks.CQRS` (ICommand/IQuery, ICommandHandler/IQueryHandler, ISender, IPipelineBehavior ValidationBehavior/LoggingBehavior, IValidator/Validator), `BuildingBlocks.Kernel.Infrastructure` (AppDbContextBase, EfRepository, SpecificationEvaluator, IUnitOfWork, IOutboxWriter, OutboxEntityTypeConfiguration), `BuildingBlocks.ServiceDefaults` (OpenTelemetry, health `/health` `/alive`, Resilience, IEndpoint, Result→HTTP, GlobalExceptionHandler, ProblemDetails), `BuildingBlocks.EventBus.RabbitMQ` (opcional, OutboxProcessor → RabbitMQ), `OroIdentityServer` Podman `oroidentityserver:latest` (OpenIddict 8 JWT, Authority `http://identity:5080`), `IQuestionSelectionStrategy` (SPEC-003, `Random`/`DifficultyAware`), `IDifficultyProgressionStrategy` (Linear/Progressive/Adaptive)

**Storage**: SQL Server (primario OroQuizClash, `rowversion` + `UNIQUE (GameId, RoundNumber)` + `UNIQUE (GameId, QuestionId)` opcional, filtered indexes `Status`); PostgreSQL `identitydb` aislado solo vía OroIdentityServer (no acceso directo); EF Core 10 sobre `OroQuizClashDbContext : AppDbContextBase`; `EfRepository<Game,GameId>` + `EfRepository<Question,QuestionId>` ya existentes (003); `Specification<Game>` + `Specification<GameRound>` para rehidratación (`Include(Rounds)`), `ApplyAsNoTracking` para lectura; Oracle como target secundario vía abstracción (sin modificar Domain/Application)

**Testing**: xUnit v3 + NSubstitute + Testcontainers.MsSql (o Sqlite InMemory) + Aspire.Hosting.Testing + Microsoft.AspNetCore.Mvc.Testing (WebApplicationFactory) + coverlet; `dotnet test` con TestingPlatform; pruebas de dominio para 5 campos invariantes + `minimumRounds≥5` gate + impredecible (Guid `NEWID()` distribución) + no repetida + filtros + progresión Linear 1→5 + `IncreaseDifficulty` clamp + `rowversion` concurrencia para `StartRound`/`CompleteRound`; pruebas api para `PresentQuestion` filtrado `IsCorrect`

**Target Platform**: Linux containers (Podman `podman build`/`podman compose`), .NET Aspire 13.5.3 AppHost (SQL Server + PostgreSQL+pgAdmin, Redis, RabbitMQ, identity-server, oroclash-api), ASP.NET Core minimal APIs (IEndpoint) + SignalR futuro (notificaciones `RoundStarted/RoundCompleted` server-driven, no source of truth)

**Project Type**: web-service — modular monolith (Domain / Application / Infrastructure / Api)

**Performance Goals**: SC-001 Create con MinRounds<5 rechazo <1s p95; SC-002 creación ronda 5 campos <500ms p95; SC-003 flujo 8 pasos sin omitir 100% (audit `RoundStarted/Completed` + `PointTransaction`); SC-004 selección impredecible <500ms p95 con 1k (ORDER BY NEWID()), distribución aleatoria p-value; SC-005 0% repetida intra-juego (PreviousQuestionIds exclusión); SC-006 Category filter 100% (2×100 preguntas); SC-007 Difficulty filter 100% (1..5); SC-008 Academic/Age filter 100%; SC-009 Linear 1→5 progresión 100% por juego

**Constraints**: <200ms p95 validación pipeline; concurrencia optimista obligatoria (`rowversion` `IsRowVersion` en `Game`); `GameRound` inmutable tras creación (solo `CompleteRound` cambia `Status`→`COMPLETED`); `MinRounds≥5` (SPEC-001), `TimeLimit 5–300s` copiado no recalculado, `Difficulty 1..5` clamp, `RoundNumber` único sin huecos; solo `PUBLISHED` con 4/1 seleccionable (SPEC-003 QST-001..004); `PresentQuestion` no revela `IsCorrect` a `PLAYER` (filtra DTO); sin store local credenciales — JWT `jwks_uri`, RBAC `ADMIN/GAME_MANAGER` para ciclo + `PLAYER` para SubmitAnswer; mapeo explícito (no AutoMapper), sin MediatR/MassTransit; `IncreaseDifficulty` no es endpoint separado (cálculo para siguiente ronda)

**Scale/Scope**: Banco 100–10k preguntas (5–5000 por categoría), 10–500 categorías, 10–1k juegos concurrentes, 2–10 jugadores por juego, 5–50 rondas por juego (MinRounds 5 default, MaxRounds 10-50), 5 campos por ronda, 8 pasos por ronda, 5 niveles dificultad, 10k `PointTransaction` ledger, 100 `Game` con 5 rondas = 500 `GameRound`

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Principle | Status | Evidence / Mitigación |
|------|-----------|--------|------------------------|
| I. Domain First | 5 campos + 8 pasos + 5 reglas selección + progresión en Domain | ✅ PASS | `Game.StartRound(IQuestionSelectionStrategy, IDifficultyProgressionStrategy)` con `IBusinessRule` (`MinimumRoundsRule`, `RoundNumberUniqueRule`, `PreviousQuestionNotRepeatedRule`, `CategoryDifficultyAcademicAgeRule`), `GameRound` como `Entity` composición (`RoundNumber/Difficulty/QuestionId/TimeLimit/Status`), `IncreaseDifficulty` como `IDifficultyProgressionStrategy.NextDifficulty`; Application solo orquesta. |
| II. Clean Architecture | `Web→Application→Domain←Infrastructure`, Domain sin refs Infra/Web | ✅ PASS | Domain solo `BuildingBlocks.Kernel.Domain` + ports `IQuestionSelectionStrategy`/`IDifficultyProgressionStrategy`; Application referencia Domain+CQRS+`IRepository`; Infrastructure implementa `EfRepository/AppDbContextBase`+strategies; Api referencia Application+Infrastructure+ServiceDefaults. Arch tests. |
| III. BuildingBlocks No Reinvention | Reuso Kernel/CQRS/Infrastructure/ServiceDefaults | ✅ PASS | Usa `AggregateRoot`, `Entity`, `ValueObject`, `StronglyTypedId`, `Enumeration`, `Result`, `IRepository`, `Specification`, `ICommand/ISender`, `AppDbContextBase`, `IEndpoint`, `GlobalExceptionHandler`; prohíbe MediatR/MassTransit/AutoMapper; `net10.0`. |
| IV. Vertical Slice + CQRS | Slice autocontenido, sin dispatcher secundario | ✅ PASS | `Features/Rounds/StartRound.cs`, `CompleteRound.cs`, `GetRoundQuestion.cs`, `SubmitAnswer.cs` cada uno con Command+Validator+Handler+Response+Endpoint+Mapping local; usa `BuildingBlocks.CQRS`; endpoint thin `ISender.SendAsync→Result.ToHttpResult()`. |
| V. Authoritative Domain Engine | Server truth para 5 campos, selección impredecible, evaluación | ✅ PASS | `StartRound` asigna `RoundNumber/Difficulty/Question/TimeLimit/Status` server-side, `SelectQuestion` aleatoria `Guid.NewGuid()` + `PreviousQuestionIds` exclusión server-side, `PresentQuestion` filtra `IsCorrect` para `PLAYER`, `EvaluateAnswers` compara `IsCorrect` server-side con `ServerTimestamp - StartedAt ≤ TimeLimit`, `CalculateScores` crea `PointTransaction` server-side. |
| VI. OroIdentityServer (Podman) | `oroidentityserver:latest` única autoridad identidad | ✅ PASS | `StartRound/CompleteRound` requieren `ADMIN/GAME_MANAGER` JWT bearer (`/.well-known/openid-configuration`); `PresentQuestion/SubmitAnswer` requieren `PLAYER` (`sub`); sin user store local; `CreatedBy`/`UserId` es `sub` externo. |
| A. Game Lifecycle State Machine | 9 estados, rowversion, 5 campos | ✅ PASS | `GameStatus` Enumeration 9 valores con transición `IN_PROGRESS/ROUND_COMPLETED→ROUND_IN_PROGRESS` protegida `RowVersion`; `GameRound.Status` 5/6; `UNIQUE (GameId,RoundNumber)` + `RowVersion` → `409` en `StartRound` doble. |
| B. Question & Category Invariants | ≥5 válidas, 4/1 + PUBLISHED, no repetida | ✅ PASS | `MinimumRounds≥5` gate en `Game.Create`; `SelectQuestion` excluye `PreviousQuestionIds` y solo `PUBLISHED` 4/1 + filtros `Category/Difficulty/Academic/Age` (SPEC-003). |
| C. Configurable Game Rules | Estrategia Difficulty configurable | ✅ PASS | `IDifficultyProgressionStrategy` con `Linear` (1→2→3→4→5 clamp) default + `Progressive/Adaptive/CategorySpecific` intercambiables; `PointsPerRound`/`TimeLimit` desde `GameConfiguration` VO inmutable tras `Start`. |
| E/F. Persistence & Concurrency | SQL Server, AppDbContextBase, rowversion, Specification | ✅ PASS | `OroQuizClashDbContext:AppDbContextBase` con `DbSet<Game>` + `GameRound` `HasMany` (field `_rounds`), `Property(RowVersion).IsRowVersion()`, `HasIndex(GameId,RoundNumber).IsUnique()`, `Specification<Game>` con `Include(Rounds)` para rehidratación, `Outbox` misma transacción. |
| G. Real-Time/Outbox | Outbox → RabbitMQ, no publish antes commit | ✅ PASS | `RoundStartedDomainEvent`/`RoundCompletedDomainEvent` dentro de `SaveChanges`; `IntegrationEvent` opcional (`RoundCompletedIntegrationEvent`) vía `IOutboxWriter`→RabbitMQ (topic, confirms, retries) nunca antes de commit. |
| H. Security Delegated | JWT jwks_uri, filtrado IsCorrect | ✅ PASS | `PresentQuestion` `GET /api/games/{id}/rounds/{roundId}/question` filtra `IsCorrect` para `PLAYER` (DTO sin `IsCorrect`), `ADMIN/GAME_MANAGER` ve `IsCorrect` vía `GET /api/questions/{id}` (SPEC-003); JWT `jwks_uri` validación. |
| I. Validation/Errors/Observability | 3 niveles, ProblemDetails, OTel, audit | ✅ PASS | `Validator<StartRoundCommand>` (pipeline) + `IBusinessRule` (dominio `MinimumRounds/PreviousQuestionNotRepeated`); `Result→HTTP` + `GlobalExceptionHandler` → RFC7807 (`400 MinRoundsTooLow/NoAvailableQuestion`, `409 RoundAlreadyInProgress`/`ConcurrencyConflict`); OTel `CorrelationId/GameId/RoundId/RoundNumber`; audit `RoundStarted/Completed`. |
| F. Idempotency | SubmitAnswer idempotente | ✅ PASS | `SubmitAnswer` idempotente por `IdempotencyKey` (`PlayerId+RoundId`) sin duplicar `PointTransaction`; `StartRound` idempotente por `GameId+RoundNumber` único. |

**Gate Result: PASS — no violations. No complexity-tracking entries required.**

## Project Structure

### Documentation (this feature)

```text
specs/005-round-engine/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── round-engine.openapi.yaml
│   └── round-progression.openapi.yaml
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── BuildingBlocks/                          # EXISTING — platform (no modificar)
│   ├── BuildingBlocks.Kernel.Domain/
│   ├── BuildingBlocks.Kernel.Infrastructure/
│   ├── BuildingBlocks.CQRS/
│   ├── BuildingBlocks.EventBus/
│   ├── BuildingBlocks.EventBus.RabbitMQ/
│   ├── BuildingBlocks.Logger/
│   └── BuildingBlocks.ServiceDefaults/
├── OroQuizClash.Domain/                     # EXTEND — Game aggregate (5 campos) + Round Strategies
│   ├── Games/
│   │   ├── Game.cs                         # AggregateRoot<GameId> — extend StartRound/CompleteRound with 5 fields, IncreaseDifficulty delegation
│   │   ├── GameId.cs                       # StronglyTypedId<Guid> (exists)
│   │   ├── GameStatus.cs                   # Enumeration 9 valores (DRAFT..FORCED_FINISHED, from 004)
│   │   ├── GameRound.cs                    # Entity<GameRoundId> — RoundNumber/Difficulty/QuestionId/TimeLimit/Status/StartedAt/CompletedAt
│   │   ├── GameRoundId.cs                  # StronglyTypedId<Guid>
│   │   ├── GamePlayer.cs                   # Entity<GamePlayerId> (from 004)
│   │   ├── ValueObjects/                   # GameConfiguration.cs (MinRounds≥5, TimeLimit 5-300, InitialDifficulty 1..5)
│   │   ├── Rules/                          # IBusinessRule (MinimumRounds, RoundNumberUnique, PreviousQuestionNotRepeated, Category/Difficulty/Academic/Age filters, DifficultyClamp)
│   │   ├── Events/                         # DomainEvent (RoundStarted/RoundCompleted + GameCreated/Finished etc. from 004)
│   │   └── Strategies/                     # IDifficultyProgressionStrategy + Linear/Progressive/Adaptive/CategorySpecific
│   ├── Categories/                          # (002) Category, IQuestionCounter
│   └── Questions/                          # (003) Question, IQuestionSelectionStrategy (Random/DifficultyAware), QuestionSelectionCriteria
├── OroQuizClash.Application/                # EXTEND — Vertical Slices Rounds
│   └── Features/
│       └── Games/
│           ├── CreateGame.cs               # (001/004) exists — DRAFT gate MinRounds≥5
│           ├── StartRound.cs               # Command+Validator+Handler+Response+Endpoint (IN_PROGRESS→ROUND_IN_PROGRESS, SelectQuestion impredecible, IncreaseDifficulty)
│           ├── CompleteRound.cs            # (004) exists — ROUND_IN_PROGRESS→ROUND_COMPLETED → IncreaseDifficulty for next
│           ├── GetRoundQuestion.cs         # Query GetRoundQuestion (PresentQuestion, filtra IsCorrect para PLAYER)
│           ├── SubmitAnswer.cs             # (004) exists — WaitForAnswers/EvaluateAnswers/CalculateScores (idempotente, server timestamp)
│           └── GetGame.cs                  # Query GetGame/GetGames via Specification (from 004)
├── OroQuizClash.Infrastructure/             # EXTEND — Persistence + Strategies
│   ├── Persistence/
│   │   ├── OroQuizClashDbContext.cs        # AppDbContextBase + DbSet<Game> (Rounds via HasMany field _rounds) + Outbox
│   │   ├── Configurations/GameTypeConfiguration.cs # EF Game + owned GameConfiguration, Rounds as HasMany with RowVersion + UNIQUE (GameId,RoundNumber)
│   │   ├── Configurations/GameRoundTypeConfiguration.cs # EF GameRound: HasKey, RoundNumber, Difficulty (Enumeration Id), QuestionId (QuestionId FK logical), TimeLimit, Status, StartedAt/CompletedAt
│   │   └── Strategies/                     # DifficultyProgression (Linear default)
│   ├── Specifications/                     # GameByIdSpecification (Include Rounds/Players), GameFilterSpecification
│   └── Selection/                          # IQuestionSelectionStrategy impls already in 003 (Random/DifficultyAware) reused by StartRound
├── OroQuizClash.Api/                        # EXISTING — Host (añadir endpoints rounds)
│   ├── Program.cs                          # AddCqrs, AddDbContext, JWT (oroidentityserver), IRepository<Game> wiring, IDifficultyProgressionStrategy wiring (Linear default)
│   └── appsettings.json
└── OroQuizClash.AppHost/                    # EXISTING — Aspire orchestration
    └── AppHost.cs                          # sqlserver/postgres/redis/rabbitmq/identity-server/api — no cambios

tests/
├── OroQuizClash.Domain.Tests/               # Unit (MinimumRounds≥5, Round 5 campos, IncreaseDifficulty Linear 1→5, Category/Difficulty/Academic filter, PreviousQuestionNotRepeated, TimeLimit copy, rowversion)
├── OroQuizClash.Application.Tests/          # Handler + ValidationBehavior (NSubstitute IRepository<Game>, IQuestionSelectionStrategy stub, IDifficultyProgressionStrategy)
├── OroQuizClash.Infrastructure.Tests/       # EF + Specification + rowversion + UNIQUE (GameId,RoundNumber) + selection impredecible (Testcontainers)
├── OroQuizClash.Api.Tests/                  # Contract + WebApplicationFactory (JWT mock ADMIN/PLAYER, PresentQuestion filtrado IsCorrect, E2E 5 rounds)
└── OroQuizClash.Architecture.Tests/         # Domain no ref Infra/Web, sin MediatR/MassTransit/AutoMapper
```

**Structure Decision**: Extender el modular monolith existente de `001+002+003+004` (4 proyectos `Domain/Application/Infrastructure/Api` + `AppHost`). `Game` y `GameRound` comparten `OroQuizClashDbContext` (mismo bounded context físico, `GameRound` como `Entity` composición con `RowVersion` en `Game` + `UNIQUE (GameId,RoundNumber)` para `RoundNumber` sin huecos). `IDifficultyProgressionStrategy` como estrategia configurable (default `Linear`, clamp 1..5) intercambiable sin cambiar flujo 8 pasos; `IQuestionSelectionStrategy` reutilizada de 003 (no duplicar) con `PreviousQuestionIds` exclusión y filtros `Category/Difficulty/Academic/Age`. `PresentQuestion` filtra DTO por rol. BuildingBlocks permanece como dependencia externa vía ProjectReference; no microservicio separado; OroIdentityServer consumido como container Podman `oroidentityserver:latest`.

## Complexity Tracking

> **No violations — table intentionally left empty.**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
