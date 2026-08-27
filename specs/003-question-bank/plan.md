# Implementation Plan: Question Bank

**Branch**: `003-question-bank` | **Date**: 2026-08-26 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/003-question-bank/spec.md`

## Summary

Gestionar el banco de preguntas como `AggregateRoot<QuestionId>` con exactamente 4 `AnswerOption` (1 correcta) y atributos `Text/CategoryId/Difficulty/AcademicLevel/AgeRange/Status/RowVersion`, cubriendo reglas QST-001..006 y 6 casos de uso (`CreateQuestion`, `UpdateQuestion`, `ActivateQuestion`, `DeactivateQuestion`, `PublishQuestion`, `ArchiveQuestion`) con gate de validación previa y protección QST-005 (publicada no puede quedar sin correcta). Exponer selección de preguntas para el motor de juego vía contrato `IQuestionSelectionStrategy.SelectAsync(QuestionSelectionCriteria)` que considera obligatoriamente `Category, Difficulty, AcademicLevel, AgeRange, PreviousQuestions, Game, Round` (estrategia intercambiable: `Random`, `DifficultyAware`, `Adaptive`), y conteo de válidas para `Category.Publish ≥5` (SPEC-002). Implementación como Vertical Slices `BuildingBlocks.CQRS` + `AppDbContextBase` + `EfRepository` + `Specification<Question>` + `rowversion`, autenticada vía OroIdentityServer (`ADMIN`/`GAME_MANAGER`), con eventos de dominio y `IOutboxWriter` para integración categoría.

## Technical Context

**Language/Version**: C# 12 / .NET 10.0 (`net10.0`, `LangVersion latest`, `Nullable enable`, `ImplicitUsings enable`)

**Primary Dependencies**: `BuildingBlocks.Kernel.Domain` (AggregateRoot, Entity, ValueObject, StronglyTypedId, Enumeration, IBusinessRule, Result/Error, IRepository, Specification, IDomainEvent), `BuildingBlocks.CQRS` (ICommand/IQuery, ICommandHandler/IQueryHandler, ISender, IPipelineBehavior ValidationBehavior/LoggingBehavior, IValidator/Validator), `BuildingBlocks.Kernel.Infrastructure` (AppDbContextBase, EfRepository, SpecificationEvaluator, IUnitOfWork, IOutboxWriter, OutboxEntityTypeConfiguration), `BuildingBlocks.ServiceDefaults` (OpenTelemetry, health `/health` `/alive`, Resilience, IEndpoint, Result→HTTP, GlobalExceptionHandler, ProblemDetails), `BuildingBlocks.EventBus.RabbitMQ` (opcional, OutboxProcessor → RabbitMQ), `OroIdentityServer` Podman `oroidentityserver:latest` (OpenIddict 8 JWT, Authority `http://identity:5080`)

**Storage**: SQL Server (primario OroQuizClash, `rowversion` + check `ExactlyOneCorrect`); PostgreSQL `identitydb` aislado solo vía OroIdentityServer (no acceso directo); EF Core 10 sobre `OroQuizClashDbContext : AppDbContextBase`; `EfRepository<Question,QuestionId>` + `EfRepository<Category,CategoryId>` ya existente (002); `Specification<Question>` para filtros/selección/conteo válidas; índices follow query patterns; Oracle como target secundario vía abstracción (sin modificar Domain/Application)

**Testing**: xUnit v3 + NSubstitute + Testcontainers.MsSql (o Sqlite InMemory) + Aspire.Hosting.Testing + Microsoft.AspNetCore.Mvc.Testing (WebApplicationFactory) + coverlet; `dotnet test` con TestingPlatform; pruebas de dominio para invariantes 4/1 + estado, concurrencia `rowversion` para `Publish`/`Update`/`Select`, selección sin `PreviousQuestions`, idempotencia `AnswerSubmissionId` (futuro) no requerida aquí

**Target Platform**: Linux containers (Podman `podman build`/`podman compose`), .NET Aspire 13.5.3 AppHost (SQL Server + PostgreSQL+pgAdmin, Redis, RabbitMQ, identity-server, oroclash-api), ASP.NET Core minimal APIs (IEndpoint) + SignalR futuro (no requerido para este feature)

**Project Type**: web-service — modular monolith (Domain / Application / Infrastructure / Api)

**Performance Goals**: SC-001 crear válida <1s p95; SC-004 Publish válida <2s p95 con evento; SC-006 selección sobre 1k preguntas <500ms p95 paginada sin full scan; segundo `Publish`/`Update` concurrente → `409` <500ms; SC-009 flujo 5 preguntas→Category Publish <5s

**Constraints**: <200ms p95 validación pipeline; concurrencia optimista obligatoria (`rowversion`); `Text` 3–500, `AnswerOption.Text` 1–500 no vacío, 4 opciones fijas display A-D, exactamente 1 correcta (DB CHECK); `CategoryId` FK lógico, `Difficulty` 1..5, `AgeRange` 0–120 `min≤max`, `AcademicLevel` 2–100; QST-005 (publicada no sin correcta), QST-006 (solo PUBLISHED+ACTIVE seleccionable); `Update` solo DRAFT/INACTIVE (o PUBLISHED si mantiene 4/1 según plan); sin store local credenciales — JWT `jwks_uri` (`/.well-known/openid-configuration`); mapeo explícito (no AutoMapper), sin MediatR/MassTransit; inmutable tras iniciar juego (respeta Game Config 001)

**Scale/Scope**: Banco inicial 100–10k preguntas, 5–5000 por categoría, 10–500 categorías (002), 4 opciones por pregunta, estados 5 (DRAFT→ACTIVE↔PUBLISHED→INACTIVE→ARCHIVED), selección con `PreviousQuestions` hasta 50 ids por Game/Round (≥5 rondas)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Principle | Status | Evidence / Mitigación |
|------|-----------|--------|------------------------|
| I. Domain First | Reglas QST-001..006 en Domain, no en controllers | ✅ PASS | `Question.Create/Update/Publish/Activate/Deactivate/Archive` con `IBusinessRule` (`QuestionMustHaveFourOptionsRule`, `ExactlyOneCorrectAnswerRule`, `MustBelongToCategoryRule`, `MustHaveDifficultyRule`, `PublishedMustHaveCorrectRule`); Application solo orquesta. Invariantes protegidas sin setters. |
| II. Clean Architecture | `Web→Application→Domain←Infrastructure`, Domain sin refs Infra/Web | ✅ PASS | Domain solo `BuildingBlocks.Kernel.Domain`; Application referencia Domain+CQRS+`ICategoryExistenceChecker` port; Infrastructure implementa `IRepository/EfRepository/AppDbContextBase/IQuestionCounter/IQuestionSelectionStrategy`; Api referencia Application+Infrastructure+ServiceDefaults. Arch tests verifican. |
| III. BuildingBlocks No Reinvention | Reuso Kernel/CQRS/Infrastructure/ServiceDefaults | ✅ PASS | Usa `AggregateRoot`, `ValueObject`, `StronglyTypedId`, `Enumeration`, `Result`, `IRepository`, `Specification`, `ICommand/ISender`, `AppDbContextBase`, `IEndpoint`, `GlobalExceptionHandler`; prohíbe MediatR/MassTransit/AutoMapper; `net10.0`. |
| IV. Vertical Slice + CQRS | Slice autocontenido, sin dispatcher secundario | ✅ PASS | `Features/Questions/CreateQuestion.cs` etc. cada uno con Command+Validator+Handler+Response+Endpoint+Mapping local; usa `BuildingBlocks.CQRS`; endpoint thin `ISender.SendAsync→Result.ToHttpResult()`. |
| V. Authoritative Domain Engine | Server truth para invariantes y selección | ✅ PASS | Validación 4/1, categoría/dificultad, alineación AcademicLevel/AgeRange server-side; selección excluye DRAFT/INACTIVE/ARCHIVED y PreviousQuestions server-side; cliente no bypass. |
| VI. OroIdentityServer (Podman) | `oroidentityserver:latest` única autoridad identidad | ✅ PASS | Create/Update/Publish/Select requieren JWT bearer `oroidentityserver:latest` (`/.well-known/openid-configuration`); sin user store local; roles `ADMIN`/`GAME_MANAGER` via claims `roles/permissions`. |
| B. Question & Category Invariants | 4 opciones/1 correcta, ≥5 para Category, IQuestionSelectionStrategy | ✅ PASS | `Question` garantiza 4/1 via Rule + DB CHECK `CK_ExactlyOneCorrectPerQuestion`; `QuestionStatus` Enumeration; `IQuestionCounter` y `IQuestionSelectionStrategy` behind `Specification<Question>`; `Category.Publish` gate ya en 002 y aquí verificado; selección considera category/difficulty/academic/age/previous (FR-013/014). |
| E/F. Persistence & Concurrency | SQL Server primario, AppDbContextBase, rowversion, Specification | ✅ PASS | `OroQuizClashDbContext:AppDbContextBase` con `DbSet<Question>`+`DbSet<AnswerOption>` owned/EF, `Property(RowVersion).IsRowVersion()`, `HasIndex(CategoryId, Status, Difficulty)`, `Specification<Question>` para filtros/selección/conteo válidas; `OutboxEntityTypeConfiguration` en misma transacción. |
| G. Real-Time/Outbox | Outbox → RabbitMQ, no publish antes commit | ✅ PASS | `QuestionPublishedDomainEvent` dispatch en `SaveChanges`; si se requiere integración categoría → `IntegrationEvent` vía `IOutboxWriter`+`OutboxProcessor`→RabbitMQ, nunca antes de commit. |
| H. Security Delegated | JWT jwks_uri, policies claim-based | ✅ PASS | Validación JWT bearer contra OroIdentityServer; `[Authorize(Policy=AdminOrGameManager)]` desde claims; no credenciales locales; audit con `PerformedBy sub`. |
| I. Validation/Errors/Observability | 3 niveles, ProblemDetails, OTel, audit | ✅ PASS | `Validator<CreateQuestionCommand>` (pipeline) + `IBusinessRule` (dominio); `Result→HTTP` + `GlobalExceptionHandler` → RFC7807 (`400` QST violations, `404` not found, `409` conflicto rowversion); `ServiceDefaults` OTel con `CorrelationId/QuestionId/CategoryId/GameId/RoundId`; audit append-only. |
| C/I. Configurable Rules | Difficulty/strategy configurable | ✅ PASS | `DifficultyLevel` Enumeration 1..5 configurable; `IQuestionSelectionStrategy` (`Random` default, `DifficultyAware`, `Adaptive`) intercambiable sin cambiar contrato 7 params. |
| Workflow SDD/Testing/DoD | Spec→Plan→Tasks, suites mínimas, DoD | ✅ PASS | Spec 003 checklist 16/16; plan genera research/data-model/contracts/quickstart; tests Domain/Application/Infrastructure/Api/Architecture requeridos; DoD cubierto. |

**Gate Result: PASS — no violations. No complexity-tracking entries required.**

## Project Structure

### Documentation (this feature)

```text
specs/003-question-bank/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── questions.openapi.yaml
│   └── question-selection.openapi.yaml
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
├── OroQuizClash.Domain/                     # EXTEND — Question aggregate + Category (002) + Game (001)
│   ├── Questions/
│   │   ├── Question.cs                     # AggregateRoot<QuestionId>
│   │   ├── QuestionId.cs                   # StronglyTypedId<Guid>
│   │   ├── QuestionStatus.cs               # Enumeration (DRAFT=1, ACTIVE=2, PUBLISHED=3, INACTIVE=4, ARCHIVED=5)
│   │   ├── AnswerOption.cs                 # Entity<AnswerOptionId> (Question composition)
│   │   ├── AnswerOptionId.cs               # StronglyTypedId<Guid>
│   │   ├── ValueObjects/                   # QuestionText, DifficultyLevel, AcademicLevel, AgeRange, DisplayOrder
│   │   ├── Rules/                          # IBusinessRule (FourOptions, ExactlyOneCorrect, MustBelongToCategory, MustHaveDifficulty, PublishedMustHaveCorrect, CategoryExists, AgeRangeCoherent)
│   │   └── Events/                         # QuestionCreated/Updated/Published/Deactivated/ArchivedDomainEvent
│   ├── Categories/                          # (002) Category, CategoryId, CategoryStatus, ValueObjects, Rules, Events
│   └── Games/                              # (001) Game, GameConfiguration, etc. — comparte DbContext
├── OroQuizClash.Application/                # EXTEND — Vertical Slices
│   └── Features/
│       └── Questions/
│           ├── CreateQuestion.cs           # Command+Validator+Handler+Response+Endpoint
│           ├── UpdateQuestion.cs
│           ├── ActivateQuestion.cs
│           ├── DeactivateQuestion.cs
│           ├── PublishQuestion.cs          # gate QST-001..004 validación previa
│           ├── ArchiveQuestion.cs
│           ├── GetQuestions.cs             # Query + Specification (filtros, paginación)
│           ├── GetQuestionById.cs
│           ├── SelectQuestions.cs          # Query SelectQuestions (Category,Difficulty,AcademicLevel,AgeRange,PreviousIds,GameId,RoundId) → IQuestionSelectionStrategy
│           └── Services/                   # IQuestionSelectionStrategy, ICategoryExistenceChecker, IQuestionCounter (for Category gate)
├── OroQuizClash.Infrastructure/             # EXTEND — Persistence + Strategies
│   ├── Persistence/
│   │   ├── OroQuizClashDbContext.cs        # EXISTING AppDbContextBase + DbSet<Question> DbSet<AnswerOption> + Outbox
│   │   ├── Configurations/QuestionTypeConfiguration.cs # EF owned AnswerOptions, converters, rowversion, indexes, CHECK ExactlyOneCorrect
│   │   └── Counters/QuestionCounter.cs    # IQuestionCounter (EfQuestionCounter) para Category 002 gate
│   ├── Specifications/                     # ValidQuestionSpecification, QuestionFilterSpecification, QuestionSelectionSpecification
│   └── Selection/                          # IQuestionSelectionStrategy impl: RandomQuestionSelectionStrategy (default), DifficultyAware, Adaptive
├── OroQuizClash.Api/                        # EXISTING — Host (añadir endpoints questions)
│   ├── Program.cs                          # ya con AddCqrs, AddDbContext, JWT (oroidentityserver), IRepository wiring, IQuestionSelectionStrategy wiring
│   └── appsettings.json
└── OroQuizClash.AppHost/                    # EXISTING — Aspire orchestration
    └── AppHost.cs                          # ya con sqlserver/postgres/redis/rabbitmq/identity-server/api — no cambios salvo si se añade seed

tests/
├── OroQuizClash.Domain.Tests/               # Unit (Question.Create 4/1, Publish gate, QST-005, estados)
├── OroQuizClash.Application.Tests/          # Handler + ValidationBehavior (NSubstitute IRepository<Question>, ICategoryExistenceChecker, IQuestionSelectionStrategy stub)
├── OroQuizClash.Infrastructure.Tests/       # EF + Specification + rowversion + CHECK, QuestionCounter, Selection (Testcontainers MsSql)
├── OroQuizClash.Api.Tests/                  # Contract + WebApplicationFactory (JWT mock ADMIN, E2E questions lifecycle)
└── OroQuizClash.Architecture.Tests/         # Domain no ref Infra/Web, sin MediatR/MassTransit/AutoMapper
```

**Structure Decision**: Extender el modular monolith de `001-game-configuration` + `002-categories` (4 proyectos + AppHost). `Question` y `Category`/`Game` comparten `OroQuizClashDbContext` (mismo bounded context físico, agregados separados por ID, sin FK cross-aggregate física obligatoria — relación lógica `Question.CategoryId`). `AnswerOption` como `Entity` owned dentro de `Question` (composición). BuildingBlocks permanece como dependencia externa vía ProjectReference; no microservicio separado.

## Complexity Tracking

> **No violations — table intentionally left empty.**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
