# Implementation Plan: Game Lifecycle

**Branch**: `004-game-lifecycle` | **Date**: 2026-08-27 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/004-game-lifecycle/spec.md`

## Summary

Extender el agregado `Game : AggregateRoot<GameId>` de 3 a 9 estados (`DRAFT→READY→WAITING_FOR_PLAYERS→IN_PROGRESS→ROUND_IN_PROGRESS↔ROUND_COMPLETED→FINISHED` + `CANCELLED`/`FORCED_FINISHED`) con 10 transiciones explícitas (`MarkReady`, `OpenLobby`, `JoinPlayer`, `Start`, `StartRound`, `CompleteRound`, `Finish`, `Cancel`, `ForceFinish`) protegiendo las 6 reglas (gates SPEC-001, jugadores suficientes, ronda previa terminada, respuestas solo en ronda activa, configuración inmutable tras iniciar, finalización solo desde estados válidos) y emitiendo 9 `DomainEvent` (`GameCreated/GameReady/PlayerJoined/GameStarted/RoundStarted/RoundCompleted/GameFinished/GameCancelled/GameForcedFinished`). Implementación como Vertical Slices `BuildingBlocks.CQRS` + `AppDbContextBase` + `EfRepository<Game>` + `Specification<Game>` + `rowversion` + `IQuestionSelectionStrategy` (SPEC-003) para `StartRound`, con `GameRound`/`GamePlayer` como `Entity` composición, autenticado vía OroIdentityServer (`ADMIN/GAME_MANAGER` para ciclo, `PLAYER` para `Join/SubmitAnswer`), idempotencia `Join/SubmitAnswer` y `Outbox` para `GameFinished` integración.

## Technical Context

**Language/Version**: C# 12 / .NET 10.0 (`net10.0`, `LangVersion latest`, `Nullable enable`, `ImplicitUsings enable`)

**Primary Dependencies**: `BuildingBlocks.Kernel.Domain` (AggregateRoot, Entity, ValueObject, StronglyTypedId, Enumeration, IBusinessRule, Result/Error, IRepository, Specification, IDomainEvent, DomainEvent), `BuildingBlocks.CQRS` (ICommand/IQuery, ICommandHandler/IQueryHandler, ISender, IPipelineBehavior ValidationBehavior/LoggingBehavior, IValidator/Validator), `BuildingBlocks.Kernel.Infrastructure` (AppDbContextBase, EfRepository, SpecificationEvaluator, IUnitOfWork, IOutboxWriter, OutboxEntityTypeConfiguration), `BuildingBlocks.ServiceDefaults` (OpenTelemetry, health `/health` `/alive`, Resilience, IEndpoint, Result→HTTP, GlobalExceptionHandler, ProblemDetails), `BuildingBlocks.EventBus.RabbitMQ` (opcional, OutboxProcessor → RabbitMQ para `GameFinishedIntegrationEvent`), `OroIdentityServer` Podman `oroidentityserver:latest` (OpenIddict 8 JWT, Authority `http://identity:5080`)

**Storage**: SQL Server (primario OroQuizClash, `rowversion` + filtered indexes `Status` + `CategoryId`); PostgreSQL `identitydb` aislado solo vía OroIdentityServer (no acceso directo); EF Core 10 sobre `OroQuizClashDbContext : AppDbContextBase`; `EfRepository<Game,GameId>` + `EfRepository<Category,CategoryId>` + `EfRepository<Question,QuestionId>` ya existentes (002/003); `Specification<Game>` para consultas/filtros (`Status`, `CategoryId`, `CreatedBy`) + `ApplyAsNoTracking` para lectura; agregados `Game` comparte `DbContext` con `GameRound`/`GamePlayer` (`HasMany` con `OwnsMany` o `HasMany.WithOwner` + `RowVersion`); Oracle como target secundario vía abstracción (sin modificar Domain/Application)

**Testing**: xUnit v3 + NSubstitute + Testcontainers.MsSql (o Sqlite InMemory) + Aspire.Hosting.Testing + Microsoft.AspNetCore.Mvc.Testing (WebApplicationFactory) + coverlet; `dotnet test` con TestingPlatform; pruebas de dominio para máquina de estados (9 estados, transiciones válidas/inválidas), gates (config válida, NotEnoughPlayers, RoundAlreadyInProgress, NoActiveRound, ConfigurationImmutable), concurrencia `rowversion` para `MarkReady/StartGame/StartRound/CompleteRound/Finish`, idempotencia `JoinGame/SubmitAnswer`

**Target Platform**: Linux containers (Podman `podman build`/`podman compose`), .NET Aspire 13.5.3 AppHost (SQL Server + PostgreSQL+pgAdmin, Redis, RabbitMQ, identity-server, oroclash-api), ASP.NET Core minimal APIs (IEndpoint) + SignalR futuro (notificaciones `RoundStarted/GameFinished` server-driven, no source of truth)

**Project Type**: web-service — modular monolith (Domain / Application / Infrastructure / Api)

**Performance Goals**: SC-001 Create `DRAFT` <1s p95; SC-002 MarkReady `DRAFT→READY` <2s p95 con validación categoría ≥5; SC-003 StartGame `WAITING→IN_PROGRESS` <500ms + `409` en concurrencia; SC-004 StartRound con selección PUBLISHED 1k <500ms p95 (`IQuestionSelectionStrategy`); SC-005 SubmitAnswer idempotente <500ms; SC-007 Finish solo desde válidos + `409` en concurrencia; SC-008 flujo completo `Create→Finish` <5s en quickstart

**Constraints**: <200ms p95 validación pipeline; concurrencia optimista obligatoria (`rowversion` `IsRowVersion` en `Game`); `GameConfiguration` inmutable tras `IN_PROGRESS` (FR-007); `MinRounds≥5` (SPEC-001), `TimeLimit 5–300s`, `Difficulty 1..5`, `MinPlayers≥1 ≤ MaxPlayers`; solo `PUBLISHED` seleccionable en `StartRound` (SPEC-003 QST-006); `WAITING_FOR_PLAYERS` único que permite `JoinGame` (no late join v1); `Reason` 3–500 para `Cancel/ForceFinish`; sin store local credenciales — JWT `jwks_uri` (`/.well-known/openid-configuration`); mapeo explícito (no AutoMapper), sin MediatR/MassTransit; `GameRounds` numeración 1..MaxRounds sin saltos

**Scale/Scope**: 100–10k preguntas en banco (003), 10–500 categorías (002), 10–1k juegos concurrentes, 2–10 jugadores por juego (max 100), 5–50 rondas por juego, 9 estados, 10 transiciones, 9 eventos de dominio, idempotencia `PlayerId+RoundId` por respuesta

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Principle | Status | Evidence / Mitigación |
|------|-----------|--------|------------------------|
| I. Domain First | Reglas 1-6 y transiciones en Domain, no en controllers | ✅ PASS | `Game.MarkReady()`, `OpenLobby()`, `JoinPlayer()`, `Start()`, `StartRound()`, `CompleteRound()`, `Finish()`, `Cancel()`, `ForceFinish()` con `IBusinessRule` (`GameConfigurationValidRule`, `NotEnoughPlayersRule`, `PreviousRoundNotCompletedRule`, `NoActiveRoundRule`, `ConfigurationImmutableRule`, `CanFinishFromStateRule`); Application solo orquesta vía `IRepository` + `IQuestionSelectionStrategy`. |
| II. Clean Architecture | `Web→Application→Domain←Infrastructure`, Domain sin refs Infra/Web | ✅ PASS | Domain solo `BuildingBlocks.Kernel.Domain`; Application referencia Domain+CQRS+`ICategoryValidator`/`IQuestionCounter`/`IQuestionSelectionStrategy` ports; Infrastructure implementa `EfRepository`/`AppDbContextBase`/`GameValidator`/`QuestionSelector`; Api referencia Application+Infrastructure+ServiceDefaults. Arch tests verifican. |
| III. BuildingBlocks No Reinvention | Reuso Kernel/CQRS/Infrastructure/ServiceDefaults | ✅ PASS | Usa `AggregateRoot`, `Entity`, `ValueObject`, `StronglyTypedId`, `Enumeration`, `Result`, `IRepository`, `Specification`, `ICommand/ISender`, `AppDbContextBase`, `IEndpoint`, `GlobalExceptionHandler`; prohíbe MediatR/MassTransit/AutoMapper; `net10.0`. |
| IV. Vertical Slice + CQRS | Slice autocontenido, sin dispatcher secundario | ✅ PASS | `Features/Games/MarkReady.cs`, `OpenLobby.cs`, `JoinGame.cs`, `StartGame.cs`, `StartRound.cs`, `CompleteRound.cs`, `FinishGame.cs`, `CancelGame.cs`, `ForceFinishGame.cs` cada uno con Command+Validator+Handler+Response+Endpoint+Mapping local; usa `BuildingBlocks.CQRS`; endpoint thin `ISender.SendAsync→Result.ToHttpResult()`. |
| V. Authoritative Domain Engine | Server truth para ciclo, ronda y respuestas | ✅ PASS | `StartGame` valida `players≥MinPlayers` server-side; `StartRound` selecciona pregunta PUBLISHED server-side no usada previamente; `SubmitAnswer` solo en `ROUND_IN_PROGRESS` con `ServerTimestamp` vs `RoundStartedAt` para `TimeLimit`; cliente no bypass; `GameConfiguration` inmutable tras iniciar. |
| VI. OroIdentityServer (Podman) | `oroidentityserver:latest` única autoridad identidad | ✅ PASS | `CreateGame/MarkReady/StartGame/Cancel` requieren `ADMIN`/`GAME_MANAGER` JWT bearer (`/.well-known/openid-configuration`); `JoinGame/SubmitAnswer` requieren `PLAYER` (`roles` claim `sub`); sin user store local; `CreatedBy`/`Player.UserId` es `sub` externo. |
| A. Game Lifecycle State Machine | 9 estados, transiciones rechazadas, rowversion | ✅ PASS | `GameStatus` Enumeration 9 valores con `IsTerminal`/`IsStarted`/`CanTransitionTo()`; matriz `IsValidTransition(from,to)` rechaza `FINISHED→StartGame`; `RowVersion` `IsRowVersion` protege `MarkReady/Start/StartRound` concurrente → `409`. |
| B. Question & Category Invariants | ≥5 válidas para Ready, 4/1 + PUBLISHED selección | ✅ PASS | `MarkReady` verifica `IQuestionCounter.CountValidAsync(CategoryId)≥5` (B); `StartRound` usa `IQuestionSelectionStrategy.SelectAsync` (B) con 7 params previas-preguntas; solo `PUBLISHED` seleccionable. |
| C. Configurable Rules | Difficulty/strategy/políticas configurables | ✅ PASS | `GameConfiguration` VO con `DifficultyStrategy`, `LossPolicy`, `WithdrawalPolicy`, `ConsolationPolicy` inmutables tras `Start`; no hardcode; `Factory` valida `Difficulty 1..5`. |
| E/F. Persistence & Concurrency | SQL Server primario, AppDbContextBase, rowversion, Specification, transacciones | ✅ PASS | `OroQuizClashDbContext:AppDbContextBase` con `DbSet<Game>` + `DbSet<GameRound>`/`GamePlayer` owned, `Property(RowVersion).IsRowVersion()`, `HasIndex(Status)`, `Specification<Game>` para `GetGames`, `SaveChanges` participa `DomainEvents`+`Outbox` misma transacción. |
| G. Real-Time/Outbox | Outbox → RabbitMQ, no publish antes commit | ✅ PASS | `GameFinishedDomainEvent` dentro de `SaveChanges`; `IntegrationEvent` (`GameFinishedIntegrationEvent`) vía `IOutboxWriter`+`OutboxProcessor`→RabbitMQ (topic, publisher confirms, manual ack, retries); nunca antes de commit; flujo `Command→Domain→Transaction→OutboxProcessor→RabbitMQ`. |
| H. Security Delegated | JWT `jwks_uri`, policies claim-based, no local credenciales | ✅ PASS | Validación JWT bearer contra `oroidentityserver:latest` (`jwks_uri`); `[Authorize(Policy=AdminOrGameManager)]` para ciclo, `[Authorize(Policy=Player)]` para `Join/SubmitAnswer`; `Player.UserId` es `sub`; no password hash local. |
| I. Validation/Errors/Observability | 3 niveles, ProblemDetails, OTel, audit | ✅ PASS | `Validator<MarkReadyCommand>` (pipeline) + `IBusinessRule` (dominio); `Result→HTTP` + `GlobalExceptionHandler` → RFC7807 (`400 InvalidGameConfiguration/NotEnoughPlayers/RoundAlreadyInProgress/NoActiveRound/ConfigurationImmutable`, `404 GameNotFound`, `409 Conflict` rowversion); `ServiceDefaults` OTel con `CorrelationId/GameId/PlayerId/RoundId`; audit append-only `GameLifecycleAudit`. |
| F. Idempotency | Join/SubmitAnswer idempotentes, at-least-once | ✅ PASS | `JoinGame` idempotente por `PlayerId` (segundo join mismo jugador → ya unido); `SubmitAnswer` idempotente por `IdempotencyKey`/`PlayerId+RoundId` sin duplicar `PointTransaction`; integración `at-least-once` con `EventId` deduplicación. |
| Workflow SDD/Testing/DoD | Spec→Plan→Tasks, suites mínimas, DoD | ✅ PASS | Spec 004 checklist 16/16; plan genera research/data-model/contracts/quickstart; tests Domain (estados/gates) + Application (handler) + Infrastructure (rowversion) + Api (WebApplicationFactory JWT mock) + Architecture requeridos; DoD cubierto. |

**Gate Result: PASS — no violations. No complexity-tracking entries required.**

## Project Structure

### Documentation (this feature)

```text
specs/004-game-lifecycle/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── game-lifecycle.openapi.yaml
│   └── game-events.openapi.yaml
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
├── OroQuizClash.Domain/                     # EXTEND — Game aggregate (009) + Category (002) + Question (003)
│   ├── Games/
│   │   ├── Game.cs                         # AggregateRoot<GameId> — extend with MarkReady/OpenLobby/Join/Start/StartRound/CompleteRound/Finish/Cancel/ForceFinish, 9 states
│   │   ├── GameId.cs                       # StronglyTypedId<Guid> (exists)
│   │   ├── GameStatus.cs                   # Enumeration 9 valores (exists, extend CanTransitionTo)
│   │   ├── GameRound.cs                    # Entity<GameRoundId> (composition, RoundNumber/QuestionId/Status/StartedAt/CompletedAt)
│   │   ├── GamePlayer.cs                   # Entity<GamePlayerId> (UserId sub, JoinedAt)
│   │   ├── GameRoundId.cs                  # StronglyTypedId<Guid>
│   │   ├── GamePlayerId.cs                 # StronglyTypedId<Guid>
│   │   ├── ValueObjects/                   # GameConfiguration.cs (exists, inmutable), RewardRules.cs
│   │   ├── Rules/                          # IBusinessRule (GameConfigurationValid, NotEnoughPlayers, PreviousRoundNotCompleted, NoActiveRound, ConfigurationImmutable, CanFinishFromState)
│   │   └── Events/                         # DomainEvent (GameCreated/GameReady/PlayerJoined/GameStarted/RoundStarted/RoundCompleted/GameFinished/GameCancelled/GameForcedFinished)
│   ├── Categories/                          # (002) Category, CategoryId, CategoryStatus, IQuestionCounter
│   └── Questions/                          # (003) Question, IQuestionSelectionStrategy, QuestionSelectionCriteria
├── OroQuizClash.Application/                # EXTEND — Vertical Slices Games
│   └── Features/
│       └── Games/
│           ├── CreateGame.cs               # (001) exists — already DRAFT
│           ├── MarkReady.cs                # Command+Validator+Handler+Response+Endpoint (DRAFT→READY gate)
│           ├── OpenLobby.cs                # READY→WAITING_FOR_PLAYERS
│           ├── JoinGame.cs                 # WAITING_FOR_PLAYERS → add GamePlayer, PlayerJoined
│           ├── StartGame.cs                # WAITING_FOR_PLAYERS→IN_PROGRESS (players≥MinPlayers gate)
│           ├── StartRound.cs               # IN_PROGRESS/ROUND_COMPLETED→ROUND_IN_PROGRESS (select PUBLISHED via IQuestionSelectionStrategy)
│           ├── CompleteRound.cs            # ROUND_IN_PROGRESS→ROUND_COMPLETED
│           ├── FinishGame.cs               # →FINISHED (valid from states)
│           ├── CancelGame.cs               # →CANCELLED (Reason 3-500)
│           ├── ForceFinishGame.cs          # →FORCED_FINISHED (Reason)
│           ├── SubmitAnswer.cs             # (future, guarded by ROUND_IN_PROGRESS — idempotente, server timestamp)
│           └── GetGame.cs                  # Query GetGame/GetGames via Specification<Game>
├── OroQuizClash.Infrastructure/             # EXTEND — Persistence + Validators + Selection
│   ├── Persistence/
│   │   ├── OroQuizClashDbContext.cs        # AppDbContextBase + DbSet<Game> + DbSet<GameRound>/GamePlayer (via Game aggregate) + Outbox
│   │   ├── Configurations/GameTypeConfiguration.cs # EF Game + owned GameConfiguration, HasMany GameRound/GamePlayer, RowVersion, indexes (Status, CategoryId)
│   │   └── Validators/                     # ICategoryValidator, IQuestionCounter impl (EfQuestionCounter), IQuestionSelectionStrategy (Random) already in 003
│   ├── Specifications/                     # GameByIdSpecification, GameFilterSpecification (Status/CategoryId/CreatedBy + paginación)
│   └── Services/                           # GameLifecycleAuditWriter (append-only)
├── OroQuizClash.Api/                        # EXISTING — Host (añadir endpoints lifecycle)
│   ├── Program.cs                          # AddCqrs, AddDbContext, JWT (oroidentityserver), IRepository<Game> wiring, AddEndpoints
│   └── appsettings.json
└── OroQuizClash.AppHost/                    # EXISTING — Aspire orchestration
    └── AppHost.cs                          # sqlserver/postgres/redis/rabbitmq/identity-server/api — no cambios salvo seed

tests/
├── OroQuizClash.Domain.Tests/               # Unit (Game.Create, MarkReady gate, Join/Start/StartRound/CompleteRound/Finish/Cancel state machine + rowversion)
├── OroQuizClash.Application.Tests/          # Handler + ValidationBehavior (NSubstitute IRepository<Game>, IQuestionCounter, IQuestionSelectionStrategy, ICategoryValidator)
├── OroQuizClash.Infrastructure.Tests/       # EF + Specification + rowversion concurrency (OroQuizClashDbContext + GameTypeConfiguration) (Testcontainers)
├── OroQuizClash.Api.Tests/                  # Contract + WebApplicationFactory (JWT mock ADMIN/PLAYER, E2E lifecycle)
└── OroQuizClash.Architecture.Tests/         # Domain no ref Infra/Web, sin MediatR/MassTransit/AutoMapper
```

**Structure Decision**: Extender el modular monolith existente de `001+002+003` (4 proyectos `Domain/Application/Infrastructure/Api` + `AppHost`). `Game` y `Category`/`Question` comparten `OroQuizClashDbContext` (mismo bounded context físico, agregados separados por ID, relación lógica `GameConfiguration.CategoryId` → `CategoryId`). `GameRound`/`GamePlayer` como `Entity` composición dentro de `Game` (no agregados separados) para garantizar invariante "no dos rondas activas" y `players≥MinPlayers` atómicamente. BuildingBlocks permanece como dependencia externa vía ProjectReference; no microservicio separado para ciclo; OroIdentityServer consumido como container Podman `oroidentityserver:latest` (no reimplementar).

## Complexity Tracking

> **No violations — table intentionally left empty.**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
