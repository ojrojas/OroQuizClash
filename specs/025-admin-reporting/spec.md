# Feature Specification: Admin Reporting

**Feature Branch**: `025-admin-reporting`

**Created**: 2026-05-13

**Status**: Draft

**Input**: User description: "025 — Admin Reporting Objetivo Proporcionar información analítica sobre el funcionamiento del juego. Descripción Los reportes deberán incluir: Games Games Players Questions Categories Answers Correct Answers Incorrect Answers Scores Withdrawals Rewards Redemptions Consolation Rewards Deberá permitir filtros por: Fecha. Categoría. Juego. Jugador. Nivel. Resultado."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Consultar reportes operativos (Games, Players, Questions, Categories) (Priority: P1)

Como administrador (ADMIN) autenticado, quiero consultar reportes analíticos operativos — totales y tendencias de juegos, jugadores, preguntas y categorías — filtrados por fecha, categoría y juego, para entender volumen y salud operativa.

**Why this priority**: Es la base de 025 — sin visión operativa de juegos/jugadores/preguntas/categorías no hay reporting. Es el MVP de analytics y desbloquea decisiones de contenido y capacidad.

**Independent Test**: Login ADMIN → `/admin/reports` → sección “Operativo” → verificar métricas Games (totales por estado), Players (participantes únicos, activos), Questions (totales, por categoría/nivel), Categories (uso por juegos) con filtros Fecha (Desde/Hasta) + Categoría + Juego aplicados y paginación <2s.

**Acceptance Scenarios**:

1. **Given** un ADMIN en `/admin/reports`, **When** selecciona rango de fechas (últimos 30 días) sin otros filtros, **Then** el sistema muestra conteos de Games (por estado `FINISHED`/`CANCELLED`/etc.), Players únicos, Questions totales y Categories activas, con gráficos/tablas y totales, en <2s percibidos con skeleton.
2. **Given** el reporte operativo cargado, **When** filtra por Categoría “Historia” y Juego específico, **Then** todas las métricas (Games/Players/Questions/Categories) se recalculan server-side para ese subconjunto, con `TotalCount` y desglose, sin cargar colecciones completas.
3. **Given** un rango sin datos (ej. futuro), **When** aplica filtro de Fecha, **Then** el sistema muestra estado `Empty` con mensaje “Sin datos para el rango” sin error, y permite resetear filtros.
4. **Given** un GAME_MANAGER autenticado, **When** abre `/admin/reports` operativo, **Then** tiene acceso; **Given** un usuario sin rol administrativo, **When** intenta acceder por URL directa o API, **Then** recibe 403/`Access Denied` sin fuga.

---

### User Story 2 - Analizar rendimiento de juego (Answers, Correct/Incorrect, Scores, Withdrawals, Nivel, Resultado) (Priority: P1)

Como operador (ADMIN o GAME_MANAGER), quiero analizar rendimiento — respuestas totales, aciertos/fallos, puntuaciones, retiros — filtrado por Fecha, Nivel, Resultado, Categoría, Juego y Jugador, para evaluar dificultad y comportamiento.

**Why this priority**: Co-prioritario con US1 — el reporting sin calidad de juego (aciertos, scores, withdrawals) es solo conteo. Este slice aporta insight pedagógico y de balance.

**Independent Test**: Desde `/admin/reports` → pestaña “Rendimiento” → verificar métricas Answers (totales), Correct/Incorrect (conteo y tasa), Scores (promedio, distribución, total ledger), Withdrawals (totales y tasa), con filtros Nivel (1–5), Resultado (ganado/perdido/retirado), Jugador, Fecha y Juego/Categoría combinados, paginados <2s.

**Acceptance Scenarios**:

1. **Given** un operador en “Rendimiento”, **When** filtra por Nivel 3 y Resultado “Correct”, **Then** el sistema muestra Answers filtradas, Correct Answers vs Incorrect Answers con tasa (`correct/total`), y Scores promedio para ese nivel, calculados server-side.
2. **Given** el reporte de rendimiento, **When** filtra por Jugador específico y rango de fechas, **Then** ve métricas solo de ese jugador (respuestas, aciertos, puntuación total desde `PointTransaction`, retiros), con desglose por juego/categoría.
3. **Given** un juego con withdrawals, **When** consulta retiros con filtro Resultado “Withdrawn”, **Then** ve conteo de Withdrawals y lista paginada con juego, jugador, momento y política aplicada (`LOSE_ALL` etc.).
4. **Given** un filtro con Nivel fuera de rango (ej. 99) o Fecha `Desde > Hasta`, **When** aplica, **Then** el sistema muestra validación por campo con mensaje accionable y no hace petición.

---

### User Story 3 - Analizar economía de recompensas (Rewards, Redemptions, Consolation) (Priority: P2)

Como administrador (ADMIN o REWARD_MANAGER), quiero analizar la economía — recompensas disponibles, canjes y consolaciones — filtrada por Fecha, Categoría, Juego, Jugador, Nivel y Resultado, para control de stock, costes y auditoría.

**Why this priority**: Eleva el reporting de operativo/rendimiento a financiero — sin economía no hay control de Points ledger ni de `IsConsolation`. Es P2 porque depende de datos de recompensas (009) y el valor base ya se entregó.

**Independent Test**: Desde `/admin/reports` → pestaña “Recompensas” → verificar métricas Rewards (totales por tipo/estado), Redemptions (totales por estado `Requested→Delivered`, coste en puntos), Consolation Rewards (conteo y coste separado con `IsConsolation:true`), con filtros Fecha/Categoría/Juego/Jugador/Nivel/Resultado y paginación <2s. Verificar que Consolation no se cuenta como premio normal.

**Acceptance Scenarios**:

1. **Given** un operador en “Recompensas”, **When** filtra por Fecha (últimos 7 días) y Categoría, **Then** ve conteo de Rewards por tipo (6 tipos) y estado (`Active`/`Inactive`/`Archived`), y Redemptions por estado, con coste total en puntos.
2. **Given** el mismo reporte, **When** filtra por Jugador, **Then** ve solo los canjes de ese jugador, con `IsConsolation` distinguido (badge “Consolación”) y no sumado en métricas de premio normal.
3. **Given** un filtro por Nivel y Resultado (ej. Nivel 2 + Resultado `Approved`), **When** aplica, **Then** el sistema filtra Redemptions cuyo juego/categoría coincide con ese nivel/resultado, con totales coherentes con el ledger `REWARD_REDEMPTION`/`CONSOLATION`.
4. **Given** un REWARD_MANAGER autenticado, **When** abre “Recompensas”, **Then** tiene acceso; **Given** un GAME_MANAGER sin permiso de recompensas intenta acceder a esa pestaña por API directa, **Then** recibe 403 sin fuga (según matriz FR-011).

---

### Edge Cases

- ¿Qué ocurre si no hay datos para los filtros (ej. Fecha futura, Categoría sin juegos)? Mostrar `Empty` con total 0 y gráfico vacío, sin error 500, con opción de limpiar filtros.
- ¿Qué ocurre si el rango de fechas es muy amplio (≥1 año, ≥10k juegos)? El sistema pagina server-side y agrega en DB (no carga colecciones completas); filtros deben seguir <2s con skeleton y `TotalCount`.
- ¿Qué ocurre si OroIdentityServer no disponible o sesión expiró mientras se consulta? BFF retorna 401, UI muestra “Sesión expirada — re-autenticar” y preserva filtros sin pérdida de estado local.
- ¿Qué ocurre si dos operadores consultan el mismo reporte con filtros distintos simultáneamente? Lecturas idempotentes, cada una con su `CorrelationId`; sin interferencia ni cache stale más allá de ventana de snapshot.
- ¿Qué ocurre si `Consolation` y `RewardRedemption` comparten mismo `RewardId`? `IsConsolation:true` lo distingue y no se suma en métricas de premio normal (Constitución C).
- ¿Qué ocurre si se filtra por Nivel 0 o 6 (fuera de 1–5)? Validación por campo 1–5 con mensaje accionable, sin petición.
- ¿Qué ocurre si `Resultado` es ambiguo (ej. `FINISHED` vs `CANCELLED` vs `WITHDRAWN`)? El catálogo de resultados es cerrado (ver FR-005) y se valida contra él.

## Requirements *(mandatory)*

### Functional Requirements

**Métricas — 12 tipos**

- **FR-001**: El sistema MUST proporcionar métricas de **Games** (conteos totales y por estado: `DRAFT`/`READY`/`WAITING_FOR_PLAYERS`/`IN_PROGRESS`/`ROUND_IN_PROGRESS`/`ROUND_COMPLETED`/`FINISHED`/`CANCELLED`/`FORCED_FINISHED`), calculadas server-side con filtros aplicados.
- **FR-002**: El sistema MUST proporcionar métricas de **Players** (participantes únicos, activos en rango, distribución por tenant) derivadas de `GamePlayer`/`Player` (`sub`).
- **FR-003**: El sistema MUST proporcionar métricas de **Questions** (totales, por estado, por categoría/nivel) y **Categories** (uso por juegos, preguntas por categoría), derivadas de `Question`/`Category` (≥5 preguntas para activar).
- **FR-004**: El sistema MUST proporcionar métricas de **Answers** (respuestas totales), **Correct Answers** (aciertos), **Incorrect Answers** (fallos) y tasa de aciertos (`correct/total`), derivadas de `Answer`/`PointTransaction` (`ANSWER_CORRECT`/`ANSWER_INCORRECT`).
- **FR-005**: El sistema MUST proporcionar métricas de **Scores** (puntuación total, promedio por juego/jugador, distribución, reconstruida desde `PointTransaction` ledger, tipos `ANSWER_CORRECT`, `ROUND_BONUS`, `GAME_BONUS`, `PENALTY`, etc.) y **Withdrawals** (conteo, tasa, política aplicada `LOSE_ALL` etc.), paginadas y sin cálculo en cliente.
- **FR-006**: El sistema MUST proporcionar métricas de **Rewards** (totales por tipo 6 y estado 3), **Redemptions** (totales por estado 5 y coste en puntos) y **Consolation Rewards** (conteo y coste con `IsConsolation:true`, no contado como premio normal, consistente con `ConsolationEligibility`).

**Filtros — 6 dimensiones**

- **FR-007**: El sistema MUST permitir filtrar **todos** los reportes por **Fecha** (rango `Desde`/`Hasta` con `Desde <= Hasta`, opcionales) y validar por campo sin petición si inválido.
- **FR-008**: El sistema MUST permitir filtrar por **Categoría** (id o nombre, existente en catálogo) y por **Juego** (id o nombre parcial), aplicados server-side vía `Specification`.
- **FR-009**: El sistema MUST permitir filtrar por **Jugador** (`sub`/`PlayerId` o búsqueda parcial por nombre/email, case-insensitive) y por **Nivel** (1–5, con validación 1–5).
- **FR-010**: El sistema MUST permitir filtrar por **Resultado** (catálogo cerrado derivado de `GameStatus`/`ParticipationState`/`RedemptionStatus`: `FINISHED`, `CANCELLED`, `WITHDRAWN`, `Approved`, `Rejected`, etc., según métrica) y combinarlo con los otros 5 filtros de forma AND.

**Paginación, autorización y presentación**

- **FR-011**: El sistema MUST paginar server-side todas las listas de reporte (`page`/`pageSize`, default 20, max 100, con `TotalCount`/`TotalPages`) y no cargar colecciones completas; MUST mostrar estados `Loading` (skeleton), `Empty`, `Error` (retry) y `Ready` por sección.
- **FR-012**: El sistema MUST aplicar autorización por rol vía OroIdentityServer (OIDC `authorization_code` + `refresh_token`): `ADMIN` acceso completo a los 12 métricas y 6 filtros; `GAME_MANAGER` acceso a Games/Players/Questions/Categories/Answers/Scores/Withdrawals con filtros Fecha/Categoría/Juego/Jugador/Nivel/Resultado; `REWARD_MANAGER` acceso a Rewards/Redemptions/Consolation con filtros Fecha/Categoría/Juego/Jugador; cualquier rol no autorizado (incl. `PLAYER`) recibe `403 Forbidden` por API y `Access Denied` en UI sin fuga. `must_change_password` gating antes de consultar.
- **FR-013**: El sistema MUST validar en tres niveles: API (contrato — tipos, rangos, paginación, `From<=To`, Nivel 1–5, catálogos cerrados), Aplicación (requisitos — coherencia de filtros combinados, existencia de categoría/juego/jugador) y Dominio (invariantes — `GameNotFound`/`CategoryNotFound` mapeados a 404/200 vacío según política, sin exponer detalles). Invariantes MUST NOT depender solo de UI.
- **FR-014**: El sistema MUST consumir exclusivamente la API/BFF (`QuizArena.Api` vía `QuizArena.Admin` BFF YARP) para todos los datos de reporte; MUST NOT acceder directamente a SQL Server/Oracle/`identitydb`; MUST propagar `CorrelationId` y mapear errores a `ProblemDetails` RFC 7807 sin fuga interna.
- **FR-015**: El sistema MUST reutilizar shell de navegación, tema `administration`, tokens y componentes del Design System (SPEC-016) sin valores hardcodeados, residir en `src/Admin/QuizArena.Admin` (Blazor Auto `net10.0`) y `src/Admin/QuizArena.Admin.Client`, y MUST exigir sesión válida vía OroIdentityServer.
- **FR-016**: El sistema MUST calcular todas las métricas server-side (agregaciones en `Application`/`Infrastructure` con `Specification` y `ApplyAsNoTracking`) sin cálculo en cliente, y MUST registrar auditoría de consultas de reporte cuando sea requerido (actor `sub`, filtros, `CorrelationId`).

### Key Entities *(include if feature involves data)*

- **ReportSnapshot**: Snapshot agregado de métricas para un conjunto de filtros. Atributos: `Filters` (Fecha, Categoría, Juego, Jugador, Nivel, Resultado, `Page`/`PageSize`), `Metrics` (12 tipos), `TotalCount`, `CalculatedAt`. No persistido, calculado server-side.
- **GameMetric**: Métrica de juegos. `TotalGames`, `ByStatus` (map 9 estados → conteo), `CreatedAt` range.
- **PlayerMetric**: `UniquePlayers`, `ActivePlayers`, `DistributionByTenant`. Derivado de `GamePlayer`.
- **QuestionMetric / CategoryMetric**: `TotalQuestions`, `ByCategory`/`ByLevel`, `CategoriesInUse`. Derivado de `Question`/`Category`.
- **AnswerMetric**: `TotalAnswers`, `CorrectAnswers`, `IncorrectAnswers`, `AccuracyRate` (correct/total). Derivado de `Answer` + `PointTransaction` (`ANSWER_CORRECT`/`ANSWER_INCORRECT`).
- **ScoreMetric**: `TotalPoints`, `AverageScore`, `Distribution` (histograma), `ByTransactionType` (10 tipos). Reconstruido desde `PointTransaction` ledger (Constitución D).
- **WithdrawalMetric**: `TotalWithdrawals`, `ByPolicy` (`LOSE_ALL` etc.), `Rate` (withdrawals/games). Derivado de `GamePlayer` (`WITHDRAWN`).
- **RewardMetric / RedemptionMetric**: `TotalRewards` (6 tipos, 3 estados), `TotalRedemptions` (5 estados), `TotalCost` (puntos), `ByStatus`/`ByType`. Derivado de `Reward`/`RewardRedemption`.
- **ConsolationMetric**: `TotalConsolations`, `TotalCostConsolation`, `ByEligibility`. `IsConsolation:true` separado (Constitución C).
- **ReportFilter**: Filtros combinados `Fecha (Desde/Hasta)`, `Categoría`, `Juego`, `Jugador`, `Nivel (1–5)`, `Resultado` (catálogo cerrado), `Page`/`PageSize`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Un ADMIN abre `/admin/reports` y ve el reporte operativo (Games/Players/Questions/Categories) con totales correctos para un rango de 30 días en <2s percibidos con skeleton en el 90% de los intentos.
- **SC-002**: El 100% de las consultas con filtros combinados (Fecha + Categoría + Juego) retornan métricas consistentes server-side y paginan sin cargar colecciones completas, con `TotalCount` correcto.
- **SC-003**: El 100% de las métricas de rendimiento (Answers/Correct/Incorrect/Scores/Withdrawals) filtradas por Nivel (1–5) y Resultado muestran tasa de aciertos y promedio de puntos correctos, con desglose por `PointTransaction`.
- **SC-004**: El 100% de los reportes de recompensas distinguen `Consolation` (`IsConsolation:true`) y no lo cuentan como premio normal, con coste total en puntos coincidente con el ledger `REWARD_REDEMPTION`/`CONSOLATION`.
- **SC-005**: El 100% de los filtros con `Desde > Hasta` o Nivel fuera de 1–5 son rechazados por validación por campo sin petición, con mensaje accionable.
- **SC-006**: Un operador completa la tarea “filtrar por Fecha + Categoría + Jugador → ver Games → ver Answers/Correct → ver Scores → ver Redemptions” en menos de 2 minutos sin ayuda externa en el 95% de los intentos.
- **SC-007**: La autorización se respeta en el 100% de los casos: `GAME_MANAGER` ve operativo/rendimiento, `REWARD_MANAGER` ve recompensas, `PLAYER` recibe `Access Denied`/`403` sin fuga.
- **SC-008**: El 100% de los errores se presentan como `ProblemDetails` RFC 7807 sin fuga, con `CorrelationId` propagado y estados `Loading`/`Empty`/`Error` por sección, y rango sin datos muestra `Empty` sin duplicados.
- **SC-009**: La UI de reportes cumple WCAG 2.2 AA en tema `administration` (contraste, foco visible, teclado, `aria-live`) y es utilizable entre 375 y 1536px sin scroll horizontal y con objetivos ≥44px, con tokens del Design System sin literales.

## Assumptions

- **Reutiliza SPEC-017/016/009 y 024**: La app Blazor `net10.0` Auto, shell, BFF YARP, OIDC y agregados `Game`/`GamePlayer`/`Question`/`Category`/`PointTransaction`/`Reward`/`RewardRedemption` ya existen (009 con ledger, 024 con `Player` queries). 025 es solo lectura analítica, sin crear nueva app ni duplicar agregados.
- **Solo lectura en v1**: Reporting es consulta y agregación; no crea/edita juegos/jugadores/premios; no exporta a CSV/PDF en v1 (si se necesita, será extensión).
- **Fuente de verdad**: Métricas calculadas server-side en `oroclash-api` (SQL Server primario, abstracción Oracle, `rowversion` donde aplica, `PointTransaction` ledger para Scores/Withdrawals/Consolation); OroIdentityServer aporta `sub`/`tenant_id` para Players. Admin nunca toca DB directamente.
- **Catálogos cerrados**: Nivel 1–5 (`PlayerCatalogs.TransactionTypes` 10, `RewardTypes` 6, `GameStatuses` 9, `RedemptionStatuses` 5) son invariantes de dominio; valores fuera de catálogo → 400. Resultado se mapea a esos catálogos según métrica (ej. `FINISHED` para Games, `Approved` para Redemptions).
- **Filtros combinados AND**: Los 6 filtros se aplican como AND server-side vía `Specification` (`Where` + `And`); paginación `page` 1..N, `pageSize` 1..100, default 20; búsqueda parcial case-insensitive para Juego/Jugador/Categoría.
- **Consolación independiente**: `Consolation` es tipo independiente (Constitución C) con `IsConsolation:true`; métricas de recompensas lo separan y no lo suman en premios normales.
- **Matriz de permisos v1**: `ADMIN` → 12 métricas + 6 filtros; `GAME_MANAGER` → Games/Players/Questions/Categories/Answers/Scores/Withdrawals; `REWARD_MANAGER` → Rewards/Redemptions/Consolation; `PLAYER` → 403 en `/admin/reports`. Si la política final difiere, se ajusta en Plan sin cambiar scope.
- **Idioma**: Español para etiquetas, coherente con SPEC-017/024, sin i18n en v1.
- **Sin acceso directo a datos**: Todo vía BFF; no lectura directa a SQL Server, Oracle ni `identitydb` (Constitución H).
