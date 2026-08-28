# Quickstart: Admin Dashboard — Validation Guide

**Branch**: `018-admin-dashboard` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)
**Data Model**: [data-model.md](data-model.md) | **Contracts**: [contracts/dashboard-bff.md](contracts/dashboard-bff.md), [contracts/navigation-map.md](contracts/navigation-map.md)

Guía runnable para validar el dashboard operacional sin implementar pruebas completas. Cada escenario es independiente y referencia el contrato/modelo correspondiente.

## Prerequisites

- `net10.0` SDK 10.0.400 (`dotnet --version` + `global.json`).
- `OroQuizClash.AppHost` corriendo: `dotnet run --project OroQuizClash.AppHost` (levanta `oroclash-api`, `identity-api` Postgres 5432, Redis si aplica, y `quizarena-admin`).
- Cliente OIDC `quizarena-admin` registrado en OroIdentityServer (confidential, `authorization_code+refresh_token`, redirect `/signin-oidc`, scopes `openid profile email offline_access {ApiScope}`) — ver `contracts/dashboard-bff.md §1` y `017/contracts/oidc-config.md`.
- Usuarios de prueba: `admin/Admin@123456` (ADMIN ve 10 métricas+7 atajos), un GAME_MANAGER y un REWARD_MANAGER (ver matriz `contracts/navigation-map.md §4`).
- Design tokens: `design-system/tokens/design-tokens.css` copiado a `src/Admin/QuizArena.Admin/wwwroot/` (gate `validate-tokens`).

## Setup

```bash
# 1. Restaurar y compilar
dotnet restore
dotnet build

# 2. Levantar Aspire (incluye admin)
dotnet run --project OroQuizClash.AppHost
# Esperar dashboard Aspire: https://localhost:15888 → recursos healthy
# Admin URL: https://localhost:XXXX (ver Aspire dashboard → quizarena-admin)

# 3. (Opcional) Validar tokens del Design System
node design-system/validate-tokens.cjs --dir src/Admin
```

## Validation Scenarios

### V1 — Vista operacional resumida (US1, FR-001..008)

**Referencia**: `spec.md US1`, `data-model.md DashboardSnapshot`, `contracts/dashboard-bff.md`.

1. Login como ADMIN → navegar a `/` (Dashboard).
2. Verificar 10 tarjetas en `MetricsGrid` (labels en español): Juegos activos, Juegos programados, Juegos finalizados, Jugadores conectados, Jugadores activos, Preguntas disponibles, Categorías, Premios, Canjes, Estadísticas generales — cada una con valor numérico.
3. Con 0 programados: verificar estado `Empty` (0 + texto sugerido, no error).
4. Simular backend lento (delay proxy): cada bloque muestra skeleton `Loading` y no bloquea los demás; al fallar un bloque (p. ej. Rewards 503) ese bloque muestra `Error` + "Reintentar" aislado; los 9 restantes en `Ready` (SC-009).
5. Login como REWARD_MANAGER → verificar solo Premios/Canjes/Estadísticas visibles o resto enmascarado con mensaje permiso (US1 esc.5).

**Expected**: 10 métricas <5s percibidos (SC-001), sin pantalla en blanco, `GeneratedAt` visible en `DashboardRefreshBar`.

**API check**:
```bash
# Cookie auth requerida; desde el servidor admin el BFF reenvía Bearer — verificar via ServerDashboardService logs/CorrelationId
curl -k https://localhost:XXXX/bff/dashboard/snapshot -H "Cookie: .AspNetCore.Cookies=..." | jq
# Debe retornar DashboardSnapshot con 10 metrics (ver contrato)
```

### V2 — Accesos rápidos (US2, FR-009..012)

**Referencia**: `contracts/navigation-map.md §2`.

1. Como ADMIN, verificar 7 atajos en `QuickActionGrid` con icono Lucide + descripción, orden de foco tras métricas.
2. Click cada atajo → verificar navegación ≤1 clic (FR-009):
   - Crear juego → `/games/new` vacío
   - Configurar juego → `/games?view=config`
   - Gestionar preguntas → `/questions`
   - Ver juegos activos → `/games?status=Active`
   - Ver jugadores → `/players`
   - Gestionar premios → `/rewards`
   - Consultar reportes → `/reports`
3. Como REWARD_MANAGER: solo Gestionar premios + Consultar reportes visibles/habilitados; los 5 restantes ocultos o `aria-disabled` con reason; acceso directo por URL `/questions` → 403 "Acceso denegado" (SC-010).

**Expected**: 100% navegación correcta (SC-002), 44px táctil, sin scroll horizontal 375–1536 (SC-006).

### V3 — Drill-down y actualización (US3, FR-013/014/008)

**Referencia**: `contracts/navigation-map.md §1`, `research.md R4/R5`.

1. Click en tarjeta "Juegos activos" (N) → `/games?status=Active` → verificar `TotalCount == N` (SC-003 coherencia).
2. Crear un juego programado en `/games/new` → volver al Dashboard → click "Actualizar" → contadores reflejan +1 en ≤30s (SC-004 manual).
3. Dejar pestaña visible 60s con auto-refresh ON → verificar actualización automática ≤60s sin recarga completa; cambiar a pestaña oculta → polling pausado (research R5).
4. Forzar 401 (borrar cookie o expirar sesión) durante auto-refresh → verificar polling detenido + banner "Sesión expirada — re-autenticar" sin bucle.
5. Viewport 375px → verificar métricas + atajos sin scroll horizontal, foco teclado lógico (SC-006).

## Automated Checks (CI)

```bash
# Arquitectura — cero acceso directo a DB desde Admin
dotnet test tests/OroQuizClash.Architecture.Tests -k "AdminNoDirectDbTests or DesignSystemNoDirectDbTests"

# Tokens — 0 literales hex fuera de tokens
node design-system/validate-tokens.cjs --dir src/Admin --strict

# Autorización dashboard (cuando se implementen)
dotnet test tests/OroQuizClash.Architecture.Tests -k "DashboardAuthorizationTests"

# (Opcional) Axe a11y sobre Dashboard con auth mock — WCAG 2.2 AA
# npm run a11y -- --url https://localhost:XXXX --include /dashboard
```

## Troubleshooting

- **BFF 401**: verificar `Identity:Authority` apunta a `identity-api` (Aspire service discovery) y cliente `quizarena-admin` registrado con redirect correcto.
- **must_change_password loop**: usuario no-admin recién creado debe pasar por `/Account/ChangePassword` del IdentityServer antes de ver datos (FR-017).
- **Snapshot 503**: `oroclash-api` caído — verificar Aspire resource `oroclash-api` healthy; `CorrelationId` en logs del BFF.
- **Jugadores conectados == activos**: backend no distingue — esperado si `SourceLabel` muestra "aprox." con tooltip (FR-003, research R2).
