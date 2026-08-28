# Contract: Player Detail BFF

**Branch**: `024-admin-players` | **Date**: 2026-05-13

Contrato de detalle de jugador (perfil, estado, estadísticas). Complementa `players-bff.md`. El cliente WASM nunca toca el API directo (BFF obligatorio).

## 1. Endpoints BFF

```
GET    /bff/players/{id}                  → GET    /api/players/{id}
GET    /bff/players/{id}/statistics       → GET    /api/players/{id}/statistics
```

Auth: cookie; forwarder adjunta `Bearer` + `X-Correlation-Id`. Políticas: `ADMIN` todo, `GAME_MANAGER` perfil/estadísticas, `REWARD_MANAGER` perfil básico.

## 2. Detail — GET /bff/players/{id}

Ver `players-bff.md` §3. `200` con `PlayerDetail` (perfil + `scoreSummary` + `state` + `totalParticipations` + `rowVersion`). `404` si `PlayerNotFound` (o 200 vacío según política), `403` si rol no autorizado, `401` si sesión expirada.

## 3. Statistics — GET /bff/players/{id}/statistics

Ver `players-bff.md` §6. `200` con `PlayerStatistics` snapshot (`totalGames`, `wins`, `averageScore`, `accuracyRate`, `bestStreak`, `distributionByDifficulty/Category`, `calculatedAt`). Calculadas server-side.

## 4. Contrato cliente (C#)

```csharp
Task<PlayerDetail> GetPlayerAsync(Guid playerId, CancellationToken ct = default);
Task<PlayerStatistics> GetStatisticsAsync(Guid playerId, CancellationToken ct = default);
```

Ver `players-bff.md` §7 para `IPlayersService` completo.

## 5. Validación de contrato

- `AdminBffTests` — solo `/bff/*` relativo
- `PlayerProfileTests` — `from<=to`, `PlayerNotFound`, 403 por rol
