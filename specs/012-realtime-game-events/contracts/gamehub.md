# Contract: GameHub (SignalR) — SPEC-012 Realtime

**Date**: 2026-08-27 | **Plan**: [../plan.md](../plan.md) | **Spec**: [../spec.md](../spec.md) | **Endpoint**: `/hubs/game` | **Auth**: JWT Bearer (OroIdentityServer), `RequireAuthorization`

Notificaciones server-driven del estado de juego en tiempo real. **El hub nunca es fuente de verdad** (Constitución V, FR-014): solo difunde hints de eventos ya persistidos; el estado autoritativo se obtiene por REST (`GET /api/games/{gameId}`, `GET /api/games/{gameId}/rounds/current`, `GET /api/games/{gameId}/questions/current`, `GET /api/games/{gameId}/players/{playerId}/state`, `GET /api/games/{gameId}/leaderboard`, `GET /api/games/{gameId}/score/{playerId}/ledger`). Si una notificación se pierde, el cliente recupera el estado consultando.

Extiende el contrato de SPEC-011 (que ya definía `PlayerJoined`, `ScoreUpdated`, `LeaderboardUpdated`, `PlayerStatusChanged`) con los 5 eventos restantes del catálogo de 9.

## Conexión y grupos

- Cliente se conecta a `/hubs/game` con JWT válido (bearer).
- Cliente invoca el método de hub `JoinGameGroup(gameId: Guid)` para suscribirse a las notificaciones de un juego.
  - El hub valida que el usuario autenticado (`sub` claim) es jugador de ese juego (`game.Players.Any(p => p.UserId == sub)`) o tiene rol organizador (`ADMIN`/`GAME_MANAGER` vía `GameClaims.IsOrganizer`); en caso contrario rechaza con `HubException`.
  - Grupo SignalR: `game-{gameId}` (un único grupo por juego).
- El hub es **broadcast-only**: no acepta comandos de juego (enviar respuestas, unirse, retirarse, iniciar ronda). Todas las mutaciones se realizan por REST. Cualquier intento de invocar un método inexistente resulta en error del hub.
- Un jugador puede tener múltiples conexiones (varias pestañas) — cada `JoinGameGroup` añade la conexión al grupo; todas reciben los eventos.
- Desconexión: SignalR remueve la conexión del grupo automáticamente; no se requiere `Leave`. Al reconectar, el cliente debe volver a invocar `JoinGameGroup`.

## Mensajes servidor → cliente

Todos se emiten al grupo `game-{gameId}` tras persistirse el domain event correspondiente (FR-017). Entrega best-effort (FR-016/FR-019): un fallo de broadcast se loguea y no falla la operación.

| Mensaje | Domain event origen | Payload JSON | Audiencia |
|---------|---------------------|-------------|-----------|
| `GameStarted` | `GameStartedDomainEvent` | `{ "gameId": uuid }` | `game-{gameId}` |
| `PlayerJoined` | `PlayerJoinedDomainEvent` | `{ "gameId": uuid, "playerId": uuid, "displayName": string \| null }` | `game-{gameId}` |
| `RoundStarted` | `RoundStartedDomainEvent` | `{ "gameId": uuid, "roundId": uuid, "roundNumber": int }` | `game-{gameId}` (activos) |
| `QuestionPresented` | `RoundStartedDomainEvent.QuestionId` | `{ "gameId": uuid, "roundId": uuid, "roundNumber": int, "question": { "questionId": uuid, "text": string, "answerOptions": [{ "id": uuid, "text": string }] } }` | `game-{gameId}` (activos) |
| `PlayerAnswered` | `AnswerSubmittedDomainEvent` | `{ "gameId": uuid, "playerId": uuid, "roundId": uuid, "answeredAt": string (ISO 8601) }` | `game-{gameId}` (activos) |
| `ScoreUpdated` | `ScoreUpdatedDomainEvent` | `{ "gameId": uuid, "playerId": uuid, "points": int, "totalPoints": int, "reason": string }` | `game-{gameId}` |
| `LeaderboardUpdated` | `AnswerEvaluatedDomainEvent`, `RoundCompletedDomainEvent` | `{ "gameId": uuid, "entries": [ LeaderboardEntry ] }` | `game-{gameId}` |
| `RoundCompleted` | `RoundCompletedDomainEvent` | `{ "gameId": uuid, "roundId": uuid, "roundNumber": int }` | `game-{gameId}` (activos) |
| `GameFinished` | `GameFinishedDomainEvent` (y variantes Forced/Cancelled) | `{ "gameId": uuid, "status": string, "entries": [ LeaderboardEntry ] }` | `game-{gameId}` |

`LeaderboardEntry` — misma forma que `GET /api/games/{gameId}/leaderboard` (SPEC-011):

```json
{
  "playerId": "uuid",
  "displayName": "string | null",
  "rank": 1,
  "points": 10,
  "correctAnswers": 2,
  "currentLevel": 3,
  "status": "ACTIVE | WITHDRAWN | ELIMINATED | WINNER | FINISHED",
  "securedPoints": 5
}
```

### Notas de payload (anti-trampa)

- `QuestionPresented.question.answerOptions` contiene solo `{ id, text }` — **nunca** `isCorrect` / `correctOptionId`. Verificado por tests de filtrado (FR-004/FR-013, SC-006).
- `PlayerAnswered` contiene solo `{ gameId, playerId, roundId, answeredAt }` — **nunca** `answerOptionId`, `correct`, `points`. La correctitud se revela solo vía `ScoreUpdated`/`LeaderboardUpdated`/`RoundCompleted` tras evaluación (FR-005).
- `ScoreUpdated`/`LeaderboardUpdated`/`GameFinished.entries` reutilizan los DTOs REST existentes — SC-008 (coincidencia 100% con consulta tradicional) es verdadero por construcción.

## Semántica de entrega

- **Best-effort**: la publicación ocurre en handlers `IDomainEventHandler<>` (auto-registrados). Falla de broadcast → `ILogger.LogError` con `GameId`/`Event`, sin propagar excepción (FR-016). El juego continúa.
- **Post-persistencia**: los eventos se difunden después de que la operación esté persistida (FR-017). Si el dispatcher es pre-commit, el hint puede preceder brevemente al commit — el cliente re-consulta REST y el estado ya existe en memoria del agregado (tolerable; ver R5 en research.md).
- **Sin orden garantizado entre tipos de mensaje**: el cliente debe tratar cada mensaje como hint y usar el snapshot recibido (especialmente `LeaderboardUpdated`) como vista completa, no como delta. Orden intra-tipo preservado por emisión secuencial al mismo grupo.
- **Sin historial ni reenvío**: eventos perdidos no se reenvían (FR-019); recuperación vía REST (FR-015).
- **Eventos de integración externos** (RabbitMQ/Outbox) no se ven afectados y conservan garantía post-commit (Constitución G).

## Métodos cliente → servidor

| Método | Parámetros | Auth | Retorno | Notas |
|--------|-----------|------|---------|-------|
| `JoinGameGroup` | `gameId: Guid` | JWT requerido, `sub` ∈ juego o `IsOrganizer` | `Task` (void) | Lanza `HubException` si no autorizado o juego no existe. |

No existen otros métodos cliente→servidor. El hub no acepta `SendAnswer`, `Withdraw`, `StartRound`, etc.

## Errores del hub

- No autenticado → `HubException("Not authenticated.")` (conexión rechazada por `RequireAuthorization` antes de llegar al método).
- Juego no existe → `HubException("Game not found.")`.
- No es jugador ni organizador → `HubException("Only players of this game or organizers may subscribe.")`.

Todos mapean a error de hub en el cliente SignalR (no a ProblemDetails HTTP — es una conexión persistente).

## Fuera de alcance

- Reconexión automática con replay, historial de mensajes y delivery garantizado (el cliente usa REST como respaldo).
- Notificaciones cross-game o globales.
- Chat o mensajería entre jugadores.
- Eventos de recompensas/consuelo (`RewardRedeemed` etc.) — quedan fuera del catálogo de 9.
- Comandos de juego vía SignalR.

## Ejemplo de uso (cliente JS — ilustrativo, no normativo)

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/game", { accessTokenFactory: () => token })
  .withAutomaticReconnect()
  .build();

connection.on("GameStarted",      (payload) => { /* payload.gameId */ });
connection.on("PlayerJoined",     (payload) => { /* payload.playerId, displayName */ });
connection.on("RoundStarted",     (payload) => { /* payload.roundId, roundNumber */ });
connection.on("QuestionPresented",(payload) => { /* payload.question.text, answerOptions (sin isCorrect) */ });
connection.on("PlayerAnswered",   (payload) => { /* payload.playerId, roundId — sin opción ni correctitud */ });
connection.on("ScoreUpdated",     (payload) => { /* payload.playerId, points, totalPoints */ });
connection.on("LeaderboardUpdated",(payload) => { /* payload.entries — snapshot completo */ });
connection.on("RoundCompleted",   (payload) => { /* payload.roundId, roundNumber */ });
connection.on("GameFinished",     (payload) => { /* payload.status, entries */ });

await connection.start();
await connection.invoke("JoinGameGroup", gameId);
```
