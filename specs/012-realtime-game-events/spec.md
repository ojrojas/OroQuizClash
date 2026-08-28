# Feature Specification: Realtime

**Feature Branch**: `012-realtime-game-events`

**Created**: 2026-08-27

**Status**: Draft

**Input**: User description: "012 — Realtime Objetivo Definir la comunicación en tiempo real entre servidor y jugadores. La implementación recomendada será SignalR. Eventos GameStarted PlayerJoined RoundStarted QuestionPresented PlayerAnswered ScoreUpdated LeaderboardUpdated RoundCompleted GameFinished Flujo Server ├── RoundStarted ─► Players ├── QuestionPresented ─► Players ├── ScoreUpdated ─► Players └── RoundCompleted ─► Players Regla importante SignalR no es fuente de verdad. Database = Source of Truth, SignalR = Distribution mechanism Dependencias SPEC-004 SPEC-005 SPEC-007 SPEC-011"

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Flujo de ronda en vivo sin recargar (Priority: P1)

Como jugador en una partida en curso, quiero que mi pantalla reciba automáticamente las transiciones de la ronda — inicio de ronda (`RoundStarted`), pregunta presentada (`QuestionPresented`) y fin de ronda (`RoundCompleted`) — en el momento en que ocurren en el servidor, de forma que pueda jugar sin recargar la página ni sondear manualmente y la partida se sienta viva y sincronizada con los demás jugadores.

**Why this priority**: Es el valor central del tiempo real: sin la distribución en vivo del flujo de ronda, cada jugador tendría que adivinar o recargar para saber en qué punto está la partida, rompiendo la experiencia de juego simultáneo que habilita SPEC-011. Entrega valor independiente aunque no existieran eventos de puntuación ni de ciclo de vida del juego.

**Independent Test**: Con un juego `IN_PROGRESS` y 2+ jugadores conectados (SPEC-004/SPEC-011), iniciar una ronda desde el organizador y verificar que todos los jugadores conectados reciben `RoundStarted`, luego `QuestionPresented` con la pregunta de la ronda, y al completarse la ronda reciben `RoundCompleted`, sin que ningún jugador ejecute una petición manual. El estado del juego consultado por canal tradicional (fuente de verdad) coincide con lo anunciado.

**Acceptance Scenarios**:

1. **Given** un juego en curso con jugadores conectados, **When** el servidor inicia una ronda, **Then** todos los jugadores activos del juego reciben `RoundStarted` con el identificador y número de ronda, sin recargar.
2. **Given** una ronda iniciada, **When** el servidor presenta la pregunta de la ronda, **Then** todos los jugadores activos reciben `QuestionPresented` con el enunciado y las opciones de respuesta, sin información que revele la opción correcta.
3. **Given** una ronda en curso, **When** el servidor completa la ronda, **Then** todos los jugadores activos reciben `RoundCompleted` con el resultado de la ronda.
4. **Given** un jugador conectado, **When** recibe cualquiera de estos eventos, **Then** no necesita realizar ninguna petición adicional para conocer la transición anunciada.

---

### User Story 2 — Puntuación y leaderboard en vivo (Priority: P2)

Como jugador, quiero ver en vivo cuándo responden mis rivales y cómo cambian los puntos y el leaderboard (`PlayerAnswered`, `ScoreUpdated`, `LeaderboardUpdated`) a medida que el servidor evalúa las respuestas, de forma que la competencia se sienta inmediata y pueda adaptar mi estrategia sin consultar manualmente la tabla de posiciones.

**Why this priority**: La tensión competitiva es el segundo pilar de la experiencia en tiempo real; depende del flujo de ronda (P1) para tener contexto, pero entrega valor propio: un jugador que solo viera el leaderboard actualizarse en vivo ya percibiría una partida "en directo".

**Independent Test**: Con un juego en curso y 2+ jugadores conectados, registrar respuestas de distintos jugadores y verificar que los demás conectados reciben `PlayerAnswered` (presencia, sin revelar opción elegida ni corrección), `ScoreUpdated` cuando el servidor evalúa y actualiza puntos (SPEC-007), y `LeaderboardUpdated` con la tabla recalculada, todo sin peticiones manuales de los clientes.

**Acceptance Scenarios**:

1. **Given** un juego en curso con jugadores conectados, **When** un jugador envía su respuesta, **Then** los demás jugadores del juego reciben `PlayerAnswered` indicando quién respondió, sin revelar la opción elegida ni si fue correcta.
2. **Given** una respuesta evaluada por el servidor, **When** el puntaje del jugador cambia, **Then** los jugadores del juego reciben `ScoreUpdated` con los puntos actualizados del jugador afectado.
3. **Given** un cambio de puntaje, **When** el leaderboard se recalcula, **Then** los jugadores reciben `LeaderboardUpdated` con las posiciones, puntos y estado actuales (mismo contenido que la consulta tradicional del leaderboard).
4. **Given** un jugador que consulta el leaderboard por vía tradicional al mismo tiempo, **When** compara el resultado con el último `LeaderboardUpdated`, **Then** ambos coinciden en contenido (la fuente de verdad manda).

---

### User Story 3 — Ciclo de vida del juego en vivo (Priority: P2)

Como jugador en el lobby o como organizador, quiero recibir en vivo los cambios de estado del juego — juego iniciado (`GameStarted`), nuevo jugador unido (`PlayerJoined`) y juego finalizado (`GameFinished`) — de forma que el lobby, el inicio y el cierre de la partida se reflejen al instante para todos los interesados sin refrescar.

**Why this priority**: Completa el catálogo de eventos y hace vivible el antes y el después de la partida (lobby y cierre). Es P2 porque el valor máximo está en la ronda en vivo (P1) y la competencia en vivo (P2 anterior), pero es independiente: un lobby que muestra llegadas de jugadores y un cierre con resultados anunciados en vivo ya entregan valor por sí solos.

**Independent Test**: Con un juego en `WAITING_FOR_PLAYERS` (SPEC-004) y participantes conectados, unir jugadores adicionales, iniciar el juego y finalizarlo, verificando que los interesados conectados reciben `PlayerJoined` por cada incorporación, `GameStarted` al iniciar y `GameFinished` al terminar, con el resultado final del juego.

**Acceptance Scenarios**:

1. **Given** un juego esperando jugadores con interesados conectados, **When** un nuevo jugador se une, **Then** los interesados del juego reciben `PlayerJoined` con la identidad del nuevo participante.
2. **Given** un juego con jugadores suficientes, **When** el organizador inicia el juego, **Then** todos los participantes conectados reciben `GameStarted`.
3. **Given** un juego en curso, **When** el juego termina (finalización normal, forzada o por retiro total), **Then** todos los participantes conectados reciben `GameFinished` con el estado final y el resultado.

---

### User Story 4 — Recuperación tras desconexión y consistencia con la fuente de verdad (Priority: P3)

Como jugador que sufre una desconexión o se incorpora tarde, quiero poder recuperar el estado completo y actual del juego consultando la fuente de verdad, de forma que perderme eventos en tiempo real nunca me deje en un estado inconsistente ni dependa de "ponerme al día" evento por evento.

**Why this priority**: Es el mecanismo de resiliencia que hace confiables a las historias anteriores: el tiempo real es un mecanismo de distribución, no la fuente de verdad; su valor depende de que cualquier cliente pueda reconstruir el estado completo sin él. Es P3 porque se ejercita en casos degradados, pero es independiente y verificable sin las demás historias.

**Independent Test**: Con un juego en curso, desconectar a un jugador, avanzar rondas y puntuaciones mientras está fuera, y reconectarlo: al consultar el estado del juego, la ronda actual, la pregunta vigente, su estado de jugador y el leaderboard por los canales tradicionales, el jugador recupera el estado completo y correcto sin haber recibido los eventos intermedios. Verificar además que ningún evento emitido mientras estuvo desconectado queda "pendiente" de forma que corrompa su estado al reconectar.

**Acceptance Scenarios**:

1. **Given** un jugador desconectado mientras el juego avanza, **When** se reconecta y consulta el estado del juego por los canales tradicionales, **Then** obtiene el estado completo y actual (juego, ronda, pregunta, su estado, leaderboard) sin depender de eventos perdidos.
2. **Given** que el canal en tiempo real no está disponible, **When** un jugador juega usando únicamente los canales tradicionales, **Then** puede completar la partida correctamente (el tiempo real degrada, el juego no).
3. **Given** un evento en tiempo real recibido, **When** el cliente lo compara con el estado de la fuente de verdad, **Then** el evento nunca contradice a la fuente de verdad; si hay discrepancia, el estado válido es el de la fuente de verdad.

---

### Edge Cases

- ¿Qué ocurre cuando un jugador se desconecta en medio de una ronda? Al reconectar debe poder recuperar el estado completo desde la fuente de verdad; los eventos perdidos no se reenvían ni se acumulan.
- ¿Qué ocurre cuando la distribución en tiempo real falla al emitir un evento? La operación de juego que lo originó NO falla; el fallo se registra y el juego continúa (la distribución es de mejor esfuerzo y nunca bloquea ni revierte la lógica de juego).
- ¿Qué ocurre cuando un jugador retirado o eliminado permanece conectado? Deja de recibir contenido de rondas y preguntas (no puede seguir jugando ni obtener ventaja informativa); los eventos públicos de cierre y resultado final del juego pueden seguir siendo visibles según su estado.
- ¿Qué ocurre cuando un jugador abre varias conexiones simultáneas (p. ej. dos pestañas)? Todas sus conexiones autenticadas reciben los eventos del juego; ninguna conexión adicional otorga capacidades extra.
- ¿Qué ocurre cuando eventos y consultas tradicionales llegan en distinto orden (carreras)? El cliente debe tratar el contenido de la fuente de verdad como authoritative; los eventos son avisos, no estado.
- ¿Qué ocurre cuando un evento se entrega duplicado o desordenado? Los clientes deben tolerar duplicados y desorden sin corromper su estado (los eventos son informativos e idempotentes desde el punto de vista del cliente).
- ¿Qué ocurre con juegos simultáneos? Los eventos de un juego nunca se entregan a participantes de otro juego (aislamiento estricto por juego).
- ¿Qué ocurre cuando el organizador está conectado? Recibe los eventos del juego con la misma inmediatez que los jugadores, para poder operar la partida en vivo.

## Requirements *(mandatory)*

### Functional Requirements

**Catálogo de eventos**

- **FR-001**: El sistema MUST distribuir en tiempo real a los interesados del juego exactamente este catálogo de eventos: `GameStarted`, `PlayerJoined`, `RoundStarted`, `QuestionPresented`, `PlayerAnswered`, `ScoreUpdated`, `LeaderboardUpdated`, `RoundCompleted`, `GameFinished`.
- **FR-002**: Cada evento MUST emitirse como consecuencia de una operación de juego ya persistida en la fuente de verdad; ningún evento MAY originar o mutar estado de juego por sí mismo (los eventos son solo notificación).
- **FR-003**: `RoundStarted` MUST anunciar el inicio de cada ronda a todos los jugadores activos del juego, incluyendo la identificación de la ronda.
- **FR-004**: `QuestionPresented` MUST presentar la pregunta vigente de la ronda (enunciado y opciones de respuesta) a todos los jugadores activos, y MUST NOT contener ningún dato que revele la opción correcta.
- **FR-005**: `PlayerAnswered` MUST notificar que un jugador envió su respuesta, y MUST NOT revelar la opción elegida ni la corrección de la respuesta antes de la divulgación oficial del resultado de la ronda.
- **FR-006**: `ScoreUpdated` MUST notificar la actualización de puntos de un jugador tras la evaluación del servidor, con el puntaje consistente con el ledger de puntos (SPEC-007).
- **FR-007**: `LeaderboardUpdated` MUST distribuir la tabla de posiciones recalculada con el mismo contenido que la consulta tradicional de leaderboard (SPEC-011): posición, jugador, puntos, respuestas correctas, nivel actual, estado y puntos asegurados.
- **FR-008**: `RoundCompleted` MUST notificar el cierre de la ronda a todos los jugadores activos con el resultado de la ronda.
- **FR-009**: `GameStarted`, `PlayerJoined` y `GameFinished` MUST notificar el inicio del juego, la incorporación de jugadores y el final del juego (con resultado final) respectivamente, a los interesados del juego.

**Audiencia, seguridad y aislamiento**

- **FR-010**: Los eventos de un juego MUST entregarse únicamente a participantes autenticados de ese juego (jugadores con participación vigente y organizadores autorizados); ningún evento MAY entregarse a usuarios ajenos al juego.
- **FR-011**: La suscripción a eventos de un juego MUST requerir autenticación válida (JWT emitido por OroIdentityServer) y pertenencia al juego; un jugador MUST NOT poder suscribirse a eventos de un juego en el que no participa.
- **FR-012**: Un jugador retirado o eliminado MUST dejar de recibir eventos con contenido de ronda/pregunta a partir de su cambio de estado.
- **FR-013**: Los payloads de eventos MUST NOT contener información sensible (respuestas correctas antes de tiempo, respuestas elegidas por otros jugadores, credenciales o datos personales no necesarios).

**Fuente de verdad y resiliencia**

- **FR-014**: La base de datos MUST permanecer como única fuente de verdad; el canal en tiempo real MUST ser únicamente un mecanismo de distribución. Todo estado anunciado por un evento MUST ser verificable mediante las consultas tradicionales correspondientes.
- **FR-015**: El estado completo del juego (juego, ronda actual, pregunta vigente, estado de cada jugador, leaderboard) MUST ser recuperable en cualquier momento vía consultas tradicionales, sin dependencia de eventos previos.
- **FR-016**: Un fallo en la distribución de un evento MUST NOT fallar, revertir ni retrasar la operación de juego que lo originó; el fallo MUST registrarse para observabilidad y el juego continúa.
- **FR-017**: Los eventos MUST emitirse después de que la operación de juego esté persistida (nunca antes del commit); un evento nunca anuncia un estado que no exista en la fuente de verdad.
- **FR-018**: Los clientes MUST poder completar una partida usando únicamente los canales tradicionales si el canal en tiempo real no está disponible (degradación transparente del tiempo real, nunca del juego).

**Entrega y alcance**

- **FR-019**: La entrega de eventos es de mejor esfuerzo: el sistema MUST tolerar desconexiones de clientes sin degradar el juego, y MUST NOT garantizar reenvío de eventos perdidos (la recuperación se hace por fuente de verdad, ver FR-015).
- **FR-020**: Todas las conexiones autenticadas de un mismo jugador MUST recibir los eventos de sus juegos (multi-conexión por jugador).
- **FR-021**: Los eventos MUST ser autocontenidos para su propósito informativo (qué ocurrió y datos públicos esenciales), sin requerir estado previo del cliente para interpretarse.

### Key Entities

- **Evento de juego en tiempo real**: Notificación efímera de un hecho de juego ya persistido. Atributos: tipo de evento (uno del catálogo de 9), juego al que pertenece, ronda asociada cuando aplique, y payload público mínimo suficiente para informar el hecho. No es estado: es un aviso sobre estado que vive en la fuente de verdad.
- **Audiencia del juego**: Conjunto de receptores autorizados de los eventos de un juego: jugadores con participación vigente y organizadores autorizados. Cambia con el ciclo de vida (incorporaciones, retiros, eliminaciones, fin del juego).
- **Conexión de cliente**: Sesión de comunicación autenticada de un participante con el servidor. Un jugador puede tener varias conexiones simultáneas; cada conexión pertenece a la audiencia de los juegos donde el jugador participa.
- **Canal tradicional**: Vía de consulta/operación existente (API de juego) que lee y escribe la fuente de verdad. Permanece como mecanismo completo y authoritative; el canal en tiempo real lo complementa, no lo reemplaza.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Durante una partida en vivo, el 100% de las transiciones del catálogo (inicio de juego, incorporación de jugadores, inicio de ronda, pregunta presentada, respuesta registrada, actualización de puntos, actualización de leaderboard, fin de ronda, fin de juego) son visibles para todos los jugadores conectados sin que ninguno recargue o consulte manualmente.
- **SC-002**: Los jugadores perciben cada evento en menos de 2 segundos desde que la operación de juego correspondiente se completa en el servidor, en condiciones normales de red.
- **SC-003**: Un jugador desconectado que se reconecta recupera el estado completo y actual del juego (juego, ronda, pregunta, su estado, leaderboard) en una única ronda de consultas tradicionales, sin depender de eventos perdidos.
- **SC-004**: El 100% de las operaciones de juego tiene éxito aunque el mecanismo de distribución en tiempo real esté completamente indisponible (el juego nunca se degrada por fallos de notificación).
- **SC-005**: Cero eventos entregados a usuarios que no participan del juego emisor (aislamiento por juego verificable en juegos simultáneos).
- **SC-006**: Cero payloads de eventos que revelen la opción correcta o la respuesta elegida por otro jugador antes de la divulgación oficial del resultado (verificable por inspección de contrato).
- **SC-007**: Con 20 juegos simultáneos de hasta 4 jugadores cada uno, todos los jugadores conectados siguen recibiendo los eventos de su juego sin fugas entre juegos y sin degradación perceptible (mismo umbral de 2 segundos de SC-002).
- **SC-008**: El contenido de todo evento de estado (puntos, leaderboard, resultado de ronda) coincide 100% con el resultado de la consulta tradicional equivalente en el mismo momento (la fuente de verdad manda).

## Assumptions

- Se construye sobre la infraestructura de notificaciones ya existente de SPEC-011 (puerto de difusión de eventos de juego, concentrador de conexiones con agrupación por juego y autenticación); este SPEC extiende y formaliza el catálogo completo de eventos, no lo reemplaza.
- Despliegue de un único nodo para el canal en tiempo real; la distribución entre múltiples nodos (backplane) queda fuera de alcance hasta que la escala lo requiera.
- Los clientes son las aplicaciones web de presentación del proyecto; el tiempo real es server-push (el servidor emite, los clientes no envían comandos por el canal en tiempo real — las operaciones siguen entrando por los canales tradicionales).
- La entrega es de mejor esfuerzo y los clientes son responsables de resincronizarse desde la fuente de verdad tras desconexiones; no se implementa historial ni reenvío de eventos perdidos.
- Los payloads reutilizan las mismas formas de datos que las respuestas de las consultas tradicionales equivalentes (leaderboard, estado del jugador, ronda/pregunta), evitando contratos duplicados que puedan divergir.
- El organizador de un juego puede observar todos los eventos de ese juego; el soporte de espectadores ajenos al juego (público general) queda fuera de alcance.
- Eventos de otros dominios (recompensas, canjes, consuelo) quedan fuera de este catálogo; solo se cubren los 9 eventos de juego listados.
- La autenticación y autorización continúan delegadas en OroIdentityServer (Constitución VI); el canal en tiempo real exige el mismo JWT bearer que los canales tradicionales.
- Dependencias funcionales: SPEC-004 (ciclo de vida del juego), SPEC-005 (motor de rondas), SPEC-007 (sistema de puntuación), SPEC-011 (multiplayer: participantes, aislamiento, leaderboard y base de notificaciones).
