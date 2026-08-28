# Contract: Realtime (Live Games vía BFF)

**Objetivo**: FR-020/023 — juegos en vivo actualizándose sin recarga, con estado de conexión visible y sin información privada de jugadores.

## 1. Topología

```
[WASM/Server UI] ── WS relativo /hubs/game ──> [QuizArena.Admin server]
      └─ MapForwarder("/hubs/game", "http://oroclash-api", Bearer transform).RequireAuthorization()
                └─ HttpForwarder proxya negotiate + WebSockets ──> [QuizArena.Api GameHub]
```

- El cliente construye `HubConnectionBuilder().WithUrl("/hubs/game").WithAutomaticReconnect()` **sin** `accessTokenFactory` (la cookie viaja en el handshake; el forwarder adjunta el JWT).
- El API ve el JWT real del operador; `JoinGameGroup(gameId)` autoriza por `GameClaims.IsOrganizer` (ADMIN/GAME_MANAGER).

## 2. Suscripción

1. UI Live Games carga snapshot inicial por REST: `GET /bff/games?status=Active` (o `ILiveGamesService.GetLiveGamesAsync`).
2. Por cada juego visible (o al abrir detalle), `ILiveGamesService.SubscribeAsync(gameId)` → conexión SignalR + `JoinGameGroup(gameId)`.
3. Eventos atendidos (catálogo GameHub, 9 eventos):

| Evento | Uso en Admin |
|--------|--------------|
| `GameStarted` | Actualizar estado/fila del juego |
| `PlayerJoined` | Conteo `ActivePlayers/TotalPlayers` |
| `RoundStarted` | Actualizar `Round` actual |
| `RoundCompleted` | Actualizar ronda/estado |
| `GameFinished` | Marcar finalizado; mover a histórico |
| `LeaderboardUpdated` | Refrescar agregados del leaderboard (detalle) |
| `QuestionPresented` | **Ignorado** (privacidad — contenido de pregunta) |
| `PlayerAnswered` | **Ignorado** (privacidad — respuesta individual) |
| `ScoreUpdated` | **Ignorado** en lista; solo agregados vía leaderboard |

## 3. Server Truth (Constitución V + GameHub FR-015/019)

- Los eventos son **best-effort**, nunca fuente de verdad.
- Tras **reconexión** (`WithAutomaticReconnect`), la UI **re-consulta REST** completa (snapshot) antes de volver a mostrar datos en vivo.
- Ante cualquier inconsistencia percibida, el operador puede forzar refresh (re-consulta REST).
- Ninguna acción administrativa se dispara automáticamente por un evento; las acciones (detener juego) requieren confirmación explícita (SC-011).

## 4. Estado de conexión (FR-023)

| Estado | UI |
|--------|----|
| `Connected` | Indicador verde sutil; datos en vivo |
| `Reconnecting` | Banner "Reconectando…"; filas congeladas con `aria-busy` |
| `Disconnected` | Banner de error + botón "Reconectar"; sin datos en vivo |

## 5. Privacidad (SPEC-016 §11 / FR-022)

- La UI admin MUST NOT renderizar respuestas individuales, temporizadores privados ni retiros de jugadores, aunque el evento llegue al grupo.
- Solo agregados: conteos, ronda, leaderboard público, estado del juego.

## 6. Fallback (si WS vía forwarder fallara en integración)

- Conexión server-side del admin al GameHub + hub propio de re-broadcast. No se implementa salvo evidencia de fallo (investigación R3).
