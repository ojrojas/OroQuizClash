# Data Model: Admin Players

**Branch**: `024-admin-players` | **Date**: 2026-05-13 | **Plan**: [plan.md](plan.md)

Modelo de datos **frontend** (proyecciones/DTOs) para participantes. Sin persistencia propia: estructuras en `QuizArena.Admin.Client/Models/Players/` que reflejan contratos `oroclash-api /api/players*` (GamePlayer, PointTransaction, Reward). Autoridad permanece en backend (Constitución V/D).

## 1. Entidades principales

### Player (Listado)

```csharp
record PlayerSummary(
    Guid PlayerId,           // sub de OroIdentityServer (Guid o string sub)
    string DisplayName,      // name claim
    string Email,            // email claim
    string? TenantId,        // tenant_id claim
    string? IdentificationType,
    string? IdentificationValue,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastActiveAt,
    string State             // Active | InGame | Withdrawn | Inactive (derivado)
);

record PlayerFilter(
    string? Search = null,   // nombre/email/sub parcial, case-insensitive
    int Page = 1,
    int PageSize = 20);
```

**Invariantes**:
- `PlayerId` único (sub).
- `Search` 0–100 chars, trim; vacío = sin filtro.
- `Page >=1`, `PageSize 1–100`.

### PlayerDetail (Perfil + Estado)

```csharp
record PlayerDetail(
    Guid PlayerId,
    string DisplayName,
    string Email,
    string? TenantId,
    string? IdentificationType,
    string? IdentificationValue,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastActiveAt,
    PlayerStateView State,
    PlayerScoreSummary ScoreSummary,
    int TotalParticipations,
    string RowVersion // para consistencia de lectura si se necesita ETag
);

enum PlayerStateView { Active, InGame, Withdrawn, Inactive }

record PlayerScoreSummary(
    int TotalPoints,         // suma ledger
    int SecuredPoints,
    int AvailablePoints);
```

**Invariantes**:
- `State` derivado: `InGame` si participación en `IN_PROGRESS`, `Withdrawn` si último `WITHDRAWN`, etc.
- `ScoreSummary` reconstruido server-side desde `PointTransaction`.

### GameHistoryEntry (Historial)

```csharp
record GameHistoryEntry(
    Guid GameId,
    string GameName,
    Guid CategoryId,
    string CategoryName,
    string Status,           // DRAFT..FORCED_FINISHED (9 estados)
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    int RoundCount,
    int? PlayerScore,        // puntos obtenidos en ese juego
    int? PlayerRank          // posición en leaderboard si FINISHED
);

record GameHistoryFilter(
    string? Search = null,   // gameName / categoryName
    string? Status = null,   // 9 estados
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 20);
```

**Invariantes**:
- `From <= To` si ambos.
- `Status ∈ {9}` si se especifica.

### PlayerParticipation

```csharp
record PlayerParticipation(
    Guid ParticipationId,
    Guid GameId,
    string GameName,
    DateTimeOffset JoinedAt,
    string State,            // JOINED | WITHDRAWN | FINISHED | KICKED
    string GameStatus,       // estado del juego en ese momento
    string? Role);           // PLAYER (futuro: HOST)

record ParticipationFilter(
    string? State = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 20);
```

### PlayerResult (Resultado por participación)

```csharp
record PlayerResult(
    Guid PlayerId,
    Guid GameId,
    int TotalScore,
    int SecuredScore,
    int Rank,
    int CorrectAnswers,
    int TotalAnswers,
    TimeSpan Duration,
    IReadOnlyList<PointTransactionView> Bonuses,
    IReadOnlyList<PointTransactionView> Penalties);
```

### ScoreLedger (PointTransaction desglose)

```csharp
enum TransactionType
{
    ANSWER_CORRECT, ANSWER_INCORRECT, ROUND_BONUS, LEVEL_BONUS,
    GAME_BONUS, PENALTY, WITHDRAWAL, REWARD_REDEMPTION,
    CONSOLATION, ADJUSTMENT
}

record PointTransactionView(
    Guid TransactionId,
    Guid PlayerId,
    Guid GameId,
    TransactionType Type,
    int Points,
    DateTimeOffset Timestamp,
    Guid? ReferenceId); // GameId / RewardId

record ScoreFilter(
    TransactionType? Type = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 20);
```

**Invariantes**:
- `Points` puede ser negativo para `PENALTY`/`WITHDRAWAL`/`REWARD_REDEMPTION`.
- Balance = Σ `Points` server-side.

### Reward / Redemption (vista del jugador)

```csharp
record PlayerRewardView(
    Guid RewardId,
    string RewardName,
    string RewardType,       // 6 tipos
    int Cost,
    string Status,           // Active / Inactive / Archived
    bool IsEligible);

record PlayerRedemptionView(
    Guid RedemptionId,
    Guid RewardId,
    string RewardName,
    string RewardType,
    int Cost,
    string Status,           // Requested..Cancelled (5)
    DateTimeOffset RequestedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? DeliveredAt,
    string? Reason,
    bool IsConsolation,
    string RowVersion);

record RedemptionFilter(
    string? Status = null,   // 5 estados
    string? RewardType = null, // 6 tipos
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 20);
```

**Invariantes**:
- `IsConsolation==true` ⇒ `RewardType==Consolation` y no cuenta como premio normal.

### PlayerStatistics

```csharp
record PlayerStatistics(
    Guid PlayerId,
    int TotalGames,
    int Wins,                // rank 1
    int Top3,
    double AverageScore,
    double AccuracyRate,     // 0..1
    int BestStreak,
    TimeSpan AverageTimePerQuestion,
    IReadOnlyDictionary<string,int> DistributionByDifficulty,
    IReadOnlyDictionary<string,int> DistributionByCategory,
    DateTimeOffset CalculatedAt);
```

**Invariantes**:
- Calculadas server-side; `CalculatedAt` indica snapshot.
- No paginadas (single snapshot).

## 2. DTOs de transporte (BFF boundary)

```csharp
record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

record GetPlayersRequest(string? Search, int Page, int PageSize);
record GetPlayerRequest(Guid PlayerId);
record GetPlayerHistoryRequest(Guid PlayerId, string? Search, string? Status, DateTimeOffset? From, DateTimeOffset? To, int Page, int PageSize);
record GetPlayerScoresRequest(Guid PlayerId, TransactionType? Type, DateTimeOffset? From, DateTimeOffset? To, int Page, int PageSize);
record GetPlayerRedemptionsRequest(Guid PlayerId, string? Status, string? RewardType, DateTimeOffset? From, DateTimeOffset? To, int Page, int PageSize);
```

Paginación: `PagedResult<PlayerSummary>` y `PagedResult<GameHistoryEntry>` etc.

## 3. Catálogos estáticos

```csharp
static class PlayerCatalogs
{
    static IReadOnlyList<string> PlayerStates => ["Active","InGame","Withdrawn","Inactive"];
    static IReadOnlyList<string> GameStatuses => ["DRAFT","READY","WAITING_FOR_PLAYERS","IN_PROGRESS","ROUND_IN_PROGRESS","ROUND_COMPLETED","FINISHED","CANCELLED","FORCED_FINISHED"];
    static IReadOnlyList<string> ParticipationStates => ["JOINED","WITHDRAWN","FINISHED","KICKED"];
    static IReadOnlyList<string> TransactionTypes => ["ANSWER_CORRECT","ANSWER_INCORRECT","ROUND_BONUS","LEVEL_BONUS","GAME_BONUS","PENALTY","WITHDRAWAL","REWARD_REDEMPTION","CONSOLATION","ADJUSTMENT"];
    static IReadOnlyList<string> RedemptionStatuses => ["Requested","Approved","Rejected","Delivered","Cancelled"];
    static IReadOnlyList<string> RewardTypes => ["Monetary","Physical","Digital","Voucher","Experience","Consolation"];
}
```

## 4. Validación por niveles

- **API**: tipos/rangos → `400` + `FieldErrors` por campo; `Page` 1..N, `From<=To`.
- **Aplicación**: `Validator` — `PlayerId` existe (`PlayerNotFound` → 404 o 200 vacío según política de auditoría), filtros coherentes, paginación.
- **Dominio**: invariantes `PlayerNotFound`, `GamePlayerNotFound` mapeados a 404/empty; no invariantes de escritura (solo lectura).

## 5. Relaciones

```text
Player (sub) ── 1:N ──> GameParticipation (via PlayerId)
Player ── 1:N ──> PointTransaction (via PlayerId, ledger)
Player ── 1:N ──> RewardRedemption (via PlayerId, IsConsolation flag)
GameParticipation ── N:1 ──> Game (via GameId)
GameHistoryEntry ── deriva ──> Game + GamePlayer + Leaderboard
PointTransactionView ── referencia 1 ──> Game (optional) / Reward (optional)
PlayerStatistics ── agrega ──> GameHistoryEntry + PointTransaction
PlayerDetail ── agrega ──> PlayerSummary + ScoreSummary + State
```

## 6. Reglas de autorización (proyección)

- `ADMIN` → `GET /bff/players*` + todas las sub-rutas (`/games`, `/scores`, `/rewards`, `/redemptions`, `/statistics`)
- `GAME_MANAGER` → `GET /bff/players`, `/players/{id}`, `/players/{id}/games`, `/players/{id}/scores`, `/players/{id}/statistics` (premios/canjes → 403 si intenta)
- `REWARD_MANAGER` → `GET /bff/players` (básico), `/players/{id}/rewards`, `/players/{id}/redemptions` (historial/estadísticas → 403)
- `PLAYER` → 403 en todas las rutas `/bff/players*`
