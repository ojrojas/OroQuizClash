# Implementation Plan: Rewards & Point Redemption

**Branch**: `009-reward-redemption` | **Date**: 2026-08-27 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/009-reward-redemption/spec.md`

## Summary

Nuevo contexto limitado `Rewards` (catálogo de premios + redenciones) independiente del agregado `Game`, financiado por el saldo de puntos per-game del jugador vía la operación existente `Game.ConsumePoints` (SPEC-007, transacción `REWARD_REDEMPTION`). Se introducen los agregados `Reward` (nombre, descripción, puntos requeridos, stock, estado ACTIVE/INACTIVE, expiración) y `RewardRedemption` (lifecycle REQUESTED → APPROVED → DELIVERED con salidas REJECTED/CANCELLED, historial de transiciones para auditoría RWD-006). La redención es atómica (RWD-003): reserva de stock + deducción de puntos + creación de redención en una única transacción de `IUnitOfWork` con concurrencia optimista (`RowVersion`) sobre ambos agregados. Rechazo/cancelación devuelven los puntos mediante nueva operación `Game.RefundPoints` (transacción ADJUSTMENT positiva, ledger append-only) y liberan el stock. Slices verticales bajo `Features/Rewards/` + endpoints `GET /api/rewards` y `POST /api/rewards/{id}/redeem` (Constitución §J).

## Technical Context

**Language/Version**: C# 12 / .NET 10.0 (`net10.0`, `LangVersion latest`, `Nullable enable`, `ImplicitUsings enable`)

**Primary Dependencies**: `BuildingBlocks.Kernel.Domain` (AggregateRoot, Entity, ValueObject, StronglyTypedId, Enumeration, IBusinessRule, Result/Error, IRepository, Specification, IDomainEvent), `BuildingBlocks.CQRS` (ICommand/IQuery, ICommandHandler/IQueryHandler, ISender, IPipelineBehavior ValidationBehavior), `BuildingBlocks.Kernel.Infrastructure` (AppDbContextBase, EfRepository, IUnitOfWork, Outbox), `BuildingBlocks.ServiceDefaults` (OpenTelemetry, health, IEndpoint, Result→HTTP, GlobalExceptionHandler, ProblemDetails), `OroIdentityServer` Podman (JWT Authority)

**Storage**: SQLite local (`oroclash.db`) / SQL Server vía Aspire (detección por connection string); EF Core 10 sobre `OroQuizClashDbContext : AppDbContextBase`; `EnsureCreatedAsync` (sin migraciones); `EfRepository<TAggregate,TId>`; nuevos `IRepository<Reward,RewardId>` e `IRepository<RewardRedemption,RewardRedemptionId>`

**Testing**: xUnit v3 + NSubstitute + coverlet; Domain unit tests para disponibilidad de Reward + lifecycle de Redemption + `Game.RefundPoints`; Application tests para handlers de redención/procesamiento; Architecture tests para dependency rules del nuevo contexto

**Target Platform**: Linux containers (Podman), .NET Aspire AppHost, ASP.NET Core minimal APIs (IEndpoint)

**Project Type**: web-service — modular monolith (Domain / Application / Infrastructure / Api)

**Performance Goals**: SC-001 — redención completa <30s UX (realistamente <200ms p95); SC-003 — cero oversells bajo contención de último stock

**Constraints**: <200ms p95; atomicidad cross-aggregate en una transacción `IUnitOfWork`; concurrencia optimista obligatoria (`RowVersion` en Reward y Game); ledger append-only (deducción REWARD_REDEMPTION, reembolso ADJUSTMENT); solo JWT autenticado; mapeo explícito (no AutoMapper); sin MediatR/MassTransit

**Scale/Scope**: catálogo 10–100 premios; ráfagas de redención al finalizar partidas (decenas de solicitudes concurrentes por segundo)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Principle | Status | Evidence / Mitigación |
|------|-----------|--------|------------------------|
| I. Domain First | Reward eligibility & redemption en Domain | ✅ PASS | `Reward.ReserveStock/ReleaseStock`, `RewardRedemption.Approve/Reject/Deliver/Cancel` como domain behavior con `IBusinessRule`; `RedemptionStatus`/`RewardStatus` como Enumeration; `Game.RefundPoints` como operación de dominio. Application solo orquesta. |
| II. Clean Architecture | `Web→Application→Domain←Infrastructure` | ✅ PASS | Nuevo folder `Domain/Rewards/` sin referencias a infra; Application referencia Domain+CQRS; Infrastructure implementa persistence (configuraciones EF, specifications). |
| III. BuildingBlocks No Reinvention | Reuso Kernel/CQRS/Infrastructure/ServiceDefaults | ✅ PASS | Usa `AggregateRoot`, `Entity`, `Enumeration`, `StronglyTypedId`, `Result`, `IRepository`, `IUnitOfWork`, `ICommand/ISender`, `IEndpoint`; prohíbe MediatR/MassTransit/AutoMapper; `net10.0`. |
| IV. Vertical Slice + CQRS | Slices autocontenidos | ✅ PASS | `Features/Rewards/{CreateReward,UpdateReward,ActivateReward,DeactivateReward,GetRewards,RedeemReward,GetPlayerRedemptions,GetRedemptions,ApproveRedemption,RejectRedemption,DeliverRedemption,CancelRedemption}.cs`; sin dispatcher secundario. |
| V. Authoritative Domain Engine | Server truth para redención | ✅ PASS | Validez (saldo, stock, expiración) evaluada server-side con timestamp del servidor; cliente nunca determina disponibilidad. |
| VI. OroIdentityServer (Podman) | JWT única autoridad identidad | ✅ PASS | Todos los endpoints requieren JWT; `sub` claim identifica al jugador; política `AdminOrRewardManager` para gestión. |
| C. Configurable Game Rules | Rewards independientemente modelados | ✅ PASS | `Reward`/`RewardRedemption` como agregados propios con lifecycle REQUESTED→APPROVED→DELIVERED + REJECTED/CANCELLED; redención atómica y bloqueada sin puntos elegibles (RWD-001..005). |
| D. Scoring via Ledger | REWARD_REDEMPTION ledger-backed | ✅ PASS | Deducción vía `Game.ConsumePoints` existente (tipo REWARD_REDEMPTION, SPEC-007); reembolso vía `Game.RefundPoints` nuevo (tipo ADJUSTMENT, ledger append-only). Saldo reconstruible. |
| E. Persistence | Integridad + índices | ✅ PASS | FK RewardRedemption→Reward; índice único filtrado `IdempotencyKey`; índices por Player/Status; `RowVersion` en Reward. |
| F. Concurrency & Idempotency | Atomicidad, sin oversell | ✅ PASS | Una transacción `SaveChanges` para Game+Reward+Redemption; `RowVersion` en ambos agregados → contención de último stock falla limpiamente (409); `IdempotencyKey` evita doble deducción por envío duplicado. |
| G. Real-Time/Outbox | Domain events in-process | ✅ PASS | `RewardRedeemedDomainEvent` + `RedemptionStatusChangedDomainEvent` dispatch en `SaveChanges`; candidatos a integration event (`RewardRedeemed`) vía Outbox cuando se active EventBus. |
| H. Security Delegated | JWT jwks_uri + políticas | ✅ PASS | Endpoints con `RequireAuthorization`; política nueva `AdminOrRewardManager` (roles ADMIN/REWARD_MANAGER) siguiendo patrón `AdminOrGameManager` existente; jugador solo opera sus propias redenciones. |
| I. Validation/Errors/Observability | 3 niveles, ProblemDetails, audit | ✅ PASS | Validator (API/Application) + IBusinessRule (Domain) + `RewardErrors`→ProblemDetails (400/404/409); auditoría vía `RedemptionTransition` (actor + timestamp por transición) + ledger. |

**Gate Result: PASS — no violations. No complexity-tracking entries required.**

## Project Structure

### Documentation (this feature)

```text
specs/009-reward-redemption/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   └── rewards.openapi.yaml
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── BuildingBlocks/                          # EXISTING — platform (no modificar)
├── OroQuizClash.Domain/                     # EXTEND — nuevo contexto Rewards + Game.RefundPoints
│   ├── Games/
│   │   └── Game.cs                         # EXTEND — RefundPoints(playerId, amount, reason)
│   ├── Rewards/                             # NEW — bounded context
│   │   ├── Reward.cs                       # NEW — AggregateRoot<RewardId>
│   │   ├── RewardId.cs                     # NEW — StronglyTypedId
│   │   ├── RewardStatus.cs                 # NEW — ACTIVE(1), INACTIVE(2)
│   │   ├── RewardRedemption.cs             # NEW — AggregateRoot<RewardRedemptionId>
│   │   ├── RewardRedemptionId.cs           # NEW
│   │   ├── RedemptionStatus.cs             # NEW — REQUESTED(1)..CANCELLED(5)
│   │   ├── RedemptionTransition.cs         # NEW — Entity<RedemptionTransitionId> (audit)
│   │   ├── RedemptionTransitionId.cs       # NEW
│   │   ├── RewardErrors.cs                 # NEW — error codes del contexto
│   │   ├── Rules/
│   │   │   ├── RewardNameValidRule.cs              # NEW
│   │   │   ├── PointsRequiredPositiveRule.cs       # NEW
│   │   │   ├── StockNotNegativeRule.cs             # NEW
│   │   │   ├── RewardAvailableRule.cs              # NEW — active + stock + expiration
│   │   │   └── RedemptionTransitionRule.cs         # NEW — state machine
│   │   └── Events/
│   │       ├── RewardCreatedDomainEvent.cs         # NEW
│   │       ├── RewardUpdatedDomainEvent.cs         # NEW
│   │       ├── RewardStatusChangedDomainEvent.cs   # NEW
│   │       ├── RewardRedeemedDomainEvent.cs        # NEW
│   │       └── RedemptionStatusChangedDomainEvent.cs # NEW
│   └── Shared/Errors/                      # EXISTING (sin cambios — errores en RewardErrors)
├── OroQuizClash.Application/                # EXTEND — slices de Rewards
│   └── Features/
│       └── Rewards/                         # NEW
│           ├── CreateReward.cs
│           ├── UpdateReward.cs
│           ├── ActivateReward.cs
│           ├── DeactivateReward.cs
│           ├── GetRewards.cs
│           ├── RedeemReward.cs
│           ├── GetPlayerRedemptions.cs
│           ├── GetRedemptions.cs
│           ├── ApproveRedemption.cs
│           ├── RejectRedemption.cs
│           ├── DeliverRedemption.cs
│           └── CancelRedemption.cs
├── OroQuizClash.Infrastructure/             # EXTEND — persistence Rewards
│   ├── Persistence/
│   │   ├── OroQuizClashDbContext.cs        # EXTEND — DbSets Rewards, RewardRedemptions
│   │   └── Configurations/
│   │       ├── RewardTypeConfiguration.cs          # NEW
│   │       └── RewardRedemptionTypeConfiguration.cs # NEW (+ RedemptionTransition)
│   └── Specifications/
│       ├── RewardSpecifications.cs         # NEW
│       └── RedemptionSpecifications.cs     # NEW
├── OroQuizClash.Api/                        # EXTEND — DI + política
│   └── Program.cs                          # EXTEND — repos Reward/RewardRedemption + política AdminOrRewardManager
└── OroQuizClash.AppHost/                    # EXISTING — Aspire (no cambios)

tests/
├── OroQuizClash.Domain.Tests/Rewards/       # NEW — Reward availability, redemption lifecycle, stock, refund
├── OroQuizClash.Application.Tests/Features/Rewards/ # NEW — handlers redención/procesamiento/catálogo
└── OroQuizClash.Architecture.Tests/         # EXTEND — RewardDependenciesTests
```

**Structure Decision**: Nuevo contexto limitado `Rewards` en Domain, paralelo a `Categories`/`Questions`/`Games`, siguiendo exactamente las convenciones existentes (AggregateRoot + RowVersion, Enumeration para estados, Errors estáticos, Rules como IBusinessRule, Events). El agregado `Game` solo se extiende con `RefundPoints` (reembolso ledger-backed); la deducción reutiliza `ConsumePoints` de SPEC-007 sin cambios. La orquestación cross-aggregate (Game + Reward + RewardRedemption en una transacción) vive en el handler de `RedeemReward` — permitida en Application, con invariantes protegidas dentro de cada agregado. `RewardRedemption` referencia el `GameId` que financió la redención (trazabilidad ledger) y mantiene historial de transiciones como entidades hijas para auditoría. BuildingBlocks permanece como dependencia externa; OroIdentityServer consumido como container Podman.

## Complexity Tracking

> **No violations — table intentionally left empty.**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
