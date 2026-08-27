# Implementation Plan: Scoring System

**Branch**: `007-scoring-system` | **Date**: 2026-08-27 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/007-scoring-system/spec.md`

## Summary

Extender el agregado `Game` con un sistema de scoring completo basado en ledger (`PointTransaction` append-only con 10 tipos), introduciendo operaciones de dominio explícitas (`AwardPoints`, `RemovePoints`, `SecurePoints`, `ConsumePoints`) que reemplazan cualquier mutación directa de balance. Se extiende `GamePlayer` con un `PlayerScore` (ValueObject) que trackea `CurrentPoints`, `SecuredPoints`, `RoundPoints`, `PotentialPoints`, `TotalPoints`. Se implementan las 4 loss policies y 4 withdrawal policies como estrategias de dominio, bonus de ronda/nivel/juego, consolation points, y ajustes administrativos. El balance es siempre reconstruible desde el historial de transacciones. Implementación como Vertical Slices `BuildingBlocks.CQRS` + `EfRepository<Game>` + `rowversion` para concurrencia, autenticado vía OroIdentityServer.

## Technical Context

**Language/Version**: C# 12 / .NET 10.0 (`net10.0`, `LangVersion latest`, `Nullable enable`, `ImplicitUsings enable`)

**Primary Dependencies**: `BuildingBlocks.Kernel.Domain` (AggregateRoot, Entity, ValueObject, StronglyTypedId, Enumeration, IBusinessRule, Result/Error, IRepository, Specification, IDomainEvent), `BuildingBlocks.CQRS` (ICommand/IQuery, ICommandHandler/IQueryHandler, ISender, IPipelineBehavior ValidationBehavior), `BuildingBlocks.Kernel.Infrastructure` (AppDbContextBase, EfRepository, SpecificationEvaluator, IUnitOfWork, IOutboxWriter), `BuildingBlocks.ServiceDefaults` (OpenTelemetry, health, IEndpoint, Result→HTTP, GlobalExceptionHandler, ProblemDetails), `OroIdentityServer` Podman (JWT Authority)

**Storage**: SQL Server (primario, `rowversion` + indexes `GameId/PlayerId`); EF Core 10 sobre `OroQuizClashDbContext : AppDbContextBase`; `EfRepository<Game,GameId>` existente; `Specification<Game>` con `Include(Rounds/Players/Answers/PointTransactions)`; Oracle como target secundario vía abstracción

**Testing**: xUnit v3 + NSubstitute + Testcontainers.MsSql + coverlet; Domain unit tests para operaciones de scoring + policies + inmutabilidad ledger + reconstrucción balance; Application tests para Handlers con IRepository mock; Integration tests para concurrencia rowversion + atomicidad ConsumePoints; Architecture tests para dependency rules

**Target Platform**: Linux containers (Podman), .NET Aspire AppHost, ASP.NET Core minimal APIs (IEndpoint)

**Project Type**: web-service — modular monolith (Domain / Application / Infrastructure / Api)

**Performance Goals**: SC-001→SC-009; scoring operations <200ms p95; ledger reconstruction <500ms para 10k transacciones

**Constraints**: <200ms p95 scoring pipeline; concurrencia optimista obligatoria (`rowversion`); `PointTransaction` append-only (no update/delete); balance nunca negativo (salvo política explícita); solo JWT autenticado; mapeo explícito (no AutoMapper); sin MediatR/MassTransit

**Scale/Scope**: 10–1k juegos concurrentes, 2–10 jugadores/juego, 5–50 rondas/juego, ~50 transacciones/jugador/juego, 10k `PointTransaction` por juego

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Principle | Status | Evidence / Mitigación |
|------|-----------|--------|------------------------|
| I. Domain First | Scoring operations, policies, ledger en Domain | ✅ PASS | `Game.AwardPoints()`, `Game.RemovePoints()`, `Game.SecurePoints()`, `Game.ConsumePoints()` como domain behavior; `PlayerScore` como ValueObject; loss/withdrawal policies como estrategias de dominio; `PointTransaction` append-only. Application solo orquesta. |
| II. Clean Architecture | `Web→Application→Domain←Infrastructure`, Domain sin refs Infra/Web | ✅ PASS | Domain solo `BuildingBlocks.Kernel.Domain`; Application referencia Domain+CQRS+`IRepository`; Infrastructure implementa persistence; Api referencia Application+Infrastructure+ServiceDefaults. |
| III. BuildingBlocks No Reinvention | Reuso Kernel/CQRS/Infrastructure/ServiceDefaults | ✅ PASS | Usa `Entity`, `AggregateRoot`, `ValueObject`, `StronglyTypedId`, `Enumeration`, `Result`, `IRepository`, `Specification`, `ICommand/ISender`, `AppDbContextBase`, `IEndpoint`; prohíbe MediatR/MassTransit/AutoMapper; `net10.0`. |
| IV. Vertical Slice + CQRS | Slice autocontenido, sin dispatcher secundario | ✅ PASS | `Features/Games/GetPlayerScore.cs` (extender), `Features/Games/GetScoreLedger.cs` (nuevo), `Features/Games/AdjustScore.cs` (nuevo); cada uno autocontenido con mapping local. |
| V. Authoritative Domain Engine | Server truth para scoring | ✅ PASS | Todas las operaciones de scoring son server-side dentro del agregado `Game`; cliente NUNCA determina puntos; `ConsumePoints` valida balance server-side. |
| VI. OroIdentityServer (Podman) | `oroidentityserver:latest` única autoridad identidad | ✅ PASS | Endpoints requieren JWT bearer; `AdjustScore` requiere policy `AdminOrGameManager`; sin user store local. |
| A. Game Lifecycle State Machine | Scoring opera solo en estados válidos | ✅ PASS | `AwardPoints`/`RemovePoints` solo en `ROUND_IN_PROGRESS`; `SecurePoints` en `ROUND_COMPLETED`; `ConsumePoints` en `FINISHED`; `Withdrawal` solo en estados activos. |
| C. Configurable Game Rules | Policies configurables, no hardcodeadas | ✅ PASS | `LossPolicy` (4 valores), `WithdrawalPolicy` (4 valores), `ConsolationPolicy` (3 valores), `ScoringSystem` (2 valores) — todas como `Enumeration` en `GameConfiguration`. |
| D. Scoring via Ledger | PointTransaction append-only, no mutación directa | ✅ PASS | Toda modificación de puntos genera `PointTransaction`; 10 tipos; balance reconstruible; append-only enforced por domain invariant. |
| E/F. Persistence & Concurrency | SQL Server, rowversion, Specification, Outbox | ✅ PASS | `PointTransaction` como composición en `Game`; `RowVersion` en `Game` protege concurrencia; `UNIQUE` constraints previenen duplicados; Outbox misma transacción. |
| G. Real-Time/Outbox | Domain events in-process, Integration events via Outbox | ✅ PASS | `ScoreUpdatedDomainEvent` dispatch en `SaveChanges`; opcional integration event vía `IOutboxWriter`→RabbitMQ. |
| H. Security Delegated | JWT jwks_uri, role-based | ✅ PASS | `GetPlayerScore`/`GetScoreLedger` requieren JWT; `AdjustScore` requiere `AdminOrGameManager` policy. |
| I. Validation/Errors/Observability | 3 niveles, ProblemDetails, OTel, audit | ✅ PASS | `Validator` (API) + `IBusinessRule` (Domain) + `Error→ProblemDetails` (`400` validación, `404` not found, `409` conflicto, `422` insufficient points); OTel `CorrelationId/GameId/PlayerId`; audit append-only. |

**Gate Result: PASS — no violations. No complexity-tracking entries required.**

## Project Structure

### Documentation (this feature)

```text
specs/007-scoring-system/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── scoring-query.openapi.yaml
│   └── scoring-adjust.openapi.yaml
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── BuildingBlocks/                          # EXISTING — platform (no modificar)
├── OroQuizClash.Domain/                     # EXTEND — Scoring operations + PlayerScore
│   ├── Games/
│   │   ├── Game.cs                         # AggregateRoot<GameId> — extend con AwardPoints, RemovePoints, SecurePoints, ConsumePoints, WithdrawPlayer scoring
│   │   ├── GamePlayer.cs                   # Entity<GamePlayerId> — extend con PlayerScore
│   │   ├── PointTransaction.cs             # Entity<PointTransactionId> — extend con ResultingBalance, Reason
│   │   ├── PointTransactionId.cs           # StronglyTypedId<Guid> (exists)
│   │   ├── ValueObjects/
│   │   │   ├── PlayerScore.cs              # NEW — CurrentPoints, SecuredPoints, RoundPoints, PotentialPoints, TotalPoints
│   │   │   └── GameConfiguration.cs        # EXISTING — ya tiene PointsPerRound, policies
│   │   ├── Enumerations/
│   │   │   ├── PointTransactionType.cs     # EXTEND — agregar GAME_BONUS, PENALTY, WITHDRAWAL, REWARD_REDEMPTION, CONSOLATION, ADJUSTMENT
│   │   │   ├── LossPolicy.cs              # EXISTING (4 valores)
│   │   │   ├── WithdrawalPolicy.cs        # EXISTING (4 valores)
│   │   │   ├── ConsolationPolicy.cs       # EXISTING (3 valores)
│   │   │   └── ScoringSystem.cs           # EXISTING (2 valores)
│   │   ├── Rules/
│   │   │   ├── BalanceCannotGoNegativeRule.cs    # NEW
│   │   │   ├── SufficientBalanceRule.cs          # NEW — para ConsumePoints
│   │   │   ├── AdjustmentReasonRequiredRule.cs   # NEW
│   │   │   └── ScoringStateValidRule.cs          # NEW — scoring solo en estados válidos
│   │   ├── Strategies/
│   │   │   ├── ILossPolicyStrategy.cs            # NEW — interface para loss policies
│   │   │   ├── LoseAllStrategy.cs                # NEW
│   │   │   ├── LoseCurrentRoundStrategy.cs       # NEW
│   │   │   ├── LoseUnsecuredPointsStrategy.cs    # NEW
│   │   │   ├── FallbackToCheckpointStrategy.cs   # NEW
│   │   │   ├── IWithdrawalPolicyStrategy.cs      # NEW — interface para withdrawal policies
│   │   │   ├── WithdrawLoseAllStrategy.cs        # NEW
│   │   │   ├── WithdrawKeepCurrentStrategy.cs    # NEW
│   │   │   ├── WithdrawKeepSecuredStrategy.cs    # NEW
│   │   │   └── WithdrawKeepCheckpointStrategy.cs # NEW
│   │   └── Events/
│   │       ├── ScoreUpdatedDomainEvent.cs        # NEW
│   │       └── PointsSecuredDomainEvent.cs       # NEW
│   └── Shared/Errors/
│       └── GameErrors.cs                   # EXTEND — InsufficientPoints, InvalidScoringState, AdjustmentReasonRequired
├── OroQuizClash.Application/                # EXTEND — Vertical Slices Scoring
│   └── Features/
│       └── Games/
│           ├── GetPlayerScore.cs           # REWRITE — extender con breakdown completo (Current/Secured/Round/Potential/Total)
│           ├── GetScoreLedger.cs           # NEW — Query ledger completo por jugador
│           └── AdjustScore.cs             # NEW — Command ajuste administrativo
├── OroQuizClash.Infrastructure/             # EXTEND — Persistence
│   ├── Persistence/
│   │   └── Configurations/
│   │       ├── GameTypeConfiguration.cs    # EXTEND — PointTransaction ResultingBalance, Reason columns
│   │       └── GamePlayerTypeConfiguration.cs # EXTEND — PlayerScore owned entity
│   └── Specifications/
│       └── GameByIdWithAnswersSpecification.cs # EXISTING — ya incluye PointTransactions
├── OroQuizClash.Api/                        # EXISTING — Host (endpoints scoring)
│   └── Program.cs                          # wiring scoring strategies DI
└── OroQuizClash.AppHost/                    # EXISTING — Aspire (no cambios)
```

**Structure Decision**: Extender el modular monolith existente de `001–006`. `PlayerScore` es un ValueObject owned por `GamePlayer` (composición dentro del agregado `Game`). `PointTransaction` se extiende con `ResultingBalance` y `Reason`. Las loss/withdrawal policies se implementan como estrategias de dominio dentro del agregado (no como servicios externos) porque son invariantes del aggregate. `GetPlayerScore` se reescribe para exponer el breakdown completo. `GetScoreLedger` y `AdjustScore` son nuevos vertical slices. BuildingBlocks permanece como dependencia externa; OroIdentityServer consumido como container Podman.

## Complexity Tracking

> **No violations — table intentionally left empty.**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
