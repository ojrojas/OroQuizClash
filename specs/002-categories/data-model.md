# Data Model: Categories

**Feature**: `002-categories` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Entities

### Category (AggregateRoot<CategoryId>)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `CategoryId : StronglyTypedId<Guid>` | PK, immutable | BuildingBlocks |
| `Name` | `CategoryName : ValueObject(string)` | 3–100 chars, no vacío, trim | FR-001 |
| `Description` | `string` | 0–500 chars, trim | FR-001 |
| `KnowledgeArea` | `KnowledgeArea : ValueObject(string)` | 2–100 chars, no vacío | FR-001 |
| `AcademicLevel` | `AcademicLevel : ValueObject(string)` | 2–100 chars, no vacío (Primaria, Secundaria...) | FR-001 |
| `AgeRange` | `AgeRange : ValueObject(min,max)` | `min≥0 max≤120 min≤max` | FR-002 |
| `DifficultyLevel` | `DifficultyLevel : Enumeration(1..5)` | `1..5` | FR-001 |
| `Tags` | `CategoryTags : ValueObject(Set<string>)` | ≤10, cada tag 2–30 lowercased deduplicado | FR-001 |
| `PublishConfiguration` | `PublishConfiguration : ValueObject` | opcional, owned | FR-001 |
| `Status` | `CategoryStatus : Enumeration` | `DRAFT(1),ACTIVE(2),INACTIVE(3),ARCHIVED(4)` | FR-003 |
| `RowVersion` | `byte[]` | `rowversion` / `IsConcurrencyToken()` | FR-003 |
| `CreatedAt` | `DateTimeOffset` | set en `Create` | audit |
| `CreatedBy` | `Guid` (sub claim) | FK lógico a OroIdentityServer `sub` | VI |
| Domain Events | `CategoryCreated/Published/ArchivedDomainEvent` | dispatch en `SaveChanges` | BuildingBlocks |

**Behavior**: `static Result<Category> Create(CategoryName, KnowledgeArea, ...)`; `Result Update(...)` (solo DRAFT/INACTIVE); `Result Activate()` (`DRAFT/INACTIVE→ACTIVE`); `Result Deactivate()` (`ACTIVE→INACTIVE`); `Result Publish(IQuestionCounter)` (gate ≥5 válidas → `ACTIVE`); `Result Archive()` (`INACTIVE/ACTIVE→ARCHIVED`). Usa `CheckRule`/`Result.Failure`.

**Invariants**:
- `Name` 3–100, `AgeRange` coherente, `Tags` normalizados.
- `Publish` solo si `CountValidQuestions() ≥5` (FR-005).
- `Update` prohibido en `ACTIVE`/`ARCHIVED`.
- Transiciones inválidas → `InvalidCategoryState`.

### Question (External, SPEC-003)

| Field | Type | Constraints |
|-------|------|-------------|
| `Id` | `QuestionId` | — |
| `CategoryId` | `CategoryId` | FK lógico |
| `Status` | `Enumeration` | `Active` para contar |
| `AnswerOptions` | `List<AnswerOption>` | `Count==4` |
| `IsCorrect` | `bool` por option | `Count(IsCorrect)==1` |
| `Difficulty` | `int 1..5` | debe alinearse a `Category.DifficultyLevel` (compatible) |
| `AcademicLevel/AgeRange` | `VO` | compatibles con `Category` |

**Validez**: `AnswerOptions.Count==4 && ExactlyOneCorrect && Status==Active && CategoryId igual && Difficulty/AcademicLevel/AgeRange compatibles` (FR-006/007). Conteo vía `IQuestionCounter.CountValidAsync(CategoryId)`.

## ValueObjects / Enumerations

- **CategoryStatus**: `DRAFT(1), ACTIVE(2), INACTIVE(3), ARCHIVED(4)` — `Enumeration<CategoryStatus>`, `GetAll()`, `FromId()`, `FromName()`, `IsTerminal(ARCHIVED)`, `CanPublishFrom(DRAFT,INACTIVE)`.
- **AgeRange**: `ValueObject(min,max)` — `GetEqualityComponents` yield min,max; `IsCompatible(AgeRange other)` checks overlap; `Contains(age)`.
- **CategoryTags**: `ValueObject` — `IReadOnlySet<string>` lowercased; `GetEqualityComponents` yield sorted tags joined.
- **DifficultyLevel**: `Enumeration` 1..5 (Básico..Experto) o `int` con `FromId`.
- **KnowledgeArea/AcademicLevel**: `ValueObject(string)` 2–100.
- **PublishConfiguration**: `ValueObject` (ej. `RequiresModeration bool`, `AutoPublish bool`).

## Business Rules (IBusinessRule)

- `CategoryNameNotEmptyRule(name)` — 3–100
- `AgeRangeCoherentRule(min,max)` — 0–120, min≤max
- `CategoryTagsValidRule(tags)` — ≤10, 2–30, deduplicados
- `CategoryMustHaveFiveValidQuestionsRule(count)` — `count≥5`
- `CategoryStateTransitionRule(from,to)` — matriz estados
- `CategoryCanUpdateRule(status)` — solo DRAFT/INACTIVE

Uso: `CheckRule(new XRule(...))` dentro de `Category.Publish`.

## Relationships

```
Category (1) ── HasMany (logical) ──> Question (SPEC-003)  // no FK física cross-aggregate si se desacopla
Category (1) ──owns──> AgeRange (1) — owned type
Category (1) ──owns──> PublishConfiguration (1) — owned type
Category (1) ──owns──> Tags (collection, via converter)
Category ──emits──> CategoryCreated/Published/ArchivedDomainEvent
```

## Persistence Mapping (EF Core)

- `OroQuizClashDbContext : AppDbContextBase` con `DbSet<Category>` (ya tiene `DbSet<Game>` de 001).
- `CategoryTypeConfiguration : IEntityTypeConfiguration<Category>`:
  - `HasKey(c => c.Id)` con `StronglyTypedId` converter (`CategoryId.Value`).
  - `Property(c => c.RowVersion).IsRowVersion().IsConcurrencyToken()`.
  - `Property(c => c.Status).HasConversion(s=>s.Id, id=>CategoryStatus.FromId(id))`.
  - `OwnsOne(c => c.AgeRange, ab=>{ ab.Property(a=>a.Min).HasColumnName("AgeMin"); ab.Property(a=>a.Max).HasColumnName("AgeMax"); })`
  - `Property(c => c.Tags).HasConversion(tags=>string.Join(",",tags), csv=>csv.Split(...))` o tabla owned collection.
  - `HasIndex(c => c.Status)`, `HasIndex(c=>c.KnowledgeArea)`.
  - `ApplyConfiguration(new OutboxEntityTypeConfiguration())` ya en DbContext.
- Índices: `IX_Category_Status`, `IX_Category_KnowledgeArea_AcademicLevel`.

## Validation Rules Summary

| Rule | Field(s) | Valid | Invalid | Error |
|------|----------|-------|---------|-------|
| Name 3–100 | Name | 3–100 | vacío/<3 | `InvalidCategoryConfiguration.InvalidName` |
| AgeRange | min/max | 0–120 min≤max | min>max/<0/>120 | `InvalidCategoryConfiguration.InvalidAgeRange` |
| Tags | tags | ≤10 2–30 lowercased | >10/vacío | `InvalidCategoryConfiguration.InvalidTags` |
| Gate ≥5 | CountValid | ≥5 | <5 | `CategoryNotPublishable` / `CategoryNotReady` |
| State | from→to | DRAFT→ACTIVE etc. | ARCHIVED→Publish | `InvalidCategoryState` |
| Difficulty | 1..5 | 1..5 | 0/6 | `InvalidCategoryConfiguration.InvalidDifficulty` |

## State Transitions

```
[DRAFT] --Update()--> [DRAFT] (edit)
[DRAFT] --Publish(IQuestionCounter≥5)--> [ACTIVE]
[DRAFT] --Activate()--> [ACTIVE] (sin gate, opcional)
[DRAFT] --Archive()--> [ARCHIVED]
[ACTIVE] --Deactivate()--> [INACTIVE]
[ACTIVE] --Archive()--> [ARCHIVED]
[INACTIVE] --Activate()--> [ACTIVE]
[INACTIVE] --Update()--> [INACTIVE]
[INACTIVE] --Publish(≥5)--> [ACTIVE]
[INACTIVE] --Archive()--> [ARCHIVED]
[ARCHIVED] --*--> FAIL (terminal)
[ACTIVE] --Update()--> FAIL (requiere Deactivate)
```

 Solo `DRAFT`/`INACTIVE` permiten `Update`; `Publish` exige `DRAFT`/`INACTIVE` + ≥5.

## IQuestionCounter Port

```csharp
public interface IQuestionCounter
{
    Task<int> CountValidAsync(CategoryId categoryId, CancellationToken ct);
}
```

- `InMemoryQuestionCounter` (stub para 002): `Dictionary<CategoryId, List<QuestionStub>>` con `IsValid` check.
- `EfQuestionCounter` (SPEC-003): query `IRepository<Question,QuestionId>` + `ValidQuestionSpecification(CategoryId)`.

