# Contract: Audit API — SPEC-013

**Date**: 2026-08-28 | **Plan**: [../plan.md](../plan.md) | **Spec**: [../spec.md](../spec.md) | **Auth**: `Audit.Read` (ADMIN) / `Report.Read` (limitado)

Registro append-only (FR-016/017). Solo lectura autorizada; sin Update/Delete.

## Endpoints

### GET /api/audit

Consulta paginada de `AuditEntry`. Requiere `Audit.Read` (o `Report.Read` para subconjunto).

**Query params:**

| Param | Tipo | Requerido | Descripción |
|-------|------|-----------|-------------|
| `correlationId` | string (uuid) | no | Filtra por traza/partida/jugador |
| `actorId` | string (guid) | no | Filtra por `sub` |
| `action` | string | no | `SubmitAnswer`, `CreateGame`, `RedeemReward`, etc. |
| `resource` | string | no | Prefijo `Game:guid` / `Round:guid` |
| `result` | string | no | `Success`/`Denied`/`ValidationFailed`/`RateLimited`/`ReplayDetected` |
| `from` | date-time | no | `Timestamp >= from` |
| `to` | date-time | no | `Timestamp <= to` |
| `page` | int | no | default 1 |
| `pageSize` | int | no | default 20, max 100 |

**Headers:**

- `X-Correlation-ID` (opcional, se propaga si no viene se genera)

**Response 200:**

```json
{
  "items": [
    {
      "id": "guid",
      "timestamp": "2026-08-28T10:00:00Z",
      "actorId": "guid",
      "actorRoles": "PLAYER",
      "action": "SubmitAnswer",
      "permission": "Game.Play",
      "resource": "Game:guid",
      "correlationId": "guid",
      "tenantId": "string|null",
      "result": "Success|Denied|ValidationFailed|RateLimited|ReplayDetected",
      "reason": "string|null",
      "details": "string|null"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "total": 100
}
```

**Errores:**

- 401 si no autenticado
- 403 si sin `Audit.Read`/`Report.Read`
- 400 si `page`/`pageSize` inválidos

### GET /api/audit/{id}

Detalle de una entrada. Mismo auth.

## Seguridad

- Nunca retorna `Authorization` header, tokens, secretos.
- Mensajes de 403 no revelan si el recurso auditado existe fuera de alcance.
- `AuditEntry` inmutable: `POST/PUT/DELETE /api/audit` no existen (405 si se intentan).

## Correlación (FR-018)

Toda operación genera/propaga `X-Correlation-ID` (via `Activity.Current` / `BuildingBlocks.ServiceDefaults`). `GET /api/audit?correlationId=...` recupera secuencia ordenada por `Timestamp` asc.

