# Research: Admin Players

**Branch**: `024-admin-players` | **Date**: 2026-05-13 | **Plan**: [plan.md](plan.md)

Todos los NEEDS CLARIFICATION resueltos. El feature reutiliza agregados y queries de `GamePlayer`/`PointTransaction`/`Reward` y patrón BFF/OIDC/Design System de 017–023; esta fase cierra las incógnitas propias de 024.

---

## R1. Perfil y estado — fuente OroIdentityServer vs dominio de juego

**Decision**: Perfil base desde OroIdentityServer (`sub` + `userinfo` claims `name`, `email`, `tenant_id`, `IdentificationType/Value`) y estado derivado del dominio `GamePlayer`/`UserSession` vía `oroclash-api`.

- **Identidad**: `GET /api/players/{sub}` en `oroclash-api` proyecta `PlayerProfile` combinando datos de `identitydb` (via sync/replicación o `GET /userinfo` cacheado) + `GamePlayer` (última actividad, sesiones). Admin Players NO escribe en `identitydb`.
- **Estado**: `GamePlayer.Status` + `PointTransaction` (última actividad) + `UserSession` (si `identity-api` expone `GET /api/user-sessions?userId={sub}`) mapeados a `PlayerStateView` (`Active`, `InGame`, `Withdrawn`, `Inactive`).
- **API**: `404 PlayerNotFound` si `sub` no existe; `200` con secciones vacías si existe identidad pero sin `GamePlayer` (edge case "Sin participaciones").

**Rationale**: Constitución VI/H exige OroIdentityServer como única autoridad de identidad; Constitución V exige que estado de juego sea Server Truth desde dominio. Combinar ambas evita duplicar `User` en juego.

**Alternatives considered**:
- Perfil solo desde `GamePlayer` sin OroIdentityServer: rechazado — pierde `tenant_id`/`IdentificationType` y viola VI.
- Perfil solo desde `identitydb` directo: rechazado — viola H (no acceso directo a `identitydb`) y pierde estado de juego.

---

## R2. Historial, participaciones y resultados — reuso de `Game`/`GamePlayer` y leaderboard

**Decision**: Reutilizar queries existentes `GetPlayerGames`, `GetGameParticipation`, `GetLeaderboard` sin nuevo agregado.

| Área spec | Query existente | DTO Admin | Detalle |
|-----------|----------------|-----------|---------|
| Historial de partidas | `GetPlayerGamesQuery` (`GamePlayer` → `Game`) | `GameHistoryEntry` | `GameId`, `GameName`, `Category`, `Status` (9 estados), `StartAt`/`FinishedAt`, `RoundCount` |
| Participaciones | `GetParticipationsQuery` | `PlayerParticipation` | `GameId`, `JoinedAt`, `State` (`JOINED`/`WITHDRAWN`/`FINISHED`), `Role` |
| Resultados | `GetPlayerResultQuery` + `GetLeaderboardQuery` | `PlayerResult` | `TotalScore`, `SecuredScore`, `Rank`, `Bonuses`/`Penalties` desde `PointTransaction`, `Duration` |

Filtros `search` (juego/categoría), `status`, `from`/`to` se aplican en `Specification` (`Where`, `Ordering`, `Pagination`) con `ApplyAsNoTracking`.

**Rationale**: Historial/participaciones/leaderboard ya existen en `OroQuizClash.Application/Features/Games` y `Players`; crear nuevos DTOs duplicaría proyecciones y rompería DDD (I).

**Alternatives considered**:
- Crear agregado `PlayerHistory` separado: rechazado — sincronización frágil.
- Historial solo desde `Game` sin `GamePlayer`: rechazado — pierde participaciones específicas del jugador.

---

## R3. Puntuaciones vía ledger `PointTransaction` (Constitución D)

**Decision**:
- **Total**: suma de `PointTransaction` (`SELECT SUM(Points) WHERE PlayerId={sub}`) server-side, nunca mutación directa de balance.
- **Desglose**: `GET /api/players/{id}/scores?type=&from=&to=&page=` devuelve `PagedResult<PointTransactionView>` con `Type ∈ {10}` (`ANSWER_CORRECT`, `ANSWER_INCORRECT`, `ROUND_BONUS`, `LEVEL_BONUS`, `GAME_BONUS`, `PENALTY`, `WITHDRAWAL`, `REWARD_REDEMPTION`, `CONSOLATION`, `ADJUSTMENT`), `Points`, `Timestamp`, `ReferenceId` (`GameId`/`RewardId`).
- **Paginación**: server-side (`page`/`pageSize` 20); cliente nunca reconstruye balance por suma local, solo muestra lo que API retorna.

**Rationale**: Constitución D exige ledger explícito; reproducir en UI violaría Server Truth. Reusar `PointTransaction` garantiza que `REWARD_REDEMPTION` y `CONSOLATION` se descuenten/bonifiquen consistentemente.

**Alternatives considered**:
- Balance mutado en `GamePlayer.Score`: rechazado — no auditable, viola D.
- Cálculo de desglose en cliente: rechazado — viola V y duplica lógica.

---

## R4. Premios y canjes — reuso de `Reward`/`RewardRedemption` y `Consolation` independiente

**Decision**: Mismo modelo que 023, en modo lectura.

- **Premios**: `GET /api/players/{id}/rewards?status=&type=&page=` proyecta `RewardSummary` (6 tipos) donde el jugador es elegible o ha interactuado; elegibilidad evaluada server-side (`Active` + stock + fechas + `ConsolationEligibility`).
- **Canjes**: `GET /api/players/{id}/redemptions?status=&type=&from=&to=&page=` devuelve `RewardRedemptionView` con `IsConsolation` bool; `Consolation` solo con `IsConsolation:true` (Constitución C) y no se cuenta como premio normal.
- **Filtros**: `status` (5 estados), `type` (6 tipos), `from`/`to` aplicados en query `Specification`.

**Rationale**: 009 y 023 ya modelan `Reward` y `RewardRedemption` con ciclo `REQUESTED→DELIVERED`; Admin Players solo proyecta. `Consolation` independiente se respeta visualmente.

**Alternatives considered**:
- Canjes solo desde `GamePlayer`: rechazado — pierde `RewardType` y `IsConsolation`.
- Premios calculados en cliente: rechazado — viola C/V.

---

## R5. Estadísticas agregadas server-side

**Decision**: Métricas calculadas server-side via `GetPlayerStatisticsQuery` (agregaciones sobre `Game`/`PointTransaction`):

- `TotalGames`, `Wins` (posición 1), `Top3`, `AverageScore`, `AccuracyRate` (`correctAnswers/totalAnswers`), `BestStreak`, `AverageTimePerQuestion`, `DistributionByDifficulty`/`ByCategory`.
- Cálculo en `Application` (`IPlayerStatisticsCalculator`) con `Specification` y caché corta (ej. 30s) o materialized view si escala ≥500 partidas.
- Expuesto como `GET /api/players/{id}/statistics` (no paginado, snapshot).

**Rationale**: SC-009 exige métricas sin cálculo en cliente y sin cargar colecciones completas; server-side garantiza consistencia con leaderboard y ledger y evita exponer lógica de agregación.

**Alternatives considered**:
- Cálculo en cliente sumando historial: rechazado — carga ≥500 juegos y expone lógica.
- Vista materializada en DB sin API: rechazado — acopla UI a esquema.

---

## R6. BFF, paginación y autorización por rol

**Decision**:
- **BFF**: `ClientPlayersService` → `HttpClient.BaseAddress = HostEnvironment.BaseAddress` → rutas `/bff/players*` (cookie viaja); `ServerPlayersService` → `http://oroclash-api/api/players*` con `Bearer` del `HttpContext`. Forwarder catch-all `/bff/{**catch-all}` → `/api/{**catch-all}` ya existe (017) y cubre `GET /bff/players?search=&page=&pageSize=`, `GET /bff/players/{id}`, `GET /bff/players/{id}/games?status=&search=&from=&to=&page=`, `GET /bff/players/{id}/scores?type=&from=&to=&page=`, `GET /bff/players/{id}/rewards`, `GET /bff/players/{id}/redemptions`, `GET /bff/players/{id}/statistics`.
- **Paginación**: Todas las listas usan `PagedResult<T>` (`Items`, `TotalCount`, `Page`, `PageSize`) con `Specification` server-side; UI muestra skeleton y estados `Loading`/`Empty`/`Error`.
- **Autorización**: Políticas `AdminOnly` (ADMIN todo), `AdminOrGameManager` (ADMIN+GAMEMANAGER perfil/historial/estadísticas), `RewardManagerOrAdmin` (ADMIN+REWARD_MANAGER premios/canjes). `PLAYER` → 403. `GAME_MANAGER` intentando premios → 403 sin fuga. `must_change_password` gating antes de consultar (VI).
- **Auditoría**: append-only opcional (`PlayerViewAudit` con `ActorId`/`PlayerId`/`Timestamp`/`CorrelationId`) en `SaveChanges` si política lo exige (Constitución I); no muta historial del jugador.

**Rationale**: Reutiliza patrón BFF de 017 (sample `BlazorWebAppOidcBffAutoYarpAspire`), evita exponer `access_token` y preserva `CorrelationId` (FR-013).

**Alternatives considered**:
- Llamar WASM → API directo: rechazado — expone JWT.
- Listado sin paginación: rechazado — no escala con ≥500 participaciones.
- Autorización en cliente solo: rechazado — viola H; API es autoridad.

---

## Consolidated Decisions Summary

| # | Decisión | Fuente |
|---|----------|--------|
| 1 | Perfil desde OroIdentityServer `sub` + estado desde `GamePlayer`/`UserSession` | FR-001/002, Constitución VI/V |
| 2 | Reutilizar `GetPlayerGames`/`GetParticipations`/`GetLeaderboard` para historial/participaciones/resultados | FR-004..006, I |
| 3 | Puntuaciones desde `PointTransaction` ledger con desglose 10 tipos | FR-007, Constitución D |
| 4 | Premios/canjes desde `Reward`/`RewardRedemption` con `IsConsolation` | FR-008/010, C |
| 5 | Estadísticas agregadas server-side via `GetPlayerStatisticsQuery` | FR-009, SC-009 |
| 6 | BFF catch-all + paginación `PagedResult` + matriz de políticas 3 roles | FR-011..016, H/VI |

Sin NEEDS CLARIFICATION pendientes. Listo para Phase 1.
