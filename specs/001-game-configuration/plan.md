# Implementation Plan: Game Configuration

**Branch**: `001-game-configuration` | **Date**: 2026-08-26 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-game-configuration/spec.md`

## Summary

Implementar la configuración inmutable de partida como ValueObject(s) dentro del agregado `Game` (DRAFT/READY → bloqueado tras `StartGame`), exigiendo nombre, categoría válida (SPEC-002/003), `minRondas ≥5 ≤ maxRondas`, `minJugadores ≥1 ≤ maxJugadores`, dificultad inicial + `DifficultyProgressionStrategy`, `TimeLimitPerQuestion` positivo (5–300s), `ScoringSystem`, `LossPolicy`/`WithdrawalPolicy`/`ConsolationPolicy` y `RewardRules`. La creación expone un Vertical Slice `CreateGame` (Command + Validator + Handler + DTO + IEndpoint) usando BuildingBlocks CQRS/Repository/UoW/Specifications, con validación en pipeline y reglas de dominio (`IBusinessRule`) retornando `Result<GameId>` mapeado a ProblemDetails, persistido vía `AppDbContextBase` + `EfRepository` + `rowversion` y consultable por `Specification<Game>`, autenticado vía OroIdentityServer OIDC JWT (ADMIN/GAME_MANAGER).

## Technical Context

**Language/Version**: C# 12 / .NET 10.0 (`net10.0`, `LangVersion latest`, `Nullable enable`, `ImplicitUsings enable`)

**Primary Dependencies**: `BuildingBlocks.Kernel.Domain` (AggregateRoot, ValueObject, StronglyTypedId, Enumeration, Result/Error, IBusinessRule, IRepository, Specification), `BuildingBlocks.CQRS` (ICommand, ICommandHandler, ISender, IPipelineBehavior Validation/Logging), `BuildingBlocks.Kernel.Infrastructure` (AppDbContextBase, EfRepository, SpecificationEvaluator, IUnitOfWork, Outbox), `BuildingBlocks.ServiceDefaults` (OpenTelemetry, health `/health` `/alive`, Resilience, IEndpoint, Result→HTTP, GlobalExceptionHandler), `BuildingBlocks.EventBus.RabbitMQ` (solo si se publican eventos de dominio), `OroIdentityServer` Podman image `oroidentityserver:latest` (OpenIddict 8 OIDC JWT, PostgreSQL identitydb aislado)

**Storage**: SQL Server (primario OroQuizClash, `rowversion`); PostgreSQL (`identitydb` solo via OroIdentityServer, aislado); Oracle como target secundario vía abstracción (sin modificar Domain/Application). EF Core 10 sobre `AppDbContextBase`; `EfRepository<Game, GameId>` + `Specification<T>`; `IOutboxWriter` si aplica eventos

**Testing**: xUnit v3 + NSubstitute + Testcontainers.PostgreSql + Aspire.Hosting.Testing + Microsoft.AspNetCore.Mvc.Testing (WebApplicationFactory) + coverlet; `dotnet test` con TestingPlatform

**Target Platform**: Linux containers (Podman `podman build`/`podman compose`), .NET Aspire 13.5.3 AppHost (PostgreSQL + pgAdmin, RabbitMQ, Redis opcionales), ASP.NET Core minimal APIs (IEndpoint) + SignalR (futuro, no requerido para esta feature)

**Project Type**: web-service — modular monolith (Domain / Application / Infrastructure / Api)

**Performance Goals**: SC-002: creación válida <2s p95 (persistida); rechazo de configuración inválida <500ms; SC-005: 90% first-attempt con mensajes claros

**Constraints**: <200ms p95 para validación en pipeline; concurrencia optimista obligatoria (`rowversion`/`ConcurrencyToken`); sin store local de credenciales — JWT validado contra `/.well-known/openid-configuration` → `jwks_uri`; `minRondas ≥5` inclusivo; `TimeLimitPerQuestion` 5–300s; configuración inmutable tras iniciar; mapeo explícito (no AutoMapper), sin MediatR/MassTransit

**Scale/Scope**: 10–100 partidas concurrentes iniciales; 2–10 jugadores por partida (1–100 límite genérico); 5–50 rondas por partida; categorías con ≥5 preguntas válidas; ~12 campos de configuración por juego

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Principle | Status | Evidence / Mitigación |
|------|-----------|--------|------------------------|
| I. Domain First | Reglas de configuración en Domain, sin lógica en controllers | ✅ PASS | `Game.Create(configuration)` con `IBusinessRule` (MinRoundsAtLeastFive, CategoryMustBeValid, DifficultyStrategyRequired, TimeLimitPositive, PoliciesRequired, RangeCoherence); Application solo orquesta. |
| II. Clean Architecture | `Web→Application→Domain←Infrastructure`, Domain sin refs a Infra/Web | ✅ PASS | Domain solo referencia `BuildingBlocks.Kernel.Domain`; Application referencia Domain+CQRS; Infrastructure implementa `IRepository`/`AppDbContextBase`; API referencia Application+Infrastructure+ServiceDefaults. Tests de arquitectura lo verifican. |
| III. BuildingBlocks No Reinvention | Reuso de Kernel/CQRS/Infrastructure/ServiceDefaults | ✅ PASS | Usa `AggregateRoot`, `ValueObject`, `StronglyTypedId`, `Result`, `IRepository`, `Specification`, `ICommand`/`ISender`, `AppDbContextBase`, `IEndpoint`, `GlobalExceptionHandler`; prohíbe MediatR/MassTransit/AutoMapper; multi-target `net10.0`. |
| IV. Vertical Slice + CQRS | Slice autocontenido, sin dispatcher secundario | ✅ PASS | `Features/Games/CreateGame.cs` contiene Command+Validator+Handler+Response+Endpoint+Mapping local; usa `BuildingBlocks.CQRS`; endpoint thin `ISender.SendAsync() → Result.ToOkResult()`. |
| V. Authoritative Domain Engine | Server truth para juego, identidad delegada | ✅ PASS | Validación server-side; inmutabilidad tras `StartGame`; sin confianza en cliente. Identidad no se re-deriva localmente (ver VI). |
| VI. OroIdentityServer (Podman) | `oroidentityserver:latest` como única autoridad identidad | ✅ PASS | `CreateGame` requiere JWT bearer de `oroidentityserver:latest` (discovery `/.well-known/openid-configuration`); sin user store local; roles `ADMIN`/`GAME_MANAGER` via claims; env `SymmetricSecurityKey` compartido; DB `identitydb` aislada. |
| Additional Constraints C/E/F/G | Config inmutable, SQL Server primario/Oracle portable, rowversion, Outbox opcional | ✅ PASS | `GameConfiguration` ValueObject inmutable; `rowversion` en `Game`; transacciones protegen `SaveChanges`; `Specification` para consultas; RabbitMQ solo si se emite `GameCreatedDomainEvent` vía Outbox. |
| H. Security Delegated | JWT validado contra jwks_uri, policies claim-based | ✅ PASS | Validación JWT bearer contra OroIdentityServer; `[Authorize(Policy=...)]` desde claims `roles`/`tenant_id`/`is_master_admin`; `GamePlayer` referencia `sub`, no credenciales. |
| I. Validation/Errors/Observability | 3 niveles, ProblemDetails, OTel, audit append-only | ✅ PASS | `Validator<CreateGameCommand>` (pipeline) + `IBusinessRule` (dominio); `Result→HTTP` + `GlobalExceptionHandler` → RFC7807; `ServiceDefaults` OTel (`CorrelationId`/`GameId`/etc.); auditoría append-only para creación de juego. |
| Workflow SDD/Testing/DoD | Spec→Plan→Tasks, suites mínimas, DoD checklist | ✅ PASS | Spec 001 aprobada; plan genera research/data-model/contracts/quickstart; tests Domain/Application/Integration/API/Architecture requeridos; DoD cubierto. |

**Gate Result: PASS — no violations. No complexity-tracking entries required.**

## Project Structure

### Documentation (this feature)

```text
specs/001-game-configuration/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   └── create-game.openapi.yaml
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
├── OroQuizClash.Domain/                     # NEW — Game, GameConfiguration, policies, IBusinessRules
│   ├── Games/
│   │   ├── Game.cs                         # AggregateRoot<GameId>
│   │   ├── GameId.cs                       # StronglyTypedId<Guid>
│   │   ├── GameStatus.cs                   # Enumeration (DRAFT, READY, WAITING_FOR_PLAYERS, IN_PROGRESS, ...)
│   │   ├── GameConfiguration.cs            # ValueObject inmutable
│   │   ├── ValueObjects/                   # ScoringSystem, RewardRules, etc.
│   │   ├── Enumerations/                   # LossPolicy, WithdrawalPolicy, ConsolationPolicy, DifficultyStrategy
│   │   ├── Rules/                          # IBusinessRule implementations (MinRoundsAtLeastFiveRule, etc.)
│   │   └── Events/                         # GameCreatedDomainEvent (in-process)
│   └── Categories/                         # Referencia externa (CategoryId VO, validada vía spec)
├── OroQuizClash.Application/                # NEW — Vertical Slices
│   └── Features/
│       └── Games/
│           └── CreateGame.cs               # Command+Validator+Handler+Response+Endpoint+Mapping
├── OroQuizClash.Infrastructure/             # NEW — Persistence
│   ├── Persistence/
│   │   ├── OroQuizClashDbContext.cs        # AppDbContextBase + OutboxEntityTypeConfiguration
│   │   ├── Configurations/GameConfiguration.cs # EF config (owned ValueObject, rowversion)
│   │   └── Repositories/                   # EfRepository<Game,GameId> via BuildingBlocks
│   └── Specifications/                     # GameByIdSpecification, etc.
├── OroQuizClash.Api/                        # NEW — Host (minimal APIs, IEndpoint, JWT bearer)
│   ├── Program.cs                          # AddServiceDefaults, AddCqrs, AddDbContext, AddUnitOfWork, AddEndpoints, JWT bearer (Authority=http://identity:5080)
│   └── appsettings.json
└── OroQuizClash.AppHost/                    # EXISTING — Aspire orchestration (añadir references a Api + OroIdentityServer container)
    └── AppHost.cs

tests/
├── OroQuizClash.Domain.Tests/               # Unit (Game.Create, ValueObjects, Rules)
├── OroQuizClash.Application.Tests/          # Handler + ValidationBehavior (NSubstitute para IRepository)
├── OroQuizClash.Infrastructure.Tests/       # EF + Specification + rowversion (Testcontainers/Aspire)
├── OroQuizClash.Api.Tests/                  # Contract + WebApplicationFactory (JWT mock + Podman oroidentityserver container opcional)
└── OroQuizClash.Architecture.Tests/         # Domain no ref Infra/Web, sin MediatR/MassTransit/AutoMapper
```

**Structure Decision**: Modular monolith de 4 proyectos (`Domain`, `Application`, `Infrastructure`, `Api`) + `AppHost` Aspire existente, alineado con constitución y `draft/game-concept.md` §3. BuildingBlocks permanece como dependencia externa vía ProjectReference; no se crea microservicio separado para identidad (se consume container Podman `oroidentityserver:latest`).

## Complexity Tracking

> **No violations — table intentionally left empty.**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
