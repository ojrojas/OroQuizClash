# Contract: Players BFF

**Branch**: `024-admin-players` | **Date**: 2026-05-13

Contrato de listado y detalle de jugadores (solo lectura, 9 áreas). El cliente WASM nunca toca el API directo (BFF obligatorio — Constitución H, FR-014).

## 1. Endpoints BFF

Todos `RequireAuthorization` con políticas (`ADMIN` todo; `GAME_MANAGER` perfil/historial/estadísticas; `REWARD_MANAGER` premios/canjes). Forwarder YARP `/bff/{**catch-all}` → `http://oroclash-api/api/{**catch-all}` ya existe (017). `PLAYER` → 403.

```
GET    /bff/players                       → GET    /api/players?search=&page=&pageSize=
GET    /bff/players/{id}                  → GET    /api/players/{id}
GET    /bff/players/{id}/games            → GET    /api/players/{id}/games?search=&status=&from=&to=&page=&pageSize=
GET    /bff/players/{id}/participations   → GET    /api/players/{id}/participations?state=&from=&to=&page=&pageSize=
GET    /bff/players/{id}/results/{gameId} → GET    /api/players/{id}/results/{gameId}
GET    /bff/players/{id}/scores           → GET    /api/players/{id}/scores?type=&from=&to=&page=&pageSize=
GET    /bff/players/{id}/rewards          → GET    /api/players/{id}/rewards?status=&type=&page=&pageSize=
GET    /bff/players/{id}/redemptions      → GET    /api/players/{id}/redemptions?status=&rewardType=&from=&to=&page=&pageSize=
GET    /bff/players/{id}/statistics       → GET    /api/players/{id}/statistics
```

Auth: cookie de sesión; forwarder adjunta `Authorization: Bearer {access_token}` server-side. `X-Correlation-Id` propagado. Solo `GET` (solo lectura en v1).

## 2. List — GET /bff/players

**Query** `search` (nombre/email/sub parcial, case-insensitive), `page`, `pageSize` (default 20, max 100).

**Response 200**

```json
{
  "items": [
    {
      "playerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "displayName": "Ana García",
      "email": "ana@example.com",
      "tenantId": "tenant-1",
      "identificationType": "DNI",
      "identificationValue": "12345678A",
      "createdAt": "2026-01-15T10:00:00Z",
      "lastActiveAt": "2026-05-12T18:30:00Z",
      "state": "Active"
    }
  ],
  "totalCount": 542,
  "page": 1,
  "pageSize": 20,
  "totalPages": 28
}
```

**Errores** `400` si `page`/`pageSize` fuera de rango o `from>to` en otros endpoints, con `ProblemDetails` + `errors.{field}`.

## 3. Detail — GET /bff/players/{id}

**Response 200**

```json
{
  "playerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "displayName": "Ana García",
  "email": "ana@example.com",
  "tenantId": "tenant-1",
  "identificationType": "DNI",
  "identificationValue": "12345678A",
  "createdAt": "2026-01-15T10:00:00Z",
  "lastActiveAt": "2026-05-12T18:30:00Z",
  "state": "InGame",
  "scoreSummary": { "totalPoints": 2450, "securedPoints": 1200, "availablePoints": 1250 },
  "totalParticipations": 42,
  "rowVersion": "AAAAAAAAB9E="
}
```

**Errores**:
- `404 PlayerNotFound` si `sub` no existe (o `200` con perfil vacío según política de auditoría — documentado en `data-model.md`).
- `403` si `REWARD_MANAGER` intenta historial/estadísticas fuera de su matriz.
- `401` si sesión expirada.

## 4. Historial / Participaciones / Resultados

**GET /bff/players/{id}/games**

```json
{
  "items": [
    {
      "gameId": "b2e3c4d5-....",
      "gameName": "Quiz Historia 001",
      "categoryId": "c1...",
      "categoryName": "Historia",
      "status": "FINISHED",
      "createdAt": "2026-05-10T10:00:00Z",
      "finishedAt": "2026-05-10T10:25:00Z",
      "roundCount": 5,
      "playerScore": 320,
      "playerRank": 2
    }
  ],
  "totalCount": 128,
  "page": 1,
  "pageSize": 20
}
```

**GET /bff/players/{id}/participations** similar con `joinedAt`, `state`, `gameStatus`.

**GET /bff/players/{id}/results/{gameId}**

```json
{
  "playerId": "3fa...",
  "gameId": "b2e...",
  "totalScore": 320,
  "securedScore": 150,
  "rank": 2,
  "correctAnswers": 8,
  "totalAnswers": 10,
  "duration": "00:25:00",
  "bonuses": [{ "type": "ROUND_BONUS", "points": 50 }],
  "penalties": []
}
```

## 5. Puntuaciones — GET /bff/players/{id}/scores

**Query** `type` (10 tipos), `from`/`to`, `page`.

**Response 200**

```json
{
  "items": [
    { "transactionId": "t1...", "playerId": "3fa...", "gameId": "b2e...", "type": "ANSWER_CORRECT", "points": 100, "timestamp": "2026-05-10T10:05:00Z", "referenceId": "b2e..." },
    { "transactionId": "t2...", "type": "PENALTY", "points": -50, "timestamp": "2026-05-10T10:10:00Z" }
  ],
  "totalCount": 340,
  "page": 1,
  "pageSize": 20
}
```

Total reconstruido server-side (`SUM(points)`). Desglose incluye `CONSOLATION` y `REWARD_REDEMPTION`.

## 6. Premios / Canjes / Estadísticas

**GET /bff/players/{id}/rewards** — lista `RewardSummary` con `IsEligible`.

**GET /bff/players/{id}/redemptions**

```json
{
  "items": [
    {
      "redemptionId": "r1...",
      "rewardId": "rw1...",
      "rewardName": "Voucher Amazon 20€",
      "rewardType": "Voucher",
      "cost": 100,
      "status": "Approved",
      "requestedAt": "2026-05-10T12:00:00Z",
      "reason": null,
      "isConsolation": false,
      "rowVersion": "AAAAAAAAB9E="
    }
  ],
  "totalCount": 12,
  "page": 1,
  "pageSize": 20
}
```

`isConsolation:true` solo si `RewardType==Consolation` (no cuenta como premio normal).

**GET /bff/players/{id}/statistics**

```json
{
  "playerId": "3fa...",
  "totalGames": 42,
  "wins": 5,
  "top3": 12,
  "averageScore": 245.5,
  "accuracyRate": 0.78,
  "bestStreak": 7,
  "averageTimePerQuestion": "00:00:18",
  "distributionByDifficulty": { "1": 10, "2": 15 },
  "distributionByCategory": { "Historia": 20 },
  "calculatedAt": "2026-05-13T10:00:00Z"
}
```

Calculadas server-side, snapshot con `calculatedAt`.

## 7. Contrato cliente (C#)

Vive en `QuizArena.Admin.Client/Services/IPlayersService.cs` (existente, extender):

```csharp
public interface IPlayersService
{
    Task<PagedResult<PlayerSummary>> GetPlayersAsync(PlayerFilter filter, CancellationToken ct = default);
    Task<PlayerDetail> GetPlayerAsync(Guid playerId, CancellationToken ct = default);
    Task<PagedResult<GameHistoryEntry>> GetPlayerGamesAsync(Guid playerId, GameHistoryFilter filter, CancellationToken ct = default);
    Task<PagedResult<PlayerParticipation>> GetParticipationsAsync(Guid playerId, ParticipationFilter filter, CancellationToken ct = default);
    Task<PlayerResult> GetResultAsync(Guid playerId, Guid gameId, CancellationToken ct = default);
    Task<PagedResult<PointTransactionView>> GetScoresAsync(Guid playerId, ScoreFilter filter, CancellationToken ct = default);
    Task<PagedResult<PlayerRedemptionView>> GetRedemptionsAsync(Guid playerId, RedemptionFilter filter, CancellationToken ct = default);
    Task<PlayerStatistics> GetStatisticsAsync(Guid playerId, CancellationToken ct = default);
}
```

Implementaciones:
- `ClientPlayersService` (WASM): `HttpClient.GetFromJsonAsync("/bff/players...")` etc.
- `ServerPlayersService` (InteractiveServer): `HttpClient.GetFromJsonAsync("http://oroclash-api/api/players...")` con `Bearer`.

## 8. Validación de contrato

- `AdminBffTests` — no URLs absolutas en cliente, solo `/bff/*`
- `DesignSystemNoDirectDbTests` — sin `DbContext` en Admin
- `PlayerProfileTests` — perfil/estado solo lectura, paginación, `from<=to`, `PlayerNotFound` 404
- `PlayerStatisticsTests` — ledger desglose 10 tipos, `IsConsolation`, filtros, 403 por rol
