# Feature Specification: Player Application

**Feature Branch**: `027-player-application`

**Created**: 2026-08-28

**Status**: Draft

**Input**: User description: "APLICACIÓN DEL JUGADOR — Aquí cambia completamente la naturaleza del producto. La aplicación será: Angular 22 y será responsable exclusivamente de la experiencia del participante. SPEC-027 — Player Application Tecnología Angular 22 Objetivo Definir la aplicación web utilizada por cada jugador para participar en QuizArena. Descripción Cada jugador utilizará una instancia independiente de la aplicación. La aplicación deberá mantener un contexto privado de: Player, Game, Game Session, Round, Question, Answer, Score, Secured Points, Timer, Status. Los jugadores podrán estar participando simultáneamente en el mismo juego, pero cada uno deberá disponer de su propia experiencia y estado de interacción. revisa la nota 4 que se encuentra en la constitution en donde se pedira que instales una libreria para angular 22 y uses una skills que se llama ngrx-signal-store que se encuenta ya instalada."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Experiencia privada e independiente por jugador (Priority: P1)

Como jugador autenticado, quiero que mi instancia de la aplicación mantenga un contexto privado y aislado con mi `Player`, `Game`, `Game Session`, `Round`, `Question`, `Answer`, `Score`, `Secured Points`, `Timer` y `Status`, de forma que mi experiencia sea exclusivamente mía y no se mezcle con la de ningún otro participante.

**Why this priority**: Es el contrato fundacional de SPEC-027 — sin contexto privado por instancia no existe aplicación del jugador. Entrega valor independiente como sesión de juego personal aislada.

**Independent Test**: Autenticar dos jugadores A y B en el mismo juego desde dos navegadores/dispositivos distintos. Verificar que cada instancia muestra su propio `Player` (sub/email), `Game` (id/nombre/configuración), `Game Session` (sesión de participación), `Round`/`Question`/`Answer` actuales, `Score`/`Secured Points` propios, `Timer` propio y `Status` propio. Verificar que acciones de A (responder, ver puntaje) no alteran ningún campo visible en B y viceversa, y que recargar la página de A no afecta a B.

**Acceptance Scenarios**:

1. **Given** jugador A autenticado con sesión válida (OroIdentityServer), **When** abre la aplicación y se une a un juego en `WAITING_FOR_PLAYERS`, **Then** su instancia crea un contexto privado con `Player` (identidad del token `sub`), `Game` (datos del juego), `Game Session` (participación), `Round` inicial (sin ronda activa), `Question` vacía, `Answer` sin enviar, `Score` 0, `Secured Points` 0, `Timer` detenido y `Status` activo.
2. **Given** jugadores A y B en el mismo juego y misma ronda activa con misma pregunta, **When** A selecciona una opción y B selecciona otra distinta, **Then** cada instancia mantiene su `Answer` elegida de forma privada, sin revelar la elección del otro, y cada `Score`/`Secured Points` permanece independiente.
3. **Given** jugador A con `Score` 500 y `Secured Points` 200, **When** B consulta su propio estado, **Then** ve su propio `Score`/`Secured Points` (no los de A) y su `Status` propio; los datos de A no son visibles ni mutables desde B.
4. **Given** jugador A que recarga su instancia o pierde conectividad momentánea, **When** vuelve a abrir la aplicación con el mismo token, **Then** su contexto privado se rehidrata con su `Game Session`/`Round`/`Question`/`Answer`/`Score`/`Secured Points`/`Status` autoritativo del servidor, sin afectar a otros jugadores.

---

### User Story 2 — Participación simultánea en el mismo juego con aislamiento total (Priority: P1)

Como jugador en un juego con N participantes simultáneos, quiero que mi interacción (ver pregunta, responder, ver mi puntaje y mi timer) ocurra en tiempo real sin interferir con la experiencia de los demás jugadores en el mismo juego, de forma que todos podamos jugar a la vez con justicia y fluidez.

**Why this priority**: Es la segunda mitad del objetivo — concurrencia masiva con aislamiento. Sin aislamiento simultáneo el producto no es multiplayer real. Entrega valor independiente como garantía de juego concurrente justo.

**Independent Test**: Con N jugadores (mínimo 5) en el mismo juego en `IN_PROGRESS`/`ROUND_IN_PROGRESS`, hacer que todos soliciten la pregunta actual y envíen su respuesta dentro de la misma ventana de tiempo. Verificar que cada instancia recibe su pregunta, envía su respuesta, recibe su evaluación y actualiza su `Score`/`Secured Points`/`Status` sin bloquear, corromper o retrasar indebidamente a los demás; verificar que el `Timer` de cada jugador refleja su ventana autoritativa.

**Acceptance Scenarios**:

1. **Given** un juego con jugadores A, B y C en `ROUND_IN_PROGRESS`, **When** el servidor publica `QuestionAvailable` para la ronda, **Then** cada instancia recibe y muestra la misma pregunta simultáneamente, cada una dentro de su contexto privado (`Round`/`Question` propias) sin compartir `Answer`.
2. **Given** jugadores A, B y C con la misma pregunta y `TimeLimitPerQuestion` activo, **When** los tres envían su respuesta simultáneamente, **Then** cada instancia procesa su envío de forma independiente, muestra su resultado individual (correcto/incorrecto/expirado) y actualiza su `Score`/`Secured Points`/`Status` sin interferencia.
3. **Given** jugador A con latencia o reconexión durante una ronda, **When** recupera conectividad, **Then** su instancia se sincroniza con el estado autoritativo de su `Game Session`/`Round`/`Timer` sin afectar el flujo de B y C, que continúan sin interrupción.
4. **Given** un jugador que intenta manipular el estado de otro (ej. enviar respuesta suplantando `PlayerId`), **When** lo intenta desde su instancia, **Then** el sistema lo rechaza por discrepancia de identidad (token `sub` ≠ `PlayerId`) y el estado del otro jugador permanece intacto.

---

### User Story 3 — Ciclo de vida de la sesión de juego del jugador (Priority: P2)

Como jugador, quiero que mi aplicación refleje fielmente el ciclo de vida de mi participación — desde unirme al juego, esperar inicio, jugar rondas, responder preguntas, ver mi puntaje y puntos asegurados, hasta retirarme o finalizar — con cada transición de `Status` y `Round` visible y coherente con el estado autoritativo del servidor.

**Why this priority**: Da continuidad a la experiencia privada a lo largo de toda la partida. Depende de US1/US2 pero entrega valor independiente como flujo completo de participación.

**Independent Test**: Flujo end-to-end con un jugador: unirse → esperar → `GameStarted` → N rondas (`RoundStarted` → `QuestionAvailable` → responder → `ScoreUpdated`/`RoundCompleted`) → `GameFinished` o `Withdraw`. Verificar que en cada paso `Game Session`/`Round`/`Question`/`Answer`/`Score`/`Secured Points`/`Timer`/`Status` se actualizan correctamente y que `Status` terminal (retirado/eliminado/ganador) bloquea nuevas respuestas.

**Acceptance Scenarios**:

1. **Given** jugador sin sesión activa, **When** se une a un juego `WAITING_FOR_PLAYERS`, **Then** su `Game Session` pasa a activa, `Status` a activo, `Score`/`Secured Points` a 0, y ve estado de espera con `Game` y conteo de jugadores/espectadores si aplica.
2. **Given** juego que transita a `IN_PROGRESS` y `ROUND_IN_PROGRESS`, **When** inicia una ronda, **Then** la instancia actualiza `Round` (número/nivel), `Question` (enunciado + 4 opciones), `Answer` (sin responder), `Timer` (cuenta regresiva desde `TimeLimitPerQuestion` con tiempo autoritativo del servidor) y `Status` permanece activo.
3. **Given** jugador que envía respuesta válida dentro del tiempo, **When** el servidor evalúa (SPEC-006), **Then** la instancia actualiza `Answer` (evaluada: correcta/incorrecta), `Score` (nuevo total desde ledger SPEC-007), `Secured Points` (si aplica política de checkpoint), `Timer` detenido para la pregunta, y muestra resultado antes de `RoundCompleted`.
4. **Given** jugador que se retira voluntariamente (SPEC-008) o es eliminado por política de pérdida, **When** ejecuta/ocurre la transición, **Then** su `Status` cambia a retirado/eliminado, `Secured Points` refleja la política (`KEEP_SECURED_SCORE`/`KEEP_CHECKPOINT_SCORE` etc.), `Round` se congela, y no puede enviar más respuestas.
5. **Given** juego que finaliza (`FINISHED`), **When** ocurre `GameFinished`, **Then** la instancia muestra `Score` final, `Secured Points` finales, `Status` ganador/no ganador según corresponda, y bloquea interacción de juego manteniendo consulta de resultados.

---

### User Story 4 — Timer autoritativo y puntos asegurados visibles (Priority: P2)

Como jugador, quiero ver en todo momento mi tiempo restante para responder y mis puntos asegurados, con la garantía de que ambos son calculados por el servidor y no manipulables desde el cliente, de forma que pueda tomar decisiones (responder rápido, asegurar puntos, retirarme) con información confiable.

**Why this priority**: `Timer` y `Secured Points` son parte explícita del contexto privado y críticas para decisiones de juego (retirada, riesgo). Entrega valor independiente como panel de control confiable.

**Independent Test**: Durante una ronda con `TimeLimitPerQuestion` de 30s, verificar que el `Timer` cuenta regresivamente en la instancia, que expira según timestamp del servidor (no del cliente), y que al expirar `Answer` pasa a expirado sin puntuar; verificar que `Secured Points` se actualiza solo tras eventos autoritativos (checkpoint, ronda asegurada) y coincide con el ledger.

**Acceptance Scenarios**:

1. **Given** ronda activa con `TimeLimitPerQuestion` 30s, **When** la pregunta se muestra, **Then** el `Timer` inicia en 30s y cuenta regresivamente de forma continua, sincronizado con el tiempo del servidor (corrección periódica si hay drift).
2. **Given** `Timer` en 5s restantes, **When** el jugador envía respuesta, **Then** el envío se evalúa contra el tiempo autoritativo del servidor; si llegó a tiempo se evalúa, si expiró se rechaza como expirado, sin confiar en el `Timer` del cliente.
3. **Given** jugador que alcanza un checkpoint (ej. ronda 5 de 10), **When** el servidor asegura puntos, **Then** su `Secured Points` se actualiza y se muestra diferenciado de `Score` total (ej. "500 pts · 200 asegurados"), persistiendo tras respuestas incorrectas según política.
4. **Given** jugador que pierde según política `FALLBACK_TO_CHECKPOINT` o `KEEP_SECURED_SCORE`, **When** ocurre la pérdida, **Then** su `Score` visible se ajusta al valor asegurado y su `Secured Points` permanece como referencia, ambos consistentes con el ledger.

---

### User Story 5 — Estado en tiempo real y rehidratación resiliente (Priority: P3)

Como jugador con conectividad intermitente, quiero que mi aplicación reciba actualizaciones en tiempo real (`RoundStarted`, `QuestionAvailable`, `ScoreUpdated`, `RoundCompleted`, `GameFinished`) y que pueda rehidratar mi contexto privado al reconectar, de forma que nunca pierda mi progreso ni vea estado corrupto.

**Why this priority**: Garantiza resiliencia y vivacidad de la experiencia. Depende de US1-US4 pero entrega valor independiente como robustez ante fallos de red.

**Independent Test**: Simular desconexión de 10s durante una ronda activa, luego reconectar. Verificar que la instancia reconecta automáticamente, rehidrata `Game`/`Game Session`/`Round`/`Question`/`Answer`/`Score`/`Secured Points`/`Timer`/`Status` desde el servidor, y retoma el flujo sin duplicar respuestas ni perder puntos.

**Acceptance Scenarios**:

1. **Given** jugador en `ROUND_IN_PROGRESS` con `Timer` corriendo, **When** pierde conectividad 5s y reconecta antes de que expire el tiempo, **Then** su `Timer` se corrige al tiempo autoritativo restante y puede aún responder si la ventana sigue abierta.
2. **Given** jugador desconectado durante `ScoreUpdated`/`RoundCompleted`, **When** reconecta, **Then** su instancia muestra `Score`/`Secured Points`/`Status` actualizados al último estado evaluado, sin requerir acción manual salvo re-autenticación si el token expiró.
3. **Given** jugador cuyo token expira durante la partida, **When** intenta continuar, **Then** la aplicación lo redirige al flujo OIDC de OroIdentityServer (`authorization_code` + `refresh_token`) y tras renovar, rehidrata su contexto sin perder su `Game Session`.

---

### Edge Cases

- ¿Qué sucede cuando dos jugadores abren la aplicación en el mismo dispositivo/navegador con usuarios distintos? Cada autenticación genera un token `sub` distinto; cada instancia debe mantener contextos completamente separados (sin compartir `localStorage`/`sessionStorage` entre identidades) y nunca mezclar `Game Session`.
- ¿Qué sucede cuando un jugador abre dos pestañas con la misma sesión? Ambas pestañas deben reflejar el mismo contexto privado autoritativo del servidor; una respuesta enviada en una pestaña debe verse reflejada en la otra tras sincronización, sin duplicar envíos (idempotencia por `AnswerSubmissionId`).
- ¿Qué sucede cuando el `Timer` del cliente y el del servidor divergen por drift de reloj? El cliente debe corregir periódicamente contra timestamp del servidor; la decisión de expiración siempre la toma el servidor.
- ¿Qué sucede cuando el jugador intenta modificar `Score`/`Secured Points`/`Status` desde el cliente (devtools, request manipulado)? El servidor ignora valores enviados y recalcula autoritativamente (Constitución V); la instancia solo visualiza.
- ¿Qué sucede cuando el jugador intenta ver o mutar el `Answer`/`Score` de otro jugador manipulando `PlayerId`/`GameId` en la request? Rechazo 403 por discrepancia de identidad, estado ajeno intacto, intento auditado.
- ¿Qué sucede cuando el jugador se une a un juego ya iniciado o lleno? Rechazo con error de dominio (`GameAlreadyStarted`/`GameFull`) y mensaje accionable, sin crear `Game Session`.
- ¿Qué sucede cuando el jugador pierde conectividad exactamente al enviar respuesta? El envío es idempotente (SPEC-006/011 FR-007): reintento con misma clave retorna el resultado original sin duplicar puntos.
- ¿Qué sucede cuando el juego finaliza mientras el jugador responde? La respuesta se evalúa o rechaza según el estado autoritativo (`FINISHED` → `InvalidGameState`), y la instancia muestra el resultado final consistente.
- ¿Qué sucede cuando `Secured Points` no aplica (juego sin checkpoints)? Se muestra como 0 o no destacado, sin error, y `Score` es la única métrica.
- ¿Qué sucede cuando la pregunta contiene datos sensibles o la categoría no está publicada? La instancia solo muestra preguntas válidas autorizadas; preguntas no publicadas no se filtran al cliente.

## Requirements *(mandatory)*

### Functional Requirements

**Contexto privado por instancia**

- **FR-001**: El sistema MUST mantener, por cada instancia autenticada de la aplicación del jugador, un contexto privado aislado que incluya exactamente estos 10 elementos: `Player` (identidad del jugador derivada de `sub` del token), `Game` (juego al que participa), `Game Session` (participación/sesión del jugador en el juego), `Round` (ronda actual del jugador), `Question` (pregunta actual con 4 opciones), `Answer` (respuesta del jugador en la ronda), `Score` (puntaje acumulado del jugador), `Secured Points` (puntos asegurados/checkpoint del jugador), `Timer` (tiempo restante autoritativo para la pregunta) y `Status` (estado de participación del jugador).
- **FR-002**: El sistema MUST garantizar que el contexto privado de un jugador nunca sea visible ni mutable por otro jugador: ninguna instancia puede leer o escribir el `Player`/`Game Session`/`Round`/`Question`/`Answer`/`Score`/`Secured Points`/`Timer`/`Status` de otro jugador; toda mutación MUST validar que el `sub` autenticado corresponde al `PlayerId` afectado.
- **FR-003**: El sistema MUST aislar el estado en memoria por instancia: el estado privado de un jugador (los 10 elementos) MUST mantenerse en un store reactivo dedicado por sesión de jugador, sin compartir estado mutable entre jugadores, pestañas de usuarios distintos o juegos distintos.

**Participación simultánea**

- **FR-004**: El sistema MUST permitir que N jugadores participen simultáneamente en el mismo juego, cada uno con su instancia y contexto privado independientes, sin que la interacción de uno bloquee, corrompa o retrase indebidamente la de otro.
- **FR-005**: El sistema MUST publicar y consumir eventos de juego en tiempo real de forma server-driven (`RoundStarted`, `QuestionAvailable`, `ScoreUpdated`, `RoundCompleted`, `GameFinished` y equivalentes) hacia cada instancia, sin que los eventos sean fuente de verdad; el estado autoritativo es el persistido en el servidor.

**Ciclo de vida y estados**

- **FR-006**: El sistema MUST reflejar el ciclo de vida de la participación del jugador: unión (`WAITING_FOR_PLAYERS` → `Game Session` activa), espera, `IN_PROGRESS`/`ROUND_IN_PROGRESS` (rondas y preguntas), evaluación de respuesta, actualización de `Score`/`Secured Points`, `ROUND_COMPLETED`, `FINISHED`/`CANCELLED`/`FORCED_FINISHED`, y transiciones terminales de `Status` (activo → retirado/eliminado/ganador) según SPEC-004/SPEC-008.
- **FR-007**: El sistema MUST bloquear envíos de respuesta cuando el `Status` del jugador es terminal (retirado/eliminado) o el juego está en estado terminal, retornando error de dominio accionable sin mutar estado.
- **FR-008**: El sistema MUST mantener `Round` y `Question` sincronizados con el servidor: `Round` avanza solo por evento autoritativo; `Question` siempre corresponde a la ronda actual del jugador y contiene exactamente 4 opciones con una correcta (validada server-side, no expuesta como tal al cliente).

**Respuesta y puntuación**

- **FR-009**: El sistema MUST permitir al jugador seleccionar una de las 4 opciones y enviar su `Answer` dentro de la ventana de `TimeLimitPerQuestion`; el envío MUST ser idempotente por jugador+ronda (clave `AnswerSubmissionId`/`IdempotencyKey`) y evaluado exclusivamente server-side (SPEC-006).
- **FR-010**: El sistema MUST mostrar `Score` y `Secured Points` del jugador como valores derivados exclusivamente del ledger `PointTransaction` (SPEC-007), consistentes con el servidor tras cada `ScoreUpdated`; MUST diferenciar visualmente `Score` total de `Secured Points` y MUST NOT confiar en valores calculados por el cliente.
- **FR-011**: El sistema MUST aplicar políticas de `Secured Points`/checkpoint según la configuración del juego (ej. cada 5 rondas, `KEEP_SECURED_SCORE`, `KEEP_CHECKPOINT_SCORE`, `FALLBACK_TO_CHECKPOINT`): al alcanzar checkpoint, `Secured Points` se actualiza autoritativamente; tras pérdida, `Score` refleja la política aplicable.

**Timer autoritativo**

- **FR-012**: El sistema MUST mostrar un `Timer` regresivo por pregunta basado en `TimeLimitPerQuestion` de la configuración del juego, sincronizado periódicamente con el timestamp del servidor; la decisión de validez/expiración de una respuesta MUST tomarse exclusivamente con el tiempo del servidor, nunca con el del cliente.
- **FR-013**: El sistema MUST detener o reiniciar el `Timer` según eventos autoritativos: inicia con `QuestionAvailable`, se detiene al enviar/evaluar o expirar, y reinicia con cada nueva ronda; MUST mostrar estado expirado de forma clara cuando la ventana cierra sin respuesta.

**Autenticación, autorización y seguridad**

- **FR-014**: El sistema MUST exigir sesión válida vía OroIdentityServer (OIDC `authorization_code` + `refresh_token`, descubrimiento `.well-known/openid-configuration`, validación `jwks_uri`), propagar `sub`/`roles`/`tenant_id`/`must_change_password`, y redirigir a `/auth/login`/`/auth/change-password`/`/connect/logout` cuando corresponda; MUST NOT implementar store local de credenciales ni firma JWT propia.
- **FR-015**: El sistema MUST hacer cumplir aislamiento de identidad en cada comando que mute estado del jugador (enviar respuesta, retirarse): el `PlayerId` del comando MUST igualar el `sub` del token; discrepancias MUST rechazarse con 401/403 y auditarse, sin fuga de detalles internos (RFC 7807 `ProblemDetails`).
- **FR-016**: El sistema MUST validar en tres niveles: API (contrato), Aplicación (requisitos de caso de uso) y Dominio (invariantes); invariantes de dominio MUST NOT depender solo de validación de UI.

**Tiempo real, resiliencia y rehidratación**

- **FR-017**: El sistema MUST reconectar automáticamente ante pérdida de conectividad (SignalR/WebSocket con backoff), rehidratar los 10 elementos del contexto privado desde el estado autoritativo del servidor al reconectar, y corregir `Timer` al tiempo restante autoritativo.
- **FR-018**: El sistema MUST manejar expiración de token durante la partida: intentar `refresh_token` silencioso; si falla, redirigir a re-autenticación y tras éxito, rehidratar `Game Session` sin pérdida de progreso ya persistido.
- **FR-019**: El sistema MUST auditar (append-only) eventos de la aplicación del jugador: unión, envío de respuesta, actualización de `Score`/`Secured Points`, cambios de `Status`, conflictos de concurrencia e intentos de suplantación, con `GameId`/`PlayerId`/`RoundId`/`QuestionId`/`CorrelationId`/`TraceId`.

**Presentación y accesibilidad**

- **FR-020**: El sistema MUST proporcionar estados de UI para cada contexto: `Loading` (skeleton), `Empty` (sin juego/ronda), `Ready` (jugando), `Error` (retry con `CorrelationId`), `Expired` (tiempo agotado), `Terminal` (retirado/eliminado/finalizado), con mensajes accionables y sin exponer detalles sensibles.
- **FR-021**: El sistema MUST cumplir WCAG 2.2 AA (contraste, foco visible, navegación por teclado, `aria-live` para `Timer`/`Score`/`Status`), ser utilizable entre 375px y 1536px sin scroll horizontal, con objetivos táctiles ≥44px y soporte de tema del Design System (SPEC-016) sin valores hardcodeados.

### Key Entities *(include if feature involves data)*

- **Player**: Identidad del participante. Atributos: `PlayerId` (derivado de `sub` del token OroIdentityServer), `DisplayName`, `Email`, `TenantId`. No es credencial local; referencia a identidad externa. Un `Player` puede tener múltiples `Game Session` en distintos juegos, pero una sola por juego.
- **Game**: Partida de QuizArena. Atributos: `GameId`, `Name`, `Status` (ciclo de vida SPEC-004: `DRAFT`/`READY`/`WAITING_FOR_PLAYERS`/`IN_PROGRESS`/`ROUND_IN_PROGRESS`/`ROUND_COMPLETED`/`FINISHED`/`CANCELLED`/`FORCED_FINISHED`), `Configuration` (categoría, min/max rounds ≥5, dificultad inicial, estrategia de progresión, `TimeLimitPerQuestion`, `PointsPerRound`, políticas de retiro/pérdida/consolación/recompensa), `Category`, `MaxPlayers`/`MinPlayers`. Inmutable tras `Start`.
- **Game Session (GamePlayer)**: Participación de un `Player` en un `Game`. Atributos: `GameSessionId`, `PlayerId`, `GameId`, `Status` (activo, retirado SPEC-008, eliminado por política de pérdida, ganador), `JoinedAt`, `CurrentRound` (última ronda alcanzada), versión de concurrencia (`rowversion`). Restricción: única por `PlayerId`+`GameId`; terminal no vuelve a activo.
- **Round**: Ronda dentro del juego. Atributos: `RoundId`, `GameId`, `RoundNumber` (1..maxRounds), `Level`/`Difficulty`, `Status` (`WAITING`/`IN_PROGRESS`/`COMPLETED`), `QuestionId`, `StartedAt`/`ExpiresAt` (server timestamps). Compartida por jugadores activos en el juego.
- **Question**: Pregunta de la ronda. Atributos: `QuestionId`, `CategoryId`, `Text`, `AnswerOptions` (exactamente 4, una correcta server-side), `Complexity`/`AcademicLevel`/`AgeRange`/`KnowledgeCategory`, `Difficulty`. Correctitud nunca expuesta al cliente antes de evaluación.
- **Answer**: Respuesta del jugador en la ronda. Atributos: `AnswerId`/`AnswerSubmissionId`, `PlayerId`, `GameId`, `RoundId`, `QuestionId`, `SelectedOptionId`, `SubmittedAt` (server timestamp), `State` (`PENDING` → `SUBMITTED` → `EVALUATED`/`EXPIRED`), `IsCorrect` (solo tras evaluación), `IdempotencyKey`. Idempotente por jugador+ronda.
- **Score**: Puntaje acumulado del jugador en el juego. Derivado exclusivamente del ledger `PointTransaction` (SPEC-007). Tipos de transacción: `ANSWER_CORRECT`, `ANSWER_INCORRECT`, `ROUND_BONUS`, `LEVEL_BONUS`, `GAME_BONUS`, `PENALTY`, `WITHDRAWAL`, `REWARD_REDEMPTION`, `CONSOLATION`, `ADJUSTMENT`. Reconstruible desde historial.
- **Secured Points (SecureScore/Checkpoint)**: Subconjunto asegurado de `Score` según política de checkpoint/retiro. Atributos: `SecuredPoints` (valor asegurado), `CheckpointRound` (ronda donde se aseguró), `Policy` (`LOSE_ALL`/`KEEP_CURRENT_SCORE`/`KEEP_SECURED_SCORE`/`KEEP_CHECKPOINT_SCORE`/`FALLBACK_TO_CHECKPOINT`). Actualizado solo por eventos autoritativos.
- **Timer**: Tiempo restante para responder la pregunta actual. Atributos: `TimeLimitPerQuestion` (configuración), `ExpiresAt` (server timestamp), `RemainingSeconds` (derivado), `State` (`RUNNING`/`STOPPED`/`EXPIRED`). Visualización en cliente, verdad en servidor.
- **Status (PlayerStatus + GameStatus)**: Estado de participación del jugador y del juego. `PlayerStatus`: activo, retirado, eliminado, ganador. `GameStatus`: estados del ciclo de vida SPEC-004. Determinan si el jugador puede interactuar.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Con N jugadores (hasta `MaxPlayers`) en el mismo juego, el 100% de las instancias mantienen su contexto privado (10 elementos) aislado: ningún jugador ve ni muta el `Answer`/`Score`/`Secured Points`/`Status` de otro, y acciones simultáneas no se interfieren.
- **SC-002**: El 100% de los envíos de respuesta simultáneos dentro de la ventana de tiempo se evalúan correctamente (cada jugador recibe su resultado individual) sin actualizaciones perdidas ni duplicadas en ningún `Score`/`Secured Points`.
- **SC-003**: El 0% de los intentos de suplantación (enviar respuesta o mutar estado con `PlayerId` distinto al `sub` autenticado) tiene éxito; todos se rechazan con 401/403 y quedan auditados.
- **SC-004**: El `Timer` mostrado al jugador se desvía menos de 1 segundo respecto al tiempo autoritativo del servidor en el 95% de las mediciones durante una ronda, y el 100% de las decisiones de expiración coinciden con el servidor.
- **SC-005**: Tras cada evaluación de respuesta, el `Score` y `Secured Points` mostrados en la instancia coinciden en el 100% con el ledger del servidor (`PointTransaction` reconstruible) en menos de 1 segundo percibido.
- **SC-006**: Un jugador completa el flujo end-to-end (unirse → jugar 5 rondas → ver `Score`/`Secured Points` finales) en menos de 3 minutos sin ayuda externa en el 90% de los intentos, con estados `Loading`/`Empty`/`Error`/`Expired`/`Terminal` correctamente mostrados.
- **SC-007**: Ante desconexión de 10 segundos durante una ronda activa, el 100% de las instancias reconectan automáticamente, rehidratan los 10 elementos del contexto privado al estado autoritativo y permiten continuar sin pérdida de progreso persistido ni duplicación de respuesta.
- **SC-008**: La aplicación del jugador cumple WCAG 2.2 AA (contraste, foco, teclado, `aria-live` para `Timer`/`Score`) y es utilizable entre 375px y 1536px sin scroll horizontal, con objetivos ≥44px, en el 100% de las vistas de juego.
- **SC-009**: El 100% de los errores de la aplicación del jugador se presentan como `ProblemDetails` RFC 7807 sin fuga de detalles internos, con `CorrelationId`/`TraceId` propagado y mensaje accionable, y con `must_change_password` gating cuando corresponde.

## Assumptions

- **Stack tecnológico**: La aplicación del jugador se implementa en **Angular 22** como SPA dedicada exclusivamente a la experiencia del participante, separada de la aplicación de administración (SPEC-017 Blazor). La gestión de estado privado por instancia utiliza **NgRx SignalStore** (`@ngrx/signals`) con `withState`/`withComputed`/`withMethods`/`withProps` y `patchState`/`rxMethod`, siguiendo la skill `ngrx-signal-store` instalada. Instalación requerida: `npm install @ngrx/signals @ngrx/signals/entities @ngrx/signals/rxjs-interop` (nota 4 de la Constitución — librería Angular 22 + skill `ngrx-signal-store`).
- **Reutiliza dominio existente**: El ciclo de vida del juego (SPEC-004), rondas y progresión (SPEC-005), evaluación de respuestas (SPEC-006), ledger de puntuación (SPEC-007), retiro/consolación/recompensas (SPEC-008/010/009), multiplayer y concurrencia (SPEC-011), eventos en tiempo real (SPEC-012), seguridad (SPEC-013) y auditoría (SPEC-014) ya existen en el backend (`oroclash-api` .NET `net10.0` + BuildingBlocks). Esta SPEC solo define la experiencia del jugador que los consume.
- **Fuente de verdad**: El servidor es autoritativo para `Answer` (correctitud), `Score`/`Secured Points` (ledger), `Timer` (timestamps), `Status`/`Round`/`Game` (máquina de estados). El cliente es solo presentación; nunca calcula ni persiste autoritativamente.
- **Identidad**: Autenticación exclusiva vía OroIdentityServer (OIDC `authorization_code` + `refresh_token`, `identitydb` PostgreSQL aislada). `PlayerId` = `sub` del JWT validado contra `jwks_uri`; no existe store local de usuarios en OroQuizClash. `must_change_password` bloquea juego hasta cambio.
- **Instancia por jugador**: Cada jugador usa una instancia independiente (navegador/dispositivo/pestaña con token propio). El estado privado no se comparte entre instancias; la rehidratación siempre consulta el servidor.
- **Game Session**: Equivale a `GamePlayer` del dominio (SPEC-011). Una `Game Session` por `PlayerId`+`GameId`; `JoinedAt` y versión de concurrencia (`rowversion`) protegen transiciones.
- **Secured Points**: Derivado de políticas configurables (SPEC-001/004): `LOSE_ALL`/`KEEP_CURRENT_SCORE`/`KEEP_SECURED_SCORE`/`KEEP_CHECKPOINT_SCORE`/`FALLBACK_TO_CHECKPOINT` y checkpoints por ronda. Si el juego no configura checkpoints, `Secured Points` permanece 0.
- **Timer**: `TimeLimitPerQuestion` proviene de `GameConfiguration` (inmutable tras `Start`). El `Timer` del cliente es visual; la expiración la decide el servidor con `SubmittedAt` vs `ExpiresAt`.
- **Tiempo real**: SignalR (o WebSocket equivalente) para notificaciones server-driven (`RoundStarted`, `QuestionAvailable`, `ScoreUpdated`, `LeaderboardUpdated`, `RoundCompleted`, `GameFinished`) con reconexión automática y backoff; no es fuente de verdad.
- **Idioma**: Español para etiquetas de la aplicación del jugador en v1, coherente con SPEC-017; sin i18n en v1.
- **Sin acceso directo a datos**: Toda lectura/escritura vía API/BFF (`oroclash-api` / `QuizArena.Player` BFF si aplica) con `CorrelationId`/`TraceId` OTel y `BuildingBlocks.ServiceDefaults`; nunca acceso directo a SQL Server/Oracle/`identitydb`.
- **Paginación y validación**: Consultas paginadas server-side cuando aplique (`page`/`pageSize`); validación en tres niveles (API/Aplicación/Dominio).

## Dependencies

- **SPEC-001 — Game Configuration**: `TimeLimitPerQuestion`, `PointsPerRound`, políticas de retiro/pérdida/consolación/recompensa, `MinPlayers`/`MaxPlayers`, niveles de dificultad.
- **SPEC-004 — Game Lifecycle**: Estados `DRAFT`→`FINISHED`, `JoinGame`, `StartGame`, `FinishGame`, límites de unión, finalización forzada.
- **SPEC-005 — Round Engine**: Rondas, estrategia de selección de preguntas (`IQuestionSelectionStrategy`), progresión de dificultad, `CurrentLevel`.
- **SPEC-006 — Answer Evaluation**: Validación y evaluación server-side, `AnswerState`, idempotencia `AnswerSubmissionId`, errores `PlayerNotInGame`/`QuestionAlreadyAnswered`/`InvalidAnswer`.
- **SPEC-007 — Scoring System**: Ledger `PointTransaction`, `AwardPoints`/`RemovePoints`/`SecurePoints`, reconstrucción de `Score`/`Secured Points`.
- **SPEC-008 — Player Withdrawal**: Políticas `LOSE_ALL`/`KEEP_*`/`FALLBACK_TO_CHECKPOINT`, estados retirado/eliminado, elegibilidad de retiro.
- **SPEC-010 — Consolation** y **SPEC-009 — Reward Redemption**: Elegibilidad y ciclo de vida que afectan `Status`/`Score` finales.
- **SPEC-011 — Multiplayer**: Estado individual `GamePlayer` (`PlayerId`/`GameId`/`Status`/`Score`/`CurrentRound`/`AnswerState`), aislamiento entre jugadores, concurrencia optimista, leaderboard por juego.
- **SPEC-012 — Realtime Game Events**: SignalR, eventos `RoundStarted`/`QuestionAvailable`/`ScoreUpdated`/`LeaderboardUpdated`/`RoundCompleted`/`GameFinished`, Outbox + `IEventBus`.
- **SPEC-013 — Game Security**: JWT de OroIdentityServer, políticas `PLAYER`/`GAME_MANAGER`/`ADMIN`, rate limiting, anti-cheating, validación de identificadores.
- **SPEC-014 — Audit Trail**: Trail append-only, `AuditEntry`, `CorrelationId`/`TraceId`.
- **SPEC-016 — UI/UX Design System**: Tokens, tema, componentes, WCAG 2.2 AA, responsive 375-1536px.
- **OroIdentityServer**: OIDC discovery, `authorization_code`+`refresh_token`, `jwks_uri`, `/connect/*`, `/auth/*`, `must_change_password`, `UserSession`.
- **BuildingBlocks**: `Entity`/`AggregateRoot`/`ValueObject`, `Result`/`Error`, `IRepository`/`IUnitOfWork`, `ICommand`/`IQuery`/`ISender`, `IntegrationEvent`/`IEventBus`/`IOutboxWriter`, `IEndpoint`, `BuildingBlocks.ServiceDefaults` (OTel, health checks).

## Out of Scope

- Aplicación de administración (SPEC-017 — Blazor Auto `net10.0`, `QuizArena.Admin` + BFF YARP): gestión de juegos, categorías, preguntas, recompensas, jugadores y auditoría administrativa.
- Matchmaking, emparejamiento automático, salas, equipos, alianzas, espectadores, chat.
- Leaderboards globales, históricos entre juegos o rankings de temporada (solo leaderboard por juego en SPEC-011).
- Selección concreta de preguntas y estrategia de dificultad (SPEC-003/SPEC-005).
- Cálculo autoritativo de puntos, correctitud, avance de nivel y elegibilidad de recompensa/consolación (dominio server-side SPEC-006/007/008/009/010).
- Entrega de recompensas y canje (SPEC-009).
- Interfaz de host/orquestación (Aspire `AppHost`, Podman Compose) y despliegue de OroIdentityServer (imagen `oroidentityserver:latest`).
- Soporte offline completo o juego sin conectividad (requiere conexión para estado autoritativo).

## References

- Constitución v1.1.0 — Principios I (Domain First), II (Clean Architecture), III (BuildingBlocks), IV (Vertical Slice + CQRS), V (Server Truth — cliente no confiable, estado multiplayer aislado, SignalR no es fuente de verdad), VI (OroIdentityServer Podman), Constraints A (ciclo de vida), B (preguntas), C (reglas configurables), D (ledger), F (concurrencia/idempotencia), G (Outbox/RabbitMQ), H (seguridad delegada), I (validación/errores/observabilidad/auditoría), J (API/Frontend OIDC).
- SPEC-004 — Game Lifecycle.
- SPEC-005 — Round Engine.
- SPEC-006 — Answer Evaluation.
- SPEC-007 — Scoring System.
- SPEC-008 — Player Withdrawal.
- SPEC-011 — Multiplayer.
- SPEC-012 — Realtime Game Events.
- SPEC-013 — Game Security.
- SPEC-014 — Audit Trail.
- SPEC-016 — UI/UX Design System.
- SPEC-017 — Admin Application.
- Nota 4 — Constitución: librería Angular 22 + skill `ngrx-signal-store` (`@ngrx/signals`, `withState`/`withComputed`/`withMethods`/`withProps`, `patchState`, `rxMethod`, `withEntities`).
- OroIdentityServer — `draft/oroidentityserver-specification.md` (OIDC, Podman `oroidentityserver:latest`, PostgreSQL `identitydb`).

