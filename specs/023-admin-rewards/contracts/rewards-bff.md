# Contract: Rewards BFF

**Branch**: `023-admin-rewards` | **Date**: 2026-08-28

Contrato de catálogo de premios (7 campos, 6 tipos). El cliente WASM nunca toca el API directo (BFF obligatorio — Constitución H, FR-018).

## 1. Endpoints BFF

Todos `RequireAuthorization: RewardManagerOrAdmin` para escritura y lectura de catálogo (el listado de premios para jugadores es `AnyAdminRole` si se expone, pero Admin Rewards requiere `RewardManagerOrAdmin`; `GAME_MANAGER` recibe 403). Forwarder YARP `/bff/{**catch-all}` → `http://oroclash-api/api/{**catch-all}` ya existe (017).

```
POST   /bff/rewards               → POST   /api/rewards
GET    /bff/rewards               → GET    /api/rewards?type=&status=&search=&onlyEligible=&page=&pageSize=
GET    /bff/rewards/{id}          → GET    /api/rewards/{id}
PUT    /bff/rewards/{id}          → PUT    /api/rewards/{id}            (If-Match: RowVersion)
POST   /bff/rewards/{id}/activate   → POST /api/rewards/{id}/activate   { rowVersion, idempotencyKey }
POST   /bff/rewards/{id}/deactivate → POST /api/rewards/{id}/deactivate { rowVersion, idempotencyKey }
POST   /bff/rewards/{id}/archive    → POST /api/rewards/{id}/archive    { rowVersion, idempotencyKey }
```

Auth: cookie de sesión; forwarder adjunta `Authorization: Bearer {access_token}` server-side. `X-Correlation-Id` propagado. `RowVersion` en `ETag`/`If-Match` + `X-Idempotency-Key`.

## 2. Create — POST /bff/rewards

**Request** `Content-Type: application/json`

```json
{
  "name": "Voucher Amazon 20€",
  "description": "Tarjeta regalo digital",
  "type": "Voucher",
  "cost": 100,
  "stock": 10,
  "availableFrom": "2026-09-01T00:00:00Z",
  "availableTo": "2026-12-31T23:59:59Z"
}
```

**Response 201 Created** `Location: /bff/rewards/{id}`

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Voucher Amazon 20€",
  "description": "Tarjeta regalo digital",
  "type": "Voucher",
  "cost": 100,
  "stock": 10,
  "availableFrom": "2026-09-01T00:00:00Z",
  "availableTo": "2026-12-31T23:59:59Z",
  "status": "Active",
  "isEligible": true,
  "rowVersion": "AAAAAAAAB9E=",
  "createdAt": "2026-08-28T12:00:00Z"
}
```

`isEligible` derivado: `Status==Active && (Stock==0?ilimitado:Stock>0) && (now ∈ [From,To] si se definen)`. `Consolation` solo elegible via regla de consolación, no como premio normal.

**Errores** `400`/`409` `ProblemDetails` con `FieldErrors`:

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "InvalidRewardData",
  "status": 400,
  "detail": "Reward type must be one of 6.",
  "errors": { "type": ["InvalidRewardType"] }
}
```

`409 RewardAlreadyExists` si nombre duplicado case-insensitive entre no archivados.

## 3. Update — PUT /bff/rewards/{id}

**Headers** `If-Match: W/"AAAAAAAAB9E="` + `X-Idempotency-Key: {uuid}`

**Request** idem Create.

**Response 200 OK** con `RewardResponse` actualizado y nuevo `rowVersion`.

**Errores**:
- `400 InvalidRewardData` con `errors.{field}` (nombre, costo 1–100000, stock ≥0, fechas `From<To`, tipo fuera de 6)
- `409 RewardAlreadyExists` si cambia nombre a duplicado
- `409 ConcurrencyConflict` si `RowVersion` desactualizado
- `403` si `GAME_MANAGER`
- `422 InvalidRewardState` si intenta editar tras `Archived`

## 4. Activate / Deactivate / Archive

```http
POST /bff/rewards/{id}/activate
If-Match: W/"AAAAAAAAB9E="
X-Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000
Content-Type: application/json

{ "rowVersion": "AAAAAAAAB9E=", "idempotencyKey": "550e8400-..." }
```

**200 OK** con nuevo `status` y `rowVersion`. `Active` es elegible si `isEligible` true; `Inactive` no es elegible.

**Errores**:
- `400 RewardUnavailable` si `Active` con tipo `Physical` y `Stock==0` y política lo considera sin stock
- `409 RewardInUse` si `Archive` con canjes `Approved` sin entregar (según política)
- `422 InvalidRewardState` si transición no permitida

## 5. Read — GET /bff/rewards

**Query** `type=Voucher`, `status=Active`, `search=Amazon`, `onlyEligible=true`, `page`, `pageSize`.

**Response 200**

```json
{
  "items": [ { "id":"...", "name":"Voucher Amazon 20€", "type":"Voucher", "cost":100, "stock":10, "status":"Active", "isEligible":true, "rowVersion":"AAAAAAAAB9E=" } ],
  "totalCount": 50,
  "page": 1,
  "pageSize": 20,
  "totalPages": 3
}
```

## 6. Read — GET /bff/rewards/{id}

**Response 200** `RewardResponse` con `history: [{from:"Active", to:"Inactive", timestamp:"...", actorId:"sub"}]` y `isEligible`.

## 7. Contrato cliente (C#)

Vive en `QuizArena.Admin.Client/Services/IRewardsService.cs` (existente, extender si falta):

```csharp
public interface IRewardsService
{
    Task<PagedResult<RewardSummary>> GetRewardsAsync(RewardFilter filter, CancellationToken ct = default);
    Task<RewardDetail> GetRewardAsync(Guid id, CancellationToken ct = default);
    Task<RewardDetail> CreateRewardAsync(CreateRewardRequest request, CancellationToken ct = default);
    Task<RewardDetail> UpdateRewardAsync(Guid id, UpdateRewardRequest request, CancellationToken ct = default);
    Task<RewardDetail> ActivateAsync(Guid id, string rowVersion, string idempotencyKey, CancellationToken ct = default);
    Task<RewardDetail> DeactivateAsync(Guid id, string rowVersion, string idempotencyKey, CancellationToken ct = default);
    Task<RewardDetail> ArchiveAsync(Guid id, string rowVersion, string idempotencyKey, CancellationToken ct = default);
}
```

Implementaciones:
- `ClientRewardsService` (WASM): `HttpClient.PostAsJsonAsync("/bff/rewards", req)` etc.
- `ServerRewardsService` (InteractiveServer): `HttpClient.PostAsJsonAsync("http://oroclash-api/api/rewards", req)` con `Bearer`.

## 8. Validación de contrato

- `AdminBffTests` — no URLs absolutas en cliente, solo `/bff/*`
- `DesignSystemNoDirectDbTests` — sin `DbContext` en Admin
- `RewardTests` — 7 campos validación + 6 tipos + `RewardAlreadyExists` + `rowversion`
