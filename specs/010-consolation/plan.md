# Implementation Plan: Consolation

**Branch**: `010-consolation` | **Date**: 2026-08-27 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/010-consolation/spec.md`

## Summary

Extender el mecanismo de consolación existente (`ConsolationPolicy` Enumeration + lógica básica en `Game.Finish()`) con reglas de elegibilidad configurables (mínimo de rondas, mínimo de preguntas respondidas), políticas adicionales (ParticipationBased, RewardBased), recompensa de consolación vía catálogo de premios (SPEC-009), y consultas de historial/estado. Se corrige un bug actual donde la consolación se otorga ANTES de determinar ganadores (inflando sus puntos). El nuevo flujo: determinar ganadores primero → evaluar elegibilidad → otorgar consolación a no-ganadores elegibles. Sin nuevos agregados — extiende `GameConfiguration` (ValueObject), `ConsolationPolicy` (Enumeration), `Game.Finish()` (comportamiento de dominio), y añade slices de consulta.

## Technical Context

**Language/Version**: C# 12 / .NET 10.0 (`net10.0`, `LangVersion latest`, `Nullable enable`, `ImplicitUsings enable`)

**Primary Dependencies**: `BuildingBlocks.Kernel.Domain` (Entity, Enumeration, ValueObject, IBusinessRule, Result/Error, IRepository, IDomainEvent), `BuildingBlocks.CQRS` (ICommand/IQuery, ICommandHandler/IQueryHandler, ISender), `BuildingBlocks.Kernel.Infrastructure` (AppDbContextBase, EfRepository, IUnitOfWork), `BuildingBlocks.ServiceDefaults` (IEndpoint, Result→HTTP), `OroIdentityServer` Podman (JWT)

**Storage**: SQLite local / SQL Server via Aspire; EF Core 10; `EnsureCreatedAsync`; `GameConfiguration` is an owned `ValueObject` on `Game`; `ConsolationPolicy` persisted as int via Enumeration conversion

**Testing**: xUnit v3 + NSubstitute + coverlet; Domain tests for eligibility rules + consolidation flow; Application tests for query handlers; Architecture tests for dependency rules

**Target Platform**: Linux containers (Podman), .NET Aspire AppHost, ASP.NET Core minimal APIs (IEndpoint)

**Project Type**: web-service — modular monolith (Domain / Application / Infrastructure / Api)

**Performance Goals**: SC-005 — consolidation evaluation <100ms within `Game.Finish()` transaction

**Constraints**: Consolation evaluated within `Game.Finish()` single transaction; no new aggregates; `GameConfiguration` immutable after game start; reward-based consolidation uses existing SPEC-009 `RewardRedemption` model; CONSOLATION `PointTransactionType` already exists (value 9)

**Scale/Scope**: 2–10 players per game; consolidation evaluated once per game finish; no cross-game aggregation

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Principle | Status | Evidence / Mitigación |
|------|-----------|--------|------------------------|
| I. Domain First | Consolidation rules en Domain | ✅ PASS | `ConsolationEligibilityRule` como `IBusinessRule`; `Game.Finish()` contiene la lógica de elegibilidad y otorgamiento; `ConsolationPolicy` como Enumeration. Application solo orquesta consultas. |
| II. Clean Architecture | `Web→Application→Domain←Infrastructure` | ✅ PASS | Domain extiende `GameConfiguration` y `ConsolationPolicy` sin referencias a infra; Application añade queries de lectura. |
| III. BuildingBlocks No Reinvention | Reuso Enumeration, IBusinessRule, IRepository | ✅ PASS | Extiende `Enumeration<ConsolationPolicy>` existente; usa `IBusinessRule` para elegibilidad; sin MediatR/MassTransit/AutoMapper. |
| IV. Vertical Slice + CQRS | Slices de lectura para consultas | ✅ PASS | `GetPlayerConsolationStatus` y `GetPlayerConsolationHistory` como slices `IQuery`; `Game.Finish()` es domain behavior. |
| V. Authoritative Domain Engine | Server truth para consolidación | ✅ PASS | Elegibilidad evaluada server-side en `Game.Finish()` usando datos del juego; cliente nunca determina elegibilidad. |
| VI. OroIdentityServer (Podman) | JWT única autoridad identidad | ✅ PASS | Endpoints de consulta requieren JWT; jugador solo ve su propia consolidación. |
| C. Configurable Game Rules | ConsolidationPolicy configurable | ✅ PASS | `ConsolationPolicy` como Enumeration configurable por juego; `GameConfiguration` incluye campos de elegibilidad. |
| D. Scoring via Ledger | CONSOLATION transaction | ✅ PASS | `PointTransactionType.Consolation` ya existe (value 9); `Game.Finish()` crea transacciones CONSOLATION. |
| E. Persistence | GameConfiguration como ValueObject | ✅ PASS | `GameConfiguration` ya es owned entity; nuevos campos se añaden al mismo ValueObject. |
| F. Concurrency | Game-level optimistic concurrency | ✅ PASS | `Game.RowVersion` protege `Finish()`; consolidación dentro de la misma transacción. |
| G. Real-Time/Outbox | Domain events in-process | ✅ PASS | `GameFinishedDomainEvent` ya existe; consolidación se registra como transacciones, no requiere evento separado. |
| H. Security Delegated | JWT jwks_uri | ✅ PASS | Endpoints de consulta con `RequireAuthorization`; jugador solo ve su historial. |
| I. Validation/Errors/Observability | 3 niveles, audit | ✅ PASS | `ConsolationEligibilityRule` (Domain) + consolidación como transacción auditable + detalles en Reason. |

**Gate Result: PASS — no violations. No complexity-tracking entries required.**

## Project Structure

### Documentation (this feature)

```text
specs/010-consolation/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   └── consolation.openapi.yaml
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── BuildingBlocks/                          # EXISTING — platform (no modificar)
├── OroQuizClash.Domain/                     # EXTEND — ConsolidationPolicy + GameConfiguration + Game.Finish()
│   ├── Games/
│   │   ├── Enumerations/
│   │   │   └── ConsolationPolicy.cs        # EXTEND — add ParticipationBased(4), RewardBased(5); rename Badge→RewardBased
│   │   ├── ValueObjects/
│   │   │   └── GameConfiguration.cs        # EXTEND — add MinimumParticipationRounds, MinimumAnsweredQuestions, ConsolationPoints, ConsolationRewardId
│   │   ├── Rules/
│   │   │   └── ConsolationEligibilityRule.cs # NEW — checks rounds, questions, winner, elimination
│   │   └── Game.cs                         # EXTEND — refactor Finish() consolidation logic (winners first, then consolidate)
│   └── Shared/Errors/
│       └── GameErrors.cs                   # EXTEND — ConsolidationRewardNotFound, InvalidConsolationConfiguration
├── OroQuizClash.Application/                # EXTEND — query slices
│   └── Features/
│       └── Games/
│           ├── GetPlayerConsolationStatus.cs   # NEW
│           └── GetPlayerConsolationHistory.cs  # NEW
├── OroQuizClash.Infrastructure/             # NO CHANGES — GameConfiguration persistence via existing ValueObject config
└── OroQuizClash.Api/                        # NO CHANGES — endpoints auto-discovered via IEndpoint
```

**Structure Decision**: Extender el contexto existente de Games sin crear un nuevo contexto. `ConsolationPolicy` se amplía con 2 valores nuevos (ParticipationBased, RewardBased). `GameConfiguration` recibe 4 campos nuevos (MinimumParticipationRounds, MinimumAnsweredQuestions, ConsolationPoints, ConsolationRewardId) — como es un ValueObject inmutable, el constructor se extiende y se añaden al `GetEqualityComponents`. `Game.Finish()` se refactoriza: (1) determinar ganadores ANTES de la consolación, (2) evaluar elegibilidad con `ConsolationEligibilityRule`, (3) otorgar puntos/recompensa. Se añaden 2 slices de consulta (IQuery) para estado e historial. Sin cambios en Infrastructure (GameConfiguration ya es owned entity, EF configura automáticamente). OroQuizClash.Api se beneficia de auto-discovery de endpoints.

## Complexity Tracking

> **No violations — table intentionally left empty.**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
