# Feature Specification: Answer Evaluation

**Feature Branch**: `006-answer-evaluation`

**Created**: 2026-08-27

**Status**: Draft

**Input**: User description: "006 — Answer Evaluation Objetivo Definir cómo se recibe, valida y evalúa una respuesta. Flujo SubmitAnswer → ValidatePlayer → ValidateGame → ValidateRound → ValidateQuestion → ValidateTime → ValidateIdempotency → EvaluateAnswer → CalculateResult. Reglas El servidor determina: correct/incorrect, elapsed time, points, eligibility. El cliente nunca determina estos valores. Estados NOT_ANSWERED, ANSWERED, EVALUATED, EXPIRED. Casos CorrectAnswer, IncorrectAnswer, Timeout, DuplicateSubmission, InvalidAnswer, PlayerNotInGame, QuestionNotActive. Dependencias SPEC-005, SPEC-007, SPEC-008."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — SubmitAnswer con validación server-side completa (Priority: P1)

Como jugador, quiero enviar mi respuesta a una pregunta activa y que el servidor la valide, evalúe y calcule mi puntaje de forma autoritativa, sin que el cliente determine correct/incorrect, elapsed time, points ni eligibility.

**Why this priority**: Es el núcleo del juego interactivo. Sin evaluación server-side no hay juego justo ni integridad de datos. Entrega valor independiente como flujo completo de una respuesta.

**Independent Test**: Con `GameRound` en `ROUND_IN_PROGRESS` con `TimeLimit=30s`, `Player` en `IN_PROGRESS`, enviar `SubmitAnswer` con `AnswerOptionId` correcto → `EvaluateAnswer` retorna `correct=true`, `elapsedTime` server-calculado (`ServerTimestamp - Round.StartedAt`), `points` según `GameConfiguration.PointsPerRound` × `Difficulty`, `status=EVALUATED`. Verificar que el cliente nunca recibe valores calculados antes del envío.

**Acceptance Scenarios**:

1. **Given** `GameRound` en `ROUND_IN_PROGRESS` con `TimeLimit=30s` y `StartedAt=T0`, `Player` en `IN_PROGRESS` con `Game`, **When** `PLAYER` envía `SubmitAnswer` con `AnswerOptionId` correcto en `T0+10s`, **Then** el servidor retorna `status=EVALUATED`, `correct=true`, `elapsedTime=10`, `points=PointsPerRound × Difficulty`, y crea `Answer` con `Status=EVALUATED`.
2. **Given** mismo escenario pero con `AnswerOptionId` incorrecto, **When** se evalúa, **Then** `correct=false`, `points=0`, `status=EVALUATED`, sin penalización fuera de alcance.
3. **Given** `SubmitAnswer` enviado en `T0+31s` (fuera de `TimeLimit`), **When** llega, **Then** se rechaza con `AnswerTimeout` y `status=EXPIRED`, sin crear `Answer` EVALUATED.
4. **Given** segundo `SubmitAnswer` con mismo `PlayerId + RoundId` (idempotencia), **When** se reenvía, **Then** retorna el mismo resultado sin duplicar `Answer` ni `PointTransaction`.
5. **Given** `SubmitAnswer` con `AnswerOptionId` que no pertenece a la `Question` activa del round, **When** se valida, **Then** se rechaza con `InvalidAnswer`.
6. **Given** jugador no es `IN_PROGRESS` en el `Game`, **When** envía `SubmitAnswer`, **Then** se rechaza con `PlayerNotInGame`.
7. **Given** `GameRound` en `ROUND_COMPLETED` o `Status != ROUND_IN_PROGRESS`, **When** se envía `SubmitAnswer`, **Then** se rechaza con `QuestionNotActive`.

---

### User Story 2 — Answer states y lifecycle (Priority: P1)

Como sistema autoritativo, quiero que cada respuesta tenga un ciclo de vida claro con estados `NOT_ANSWERED → ANSWERED → EVALUATED / EXPIRED`, y que el servidor sea la única fuente de verdad para transiciones.

**Why this priority**: Garantiza integridad de datos y trazabilidad. Sin estados claros no hay auditoría ni resolución de conflictos.

**Independent Test**: Verificar que un `Answer` recién creado tiene `Status=NOT_ANSWERED`, tras `SubmitAnswer` exitoso transita a `ANSWERED` momentáneamente y luego a `EVALUATED` con resultado, que un timeout produce `EXPIRED`, y que el cliente no puede forzar transiciones.

**Acceptance Scenarios**:

1. **Given** `Answer` creado por `SubmitAnswer`, **When** el servidor procesa, **Then** transita `NOT_ANSWERED → ANSWERED → EVALUATED` en una transacción atómica (no expone `ANSWERED` al cliente, es interno).
2. **Given** `TimeLimit` expirado sin `SubmitAnswer`, **When** el servidor calcula, **Then** `Answer` queda en `EXPIRED` (o se crea `Answer` con `Status=EXPIRED` si se requiere registro).
3. **Given** `Answer` en `EVALUATED`, **When** se intenta mutar `correct` o `points` vía `Update`, **Then** se rechaza (inmutable tras evaluación).
4. **Given** `Answer` en `EXPIRED`, **When** se consulta, **Then** retorna `status=EXPIRED`, `correct=null`, `points=0`, `elapsedTime=TimeLimit` (timeout completo).

---

### User Story 3 — CalculateResult y PointTransaction (Priority: P1)

Como sistema, quiero que tras `EvaluateAnswer` se ejecute `CalculateResult` que cree `PointTransaction` ledger con `Points` según `GameConfiguration.PointsPerRound` y `Difficulty`, y que el resultado sea consultable.

**Why this priority**: Sin cálculo de puntos no hay `Score` ni `Leaderboard`. Es la segunda mitad del flujo EvaluateAnswer.

**Independent Test**: `SubmitAnswer` con respuesta correcta → `CalculateResult` crea `PointTransaction` con `Type=ANSWER_CORRECT`, `Points=PointsPerRound × DifficultyMultiplier`, `GameId`, `PlayerId`, `RoundId`, `QuestionId`. Respuesta incorrecta → `Type=ANSWER_INCORRECT`, `Points=0`. Verificar que `Score` del jugador se actualiza.

**Acceptance Scenarios**:

1. **Given** `EvaluateAnswer` retorna `correct=true`, **When** `CalculateResult` ejecuta, **Then** crea `PointTransaction` con `Type=ANSWER_CORRECT`, `Points=GameConfiguration.PointsPerRound × DifficultyMultiplier`, `GameId`, `PlayerId`, `RoundId`, `QuestionId`, `CreatedAt=UtcNow`.
2. **Given** `EvaluateAnswer` retorna `correct=false`, **When** `CalculateResult` ejecuta, **Then** crea `PointTransaction` con `Type=ANSWER_INCORRECT`, `Points=0`.
3. **Given** respuesta duplicada (idempotencia), **When** `CalculateResult` se invoca, **Then** retorna el `PointTransaction` existente sin duplicar.
4. **Given** `PointTransaction` creado, **When** se consulta `Score` del jugador, **Then** `Score = SUM(PointTransaction.Points)` para ese `GameId + PlayerId`.
5. **Given** `CalculateResult` falla parcialmente, **When** se produce, **Then** la transacción se revierte (no hay `PointTransaction` sin `Answer` EVALUATED).

---

### Edge Cases

- ¿Qué sucede cuando `SubmitAnswer` llega exactamente en `TimeLimit` con skew de reloj? El servidor usa `ServerTimestamp - Round.StartedAt`; ≤TimeLimit es válido, >TimeLimit es `Timeout`.
- ¿Qué sucede cuando `AnswerOptionId` no existe o fue eliminado después de `StartRound`? Rechazo `InvalidAnswer` (validación contra snapshot de la pregunta del round).
- ¿Qué sucede cuando el jugador ya respondió y envía otra respuesta para la misma pregunta? Idempotente: retorna mismo resultado, no duplica.
- ¿Qué sucede cuando `Game` cambia a `FINISHED` mientras `SubmitAnswer` está en tránsito? `ValidateGame` rechaza con `GameNotActive`.
- ¿Qué sucede cuando dos `SubmitAnswer` concurrentes del mismo jugador llegan simultáneamente? `ValidateIdempotency` con `rowversion` produce `409 Conflict` en el segundo.
- ¿Qué sucede cuando `PointsPerRound` es 0 en `GameConfiguration`? `CalculateResult` crea `PointTransaction` con `Points=0` (válido, sin error).
- ¿Qué sucede cuando `Difficulty` del round es 5 (Expert) y `PointsPerRound` tiene multiplicador? `Points = PointsPerRound × DifficultyMultiplier(5)` (clamp definido por configuración de scoring).
- ¿Qué sucede cuando `Player` se `Withdraw` del juego mientras procesa `SubmitAnswer`? `ValidatePlayer` rechaza con `PlayerNotInGame` (estado `WITHDRAWN`).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema MUST recibir `SubmitAnswer` con `AnswerOptionId` y calcular `ServerTimestamp` para `elapsedTime = ServerTimestamp - Round.StartedAt`; el cliente NO determina `elapsedTime`, `correct`, `points` ni `eligibility`.
- **FR-002**: El sistema MUST ejecutar la cadena de validación en orden: `ValidatePlayer` → `ValidateGame` → `ValidateRound` → `ValidateQuestion` → `ValidateTime` → `ValidateIdempotency` → `EvaluateAnswer` → `CalculateResult`; cada paso que falla retorna error específico sin ejecutar pasos posteriores.
- **FR-003**: El sistema MUST determinar `correct` server-side comparando `AnswerOptionId` enviado contra `Question.AnswerOptions.First(o => o.Id == answerOptionId).IsCorrect`; el cliente NUNCA envía `correct`.
- **FR-004**: El sistema MUST calcular `elapsedTime` como `min(ServerTimestamp - Round.StartedAt, Round.TimeLimit)`; si `elapsedTime > TimeLimit` la respuesta es `EXPIRED` y no `EVALUATED`.
- **FR-005**: El sistema MUST calcular `points` server-side: `correct == true → PointsPerRound × DifficultyMultiplier`, `correct == false → 0`; el cliente NUNCA envía `points`.
- **FR-006**: El sistema MUST crear `Answer` con `Status` transición atómica `NOT_ANSWERED → ANSWERED → EVALUATED` (o `EXPIRED`); `ANSWERED` es estado interno transaccional, no expuesto al cliente como estado final.
- **FR-007**: El sistema MUST implementar idempotencia por `PlayerId + RoundId` (o `PlayerId + QuestionId`): segundo `SubmitAnswer` con mismo identificador retorna el `Answer` existente sin duplicar `PointTransaction`.
- **FR-008**: El sistema MUST crear `PointTransaction` ledger en `CalculateResult` solo cuando `Answer.Status == EVALUATED`; `EXPIRED` no genera `PointTransaction`.
- **FR-009**: El sistema MUST validar `AnswerOptionId` pertenece a la `Question` activa del `GameRound` (snapshot `QuestionId` del round); `AnswerOptionId` inválido retorna `InvalidAnswer`.
- **FR-010**: El sistema MUST validar que el `Player` está en estado `IN_PROGRESS` en el `Game` (`GamePlayer.Status == IN_PROGRESS`); `WITHDRAWN` o ausente retorna `PlayerNotInGame`.
- **FR-011**: El sistema MUST validar que el `Game` está en `IN_PROGRESS` o `ROUND_IN_PROGRESS`; `FINISHED`/`CANCELLED`/`FORCED_FINISHED` retorna `GameNotActive`.
- **FR-012**: El sistema MUST validar que el `GameRound` está en `ROUND_IN_PROGRESS` y `Status != ROUND_COMPLETED`; ronda completada retorna `QuestionNotActive`.
- **FR-013**: El sistema MUST validar `ServerTimestamp - Round.StartedAt ≤ Round.TimeLimit`; fuera de ventana retorna `AnswerTimeout` con `status=EXPIRED`.
- **FR-014**: El sistema MUST modelar `Answer` como `Entity<AnswerId>` dentro del agregado `Game` (o como entidad separada con FK a `GameId + PlayerId + RoundId + QuestionId`) con `Status: Enumeration`, `Correct: bool?`, `Points: int`, `ElapsedTime: int`, `AnswerOptionId`, `CreatedAt`, `RowVersion`.
- **FR-015**: El sistema MUST exponer el resultado vía Vertical Slice CQRS (`ICommand`/`IQuery` + `Validator` + `Handler` + `Response DTO` + `IEndpoint`) con `ValidationBehavior` + `IBusinessRule`, y mapear `Error` a `ProblemDetails` (`400` validación, `404` not found, `409` conflicto idempotencia).
- **FR-016**: El sistema MUST auditar cada `SubmitAnswer` con `CorrelationId`, `GameId`, `RoundId`, `QuestionId`, `PlayerId`, `AnswerOptionId`, `Correct`, `Points`, `ElapsedTime`, `Status`, `FromStatus`, `ToStatus`, `Timestamp`, `Duration`.
- **FR-017**: El sistema MUST garantizar que `Answer` es inmutable tras `EVALUATED` o `EXPIRED`: no permite mutación de `Correct`, `Points`, `ElapsedTime`, `Status` vía `Update` directo.

### Key Entities *(include if feature involves data)*

- **Answer (Entity<AnswerId> — respuesta evaluada)** — Lifecycle de respuesta. Atributos: `AnswerId: StronglyTypedId<Guid>`, `GameId: GameId`, `PlayerId: Guid (sub)`, `RoundId: GameRoundId`, `QuestionId: QuestionId`, `AnswerOptionId: AnswerOptionId`, `Status: AnswerStatus (NOT_ANSWERED, ANSWERED, EVALUATED, EXPIRED)`, `Correct: bool?` (null si EXPIRED), `Points: int`, `ElapsedTime: int (seconds)`, `CreatedAt: DateTimeOffset`, `EvaluatedAt: DateTimeOffset?`, `RowVersion`. Comportamiento: creado vía `SubmitAnswer`, evaluado vía `EvaluateAnswer`, calculado vía `CalculateResult`, inmutable tras `EVALUATED/EXPIRED`. Relación: `Game 1—* Answer`, `GameRound 1—* Answer`, `Player 1—* Answer`, `Question 1—* Answer`.

- **PointTransaction (Entity<PointTransactionId> — ledger de puntos)** — Ledger append-only. Atributos: `PointTransactionId: StronglyTypedId<Guid>`, `GameId: GameId`, `PlayerId: Guid (sub)`, `RoundId: GameRoundId`, `QuestionId: QuestionId`, `AnswerId: AnswerId`, `Type: PointTransactionType (ANSWER_CORRECT, ANSWER_INCORRECT, ROUND_BONUS, LEVEL_BONUS)`, `Points: int`, `CreatedAt: DateTimeOffset`. Comportamiento: creado solo vía `CalculateResult` cuando `Answer.Status == EVALUATED`, append-only (no update/delete). Relación: `Game 1—* PointTransaction`, `Answer 1—1 PointTransaction`.

- **AnswerStatus (Enumeration)** — Estados: `NOT_ANSWERED(1)`, `ANSWERED(2)`, `EVALUATED(3)`, `EXPIRED(4)`. Transiciones: `NOT_ANSWERED → ANSWERED → EVALUATED` (submit exitoso), `NOT_ANSWERED → EXPIRED` (timeout), `ANSWERED → EVALUATED` (evaluación completada). `ANSWERED` es interno transaccional.

- **PointTransactionType (Enumeration)** — Tipos: `ANSWER_CORRECT(1)`, `ANSWER_INCORRECT(2)`, `ROUND_BONUS(3)`, `LEVEL_BONUS(4)`. Solo `ANSWER_CORRECT` e `ANSWER_INCORRECT` se usan en `CalculateResult` de este SPEC.

- **Game (AggregateRoot — extendido para Answer)** — Agregado raíz que contiene `Answers: IReadOnlyList<Answer>` y `PointTransactions: IReadOnlyList<PointTransaction>` como composition. Comportamiento nuevo: `SubmitAnswer(AnswerOptionId)` valida player/game/round/question/time/idempotency, evalúa, calcula, crea `Answer` + `PointTransaction`. `GetScore(PlayerId)` retorna `SUM(PointTransaction.Points)`.

- **GameRound (Entity — extendido para Answer)** — Agregado que contiene `Answers: IReadOnlyList<Answer>` como composition. Atributo nuevo: `StartedAt` se usa para cálculo de `ElapsedTime`.

- **GamePlayer (Entity — extendido para Answer)** — Atributo relevante: `Status: PlayerStatus (IN_PROGRESS, WITHDRAWN)` usado en `ValidatePlayer`.

## Success Criteria *(mandatory)*

### Measurable Outaways

- **SC-001**: 100% de `SubmitAnswer` con `AnswerOptionId` correcto son evaluados como `correct=true` server-side en <1s p95.
- **SC-002**: 100% de `SubmitAnswer` con `AnswerOptionId` incorrecto son evaluados como `correct=false` server-side en <1s p95.
- **SC-003**: 100% de `SubmitAnswer` fuera de `TimeLimit` retornan `AnswerTimeout` con `status=EXPIRED` en <1s p95.
- **SC-004**: 100% de `SubmitAnswer` duplicados (mismo `PlayerId + RoundId`) son idempotentes: retornan mismo resultado sin duplicar `PointTransaction`.
- **SC-005**: 100% de `SubmitAnswer` con `AnswerOptionId` inválido retornan `InvalidAnswer` sin crear `Answer` EVALUATED.
- **SC-006**: 0% de `Answer` puede ser mutado tras `EVALUATED` o `EXPIRED` (inmutabilidad verificada).
- **SC-007**: 100% de `CalculateResult` crea `PointTransaction` solo cuando `Answer.Status == EVALUATED`; `EXPIRED` no genera ledger.
- **SC-008**: El cliente NUNCA determina `correct`, `elapsedTime`, `points` ni `eligibility` (verificado por contract test que assert que el request no contiene estos campos y el response los calcula server-side).
- **SC-009**: 100% de `SubmitAnswer` fallidos (`PlayerNotInGame`, `GameNotActive`, `QuestionNotActive`, `AnswerTimeout`, `InvalidAnswer`, `DuplicateSubmission`) retornan error específico sin crear `Answer` EVALUATED.

## Assumptions

- `SPEC-005` existe y define `GameRound` con `TimeLimit`, `StartedAt`, `Status` (`ROUND_IN_PROGRESS/ROUND_COMPLETED`), `QuestionId`; este SPEC consume esos campos para `ValidateTime` y `ValidateRound`.
- `SPEC-007` (Score/Leaderboard) es dependiente: este SPEC crea `PointTransaction` que `SPEC-007` consumirá para `Score` y `Leaderboard`.
- `SPEC-008` (Player Management) es dependiente: este SPEC valida `GamePlayer.Status == IN_PROGRESS` vía `ValidatePlayer`.
- `GameConfiguration.PointsPerRound` se define en `SPEC-001` y se usa aquí como base para cálculo de `Points`.
- `GameConfiguration.TimeLimitPerQuestion` se copia a `GameRound.TimeLimit` en `SPEC-005`; este SPEC lo usa como ventana de tiempo.
- `AnswerOption.IsCorrect` se define en `SPEC-003` (Question Bank); este SPEC lo compara server-side sin confiar en el cliente.
- La idempotencia se logra vía `PlayerId + RoundId` (o `PlayerId + QuestionId`); `rowversion` en `Game` protege concurrencia.
- `ElapsedTime` se calcula como `min(ServerTimestamp - Round.StartedAt, Round.TimeLimit)`; no se expone al cliente antes del envío.
- `PointsPerRound` puede ser 0 (sin puntos); `CalculateResult` crea `PointTransaction` con `Points=0` sin error.
- `DifficultyMultiplier` es configurable (ej. 1.0 para Basic, 1.5 para Expert); se define en `GameConfiguration.ScoringSystem` (SPEC-001).
- `ANSWERED` es estado interno transaccional que no se expone al cliente; el cliente solo ve `NOT_ANSWERED`, `EVALUATED`, `EXPIRED`.

## Dependencies

- `SPEC-005` — Round Engine (`GameRound.TimeLimit`, `GameRound.StartedAt`, `GameRound.Status ROUND_IN_PROGRESS/ROUND_COMPLETED`, `GameRound.QuestionId`). Este SPEC consume esos campos para `ValidateTime`, `ValidateRound`, `ValidateQuestion`.
- `SPEC-007` — Score/Leaderboard (futuro). Este SPEC crea `PointTransaction` que `SPEC-007` consumirá para `Score` y `Leaderboard`.
- `SPEC-008` — Player Management (futuro). Este SPEC valida `GamePlayer.Status == IN_PROGRESS` vía `ValidatePlayer`.
- `SPEC-001` — Game Configuration (`PointsPerRound`, `TimeLimitPerQuestion`, `ScoringSystem` con `DifficultyMultiplier`).
- `SPEC-003` — Question Bank (`Question.AnswerOptions`, `AnswerOption.IsCorrect`, `AnswerOptionId`).
- `BuildingBlocks.Kernel.Domain` — `AggregateRoot`, `Entity`, `ValueObject`, `StronglyTypedId`, `Enumeration`, `IBusinessRule`, `Result`, `IDomainEvent`, `IRepository<TAggregate,TId>`, `Specification<T>`.
- `BuildingBlocks.CQRS` — `ICommand`/`IQuery`, `ICommandHandler`/`IQueryHandler`, `ISender`, `IPipelineBehavior`, `IValidator`, `ValidationBehavior`.
- `BuildingBlocks.Kernel.Infrastructure` — `AppDbContextBase`, `EfRepository`, `SpecificationEvaluator`, `IUnitOfWork`, `IOutboxWriter`, `OutboxEntityTypeConfiguration`.
- `BuildingBlocks.ServiceDefaults` — `IEndpoint`, `Result → HTTP`, `GlobalExceptionHandler`, `ProblemDetails`, OpenTelemetry (`CorrelationId/GameId/RoundId/PlayerId/AnswerOptionId`).

## Out of Scope

- Lógica de `ROUND_BONUS` / `LEVEL_BONUS` más allá de crear `PointTransaction` con tipo `ANSWER_CORRECT/ANSWER_INCORRECT` — bonificaciones futuras en `SPEC-007`.
- UI específica (Angular/Web) más allá de contratos `POST /api/games/{id}/answers`, `GET /api/games/{id}/answers/{answerId}`.
- Evaluación de respuestas parcialmente correctas — solo 1 correcta por `SPEC-003`.
- Sistema de anti-cheat avanzado (fingerprinting, behavior analysis) — fuera de alcance.
- Resolución de disputas o apelaciones — fuera de alcance.
- Notificaciones en tiempo real de resultados (SignalR push) — futuro `SPEC-007`.

## References

- Constitución v1.1.0 — Principios I-III (Domain First, Clean Arch, BuildingBlocks), V (Authoritative Server Truth), A (State Machine), E/F (SqlServer `rowversion` + Oracle abstract, Specification), I (Validation 3 niveles, ProblemDetails, OTel).
- `draft/constitution.md` §5 (States), §8 (Game Configuration), §6 (Question Invariants).
- `draft/game-concept.md` §3-5 (Game/Round lifecycle, scoring).
- `draft/libraries/buildingblocks.md` (Enumeration, ValueObject, Specification).
- SPEC-001 — Game Configuration (PointsPerRound, TimeLimit, ScoringSystem).
- SPEC-003 — Question Bank (AnswerOptions, IsCorrect).
- SPEC-005 — Round Engine (GameRound TimeLimit/StartedAt/Status/QuestionId).
