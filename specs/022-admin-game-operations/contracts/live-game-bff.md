# Contract: Live Game BFF

**Branch**: `022-admin-game-operations` | **Date**: 2026-08-28

Contrato de lectura en vivo (10 indicadores) y listado de juegos activos. El cliente WASM nunca toca el API directo (BFF obligatorio — Constitución H, FR-016).

## 1. Endpoints BFF

Todos `RequireAuthorization: AnyAdminRole` para lectura (el listado y el detalle son visibles para `ADMIN`/`GAME_MANAGER`; `REWARD_MANAGER` recibe 403). Forwarder YARP `/bff/{**catch-all}` → `http://oroclash-api/api/{**catch-all}` ya existe (017). Hub WebSocket `/hubs/game` via `MapGameHubForwarder` ya existe.

```
GET    /bff/games                    → GET    /api/games?status=Running&page=&pageSize=   (para /admin/live listado)
GET    /bff/games/{id}               → GET    /api/games/{id}
GET    /bff/games/{id}/leaderboard   → GET    /api/games/{id}/leaderboard
GET    /bff/games/{id}/players       → GET    /api/games/{id}/players?status=
GET    /bff/games/{id}/questions/current → GET /api/games/{id}/questions/current
GET    /bff/games/{id}/live          → GET    /api/games/{id}/live  (agregado 10 indicadores, si existe; si no, fallback a 4 calls)
WebSocket /hubs/game                 →  forwarder a http://oroclash-api/hubs/game (Group game-{id})
```

Auth: cookie de sesión; forwarder adjunta `Authorization: Bearer {access_token}` server-side. `X-Correlation-Id` propagado. `RowVersion` en `ETag`/`If-Match`.

## 2. GET /bff/games/{id}/live — agregado 10 indicadores

Si el API expone `/api/games/{id}/live`, el BFF lo proxy directo. Si no, el BFF hace fan-out server-side `Task.WhenAll` sobre 4 endpoints y compone `LiveGameView` (research R2 fallback).

**Request**

```http
GET /bff/games/3fa85f64-5717-4562-b3fc-2c963f66afa6/live HTTP/1.1
Cookie: .AspNetCore.Cookies=...
Accept: application/json
```

**Response 200** `Content-Type: application/json`

```json
{
  "gameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Running",
  "currentRound": 2,
  "currentQuestion": {
    "questionId": "b2e3c4d5-...-...",
    "text": "¿Cuál es la capital de Francia?",
    "options": [
      { "optionId":"...", "text":"Londres", "position":"A" },
      { "optionId":"...", "text":"París", "position":"B" },
      { "optionId":"...", "text":"Berlín", "position":"C" },
      { "optionId":"...", "text":"Roma", "position":"D" }
    ]
  },
  "totalRounds": 5,
  "players": 5,
  "playersConnected": 3,
  "playersAnswered": 2,
  "playersWaiting": 1,
  "scores": [
    { "playerId":"...", "displayName":"Ana", "score": 150, "securedPoints": 50, "level": 2, "hasAnswered": true },
    { "playerId":"...", "displayName":"Luis", "score": 100, "securedPoints": 0, "level": 1, "hasAnswered": false }
  ],
  "currentLevel": 2,
  "remainingSeconds": 18,
  "rowVersion": "AAAAAAAAB9E=",
  "lastUpdated": "2026-08-28T12:00:03Z"
}
```

**Notas**:
- `status` mapeado a `GameStateView` 8 estados (Draft..Cancelled + Running/Paused).
- `playersAnswered + playersWaiting == playersConnected` cuando hay `currentQuestion` y `status==Running`.
- `scores` ordenados por `score` desc, reconstruidos desde `PointTransaction` ledger.
- `remainingSeconds` derivado de `TimePerQuestion − (now − StartedAt)` server-side, congelado en `Paused`.
- `currentQuestion.correctAnswer` nunca se expone en vista operador (si se expone, es solo para debug y no es autoridad).

**Errores** `400`/`404`/`401`:

```json
{
  "type": "https://httpstatuses.com/404",
  "title": "GameNotFound",
  "status": 404
}
```

`401` si sesión expirada → polling/WebSocket se detiene + banner "Sesión expirada".

## 3. GET /bff/games?status=Running — listado /admin/live

**Response 200** `PagedResult<LiveGameView>` (solo campos `gameId`, `status`, `currentRound`, `players`, `playersConnected`)

```json
{
  "items": [ { "gameId":"...", "status":"Running", "currentRound":1, "players":5, "playersConnected":3 } ],
  "totalCount": 2,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

Usado para `Dashboard → Ver juegos activos → Live` drill-down coherente (SC-002 de 018).

## 4. WebSocket Hub `/hubs/game`

El cliente se conecta a `wss://admin-host/hubs/game` (misma origen). El forwarder YARP proxy WebSockets a `http://oroclash-api/hubs/game` con `Authorization: Bearer` del `access_token`.

- **Eventos server → client**: `QuestionAvailable`, `PlayerAnswered`, `ScoreUpdated`, `RoundCompleted`, `GamePaused`, `GameResumed`, `GameFinished`, `GameCancelled`.
- **Grupos**: `Groups.AddToGroupAsync(Context.ConnectionId, $"game-{gameId}")` server-side; el operador se une al grupo de su `gameId`.
- **Fallback**: si `HubConnection.State != Connected`, la UI usa polling 3–5s (`GET /bff/games/{id}/live`).

## 5. Contrato cliente (C#)

Vive en `QuizArena.Admin.Client/Services/ILiveGameService.cs` (existente) + `ILiveGameOperationsService.cs` (nuevo para operaciones):

```csharp
public interface ILiveGameService
{
    Task<PagedResult<LiveGameView>> GetLiveGamesAsync(LiveGamesFilter filter, CancellationToken ct = default);
    Task<LiveGameView> GetLiveGameAsync(Guid gameId, CancellationToken ct = default);
    Task<IReadOnlyList<LiveScore>> GetLeaderboardAsync(Guid gameId, CancellationToken ct = default);
}

public interface ILiveGameOperationsService
{
    Task<LiveGameView> PauseAsync(Guid gameId, string rowVersion, string idempotencyKey, CancellationToken ct = default);
    Task<LiveGameView> ResumeAsync(Guid gameId, string rowVersion, string idempotencyKey, CancellationToken ct = default);
    Task<LiveGameView> CancelAsync(Guid gameId, string rowVersion, string idempotencyKey, string? reason, CancellationToken ct = default);
    Task<LiveGameView> ForceFinishAsync(Guid gameId, string rowVersion, string idempotencyKey, CancellationToken ct = default);
}
```

Implementaciones:
- `ClientLiveGameService` (WASM): `HttpClient.GetFromJsonAsync<LiveGameView>("/bff/games/{id}/live")` + `HubConnection` a `"/hubs/game"`.
- `ServerLiveGameService` (InteractiveServer): `HttpClient.GetFromJsonAsync<LiveGameView>("http://oroclash-api/api/games/{id}/live")` + hub forwarder.

## 6. Validación de contrato

- `AdminBffTests` — no URLs absolutas en cliente, solo `/bff/*` y `/hubs/game`
- `DesignSystemNoDirectDbTests` — sin `DbContext` en Admin
- `LiveGameViewTests` — 10 indicadores + coherencia `Answered+Waiting == Connected` + `Scores` ledger
