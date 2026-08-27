# Feature Specification: Game Lifecycle

**Feature Branch**: `004-game-lifecycle`

**Created**: 2026-08-27

**Status**: Draft

**Input**: User description: "004 — Game Lifecycle Objetivo Definir el ciclo de vida completo de una partida. Estados DRAFT READY WAITING_FOR_PLAYERS IN_PROGRESS ROUND_IN_PROGRESS ROUND_COMPLETED FINISHED CANCELLED FORCED_FINISHED Transiciones DRAFT ↓ READY ↓ WAITING_FOR_PLAYERS ↓ IN_PROGRESS ↓ ROUND_IN_PROGRESS ↓ ROUND_COMPLETED ↓ ROUND_IN_PROGRESS ... ↓ FINISHED Reglas Un juego: No puede iniciar sin configuración válida. No puede iniciar sin jugadores suficientes. No puede comenzar una ronda si la anterior no terminó. No puede recibir respuestas si no está en una ronda activa. No puede modificarse después de comenzar. Solo puede finalizar desde estados válidos. Eventos GameCreated GameReady PlayerJoined GameStarted GameFinished GameCancelled GameForcedFinished Dependencias SPEC-001 SPEC-002 SPEC-003 SPEC-011"

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Crear y preparar partida hasta sala de espera (Priority: P1)

Como organizador (ADMIN / GAME_MANAGER), quiero crear un juego en `DRAFT`, completarlo a `READY` solo si la configuración es válida (SPEC-001), y abrir la sala `WAITING_FOR_PLAYERS` para que jugadores se unan, de forma que la partida tenga precondiciones sólidas antes de iniciar.

**Why this priority**: Es el gate inicial que bloquea todos los demás flujos; sin `DRAFT→READY→WAITING_FOR_PLAYERS` no existe ciclo jugable. Sin esta validación se violarían las reglas "No puede iniciar sin configuración válida" y "No puede iniciar sin jugadores suficientes". Entrega valor independiente como preparación de partida.

**Independent Test**: Crear `Game` con `CreateGame` (config válida SPEC-001, categoría publicada SPEC-002 con ≥5 preguntas válidas SPEC-003) → `GameCreated` + estado `DRAFT`; invocar `PrepareGame`/`MarkReady` con config válida → `GameReady` + `READY`; con config inválida → `400 InvalidGameConfiguration` permanece `DRAFT`. Invocar `OpenLobby` / `WaitForPlayers` → `WAITING_FOR_PLAYERS`; unir jugadores vía `JoinGame` → `PlayerJoined` hasta alcanzar `MinPlayers`.

**Acceptance Scenarios**:

1. **Given** payload con `CategoryId` publicada, `MinRounds=5`, `MaxRounds=10`, `TimeLimitPerQuestion=30s`, `PointsPerRound=10`, `Difficulty` y políticas válidas (SPEC-001), **When** se envía `CreateGame`, **Then** el sistema crea `Game` en `DRAFT`, emite `GameCreated`, persiste `GameConfiguration` inmutable y retorna `GameId`.
2. **Given** juego en `DRAFT` con configuración completa y categoría aún publicada, **When** se invoca `MarkReady`/`PrepareGame`, **Then** transita a `READY` y emite `GameReady`; **When** la categoría deja de ser publicable (<5 válidas) antes de `MarkReady`, **Then** se rechaza con `CategoryNotReady`/`InvalidGameConfiguration` y permanece `DRAFT`.
3. **Given** intento `CreateGame` sin configuración válida (ej. `MinRounds=3`, `CategoryId` inexistente/archivada, `TimeLimit=0`), **When** se envía, **Then** se rechaza con `InvalidGameConfiguration`/`CategoryNotFound` (regla "No puede iniciar sin configuración válida") y no se persiste `Game` jugable.
4. **Given** juego en `READY`, **When** se invoca `OpenWaitingForPlayers` (o `StartLobby`), **Then** transita a `WAITING_FOR_PLAYERS` y queda abierto a `JoinGame`.
5. **Given** juego en `WAITING_FOR_PLAYERS` con `MinPlayers=2`, **When** 1 jugador hace `JoinGame` → `PlayerJoined` pero permanece `WAITING_FOR_PLAYERS`; **When** se une el 2º jugador → sigue `WAITING_FOR_PLAYERS` pero ya cumple el mínimo para poder `StartGame` (ver US2).
6. **Given** intento `MarkReady` dos veces concurrentes desde `DRAFT`, **When** ambos envían, **Then** uno gana y el segundo recibe `409 Conflict` por `rowversion` / estado ya `READY`.

---

### User Story 2 — Iniciar partida y ciclo de rondas (Priority: P1)

Como sistema y como jugadores, quiero que una partida en `WAITING_FOR_PLAYERS` con jugadores suficientes pase a `IN_PROGRESS` y luego itere `ROUND_IN_PROGRESS ↔ ROUND_COMPLETED` hasta agotar rondas y llegar a `FINISHED`, para orquestar el juego real.

**Why this priority**: Es el corazón del motor autoritativo; sin `IN_PROGRESS` + ciclo de rondas no hay `SubmitAnswer` ni `Score`. Cubre "No puede iniciar sin jugadores suficientes" y "No puede comenzar una ronda si la anterior no terminó". Entrega valor independiente como motor mínimo jugable.

**Independent Test**: Con `Game` en `WAITING_FOR_PLAYERS` + `MinPlayers` alcanzados → `StartGame` → `GameStarted` + `IN_PROGRESS`; con jugadores insuficientes → `400 NotEnoughPlayers` permanece `WAITING_FOR_PLAYERS`. Luego `StartRound` → `ROUND_IN_PROGRESS`; intentar `StartRound` de nuevo sin `CompleteRound` → `400 RoundAlreadyInProgress`. `CompleteRound` → `ROUND_COMPLETED` → `StartRound` siguiente → `ROUND_IN_PROGRESS`; tras `MaxRounds` completar → `FinishGame` → `FINISHED` + `GameFinished`.

**Acceptance Scenarios**:

1. **Given** juego en `WAITING_FOR_PLAYERS` con `players (2) ≥ MinPlayers (2)`, **When** se invoca `StartGame` (GAME_MANAGER), **Then** transita a `IN_PROGRESS`, emite `GameStarted`, bloquea configuración (regla "No puede modificarse después de comenzar").
2. **Given** juego en `WAITING_FOR_PLAYERS` con `players (1) < MinPlayers (2)`, **When** se invoca `StartGame`, **Then** se rechaza con `NotEnoughPlayers` y permanece `WAITING_FOR_PLAYERS` (regla 2).
3. **Given** juego en `IN_PROGRESS` sin ronda activa, **When** se invoca `StartRound` (roundNumber=1, selecciona pregunta SPEC-003), **Then** crea `GameRound` en `ROUND_IN_PROGRESS` y avanza el contador; **When** se invoca `StartRound` de nuevo antes de `CompleteRound`, **Then** se rechaza con `RoundAlreadyInProgress` (regla 3).
4. **Given** ronda en `ROUND_IN_PROGRESS`, **When** se invoca `CompleteRound` (todos respondieron o timeout server-side), **Then** transita a `ROUND_COMPLETED`; **When** quedan rondas (`completed < MaxRounds` y `MinRounds` no necesariamente alcanzado si es variable), **Then** `StartRound` siguiente es permitido y vuelve a `ROUND_IN_PROGRESS`.
5. **Given** juego que completó `MinRounds` y alcanzó `MaxRounds` (o condición de fin definida por configuración), **When** se invoca `FinishGame` desde `ROUND_COMPLETED` o `IN_PROGRESS` sin ronda activa, **Then** transita a `FINISHED` y emite `GameFinished`.
6. **Given** dos `StartGame` concurrentes desde `WAITING_FOR_PLAYERS`, **When** ambos envían, **Then** uno gana `IN_PROGRESS`, el segundo `409 Conflict` por `rowversion`.

---

### User Story 3 — Defensa de invariantes durante el juego (Priority: P1)

Como sistema autoritativo, quiero que el dominio rechace operaciones inválidas aunque la API las intente: no recibir respuestas fuera de ronda activa, no modificar configuración después de comenzar, y solo finalizar desde estados válidos, para garantizar equidad y auditabilidad.

**Why this priority**: Protege las reglas 4, 5 y 6 que evitan cheating, corrupción de estado y finales ilegales. Sin esta defensa el cliente podría mutar puntajes o forzar transiciones. Entrega valor independiente como guarda de integridad.

**Independent Test**: Intentar `UpdateGame` después de `StartGame` → `400 ConfigurationImmutable`; `SubmitAnswer` en `IN_PROGRESS` sin `ROUND_IN_PROGRESS` → `400 NoActiveRound`; `SubmitAnswer` en `ROUND_IN_PROGRESS` → `202 Accepted` con `PointTransaction`; `FinishGame` desde `DRAFT` → `400 InvalidGameState`.

**Acceptance Scenarios**:

1. **Given** juego en `WAITING_FOR_PLAYERS` o `IN_PROGRESS` (ya iniciado), **When** se intenta `UpdateGame`/`UpdateConfiguration` (cambiar `MinRounds`, `CategoryId`, `Difficulty`, `TimeLimit`), **Then** se rechaza con `ConfigurationImmutable`/`InvalidGameState` (regla "No puede modificarse después de comenzar") y no muta `GameConfiguration`.
2. **Given** juego en `IN_PROGRESS` pero sin ronda activa (estado intermedio `IN_PROGRESS` o `ROUND_COMPLETED`), **When** jugador envía `SubmitAnswer` (`QuestionId`, `AnswerOptionId`), **Then** se rechaza con `NoActiveRound`/`InvalidGameState` (regla "No puede recibir respuestas si no está en una ronda activa").
3. **Given** juego en `ROUND_IN_PROGRESS` con pregunta asignada (`QuestionId` PUBLISHED), **When** jugador envía `SubmitAnswer` válida dentro del `TimeLimit`, **Then** el servidor evalúa correctitud (SPEC-003, `IsCorrect` server-side), registra `PointTransaction`, emite evento y retorna resultado; **When** fuera de tiempo, **Then** se rechaza con `AnswerTimeout` (server timestamp, no cliente).
4. **Given** juego en `DRAFT` o `READY`, **When** se intenta `FinishGame`/`ForceFinish`, **Then** se rechaza con `InvalidGameState` (regla "Solo puede finalizar desde estados válidos"); **When** en `IN_PROGRESS`, `ROUND_IN_PROGRESS`, `ROUND_COMPLETED` o `WAITING_FOR_PLAYERS` (según matriz válida), **Then** se permite transición a `FINISHED`/`CANCELLED`/`FORCED_FINISHED`.
5. **Given** intento de transición ilegal `FINISHED → StartGame` o `CANCELLED → StartRound`, **When** se envía, **Then** se rechaza con `InvalidGameState` y `rowversion` protege concurrencia.

---

### User Story 4 — Finalización y cancelación controlada (Priority: P2)

Como organizador o sistema, quiero poder llevar un juego a `FINISHED` tras completar rondas, o a `CANCELLED`/`FORCED_FINISHED` por decisión administrativa o condición de sistema (abandono, timeout global, categoría invalidada), emitiendo eventos y bloqueando toda acción posterior.

**Why this priority**: Cierra el ciclo y habilita estadísticas/recompensas/consolación (SPEC-011); es P2 porque depende de US1-US3 pero es necesaria para reporte y auditoría. Entrega valor independiente como cierre auditable.

**Independent Test**: Desde `ROUND_COMPLETED` con rondas completadas → `FinishGame` → `FINISHED` + `GameFinished` + `No further SubmitAnswer/StartRound` permitido. Desde `WAITING_FOR_PLAYERS` con admin → `CancelGame` → `CANCELLED` + `GameCancelled`. Desde `IN_PROGRESS` por timeout/sistema → `ForceFinishGame` → `FORCED_FINISHED` + `GameForcedFinished`. `GET /api/games/{id}` refleja estado terminal.

**Acceptance Scenarios**:

1. **Given** juego en `ROUND_COMPLETED` habiendo alcanzado condición de fin (`completedRounds ≥ MinRounds` y `completedRounds == MaxRounds` o política de fin), **When** se invoca `FinishGame`, **Then** transita a `FINISHED`, emite `GameFinished`, persiste `FinishedAt`, y rechaza todo `SubmitAnswer`/`StartRound`/`JoinGame` posterior con `InvalidGameState`.
2. **Given** juego en `DRAFT`/`READY`/`WAITING_FOR_PLAYERS`/`IN_PROGRESS` (antes de `FINISHED`), **When** ADMIN invoca `CancelGame` con motivo, **Then** transita a `CANCELLED`, emite `GameCancelled`, y bloquea jugabilidad (según matriz, `CANCELLED` es terminal).
3. **Given** juego en `IN_PROGRESS` o `ROUND_IN_PROGRESS` donde sistema detecta condición forzada (ej. `Timeout` global, `Category` archivada, no hay preguntas válidas para siguiente ronda, o todos los jugadores abandonaron según SPEC-011), **When** se invoca `ForceFinishGame` (sistema o GAME_MANAGER), **Then** transita a `FORCED_FINISHED`, emite `GameForcedFinished`, y registra auditoría con causa.
4. **Given** juego ya en `FINISHED`, `CANCELLED` o `FORCED_FINISHED`, **When** se intenta cualquier transición (`StartGame`, `StartRound`, `SubmitAnswer`, `JoinGame`), **Then** se rechaza con `InvalidGameState` y `rowversion` protege doble `Finish` concurrente (`409`).

---

### Edge Cases

- ¿Qué sucede cuando `MarkReady` se invoca con categoría que acaba de quedar `ARCHIVED` o con <5 válidas entre `CreateGame` y `MarkReady`? Debe rechazar `CategoryNotReady` y permanecer `DRAFT`.
- ¿Qué sucede cuando `JoinGame` se invoca después de `StartGame` (`IN_PROGRESS`)? Rechazo `InvalidGameState` (lobby cerrado) salvo que spec permita `late join` — fuera de alcance, se rechaza.
- ¿Qué sucede cuando `MinPlayers=0` o `MaxPlayers` incoherente? Debe rechazarse en `CreateGame` (SPEC-001), pero si existe, `StartGame` exige `players ≥ max(1, MinPlayers)` y `players ≤ MaxPlayers`.
- ¿Qué sucede cuando `StartRound` no encuentra pregunta válida SPEC-003 (`NoAvailableQuestion`)? Debe abortar ronda y forzar `FORCED_FINISHED` o mantener `ROUND_COMPLETED` y emitir `NoAvailableQuestion` sin avanzar.
- ¿Qué sucede cuando `SubmitAnswer` llega duplicado (mismo `PlayerId` + `RoundId` + `IdempotencyKey`)? Debe ser idempotente: segunda vez retorna mismo resultado sin duplicar `PointTransaction` ni `Score`.
- ¿Qué sucede cuando dos `StartRound` o dos `CompleteRound` concurren? Solo uno gana (`rowversion`); el segundo recibe `409 Conflict`.
- ¿Qué sucede cuando se intenta `UpdateGame`/`FinishGame` con `rowversion` stale? `409 Conflict` y requiere recargar `GET /api/games/{id}`.
- ¿Qué sucede cuando `ForceFinish` se invoca sin motivo? Requiere `Reason` 3–500 chars; vacío → `400 Validation`.
- ¿Qué sucede cuando `CancelGame` se invoca desde `FINISHED`? Rechazo `InvalidGameState`.
- ¿Qué sucede cuando `SubmitAnswer` llega exactamente en el límite `TimeLimitPerQuestion` con skew? Se evalúa con `ServerTimestamp - RoundStartedAt`; >TimeLimit → `Timeout` (rechazo temporal).
- ¿Qué sucede cuando el juego queda sin jugadores elegibles tras abandono (SPEC-011)? Próximo `StartRound` debe fallar y permitir solo `Finish`/`ForcedFinished`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema MUST modelar el ciclo de vida como máquina de estados explícita con los 9 estados `DRAFT → READY → WAITING_FOR_PLAYERS → IN_PROGRESS → ROUND_IN_PROGRESS → ROUND_COMPLETED → (loop) → FINISHED`, más terminales `CANCELLED` y `FORCED_FINISHED`, con transiciones sólo por comportamiento de dominio y protegidas por concurrencia optimista (`rowversion`).
- **FR-002**: El sistema MUST exponer transiciones explícitas: `CreateGame` (`→ DRAFT`), `Prepare/MarkReady` (`DRAFT → READY` con gate), `OpenLobby/WaitForPlayers` (`READY → WAITING_FOR_PLAYERS`), `JoinGame` (agrega `GamePlayer` en `WAITING_FOR_PLAYERS` y emite `PlayerJoined`; valida `MaxPlayers` y no duplicados), `StartGame` (`WAITING_FOR_PLAYERS → IN_PROGRESS` con gates), `StartRound` (`IN_PROGRESS` o `ROUND_COMPLETED` → `ROUND_IN_PROGRESS` criando `GameRound` + asignando `QuestionId` PUBLISHED), `CompleteRound` (`ROUND_IN_PROGRESS → ROUND_COMPLETED`), `FinishGame` (`IN_PROGRESS`/`ROUND_COMPLETED`/`WAITING_FOR_PLAYERS`/`ROUND_IN_PROGRESS` según matriz válida → `FINISHED`), `CancelGame` (`DRAFT/READY/WAITING_FOR_PLAYERS/IN_PROGRESS/ROUND_*` → `CANCELLED`), `ForceFinishGame` (`IN_PROGRESS/ROUND_*` → `FORCED_FINISHED`).
- **FR-003**: El sistema MUST impedir `MarkReady`/`StartGame` sin configuración válida (SPEC-001: `CategoryId` publicada con ≥5 válidas SPEC-002/003, `MinRounds≥5`, `MaxRounds≥MinRounds`, `TimeLimitPerQuestion` 5–300s positivo, `Difficulty` 1..5 + `DifficultyStrategy`, `Loss/Withdrawal/Consolation` policies, `MinPlayers≥1 ≤ MaxPlayers`); si inválida MUST retornar `InvalidGameConfiguration`/`CategoryNotReady` y no cambiar estado.
- **FR-004**: El sistema MUST impedir `StartGame` sin jugadores suficientes: `players.Count ≥ MinPlayers` y `players.Count ≤ MaxPlayers`; si insuficiente MUST retornar `NotEnoughPlayers`; `JoinGame` MUST validar duplicados (`PlayerAlreadyJoined`), capacidad (`GameFull`) y estado (`WAITING_FOR_PLAYERS` únicamente).
- **FR-005**: El sistema MUST impedir `StartRound` si la ronda anterior no terminó: solo permitido desde `IN_PROGRESS` (sin ronda activa) o `ROUND_COMPLETED`; si `ROUND_IN_PROGRESS` activo MUST retornar `RoundAlreadyInProgress`/`PreviousRoundNotCompleted`; `StartRound` MUST seleccionar `Question` válida PUBLISHED (SPEC-003) no usada previamente en el `Game` y crear `GameRound` con `RoundNumber` incremental.
- **FR-006**: El sistema MUST impedir `SubmitAnswer` si no está en `ROUND_IN_PROGRESS`: solo `ROUND_IN_PROGRESS` acepta respuestas; en cualquier otro estado MUST retornar `NoActiveRound`/`InvalidGameState`; la evaluación de correctitud MUST ser server-side (compara `AnswerOption.IsCorrect` desde `Question` PUBLISHED, no confía en cliente), usando `ServerTimestamp` para `TimeLimit`.
- **FR-007**: El sistema MUST impedir cualquier modificación de `Game` o `GameConfiguration` después de comenzar: toda mutación de configuración (`UpdateGame`, `UpdateConfiguration`) después de `IN_PROGRESS`/`ROUND_*`/`FINISHED`/`CANCELLED`/`FORCED_FINISHED` MUST retornar `ConfigurationImmutable`/`InvalidGameState`; `GameConfiguration` es inmutable tras `StartGame`.
- **FR-008**: El sistema MUST permitir solo finalización desde estados válidos: `FINISHED` permitido solo desde `IN_PROGRESS`, `ROUND_COMPLETED` (y `ROUND_IN_PROGRESS` si política lo permite); `CANCELLED` desde `DRAFT/READY/WAITING_FOR_PLAYERS/IN_PROGRESS/ROUND_*` (no desde terminal); `FORCED_FINISHED` desde `IN_PROGRESS/ROUND_*` (sistema/GAME_MANAGER); cualquier `Finish/Cancel/Forced` desde `FINISHED/CANCELLED/FORCED_FINISHED` MUST retornar `InvalidGameState`.
- **FR-009**: El sistema MUST modelar `Game` como `AggregateRoot<GameId>` con `GameId:StronglyTypedId<Guid>`, `GameStatus:Enumeration` (9 valores), `GameConfiguration:ValueObject` (inmutable), `RowVersion:byte[]`, `CreatedAt`, `StartedAt?`, `FinishedAt?`, `CreatedBy (sub)`, colecciones `GamePlayers` y `GameRounds`; y `GameRound : Entity<GameRoundId>` con `RoundNumber`, `QuestionId`, `Status`, `StartedAt`, `CompletedAt`; mutaciones solo vía comportamiento (`Game.Create()`, `MarkReady()`, `OpenLobby()`, `JoinPlayer()`, `Start()`, `StartRound()`, `CompleteRound()`, `Finish()`, `Cancel()`, `ForceFinish()`) retornando `Result` y aplicando `IBusinessRule`.
- **FR-010**: El sistema MUST emitir eventos de dominio tras transiciones exitosas: `GameCreated (DRAFT)`, `GameReady (READY)`, `PlayerJoined (WAITING_FOR_PLAYERS)`, `GameStarted (IN_PROGRESS)`, `RoundStarted (ROUND_IN_PROGRESS)`, `RoundCompleted (ROUND_COMPLETED)`, `GameFinished (FINISHED)`, `GameCancelled (CANCELLED)`, `GameForcedFinished (FORCED_FINISHED)` como `DomainEvent` dentro de `AppDbContextBase.SaveChanges`; eventos de integración vía `Outbox` (`IOutboxWriter` → RabbitMQ) solo si se requiere publicación externa (e.g., `GameFinishedIntegrationEvent`).
- **FR-011**: El sistema MUST exponer cada transición vía Vertical Slice CQRS (`ICommand`/`IQuery` + `Validator` + `Handler` + `Response DTO` + `IEndpoint` thin `ISender.SendAsync → Result.ToHttpResult()`) con `ValidationBehavior` + `IBusinessRule`, y mapear `Error` a `ProblemDetails` (`400` validación, `404` not found, `409` conflicto `rowversion`, `422` si se distingue).
- **FR-012**: El sistema MUST persistir `Game` consultable vía `Specification<Game>` (filtros por `Status`, `CategoryId`, `CreatedBy`) con paginación y `ApplyAsNoTracking`, y proteger transiciones con `rowversion` (`IsRowVersion`); `DbContext` MUST derivar de `AppDbContextBase` y transacciones MUST proteger cambios multi-agregado (`Game` + `GameRound` + `Outbox`) en `SaveChanges`.
- **FR-013**: El sistema MUST garantizar idempotencia donde aplica: `JoinGame` idempotente por `PlayerId` (segundo join mismo jugador → ya unido sin duplicar), `SubmitAnswer` idempotente por `IdempotencyKey`/`PlayerId+RoundId` (duplicado no duplica `PointTransaction`), `StartRound`/`CompleteRound` protegidos por `GameId+RoundNumber` único; integración de eventos idempotente bajo `at-least-once`.
- **FR-014**: El sistema MUST auditar transiciones (append-only) con `CorrelationId`, `GameId`, `PlayerId?`, `RoundId?`, `FromState`, `ToState`, `Command`, `PerformedBy (sub)`, `Timestamp`; y observar vía `BuildingBlocks.ServiceDefaults` (OTel logs/traces/metrics + `/health`).

### Key Entities *(include if feature involves data)*

- **Game (AggregateRoot<GameId>)**: Agregado raíz del ciclo. Atributos: `GameId:StronglyTypedId<Guid>`, `Name:string`, `GameConfiguration:ValueObject` (CategoryId, MinRounds 5..50, MaxRounds, InitialDifficulty 1..5, DifficultyStrategy, TimeLimitPerQuestion, ScoringSystem, LossPolicy, WithdrawalPolicy, ConsolationPolicy, RewardRules, MinPlayers, MaxPlayers), `Status:Enumeration (DRAFT 1, READY 2, WAITING_FOR_PLAYERS 3, IN_PROGRESS 4, ROUND_IN_PROGRESS 5, ROUND_COMPLETED 6, FINISHED 7, CANCELLED 8, FORCED_FINISHED 9)`, `RowVersion:byte[]`, `CreatedAt`, `ReadyAt?`, `StartedAt?`, `FinishedAt?`, `CreatedBy (Guid sub)`, `Players: IReadOnlyList<GamePlayer>`, `Rounds: IReadOnlyList<GameRound>`. Comportamiento: `Create(config)`, `MarkReady(ICategoryValidator, IQuestionCounter)`, `OpenLobby()`, `JoinPlayer(playerId)`, `Start()`, `StartRound(IQuestionSelectionStrategy)`, `CompleteRound()`, `Finish()`, `Cancel(reason)`, `ForceFinish(reason)`; cada uno valida `FR-003..FR-008` y emite evento.

- **GameRound (Entity<GameRoundId> dentro de Game)**: Ronda. Atributos: `GameRoundId:StronglyTypedId<Guid>`, `GameId`, `RoundNumber:int (1..MaxRounds)`, `QuestionId:QuestionId (PUBLISHED)`, `Status:Enumeration (ROUND_IN_PROGRESS, ROUND_COMPLETED)`, `StartedAt`, `CompletedAt?`. Pertenece exclusivamente a `Game` (composición); mutación solo vía `Game`. Invariante: no puede haber dos `ROUND_IN_PROGRESS` simultáneas en el mismo `Game`.

- **GamePlayer (Entity<GamePlayerId> dentro de Game)**: Jugador en lobby/partida. Atributos: `GamePlayerId`, `GameId`, `UserId (sub claim de OroIdentityServer)`, `JoinedAt`, `ScoreId?` (ref). Invariante: `UserId` único por `Game`; `Count ≤ MaxPlayers`; solo `WAITING_FOR_PLAYERS` permite `Join`.

- **GameConfiguration (ValueObject)**: Config inmutable tras `StartGame`. Atributos: `CategoryId`, `MinRounds (≥5)`, `MaxRounds`, `InitialDifficulty`, `DifficultyStrategy`, `TimeLimitPerQuestion`, `PointsPerRound`, `WithdrawalPolicy`, `LossPolicy`, `ConsolationPolicy`, `RewardRules`, `MinPlayers`, `MaxPlayers`. Validado en `Game.Create` y `MarkReady` (FR-003).

- **Domain Events**: `GameCreatedDomainEvent(GameId)`, `GameReadyDomainEvent(GameId)`, `PlayerJoinedDomainEvent(GameId, UserId)`, `GameStartedDomainEvent(GameId)`, `RoundStartedDomainEvent(GameId, RoundId, RoundNumber, QuestionId)`, `RoundCompletedDomainEvent(GameId, RoundId)`, `GameFinishedDomainEvent(GameId)`, `GameCancelledDomainEvent(GameId, Reason)`, `GameForcedFinishedDomainEvent(GameId, Reason)`.

### Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% de `CreateGame` con configuración válida resulta en `201` `DRAFT` + `GameCreated`; con configuración inválida (MinRounds<5, categoría no publicable, TimeLimit≤0) 100% rechazado `InvalidGameConfiguration` sin `Game` persistido, <1s p95.
- **SC-002**: `MarkReady`/`PrepareGame` con categoría ≥5 válidas y config coherente transita `DRAFT→READY` 100% y emite `GameReady` <2s; con <5 válidas o categoría archivada 100% rechazado con `CategoryNotReady` y permanece `DRAFT`.
- **SC-003**: `StartGame` con `players ≥ MinPlayers` transita `WAITING_FOR_PLAYERS→IN_PROGRESS` 100% y emite `GameStarted`; con `players < MinPlayers` 100% rechazado `NotEnoughPlayers` y estado no cambia; segundo `StartGame` concurrente → `409 Conflict` 100% <500ms.
- **SC-004**: `StartRound` permitido solo desde `IN_PROGRESS` o `ROUND_COMPLETED`; desde `ROUND_IN_PROGRESS` 100% rechazado `RoundAlreadyInProgress`; tras `ROUND_COMPLETED` siguiente `StartRound` vuelve a `ROUND_IN_PROGRESS` 100%; realiza selección de pregunta PUBLISHED no usada previamente (SPEC-003) en <500ms p95 para 1k preguntas.
- **SC-005**: `SubmitAnswer` solo aceptado en `ROUND_IN_PROGRESS`: en `IN_PROGRESS` sin ronda activa 100% rechazado `NoActiveRound`; en `ROUND_IN_PROGRESS` 100% evaluado server-side con `PointTransaction` y auditable; duplicado por `PlayerId+RoundId+IdempotencyKey` no duplica puntos (idempotente) 100%.
- **SC-006**: 0% de intentos `UpdateGame`/`UpdateConfiguration` después de `IN_PROGRESS`/`ROUND_*`/`FINISHED` mutan `GameConfiguration`; 100% rechazados `ConfigurationImmutable` y auditados.
- **SC-007**: `FinishGame` desde estado válido (`ROUND_COMPLETED` o `IN_PROGRESS` sin ronda) transita a `FINISHED` + `GameFinished` 100%; desde `DRAFT`/`READY` 100% rechazado `InvalidGameState`; `CancelGame` solo desde no-terminal y `ForceFinish` solo desde `IN_PROGRESS`/`ROUND_*` 100% según matriz; terminales rechazan cualquier transición posterior 100% + `409` en concurrencia.
- **SC-008**: 90% de organizadores completan el flujo `Create→Ready→Wait→Join (2 jugadores)→Start→StartRound→CompleteRound→Finish` en primer intento sin consultar soporte, medido por test de usabilidad del quickstart.

## Assumptions

- `SPEC-001` existe y define reglas de configuración (MinRounds≥5, TimeLimit 5–300s, políticas, Difficulty 1..5, Min/MaxPlayers); este SPEC no redefine esas reglas, solo las aplica como gate para `MarkReady`/`StartGame`.
- `SPEC-002` provee categorías `DRAFT/ACTIVE/INACTIVE/ARCHIVED` y gate ≥5 válidas; `SPEC-003` provee preguntas `PUBLISHED` con 4/1 y selección por 7 criterios; este SPEC asume ambos disponibles (o stub `ICategoryValidator`/`IQuestionCounter`/`IQuestionSelectionStrategy` en tests).
- Estados intermedios: `IN_PROGRESS` es estado de partida sin ronda activa (entre `ROUND_COMPLETED` y siguiente `StartRound`); `ROUND_IN_PROGRESS`/`ROUND_COMPLETED` son estados de ronda observables pero modelados también como `Status` del `Game` para simplificar orquestación (alternativa: `Game.Status=IN_PROGRESS` + `CurrentRound.Status`; se adopta lo segundo y `Game.Status` refleja el estado grueso, `Game.CurrentRound.Status` el fino — el plan técnico decidirá y documentará sin romper matriz).
- `WAITING_FOR_PLAYERS` es el único estado que permite `JoinGame`; `late join` después de `IN_PROGRESS` no se permite en v1.
- `GameRounds` son 1..`MaxRounds` numerados secuenciales, sin saltos; `Round` no puede terminarse dos veces.
- Concurrencia: `rowversion` (`byte[]`) protege toda transición; segundo intento concurrente `MarkReady`/`StartGame`/`StartRound`/`CompleteRound`/`Finish` → `409 Conflict` y debe recargar `GET /api/games/{id}`.
- Eventos `GameReady`/`PlayerJoined`/`GameStarted` etc. son `DomainEvent` dentro de `AppDbContextBase.SaveChanges`; integración futura vía `Outbox` (no requerida para MVP de ciclo, pero diseño lo permite).
- Identidad vía OroIdentityServer (`oroidentityserver:latest`): `CreateGame`/`MarkReady`/`StartGame`/`Cancel` requieren `ADMIN`/`GAME_MANAGER`; `JoinGame`/`SubmitAnswer` requieren `PLAYER` (JWT `roles`).
- Cancelación y Forced requieren `Reason` 3–500 chars; `ForcedFinished` puede ser invocado por sistema (scheduler) o por `GAME_MANAGER`; `Cancel` es administrativo.
- Si `StartRound` no encuentra pregunta válida (`NoAvailableQuestion`), la ronda no se crea y se permite `Finish`/`ForcedFinished` sin bloquear; no se crea `ROUND_IN_PROGRESS` fantasma.
- `MinPlayers` por defecto 2, `MaxPlayers` por defecto 10 si no se especifica, coherente con SPEC-001.

## Dependencies

- `SPEC-001` — Game Configuration (reglas de configuración válida, `MinRounds≥5`, `TimeLimit`, políticas, `GameConfiguration` VO).
- `SPEC-002` — Categories (categoría publicada, `ICategoryValidator` / `IQuestionCounter` ≥5).
- `SPEC-003` — Question Bank (pregunta `PUBLISHED` 4/1, `IQuestionSelectionStrategy` 7 params, `ValidQuestionSpecification`).
- `SPEC-011` — (Referenciada en input) consignas/consolación/recompensas y abandono/forzado (no modelada aquí, solo `ForceFinish` reason).
- `BuildingBlocks.Kernel.Domain` — `AggregateRoot`, `Entity`, `ValueObject`, `StronglyTypedId`, `Enumeration`, `IBusinessRule`, `Result`, `IDomainEvent`.
- `BuildingBlocks.CQRS` — `ICommand`/`IQuery`, `ICommandHandler`/`IQueryHandler`, `ISender`, `IPipelineBehavior`, `ValidationBehavior`.
- `BuildingBlocks.Kernel.Infrastructure` — `IRepository`, `IUnitOfWork`, `AppDbContextBase`, `Specification<T>`, `IOutboxWriter`.
- `BuildingBlocks.ServiceDefaults` — `IEndpoint`, `Result → HTTP`, `GlobalExceptionHandler`, `ProblemDetails`, OpenTelemetry, health checks.
- OroIdentityServer — OIDC discovery `/.well-known/openid-configuration`, JWT `jwks_uri`, claims `sub/roles/tenant_id`.

## Out of Scope

- Lógica de selección concreta (Random/DifficultyAware/Adaptive) más allá del contrato 7 params — definida en SPEC-003 / plan técnico ADR-008.
- Evaluación de respuestas y cálculo de puntaje detallado, `PointTransaction` ledger, avance de dificultad por ronda, retiro/pérdida/consolación/premios — motor evaluativo posterior a `RoundCompleted` (usa `GameFinished` en SPEC-011).
- UI específica (Angular/Web) más allá del contrato REST necesario (`POST /api/games`, `POST /api/games/{id}/ready`, `POST /api/games/{id}/open-lobby`, `POST /api/games/{id}/players`, `POST /api/games/{id}/start`, `POST /api/games/{id}/rounds/start`, `POST /api/games/{id}/rounds/{roundId}/complete`, `POST /api/games/{id}/finish|cancel|force-finish`).
- Importación masiva de rondas, repetición de preguntas entre juegos distintos (permitida), o `late join` después de `IN_PROGRESS`.

## References

- Constitución v1.1.0 — Principios I-VI, Additional Constraints A (State Machine), B (Question/Category ≥5), C (Configurable), E/F (persistencia/rowversion), G (Outbox), H (OroIdentityServer).
- `draft/constitution.md` §5 (State Machine), §8 (Game Configuration), §6 (Question Invariants).
- `draft/game-concept.md` §3-5 (Game/Round lifecycle), §12-14 (Withdrawal/Loss).
- SPEC-001 — Game Configuration.
- SPEC-002 — Categories.
- SPEC-003 — Question Bank.
- SPEC-011 — (Abandono/Consolación — referencia para `ForcedFinished`).

