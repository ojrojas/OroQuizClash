# Quickstart: Admin Reporting — Validation Guide

**Branch**: `025-admin-reporting` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)
**Data Model**: [data-model.md](data-model.md) | **Contracts**: [contracts/reports-bff.md](contracts/reports-bff.md), [contracts/report-filters-bff.md](contracts/report-filters-bff.md)

Guía runnable para validar reporting analítico (12 métricas: Games, Players, Questions, Categories, Answers, Correct/Incorrect, Scores, Withdrawals, Rewards, Redemptions, Consolation) con 6 filtros combinados (Fecha, Categoría, Juego, Jugador, Nivel, Resultado) solo lectura, con autorización por rol. Cada escenario es independiente y referencia contrato/modelo.

## Prerequisites

- `net10.0` SDK 10.0.400 (`global.json` 10.0.400)
- `OroQuizClash.AppHost` corriendo: `dotnet run --project OroQuizClash.AppHost` (levanta `oroclash-api`, `identity-api` Postgres 5432, `quizarena-admin`)
- Cliente OIDC `quizarena-admin` registrado en OroIdentityServer (confidential, `authorization_code+refresh_token`, redirect `/signin-oidc`, scopes `openid profile email offline_access {ApiScope}`) — ver `017/contracts/oidc-config.md`
- Usuarios: `admin/Admin@123456` (ADMIN), `game_manager` (GAME_MANAGER) y `reward_manager` (REWARD_MANAGER) con roles `ADMIN`, `GAME_MANAGER`, `REWARD_MANAGER`
- Datos: ≥10k juegos históricos, ≥500 jugadores, categorías con ≥5 preguntas, `PointTransaction` con 10 tipos, `Reward`/`RewardRedemption` con `IsConsolation`, para probar agregación ≥10k
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

### V1 — Reportes operativos (US1, FR-001..003, FR-007..011)

**Referencia**: `spec.md US1`, `data-model.md OperationalMetrics`, `contracts/reports-bff.md` §2.

1. Login ADMIN → `/admin/reports` → pestaña “Operativo” → verificar métricas Games (totales por estado 9), Players (únicos/activos), Questions, Categories sin filtros, con skeleton <2s.
2. Seleccionar rango de fechas últimos 30 días (`from=2026-04-13`/`to=2026-05-13`) → verificar `GET /bff/reports/operational?from=...&to=...&page=1` retorna `operational.games.totalGames` y `byStatus.FINISHED` correctos y `calculatedAt` reciente.
3. Filtrar por Categoría “Historia” y Juego específico → verificar `GET /bff/reports/operational?categoryName=Historia&gameName=Quiz&page=1` recalcula todas las métricas para ese subconjunto con `TotalCount`.
4. Probar rango sin datos (futuro `from=2099-01-01`/`to=2099-12-31`) → verificar `Empty` con “Sin datos para el rango” sin error, y botón limpiar filtros.
5. Login GAME_MANAGER → repetir 1-2 → verificar acceso; Login REWARD_MANAGER sin permiso operativo intenta `GET /bff/reports/operational` → `403 Forbidden` sin fuga; no-auth → `401`.

**Expected**: SC-001 <2s (90%), SC-002 100% filtros combinados paginados, SC-007 GAME_MANAGER ve operativo, REWARD_MANAGER 403 en operativo.

**API check**:
```bash
curl -k "https://localhost:XXXX/bff/reports/operational?from=2026-04-13T00:00:00Z&to=2026-05-13T23:59:59Z&page=1&pageSize=20" -H "Cookie: .AspNetCore.Cookies=..." | jq
curl -k "https://localhost:XXXX/bff/reports/operational?categoryName=Historia&page=1" -H "Cookie: ..." | jq
```

### V2 — Rendimiento (US2, FR-004/005, FR-007..010)

**Referencia**: `contracts/reports-bff.md` §3.

1. Desde `/admin/reports` → pestaña “Rendimiento” → verificar métricas Answers (totales), Correct/Incorrect con tasa (`correct/total`), Scores (promedio, distribución, `ByTransactionType`), Withdrawals (totales/tasa) sin filtros, con skeleton <2s.
2. Filtrar por Nivel 3 y Resultado “Correct” → verificar `GET /bff/reports/performance?level=3&result=Correct&page=1` retorna `answers.correctAnswers` y `scores.averageScore` para ese nivel, `accuracyRate` correcto.
3. Filtrar por Jugador específico (`playerSearch=ana`) y rango de fechas → verificar `GET /bff/reports/performance?playerSearch=ana&from=...&to=...` solo de ese jugador, con desglose por juego/categoría.
4. Filtrar por Resultado “Withdrawn” → verificar `withdrawals.totalWithdrawals` y lista paginada con `byPolicy`.
5. Probar Nivel 99 o `from=2026-05-20`/`to=2026-01-01` → verificar validación por campo `errors.level`/`errors.DateRange` sin petición.

**Expected**: SC-003 100% métricas por Nivel/Resultado con tasa correcta, SC-005 validación sin petición, SC-006 flujo completo <2min.

**API check**:
```bash
curl -k "https://localhost:XXXX/bff/reports/performance?level=3&result=Correct&page=1" -H "Cookie: ..." | jq
curl -k "https://localhost:XXXX/bff/reports/performance?playerSearch=ana&page=1" -H "Cookie: ..." | jq
```

### V3 — Recompensas y Consolation (US3, FR-006, FR-010..012)

1. Pestaña “Recompensas” → verificar `GET /bff/reports/rewards?page=1` con `rewards.totalRewards` por tipo 6 y estado 3, `redemptions.totalRedemptions` por estado 5 y `totalCost`, `consolations.totalConsolations` separado.
2. Filtrar por Fecha últimos 7 días y Categoría → verificar `GET /bff/reports/rewards?from=...&to=...&categoryName=Historia` recalcula `byType`/`byStatus` y coste.
3. Filtrar por Jugador → verificar `GET /bff/reports/rewards?playerId={sub}&page=1` solo canjes de ese jugador, con `IsConsolation:true` distinguido (badge) y no sumado en premio normal.
4. Filtrar por Nivel 2 + Resultado `Approved` → verificar `redemptions.byStatus.Approved` filtra por juego/categoría con ese nivel/resultado, coherente con ledger `REWARD_REDEMPTION`/`CONSOLATION`.
5. REWARD_MANAGER → acceso a “Recompensas” 200; GAME_MANAGER intenta `GET /bff/reports/rewards` → `403` sin fuga; verificar que `Consolation` no se cuenta en `rewards.totalRewards`.

**Expected**: SC-004 100% `IsConsolation` separado y coste ledger correcto, SC-007 REWARD_MANAGER ve recompensas, GAME_MANAGER 403.

**API check**:
```bash
curl -k "https://localhost:XXXX/bff/reports/rewards?from=2026-05-01T00:00:00Z&to=2026-05-13T23:59:59Z&page=1" -H "Cookie: ..." | jq
curl -k "https://localhost:XXXX/bff/reports/rewards?playerId={sub}&page=1" -H "Cookie: ..." | jq
```

### V4 — Paginación masiva, validación y accesibilidad (SC-002, SC-008, SC-009)

1. Con ≥10k juegos en rango de 1 año → verificar `GET /bff/reports/operational?from=2025-05-13&to=2026-05-13&page=1&pageSize=20` y `page=2` sin duplicados y <2s con `TotalCount`.
2. Probar `page=0` o `pageSize=200` → `400 InvalidFilter` con `errors.page`/`errors.pageSize`; `level=0` → `400` con `errors.level`.
3. Viewport 375–1536 → verificar dashboard con 3 pestañas y 6 filtros sin scroll horizontal, objetivos ≥44px, `aria-live` en errores, foco visible (SC-009).
4. Forzar 500 en API (sin `oroclash-api`) → verificar `ProblemDetails` RFC7807 con `CorrelationId` sin fuga, estados `Error` con retry por sección.

## Automated Checks (CI)

```bash
dotnet test tests/OroQuizClash.Architecture.Tests -k "DesignSystemNoDirectDbTests or AdminBffTests"
dotnet test tests/QuizArena.Admin.Tests -k "ReportsOperationalTests or ReportsRewardsTests"
node .opencode/skills/design-system/scripts/validate-tokens.cjs --dir src/Admin --strict
# Axe a11y opcional sobre /admin/reports con auth mock
```

## Troubleshooting

- **Empty con TotalCount 0**: verificar rango de fechas y que `CategoryName`/`GameName` existe; `from`/`to` sin filtro debe retornar datos.
- **400 InvalidFilter**: verificar `level` 1–5, `from<=to`, `result` en catálogo 9/5; no hacer petición si inválido.
- **403 en operativo con REWARD_MANAGER**: esperado — usar ADMIN o GAME_MANAGER para operativo/rendimiento.
- **403 en recompensas con GAME_MANAGER**: esperado — usar ADMIN o REWARD_MANAGER.
- **401 al filtrar**: cookie expirada → re-autenticar; filtros se preservan localmente.
- **IsConsolation no aparece**: `GET /bff/reports/rewards` sin filtro de `result` para ver todos; `isConsolation` solo si `RewardType==Consolation`.
- **Paginación duplicada**: verificar `page` incrementa y `TotalCount` se usa para `totalPages`; no cachear con stale.

