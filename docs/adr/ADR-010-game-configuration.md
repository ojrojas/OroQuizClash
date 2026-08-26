# ADR-010: Game Configuration

**Status**: Accepted
**Date**: 2026-08-26
**Deciders**: Architecture Team

## Context
Necesidad de configurar partida antes de iniciar con 12 campos, validando CFG-001..007, inmutable tras StartGame.

## Decision
- `Game` AggregateRoot<GameId> con `GameConfiguration` ValueObject owned (EF Core OwnsOne), `RowVersion` concurrency.
- Enumerations `GameStatus`, `DifficultyProgressionStrategy`, `LossPolicy`, `WithdrawalPolicy`, `ConsolationPolicy`, `ScoringSystem` via `Enumeration<T>`.
- Validación en dos niveles: `Validator<CreateGameCommand>` (pipeline, rango sintáctico, 5-300s, 3-100 chars) + `IBusinessRule` en `Game.Create` (MinRounds≥5, ranges, policies, category stub).
- Vertical Slice `Features/Games/CreateGame.cs` y `StartGame.cs` con `IEndpoint` thin, `Result` → ProblemDetails.
- Persistencia `OroQuizClashDbContext : AppDbContextBase` + `EfRepository` + `GameByIdSpecification`, Outbox opcional.
- Identidad delegada a `oroidentityserver:latest` Podman (JWT bearer, Authority http://identity:5080, policy AdminOrGameManager), stub `ICategoryValidator` hasta SPEC-002.
- No MediatR/MassTransit/AutoMapper.

## Consequences
- Config inmutable tras `StartGame` (Error ConfigurationImmutable), protege equidad y concurrencia.
- `rowversion` protege transiciones; domain events `GameCreated`/`GameStarted` dispatch en SaveChanges.
- Tests Domain/Application/Infrastructure/Api/Architecture requeridos; quickstart valida <2s p95.

## Alternatives
- `GameConfiguration` como entidad separada: rechazado (sin identidad, parte del agregado).
- MediatR/Sqlite sin rowversion: rechazado (prohibido por constitución, pierde concurrencia).
