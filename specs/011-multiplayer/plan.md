# Implementation Plan: Multiplayer

**Branch**: `011-multiplayer` | **Date**: 2026-08-27 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/011-multiplayer/spec.md`

## Summary

Completar el contrato multiplayer del juego: el estado individual por jugador ya existe parcialmente (`GamePlayer` con `UserId`, `ParticipationStatus`, `PlayerScore`; idempotencia de respuesta por jugador+ronda; ledger atómico `PointTransaction`; `Game.RowVersion`), pero faltan piezas exigidas por SPEC-011. Este plan extiende `GamePlayer` con `CurrentRoundNumber` (avanza en `StartRound`, se congela en retiro/eliminación) y expone `AnswerState` derivado de las entidades `Answer` existentes (fuente única de verdad, sin duplicar estado); aplica el aislamiento entre jugadores cableando la identidad JWT `sub` en `SubmitAnswer` (hoy usa `Guid.Empty` placeholder) y `WithdrawPlayer` con nuevo error `PlayerIdentityMismatch`; completa el manejo de `DbUpdateConcurrencyException` → `ConcurrencyConflict` (409) en los handlers que aún no lo tienen; extiende `GetLeaderboard` con `CorrectAnswers`, `CurrentLevel`, `Status` y desempate determinista (Points desc → CorrectAnswers desc → consecución más temprana); añade el slice `GetPlayerState` (FR-015) y notificaciones server-driven vía SignalR (`GameHub` + port `IGameNotificationsBroadcaster` + handlers de domain events). Sin nuevos agregados — todo extiende el agregado `Game` existente.

## Technical Context

**Language/Version**: C# 12 / .NET 10.0 (`net10.0`, `LangVersion latest`, `Nullable enable`, `ImplicitUsings enable`)

**Primary Dependencies**: `BuildingBlocks.Kernel.Domain` (Entity, AggregateRoot, Enumeration, ValueObject, IBusinessRule, Result/Error, IRepository, Specification, IDomainEvent), `BuildingBlocks.CQRS` (ICommand/IQuery, handlers, ISender, IDomainEventHandler, ValidationBehavior), `BuildingBlocks.Kernel.Infrastructure` (AppDbContextBase, EfRepository, IUnitOfWork), `BuildingBlocks.ServiceDefaults` (IEndpoint, Result→HTTP, GlobalExceptionHandler), ASP.NET Core SignalR (shared framework, sin paquete adicional), `OroIdentityServer` Podman (JWT `sub` claim)

**Storage**: SQLite local / SQL Server via Aspire; EF Core 10; `EnsureCreatedAsync` (sin migraciones); `GamePlayer` es `Entity` dentro del agregado `Game` con índice único `(GameId, UserId)`; `Answer` con índice único `(GameId, PlayerId, RoundId)` y `RowVersion`; `Game.RowVersion` como token de concurrencia del agregado

**Testing**: xUnit v3 + NSubstitute + coverlet; Domain tests para estado de participación y aislamiento; Infrastructure tests para concurrencia optimista e idempotencia bajo envíos simultáneos (EF Sqlite); Application tests para ranking/identidad; Architecture tests para reglas de dependencia

**Target Platform**: Linux containers (Podman), .NET Aspire AppHost, ASP.NET Core minimal APIs (IEndpoint) + SignalR hub

**Project Type**: web-service — modular monolith (Domain / Application / Infrastructure / Api)

**Performance Goals**: SC-005 — envíos simultáneos de todos los jugadores (ventana de 1s) sin degradación >2× vs jugador único; SC-007 — consultas de estado propio y leaderboard <1s p95; SC-001 — 100% de envíos simultáneos válidos evaluados

**Constraints**: Sin nuevos agregados; `Game` es el límite de consistencia (jugadores, rondas, respuestas y ledger dentro del agregado); las mutaciones de estado de jugador solo ocurren vía comportamiento del agregado; las notificaciones SignalR son hints best-effort (nunca fuente de verdad); el detalle privado de respuestas de otros jugadores no se expone; identidad delegada en OroIdentityServer

**Scale/Scope**: 2–10 jugadores por juego (`MinPlayers`/`MaxPlayers` SPEC-001); hasta 50 rondas por juego; leaderboard por juego (sin agregación cross-game)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Principle | Status | Evidence / Mitigación |
|------|-----------|--------|------------------------|
| I. Domain First | Estado multiplayer en Domain | ✅ PASS | `GamePlayer.CurrentRoundNumber` y su avance/congelación viven en `Game.StartRound()`/`WithdrawPlayer()`/`EliminatePlayer()` (comportamiento de agregado); `Game.GetPlayerAnswerState()` deriva el estado de respuesta; aislamiento y reglas de participación como dominio, no en controllers. |
| II. Clean Architecture | `Web→Application→Domain←Infrastructure` | ✅ PASS | Domain extiende `GamePlayer`/`Game` sin referencias externas; Application añade/extiende slices y el port `IGameNotificationsBroadcaster`; Api implementa el hub SignalR (infraestructura de presentación) e inyecta la implementación del port. |
| III. BuildingBlocks No Reinvention | Reuso de abstracciones plataforma | ✅ PASS | Reuso `Entity<GamePlayerId>`, `Enumeration<PlayerParticipationStatus/AnswerStatus>`, `Result/Error`, `IBusinessRule`, `IRepository`/`Specification<T>`, `IDomainEventHandler` (auto-registrados por `AddCqrs`), `IEndpoint`; sin MediatR/MassTransit/AutoMapper; SignalR es capacidad nativa de ASP.NET Core (no librería externa). |
| IV. Vertical Slice + CQRS | Slices autocontenidos | ✅ PASS | `GetPlayerState` como nuevo slice `IQuery` completo (Query+Handler+Response+Endpoint); extensiones de `SubmitAnswer`/`WithdrawPlayer`/`GetLeaderboard` dentro de sus slices existentes bajo `Features/Games/`. |
| V. Authoritative Domain Engine | Server truth + aislamiento multiplayer | ✅ PASS | Mandato explícito del Principio V: "Multiplayer player state MUST be isolated". Identidad del jugador desde JWT `sub` (nunca cuerpo del request para jugadores); correct/puntos/elapsed siempre server-side (`Game.SubmitAnswer`); SignalR solo notifica (`ScoreUpdated`, `LeaderboardUpdated`, `PlayerStatusChanged`), nunca es fuente de verdad. |
| VI. OroIdentityServer (Podman) | JWT única autoridad de identidad | ✅ PASS | `PlayerId` = claim `sub` del token emitido por OroIdentityServer; endpoints con `RequireAuthorization()`; sin store local de usuarios. |
| A. Game Lifecycle State Machine | Transiciones protegidas | ✅ PASS | Participación gobierna por estados existentes (`JoinPlayer` solo en `WAITING_FOR_PLAYERS`, respuestas solo en `ROUND_IN_PROGRESS`); sin nuevas transiciones de estado de juego. |
| D. Scoring via Ledger | Puntaje vía ledger | ✅ PASS | `Score`/`Points` del leaderboard se derivan de `PointTransaction` (append-only); ninguna mutación directa de saldo; `CorrectAnswers` se deriva de entidades `Answer` evaluadas. |
| E. Persistence | Integridad y constraints | ✅ PASS | Índice único `(GameId, UserId)` (una participación por usuario/juego) y `(GameId, PlayerId, RoundId)` (idempotencia de respuesta) ya existen; nuevo campo `CurrentRoundNumber` se mapea en `GamePlayerTypeConfiguration`; transacciones multi-entidad en `SaveChanges`. |
| F. Concurrency & Idempotency | Optimistic concurrency + idempotencia | ✅ PASS | `Game.RowVersion` protege toda mutación del agregado (incluido estado de jugador — el agregado es el límite de consistencia); `DbUpdateConcurrencyException` → `ConcurrencyConflict` (409) en todos los handlers de mutación; idempotencia de respuesta por `(GameId, PlayerId, RoundId)` vía regla de dominio + índice único DB. |
| G. Real-Time/Outbox | Notificaciones sin ser fuente de verdad | ✅ PASS | Domain events in-process (`ScoreUpdatedDomainEvent`, `PlayerJoinedDomainEvent`, `PlayerWithdrawnDomainEvent`, `PlayerEliminatedDomainEvent`, `RoundCompletedDomainEvent`, `GameFinishedDomainEvent`) ya existen; handlers de aplicación publican al hub SignalR como hint; sin eventos de integración nuevos ni RabbitMQ para estado de juego. |
| H. Security Delegated | JWT + autorización | ✅ PASS | Aislamiento FR-003: comandos de jugador validan `sub == playerId` (nuevo error `PlayerIdentityMismatch` → 403); organizadores (`AdminOrGameManager`) pueden actuar en nombre de otros; detalle privado de respuestas solo para su dueño u organizadores. |
| I. Validation/Errors/Observability | 3 niveles + audit | ✅ PASS | Validación en endpoint (JWT), handler (`ValidationBehavior`) y dominio (`ValidatePlayerRule` etc.); errores explícitos (`PlayerIdentityMismatch`, `ConcurrencyConflict`, `PlayerNotInGame`); logging estructurado con `GameId`/`PlayerId`/`RoundId` vía `LoggingBehavior` existente. |

**Gate Result: PASS — no violations. No complexity-tracking entries required.**

## Project Structure

### Documentation (this feature)

```text
specs/011-multiplayer/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── multiplayer.openapi.yaml
│   └── gamehub.md
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── BuildingBlocks/                                    # EXISTING — platform (no modificar)
├── OroQuizClash.Domain/                               # EXTEND — estado de participación multiplayer
│   ├── Games/
│   │   ├── GamePlayer.cs                              # EXTEND — + CurrentRoundNumber, AdvanceToRound(), congelación en MarkWithdrawn/MarkEliminated
│   │   └── Game.cs                                    # EXTEND — StartRound() avanza CurrentRoundNumber de activos; + GetPlayerAnswerState(playerId)
│   └── Shared/Errors/
│       └── GameErrors.cs                              # EXTEND — + PlayerIdentityMismatch (Forbidden)
├── OroQuizClash.Application/                          # EXTEND — identidad, concurrencia, leaderboard, estado, notificaciones
│   └── Features/Games/
│       ├── SubmitAnswer.cs                            # EXTEND — playerId desde JWT sub (eliminar placeholder Guid.Empty), catch DbUpdateConcurrencyException
│       ├── WithdrawPlayer.cs                          # EXTEND — validación de identidad (sub == PlayerId salvo organizador), catch concurrencia
│       ├── AdjustScore.cs                             # EXTEND — catch DbUpdateConcurrencyException
│       ├── GetLeaderboard.cs                          # EXTEND — + CorrectAnswers, CurrentLevel, Status; desempate determinista
│       ├── GetPlayerState.cs                          # NEW — slice IQuery: Status/Score/CurrentRound/AnswerState del jugador
│       ├── IGameNotificationsBroadcaster.cs           # NEW — port de notificaciones (Application, sin dependencia de framework)
│       └── Notifications/
│           └── GameEventBroadcastHandlers.cs          # NEW — IDomainEventHandler<> → broadcaster (PlayerJoined, ScoreUpdated, AnswerEvaluated/RoundCompleted → leaderboard, Withdrawn/Eliminated/Finished → status)
├── OroQuizClash.Infrastructure/                       # EXTEND — mapeo del nuevo campo
│   └── Persistence/Configurations/
│       └── GamePlayerTypeConfiguration.cs             # EXTEND — mapear CurrentRoundNumber
└── OroQuizClash.Api/                                  # EXTEND — SignalR hub
    ├── Program.cs                                     # EXTEND — AddSignalR(), MapHub<GameHub>("/hubs/game")
    └── Hubs/
        ├── GameHub.cs                                 # NEW — hub broadcast-only, RequireAuthorization, grupos por juego
        └── SignalRGameNotificationsBroadcaster.cs     # NEW — implementa IGameNotificationsBroadcaster vía IHubContext<GameHub>

tests/
├── OroQuizClash.Domain.Tests/
│   └── Games/
│       └── MultiplayerParticipationTests.cs           # NEW — CurrentRound avance/congelación, AnswerState derivado, aislamiento de estado
├── OroQuizClash.Infrastructure.Tests/
│   └── Persistence/
│       └── GameConcurrencyTests.cs                    # EXTEND — reemplazar stub: envíos simultáneos de 2+ jugadores, duplicado idempotente, rowversion stale → conflicto
├── OroQuizClash.Application.Tests/
│   └── Features/Games/
│       ├── LeaderboardRankingTests.cs                 # NEW — orden determinista y desempates
│       ├── SubmitAnswerIdentityTests.cs               # NEW — identidad JWT, PlayerIdentityMismatch
│       └── GetPlayerStateHandlerTests.cs              # NEW
├── OroQuizClash.Api.Tests/
│   └── Contracts/
│       └── MultiplayerContractTests.cs                # NEW — rutas/shape de respuestas leaderboard y player state
└── OroQuizClash.Architecture.Tests/
    └── MultiplayerDependenciesTests.cs                # NEW — reglas de dependencia del slice multiplayer
```

**Structure Decision**: Extender el agregado `Game` existente sin crear nuevos agregados ni contextos: `GamePlayer` gana `CurrentRoundNumber` (atributo exigido por FR-001/FR-010) y `AnswerState` se expone como derivación de las entidades `Answer` vía `Game.GetPlayerAnswerState()` (decisión R2 en research.md — fuente única de verdad, sin invariante de sincronización). La concurrencia se protege con el `RowVersion` del agregado (decisión R3 — el agregado es el límite de consistencia; un token por jugador sería redundante). El aislamiento (FR-003) se aplica en Application cableando el claim `sub` en los endpoints de jugador. El leaderboard se extiende en su slice existente. Las notificaciones (FR-014) usan SignalR con un port en Application e implementación en Api, preservando la dirección de dependencias. Infrastructure solo mapea el nuevo campo; Api añade el hub.

## Complexity Tracking

> **No violations — table intentionally left empty.**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
