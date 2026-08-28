# Research: Admin Audit

**Branch**: `026-admin-audit` | **Date**: 2026-05-13 | **Plan**: [plan.md](plan.md)

Todos los NEEDS CLARIFICATION resueltos. El feature reutiliza trail `AuditEntry` append-only + Outbox de `014-audit-trail` y patrón BFF/OIDC/Design System de 017–025; esta fase cierra las incógnitas propias de 026.

---

## R1. 9 campos de auditoría — Who/What/When/Where/Entity/Previous/New/Action/Result

**Decision**: 9 campos como proyección de `AuditEntry` de 014, sin nuevo agregado.

- **Who**: `sub` + `DisplayName`/`Email`/`TenantId` (claims OroIdentityServer) + `ActorId`.
- **What**: descripción textual generada en `Application` (ej. `CreateCategory`, `ApproveRedemption`).
- **When**: `Timestamp` UTC (`DateTimeOffset`).
- **Where**: servicio/endpoint/IP + `CorrelationId`/`TraceId` (OTel) propagado desde `HttpContext`.
- **Entity**: tipo agregado (`Game`, `Category`, `Question`, `GamePlayer`, `Reward`, `RewardRedemption`, `Player`) + `EntityId` (Guid).
- **Previous Value**: JSON snapshot previo o `null` para `CREATE`.
- **New Value**: JSON snapshot posterior (o `null` para `DELETE`).
- **Action**: catálogo cerrado (`CREATE`/`UPDATE`/`DELETE`/`ACTIVATE`/`DEACTIVATE`/`ARCHIVE`/`APPROVE`/`REJECT`/`DELIVER`/`CANCEL`/`START`/`FINISH` etc.).
- **Result**: `Success`/`Failed` + `ErrorCode`/`Detail` si `Failed`.

UI: `AuditTable.razor` con 9 columnas (truncado `Previous`/`New` con “Ver JSON”), `AuditDetail.razor` con diff.

**Rationale**: 014 ya define `AuditEntry` con esos 9 campos y `Previous`/`New` inmutables; Admin Audit solo proyecta.

**Alternatives considered**:
- 5 campos sin Previous/New: rechazado — pierde trazabilidad forense.
- Nuevo agregado `AdminAuditEntry` separado: rechazado — duplicación y desincronización.

---

## R2. Previous Value / New Value diff y CorrelationId

**Decision**:
- **Diff**: `AuditDetail.razor` muestra `Previous Value` y `New Value` como JSON formateado con diff visual (verde/rojo) cuando ambos existen; `CREATE` → Previous `—`, `UPDATE` → ambos, `DELETE` → New `—`; truncado a 10KB con botón “Ver JSON completo” y enmascarado de secretos (`password`, `secret`).
- **CorrelationId**: `Where` incluye `CorrelationId`/`TraceId` clicable que copia al portapapeles y se puede pegar en logs/traces OTel (`/health` + Seq/Jaeger).

**Rationale**: SC-003 exige diff correcto y SC-006 copiar `CorrelationId`; enmascarado evita fuga en `ProblemDetails`.

**Alternatives considered**:
- Solo texto plano sin diff: rechazado — no visualiza cambios.
- Sin `CorrelationId`: rechazado — no correlaciona con observabilidad (I).

---

## R3. Integración con SPEC-014 Audit — append-only + Outbox

**Decision**: Reutilizar `AuditEntry` de 014 sin duplicar.

- **Trails**: `oroclash-api` `AppDbContextBase.SaveChanges` persiste `AuditEntry` + `Outbox` en misma transacción (014); Admin Audit consume `GET /api/audit` que lee `AuditEntry` con `Specification` (`Where` + `And` + `Pagination` + `ApplyAsNoTracking`).
- **Inmutabilidad**: `AuditEntry` sin `Update`/`Delete` en `Application`; Admin Audit solo `GET` (solo lectura, FR-005). Intento de mutación → `403`/`404` sin mutación.
- **Retención**: misma política que 014 (append-only, sin borrado); Admin Audit no implementa purga.
- **Auditoría de consultas**: opcional `AuditViewAudit` (actor, filtros, `CorrelationId`, timestamp) registrado en `SaveChanges` si política lo exige, sin mutar trail.

**Rationale**: Constitución I exige append-only y Outbox; reusar 014 evita duplicar infraestructura y garantiza que `CreateCategory` en 014 aparece idéntico en Admin Audit (SC-004).

**Alternatives considered**:
- Nuevo trail `AdminAuditEntry` separado: rechazado — duplicación y divergencia.
- Lectura directa a `AuditEntry` DB desde Admin: rechazado — viola H (no acceso directo a SQL).

---

## R4. Paginación server-side y filtros combinados (9 campos, AND)

**Decision**:
- **Paginación**: `PagedResult<AuditEntry>` (`Items`, `TotalCount`, `Page`, `PageSize`, `TotalPages`) con `Specification` server-side (`Skip`/`Take` + `Count`); `Page` 1..N, `PageSize` 1..100, default 20; `TotalCount` correcto sin cargar colecciones.
- **Filtros**: 9 campos combinados AND: `Who` (sub/búsqueda parcial DisplayName/email), `What` (búsqueda parcial descripción), `When` (`From`/`To` con `From<=To`), `Where` (búsqueda parcial servicio/endpoint/`CorrelationId`), `Entity` (tipo + `EntityId`), `Action` (catálogo cerrado), `Result` (`Success`/`Failed` + `ErrorCode`). Búsquedas case-insensitive, parcial.
- **Validación**: `From>To` → `400 InvalidFilter` con `errors.DateRange` sin petición; `Action`/`Result`/`Entity` fuera de catálogo → `400` con `errors.action`.

**Rationale**: SC-002 exige filtros combinados AND paginados <2s sin cargar colecciones; `Specification` ya optimiza queries en 014.

**Alternatives considered**:
- Filtros OR: rechazado — semantics ambiguo.
- Paginación cliente: rechazado — no escala con ≥10k entradas.

---

## R5. BFF, autorización por rol y auditoría de consultas

**Decision**:
- **BFF**: `ClientAuditService` → `HttpClient.BaseAddress = HostEnvironment.BaseAddress` → rutas `/bff/audit` y `/bff/audit/{id}` (cookie viaja); `ServerAuditService` → `http://oroclash-api/api/audit*` con `Bearer` del `HttpContext`. Forwarder catch-all `/bff/{**catch-all}` → `/api/{**catch-all}` ya existe (017) y cubre `GET /bff/audit?who=&what=&whenFrom=&whenTo=&where=&entityType=&entityId=&action=&result=&page=`, `GET /bff/audit/{id}`.
- **Autorización**: `ADMIN` → 9 campos + todas las entidades; `GAME_MANAGER` → `Game`/`Category`/`Question`/`GamePlayer`; `REWARD_MANAGER` → `Reward`/`RewardRedemption`; `PLAYER` → 403. `must_change_password` gating (VI).
- **Auditoría de consultas**: si política activa, `AuditViewAudit` con `ActorId`/`Filters`/`CorrelationId` en `SaveChanges`.

**Rationale**: Reutiliza patrón BFF de 017 (sample `BlazorWebAppOidcBffAutoYarpAspire`), evita exponer `access_token` y preserva `CorrelationId`.

**Alternatives considered**:
- WASM → API directo: rechazado — expone JWT.
- Sin matriz por rol: rechazado — viola H.

---

## R6. Observabilidad — CorrelationId/TraceId y ProblemDetails

**Decision**:
- **CorrelationId/TraceId**: `Where` muestra `CorrelationId` clicable; `ServerAuditService` propaga `X-Correlation-Id`/`X-Trace-Id` desde `HttpContext.TraceIdentifier` y OTel `Activity.Current.Id`; UI lo copia para Jaeger/Seq.
- **ProblemDetails**: `ApiResponseExtensions` mapea `RFC 7807` (`type`, `title`, `status`, `detail`, `errors.{field}`, `traceId`) sin fuga; `Previous`/`New` truncados si >10KB.
- **OTel**: `BuildingBlocks.ServiceDefaults` (`AddServiceDefaults()` con OTLP, `/health`, resilience) ya en 017.

**Rationale**: Constitución I exige `CorrelationId`/`TraceId` en logs; SC-008 exige `ProblemDetails` sin fuga y `Loading`/`Empty`/`Error` por lista.

**Alternatives considered**:
- Sin `CorrelationId`: rechazado — no correlaciona.
- Logs sin OTel: rechazado — viola I.

---

## Consolidated Decisions Summary

| # | Decisión | Fuente |
|---|----------|--------|
| 1 | 9 campos `Who`/`What`/`When`/`Where`/`Entity`/`Previous`/`New`/`Action`/`Result` como proyección de 014 | FR-001 |
| 2 | `Previous`/`New` diff + `CorrelationId` clicable, enmascarado | FR-003, SC-003/006 |
| 3 | Reutilizar `AuditEntry` append-only + Outbox de 014, solo `GET`, auditoría de consultas opcional | FR-004..006, I |
| 4 | `PagedResult` + 9 filtros AND con `From<=To` y catálogos cerrados | FR-002/007, SC-002 |
| 5 | BFF catch-all + matriz 3 roles (`ADMIN` todo, `GAME_MANAGER` juego, `REWARD_MANAGER` premios) | FR-010, H/VI |
| 6 | `CorrelationId`/`TraceId` OTel + `ProblemDetails` RFC7807 | FR-009, I |

Sin NEEDS CLARIFICATION pendientes. Listo para Phase 1.
