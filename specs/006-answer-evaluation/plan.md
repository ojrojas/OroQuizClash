# Implementation Plan: Answer Evaluation

**Branch**: `006-answer-evaluation` | **Date**: 2026-08-27 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/006-answer-evaluation/spec.md`

## Summary

Extender el agregado `Game` para recibir, validar y evaluar respuestas de jugadores con cadena de validación server-side de 7 pasos (`ValidatePlayer→ValidateGame→ValidateRound→ValidateQuestion→ValidateTime→ValidateIdempotency→EvaluateAnswer→CalculateResult`), modelando `Answer` como `Entity<AnswerId>` composición dentro de `Game` con lifecycle `NOT_ANSWERED→ANSWERED→EVALUATED/EXPIRED`, y `PointTransaction` como ledger append-only para scoring. El servidor determina autoritativamente `correct/elapsedTime/points/eligibility` usando `ServerTimestamp - Round.StartedAt`, implementando idempotencia por `PlayerId+RoundId` con `rowversion` para concurrencia, y audit con `CorrelationId/GameId/RoundId/PlayerId/AnswerOptionId`. Implementación como Vertical Slices `BuildingBlocks.CQRS` + `AppDbContextBase` + `EfRepository<Game>` + `Specification<Game>` + `rowversion` + `IOutboxWriter`, autenticado vía OroIdentityServer (`PLAYER` para SubmitAnswer).

## Technical Context

**Language/Version**: C# 12 / .NET 10.0 (`net10.0`, `LangVersion latest`, `Nullable enable`, `ImplicitUsings enable`)

**Primary Dependencies**: `BuildingBlocks.Kernel.Domain` (AggregateRoot, Entity, ValueObject, StronglyTypedId, Enumeration, IBusinessRule, Result/Error, IRepository, Specification, IDomainEvent), `BuildingBlocks.CQRS` (ICommand/IQuery, ICommandHandler/IQueryHandler, ISender, IPipelineBehavior ValidationBehavior, IValidator), `BuildingBlocks.Kernel.Infrastructure` (AppDbContextBase, EfRepository, SpecificationEvaluator, IUnitOfWork, IOutboxWriter, OutboxEntityTypeConfiguration), `BuildingBlocks.ServiceDefaults` (OpenTelemetry, health, IEndpoint, Result→HTTP, GlobalExceptionHandler, ProblemDetails), `OroIdentityServer` Podman (JWT Authority)

**Storage**: SQL Server (primario, `rowversion` + indexes `GameId/PlayerId/RoundId`); EF Core 10 sobre `OroQuizClashDbContext : AppDbContextBase`; `EfRepository<Game,GameId>` existente; `Specification<Game>` con `Include(Rounds/Players/Answers)`; Oracle como target secundario vía abstracción

**Testing**: xUnit v3 + NSubstitute + Testcontainers.MsSql + coverlet; Domain unit tests para validación 7 pasos + idempotencia + inmutabilidad + cálculo points; Application tests para Handler con IRepository mock; Integration tests para concurrencia rowversion + PointTransaction ledger; Architecture tests para dependency rules

**Target Platform**: Linux containers (Podman), .NET Aspire AppHost, ASP.NET Core minimal APIs (IEndpoint)

**Project Type**: web-service — modular monolith (Domain / Application / Infrastructure / Api)

**Performance Goals**: SC-001→SC-009 en <1s p95; idempotencia sin duplicación 100%; inmutabilidad post-EVALUATED 100%

**Constraints**: <200ms p95 validación pipeline; concurrencia optimista obligatoria (`rowversion`); `Answer` inmutable tras `EVALUATED/EXPIRED`; `PointTransaction` append-only; solo `PLAYER` autenticado vía JWT; mapeo explícito (no AutoMapper); sin MediatR/MassTransit

**Scale/Scope**: 10–1k juegos concurrentes, 2–10 jugadores/juego, 5–50 rondas/juego, 1 respuesta/jugador/ronda, 10k `PointTransaction` ledger

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Principle | Status | Evidence / Mitigación |
|------|-----------|--------|------------------------|
| I. Domain First | Answer evaluation, PointTransaction, validation chain en Domain | ✅ PASS | `Game.SubmitAnswer(AnswerOptionId)` con `IBusinessRule` (`ValidatePlayerRule`, `ValidateGameRule`, `ValidateRoundRule`, `ValidateQuestionRule`, `ValidateTimeRule`, `ValidateIdempotencyRule`), `Answer` como `Entity<AnswerId>` composición, `PointTransaction` como `Entity<PointTransactionId>` append-only, `AnswerStatus` Enumeration, `EvaluateAnswer`/`CalculateResult` como domain behavior. Application solo orquesta. |
| II. Clean Architecture | `Web→Application→Domain←Infrastructure`, Domain sin refs Infra/Web | ✅ PASS | Domain solo `BuildingBlocks.Kernel.Domain`; Application referencia Domain+CQRS+`IRepository`; Infrastructure implementa `EfRepository/AppDbContextBase`; Api referencia Application+Infrastructure+ServiceDefaults. |
| III. BuildingBlocks No Reinvention | Reuso Kernel/CQRS/Infrastructure/ServiceDefaults | ✅ PASS | Usa `Entity`, `AggregateRoot`, `StronglyTypedId`, `Enumeration`, `Result`, `IRepository`, `Specification`, `ICommand/ISender`, `AppDbContextBase`, `IEndpoint`; prohíbe MediatR/MassTransit/AutoMapper; `net10.0`. |
| IV. Vertical Slice + CQRS | Slice autocontenido, sin dispatcher secundario | ✅ PASS | `Features/Games/SubmitAnswer.cs` (Command+Validator+Handler+Response+Endpoint), `Features/Games/GetAnswer.cs` (Query+Handler+Response+Endpoint); cada uno autocontenido con mapping local. |
| V. Authoritative Domain Engine | Server truth para correct/elapsedTime/points/eligibility | ✅ PASS | `SubmitAnswer` calcula `ServerTimestamp - Round.StartedAt` server-side, compara `AnswerOptionId` contra `Question.AnswerOptions.IsCorrect` server-side, crea `PointTransaction` server-side, cliente NUNCA determina estos valores. |
| VI. OroIdentityServer (Podman) | `oroidentityserver:latest` única autoridad identidad | ✅ PASS | `SubmitAnswer` requiere `PLAYER` JWT bearer; sin user store local; `GamePlayer.PlayerId` es `sub` externo. |
| A. Game Lifecycle State Machine | Estados Answer con transiciones protegidas | ✅ PASS | `AnswerStatus` Enumeration 4 valores con transiciones `NOT_ANSWERED→ANSWERED→EVALUATED` / `NOT_ANSWERED→EXPIRED` protegidas por domain behavior; inmutabilidad post-EVALUATED. |
| B. Question & Category Invariants | ValidateQuestion contra Question del round | ✅ PASS | `ValidateQuestion` verifica `AnswerOptionId` pertenece a `Question.AnswerOptions` del `GameRound.QuestionId`; snapshot del round garantiza consistencia. |
| C. Configurable Game Rules | PointsPerRound y DifficultyMultiplier desde GameConfiguration | ✅ PASS | `CalculateResult` usa `Game.Configuration.PointsPerRound × DifficultyMultiplier` calculado desde `GameConfiguration.ScoringSystem` (SPEC-001). |
| D. Scoring via Ledger | PointTransaction append-only, no mutación directa | ✅ PASS | `PointTransaction` creado solo vía `CalculateResult` cuando `Answer.Status==EVALUATED`; append-only (no update/delete); balance reconstruible desde historial. |
| E/F. Persistence & Concurrency | SQL Server, rowversion, Specification, Outbox | ✅ PASS | `Answer` y `PointTransaction` como composición en `Game`; `RowVersion` en `Game` protege concurrencia; `UNIQUE (GameId,PlayerId,RoundId)` previene duplicados; Outbox misma transacción. |
| G. Real-Time/Outbox | Domain events in-process, Integration events via Outbox | ✅ PASS | `AnswerSubmittedDomainEvent` / `AnswerEvaluatedDomainEvent` dispatch en `SaveChanges`; opcional `AnswerEvaluatedIntegrationEvent` vía `IOutboxWriter`→RabbitMQ. |
| H. Security Delegated | JWT jwks_uri, PLAYER role | ✅ PASS | `SubmitAnswer` `POST /api/games/{id}/answers` requiere `PLAYER` JWT; validación `sub` contra `GamePlayer.PlayerId`. |
| I. Validation/Errors/Observability | 3 niveles, ProblemDetails, OTel, audit | ✅ PASS | `Validator<SubmitAnswerCommand>` (API) + `IBusinessRule` (Domain: 7 rules) + `Error→ProblemDetails` (`400` validación, `404` not found, `409` conflicto idempotencia, `408` timeout); OTel `CorrelationId/GameId/RoundId/PlayerId/AnswerOptionId`; audit append-only. |

**Gate Result: PASS — no violations. No complexity-tracking entries required.**

## Project Structure

### Documentation (this feature)

```text
specs/006-answer-evaluation/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── answer-evaluation.openapi.yaml
│   └── answer-query.openapi.yaml
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
│   └── BuildingBlocks.ServiceDefaults/
├── OroQuizClash.Domain/                     # EXTEND — Answer + PointTransaction + rules
│   ├── Games/
│   │   ├── Game.cs                         # AggregateRoot<GameId> — extend SubmitAnswer + GetScore
│   │   ├── GameId.cs                       # StronglyTypedId<Guid> (exists)
│   │   ├── GameRound.cs                    # Entity<GameRoundId> — StartedAt usado por ValidateTime
│   │   ├── GamePlayer.cs                   # Entity<GamePlayerId> — Status IN_PROGRESS/WITHDRAWN
│   │   ├── Answer.cs                       # Entity<AnswerId> — lifecycle NOT_ANSWERED→EVALUATED/EXPIRED
│   │   ├── AnswerId.cs                     # StronglyTypedId<Guid>
│   │   ├── PointTransaction.cs             # Entity<PointTransactionId> — ledger append-only
│   │   ├── PointTransactionId.cs           # StronglyTypedId<Guid>
│   │   ├── Enumerations/
│   │   │   ├── AnswerStatus.cs             # NOT_ANSWERED(1), ANSWERED(2), EVALUATED(3), EXPIRED(4)
│   │   │   └── PointTransactionType.cs     # ANSWER_CORRECT(1), ANSWER_INCORRECT(2), ROUND_BONUS(3), LEVEL_BONUS(4)
│   │   ├── Rules/
│   │   │   ├── ValidatePlayerRule.cs       # GamePlayer.Status == IN_PROGRESS
│   │   │   ├── ValidateGameRule.cs         # Game.Status IN (IN_PROGRESS, ROUND_IN_PROGRESS)
│   │   │   ├── ValidateRoundRule.cs        # GameRound.Status == ROUND_IN_PROGRESS
│   │   │   ├── ValidateQuestionRule.cs     # AnswerOptionId ∈ Question.AnswerOptions
│   │   │   ├── ValidateTimeRule.cs         # ServerTimestamp - StartedAt ≤ TimeLimit
│   │   │   ├── ValidateIdempotencyRule.cs  # PlayerId+RoundId unique (no duplicar)
│   │   │   └── AnswerImmutabilityRule.cs   # No mutación post-EVALUATED/EXPIRED
│   │   └── Events/
│   │       ├── AnswerSubmittedDomainEvent.cs
│   │       └── AnswerEvaluatedDomainEvent.cs
│   ├── Categories/                          # (002) existente
│   └── Questions/                          # (003) existente — Question.AnswerOptions.IsCorrect
├── OroQuizClash.Application/                # EXTEND — Vertical Slices Answers
│   └── Features/
│       └── Games/
│           ├── SubmitAnswer.cs             # REWRITE — Command+Validator+Handler+Response+Endpoint (7 pasos)
│           ├── GetAnswer.cs                # NEW — Query por AnswerId → Response con resultado
│           └── GetGame.cs                  # (004) existente — extender si incluye Answers
├── OroQuizClash.Infrastructure/             # EXTEND — Persistence
│   ├── Persistence/
│   │   ├── OroQuizClashDbContext.cs        # AppDbContextBase + DbSet<Game> (Answers/PointTransactions via HasMany)
│   │   └── Configurations/
│   │       ├── GameTypeConfiguration.cs    # EXTEND — HasMany Answers + PointTransactions composition
│   │       ├── AnswerTypeConfiguration.cs  # NEW — Answer EF config (HasConversion, indexes)
│   │       └── PointTransactionTypeConfiguration.cs # NEW — PointTransaction EF config (append-only, indexes)
│   └── Specifications/
│       ├── GameByIdWithAnswersSpecification.cs  # NEW — Include(Rounds, Players, Answers, PointTransactions)
│       └── AnswerByIdSpecification.cs           # NEW — Where(GameId, AnswerId)
├── OroQuizClash.Api/                        # EXISTING — Host (endpoints answers)
│   └── Program.cs                          # wiring Answer services
└── OroQuizClash.AppHost/                    # EXISTING — Aspire (no cambios)
```

**Structure Decision**: Extender el modular monolith existente de `001+002+003+004+005`. `Answer` y `PointTransaction` son composición dentro del agregado `Game` (mismo `OroQuizClashDbContext`), con `RowVersion` en `Game` protegiendo concurrencia. `SubmitAnswer.cs` se reescribe completamente (el actual es placeholder/demo). `AnswerStatus` y `PointTransactionType` son Enumerations separadas. Idempotencia por `UNIQUE (GameId,PlayerId,RoundId)` en Answer + `rowversion` en Game para concurrencia optimista. BuildingBlocks permanece como dependencia externa; OroIdentityServer consumido como container Podman.

## Complexity Tracking

> **No violations — table intentionally left empty.**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
