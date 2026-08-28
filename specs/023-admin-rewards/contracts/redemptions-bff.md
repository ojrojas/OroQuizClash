# Contract: Redemptions BFF

**Branch**: `023-admin-rewards` | **Date**: 2026-08-28

Contrato de canjes (`RewardRedemption`) con ciclo `Requested → Approved/Rejected → Delivered/Cancelled`. El cliente WASM nunca toca el API directo (BFF obligatorio — Constitución H, FR-018).

## 1. Endpoints BFF

Todos `RequireAuthorization: RewardManagerOrAdmin` (403 si `GAME_MANAGER`). Forwarder YARP catch-all ya existe.

```
GET    /bff/redemptions             → GET    /api/redemptions?status=&type=&playerId=&search=&from=&to=&page=&pageSize=
GET    /bff/redemptions/{id}        → GET    /api/redemptions/{id}
POST   /bff/redemptions/{id}/approve → POST /api/redemptions/{id}/approve { rowVersion, idempotencyKey }
POST   /bff/redemptions/{id}/reject  → POST /api/redemptions/{id}/reject  { rowVersion, idempotencyKey, reason }
POST   /bff/redemptions/{id}/deliver → POST /api/redemptions/{id}/deliver { rowVersion, idempotencyKey }
POST   /bff/redemptions/{id}/cancel  → POST /api/redemptions/{id}/cancel  { rowVersion, idempotencyKey, reason? }
```

Auth: cookie; forwarder adjunta `Bearer` + `X-Correlation-Id` + `X-Idempotency-Key` + `If-Match`.

## 2. List — GET /bff/redemptions

**Query** `status=Requested`, `type=Voucher`, `playerId`, `search` (reward/player), `from`/`to` (rango `RequestedAt`), `page`, `pageSize`.

**Response 200**

```json
{
  "items": [
    {
      "redemptionId": "b2e3c4d5-...-...",
      "rewardId": "3fa85f64-...",
      "rewardName": "Voucher Amazon 20€",
      "rewardType": "Voucher",
      "playerId": "sub-123",
      "playerName": "Ana",
      "cost": 100,
      "status": "Requested",
      "requestedAt": "2026-08-28T12:00:00Z",
      "rowVersion": "AAAAAAAAB9E=",
      "isConsolation": false
    }
  ],
  "totalCount": 12,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

## 3. Approve — POST /bff/redemptions/{id}/approve

**Request**

```http
POST /bff/redemptions/b2e3c4d5-.../approve
If-Match: W/"AAAAAAAAB9E="
X-Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
Content-Type: application/json

{ "rowVersion": "AAAAAAAAB9E=", "idempotencyKey": "550e8400-..." }
```

**Response 200**

```json
{
  "redemptionId":"b2e3c4d5-...",
  "status":"Approved",
  "approvedAt":"2026-08-28T12:01:00Z",
  "rowVersion":"AAAAAAAAB9I="
}
```

- Descuenta `Stock` (si limitado) de forma transaccional (`Reward.Stock--` + `PointTransaction` `REWARD_REDEMPTION` + `Redemption.Status=Approved` en misma transacción Outbox).
- `Stock` 0 con `Physical` limitado → `409 RewardOutOfStock` sin mutación.

**Reject — POST /bff/redemptions/{id}/reject**

```json
{ "rowVersion":"AAAAAAAAB9E=", "idempotencyKey":"...", "reason":"Fuera de stock o puntos insuficientes" }
```

**Response 200** con `status: Rejected`, `rejectedAt`, `reason`. No descuenta stock/puntos (o reembolsa si se había reservado según política de `009`).

**Deliver — POST /bff/redemptions/{id}/deliver**

```json
{ "rowVersion":"AAAAAAAAB9I=", "idempotencyKey":"..." }
```

**Response 200** con `status: Delivered`, `deliveredAt`, `deliveredBy` (actorId). Solo desde `Approved`.

**Cancel — POST /bff/redemptions/{id}/cancel**

```json
{ "rowVersion":"AAAAAAAAB9E=", "idempotencyKey":"...", "reason":"Cancelado por operador" }
```

`Requested`/`Approved` → `Cancelled` (terminal), libera inventario si se había reservado.

## 4. Errores

| Código | HTTP | Cuando | Auditoría |
|--------|------|--------|-----------|
| `InvalidRedemptionState` | 422 | Transición no permitida (p. ej., `Rejected → Delivered`, `Delivered → Approved`) | No |
| `RewardOutOfStock` | 409 | `Approve` con `Stock==0` y tipo `Physical` limitado | No |
| `InsufficientPoints` | 409 | `Cost` > puntos elegibles del jugador (ledger) | No (o `Rejected` inmediato) |
| `ConcurrencyConflict` | 409 | `RowVersion` desactualizado (otro operador aprobó antes) | No |
| `RewardUnavailable` | 400 | `Reward` no `Active` o fuera de disponibilidad/fechas | No |
| `Unauthorized` | 401 | sesión expirada | No |
| `Forbidden` | 403 | `GAME_MANAGER` → Access Denied | No |
| `IdempotentReplay` | 200 | Segundo intento con mismo `IdempotencyKey` → retorna mismo resultado sin mutar ni duplicar auditoría | No nueva |

Todos `application/problem+json` con `errors.{field}` si aplica.

## 5. Auditoría append-only

Cada transición exitosa genera `RedemptionAuditEntry` via Outbox en `SaveChanges`:

```json
{
  "redemptionId":"b2e3c4d5-...",
  "actorId":"sub-123",
  "timestamp":"2026-08-28T12:01:00Z",
  "fromState":"Requested",
  "toState":"Approved",
  "action":"Approve",
  "reason": null,
  "correlationId":"00-abc123-01",
  "result":"Success",
  "idempotencyKey":"550e8400-..."
}
```

Intentos fallidos no generan auditoría de éxito (solo log de error).

Expuesto como `GET /bff/redemptions/{id}` con `history` o `GET /bff/redemptions/{id}/audit`.

## 6. Idempotencia

El servidor almacena `IdempotencyKey` por `RedemptionId` + `Action` en `Outbox`/`Audit`. Segundo `POST` con mismo `IdempotencyKey` retorna `200` con el mismo resultado (replay) sin mutar `Stock`/`Score` ni crear segunda auditoría.

## 7. Contrato cliente (C#)

```csharp
public enum RedemptionStateView { Requested, Approved, Rejected, Delivered, Cancelled }

public interface IRedemptionsService
{
    Task<PagedResult<RewardRedemption>> GetRedemptionsAsync(RedemptionFilter filter, CancellationToken ct = default);
    Task<RewardRedemption> GetRedemptionAsync(Guid id, CancellationToken ct = default);
    Task<RewardRedemption> ApproveAsync(Guid id, string rowVersion, string idempotencyKey, CancellationToken ct = default);
    Task<RewardRedemption> RejectAsync(Guid id, string rowVersion, string idempotencyKey, string reason, CancellationToken ct = default);
    Task<RewardRedemption> DeliverAsync(Guid id, string rowVersion, string idempotencyKey, CancellationToken ct = default);
    Task<RewardRedemption> CancelAsync(Guid id, string rowVersion, string idempotencyKey, string? reason, CancellationToken ct = default);
}
```

Page `RedemptionsList.razor` (`/admin/rewards` pestaña "Canjes") consume `GetRedemptionsAsync` con filtros; `RedemptionRow.razor` muestra `Approve`/`Reject`/`Deliver`/`Cancel` con confirmación + `RowVersion` + `IdempotencyKey` (UUID v4 por click).

## 8. Validación de contrato

- `RedemptionTests` — 5 estados canje, guards, `RewardOutOfStock`, `InsufficientPoints`, `InvalidRedemptionState`, `ConcurrencyConflict`, idempotencia, 403 para `GAME_MANAGER`
