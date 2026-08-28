# Quickstart: Admin Players — Validation Guide

**Branch**: `024-admin-players` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)
**Data Model**: [data-model.md](data-model.md) | **Contracts**: [contracts/players-bff.md](contracts/players-bff.md)

Guía runnable para validar consulta de participantes (9 áreas: perfil, estado, historial, participaciones, resultados, puntuaciones, premios, canjes, estadísticas) solo lectura, con autorización por rol. Cada escenario es independiente y referencia contrato/modelo.

## Prerequisites

- `net10.0` SDK 10.0.400 (`global.json` 10.0.400)
- `OroQuizClash.AppHost` corriendo: `dotnet run --project OroQuizClash.AppHost` (levanta `oroclash-api`, `identity-api` Postgres 5432, `quizarena-admin`)
- Cliente OIDC `quizarena-admin` registrado en OroIdentityServer (confidential, `authorization_code+refresh_token`, redirect `/signin-oidc`, scopes `openid profile email offline_access {ApiScope}`) — ver `017/contracts/oidc-config.md`
- Usuarios: `admin/Admin@123456` (ADMIN), `game_manager` (GAME_MANAGER) y `reward_manager` (REWARD_MANAGER) con roles `ADMIN`, `GAME_MANAGER`, `REWARD_MANAGER` (matriz `AdminNavigation` y FR-011)
- Jugadores con datos: al menos 1 jugador con ≥5 partidas finalizadas, ≥200 participaciones para paginación, con `PointTransaction` variados (10 tipos), con premios/canjes incluyendo `Consolation`, y 1 jugador sin historial (para Empty)
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

### V1 — Consultar perfil y estado (US1, FR-001..003)

**Referencia**: `spec.md US1`, `data-model.md PlayerDetail`, `contracts/players-bff.md`.

1. Login ADMIN → `/admin/players` → verificar listado paginado con búsqueda.
2. Buscar por nombre parcial o email (ej. "ana") → verificar `GET /bff/players?search=ana&page=1&pageSize=20` retorna `items` con `displayName`/`email` coincidentes y `totalCount`, con skeleton <2s.
3. Abrir detalle de un jugador con historial → verificar `GET /bff/players/{id}` muestra `displayName`, `email`, `tenantId`, `identificationType/Value`, `state` (`Active`/`InGame`), `scoreSummary` (total/secured), `totalParticipations`, y pestañas.
4. Repetir con jugador sin participaciones → verificar perfil básico sin error, secciones "Historial"/"Puntuaciones" vacías con `Empty` y mensaje "Sin participaciones".
5. Login GAME_MANAGER → repetir pasos 2-3 → verificar mismo acceso; Login REWARD_MANAGER → `/admin/players` → verificar solo ve perfil básico + premios/canjes, historial bloqueado o 403 al intentar `GET /bff/players/{id}/games` → `403 Forbidden` con `ProblemDetails`.
6. Intentar acceso no autenticado a `/bff/players` → verificar `401 Unauthorized`.

**Expected**: SC-001 <30s (90%), SC-002 100% búsquedas <2s con skeleton, SC-007 autorización 100% (PLAYER 403, REWARD_MANAGER limitado).

**API check**:
```bash
curl -k https://localhost:XXXX/bff/players?search=ana&page=1&pageSize=20 -H "Cookie: .AspNetCore.Cookies=..." | jq
curl -k https://localhost:XXXX/bff/players/{id} -H "Cookie: ..." | jq
```

### V2 — Historial, participaciones y resultados (US2, FR-004..006)

**Referencia**: `contracts/players-bff.md` §4.

1. Desde detalle del jugador con ≥5 partidas → pestaña "Historial" → verificar `GET /bff/players/{id}/games?page=1&pageSize=20` lista con `gameName`, `categoryName`, `status` (9 estados), `roundCount`, `playerScore`/`playerRank`, orden descendente por `createdAt`.
2. Aplicar filtros: `search=Historia`, `status=FINISHED`, `from=2026-01-01`/`to=2026-05-13` → verificar `GET /bff/players/{id}/games?search=Historia&status=FINISHED&from=...&to=...&page=1` pagina correctamente y mantiene <2s.
3. Pestaña "Participaciones" → verificar `GET /bff/players/{id}/participations?page=1` con `joinedAt`, `state` (`JOINED`/`WITHDRAWN`/`FINISHED`), `gameStatus`; filtrar por `state=FINISHED` y rango de fechas.
4. Click en una participación `FINISHED` → "Resultados" → verificar `GET /bff/players/{id}/results/{gameId}` con `totalScore`, `securedScore`, `rank`, `correctAnswers/totalAnswers`, `duration`, `bonuses`/`penalties` desde `PointTransaction`.
5. Con jugador de ≥200 participaciones → verificar paginación `page=2` sin cargar todo y sin duplicados.

**Expected**: SC-003 100% paginación correcta (≥200) y filtros <2s, SC-005 flujo completo <2min.

**API check**:
```bash
curl -k "https://localhost:XXXX/bff/players/{id}/games?status=FINISHED&page=1&pageSize=20" -H "Cookie: ..." | jq
curl -k https://localhost:XXXX/bff/players/{id}/results/{gameId} -H "Cookie: ..." | jq
```

### V3 — Puntuaciones, premios, canjes y estadísticas (US3, FR-007..010)

1. Pestaña "Puntuaciones" → verificar `GET /bff/players/{id}/scores?page=1` desglose con `type` 10 valores (`ANSWER_CORRECT`, `PENALTY`, `REWARD_REDEMPTION`, `CONSOLATION`...), `points`, `timestamp`, `referenceId`; filtrar por `type=ANSWER_CORRECT` y `from`/`to`; verificar total reconstruido coincide con `scoreSummary.totalPoints`.
2. Pestaña "Premios" → `GET /bff/players/{id}/rewards?status=Active&page=1` → verificar premios con `IsEligible`; "Canjes" → `GET /bff/players/{id}/redemptions?status=Approved&page=1` → verificar `rewardName`, `cost`, `status` (5), `isConsolation`; filtrar por `rewardType=Voucher` y fecha.
3. Para `Consolation` → verificar canje con `isConsolation:true` y `rewardType=Consolation` no se cuenta como premio normal.
4. Pestaña "Estadísticas" → `GET /bff/players/{id}/statistics` → verificar `totalGames`, `wins`, `top3`, `averageScore`, `accuracyRate`, `bestStreak`, `averageTimePerQuestion`, `distributionByDifficulty/Category` y `calculatedAt`; sin cálculo en cliente, con skeleton.
5. GAME_MANAGER consulta puntuaciones/estadísticas → 200; REWARD_MANAGER consulta estadísticas → 403 si política lo restringe; REWARD_MANAGER consulta premios/canjes → 200.

**Expected**: SC-004 100% puntuaciones ledger correctas, SC-006 100% canjes con `IsConsolation` correcto, SC-009 estadísticas snapshot.

**API check**:
```bash
curl -k "https://localhost:XXXX/bff/players/{id}/scores?type=ANSWER_CORRECT&page=1" -H "Cookie: ..." | jq
curl -k "https://localhost:XXXX/bff/players/{id}/statistics" -H "Cookie: ..." | jq
curl -k "https://localhost:XXXX/bff/players/{id}/redemptions?status=Approved" -H "Cookie: ..." | jq
```

### V4 — Paginación masiva, validación y accesibilidad (SC-002..009)

1. Buscar jugador con texto vacío → verificar paginación `page=1` y `page=2` sin duplicados; `search` con 100 chars → sin error.
2. Filtrar con `from=2026-05-20`/`to=2026-01-01` (Desde > Hasta) → verificar validación por campo con mensaje "Desde debe ser ≤ Hasta" y sin petición.
3. Viewport 375–1536 → verificar listado y detalle con 6 pestañas sin scroll horizontal, objetivos ≥44px, `aria-live` en errores, foco visible (SC-009).
4. Forzar 500 error en API (sin `oroclash-api`) → verificar `ProblemDetails` RFC7807 con `title`/`detail`/`correlationId` sin fuga, estados `Error` con retry por pestaña.

## Automated Checks (CI)

```bash
dotnet test tests/OroQuizClash.Architecture.Tests -k "DesignSystemNoDirectDbTests or AdminBffTests"
dotnet test tests/QuizArena.Admin.Tests -k "PlayerProfileTests or PlayerStatisticsTests"
node .opencode/skills/design-system/scripts/validate-tokens.cjs --dir src/Admin --strict
# Axe a11y opcional sobre /admin/players con auth mock
```

## Troubleshooting

- **PlayerNotFound 404**: `GET /bff/players/{id}` → verificar `sub` existe en OroIdentityServer y tiene `GamePlayer`; si solo identidad sin juego, debe retornar 200 con secciones vacías según política (ver `data-model.md`).
- **403 al ver historial con REWARD_MANAGER**: esperado por matriz FR-011 → usar ADMIN o GAME_MANAGER.
- **401 al consultar**: cookie expirada → re-autenticar; filtros/búsqueda se preservan localmente.
- **Historial vacío**: verificar jugador tiene `GamePlayer` y `Game` con `GameStatus` finales; `GET /bff/players/{id}/games` debe retornar `totalCount` y `items`.
- **Puntuaciones no coinciden**: verificar ledger server-side `SUM(points)` vs `scoreSummary`; no recalcular en cliente.
- **Consolation no aparece**: `GET /bff/players/{id}/redemptions` → filtrar sin `status` para ver todos; `isConsolation:true` solo si `RewardType==Consolation`.
- **Desde > Hasta**: corregir rango; validación `From <= To` por campo.
