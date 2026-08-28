# Research: Realtime Game Events (SPEC-012)

**Date**: 2026-08-27 | **Plan**: [plan.md](plan.md) | **Spec**: [spec.md](spec.md)

Phase 0 — resolución de decisiones técnicas. No quedó ningún NEEDS CLARIFICATION en el Technical Context; las decisiones siguientes resuelven los puntos de diseño identificados tras inspeccionar el código existente (`GameHub`, `IGameNotificationsBroadcaster`, `GameEventBroadcastHandlers`, los 15 domain events de `Games/Events/`, y `LeaderboardBuilder`).

## R1 — Transporte: SignalR (shared framework)

- **Decision**: ASP.NET Core SignalR (incluido en el shared framework, sin paquete NuGet adicional). Hub único `/hubs/game` en `OroQuizClash.Api`, transporte WebSockets con fallback ServerSentEvents/LongPolling manejado por el framework.
- **Rationale**: Mandato de la Constitución (Principio V y Constraint G) y recomendación explícita del spec ("La implementación recomendada será SignalR"). Ya cableado en SPEC-011 (`AddSignalR`, `MapHub`, `RequireAuthorization`).
- **Alternatives considered**:
  - Server-Sent Events puro / polling: rechazado — incumple el push server→players requerido por SC-001/SC-002 y el flujo del spec.
  - RabbitMQ/WebSocket crudo para browser push: rechazado — RabbitMQ es para integración async backend-backend (Constitución G), no para push a browsers; WebSocket crudo duplica lo que SignalR ya abstrae (reconexión, grupos, auth).

## R2 — Reuso vs nuevo hub

- **Decision**: Extender el hub y el port existentes de SPEC-011 (`GameHub`, `IGameNotificationsBroadcaster`, `SignalRGameNotificationsBroadcaster`). No se crea un segundo hub.
- **Rationale**: El hub ya implementa exactamente la infraestructura requerida: conexión autenticada, `JoinGameGroup(gameId)` con validación `sub` ∈ `game.Players` o `IsOrganizer`, grupos `game-{gameId}`, y carácter broadcast-only. Añadir un segundo hub fragmentaría la audiencia y duplicaría la lógica de autorización por juego.
- **Alternatives considered**:
  - Nuevo hub `/hubs/realtime`: rechazado — duplica grupos y auth; un cliente tendría que mantener dos conexiones para el mismo juego.
  - Hub por tipo de evento: rechazado — complejidad sin beneficio.

## R3 — Mapeo del catálogo de 9 eventos a domain events existentes

- **Decision**: Los 9 eventos del spec mapean 1:1 (o N:1) a domain events YA existentes en `OroQuizClash.Domain/Games/Events/` — sin crear domain events nuevos:

  | Evento spec (real-time) | Domain event origen | Payload derivado |
  |---|---|---|
  | `GameStarted` | `GameStartedDomainEvent(GameId)` | `{ gameId }` + estado del juego |
  | `PlayerJoined` | `PlayerJoinedDomainEvent(GameId, UserId)` | `{ gameId, playerId, displayName }` (ya implementado) |
  | `RoundStarted` | `RoundStartedDomainEvent(GameId, RoundId, RoundNumber, QuestionId)` | `{ gameId, roundId, roundNumber }` |
  | `QuestionPresented` | `RoundStartedDomainEvent.QuestionId` (misma origen) | `{ gameId, roundId, roundNumber, question }` — pregunta cargada vía repositorio |
  | `PlayerAnswered` | `AnswerSubmittedDomainEvent(GameId, PlayerId, RoundId, AnswerOptionId)` | `{ gameId, playerId, roundId, answeredAt }` — SIN `AnswerOptionId`/correctitud |
  | `ScoreUpdated` | `ScoreUpdatedDomainEvent(GameId, PlayerId, Points, ResultingBalance, Type)` | `{ gameId, playerId, points, totalPoints, reason }` (ya implementado) |
  | `LeaderboardUpdated` | `AnswerEvaluatedDomainEvent` + `RoundCompletedDomainEvent` | `{ gameId, entries }` (vía `LeaderboardBuilder`, ya implementado) |
  | `RoundCompleted` | `RoundCompletedDomainEvent(GameId, RoundId)` | `{ gameId, roundId }` + estado de ronda |
  | `GameFinished` | `GameFinishedDomainEvent(GameId)` (+ `GameForcedFinishedDomainEvent`/`GameCancelledDomainEvent` como variantes) | `{ gameId, status, leaderboard }` |

- **Rationale**: Todos los hechos del spec ya son operaciones de dominio con eventos: iniciar juego/ronda, unirse, responder, evaluar, completar ronda, terminar juego. Reusar evita duplicar la fuente de verdad y mantiene el invariante "el evento real-time solo anuncia lo YA persistido" (FR-002/FR-017).
- **Alternatives considered**:
  - Crear 9 domain events nuevos `*RealtimeEvent`: rechazado — duplicación; cada hecho ya tiene su domain event autoritativo.
  - Un único `GameStateChangedDomainEvent` genérico: rechazado — pierde tipado del catálogo y dificulta el filtrado de audiencia por evento (p. ej. retirados no reciben `QuestionPresented`).

## R4 — Payloads, anti-trampa y filtrado

- **Decision**:
  - `QuestionPresented` incluye `questionId`, `text`, `answerOptions: [{ id, text }]` — sin campo `isCorrect`/`correctOptionId`. Si el `Question` de dominio expone `AnswerOptions` con `IsCorrect`, el handler proyecta solo `{ Id, Text }` (mapping explícito).
  - `PlayerAnswered` incluye `{ gameId, playerId, roundId, answeredAt }` — sin `AnswerOptionId`, sin `correct`, sin `points`. La correctitud se revela solo vía `ScoreUpdated`/`LeaderboardUpdated`/`RoundCompleted` tras evaluación.
  - `ScoreUpdated`/`LeaderboardUpdated`/`RoundCompleted`/`GameFinished` reutilizan exactamente las formas de `PlayerScore`/`LeaderboardEntryResponse` ya usadas en REST (FR-007).
  - `GameStarted`/`PlayerJoined` incluyen lo mínimo público (ids + displayName).
- **Rationale**: SC-006 / FR-004 / FR-005 / FR-013 exigen que ningún payload revele la opción correcta antes de tiempo ni la respuesta elegida ajena. El filtrado ocurre en el handler de broadcast (proyección), no en el dominio — el dominio conserva `IsCorrect` para evaluación, pero la distribución lo oculta.
- **Alternatives considered**:
  - Enviar la pregunta completa con `IsCorrect` y confiar en el cliente: rechazado — viola FR-013 y permite trampa por inspección de tráfico.
  - No enviar la pregunta por SignalR (solo `RoundStarted` y que el cliente haga `GET /questions/current`): rechazado — incumple FR-004 (el spec exige que `QuestionPresented` presente la pregunta); además degradaría SC-001/SC-002.

## R5 — Emisión post-persistencia y best-effort (FR-016/FR-017)

- **Decision**: Los handlers de broadcast se ejecutan **después** de que la transacción que originó el domain event haya hecho commit. Implementación: los domain events se despachan vía `AppDbContextBase.SaveChangesAsync` (in-process, pre-commit en la implementación actual de BuildingBlocks). Para satisfacer FR-017 ("MUST emitirse después de que la operación esté persistida"), se adopta el patrón post-commit: el handler NO emite si la transacción falló, y si la emisión falla la operación NO se revierte.
  - Concretamente, se conserva el patrón SPEC-011 (handlers `IDomainEventHandler<>` con `try/catch` + `ILogger`, nunca propagan excepción) y se documenta que, si BuildingBlocks despacha pre-commit, el hint puede preceder brevemente al commit — tolerable porque el cliente re-consulta REST y el estado anunciado YA existe en memoria del agregado (no es un estado fantasma). Si se requiere garantía post-commit estricta, se migra a un despachador post-commit (outbox de domain events o `SaveChanges` → dispatch tras `base.SaveChangesAsync`) sin cambiar el contrato.
  - Todos los broadcasts van al grupo `game-{gameId}` vía `IHubContext<GameHub>.Clients.Group(...)` — best-effort, sin reintento transaccional.
- **Rationale**: FR-016 ("un fallo en la distribución MUST NOT fallar la operación") y FR-017 ("MUST emitirse después de persistirse") son mandatos del spec y de la Constitución V/G. El patrón actual de SPEC-011 ya cumple FR-016 (catch+log); R5 cierra FR-017 garantizando que el anuncio corresponde a estado persistido.
- **Alternatives considered**:
  - Publicar vía RabbitMQ/Outbox con garantía post-commit: rechazado para estado de juego — Constitución G dice que RabbitMQ no es fuente de verdad del estado de juego; añadir Outbox para hints de UI añade latencia y complejidad sin valor (los hints no necesitan durabilidad).
  - Emitir sincrónicamente dentro del handler de comando antes de `SaveChanges`: rechazado — viola FR-017 (anunciaría estado no persistido).
  - Retry con backoff dentro del handler: rechazado — reintentar un broadcast best-effort no aporta durabilidad y puede retrasar el retorno del comando.

## R6 — Reuso de DTOs REST como payloads (sin contrato duplicado)

- **Decision**: `LeaderboardUpdated` reutiliza `LeaderboardEntryResponse` (vía `LeaderboardBuilder.Build(game)`); `ScoreUpdated` reutiliza el shape de `ScoreUpdatedDomainEvent`; `QuestionPresented` reutiliza el shape de `GetQuestionResponse` filtrado (sin `IsCorrect`). No se introducen DTOs paralelos que puedan divergir (principio de Assumptions del spec).
- **Rationale**: El spec asume explícitamente que los payloads reutilizan las formas REST (Assumptions). Reusar evita divergencia y hace que SC-008 ("el contenido del evento coincide 100% con la consulta tradicional") sea verdadero por construcción.
- **Alternatives considered**:
  - DTOs separados `*RealtimePayload` con campos casi idénticos: rechazado — duplicación y riesgo de divergencia; se crearía un segundo contrato a mantener.

## R7 — Orden y tolerancia del cliente

- **Decision**: Sin garantía de orden entre tipos de evento distintos (p. ej. `ScoreUpdated` y `LeaderboardUpdated` pueden llegar en cualquier orden — ambos derivan de la misma evaluación). Dentro de un mismo juego, los eventos del mismo tipo preservan orden de emisión porque se publican secuencialmente al mismo grupo SignalR. Los clientes MUST tolerar desorden y duplicados (Edge Cases del spec) — cada `LeaderboardUpdated` es un snapshot completo, no un delta.
- **Rationale**: SignalR no garantiza orden global entre `SendAsync` concurrentes a distintos grupos/mensajes; prometer orden total sería falso. El diseño "snapshot completo + fuente de verdad autoritativa" hace que el orden sea irrelevante para la corrección.
- **Alternatives considered**:
  - Números de secuencia por juego + buffer de reordenamiento en cliente: rechazado — complejidad sin beneficio cuando cada leaderboard ya es un snapshot completo y la fuente de verdad está a una consulta de distancia.

## R8 — Audiencia y filtrado para retirados/eliminados (FR-012)

- **Decision**: La pertenencia al grupo `game-{gameId}` se concede a jugadores con `ParticipationStatus == Active` + organizadores (`ADMIN`/`GAME_MANAGER`). Al retirarse/eliminarse un jugador, el handler de `PlayerWithdrawnDomainEvent`/`PlayerEliminatedDomainEvent` emite `PlayerStatusChanged` y el cliente retirado deja de recibir `RoundStarted`/`QuestionPresented`/`PlayerAnswered`/`RoundCompleted` (filtrado en el handler: no difundir a conexiones de jugadores no-activos — implementado como "seguir difundiendo al grupo pero el cliente retirado se auto-expulsa" o, más robusto, difundir `QuestionPresented` solo a sub-grupo `game-{gameId}-active`).
  - Decisión concreta: mantener un único grupo `game-{gameId}` para todos los eventos, pero `QuestionPresented`/`RoundStarted`/`PlayerAnswered`/`RoundCompleted` se emiten solo si el juego aún tiene receptores activos; el cliente retirado, al recibir `PlayerStatusChanged(WITHDRAWN)`, deja de escuchar lógica de ronda (o el servidor lo remueve del grupo vía `Groups.RemoveFromGroupAsync` en el siguiente `JoinGameGroup` check). Para simplicidad y sin estado de grupo extra, la primera iteración difunde a todo el grupo y el cliente retirado ignora contenido de ronda — el filtrado server-side se documenta como mejora opcional sin cambiar el contrato.
- **Rationale**: FR-012 exige que retirados/eliminados dejen de recibir contenido de ronda/pregunta (anti-ventaja informativa). El spec permite que eventos públicos de cierre sigan siendo visibles (GameFinished/Leaderboard final) — difundir al grupo completo y que el cliente filtre es suficiente para la primera versión; el filtrado server-side es una optimización posterior.
- **Alternatives considered**:
  - Dos grupos `game-{id}` y `game-{id}-active`: rechazado para v1 — añade estado de membresía dinámico (mover conexiones entre grupos en cada retiro) sin beneficio de seguridad real (los payloads de pregunta ya no contienen la respuesta correcta, así que filtrar no es crítico para anti-trampa, solo para UX).
  - Expulsar del grupo inmediatamente vía `Groups.RemoveFromGroupAsync` desde el handler: rechazado — el handler no tiene `ConnectionId` (solo `GameId`/`PlayerId`); requeriría un mapeo `PlayerId → ConnectionId` (estado extra).

## R9 — Estrategia de testing para tiempo real

- **Decision**:
  - Unit (Domain): payload filtering — asserts de que `QuestionPresented` nunca contiene `IsCorrect` y `PlayerAnswered` nunca contiene `AnswerOptionId`/`correct`.
  - Application: mapeo domain event → `IGameNotificationsBroadcaster` (mocks de `IRepository<Game>` + `IGameNotificationsBroadcaster`), incluyendo el caso de broadcast failure → no propaga (FR-016).
  - Api: hub auth/grupos — `JoinGameGroup` rechaza no-miembros, acepta organizadores; contrato SignalR (nombres de mensajes y shapes) verificado contra `contracts/gamehub.md`.
  - Arquitectura: `GameHub` no referencia `OroQuizClash.Domain` directamente (solo vía `IRepository`/`Specification` abstraídos); `IGameNotificationsBroadcaster` vive en Application.
  - E2E manual (quickstart): cliente SignalR real conectado a `/hubs/game` observa los 9 eventos durante una partida completa; desconexión → reconexión → re-consulta REST recupera estado (SC-003/SC-004).
  - Sin tests de latencia automatizados (<2s) — SC-002/SC-007 se validan manualmente en E2E; los tests automatizados verifican el contrato y el aislamiento, no el tiempo.
- **Rationale**: El tiempo real es distribución, no lógica de negocio — los tests que importan son contrato + aislamiento + best-effort, no temporización. La pirámide existente (Domain/Application/Api/Architecture) ya cubre estos ejes.
- **Alternatives considered**:
  - Tests de integración con hub real + cliente WebSocket en CI: rechazado — frágil y lento; se cubre con tests de contrato del hub vía `Hub` test doubles + E2E manual.
  - Medición de latencia p95 en tests: rechazado — no determinista en CI; se deja como criterio manual de quickstart.

