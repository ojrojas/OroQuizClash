# Feature Specification: Admin Game Operations

**Feature Branch**: `022-admin-game-operations`

**Created**: 2026-08-28

**Status**: Draft

**Input**: User description: "022 — Admin Game Operations Objetivo Permitir al operador supervisar y controlar partidas en ejecución. Descripción El administrador podrá visualizar: Game Status, Current Round, Current Question, Players, Players Connected, Players Answered, Players Waiting, Scores, Current Level, Game Timer. Deberá existir una vista de juego en vivo. Acciones administrativas controladas: Pause, Resume, Cancel, Force Finish. Las operaciones privilegiadas deberán quedar registradas mediante auditoría."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Supervisar el estado vivo de una partida (Priority: P1)

Como operador (ADMIN o GAME_MANAGER) autenticado, quiero abrir la vista de juego en vivo y ver en tiempo casi real los 10 indicadores operativos (Game Status, Current Round, Current Question, Players, Players Connected, Players Answered, Players Waiting, Scores, Current Level, Game Timer) para tomar decisiones informadas durante la ejecución.

**Why this priority**: Es el núcleo de 022 — sin supervisión no hay control. Requiere lectura consistente del motor autoritativo y actualización sin recarga completa. Constituye el MVP estricto.

**Independent Test**: Iniciar un juego (Running) con 2 jugadores conectados → abrir `/admin/live/{gameId}` → verificar que los 10 indicadores muestran valores coherentes con el backend (Status=Running, Round N, Question con 4 opciones, conteos de jugadores por estado, scores, level, timer) y que se actualizan sin recarga manual. No requiere ejecutar acciones de control.

**Acceptance Scenarios**:

1. **Given** un juego en `Running` con 1 ronda activa y pregunta disponible, **When** el operador abre la vista en vivo, **Then** ve `Game Status: Running`, `Current Round: 1`, `Current Question` con texto y 4 opciones, `Players: 5`, `Players Connected: 3`, `Players Answered: 2`, `Players Waiting: 1`, `Scores` (tabla con puntos por jugador), `Current Level: 2`, `Game Timer` (cuenta regresiva por pregunta).
2. **Given** la vista en vivo abierta, **When** un jugador responde correctamente, **Then** los contadores `Players Answered`/`Waiting` y `Scores` se actualizan en ≤3s sin recarga completa (polling o push), manteniendo `Current Question` sin parpadeo.
3. **Given** un juego sin jugadores conectados, **When** se abre la vista, **Then** `Players Connected: 0` con estado Vacío informativo (no error) y `Players Waiting` muestra 0.
4. **Given** un juego en `Paused`, **When** se abre la vista, **Then** `Game Timer` muestra pausa (congelado) y `Game Status: Paused` con badge destacado.
5. **Given** un REWARD_MANAGER (sin permiso `Game.Operate`), **When** intenta acceder a la vista en vivo, **Then** ve `Access Denied` y no recibe datos de jugadores/scores.

---

### User Story 2 - Controlar el ciclo de vida en ejecución con auditoría (Priority: P1)

Como operador autorizado, quiero ejecutar acciones controladas `Pause`, `Resume`, `Cancel` y `Force Finish` sobre una partida en ejecución, con confirmación y registro de auditoría, para intervenir de forma segura ante incidencias o condiciones de operación.

**Why this priority**: El objetivo exige explícitamente 4 acciones privilegiadas con auditoría. Sin control, la supervisión es solo lectura. Co-prioritario con US1 para valor operacional.

**Independent Test**: Con un juego en `Running` → ejecutar `Pause` → verificar `Paused` y `Game Timer` congelado y entrada de auditoría con actor/timestamp; `Resume` → `Running`; `Cancel` y `Force Finish` → estados terminales `Cancelled`/`Finished` con auditoría y sin permitir más respuestas. No requiere que todos los 10 indicadores estén perfectos.

**Acceptance Scenarios**:

1. **Given** un juego en `Running`, **When** el operador ejecuta `Pause` y confirma, **Then** el juego transita a `Paused`, el timer se congela server-side, se bloquea envío de respuestas y se crea registro de auditoría `Pause` con `ActorId`, `Timestamp`, `GameId`, `From: Running → Paused`, `CorrelationId`.
2. **Given** un juego en `Paused`, **When** ejecuta `Resume` y confirma, **Then** transita a `Running`, el timer se reanuda y se audita `Resume`.
3. **Given** un juego en `Running` o `Paused`, **When** ejecuta `Cancel` con confirmación y motivo opcional, **Then** transita a `Cancelled` (terminal), se notifica a jugadores y se audita `Cancel` con motivo.
4. **Given** un juego en `Running` o `Paused` que no puede finalizar normalmente (p. ej., ronda atascada), **When** ejecuta `Force Finish`, **Then** transita a `Finished` (o `ForcedFinished`) de forma forzada, se audita `ForceFinish` con marca `privileged` y se impide `Resume`.
5. **Given** un intento de `Pause` sobre un juego en `Finished`/`Cancelled`, **When** se ejecuta, **Then** el sistema rechaza con `InvalidGameState` y no muta ni audita la transición.
6. **Given** un REWARD_MANAGER intenta `Force Finish`, **When** se ejecuta, **Then** el sistema retorna 403 sin fuga y sin auditoría de éxito.

---

### User Story 3 - Mantener coherencia operativa y manejo de edge cases (Priority: P2)

Como operador, quiero que la vista en vivo mantenga coherencia con el motor (conteos `Players Answered + Waiting == Players Connected` cuando aplica, `Scores` reconstruible desde ledger, `Current Level` derivado de progresión) y maneje desconexiones, reconexiones y concurrencia de forma segura.

**Why this priority**: Eleva la supervisión de "mostrar datos" a "mostrar datos confiables" (Constitución V: Server Truth, D: ledger). Depende de US1/US2 y es P2 porque el valor base ya se entregó.

**Independent Test**: Forzar reconexión del operador (caída de WebSocket/polling) → verificar reconexión automática y re-sincronización de `Current Question`/`Scores` sin duplicar auditoría; ejecutar `Pause` concurrente desde dos operadores → solo uno tiene éxito, el otro recibe `ConcurrencyConflict`/`InvalidGameState` sin doble auditoría.

**Acceptance Scenarios**:

1. **Given** la vista en vivo con `Players Connected: 3`, `Answered: 2`, `Waiting: 1`, **When** se recarga la vista, **Then** los mismos conteos se mantienen (coherencia server-side, no cálculo en UI).
2. **Given** un juego con `Scores` basados en `PointTransaction` ledger, **When** se consulta el detalle, **Then** los scores mostrados son reconstruibles desde el ledger y coinciden con `GET /api/games/{id}/leaderboard`.
3. **Given** una desconexión temporal del operador (polling/WebSocket caído), **When** se reconecta, **Then** la vista re-sincroniza `Current Round`/`Current Question`/`Game Timer` sin perder auditoría y sin mostrar datos obsoletos.
4. **Given** dos operadores ejecutan `Pause` simultáneamente, **When** ambos confirman, **Then** uno tiene éxito y el otro recibe `ConcurrencyConflict`/`InvalidGameState` sin crear segunda entrada de auditoría para el mismo evento.
5. **Given** un juego finaliza (`Finished`) mientras la vista está abierta, **When** ocurre, **Then** la vista muestra `Game Status: Finished`, congela `Game Timer`, deshabilita las 4 acciones y muestra resumen final de `Scores`.

---

### Edge Cases

- ¿Qué ocurre si la vista en vivo se abre para un juego `Draft`/`Configured` (no iniciado)? Muestra `Game Status` y `Players` pero `Current Round`/`Current Question`/`Game Timer` en estado Vacío con mensaje "Juego no iniciado" y acciones de control deshabilitadas (solo `Cancel` habilitado).
- ¿Qué ocurre si `Current Question` tarda >5s en cargar? La tarjeta muestra skeleton y `aria-busy`, sin bloquear el resto de indicadores; al fallar, muestra `Error` con Reintentar aislado y reintento no genera auditoría.
- ¿Qué ocurre si un jugador se desconecta durante `Players Answered`? `Players Connected` decrementa y `Players Waiting` se recalcula server-side; la vista refleja el cambio en ≤3s sin recarga completa.
- ¿Qué ocurre si el operador pierde sesión mientras la vista hace polling/WebSocket? La petición falla con 401, el polling/WebSocket se detiene y se muestra "Sesión expirada — re-autenticar" sin bucle, sin auditoría de operación.
- ¿Qué ocurre si `Force Finish` se ejecuta con jugadores aún respondiendo? El motor cierra la ronda actual, congela scores y transita a `Finished`; las respuestas tardías son rechazadas con `GameFinished` y no se puntúan.
- ¿Qué ocurre con auditoría si la misma operación se reintenta por falta de confirmación de red (idempotencia)? El segundo intento con mismo `IdempotencyKey` no crea segunda entrada de auditoría ni muta el estado.
- ¿Qué ocurre en viewport móvil (375px)? La vista en vivo es utilizable sin scroll horizontal, con 10 indicadores apilados y acciones con objetivos ≥44px.

## Requirements *(mandatory)*

### Functional Requirements

**Supervisión en vivo — 10 indicadores**

- **FR-001**: El sistema MUST mostrar en la vista de juego en vivo (`/admin/live/{gameId}`) el `Game Status` derivado del backend (`Draft`, `Configured`, `Scheduled`, `Ready`, `Running`, `Paused`, `Finished`, `Cancelled` y estados de dominio `ROUND_IN_PROGRESS`/`ROUND_COMPLETED`) con badge y tooltip de mapeo.
- **FR-002**: El sistema MUST mostrar `Current Round` (número de ronda actual, 0 si no iniciado) y `Current Question` (texto de pregunta con 4 opciones A–D, sin revelar la correcta en la vista de operador salvo que la política lo permita; la autoridad de corrección permanece server-side).
- **FR-003**: El sistema MUST mostrar `Players` (total de `GamePlayer` asociados), `Players Connected` (presencia online via SignalR/Hub o `GamePlayer` con `LastSeen` reciente) y distinguir claramente `Connected` (sesión) vs `Players` (inscritos).
- **FR-004**: El sistema MUST mostrar `Players Answered` (jugadores que enviaron respuesta válida para la pregunta actual) y `Players Waiting` (`Connected` − `Answered` para la ronda actual, o `Players` con estado `Waiting` según definición de dominio) de forma coherente y sin cálculo en UI.
- **FR-005**: El sistema MUST mostrar `Scores` (tabla `PlayerId`/`DisplayName`/`Score`/`SecuredPoints`/`Level` reconstruible desde `PointTransaction` ledger) y `Current Level` (nivel de dificultad/progresión actual, 1–5, derivado de `DifficultyStrategy`).
- **FR-006**: El sistema MUST mostrar `Game Timer` (cuenta regresiva por pregunta derivada de `TimePerQuestion` y `StartedAt` server-side, congelado en `Paused`, 0 al finalizar) y mantenerlo sincronizado con el servidor (no confiar en reloj del cliente).
- **FR-007**: La vista en vivo MUST actualizarse sin recarga completa de la página (polling 3–5s o WebSocket via `SignalR` reenviado por BFF) y MUST mantener coherencia: `Players Answered + Players Waiting` y `Scores` coinciden con `GET /api/games/{id}/leaderboard` y `GET /api/games/{id}/players`.

**Vista de juego en vivo y navegación**

- **FR-008**: El sistema MUST proveer una vista de juego en vivo dedicada (`/admin/live` como listado de juegos `Running`/`Paused` y `/admin/live/{gameId}` como detalle) accesible desde Dashboard (`Ver juegos activos` → Live) y desde el listado de juegos, con navegación drill-down coherente (conteo en Dashboard coincide con listado).
- **FR-009**: La vista en vivo MUST manejar estados `Loading` (skeleton por indicador), `Ready`, `Empty` (0 jugadores) y `Error` con reintento aislado por indicador, sin bloquear los demás, y MUST ser accesible (`aria-live` para cambios de scores/timer, contraste AA en tema claro).

**Acciones administrativas controladas**

- **FR-010**: El sistema MUST exponer 4 acciones controladas en la vista en vivo: `Pause` (`Running` → `Paused`), `Resume` (`Paused` → `Running`), `Cancel` (`Running`/`Paused`/`Ready`/`Scheduled` → `Cancelled` con motivo opcional) y `Force Finish` (`Running`/`Paused` → `Finished` forzado), cada una con diálogo de confirmación y con `RowVersion`/`If-Match` para concurrencia.
- **FR-011**: Toda transición inválida (p. ej., `Finished → Pause`, `Draft → Force Finish`) MUST ser rechazada con `InvalidGameState` sin mutación parcial y sin auditoría de éxito.
- **FR-012**: El sistema MUST restringir las 4 acciones a roles `ADMIN` y `GAME_MANAGER` (política `AdminOrGameManager`); `REWARD_MANAGER` y `PLAYER` MUST recibir `Access Denied` en UI y 403 por API sin fuga, y los botones MUST estar deshabilitados u ocultos con razón.

**Auditoría y observabilidad**

- **FR-013**: Cada operación privilegiada (`Pause`, `Resume`, `Cancel`, `Force Finish`) que tenga éxito MUST generar un registro de auditoría append-only con `GameId`, `ActorId` (sub de OroIdentityServer), `Timestamp` UTC, `FromState`, `ToState`, `Action`, `Reason` (si aplica), `CorrelationId`, `Result`, sin mutar historial; los intentos fallidos MUST NOT generar auditoría de éxito (solo log de error).
- **FR-014**: El sistema MUST propagar `CorrelationId` y mapear `Result` → HTTP (`ProblemDetails` RFC 7807) sin exponer detalles internos; los errores de negocio usan códigos explícitos (`InvalidGameState`, `ConcurrencyConflict`, `GameNotFound`).
- **FR-015**: El sistema MUST registrar identificadores de correlación y NO exponer detalles internos en errores de la vista en vivo; los errores de negocio del backend se muestran como mensajes accionables con reintento aislado.

**Integración y presentación**

- **FR-016**: La vista en vivo MUST consumir exclusivamente la API/BFF (`QuizArena.Api` via `QuizArena.Admin` BFF: `GET /bff/games/{id}`, `GET /bff/games/{id}/leaderboard`, `GET /bff/games/{id}/players`, `GET /bff/games/{id}/questions/current` y hub `/hubs/game` reenviado) y MUST NOT acceder directamente a SQL Server/Oracle/`identitydb`.
- **FR-017**: El sistema MUST reutilizar el shell de navegación, tema `administration`, tokens y componentes del Design System (SPEC-016) sin valores hardcodeados y MUST residir en `src/Admin/QuizArena.Admin` (Blazor Auto net10.0) y `src/Admin/QuizArena.Admin.Client`, con `data-theme="administration"` y `design-tokens.css`.
- **FR-018**: El sistema MUST exigir sesión válida via `OroIdentityServer` (OIDC `authorization_code` + `refresh_token`) y manejar `must_change_password` y expiración antes de mostrar la vista en vivo; en 401 durante polling/WebSocket, el polling se detiene y se muestra "Sesión expirada".
- **FR-019**: El sistema MUST proteger las transiciones con concurrencia optimista (`rowversion`) e idempotencia (`IdempotencyKey` para `Pause`/`Resume`/`Cancel`/`ForceFinish`) para que reintentos por red no dupliquen auditoría ni muten dos veces.

### Key Entities *(include if feature involves data)*

- **Live Game View**: Proyección de lectura en tiempo casi real de una partida en ejecución. Atributos: `GameId`, `GameStatus` (8 estados admin + mapeo dominio), `CurrentRound` (int), `CurrentQuestion` (texto + 4 opciones, sin revelar correcta salvo política), `RowVersion`. Refresca via polling/WebSocket.
- **Game Round State**: Estado de ronda actual (`RoundNumber`, `Status`, `StartedAt`, `QuestionId`). Derivado de `GameRound` del dominio.
- **Player Presence (Live)**: `Players` (total `GamePlayer`), `Players Connected` (sesión online), `Players Answered` (respuesta válida para pregunta actual), `Players Waiting` (`Connected − Answered` o estado `Waiting`). Fuente: `GamePlayer` + hub presence.
- **Live Scores**: `PlayerId`, `DisplayName`, `Score` (reconstruido desde `PointTransaction` ledger), `SecuredPoints`, `CurrentLevel` (1–5 derivado de progresión). Fuente: `GET /api/games/{id}/leaderboard`.
- **Game Timer**: Cuenta regresiva derivada de `TimePerQuestion` y `StartedAt` server-side; congelado en `Paused`. No es autoridad, es visualización sincronizada con servidor.
- **Game Operation**: Comando privilegiado `Pause`, `Resume`, `Cancel`, `ForceFinish` con `GameId`, `RowVersion`, `IdempotencyKey`, `Reason?`, `ActorId`, `Timestamp`, `CorrelationId`. Genera `GameAuditEntry` si tiene éxito.
- **Game Audit Entry (Live)**: Registro append-only: `GameId`, `ActorId`, `FromState`, `ToState`, `Action`, `Reason?`, `Timestamp`, `CorrelationId`, `Result`, `IdempotencyKey`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Un operador abre la vista en vivo de un juego `Running` y percibe los 10 indicadores correctos en <3s percibidos (carga inicial <2s con skeleton por indicador, sin pantalla en blanco).
- **SC-002**: Los contadores `Players Answered`/`Waiting` y `Scores` se actualizan en ≤3s tras una respuesta sin recarga completa, y coinciden con `GET /api/games/{id}/leaderboard` en el 100% de las pruebas.
- **SC-003**: El 100% de las acciones válidas (`Pause` `Running→Paused`, `Resume` `Paused→Running`, `Cancel` → `Cancelled`, `Force Finish` → `Finished`) se ejecutan con éxito, transitan el estado y generan auditoría con `ActorId`/`CorrelationId`; el 100% de las acciones inválidas son rechazadas con `InvalidGameState` sin mutación ni auditoría de éxito.
- **SC-004**: La autorización se respeta en el 100% de los casos: `REWARD_MANAGER` ve `Access Denied` en la vista en vivo y en las 4 acciones, y cualquier intento por API retorna 403 sin fuga; `ADMIN`/`GAME_MANAGER` operan sin fricción.
- **SC-005**: La vista en vivo es coherente: `Players Answered + Players Waiting` y `Scores` reconstruidos desde ledger coinciden con el backend en el 100% de las pruebas; no hay cálculo de scores en UI.
- **SC-006**: El `Game Timer` permanece sincronizado con el servidor (derivado de `StartedAt` server-side) y se congela en `Paused` en el 100% de los casos; el reloj del cliente nunca es autoridad.
- **SC-007**: La vista en vivo maneja `Loading`/`Empty`/`Error` por indicador con reintento aislado en el 100% de los bloques; ningún error de un indicador bloquea los demás (SC-009 de 018 reforzado).
- **SC-008**: Concurrencia: dos operadores ejecutan `Pause` simultáneamente → uno tiene éxito y el otro recibe `ConcurrencyConflict`/`InvalidGameState` sin doble auditoría en el 100% de las pruebas de colisión.
- **SC-009**: La vista en vivo es utilizable entre 375 y 1536px sin scroll horizontal, con 0 violaciones de objetivos táctiles <44px y orden de foco lógico (indicadores → acciones), y cumple WCAG 2.2 AA en tema claro.
- **SC-010**: El 90% de los operadores completa la tarea "abrir Dashboard → ver juego activo → abrir vista en vivo → pausar y reanudar" en <30s en el primer intento.

## Assumptions

- **Reutiliza SPEC-017/018/019/012**: La app Blazor net10.0 Auto, shell de 10 secciones, BFF YARP, OIDC, Dashboard y `Game`/`GameRound` de dominio ya existen (SPEC-017/018/019 y `012-realtime-game-events` para hub). 022 extiende la superficie de supervisión/control en vivo, sin crear nueva app ni duplicar autenticación.
- **Estados**: Los 10 indicadores usan los 8 estados administrativos de 019 (`Draft` etc.) mapeados a dominio `Game`/`GameRound` (Constitución A). `Running` = `IN_PROGRESS`/`ROUND_IN_PROGRESS`/`ROUND_COMPLETED`, `Paused` es `Running` con flag `IsPaused`.
- **Live Games existente**: `LiveGamesService` y `LiveGames.razor` ya existen (017/018) con `MapForwarder("/hubs/game")` y `GetGamesAsync(status=Active)`; 022 los enriquece con 10 indicadores y 4 acciones auditadas, sin romper el hub.
- **Players**: `Players` = total de `GamePlayer` inscritos en el juego; `Players Connected` = presencia online (SignalR/Hub `UserSession` o `LastSeen` reciente); `Players Answered`/`Waiting` derivados de `GamePlayer` con estado de respuesta para la pregunta actual. Si el backend no distingue, se usa la mejor aproximación con tooltip.
- **Scores**: Reconstruibles desde `PointTransaction` ledger (Constitución D) y expuestos via `GET /api/games/{id}/leaderboard`; la UI no calcula puntos (Server Truth).
- **Game Timer**: Derivado de `TimePerQuestion` y `StartedAt` del servidor; en `Paused` se congela server-side y la UI lo refleja; no se usa `DateTime.Now` del cliente como autoridad.
- **Live view**: Polling 3–5s o WebSocket via BFF `MapForwarder("/hubs/game")` ya existente; la UI prefiere push si está disponible y cae a polling si no, sin duplicar auditoría.
- **Acciones**: `Pause`/`Resume` son reversibles; `Cancel` y `Force Finish` son terminales; todas requieren confirmación y `RowVersion` + `IdempotencyKey` para idempotencia.
- **Auditoría**: Append-only via Outbox (`GameAuditEntry`) en `SaveChanges` (Constitución I); `Force Finish` marca `privileged` y no permite `Resume` posterior.
- **Idioma**: Español para etiquetas de la vista en vivo (coherente con 017–021), sin i18n en v1.
- **Sin acceso directo a datos**: Todo conteo/validación via BFF; no lectura directa a SQL Server/Oracle/`identitydb` (Constitución H).
