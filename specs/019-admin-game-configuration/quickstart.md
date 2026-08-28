# Quickstart: Admin Game Configuration — Validation Guide

**Branch**: `019-admin-game-configuration` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)
**Data Model**: [data-model.md](data-model.md) | **Contracts**: [contracts/game-configuration-bff.md](contracts/game-configuration-bff.md), [contracts/state-transitions.md](contracts/state-transitions.md)

Guía runnable para validar creación/configuración + ciclo de vida de 8 estados. Cada escenario es independiente y referencia contrato/modelo.

## Prerequisites

- `net10.0` SDK 10.0.400 (`global.json` 10.0.400)
- `OroQuizClash.AppHost` corriendo: `dotnet run --project OroQuizClash.AppHost` (levanta `oroclash-api`, `identity-api` Postgres 5432, `quizarena-admin`)
- Cliente OIDC `quizarena-admin` registrado en OroIdentityServer (confidential, `authorization_code+refresh_token`, redirect `/signin-oidc`, scopes `openid profile email offline_access {ApiScope}`) — ver `017/contracts/oidc-config.md`
- Usuarios: `admin/Admin@123456` (ADMIN), GAME_MANAGER y REWARD_MANAGER (matriz `AdminNavigation`)
- Categoría `Active` con ≥5 preguntas válidas (4 opciones, 1 correcta) — crear via `src/Admin` o API si no existe; `Reward` Active con stock para premios
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

### V1 — Crear y configurar partida completa (US1, FR-001..008, FR-010..012)

**Referencia**: `spec.md US1`, `data-model.md GameConfiguration`, `contracts/game-configuration-bff.md`.

1. Login ADMIN → `/admin/games` → "Crear juego".
2. Completar 16 campos válidos: nombre 3–100, descripción ≤500, categoría Active ≥5 preguntas, rondas 5–10, maxPlayers ≥2, tiempo 5–300, dificultad 1–5, progresión `Adaptive`, scoring `ProgressiveBonus`, secured `KeepCheckpoint`, withdrawal `KEEP_SECURED_SCORE`, finish `FALLBACK_TO_CHECKPOINT`, premios `Active` (opcional), `ScheduledAt` vacía o futura ≥5m → Guardar.
3. Verificar `201 Created`, `status` `Draft` → `Configured`, `rowVersion` presente, y que el juego aparece en listado.
4. Intentar con categoría inexistente o inactiva/<5 preguntas → verificar `400 CategoryNotReady` con `errors.categoryId` y que no se crea.
5. Dejar nombre vacío o rondas 4 → verificar error por campo sin pantalla en blanco (400 `InvalidConfiguration`).
6. Login REWARD_MANAGER → intentar `/admin/games/new` → verificar `Access Denied` y `403` por API directa `POST /bff/games`.

**Expected**: SC-001 <3m (90%), SC-004 <2s rechazo, SC-005 coherencia 100% al recargar detalle, SC-006 autorización 100%.

**API check**:
```bash
curl -k https://localhost:XXXX/bff/games -H "Cookie: .AspNetCore.Cookies=..." | jq
curl -k -X POST https://localhost:XXXX/bff/games -H "Content-Type: application/json" -H "Cookie: ..." -d '{"name":"QA 019","categoryId":"...","numberOfRounds":5,"maxPlayers":10,"timePerQuestion":30,"initialDifficulty":3,"difficultyProgression":"Linear","scoringSystem":"Standard","withdrawalPolicy":"LOSE_ALL","finishPolicy":"LOSE_ALL"}' | jq
```

### V2 — Programar y controlar ciclo de vida 8 estados (US2, FR-008/009, FR-013)

**Referencia**: `contracts/state-transitions.md`.

1. Tomar juego en `Configured` → editar → asignar `ScheduledAt` futura +10m → Guardar → verificar `Scheduled` + fecha en detalle.
2. Intentar `ScheduledAt` en pasado → verificar `400 ValidationError` "La fecha debe ser futura".
3. Mover `Scheduled → Ready` → verificar habilitado "Iniciar"; ejecutar `Ready → Running` → verificar edición bloqueada (campos inmutables, FR-010) y `409` si se intenta `PUT`.
4. Ejecutar `Running → Paused` → verificar timer congelado; `Paused → Running` (Reanudar) → verificar reanudación sin repetir preguntas.
5. Ejecutar `Running → Finished` y desde otro juego `Draft → Cancelled` → verificar terminales e historial `GET /bff/games/{id}` con `history` audit.
6. Intentar transición inválida `Finished → Running` → verificar `422 InvalidGameState` sin mutación parcial.

**Expected**: SC-002 100% transiciones válidas auditadas, 100% inválidas rechazadas; SC-003 100% bloqueo edición tras `Ready`/`Running`.

### V3 — Validación avanzada, premios y reglas (US3, FR-003..006, FR-011)

1. Editar juego en `Draft` → seleccionar `DifficultyProgression Adaptive`, `Scoring ProgressiveBonus`, `Withdrawal KEEP_SECURED_SCORE`, `Finish FALLBACK_TO_CHECKPOINT`, `FinalReward` y `ConsolationReward` Active distintos → Guardar con éxito.
2. Intentar `Reward` inactivo o mismo reward para final/consolación cuando política exige distinción → verificar `400 RewardUnavailable`/`InvalidConfiguration`.
3. Intentar `Puntos asegurados` incoherente con rondas (p. ej., 11 con rondas 5) → verificar error por campo.
4. Llevar juego a `Running` → verificar formulario solo lectura y que `PUT /bff/games/{id}` retorna `422 InvalidGameState`.

**Expected**: SC-003 inmutabilidad 100%, SC-004/005 validación sin fuga.

### V4 — Concurrencia, paginación y accesibilidad (SC-007..009)

1. Abrir mismo juego en `Draft` en dos pestañas ADMIN → editar distinto campo simultáneamente → verificar uno persiste y otro recibe `409 ConcurrencyConflict` con opción de recargar (SC-008).
2. Crear 100 juegos (seed o script) → verificar `GET /bff/games?page=1&pageSize=20` pagina con `totalCount` y filtros `status`/`category` <2s con skeleton (SC-009).
3. Viewport 375–1536 → verificar formulario sin scroll horizontal, objetivos ≥44px, `aria-live` en errores, foco visible (SC-007).

## Automated Checks (CI)

```bash
dotnet test tests/OroQuizClash.Architecture.Tests -k "DesignSystemNoDirectDbTests or AdminBffTests"
dotnet test tests/QuizArena.Admin.Tests -k "GameConfigurationTests or GameStateTransitionTests"
node design-system/validate-tokens.cjs --dir src/Admin --strict
# Axe a11y opcional sobre /admin/games/new con auth mock
```

## Troubleshooting

- **CategoryNotReady**: verificar `GET /bff/categories?status=Active` → `ValidQuestionCount ≥5`; crear preguntas con 4 opciones/1 correcta y publicar.
- **401 al guardar**: cookie expirada → re-autenticar; el formulario conserva borrador local (FR-012 edge case).
- **409 ConcurrencyConflict**: recargar detalle para nuevo `RowVersion` y reintentar.
- **422 InvalidGameState tras Running**: esperado — configuración inmutable; crear nuevo juego para nuevo ciclo.
- **RewardUnavailable**: `GET /bff/rewards?status=Active` debe incluir el reward con `Stock>0`.
