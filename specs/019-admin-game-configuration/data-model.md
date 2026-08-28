# Data Model: Admin Game Configuration

**Branch**: `019-admin-game-configuration` | **Date**: 2026-08-28 | **Plan**: [plan.md](plan.md)

Modelo de datos **frontend** (proyecciones/DTOs) para configuración administrativa. Sin persistencia propia: estructuras en `QuizArena.Admin.Client/Models/GameConfiguration/` y `QuizArena.Admin.Client/Models/` que reflejan contratos `oroclash-api /api/games*` (SPEC-001). Autoridad permanece en backend (Constitución V).

## 1. Entidades principales

### GameConfiguration (agregado de configuración)

Inmutable tras `Running` (Constitución C). Vive como ValueObject en dominio `Game`; en Admin es DTO `GameConfigurationForm` + vista `GameDetail`.

```csharp
enum GameStateView
{
    Draft,          // DRAFT — borrador incompleto
    Configured,     // READY sin fecha — configuración válida
    Scheduled,      // WAITING_FOR_PLAYERS + ScheduledAt futura
    Ready,          // READY listo para Start
    Running,        // IN_PROGRESS
    Paused,         // IN_PROGRESS con IsPaused
    Finished,       // FINISHED terminal
    Cancelled       // CANCELLED/ FORCED_FINISHED terminal
}

record GameConfiguration(
    Guid GameId,
    string Name,                    // 3–100
    string? Description,            // 0–500
    Guid CategoryId,                // FK Category Active ≥5 preguntas
    int NumberOfRounds,             // 5–10
    int MaxPlayers,                 // 2–1000
    int TimePerQuestion,            // 5–300s
    int InitialDifficulty,          // 1–5
    DifficultyStrategy DifficultyProgression, // Linear/Progressive/Adaptive/CategorySpecific
    ScoringSystem Scoring,           // Standard/ProgressiveBonus
    int PointsPerRound,             // derivado de Scoring
    SecuredPointsPolicy SecuredPoints, // None/KEEP_CHECKPOINT/KEEP_SECURED
    WithdrawalPolicy Withdrawal,    // LOSE_ALL / KEEP_CURRENT_SCORE / KEEP_SECURED_SCORE / KEEP_CHECKPOINT_SCORE
    LossPolicy FinishPolicy,        // LOSE_ALL / LOSE_CURRENT_ROUND / LOSE_UNSECURED_POINTS / FALLBACK_TO_CHECKPOINT
    Guid? FinalRewardId,            // FK Reward Active (opcional)
    Guid? ConsolationRewardId,      // FK Reward Active (opcional)
    DateTimeOffset? ScheduledAt,    // UTC ≥ now+5m si Scheduled
    GameStateView Status,
    string RowVersion               // base64 rowversion para If-Match
);

enum DifficultyStrategy { Linear, Progressive, Adaptive, CategorySpecific }
enum ScoringSystem { Standard, ProgressiveBonus }
enum SecuredPointsPolicy { None, KeepCheckpoint, KeepSecured }
enum WithdrawalPolicy { LoseAll, KeepCurrentScore, KeepSecuredScore, KeepCheckpointScore }
enum LossPolicy { LoseAll, LoseCurrentRound, LoseUnsecuredPoints, FallbackToCheckpoint }
```

**Invariantes**:
- `NumberOfRounds ∈ [5,10]`; `MaxPlayers ≥2`; `TimePerQuestion ∈ [5,300]`; `InitialDifficulty ∈ [1,5]`.
- `CategoryId` referencia `Category` con `Status==Active && ValidQuestionCount≥5` (Constitución B).
- `FinalRewardId`/`ConsolationRewardId` → `Reward.Status==Active && Stock>0` si aplica; no iguales cuando política exige distinción.
- `ScheduledAt` requerida para `Scheduled`, futura ≥5m, nula para `Draft`/`Configured` opcional.
- Inmutable tras `Ready`/`Running`/`Paused` (FR-010).

### GameSummary / GameDetail (proyecciones de listado)

```csharp
record GameSummary(
    Guid Id,
    string Name,
    Guid CategoryId,
    string CategoryName,
    GameStateView Status,
    int NumberOfRounds,
    int MaxPlayers,
    int TimePerQuestion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ScheduledAt);

record GameDetail : GameSummary
{
    string? Description;
    int InitialDifficulty;
    DifficultyStrategy DifficultyProgression;
    ScoringSystem Scoring;
    WithdrawalPolicy WithdrawalPolicy;
    LossPolicy FinishPolicy;
    Guid? FinalRewardId;
    Guid? ConsolationRewardId;
    string RowVersion;
    IReadOnlyList<GameStateTransition> History; // auditoría
}

record GameStateTransition(
    GameStateView From,
    GameStateView To,
    DateTimeOffset Timestamp,
    string ActorId,
    string? Reason);
```

### Category Reference (read-model)

```csharp
record CategorySummary(
    Guid Id,
    string Name,
    CategoryStatusView Status,
    int ValidQuestionCount);
```

### Reward Reference (read-model)

```csharp
record RewardSummary(
    Guid Id,
    string Name,
    RewardStatusView Status,
    int Stock);
```

### GameAuditEntry (append-only)

```csharp
record GameAuditEntry(
    Guid GameId,
    string ActorId, // sub
    DateTimeOffset Timestamp,
    GameStateView FromState,
    GameStateView ToState,
    IReadOnlyDictionary<string,string> ChangedFields,
    string CorrelationId,
    string Result);
```

## 2. DTOs de transporte (BFF boundary)

```csharp
record CreateGameRequest(
    string Name,
    string? Description,
    Guid CategoryId,
    int NumberOfRounds,
    int MaxPlayers,
    int TimePerQuestion,
    int InitialDifficulty,
    DifficultyStrategy DifficultyProgression,
    ScoringSystem Scoring,
    int PointsPerRound,
    WithdrawalPolicy WithdrawalPolicy,
    LossPolicy FinishPolicy,
    Guid? FinalRewardId,
    Guid? ConsolationRewardId,
    DateTimeOffset? ScheduledAt);

record UpdateGameRequest : CreateGameRequest
{
    string RowVersion; // If-Match
}

record GameResponse : GameDetail; // camelCase JSON
```

Paginación: `PagedResult<GameSummary> { Items, TotalCount, Page, PageSize }`.

## 3. Catálogos estáticos (client)

```csharp
static class GameCatalogs
{
    static IReadOnlyList<string> DifficultyStrategies => ["Linear","Progressive","Adaptive","CategorySpecific"];
    static IReadOnlyList<string> WithdrawalPolicies => ["LOSE_ALL","KEEP_CURRENT_SCORE","KEEP_SECURED_SCORE","KEEP_CHECKPOINT_SCORE"];
    static IReadOnlyList<string> LossPolicies => ["LOSE_ALL","LOSE_CURRENT_ROUND","LOSE_UNSECURED_POINTS","FALLBACK_TO_CHECKPOINT"];
    static IReadOnlyList<string> ScoringSystems => ["Standard","ProgressiveBonus"];
}
```

Mapeo `ToApi`/`FromApi` via `GameStateViewMap` (admin ↔ dominio).

## 4. Validación por niveles

- **API**: tipos/rangos → `400` + `FieldErrors` por campo.
- **Aplicación**: `Validator` FluentValidation — categoría ≥5, `ScheduledAt` futura, policies en catálogo, premios existentes.
- **Dominio**: invariantes `CategoryNotReady`, `InvalidConfiguration`, `InvalidGameState`, `RewardUnavailable`, `ConcurrencyConflict` (rowversion).

## 5. Relaciones

```text
GameConfiguration ── referencia 1 ──> Category (Active ≥5)
GameConfiguration ── referencia 0..1 ──> Reward (Final)
GameConfiguration ── referencia 0..1 ──> Reward (Consolation)
GameConfiguration ── contiene 1 ──> GameStateView (máquina 8 estados)
Game ── contiene N ──> GameStateTransition / GameAuditEntry
GameSummary ── deriva ──> PagedResult (listado filtrado por status/category)
```

## 6. Transiciones de estado (máquina)

```text
Draft → Configured [guard: configuración mínima válida]
Configured → Scheduled [guard: ScheduledAt futura + categoría válida]
Scheduled → Ready [guard: ScheduledAt alcanzable + categoría sigue válida]
Ready → Running [command: StartGame, inmutable desde aquí]
Running ↔ Paused [commands: PauseGame / ResumeGame, congela timer]
Running/Paused → Finished [terminal]
Draft/Configured/Scheduled → Cancelled [terminal]
Running/Paused → Cancelled [solo con auditoría si hay jugadores PLAYING]

Inválidas → InvalidGameState, sin mutación parcial, protegidas por rowversion.
```

No hay máquina de dominio nueva; es vista admin sobre estados de `Game` (001).

## 7. Reglas de autorización (proyección)

- `ADMIN`/`GAME_MANAGER` (`AdminOrGameManager`) → `Create/Update/Schedule/Ready/Start/Pause/Resume/Finish/Cancel`.
- `REWARD_MANAGER` → `403 Access Denied` en todas las rutas de configuración (FR-013).
