# Contract: Audit BFF

**Branch**: `026-admin-audit` | **Date**: 2026-05-13

Contrato de consulta de auditoría (9 campos). El cliente WASM nunca toca el API directo (BFF obligatorio — Constitución H, FR-011).

## 1. Endpoints BFF

Todos `RequireAuthorization` con políticas (`ADMIN` todo; `GAME_MANAGER` `Game`/`Category`/`Question`/`GamePlayer`; `REWARD_MANAGER` `Reward`/`RewardRedemption`). Forwarder YARP `/bff/{**catch-all}` → `http://oroclash-api/api/{**catch-all}` ya existe (017). `PLAYER` → 403.

```
GET    /bff/audit                       → GET    /api/audit?who=&what=&whenFrom=&whenTo=&where=&entityType=&entityId=&action=&result=&errorCode=&page=&pageSize=
GET    /bff/audit/{id}                  → GET    /api/audit/{id}
```

Auth: cookie de sesión; forwarder adjunta `Authorization: Bearer {access_token}` server-side. `X-Correlation-Id`/`X-Trace-Id` propagado. Solo `GET` (solo lectura, FR-005).

## 2. List — GET /bff/audit

**Query** `who` (sub o DisplayName/email parcial, case-insensitive), `what` (descripción parcial), `whenFrom`/`whenTo` (ISO 8601, `whenFrom<=whenTo`), `where` (servicio/endpoint/`CorrelationId` parcial), `entityType` (7 tipos), `entityId` (Guid), `action` (catálogo cerrado), `result` (`Success`/`Failed`), `errorCode`, `page`, `pageSize` (default 20, max 100).

**Response 200**

```json
{
  "items": [
    {
      "auditId": "a1b2c3d4-....",
      "who": { "actorId": "sub-123", "displayName": "Admin", "email": "admin@example.com", "tenantId": "tenant-1" },
      "what": "CreateCategory",
      "when": "2026-05-13T10:00:00Z",
      "where": { "service": "oroclash-api", "endpoint": "POST /api/categories", "ipAddress": "10.0.0.1", "correlationId": "00-abc123-01", "traceId": "abc123" },
      "entity": { "entityType": "Category", "entityId": "c1..." },
      "previousValue": null,
      "newValue": "{ \"Name\": \"Historia\", \"KnowledgeArea\": \"Historia\" }",
      "action": "CREATE",
      "result": { "status": "Success", "errorCode": null, "detail": null }
    },
    {
      "auditId": "b2...",
      "who": { "actorId": "sub-456", "displayName": "GameManager", "email": "gm@example.com" },
      "what": "UpdateReward",
      "when": "2026-05-13T10:05:00Z",
      "where": { "service": "oroclash-api", "endpoint": "PUT /api/rewards/{id}", "correlationId": "00-def456-02" },
      "entity": { "entityType": "Reward", "entityId": "r1..." },
      "previousValue": "{ \"Name\": \"Viejo\", \"Cost\": 100 }",
      "newValue": "{ \"Name\": \"Nuevo\", \"Cost\": 200 }",
      "action": "UPDATE",
      "result": { "status": "Failed", "errorCode": "ConcurrencyConflict", "detail": "RowVersion mismatch" }
    }
  ],
  "totalCount": 542,
  "page": 1,
  "pageSize": 20,
  "totalPages": 28
}
```

Orden por `when` descendente. `previousValue` `null` para `CREATE`, `newValue` `null` para `DELETE`. Truncado si >10KB con `isTruncated:true` y enmascarado de secretos.

**Errores** `400 InvalidFilter` con `errors.whenFrom`/`errors.action`/`errors.entityType` si `whenFrom>whenTo` o fuera de catálogo.

## 3. Detail — GET /bff/audit/{id}

**Response 200**

```json
{
  "auditId": "b2...",
  "who": { "actorId": "sub-456", "displayName": "GameManager" },
  "what": "UpdateCategory",
  "when": "2026-05-13T10:05:00Z",
  "where": { "service": "oroclash-api", "endpoint": "PUT /api/categories/{id}", "correlationId": "00-def456-02", "traceId": "def456" },
  "entity": { "entityType": "Category", "entityId": "c1..." },
  "previousValue": "{ \"Name\": \"Viejo\" }",
  "newValue": "{ \"Name\": \"Nuevo\" }",
  "action": "UPDATE",
  "result": { "status": "Success", "errorCode": null, "detail": null },
  "diff": [
    { "path": "$.Name", "previous": "Viejo", "new": "Nuevo", "changeType": "Modified" }
  ]
}
```

`diff` calculado server-side o cliente a partir de `previousValue`/`newValue`. `CorrelationId` clicable para Jaeger/Seq.

**Errores**:
- `404 AuditEntryNotFound` si `id` no existe.
- `403` si `GAME_MANAGER` intenta ver `Reward` audit fuera de su matriz.
- `401` si sesión expirada.

## 4. Filtros combinados — AND

Los 9 filtros se aplican como `AND` server-side vía `Specification` (`Where` + `And`):

```
GET /bff/audit?who=admin&whenFrom=2026-05-06T00:00:00Z&whenTo=2026-05-13T23:59:59Z&entityType=Category&action=CREATE&result=Success&page=1&pageSize=20
→ 200 con solo entradas que cumplen todos los filtros
```

## 5. Paginación

`page` 1..N, `pageSize` 1..100, default 20, `TotalCount`/`TotalPages` correctos sin cargar colecciones.

## 6. Contrato cliente (C#)

Vive en `QuizArena.Admin.Client/Services/IAuditService.cs` (existente, extender):

```csharp
public interface IAuditService
{
    Task<PagedResult<AuditEntry>> GetAuditAsync(AuditFilter filter, CancellationToken ct = default);
    Task<AuditDetail> GetAuditDetailAsync(Guid auditId, CancellationToken ct = default);
}
```

`AuditFilter` con 9 campos + `Validate()` (`WhenFrom<=WhenTo`, catálogos).

Implementaciones:
- `ClientAuditService` (WASM): `HttpClient.GetFromJsonAsync("/bff/audit?who=&...")` etc.
- `ServerAuditService` (InteractiveServer): `HttpClient.GetFromJsonAsync("http://oroclash-api/api/audit?who=&...")` con `Bearer`.

## 7. Validación de contrato

- `AdminBffTests` — no URLs absolutas en cliente, solo `/bff/*`
- `DesignSystemNoDirectDbTests` — sin `DbContext` en Admin
- `AuditListTests` — 9 campos, filtros combinados AND, `WhenFrom<=WhenTo`, paginación, 403 por rol
- `AuditDetailTests` — `Previous`/`New` diff, `CorrelationId` propagado, `CREATE` Previous null
