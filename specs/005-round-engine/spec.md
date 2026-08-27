# Feature Specification: Round Engine

**Feature Branch**: `005-round-engine`

**Created**: 2026-08-27

**Status**: Draft

**Input**: User description: "005 — Round Engine Objetivo Definir el motor encargado de administrar las rondas. Reglas Cada juego: minimumRounds >= 5 Cada ronda debe tener: RoundNumber Difficulty Question TimeLimit Status Flujo StartRound ↓ SelectQuestion ↓ PresentQuestion ↓ WaitForAnswers ↓ EvaluateAnswers ↓ CalculateScores ↓ CompleteRound ↓ IncreaseDifficulty Selección aleatoria La selección debe: Ser impredecible desde el cliente. Evitar preguntas repetidas dentro del juego. Respetar categoría. Respetar dificultad. Respetar reglas académicas/etarias. Progresión Ejemplo: Round 1 → Level 1 Round 2 → Level 2 Round 3 → Level 3 Round 4 → Level 4 Round 5 → Level 5 La estrategia debe ser configurable. Dependencias SPEC-001 SPEC-003 SPEC-004"

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Iniciar ronda y seleccionar pregunta impredecible y no repetida (Priority: P1)

Como motor del juego, quiero iniciar una ronda (`StartRound`) seleccionando una pregunta aleatoria que el cliente no pueda predecir, que no se haya usado antes en el mismo juego, y que respete `Category`/`Difficulty`/`AcademicLevel`/`AgeRange` del juego y la categoría.

**Why this priority**: Es el núcleo que conecta `SPEC-004` (lifecycle) con `SPEC-003` (banco). Sin selección impredecible y filtrada no hay juego justo ni cumple la progresión. Entrega valor independiente como primera mitad del flujo `StartRound→SelectQuestion→PresentQuestion`.

**Independent Test**: Con `Game` en `IN_PROGRESS` o `ROUND_COMPLETED` con config `Category X`, `Difficulty 2`, `AcademicLevel Secundaria`, `Age 13-17`, y banco con 10 preguntas `PUBLISHED` que cumplen filtros y 5 que no, al invocar `StartRound` debe crear `GameRound` con `RoundNumber` incremental, `Difficulty` según progresión, `Question` con `QuestionId` PUBLISHED no usada previamente, `TimeLimit` copiado de `GameConfiguration.TimeLimitPerQuestion`, `Status=ROUND_IN_PROGRESS`. Verificar que `QuestionId` no se repite en el mismo `Game` en llamadas sucesivas y que preguntas fuera de categoría/dificultad nunca son seleccionadas. Verificar que dos juegos distintos seleccionan preguntas diferentes (no correlacionadas) en el mismo `RoundNumber` incluso con mismos filtros (impredecible).

**Acceptance Scenarios**:

1. **Given** `Game` en `IN_PROGRESS` sin ronda activa, con `previousQuestionIds=[]`, **When** se invoca `StartRound`, **Then** el sistema selecciona 1 `Question` `PUBLISHED` que coincide con `CategoryId`, `Difficulty` del round, `AcademicLevel` y `AgeRange`, la asigna a `GameRound.QuestionId`, fija `RoundNumber = previousRounds.Count+1`, `TimeLimit = GameConfiguration.TimeLimitPerQuestion`, `Status=ROUND_IN_PROGRESS`, y emite `RoundStarted`.
2. **Given** `Game` con `previousQuestionIds=[Q1,Q2]` ya usadas, **When** se invoca `StartRound` siguiente, **Then** el sistema excluye `Q1,Q2` del universo elegible; si todas las que cumplen filtros ya fueron usadas, retorna `NoAvailableQuestion` y no crea `ROUND_IN_PROGRESS` fantasma (permite `Finish`/`ForcedFinish`).
3. **Given** banco con preguntas que violan `Difficulty` o `AcademicLevel` o `AgeRange` o `Category` o son `DRAFT/ARCHIVED`, **When** se invoca `StartRound`, **Then** ninguna de esas es elegible; solo `PUBLISHED` alineadas se consideran.
4. **Given** dos `StartRound` concurrentes para el mismo `Game` en `IN_PROGRESS`, **When** ambos llegan simultáneamente, **Then** solo uno crea `ROUND_IN_PROGRESS` (rowversion), el otro recibe `409 Conflict` / `RoundAlreadyInProgress`.
5. **Given** cliente intenta predecir siguiente pregunta observando payload de `StartRound` anterior o `GET /api/games/{id}`, **When** inspecciona respuesta, **Then** no hay pista determinista (orden aleatorio, no correlación por `RoundNumber` o `QuestionId` secuencial); la selección es server-side aleatoria.

---

### User Story 2 — Presentar pregunta, esperar respuestas, evaluar y calcular puntajes, completar ronda (Priority: P1)

Como jugador y como sistema autoritativo, quiero que tras `SelectQuestion` el sistema `PresentQuestion` (exponga pregunta sin revelar `IsCorrect`), `WaitForAnswers` dentro del `TimeLimit`, `EvaluateAnswers` contra `AnswerOption.IsCorrect` server-side, `CalculateScores` (ledger `PointTransaction`), y `CompleteRound`→`ROUND_COMPLETED` para avanzar.

**Why this priority**: Cierra el ciclo de una ronda jugable completa y habilita la progresión. Sin evaluar/calc puntajes no hay `Score` ni `Leaderboard`. Entrega valor independiente como segunda mitad del flujo; puede probarse con una sola ronda aunque el juego requiera ≥5.

**Independent Test**: Iniciar ronda → `GET /api/games/{id}/rounds/{roundId}` debe retornar `Question` con `Text` y 4 `AnswerOptions` sin `IsCorrect` expuesto al `PLAYER` (solo `Id/Text/DisplayOrder`). Enviar `SubmitAnswer` dentro de `TimeLimit` con `AnswerOptionId` correcto vs incorrecto → `EvaluateAnswers` retorna `correct=true/false` server-side, `CalculateScores` crea `PointTransaction` con `Points` según `PointsPerRound` y dificultad, `RoundStatus` sigue `ROUND_IN_PROGRESS` hasta `CompleteRound`. Invocar `CompleteRound` → `Status=ROUND_COMPLETED`, emite `RoundCompleted`, persiste `CompletedAt`, bloquea más `SubmitAnswer` para esa ronda (`NoActiveRound`).

**Acceptance Scenarios**:

1. **Given** `GameRound` en `ROUND_IN_PROGRESS` con `Question Q1` (PUBLISHED, 4 opciones, 1 correcta), **When** `PLAYER` hace `GET` de la pregunta de la ronda, **Then** el sistema retorna `QuestionId, Text, CategoryId, Difficulty, TimeLimit, Status=ROUND_IN_PROGRESS` y `AnswerOptions` con `Id/Text/DisplayOrder` pero sin revelar `IsCorrect` (o filtrado por rol `PLAYER`).
2. **Given** `ROUND_IN_PROGRESS` con `TimeLimit=30s` y `StartedAt=T0`, **When** `PLAYER` envía `SubmitAnswer` con `AnswerOptionId` correcto en `T0+10s`, **Then** `EvaluateAnswers` valida `serverTimestamp - StartedAt ≤ 30s`, compara contra `IsCorrect` del agregado `Question` (no confía en cliente), marca `correct=true`, y `CalculateScores` crea `PointTransaction` ledger (`Type=ANSWER_CORRECT`, `Points=PointsPerRound` ajustado por dificultad si aplica).
3. **Given** mismo `SubmitAnswer` pero con `AnswerOptionId` incorrecta, **When** se evalúa, **Then** `correct=false`, `PointTransaction` con 0 o penalización según `LossPolicy` (fuera de alcance de este SPEC, pero no duplica `Score`).
4. **Given** `SubmitAnswer` enviado en `T0+31s` (fuera de `TimeLimit`) o después de `CompleteRound`, **When** llega, **Then** se rechaza con `AnswerTimeout`/`NoActiveRound` y no crea `PointTransaction`.
5. **Given** `SubmitAnswer` duplicado con mismo `IdempotencyKey` (`PlayerId+RoundId`), **When** se reenvía, **Then** es idempotente: retorna mismo resultado sin duplicar `PointTransaction`/puntaje.
6. **Given** `ROUND_IN_PROGRESS` con varios jugadores, **When** todos envían respuestas o expira `TimeLimit` (server-side scheduler o `CompleteRound` manual), **Then** el organizador/`SYSTEM` puede invocar `CompleteRound` y el sistema transita a `ROUND_COMPLETED` con `CompletedAt`, bloqueando más respuestas para esa ronda.

---

### User Story 3 — Progresión de dificultad configurable por ronda (Priority: P1)

Como organizador, quiero que la dificultad aumente por ronda según una estrategia configurable (`Linear` ejemplo 1→2→3→4→5) para que el juego sea progresivamente desafiante y la selección respete la dificultad del round.

**Why this priority**: Es la regla de negocio diferenciadora del Round Engine (no es solo repetir rondas). Sin progresión, la experiencia es plana. Entrega valor independiente como política intercambiable sin cambiar flujo.

**Independent Test**: Configurar `Game` con `MinRounds=5`, `MaxRounds=5`, `InitialDifficulty=1`, `DifficultyStrategy=Linear`. Iniciar 5 rondas secuenciales (Start→Complete loop) y verificar que `GameRound.Difficulty` sea `1,2,3,4,5` respectivamente y que `SelectQuestion` de cada ronda filtró por esa dificultad (solo preguntas con `Difficulty` igual a la del round fueron elegibles). Cambiar estrategia a `Progressive` o `Adaptive` y verificar que `Round.Difficulty` sigue la otra curva sin cambiar contrato `StartRound`.

**Acceptance Scenarios**:

1. **Given** `GameConfiguration` con `InitialDifficulty=1`, `DifficultyStrategy=Linear`, `MinRounds=5`, **When** se inicia `Round 1`, **Then** `Difficulty=1`; **When** se completa `Round 1` y se inicia `Round 2`, **Then** `Difficulty=2` (incrementa 1), y así hasta `Round 5→5`.
2. **Given** `DifficultyStrategy=Progressive` (ej. 1,1,2,3,5) u otra registrada, **When** se avanza de ronda, **Then** el sistema aplica la estrategia correspondiente sin cambiar el flujo `StartRound→…→CompleteRound`; la estrategia es intercambiable detrás de `IDifficultyProgressionStrategy`.
3. **Given** `Game` con `MaxRounds=7` pero `MinRounds=5`, **When** se alcanzó `Round 5` y la organización decide continuar, **Then** `IncreaseDifficulty` sigue aplicando la estrategia para `Round 6` y `Round 7` mientras queden rondas y el juego no haya alcanzado `FINISHED`.
4. **Given** intento de crear juego con `MinRounds <5`, **When** se envía `CreateGame`, **Then** se rechaza con `InvalidGameConfiguration.MinRoundsTooLow` (regla `minimumRounds >=5`).
5. **Given** `GameRound` con `Difficulty` ya asignada, **When** se intenta mutar `Difficulty` directamente vía `Update`, **Then** se rechaza (inmutable dentro de la ronda; solo `IncreaseDifficulty` en transición crea la siguiente ronda con nueva dificultad).

---

### User Story 4 — Invariantes de ronda y flujo completo de 8 pasos (Priority: P2)

Como sistema autoritativo, quiero que cada ronda siempre tenga `RoundNumber, Difficulty, Question, TimeLimit, Status`, que un juego nunca tenga menos de 5 rondas mínimas al finalizar, y que el flujo canónico de 8 pasos se preserve `StartRound→SelectQuestion→PresentQuestion→WaitForAnswers→EvaluateAnswers→CalculateScores→CompleteRound→IncreaseDifficulty` como una orquestación transaccional y auditable.

**Why this priority**: Garantiza integridad estructural y trazabilidad del motor; es P2 porque depende de US1-US3 pero es necesaria para reporte, `FINISHED` y auditoría. Entrega valor independiente como verificación de invariantes y flujo.

**Independent Test**: Crear juego con `MinRounds=5`, generar 5 rondas completas con el flujo de 8 pasos, verificar que cada `GameRound` persiste los 5 campos no nulos, que `RoundNumber` es 1..5 sin saltos ni duplicados por `GameId`, que `TimeLimit` coincide con `GameConfiguration.TimeLimitPerQuestion`, y que `Status` transita `ROUND_IN_PROGRESS→ROUND_COMPLETED` atomáticamente en `CompleteRound`. Intentar finalizar juego con solo 3 rondas completadas → `400 InvalidGameState` (mínimo no alcanzado). Verificar audit log `RoundStarted/RoundCompleted` con `RoundId`.

**Acceptance Scenarios**:

1. **Given** juego en `IN_PROGRESS` con `MinRounds=5`, **When** se crean y completan 5 rondas, **Then** cada `GameRound` tiene `RoundNumber` (1..5), `Difficulty` (según progresión), `QuestionId` (PUBLISHED, no repetida), `TimeLimit` (= `GameConfiguration.TimeLimitPerQuestion`), `Status` (`ROUND_IN_PROGRESS` luego `ROUND_COMPLETED`), y `FinishedAt` tras `CompleteRound`.
2. **Given** intento de crear juego con `MinRounds=4`, **When** se envía, **Then** se rechaza con `InvalidGameConfiguration` y no se persiste juego jugable.
3. **Given** flujo de 8 pasos en orden, **When** se ejecuta `StartRound` (crea ronda) → `SelectQuestion` (aleatoria no repetida) → `PresentQuestion` (sin revelar correcta) → `WaitForAnswers` (ventana `TimeLimit`) → `EvaluateAnswers` (server-side) → `CalculateScores` (ledger) → `CompleteRound` (transita) → `IncreaseDifficulty` (siguiente ronda con dificultad incrementada), **Then** todo ocurre en transacción por ronda (agregado + Outbox) y cada paso emite log `RoundId`/`GameId`.
4. **Given** juego con 3 rondas completadas (`< MinRounds`), **When** se intenta `FinishGame` (`FINISHED`), **Then** se rechaza con `NotEnoughRounds`/`InvalidGameState` hasta alcanzar 5.
5. **Given** dos `StartRound` concurrentes para el mismo `Game` en `ROUND_COMPLETED`, **When** ambos llegan, **Then** solo uno crea `ROUND_IN_PROGRESS` (rowversion/unique `RoundNumber`), el otro `409 Conflict`.

---

### Edge Cases

- ¿Qué sucede cuando `StartRound` no encuentra pregunta que cumpla `Category`/`Difficulty`/`AcademicLevel`/`AgeRange` y sin repetir? Retorna `NoAvailableQuestion` (409/400) y no crea ronda fantasma; permite `Finish`/`ForcedFinish` sin bloquear.
- ¿Qué sucede cuando `TimeLimitPerQuestion` es `0` o `>300`? Rechazo `InvalidTimeLimit` en `CreateGame` (SPEC-001); `GameRound.TimeLimit` copia el valor validado, no se recalcula.
- ¿Qué sucede cuando `Question` asignada es archivada o pasa a no `PUBLISHED` después de `StartRound` pero antes de `CompleteRound`? La ronda ya creó snapshot con `QuestionId`; la evaluación sigue usando el snapshot, pero futuras rondas no la seleccionan.
- ¿Qué sucede cuando `SubmitAnswer` llega exactamente en `TimeLimit` con skew? Evaluación usa `ServerTimestamp - Round.StartedAt`; ≤TimeLimit correcto, >TimeLimit `Timeout` (rechazo).
- ¿Qué sucede cuando `Difficulty` progresiva pide `Level 6` pero solo existen 5 niveles? Se mantiene en `5` (clamp) o usa estrategia `CategorySpecific` que mapea; nunca excede 1..5.
- ¿Qué sucede cuando `RoundNumber` duplica por concurrencia? `UNIQUE (GameId, RoundNumber)` + `rowversion` en `Game` produce `409 Conflict` en el segundo.
- ¿Qué sucede cuando `GameConfiguration` cambia entre rondas (ej. categoría se despublica)? `GameConfiguration` es inmutable tras `StartGame` (SPEC-004), por lo que `Round` siempre usa la config Snapshot del `Game` inicial.
- ¿Qué sucede cuando `IncreaseDifficulty` se invoca sin `CompleteRound` previo? Rechazo `PreviousRoundNotCompleted` (mismo que `StartRound` guard).
- ¿Qué sucede cuando `MinRounds=5` y `MaxRounds=50` pero el banco solo tiene 5 preguntas que cumplen filtros? `Round 6` fallará `NoAvailableQuestion`; el juego debe poder `Finish` tras 5 aunque `MaxRounds` sea 50 (mínimo satisfecho).
- ¿Qué sucede cuando `PresentQuestion` filtra `IsCorrect` pero un `Admin` necesita ver la correcta? `GET /api/questions/{id}` con rol `ADMIN/GAME_MANAGER` sí expone `IsCorrect`; `GET /api/games/{id}/rounds/{roundId}/question` con `PLAYER` no.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema MUST exigir que cada `Game` tenga `minimumRounds >= 5`; `CreateGame` con `MinRounds <5` MUST rechazarse con `InvalidGameConfiguration.MinRoundsTooLow` y no persistir juego jugable. El sistema MUST persistir `MinRounds` como parte de `GameConfiguration` y usarlo como gate para `FinishGame` (`completedRounds ≥ MinRounds`).
- **FR-002**: El sistema MUST garantizar que cada `GameRound` tenga exactamente `RoundNumber` (int 1..MaxRounds, único por `GameId`, incremental sin saltos), `Difficulty` (Enumeration 1..5), `QuestionId` (FK lógico a `Question` PUBLISHED, no repetida dentro del mismo `Game`), `TimeLimit` (int seconds 5–300 copiado de `GameConfiguration.TimeLimitPerQuestion`), y `Status` (Enumeration `ROUND_IN_PROGRESS`/`ROUND_COMPLETED`). Campos nulos o duplicados MUST rechazarse.
- **FR-003**: El sistema MUST implementar el flujo canónico de 8 pasos como orquestación del agregado `Game` (o `RoundEngine` si se extrae como servicio de dominio): `StartRound` → `SelectQuestion` → `PresentQuestion` → `WaitForAnswers` (ventana `TimeLimit`) → `EvaluateAnswers` (server-side) → `CalculateScores` (ledger) → `CompleteRound` → `IncreaseDifficulty` (prepara siguiente). Cada paso MUST ser auditable y transaccional por ronda (agregado + Outbox misma transacción).
- **FR-004**: El sistema MUST hacer que `SelectQuestion` sea aleatoria e impredecible desde el cliente: la elección MUST ser server-side vía `IQuestionSelectionStrategy` (SPEC-003) con `ORDER BY NEWID()` / random `Guid` o equivalente, sin exponer semilla o correlación por `RoundNumber`/`QuestionId` secuencial; el cliente no debe poder predecir la siguiente pregunta observando respuestas previas.
- **FR-005**: El sistema MUST evitar preguntas repetidas dentro del mismo `Game`: `SelectQuestion` MUST excluir `PreviousQuestionIds = Rounds.Select(r=>r.QuestionId)` del juego; si todas las que cumplen filtros ya fueron usadas, MUST retornar `NoAvailableQuestion` y no crear `ROUND_IN_PROGRESS` fantasma; preguntas repetidas entre juegos distintos están permitidas.
- **FR-006**: El sistema MUST respetar `Category` en la selección: `SelectQuestion` MUST filtrar `Question.CategoryId == Game.Configuration.CategoryId`; preguntas fuera de la categoría del juego MUST ser siempre excluidas, incluso si cumplen otras características.
- **FR-007**: El sistema MUST respetar `Difficulty` del round en la selección: `SelectQuestion` MUST filtrar `Question.Difficulty == GameRound.Difficulty` del round actual (determinado por `IDifficultyProgressionStrategy`); preguntas con dificultad distinta MUST ser excluidas del universo elegible de esa ronda.
- **FR-008**: El sistema MUST respetar reglas académicas/etarias en la selección: `SelectQuestion` MUST filtrar `AcademicLevel` y `AgeRange` compatibles con `GameConfiguration` (y `Category`); la compatibilidad es `AcademicLevel.Value == Question.AcademicLevel.Value` (case-insensitive) y `AgeRange` solapamiento (`Max ≥ other.Min && Min ≤ other.Max`); preguntas desalineadas MUST ser excluidas.
- **FR-009**: El sistema MUST hacer que la progresión de dificultad sea configurable: el cálculo `Round.Difficulty = IDifficultyProgressionStrategy.NextDifficulty(game, completedRounds)` MUST ser intercambiable sin cambiar flujo; la estrategia por defecto MUST ser `Linear` (ej. 1→2→3→4→5 para 5 rondas, clamp 1..5); otras registradas MUST incluir `Progressive`, `Adaptive`, `CategorySpecific` (al menos 2 además de `Linear`).
- **FR-010**: El sistema MUST implementar el ejemplo de progresión `Round 1→Level 1, Round 2→Level 2, ... Round 5→Level 5` cuando `InitialDifficulty=1`, `Strategy=Linear`, `MinRounds=5`; y MUST garantizar que cambiar estrategia no rompe invariantes `FR-002` ni contrato `StartRound`.
- **FR-011**: El sistema MUST modelar `Game` como `AggregateRoot<GameId>` y `GameRound : Entity<GameRoundId>` con `GameId`, `RoundNumber`, `Difficulty`, `QuestionId`, `TimeLimit`, `Status`, `StartedAt`, `CompletedAt`, `RowVersion` en `Game`; mutaciones solo vía comportamiento (`Game.StartRound(...)`, `CompleteRound(...)`) retornando `Result` con `IBusinessRule` y emitiendo `DomainEvent` dentro de `AppDbContextBase.SaveChanges`.
- **FR-012**: El sistema MUST exponer el Round Engine vía Vertical Slice CQRS (`ICommand`/`IQuery` + `Validator` + `Handler` + `Response DTO` + `IEndpoint` thin `ISender.SendAsync → Result.ToHttpResult()`) con `ValidationBehavior` + `IBusinessRule`, y mapear `Error` a `ProblemDetails` (`400` validación, `404` not found, `409` conflicto `rowversion` / `RoundAlreadyInProgress` / `NoAvailableQuestion`).
- **FR-013**: El sistema MUST persistir `Game` y `GameRound` consultables vía `Specification<Game>` / `Specification<GameRound>` con `Include(Rounds)` + `Include(Players)` para rehidratación, proteger `StartRound`/`CompleteRound` con `rowversion` (`IsRowVersion`) y `UNIQUE (GameId, RoundNumber)` / `UNIQUE (GameId, QuestionId)` opcional para idempotencia, y transacciones MUST proteger cambios multi-entidad (`Game` + `GameRound` + `Outbox`) en `SaveChanges`.
- **FR-014**: El sistema MUST garantizar que `PresentQuestion` no revele `AnswerOption.IsCorrect` a rol `PLAYER`: `GET /api/games/{id}/rounds/{roundId}/question` con `PLAYER` MUST retornar `Question` con `AnswerOptions` filtradas a `Id/Text/DisplayOrder` sin `IsCorrect`; con `ADMIN/GAME_MANAGER` sí puede exponer `IsCorrect` vía `GET /api/questions/{id}` (SPEC-003).
- **FR-015**: El sistema MUST auditar cada paso del flujo con `CorrelationId`, `GameId`, `RoundId`, `RoundNumber`, `QuestionId`, `Difficulty`, `TimeLimit`, `FromStatus`, `ToStatus`, `Command`, `PerformedBy (sub)`, `Timestamp`, `Duration`; y observar vía `BuildingBlocks.ServiceDefaults` (OTel logs/traces/metrics + `/health`).

### Key Entities *(include if feature involves data)*

- **Game (AggregateRoot<GameId>)** — Ya existe (SPEC-001/004). Para este SPEC: `GameConfiguration:ValueObject` (CategoryId, MinRounds≥5, MaxRounds, InitialDifficulty 1..5, DifficultyStrategy, TimeLimitPerQuestion 5–300, PointsPerRound, Min/MaxPlayers, políticas), `Status:Enumeration` 9 estados, `Rounds: IReadOnlyList<GameRound>`, `Players: IReadOnlyList<GamePlayer>`, `RowVersion`. Comportamiento relevante: `StartRound(IQuestionSelectionStrategy)`, `CompleteRound(RoundId)`, `Finish()` gate `completedRounds≥MinRounds`. Invariante: `MinRounds≥5`.

- **GameRound (Entity<GameRoundId> dentro de Game)** — Ronda jugable. Atributos: `GameRoundId:StronglyTypedId<Guid>`, `GameId:GameId`, `RoundNumber:int` (1..MaxRounds, único por GameId, incremental), `Difficulty:DifficultyLevel : Enumeration 1..5` (determinada por `IDifficultyProgressionStrategy`), `QuestionId:QuestionId` (PUBLISHED, no repetida), `TimeLimit:int` (copiado de `GameConfiguration.TimeLimitPerQuestion`), `Status:Enumeration (ROUND_IN_PROGRESS, ROUND_COMPLETED)`, `StartedAt:DateTimeOffset`, `CompletedAt:DateTimeOffset?`, `RowVersion` vía agregado `Game`. Comportamiento: creada solo vía `Game.StartRound`, completada vía `Game.CompleteRound`, inmutable `Difficulty/QuestionId/TimeLimit` tras creación.

- **Question (referencia externa, SPEC-003)** — No se modela aquí salvo `QuestionId` FK lógico, `Difficulty`, `AcademicLevel`, `AgeRange`, `CategoryId`, `Status=PUBLISHED`, `AnswerOptions (4, 1 correcta)`. Debe ser seleccionable vía `Specification<Question>` (`ValidQuestionSpecification`, `QuestionSelectionSpecification`). Relación `Game 1—* GameRound *—1 Question`.

- **IDifficultyProgressionStrategy (Strategy)** — Política configurable de progresión. Contrato `NextDifficulty(Game, completedRounds) → DifficultyLevel`. Implementaciones: `LinearDifficultyStrategy` (1→2→3→4→5 clamp 1..5), `ProgressiveDifficultyStrategy` (ej. 1,1,2,3,5), `AdaptiveDifficultyStrategy` (basada en desempeño), `CategorySpecificDifficultyStrategy`. Al menos 2 además de `Linear` deben existir.

- **IQuestionSelectionStrategy (referencia externa, SPEC-003)** — Abstracción ya existente (`RandomQuestionSelectionStrategy` default, `DifficultyAware`). Contrato `SelectAsync(QuestionSelectionCriteria) → IReadOnlyList<Question>` donde `criteria` incluye `CategoryId, Difficulty (del round), AcademicLevel, AgeRange, PreviousQuestionIds (del Game), GameId, RoundNumber, Take=1`. Este SPEC la consume en `StartRound`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% de juegos creados con `MinRounds <5` son rechazados con `InvalidGameConfiguration.MinRoundsTooLow` sin persistir `Game` (<1s p95).
- **SC-002**: Cada ronda creada tiene los 5 campos (`RoundNumber` 1..MaxRounds sin saltos, `Difficulty` 1..5, `QuestionId` PUBLISHED no nula, `TimeLimit` = `GameConfiguration.TimeLimitPerQuestion`, `Status` `ROUND_IN_PROGRESS` luego `ROUND_COMPLETED`) en 100% de rondas, verificado por `GET /api/games/{id}` y DB `UNIQUE (GameId, RoundNumber)`.
- **SC-003**: La secuencia de 8 pasos `StartRound→SelectQuestion→PresentQuestion→WaitForAnswers→EvaluateAnswers→CalculateScores→CompleteRound→IncreaseDifficulty` se completa como orquestación por ronda sin omitir pasos en 100% de rondas (verificable por audit log `RoundStarted/RoundCompleted` y `PointTransaction` creado).
- **SC-004**: La selección de pregunta es impredecible desde el cliente: dos invocaciones sucesivas de `StartRound` con mismos filtros en el mismo juego retornan preguntas que no siguen correlación por `RoundNumber` ni `QuestionId` secuencial, y dos juegos distintos con mismos filtros no correlacionan su selección (verificado por distribución aleatoria y `ORDER BY NEWID()` en 1k muestras, p-value aleatoriedad).
- **SC-005**: 0% de rondas en el mismo juego reciben la misma `QuestionId` (no repetición intra-juego) en 100% de juegos que completan `MinRounds` preguntas cuando el banco tiene suficientes; si el banco se agota, `NoAvailableQuestion` se retorna correctamente y no se crea ronda fantasma en 100% de casos.
- **SC-006**: 100% de rondas seleccionan preguntas con `Category == Game.CategoryId`; 0% de preguntas fuera de categoría son elegibles, verificado sobre banco con 2 categorías × 100 preguntas cada una.
- **SC-007**: 100% de rondas seleccionan preguntas con `Difficulty == Round.Difficulty` (según progresión); 0% de dificultad distinta es elegible para esa ronda, verificado sobre banco con dificultades 1..5 distribuidas.
- **SC-008**: 100% de rondas seleccionan preguntas con `AcademicLevel` y `AgeRange` compatibles con `GameConfiguration`/`Category`; 0% desalineadas son elegibles, verificado sobre banco con niveles/edades variadas.
- **SC-009**: Con `Linear` estrategia e `InitialDifficulty=1`, la progresión `Round 1→1, 2→2, 3→3, 4→4, 5→5` ocurre en 100% de juegos con `MinRounds=5`; cambiar a otra estrategia configurada (`Progressive`, `Adaptive`, `CategorySpecific`) cambia la secuencia sin romper invariantes, verificado en 100% de juegos con cada estrategia registrada.

## Assumptions

- `SPEC-001` existe y define `GameConfiguration` (CategoryId, Min/MaxRounds, InitialDifficulty, DifficultyStrategy, TimeLimit 5–300, PointsPerRound, Min/MaxPlayers, políticas); este SPEC no redefine esa configuración, solo la exige como invariante `MinRounds≥5` y la usa para `Round.Difficulty/TimeLimit`.
- `SPEC-003` existe y provee `Question` con 4/1, `PUBLISHED`, `CategoryId`, `Difficulty`, `AcademicLevel`, `AgeRange`, y `IQuestionSelectionStrategy` con contrato 7 params (`CategoryId, Difficulty, AcademicLevel, AgeRange, PreviousQuestionIds, GameId, RoundNumber`); este SPEC la consume en `StartRound` y no la reimplementa.
- `SPEC-004` existe y define ciclo de vida 9 estados (`DRAFT→READY→WAITING_FOR_PLAYERS→IN_PROGRESS→ROUND_IN_PROGRESS↔ROUND_COMPLETED→FINISHED` + `CANCELLED`/`FORCED_FINISHED`) y transiciones `StartRound`, `CompleteRound`, `Finish`, con `rowversion` y `GameRound` como `Entity` composición; este SPEC extiende ese ciclo sin redefinirlo.
- `GameRound.TimeLimit` copia `GameConfiguration.TimeLimitPerQuestion` al momento de `StartRound`; no se recalcula si la configuración global cambiara (es inmutable tras `StartGame` de todos modos).
- `RoundNumber` es 1..`MaxRounds` sin huecos, `UNIQUE (GameId, RoundNumber)`; concurrencia `rowversion` en `Game` + constraint produce `409 Conflict` en `StartRound` doble.
- `Difficulty` 1..5 (Basic..Expert) clamp: si `Linear` pide `6` por `InitialDifficulty=5 + Round 2`, se mantiene en `5`; otras estrategias mapean dentro de 1..5.
- `PresentQuestion` para `PLAYER` filtra `IsCorrect`; `ADMIN/GAME_MANAGER` puede ver `IsCorrect` vía `GET /api/questions/{id}` existente (no es parte de este SPEC).
- `WaitForAnswers` y `EvaluateAnswers`/`CalculateScores` son sincrónicos en esta versión: `WaitForAnswers` es ventana `TimeLimit` donde llegan `SubmitAnswer` (SPEC-004 idempotencia), `EvaluateAnswers` compara `AnswerOption.IsCorrect` server-side, `CalculateScores` crea `PointTransaction` ledger (tipo `ANSWER_CORRECT`/`ANSWER_INCORRECT` + bonificaciones futuras).
- `IncreaseDifficulty` no es un endpoint separado; es el cálculo de `NextDifficulty` para la siguiente ronda al invocar `StartRound` siguiente; no se expone como `POST /api/games/{id}/rounds/{roundId}/increase-difficulty`.
- Si `SelectQuestion` no encuentra candidata (banco agotado o filtros muy restrictivos), `StartRound` retorna `NoAvailableQuestion` (409/400) y el juego puede `Finish` si `completedRounds ≥ MinRounds` o `ForcedFinish` si no.
- La aleatoriedad es server-side `Guid.NewGuid()` / `ORDER BY NEWID()` sin semilla cliente; no se expone `RandomSeed` ni correlación por `QuestionId` incremental.
- La estrategia por defecto si no se especifica es `Linear`.

## Dependencies

- `SPEC-001` — Game Configuration (CategoryId, Min/MaxRounds≥5, InitialDifficulty 1..5, DifficultyStrategy, TimeLimitPerQuestion 5–300, PointsPerRound, Min/MaxPlayers, políticas). Este SPEC exige `MinRounds≥5` y usa su `TimeLimit` para `Round.TimeLimit`.
- `SPEC-003` — Question Bank (Question PUBLISHED 4/1 con CategoryId, Difficulty, AcademicLevel, AgeRange, e `IQuestionSelectionStrategy` con 7 params + `ValidQuestionSpecification`/`QuestionSelectionSpecification`). Este SPEC consume `SelectQuestion` con exclusión de `PreviousQuestionIds`.
- `SPEC-004` — Game Lifecycle (9 estados, `Game` aggregate, `GameRound` como `Entity` composición, `StartRound`/`CompleteRound`/`Finish` con `rowversion`, `GameRound.Status` `ROUND_IN_PROGRESS`/`ROUND_COMPLETED`). Este SPEC extiende su motor de rondas con flujo 8 pasos y progresión.
- `BuildingBlocks.Kernel.Domain` — `AggregateRoot`, `Entity`, `ValueObject`, `StronglyTypedId`, `Enumeration`, `IBusinessRule`, `Result`, `IDomainEvent`, `IRepository<TAggregate,TId>`, `Specification<T>`.
- `BuildingBlocks.CQRS` — `ICommand`/`IQuery`, `ICommandHandler`/`IQueryHandler`, `ISender`, `IPipelineBehavior`, `IValidator`, `ValidationBehavior`.
- `BuildingBlocks.Kernel.Infrastructure` — `AppDbContextBase`, `EfRepository`, `SpecificationEvaluator`, `IUnitOfWork`, `IOutboxWriter`, `OutboxEntityTypeConfiguration`.
- `BuildingBlocks.ServiceDefaults` — `IEndpoint`, `Result → HTTP`, `GlobalExceptionHandler`, `ProblemDetails`, OpenTelemetry (`CorrelationId/GameId/RoundId/QuestionId`).

## Out of Scope

- Evaluación detallada de `AnswerOption` fuera de `IsCorrect` server-side (ej. lógica de parcialmente correctas) — solo 1 correcta por SPEC-003.
- Lógica de `PointsPerRound` exacta y bonificaciones `ROUND_BONUS`/`LEVEL_BONUS` más allá de crear `PointTransaction` con `Points` según `GameConfiguration.PointsPerRound` y `Difficulty` (cálculo completo en spec de scoring posterior, no en este motor de rondas).
- UI específica (Angular/Web) más allá de contratos `POST /api/games/{id}/rounds/start`, `GET /api/games/{id}/rounds/{roundId}`, `GET /api/games/{id}/rounds/{roundId}/question` (PLAYER sin `IsCorrect`), `POST /api/games/{id}/answers`.
- Selección `CategorySpecific` detallada (ej. dificultad ligada a categoría) más allá de filtrar por `CategoryId` ya especificado; se deja a estrategia configurada.
- Importación masiva de rondas o preguntas, o preguntas repetidas entre juegos distintos (permitidas).

## References

- Constitución v1.1.0 — Principios I-III (Domain First, Clean Arch, BuildingBlocks), V (Authoritative Server Truth), A (State Machine 9 estados), B (Question 4/1, Category ≥5), C (estrategia configurable), E/F (SqlServer `rowversion` + Oracle abstract, Specification), H (OroIdentityServer no local), I (Validation 3 niveles, ProblemDetails, OTel).
- `draft/constitution.md` §6 (Question Invariants), §5 (States), §8 (Game Configuration), §7 (Difficulty 5 levels).
- `draft/game-concept.md` §3-5 (Game/Round lifecycle, Round 1..5 ejemplo).
- `draft/libraries/buildingblocks.md` (Enumeration, ValueObject, Specification).
- SPEC-001 — Game Configuration (MinRounds≥5, TimeLimit 5–300, DifficultyStrategy).
- SPEC-003 — Question Bank (IQuestionSelectionStrategy 7 params, ValidQuestion).
- SPEC-004 — Game Lifecycle (Game Aggregate, GameRound Entity, 9 estados, StartRound/CompleteRound/Finish).

