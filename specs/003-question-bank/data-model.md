# Data Model: Question Bank

**Feature**: `003-question-bank` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Entities

### Question (AggregateRoot<QuestionId>)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `QuestionId : StronglyTypedId<Guid>` | PK, immutable, Guid | BuildingBlocks |
| `Text` | `QuestionText : ValueObject(string)` | 3–500 chars, no vacío, trim | FR-009, QST-001..004 validación aplicación |
| `CategoryId` | `CategoryId : StronglyTypedId<Guid>` | FK lógico, required, must exist & not ARCHIVED | FR-003, QST-003 |
| `Difficulty` | `DifficultyLevel : Enumeration(1..5)` | 1..5 required | FR-004, QST-004 |
| `AcademicLevel` | `AcademicLevel : ValueObject(string)` | 2–100 chars, no vacío (Primaria, Secundaria, Universidad...) | FR-009 |
| `AgeRange` | `AgeRange : ValueObject(min,max)` | `min≥0 max≤120 min≤max` | FR-009 |
| `Status` | `QuestionStatus : Enumeration` | `DRAFT(1), ACTIVE(2), PUBLISHED(3), INACTIVE(4), ARCHIVED(5)` | FR-008, QST-005/006 |
| `AnswerOptions` | `IReadOnlyList<AnswerOption>` | Exactly 4, composition owned | FR-001/002, QST-001/002 |
| `RowVersion` | `byte[]` | `rowversion` / `IsConcurrencyToken()` | FR-008, F |
| `CreatedAt` | `DateTimeOffset` | set en `Create` | audit |
| `UpdatedAt` | `DateTimeOffset` | set en `Update/Publish/Activate` | audit |
| `PublishedAt` | `DateTimeOffset?` | set en `Publish` | FR-006 |
| `CreatedBy` | `Guid` (sub claim) | FK lógico a OroIdentityServer `sub` | VI |
| Domain Events | `QuestionCreatedDomainEvent`, `QuestionUpdatedDomainEvent`, `QuestionPublishedDomainEvent`, `QuestionDeactivatedDomainEvent`, `QuestionArchivedDomainEvent` | dispatch en `SaveChanges` | BuildingBlocks |

**Behavior**: `static Result<Question> Create(QuestionText, CategoryId, DifficultyLevel, AcademicLevel, AgeRange, IReadOnlyList<AnswerOptionData>)` valida QST-001..004 via `CheckRule`; `Result Update(QuestionText, CategoryId, Difficulty, AcademicLevel, AgeRange, options)` solo DRAFT/INACTIVE (o PUBLISHED si mantiene 4/1); `Result Activate()` (DRAFT/INACTIVE→ACTIVE); `Result Deactivate()` (ACTIVE/PUBLISHED→INACTIVE); `Result Publish()` (DRAFT/ACTIVE→PUBLISHED gate QST-001..004); `Result Archive()` (ACTIVE/PUBLISHED/INACTIVE→ARCHIVED terminal); `Result SetCorrectAnswer(AnswerOptionId)` garantiza exactamente 1 (uso interno de `Update`). Todas retornan `Result` con `Error` tipificado y aplican `IBusinessRule`.

**Invariants**:
- Exactly 4 `AnswerOption`, exactly 1 `IsCorrect==true` (FR-001/002, DB CHECK).
- `CategoryId` required & exists (FR-003).
- `Difficulty` 1..5 required (FR-004).
- `PUBLISHED` no puede quedar con 0/>1 correctas (QST-005 → FR-005).
- Solo `PUBLISHED` (y `ACTIVE` si se distingue) con invariantes superadas es seleccionable (QST-006 → FR-006).
- `Update` prohibido en `ARCHIVED`; `PUBLISHED` update solo si mantiene 4/1.
- Transiciones inválidas → `InvalidQuestionState`.

### AnswerOption (Entity<AnswerOptionId> dentro de Question)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `AnswerOptionId : StronglyTypedId<Guid>` | PK dentro agregado, immutable | composition |
| `QuestionId` | `QuestionId` | FK a Question, cascade delete | owned |
| `Text` | `string` | 1–500 chars, no vacío, trim, no duplicado exacto dentro misma Question (aplicación) | FR-010 |
| `IsCorrect` | `bool` | exactamente 1 true por Question | FR-010, QST-002, DB CHECK |
| `DisplayOrder` | `int` | 0..3 → A-D, único por Question | FR-010 |
| `CreatedAt` | `DateTimeOffset` | set en agregado Create | — |

**Behavior**: No expone mutación directa; solo vía `Question` aggregate (`Question.Update` recrea/actualiza colección preservando Ids donde sea posible; `Question.SetCorrectAnswer` itera y setea single true). Composition: `Question` owns `AnswerOptions` (EF `OwnsMany` o `HasMany` con `WithOwner`).

**Invariante composición**: `AnswerOptions.Count==4` y `ExactlyOneCorrect` siempre; DB `CHECK CK_ExactlyOneCorrectPerQuestion` refuerza.

### Category (External, SPEC-002 — ya modelado, referencia aquí)

| Field | Type | Constraints |
|-------|------|-------------|
| `Id` | `CategoryId` | — |
| `Status` | `CategoryStatus : Enumeration` | `DRAFT, ACTIVE, INACTIVE, ARCHIVED` |
| `DifficultyLevel` | `DifficultyLevel` | 1..5 para alineación |
| `AcademicLevel` | `ValueObject(string)` | 2–100 |
| `AgeRange` | `AgeRange VO` | 0–120 |
| `ValidQuestionsCount` | derivado | `CountValidQuestions() ≥5` gate |

**Relación**: `Question.CategoryId` → `Category.Id` lógica (no FK física cross-aggregate requerida; validada via `ICategoryExistenceChecker` + `ValidQuestionSpecification` para conteo). `IQuestionCounter.CountValidAsync(CategoryId)` usa `Specification<Question>` de este feature para gate SPEC-002.

### QuestionSelectionCriteria (ValueObject / DTO de consulta, no persistido)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `CategoryId` | `CategoryId?` | optional | FR-013 |
| `Difficulty` | `DifficultyLevel?` | optional 1..5 | FR-013 |
| `AcademicLevel` | `string?` | optional 2–100 | FR-013 |
| `AgeRange` | `AgeRange?` | optional 0–120 | FR-013 |
| `PreviousQuestionIds` | `IReadOnlyList<QuestionId>` | required (puede estar vacía), hasta 50+ | FR-013, exclusión PreviousQuestions |
| `GameId` | `GameId : StronglyTypedId<Guid>` | required | FR-013, contexto Game |
| `RoundId` / `RoundNumber` | `Guid/int?` | required para idempotencia Round | FR-013 |
| `Take` | `int` | 1 por defecto, configurable para batch | 002 usaba 1 |

Usado por `IQuestionSelectionStrategy.SelectAsync(criteria)` y por `QuestionSelectionSpecification` (composición Where).

## ValueObjects / Enumerations

- **QuestionStatus**: `DRAFT(1), ACTIVE(2), PUBLISHED(3), INACTIVE(4), ARCHIVED(5)` — `Enumeration<QuestionStatus>`, `GetAll()`, `FromId()`, `FromName()`, `CanTransitionTo(to)`, `IsAvailableForSelection` (solo `PUBLISHED`), `IsTerminal(ARCHIVED)`.
- **QuestionText**: `ValueObject(string)` 3–500, `GetEqualityComponents` yield `Text.Trim()`.
- **DifficultyLevel**: `Enumeration` 1..5 (Básico=1, Elemental=2, Intermedio=3, Avanzado=4, Experto=5) o `int` con `FromId`; compartido con Category/Game; `FromId(int)`, `GetAll()`.
- **AcademicLevel**: `ValueObject(string)` 2–100 (Primaria, Secundaria, Bachillerato, Universidad, Postgrado); `GetEqualityComponents` yield normalized lowercased.
- **AgeRange**: `ValueObject(min,max)` — `GetEqualityComponents` yield min,max; `IsCompatible(AgeRange other)` verifica `Max>=other.Min && Min<=other.Max` (solapamiento); `Contains(age)`, `IsValid()` 0–120 min≤max.
- **AnswerOptionText**: `ValueObject(string)` 1–500, no vacío, trim.
- **DisplayOrder**: `int` 0..3, map A=0 B=1 C=2 D=3.
- **QuestionSelectionCriteria**: `ValueObject` (ver arriba), igualdad por componentes (orden PreviousIds no relevante — se sortea para GetEquality).

## Business Rules (IBusinessRule)

- `QuestionMustHaveFourOptionsRule(int count)` — `count==4` else `QuestionMustHaveFourOptions`.
- `ExactlyOneCorrectAnswerRule(int correctCount)` — `correctCount==1` else `QuestionMustHaveOneCorrectAnswer`.
- `QuestionMustBelongToCategoryRule(CategoryId? id, bool exists)` — required & exists else `QuestionMustBelongToCategory/CategoryNotFound`.
- `QuestionMustHaveDifficultyRule(DifficultyLevel? difficulty)` — 1..5 else `QuestionMustHaveDifficulty`.
- `PublishedQuestionMustHaveCorrectRule(QuestionStatus status, int correctCount)` — if `PUBLISHED` then correct==1 else `PublishedQuestionMustHaveCorrectAnswer` (QST-005).
- `QuestionStateTransitionRule(QuestionStatus from, QuestionStatus to)` — matriz estados (ver below) else `InvalidQuestionState`.
- `QuestionCanUpdateRule(QuestionStatus status)` — solo DRAFT/INACTIVE (y PUBLISHED si mantiene 4/1 según flag) else `InvalidQuestionState`.
- `AgeRangeCoherentRule(int min,int max)` — 0–120 min≤max else `InvalidAgeRange`.
- `AcademicLevelValidRule(string level)` — 2–100 else `InvalidAcademicLevel`.
- `CategoryExistsRule(bool exists)` — para `ICategoryExistenceChecker`.
- `ValidQuestionForCategoryRule(Question)` — `IsValid` ⇔ 4/1 + PUBLISHED/ACTIVE + CategoryId igual + Difficulty/AcademicLevel/AgeRange compatibles (usada por `IQuestionCounter`).

Uso: `CheckRule(new XRule(...))` dentro de `Question.Create/Update/Publish`; cada regla expone `Error` con `Code` y `Type`.

## Relationships

```
Question (1) ──HasMany(composition, cascade)──> AnswerOption (4) // FK QuestionId, owned table AnswerOptions
Question (1) ── HasOne (logical FK) ──> Category (1) // Question.CategoryId, validated via ICategoryExistenceChecker
Question (1) ── owns ──> AgeRange (1) — owned type (AgeMin, AgeMax columns)
Question (1) ── emits ──> QuestionCreated/Updated/Published/Deactivated/ArchivedDomainEvent
Question (1) ── queried by ──> ValidQuestionSpecification, QuestionFilterSpecification, QuestionSelectionSpecification
QuestionSelectionCriteria (VO) ── used by ──> IQuestionSelectionStrategy.SelectAsync → returns List<Question>
Category (1) ── counted by ──> IQuestionCounter.CountValidAsync(CategoryId) → ValidQuestionSpecification over Question
```

## Persistence Mapping (EF Core)

- `OroQuizClashDbContext : AppDbContextBase` con `DbSet<Question>` (ya tiene `DbSet<Category>` 002 + `DbSet<Game>` 001).
- `QuestionTypeConfiguration : IEntityTypeConfiguration<Question>`:
  - `HasKey(q => q.Id)` con converter `QuestionId.Value` (`HasConversion(id=>id.Value, guid=>QuestionId.From(guid))`).
  - `Property(q => q.RowVersion).IsRowVersion().IsConcurrencyToken()`.
  - `Property(q => q.Status).HasConversion(s=>s.Id, id=>QuestionStatus.FromId(id))` + `HasColumnName("StatusId")`.
  - `Property(q => q.Difficulty).HasConversion(d=>d.Id, id=>DifficultyLevel.FromId(id))` + `HasColumnName("DifficultyId")`.
  - `Property(q => q.Text).HasColumnName("Text").HasMaxLength(500).IsRequired()`.
  - `OwnsOne(q => q.AgeRange, ab=>{ ab.Property(a=>a.Min).HasColumnName("AgeMin"); ab.Property(a=>a.Max).HasColumnName("AgeMax"); })` o `ComplexProperty`.
  - `Property(q => q.AcademicLevel).HasConversion(l=>l.Value, s=>AcademicLevel.From(s)).HasMaxLength(100)`.
  - `Property(q => q.AgeRange)` owned ya; `Property(q => q.CategoryId).HasConversion(id=>id.Value, g=>CategoryId.From(g)).HasColumnName("CategoryId").IsRequired()`.
  - `HasMany(q => q.AnswerOptions).WithOne().HasForeignKey("QuestionId").OnDelete(DeleteBehavior.Cascade)` + `OwnsMany` alternative: `HasMany<AnswerOption>` table `AnswerOptions` con `HasKey(a=>a.Id)` + `Property(a=>a.Text).HasMaxLength(500).IsRequired()` + `Property(a=>a.IsCorrect).IsRequired()` + `Property(a=>a.DisplayOrder).IsRequired()` + `HasIndex(a=> new {a.QuestionId, a.DisplayOrder}).IsUnique()` + `HasIndex(a=> a.QuestionId)`.
  - `HasIndex(q => new {q.CategoryId, q.Status})`, `HasIndex(q => q.Difficulty)`, `HasIndex(q => q.Status).HasFilter("[Status]=3")` (PUBLISHED filtered), `HasIndex(q => q.AcademicLevel)`, `HasIndex(q => new {q.CategoryId, q.Status, q.Difficulty})` for selection.
  - Constraint: `HasCheckConstraint("CK_Question_ExactlyOneCorrect", "(SELECT COUNT(*) FROM AnswerOptions WHERE AnswerOptions.QuestionId = Questions.Id AND IsCorrect = 1) = 1")` → implementado como `CREATE TRIGGER` o EF `HasCheckConstraint` si AnswerOptions JSON; alternativa: migración raw SQL `CHECK` + `TRIGGER` para ExactlyOneCorrect; se documenta en migración `AddQuestionTable`.
  - `ApplyConfiguration(new OutboxEntityTypeConfiguration())` ya en DbContext; `SaveChanges` participa domain events + Outbox transaction.
- Índices adicionales: `IX_AnswerOptions_QuestionId_IsCorrect`, `IX_Question_CategoryId_Status_Difficulty`.
- `Question` soft no-delete; `Archive` es estado terminal, no `IsDeleted`.

## Validation Rules Summary

| Rule | Field(s) | Valid | Invalid | Error Code | HTTP |
|------|----------|-------|---------|------------|------|
| Text 3–500 | Text | 3–500 no vacío | vacío/2/501 | `InvalidQuestionConfiguration.InvalidText` | 400 |
| Exactly 4 options | AnswerOptions | Count==4 | 3/5 | `QuestionMustHaveFourOptions` | 400 |
| Exactly 1 correct | IsCorrect | Count(IsCorrect)==1 | 0/2 | `QuestionMustHaveOneCorrectAnswer` | 400 |
| Category required | CategoryId | Guid required & exists & not ARCHIVED | null/missing/archived | `QuestionMustBelongToCategory` / `CategoryNotFound` | 400/404 |
| Difficulty 1..5 | Difficulty | 1..5 | 0/6/null | `QuestionMustHaveDifficulty` | 400 |
| AgeRange | min/max | 0–120 min≤max | min>max/<0/>120 | `InvalidAgeRange` | 400 |
| AcademicLevel | level | 2–100 no vacío | vacío/1 char | `InvalidAcademicLevel` | 400 |
| AnswerOption.Text | Text | 1–500 no vacío, no duplicado exacto | vacío/>500/duplicado | `InvalidAnswerOption.InvalidText` / `DuplicateAnswerOption` | 400 |
| Publish gate | Status+DRAFT/ACTIVE→PUBLISHED | 4/1 + CategoryId + Difficulty + valid Academic/Age | 3 ops/0 correct | `QuestionNotPublishable` / `QuestionNotValidated` | 400 |
| Published keep correct | PUBLISHED Update | mantiene 1 correcta | deja 0/2 | `PublishedQuestionMustHaveCorrectAnswer` | 400 |
| State transition | from→to | DRAFT→ACTIVE etc. (ver below) | ARCHIVED→Publish etc. | `InvalidQuestionState` | 400/409 |
| Selection available | Status | PUBLISHED (or ACTIVE if unified) | DRAFT/INACTIVE/ARCHIVED | `NoAvailableQuestion` (excluida) | 404 |
| Concurrency | RowVersion | matches DB | stale | `ConcurrencyConflict` | 409 |

## State Transitions

```
[DRAFT] --Update(valid 4/1)--> [DRAFT]
[DRAFT] --Activate()--> [ACTIVE]
[DRAFT] --Publish(gate 4/1 + category/difficulty)--> [PUBLISHED] (+PublishedAt, emits QuestionPublishedDomainEvent, now selectable)
[DRAFT] --Deactivate()--> FAIL (must be ACTIVE/PUBLISHED)
[DRAFT] --Archive()--> [ARCHIVED] (terminal, no Publish/Update after)
[ACTIVE] --Update(valid 4/1)--> [ACTIVE] (or FAIL if policy strict — flag, default allow)
[ACTIVE] --Publish(gate)--> [PUBLISHED]
[ACTIVE] --Deactivate()--> [INACTIVE]
[ACTIVE] --Archive()--> [ARCHIVED]
[PUBLISHED] --Update(valid 4/1, keep 1 correct)--> [PUBLISHED] (QST-005, or FAIL if strict immutability flag)
[PUBLISHED] --Deactivate()--> [INACTIVE] (no longer selectable, no longer counts for Category gate)
[PUBLISHED] --Activate()--> FAIL (already published; Activate is for INACTIVE→ACTIVE)
[PUBLISHED] --Publish()--> FAIL (already published → 409/400)
[PUBLISHED] --Archive()--> [ARCHIVED]
[INACTIVE] --Update(valid 4/1)--> [INACTIVE]
[INACTIVE] --Activate()--> [ACTIVE]
[INACTIVE] --Publish(gate)--> [PUBLISHED]
[INACTIVE] --Deactivate()--> FAIL
[INACTIVE] --Archive()--> [ARCHIVED]
[ARCHIVED] --*--> FAIL (terminal, requires explicit unarchive spec future)
```

Solo `DRAFT`/`INACTIVE` (y condicionalmente `PUBLISHED` con 4/1) permiten `Update`; `Publish` exige `DRAFT`/`ACTIVE`/`INACTIVE` + gate; `Archive` desde `ACTIVE`/`PUBLISHED`/`INACTIVE`.

## IQuestionCounter & IQuestionSelectionStrategy Ports

```csharp
public interface IQuestionCounter
{
    Task<int> CountValidAsync(CategoryId categoryId, CancellationToken ct);
    // Valid ⇔ Status==PUBLISHED (or ACTIVE+PUBLISHED if unified) && AnswerOptions.Count==4 && ExactlyOneCorrect && CategoryId matches && Difficulty/AcademicLevel/AgeRange compatibles
}
public interface IQuestionSelectionStrategy
{
    Task<Result<IReadOnlyList<Question>>> SelectAsync(QuestionSelectionCriteria criteria, CancellationToken ct);
}
public sealed record QuestionSelectionCriteria(
    CategoryId? CategoryId,
    DifficultyLevel? Difficulty,
    string? AcademicLevel, // or AcademicLevel VO
    AgeRange? AgeRange,
    IReadOnlyList<QuestionId> PreviousQuestionIds,
    GameId GameId,
    int? RoundNumber,
    int Take = 1
);
```

- `EfQuestionCounter` (para SPEC-002 `Category.Publish`): `IRepository<Question,QuestionId>` + `ValidQuestionSpecification(CategoryId)` (`Where Status==PUBLISHED && ValidRule IsSatisfied`).
- `RandomQuestionSelectionStrategy` (default): `IRepository<Question,QuestionId>` + `QuestionSelectionSpecification(criteria)` + `OrderByRandom` (`Guid.NewGuid()` or `ORDER BY NEWID()`) + `Take(criteria.Take)`.

## Specifications

- `ValidQuestionSpecification(CategoryId)` — `Where(q => q.Status==PUBLISHED && q.AnswerOptions.Count==4 && q.AnswerOptions.Count(a=>a.IsCorrect)==1 && q.CategoryId==categoryId && academic/difficulty/ageRange compatibles)` (para `CountValid`).
- `QuestionFilterSpecification(CategoryId?, Difficulty?, AcademicLevel?, AgeRange?, Status?, searchText?)` — combinada con `And` para `GetQuestions` Query + paginación `Skip/Take` + `OrderBy(CreatedAt desc)`.
- `QuestionSelectionSpecification(QuestionSelectionCriteria)` — `Where(Status==PUBLISHED)` + optional `CategoryId` + optional `Difficulty` + optional `AcademicLevel` + optional `AgeRange` + `Where(!PreviousQuestionIds.Contains(Id))` + `AsNoTracking` + `OrderByRandom/Take` (para motor).
- Todas heredan `Specification<Question>` BuildingBlocks (`Where`, `And`, `Or`, `Not`, `Include`, `OrderBy`, `Paginate`).

## Audit & Domain Events

- `QuestionCreatedDomainEvent(QuestionId, CategoryId, Difficulty)`, `QuestionUpdatedDomainEvent(QuestionId)`, `QuestionPublishedDomainEvent(QuestionId, CategoryId)`, `QuestionDeactivatedDomainEvent(QuestionId)`, `QuestionArchivedDomainEvent(QuestionId)` — dispatch in `AppDbContextBase.SaveChanges` (in-process). `QuestionPublished` puede proyectar `Category.ValidQuestionsCount` si se usa denormalización futura.
- Audit append-only: tabla `AuditLog` con `CorrelationId`, `QuestionId`, `CategoryId`, `GameId?`, `PerformedBy (sub)`, `Command (Create/Update/Publish/Select)`, `Timestamp`, `Before/After` snapshot (no muta histórico).
- OTel via `BuildingBlocks.ServiceDefaults`: logs con `TraceId`, `QuestionId`, `CategoryId`, `GameId`, `RoundId`.

