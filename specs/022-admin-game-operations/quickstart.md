# Quickstart: Admin Game Operations — Validation Guide

**Branch**: `022-admin-game-operations` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)
**Data Model**: [data-model.md](data-model.md) | **Contracts**: [contracts/live-game-bff.md](contracts/live-game-bff.md), [contracts/live-operations.md](contracts/live-operations.md)

Guía runnable para validar supervisión en vivo (10 indicadores) + 4 acciones controladas con auditoría. Cada escenario es independiente y referencia contrato/modelo.

## Prerequisites

- `net10.0` SDK 10.0.400 (`global.json` 10.0.400)
- `OroQuizClash.AppHost` corriendo: `dotnet run --project OroQuizClash.AppHost` (levanta `oroclash-api`, `identity-api` Postgres 5432, `quizarena-admin`, `hubs/game` forwarder)
- Cliente OIDC `quizarena-admin` registrado en OroIdentityServer (confidential, `authorization_code+refresh_token`, redirect `/signin-oidc`, scopes `openid profile email offline_access {ApiScope}`) — ver `017/contracts/oidc-config.md`
- Usuarios: `admin/Admin@123456` (ADMIN), GAME_MANAGER y REWARD_MANAGER (matriz `AdminNavigation`)
- Juego en `Running` con 2–5 jugadores conectados y 1 ronda activa para V1/V2 (crear via `019-admin-game-configuration` + `StartGame` o seed); categoría `Active` con ≥5 preguntas válidas para que el juego pueda iniciar
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

### V1 — Supervisar el estado vivo (US1, FR-001..007)

**Referencia**: `spec.md US1`, `data-model.md LiveGameView`, `contracts/live-game-bff.md`.

1. Crear juego via `019` y llevarlo a `Running` con 2 jugadores conectados (unirse via `POST /bff/games/{id}/players` o simular con `GamePlayer` seed).
2. Login ADMIN → `/admin/live/{gameId}` (o `/admin/live` → click en juego activo).
3. Verificar 10 indicadores sin pantalla en blanco (skeleton por indicador, carga <2s):
   - `Game Status: Running`, `Current Round: 1`, `Current Question` con 4 opciones A–D,
   - `Players: 5`, `Players Connected: 3`, `Players Answered: 2`, `Players Waiting: 1`,
   - `Scores` tabla con `Score`/`SecuredPoints`/`Level`, `Current Level: 2`, `Game Timer` cuenta regresiva 5–300s.
4. Pedir a un jugador que responda → verificar `Players Answered`/`Waiting` y `Scores` se actualizan en ≤3s sin recarga completa (polling 3–5s o WebSocket) y `Current Question` sin parpadeo.
5. Con juego sin jugadores conectados → verificar `Players Connected: 0` con estado `Empty` (no error).
6. Login REWARD_MANAGER → intentar `/admin/live/{gameId}` → verificar `Access Denied` y `403` por `GET /bff/games/{id}/live`.

**Expected**: SC-001 <3s percibidos (carga <2s), SC-002 ≤3s actualización y coherencia 100% con `GET /bff/games/{id}/leaderboard`, SC-007 cada indicador maneja `Loading/Empty/Error` aislado.

**API check**:
```bash
curl -k https://localhost:XXXX/bff/games/{id}/live -H "Cookie: .AspNetCore.Cookies=..." | jq
curl -k https://localhost:XXXX/bff/games/{id}/leaderboard -H "Cookie: ..." | jq
# Verificar playersAnswered + playersWaiting == playersConnected y scores coinciden
```

### V2 — Controlar el ciclo de vida en ejecución con auditoría (US2, FR-010..014)

**Referencia**: `contracts/live-operations.md`.

1. Con juego en `Running` → en la vista en vivo, click `Pause` → confirmar diálogo → verificar `Paused`, `Game Timer` congelado, y auditoría `GET /bff/games/{id}` con `history` contiene `Pause` con `ActorId`/`CorrelationId`.
2. Con `Paused` → `Resume` → verificar `Running` y timer reanudado + auditoría `Resume`.
3. Con `Running` o `Paused` → `Cancel` con motivo opcional → verificar `Cancelled` terminal, notificación a jugadores (hub `GameCancelled`), y auditoría `Cancel` con `Reason`.
4. Con `Running` atascado → `Force Finish` → verificar `Finished` (o `ForcedFinished`) forzado, `privileged:true` en auditoría, y que `Resume` queda deshabilitado.
5. Intentar `Pause` sobre `Finished` → verificar `422 InvalidGameState` sin mutación ni auditoría de éxito.
6. Login REWARD_MANAGER → intentar `POST /bff/games/{id}/pause` → verificar `403` sin fuga y sin auditoría de éxito.

**Expected**: SC-003 100% transiciones válidas con auditoría, 100% inválidas 422 sin mutación; SC-004 autorización 100%.

**API check**:
```bash
curl -k -X POST https://localhost:XXXX/bff/games/{id}/pause -H "Cookie: ..." -H "If-Match: W/\"$ROWVERSION\"" -H "X-Idempotency-Key: $(uuidgen)" -H "Content-Type: application/json" -d "{\"rowVersion\":\"$ROWVERSION\",\"idempotencyKey\":\"$(uuidgen)\"}" | jq
curl -k https://localhost:XXXX/bff/games/{id} -H "Cookie: ..." | jq '.history'
```

### V3 — Coherencia operativa, reconexión y concurrencia (US3, FR-005..007, FR-019)

**Referencia**: `spec.md US3`.

1. Con `Players Connected: 3`, `Answered: 2`, `Waiting: 1` → recargar la vista → verificar mismos conteos (coherencia server-side, no cálculo en UI) y `Scores` coinciden con `leaderboard`.
2. Forzar desconexión del operador (matar WebSocket o bloquear polling con DevTools offline 5s) → reconectar → verificar re-sincronización de `Current Round`/`Current Question`/`Game Timer` sin duplicar auditoría y sin mostrar datos obsoletos.
3. Con dos operadores ADMIN en la misma vista en vivo, ejecutar `Pause` simultáneamente (dos `POST /bff/games/{id}/pause` con distintos `IdempotencyKey` pero mismo `RowVersion`) → verificar uno tiene éxito 200 y el otro recibe `409 ConcurrencyConflict` o `422 InvalidGameState` sin segunda auditoría.
4. Reintentar `Pause` con mismo `IdempotencyKey` tras timeout de red → verificar `200` replay sin mutar ni duplicar auditoría (idempotencia).
5. Con juego `Finished` mientras la vista está abierta → verificar `Game Status: Finished`, `Game Timer` congelado en 0, 4 acciones deshabilitadas y resumen final de `Scores`.

**Expected**: SC-005 coherencia 100% ledger, SC-006 timer sincronizado y congelado en `Paused`, SC-008 concurrencia sin doble auditoría.

### V4 — Responsive, a11y y edge cases (SC-009..010)

1. Viewport 375–1536 → verificar 10 indicadores apilados sin scroll horizontal, objetivos táctiles ≥44px, `aria-live="polite"` para `Scores`/`Timer`, foco visible (SC-009, WCAG AA).
2. Viewport 375px → verificar `LivePlayersPanel` y `LiveScoresTable` utilizables sin scroll horizontal y `LiveOperationsBar` con botones ≥44px.
3. Abrir vista para juego `Draft`/`Configured` (no iniciado) → verificar `Current Round`/`Current Question`/`Game Timer` en `Empty` con "Juego no iniciado" y acciones deshabilitadas excepto `Cancel`.
4. Simular `Current Question` lenta >5s → verificar skeleton y `aria-busy`, luego `Error` con Reintentar aislado sin bloquear el resto y sin generar auditoría.

## Automated Checks (CI)

```bash
dotnet test tests/OroQuizClash.Architecture.Tests -k "DesignSystemNoDirectDbTests or AdminBffTests"
dotnet test tests/QuizArena.Admin.Tests -k "LiveGameViewTests or LiveOperationsTests"
node design-system/validate-tokens.cjs --dir src/Admin --strict
# Axe a11y opcional sobre /admin/live/{id} con auth mock
```

## Troubleshooting

- **401 al abrir vista en vivo**: cookie expirada → re-autenticar; polling/WebSocket se detiene y muestra "Sesión expirada" sin bucle.
- **CategoryNotReady / QuestionInUse**: no aplica a 022 (son de 020/021), pero si `Current Question` no aparece, verificar que la categoría del juego tiene `ValidQuestionCount ≥5` y que la pregunta está `Active` con 4/1.
- **409 ConcurrencyConflict al pausar**: recargar la vista para nuevo `RowVersion` y reintentar con nuevo `IdempotencyKey`.
- **422 InvalidGameState al forzar**: verificar que el juego está en `Running`/`Paused`, no en `Finished`/`Cancelled`.
- **Scores no coinciden con leaderboard**: verificar que `Scores` se reconstruye desde `PointTransaction` ledger, no desde cálculo en UI; re-consultar `GET /bff/games/{id}/leaderboard`.
- **Game Timer no se congela en Paused**: verificar que el servidor congela `StartedAt`/`PausedAt`; la UI solo refleja `remainingSeconds` del servidor.
