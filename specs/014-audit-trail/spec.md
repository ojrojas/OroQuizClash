# Feature Specification: Audit Trail

**Feature Branch**: `014-audit-trail`

**Created**: 2026-08-28

**Status**: Draft

**Input**: User description: "014 — Audit Objetivo Registrar eventos relevantes del sistema para trazabilidad. Eventos auditables GameCreated GameConfigured GameStarted PlayerJoined RoundStarted QuestionPresented AnswerSubmitted AnswerEvaluated PointsAwarded PointsRemoved PlayerWithdrawn PlayerEliminated GameFinished RewardRedeemed ConsolationGranted AdministrativeAdjustment Audit Record Conceptualmente: AuditRecord ├── Id ├── Timestamp ├── Actor ├── Action ├── Resource ├── ResourceId ├── GameId ├── PlayerId ├── CorrelationId ├── Data └── Result Reglas La auditoría debe ser: Append-oriented Immutable Searchable Traceable No debe utilizarse para modificar el estado de negocio. Dependencias Transversal."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Auditoría del ciclo de vida del juego (Priority: P1)

Como operador de plataforma, quiero que cada transición relevante del ciclo de vida de un juego quede registrada de forma automática e inmutable, de forma que pueda reconstruir qué pasó, cuándo y quién lo provocó sin depender de logs dispersos.

**Why this priority**: Es el núcleo de trazabilidad: cubre la creación y evolución de la partida, que es transversal a todos los flujos posteriores (rondas, respuestas, recompensas). Sin este registro, no hay base para auditoría de puntos ni de administración. Entrega valor independiente como historial verificable del juego.

**Independent Test**: Crear un juego, configurarlo, iniciarlo, unir un jugador, iniciar una ronda y presentar la pregunta. Luego consultar el trail de auditoría filtrando por `GameId`. Verificar que existen registros append-only para `GameCreated`, `GameConfigured`, `GameStarted`, `PlayerJoined`, `RoundStarted` y `QuestionPresented`, cada uno con `Id`, `Timestamp` de servidor, `Actor`, `Action`, `Resource`/`ResourceId`, `GameId`, `CorrelationId`, `Data` y `Result`, sin posibilidad de modificarlos, y que la lectura no afecta el estado del juego.

**Acceptance Scenarios**:

1. **Given** un juego inexistente, **When** se ejecuta `GameCreated` con actor organizador, **Then** se persiste un `AuditRecord` con `Action=GameCreated`, `Actor` = identidad del creador, `Resource=Game`, `Result=Succeeded` y `Timestamp` de servidor.
2. **Given** un juego en `Draft`, **When** se reconfigura (`GameConfigured`), **Then** se registra `GameConfigured` con `Data` que refleja la configuración aplicada y `GameId` correlacionado.
3. **Given** un juego con jugadores en lobby, **When** se ejecuta `GameStarted` / `PlayerJoined` / `RoundStarted` / `QuestionPresented`, **Then** cada evento genera su `AuditRecord` correspondiente con `PlayerId` cuando aplica y `CorrelationId` que vincula la traza.
4. **Given** registros ya persistidos, **When** se intenta editar o borrar un `AuditRecord`, **Then** la operación es rechazada y el registro permanece inmutable.

---

### User Story 2 — Auditoría de jugadas y puntuación (Priority: P1)

Como responsable de integridad del juego, quiero que cada jugada y cada movimiento de puntos quede auditado con su resultado, de forma que una puntuación pueda ser explicada y defendida ante reclamos.

**Why this priority**: La puntuación es el activo más sensible (SC-007 de SPEC-013, SPEC-007 ledger). Sin audit de `AnswerSubmitted` → `AnswerEvaluated` → `PointsAwarded`/`PointsRemoved`, no hay forma de demostrar por qué un jugador ganó/perdió puntos. Es P1 junto con US1 porque ambos forman el MVP de trazabilidad del core gameplay.

**Independent Test**: Con un juego en ronda activa, enviar una respuesta, evaluarla y verificar los puntos. Consultar auditoría por `GameId` + `PlayerId`. Verificar que existen `AnswerSubmitted` (con `ResourceId` de la respuesta), `AnswerEvaluated` (con `Data` de corrección), `PointsAwarded` o `PointsRemoved` (con `Data` de delta y balance), cada uno con el mismo `CorrelationId` de la jugada y sin que la existencia del audit modifique la evaluación o el ledger.

**Acceptance Scenarios**:

1. **Given** una respuesta enviada por un jugador, **When** el servidor la recibe, **Then** se registra `AnswerSubmitted` con `Actor=PlayerId`, `Resource=Answer`, `Result` según validación, y nunca se usa ese registro para decidir si la respuesta es correcta.
2. **Given** una respuesta evaluada, **When** el motor determina corrección, **Then** se registra `AnswerEvaluated` con `Data` que incluye corrección y tiempo de servidor, separado del cálculo en sí.
3. **Given** una evaluación correcta, **When** se acreditan puntos, **Then** se registra `PointsAwarded` con `Data` del delta y referencia al `GameId`/`PlayerId`; si es incorrecta con penalización, se registra `PointsRemoved` análogo.
4. **Given** que la auditoría falle transitoriamente, **When** se intentó registrar `PointsAwarded`, **Then** la acreditación de puntos ya confirmada no se revierte por el fallo de audit (la trazabilidad es observabilidad, no gate de negocio).

---

### User Story 3 — Auditoría de salidas y cierre (Priority: P2)

Como auditor de juego, quiero que las salidas de jugadores y el cierre del juego, incluyendo recompensas y ajustes, queden trazados, de forma que una salida anticipada, una eliminación, un canje o una compensación no puedan ser repudiadas.

**Why this priority**: Cubre los eventos terminales y económicos, donde el riesgo de disputa es mayor. Depende de US1/US2 haber establecido el patrón append-only, pero entrega valor propio al cerrar el ciclo de vida con trazabilidad.

**Independent Test**: Con un juego en curso, provocar `PlayerWithdrawn`, `PlayerEliminated`, finalizar el juego, canjear una recompensa, otorgar `ConsolationGranted` y ejecutar un `AdministrativeAdjustment`. Filtrar auditoría por `Action` y por `GameId`. Verificar un registro por cada evento, con `Actor` correspondiente (jugador, sistema o administrador), `Result` y `Data` pertinente, y que `GameFinished` quede correlacionado con los `PlayerWithdrawn`/`PlayerEliminated` previos vía `CorrelationId`/`GameId`.

**Acceptance Scenarios**:

1. **Given** un jugador que se retira, **When** se ejecuta `PlayerWithdrawn`, **Then** se registra con `Actor=PlayerId`, `Resource=Player`, `GameId` y `Result` sin que ese registro condicione la política de retiro.
2. **Given** un jugador eliminado por regla, **When** ocurre `PlayerEliminated`, **Then** se registra con `Actor=System` o árbitro y `Data` con motivo.
3. **Given** un juego que finaliza, **When** se emite `GameFinished`, **Then** se registra con `Resource=Game` y los `PlayerId`/`GameId` afectados.
4. **Given** un canje de recompensa, **When** se ejecuta `RewardRedeemed`, **Then** se registra con `Resource=Reward`, `ResourceId` de la recompensa y `PlayerId`; de forma análoga `ConsolationGranted` para compensaciones.
5. **Given** un ajuste administrativo (ej. corrección de puntos por incidencia), **When** un ADMIN lo ejecuta, **Then** se registra `AdministrativeAdjustment` con `Actor=Admin`, `Data` del delta y justificación, sin que la lectura del audit pueda ejecutar un nuevo ajuste.

---

### User Story 4 — Búsqueda y trazabilidad transversal (Priority: P2)

Como investigador u operador, quiero buscar y trazar registros de auditoría por `GameId`, `PlayerId`, `Action`, `Resource`, `CorrelationId`, ventana temporal y paginación, de forma que pueda reconstruir la historia de una partida o de un jugador en segundos sin escanear todos los juegos.

**Why this priority**: Hace útil lo registrado: sin búsqueda y correlación, el volumen de eventos (16 tipos × N juegos × M jugadores) vuelve la auditoría inoperante. Es P2 porque presupone que los eventos ya se registran (US1–US3), pero entrega valor independiente como capacidad de consulta.

**Independent Test**: Generar 20 juegos con 50 eventos cada uno (mezcla de los 16 tipos), con `CorrelationId` compartido por flujo (ej. misma ronda). Luego ejecutar búsquedas: por `GameId` devuelve solo ese juego; por `PlayerId` filtra jugadas de ese jugador; por `Action=AnswerEvaluated` y `Resource=Game`; por `CorrelationId` recupera la secuencia exacta en orden temporal; por ventana `Timestamp` + paginación. Verificar que toda búsqueda es de solo lectura y que ningún filtro modifica estado.

**Acceptance Scenarios**:

1. **Given** 1000 registros con distintos `GameId`, **When** se busca por `GameId` específico, **Then** solo se retornan los de ese juego, ordenados por `Timestamp`.
2. **Given** registros con mismo `CorrelationId` a lo largo de una ronda, **When** se busca por ese `CorrelationId`, **Then** se recupera la traza completa en orden cronológico.
3. **Given** una búsqueda con paginación `page/pageSize` y filtros `Action`/`Resource`, **When** se solicita, **Then** se retorna página con `total` y sin duplicar ni perder registros entre páginas.
4. **Given** una consulta de auditoría, **When** se ejecuta, **Then** no produce ningún `AuditRecord` adicional sobre sí misma ni muta negocio.
5. **Given** un actor sin permiso `Audit.Read`, **When** intenta buscar, **Then** es rechazado antes de exponer datos (autorización transversal de SPEC-013).

---

### Edge Cases

- ¿Qué ocurre cuando un `GameId`/`PlayerId` de la petición no existe? Se registra el intento con `Result=Failed` y el trail no inventa un recurso; la búsqueda por ese id retorna vacío sin error.
- ¿Qué ocurre cuando un evento se registra dos veces por reintento de red (ej. doble `AnswerSubmitted`)? El segundo `AuditRecord` no se usa para decidir idempotencia del negocio (SPEC-013 `IdempotencyKey` manda); ambos intentos quedan registrados con distinto `Id` pero mismo `CorrelationId` y `Result` diferenciado.
- ¿Qué ocurre cuando `Data` contiene PII o secreto? El contenido de `Data` debe ser sanitizado/tuncado según política de SPEC-013 FR-020; no se registran tokens ni secretos.
- ¿Qué ocurre cuando el reloj de distintos nodos diverge? `Timestamp` es siempre de servidor en UTC al momento de persistir, no del cliente, y la búsqueda ordena por ese timestamp.
- ¿Qué ocurre cuando un `AdministrativeAdjustment` intenta corregir un evento pasado? Se registra como nuevo evento `AdministrativeAdjustment`, nunca reescribe el `AuditRecord` original (append-only).
- ¿Qué ocurre cuando la auditoría crece indefinidamente? La retención y purga son operaciones privilegiadas separadas y auditadas (ver SPEC-013 assumptions), no parte del flujo normal.
- ¿Qué ocurre cuando se consulta con ventana temporal muy amplia? La paginación y los índices por `GameId`/`Timestamp`/`CorrelationId` evitan escaneos completos; la API impone `pageSize` máximo.

## Requirements *(mandatory)*

### Functional Requirements

**Modelo de registro**

- **FR-001**: El sistema MUST persistir `AuditRecord` con exactamente los campos conceptuales: `Id` (identificador único), `Timestamp` (instante de servidor en UTC), `Actor` (identidad verificada, ej. `sub` o `system`), `Action` (uno de los 16 tipos), `Resource` (tipo de recurso: `Game`, `Round`, `Player`, `Question`, `Answer`, `Reward`, `Consolation`, etc.), `ResourceId` (id del recurso cuando aplica), `GameId` (id del juego cuando el evento está asociado a un juego), `PlayerId` (id del jugador cuando aplica), `CorrelationId` (id de traza del flujo), `Data` (payload JSON sanitizado con detalles del evento), `Result` (éxito/fracaso del intento).
- **FR-002**: El sistema MUST registrar exactamente los 16 `Action` auditables: `GameCreated`, `GameConfigured`, `GameStarted`, `PlayerJoined`, `RoundStarted`, `QuestionPresented`, `AnswerSubmitted`, `AnswerEvaluated`, `PointsAwarded`, `PointsRemoved`, `PlayerWithdrawn`, `PlayerEliminated`, `GameFinished`, `RewardRedeemed`, `ConsolationGranted`, `AdministrativeAdjustment`. No debe omitir ninguno ni registrar un `Action` fuera de este catálogo sin ADR.
- **FR-003**: Cada `AuditRecord` MUST incluir `GameId` cuando el evento pertenece a un juego y `PlayerId` cuando involucra a un jugador; cuando el evento es puramente administrativo o de sistema, esos campos MAY ser nulos, pero `Resource`/`ResourceId` MUST identificar el objeto afectado.

**Reglas de auditoría**

- **FR-004**: La auditoría MUST ser append-oriented: solo inserción de nuevos `AuditRecord`; nunca actualización parcial ni reescritura de un registro existente.
- **FR-005**: La auditoría MUST ser immutable: ningún actor, incluido `ADMIN`, puede modificar o borrar un `AuditRecord` ya persistido vía API normal; cualquier corrección se registra como nuevo `AdministrativeAdjustment` que referencia al evento original en `Data`/`CorrelationId`.
- **FR-006**: La auditoría MUST ser searchable: el sistema MUST exponer búsqueda paginada con filtros combinables por `GameId`, `PlayerId`, `Action`, `Resource`, `ResourceId`, `CorrelationId` y ventana `Timestamp` (`from`/`to`), con orden cronológico por `Timestamp` y paginación `page`/`pageSize`.
- **FR-007**: La auditoría MUST ser traceable: todo `AuditRecord` perteneciente al mismo flujo de negocio (ej. iniciar ronda → presentar pregunta → responder → evaluar → puntuar) MUST compartir el mismo `CorrelationId` propagado desde la petición/operación origen, y la búsqueda por `CorrelationId` MUST retornar la secuencia completa ordenada.
- **FR-008**: La auditoría MUST NOT utilizarse para modificar estado de negocio: la lectura, búsqueda o existencia de `AuditRecord` nunca debe condicionar, revertir o duplicar una transición de dominio, cálculo de puntos, canje o ajuste; los handlers de negocio no deben consultar auditoría para decidir.
- **FR-009**: El sistema MUST registrar cada intento relevante, tanto éxitos como fracasos (validación fallida, autorización denegada, rate limited, replay detectado), con `Result` que refleje el resultado y `Data`/`Reason` sin exponer secretos; los intentos rechazados antes de lógica de dominio también generan `AuditRecord` cuando son relevantes para trazabilidad (ej. `AnswerSubmitted` con `Result=Denied`).
- **FR-010**: La auditoría es transversal: todo SPEC previo (001–013) que ejecute uno de los 16 `Action` MUST generar su `AuditRecord` correspondiente sin requerir código ad-hoc disperso por feature; la implementación es centralizada (behavior/interceptor) y aplica a todos los flujos.

**Transversalidad y seguridad**

- **FR-011**: La consulta de auditoría MUST estar protegida por `Audit.Read` (y `Report.Read` para subconjuntos según SPEC-013); sin permiso, la búsqueda es denegada sin revelar existencia de registros.
- **FR-012**: El contenido de `Data` MUST ser sanitizado: nunca incluir `IsCorrect` antes de divulgación, tokens, secretos o PII no necesaria, conforme a SPEC-013 FR-020 y SPEC-012 filtrado.

### Key Entities

- **AuditRecord**: Registro inmutable y append-only de un intento de evento. Atributos: `Id` (PK), `Timestamp` (UTC servidor), `Actor` (identidad `sub` o `system`/`ADMIN`), `Action` (uno de 16), `Resource` (tipo), `ResourceId` (id del recurso), `GameId` (juego asociado), `PlayerId` (jugador asociado), `CorrelationId` (traza del flujo), `Data` (JSON sanitizado con detalles del evento), `Result` (Succeeded/Failed/Denied/RateLimited/ReplayDetected). No tiene comportamiento de negocio; solo expone creación append-only.
- **Game / Player / Round / Answer / Reward**: Entidades de dominio existentes que originan eventos auditables; no se modifican por auditoría, solo son referenciadas por `Resource`/`ResourceId`/`GameId`/`PlayerId`.
- **CorrelationId**: Identificador de traza (ej. `X-Correlation-ID` propagado). Permite agrupar `AuditRecord` de un mismo flujo transversal (de `RoundStarted` a `PointsAwarded`) para trazabilidad end-to-end.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El 100% de los 16 `Action` auditables generan un `AuditRecord` con todos los campos obligatorios (`Id`, `Timestamp`, `Actor`, `Action`, `Resource`, `GameId`/`PlayerId` cuando aplica, `CorrelationId`, `Data`, `Result`) sin omitir ninguno en una partida completa que ejercita todos los eventos.
- **SC-002**: El 0% de los `AuditRecord` puede ser modificado o borrado vía API normal; cualquier intento de `PUT`/`DELETE` sobre auditoría es rechazado y el registro original permanece inalterado.
- **SC-003**: El 100% de las búsquedas por `GameId`, `PlayerId`, `Action`, `Resource`, `CorrelationId` y ventana `Timestamp` retornan exactamente el conjunto esperado, paginado y ordenado por `Timestamp`, sin duplicar ni perder registros entre páginas.
- **SC-004**: Una traza con `CorrelationId` compartido es recuperable al 100% y en orden cronológico con una sola búsqueda, sin requerir escanear todos los juegos.
- **SC-005**: El 0% de las lecturas/búsquedas de auditoría produce un nuevo `AuditRecord` o modifica estado de negocio; el contador de registros no aumenta tras consultar.
- **SC-006**: Ninguna decisión de negocio (evaluación de respuesta, cálculo de puntos, canje, transición de estado) consulta `AuditRecord` para decidir; verificable por ausencia de dependencias `Audit` en handlers de dominio y por pruebas que demuestran que borrar hypotéticamente el audit no altera el resultado.
- **SC-007**: Bajo carga de 20 juegos × 50 eventos cada uno (1000 registros), una búsqueda por `GameId` o `CorrelationId` retorna en < 500 ms p95 y la inserción de un nuevo `AuditRecord` no incrementa la latencia de la operación de negocio en más de 50 ms p95.

## Assumptions

- Se reutiliza la infraestructura de auditoría introducida en SPEC-013 (`AuditEntry` append-only, `AuditBehavior`, `GET /api/audit`); este SPEC la extiende de auditoría de seguridad a auditoría de dominio completa con los 16 `Action` y el modelo `AuditRecord` conceptual (mapeable a `AuditEntry` con `Resource`/`ResourceId`/`GameId`/`PlayerId`/`Data`/`Result`).
- La autenticación y `Audit.Read`/`Report.Read` permanecen delegados a OroIdentityServer (Constitución VI); sin permiso, la búsqueda es 403 sin fuga.
- `Timestamp` es siempre de servidor en UTC al momento de persistir, no del cliente; `CorrelationId` se propaga vía `X-Correlation-ID` / `Activity` ya existente en `BuildingBlocks.ServiceDefaults`.
- `Data` es JSON sanitizado con detalles mínimos necesarios para traza (ej. delta de puntos, `questionId`, `answerOptionId`, motivo de eliminación), sin `IsCorrect` previo a divulgación ni secretos.
- Transversal significa que la implementación es centralizada (behavior/interceptor) y aplica a SPEC 001–013 y futuros sin código disperso; la generación de `AuditRecord` es best-effort observabilidad y no bloquea el negocio si falla (se loguea como warning, ver SPEC-013 edge case), pero el éxito de negocio ya confirmado no se revierte.
- Retención y purga de auditoría son operaciones privilegiadas fuera del flujo normal, auditadas a su vez, y no parte de los criterios de éxito inmediatos.
- Alcance single-node inicial; indexación por `GameId`/`Timestamp`/`CorrelationId` es suficiente para SC-007; backplane distribuido queda fuera hasta multi-nodo.
- La auditoría no reemplaza el ledger de puntos (SPEC-007) ni el `PointTransaction`; lo complementa con trazabilidad de quién/cuándo/por qué.

