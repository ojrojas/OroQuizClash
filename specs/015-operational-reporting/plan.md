# Implementation Plan: Operational Reporting

**Branch**: `015-operational-reporting` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/015-operational-reporting/spec.md`

## Summary

Reportería operativa de solo lectura sobre juegos, jugadores, preguntas, categorías, recompensas y leaderboard, con filtros combinables `Global`/`Game`/`Category`/`Period` y garantía de no-mutación del dominio transaccional. Se implementa como `IQuery<T>` + `IQueryHandler` en Vertical Slice, reutilizando `PointTransaction` ledger (SPEC-007), `Answer` evaluadas (SPEC-006), `Game`/`GamePlayer`/`GameRound` (SPEC-004) y `AuditEntry` opcional (SPEC-014), con `Specification<T>` para filtrado/paginación cuando corresponda. Sin nuevas tablas ni migraciones; extensión del `Leaderboard` existente (SPEC-011) con filtros adicionales.

## Technical Context

**Language/Version**: C# 12 / .NET 10.0 (`net10.0`, `LangVersion latest`, `Nullable enable`, `ImplicitUsings enable`)

**Primary Dependencies**: `BuildingBlocks.Kernel.Domain` (Entity, AggregateRoot, Enumeration, Specification, Result/Error, IRepository), `BuildingBlocks.CQRS` (IQuery, IQueryHandler, ISender, IPipelineBehavior), `BuildingBlocks.Kernel.Infrastructure` (AppDbContextBase, EfRepository, SpecificationEvaluator), `BuildingBlocks.ServiceDefaults` (IEndpoint, Result→HTTP, OTel), `Microsoft.EntityFrameworkCore` 10, `Microsoft.AspNetCore.Authentication.JwtBearer` (OroIdentityServer)

**Storage**: SQL Server primario (Aspire) / SQLite local (`oroclash.db`, `EnsureCreatedAsync`); EF Core 10; sin nuevas tablas para reportes — lectura desde `Games`, `GamePlayers`, `GameRounds`, `Answers`, `Questions`, `Categories`, `PointTransactions`, `Rewards`, `RewardRedemptions`, `AuditEntries`; `Specification<T>` ya existente para filtrado

**Testing**: xUnit v3 + NSubstitute + coverlet + `Microsoft.EntityFrameworkCore.InMemory`/`Sqlite`; Domain tests para cálculos (Accuracy), Application tests para `IQueryHandler` con datos en memoria, Api tests para filtros y 403 sin `Report.Read`, Architecture tests para lectura pura y uso de CQRS

**Target Platform**: Linux containers (Podman), .NET Aspire AppHost, ASP.NET Core minimal APIs (IEndpoint)

**Project Type**: web-service — modular monolith (Domain / Application / Infrastructure / Api) — cross-cutting read

**Performance Goals**: SC-003 `AverageResponseTime` error <1% vs promedio real; SC-007/008 `CategoryReport`/`RewardReport` dentro de 1% del cálculo manual; consultas paginadas con `pageSize` máximo evitan full scan sobre 10k juegos; p95 <200 ms para reportes con filtros

**Constraints**: Constitución I/II/III/VI/H/I; reporting no modifica dominio (FR-008); solo lectura via `IQuery` (FR-009); `from` ≤ `to` validado; `Winner` derivado de ledger rank 1, no campo cliente; `AverageResponseTime` solo sobre `Evaluated`; `Report.Read` delegado a OroIdentityServer; single-node; BuildingBlocks obligatorio

**Scale/Scope**: 5 reportes + leaderboard extendido; filtros 4 ejes combinables; volumen de referencia 10k juegos, lectura con `Specification` y paginación `page`/`pageSize`/`total`

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Principle | Status | Evidence / Mitigación |
|------|-----------|--------|------------------------|
| I. Domain First | Cálculos en dominio, no en controllers | ✅ PASS | `Accuracy`, `AverageResponseTime`, `Winner` calculados en `Game`/`Answer`/`PointTransaction` (no en `Api`); reportes solo proyectan. |
| II. Clean Architecture | `Web→Application→Domain←Infrastructure` | ✅ PASS | Domain define entidades y cálculos; Application define `IQuery`/`Specification`; Api expone `IEndpoint`; Infrastructure implementa `EfRepository`+`SpecificationEvaluator`. |
| III. BuildingBlocks No Reinvention | Reuso de plataforma | ✅ PASS | Reuso `Entity`/`Enumeration`/`Result`, `IRepository`/`Specification`, `IQuery`/`IQueryHandler`/`ISender`, `IEndpoint`; sin MediatR/MassTransit/AutoMapper. |
| IV. Vertical Slice + CQRS | Slices autocontenidos | ✅ PASS | Cada reporte es `Query`+`Handler`+`Response DTO`+`Endpoint` bajo `Features/Reporting/`; sin carpeta genérica. |
| V. Authoritative Domain Engine | Server truth, no trust cliente | ✅ PASS | `GameReport`/`PlayerReport`/`Leaderboard` usan `PointTransaction` ledger y `Answer.IsCorrect` de servidor, no campos cliente. |
| VI. OroIdentityServer | Identidad delegada | ✅ PASS | `PlayerId` es `sub` claim; `Report.Read`/`Audit.Read` mapeados a roles OroIdentityServer; sin tabla local `User`. |
| E. Persistence | EF, sin fuga | ✅ PASS | Lectura vía `IRepository`+`Specification` (Where/Include, ordering, pagination, AsNoTracking); `DbContext` deriva de `AppDbContextBase`; sin SQL crudo. |
| F. Concurrency & Idempotency | Solo lectura, no afecta | ✅ PASS | Reportes son `IQuery` sin `SaveChanges`; no crean `PointTransaction`/`AuditEntry` de escritura; idempotencia verificada por 0 side-effects (SC-005). |
| I. Validation/Errors/Observability | Validación + ProblemDetails | ✅ PASS | `from`/`to` validado (`from` ≤ `to`), `gameId`/`categoryId` inexistente → `NotFound` o vacío según `GameReport` vs agregado; `Result`→HTTP con `GlobalExceptionHandler`. |

**Gate Result: PASS — no violations. No complexity-tracking entries required.**

## Project Structure

### Documentation (this feature)

```text
specs/015-operational-reporting/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── reporting-api.md       # Endpoints IQuery para 5 reportes + leaderboard extendido
│   └── reporting-queries.md   # Definición de IQuery/IQueryHandler + Specifications
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── BuildingBlocks/                                   # EXISTING — platform (no modificar)
├── OroQuizClash.Domain/                              # EXISTING — Game, GamePlayer, GameRound, Answer, Question, Category, PointTransaction, Reward; EXTEND si falta cálculo Accuracy
│   └── Reporting/                                    # NEW (opcional) — value objects para cálculos si se extraen
├── OroQuizClash.Application/                         # EXTEND — queries de reporte
│   └── Features/Reporting/
│       ├── GameReport.cs                             # NEW — GetGameReportQuery + Handler + Response + Endpoint
│       ├── PlayerReport.cs                           # NEW — GetPlayerReportQuery + Handler
│       ├── QuestionReport.cs                         # NEW — GetQuestionReportQuery + Handler
│       ├── CategoryReport.cs                         # NEW — GetCategoryReportQuery + Handler
│       ├── RewardReport.cs                           # NEW — GetRewardReportQuery + Handler
│       └── LeaderboardExtended.cs                    # EXTEND — GetLeaderboard ya existente, añadir filtros Category/Period
├── OroQuizClash.Infrastructure/                      # EXTEND — Specifications para reportes
│   └── Specifications/
│       ├── GameReportSpecifications.cs               # NEW — GameByIdWithRounds, PlayerGamesByPeriod, etc.
│       ├── QuestionReportSpecifications.cs           # NEW — AnswersByQuestion/Period, RoundsByQuestion
│       └── ReportingSpecifications.cs                # NEW — Category/Reward agregados
└── OroQuizClash.Api/                                 # EXTEND — MapEndpoints para nuevos IEndpoint
tests/
├── OroQuizClash.Domain.Tests/                        # EXTEND — cálculos Accuracy, Winner, AverageResponseTime
├── OroQuizClash.Application.Tests/                   # EXTEND — handlers de reporte con datos InMemory, no side-effects
├── OroQuizClash.Api.Tests/                           # EXTEND — filtros Global/Game/Category/Period, 403 sin Report.Read
└── OroQuizClash.Architecture.Tests/                  # EXTEND — IQuery sin SaveChanges, Specification usada
```

**Structure Decision**: Modular monolith existente (opción web-service). Sin nuevos proyectos: se extiende `OroQuizClash.Application/Features/Reporting` con 5 slices `IQuery` + `Leaderboard` extendido. `OroQuizClash.Infrastructure` añade `Specification` de lectura; `Domain` no requiere nuevas tablas.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
