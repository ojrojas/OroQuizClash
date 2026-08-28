# Contract: Live Operations

**Branch**: `022-admin-game-operations` | **Date**: 2026-08-28

Contrato de las 4 acciones controladas con confirmación, `RowVersion`/`IdempotencyKey` y auditoría append-only (FR-010..014, FR-019).

## 1. Endpoints BFF

Todos `RequireAuthorization: AdminOrGameManager` (403 si `REWARD_MANAGER`). Forwarder YARP catch-all ya existe.

```
POST /bff/games/{id}/pause       → POST /api/games/{id}/pause       { rowVersion, idempotencyKey }
POST /bff/games/{id}/resume      → POST /api/games/{id}/resume      { rowVersion, idempotencyKey }
POST /bff/games/{id}/cancel      → POST /api/games/{id}/cancel      { rowVersion, idempotencyKey, reason? }
POST /bff/games/{id}/force-finish→ POST /api/games/{id}/force-finish { rowVersion, idempotencyKey, reason? }
```

Headers: `If-Match: W/"{RowVersion}"` (alternativa a body) + `X-Idempotency-Key: {uuid}` + `X-Correlation-Id`.

## 2. Request/Response por operación

**Pause**

```http
POST /bff/games/3fa85f64-5717-4562-b3fc-2c963f66afa6/pause HTTP/1.1
If-Match: W/"AAAAAAAAB9E="
X-Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
Content-Type: application/json

{ "rowVersion": "AAAAAAAAB9E=", "idempotencyKey": "550e8400-e29b-41d4-a716-446655440000" }
```

**200 OK** con nuevo `LiveGameView` (`status: Paused`, `remainingSeconds` congelado, `rowVersion` nuevo):

```json
{
  "gameId":"3fa85f64-...",
  "status":"Paused",
  "remainingSeconds": 18,
  "rowVersion":"AAAAAAAAB9I=",
  "lastUpdated":"2026-08-28T12:01:00Z"
}
```

**Resume / Cancel / ForceFinish** — idem, con `reason` opcional para `Cancel`/`ForceFinish`:

```json
{ "rowVersion":"AAAAAAAAB9I=", "idempotencyKey":"...", "reason":"Incidencia técnica" }
```

## 3. Errores

| Código | HTTP | Cuando | Auditoría |
|--------|------|--------|-----------|
| `InvalidGameState` | 422 | Transición no permitida (p. ej., `Finished → Pause`, `Draft → ForceFinish`) | No |
| `ConcurrencyConflict` | 409 | `RowVersion` desactualizado (otro operador pausó antes) | No |
| `GameNotFound` | 404 | `GameId` inexistente | No |
| `Unauthorized` | 401 | sesión expirada → polling/WebSocket se detiene | No |
| `Forbidden` | 403 | `REWARD_MANAGER` → Access Denied | No |
| `IdempotentReplay` | 200 | Segundo intento con mismo `IdempotencyKey` → retorna mismo `LiveGameView` sin mutar ni duplicar auditoría | No nueva |

Todos `application/problem+json` con `errors.{field}` si aplica.

## 4. Auditoría append-only

Cada operación exitosa genera `GameAuditEntry` via Outbox en `SaveChanges`:

```json
{
  "gameId":"3fa85f64-...",
  "actorId":"sub-123",
  "timestamp":"2026-08-28T12:01:00Z",
  "fromState":"Running",
  "toState":"Paused",
  "action":"Pause",
  "reason": null,
  "correlationId":"00-abc123-01",
  "result":"Success",
  "idempotencyKey":"550e8400-...",
  "privileged": false
}
```

`ForceFinish` marca `privileged:true`. Intentos fallidos no generan auditoría de éxito (solo log de error con `CorrelationId`).

Expuesto como `GET /bff/games/{id}/audit?from=&to=` (si existe) o embebido en `GET /bff/games/{id}` con `history`.

## 5. Idempotencia

El servidor almacena `IdempotencyKey` por `GameId` + `Action` en `Outbox`/`Audit`. Segundo `POST` con mismo `IdempotencyKey` retorna `200` con el mismo `LiveGameView` (replay) sin mutar el estado ni crear segunda auditoría.

## 6. Contrato cliente (C#)

```csharp
public enum GameOperationKind { Pause, Resume, Cancel, ForceFinish }

public interface ILiveGameOperationsService
{
    Task<LiveGameView> PauseAsync(Guid gameId, string rowVersion, string idempotencyKey, CancellationToken ct = default);
    Task<LiveGameView> ResumeAsync(Guid gameId, string rowVersion, string idempotencyKey, CancellationToken ct = default);
    Task<LiveGameView> CancelAsync(Guid gameId, string rowVersion, string idempotencyKey, string? reason, CancellationToken ct = default);
    Task<LiveGameView> ForceFinishAsync(Guid gameId, string rowVersion, string idempotencyKey, CancellationToken ct = default);
}
```

Page `LiveOperationsBar.razor` genera `IdempotencyKey = Guid.NewGuid().ToString()` por click + diálogo de confirmación (`"¿Pausar partida? Se congelará el timer."` etc.) y deshabilita botones si `Status` no permite la transición (`Finished → Pause` deshabilitado con tooltip).

## 7. Validación de contrato

- `LiveOperationsTests` — 4 acciones con guardas, `InvalidGameState` 422, `ConcurrencyConflict` 409, idempotencia sin doble auditoría, 403 para `REWARD_MANAGER`
