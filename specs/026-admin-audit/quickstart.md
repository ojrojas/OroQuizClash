# Quickstart: Admin Audit — Validation Guide

**Branch**: `026-admin-audit` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)
**Data Model**: [data-model.md](data-model.md) | **Contracts**: [contracts/audit-bff.md](contracts/audit-bff.md), [contracts/audit-detail-bff.md](contracts/audit-detail-bff.md)

Guía runnable para validar trazabilidad (9 campos: Who, What, When, Where, Entity, Previous Value, New Value, Action, Result) con integración SPEC-014 Audit trail append-only. Cada escenario es independiente y referencia contrato/modelo.

## Prerequisites

- `net10.0` SDK 10.0.400 (`global.json` 10.0.400)
- `OroQuizClash.AppHost` corriendo: `dotnet run --project OroQuizClash.AppHost` (levanta `oroclash-api`, `identity-api` Postgres 5432, `quizarena-admin`)
- Cliente OIDC `quizarena-admin` registrado en OroIdentityServer (confidential, `authorization_code+refresh_token`, redirect `/signin-oidc`, scopes `openid profile email offline_access {ApiScope}`) — ver `017/contracts/oidc-config.md`
- Usuarios: `admin/Admin@123456` (ADMIN), `game_manager` (GAME_MANAGER) y `reward_manager` (REWARD_MANAGER) con roles `ADMIN`, `GAME_MANAGER`, `REWARD_MANAGER`
- Datos: operaciones que generen `AuditEntry` en 014 (ej. `CreateCategory`, `UpdateReward`, `ApproveRedemption`, `StartGame`) con `Previous`/`New` y `CorrelationId`; al menos 1 entrada `UPDATE` con diff y 1 `CREATE` sin Previous
- Design tokens en `src/Admin/QuizArena.Admin/wwwroot/design-tokens.css` (gate `validate-tokens`)

## Setup

```bash
dotnet restore
dotnet build
dotnet run --project OroQuizClash.AppHost
# Esperar Aspire dashboard https://localhost:15888 → recursos healthy
# Admin URL → quizarena-admin (ver Aspire)
node .opencode/skills/design-system/scripts/validate-tokens.cjs --dir src/Admin --strict
```

## Validation Scenarios

### V1 — Consultar auditoría con 9 filtros (US1, FR-001/002/007)

**Referencia**: `spec.md US1`, `data-model.md AuditFilter`, `contracts/audit-bff.md`.

1. Login ADMIN → `/admin/audit` → verificar listado paginado con 9 columnas Who/What/When/Where/Entity/Previous/New/Action/Result, ordenado por `When` descendente, con skeleton <2s.
2. Sin filtros → verificar `GET /bff/audit?page=1&pageSize=20` retorna `items` con 9 campos y `totalCount`/`totalPages`.
3. Aplicar filtros combinados: Who “admin” + When últimos 7 días (`whenFrom=2026-05-06`/`whenTo=2026-05-13`) + Entity “Game” + Action “CREATE” → verificar `GET /bff/audit?who=admin&whenFrom=...&whenTo=...&entityType=Game&action=CREATE&page=1` solo entradas que cumplen AND con `TotalCount` correcto.
4. Probar rango sin datos (futuro `whenFrom=2099-01-01`) → verificar `Empty` con “Sin registros para los filtros” sin error, y botón limpiar filtros.
5. Login GAME_MANAGER → repetir 1-2 → verificar acceso según matriz (ver V4); no-auth → `401`; `PLAYER` → `403`.

**Expected**: SC-001 <2s (90%), SC-002 100% filtros AND paginados, SC-005 validación sin petición.

**API check**:
```bash
curl -k "https://localhost:XXXX/bff/audit?who=admin&whenFrom=2026-05-06T00:00:00Z&whenTo=2026-05-13T23:59:59Z&entityType=Category&action=CREATE&page=1&pageSize=20" -H "Cookie: .AspNetCore.Cookies=..." | jq
```

### V2 — Detalle con Previous/New Value y CorrelationId (US2, FR-003)

**Referencia**: `contracts/audit-detail-bff.md`.

1. Desde listado → click en entrada `UPDATE` de `Category` (cambio de nombre) → verificar `GET /bff/audit/{id}` con 9 campos + `previousValue: { "Name": "Viejo" }` y `newValue: { "Name": "Nuevo" }` con diff visual (verde/rojo) y `Where.correlationId` clicable.
2. Abrir entrada `CREATE` → verificar `previousValue: null`/`—` y `newValue` con payload, `Action: CREATE`, `Result: Success`.
3. Abrir entrada con Result `Failed` (`ConcurrencyConflict`) → verificar `result.status: Failed` con `errorCode` y `detail`, y `Previous`/`New` según corresponda, sin fuga.
4. Copiar `Where.correlationId` → verificar que se puede pegar en logs/traces OTel (`/health` + Jaeger/Seq) y correlaciona.

**Expected**: SC-003 100% diff correcto y `CorrelationId` propagado.

**API check**:
```bash
curl -k https://localhost:XXXX/bff/audit/{id} -H "Cookie: ..." | jq
```

### V3 — Integración con SPEC-014 Audit (US3, FR-004..006)

1. Realizar operación que genere `AuditEntry` en 014 (ej. crear categoría `POST /bff/categories` o aprobar canje) → verificar que inmediatamente `GET /bff/audit?entityType=Category&action=CREATE&from=...` muestra entrada con Who (`sub` del actor), What, When, Where (endpoint), Entity (`Category`), Previous/New, Action, Result idénticos a 014, sin duplicación.
2. Verificar inmutabilidad: intentar `PUT /bff/audit/{id}` o `DELETE /bff/audit/{id}` → `405`/`403` sin mutación; `GET /bff/audit/{id}` sigue igual.
3. Con política de auditoría de consultas activa, filtrar por Who `admin` + Entity `Reward` → verificar que se registra `AuditViewAudit` (actor, filtros, `CorrelationId`) sin mutar trail (ver logs `AuditViewAudit` si se expone, o al menos que la consulta no cree `AuditEntry` nuevo en el trail auditado).
4. REWARD_MANAGER consulta `?entityType=Reward&action=APPROVE` → 200; GAME_MANAGER intenta mismo filtro → `403` sin fuga si no tiene permiso (según matriz FR-010).

**Expected**: SC-004 100% entradas 014 idénticas sin re-escritura, SC-007 REWARD_MANAGER ve `Reward`/`Redemption`, GAME_MANAGER ve `Game`/`Category`.

**API check**:
```bash
# Crear categoría y luego auditar
curl -k -X POST https://localhost:XXXX/bff/categories -H "Cookie: ..." -H "Content-Type: application/json" -d '{"name":"Test Audit","knowledgeArea":"Test","academicLevel":"Secundaria","ageMin":12,"ageMax":18,"difficulty":3,"tags":[]}' | jq
curl -k "https://localhost:XXXX/bff/audit?entityType=Category&action=CREATE&page=1" -H "Cookie: ..." | jq
```

### V4 — Paginación masiva, validación y accesibilidad (SC-002, SC-008, SC-009)

1. Con ≥10k entradas en rango de 1 año → verificar `GET /bff/audit?whenFrom=2025-05-13&whenTo=2026-05-13&page=1&pageSize=20` y `page=2` sin duplicados y <2s con `TotalCount`.
2. Probar `page=0` o `pageSize=200` → `400 InvalidFilter` con `errors.page`; `whenFrom=2026-05-20`/`whenTo=2026-01-01` → `400` con `errors.DateRange`; `action=INVALID` → `400` con `errors.action`; sin petición si inválido.
3. Viewport 375–1536 → verificar listado con 9 columnas (scroll horizontal controlado o cards responsive) sin scroll de página horizontal, objetivos ≥44px, `aria-live` en errores, foco visible (SC-009).
4. Forzar 500 en API (sin `oroclash-api`) → verificar `ProblemDetails` RFC7807 con `CorrelationId` sin fuga, estados `Error` con retry por lista; rango sin datos → `Empty`.
5. Verificar que `Previous Value`/`New Value` >10KB se trunca con “Ver JSON completo” y que `password`/`secret` se enmascara a `***`.

## Automated Checks (CI)

```bash
dotnet test tests/OroQuizClash.Architecture.Tests -k "DesignSystemNoDirectDbTests or AdminBffTests"
dotnet test tests/QuizArena.Admin.Tests -k "AuditListTests or AuditDetailTests"
node .opencode/skills/design-system/scripts/validate-tokens.cjs --dir src/Admin --strict
# Axe a11y opcional sobre /admin/audit con auth mock
```

## Troubleshooting

- **Empty con TotalCount 0**: verificar `whenFrom`/`whenTo` y que `EntityType`/`Action` existe en catálogo; `who` con DisplayName/email parcial debe ser case-insensitive.
- **400 InvalidFilter**: verificar `level` no aplica aquí, pero `whenFrom<=whenTo`, `action` en catálogo 14, `entityType` en 7, `page` 1..N; no hacer petición si inválido.
- **403 en audit con GAME_MANAGER**: esperado si filtra `EntityType=Reward` — usar ADMIN o REWARD_MANAGER para `Reward`/`Redemption`.
- **403 en audit con REWARD_MANAGER**: esperado si filtra `EntityType=Game` — usar ADMIN o GAME_MANAGER.
- **401 al filtrar**: cookie expirada → re-autenticar; filtros se preservan localmente.
- **Previous Value null**: esperado para `CREATE` — mostrar “—”.
- **Diff no aparece**: verificar `previousValue` y `newValue` son JSON válidos; `diff` puede ser server-side o cliente.
- **CorrelationId no correlaciona**: verificar `Where.correlationId` se propaga desde `HttpContext.TraceIdentifier` y OTel `Activity.Current.Id`; pegarlo en Jaeger/Seq con `traceId`.
```

