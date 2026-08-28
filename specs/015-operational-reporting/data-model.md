# Data Model: Operational Reporting (SPEC-015)

**Date**: 2026-08-28 | **Plan**: [plan.md](plan.md) | **Research**: [research.md](research.md)

Sin nuevas tablas ni agregados transaccionales. Todos los reportes son proyecciones de lectura (`IQuery`) sobre entidades existentes. Leyenda: **EXISTING** (ya en SPEC-004/003/007/009/014), **DERIVED** (read model del reporte), **NEW** (value object de cálculo si se extrae).

## Entidades existentes (solo lectura, AsNoTracking)

| Entidad | Campos relevantes para reportes | Uso en reportes |
|---------|--------------------------------|-----------------|
| `Game` (AggregateRoot) | `Id`, `Name`, `Status`, `CategoryId`, `CreatedAt`, `FinishedAt`, `Players`, `Rounds` | `GameReport` (Start/End/Players/Rounds/Winner), `CategoryReport` Games/Players |
| `GamePlayer` | `UserId`, `Status`, `Score.CurrentPoints`, `JoinedAt` | `GameReport` Players, `PlayerReport` GamesPlayed/Won/Lost/Withdrawn, `CategoryReport` Players únicos |
| `GameRound` | `Id`, `GameId`, `RoundNumber`, `QuestionId`, `StartedAt` | `GameReport` Rounds, `QuestionReport` TimesPresented (COUNT donde QuestionId) |
| `Answer` | `Id`, `GameId`, `PlayerId`, `QuestionId`, `Correct`, `Status` (`Evaluated`), `ElapsedTime`, `CreatedAt` | `PlayerReport` QuestionsAnswered/Correct, `QuestionReport` Correct/Incorrect/AvgTime, `CategoryReport` AverageAccuracy |
| `PointTransaction` | `GameId`, `PlayerId`, `Points`, `Type`, `CreatedAt`, `ResultingBalance` | `PlayerReport` PointsEarned, `CategoryReport` AverageScore, `Leaderboard` ranking (FR-011) |
| `Question` | `Id`, `CategoryId`, `Difficulty`, `Text` | `QuestionReport` Category/Difficulty, `CategoryReport` Questions |
| `Category` | `Id`, `Name` | `CategoryReport` Category, `QuestionReport` Category |
| `Reward` | `Id`, `Name`, `Stock` | `RewardReport` Reward, AvailableStock |
| `RewardRedemption` | `Id`, `RewardId`, `PlayerId`, `Points`, `Status` (`REQUESTED`/`DELIVERED`/`PENDING`), `RequestedAt` | `PlayerReport` PointsRedeemed, `RewardReport` Redemptions/PointsConsumed/Pending/Delivered |
| `AuditEntry` | `GameId`, `Timestamp`, `CorrelationId` | Opcional trazabilidad, no para cálculos de puntos |

## Read models derivados (no persistidos, solo `Response DTO`)

### GameReport (DERIVED)

| Campo | Tipo | Fuente |
|-------|------|--------|
| `GameId` | `Guid` | `Game.Id` |
| `Name` | `string` | `Game.Name` |
| `Start` | `DateTimeOffset` | `Game.CreatedAt` |
| `End` | `DateTimeOffset?` | `Game.FinishedAt` |
| `Players` | `IReadOnlyList<PlayerRef>` | `Game.Players` (id + estado) |
| `Rounds` | `IReadOnlyList<RoundRef>` | `Game.Rounds` (RoundNumber, QuestionId) |
| `Winner` | `PlayerRef?` | `LeaderboardBuilder.Build(game)` rank 1 si `FINISHED` |
| `TotalQuestions` | `int` | `Rounds.Count` |
| `TotalRounds` | `int` | `Rounds.Count` |

`PlayerRef`: `{ PlayerId, DisplayName, Status }`; `RoundRef`: `{ RoundId, RoundNumber, QuestionId }`.

### PlayerReport (DERIVED)

| Campo | Tipo | Fuente / Cálculo |
|-------|------|------------------|
| `PlayerId` | `Guid` | `sub` claim |
| `GamesPlayed` | `int` | `COUNT Game where Status==FINISHED && Players.Any(p.UserId==PlayerId)` |
| `GamesWon` | `int` | `COUNT GamesPlayed where Winner.PlayerId==PlayerId` |
| `GamesLost` | `int` | `GamesPlayed - GamesWon - GamesWithdrawn` |
| `GamesWithdrawn` | `int` | `COUNT GamePlayer.Status==WITHDRAWN` en juegos terminados |
| `QuestionsAnswered` | `int` | `COUNT Answer where PlayerId && Status==EVALUATED` |
| `CorrectAnswers` | `int` | `COUNT Answer where Correct==true` |
| `Accuracy` | `double?` | `Correct/Answered*100` o `null` si 0 |
| `PointsEarned` | `int` | `SUM PointTransaction.Points where PlayerId && Type in (ANSWER_CORRECT, ROUND_BONUS, LEVEL_BONUS, GAME_BONUS)` |
| `PointsRedeemed` | `int` | `SUM RewardRedemption.Points where PlayerId` |

Filtros `Game`/`Category`/`Period` restringen el scope de las consultas base (ver R5).

### QuestionReport (DERIVED)

| Campo | Tipo | Fuente |
|-------|------|--------|
| `QuestionId` | `Guid` | `Question.Id` |
| `CategoryId` | `Guid` | `Question.CategoryId` |
| `CategoryName` | `string` | `Category.Name` |
| `Difficulty` | `string` | `Question.Difficulty` |
| `TimesPresented` | `int` | `COUNT GameRound where QuestionId` (FR-010) |
| `CorrectAnswers` | `int` | `COUNT Answer where QuestionId && Correct==true && Evaluated` |
| `IncorrectAnswers` | `int` | `COUNT Answer where QuestionId && Correct==false && Evaluated` |
| `Accuracy` | `double?` | `Correct / TimesPresented*100` o `Correct/(Correct+Incorrect)` (si 0 → null) |
| `AverageResponseTime` | `double?` (s) | `AVG Answer.ElapsedTime` donde `Evaluated` y `ElapsedTime != null` |

`AverageResponseTime` en segundos, `null` si sin evaluadas.

### CategoryReport (DERIVED)

| Campo | Tipo | Fuente |
|-------|------|--------|
| `CategoryId` | `Guid` | `Category.Id` |
| `CategoryName` | `string` | `Category.Name` |
| `Questions` | `int` | `COUNT Question where CategoryId` |
| `Games` | `int` | `COUNT Game where CategoryId` dentro de `Period` |
| `Players` | `int` | `COUNT DISTINCT GamePlayer.UserId` en esos `Games` |
| `AverageScore` | `double?` | `AVG SUM PointTransaction.Points por jugador-juego` |
| `AverageAccuracy` | `double?` | `AVG PlayerReport.Accuracy` de esos juegos |

### RewardReport (DERIVED)

| Campo | Tipo | Fuente |
|-------|------|--------|
| `RewardId` | `Guid` | `Reward.Id` |
| `RewardName` | `string` | `Reward.Name` |
| `AvailableStock` | `int` | `Reward.Stock - COUNT RewardRedemption where RewardId` |
| `Redemptions` | `int` | `COUNT RewardRedemption where RewardId` |
| `PointsConsumed` | `int` | `SUM RewardRedemption.Points` |
| `Pending` | `int` | `COUNT where Status==PENDING/REQUESTED` |
| `Delivered` | `int` | `COUNT where Status==DELIVERED` |

Filtros `Period`/`Category` restringen por `RequestedAt` y por `Reward.CategoryId` si existe.

### Leaderboard (DERIVED, extensión de SPEC-011)

Reutiliza `LeaderboardEntry` existente (`PlayerId`/`Rank`/`Points`/`CorrectAnswers`/`CurrentLevel`/`Status`/`SecuredPoints`) + filtros `CategoryId`/`From`/`To` (R6). No es nueva entidad, es `LeaderboardResponse` extendida.

## Relaciones (solo lectura)

```text
Game (1) ──< (N) GameRound (QuestionId) ──1 Question (Category)
Game (1) ──< (N) GamePlayer (UserId)
Answer (N) ──1 GameRound (QuestionId) ──1 Question
PointTransaction (N) ──1 Game (GameId) ──1 GamePlayer (UserId)
RewardRedemption (N) ──1 Reward
Category (1) ──< (N) Question
Category (1) ──< (N) Game (CategoryId)
```

## Validaciones

- `from`/`to` en `Period` validan `from` ≤ `to` (FR-007).
- `GameReport` por `gameId` inexistente → `NotFound`.
- `Accuracy` y `Average*` retornan `null` si denominador 0 (no división por cero).
- `Winner` es `null` si `Game.Status != FINISHED`.

## Invariantes de solo lectura

1. Ningún `IQueryHandler` llama `AddAsync`/`Update`/`SaveChanges` (FR-008, SC-005).
2. Todos los `IQueryHandler` usan `ApplyAsNoTracking()` y `Specification` cuando filtran (FR-009, SC-006).
3. `Leaderboard` no duplica ranking (R6, FR-006).

