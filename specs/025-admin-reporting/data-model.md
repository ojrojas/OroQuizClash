# Data Model: Admin Reporting

**Branch**: `025-admin-reporting` | **Date**: 2026-05-13 | **Plan**: [plan.md](plan.md)

Modelo de datos **frontend** (proyecciones/DTOs) para reporting analítico. Sin persistencia propia: estructuras en `QuizArena.Admin.Client/Models/Reports/` que reflejan contratos `oroclash-api /api/reports*` (agregaciones sobre `Game`/`Player`/`Question`/`PointTransaction`/`Reward`). Autoridad permanece en backend (Constitución V/D).

## 1. Entidades principales

### ReportFilter (6 dimensiones)

```csharp
record ReportFilter(
    DateTimeOffset? From = null,       // Fecha Desde
    DateTimeOffset? To = null,         // Fecha Hasta, From <= To si ambos
    Guid? CategoryId = null,           // Categoría
    string? CategoryName = null,       // alternativa nombre
    Guid? GameId = null,               // Juego
    string? GameName = null,           // búsqueda parcial
    Guid? PlayerId = null,             // Jugador (sub)
    string? PlayerSearch = null,       // nombre/email parcial
    int? Level = null,                 // 1–5
    string? Result = null,             // catálogo cerrado por métrica
    int Page = 1,
    int PageSize = 20);
```

**Invariantes**:
- `From <= To` si ambos.
- `Level ∈ [1,5]` si se especifica.
- `Result ∈ catálogo cerrado` si se especifica (ver `ReportCatalogs`).
- `Page >=1`, `PageSize 1–100`.

### ReportSnapshot (agregado)

```csharp
record ReportSnapshot(
    ReportFilter Filters,
    OperationalMetrics Operational,
    PerformanceMetrics Performance,
    RewardsMetrics Rewards,
    int TotalCount,                    // para listas paginadas
    DateTimeOffset CalculatedAt);
```

No persistido; `CalculatedAt` indica freshness del snapshot.

### OperationalMetrics (US1)

```csharp
record OperationalMetrics(
    GameMetric Games,
    PlayerMetric Players,
    QuestionMetric Questions,
    CategoryMetric Categories);

record GameMetric(
    int TotalGames,
    IReadOnlyDictionary<string,int> ByStatus); // 9 estados

record PlayerMetric(
    int UniquePlayers,
    int ActivePlayers,
    IReadOnlyDictionary<string,int> DistributionByTenant);

record QuestionMetric(
    int TotalQuestions,
    IReadOnlyDictionary<string,int> ByCategory,
    IReadOnlyDictionary<int,int> ByLevel);

record CategoryMetric(
    int TotalCategories,
    int CategoriesInUse,
    IReadOnlyDictionary<string,int> QuestionsPerCategory);
```

### PerformanceMetrics (US2)

```csharp
record PerformanceMetrics(
    AnswerMetric Answers,
    ScoreMetric Scores,
    WithdrawalMetric Withdrawals);

record AnswerMetric(
    int TotalAnswers,
    int CorrectAnswers,
    int IncorrectAnswers,
    double AccuracyRate); // correct/total, 0..1

record ScoreMetric(
    int TotalPoints,                   // SUM ledger
    double AverageScore,
    IReadOnlyDictionary<string,int> Distribution, // histograma
    IReadOnlyDictionary<string,int> ByTransactionType); // 10 tipos

record WithdrawalMetric(
    int TotalWithdrawals,
    IReadOnlyDictionary<string,int> ByPolicy, // LOSE_ALL etc.
    double Rate); // withdrawals/games
```

**Invariantes**:
- `AccuracyRate = Correct/Total` (0 si `Total==0`).
- `Scores` reconstruidos desde `PointTransaction` (D).

### RewardsMetrics (US3)

```csharp
record RewardsMetrics(
    RewardMetric Rewards,
    RedemptionMetric Redemptions,
    ConsolationMetric Consolations);

record RewardMetric(
    int TotalRewards,
    IReadOnlyDictionary<string,int> ByType,   // 6 tipos
    IReadOnlyDictionary<string,int> ByStatus); // 3 estados

record RedemptionMetric(
    int TotalRedemptions,
    IReadOnlyDictionary<string,int> ByStatus, // 5 estados
    IReadOnlyDictionary<string,int> ByType,   // 6 tipos
    int TotalCost); // SUM Cost

record ConsolationMetric(
    int TotalConsolations,
    int TotalCostConsolation,
    IReadOnlyDictionary<string,int> ByEligibility);
```

**Invariantes**:
- `Consolations` con `IsConsolation:true` separado (C), no sumado en `Rewards`.

### PagedResult (listas desglosadas)

```csharp
record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    int TotalPages => PageSize<=0?0:(int)Math.Ceiling(TotalCount/(double)PageSize);
}
```

Para listas desglosadas: `PagedResult<GameMetricRow>`, `PagedResult<AnswerRow>` etc., donde `GameMetricRow` es entrada individual para tabla.

## 2. DTOs de transporte (BFF boundary)

```csharp
record OperationalReportResponse(OperationalMetrics Metrics, int TotalCount, DateTimeOffset CalculatedAt);
record PerformanceReportResponse(PerformanceMetrics Metrics, int TotalCount, DateTimeOffset CalculatedAt);
record RewardsReportResponse(RewardsMetrics Metrics, int TotalCount, DateTimeOffset CalculatedAt);
record FullReportResponse(OperationalMetrics Operational, PerformanceMetrics Performance, RewardsMetrics Rewards, DateTimeOffset CalculatedAt);

// Filtros como query string: ?from=&to=&categoryId=&gameId=&playerId=&level=&result=&page=&pageSize=
```

## 3. Catálogos estáticos

```csharp
static class ReportCatalogs
{
    static IReadOnlyList<string> GameStatuses => ["DRAFT","READY","WAITING_FOR_PLAYERS","IN_PROGRESS","ROUND_IN_PROGRESS","ROUND_COMPLETED","FINISHED","CANCELLED","FORCED_FINISHED"];
    static IReadOnlyList<int> Levels => [1,2,3,4,5];
    static IReadOnlyList<string> Results => ["FINISHED","CANCELLED","WITHDRAWN","Approved","Rejected","Correct","Incorrect"]; // por métrica
    static IReadOnlyList<string> TransactionTypes => ["ANSWER_CORRECT","ANSWER_INCORRECT","ROUND_BONUS","LEVEL_BONUS","GAME_BONUS","PENALTY","WITHDRAWAL","REWARD_REDEMPTION","CONSOLATION","ADJUSTMENT"];
    static IReadOnlyList<string> RewardTypes => ["Monetary","Physical","Digital","Voucher","Experience","Consolation"];
    static IReadOnlyList<string> RedemptionStatuses => ["Requested","Approved","Rejected","Delivered","Cancelled"];
}
```

## 4. Validación por niveles

- **API**: tipos/rangos → `400` + `FieldErrors` por campo; `Page` 1..N, `Level` 1–5, `From<=To`, `Result` en catálogo.
- **Aplicación**: `Validator` — `CategoryId`/`GameId`/`PlayerId` existen si se filtran (`NotFound` → 404 o 200 vacío según política), filtros combinados coherentes.
- **Dominio**: invariantes `InvalidLevel`, `InvalidResult` mapeados a 400; no mutación (solo lectura).

## 5. Relaciones

```text
ReportSnapshot ── agrega 1 ──> OperationalMetrics (Game/Player/Question/Category)
ReportSnapshot ── agrega 1 ──> PerformanceMetrics (Answer/Score/Withdrawal)
ReportSnapshot ── agrega 1 ──> RewardsMetrics (Reward/Redemption/Consolation)
OperationalMetrics ── deriva ──> Game + GamePlayer + Question + Category
PerformanceMetrics ── deriva ──> Answer + PointTransaction + GamePlayer
RewardsMetrics ── deriva ──> Reward + RewardRedemption (IsConsolation)
ReportFilter ── filtra ──> ReportSnapshot (AND 6 dimensiones)
PagedResult<T> ── pagina ──> ReportSnapshot listas desglosadas
```

## 6. Reglas de autorización (proyección)

- `ADMIN` → `GET /bff/reports/operational`, `/performance`, `/rewards` (12 métricas + 6 filtros)
- `GAME_MANAGER` → `/operational`, `/performance` (Games/Players/Questions/Answers/Scores/Withdrawals); `/rewards` → 403
- `REWARD_MANAGER` → `/rewards` (Rewards/Redemptions/Consolation); `/operational`/`/performance` → 403 según política
- `PLAYER` → 403 en todas las rutas `/bff/reports*`
