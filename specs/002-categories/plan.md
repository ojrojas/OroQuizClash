# Implementation Plan: Categories

**Branch**: `002-categories` | **Date**: 2026-08-26 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/002-categories/spec.md`

## Summary

Gestionar el ciclo de vida de `Category` (`DRAFT → ACTIVE ↔ INACTIVE → ARCHIVED`) con datos `Name/Description/KnowledgeArea/AcademicLevel/AgeRange/DifficultyLevel/Tags/PublishConfiguration` y gate no-negociable `PublishCategory` solo si `CountValidQuestions() ≥5` (cada pregunta 4 opciones, 1 correcta, activa, alineada a la categoría). Implementación como `AggregateRoot<CategoryId>` con VOs/Enumerations, `IBusinessRule` y `Result`, expuesta en 6 Vertical Slices (`Create/Update/Activate/Deactivate/Publish/Archive` + `GetCategories/GetCategoryById`) usando `BuildingBlocks.CQRS/IRepository/Specification/AppDbContextBase/rowversion`, autenticada vía OroIdentityServer (`ADMIN`/`GAME_MANAGER`), y contando válidas vía `IQuestionCounter` (SPEC-003 stub).

## Technical Context

**Language/Version**: C# 12 / .NET 10.0 (`net10.0`, `LangVersion latest`, `Nullable enable`, `ImplicitUsings enable`)

**Primary Dependencies**: `BuildingBlocks.Kernel.Domain` (AggregateRoot, ValueObject, StronglyTypedId, Enumeration, Result/Error, IBusinessRule, IRepository, Specification), `BuildingBlocks.CQRS` (ICommand/IQuery, ISender, IPipelineBehavior Validation/Logging, IValidator), `BuildingBlocks.Kernel.Infrastructure` (AppDbContextBase, EfRepository, SpecificationEvaluator, IUnitOfWork, Outbox), `BuildingBlocks.ServiceDefaults` (OpenTelemetry, health `/health` `/alive`, Resilience, IEndpoint, Result→HTTP, GlobalExceptionHandler), `BuildingBlocks.EventBus.RabbitMQ` (opcional), `OroIdentityServer` Podman `oroidentityserver:latest` (OpenIddict 8 JWT)

**Storage**: SQL Server (primario OroQuizClash, `rowversion`); PostgreSQL `identitydb` aislado solo vía OroIdentityServer; EF Core 10 sobre `AppDbContextBase`; `EfRepository<Category,CategoryId>` + `Specification<T>`; `IQuestionCounter` abstrae conteo SPEC-003 (stub `InMemoryQuestionCounter` o `QuestionRepository`); `Game` ya persiste en mismo `OroQuizClashDbContext` (compartido)

**Testing**: xUnit v3 + NSubstitute + Testcontainers.PostgreSql + Aspire.Hosting.Testing + Microsoft.AspNetCore.Mvc.Testing (WebApplicationFactory) + coverlet; `dotnet test` con TestingPlatform; pruebas de concurrencia `rowversion` para `Publish`

**Target Platform**: Linux containers (Podman `podman build`/`podman compose`), .NET Aspire 13.5.3 AppHost (PostgreSQL+pgAdmin, SQL Server, Redis, RabbitMQ, identity-server), ASP.NET Core minimal APIs (IEndpoint)

**Project Type**: web-service — modular monolith (Domain / Application / Infrastructure / Api)

**Performance Goals**: SC-001 crear categoría válida <1s p95; SC-002/003 `Publish` con gate <2s p95; SC-005 segundo `Publish` concurrente → `409` en <500ms; filtrado `GET /api/categories` con 20 items <500ms; SC-007 90% flujo sin soporte

**Constraints**: <200ms p95 validación pipeline; concurrencia optimista obligatoria (`rowversion`); `Name` 3–100, `Description` 0–500, `KnowledgeArea`/`AcademicLevel` 2–100, `AgeRange` 0–120 `min≤max`, `DifficultyLevel` 1..5, `Tags` ≤10 deduplicados 2–30 lowercased, `Publish` gate ≥5 válidas (no bypass); sin store local credenciales — JWT `jwks_uri`; mapeo explícito (no AutoMapper), sin MediatR/MassTransit; `Update` solo `DRAFT`/`INACTIVE`

**Scale/Scope**: Catálogo inicial 10–500 categorías, 5–5000 preguntas por categoría (SPEC-003), 4 opciones por pregunta, 1 correcta; tags ≤10 por categoría; estados 4

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Principle | Status | Evidence / Mitigación |
|------|-----------|--------|------------------------|
| I. Domain First | Reglas de publicación en Domain, no en controllers | ✅ PASS | `Category.Publish(IQuestionCounter)` con `IBusinessRule` (`CategoryMustHaveFiveValidQuestionsRule`); `Category.Update/Activate/Archive` protegen `AgeRange`, `Tags`; Application solo orquesta. |
| II. Clean Architecture | `Web→Application→Domain←Infrastructure`, Domain sin refs Infra/Web | ✅ PASS | Domain solo `BuildingBlocks.Kernel.Domain`; Application referencia Domain+CQRS; Infrastructure implementa `IRepository/EfRepository/AppDbContextBase`; Api referencia Application+Infrastructure+ServiceDefaults. Arch tests verifican. |
| III. BuildingBlocks No Reinvention | Reuso Kernel/CQRS/Infrastructure/ServiceDefaults | ✅ PASS | Usa `AggregateRoot`, `ValueObject`, `StronglyTypedId`, `Enumeration`, `Result`, `IRepository`, `Specification`, `ICommand/ISender`, `AppDbContextBase`, `IEndpoint`, `GlobalExceptionHandler`; prohíbe MediatR/MassTransit/AutoMapper; `net10.0`. |
| IV. Vertical Slice + CQRS | Slice autocontenido, sin dispatcher secundario | ✅ PASS | `Features/Categories/CreateCategory.cs` etc. cada uno con Command+Validator+Handler+Response+Endpoint+Mapping local; usa `BuildingBlocks.CQRS`; endpoint thin `ISender.SendAsync→Result.ToHttpResult()`. |
| V. Authoritative Domain Engine | Server truth para invariantes pregunta/categoría | ✅ PASS | `Publish` cuenta válidas server-side vía `IQuestionCounter` (4 opciones/1 correcta/activa/alineada); `AgeRange`/`Difficulty` validados server-side; cliente no bypass. |
| VI. OroIdentityServer (Podman) | `oroidentityserver:latest` única autoridad identidad | ✅ PASS | `Create/Update/Publish` requieren JWT bearer `oroidentityserver:latest` (`/.well-known/openid-configuration`); sin user store local; roles `ADMIN`/`GAME_MANAGER` via claims. |
| Additional Constraints B/E/F | Invariantes pregunta/categoría, SQL Server primario, rowversion | ✅ PASS | `CategoryStatus` Enumeration + `rowversion` en `Category`; `IQuestionCounter` respeta constitución B (≥5, 4 opciones/1 correcta); `Specification` para filtros; `AppDbContextBase` + `EfRepository`. |
| H. Security Delegated | JWT `jwks_uri`, policies claim-based | ✅ PASS | Validación JWT bearer contra OroIdentityServer; `[Authorize(Policy=AdminOrGameManager)]` desde claims `roles`; no credenciales locales. |
| I. Validation/Errors/Observability | 3 niveles, ProblemDetails, OTel, audit | ✅ PASS | `Validator<CreateCategoryCommand>` (pipeline) + `IBusinessRule` (dominio); `Result→HTTP` + `GlobalExceptionHandler` → RFC7807 (`400` `InvalidCategoryConfiguration`, `409` conflicto, `404` not found); `ServiceDefaults` OTel. |
| Workflow SDD/Testing/DoD | Spec→Plan→Tasks, suites mínimas, DoD | ✅ PASS | Spec 002 aprobada checklist 16/16; plan genera research/data-model/contracts/quickstart; tests Domain/Application/Infrastructure/Api/Architecture requeridos. |

**Gate Result: PASS — no violations. No complexity-tracking entries required.**

## Project Structure

### Documentation (this feature)

```text
specs/002-categories/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── categories.openapi.yaml
│   └── category-lifecycle.openapi.yaml
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
├── OroQuizClash.Domain/                     # EXTEND — Category aggregate + Game (001) ya existe
│   ├── Categories/
│   │   ├── Category.cs                     # AggregateRoot<CategoryId>
│   │   ├── CategoryId.cs                   # StronglyTypedId<Guid>
│   │   ├── CategoryStatus.cs               # Enumeration (DRAFT, ACTIVE, INACTIVE, ARCHIVED)
│   │   ├── ValueObjects/                   # CategoryName, KnowledgeArea, AcademicLevel, AgeRange, DifficultyLevel, Tags
│   │   ├── Rules/                          # IBusinessRule (AgeRangeCoherent, CategoryNotArchived, MustHaveFiveValid...)
│   │   └── Events/                         # CategoryCreated/Published/ArchivedDomainEvent
│   └── Games/                              # (001) Game, GameConfiguration, etc. — compartido DbContext
├── OroQuizClash.Application/                # EXTEND — Vertical Slices
│   └── Features/
│       └── Categories/
│           ├── CreateCategory.cs           # Command+Validator+Handler+Response+Endpoint
│           ├── UpdateCategory.cs
│           ├── ActivateCategory.cs
│           ├── DeactivateCategory.cs
│           ├── PublishCategory.cs          # gate ≥5 válidas via IQuestionCounter
│           ├── ArchiveCategory.cs
│           └── GetCategories.cs            # Query + Specification filtros
├── OroQuizClash.Infrastructure/             # EXTEND — Persistence
│   ├── Persistence/
│   │   ├── OroQuizClashDbContext.cs        # EXISTING AppDbContextBase + DbSet<Category> + Outbox
│   │   ├── Configurations/CategoryTypeConfiguration.cs # EF owned VOs, rowversion
│   │   └── Counters/InMemoryQuestionCounter.cs # IQuestionCounter stub (SPEC-003 hasta implementar Question)
│   └── Specifications/                     # CategoryByStatusSpecification, CategoryFilterSpecification
├── OroQuizClash.Api/                        # EXISTING — Host (añadir endpoints categorías)
│   ├── Program.cs                          # ya con AddCqrs, AddDbContext, JWT, IRepository<Category> wiring
│   └── appsettings.json
└── OroQuizClash.AppHost/                    # EXISTING — Aspire orchestration
    └── AppHost.cs                          # ya con sqlserver/postgres/redis/rabbitmq/identity-server/api

tests/
├── OroQuizClash.Domain.Tests/               # Unit (Category.Create, Publish gate, AgeRange)
├── OroQuizClash.Application.Tests/          # Handler + ValidationBehavior (NSubstitute IQuestionCounter, IRepository)
├── OroQuizClash.Infrastructure.Tests/       # EF + Specification + rowversion (Testcontainers)
├── OroQuizClash.Api.Tests/                  # Contract + WebApplicationFactory (JWT mock, Aspire Testing)
└── OroQuizClash.Architecture.Tests/         # Domain no ref Infra/Web
```

**Structure Decision**: Extender el modular monolith existente de `001-game-configuration` (4 proyectos + AppHost). `Category` y `Game` comparten `OroQuizClashDbContext` (mismo bounded context físico, agregados separados); no se crea microservicio separado. BuildingBlocks permanece como dependencia externa vía ProjectReference.

## Complexity Tracking

> **No violations — table intentionally left empty.**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
