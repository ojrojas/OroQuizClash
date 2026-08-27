# Implementation Plan: Player Withdrawal

**Branch**: `008-player-withdrawal` | **Date**: 2026-08-27 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/008-player-withdrawal/spec.md`

## Summary

Formalizar el ciclo de vida de participación del jugador introduciendo `PlayerParticipationStatus` (ACTIVE, WITHDRAWN, ELIMINATED, WINNER) que reemplaza el flag booleano `IsWithdrawn` implementado en SPEC-007. Se extiende `Game.WithdrawPlayer()` con validación completa (no doble retiro, no retiro tras eliminación, no retiro en juego terminal, no retiro tras participación finalizada), se añade determinación de WINNER al finalizar el juego, y se garantiza la exclusión de jugadores retirados/eliminados de rondas, bonuses y premios. La mecánica de deducción de puntos por política de retiro ya existe (SPEC-007) y se reutiliza sin cambios. Implementación como extensión del agregado `Game` existente + Vertical Slice `WithdrawPlayer` ya creado, con concurrencia optimista vía `rowversion`.

## Technical Context

**Language/Version**: C# 12 / .NET 10.0 (`net10.0`, `LangVersion latest`, `Nullable enable`, `ImplicitUsings enable`)

**Primary Dependencies**: `BuildingBlocks.Kernel.Domain` (AggregateRoot, Entity, ValueObject, StronglyTypedId, Enumeration, IBusinessRule, Result/Error, IRepository, Specification, IDomainEvent), `BuildingBlocks.CQRS` (ICommand/IQuery, ICommandHandler/IQueryHandler, ISender, IPipelineBehavior ValidationBehavior), `BuildingBlocks.Kernel.Infrastructure` (AppDbContextBase, EfRepository, IUnitOfWork), `BuildingBlocks.ServiceDefaults` (OpenTelemetry, health, IEndpoint, Result→HTTP, GlobalExceptionHandler, ProblemDetails), `OroIdentityServer` Podman (JWT Authority)

**Storage**: SQL Server (primario, `rowversion`); EF Core 10 sobre `OroQuizClashDbContext : AppDbContextBase`; `EfRepository<Game,GameId>` existente; Oracle como target secundario vía abstracción

**Testing**: xUnit v3 + NSubstitute + coverlet; Domain unit tests para validación de retiro + status lifecycle + winner determination; Application tests para Handler; Architecture tests para dependency rules

**Target Platform**: Linux containers (Podman), .NET Aspire AppHost, ASP.NET Core minimal APIs (IEndpoint)

**Project Type**: web-service — modular monolith (Domain / Application / Infrastructure / Api)

**Performance Goals**: SC-004 — withdrawal decision <5s (realistically <200ms p95); SC-007 — zero race-condition failures

**Constraints**: <200ms p95; concurrencia optimista obligatoria (`rowversion`); retiro atómico e irreversible; solo JWT autenticado; mapeo explícito (no AutoMapper); sin MediatR/MassTransit

**Scale/Scope**: 10–1k juegos concurrentes, 2–10 jugadores/juego

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Principle | Status | Evidence / Mitigación |
|------|-----------|--------|------------------------|
| I. Domain First | Withdrawal + participation status en Domain | ✅ PASS | `Game.WithdrawPlayer()` como domain behavior con `IBusinessRule`; `PlayerParticipationStatus` como Enumeration; transiciones protegidas dentro del agregado. Application solo orquesta. |
| II. Clean Architecture | `Web→Application→Domain←Infrastructure` | ✅ PASS | Domain solo `BuildingBlocks.Kernel.Domain`; Application referencia Domain+CQRS; Infrastructure implementa persistence. |
| III. BuildingBlocks No Reinvention | Reuso Kernel/CQRS/Infrastructure/ServiceDefaults | ✅ PASS | Usa `Entity`, `Enumeration`, `Result`, `IRepository`, `ICommand/ISender`, `IEndpoint`; prohíbe MediatR/MassTransit/AutoMapper; `net10.0`. |
| IV. Vertical Slice + CQRS | Slice autocontenido | ✅ PASS | `Features/Games/WithdrawPlayer.cs` existente (SPEC-007) se extiende; sin dispatcher secundario. |
| V. Authoritative Domain Engine | Server truth para withdrawal | ✅ PASS | FR-017: solo servidor decide; validación de estado server-side; cliente nunca determina outcome. |
| VI. OroIdentityServer (Podman) | JWT única autoridad identidad | ✅ PASS | `POST /api/games/{id}/withdraw` requiere JWT; `sub` claim identifica al jugador. |
| A. Game Lifecycle State Machine | Withdrawal solo en estados no-terminales | ✅ PASS | FR-008: rechazo en FINISHED/CANCELLED/FORCED_FINISHED; ya implementado en SPEC-007, se mantiene. |
| C. Configurable Game Rules | WithdrawalPolicy configurable | ✅ PASS | 4 políticas (`LoseAll`, `KeepCurrentScore`, `KeepSecuredScore`, `KeepCheckpointScore`) como Enumeration + strategies (SPEC-007, sin cambios). |
| D. Scoring via Ledger | WITHDRAWAL transaction | ✅ PASS | Ya implementado en SPEC-007: toda deducción por retiro genera `PointTransaction` tipo WITHDRAWAL. |
| E/F. Persistence & Concurrency | rowversion, atomicidad | ✅ PASS | `Game.RowVersion` protege concurrencia; retiro atómico (FR-016); retiros simultáneos procesados independientemente. |
| G. Real-Time/Outbox | Domain events in-process | ✅ PASS | `PlayerWithdrawnDomainEvent` dispatch en `SaveChanges`; notificación a jugadores restantes vía evento. |
| H. Security Delegated | JWT jwks_uri | ✅ PASS | Endpoint requiere JWT bearer; validación `sub` contra `GamePlayer.UserId`. |
| I. Validation/Errors/Observability | 3 niveles, ProblemDetails, audit | ✅ PASS | Validator (API) + IBusinessRule (Domain) + Error→ProblemDetails (400/404/409); audit vía WithdrawalRecord (PointTransaction + status). |

**Gate Result: PASS — no violations. No complexity-tracking entries required.**

## Project Structure

### Documentation (this feature)

```text
specs/008-player-withdrawal/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   └── withdrawal.openapi.yaml
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── BuildingBlocks/                          # EXISTING — platform (no modificar)
├── OroQuizClash.Domain/                     # EXTEND — participation status + winner
│   ├── Games/
│   │   ├── Game.cs                         # EXTEND — WithdrawPlayer validation + DetermineWinners at Finish
│   │   ├── GamePlayer.cs                   # EXTEND — replace IsWithdrawn with ParticipationStatus
│   │   ├── Enumerations/
│   │   │   └── PlayerParticipationStatus.cs # NEW — ACTIVE(1), WITHDRAWN(2), ELIMINATED(3), WINNER(4)
│   │   ├── Rules/
│   │   │   ├── PlayerAlreadyEliminatedRule.cs      # NEW
│   │   │   └── ParticipationAlreadyFinishedRule.cs # NEW
│   │   └── Events/
│   │       └── PlayerWithdrawnDomainEvent.cs       # NEW — GameId, PlayerId, RetainedPoints, Policy
│   └── Shared/Errors/
│       └── GameErrors.cs                   # EXTEND — PlayerAlreadyEliminated, ParticipationAlreadyFinished
├── OroQuizClash.Application/                # EXTEND — WithdrawPlayer slice update
│   └── Features/
│       └── Games/
│           └── WithdrawPlayer.cs           # EXTEND — expose participation status in response
├── OroQuizClash.Infrastructure/             # EXTEND — Persistence
│   └── Persistence/
│       └── Configurations/
│           └── GamePlayerTypeConfiguration.cs # EXTEND — ParticipationStatus column (replaces IsWithdrawn)
├── OroQuizClash.Api/                        # EXISTING — Host (no cambios)
└── OroQuizClash.AppHost/                    # EXISTING — Aspire (no cambios)
```

**Structure Decision**: Extender el agregado `Game` existente. `PlayerParticipationStatus` reemplaza `IsWithdrawn`/`WithdrawnAt` booleanos con una Enumeration de 4 estados + timestamp de salida. La mecánica de scoring del retiro (SPEC-007) permanece intacta — solo se extiende la validación y el lifecycle. `PlayerEliminated` (ELIMINATED) se introduce como operación de dominio pero su trigger concreto (reglas de eliminación) queda fuera de scope — se expone el método para uso futuro de SPEC-009/010. Winner determination ocurre en `Game.Finish()`: el jugador ACTIVE con mayor puntuación recibe status WINNER. BuildingBlocks permanece como dependencia externa; OroIdentityServer consumido como container Podman.

## Complexity Tracking

> **No violations — table intentionally left empty.**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
