# Feature Specification: Admin Game Configuration

**Feature Branch**: `019-admin-game-configuration`

**Created**: 2026-08-28

**Status**: Draft

**Input**: User description: "019 — Admin Game Configuration Objetivo Permitir crear y configurar una partida antes de que sea iniciada. Descripción El administrador podrá definir: Nombre del juego. Descripción. Categoría. Número de rondas. Número máximo de jugadores. Tiempo por pregunta. Dificultad inicial. Progresión de dificultad. Puntuación. Puntos asegurados. Reglas de retiro. Reglas de finalización. Premio final. Premio de consolación. Fecha/hora de inicio. Estado del juego. Estados: Draft, Configured, Scheduled, Ready, Running, Paused, Finished, Cancelled"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Crear y configurar una partida completa antes de iniciar (Priority: P1)

Como administrador (ADMIN) o gestor de juegos (GAME_MANAGER) autenticado, quiero crear una nueva partida y definir los 16 atributos configurables (nombre, descripción, categoría, rondas, jugadores máximos, tiempo por pregunta, dificultad inicial/progresión, puntuación, puntos asegurados, reglas de retiro/finalización, premios final y consolación, fecha/hora de inicio, estado inicial) para que la partida quede lista para programar o iniciar sin re-trabajo.

**Why this priority**: Es el núcleo de 019 — sin creación/configuración no existe flujo de juego administrable. Constituye el MVP estricto y desbloquea todos los estados posteriores. Requiere validación completa y persistencia transaccional antes de cualquier transición.

**Independent Test**: Iniciar sesión como ADMIN → navegar a "Crear juego" / "Game Configuration" → completar formulario con 16 campos válidos (categoría existente, rondas ≥5, etc.) → guardar → verificar que el juego aparece en listado con estado `Draft` → `Configured` y que todos los valores persistidos coinciden con lo ingresado. No requiere programar fecha ni iniciar.

**Acceptance Scenarios**:

1. **Given** un ADMIN en la pantalla de creación, **When** completa todos los campos obligatorios con valores válidos (nombre 3–100, descripción ≤500, categoría activa con ≥5 preguntas, rondas 5–10, jugadores máximos ≥2, tiempo 5–300s, dificultad 1–5, etc.) y guarda, **Then** el sistema crea el juego en estado `Draft` → `Configured` y muestra confirmación con ID y resumen.
2. **Given** un intento con categoría inexistente o inactiva sin preguntas suficientes, **When** guarda, **Then** el sistema rechaza con error accionable `CategoryNotReady` y no crea el juego.
3. **Given** un juego en `Draft`, **When** el administrador completa la configuración mínima (nombre, categoría, rondas ≥5, tiempo, dificultad), **Then** el juego transita automáticamente a `Configured` (configuración válida) sin requerir acción adicional.
4. **Given** un juego en `Configured`, **When** se recarga la página de edición, **Then** todos los 16 campos muestran los valores guardados, editables mientras el juego no haya alcanzado `Ready`/`Running`.
5. **Given** un usuario con rol REWARD_MANAGER (sin permiso `Game.Create`), **When** intenta acceder a "Crear juego", **Then** ve denegación clara y no puede crear ni editar configuración.

---

### User Story 2 - Programar, preparar y controlar el ciclo de vida previo a la ejecución (Priority: P1)

Como administrador, quiero llevar una partida configurada a través de `Configured → Scheduled → Ready → Running → Paused → Finished/Cancelled` (con `Draft` como estado inicial de borrador) definiendo fecha/hora de inicio y controlando transiciones válidas, para operar el calendario de juegos y pausar/reanudar si es necesario.

**Why this priority**: El objetivo incluye explícitamente 8 estados y fecha/hora de inicio. Sin transiciones controladas, la configuración queda huérfana y no se puede orquestar operación en vivo. Co-prioritario con US1 para valor operacional.

**Independent Test**: Tomar un juego en `Configured` → asignar fecha/hora futura → verificar transición a `Scheduled`; avanzar a `Ready` cuando la categoría sigue válida y la fecha se acerca; iniciar (`Ready → Running`), pausar (`Running → Paused`), reanudar (`Paused → Running`), finalizar (`Running → Finished`) y cancelar (`Draft/Configured/Scheduled → Cancelled`) cada una con comando dedicado y auditoría. No requiere editar campos de US1.

**Acceptance Scenarios**:

1. **Given** un juego en `Configured`, **When** el ADMIN asigna `ScheduledAt` futuro (≥5 minutos en adelante, UTC) y guarda, **Then** el juego transita a `Scheduled` y muestra la fecha programada en el detalle y listado.
2. **Given** un juego en `Scheduled` con fecha alcanzada y validaciones superadas, **When** el sistema o el ADMIN lo mueve a `Ready`, **Then** el juego queda en `Ready` y se habilita el comando "Iniciar".
3. **Given** un juego en `Ready`, **When** el ADMIN ejecuta "Iniciar", **Then** el juego transita a `Running` y se bloquea la edición de los 16 campos configurables (inmutables tras inicio — Constitución C).
4. **Given** un juego en `Running`, **When** el ADMIN ejecuta "Pausar", **Then** el juego transita a `Paused` y deshabilita envío de respuestas hasta "Reanudar" (`Paused → Running`).
5. **Given** un juego en `Running` o `Paused`, **When** el ADMIN ejecuta "Finalizar", **Then** transita a `Finished` (terminal); si ejecuta "Cancelar" desde `Draft/Configured/Scheduled`, transita a `Cancelled` (terminal). Transiciones inválidas (p. ej., `Finished → Running`) son rechazadas con `InvalidGameState`.
6. **Given** un juego en `Draft` con `ScheduledAt` en el pasado, **When** guarda, **Then** el sistema rechaza con error de validación "La fecha debe ser futura".

---

### User Story 3 - Validación avanzada, premios y reglas de negocio configurables (Priority: P2)

Como administrador avanzado, quiero configurar puntuación, puntos asegurados, reglas de retiro/finalización y premios (final y consolación) con validaciones de dominio y feedback inmediato, para que las reglas del juego sean explícitas, auditables y no puedan quedar inconsistentes.

**Why this priority**: Eleva la configuración de "formulario básico" a "motor configurable" (Constitución C). Depende de US1/US2 y es P2 porque el valor base ya se entregó, pero es necesario para diferenciar 019 de la configuración mínima de la fase 001.

**Independent Test**: Editar un juego en `Draft/Configured` → seleccionar `Scoring: ProgressiveBonus`, `SecuredPoints: KEEP_CHECKPOINT`, `Withdrawal: KEEP_SECURED_SCORE`, `Finish: FALLBACK_TO_CHECKPOINT`, `Reward Final` y `Consolation` válidos → guardar con éxito; luego intentar combinaciones inválidas (p. ej., premio inexistente, puntos asegurados > rondas) → ver errores de dominio claros sin persistencia parcial. Verificar que tras `Running` estos campos son solo lectura.

**Acceptance Scenarios**:

1. **Given** un juego en `Draft`, **When** selecciona `Dificultad inicial 3` y `Progresión Adaptive` y guarda, **Then** el sistema persiste la estrategia y la muestra en el detalle (validada contra las 5 estrategias permitidas).
2. **Given** un juego con `Puntuación` y `Puntos asegurados` configurados, **When** guarda, **Then** el sistema valida que los puntos asegurados son coherentes con rondas y política de pérdida; si no, muestra error `InvalidConfiguration` con campo señalado.
3. **Given** `Reglas de retiro` y `Reglas de finalización` seleccionadas, **When** guarda, **Then** solo se permiten los valores del catálogo constitución (`LOSE_ALL`, `KEEP_CURRENT_SCORE`, `KEEP_SECURED_SCORE`, `KEEP_CHECKPOINT_SCORE` y loss `LOSE_ALL`/`LOSE_CURRENT_ROUND`/etc.); valores fuera de catálogo son rechazados.
4. **Given** un `Premio final` y `Premio de consolación` seleccionados, **When** guarda, **Then** ambos deben referenciar `Reward` existente en estado `Active`; si el reward está inactivo o sin stock, el sistema rechaza con `RewardUnavailable`.
5. **Given** un juego en `Running` (post-inicio), **When** intenta editar cualquier campo de reglas o premios, **Then** el formulario es solo lectura y el intento por API es rechazado con `InvalidGameState` y no muta la configuración.

---

### Edge Cases

- ¿Qué ocurre si la categoría tiene exactamente 5 preguntas pero una es archivada durante la edición? La validación de publicación (≥5 válidas) se re-evalúa en cada guardado; el juego permanece en `Draft` hasta recuperar el umbral.
- ¿Qué ocurre si `Fecha/hora de inicio` coincide con otro juego programado para la misma categoría con solapamiento de jugadores máximos? El sistema permite solapamiento (sin restricción de calendario en MVP) pero lo advierte en UI si existe; no bloquea el guardado.
- ¿Qué ocurre si el ADMIN pierde sesión mientras configura? El guardado falla con 401, el formulario conserva el borrador local y muestra banner "Sesión expirada — re-autenticar" sin perder datos ingresados (solo lectura local hasta re-autenticar).
- ¿Qué ocurre con `Pausar` durante una ronda activa (`ROUND_IN_PROGRESS`)? `Paused` congela el temporizador server-side y preserva `RoundNumber`/`QuestionId` para reanudar sin repetir preguntas.
- ¿Qué ocurre si se intenta `Scheduled → Ready` con `ScheduledAt` en pasado lejano (reprogramación olvidada)? El sistema rechaza con "Reprograme a fecha futura" y no transita.
- ¿Qué ocurre si `Número de rondas` se cambia después de `Configured` pero antes de `Ready`? Permitido; si se reduce por debajo de 5, el guardado es rechazado por invariante de dominio.
- ¿Qué ocurre con concurrencia (dos ADMIN editando el mismo juego en `Draft`)? Optimistic concurrency (`rowversion`) detecta conflicto y uno recibe `ConcurrencyConflict` con opción de recargar.
- ¿Qué ocurre con `Tiempo por pregunta` = 0 o 301? Rechazado por validación de rango 5–300s con mensaje por campo.

## Requirements *(mandatory)*

### Functional Requirements

**Creación y definición (16 campos)**

- **FR-001**: El sistema MUST permitir crear un juego con `Nombre` (3–100, requerido), `Descripción` (0–500, opcional) y `Categoría` (requerida, debe existir y estar en estado `Active` con ≥5 preguntas válidas) en estado inicial `Draft`.
- **FR-002**: El sistema MUST permitir definir `Número de rondas` (entero 5–10, requerido, inmutable tras `Running`) y `Número máximo de jugadores` (entero ≥2 y ≤1000, requerido) con validación por campo y mensaje señalado.
- **FR-003**: El sistema MUST permitir definir `Tiempo por pregunta` (segundos 5–300, requerido) y `Dificultad inicial` (1–5, requerida) y `Progresión de dificultad` (requerida, uno de `Linear`, `Progressive`, `Adaptive`, `CategorySpecific`).
- **FR-004**: El sistema MUST permitir definir `Puntuación` (`PointsPerRound` / `ScoringSystem` — al menos `Standard`, `ProgressiveBonus`) y `Puntos asegurados` (política `SecuredPoints`: `None`, `KEEP_CHECKPOINT`, `KEEP_SECURED` o equivalente mapeado a `WITHDRAWAL`/`LOSS` policies) con coherencia validada (p. ej., puntos asegurados no puede exceder rondas).
- **FR-005**: El sistema MUST permitir definir `Reglas de retiro` (uno de `LOSE_ALL`, `KEEP_CURRENT_SCORE`, `KEEP_SECURED_SCORE`, `KEEP_CHECKPOINT_SCORE`) y `Reglas de finalización` / Loss policy (uno de `LOSE_ALL`, `LOSE_CURRENT_ROUND`, `LOSE_UNSECURED_POINTS`, `FALLBACK_TO_CHECKPOINT`) con catálogo cerrado y validado en dominio.
- **FR-006**: El sistema MUST permitir definir `Premio final` y `Premio de consolación` como referencias opcionales a `Reward` existente en estado `Active`; si se especifican, el sistema MUST validar disponibilidad/stock y que no sean el mismo reward cuando la distinción es requerida por la política.
- **FR-007**: El sistema MUST permitir definir `Fecha/hora de inicio` (`ScheduledAt`, opcional en `Draft/Configured`, requerida para `Scheduled`, debe ser UTC futura ≥5 minutos) con selector accesible y validación de fecha pasada.
- **FR-008**: El sistema MUST exponer y persistir `Estado del juego` como uno de `Draft`, `Configured`, `Scheduled`, `Ready`, `Running`, `Paused`, `Finished`, `Cancelled` con transición automática `Draft → Configured` al completar la configuración mínima válida, y transiciones explícitas para el resto (ver FR-009).
- **FR-009**: El sistema MUST aplicar la máquina de estados con transiciones permitidas: `Draft → Configured → Scheduled → Ready → Running ↔ Paused → Finished` y `Draft/Configured/Scheduled → Cancelled`; además `Running/Paused → Cancelled` solo si no hay `GamePlayer` con estado `PLAYING` o si se fuerza con auditoría. Toda transición inválida MUST ser rechazada con `InvalidGameState` y sin mutación parcial, protegida por concurrencia optimista (`rowversion`).

**Edición, inmutabilidad y validación**

- **FR-010**: El sistema MUST permitir editar los 16 campos mientras el juego está en `Draft`, `Configured` o `Scheduled`; al alcanzar `Ready`/`Running`/`Paused` los campos de configuración (rondas, tiempo, dificultad, políticas, premios) MUST volverse inmutables (solo lectura en UI y rechazados por API).
- **FR-011**: El sistema MUST validar en tres niveles: API (contrato), Aplicación (requisitos de caso de uso) y Dominio (invariantes — categoría con ≥5 válidas, rondas ≥5, tiempo 5–300, dificultad 1–5, enumerable de políticas, `ScheduledAt` futura). Los invariantes de dominio MUST NOT depender solo de validación de UI.
- **FR-012**: El sistema MUST mostrar errores por campo con códigos accionables (`CategoryNotReady`, `InvalidConfiguration`, `InvalidGameState`, `RewardUnavailable`, `ConcurrencyConflict`) y MUST preservar el borrador local en caso de 401 sin pérdida de datos ingresados hasta re-autenticar.

**Autorización y auditoría**

- **FR-013**: El sistema MUST restringir creación y edición a roles `ADMIN` y `GAME_MANAGER` (política `AdminOrGameManager`); `REWARD_MANAGER` y `PLAYER` MUST recibir `Access Denied` en UI y 403 por API sin fuga de datos. `OroIdentityServer` es la única autoridad de identidad (Constitución VI).
- **FR-014**: El sistema MUST auditar de forma append-only cada creación, modificación de configuración y transición de estado (actor, timestamp UTC, `GameId`, estado anterior/nuevo, diff de campos clave) sin mutar decisiones históricas.
- **FR-015**: El sistema MUST propagar `CorrelationId` y mapear `Result` → HTTP (`ProblemDetails` RFC 7807) sin exponer detalles internos; los errores de negocio usan códigos explícitos señalados por campo.

**Integración y presentación**

- **FR-016**: El Dashboard y listados MUST consumir exclusivamente la API/BFF (`QuizArena.Api` via `QuizArena.Admin` BFF) para todos los datos de configuración; MUST NOT acceder directamente a base de datos de dominio ni a `identitydb`.
- **FR-017**: El sistema MUST reutilizar el shell de navegación, tema `administration`, tokens y componentes del Design System (SPEC-016) sin valores hardcodeados y MUST residir en `src/Admin/QuizArena.Admin` (Blazor Auto net10.0) y `src/Admin/QuizArena.Admin.Client`.
- **FR-018**: El sistema MUST exigir sesión válida via `OroIdentityServer` (OIDC `authorization_code` + `refresh_token`) y manejar `must_change_password` y expiración antes de permitir configurar.
- **FR-019**: El sistema MUST listar juegos configurables con paginación y filtros por estado y categoría, y MUST ofrecer detalle con historial de transiciones y configuración inmutable resaltada cuando el juego está en ejecución.

### Key Entities *(include if feature involves data)*

- **GameConfiguration**: Agregado de configuración previo al inicio. Atributos: `GameId`, `Name`, `Description`, `CategoryId`, `NumberOfRounds` (5–10), `MaxPlayers` (≥2), `TimePerQuestion` (5–300s), `InitialDifficulty` (1–5), `DifficultyProgression` (`Linear`/`Progressive`/`Adaptive`/`CategorySpecific`), `Scoring` (`PointsPerRound`, `ScoringSystem`), `SecuredPoints` (política), `WithdrawalPolicy`, `Finish/LossPolicy`, `FinalRewardId?`, `ConsolationRewardId?`, `ScheduledAt?` (UTC), `Status` (8 estados), `RowVersion`. Invariante: inmutable tras `Running`.
- **Game State Machine**: Estados `Draft`, `Configured`, `Scheduled`, `Ready`, `Running`, `Paused`, `Finished`, `Cancelled` con transiciones permitidas y guardas (configuración válida, categoría ≥5 preguntas, `ScheduledAt` futura, no terminal). Protegida por concurrencia optimista.
- **Category Reference**: `CategoryId` referencia a `Category` en estado `Active` con ≥5 `Question` válidas (4 opciones, 1 correcta). Validada en cada guardado.
- **Reward Reference**: `RewardId` referencia a `Reward` en estado `Active` con stock/validación; `Final` y `Consolation` son opcionales pero si se definen deben ser distintos cuando la política lo exige.
- **Game Audit Entry**: Registro append-only: `GameId`, `ActorId` (sub de OroIdentityServer), `Timestamp`, `FromState`, `ToState`, `ChangedFields` (diff), `CorrelationId`, `Result`.
- **Withdrawal/Loss/Scoring Policies**: Enumeraciones de dominio (`WithdrawalPolicy`, `LossPolicy`, `ScoringSystem`, `DifficultyStrategy`) mapeadas desde configuración y validadas contra catálogo constitución C.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Un ADMIN completa la creación y configuración válida de una partida (16 campos) en menos de 3 minutos en el 90% de los intentos medidos desde "Crear juego" hasta confirmación `Configured`.
- **SC-002**: El 100% de las transiciones de estado válidas (`Draft→Configured→Scheduled→Ready→Running→Paused→Running→Finished` y `→Cancelled`) se ejecutan con éxito y son auditadas; el 100% de las transiciones inválidas son rechazadas con `InvalidGameState` sin mutación parcial.
- **SC-003**: El 100% de las ediciones tras `Ready`/`Running` de campos inmutables (rondas, tiempo, dificultad, políticas, premios) son bloqueadas en UI (solo lectura) y rechazadas por API con código por campo.
- **SC-004**: El 100% de los intentos con `Categoría` inválida (inexistente/inactiva/<5 preguntas) o `ScheduledAt` pasada son rechazados con mensaje por campo en <2 segundos percibidos, sin pantalla en blanco.
- **SC-005**: La configuración persiste de forma transaccional y es reconstruible: el detalle recargado muestra exactamente los 16 valores guardados (coherencia 100% en pruebas paginadas).
- **SC-006**: La autorización se respeta en el 100% de los casos: `REWARD_MANAGER` ve `Access Denied` en "Crear/Configurar" y cualquier intento por API retorna 403 sin fuga; `ADMIN`/`GAME_MANAGER` operan sin fricción.
- **SC-007**: El formulario cumple WCAG 2.2 AA en tema `administration` (contraste, foco visible, navegación teclado, anuncios `aria-live` en errores) y es utilizable entre 375 y 1536px sin scroll horizontal y con objetivos táctiles ≥44px.
- **SC-008**: Concurrencia: bajo edición simultánea del mismo juego en `Draft`, uno de los escritores recibe `ConcurrencyConflict` con opción de recargar y el otro persiste con éxito; no hay sobrescritura silenciosa en el 100% de las pruebas de colisión.
- **SC-009**: El listado de juegos configurables pagina correctamente (≥100 juegos) y filtra por estado/categoría en <2 segundos percibidos con skeleton por bloque, sin cargar colecciones completas.
- **SC-010**: El 90% de los operadores completa la tarea "crear → configurar → programar (Scheduled) → listo (Ready)" sin ayuda externa en el primer intento.

## Assumptions

- **Reutiliza SPEC-017/018/001**: La app Blazor net10.0 Auto, shell de 10 secciones, BFF YARP, OIDC y Dashboard ya existen (SPEC-017/018). El agregado `Game` y su `GameConfiguration` de dominio ya existen en `001-game-configuration`; 019 extiende la superficie administrativa de UI + orquestación de estados de pre-inicio, sin crear una nueva app ni duplicar autenticación.
- **Estados administrativos vs. dominio**: Los 8 estados del enunciado (`Draft` etc.) son la vista administrativa; se mapean a los estados de dominio Constitución A (`DRAFT`, `READY`, `WAITING_FOR_PLAYERS`, `IN_PROGRESS`, etc.) en el plan. `Configured` equivale a configuración válida aún sin fecha; `Scheduled` requiere `ScheduledAt` futura; `Paused` aplica solo a `Running` con ronda activa.
- **Categoría**: Debe existir, estar `Active` y tener ≥5 preguntas válidas (4 opciones, 1 correcta) por Constitución B. Si no cumple, la partida permanece en `Draft`.
- **Rondas**: Mínimo 5 por Constitución C (Game Configuration immutable after start, min rounds ≥5). Rango administrativo 5–10 para MVP; ampliación post-MVP sin cambiar contrato.
- **Premios**: `Final` y `Consolation` son opcionales y referencian `Reward` en estado `Active`; `Consolation` es independiente de `Reward` normal (Constitución C) y su elegibilidad se evalúa server-side, no en la UI.
- **Políticas**: Catálogos cerrados de la Constitución C (`Withdrawal`, `Loss`, `DifficultyStrategy`, `ScoringSystem`). Valores fuera de catálogo son rechazados.
- **Wording**: Se usa `Puntuación` como `PointsPerRound` + `ScoringSystem`, `Puntos asegurados` como política de checkpoint/keep, `Reglas de finalización` como Loss policy. Si el backend no distingue `Secured` vs `Withdrawal`, la UI documenta la aproximación con tooltip.
- **Fecha/hora**: Siempre UTC; selector accesible con validación ≥5 minutos en el futuro para evitar carreras de programador. No se impone restricción de solapamiento entre juegos en MVP (solo advertencia).
- **Inmutabilidad**: Tras `Running` toda la configuración es inmutable (Constitución C). Ediciones previas a `Ready` son permitidas con validación completa en cada guardado.
- **Idioma**: Español para etiquetas de configuración, coherente con SPEC-017/018, sin i18n en v1.
- **Sin acceso directo a datos**: Todo conteo/validación proviene del backend via BFF; no hay lectura directa a SQL Server, Oracle ni `identitydb` (Constitución H).
