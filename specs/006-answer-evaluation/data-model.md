# Data Model: Answer Evaluation

**Feature**: `006-answer-evaluation` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Entities

### Answer (Entity<AnswerId> — composition within Game)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `AnswerId : StronglyTypedId<Guid>` | PK, immutable, `New()` | BuildingBlocks |
| `GameId` | `GameId` | FK a Game, cascade delete, `HasConversion` | composition |
| `PlayerId` | `Guid` (sub) | FK lógico a OroIdentityServer `sub`, NOT NULL | VI — external identity |
| `RoundId` | `GameRoundId` | FK a GameRound, `HasConversion` | composition |
| `QuestionId` | `QuestionId` | FK lógico a Question (snapshot del round), `HasConversion` | SPEC-003 |
| `AnswerOptionId` | `AnswerOptionId` | FK lógico a AnswerOption, `HasConversion` | respuesta del jugador |
| `Status` | `AnswerStatus : Enumeration` | 4 valores, `HasConversion(s=>s.Id)` | NOT_ANSWERED(1), ANSWERED(2), EVALUATED(3), EXPIRED(4) |
| `Correct` | `bool?` | nullable — `null` si `EXPIRED`, `true/false` si `EVALUATED` | server-side only |
| `Points` | `int` | `≥0`, default `0` | server-side: `PointsPerRound × DifficultyMultiplier` si correct, `0` si incorrect |
| `ElapsedTime` | `int` | `≥0`, seconds | `min(ServerTimestamp - StartedAt, TimeLimit)` |
| `CreatedAt` | `DateTimeOffset` | set en construcción | audit |
| `EvaluatedAt` | `DateTimeOffset?` | set en `Evaluate()` o `Expire()` | audit |
| `RowVersion` | `byte[]` | `rowversion` en Answer (concurrency token) | E/F |

**Behavior**: Creado vía `Game.SubmitAnswer()` con `Status=NOT_ANSWERED`, transita internamente a `ANSWERED` (momentáneo, no expuesto) y luego a `EVALUATED` con resultado o `EXPIRED` por timeout. No expone setters públicos. Inmutable tras `EVALUATED`/`EXPIRED` (no mutación de `Correct`, `Points`, `ElapsedTime`, `Status`).

**Internal methods**:
- `Submit()` — `NOT_ANSWERED → ANSWERED` (transición interna transaccional)
- `Evaluate(bool correct, int points, int elapsedTime)` — `ANSWERED → EVALUATED`, set `Correct`, `Points`, `ElapsedTime`, `EvaluatedAt`
- `Expire(int timeLimit)` — `NOT_ANSWERED → EXPIRED`, set `Correct=null`, `Points=0`, `ElapsedTime=timeLimit`, `EvaluatedAt=UtcNow`

**Invariantes**:
- `UNIQUE (GameId, PlayerId, RoundId)` — un jugador solo una respuesta por ronda (idempotencia)
- `Status` transita solo vía métodos internos: `NOT_ANSWERED→ANSWERED→EVALUATED` o `NOT_ANSWERED→EXPIRED`
- `Correct` es `null` si y solo si `Status == EXPIRED`
- `Points ≥ 0` siempre
- `ElapsedTime ≥ 0` siempre
- Inmutable tras `EVALUATED`/`EXPIRED`

### PointTransaction (Entity<PointTransactionId> — ledger append-only, composition within Game)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `PointTransactionId : StronglyTypedId<Guid>` | PK, immutable, `New()` | BuildingBlocks |
| `GameId` | `GameId` | FK a Game, cascade delete, `HasConversion` | composition |
| `PlayerId` | `Guid` (sub) | FK lógico a OroIdentityServer `sub`, NOT NULL | VI |
| `RoundId` | `GameRoundId` | FK a GameRound, `HasConversion` | composition |
| `QuestionId` | `QuestionId` | FK lógico a Question, `HasComposition` | snapshot |
| `AnswerId` | `AnswerId` | FK a Answer, `HasConversion`, `UNIQUE (GameId, AnswerId)` | 1:1 con Answer EVALUATED |
| `Type` | `PointTransactionType : Enumeration` | 4 valores, `HasConversion(t=>t.Id)` | ANSWER_CORRECT(1), ANSWER_INCORRECT(2), ROUND_BONUS(3), LEVEL_BONUS(4) |
| `Points` | `int` | `≥0` | `PointsPerRound × DifficultyMultiplier` si correct, `0` si incorrect |
| `CreatedAt` | `DateTimeOffset` | set en construcción | audit |

**Behavior**: Creado solo vía `Game.CalculateResult()` cuando `Answer.Status == EVALUATED`. Append-only (no update/delete). `EXPIRED` no genera `PointTransaction`.

**Invariantes**:
- `UNIQUE (GameId, AnswerId)` — un `PointTransaction` por `Answer` (evita duplicación)
- Solo se crea cuando `Answer.Status == EVALUATED`
- `Points ≥ 0` siempre
- Append-only: no mutación tras creación

### Game (AggregateRoot — extendido para Answer)

| Field | Type | Changes | Notes |
|-------|------|---------|-------|
| `Answers` | `IReadOnlyList<Answer>` | NUEVO — composition, `HasMany` backing field `_answers` | 0..MaxRounds×MaxPlayers |
| `PointTransactions` | `IReadOnlyList<PointTransaction>` | NUEVO — composition, `HasMany` backing field `_pointTransactions` | append-only ledger |

**Behavior nuevo**:
- `SubmitAnswer(Guid answerOptionId)` — valida 7 pasos, crea `Answer` + `PointTransaction`, retorna `Result<(Answer, PointTransaction)>`
- `GetScore(Guid playerId)` — retorna `SUM(PointTransaction.Points)` para ese `PlayerId`
- `GetAnswer(Guid playerId, GameRoundId roundId)` — retorna `Answer` existente para idempotencia

## Enumerations

### AnswerStatus (Enumeration)

| Id | Name | Description |
|----|------|-------------|
| 1 | `NotAnswered` | Estado inicial; respuesta no enviada aún |
| 2 | `Answered` | Transición interna transaccional; no expuesto al cliente |
| 3 | `Evaluated` | Respuesta evaluada server-side; `Correct`/`Points`/`ElapsedTime` fijados |
| 4 | `Expired` | Timeout; `Correct=null`, `Points=0`, `ElapsedTime=TimeLimit` |

**Transiciones válidas**:
- `NotAnswered → Answered` (Submit interno)
- `Answered → Evaluated` (Evaluate interno)
- `NotAnswered → Expired` (timeout)

**Propiedades**:
- `IsTerminal => Status == Evaluated || Status == Expired`
- `IsInternal => Status == Answered` (no expuesto al cliente)

### PointTransactionType (Enumeration)

| Id | Name | Description |
|----|------|-------------|
| 1 | `AnswerCorrect` | Respuesta correcta; `Points = PointsPerRound × DifficultyMultiplier` |
| 2 | `AnswerIncorrect` | Respuesta incorrecta; `Points = 0` |
| 3 | `RoundBonus` | Bonus de ronda (futuro SPEC-007) |
| 4 | `LevelBonus` | Bonus de nivel (futuro SPEC-007) |

**Uso en este SPEC**: Solo `AnswerCorrect` e `AnswerIncorrect` se crean en `CalculateResult`.

## ValueObjects

### AnswerResult (ValueObject — retorno de EvaluateAnswer)

| Field | Type | Description |
|-------|------|-------------|
| `AnswerId` | `AnswerId` | ID de la respuesta creada |
| `Correct` | `bool` | `true` si `AnswerOptionId` coincide con `IsCorrect` |
| `Points` | `int` | `PointsPerRound × DifficultyMultiplier` si correct, `0` si incorrect |
| `ElapsedTime` | `int` | `min(ServerTimestamp - StartedAt, TimeLimit)` segundos |
| `Status` | `AnswerStatus` | `Evaluated` o `Expired` |

**GetEqualityComponents**: `AnswerId`, `Correct`, `Points`, `ElapsedTime`, `Status`

## Business Rules (IBusinessRule)

### ValidatePlayerRule

- **Check**: `GamePlayer.Status == PlayerStatus.InProgress`
- **Error**: `GameErrors.PlayerNotInGame` → `Error.Validation("PlayerNotInGame", "Player is not in progress in this game.")`
- **Usage**: Primer paso en `Game.SubmitAnswer()`

### ValidateGameRule

- **Check**: `Game.Status == GameStatus.InProgress || Game.Status == GameStatus.RoundInProgress`
- **Error**: `GameErrors.GameNotActive` → `Error.Validation("GameNotActive", "Game is not in active state.")`
- **Usage**: Segundo paso en `Game.SubmitAnswer()`

### ValidateRoundRule

- **Check**: `CurrentRound != null && CurrentRound.Status == GameStatus.RoundInProgress`
- **Error**: `GameErrors.QuestionNotActive` → `Error.Validation("QuestionNotActive", "Round is not in progress.")`
- **Usage**: Tercer paso en `Game.SubmitAnswer()`

### ValidateQuestionRule

- **Check**: `Question.AnswerOptions.Any(o => o.Id == answerOptionId)` — validado contra `QuestionRepository` con snapshot del round
- **Error**: `GameErrors.InvalidAnswer` → `Error.Validation("InvalidAnswer", "Answer option does not belong to the active question.")`
- **Usage**: Cuarto paso en `Game.SubmitAnswer()`; requiere `IRepository<Question,QuestionId>` inyectado en handler

### ValidateTimeRule

- **Check**: `serverTimestamp - round.StartedAt <= round.TimeLimit`
- **Error**: `AnswerErrors.AnswerTimeout` → `Error.Validation("AnswerTimeout", "Answer submitted after time limit.")`
- **Usage**: Quinto paso en `Game.SubmitAnswer()`; `serverTimestamp` calculado en handler

### ValidateIdempotencyRule

- **Check**: `!Answers.Any(a => a.PlayerId == playerId && a.RoundId == roundId)` — ya existe respuesta para este jugador en esta ronda
- **Error**:返回 respuesta existente (idempotente), no error
- **Usage**: Sexto paso en `Game.SubmitAnswer()`; si existe, retorna `Answer` existente sin crear nueva

### AnswerImmutabilityRule

- **Check**: `Status != AnswerStatus.Evaluated && Status != AnswerStatus.Expired`
- **Error**: `AnswerErrors.AnswerImmutable` → `Error.Validation("AnswerImmutable", "Answer cannot be modified after evaluation.")`
- **Usage**: Protege contra mutación directa vía `Update` en `Answer`

## Relationships

```
Game (1) ──HasMany(composition, cascade, field _answers)──> Answer (0..*) 
  // FK GameId, UNIQUE (GameId,PlayerId,RoundId), HasConversion QuestionId/AnswerOptionId

Game (1) ──HasMany(composition, cascade, field _pointTransactions)──> PointTransaction (0..*) 
  // FK GameId, UNIQUE (GameId,AnswerId), append-only

Game (1) ──HasMany(composition)──> GameRound (1..*) 
  // FK GameId, UNIQUE (GameId,RoundNumber) — existente 005

Game (1) ──HasMany(composition)──> GamePlayer (0..*) 
  // FK GameId, UNIQUE (GameId,UserId) — existente 004

Answer (1) ── HasOne (logical FK) ──> GameRound (1) via RoundId
Answer (1) ── HasOne (logical FK) ──> Question (1) via QuestionId (snapshot del round)
Answer (1) ── HasOne (logical FK) ──> AnswerOption (1) via AnswerOptionId
Answer (1) ── 1:1 ──> PointTransaction via AnswerId (UNIQUE)

PointTransaction (1) ── HasOne (logical FK) ──> Answer (1) via AnswerId
```

## Persistence Mapping (EF Core)

### AnswerTypeConfiguration : IEntityTypeConfiguration<Answer>

```csharp
HasKey(a => a.Id); // AnswerId converter
Property(a => a.GameId).HasConversion(id => id.Value, v => new GameId(v)).IsRequired();
Property(a => a.PlayerId).IsRequired();
Property(a => a.RoundId).HasConversion(id => id.Value, v => new GameRoundId(v)).IsRequired();
Property(a => a.QuestionId).HasConversion(id => id.Value, v => new QuestionId(v)).IsRequired();
Property(a => a.AnswerOptionId).HasConversion(id => id.Value, v => new AnswerOptionId(v)).IsRequired();
Property(a => a.Status).HasConversion(s => s.Id, id => AnswerStatus.FromId(id)).HasColumnName("StatusId").IsRequired();
Property(a => a.Correct).IsRequired(false); // nullable
Property(a => a.Points).IsRequired();
Property(a => a.ElapsedTime).IsRequired();
Property(a => a.CreatedAt).IsRequired();
Property(a => a.EvaluatedAt).IsRequired(false);
Property(a => a.RowVersion).IsRowVersion().IsConcurrencyToken().IsRequired();
HasIndex(a => new { a.GameId, a.PlayerId, a.RoundId }).IsUnique(); // idempotencia
HasIndex(a => new { a.GameId, a.RoundId }); // queries por ronda
HasIndex(a => new { a.GameId, a.PlayerId }); // queries por jugador
HasIndex(a => a.Status); // filtro por estado
ToTable("Answers");
```

### PointTransactionTypeConfiguration : IEntityTypeConfiguration<PointTransaction>

```csharp
HasKey(pt => pt.Id); // PointTransactionId converter
Property(pt => pt.GameId).HasConversion(id => id.Value, v => new GameId(v)).IsRequired();
Property(pt => pt.PlayerId).IsRequired();
Property(pt => pt.RoundId).HasConversion(id => id.Value, v => new GameRoundId(v)).IsRequired();
Property(pt => pt.QuestionId).HasConversion(id => id.Value, v => new QuestionId(v)).IsRequired();
Property(pt => pt.AnswerId).HasConversion(id => id.Value, v => new AnswerId(v)).IsRequired();
Property(pt => pt.Type).HasConversion(t => t.Id, id => PointTransactionType.FromId(id)).HasColumnName("TypeId").IsRequired();
Property(pt => pt.Points).IsRequired();
Property(pt => pt.CreatedAt).IsRequired();
HasIndex(pt => new { pt.GameId, pt.AnswerId }).IsUnique(); // 1:1 con Answer
HasIndex(pt => new { pt.GameId, a.PlayerId }); // Score query
HasIndex(pt => new { pt.GameId, pt.RoundId }); // Round score
ToTable("PointTransactions");
```

### GameTypeConfiguration — Extensiones

```csharp
// Agregar a GameTypeConfiguration existente:
HasMany(g => g.Answers).WithOne().HasForeignKey("GameId").OnDelete(DeleteBehavior.Cascade)
    .Navigation("Answers").HasField("_answers").UsePropertyAccessMode(Field);
HasMany(g => g.PointTransactions).WithOne().HasForeignKey("GameId").OnDelete(DeleteBehavior.Cascade)
    .Navigation("PointTransactions").HasField("_pointTransactions").UsePropertyAccessMode(Field);
```

## Validation Rules Summary

| Rule | Field(s) | Valid | Invalid | Error Code | HTTP |
|------|----------|-------|---------|------------|------|
| PlayerInGame | PlayerId + Game | `GamePlayer.Status == IN_PROGRESS` | WITHDRAWN/absent | `PlayerNotInGame` | 400 |
| GameActive | Game.Status | IN_PROGRESS/ROUND_IN_PROGRESS | FINISHED/CANCELLED/FORCED_FINISHED | `GameNotActive` | 400 |
| RoundActive | CurrentRound.Status | ROUND_IN_PROGRESS | null/ROUND_COMPLETED | `QuestionNotActive` | 400 |
| ValidAnswerOption | AnswerOptionId ∈ Question.AnswerOptions | belongs to question | doesn't belong | `InvalidAnswer` | 400 |
| TimeLimit | ServerTimestamp - StartedAt | ≤ TimeLimit | > TimeLimit | `AnswerTimeout` | 408 |
| Idempotency | PlayerId + RoundId | no duplicate Answer | duplicate exists | return existing | 200 (idempotent) |
| Concurrency | RowVersion | matches DB | stale | `ConcurrencyConflict` | 409 |
| AnswerImmutability | Answer.Status | NOT_ANSWERED/ANSWERED | EVALUATED/EXPIRED | `AnswerImmutable` | 400 |

## State Transitions

```
Answer Status (4 estados):
[NOT_ANSWERED] --Submit()--> [ANSWERED] --Evaluate(correct, points, elapsed)--> [EVALUATED]
[NOT_ANSWERED] --Expire(timeLimit)--> [EXPIRED]

ANSWERED es interno transaccional: se crea y evalúa en la misma transacción atómica.
El cliente solo ve: NOT_ANSWERED, EVALUATED, EXPIRED.

PointTransaction: append-only, sin transiciones de estado.
```

## Domain Events

- `AnswerSubmittedDomainEvent(GameId, AnswerId, PlayerId, RoundId, QuestionId, AnswerOptionId)` — tras creación de Answer (in-process)
- `AnswerEvaluatedDomainEvent(GameId, AnswerId, PlayerId, RoundId, Correct, Points, ElapsedTime, Status)` — tras evaluación (in-process)
- `AnswerExpiredDomainEvent(GameId, PlayerId, RoundId, QuestionId)` — tras timeout (in-process)
- `PointTransactionCreatedDomainEvent(GameId, PointTransactionId, PlayerId, AnswerId, Type, Points)` — tras creación de ledger (in-process)
- Todos dispatch en `AppDbContextBase.SaveChanges`; opcional `AnswerEvaluatedIntegrationEvent` vía `IOutboxWriter`→RabbitMQ (topic `answer.evaluated`)

## Specifications

- `GameByIdWithAnswersSpecification(GameId)` — `Where(g => g.Id == gameId)` + `Include(Rounds)` + `Include(Players)` + `Include(Answers)` + `Include(PointTransactions)` + `AsNoTracking` — rehidratación completa para `SubmitAnswer`
- `AnswerByIdSpecification(GameId, AnswerId)` — `Where(a => a.GameId == gameId && a.Id == answerId)` + `AsNoTracking` — query `GetAnswer`
- `AnswersByGameAndPlayerSpecification(GameId, PlayerId)` — `Where(a => a.GameId == gameId && a.PlayerId == playerId)` + `OrderBy(RoundId)` + `AsNoTracking` — historial de respuestas
- `PointTransactionsByGameSpecification(GameId)` — `Where(pt => pt.GameId == gameId)` + `AsNoTracking` — Score total

## Audit & Observability

- `AnswerEvaluationAudit` tabla `AuditLog` con `CorrelationId`, `GameId`, `RoundId`, `QuestionId`, `PlayerId`, `AnswerOptionId`, `Correct`, `Points`, `ElapsedTime`, `Status`, `FromStatus`, `ToStatus`, `Timestamp`, `Duration` (append-only).
- OTel: logs `CorrelationId`, `GameId`, `RoundId`, `PlayerId`, `AnswerOptionId`, `Command (SubmitAnswer)`, `Duration`, `Result`, `Correct`, `Points`; metrics `answer_submissions_total` por `Status/Correct`, `answer_evaluation_duration_seconds` histogram (p95 <1s).
