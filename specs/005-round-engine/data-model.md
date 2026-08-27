# Data Model: Round Engine

**Feature**: `005-round-engine` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Entities

### Game (AggregateRoot<GameId> — extended from 001/004)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `GameId : StronglyTypedId<Guid>` | PK, immutable | BuildingBlocks |
| `Name` | `string` | 3–100, trim | SPEC-001 |
| `Configuration` | `GameConfiguration : ValueObject` | Inmutable tras `IsStarted` (≥WAITING_FOR_PLAYERS) | SPEC-001, FR-001 |
| `Status` | `GameStatus : Enumeration` | 9 valores `DRAFT(1), READY(2), WAITING_FOR_PLAYERS(3), IN_PROGRESS(4), ROUND_IN_PROGRESS(5), ROUND_COMPLETED(6), FINISHED(7), CANCELLED(8), FORCED_FINISHED(9)` | 004 |
| `Rounds` | `IReadOnlyList<GameRound>` | 0..MaxRounds, composition, `RoundNumber` único sin huecos | FR-002 |
| `Players` | `IReadOnlyList<GamePlayer>` | 0..MaxPlayers, composition | 004 |
| `RowVersion` | `byte[]` | `rowversion` / `IsConcurrencyToken()` | E/F |
| `CreatedAt` | `DateTimeOffset` | set en `Create` | audit |
| `ReadyAt` | `DateTimeOffset?` | set en `MarkReady` | 004 |
| `StartedAt` | `DateTimeOffset?` | set en `Start` | 004 |
| `FinishedAt` | `DateTimeOffset?` | set en `Finish/Cancel/ForceFinish` | 004 |
| `CreatedBy` | `Guid` (sub) | FK lógico a OroIdentityServer `sub` | VI |
| Domain Events | 9 eventos (GameCreated, Ready, PlayerJoined, Started, RoundStarted, RoundCompleted, Finished, Cancelled, ForcedFinished) | dispatch en `SaveChanges` | BuildingBlocks |

**Behavior (relevant to Round Engine)**: `Result<GameRound> StartRound(IQuestionSelectionStrategy selector, IDifficultyProgressionStrategy difficultyStrategy)` (validates `Status==IN_PROGRESS` o `ROUND_COMPLETED` + no `CurrentRound` activo + `Rounds.Count < MaxRounds`, calcula `Difficulty = difficultyStrategy.NextDifficulty(this, completed)`, `TimeLimit = Configuration.TimeLimitPerQuestion`, `QuestionId` via `selector.SelectAsync(criteria)` con `PreviousQuestionIds=Rounds.Select(r=>r.QuestionId)`, crea `GameRound`, set `Status=ROUND_IN_PROGRESS`, emit `RoundStartedDomainEvent`); `Result CompleteRound(GameRoundId)` (`ROUND_IN_PROGRESS→ROUND_COMPLETED`); `Result Finish()` gate `completedRounds≥MinRounds` (FR-001); `Result UpdateConfiguration` solo `DRAFT/READY`. Invariants: `MinRounds≥5` (FR-001), `RoundNumber` único, `Difficulty` 1..5, `TimeLimit` 5–300, `Status` transiciones `IsValidTransition`.

### GameRound (Entity<GameRoundId> dentro de Game)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `GameRoundId : StronglyTypedId<Guid>` | PK dentro agregado, `New()` | BuildingBlocks |
| `GameId` | `GameId` | FK a Game, cascade delete, `HasConversion` | composition |
| `RoundNumber` | `int` | 1..MaxRounds, `UNIQUE (GameId,RoundNumber)`, incremental `Rounds.Count+1` sin huecos | FR-002 |
| `Difficulty` | `DifficultyLevel : Enumeration 1..5` | 1..5, via `HasConversion(d=>d.Id)` | FR-002, FR-009 |
| `QuestionId` | `QuestionId : StronglyTypedId<Guid>` | FK lógico a `Question` PUBLISHED, no repetida dentro Game (`UNIQUE (GameId,QuestionId)` opcional + `PreviousQuestionIds` check), `HasConversion` | FR-002, FR-005, SPEC-003 |
| `TimeLimit` | `int` | 5–300, copiado de `Game.Configuration.TimeLimitPerQuestion`, `IsRequired()` | FR-002 |
| `Status` | `GameStatus` (5 `ROUND_IN_PROGRESS`, 6 `ROUND_COMPLETED`) | `Enumeration` 5/6, `HasConversion(s=>s.Id)` | FR-002 |
| `StartedAt` | `DateTimeOffset` | set en `StartRound` | audit |
| `CompletedAt` | `DateTimeOffset?` | set en `CompleteRound` | audit |

**Behavior**: No expone setters públicos; creado solo vía `Game.StartRound` (internal ctor `GameRound(GameRoundId, GameId, RoundNumber, Difficulty, QuestionId, TimeLimit)`), completado vía `Game.CompleteRound` (internal `Complete()` sets `Status=ROUND_COMPLETED`, `CompletedAt=UtcNow`). `RowVersion` no en `GameRound` (agregado raíz `Game.RowVersion` protege todo).

**Invariantes**:
- 5 campos no nulos, `RoundNumber` único por `GameId` (DB `UNIQUE`), sin huecos (`Rounds.Count+1` lógica + DB constraint evita huecos por concurrencia → `409`).
- `Difficulty` 1..5 clamp (FR-009), `TimeLimit` 5–300 (copiado, no recalculado), `QuestionId` PUBLISHED + 4/1 + no repetida (FR-005 + SPEC-003).
- `Status` transita `ROUND_IN_PROGRESS→ROUND_COMPLETED` solo vía `CompleteRound` (no directo `Update`).
- Inmutable `Difficulty/QuestionId/TimeLimit` tras creación (solo `CompletedAt` muta).

### Question (referencia externa, SPEC-003 — no se modela aquí salvo FK lógico)

| Field | Type | Constraints |
|-------|------|-------------|
| `Id` | `QuestionId` | FK lógico, `Status==PUBLISHED` + `AnswerOptions.Count==4` + `ExactlyOneCorrect` + `CategoryId==Game.CategoryId` + `Difficulty==Round.Difficulty` + `AcademicLevel` case-insensitive `==` + `AgeRange` overlap + `!Previous.Contains(Id)` |
| `Difficulty` | `DifficultyLevel 1..5` | Must match `GameRound.Difficulty` |
| `AcademicLevel` | `ValueObject` string 2..100 | Compatible if `Value.ToLower()==Question.Value.ToLower()` |
| `AgeRange` | `ValueObject` min/max 0..120 | Compatible if `Max ≥ other.Min && Min ≤ other.Max` (overlap) |
| `CategoryId` | `CategoryId` | Must match `Game.Configuration.CategoryId` |

**Validez para selección**: `Status==PUBLISHED` + 4/1 + todos los filtros + `!Previous`. Conteo para `NoAvailableQuestion` si 0 candidatas.

### IDifficultyProgressionStrategy (Strategy, configurable)

| Method | Signature | Notes |
|--------|-----------|-------|
| `NextDifficulty` | `DifficultyLevel NextDifficulty(Game game, int completedRounds)` | `completedRounds = game.Rounds.Count(r=>r.Status==ROUND_COMPLETED)`, `clamp(1..5)` |

**Implementaciones**:
- `LinearDifficultyStrategy` : `Next = clamp(InitialDifficulty + completedRounds, 1,5)` → 1,2,3,4,5 para Initial=1, Min=5 (FR-010).
- `ProgressiveDifficultyStrategy` : curva 1,1,2,3,5 (ejemplo spec) o 1,2,2,3,4.
- `AdaptiveDifficultyStrategy` : basada en `PointTransaction` avg (si >80% sube 2, <30% baja 1, clamp).
- `CategorySpecificDifficultyStrategy` : mapea `CategoryId` a curva específica.

Registro: `AddScoped<IDifficultyProgressionStrategy, LinearDifficultyStrategy>` default, configurable via `appsettings.json` `Game:DifficultyStrategy` o `DifficultyProgressionStrategy` enum; al menos `Linear` + 2 registradas.

### IQuestionSelectionStrategy (referencia externa, SPEC-003 — ya existe)

| Method | Signature | Notes |
|--------|-----------|-------|
| `SelectAsync` | `Task<Result<IReadOnlyList<Question>>> SelectAsync(QuestionSelectionCriteria criteria, CancellationToken)` | `criteria` con `CategoryId, Difficulty (del round), AcademicLevel, AgeRange, PreviousQuestionIds (Game), GameId, RoundNumber, Take=1`, `ORDER BY NEWID()` + `AsNoTracking` + `Take(1)` |

**Uso en Round Engine**: `Game.StartRound(selector)` construye `criteria` con `CategoryId=Configuration.CategoryId`, `Difficulty=NextDifficulty`, `AcademicLevel=Category.AcademicLevel.Value`, `AgeRange=Category.AgeRange`, `Previous=Rounds.Select(r=>r.QuestionId)`, `GameId`, `RoundNumber`.

## ValueObjects / Enumerations

- **GameStatus** (9): `DRAFT(1), READY(2), WAITING_FOR_PLAYERS(3), IN_PROGRESS(4), ROUND_IN_PROGRESS(5), ROUND_COMPLETED(6), FINISHED(7), CANCELLED(8), FORCED_FINISHED(9)` — `Enumeration<GameStatus>`, `IsTerminal (7,8,9)`, `IsStarted (≥3)`, `IsRoundActive (5)`, `CanTransitionTo(to)`, `IsValidTransition(from,to)`.
- **GameRoundStatus** (reusa `GameStatus` 5/6): `ROUND_IN_PROGRESS(5), ROUND_COMPLETED(6)` — persistido como `int` via `HasConversion`.
- **DifficultyLevel** (1..5): `Enumeration<DifficultyLevel>` (Basic 1, Elementary 2, Intermediate 3, Advanced 4, Expert 5) — `FromId(clamp)`.
- **AgeRange** (0..120): `ValueObject` con `Min, Max, IsCompatible(other) => Max ≥ other.Min && Min ≤ other.Max`, `Contains(age)`, `GetEqualityComponents` Min/Max.
- **GameConfiguration** (VO): `CategoryId, MinRounds≥5, MaxRounds, InitialDifficulty 1..5, DifficultyStrategy, TimeLimit 5–300, PointsPerRound, MinPlayers≥1, MaxPlayers, Withdrawal/Loss/Consolation Policies, ScoringSystem, RewardRules` — `GetEqualityComponents` todos los campos; `OWNED` en `GameTypeConfiguration`.
- **QuestionSelectionCriteria** (VO, SPEC-003): `CategoryId?, Difficulty?, AcademicLevel?, AgeRange?, PreviousQuestionIds, GameId, RoundNumber, Take` — igualdad por `Previous.OrderBy` determinística.

## Business Rules (IBusinessRule)

- `MinimumRoundsRule(int min)` — `min <5` → `InvalidGameConfiguration.MinRoundsTooLow` (FR-001).
- `RoundNumberUniqueRule(GameId, int roundNumber, IReadOnlyList<GameRound>)` — `Rounds.Any(r=>r.RoundNumber==roundNumber)` → `DuplicateRoundNumber` (raro, pero `UNIQUE` DB protege).
- `PreviousQuestionNotRepeatedRule(QuestionId, IReadOnlyList<QuestionId> previous)` — `previous.Contains(questionId)` → `DuplicateQuestion` (pero `SelectAsync` ya excluye; guarda extra si alguien fuerza `QuestionId` manualmente).
- `RoundAlreadyInProgressRule(GameStatus status)` — `status==ROUND_IN_PROGRESS` || `CurrentRound != null` → `RoundAlreadyInProgress` (FR-005 guard para `StartRound`).
- `CategoryMustMatchRule(Question.CategoryId, Game.CategoryId)` — `!=` → `CategoryMismatch` (pero `SelectAsync` ya filtra; guard extra).
- `DifficultyMustMatchRule(Question.Difficulty, Round.Difficulty)` — `!=` → `DifficultyMismatch`.
- `AcademicLevelCompatibleRule(Question.AcademicLevel, Game.AcademicLevel)` — `Value.ToLower()!=` → `AcademicLevelMismatch`.
- `AgeRangeCompatibleRule(Question.AgeRange, Game.AgeRange)` — `!IsCompatible` → `AgeRangeMismatch`.
- `DifficultyClampRule(int difficulty)` — `difficulty <1 || >5` → clamp, pero si se fuerza fuera de rango → `InvalidDifficulty`.
- `NotEnoughRoundsRule(int completed, int min)` — `completed < min` → `NotEnoughRounds` / `InvalidGameState` para `Finish` gate.
- `NoAvailableQuestionRule(IReadOnlyList<Question> candidates)` — `candidates.Count==0` → `NoAvailableQuestion`.

Uso: `if (new XRule(...).IsBroken()) return Result.Failure(GameErrors.X)` dentro de `Game.StartRound` / `Game.Finish`.

## Relationships

```
Game (1) ──HasMany(composition, cascade, field _rounds)──> GameRound (1..MaxRounds) // FK GameId, UNIQUE (GameId,RoundNumber), HasConversion QuestionId→Guid
Game (1) ──HasMany(composition, cascade, field _players)──> GamePlayer (0..MaxPlayers) // from 004, UNIQUE (GameId,UserId)
Game (1) ── HasOne (logical FK) ──> Question (1) via GameRound.QuestionId (PUBLISHED, 4/1, !Previous, Category/Difficulty/Academic/Age) — validated via IQuestionSelectionStrategy
Game (1) ──owns──> GameConfiguration (1) — OWNED (CategoryId, MinRounds≥5, TimeLimit 5–300, DifficultyStrategy, etc.)
Game (1) ── HasOne (logical FK) ──> Category (1) via Configuration.CategoryId (SPEC-002, ≥5 valid)
Game (1) ──emits──> RoundStartedDomainEvent(RoundId, RoundNumber, QuestionId) / RoundCompletedDomainEvent
GameRound (1) ── HasOne (logical) ──> Question (1) (no FK físico cross-aggregate, FK lógico)
IDifficultyProgressionStrategy ←used by── Game.StartRound (calcula Round.Difficulty = NextDifficulty(completed))
IQuestionSelectionStrategy ←used by── Game.StartRound (SelectAsync criteria con Category/Difficulty/Academic/Age/Previous)
```

## Persistence Mapping (EF Core)

- `OroQuizClashDbContext : AppDbContextBase` ya con `DbSet<Game>`; `DbSet<GameRound>` no separado (via `HasMany` en `Game`), pero `GameRoundTypeConfiguration` registra `ToTable("GameRounds")`.
- `GameTypeConfiguration : IEntityTypeConfiguration<Game>` (existente, extender si falta):
  - `HasKey(g => g.Id)` con `StronglyTypedId` converter `GameId.Value`.
  - `Property(g => g.RowVersion).IsRowVersion().IsConcurrencyToken().IsRequired()`.
  - `Property(g => g.Status).HasConversion(s=>s.Id, id=>GameStatus.FromId(id)).HasColumnName("StatusId")`.
  - `Property(g => g.Name).HasMaxLength(100).IsRequired()`.
  - `OwnsOne(g => g.Configuration, cb => { cb.Property(c=>c.CategoryId).HasConversion(id=>id.Value, v=>new CategoryId(v)); cb.Property(c=>c.MinRounds); cb.Property(c=>c.MaxRounds); cb.Property(c=>c.InitialDifficulty); cb.Property(c=>c.DifficultyStrategy).HasConversion(s=>s.Id, id=>DifficultyProgressionStrategy.FromId(id)); cb.Property(c=>c.TimeLimitPerQuestionSeconds); cb.Property(c=>c.MinPlayers); cb.Property(c=>c.MaxPlayers); })`.
  - `HasMany(g => g.Rounds).WithOne().HasForeignKey("GameId").OnDelete(DeleteBehavior.Cascade)` + `Navigation("Rounds").HasField("_rounds").UsePropertyAccessMode(Field)` (backing field).
  - `HasMany(g => g.Players).WithOne().HasForeignKey("GameId")` similar.
  - `HasIndex(g => g.Status)`, `HasIndex(g => g.Configuration.CategoryId)`, `HasIndex(g => g.CreatedBy)`.
- `GameRoundTypeConfiguration : IEntityTypeConfiguration<GameRound>`:
  - `HasKey(r => r.Id)` con `GameRoundId` converter.
  - `Property(r => r.GameId).HasConversion(id=>id.Value, v=>new GameId(v)).IsRequired()`.
  - `Property(r => r.RoundNumber).IsRequired()`.
  - `Property(r => r.Difficulty).HasConversion(d=>d.Id, id=>DifficultyLevel.FromId(id)).IsRequired()` (o `int`).
  - `Property(r => r.QuestionId).HasConversion(id=>id.Value, v=>new QuestionId(v)).IsRequired()` (FK lógico).
  - `Property(r => r.TimeLimit).IsRequired()`.
  - `Property(r => r.Status).HasConversion(s=>s.Id, id=>GameStatus.FromId(id)).IsRequired()`.
  - `Property(r => r.StartedAt).IsRequired()`, `Property(r => r.CompletedAt)`.
  - `HasIndex(r => new {r.GameId, r.RoundNumber}).IsUnique()` (sin huecos) + `HasIndex(r => new {r.GameId, r.QuestionId}).IsUnique()` opcional (no repetida) + `HasIndex(r => r.QuestionId)`.
- `GamePlayerTypeConfiguration` ya existe (004) con `UNIQUE (GameId,UserId)`.
- `OutboxEntityTypeConfiguration` ya en `AppDbContextBase`; `SaveChanges` participa `DomainEvents` + `Outbox` misma transacción (`Game` + `GameRound` + `Outbox`).

## Validation Rules Summary

| Rule | Field(s) | Valid | Invalid | Error Code | HTTP |
|------|----------|-------|---------|------------|------|
| MinRounds ≥5 | MinRounds | ≥5 ≤50 | 4, 0 | `InvalidGameConfiguration.MinRoundsTooLow` | 400 |
| Round 5 campos | RoundNumber/Difficulty/QuestionId/TimeLimit/Status | 1..MaxRounds/1..5/PUBLISHED not null/5–300/IN_PROGRESS→COMPLETED | null/6/empty/0/ARCHIVED | `InvalidRound.InvalidFields` | 400 |
| RoundNumber único | (GameId,RoundNumber) | único sin huecos | duplicado 1,2,2 | `DuplicateRoundNumber` | 409 |
| Question no repetida | PreviousQuestionIds | !Previous.Contains(QuestionId) | contiene | `DuplicateQuestion` | 409 |
| Category match | Question.CategoryId | == Game.CategoryId | otra | `CategoryMismatch` | 400 (pero SelectAsync ya excluye) |
| Difficulty match | Question.Difficulty | == Round.Difficulty | 2 vs 4 | `DifficultyMismatch` | 400 |
| Academic compatible | AcademicLevel | Value.ToLower()== | diferente | `AcademicLevelMismatch` | 400 |
| Age compatible | AgeRange | overlap | no overlap | `AgeRangeMismatch` | 400 |
| NoAvailableQuestion | candidates | ≥1 | 0 | `NoAvailableQuestion` | 409 |
| Round already in progress | Game.Status/CurrentRound | IN_PROGRESS/COMPLETED sin ronda activa | ROUND_IN_PROGRESS | `RoundAlreadyInProgress` | 409 |
| Previous not completed | CurrentRound.Status | null o COMPLETED | IN_PROGRESS | `PreviousRoundNotCompleted` | 400 |
| Difficulty clamp | NextDifficulty | 1..5 clamp | 6 | `InvalidDifficulty` (pero clamp a 5) | 400 |
| TimeLimit copy | TimeLimit | 5–300 copiado | 0, 301 | `InvalidTimeLimit` (en CreateGame) | 400 |
| PresentQuestion filrado | IsCorrect | no expone a PLAYER | expone | `Forbidden` | 403 |
| Concurrency | RowVersion | matches DB | stale | `ConcurrencyConflict` | 409 |
| Finish gate MinRounds | completedRounds | ≥MinRounds | 3 <5 | `NotEnoughRounds` | 400 |

## State Transitions (Game + GameRound)

```
Game Status (de 004, 9 estados):
[DRAFT] --CreateGame(min≥5)--> [DRAFT]
[DRAFT] --MarkReady(gate category≥5)--> [READY] → [WAITING_FOR_PLAYERS] → [IN_PROGRESS] → loop ROUND_IN_PROGRESS ↔ ROUND_COMPLETED → [FINISHED]
Terminal: FINISHED/CANCELLED/FORCED_FINISHED → no salidas

GameRound Status dentro de Game:
[IN_PROGRESS] --StartRound(selector, difficultyStrategy)→ [ROUND_IN_PROGRESS] (RoundNumber = Rounds.Count+1, Difficulty=NextDifficulty, QuestionId=SelectAsync(!Previous), TimeLimit=copied, StartedAt=UtcNow, Raise RoundStarted)
[ROUND_IN_PROGRESS] --CompleteRound(roundId)→ [ROUND_COMPLETED] (CompletedAt=UtcNow, Raise RoundCompleted) → Game.Status also ROUND_COMPLETED
[ROUND_COMPLETED] --StartRound→ [ROUND_IN_PROGRESS] (next RoundNumber, IncreaseDifficulty applied)
[ROUND_COMPLETED] --Finish(completed≥MinRounds)→ [FINISHED] (Game.Finish)
```

`IncreaseDifficulty` no es transición separada; es `NextDifficulty` calculado para el siguiente `StartRound` (no muta ronda existente).

## Domain Events / Integration Events

- `RoundStartedDomainEvent(GameId, RoundId, RoundNumber, QuestionId, Difficulty, TimeLimit)` — tras `StartRound` (in-process).
- `RoundCompletedDomainEvent(GameId, RoundId, RoundNumber)` — tras `CompleteRound`.
- `GameFinishedDomainEvent(GameId)` ya existe (004) para `Finish` gate `completed≥5`.
- Todos dispatch en `AppDbContextBase.SaveChanges` (in-process `IDomainEventHandler` si aplica); integración opcional `RoundCompletedIntegrationEvent` vía `IOutboxWriter` → `OutboxProcessor` → RabbitMQ (topic `round.completed`) para `PointTransaction`/`Leaderboard` si se requiere async (pero `CalculateScores` en este SPEC es sincrónico ledger dentro de `SubmitAnswer`).
- `RoundStarted` puede ser `IntegrationEvent` para SignalR `RoundStarted` notification (server-driven, no source of truth).

## Specifications

- `GameByIdSpecification(GameId)` — `Where(g=>g.Id==id)` + `Include(Rounds)` + `Include(Players)` + `AsNoTracking` para rehidratación `GetGame` → `PresentQuestion` necesita `Rounds` para `CurrentRound`.
- `GameFilterSpecification(Status?, CategoryId?, CreatedBy?, Search?, page, pageSize)` ya existe (004) con `Where(Status)`, `Where(CategoryId)`, paginación + `AsNoTracking`.
- `ValidQuestionSpecification(CategoryId)` / `QuestionSelectionSpecification(QuestionSelectionCriteria)` ya en 003 (Status==PUBLISHED + 4/1 + todos filtros + !Previous + AsNoTracking + Take(1) + OrderBy NEWID()).
- No `GameRoundSpecification` separado (acceso vía `Game.Rounds`); si se necesita `GameRoundByIdSpecification` — `Where(r=>r.Id==roundId && r.GameId==gameId)` con `AsNoTracking`.

## Audit & Observability

- `GameLifecycleAudit` / `RoundEngineAudit` tabla `AuditLog` con `CorrelationId`, `GameId`, `RoundId`, `RoundNumber`, `QuestionId`, `Difficulty`, `TimeLimit`, `FromStatus`, `ToStatus`, `Command (StartRound/CompleteRound)`, `PerformedBy (sub)`, `Timestamp`, `Duration` (append-only, `HasIndex(GameId, RoundNumber)` + `HasIndex(GameId, Timestamp)`).
- OTel `BuildingBlocks.ServiceDefaults`: logs `TraceId`, `GameId`, `RoundId`, `RoundNumber`, `QuestionId`, `Difficulty`, `TimeLimit`, `Command`, `Duration`, `Result`; metrics `round_engine_transitions_total` por `Status`, `round_selection_duration_seconds` histogram (p95 <500ms).
- `OutboxMessages` para `RoundCompletedIntegrationEvent` si se publica (con `Id`, `Type`, `Payload`, `OccurredOn`, `ProcessedAt?`).

