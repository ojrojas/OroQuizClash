# Contract: GameHub (SignalR) — SPEC-011 FR-014

**Date**: 2026-08-27 | **Endpoint**: `/hubs/game` | **Auth**: JWT Bearer (OroIdentityServer), `RequireAuthorization`

Notificaciones server-driven del estado multiplayer. **El hub nunca es fuente de verdad** (Constitución V): solo difunde hints de eventos ya persistidos; el estado autoritativo se obtiene por REST (`GET /api/games/{gameId}/players/{playerId}/state`, `GET /api/games/{gameId}/leaderboard`). Si una notificación se pierde, el cliente recupera el estado consultando.

## Conexión y grupos

- Cliente se conecta a `/hubs/game` con JWT válido.
- Cliente invoca el método de hub `JoinGameGroup(gameId)` para suscribirse a las notificaciones de un juego.
  - El hub valida que el usuario autenticado (`sub`) es jugador de ese juego o tiene rol organizador (`ADMIN`/`GAME_MANAGER`); en caso contrario rechaza la unión al grupo.
  - Grupo SignalR: `game-{gameId}`.
- El hub es **broadcast-only**: no acepta comandos de juego (enviar respuestas, unirse, retirarse). Todas las mutaciones se realizan por REST (ver `multiplayer.openapi.yaml`).

## Mensajes servidor → cliente

Todos se emiten al grupo `game-{gameId}` tras persistirse el evento de dominio correspondiente.

| Mensaje | Evento de dominio origen | Payload (JSON) |
|---------|--------------------------|----------------|
| `PlayerJoined` | `PlayerJoinedDomainEvent` | `{ "gameId": uuid, "playerId": uuid, "displayName": string? }` |
| `ScoreUpdated` | `ScoreUpdatedDomainEvent` | `{ "gameId": uuid, "playerId": uuid, "points": int, "totalPoints": int, "reason": string }` |
| `LeaderboardUpdated` | `AnswerEvaluatedDomainEvent`, `RoundCompletedDomainEvent` | `{ "gameId": uuid, "players": [LeaderboardEntry] }` — misma forma que `LeaderboardResponse` del contrato REST (Rank, Player, Points, CorrectAnswers, CurrentLevel, Status) |
| `PlayerStatusChanged` | `PlayerWithdrawnDomainEvent`, `PlayerEliminatedDomainEvent`, `GameFinishedDomainEvent` | `{ "gameId": uuid, "playerId": uuid, "status": "WITHDRAWN"\|"ELIMINATED"\|"WINNER"\|"FINISHED", "finalScore": int? }` |

## Semántica de entrega

- **Best-effort**: la publicación ocurre en los handlers de domain events dentro de la transacción de `SaveChanges` (pre-commit). Si la transacción falla tras publicarse una notificación, el cliente puede observar un hint sin estado persistido; la re-consulta REST corrige la vista (decisión R7 en research.md).
- Sin garantías de orden entre mensajes de distinto tipo; el cliente debe tratar cada mensaje como hint y usar el `Rank`/estado recibido del leaderboard como snapshot completo.
- Los eventos de integración externos (RabbitMQ/Outbox) no se ven afectados y conservan la garantía post-commit.

## Fuera de alcance

- Reconexión automática, historial de mensajes y delivery garantizado (el cliente usa polling REST como respaldo).
- Notificaciones cross-game o globales.
- Chat o mensajería entre jugadores.
