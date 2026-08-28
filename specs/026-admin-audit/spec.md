# Feature Specification: Admin Audit

**Feature Branch**: `026-admin-audit`

**Created**: 2026-05-13

**Status**: Draft

**Input**: User description: "026 — Admin Audit Objetivo Proporcionar trazabilidad de las operaciones administrativas. Descripción Deberá permitir consultar: Who What When Where Entity Previous Value New Value Action Result Debe integrarse con SPEC-014 Audit"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Consultar auditoría con filtros Who/What/When/Where/Entity/Action/Result (Priority: P1)

Como administrador (ADMIN) autenticado, quiero consultar el registro de auditoría paginado con filtros por Who (actor), What (qué se hizo), When (cuándo), Where (dónde), Entity, Action y Result, para auditar operaciones administrativas y resolver incidencias.

**Why this priority**: Es la base de 026 — sin consulta paginada y filtrable por los 9 campos no hay trazabilidad. Es el MVP de auditoría y desbloquea cumplimiento y soporte.

**Independent Test**: Login ADMIN → `/admin/audit` → verificar listado paginado de entradas con columnas Who/What/When/Where/Entity/Action/Result, con filtros Who (actor `sub`/email), What (descripción), When (Desde/Hasta), Where (origen/IP/endpoint), Entity (tipo e id), Action (CREATE/UPDATE/DELETE por ejemplo), Result (Success/Failed) aplicados de forma AND y paginación <2s.

**Acceptance Scenarios**:

1. **Given** un ADMIN en `/admin/audit`, **When** abre el listado sin filtros, **Then** el sistema muestra entradas paginadas (`page`/`pageSize`, `TotalCount`) ordenadas por `When` descendente, con Who (`sub` + DisplayName), What, When (UTC), Where, Entity, Action y Result, en <2s percibidos con skeleton.
2. **Given** el listado cargado, **When** filtra por Who “admin” + When últimos 7 días + Entity “Game” + Action “CREATE”, **Then** todas las entradas retornadas cumplen los filtros combinados AND, con `TotalCount` correcto y sin cargar colecciones completas.
3. **Given** un rango sin datos (futuro), **When** aplica filtro When, **Then** el sistema muestra estado `Empty` con mensaje “Sin registros para los filtros” sin error, y permite limpiar filtros.
4. **Given** un GAME_MANAGER autenticado, **When** abre `/admin/audit`, **Then** tiene acceso según matriz (ver FR-010); **Given** un usuario sin rol administrativo, **When** intenta acceder por URL directa o API, **Then** recibe 403/`Access Denied` sin fuga.

---

### User Story 2 - Ver detalle de auditoría con Previous Value / New Value y trazabilidad (Priority: P1)

Como auditor, quiero ver el detalle de una entrada con Previous Value y New Value, más correlación Who/When/Where/Entity/Action/Result, para reconstruir qué cambió y auditar integridad.

**Why this priority**: Co-prioritario con US1 — el listado sin detalle de valores anterior/nuevo no permite trazabilidad de cambios. Este slice aporta el valor forense.

**Independent Test**: Desde el listado → click en una entrada con Action `UPDATE` → verificar vista detalle con Who (actor, `sub`, email, tenant), What (descripción), When (timestamp UTC), Where (servicio/endpoint/IP/`CorrelationId`), Entity (tipo + `EntityId`), Previous Value (JSON), New Value (JSON), Action y Result (`Success`/`Failed` + `ErrorCode` si aplica), con diff visual y `CorrelationId` propagado.

**Acceptance Scenarios**:

1. **Given** una entrada `UPDATE` de `Category` (cambio de nombre), **When** el auditor abre el detalle, **Then** ve Previous Value `{ "Name": "Viejo" }` y New Value `{ "Name": "Nuevo" }` con diff resaltado, además de Who/When/Where/Entity/Action/Result coherentes.
2. **Given** una entrada `CREATE` (sin previo), **When** abre el detalle, **Then** ve Previous Value `null`/`—` y New Value con payload creado, con Action `CREATE` y Result `Success`.
3. **Given** una entrada con Result `Failed` (ej. `ConcurrencyConflict`), **When** abre el detalle, **Then** ve Result `Failed` con `ErrorCode` y `Detail`, y Previous/New Value según corresponda (sin fuga interna).
4. **Given** una entrada con `Where` que incluye `CorrelationId`/`TraceId`, **When** el auditor copia el `CorrelationId`, **Then** puede correlacionar con logs/traces de OTel (Constitución I) sin exponer datos sensibles.

---

### User Story 3 - Integrar con SPEC-014 Audit y filtros avanzados (Priority: P2)

Como administrador, quiero que Admin Audit consuma el trail append-only de SPEC-014 Audit (Outbox + `AuditEntry`) con garantías de no mutación, retención y auditoría de consultas, para tener trazabilidad end-to-end (juego + administración) y exportación bajo demanda.

**Why this priority**: Eleva la consulta de “ver auditoría” a “trazabilidad integrada y gobernada”. Depende de US1/US2 y es P2 porque el valor base ya se entregó, pero es crítico para cumplimiento (014) y para no duplicar infraestructura.

**Independent Test**: Verificar que `GET /bff/audit*` consume `oroclash-api /api/audit*` que a su vez lee `AuditEntry` append-only de 014 (mismo `SaveChanges` + Outbox), con paginación, filtros 9 campos, y que la UI muestra `Who`/`What` consistentes con `Game`/`Category`/`Question`/`Reward`/`Player` y que una consulta de auditoría queda auditada cuando la política lo exige.

**Acceptance Scenarios**:

1. **Given** una operación administrativa que generó `AuditEntry` en 014 (ej. `CreateCategory`, `UpdateReward`, `ApproveRedemption`), **When** el ADMIN consulta `/admin/audit?entity=Category&action=CREATE&from=...`, **Then** la entrada aparece con Who (actor `sub`), What (descripción de 014), When, Where (endpoint), Entity (`Category`), Previous/New Value, Action y Result idénticos a 014, sin duplicación ni re-escritura.
2. **Given** el trail 014 con retención y append-only, **When** se consulta desde Admin Audit, **Then** el sistema no muta historial (solo lectura) y `Previous Value`/`New Value` son inmutables y coinciden con el snapshot de 014.
3. **Given** una consulta de auditoría sensible (ej. filtro por Who `admin` + Entity `Reward`), **When** la política de auditoría de consultas está activa, **Then** se registra `AuditViewAudit` (actor, filtros, `CorrelationId`, timestamp) sin mutar el trail auditado, y no se expone `identitydb` directamente.
4. **Given** un REWARD_MANAGER consulta auditoría filtrada por Entity `Reward`/`Redemption`, **When** accede, **Then** ve solo esas entidades (según matriz FR-010); **Given** un GAME_MANAGER intenta ver `Reward` audit, **Then** recibe 403 sin fuga si no tiene permiso.

---

### Edge Cases

- ¿Qué ocurre si no hay registros para los filtros (ej. Who inexistente, Fecha futura)? Mostrar `Empty` con total 0, sin error 500, con opción de limpiar filtros.
- ¿Qué ocurre si `Previous Value` o `New Value` es muy grande (JSON >10KB) o contiene datos sensibles? Truncar visualmente con “Ver JSON completo” y enmascarar campos sensibles (ej. secretos), sin fuga en `ProblemDetails`.
- ¿Qué ocurre si OroIdentityServer no disponible o sesión expiró mientras se consulta? BFF retorna 401, UI muestra “Sesión expirada — re-autenticar” y preserva filtros (CorrelationId incluido).
- ¿Qué ocurre si dos operadores consultan el mismo rango con filtros distintos simultáneamente? Lecturas idempotentes, cada una con su `CorrelationId`; sin interferencia ni cache stale.
- ¿Qué ocurre si `When` tiene `Desde > Hasta` o `Result` fuera de catálogo (`Success`/`Failed`)? Validación por campo sin petición, mensaje accionable.
- ¿Qué ocurre si `Entity` es `Game` con `Previous Value` null para `CREATE`? Mostrar “—” para Previous y New Value con payload, sin error.
- ¿Qué ocurre si se intenta editar/borrar una entrada de auditoría? Rechazado — trail es append-only (Constitución I) — por API 403/404 y sin mutación.

## Requirements *(mandatory)*

### Functional Requirements

**Consulta — 9 campos**

- **FR-001**: El sistema MUST proporcionar consulta paginada de auditoría con los 9 campos: **Who** (actor `sub`/DisplayName/email/tenant), **What** (descripción de la operación), **When** (timestamp UTC), **Where** (origen: servicio/endpoint/IP/`CorrelationId`/`TraceId`), **Entity** (tipo agregado + `EntityId`), **Previous Value** (snapshot JSON previo o null), **New Value** (snapshot posterior), **Action** (verbo de la operación) y **Result** (`Success`/`Failed` + `ErrorCode`/`Detail` si aplica), ordenada por `When` descendente.
- **FR-002**: El sistema MUST permitir filtrar por **Who** (actor `sub` o búsqueda parcial por DisplayName/email, case-insensitive), **What** (búsqueda parcial por descripción), **When** (rango `Desde`/`Hasta` con `Desde <= Hasta`), **Where** (búsqueda parcial por servicio/endpoint/`CorrelationId`), **Entity** (tipo: `Game`/`Category`/`Question`/`GamePlayer`/`Reward`/`RewardRedemption`/`Player` etc. y/o `EntityId`), **Previous Value**/**New Value** (búsqueda parcial por JSON si se requiere), **Action** (catálogo cerrado por ejemplo `CREATE`/`UPDATE`/`DELETE`/`ACTIVATE`/`APPROVE`/`REJECT`/`DELIVER`/`START`/`FINISH` etc.) y **Result** (`Success`/`Failed` + filtros por `ErrorCode`), combinados como AND.
- **FR-003**: El sistema MUST mostrar detalle de una entrada con todos los 9 campos, con `Previous Value` y `New Value` como JSON diff (cuando ambos existen) y con `CorrelationId`/`TraceId` clicable para correlacionar con logs/traces OTel, sin fuga de datos sensibles.

**Integración con SPEC-014 Audit**

- **FR-004**: El sistema MUST integrarse con **SPEC-014 Audit** (trail append-only `AuditEntry` + Outbox `IOutboxWriter` en `AppDbContextBase.SaveChanges`): Admin Audit consume `oroclash-api /api/audit*` que lee el mismo `AuditEntry` (actor, timestamp, Entity, Action, Previous/New Value, Result, CorrelationId) sin duplicar ni re-escribir; no muta historial.
- **FR-005**: El sistema MUST garantizar **inmutabilidad** del trail: ninguna operación de Admin Audit puede crear/editar/borrar `AuditEntry` existente; cualquier intento retorna 403/404 sin mutación y es auditado como intento fallido si la política lo exige.
- **FR-006**: El sistema MUST propagar **auditoría de consultas** cuando la política lo requiera: registrar `AuditViewAudit` (actor `sub`, filtros aplicados, `PlayerId`/`EntityId` consultado si aplica, timestamp, `CorrelationId`) sin mutar el trail auditado, y sin escribir en `identitydb`.

**Paginación, autorización y presentación**

- **FR-007**: El sistema MUST paginar server-side todas las consultas (`page`/`pageSize`, default 20, max 100, con `TotalCount`/`TotalPages`) y no cargar colecciones completas; MUST mostrar estados `Loading` (skeleton), `Empty`, `Error` (retry) y `Ready` por lista, con `TotalCount` correcto.
- **FR-008**: El sistema MUST validar en tres niveles: API (contrato — tipos, rangos, paginación, `From<=To`, catálogos `Action`/`Result`/`Entity`), Aplicación (requisitos — coherencia de filtros combinados, existencia de Entity si se filtra por `EntityId`) y Dominio (invariantes — `AuditEntry` append-only, sin edición). Invariantes MUST NOT depender solo de UI.
- **FR-009**: El sistema MUST mapear `Result` → HTTP sin fuga: `Success` → 200 con datos; `Failed` → 200 con entrada Result `Failed` (no 500); errores de API → `ProblemDetails` RFC 7807 con `CorrelationId`; `Previous`/`New Value` truncados/enmascarados si contienen secretos.
- **FR-010**: El sistema MUST aplicar autorización por rol vía OroIdentityServer (OIDC `authorization_code` + `refresh_token`): `ADMIN` acceso completo a 9 campos y todas las entidades; `GAME_MANAGER` acceso a auditoría de `Game`/`Category`/`Question`/`GamePlayer` con filtros Who/What/When/Where/Entity/Action/Result; `REWARD_MANAGER` acceso a `Reward`/`RewardRedemption`; cualquier rol no autorizado (incl. `PLAYER`) recibe `403 Forbidden` por API y `Access Denied` en UI sin fuga. `must_change_password` gating antes de consultar.
- **FR-011**: El sistema MUST consumir exclusivamente la API/BFF (`QuizArena.Api` vía `QuizArena.Admin` BFF YARP) para todos los datos de auditoría; MUST NOT acceder directamente a SQL Server/Oracle/`identitydb`; MUST propagar `CorrelationId` y `TraceId` (OTel) y usar `BuildingBlocks.ServiceDefaults`.
- **FR-012**: El sistema MUST reutilizar shell de navegación, tema `administration`, tokens y componentes del Design System (SPEC-016) sin valores hardcodeados, residir en `src/Admin/QuizArena.Admin` (Blazor Auto `net10.0`) y `src/Admin/QuizArena.Admin.Client`, y MUST exigir sesión válida vía OroIdentityServer.

### Key Entities *(include if feature involves data)*

- **AuditEntry (014)**: Registro append-only. Atributos: `AuditId` (Guid), `Who` (Actor `sub`, DisplayName, Email, Tenant), `What` (descripción), `When` (timestamp UTC), `Where` (servicio/endpoint/IP/`CorrelationId`/`TraceId`), `Entity` (tipo: `Game`/`Category`/`Question`/`Reward`/`Player` etc. + `EntityId`), `PreviousValue` (JSON o null), `NewValue` (JSON), `Action` (`CREATE`/`UPDATE`/`DELETE`/`ACTIVATE`/`APPROVE`/`REJECT`/`DELIVER`/`START`/`FINISH` etc.), `Result` (`Success`/`Failed`, `ErrorCode`, `Detail`). Inmutable.
- **AuditFilter**: Filtros combinados `Who` (sub/búsqueda), `What` (búsqueda), `When` (`Desde`/`Hasta`), `Where` (búsqueda), `Entity` (tipo + `EntityId`), `Action` (catálogo cerrado), `Result` (`Success`/`Failed`), `Page`/`PageSize`.
- **AuditDetail**: Vista detalle de una entrada: los 9 campos + `PreviousValue`/`NewValue` diff + `CorrelationId`/`TraceId` para OTel.
- **AuditViewAudit** (opcional): Registro de consulta de auditoría (actor, filtros, timestamp, `CorrelationId`) cuando la política lo exige; no muta `AuditEntry`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Un ADMIN abre `/admin/audit` y ve listado paginado con los 9 campos (Who/What/When/Where/Entity/Previous/New/Action/Result) en <2s percibidos con skeleton en el 90% de los intentos.
- **SC-002**: El 100% de las consultas con filtros combinados (Who + When + Entity + Action + Result) retornan solo entradas que cumplen todos los filtros AND, con `TotalCount` correcto y sin cargar colecciones completas.
- **SC-003**: El 100% de los detalles con Action `UPDATE` muestran `Previous Value` y `New Value` con diff correcto y `CorrelationId` propagado para correlación con logs/traces.
- **SC-004**: El 100% de las entradas creadas por 014 (ej. `CreateCategory`, `ApproveRedemption`) aparecen idénticas en Admin Audit (mismo Who/What/When/Where/Entity/Previous/New/Action/Result) sin duplicación ni re-escritura.
- **SC-005**: El 100% de los filtros con `Desde > Hasta` o `Action`/`Result`/`Entity` fuera de catálogo son rechazados por validación por campo sin petición, con mensaje accionable.
- **SC-006**: Un auditor completa la tarea “filtrar por Who `admin` + Entity `Category` + Action `CREATE` → abrir detalle → verificar Previous/New Value → copiar CorrelationId → correlacionar en logs” en menos de 2 minutos sin ayuda externa en el 95% de los intentos.
- **SC-007**: La autorización se respeta en el 100% de los casos: `GAME_MANAGER` ve auditoría de `Game`/`Category`, `REWARD_MANAGER` ve `Reward`/`Redemption`, `PLAYER` recibe `Access Denied`/`403` sin fuga.
- **SC-008**: El 100% de los errores se presentan como `ProblemDetails` RFC 7807 sin fuga, con `CorrelationId` propagado y estados `Loading`/`Empty`/`Error` por lista, y rango sin datos muestra `Empty` sin duplicados.
- **SC-009**: La UI de auditoría cumple WCAG 2.2 AA en tema `administration` (contraste, foco visible, teclado, `aria-live`) y es utilizable entre 375 y 1536px sin scroll horizontal y con objetivos ≥44px, con tokens del Design System sin literales.

## Assumptions

- **Reutiliza SPEC-014 y 017/016**: La app Blazor `net10.0` Auto, shell, BFF YARP, OIDC y trail `AuditEntry` append-only + Outbox (`014-audit-trail`) ya existen. 026 es solo lectura de auditoría, sin crear nueva app ni duplicar agregados ni re-escribir trail.
- **Solo lectura en v1**: Admin Audit es consulta y diff; no edita/borra entradas; no exporta a CSV/PDF en v1 (si se necesita, será extensión con `Accept: text/csv`).
- **Fuente de verdad**: `AuditEntry` en `oroclash-api` (SQL Server primario, abstracción Oracle, `rowversion` donde aplica) via `GET /api/audit*` con `PagedResult` y `CorrelationId`/`TraceId` OTel; Admin nunca toca DB ni `identitydb` directamente.
- **Catálogos cerrados**: `Action` (ej. `CREATE`/`UPDATE`/`DELETE`/`ACTIVATE`/`DEACTIVATE`/`ARCHIVE`/`APPROVE`/`REJECT`/`DELIVER`/`CANCEL`/`START`/`FINISH`/`JOIN`/`WITHDRAW`) y `Result` (`Success`/`Failed`) y `Entity` (`Game`/`Category`/`Question`/`GamePlayer`/`Reward`/`RewardRedemption`/`Player`) son invariantes; valores fuera → 400.
- **Filtros combinados AND**: Los 9 campos se aplican como AND server-side vía `Specification` (`Where` + `And`); paginación `page` 1..N, `pageSize` 1..100, default 20; búsquedas parciales case-insensitive para Who/What/Where/Entity.
- **Previous/New Value**: JSON snapshot inmutable; `CREATE` → Previous null, New con payload; `UPDATE` → ambos con diff; `DELETE` → Previous con valores, New null; truncado/enmascarado si >10KB o con secretos.
- **Matriz de permisos v1**: `ADMIN` → 9 campos + todas las entidades; `GAME_MANAGER` → `Game`/`Category`/`Question`/`GamePlayer`; `REWARD_MANAGER` → `Reward`/`RewardRedemption`; `PLAYER` → 403 en `/admin/audit`. Si la política final difiere, se ajusta en Plan sin cambiar scope.
- **Idioma**: Español para etiquetas, coherente con SPEC-017/014, sin i18n en v1.
- **Sin acceso directo a datos**: Todo vía BFF; no lectura directa a SQL Server, Oracle ni `identitydb` (Constitución H).
