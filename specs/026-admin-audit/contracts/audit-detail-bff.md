# Contract: Audit Detail BFF

**Branch**: `026-admin-audit` | **Date**: 2026-05-13

Contrato de detalle de auditoría con `Previous Value`/`New Value` diff y `CorrelationId`. Complementa `audit-bff.md`.

## 1. Endpoint BFF

```
GET    /bff/audit/{id}                  → GET    /api/audit/{id}
```

Auth: cookie; `RequireAuthorization` con matriz 7 entidades; `X-Correlation-Id` propagado. Solo `GET`.

## 2. Detail — GET /bff/audit/{id}

**Response 200** (ver `audit-bff.md` §3).

```json
{
  "auditId": "b2...",
  "who": { "actorId": "sub-456", "displayName": "GameManager" },
  "what": "UpdateReward",
  "when": "2026-05-13T10:05:00Z",
  "where": { "service": "oroclash-api", "endpoint": "PUT /api/rewards/{id}", "correlationId": "00-def456-02", "traceId": "def456" },
  "entity": { "entityType": "Reward", "entityId": "r1..." },
  "previousValue": "{ \"Cost\": 100 }",
  "newValue": "{ \"Cost\": 200 }",
  "action": "UPDATE",
  "result": { "status": "Success", "errorCode": null, "detail": null },
  "diff": [{ "path": "$.Cost", "previous": "100", "new": "200", "changeType": "Modified" }]
}
```

**Casos**:
- `CREATE` → `previousValue: null`, `newValue: { "Name": "..." }`
- `DELETE` → `previousValue: { "Name": "..." }`, `newValue: null`
- `UPDATE` `Failed` con `ConcurrencyConflict` → `result: { "status": "Failed", "errorCode": "ConcurrencyConflict", "detail": "RowVersion mismatch" }`, con `Previous`/`New` según corresponda.
- `Where` con `CorrelationId` clicable → copia para Jaeger/Seq.
- JSON >10KB → `isTruncated:true` con botón “Ver JSON completo” y enmascarado de `password`/`secret` → `***`.

**Errores**:
- `404 AuditEntryNotFound` si `id` no existe.
- `403` si `REWARD_MANAGER` intenta ver `Game` audit.
- `401` si sesión expirada.

## 3. Contrato cliente (C#)

```csharp
Task<AuditDetail> GetAuditDetailAsync(Guid auditId, CancellationToken ct = default);
```

Ver `audit-bff.md` §6 para `IAuditService` completo. `AuditDetail` extiende `AuditEntry` con `Diff` (ver `data-model.md`).

## 4. Validación de contrato

- `AuditDetailTests` — `CREATE` Previous null, `UPDATE` diff, `CorrelationId` propagado, 403 por rol
