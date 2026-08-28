# Feature Specification: Operational Reporting

**Feature Branch**: `015-operational-reporting`

**Created**: 2026-08-28

**Status**: Draft

**Input**: User description: "015 — Reporting Objetivo Definir consultas y reportes operativos sobre juegos, jugadores, preguntas y premios. Reportes mínimos Game Report Game Start End Players Rounds Winner Total questions Player Report Player Games played Games won Games lost Games withdrawn Questions answered Correct answers Accuracy Points earned Points redeemed Question Report Question Category Difficulty Times presented Correct answers Incorrect answers Accuracy Average response time Este último es especialmente interesante porque permite detectar preguntas demasiado fáciles/difíciles. Category Report Category Questions Games Players Average score Average accuracy Reward Report Reward Available stock Redemptions Points consumed Pending Delivered Leaderboard Debe soportar: Global Game Category Period Regla Reporting no debe modificar el dominio transaccional. Las consultas deben utilizar CQRS: IQuery<T> IQueryHandler<TQuery,TResult> y pueden utilizar Specifications cuando corresponda. Dependencias SPEC-004 SPEC-006 SPEC-007 SPEC-009 SPEC-014"

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Reporte de juego y leaderboard operativo (Priority: P1) 🎯 MVP

Como operador de juegos, quiero consultar un reporte consolidado de un juego específico y el leaderboard filtrado, de forma que pueda ver de un vistazo estado, participantes, rondas, ganador, y desempeño sin afectar la partida.

**Why this priority**: Es el reporte más consultado en operación diaria y el que valida que la lectura es pura (no muta dominio). Sin este, ningún otro reporte tiene contexto de partida. Entrega valor independiente como snapshot operativo.

**Independent Test**: Con 2–3 juegos en estados distintos (WAITING_FOR_PLAYERS, IN_PROGRESS, FINISHED, con 2–5 jugadores y 5 rondas cada uno), ejecutar `GameReport` por `gameId` y `Leaderboard` con filtros Global / Game / Category / Period. Verificar que el reporte retorna Game/Start/End/Players/Rounds/Winner/TotalQuestions correcto y que el leaderboard respeta el filtro aplicado, sin crear `PointTransaction` ni `AuditRecord` nuevo por la consulta.

**Acceptance Scenarios**:

1. **Given** un juego `FINISHED` con 4 jugadores y 5 rondas, **When** se solicita `GameReport` por ese `gameId`, **Then** se retorna `Game`, `Start`, `End`, lista `Players` (con estado), `Rounds` (c/ `RoundNumber` y `QuestionId`), `Winner` (determinado por ledger, no por campo cliente) y `TotalQuestions` (5).
2. **Given** varios juegos en el periodo 2026-08-01 a 2026-08-28, **When** se solicita `Leaderboard` con filtro `Period` = ese rango, **Then** el ranking solo incluye puntos obtenidos dentro del rango y excluye fuera de rango.
3. **Given** un `gameId` inexistente, **When** se solicita `GameReport`, **Then** se retorna error `GameNotFound` sin crear registro de auditoría de escritura.
4. **Given** un usuario con rol `PLAYER`, **When** solicita `GameReport` de su propio juego, **Then** lo obtiene; **When** solicita `Leaderboard` Global, **Then** también lo obtiene (lectura permitida por `Report.Read`).

---

### User Story 2 — Reporte de jugador (Priority: P1)

Como gestor de comunidad, quiero ver el histórico de un jugador (partidas jugadas/ganadas/perdidas/retiradas, preguntas respondidas/acertadas, accuracy, puntos ganados/canjeados) filtrado por periodo y por juego/categoría, de forma que pueda evaluar compromiso y equidad.

**Why this priority**: Junto con el reporte de juego forma el núcleo de analítica de usuario. Es P1 porque depende de datos ya existentes (SPEC-006/007) y no requiere nuevas escrituras. Independiente del reporte de juego: puede validarse solo con datos de `GamePlayer`/`Answer`/`PointTransaction`/`RewardRedemption`.

**Independent Test**: Con un jugador que ha jugado 5 partidas (2 ganadas, 1 perdida, 1 retirada, 1 en curso → 4 finalizadas computables), con 20 preguntas respondidas (14 correctas, 6 incorrectas, 350 puntos ganados, 100 canjeados), solicitar `PlayerReport` por `playerId` con filtro `Period` = último mes y sin filtro. Verificar los 10 campos calculados coinciden con reconstrucción desde ledger y no generan escrituras.

**Acceptance Scenarios**:

1. **Given** un jugador con 5 juegos (4 terminados, 1 retirado a mitad de partida), **When** se solicita `PlayerReport` sin filtros, **Then** `GamesPlayed=4`, `GamesWon=2`, `GamesLost=1`, `GamesWithdrawn=1`.
2. **Given** 20 respuestas (14 correctas), **When** se solicita `PlayerReport`, **Then** `QuestionsAnswered=20`, `CorrectAnswers=14`, `Accuracy=70%`, `PointsEarned=350`, `PointsRedeemed=100`.
3. **Given** filtro `Category` = "Historia", **When** se solicita `PlayerReport` con esa categoría, **Then** solo se computan juegos/preguntas de esa categoría.
4. **Given** filtro `Game` = un `gameId` específico, **When** se solicita `PlayerReport`, **Then** los contadores se limitan a esa partida.

---

### User Story 3 — Reporte de pregunta — detección de dificultad (Priority: P2)

Como responsable de contenido, quiero ver por cada pregunta cuántas veces se presentó, cuántos aciertos/fallos, accuracy y tiempo medio de respuesta, filtrado por juego/categoría/periodo, de forma que pueda detectar preguntas demasiado fáciles (accuracy >90%) o demasiado difíciles (<20%).

**Why this priority**: Es el reporte que cierra el loop de calidad de `QuestionBank` (SPEC-003). Es P2 porque requiere agregar datos de `Answer` + `GameRound` pero no bloquea los reportes de juego/jugador. Entrega valor independiente como herramienta de curaduría.

**Independent Test**: Crear una pregunta "Capital de Francia?" con 100 presentaciones (80 correctas, 20 incorrectas, tiempo medio 4.2s) y otra "Teorema X" con 100 presentaciones (15 correctas, 85 incorrectas, 12.1s). Solicitar `QuestionReport` por cada `questionId` y luego filtrado por `Category` y `Period`. Verificar `TimesPresented`, `CorrectAnswers`, `IncorrectAnswers`, `Accuracy`, `AverageResponseTime` y que el reporte marca la primera como "fácil" y la segunda como "difícil" según umbrales.

**Acceptance Scenarios**:

1. **Given** una pregunta con 100 presentaciones y 80 aciertos, **When** se solicita `QuestionReport` por `questionId`, **Then** `TimesPresented=100`, `CorrectAnswers=80`, `IncorrectAnswers=20`, `Accuracy=80%`, `AverageResponseTime` = promedio de `ElapsedTime` de `Answer`.
2. **Given** filtro `Category` = "Geografía", **When** se solicita `QuestionReport` agregado por categoría, **Then** solo se consideran presentaciones de esa categoría.
3. **Given** filtro `Period` = última semana, **When** se solicita `QuestionReport`, **Then** se excluyen presentaciones fuera de ventana.
4. **Given** una pregunta nunca presentada, **When** se solicita su reporte, **Then** `TimesPresented=0` y los promedios son `null`/`0` sin error.

---

### User Story 4 — Reporte de categoría y de recompensas (Priority: P2)

Como administrador de contenido y de recompensas, quiero ver por categoría (preguntas, juegos, jugadores, score medio, accuracy medio) y por recompensa (stock disponible, canjes, puntos consumidos, pendientes, entregados) de forma que pueda balancear oferta y demanda.

**Why this priority**: Cubre los dos dominios de catálogo restantes (SPEC-002/009). Es P2 porque depende de que los reportes base ya estén estabilizados, pero es independiente: puede probarse solo con datos de `Category`/`Reward`.

**Independent Test**: Con una categoría "Ciencia" que tiene 12 preguntas, 10 juegos y 25 jugadores únicos, calcular `CategoryReport` y verificar promedios. Con una recompensa "Voucher 100pts" con `Stock=50`, 20 canjes (12 entregados, 8 pendientes, 2000 puntos consumidos), verificar `RewardReport`. Ambos con filtro `Period`.

**Acceptance Scenarios**:

1. **Given** categoría "Ciencia" con 12 preguntas y 10 juegos en el último mes, **When** se solicita `CategoryReport` por `categoryId` y `Period` = mes, **Then** `Questions=12`, `Games=10`, `Players=25` (únicos), `AverageScore` y `AverageAccuracy` calculados desde ledger de esos juegos.
2. **Given** categoría sin juegos en el periodo, **When** se solicita `CategoryReport`, **Then** `Games=0`, `Players=0` y los promedios son `null`/`0`.
3. **Given** recompensa con `Stock=50` y 20 `RewardRedemption` (12 `DELIVERED`, 8 `PENDING`), **When** se solicita `RewardReport`, **Then** `AvailableStock=30`, `Redemptions=20`, `PointsConsumed=2000`, `Pending=8`, `Delivered=12`.
4. **Given** filtro `Period` fuera de rango de canjes, **When** se solicita `RewardReport`, **Then** `Redemptions=0` y `PointsConsumed=0`.

---

### User Story 5 — Filtros transversales y no-mutación (Priority: P2)

Como auditor, quiero que todos los reportes soporten los ejes `Global` / `Game` / `Category` / `Period` de forma combinable y que ninguna consulta de reporte modifique el dominio transaccional ni genere `PointTransaction`/`AuditRecord` de escritura, usando `IQuery` y `Specification` cuando corresponda.

**Why this priority**: Es la regla transversal que hace que los reportes sean seguros y componibles. Es P2 porque valida que la arquitectura de consulta es pura (CQRS) y que el filtrado es consistente en todos los reportes.

**Independent Test**: Ejecutar cada reporte con combinaciones: Global (sin filtros), Game (por `gameId`), Category (por `categoryId`), Period (`from`/`to`), y combinados (Game + Period, Category + Period). Para cada variante, contar `PointTransaction`, `AuditRecord` y `RewardRedemption` antes y después; verificar que no aumentan y que el `Result` es el mismo en dos ejecuciones consecutivas.

**Acceptance Scenarios**:

1. **Given** cualquier reporte, **When** se ejecuta dos veces con mismos filtros, **Then** ambos resultados son idénticos y no se crean registros nuevos.
2. **Given** filtros combinados `Category` + `Period`, **When** se solicita `Leaderboard` o `QuestionReport`, **Then** el resultado es la intersección correcta (solo datos de esa categoría dentro del periodo).
3. **Given** `Period` con `from` > `to`, **When** se solicita cualquier reporte, **Then** se retorna error de validación sin consultar dominio.
4. **Given** un `gameId` o `categoryId` inexistente, **When** se solicita reporte con ese filtro, **Then** se retorna reporte vacío (o `GameNotFound` para `GameReport` por `gameId` directo) sin crear datos.

---

### Edge Cases

- ¿Qué ocurre cuando no hay datos en el periodo/category/juego filtrado? Se retorna reporte vacío con ceros/`null` y `total=0`, no error, salvo `GameReport` por `gameId` inexistente que debe ser `NotFound`.
- ¿Qué ocurre cuando un juego aún no ha terminado y se pide `Winner`? `Winner` es `null` hasta `FINISHED`; `Start`/`End` reflejan `CreatedAt`/`FinishedAt` (`End` = `null` si en curso).
- ¿Qué ocurre cuando una pregunta fue presentada pero ninguna respuesta fue evaluada (timeout)? `TimesPresented` cuenta la presentación, `CorrectAnswers`/`IncorrectAnswers` usan solo respuestas evaluadas (`AnswerEvaluated`), `AverageResponseTime` usa solo evaluadas con `ElapsedTime`.
- ¿Qué ocurre cuando un jugador se retiró a mitad de partida? `GamesPlayed` cuenta solo juegos terminados donde participó; `GamesWithdrawn` incrementa; `QuestionsAnswered`/`CorrectAnswers` solo de rondas donde realmente respondió.
- ¿Qué ocurre cuando `PointsRedeemed` incluye canjes pendientes vs entregados? `PointsRedeemed` cuenta puntos efectivamente descontados del ledger (canjes `REQUESTED`/`DELIVERED` según SPEC-009), no solo entregados; se desglosa en `Pending` vs `Delivered` en `RewardReport`.
- ¿Qué ocurre cuando `Period` abarca parcialmente una partida larga? Solo se computan eventos (respuestas, puntos) cuyo `Timestamp` cae dentro del periodo; `Start`/`End` del juego fuera de periodo no excluyen sus puntos si los puntos caen dentro.
- ¿Qué ocurre cuando `Category` tiene preguntas nunca usadas? `TimesPresented` = 0 y no afecta promedios.
- ¿Qué ocurre cuando la consulta es muy amplia (Global sin filtros sobre 10k juegos)? La paginación y los índices por `GameId`/`Timestamp` evitan full scan; la API impone `pageSize` máximo y `from`/`to` requerido para Global si se configura.

## Requirements *(mandatory)*

### Functional Requirements

**Reportes mínimos y campos**

- **FR-001**: El sistema MUST exponer `GameReport` por `gameId` con campos: `Game` (id/nombre), `Start` (`CreatedAt`), `End` (`FinishedAt` o `null` si no terminado), `Players` (lista de participantes con estado), `Rounds` (lista con `RoundNumber`/`QuestionId`), `Winner` (`PlayerId`/`DisplayName` o `null` si no terminado), `TotalQuestions` (conteo de rondas).
- **FR-002**: El sistema MUST exponer `PlayerReport` por `playerId` con campos: `Player` (id), `GamesPlayed` (juegos terminados donde participó), `GamesWon`, `GamesLost`, `GamesWithdrawn`, `QuestionsAnswered`, `CorrectAnswers`, `Accuracy` (`Correct/Answered`), `PointsEarned` (suma `PointTransaction` tipo `ANSWER_CORRECT`/`ROUND_BONUS` etc.), `PointsRedeemed` (suma `RewardRedemption` descontados), filtrado por `Game`/`Category`/`Period` cuando se indique.
- **FR-003**: El sistema MUST exponer `QuestionReport` por `questionId` (y agregado por categoría) con campos: `Question` (id), `Category` (id/nombre), `Difficulty` (nivel), `TimesPresented` (conteo de rondas donde se presentó), `CorrectAnswers`, `IncorrectAnswers`, `Accuracy`, `AverageResponseTime` (promedio `ElapsedTime` de respuestas evaluadas), con desglose que permita identificar preguntas fáciles/difíciles.
- **FR-004**: El sistema MUST exponer `CategoryReport` por `categoryId` con campos: `Category` (id/nombre), `Questions` (conteo), `Games` (juegos que usaron la categoría en periodo), `Players` (jugadores únicos en esos juegos), `AverageScore` (promedio puntos por jugador-juego), `AverageAccuracy` (promedio accuracy de respuestas evaluadas), filtrado por `Period`.
- **FR-005**: El sistema MUST exponer `RewardReport` por `rewardId` (y listado global) con campos: `Reward` (id/nombre), `AvailableStock` (`Stock` − `Redemptions`), `Redemptions` (conteo total), `PointsConsumed` (suma puntos descontados), `Pending` (conteo `PENDING`/`REQUESTED`), `Delivered` (conteo `DELIVERED`), filtrado por `Period` y por `Category` cuando la recompensa esté vinculada.
- **FR-006**: El sistema MUST exponer `Leaderboard` que soporte ejes `Global` (todos los juegos), `Game` (un `gameId`), `Category` (una `categoryId`), `Period` (`from`/`to`) y combinaciones (`Game`+`Period`, `Category`+`Period`), ordenado por puntos y desempates deterministas (SPEC-011), sin duplicar lógica de ranking.

**Filtros y regla de no-mutación**

- **FR-007**: Todos los reportes MUST soportar filtros combinables `Global` (sin filtros), `Game` (`gameId`), `Category` (`categoryId`), `Period` (`from`/`to` en UTC). La ausencia de un filtro significa no filtrar por ese eje; `from`/`to` validan `from` ≤ `to`.
- **FR-008**: Ningún reporte MUST modificar el dominio transaccional: la ejecución de un `IQuery` no debe crear `PointTransaction`, `Answer`, `RewardRedemption`, `AuditRecord` de escritura ni ningún `DomainEvent`; solo lectura. Dos ejecuciones idénticas MUST retornar resultado idéntico sin side-effects.
- **FR-009**: Todas las consultas de reporte MUST implementarse como `IQuery<T>` con `IQueryHandler<TQuery,TResult>` y MAY usar `Specification<T>` para filtrado/paginación/orden, manteniendo el patrón Vertical Slice (`Query` + `Handler` + `Response DTO` + `Endpoint`).
- **FR-010**: Para `QuestionReport`, `TimesPresented` MUST contar presentaciones (`GameRound` con `QuestionId`), mientras `CorrectAnswers`/`IncorrectAnswers`/`AverageResponseTime` MUST usar solo respuestas `Evaluated` (`AnswerEvaluated`), no `Submitted` sin evaluar.

**Dependencias y trazabilidad**

- **FR-011**: `GameReport` y `Leaderboard` MUST reutilizar la reconstrucción desde `PointTransaction` ledger (SPEC-007) y `Answer` evaluadas (SPEC-006), no recalcular desde campos cliente.
- **FR-012**: Cuando un reporte se filtra por `Period`, el sistema MUST considerar solo eventos cuyo `Timestamp` de servidor cae dentro de `[from, to]` (ej. `AnswerEvaluated.Timestamp`, `PointTransaction.CreatedAt`, `RewardRedemption.RequestedAt`).
- **FR-013**: Todo conteo de `GamesPlayed`/`GamesWon`/`GamesLost` MUST basarse en `Game.Status` terminal (`FINISHED`) y en `PointTransaction`/`Winner` derivado, no en estado cliente.
- **FR-014**: Cada endpoint de reporte MUST requerir autenticación y `Report.Read` (o `Audit.Read` para trazas) y respetar `Audit.Read` transversal de SPEC-014 para trazabilidad, sin exponer `IsCorrect` previo ni secretos en `Data`.

### Key Entities

- **GameReport**: Snapshot de un juego. Atributos: `GameId`, `Name`, `Start` (`CreatedAt`), `End` (`FinishedAt`/`null`), `Players` (id + estado `ACTIVE`/`WITHDRAWN`/`ELIMINATED`/`WINNER`), `Rounds` (id, `RoundNumber`, `QuestionId`), `Winner` (`PlayerId`/`DisplayName`/`null`), `TotalQuestions`/`TotalRounds`.
- **PlayerReport**: Agregado por jugador. Atributos: `PlayerId`, `GamesPlayed`, `GamesWon`, `GamesLost`, `GamesWithdrawn`, `QuestionsAnswered`, `CorrectAnswers`, `Accuracy` (0–100%), `PointsEarned`, `PointsRedeemed`.
- **QuestionReport**: Métrica por pregunta. Atributos: `QuestionId`, `CategoryId`/`CategoryName`, `Difficulty`, `TimesPresented`, `CorrectAnswers`, `IncorrectAnswers`, `Accuracy`, `AverageResponseTime` (`TimeSpan`/`double` segundos).
- **CategoryReport**: Agregado por categoría. Atributos: `CategoryId`, `CategoryName`, `Questions` (count), `Games` (count), `Players` (unique count), `AverageScore`, `AverageAccuracy`.
- **RewardReport**: Agregado por recompensa. Atributos: `RewardId`, `RewardName`, `AvailableStock`, `Redemptions` (count), `PointsConsumed` (sum), `Pending` (count), `Delivered` (count).
- **Leaderboard**: Ranking ya existente (SPEC-011) extendido con filtros `Global`/`Game`/`Category`/`Period`; no es nueva entidad, reutiliza `LeaderboardEntry` (`PlayerId`, `Rank`, `Points`, `CorrectAnswers`, `CurrentLevel`, `Status`, `SecuredPoints`).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Para un juego `FINISHED` con 4 jugadores y 5 rondas, `GameReport` retorna `Game`/`Start`/`End`/`Players`/`Rounds`/`Winner`/`TotalQuestions` 100% correcto vs. reconstrucción desde `Game`/`GameRound`/`PointTransaction`.
- **SC-002**: Para un jugador con historial de 5 juegos (2 ganadas,1 perdida,1 retirada,1 en curso), `PlayerReport` retorna `GamesPlayed=4`, `GamesWon=2`, `GamesLost=1`, `GamesWithdrawn=1`, con `QuestionsAnswered`/`CorrectAnswers`/`Accuracy`/`PointsEarned`/`PointsRedeemed` exactos vs. ledger.
- **SC-003**: Para una pregunta con 100 presentaciones y 80 aciertos, `QuestionReport` retorna `TimesPresented=100`, `CorrectAnswers=80`, `IncorrectAnswers=20`, `Accuracy=80%`, `AverageResponseTime` = promedio real de `ElapsedTime` con error <1%.
- **SC-004**: Con filtros `Global`, `Game`, `Category`, `Period` y combinaciones (`Category`+`Period`, `Game`+`Period`), el resultado de cada reporte es la intersección correcta (solo datos dentro del filtro) en 100% de los casos, y sin filtros es el global.
- **SC-005**: Dos ejecuciones consecutivas del mismo reporte con idénticos filtros retornan resultado idéntico y no incrementan `PointTransaction`/`AuditEntry`/`RewardRedemption` (0 side-effects).
- **SC-006**: Toda consulta de reporte implementada como `IQuery<T>` y, cuando filtra, usa `Specification<T>` (verificable por inspección de `IQueryHandler` y `Specification`).
- **SC-007**: Para una categoría con 12 preguntas y 10 juegos en el último mes, `CategoryReport` retorna `Questions=12`, `Games=10`, `Players=25` únicos, `AverageScore` y `AverageAccuracy` dentro de 1% del cálculo manual desde ledger.
- **SC-008**: Para una recompensa con `Stock=50` y 20 canjes (12 `DELIVERED`, 8 `PENDING`, 2000 puntos), `RewardReport` retorna `AvailableStock=30`, `Redemptions=20`, `PointsConsumed=2000`, `Pending=8`, `Delivered=12` exactos.
- **SC-009**: `Leaderboard` `Global`/`Game`/`Category`/`Period` respeta orden determinista (puntos desc, correctas desc, timestamp) y coincide 100% con el ranking ya auditado, sin recalcular desde cliente.

## Assumptions

- Se reutilizan agregados y ledger existentes: `Game`/`GamePlayer`/`GameRound`/`Answer`/`PointTransaction` (SPEC-004/006/007), `Question`/`Category` (SPEC-003), `Reward`/`RewardRedemption` (SPEC-009), `AuditEntry` (SPEC-014) para trazabilidad opcional.
- Los reportes son solo lectura: no requieren nuevas tablas ni migraciones, solo `IQuery` + `Specification` + índices existentes (`GameId`, `Timestamp`, `CorrelationId` de SPEC-014).
- `Player` se identifica por `PlayerId` (`sub` claim de OroIdentityServer) como en SPEC-011/013; no se crea entidad `User` local.
- `Accuracy` = `CorrectAnswers` / `QuestionsAnswered` × 100, con `null`/`0` si `QuestionsAnswered=0`.
- `AverageResponseTime` promedia solo respuestas `Evaluated` con `ElapsedTime` no nulo; presentaciones sin evaluación solo cuentan en `TimesPresented`.
- `Period` es ventana UTC `[from, to]` inclusive, con `from` ≤ `to`; si no se provee, se asume Global sin límite temporal.
- `Winner` de `GameReport` se deriva del `Leaderboard` final (rank 1) cuando `Game.Status == FINISHED`; si no terminado, `Winner = null`.
- `Leaderboard` ya existe por SPEC-011; este SPEC lo extiende con filtros `Global`/`Category`/`Period` sin duplicar lógica.
- Autenticación y `Report.Read`/`Audit.Read` permanecen delegados a OroIdentityServer (Constitución VI); sin permiso, reporte es 403.
- Alcance single-node inicial; paginación con `page`/`pageSize` y `total` evita full scan para SC de volumen moderado.
- Reporting no reemplaza `RewardReport` transaccional ni `PointTransaction`; lo complementa con lectura agregada.

