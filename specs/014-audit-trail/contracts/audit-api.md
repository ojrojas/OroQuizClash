# Contract: Audit API — SPEC-014 (extiende SPEC-013)

**Date**: 2026-08-28 | **Plan**: [../plan.md](../plan.md) | **Spec**: [../spec.md](../spec.md) | **Auth**: `Audit.Read` (ADMIN) / `Report.Read` (limitado, subconjunto)

Registro `AuditRecord`/`AuditEntry` append-only e immutable (FR-004/005). Solo lectura autorizada; sin Update/Delete. Extiende `specs/013-game-security/contracts/audit-api.md` con campos `ResourceId`/`GameId`/`PlayerId`/`Data` y catálogo cerrado de 16 `Action`.

## Endpoints

### GET /api/audit

Consulta paginada y filtrable. Requiere `Audit.Read` (o `Report.Read` para subconjunto según política).

**Query params (todos opcionales, combinables):**

| Param | Tipo | Descripción |
|-------|------|-------------|
| `correlationId` | string (uuid) | Traza del flujo (`X-Correlation-ID`) |
| `actorId` | string (guid) | `sub` del actor |
| `action` | string | Uno de 16: `GameCreated`, `GameConfigured`, `GameStarted`, `PlayerJoined`, `RoundStarted`, `QuestionPresented`, `AnswerSubmitted`, `AnswerEvaluated`, `PointsAwarded`, `PointsRemoved`, `PlayerWithdrawn`, `PlayerEliminated`, `GameFinished`, `RewardRedeemed`, `ConsolationGranted`, `AdministrativeAdjustment` |
| `resource` | string | Tipo: `Game`, `Round`, `Player`, `Question`, `Answer`, `Reward`, `Consolation` |
| `resourceId` | string (guid) | Id del recurso (`GameId`, `RoundId`, `AnswerId`, `RewardId`) |
| `gameId` | string (guid) | Filtra por `GameId` |
| `playerId` | string (guid) | Filtra por `PlayerId` |
| `result` | string | `Succeeded`/`Failed`/`Denied`/`RateLimited`/`ReplayDetected`/`Success` |
| `from` | date-time (UTC) | `Timestamp >= from` |
| `to` | date-time (UTC) | `Timestamp <= to` |
| `page` | int | default 1, min 1 |
| `pageSize` | int | default 20, max 100 |

**Headers:**

- `X-Correlation-ID` (opcional, se propaga; si no viene se genera por `TraceIdentifier`/`Activity`)

**Response 200 (orden cronológico `Timestamp` asc):**

```json
{
  "items": [
    {
      "id": "guid",
      "timestamp": "2026-08-28T10:00:00Z",
      "actor": "guid|system",
      "action": "GameCreated",
      "resource": "Game",
      "resourceId": "guid",
      "gameId": "guid|null",
      "playerId": "guid|null",
      "correlationId": "guid",
      "data": "{\"delta\":10,\"reason\":\"correct\"}",
      "result": "Succeeded"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "total": 100
}
```

Mapeo a `AuditEntry` (SPEC-013): `actor` → `ActorId`, `data` → `Details`/`Data`, `result` → `Result`, `resourceId` → `ResourceId` (nuevo), `gameId` → `GameId` (nuevo), `playerId` → `PlayerId` (nuevo).

**Errores:**

- 401 si no autenticado (sin JWT válido)
- 403 si sin `Audit.Read`/`Report.Read` (sin fuga de existencia)
- 400 si `page`/`pageSize` fuera de rango o `action` no ∈ catálogo 16

**Paginación:**

- `page`/`pageSize` con `total` en respuesta; no duplicados ni pérdidas entre páginas (SC-003).
- `total` es conteo del filtro; `pageSize` máximo 100 impuesto por API.

### GET /api/audit/{id}

Detalle de un `AuditRecord` por `Id`. Mismo `Audit.Read`.

**Response 200:** objeto `AuditRecord` como arriba.

**Errores:** 404 si `Id` no existe, 403 sin permiso.

## Seguridad y reglas

- **Append-only/immutable**: `POST`/`PUT`/`DELETE`/`PATCH` sobre `/api/audit` → 405 Method Not Allowed; ningún actor puede modificar/borrar (SC-002).
- **Solo lectura no genera audit**: `GET /api/audit` no crea un nuevo `AuditRecord` (SC-005).
- **Sanitización** (`Data`): nunca incluye `IsCorrect` previo a divulgación, tokens, secretos o PII innecesaria (FR-012, SPEC-012).
- **Timestamp** siempre UTC servidor; `CorrelationId` propagado vía `X-Correlation-ID`/`Activity` (traceable, FR-007).

## Correlación y trazabilidad (FR-007)

Toda operación de un mismo flujo (ej. `RoundStarted` → `QuestionPresented` → `AnswerSubmitted` → `AnswerEvaluated` → `PointsAwarded`) comparte el `CorrelationId` de la petición origen. `GET /api/audit?correlationId=...` retorna la secuencia completa ordenada por `Timestamp`.

## Compatibilidad con SPEC-013

Este contrato extiende `specs/013-game-security/contracts/audit-api.md` (que usaba `actorId`/`actorRoles`/`permission`/`resource`/`reason`/`details`). Los campos nuevos `resourceId`/`gameId`/`playerId`/`data` son aditivos y nullable para compatibilidad; los clientes que usan el contrato antiguo siguen funcionando.

