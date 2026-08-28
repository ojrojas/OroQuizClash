# Implementation Plan: Realtime Game Events

**Branch**: `012-realtime-game-events` | **Date**: 2026-08-27 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/012-realtime-game-events/spec.md`

## Summary

Formalizar y completar la distribución en tiempo real ya iniciada en SPEC-011: 9 eventos de juego (`GameStarted`, `PlayerJoined`, `RoundStarted`, `QuestionPresented`, `PlayerAnswered`, `ScoreUpdated`, `LeaderboardUpdated`, `RoundCompleted`, `GameFinished`) se distribuyen server-push vía SignalR a la audiencia autenticada del juego (jugadores activos + organizadores en grupo `game-{gameId}`). La DB permanece como única fuente de verdad (FR-014): los eventos son hints best-effort emitidos post-persistencia por handlers de domain events existentes, nunca bloquean ni revierten la operación que los origina, y toda recuperación se hace vía consultas REST tradicionales. Sin nuevos agregados ni nuevas tablas: se extiende el hub y el port `IGameNotificationsBroadcaster` de SPEC-011 con los eventos faltantes, se añade filtrado anti-trampa en `QuestionPresented`/`PlayerAnswered`, aislamiento por juego y filtrado de audiencia para retirados/eliminados, y garantía post-commit.

## Technical Context

**Language/Version**: C# 12 / .NET 10.0 (`net10.0`, `LangVersion latest`, `Nullable enable`, `ImplicitUsings enable`)

**Primary Dependencies**: `BuildingBlocks.Kernel.Domain` (Entity, AggregateRoot, DomainEvent, IDomainEventHandler, Result/Error), `BuildingBlocks.CQRS` (ICommand/IQuery, ISender, ValidationBehavior), `BuildingBlocks.Kernel.Infrastructure` (AppDbContextBase, EfRepository, IUnitOfWork), `BuildingBlocks.ServiceDefaults` (IEndpoint, Result→HTTP, GlobalExceptionHandler, OTel), ASP.NET Core SignalR (shared framework, sin paquete adicional), `OroIdentityServer` Podman (JWT `sub` claim, `RequireAuthorization`)

**Storage**: SQLite local / SQL Server vía Aspire (mismo `OroQuizClashDbContext`); sin nuevas entidades ni migraciones para este SPEC — los eventos son efímeros (no persistidos). Reuso de `EnsureCreatedAsync`; `Game.Rounds`/`Game.Answers`/`Game.Players` como fuente; `Question` + `AnswerOption` para payloads de pregunta

**Testing**: xUnit v3 + NSubstitute + coverlet; Domain tests para anti-trampa de payloads; Application tests para mapeo de eventos y aislamiento; Infrastructure tests no requeridos (sin persistencia nueva); Api tests para hub auth/grupos/contrato; Architecture tests para reglas hub→port

**Target Platform**: Linux containers (Podman), .NET Aspire AppHost, ASP.NET Core minimal APIs (IEndpoint) + SignalR hub en `/hubs/game`

**Project Type**: web-service — modular monolith (Domain / Application / Infrastructure / Api) — extensión de feature

**Performance Goals**: SC-002 — eventos percibidos <2s en red normal; SC-007 — 20 juegos × 4 jugadores sin fugas ni degradación >2s; SC-001 — 100% de transiciones visibles sin recarga para conectados

**Constraints**: Sin nuevos agregados; hub broadcast-only (no comandos por SignalR); DB = única fuente de verdad (Constitución V); JWT obligatorio; `QuestionPresented` y `PlayerAnswered` nunca revelan correctitud/opción secreta; entrega best-effort (sin reenvío ni historial); un solo nodo (sin backplane); withdrawn/eliminated dejan de recibir contenido de ronda/pregunta

**Scale/Scope**: Catálogo cerrado de 9 eventos; audiencia por juego (2–10 jugadores + organizadores); payloads reutilizan DTOs REST existentes; fuera de alcance: espectadores públicos, eventos de recompensas/consuelo, historial/replay de eventos, comandos vía SignalR

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Principle | Status | Evidence / Mitigación |
|------|-----------|--------|------------------------|
| I. Domain First | Reglas en dominio, sin lógica en controllers/hub | ✅ PASS | Ninguna regla nueva en Domain: los 9 eventos mapean a domain events YA existentes (`GameStartedDomainEvent`, `PlayerJoinedDomainEvent`, `RoundStartedDomainEvent`, `AnswerSubmittedDomainEvent`, `ScoreUpdatedDomainEvent`, `AnswerEvaluatedDomainEvent`, `RoundCompletedDomainEvent`, `GameFinishedDomainEvent`); el hub no contiene reglas — solo agrupa y difunde. |
| II. Clean Architecture | `Web→Application→Domain←Infrastructure` | ✅ PASS | Domain no referencia SignalR ni Application; Application define el port `IGameNotificationsBroadcaster` (y sus nuevos métodos) — Api implementa el adaptador SignalR (`SignalRGameNotificationsBroadcaster`). Domain events no conocen el hub. |
| III. BuildingBlocks No Reinvention | Reuso de plataforma | ✅ PASS | Reuso `IDomainEvent`, `IDomainEventHandler<>` (auto-registrados por `AddCqrs`), `Result/Error`, `IRepository`/`Specification`, `IEndpoint`; sin MediatR/MassTransit/AutoMapper; SignalR es capacidad nativa de ASP.NET Core (mandato Constitución G/V). |
| IV. Vertical Slice + CQRS | Slices autocontenidos | ✅ PASS | Sin nuevo slice CQRS: el trabajo vive en `Features/Games/Notifications/` (handlers de broadcast) — mismo patrón de SPEC-011; comandos/queries de juego no se duplican ni se mueven al hub. |
| V. Authoritative Domain Engine | Server truth + SignalR solo notificación | ✅ PASS | Principio V explícito: "SignalR MAY be used for server-driven notifications but MUST NOT be source of truth". FR-014/FR-017/FR-018 del spec son mandatos directos; broadcast siempre post-persistencia (dentro de `SaveChanges` pero post-`Persisted` o con garantía post-commit — ver R5); clientes re-consultan REST ante duda. |
| VI. OroIdentityServer (Podman) | JWT única autoridad | ✅ PASS | `JoinGameGroup` exige JWT válido y `sub` + pertenencia al juego/rol organizador; sin store local ni reimplementación de auth. |
| A. Game Lifecycle State Machine | Transiciones protegidas | ✅ PASS | `GameStarted`/`RoundStarted`/`RoundCompleted`/`GameFinished` solo se emiten cuando la transición YA fue aceptada por el agregado; estado inválido es rechazado antes de cualquier evento. |
| D. Scoring via Ledger | Puntaje vía ledger | ✅ PASS | `ScoreUpdated`/`LeaderboardUpdated` derivan de `PointTransaction`/`ScoreUpdatedDomainEvent`/`AnswerEvaluatedDomainEvent`; sin cálculo cliente ni mutación directa de saldo. |
| E. Persistence | Sin filtración de concerns DB | ✅ PASS | Sin nuevas tablas/columnas: los eventos no se persisten; el único acceso a datos es lectura del agregado para construir payloads (vía `IRepository`+`Specification`). |
| F. Concurrency & Idempotency | Asunción de concurrencia | ✅ PASS | Broadcast nunca interfiere con la concurrencia optimista: handlers best-effort, excepciones capturadas y logueadas (FR-016) — el 409/`Game.RowVersion`/`GamePlayer.RowVersion` siguen gobernados por Domain/Infrastructure sin cambios. |
| G. Real-Time/Outbox | Notificación sin ser fuente de verdad | ✅ PASS | Flujo `Command→Domain op→Domain events→Transaction (agregados)→Broadcast hint` (RabbitMQ/Outbox no se usa para estado de juego — Constitución G); publicación externa antes de commit prohibida → se adopta emisión post-commit (ver R5). |
| H. Security Delegated | Aislamiento y anti-trampa | ✅ PASS | FR-010/FR-011/FR-013: grupos `game-{gameId}` aislados, auth en `JoinGameGroup`, payloads filtrados (sin opción correcta, sin respuesta elegida ajena). |
| I. Validation/Errors/Observability | 3 niveles + audit | ✅ PASS | Sin nueva validación de negocio: la existente del ciclo de vida/ronda/respuesta ya protege las operaciones; errores de broadcast → `ILogger` estructurado con `GameId`/`Event` (no filtrados como 500 al cliente). |

**Gate Result: PASS — no violations. No complexity-tracking entries required.**

## Project Structure

### Documentation (this feature)

```text
specs/012-realtime-game-events/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── gamehub.md       # Hub SignalR /hubs/game — catálogo completo de 9 eventos
│   └── realtime.payloads.yaml  # Esquemas JSON de payloads de los 9 eventos (referencia, no gen OpenAPI)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── BuildingBlocks/                                    # EXISTING — platform (no modificar)
├── OroQuizClash.Domain/                               # EXISTING — domain events ya cubren los 9 eventos
│   └── Games/Events/
│       ├── GameStartedDomainEvent.cs                  # EXISTING → GameStarted
│       ├── PlayerJoinedDomainEvent.cs                 # EXISTING → PlayerJoined
│       ├── RoundStartedDomainEvent.cs                 # EXISTING → RoundStarted + QuestionPresented (QuestionId)
│       ├── AnswerSubmittedDomainEvent.cs              # EXISTING → PlayerAnswered (sin correctitud)
│       ├── ScoreUpdatedDomainEvent.cs                 # EXISTING → ScoreUpdated
│       ├── AnswerEvaluatedDomainEvent.cs              # EXISTING → LeaderboardUpdated (vía evaluación)
│       ├── RoundCompletedDomainEvent.cs               # EXISTING → RoundCompleted + LeaderboardUpdated
│       └── GameFinishedDomainEvent.cs                 # EXISTING → GameFinished
├── OroQuizClash.Application/                          # EXTEND — broadcasts de tiempo real
│   └── Features/Games/
│       ├── IGameNotificationsBroadcaster.cs           # EXTEND — + GameStarted/ RoundStarted/ QuestionPresented/ PlayerAnswered/ RoundCompleted/ GameFinished
│       └── Notifications/
│           ├── GameEventBroadcastHandlers.cs          # EXTEND — handlers para los 9 eventos (5 existentes + 4 nuevos)
│           └── RealtimePayloads.cs                    # NEW (opcional) — DTOs de payload si no reutilizan Response existentes
├── OroQuizClash.Infrastructure/                       # NO CAMBIOS (sin persistencia nueva)
└── OroQuizClash.Api/                                  # EXTEND — hub + adaptador
    ├── Hubs/
    │   ├── GameHub.cs                                 # EXTEND — doc de los 9 eventos (sin nuevos métodos cliente→servidor)
    │   └── SignalRGameNotificationsBroadcaster.cs     # EXTEND — implementa los nuevos métodos del port
    └── Program.cs                                      # EXISTING — AddSignalR + MapHub + DI ya cableados (sin cambios)
tests/
├── OroQuizClash.Domain.Tests/                         # EXTEND — anti-trampa de payloads (QuestionPresented/PlayerAnswered)
├── OroQuizClash.Application.Tests/                    # EXTEND — mapeo de domain event → broadcast + aislamiento de audiencia
├── OroQuizClash.Api.Tests/                            # EXTEND — contrato hub, auth/grupos, best-effort (broadcast failure ≠ 500)
└── OroQuizClash.Architecture.Tests/                   # EXTEND — hub no referencia Domain directamente, port en Application
```

**Structure Decision**: Modular monolith existente (opción web-service). Sin nuevos proyectos ni nuevas capas: se extienden `OroQuizClash.Application/Features/Games/Notifications` y `OroQuizClash.Api/Hubs` creados en SPEC-011. `OroQuizClash.Domain` y `OroQuizClash.Infrastructure` no requieren cambios de esquema.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |

