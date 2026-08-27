# Data Model: Game Lifecycle

**Feature**: `004-game-lifecycle` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Entities

### Game (AggregateRoot<GameId>)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `GameId : StronglyTypedId<Guid>` | PK, immutable, Guid | BuildingBlocks |
| `Name` | `string` | 3–100 chars, no vacío, trim | SPEC-001 |
| `Configuration` | `GameConfiguration : ValueObject` | Inmutable tras `Start` (FR-007) | SPEC-001 |
| `Status` | `GameStatus : Enumeration` | 9 valores `DRAFT(1), READY(2), WAITING_FOR_PLAYERS(3), IN_PROGRESS(4), ROUND_IN_PROGRESS(5), ROUND_COMPLETED(6), FINISHED(7), CANCELLED(8), FORCED_FINISHED(9)` | FR-001 |
| `RowVersion` | `byte[]` | `rowversion` / `IsConcurrencyToken()` | FR-001, F |
| `CreatedAt` | `DateTimeOffset` | set en `Create` | audit |
| `ReadyAt` | `DateTimeOffset?` | set en `MarkReady` | audit |
| `StartedAt` | `DateTimeOffset?` | set en `Start` | audit |
| `FinishedAt` | `DateTimeOffset?` | set en `Finish/Cancel/ForceFinish` | audit |
| `CreatedBy` | `Guid` (sub claim) | FK lógico a OroIdentityServer `sub` | VI |
| `Players` | `IReadOnlyList<GamePlayer>` | 0..MaxPlayers, composition | FR-004 |
| `Rounds` | `IReadOnlyList<GameRound>` | 0..MaxRounds, orden `RoundNumber` | FR-005 |
| `CurrentRound` | `GameRound?` | derivado `Rounds.FirstOrDefault(r=>r.Status==ROUND_IN_PROGRESS)` | helper |
| Domain Events | 9 eventos (ver abajo) | dispatch en `SaveChanges` | BuildingBlocks |

**Behavior**: `static Result<Game> Create(GameConfiguration, Guid createdBy)` (valida SPEC-001); `Result MarkReady(ICategoryValidator, IQuestionCounter)` (`DRAFT→READY` gate FR-003); `Result OpenLobby()` (`READY→WAITING_FOR_PLAYERS`); `Result JoinPlayer(Guid userId)` (solo `WAITING_FOR_PLAYERS`, valida duplicado/GameFull → `PlayerJoinedDomainEvent`); `Result Start()` (`WAITING_FOR_PLAYERS→IN_PROGRESS` gate `players≥MinPlayers` FR-004); `Result StartRound(IQuestionSelectionStrategy, QuestionSelectionCriteria)` (`IN_PROGRESS`/`ROUND_COMPLETED`→`ROUND_IN_PROGRESS` gate FR-005, crea `GameRound` + asigna `QuestionId`); `Result CompleteRound()` (`ROUND_IN_PROGRESS→ROUND_COMPLETED`); `Result Finish()` (→`FINISHED` FR-008); `Result Cancel(string reason)` (→`CANCELLED`); `Result ForceFinish(string reason)` (→`FORCED_FINISHED`); `Result UpdateConfiguration(newConfig)` (solo `DRAFT`/`READY`, FR-007). Todas retornan `Result` con `Error` tipificado y aplican `IBusinessRule`; mutaciones protegidas por `RowVersion`.

**Invariants**:
- `Status` 9 valores, transiciones solo vía `IsValidTransition` (FR-001).
- `Configuration` inmutable tras `IsStarted` (≥WAITING_FOR_PLAYERS) (FR-007).
- No dos `ROUND_IN_PROGRESS` simultáneas (FR-005).
- `players.Count ≥ MinPlayers` para `Start` (FR-004).
- Solo `ROUND_IN_PROGRESS` acepta `SubmitAnswer` (FR-006) — validado en `Game` aunque `SubmitAnswer` sea slice futuro, `Game` expone `CanSubmitAnswer(playerId)` helper.
- `FINISHED/CANCELLED/FORCED_FINISHED` terminales, no salidas (FR-008).

### GameRound (Entity<GameRoundId> dentro de Game)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `GameRoundId : StronglyTypedId<Guid>` | PK dentro agregado, immutable | composition |
| `GameId` | `GameId` | FK a Game, cascade delete | owned |
| `RoundNumber` | `int` | 1..MaxRounds, único por `GameId`, incremental sin saltos | FR-005 |
| `QuestionId` | `QuestionId : StronglyTypedId<Guid>` | FK lógico a `Question` PUBLISHED, required | SPEC-003 |
| `Status` | `GameRoundStatus : Enumeration` | `ROUND_IN_PROGRESS(5), ROUND_COMPLETED(6)` (reusa `GameStatus` values 5-6) | FR-002 |
| `StartedAt` | `DateTimeOffset` | set en `StartRound` | audit |
| `CompletedAt` | `DateTimeOffset?` | set en `CompleteRound` | audit |
| `CreatedAt` | `DateTimeOffset` | set en creación | — |

**Behavior**: No expone mutación directa; solo vía `Game.StartRound` (crea con `RoundNumber = Rounds.Count+1`, `Status=ROUND_IN_PROGRESS`, `StartedAt=UtcNow`) y `Game.CompleteRound` (set `Status=ROUND_COMPLETED`, `CompletedAt=UtcNow`). Composition: `Game` owns `Rounds` (EF `HasMany.WithOwner`).

**Invariante composición**: `RoundNumber` único por `GameId` (DB `UNIQUE Index (GameId, RoundNumber)`), `StartedAt ≤ CompletedAt` si completada.

### GamePlayer (Entity<GamePlayerId> dentro de Game)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `GamePlayerId : StronglyTypedId<Guid>` | PK dentro agregado, immutable | composition |
| `GameId` | `GameId` | FK a Game, cascade delete | owned |
| `UserId` | `Guid` (sub claim) | FK lógico a OroIdentityServer `sub`, único por `GameId` | VI |
| `JoinedAt` | `DateTimeOffset` | set en `JoinPlayer` | audit |
| `DisplayName` | `string?` | 0–100 desde `userinfo` (opcional) | — |

**Behavior**: Solo vía `Game.JoinPlayer(userId)` (valida `Status==WAITING_FOR_PLAYERS`, `Count < MaxPlayers`, `!Players.Any(p=>p.UserId==userId)`). Composition `Game` owns `Players` (EF `HasMany`).

**Invariante composición**: `UserId` único por `GameId` (`UNIQUE Index (GameId, UserId)`), `Count ≤ MaxPlayers`.

### GameConfiguration (ValueObject, inmutable)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `CategoryId` | `CategoryId` | required, must be `ACTIVE` / `PUBLISHED` con ≥5 válidas | SPEC-002/003 |
| `MinRounds` | `int` | ≥5, ≤50 | SPEC-001 CFG-002 |
| `MaxRounds` | `int` | ≥MinRounds, ≤50 | SPEC-001 |
| `InitialDifficulty` | `int` | 1..5 | SPEC-001 |
| `DifficultyStrategy` | `DifficultyProgressionStrategy : Enumeration` | `Linear, Progressive, Adaptive, CategorySpecific` | SPEC-001 |
| `TimeLimitPerQuestionSeconds` | `int` | 5–300 positivo | SPEC-001 CFG-006 |
| `PointsPerRound` | `int` | ≥1 | SPEC-001 |
| `WithdrawalPolicy` | `WithdrawalPolicy : Enumeration` | `LOSE_ALL, KEEP_CURRENT_SCORE, KEEP_SECURED_SCORE, KEEP_CHECKPOINT_SCORE` | SPEC-001 |
| `LossPolicy` | `LossPolicy : Enumeration` | `LOSE_ALL, LOSE_CURRENT_ROUND, LOSE_UNSECURED_POINTS, FALLBACK_TO_CHECKPOINT` | SPEC-001 |
| `ConsolationPolicy` | `ConsolationPolicy : Enumeration` | `NONE, CONSOLATION` | SPEC-001 |
| `RewardRules` | `RewardRules : ValueObject` | `Type` required | SPEC-001 |
| `ScoringSystem` | `ScoringSystem : Enumeration` | required | SPEC-001 |
| `MinPlayers` | `int` | ≥1, ≤MaxPlayers, por defecto 2 | SPEC-001 |
| `MaxPlayers` | `int` | ≥MinPlayers, ≤100, por defecto 10 | SPEC-001 |
| `Name` | `string` | 3–100 | SPEC-001 |

**Behavior**: `GameConfiguration` es VO con `GetEqualityComponents` (todos los campos); sin setters; validado en `Game.Create` y `Game.MarkReady` via `IBusinessRule`. Inmutable: `Game.UpdateConfiguration` solo permitido en `DRAFT`/`READY` (FR-007).

## ValueObjects / Enumerations

- **GameStatus**: `DRAFT(1), READY(2), WAITING_FOR_PLAYERS(3), IN_PROGRESS(4), ROUND_IN_PROGRESS(5), ROUND_COMPLETED(6), FINISHED(7), CANCELLED(8), FORCED_FINISHED(9)` — `Enumeration<GameStatus>`, `GetAll()`, `FromId()`, `FromName()`, `IsTerminal (FINISHED/CANCELLED/FORCED_FINISHED)`, `IsStarted (≥WAITING_FOR_PLAYERS)`, `IsRoundActive (ROUND_IN_PROGRESS)`, `CanTransitionTo(to)` con matriz.
- **GameRoundStatus**: Reusa valores `5`/`6` de `GameStatus` o `Enumeration<GameRoundStatus>` separado `IN_PROGRESS(5), COMPLETED(6)`; persistido como `int` en `GameRound.StatusId`.
- **GameConfiguration**: `ValueObject` con `GetEqualityComponents` yield todos los campos ordenados; `GetEquality` estructural.
- **CategoryId / QuestionId / GameId / GameRoundId / GamePlayerId**: `StronglyTypedId<Guid>` cada uno con `New()` factory.
- **DifficultyLevel** (1..5), **LossPolicy**, **WithdrawalPolicy**, **ConsolationPolicy**, **ScoringSystem**, **DifficultyProgressionStrategy**: `Enumeration` existentes en `Games/Enumerations`.

## Business Rules (IBusinessRule)

- `GameConfigurationValidRule(GameConfiguration)` — valida `MinRounds≥5`, `MaxRounds≥MinRounds`, `TimeLimit 5–300`, `Difficulty 1..5`, `MinPlayers≥1 ≤ MaxPlayers` else `InvalidGameConfiguration`.
- `CategoryMustBeReadyRule(CategoryId, bool isPublished, int validCount)` — `isPublished && validCount≥5` else `CategoryNotReady` (para `MarkReady`).
- `NotEnoughPlayersRule(int current, int min)` — `current < min` else `NotEnoughPlayers`.
- `PlayerAlreadyJoinedRule(Game, Guid userId)` — `Players.Any(p=>p.UserId==userId)` → `PlayerAlreadyJoined`.
- `GameFullRule(int current, int max)` — `current ≥ max` → `GameFull`.
- `PreviousRoundNotCompletedRule(GameStatus current, GameRound? currentRound)` — `current==ROUND_IN_PROGRESS` o `currentRound.Status==ROUND_IN_PROGRESS` → `RoundAlreadyInProgress` / `PreviousRoundNotCompleted`.
- `NoActiveRoundRule(GameStatus current)` — `current != ROUND_IN_PROGRESS` → `NoActiveRound` (para `SubmitAnswer` guard).
- `ConfigurationImmutableRule(GameStatus status)` — `status.IsStarted` → `ConfigurationImmutable`.
- `CanFinishFromStateRule(GameStatus from, GameStatus to)` — matriz `IsValidTransition(from,to)` else `InvalidGameState`; `from.IsTerminal` → `InvalidGameState`.
- `CanCancelFromStateRule(GameStatus from)` — `from.IsTerminal` → `InvalidGameState`; `FORCED_FINISHED` solo desde `IN_PROGRESS/ROUND_*`.
- `ReasonRequiredRule(string? reason)` — `3–500` chars else `InvalidReason`.

Uso: `if (new XRule(...).IsBroken()) return Result.Failure(GameErrors.X)` dentro de `Game.MarkReady/JoinPlayer/Start/StartRound` etc.

## Relationships

```
Game (1) ──HasMany(composition, cascade)──> GamePlayer (0..MaxPlayers) // FK GameId, UNIQUE (GameId, UserId)
Game (1) ──HasMany(composition, cascade)──> GameRound (0..MaxRounds) // FK GameId, UNIQUE (GameId, RoundNumber)
Game (1) ──owns──> GameConfiguration (1) — owned type (CategoryId, MinRounds, MaxRounds, etc.)
Game (1) ──owns──> GameStatus (1) — HasConversion Id→Enumeration
Game (1) ── HasOne (logical FK) ──> Category (1) via GameConfiguration.CategoryId (validated via ICategoryValidator)
GameRound (1) ── HasOne (logical FK) ──> Question (1) via QuestionId (PUBLISHED, selected via IQuestionSelectionStrategy)
Game (1) ──emits──> GameCreated/Ready/PlayerJoined/Started/RoundStarted/RoundCompleted/Finished/Cancelled/ForcedFinished DomainEvent
Game (1) ──queried by──> GameFilterSpecification, GameByIdSpecification
Category (1) ──validated by──> ICategoryValidator.IsPublished / IQuestionCounter.CountValid
```

## Persistence Mapping (EF Core)

- `OroQuizClashDbContext : AppDbContextBase` ya con `DbSet<Game>` (001); extender con configuración para `GameRound`/`GamePlayer` como `Entity` (no `DbSet` separado, via `HasMany` en `Game`).
- `GameTypeConfiguration : IEntityTypeConfiguration<Game>` (existente, extender):
  - `HasKey(g => g.Id)` con `StronglyTypedId` converter `GameId.Value`.
  - `Property(g => g.RowVersion).IsRowVersion().IsConcurrencyToken().IsRequired()`.
  - `Property(g => g.Status).HasConversion(s=>s.Id, id=>GameStatus.FromId(id)).HasColumnName("StatusId").IsRequired()`.
  - `Property(g => g.Name).HasMaxLength(100).IsRequired()`.
  - `OwnsOne(g => g.Configuration, cb => { cb.Property(c=>c.CategoryId).HasConversion(id=>id.Value, v=>new CategoryId(v)).HasColumnName("CategoryId").IsRequired(); cb.Property(c=>c.MinRounds).IsRequired(); cb.Property(c=>c.MaxRounds).IsRequired(); cb.Property(c=>c.TimeLimitPerQuestionSeconds).IsRequired(); cb.Property(c=>c.MinPlayers).IsRequired(); cb.Property(c=>c.MaxPlayers).IsRequired(); /* Difficulty, Policies enums with HasConversion */ })`.
  - `HasMany(g => g.Players).WithOne().HasForeignKey("GameId").OnDelete(DeleteBehavior.Cascade)` + `HasMany(g => g.Rounds).WithOne().HasForeignKey("GameId").OnDelete(DeleteBehavior.Cascade)`.
  - `HasIndex(g => g.Status)`, `HasIndex(g => g.Configuration.CategoryId)`, `HasIndex(g => new {g.Status, g.CreatedBy})`.
  - `Navigation` para `Players`/`Rounds` con `Field("_players")` / `Field("_rounds")` (backing field) si usan private list.
- `GameRoundTypeConfiguration : IEntityTypeConfiguration<GameRound>`:
  - `HasKey(gr => gr.Id)` con `GameRoundId` converter.
  - `Property(gr => gr.GameId).HasConversion(id=>id.Value, v=>new GameId(v)).IsRequired()`.
  - `Property(gr => gr.RoundNumber).IsRequired()`.
  - `Property(gr => gr.QuestionId).HasConversion(id=>id.Value, v=>new QuestionId(v)).IsRequired()`.
  - `Property(gr => gr.Status).HasConversion(s=>s.Id, id=>GameStatus.FromId(id)).IsRequired()` (o `GameRoundStatus`).
  - `Property(gr => gr.StartedAt).IsRequired()`, `Property(gr => gr.CompletedAt)`.
  - `HasIndex(gr => new {gr.GameId, gr.RoundNumber}).IsUnique()` (idempotencia `StartRound`).
  - `HasIndex(gr => gr.QuestionId)`.
- `GamePlayerTypeConfiguration : IEntityTypeConfiguration<GamePlayer>`:
  - `HasKey(gp => gp.Id)` con `GamePlayerId` converter.
  - `Property(gp => gp.GameId).HasConversion(id=>id.Value, v=>new GameId(v)).IsRequired()`.
  - `Property(gp => gp.UserId).IsRequired()`.
  - `HasIndex(gp => new {gp.GameId, gp.UserId}).IsUnique()` (no duplicados).
  - `HasIndex(gp => gp.GameId)`.
- `OutboxEntityTypeConfiguration` ya en `AppDbContextBase`; `SaveChanges` participa `DomainEvents` + `Outbox` misma transacción.
- `GameLifecycleAudit` tabla `AuditLog` con `Id, CorrelationId, GameId, PlayerId, RoundId, FromState, ToState, Command, PerformedBy, Timestamp` (append-only, `HasIndex(GameId, Timestamp)`).

## Validation Rules Summary

| Rule | Field(s) | Valid | Invalid | Error Code | HTTP |
|------|----------|-------|---------|------------|------|
| Name 3–100 | Name | 3–100 no vacío | vacío/101 | `InvalidGameConfiguration.InvalidName` | 400 |
| MinRounds ≥5 | MinRounds | ≥5 ≤50 | 3, 0 | `InvalidGameConfiguration.MinRoundsTooLow` | 400 |
| Rounds range | MinRounds/MaxRounds | Min≤Max | 10/5 | `InvalidGameConfiguration.InvalidRange` | 400 |
| TimeLimit 5–300 | TimeLimitPerQuestionSeconds | 5–300 positivo | 0, 301 | `InvalidGameConfiguration.InvalidTimeLimit` | 400 |
| Category published | CategoryId | ACTIVE + ≥5 válidas | ARCHIVED, 0 válidas | `CategoryNotReady` | 400 |
| Players range | MinPlayers/MaxPlayers | 1≤Min≤Max≤100 | 0, Min>Max | `InvalidGameConfiguration.InvalidRange` | 400 |
| Join status | Game.Status | WAITING_FOR_PLAYERS | DRAFT, IN_PROGRESS | `InvalidGameState` | 400 |
| Join duplicate | UserId | no existe en Players | ya existe | `PlayerAlreadyJoined` | 409 |
| Game full | Players.Count | <MaxPlayers | ≥MaxPlayers | `GameFull` | 409 |
| Start players | players.Count vs MinPlayers | ≥MinPlayers | 1 < 2 | `NotEnoughPlayers` | 400 |
| StartRound status | Game.Status | IN_PROGRESS o ROUND_COMPLETED | ROUND_IN_PROGRESS | `RoundAlreadyInProgress` | 400 |
| StartRound NoQuestion | Question selection | 1 PUBLISHED no usada | 0 disponibles | `NoAvailableQuestion` | 409/400 |
| SubmitAnswer status | Game.Status | ROUND_IN_PROGRESS | IN_PROGRESS sin ronda | `NoActiveRound` | 400 |
| Config immutable | Game.Status | DRAFT/READY | ≥WAITING_FOR_PLAYERS | `ConfigurationImmutable` | 400 |
| Finish from valid | Game.Status→FINISHED | IN_PROGRESS/ROUND_COMPLETED | DRAFT/READY | `InvalidGameState` | 400 |
| Cancel/ForceFinish | Game.Status | no terminal | FINISHED | `InvalidGameState` | 400 |
| Reason 3–500 | Reason | 3–500 | vacío/501 | `InvalidReason` | 400 |
| Concurrency | RowVersion | matches DB | stale | `ConcurrencyConflict` | 409 |
| Terminal no transition | From.IsTerminal | false | FINISHED→Start | `InvalidGameState` | 400 |

## State Transitions

```
[DRAFT] --CreateGame(config valid)--> [DRAFT] (GameCreated)
[DRAFT] --MarkReady(gate category≥5 + config)--> [READY] (GameReady)  FAIL → stays DRAFT (CategoryNotReady)
[DRAFT] --Cancel(reason)--> [CANCELLED] (GameCancelled)  // admin may cancel before ready
[READY] --OpenLobby()--> [WAITING_FOR_PLAYERS] (LobbyOpened)
[READY] --Cancel(reason)--> [CANCELLED]
[WAITING_FOR_PLAYERS] --JoinPlayer(userId)--> [WAITING_FOR_PLAYERS] (PlayerJoined)  FAIL → GameFull/AlreadyJoined
[WAITING_FOR_PLAYERS] --Start(players≥MinPlayers)--> [IN_PROGRESS] (GameStarted)  FAIL → NotEnoughPlayers
[WAITING_FOR_PLAYERS] --Cancel(reason)--> [CANCELLED]
[IN_PROGRESS] --StartRound(select PUBLISHED)--> [ROUND_IN_PROGRESS] (RoundStarted)  FAIL → NoAvailableQuestion
[IN_PROGRESS] --Finish()--> [FINISHED] (GameFinished) // if no rounds needed or early finish
[IN_PROGRESS] --Cancel(reason)--> [CANCELLED]
[IN_PROGRESS] --ForceFinish(reason)--> [FORCED_FINISHED] (GameForcedFinished)
[ROUND_IN_PROGRESS] --CompleteRound()--> [ROUND_COMPLETED] (RoundCompleted)
[ROUND_IN_PROGRESS] --Cancel(reason)--> [CANCELLED]
[ROUND_IN_PROGRESS] --ForceFinish(reason)--> [FORCED_FINISHED]
[ROUND_IN_PROGRESS] --Finish()--> [FINISHED] // if policy allows finish mid-round
[ROUND_COMPLETED] --StartRound()--> [ROUND_IN_PROGRESS] (next RoundNumber)  // loop
[ROUND_COMPLETED] --Finish()--> [FINISHED] (GameFinished) // when completedRounds≥MinRounds && ==MaxRounds or policy
[ROUND_COMPLETED] --Cancel(reason)--> [CANCELLED]
[ROUND_COMPLETED] --ForceFinish(reason)--> [FORCED_FINISHED]
[FINISHED/CANCELLED/FORCED_FINISHED] --*--> FAIL (terminal) InvalidGameState, RowVersion 409 if concurrent

[ANY] --UpdateConfiguration()--> FAIL if IsStarted (≥WAITING_FOR_PLAYERS) → ConfigurationImmutable
```

Solo `DRAFT` permite `MarkReady`; solo `WAITING_FOR_PLAYERS` permite `Join`; solo `WAITING_FOR_PLAYERS` con `players≥MinPlayers` permite `Start`; solo `IN_PROGRESS`/`ROUND_COMPLETED` permite `StartRound` si no hay `ROUND_IN_PROGRESS` activo; solo `ROUND_IN_PROGRESS` permite `CompleteRound` y `SubmitAnswer` (guard externo pero `CanSubmitAnswer` helper en `Game`).

## Domain Events / Integration Events

- `GameCreatedDomainEvent(GameId)` — tras `Create` (in-process).
- `GameReadyDomainEvent(GameId)` — tras `MarkReady`.
- `PlayerJoinedDomainEvent(GameId, UserId)` — tras `JoinPlayer`.
- `GameStartedDomainEvent(GameId)` — tras `Start`.
- `RoundStartedDomainEvent(GameId, RoundId, RoundNumber, QuestionId)` — tras `StartRound`.
- `RoundCompletedDomainEvent(GameId, RoundId)` — tras `CompleteRound`.
- `GameFinishedDomainEvent(GameId)` — tras `Finish`.
- `GameCancelledDomainEvent(GameId, Reason)` — tras `Cancel`.
- `GameForcedFinishedDomainEvent(GameId, Reason)` — tras `ForceFinish`.
- Todos dispatch en `AppDbContextBase.SaveChanges` (in-process `IDomainEventHandler` si aplica).
- Integración futura: `GameFinishedIntegrationEvent(GameId, FinishedAt, Players, Rounds)` publicado vía `IOutboxWriter` → `OutboxProcessor` → RabbitMQ (`GameFinished` topic) para estadísticas/recompensas (SPEC-011).

## Specifications

- `GameByIdSpecification(GameId)` — `Where(g => g.Id == id)` + `Include(Rounds)` + `Include(Players)` + `AsNoTracking` para lectura.
- `GameFilterSpecification(Status?, CategoryId?, CreatedBy?, Search?, page, pageSize)` — `Where(Status==)`, `Where(Configuration.CategoryId==)`, `Where(CreatedBy==)`, `Where(Name.Contains(search))` + `Pagination` + `AsNoTracking` + `OrderByDescending(CreatedAt)`.
- Todas heredan `Specification<Game>` BuildingBlocks (`Where`, `And`, `Or`, `Not`, `Include`, `OrderBy`, `Paginate`, `ApplyAsNoTracking`).

## Audit & Domain Events

- `GameLifecycleAudit` tabla `AuditLog` con `CorrelationId`, `GameId`, `PlayerId?`, `RoundId?`, `FromState`, `ToState`, `Command (MarkReady/Join/Start/StartRound/CompleteRound/Finish/Cancel/ForceFinish)`, `PerformedBy (sub)`, `Timestamp`, `Reason?` (append-only, no muta histórico, `HasIndex(GameId, Timestamp)`).
- OTel `BuildingBlocks.ServiceDefaults`: logs `TraceId`, `GameId`, `PlayerId`, `RoundId`, `FromState`, `ToState`, `Command`, `Duration`, `Result`; metrics `game_transitions_total` por estado.
- `OutboxMessages` tabla para `GameFinishedIntegrationEvent` (si se publica) con `Id`, `Type`, `Payload`, `OccurredOn`, `ProcessedAt?`.

