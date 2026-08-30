# API Contracts: Player Rewards (036)

**Branch**: `036-player-rewards` | **Date**: 2026-08-29 | **Spec**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)

Flujo `Points Wallet` → `Rewards Catalog` `Available/Required/Remaining/Reward Status` → `POST /rewards/{id}/redeem` `X-Idempotency-Key` per `rewardId` + `GET /redemptions` History + `Consolation`.

## 1. GET /api/rewards — Rewards Catalog + Available Points

**Reuse** `GetRewards` — lista recompensas con `AvailablePoints` autoritativo per `sub` via `GamePlayer.Score.CurrentPoints` (ledger).

### Request

```
GET /api/rewards?gameId={gameId}&includeUnavailable=false
Authorization: Bearer <JWT oroclash-api, sub=PlayerId>
X-Correlation-Id: <uuid v4>
```

- `gameId`: opcional Guid para derivar `AvailablePoints` per `Game`; sin `gameId` solo catalog sin Available.
- Auth: JWT `jwks_uri`, `sub=PlayerId`, sin JWT → 401.
- Query: `includeUnavailable` default false (solo `AvailableRewardsSpecification`); true para admin.

### Response 200 OK

```json
{
  "rewards": [
    {
      "id": "reward-uuid",
      "name": "Pack Oro",
      "description": "Skin premium",
      "pointsRequired": 800,
      "stock": 10,
      "status": "ACTIVE",
      "expirationDate": null,
      "available": true
    }
  ],
  "availablePoints": 1200,
  "gameId": "game-uuid"
}
```

- `availablePoints`: `GamePlayer.Score.CurrentPoints` si `gameId` + jugador en game; null si no contexto.
- Frontend deriva: `Reward Status = Canjeable` si `availablePoints >= pointsRequired && available === true`; `Puntos insuficientes` si `availablePoints < pointsRequired`; `Agotada` si `stock===0`; `No disponible` si `status INACTIVE` o expired.
- `Remaining Points` proyección: si canjeable `available - pointsRequired` (400); si faltante `pointsRequired - available` (700) para mensaje "Te faltan X".
- Error 401 sin JWT; 403 `PlayerNotInGame` si `gameId` inválido para sub pero aún retorna catalog sin available.

## 2. POST /api/rewards/{rewardId}/redeem — Redeem con idempotencia per rewardId

**Reuse** `RedeemReward` — `Reward.ReserveStock` + `Game.ConsumePoints` + `RewardRedemption.Create` + `REWARD_REDEMPTION` ledger atómico.

### Request

```
POST /api/rewards/{rewardId}/redeem
Authorization: Bearer <JWT oroclash-api, sub=PlayerId>
X-Idempotency-Key: <uuid v4 per rewardId sessionStorage idemp-redeem-{rewardId}>
X-Correlation-Id: <uuid v4>
Content-Type: application/json

{
  "gameId": "game-uuid",
  "idempotencyKey": "same-as-header-uuid"
}
```

- `rewardId`: Guid `RewardId`.
- Body `gameId`: Guid `GameId` contexto para `ConsumePoints` (ledger per game).
- `X-Idempotency-Key` UUID per `rewardId` `sessionStorage` `idemp-redeem-{rewardId}`; reuso misma key para reintento.
- Auth: `sub` es `PlayerId`; `must_change_password` gating 302 → `/auth/change-password`.

### Responses

#### 200 OK — Canje exitoso (primero o idempotente reuso misma key)

```json
{
  "redemptionId": "redeem-uuid",
  "rewardId": "reward-uuid",
  "gameId": "game-uuid",
  "points": 800,
  "status": "REQUESTED",
  "requestedAt": "2026-08-29T12:00:00Z"
}
```

- Segundo `POST` misma `X-Idempotency-Key` → mismo `redemptionId` `REQUESTED` sin nuevo `PointTransaction` ledger `COUNT` ni segundo `RewardRedemption` (idempotente `UNIQUE (PlayerId,IdempotencyKey)`).
- Side effects: `Reward.Stock--` + `Game.PointTransactions` `REWARD_REDEMPTION` `-800` `ResultingBalance 400` + `RewardRedeemed` Outbox → RabbitMQ `RewardRedeemed`.

#### 409 RewardUnavailable — recompensa inactiva/agotada/expirada

```json
{
  "type": "https://httpstatuses.com/409",
  "title": "RewardUnavailable",
  "detail": "Reward is inactive, out of stock, or expired.",
  "status": 409,
  "code": "RewardUnavailable",
  "traceId": "00-...",
  "correlationId": "corr-uuid"
}
```

#### 409 InsufficientPoints / GameErrors.InsufficientPoints — saldo insuficiente

```json
{
  "type": "https://httpstatuses.com/409",
  "title": "InsufficientPoints",
  "detail": "Insufficient points. Required 1500, available 800.",
  "status": 409,
  "code": "InsufficientPoints",
  "traceId": "00-...",
  "correlationId": "corr-uuid"
}
```

- Generado por `Game.ConsumePoints` `SufficientBalanceRule` si `Available 800 < Required 1500`; `Stock` revertido via `ReleaseStock()`.

#### 404 RewardNotFound — rewardId o gameId no existe

```json
{
  "type": "https://httpstatuses.com/404",
  "title": "RewardNotFound",
  "status": 404,
  "code": "RewardNotFound",
  "correlationId": "corr-uuid"
}
```

#### 403 PlayerNotInGame — jugador no en gameId

```json
{
  "type": "https://httpstatuses.com/403",
  "title": "PlayerNotInGame",
  "status": 403,
  "code": "PlayerNotInGame",
  "correlationId": "corr-uuid"
}
```

## 3. GET /api/redemptions — Redemption History (por jugador)

**Reuse** `GetPlayerRedemptions` — historial por `sub`.

### Request

```
GET /api/redemptions
Authorization: Bearer <JWT>
X-Correlation-Id: <uuid>
```

- Auth `sub` derivado, no parámetro `playerId` (previene acceso a historial ajeno).

### Response 200 OK

```json
{
  "redemptions": [
    {
      "id": "redeem-uuid",
      "rewardId": "reward-uuid",
      "gameId": "game-uuid",
      "points": 800,
      "status": "REQUESTED",
      "requestedAt": "2026-08-29T12:00:00Z",
      "deliveredAt": null
    },
    {
      "id": "consolation-uuid",
      "rewardId": "consolation-reward-uuid",
      "gameId": "game-uuid",
      "points": 0,
      "status": "APPROVED",
      "requestedAt": "2026-08-29T12:30:00Z",
      "deliveredAt": "2026-08-29T12:30:00Z"
    }
  ]
}
```

- Orden `RequestedAt` desc; paginación opcional `?page=1&pageSize=20` si `GetRedemptions` con espeficicación paginada; vacío → `[]` con `200`.
- `Consolation` se expone con `points 0` `status APPROVED` y puede incluir `transitions` si `GetRedemptions` extendido; frontend badge `Consolation`.

## 4. Consolation Reward (automática, no endpoint canje manual)

- **Trigger**: `Game.Finish()` evalúa `ConsolationPolicy` (None/FixedPoints/RewardBased/ParticipationBased) + `GameConfiguration` thresholds; si elegible y no `Withdrawn`/`Eliminated`, crea `RewardRedemption.CreateAsConsolation(playerId, rewardId, gameId)` `APPROVED` `points 0` + `PointTransaction` `CONSOLATION` si `FixedPoints` (crédito) → visible en `GET /redemptions` y en `GET /rewards?gameId` `availablePoints` actualizado si crédito.
- **No canje manual**: `Consolation` no aparece en `GET /rewards` como `Canjeable`; solo en history.

## 5. Headers transversales

- `X-Correlation-Id`: UUID per request `correlationIdInterceptor` backend echo en `ProblemDetails.correlationId`.
- `Authorization: Bearer` solo `secureRoutes=[apiUrl]` (`authInterceptor`), nunca a `identityAuthority`.
- Errores siempre RFC7807 `ProblemDetails` con `traceId` + `correlationId`.

