# Research: Admin Reporting

**Branch**: `025-admin-reporting` | **Date**: 2026-05-13 | **Plan**: [plan.md](plan.md)

Todos los NEEDS CLARIFICATION resueltos. El feature reutiliza agregados y queries de `Game`/`Question`/`Category`/`GamePlayer`/`PointTransaction`/`Reward` y patrón BFF/OIDC/Design System de 015 y 017–024; esta fase cierra las incógnitas propias de 025.

---

## R1. Métricas operativas — Games, Players, Questions, Categories

**Decision**: Métricas operativas agregadas server-side vía `Specification` sobre `Game`/`GamePlayer`/`Question`/`Category` existentes.

- **Games**: `GetGamesReportQuery` — `TotalGames` + `ByStatus` (map 9 estados `DRAFT`..`FORCED_FINISHED` → conteo) con filtros `From`/`To`/`Category`/`Game`.
- **Players**: `GetPlayersReportQuery` — `UniquePlayers` (COUNT DISTINCT `PlayerId` en `GamePlayer`), `ActivePlayers` (con actividad en rango), `DistributionByTenant`.
- **Questions/Categories**: `GetQuestionsReportQuery` — `TotalQuestions`, `ByCategory`, `ByLevel`, `CategoriesInUse` (COUNT DISTINCT `CategoryId` en `Game`/`Question`).

Agregación con `ApplyAsNoTracking` + `GroupBy` en DB, paginación `page`/`pageSize` solo para listas desglosadas; totales no paginados (single snapshot con `CalculatedAt`).

**Rationale**: 015 `OperationalReporting` ya agrega `Game`/`Player`; reusar evita nuevo agregado y respeta DDD (I). Server-side garantiza `TotalCount` correcto sin cargar colecciones.

**Alternatives considered**:
- Agregación en cliente sumando juegos: rechazado — carga ≥10k juegos y expone lógica.
- Vista materializada sin API: rechazado — acopla UI a esquema.

---

## R2. Métricas de rendimiento — Answers, Correct/Incorrect, Scores, Withdrawals, Nivel, Resultado

**Decision**:
- **Answers**: `GetAnswersReportQuery` — `TotalAnswers` (COUNT `Answer`), `CorrectAnswers` (WHERE `IsCorrect=true`), `IncorrectAnswers` (`total-correct`), `AccuracyRate` (`correct/total`) con filtros `Level` (1–5), `Result`, `Category`/`Game`/`Player`/`Fecha`.
- **Scores**: `GetScoresReportQuery` — `TotalPoints` (SUM `PointTransaction.Points`), `AverageScore` (AVG por juego/jugador), `Distribution` (histograma) y `ByTransactionType` (10 tipos). Reconstruido desde `PointTransaction` ledger, nunca balance mutado (D).
- **Withdrawals**: `GetWithdrawalsReportQuery` — `TotalWithdrawals` (WHERE `GamePlayer.State==WITHDRAWN`), `ByPolicy` (`LOSE_ALL` etc.), `Rate` (`withdrawals/games`) con `JoinedAt`/`ExitedAt`.

Todos calculados en `Application` (`IReportCalculator`) con `Specification` (`Where` + `And`).

**Rationale**: Constitución D exige ledger para Scores; `Answer` + `PointTransaction` ya contienen `IsCorrect` y `Points`; reusar asegura tasa correcta y evita recalcular en cliente (V).

**Alternatives considered**:
- Scores mutados en `GamePlayer.Score`: rechazado — no auditable, viola D.
- Cálculo de `AccuracyRate` en cliente: rechazado — viola V.

---

## R3. Métricas de economía — Rewards, Redemptions, Consolation

**Decision**: Mismo modelo que 023/024, en modo agregación.

- **Rewards**: `GetRewardsReportQuery` — `TotalRewards` + `ByType` (6 tipos) + `ByStatus` (3 estados) desde `Reward`.
- **Redemptions**: `GetRedemptionsReportQuery` — `TotalRedemptions` + `ByStatus` (5) + `TotalCost` (SUM `Cost`) + `ByType` desde `RewardRedemption`.
- **Consolation**: `GetConsolationReportQuery` — `TotalConsolations` + `TotalCostConsolation` donde `IsConsolation:true`; separado de premios normales (Constitución C) y no sumado en `TotalRewards`.

Filtros `RewardType`, `Status`, `From`/`To` aplicados en query.

**Rationale**: 009 ya modela `Reward`/`RewardRedemption` con `IsConsolation`; Admin Reporting solo agrega. Separar `Consolation` evita que se cuente como premio normal.

**Alternatives considered**:
- Redemptions solo desde `PointTransaction`: rechazado — pierde `RewardType` y `IsConsolation`.
- Rewards calculados en cliente: rechazado — viola C/V.

---

## R4. Filtros combinados — 6 dimensiones con validación

**Decision**:
- **Fecha**: `From`/`To` opcionales (`DateTimeOffset`), `From <= To` si ambos; sin fechas = todo el histórico.
- **Categoría**: `CategoryId` o nombre (existe en `CategoryCatalogs`), `null` = todas.
- **Juego**: `GameId` o búsqueda parcial por `GameName` (case-insensitive), `null` = todos.
- **Jugador**: `PlayerId` (`sub`) o búsqueda parcial `DisplayName`/`Email` (case-insensitive), `null` = todos.
- **Nivel**: `1..5` (`Difficulty` 1–5), otros → `400 InvalidFilter` con `errors.level`.
- **Resultado**: catálogo cerrado por métrica: `GameStatus` (9) para Games, `ParticipationState` (4) para Withdrawals, `RedemptionStatus` (5) para Redemptions, `Answer correctness` (`Correct`/`Incorrect`) para Answers. Fuera de catálogo → `400`.

Combinación AND: `Where(Fecha).And(Categoría).And(Juego)…` en `Specification`.

**Rationale**: SC-002 exige filtros combinados consistentes server-side; catálogos cerrados evitan valores inválidos y validación por campo sin petición.

**Alternatives considered**:
- Filtros OR: rechazado — semantics ambiguo para reporting.
- Validación solo en cliente: rechazado — viola H/I.

---

## R5. BFF, paginación y autorización por rol

**Decision**:
- **BFF**: `ClientReportsService` → `HttpClient.BaseAddress = HostEnvironment.BaseAddress` → rutas `/bff/reports/operational`, `/bff/reports/performance`, `/bff/reports/rewards` (cookie viaja); `ServerReportsService` → `http://oroclash-api/api/reports/*` con `Bearer` del `HttpContext`. Forwarder catch-all `/bff/{**catch-all}` → `/api/{**catch-all}` ya existe (017) y cubre `GET /bff/reports/operational?from=&to=&category=&game=&player=&level=&result=&page=`.
- **Paginación**: `PagedResult<T>` (`Items`, `TotalCount`, `Page`, `PageSize`, `TotalPages`) para listas desglosadas; `ReportSnapshot` (single) para totales con `CalculatedAt`. Server-side `Specification` con `Skip`/`Take` + `Count`.
- **Autorización**: `ADMIN` → 12 métricas + 6 filtros; `GAME_MANAGER` → Games/Players/Questions/Categories/Answers/Scores/Withdrawals; `REWARD_MANAGER` → Rewards/Redemptions/Consolation; `PLAYER` → 403. `must_change_password` gating (VI).
- **Auditoría**: append-only opcional (`ReportViewAudit` con `ActorId`/`Filters`/`CorrelationId`) si política lo exige.

**Rationale**: Reutiliza patrón BFF de 017 (sample `BlazorWebAppOidcBffAutoYarpAspire`), evita exponer `access_token` y preserva `CorrelationId`.

**Alternatives considered**:
- WASM → API directo: rechazado — expone JWT.
- Sin paginación: rechazado — no escala con ≥10k juegos.

---

## R6. Agregación server-side y performance

**Decision**:
- Agregación en `Infrastructure` con `SpecificationEvaluator` + `GroupBy` + `ApplyAsNoTracking`; índices en `Game.CreatedAt`, `GamePlayer.PlayerId`, `PointTransaction.Timestamp`, `RewardRedemption.RequestedAt`.
- Snapshot `CalculatedAt` en `ReportSnapshot` para indicar freshness; caché corta (30s) o `IMemoryCache` en `Application` si ≥10k agregaciones.
- Skeleton en UI mientras `IsLoading`; `Empty` si `TotalCount==0`.

**Rationale**: SC-001 (<2s con skeleton) y SC-002 (paginación sin cargar colecciones) exigen agregación en DB, no en cliente. `BuildingBlocks` `Specification` ya optimiza queries.

**Alternatives considered**:
- Agregación en cliente: rechazado — carga masiva y viola V.
- Materialized view sin `Specification`: rechazado — rigidez.

---

## Consolidated Decisions Summary

| # | Decisión | Fuente |
|---|----------|--------|
| 1 | Operativo via `GetGames/Players/Questions` con `ByStatus`/`ByCategory` | FR-001..003 |
| 2 | Rendimiento via `Answer` + `PointTransaction` ledger (Correct/Incorrect/Scores/Withdrawals) | FR-004/005, D |
| 3 | Economía via `Reward`/`RewardRedemption` con `IsConsolation` separado | FR-006, C |
| 4 | 6 filtros combinados AND con validación `From<=To` y catálogos cerrados | FR-007..010 |
| 5 | BFF catch-all + `PagedResult`/`ReportSnapshot` + matriz 3 roles | FR-011/012, H/VI |
| 6 | Agregación server-side con `Specification` + `GroupBy` + índices | FR-016, SC-001 |

Sin NEEDS CLARIFICATION pendientes. Listo para Phase 1.
