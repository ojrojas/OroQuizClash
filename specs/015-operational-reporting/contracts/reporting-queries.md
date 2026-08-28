# Contract: Reporting Queries (CQRS) — SPEC-015

**Date**: 2026-08-28 | **Plan**: [../plan.md](../plan.md) | **Spec**: [../spec.md](../spec.md)

Todas las consultas son `IQuery<T>` con `IQueryHandler<TQuery,TResult>` y usan `Specification<T>` cuando filtran. Sin `SaveChanges` ni `DomainEvent`.

## Queries

### GetGameReportQuery

```csharp
public sealed record GetGameReportQuery(Guid GameId) : IQuery<Result<GameReportResponse>>;
```

**Handler**: `GetGameReportHandler(IRepository<Game, GameId>)` → `Specification` `GameByIdWithRoundsSpecification` + `LeaderboardBuilder.Build(game)` para `Winner` + `PointTransaction` no necesario (solo conteo de rondas). `ApplyAsNoTracking()`.

**Specification**: `GameByIdSpecification` existente (Where `Id==GameId`, Include `Players`/`Rounds`).

### GetPlayerReportQuery

```csharp
public sealed record GetPlayerReportQuery(
    Guid PlayerId,
    Guid? GameId = null,
    Guid? CategoryId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null) : IQuery<Result<PlayerReportResponse>>;
```

**Handler**: `GetPlayerReportHandler(IRepository<Game, GameId>, IRepository<Answer, AnswerId>, IRepository<PointTransaction, ...>, IRepository<RewardRedemption, ...>)` → filtra `Game` por `GameId`/`CategoryId`/`Period` (`CreatedAt`/`FinishedAt` y `PointTransaction.CreatedAt`), luego agrega `Answer`/`PointTransaction`/`RewardRedemption` por `PlayerId` y periodo. Usa `Specification` compuestas:
- `PlayerGamesSpecification(PlayerId, GameId, CategoryId, From, To)`
- `AnswersByPlayerSpecification(PlayerId, GameId, CategoryId, From, To)` (Where `PlayerId` && `Status==EVALUATED` && `CreatedAt` en periodo)
- `PointTransactionsByPlayerSpecification(PlayerId, Period)` (Where `Type` en correctos)
- `RewardRedemptionsByPlayerSpecification(PlayerId, Period)`

`Accuracy` y promedios calculados en handler (no en BD).

### GetQuestionReportQuery

```csharp
public sealed record GetQuestionReportQuery(
    Guid QuestionId,
    Guid? GameId = null,
    Guid? CategoryId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null) : IQuery<Result<QuestionReportResponse>>;
```

**Handler**: `GetQuestionReportHandler(IRepository<Question, QuestionId>, IRepository<GameRound, ...>, IRepository<Answer, ...>)` → `TimesPresented` = `COUNT GameRound where QuestionId` con `Specification` `RoundsByQuestionSpecification`, `CorrectAnswers`/`IncorrectAnswers` = `COUNT Answer where QuestionId && Correct && Evaluated` con `AnswersByQuestionSpecification` + periodo, `AverageResponseTime` = `AVG ElapsedTime`.

**Specifications**:
- `RoundsByQuestionSpecification(QuestionId, GameId, CategoryId, From, To)`
- `AnswersByQuestionSpecification(QuestionId, GameId, CategoryId, From, To)` (filtra por `QuestionId` + `Status==EVALUATED`)

### GetCategoryReportQuery

```csharp
public sealed record GetCategoryReportQuery(
    Guid CategoryId,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null) : IQuery<Result<CategoryReportResponse>>;
```

**Handler**: `GetCategoryReportHandler(IRepository<Category, CategoryId>, IRepository<Question, QuestionId>, IRepository<Game, GameId>)` → `Questions` = `COUNT Question where CategoryId`, `Games` = `COUNT Game where CategoryId && CreatedAt/FinishedAt en periodo` via `GamesByCategorySpecification`, `Players` = `COUNT DISTINCT GamePlayer.UserId` en esos juegos, `AverageScore`/`AverageAccuracy` = `AVG` de `PlayerReport` agregados.

### GetRewardReportQuery

```csharp
public sealed record GetRewardReportQuery(
    Guid? RewardId = null,
    Guid? CategoryId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1, int PageSize = 20) : IQuery<Result<RewardReportResponse>>;
```

**Handler**: `GetRewardReportHandler(IRepository<Reward, RewardId>, IRepository<RewardRedemption, RewardRedemptionId>)` → `AvailableStock` = `Reward.Stock - Redemptions`, etc., con `RewardRedemptionsByPeriodSpecification(From, To)` y `RewardByCategorySpecification`.

### GetLeaderboardExtendedQuery

```csharp
public sealed record GetLeaderboardExtendedQuery(
    Guid? GameId = null,
    Guid? CategoryId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null) : IQuery<Result<LeaderboardResponse>>;
```

**Handler**: Extiende `GetLeaderboardHandler` existente (SPEC-011) → reutiliza `LeaderboardBuilder.Build(game)` filtrando `PointTransaction` por periodo/categoría antes de ranking. No duplica desempate.

## Specifications (reutilizadas/extendidas)

- `GameByIdSpecification` (existente, + `ApplyAsNoTracking`)
- `AnswersByPlayerSpecification` (existente, extendida con `CategoryId`/`Period`)
- `RoundsByQuestionSpecification` (nueva, Where `QuestionId` + `GameId`/`CategoryId`/`Period`)
- `GamesByCategorySpecification` (nueva, Where `CategoryId` && `CreatedAt` en periodo)
- `RewardRedemptionsByPeriodSpecification` (existente, extendida)

Todas validan `from` ≤ `to` en `Validator` (`IValidator<TQuery>`).

## Validación (SC-006)

Cada `IQueryHandler` es verificable por inspección: `IQuery` sin `SaveChanges`, usa `Specification` cuando hay filtro, `ApplyAsNoTracking()` para lectura.

