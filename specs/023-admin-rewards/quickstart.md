# Quickstart: Admin Rewards — Validation Guide

**Branch**: `023-admin-rewards` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)
**Data Model**: [data-model.md](data-model.md) | **Contracts**: [contracts/rewards-bff.md](contracts/rewards-bff.md), [contracts/redemptions-bff.md](contracts/redemptions-bff.md)

Guía runnable para validar catálogo (7 campos, 6 tipos) + ciclo de canjes `Requested→Approved→Delivered`. Cada escenario es independiente y referencia contrato/modelo.

## Prerequisites

- `net10.0` SDK 10.0.400 (`global.json` 10.0.400)
- `OroQuizClash.AppHost` corriendo: `dotnet run --project OroQuizClash.AppHost` (levanta `oroclash-api`, `identity-api` Postgres 5432, `quizarena-admin`)
- Cliente OIDC `quizarena-admin` registrado en OroIdentityServer (confidential, `authorization_code+refresh_token`, redirect `/signin-oidc`, scopes `openid profile email offline_access {ApiScope}`) — ver `017/contracts/oidc-config.md`
- Usuarios: `admin/Admin@123456` (ADMIN), REWARD_MANAGER y GAME_MANAGER (matriz `AdminNavigation`)
- Jugador con puntos suficientes para canjear (vía `PointTransaction` ledger, `GET /api/players/{id}/score`); premio `Physical` con stock limitado para probar `RewardOutOfStock`; premio `Consolation` para probar elegibilidad independiente
- Design tokens en `src/Admin/QuizArena.Admin/wwwroot/design-tokens.css` (gate `validate-tokens`)

## Setup

```bash
dotnet restore
dotnet build
dotnet run --project OroQuizClash.AppHost
# Esperar Aspire dashboard https://localhost:15888 → recursos healthy
# Admin URL → quizarena-admin (ver Aspire)
node design-system/validate-tokens.cjs --dir src/Admin --strict
```

## Validation Scenarios

### V1 — Gestionar el catálogo de premios (US1, FR-001..005)

**Referencia**: `spec.md US1`, `data-model.md Reward`, `contracts/rewards-bff.md`.

1. Login REWARD_MANAGER → `/admin/rewards` → "Crear premio".
2. Completar 7 campos válidos: nombre 3–100, descripción 0–500, tipo `Physical` (de 6), costo 500, stock 10, disponibilidad `2026-09-01` a `2026-12-31` con `From<To` → Guardar.
3. Verificar `201 Created`, `status` `Active`, `isEligible` true, `rowVersion` presente, y que el premio aparece en listado con tipo/stock/costo y es visible para jugadores elegibles (`GET /bff/rewards?onlyEligible=true` lo incluye).
4. Intentar tipo fuera de 6 "Gold" o costo 0 o stock -1 o fechas `From 2026-12-31`/`To 2026-09-01` → verificar `400 InvalidRewardData` con `errors.type`/`cost`/`stock`/`availability` y que no se crea.
5. Intentar nombre duplicado case-insensitive "voucher amazon 20€" con otro premio `Active` no archivado → verificar `409 RewardAlreadyExists` con `errors.name`.
6. Editar premio `Active` stock 5 → costo a 600 → Guardar → verificar `200 OK` con nuevo `cost` y nuevo `rowVersion`.
7. Ejecutar "Desactivar" (`Active → Inactive`) → verificar `Inactive` y no elegible (`GET /bff/rewards?onlyEligible=true` no lo incluye); "Activar" → `Active` si mantiene stock/fechas válidas.

**Expected**: SC-001 <2m (90%), SC-002 100% rechazo con `InvalidRewardData`, SC-003 transiciones `Active↔Inactive` y `→Archived` con éxito, SC-006 autorización 100% (GAME_MANAGER 403).

**API check**:
```bash
curl -k https://localhost:XXXX/bff/rewards -H "Cookie: .AspNetCore.Cookies=..." | jq
curl -k -X POST https://localhost:XXXX/bff/rewards -H "Content-Type: application/json" -H "Cookie: ..." -d '{"name":"Voucher Amazon 20€","type":"Voucher","cost":100,"stock":10}' | jq
```

### V2 — Operar canjes y su ciclo de vida (US2, FR-006..009)

**Referencia**: `contracts/redemptions-bff.md`.

1. Preparar jugador con puntos suficientes (vía `POST /api/games` y `PointTransaction` o seed) y premio `Physical` `Active` con stock 5, costo 100.
2. Jugador canjea premio → `POST /bff/redemptions` o `POST /api/rewards/{id}/redeem` → verificar `201` con `status Requested` y aparece en `GET /bff/redemptions?status=Requested` con `PlayerName`/`RewardName`/`Cost`.
3. Login REWARD_MANAGER → `/admin/rewards` pestaña "Canjes" → filtrar `Requested` → ver canje → "Aprobar" con confirmación → verificar `200` `Approved` con `approvedAt`, stock 5→4, y auditoría `GET /bff/redemptions/{id}` con `history` contiene `Approve` con `ActorId`/`CorrelationId`.
4. Con stock 1, aprobar 2 canjes `Requested` simultáneos → verificar uno `200` y otro `409 RewardOutOfStock` sin mutación parcial.
5. `Requested` → "Rechazar" con motivo → verificar `Rejected` con `Reason`, sin descuento de stock (o reembolsado según política).
6. `Approved` → "Marcar entregado" → verificar `Delivered` con `deliveredAt`; intentar entregar `Rejected` → `422 InvalidRedemptionState`.
7. `Requested` → "Cancelar" → verificar `Cancelled` terminal; `GAME_MANAGER` intenta aprobar → `403`.

**Expected**: SC-004 100% `Requested→Approved` con stock + puntos y `RewardOutOfStock` sin mutación, SC-005 `Requested→Rejected` y `Approved→Delivered` auditados y `Delivered` sobre `Rejected` 422, SC-008 concurrencia sin doble descuento, SC-006 autorización 100%.

**API check**:
```bash
curl -k https://localhost:XXXX/bff/redemptions?status=Requested -H "Cookie: ..." | jq
curl -k -X POST https://localhost:XXXX/bff/redemptions/{id}/approve -H "Cookie: ..." -H "If-Match: W/\"$ROWVERSION\"" -H "X-Idempotency-Key: $(uuidgen)" -H "Content-Type: application/json" -d "{\"rowVersion\":\"$ROWVERSION\",\"idempotencyKey\":\"$(uuidgen)\"}" | jq
```

### V3 — Disponibilidad, inventario y tipos con coherencia (US3, FR-002/003/010/011)

1. Crear premio `Digital` con `From 2026-09-01`/`To 2026-09-30` → verificar fuera de disponibilidad `2026-10-01` muestra `Fuera de disponibilidad` y no es elegible (`isEligible false` aunque `Active`).
2. Crear premio `Physical` stock 2 → aprobar 2 canjes → stock 0 → verificar `Sin stock` y tercer canje `409 RewardOutOfStock`.
3. Crear premio `Monetary` costo 1000 → jugador con 500 puntos intenta canjear → verificar `409 InsufficientPoints` sin crear `Requested` o con `Rejected` inmediato.
4. Crear premio `Consolation` tipo `Consolation` → intentar canjear como premio normal → `400 InvalidRewardType`; si jugador es elegible por `ConsolationEligibility` (via `GET /api/players/{id}/consolation-eligibility`), canje se crea con `isConsolation:true`.
5. Crear premio `Experience` stock 0 (ilimitado) → canjear 100 veces → verificar todos aprobables sin agotar.

**Expected**: SC-010 `Voucher` flujo sin ayuda, `Consolation` solo via regla, stock/fechas coherentes.

### V4 — Concurrencia, paginación y accesibilidad (SC-007..009)

1. Abrir mismo premio `Active` en dos pestañas REWARD_MANAGER → editar distinto campo simultáneamente → verificar uno persiste y otro recibe `409 ConcurrencyConflict` con opción de recargar (SC-008).
2. Crear 50 premios (6 tipos) → verificar `GET /bff/rewards?type=Voucher&status=Active&search=Amazon&page=1&pageSize=20` pagina con `totalCount` y filtros <2s con skeleton (SC-009); igual para canjes `GET /bff/redemptions?status=Requested&page=1`.
3. Viewport 375–1536 → verificar formulario y listados premios/canjes sin scroll horizontal, objetivos ≥44px, `aria-live` en errores, foco visible (SC-007).

## Automated Checks (CI)

```bash
dotnet test tests/OroQuizClash.Architecture.Tests -k "DesignSystemNoDirectDbTests or AdminBffTests"
dotnet test tests/QuizArena.Admin.Tests -k "RewardTests or RedemptionTests"
node design-system/validate-tokens.cjs --dir src/Admin --strict
# Axe a11y opcional sobre /admin/rewards/new con auth mock
```

## Troubleshooting

- **RewardAlreadyExists**: nombre duplicado case-insensitive entre no archivados → cambiar nombre o archivar el existente.
- **RewardOutOfStock al aprobar**: `GET /bff/rewards/{id}` → `stock` 0 con tipo `Physical` limitado → reponer stock via `PUT /bff/rewards/{id}` con `Stock` >0.
- **401 al guardar/aprobar**: cookie expirada → re-autenticar; el formulario/borrador conserva datos localmente (FR-014 edge case).
- **409 ConcurrencyConflict**: recargar detalle para nuevo `RowVersion` y reintentar con nuevo `IdempotencyKey`.
- **422 InvalidRedemptionState al entregar**: verificar que el canje está en `Approved`, no en `Rejected`/`Cancelled`.
- **RewardUnavailable fuera de fechas**: `GET /bff/rewards/{id}` → `isEligible false` con `AvailableFrom`/`To` fuera de hoy → ajustar fechas o esperar ventana.
- **InsufficientPoints**: `GET /bff/players/{id}/score` → puntos elegibles < costo → el jugador debe ganar puntos primero (ledger).
