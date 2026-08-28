# Data Model: Realtime Game Events (SPEC-012)

**Date**: 2026-08-27 | **Plan**: [plan.md](plan.md) | **Research**: [research.md](research.md)

Este SPEC no añade entidades persistidas. Formaliza la distribución efímera de eventos ya persistidos como estado de juego. Leyenda: **EXISTING** (sin cambios), **EXTEND** (se amplía el port/hub), **TRANSIENT** (mensaje efímero, no persistido), **DERIVED** (proyección de estado existente).

## Entidades persistidas — sin cambios

El modelo de persistencia no cambia respecto a SPEC-011. Referencia: [data-model.md de SPEC-011](../011-multiplayer/data-model.md).

- `Game` (AggregateRoot<GameId>) — EXISTING
- `GamePlayer` (Entity dentro de Game) — EXISTING (con `CurrentRoundNumber` y `RowVersion` de SPEC-011)
- `GameRound`, `Answer`, `PointTransaction`, `Question`, `AnswerOption` — EXISTING

Ninguna tabla nueva, ninguna columna nueva, ninguna migración.

## Mensajes efímeros (no persistidos) — TRANSIENT

Los 9 eventos son notificaciones efímeras: se construyen a partir del agregado ya persistido y se difunden al grupo SignalR; no se almacenan ni se reenvían.

### Catálogo

| # | Evento | Domain event origen | Audiencia | Persistido |
|---|--------|---------------------|-----------|------------|
| 1 | `GameStarted` | `GameStartedDomainEvent` | grupo `game-{gameId}` (jugadores + organizadores) | no |
| 2 | `PlayerJoined` | `PlayerJoinedDomainEvent` | grupo `game-{gameId}` | no |
| 3 | `RoundStarted` | `RoundStartedDomainEvent` | grupo `game-{gameId}` (activos) | no |
| 4 | `QuestionPresented` | `RoundStartedDomainEvent.QuestionId` | grupo `game-{gameId}` (activos, filtrado — ver R8) | no |
| 5 | `PlayerAnswered` | `AnswerSubmittedDomainEvent` | grupo `game-{gameId}` (activos) | no |
| 6 | `ScoreUpdated` | `ScoreUpdatedDomainEvent` | grupo `game-{gameId}` | no |
| 7 | `LeaderboardUpdated` | `AnswerEvaluatedDomainEvent` + `RoundCompletedDomainEvent` | grupo `game-{gameId}` | no |
| 8 | `RoundCompleted` | `RoundCompletedDomainEvent` | grupo `game-{gameId}` (activos) | no |
| 9 | `GameFinished` | `GameFinishedDomainEvent` (y variantes `GameForcedFinished`/`GameCancelled`) | grupo `game-{gameId}` | no |

### Payloads (contrato hub — ver [contracts/gamehub.md](contracts/gamehub.md) para el JSON exacto)

| Evento | Campos | Fuente | Notas de filtrado |
|--------|--------|--------|-------------------|
| `GameStarted` | `gameId: Guid` | `GameStartedDomainEvent.GameId` | — |
| `PlayerJoined` | `gameId: Guid`, `playerId: Guid`, `displayName: string?` | `PlayerJoinedDomainEvent` + `Game.Players[].DisplayName` | — |
| `RoundStarted` | `gameId: Guid`, `roundId: Guid`, `roundNumber: int` | `RoundStartedDomainEvent` | — |
| `QuestionPresented` | `gameId: Guid`, `roundId: Guid`, `roundNumber: int`, `question: { questionId, text, answerOptions: [{ id, text }] }` | `Question` cargada por `QuestionId` | **Filtrado anti-trampa**: `AnswerOption.IsCorrect` nunca se incluye (proyección `{ Id, Text }`); ver R4 |
| `PlayerAnswered` | `gameId: Guid`, `playerId: Guid`, `roundId: Guid`, `answeredAt: DateTimeOffset` | `AnswerSubmittedDomainEvent` | **Filtrado**: sin `AnswerOptionId`, sin `correct`, sin `points` (R4) |
| `ScoreUpdated` | `gameId: Guid`, `playerId: Guid`, `points: int`, `totalPoints: int`, `reason: string` | `ScoreUpdatedDomainEvent` | Reutiliza shape REST |
| `LeaderboardUpdated` | `gameId: Guid`, `entries: LeaderboardEntryResponse[]` | `LeaderboardBuilder.Build(game)` | Snapshot completo, mismo shape que `GET /leaderboard` |
| `RoundCompleted` | `gameId: Guid`, `roundId: Guid`, `roundNumber: int` | `RoundCompletedDomainEvent` | — |
| `GameFinished` | `gameId: Guid`, `status: string`, `entries: LeaderboardEntryResponse[]` | `GameFinishedDomainEvent` + `LeaderboardBuilder.Build(game)` | Incluye leaderboard final |

`LeaderboardEntryResponse` (reutilizado de SPEC-011): `{ playerId, displayName, rank, points, correctAnswers, currentLevel, status, securedPoints }`.

### Port extendido

`IGameNotificationsBroadcaster` (Application) — EXTEND: se añaden 5 métodos a los 4 existentes de SPEC-011.

```csharp
// EXISTING (SPEC-011)
Task PlayerJoinedAsync(Guid gameId, Guid playerId, string? displayName, ...);
Task ScoreUpdatedAsync(Guid gameId, Guid playerId, int points, int totalPoints, string reason, ...);
Task LeaderboardUpdatedAsync(Guid gameId, IReadOnlyList<LeaderboardEntryResponse> entries, ...);
Task PlayerStatusChangedAsync(Guid gameId, Guid playerId, string status, int? finalScore, ...);

// NEW (SPEC-012)
Task GameStartedAsync(Guid gameId, CancellationToken ct = default);
Task RoundStartedAsync(Guid gameId, Guid roundId, int roundNumber, CancellationToken ct = default);
Task QuestionPresentedAsync(Guid gameId, Guid roundId, int roundNumber, QuestionPresentedPayload payload, CancellationToken ct = default);
Task PlayerAnsweredAsync(Guid gameId, Guid playerId, Guid roundId, DateTimeOffset answeredAt, CancellationToken ct = default);
Task RoundCompletedAsync(Guid gameId, Guid roundId, int roundNumber, CancellationToken ct = default);
Task GameFinishedAsync(Guid gameId, string status, IReadOnlyList<LeaderboardEntryResponse> entries, CancellationToken ct = default);
```

`QuestionPresentedPayload`: `{ Guid QuestionId, string Text, IReadOnlyList<{ Guid Id, string Text }> AnswerOptions }` — sin `IsCorrect`.

Implementación: `SignalRGameNotificationsBroadcaster` (Api) mapea cada método a `IHubContext<GameHub>.Clients.Group($"game-{gameId}").SendAsync("<EventName>", payload, ct)`.

### Hub y grupos

- Hub: `GameHub : Hub` en `/hubs/game` — EXISTING, se documenta el catálogo completo.
- Grupo: `game-{gameId}` — EXISTING. Un único grupo por juego (ver R8 para el filtrado de retirados — cliente ignora contenido de ronda tras `WITHDRAWN`; filtrado server-side es mejora futura sin cambio de contrato).
- Método cliente→servidor: `JoinGameGroup(Guid gameId)` — EXISTING. Valida JWT `sub` ∈ `game.Players` o `IsOrganizer`; `Groups.AddToGroupAsync`.
- Sin nuevos métodos cliente→servidor (hub broadcast-only — Constitución V).
- Conexión: `RequireAuthorization` (JWT OroIdentityServer) — EXISTING.

## Relaciones

```text
Game (1) ──< (N) GamePlayer          # audiencia: Active + organizadores
Game (1) ──< (N) GameRound           # RoundStarted/RoundCompleted → RoundId/RoundNumber
GameRound (1) ── 1 Question          # QuestionPresented → QuestionId → Question
Game (1) ──< (N) Answer              # PlayerAnswered → AnswerSubmitted (sin correctitud)
Answer (1) ── 1 PointTransaction     # ScoreUpdated / LeaderboardUpdated (post-evaluación)
Game ──> LeaderboardEntry[]          # LeaderboardUpdated / GameFinished (snapshot completo)
GameHub.Group("game-{gameId}") ──< (N) Connection  # SignalR group membership (transient)
Connection ── 1 User (JWT sub)       # autenticación OroIdentityServer
```

## Invariantes de distribución

1. Ningún evento muta estado — solo anuncia estado YA persistido (FR-002/FR-017).
2. Ningún payload revela `IsCorrect` ni `AnswerOptionId` ajeno antes de divulgación oficial (FR-004/FR-005/FR-013).
3. Ningún evento se entrega fuera de su `game-{gameId}` (FR-010/FR-011).
4. Un fallo de broadcast nunca falla la operación que lo originó (FR-016) — handlers capturan y loguean.
5. Todo estado anunciado es verificable vía REST (FR-014/FR-015) — el cliente puede reconstruir el estado completo sin haber recibido ningún evento.
6. `LeaderboardUpdated` es siempre un snapshot completo determinista (mismo orden que `GET /leaderboard` — FR-007).

## Sin estado persistido adicional

Este diseño no crea tablas, índices ni columnas. Si en el futuro se requiere historial de eventos o replay, se evaluará un store de eventos dedicado (fuera de alcance de SPEC-012 — delivery es best-effort, recuperación vía fuente de verdad).
