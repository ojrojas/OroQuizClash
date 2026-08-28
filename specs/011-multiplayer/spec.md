# Feature Specification: Multiplayer

**Feature Branch**: `011-multiplayer`

**Created**: 2026-08-27

**Status**: Draft

**Input**: User description: "011 — Multiplayer Objetivo Definir la participación concurrente de múltiples jugadores dentro del mismo juego. Conceptos Game ├── Player A ├── Player B ├── Player C └── Player N Reglas Cada jugador debe tener: PlayerId GameId Status Score CurrentRound AnswerState Los jugadores no pueden modificar información de otros jugadores. Concurrencia Debe contemplarse: A responde B responde C responde simultáneamente. Protección Debe existir: Optimistic Concurrency Idempotency Atomic score updates Leaderboard Debe poder obtenerse: Rank Player Points CorrectAnswers CurrentLevel Status Dependencias SPEC-004 SPEC-005 SPEC-006 SPEC-007"

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Participación concurrente con estado individual aislado (Priority: P1)

Como jugador, quiero participar en un juego junto a otros jugadores (A, B, C, ... N), teniendo cada uno su propio estado de participación — `PlayerId`, `GameId`, `Status`, `Score`, `CurrentRound`, `AnswerState` — que evoluciona de forma independiente a medida que avanza la partida, de forma que mi progreso sea mío y refleje fielmente mi desempeño.

**Why this priority**: Es el contrato central del multiplayer: sin estado individual por jugador no existe participación concurrente ni se puede construir el leaderboard. Entrega valor independiente como registro vivo de la participación de cada jugador en un juego.

**Independent Test**: Con un juego en `WAITING_FOR_PLAYERS` (SPEC-004), unir N jugadores (hasta `MaxPlayers`). Al iniciar el juego, verificar que cada jugador tiene su propio registro de participación con los 6 atributos requeridos, que cada `Score` inicia en 0, `CurrentRound` avanza con cada ronda para los jugadores activos, y `AnswerState` refleja el estado de respuesta del jugador en la ronda actual. Verificar que el estado de cada jugador evoluciona independientemente (uno responde correcto, otro incorrecto, otro no responde → tres estados distintos).

**Acceptance Scenarios**:

1. **Given** un juego en `WAITING_FOR_PLAYERS` con capacidad para N jugadores, **When** N jugadores se unen al juego, **Then** cada uno queda registrado con su `PlayerId`, `GameId`, `Status` activo, `Score` inicial 0, `CurrentRound` inicial (sin ronda activa) y `AnswerState` sin respuesta.
2. **Given** un juego iniciado con jugadores A, B y C en una ronda activa, **When** la ronda avanza, **Then** el `CurrentRound` de cada jugador activo se actualiza a la ronda en curso y cada jugador mantiene su propio `Score` acumulado.
3. **Given** jugadores A, B y C en la misma ronda, **When** A responde correctamente, B responde incorrectamente y C no responde antes del límite de tiempo, **Then** el `AnswerState` de A queda evaluado como correcto, el de B como incorrecto y el de C como expirado, y sus `Score` reflejan resultados distintos e independientes.
4. **Given** un jugador que se retira voluntariamente (SPEC-008) o es eliminado por política de pérdida, **When** su participación termina, **Then** su `Status` cambia a retirado o eliminado, su `CurrentRound` queda congelado en la última ronda alcanzada y su `Score` conserva el resultado según la política aplicable.
5. **Given** un intento de unión duplicada del mismo usuario al mismo juego o un intento de unión cuando el juego está lleno (`MaxPlayers`), **When** se envía, **Then** el sistema rechaza o resuelve idempotentemente sin crear participaciones duplicadas (SPEC-004).

---

### User Story 2 — Respuestas simultáneas sin interferencia (Priority: P1)

Como jugador en una partida multiplayer, quiero poder enviar mi respuesta al mismo tiempo que los demás jugadores (A responde, B responde, C responde simultáneamente), con la garantía de que cada respuesta se procesa de forma independiente, atómica y sin que la mía se pierda, se retrase indebidamente o se vea afectada por las de otros.

**Why this priority**: La concurrencia simultánea es el escenario definitorio del multiplayer. Si las respuestas simultáneas se pisan, se pierden o se corrompen, el juego es injusto y no es jugable. Entrega valor independiente como garantía de integridad bajo concurrencia.

**Independent Test**: Con una ronda activa y los N jugadores del juego, simular que todos envían su respuesta dentro de la misma ventana de tiempo (envíos simultáneos). Verificar que 100% de los envíos válidos se evalúan, que cada jugador recibe su propio resultado (correcto/incorrecto/expirado), que no hay actualizaciones perdidas ni duplicadas en ningún `Score`, y que el tiempo de respuesta percibido por jugador no se degrada por la concurrencia.

**Acceptance Scenarios**:

1. **Given** una ronda activa con jugadores A, B y C, **When** los tres envían su respuesta simultáneamente dentro del límite de tiempo, **Then** el sistema evalúa las tres respuestas de forma independiente y cada jugador obtiene su propio resultado con su correspondiente actualización de `Score`.
2. **Given** envíos simultáneos de A, B y C, **When** se procesan, **Then** la actualización de `Score` de cada jugador es atómica: ningún jugador recibe puntos de otro, ningún punto se pierde y ningún punto se duplica.
3. **Given** un jugador A cuya respuesta llega exactamente al límite de tiempo mientras otros también envían, **When** se evalúa, **Then** la decisión de tiempo (válida/expirada) se toma con criterio del servidor para A, sin verse afectada por el orden de llegada de los envíos de B y C.
4. **Given** un jugador que envía su respuesta y el sistema detecta un conflicto de concurrencia sobre su propio estado, **When** se procesa, **Then** el sistema resuelve sin corromper datos: el envío se reintenta o se rechaza como conflicto recuperable, y el jugador puede consultar su estado autoritativo actualizado.
5. **Given** un jugador que reenvía la misma respuesta para la misma ronda (duplicado por reintento de red), **When** el duplicado llega, **Then** el sistema lo trata idempotentemente: retorna el mismo resultado original sin duplicar respuestas ni puntos.

---

### User Story 3 — Aislamiento entre jugadores (protección contra manipulación) (Priority: P1)

Como sistema autoritativo y como jugador honesto, quiero que ningún jugador pueda modificar ni alterar la información de otro jugador (respuesta, puntaje, estado, ronda), y que el servidor haga cumplir esta regla usando la identidad autenticada, de forma que el juego sea justo y a prueba de trampas.

**Why this priority**: Es una regla explícita del objetivo ("Los jugadores no pueden modificar información de otros jugadores") y un mandato constitucional de aislamiento de estado multiplayer. Sin aislamiento, cualquier cliente malicioso podría alterar puntajes ajenos. Entrega valor independiente como garantía de integridad y anti-cheating.

**Independent Test**: Con jugadores A y B en un juego, intentar (como A) enviar una respuesta en nombre de B, modificar el `Score` de B, cambiar el `Status` de B, o alterar el `AnswerState` de B. Verificar que 100% de esos intentos se rechazan con error de autorización/identidad, que el estado de B permanece intacto y que el intento queda auditado.

**Acceptance Scenarios**:

1. **Given** jugador A autenticado en un juego con jugador B, **When** A intenta enviar una respuesta haciéndose pasar por B (identidad de jugador distinta a la suya), **Then** el sistema rechaza la operación por discrepancia de identidad y el estado de B no cambia.
2. **Given** jugador A autenticado, **When** A intenta modificar directamente el `Score`, `Status`, `CurrentRound` o `AnswerState` de B, **Then** el sistema rechaza la operación; todo cambio de estado de un jugador solo ocurre como consecuencia de acciones autoritativas del servidor sobre el propio jugador.
3. **Given** jugador A autenticado, **When** A consulta información del juego, **Then** A puede ver su propio estado completo y los datos públicos del leaderboard, pero no puede ver el detalle privado de las respuestas de B (opción elegida por B, estado de respuesta en curso); el servidor nunca confía en valores de puntaje o correctitud enviados por el cliente.
4. **Given** un cliente que envía valores calculados (puntos, correctitud, nivel) en nombre de cualquier jugador, **When** se procesa, **Then** el sistema ignora/rechaza esos valores y los recalcula server-side (SPEC-006, SPEC-007).

---

### User Story 4 — Leaderboard en vivo del juego (Priority: P2)

Como jugador y como organizador, quiero poder obtener el leaderboard del juego en cualquier momento con `Rank`, `Player`, `Points`, `CorrectAnswers`, `CurrentLevel` y `Status` de cada participante, de forma que la clasificación sea transparente, determinista y refleje fielmente el estado evaluado de la partida.

**Why this priority**: El leaderboard es la visualización competitiva del multiplayer y un requisito explícito del objetivo, pero depende de que el estado individual y la puntuación (US1-US3, SPEC-007) existan. Entrega valor independiente como consulta de clasificación del juego.

**Independent Test**: Con un juego en curso con varios jugadores y puntajes distintos tras algunas rondas evaluadas, consultar el leaderboard. Verificar que retorna una entrada por jugador con `Rank`, `Player`, `Points`, `CorrectAnswers`, `CurrentLevel`, `Status`; que el orden es determinista (mayor puntaje primero, con desempate definido); que los valores coinciden exactamente con el ledger de puntos (SPEC-007); y que el leaderboard se actualiza tras cada evaluación de ronda.

**Acceptance Scenarios**:

1. **Given** un juego con jugadores A (300 puntos, 3 correctas), B (500 puntos, 4 correctas) y C (100 puntos, 1 correcta) tras rondas evaluadas, **When** se consulta el leaderboard, **Then** retorna B en `Rank` 1, A en `Rank` 2, C en `Rank` 3, cada uno con sus `Points`, `CorrectAnswers`, `CurrentLevel` y `Status`.
2. **Given** dos jugadores empatados en `Points`, **When** se calcula el leaderboard, **Then** el desempate se aplica de forma determinista (mayor número de respuestas correctas; si persiste el empate, el que alcanzó antes el puntaje), produciendo siempre el mismo orden para el mismo estado del juego.
3. **Given** un jugador retirado o eliminado, **When** se consulta el leaderboard, **Then** el jugador aparece con su `Status` correspondiente y su puntaje final, sin ser eliminado del histórico de clasificación.
4. **Given** una ronda en curso aún no evaluada, **When** se consulta el leaderboard, **Then** muestra el último snapshot consistente (resultados evaluados hasta la ronda anterior), nunca un ranking parcial o corrupto de la ronda en curso.
5. **Given** el juego finaliza (`FINISHED`), **When** se consulta el leaderboard final, **Then** refleja los puntajes definitivos con el `Status` final de cada jugador (incluyendo ganador/es según SPEC-008) y permanece estable.

---

### User Story 5 — Protección de integridad: concurrencia optimista, idempotencia y atomicidad (Priority: P2)

Como sistema, quiero que toda mutación de estado de un jugador esté protegida por concurrencia optimista, idempotencia y actualizaciones atómicas de puntaje, de forma que bajo cualquier combinación de envíos simultáneos, reintentos y fallos, el estado resultante sea siempre consistente y auditable.

**Why this priority**: Es el mecanismo de protección exigido por el objetivo; complementa US2/US3 desde la perspectiva de recuperación ante conflictos y fallos. Entrega valor independiente como garantía de consistencia bajo condiciones adversas.

**Independent Test**: Someter el estado de un jugador a: (a) dos mutaciones simultáneas sobre el mismo estado → una gana y la otra recibe conflicto recuperable; (b) el mismo comando enviado dos veces → un solo efecto; (c) actualización de puntaje concurrente con otra operación del mismo jugador → el ledger queda consistente y reconstruible. Verificar que en todos los casos el `Score` final coincide con la suma del ledger y que los conflictos se reportan como errores recuperables, no como corrupción.

**Acceptance Scenarios**:

1. **Given** dos operaciones concurrentes que intentan mutar el mismo estado de un jugador (misma versión base), **When** ambas llegan, **Then** exactamente una tiene éxito y la otra recibe un error de conflicto recuperable; no hay actualización perdida ni estado inconsistente.
2. **Given** un comando de envío de respuesta duplicado (mismo jugador, misma ronda, misma clave de idempotencia), **When** se procesa por segunda vez, **Then** retorna el resultado original sin crear una segunda respuesta ni una segunda transacción de puntos.
3. **Given** una actualización de puntaje para un jugador, **When** se ejecuta, **Then** es atómica: o se registra completa (respuesta evaluada + transacción de puntos + estado actualizado) o no se registra nada; nunca queda un punto aplicado sin su transacción de ledger (SPEC-007).
4. **Given** un jugador cuyo estado quedó desactualizado en el cliente tras un conflicto, **When** consulta su estado, **Then** el sistema le retorna el estado autoritativo actual para reintentar o continuar.

---

### Edge Cases

- ¿Qué sucede cuando todos los jugadores envían respuesta exactamente en el mismo instante? Todos los envíos válidos se procesan de forma independiente; el orden de llegada no altera el resultado de cada jugador (la ventana de tiempo se evalúa individualmente contra el servidor).
- ¿Qué sucede cuando un jugador reenvía su respuesta varias veces por fallos de red? Idempotencia por jugador + ronda: el primer envío válido gana; los duplicados retornan el mismo resultado sin efectos adicionales.
- ¿Qué sucede cuando el jugador A intenta actuar sobre el jugador B (suplantación o manipulación)? Rechazo por discrepancia de identidad/autorización, estado de B intacto, intento auditado.
- ¿Qué sucede cuando hay empate de puntos en el leaderboard? Desempate determinista: mayor `CorrectAnswers`; si persiste, el jugador que alcanzó antes el puntaje final; el orden es estable entre consultas con el mismo estado.
- ¿Qué sucede cuando un jugador se retira o es eliminado en medio de una ronda? Su `AnswerState` de la ronda en curso queda sin respuesta válida (no puntúa), su `Status` pasa a retirado/eliminado (SPEC-008), su `CurrentRound` se congela y sigue apareciendo en el leaderboard con su resultado final.
- ¿Qué sucede cuando un jugador retirado o eliminado intenta enviar respuesta? Se rechaza con error de jugador no activo (SPEC-006 `PlayerNotInGame`).
- ¿Qué sucede cuando el juego alcanza `MaxPlayers` y otro usuario intenta unirse? Se rechaza por juego lleno (SPEC-004).
- ¿Qué sucede cuando todos los jugadores quedan retirados o eliminados antes de terminar? No quedan jugadores activos; la resolución del juego (finalización forzada) se delega en SPEC-004.
- ¿Qué sucede cuando se consulta el leaderboard mientras una ronda está en curso? Se retorna el último snapshot consistente evaluado; los puntos de la ronda en curso no se muestran hasta su evaluación.
- ¿Qué sucede cuando dos operaciones de puntaje del mismo jugador concurren (ej. respuesta correcta + bono de ronda)? Se serializan sin pérdida; el saldo final es reconstruible desde el ledger.
- ¿Qué sucede cuando el cliente envía valores de puntaje, correctitud o nivel calculados por él? El servidor los ignora y recalcula todo server-side; el cliente es solo presentación.
- ¿Qué sucede cuando un jugador se une dos veces (doble clic)? La unión es idempotente por usuario + juego o se rechaza por duplicado (SPEC-004); nunca se crean dos participaciones del mismo usuario en el mismo juego.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema MUST modelar la participación de cada jugador en un juego con exactamente estos atributos de estado individual: `PlayerId` (identidad del jugador), `GameId` (juego al que pertenece), `Status` (estado de participación), `Score` (puntaje del jugador en el juego), `CurrentRound` (ronda actual del jugador) y `AnswerState` (estado de respuesta del jugador en la ronda actual).
- **FR-002**: El sistema MUST gestionar el ciclo de vida del `Status` del jugador: activo mientras participa, retirado si se retira voluntariamente (SPEC-008), eliminado si una política de pérdida lo elimina, y ganador al finalizar el juego si corresponde; un jugador en estado terminal (retirado/eliminado) MUST NOT volver a estado activo ni aceptar más respuestas.
- **FR-003**: El sistema MUST garantizar que cada jugador solo pueda afectar su propio estado: toda operación que mute estado de un jugador MUST validar que la identidad autenticada del solicitante corresponde al jugador afectado; intentos de afectar a otro jugador MUST rechazarse con error de identidad/autorización y auditarse.
- **FR-004**: El sistema MUST proteger el detalle privado del estado de cada jugador: la respuesta concreta elegida y el detalle del `AnswerState` en curso de un jugador MUST NOT ser visibles para otros jugadores; solo el propio jugador y roles organizadores pueden consultarlo. La exposición pública entre jugadores se limita a los datos del leaderboard (`Rank`, `Player`, `Points`, `CorrectAnswers`, `CurrentLevel`, `Status`).
- **FR-005**: El sistema MUST procesar respuestas simultáneas de múltiples jugadores en la misma ronda de forma independiente: el envío de un jugador MUST NOT bloquear, corromper ni alterar el resultado de otro; todos los envíos válidos dentro de la ventana de tiempo MUST evaluarse.
- **FR-006**: El sistema MUST proteger toda mutación de estado del jugador con concurrencia optimista (control de versión): dos escrituras concurrentes sobre la misma versión MUST resolverse con exactamente un ganador y un error de conflicto recuperable para el perdedor; MUST NOT existir actualización perdida ni estado inconsistente.
- **FR-007**: El sistema MUST garantizar idempotencia en el envío de respuestas: envíos duplicados del mismo jugador para la misma ronda (misma clave de idempotencia) MUST retornar el resultado original sin duplicar respuestas ni transacciones de puntos.
- **FR-008**: El sistema MUST realizar actualizaciones atómicas de puntaje: todo cambio de `Score` MUST registrarse como transacción de ledger (SPEC-007) dentro de la misma operación atómica que la evaluación de la respuesta; MUST NOT existir punto aplicado sin transacción ni transacción sin evaluación; el saldo del jugador MUST ser reconstruible desde el ledger.
- **FR-009**: El sistema MUST mantener el `AnswerState` de cada jugador por ronda según los estados de respuesta definidos en SPEC-006 (sin respuesta → respondida → evaluada / expirada), actualizándose server-side conforme el jugador envía su respuesta o expira el tiempo.
- **FR-010**: El sistema MUST mantener el `CurrentRound` de cada jugador: avanza con cada nueva ronda para los jugadores activos; para jugadores retirados o eliminados MUST congelarse en la última ronda alcanzada.
- **FR-011**: El sistema MUST exponer un leaderboard por juego con exactamente estos datos por jugador: `Rank`, `Player`, `Points`, `CorrectAnswers`, `CurrentLevel`, `Status`. El orden MUST ser determinista: mayor `Points` primero; en caso de empate, mayor `CorrectAnswers`; si persiste, el jugador que alcanzó antes dicho puntaje. El leaderboard MUST ser consistente con el ledger de puntos.
- **FR-012**: El sistema MUST mantener el leaderboard actualizado tras cada evaluación de ronda, mostrando siempre un snapshot consistente; los resultados de una ronda en curso MUST NOT aparecer hasta que la ronda sea evaluada. Al finalizar el juego, el leaderboard final MUST permanecer estable.
- **FR-013**: El sistema MUST hacer cumplir los límites de participación: unión solo mientras el juego la permite (SPEC-004), sin duplicados (un usuario = una participación por juego) y respetando `MinPlayers`/`MaxPlayers` de la configuración (SPEC-001).
- **FR-014**: El sistema MUST notificar los cambios relevantes de estado multiplayer a los participantes mediante notificaciones server-driven: unión de jugador, actualización de puntaje, actualización de leaderboard, cambio de estado de jugador (retirado/eliminado/ganador). Las notificaciones MUST NOT ser fuente de verdad; el estado autoritativo es el persistido.
- **FR-015**: El sistema MUST permitir a cada jugador consultar su propio estado autoritativo en cualquier momento del juego (`Status`, `Score`, `CurrentRound`, `AnswerState`), incluyendo tras un conflicto de concurrencia, para recuperar la vista correcta.
- **FR-016**: El sistema MUST auditar (append-only) los eventos de participación multiplayer: unión de jugador, cambios de estado (retiro/eliminación/ganador), actualizaciones de puntaje y conflictos de concurrencia, con identificación de juego, jugador, ronda, comando, actor y marca de tiempo.

### Key Entities *(include if feature involves data)*

- **GamePlayer (participación de jugador en un juego)**: Estado individual de cada jugador dentro de un juego. Atributos: `PlayerId` (identidad del jugador, derivada de la identidad autenticada), `GameId`, `Status` (activo, retirado, eliminado, ganador), `Score` (puntaje acumulado en el juego, derivado del ledger), `CurrentRound` (ronda actual o última alcanzada), `AnswerState` (estado de respuesta en la ronda actual), `JoinedAt`, versión de concurrencia. Restricciones: único por usuario y juego; mutable solo por comportamiento autoritativo del dominio; aislado entre jugadores (FR-003, FR-004).
- **PlayerStatus (Enumeration)**: Estados de participación del jugador: activo (en juego), retirado (SPEC-008), eliminado (por política de pérdida), ganador (al finalizar el juego si corresponde). Transiciones terminales: retirado y eliminado no vuelven a activo.
- **AnswerState (estado de respuesta por jugador y ronda)**: Referencia al ciclo de vida de respuesta de SPEC-006 (sin respuesta, respondida, evaluada, expirada) aplicado al jugador en su ronda actual. Determina si el jugador ya respondió, está pendiente, o expiró su ventana de tiempo.
- **LeaderboardEntry (entrada de clasificación)**: Fila del leaderboard de un juego. Atributos: `Rank` (posición determinista), `Player` (identidad/nombre del jugador), `Points` (puntaje consistente con ledger), `CorrectAnswers` (total de respuestas correctas), `CurrentLevel` (nivel de dificultad actual/último alcanzado del jugador según progresión SPEC-005), `Status` (estado de participación).
- **Leaderboard (consulta de clasificación por juego)**: Colección ordenada de `LeaderboardEntry` para un juego. Propiedades: orden determinista (Points desc → CorrectAnswers desc → tiempo de consecución), snapshot consistente tras cada evaluación, estable tras `FINISHED`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: En un juego con el máximo de jugadores configurado (hasta 10 por defecto), el 100% de los envíos de respuesta simultáneos dentro de la ventana de tiempo se evalúan correctamente, sin actualizaciones perdidas ni duplicadas en ningún puntaje.
- **SC-002**: El 100% de los envíos duplicados (reintentos del mismo jugador para la misma ronda) se resuelven idempotentemente: mismo resultado, cero duplicaciones de respuesta o de puntos.
- **SC-003**: El 0% de los intentos de un jugador por modificar o suplantar el estado de otro jugador tiene éxito; todos se rechazan y quedan auditados.
- **SC-004**: El leaderboard consultado en cualquier momento posterior a una evaluación coincide en el 100% con el ledger de puntos y mantiene un orden determinista y estable entre consultas repetidas.
- **SC-005**: Bajo carga concurrente (todos los jugadores enviando dentro de una ventana de 1 segundo), el tiempo percibido por jugador para recibir su resultado no supera el doble del tiempo observado con un solo jugador, y el 100% de los saldos finales es reconstruible desde el ledger.
- **SC-006**: Toda mutación de estado de jugador que pierde un conflicto de concurrencia se reporta como error recuperable en el 100% de los casos, permitiendo al jugador reconsultar su estado autoritativo y continuar sin corrupción.
- **SC-007**: Los jugadores pueden consultar su propio estado (`Status`, `Score`, `CurrentRound`, `AnswerState`) y el leaderboard en menos de 1 segundo en el 95% de las consultas durante un juego activo.
- **SC-008**: Tras la finalización de un juego, el leaderboard final permanece inmutable en el 100% de las consultas posteriores, con el estado final (incluyendo ganador/es) de cada participante.

## Assumptions

- SPEC-004 provee el ciclo de vida del juego y la mecánica de unión (`JoinGame` en `WAITING_FOR_PLAYERS`, límites `MinPlayers`/`MaxPlayers`, sin `late join` tras el inicio); este SPEC extiende la participación con el estado individual completo del jugador durante el juego.
- SPEC-005 provee la progresión de dificultad por ronda; `CurrentLevel` del jugador corresponde al nivel de dificultad de su ronda actual (o última alcanzada si fue retirado/eliminado). Dado que la progresión es por ronda y compartida, todos los jugadores activos comparten el mismo nivel por ronda.
- SPEC-006 provee la evaluación server-side de respuestas y los estados de respuesta (sin respuesta → respondida → evaluada / expirada); este SPEC los reutiliza como `AnswerState` por jugador y ronda.
- SPEC-007 provee el ledger de puntos (`PointTransaction`) y las operaciones atómicas de puntaje; `Score` y `Points` del leaderboard se derivan exclusivamente del ledger.
- SPEC-008 define el retiro voluntario y los estados de participación (activo, retirado, eliminado, ganador); este SPEC adopta esos estados como el `Status` del jugador.
- La identidad del jugador proviene del proveedor de identidad externo (reclamo de sujeto del token); "jugador" es la participación en un juego de un usuario autenticado, no una credencial local.
- Regla de desempate del leaderboard por defecto: mayor puntaje → mayor número de respuestas correctas → consecución más temprana del puntaje. Es determinista y suficiente para v1; una regla alternativa configurable puede añadirse después sin cambiar el contrato.
- `MaxPlayers` por defecto 10 y `MinPlayers` por defecto 2 (coherente con SPEC-001/SPEC-004).
- Las notificaciones multiplayer (puntaje, leaderboard, estado de jugador) son impulsadas por el servidor y optimistas en entrega; la consulta autoritativa del estado siempre está disponible como respaldo.
- El leaderboard es visible para todos los participantes del juego y roles organizadores; el detalle privado de respuestas de otros jugadores no se expone.
- La resolución de un juego que queda sin jugadores activos (todos retirados/eliminados) se delega en SPEC-004 (finalización forzada); este SPEC solo detecta y reporta la condición.

## Dependencies

- **SPEC-004 — Game Lifecycle**: Ciclo de vida del juego, `JoinGame`, límites de jugadores, estados del juego que gobiernan cuándo se permite participar y responder, finalización forzada cuando no quedan jugadores activos.
- **SPEC-005 — Round Engine**: Rondas, progresión de dificultad (`CurrentLevel`), ventana de tiempo por ronda que delimita la respuesta simultánea de los jugadores.
- **SPEC-006 — Answer Evaluation**: Cadena de validación y evaluación server-side de respuestas, estados de respuesta (`AnswerState`), idempotencia por jugador + ronda, errores `PlayerNotInGame`/`QuestionNotActive`.
- **SPEC-007 — Scoring System**: Ledger `PointTransaction`, operaciones atómicas (`AwardPoints`, `RemovePoints`, `SecurePoints`), reconstrucción de saldo, base del `Score` y de los `Points` del leaderboard.
- **SPEC-008 — Player Withdrawal** (relacionada): Estados de participación del jugador (activo, retirado, eliminado, ganador) y políticas de retiro/pérdida que alimentan el `Status` del jugador en este SPEC.
- OroIdentityServer — identidad autenticada del jugador (reclamo `sub`) para el aislamiento entre jugadores (FR-003).

## Out of Scope

- Matchmaking, emparejamiento automático o formación de salas entre juegos (la unión a un juego existente la define SPEC-004).
- Leaderboards globales, históricos entre juegos o rankings de temporada (el leaderboard de este SPEC es por juego).
- Equipos, alianzas, espectadores o chat dentro del juego.
- Mecánica concreta de retiro, eliminación y cálculo de puntos (definidas en SPEC-008 y SPEC-007); este SPEC solo consume sus resultados de estado y puntaje.
- Selección de preguntas y progresión de dificultad (SPEC-003/SPEC-005).
- Entrega de recompensas y consolación al finalizar (SPEC-009/SPEC-010).
- Interfaz de usuario concreta (web/móvil); solo el contrato de datos y comportamiento.

## References

- Constitución v1.1.0 — Principio I (Domain First: `GamePlayer` como concepto de dominio), Principio V (Server Truth: aislamiento explícito de estado multiplayer — un jugador MUST NOT mutar respuesta/puntaje/nivel/retiro/recompensa de otro; notificaciones `ScoreUpdated`/`LeaderboardUpdated` sin ser fuente de verdad), Constraint D (Scoring via Ledger), Constraint F (Concurrencia e Idempotencia), Constraint H (identidad delegada).
- SPEC-004 — Game Lifecycle.
- SPEC-005 — Round Engine.
- SPEC-006 — Answer Evaluation.
- SPEC-007 — Scoring System.
- SPEC-008 — Player Withdrawal.
