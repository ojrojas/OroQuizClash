# Data Model: Admin Game Operations

**Branch**: `022-admin-game-operations` | **Date**: 2026-08-28 | **Plan**: [plan.md](plan.md)

Modelo de datos **frontend** (proyecciones/DTOs) para supervisión y control en vivo. Sin persistencia propia: estructuras en `QuizArena.Admin.Client/Models/LiveGame/` que reflejan contratos `oroclash-api /api/games*` y `GameHub` (SPEC-012). Autoridad permanece en backend (Constitución V).

## 1. Entidades principales

### LiveGameView

Proyección de lectura en tiempo casi real de una partida. Inmutable y reconstruida por polling/WebSocket.

```csharp
enum GameStateView
{
    Draft, Configured, Scheduled, Ready, Running, Paused, Finished, Cancelled
    // mapeo dominio: Running = IN_PROGRESS/ROUND_IN_PROGRESS/ROUND_COMPLETED, Paused = Running + IsPaused
}

record LiveGameView(
    Guid GameId,
    GameStateView Status,
    int CurrentRound,               // 0 si no iniciado
    QuestionView? CurrentQuestion,  // null si no hay ronda activa
    int TotalRounds,
    int Players,                    // total GamePlayer
    int PlayersConnected,           // presencia online
    int PlayersAnswered,            // respuesta válida para pregunta actual
    int PlayersWaiting,             // Connected − Answered (server-side)
    IReadOnlyList<LiveScore> Scores, // ledger
    int CurrentLevel,               // 1–5
    int RemainingSeconds,           // TimePerQuestion − (now − StartedAt), congelado en Paused
    string RowVersion,              // base64 rowversion para transiciones
    DateTimeOffset LastUpdated      // timestamp de generación server-side
);

record QuestionView(
    Guid QuestionId,
    string Text,
    IReadOnlyList<AnswerView> Options, // 4 opciones A–D, sin IsCorrect salvo política
    string? CorrectAnswer // solo si política lo permite, normalmente null en vista operador
);

record AnswerView(Guid OptionId, string Text, char Position); // A–D

record LiveScore(
    Guid PlayerId,
    string DisplayName,
    int Score,                      // reconstruido desde PointTransaction
    int SecuredPoints,
    int Level,                      // 1–5
    bool HasAnswered               // para la pregunta actual
);
```

**Invariantes**:
- `PlayersAnswered + PlayersWaiting == PlayersConnected` cuando `Status == Running` y hay `CurrentQuestion` (si `PlayersConnected ==0`, ambos 0).
- `Scores` ordenados por `Score` desc, reconstruibles desde `PointTransaction` ledger.
- `RemainingSeconds ∈ [0, TimePerQuestion]`, 0 si `Finished`/`Cancelled`, congelado si `Paused`.
- `RowVersion` incrementado en cada transición exitosa.

### GameRoundState (derivado)

```csharp
record GameRoundState(
    int RoundNumber,
    string Status, // WAITING, IN_PROGRESS, COMPLETED
    DateTimeOffset StartedAt,
    Guid? QuestionId);
```

No es entidad independiente; es parte de `LiveGameView` ( `CurrentRound`/`CurrentQuestion`).

### PlayerPresence (Live)

```csharp
record PlayerPresence(
    Guid GameId,
    int TotalPlayers,           // GamePlayer totales
    int Connected,              // LastSeen > now-2m o Hub presence
    int Answered,               // respuesta válida para pregunta actual
    int Waiting                 // Connected − Answered
);
```

Fuente: `GamePlayer` + hub `UserSession`. `Connected` es presencia, `Answered` es respuesta, `Waiting` derivado server-side.

### GameTimer

No es entidad persistida; es campo `RemainingSeconds` derivado de `TimePerQuestion` y `StartedAt` server-side. UI lo decrementa localmente 1s pero se re-sincroniza cada 3–5s con el servidor.

### GameOperation

Comando privilegiado para transiciones. No es proyección, es comando.

```csharp
enum GameOperationKind
{
    Pause,      // Running → Paused
    Resume,     // Paused → Running
    Cancel,     // * → Cancelled
    ForceFinish // Running/Paused → Finished (forzado)
}

record GameOperation(
    Guid GameId,
    GameOperationKind Kind,
    string RowVersion,          // If-Match
    string IdempotencyKey,      // UUID v4, X-Idempotency-Key
    string? Reason,             // para Cancel/ForceFinish
    string ActorId,             // sub de JWT
    DateTimeOffset Timestamp,
    string CorrelationId);
```

### GameAuditEntry (Live)

Registro append-only para operaciones privilegiadas.

```csharp
record GameAuditEntry(
    Guid GameId,
    string ActorId,             // sub
    DateTimeOffset Timestamp,
    GameStateView FromState,
    GameStateView ToState,
    string Action,              // Pause/Resume/Cancel/ForceFinish
    string? Reason,
    string CorrelationId,
    string Result,              // Success/InvalidGameState/ConcurrencyConflict
    string IdempotencyKey,
    bool Privileged             // true para ForceFinish
);
```

## 2. DTOs de transporte (BFF boundary)

```csharp
record LiveGameResponse : LiveGameView; // camelCase JSON

record GameOperationRequest(
    string Action, // pause/resume/cancel/force-finish
    string RowVersion,
    string IdempotencyKey,
    string? Reason);

record LiveGamesFilter(
    GameStateView? Status = null, // Running/Paused para listado /admin/live
    int Page = 1,
    int PageSize = 20);
```

Paginación: `PagedResult<LiveGameView> { Items, TotalCount, Page, PageSize }` para `GET /bff/games?status=Running`.

## 3. Validación y estados por indicador

- Cada indicador lleva `State` (`Loading/Ready/Empty/Error`) y `Retryable` para reintento aislado (research R6).
- `Loading` → skeleton, `Empty` → 0 con mensaje, `Error` → `ApiErrorView` con `Retry`.

## 4. Relaciones

```text
LiveGameView ── contiene 1 ──> GameStateView (8 estados)
LiveGameView ── contiene 0..1 ──> QuestionView (4 AnswerView)
LiveGameView ── contiene N ──> LiveScore (1 por Player)
LiveGameView ── deriva ──> PlayerPresence (4 conteos)
LiveGameView ── deriva ──> GameTimer (RemainingSeconds)
GameOperation ── genera 1 ──> GameAuditEntry
GameAuditEntry ── referencia 1 ──> LiveGameView (GameId)
```

## 5. Transiciones de estado (máquina 8 estados, vista admin)

```text
Draft → Configured → Scheduled → Ready → Running ↔ Paused → Finished
  │         │            │                              (Resume)
  └─────────┴────────────┴──► Cancelled (terminal desde Draft/Configured/Scheduled/Ready/Running/Paused)
Running/Paused → Finished (normal) y → Finished (forzado via ForceFinish con privileged:true)

Inválidas → InvalidGameState, sin mutación parcial, protegidas por rowversion + IdempotencyKey.
```

`ForceFinish` es `Running/Paused → Finished` forzado (no requiere `RoundCompleted`).

## 6. Reglas de autorización (proyección)

- `ADMIN`/`GAME_MANAGER` (`AdminOrGameManager`) → `LiveGameView` lectura + 4 operaciones.
- `REWARD_MANAGER` → `403 Access Denied` en `LiveGameView` y en operaciones.
