# Feature Specification: Game Security

**Feature Branch**: `013-game-security`

**Created**: 2026-08-28

**Status**: Draft

**Input**: User description: "013 — Security Objetivo Definir seguridad, autenticación, autorización y protección de las operaciones del juego. Roles ADMIN GAME_MANAGER PLAYER REWARD_MANAGER Permisos Category.Read Category.Write Category.Publish Question.Read Question.Write Question.Publish Game.Create Game.Start Game.Play Reward.Read Reward.Redeem Reward.Manage Report.Read Audit.Read Reglas El servidor no debe confiar en: Score Correctness Time PlayerId GameState provenientes del cliente. Seguridad adicional Debe considerar: Rate limiting. Input validation. Idempotency. Anti-replay. Authorization policies. Audit trail. Protección contra manipulación de respuestas. Dependencias Transversal a todos los SPEC."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Autorización por roles y permisos (Priority: P1) 🎯 MVP

Como usuario autenticado del sistema, quiero que cada operación que intento esté protegida por el rol que poseo y el permiso que requiera, de forma que solo pueda ejecutar lo que me corresponde (administrar, gestionar juegos, jugar o gestionar recompensas) y cualquier intento no autorizado sea rechazado de forma clara sin exponer información sensible.

**Why this priority**: Es la base transversal de toda la plataforma: sin RBAC, cualquier protección posterior (validación, auditoría, anti-trampa) puede ser eludida suplantando rol/permiso. Entrega valor independiente: aun sin protecciones de tiempo/anti-replay, el sistema ya impide que un PLAYER publique categorías o que un usuario anónimo cree juegos.

**Independent Test**: Autenticar usuarios con cada rol (ADMIN, GAME_MANAGER, PLAYER, REWARD_MANAGER) mediante el proveedor externo de identidad y ejecutar operaciones de cada dominio. Verificar matriz: ADMIN accede a todo lo requerido, GAME_MANAGER solo a Category/Question/Game, PLAYER solo a Game.Play/Reward.Read/Redeem, REWARD_MANAGER solo a Reward.Manage/Report.Read; cualquier operación sin permiso retorna rechazo de autorización; operaciones sin autenticación son rechazadas antes de llegar a lógica de negocio.

**Acceptance Scenarios**:

1. **Given** un usuario autenticado como PLAYER, **When** intenta `Category.Publish` o `Question.Publish` o `Game.Start`, **Then** el sistema rechaza la operación por falta de permiso.
2. **Given** un usuario autenticado como GAME_MANAGER, **When** ejecuta `Category.Write`, `Question.Write`, `Game.Create` y `Game.Start`, **Then** el sistema permite la operación.
3. **Given** un usuario autenticado como REWARD_MANAGER, **When** ejecuta `Reward.Manage` o `Report.Read` o `Audit.Read`, **Then** el sistema permite la operación y deniega `Game.Play` ajeno.
4. **Given** una petición sin credenciales válidas o con token expirado/revocado, **When** intenta cualquier operación protegida, **Then** el sistema la rechaza como no autenticada sin revelar si el recurso existe.
5. **Given** un usuario autenticado, **When** intenta una operación para la que tiene rol pero no scope/tenant correspondiente, **Then** el sistema la deniega.

---

### User Story 2 — Servidor como única autoridad — anti-manipulación (Priority: P1)

Como operador del juego, quiero que el servidor nunca confíe en datos críticos enviados por el cliente (puntaje, corrección, tiempo, identidad de jugador, estado del juego), de forma que ningún cliente pueda autoadjudicarse puntos, falsificar tiempo de respuesta, suplantar a otro jugador o forzar transiciones de estado ilegales.

**Why this priority**: Es la protección de integridad más crítica del dominio: si el cliente pudiera imponer Score/Correctness/Time, todas las reglas de negocio (evaluación, scoring, recompensas) colapsan. Es independiente de RBAC: aun con roles correctos, un PLAYER no debe poder inyectar su propio puntaje.

**Independent Test**: Con un juego en curso, enviar desde el cliente valores manipulados: puntaje arbitrario en `SubmitAnswer`, `correct=true`, `elapsedTime` falso, `playerId` de otro jugador, `gameState=FINISHED` para forzar cierre. Verificar que el servidor ignora completamente esos campos, evalúa la respuesta usando su propia pregunta/opción correcta almacenada y su reloj de servidor, resuelve `PlayerId` desde la identidad autenticada y valida transiciones de estado solo contra su máquina de estados.

**Acceptance Scenarios**:

1. **Given** un envío de respuesta con campo `score` o `correctness` inventado, **When** el servidor lo procesa, **Then** ignora esos campos y calcula corrección y puntos exclusivamente desde la pregunta y reglas de dominio.
2. **Given** un envío con `elapsedTime` o `timeRemaining` manipulado, **When** el servidor evalúa, **Then** usa su propia marca temporal de recepción y límites configurados, no el tiempo del cliente.
3. **Given** un envío con `playerId` de otro jugador en el cuerpo, **When** el servidor procesa `Game.Play`, **Then** usa el identificador de la identidad autenticada y, si difiere, rechaza por suplantación.
4. **Given** una petición que intenta forzar `GameState` (ej. enviar estado `IN_PROGRESS` para saltarse validación), **When** el servidor valida, **Then** rechaza la transición por estado ilegal según su máquina de estados.
5. **Given** una respuesta que intenta reutilizar `questionId`/`answerOptionId` no pertenecientes a la ronda actual, **When** el servidor valida, **Then** rechaza por manipulación de respuestas.

---

### User Story 3 — Resiliencia operativa transversal (Priority: P2)

Como operador de plataforma, quiero que todas las operaciones sensibles estén protegidas contra abuso y errores repetidos mediante validación de entrada, idempotencia, anti-replay y limitación de tasa, de forma que el sistema permanezca disponible, consistente y justo bajo carga o reintentos.

**Why this priority**: Protege disponibilidad y equidad sin depender de que el cliente se comporte bien: evita que un cliente inunde el servidor, que un reintento de red duplique puntos/recompensas, o que un payload malformado corrompa estado. Depende de US1/US2 haber asegurado quién puede llamar y qué datos son autoritativos, pero entrega valor propio.

**Independent Test**: Con usuarios autenticados, enviar ráfagas de peticiones idénticas y malformadas: 50 envíos de la misma respuesta en 1s, payloads con campos faltantes/tipos incorrectos/rangos fuera de límite, y el mismo `Idempotency-Key` repetido. Verificar: reintentos idempotentes no duplican efectos (segundo envío retorna mismo resultado sin nuevo movimiento de puntos), entradas inválidas son rechazadas con error de validación claro, y el exceso de tasa es limitado con respuesta de límite excedido sin degradar a otros jugadores.

**Acceptance Scenarios**:

1. **Given** un cliente que envía dos peticiones idénticas con el mismo identificador de idempotencia para la misma operación sensible (respuesta, canje, creación), **When** el servidor recibe la segunda, **Then** no crea un segundo efecto y retorna el resultado original.
2. **Given** un cliente que reenvía una petición antigua o con identificador ya usado fuera de ventana (replay), **When** el servidor la detecta, **Then** la rechaza como replay sin ejecutar lógica de negocio.
3. **Given** un payload con campos faltantes, tipos incorrectos, longitudes o rangos inválidos, **When** el servidor valida, **Then** rechaza con error de validación que no expone detalles internos.
4. **Given** un cliente que excede el límite de peticiones configurado para una operación (ej. envíos por segundo por jugador/juego/IP), **When** continúa enviando, **Then** las peticiones excedentes son limitadas y el cliente recibe señal clara de reintento posterior.
5. **Given** operaciones concurrentes válidas de distintos jugadores, **When** se ejecutan cerca en el tiempo, **Then** el rate limiting no penaliza a jugadores inocentes y solo limita al emisor abusivo.

---

### User Story 4 — Auditoría y trazabilidad (Priority: P2)

Como auditor u operador, quiero que toda operación relevante deje un rastro inmutable, correlacionable y consultable (quién hizo qué, cuándo, sobre qué recurso y con qué resultado), de forma que pueda investigar incidentes, demostrar cumplimiento y reconstruir la historia de un juego sin depender de logs efímeros.

**Why this priority**: Es el requisito de observabilidad y cumplimiento que hace verificables a las historias anteriores: sin audit trail, no se puede probar que la autorización, la autoridad del servidor y las protecciones operativas realmente ocurrieron. Es independiente en el sentido de que el registro ocurre aun si las operaciones fueron rechazadas.

**Independent Test**: Ejecutar una partida completa con operaciones exitosas y rechazadas (intentos no autorizados, validaciones fallidas, replays bloqueados, límites excedidos) y consultar el registro de auditoría. Verificar que cada evento registra actor (identidad), acción/permiso evaluado, recurso (juego/ronda/pregunta), marca temporal de servidor, resultado y razón de rechazo cuando aplica; que el registro es append-only e inmutable y que usuarios sin `Audit.Read`/`Report.Read` no pueden consultarlo.

**Acceptance Scenarios**:

1. **Given** una operación sensible (crear juego, iniciar ronda, enviar respuesta, canjear recompensa, publicar categoría), **When** se ejecuta (éxito o rechazo), **Then** queda registrada con actor, permiso evaluado, recurso, tiempo de servidor y resultado.
2. **Given** un intento rechazado por autorización, validación, idempotencia o rate limiting, **When** se registra, **Then** el registro incluye la razón de rechazo sin exponer secretos (tokens, datos sensibles).
3. **Given** un usuario sin `Audit.Read`/`Report.Read`, **When** intenta consultar auditoría, **Then** el sistema lo deniega.
4. **Given** registros existentes, **When** se intenta modificar o borrar un registro, **Then** la operación es rechazada — el registro es inmutable.
5. **Given** un flujo de varias operaciones correlacionadas (misma partida/jugador), **When** se consulta con su identificador de correlación, **Then** se recupera la secuencia completa ordenada temporalmente.

---

### Edge Cases

- ¿Qué ocurre cuando un token es robado o expira a mitad de partida? El servidor debe rechazar inmediatamente toda operación posterior hasta re-autenticación, sin permitir que operaciones en vuelo usen estado obsoleto.
- ¿Qué ocurre cuando el cliente envía `PlayerId` suplantado pero también tiene rol ADMIN? Solo ADMIN/GAME_MANAGER pueden actuar en nombre de otro cuando la operación lo permite explícitamente; cualquier otro intento se deniega aunque el `PlayerId` exista.
- ¿Qué ocurre cuando un payload contiene campos extra no esperados (ej. `score`, `gameState`)? El servidor los ignora completamente — no los valida ni los persiste.
- ¿Qué ocurre cuando el reloj del cliente está desfasado o es manipulado? El servidor usa exclusivamente su reloj para ventanas de tiempo, duración de ronda y marcas de auditoría.
- ¿Qué ocurre cuando un atacante envía la misma respuesta correcta mil veces con el mismo `AnswerOptionId`? La idempotencia por `(GameId, PlayerId, RoundId)` evita duplicación de puntos, independientemente del contenido.
- ¿Qué ocurre cuando el rate limiting y la idempotencia compiten (reintento legítimo tras 429)? El reintento dentro de ventana no crea efecto duplicado y respeta el límite; el cliente debe respetar señal de espera.
- ¿Qué ocurre cuando la auditoría falla al escribirse? La operación de negocio original debe decidir su destino sin dejar el sistema en estado inconsistente ni perder trazabilidad crítica (el fallo se registra como evento de observabilidad y no se confirma éxito sin rastro cuando el requisito es estricto).
- ¿Qué ocurre con juegos simultáneos? Las políticas de autorización y límites se aplican por juego/jugador, no global glotón que penalice partidas inocentes.
- ¿Qué ocurre cuando un permiso es revocado a mitad de sesión? La siguiente petición con ese permiso debe ser denegada aunque el token anterior aún no haya expirado, según mecanismo de revocación/introspección vigente.

## Requirements *(mandatory)*

### Functional Requirements

**Autorización RBAC — roles y permisos**

- **FR-001**: El sistema MUST autenticar toda petición protegida mediante identidad externa delegada (Constitución VI — OroIdentityServer) y MUST rechazar peticiones no autenticadas sin revelar existencia de recursos.
- **FR-002**: El sistema MUST definir y hacer cumplir exactamente los roles `ADMIN`, `GAME_MANAGER`, `PLAYER`, `REWARD_MANAGER` y mapearlos a permisos; un rol no listado no puede ser asumido ni escalado por el cliente.
- **FR-003**: El sistema MUST hacer cumplir la matriz de permisos:
  - `Category.Read` — lectura de categorías (GAME_MANAGER, ADMIN, PLAYER según visibilidad configurada; por defecto lectura requiere autenticación)
  - `Category.Write` — crear/editar categorías → `GAME_MANAGER`, `ADMIN`
  - `Category.Publish` — publicar categoría → `GAME_MANAGER`, `ADMIN`
  - `Question.Read` — lectura de preguntas → `GAME_MANAGER`, `ADMIN` (PLAYER no accede directo fuera de `QuestionPresented` de ronda)
  - `Question.Write` — crear/editar preguntas → `GAME_MANAGER`, `ADMIN`
  - `Question.Publish` — publicar pregunta → `GAME_MANAGER`, `ADMIN`
  - `Game.Create` — crear juego → `GAME_MANAGER`, `ADMIN`
  - `Game.Start` — iniciar/preparar juego, abrir lobby, iniciar/completar ronda, forzar cierre → `GAME_MANAGER`, `ADMIN`
  - `Game.Play` — unirse, responder, retirarse, consultar estado propio/leaderboard de su juego → `PLAYER` (sobre su propia participación), `ADMIN`/`GAME_MANAGER` como observador/autorizado
  - `Reward.Read` — consultar recompensas → todos autenticados
  - `Reward.Redeem` — canjear recompensa → `PLAYER` sobre sus propios puntos
  - `Reward.Manage` — crear/gestionar recompensas y aprobaciones → `REWARD_MANAGER`, `ADMIN`
  - `Report.Read` — reportes operativos → `REWARD_MANAGER`, `ADMIN`, `GAME_MANAGER` según reporte
  - `Audit.Read` — consulta de auditoría → `ADMIN` (y `GAME_MANAGER` limitado a sus juegos si se configura)
- **FR-004**: El sistema MUST denegar por defecto (deny-by-default): toda operación sin permiso explícito mapeado es denegada, y los mensajes de denegación MUST NOT filtrar si el recurso existe o no cuando el solicitante no está autorizado a saberlo.
- **FR-005**: El sistema MUST validar autorización a nivel de recurso además de rol: poseer `Game.Play` no permite operar sobre un juego donde el actor no es participante (excepto `ADMIN`/`GAME_MANAGER` con alcance explícito).

**Servidor como autoridad — anti-tampering**

- **FR-006**: El sistema MUST ignorar completamente cualquier `Score`, `Correctness`, `Time`/`elapsedTime`/`timeRemaining`, `PlayerId` o `GameState` proveniente del cuerpo del cliente y MUST resolver cada uno desde fuente autoritativa: corrección desde la pregunta/answerOptions almacenadas, puntos desde reglas de scoring/ledger en servidor, tiempo desde reloj de servidor, identidad desde token autenticado, estado desde máquina de estados del agregado.
- **FR-007**: El sistema MUST resolver `PlayerId` para `Game.Play` exclusivamente desde la identidad autenticada (`sub` claim); un `playerId` en el cuerpo solo se permite cuando la operación documenta suplantación autorizada y el actor posee privilegio explícito (`ADMIN`/`GAME_MANAGER`), de lo contrario MUST ser rechazado.
- **FR-008**: El sistema MUST validar toda transición de estado de juego/ronda contra la máquina de estados autoritativa; cualquier `GameState` enviado por el cliente MUST ser ignorado y una transición ilegal MUST ser rechazada antes de mutar estado.
- **FR-009**: El sistema MUST validar que toda respuesta referencie una `questionId`/`answerOptionId` perteneciente a la ronda actual del juego solicitante; referencias fuera de ronda/juego o inexistentes MUST ser rechazadas como manipulación.
- **FR-010**: El sistema MUST nunca incluir en respuestas dirigidas a jugadores información que revele `IsCorrect` antes de divulgación oficial (ya cubierto por SPEC-012 `QuestionPresented` filtrado y `PlayerAnswered` sin correctitud), y MUST re-validar en servidor aunque el cliente afirme conocer la respuesta correcta.

**Validación, idempotencia y anti-replay**

- **FR-011**: El sistema MUST validar toda entrada en tres niveles (transporte, caso de uso, invariante de dominio) y MUST rechazar payloads malformados/tipos incorrectos/rangos/longitudes inválidas con errores de validación que no expongan detalles internos.
- **FR-012**: El sistema MUST tratar como idempotentes las operaciones sensibles sensibles a duplicación por reintento de red: segundo envío con el mismo identificador lógico `(GameId, PlayerId, RoundId)` para respuestas, o mismo `Idempotency-Key` para canjes/creaciones donde aplique, MUST retornar el resultado original sin crear un segundo efecto (puntos, ledger, canje).
- **FR-013**: El sistema MUST implementar protección anti-replay: identificadores de idempotencia fuera de ventana, reutilizados con payload distinto, o con marca temporal antigua/futura fuera de tolerancia, MUST ser rechazados sin ejecutar lógica de negocio.
- **FR-014**: El sistema MUST aplicar rate limiting por actor y por recurso (por jugador, por juego y por IP/identidad según operación) para `Game.Play` (envíos de respuesta) y operaciones sensibles; el exceso MUST ser limitado con señal de reintento y sin degradar a actores inocentes.
- **FR-015**: El sistema MUST aislar límites por juego/jugador: una ráfaga abusiva en un juego no penaliza a jugadores de otros juegos ni a operaciones de lectura inocuas.

**Auditoría y políticas**

- **FR-016**: El sistema MUST registrar de forma append-only e inmutable todo intento de operación relevante (éxito, denegación por autorización, fallo de validación, bloqueo por idempotencia/anti-replay/rate limit) con: actor (identidad), acción/permiso evaluado, recurso (juego/ronda/pregunta/recompensa), marca temporal de servidor, resultado y razón de rechazo (sin secretos).
- **FR-017**: El sistema MUST proteger la inmutabilidad de auditoría: ningún actor puede modificar o borrar registros; solo append y lectura autorizada (`Audit.Read`/`Report.Read`).
- **FR-018**: El sistema MUST correlacionar operaciones de la misma traza/partida/jugador mediante identificador de correlación propagado y consultable para reconstruir secuencias ordenadas.
- **FR-019**: El sistema MUST aplicar políticas de autorización declarativas y centralizadas (no dispersas ad-hoc por endpoint) y MUST registrar la política evaluada en auditoría para cada decisión.
- **FR-020**: El sistema MUST sanitizar y registrar de forma segura: nunca registrar secretos (tokens, secretos de cliente), y los errores de autorización/validación MUST NOT revelar existencia de recursos ni detalles de implementación.

### Key Entities

- **Principal autenticado**: Identidad verificada contra OroIdentityServer (subject `sub`, roles, claims, tenant, expiración). Fuente única de `PlayerId` y de evaluación de permisos; nunca se deriva de campos del cuerpo.
- **Rol**: Conjunto nominal (`ADMIN`, `GAME_MANAGER`, `PLAYER`, `REWARD_MANAGER`) que agrupa permisos. Asignado externamente, validado en cada petición; no escalable por el cliente.
- **Permiso**: Capacidad atómica (`Category.Publish`, `Game.Play`, `Reward.Redeem`, etc.). Unidad de autorización evaluada por política; mapeada a uno o más roles según FR-003.
- **Política de autorización**: Regla declarativa que combina rol + permiso + alcance de recurso (ej. "Game.Play sobre su propia participación"). Evaluada centralmente y auditada.
- **Operación sensible**: Cualquier comando que muta estado de juego, puntaje, recompensa o configuración. Sujeta a validación, idempotencia, anti-replay, rate limiting y auditoría.
- **Registro de auditoría**: Evento inmutable append-only con actor, acción/permiso, recurso, timestamp de servidor, resultado y razón. No editable, consultable solo con `Audit.Read`/`Report.Read`.
- **Ventana de idempotencia / anti-replay**: Identificador lógico y ventana temporal que permiten detectar reintentos y replays sin re-ejecutar efectos.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El 100% de las operaciones mapeadas en FR-003 son denegadas cuando el actor carece del permiso requerido y permitidas cuando lo posee (matriz de 14 permisos × 4 roles verificada).
- **SC-002**: El 0% de los intentos de manipulación de `Score`/`Correctness`/`Time`/`PlayerId`/`GameState` desde el cliente tienen efecto: el estado resultante coincide exactamente con el cálculo autoritativo del servidor en el 100% de los casos.
- **SC-003**: El 100% de los envíos duplicados idempotentes (mismo `(GameId, PlayerId, RoundId)` o mismo `Idempotency-Key`) retornan el resultado original sin crear un segundo efecto observable (puntos, ledger, canje, creación).
- **SC-004**: El 100% de los replays fuera de ventana o con payload divergente son rechazados sin ejecutar lógica de negocio.
- **SC-005**: Bajo ráfaga de 50 peticiones idénticas en 1 segundo del mismo jugador hacia la misma operación sensible, solo 1 efecto es persistido y el resto son respuestas idempotentes o limitadas, sin duplicación.
- **SC-006**: El 100% de los intentos de acceso sin autenticación válida o con token expirado/revocado son rechazados antes de lógica de dominio, sin fuga de existencia de recursos.
- **SC-007**: El 100% de las operaciones relevantes (éxito y rechazo) generan un registro de auditoría inmutable con actor, permiso evaluado, recurso, timestamp de servidor y resultado, recuperable por correlación y no modificable.
- **SC-008**: Ninguna respuesta de autorización/validación revela existencia de recursos a actores no autorizados ni expone secretos/tokens en mensajes o registros.
- **SC-009**: El rate limiting aísla por juego/jugador: una ráfaga abusiva en un juego no incrementa la tasa de rechazo ni la latencia de jugadores de otros juegos en más de 5% durante la ventana de prueba.

## Assumptions

- La autenticación y emisión/validación de tokens permanece delegada a OroIdentityServer (Constitución VI); este SPEC no crea un nuevo proveedor de identidad ni almacena credenciales localmente.
- Los roles `ADMIN`, `GAME_MANAGER`, `PLAYER`, `REWARD_MANAGER` existen como claims `role`/`roles` en el JWT emitido por OroIdentityServer; la asignación/gestión de roles ocurre externamente.
- La matriz de permisos de FR-003 es la autoritativa para esta versión; permisos adicionales futuros se añaden extendiendo la matriz sin romper los existentes.
- Por defecto, toda operación requiere autenticación salvo `health`/`alive` y well-known endpoints explícitamente documentados; el sistema es deny-by-default.
- Los campos `Score`, `Correctness`, `Time`, `PlayerId`, `GameState` en payloads de cliente son considerados no confiables por diseño; el cliente puede enviarlos pero el servidor los ignora (compatibilidad hacia atrás sin efecto).
- La ventana de idempotencia/anti-replay y los umbrales de rate limiting son configurables por entorno (valores por defecto razonables documentados en plan); no se asumen valores fijos globales en el spec.
- La auditoría es append-only y retenida según política de retención del entorno (mínimo operativo: toda la vida del juego + periodo de cumplimiento); la purga, si existe, es operación privilegiada separada y auditada.
- Este SPEC es transversal: sus requisitos se aplican a todos los SPEC existentes (001–012) y futuros; la implementación puede ser incremental por dominio pero los criterios de éxito se verifican end-to-end.
- El alcance inicial es single-node (mismo umbral que SPEC-012); rate limiting distribuido/backplane queda fuera hasta que el despliegue multi-nodo lo requiera.

