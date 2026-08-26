# Data Model: Game Configuration

**Feature**: `001-game-configuration` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Entities

### Game (AggregateRoot<GameId>)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `GameId : StronglyTypedId<Guid>` | PK, immutable | `BuildingBlocks.Kernel.Domain` |
| `Name` | `string` | `3–100` chars, no vacío, trim | FR-008, validado en Validator + Rule |
| `Configuration` | `GameConfiguration` (ValueObject, owned) | Inmutable, sin setters | FR-011, owned type en EF |
| `Status` | `GameStatus` (Enumeration) | `DRAFT` inicial → `READY` → `WAITING_FOR_PLAYERS`/`IN_PROGRESS`... | Constitución A, FR-003 |
| `RowVersion` | `byte[]` | `rowversion` / `IsConcurrencyToken()` | FR-013, Constitución F |
| `CreatedAt` | `DateTimeOffset` | Set en `Create` | Audit |
| `CreatedBy` | `Guid` (sub claim) | FK lógico a OroIdentityServer `sub` | Constitución VI |
| Domain Events | `GameCreatedDomainEvent` | Dispatch en `SaveChanges` | BuildingBlocks |

**Behavior**: `static Result<Game> Create(GameConfiguration config, ICategoryValidator)` → aplica `IBusinessRule` (ver abajo) y retorna `Result<Game>`; `Result<Unit> Start()` → verifica CFG-001 y `Status ∈ {DRAFT,READY}`; tras `Start()` cualquier mutación de `Configuration` retorna `Error ConfigurationImmutable/InvalidGameState`.

**Invariants**:
- No existe `Game` sin `Configuration` válida (CFG-001).
- `MinRounds ≥5` (CFG-002), `MinRounds ≤ MaxRounds`, `MinPlayers ≥1`, `MinPlayers ≤ MaxPlayers`.
- `TimeLimitPerQuestion` positivo 5–300s (CFG-006).
- `CategoryId` referencia categoría `Published` (CFG-004).
- `DifficultyStrategy` y `InitialDifficulty` válidos y coherentes (CFG-005).
- `LossPolicy` y `WithdrawalPolicy` definidos (CFG-007).

### GameConfiguration (ValueObject)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Name` | `string` | Redundante con Game.Name o solo en Game | Decisión: Name en Game, no duplicar |
| `CategoryId` | `CategoryId : StronglyTypedId<Guid>` | FK lógico, no FK física cross-DB | Validado vía `IRepository<Category>` |
| `MinRounds` | `int` | `≥5`, `≤ MaxRounds` | CFG-002, FR-009 |
| `MaxRounds` | `int` | `≥ MinRounds`, `≥5` | FR-009 |
| `InitialDifficulty` | `int` / `DifficultyLevel` (Enumeration) | `∈ {1..5}` o conjunto configurado | FR-010, Constitución C |
| `DifficultyStrategy` | `DifficultyProgressionStrategy` (Enumeration) | `Linear`/`Progressive`/`Adaptive`/`CategorySpecific` | FR-005, CFG-005 |
| `TimeLimitPerQuestion` | `TimeSpan` / `int Seconds` | `5–300s`, `>0` | FR-006, CFG-006 |
| `ScoringSystem` | `ScoringSystem` (Enumeration/VO) | No nulo, cerrado | FR-008 |
| `LossPolicy` | `LossPolicy` (Enumeration) | `LOSE_ALL`/`LOSE_CURRENT_ROUND`/`LOSE_UNSECURED_POINTS`/`FALLBACK_TO_CHECKPOINT` | FR-007 |
| `WithdrawalPolicy` | `WithdrawalPolicy` (Enumeration) | `LOSE_ALL`/`KEEP_CURRENT_SCORE`/`KEEP_SECURED_SCORE`/`KEEP_CHECKPOINT_SCORE` | FR-007 |
| `ConsolationPolicy` | `ConsolationPolicy` (Enumeration/VO) | Definida | FR-008 |
| `RewardRules` | `RewardRules` (ValueObject) | Definida, no nula | FR-008 |
| `MinPlayers` | `int` | `≥1`, `≤ MaxPlayers` | FR-009 |
| `MaxPlayers` | `int` | `≥ MinPlayers`, `≥1` | FR-009 |

**Equality**: por valor (todos los campos). **Immutabilidad**: `private set`, construcción solo vía constructor/ factory.

### Category (External Reference)

| Field | Type | Constraints |
|-------|------|-------------|
| `Id` | `CategoryId` | — |
| `Status` | `Enumeration` | `Published` requerido para crear Game |
| `ValidQuestionsCount` | `int` | `≥5` para ser Published |

Nota: `Category` vive en otro bounded context (SPEC-002/003). `Game` no duplica la entidad; solo valida vía `ISpecification<Category>`.

## Enumerations / ValueObjects

- **GameStatus**: `DRAFT`, `READY`, `WAITING_FOR_PLAYERS`, `IN_PROGRESS`, `ROUND_IN_PROGRESS`, `ROUND_COMPLETED`, `FINISHED`, `CANCELLED`, `FORCED_FINISHED`
- **DifficultyProgressionStrategy**: `Linear`, `Progressive`, `Adaptive`, `CategorySpecific`
- **LossPolicy**: `LOSE_ALL`, `LOSE_CURRENT_ROUND`, `LOSE_UNSECURED_POINTS`, `FALLBACK_TO_CHECKPOINT`
- **WithdrawalPolicy**: `LOSE_ALL`, `KEEP_CURRENT_SCORE`, `KEEP_SECURED_SCORE`, `KEEP_CHECKPOINT_SCORE`
- **ConsolationPolicy**: según reglas de negocio (p.ej. `None`, `FixedPoints`, `Badge`)
- **ScoringSystem**: p.ej. `Standard`, `ProgressiveBonus` (extensible)
- **RewardRules**: ValueObject con `RewardType`, `Thresholds`, etc.

## Business Rules (IBusinessRule)

Cada regla implementa `IBusinessRule` con `string Message` y `bool IsBroken()`:

- `MinRoundsAtLeastFiveRule(minRounds)`
- `RoundsRangeCoherenceRule(minRounds, maxRounds)`
- `PlayersRangeCoherenceRule(minPlayers, maxPlayers)`
- `CategoryMustBeValidRule(category, exists, isPublished)`
- `DifficultyStrategyRequiredRule(strategy, initialDifficulty)`
- `TimeLimitPositiveRule(seconds)` + `TimeLimitRangeRule(5,300)`
- `PoliciesRequiredRule(lossPolicy, withdrawalPolicy)`
- `GameNameNotEmptyRule(name)`
- `GameConfigurationMustBeValidRule(configuration)` (compuesta)

Uso: `CheckRule(new XRule(...))` dentro de `Game.Create`.

## Relationships

```
Game (1) ──owns──> GameConfiguration (1) — ValueObject, owned type
Game ──references──> Category (CategoryId) — validado, no FK física cross-DB
Game ──emits──> GameCreatedDomainEvent (in-process, opcional Outbox → RabbitMQ)
```

## Persistence Mapping (EF Core)

- `OroQuizClashDbContext : AppDbContextBase` con `DbSet<Game>`.
- `GameTypeConfiguration : IEntityTypeConfiguration<Game>`:
  - `HasKey(g => g.Id)` con `StronglyTypedId` converter (`GameId.Value`).
  - `OwnsOne(g => g.Configuration, cb => { cb.Property(...); })` para `GameConfiguration` como owned type (columnas `Configuration_MinRounds`, etc.).
  - `Property(g => g.RowVersion).IsRowVersion().IsConcurrencyToken()`.
  - `Property(g => g.Status).HasConversion(...)` para Enumeration.
- `ApplyConfiguration(new OutboxEntityTypeConfiguration())` para Outbox.
- Índices: `IX_Game_Status`, `IX_Game_CategoryId` según patrones de consulta; unicidad de nombre no requerida en v1.

## Validation Rules Summary

| Rule | Field(s) | Valid | Invalid | Error |
|------|----------|-------|---------|-------|
| CFG-002 | MinRounds | ≥5 | <5 | `InvalidGameConfiguration.MinRoundsTooLow` |
| CFG-003 | Configuration after Start | — | mutación tras `Start()` | `InvalidGameState.ConfigurationImmutable` |
| CFG-004 | CategoryId | exists && Published | no existe / no Published | `CategoryNotFound` / `CategoryNotReady` |
| CFG-005 | DifficultyStrategy | ∈ conjunto | null/desconocido | `InvalidGameConfiguration.DifficultyStrategyRequired` |
| CFG-006 | TimeLimit | 5–300s, >0 | ≤0, >300 | `InvalidGameConfiguration.InvalidTimeLimit` |
| CFG-007 | Loss/Withdrawal | definidos, válidos | null/desconocido | `InvalidGameConfiguration.PoliciesRequired` |
| Range | min/max rounds/players | min≤max, min≥1 | min>max, min<1 | `InvalidGameConfiguration.InvalidRange` |
| Name | Name | 3–100, no vacío | vacío/<3/>100 | `InvalidGameConfiguration.InvalidName` |

## State Transitions

```
[DRAFT] --Game.Create(valid)--> [DRAFT] --Start()--> [READY] --Start()--> [WAITING_FOR_PLAYERS] → ...
                         \--invalid--> Result.Fail (no persist)
[WAITING_FOR_PLAYERS|IN_PROGRESS|*] --UpdateConfiguration()--> FAIL (ConfigurationImmutable)
```

Solo `DRAFT`/`READY` permiten correcciones futuras (si se habilita en spec posterior); todo estado ≥ `WAITING_FOR_PLAYERS` bloquea configuración.
