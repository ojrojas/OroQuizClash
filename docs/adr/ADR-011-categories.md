# ADR-011: Categories

**Status**: Accepted
**Date**: 2026-08-26
**Deciders**: Architecture Team

## Context
Necesidad de gestionar categorías de conocimiento con ciclo de vida DRAFT→ACTIVE→INACTIVE→ARCHIVED, gate de publicación ≥5 preguntas válidas (4 opciones, 1 correcta, activa, alineada), filtrado por área/nivel/edad/dificultad/tags, y concurrencia optimista.

## Decision

### 1. AggregateRoot + ValueObjects
- **Category : AggregateRoot<CategoryId>** con `CategoryId : StronglyTypedId<Guid>`
- **ValueObjects inmutables**: `CategoryName` (3-100), `KnowledgeArea` (string 2-100), `AcademicLevel` (string 2-100), `AgeRange(min,max)` (0-120, min≤max), `DifficultyLevel` (Enumeration 1..5), `CategoryTags` (Set<string> normalizado lowercase/trim/deduplicado, ≤10, 2-30 chars), `PublishConfiguration` (requiresModeration)
- **Enumerations**: `CategoryStatus : Enumeration<DRAFT=1, ACTIVE=2, INACTIVE=3, ARCHIVED=4>`
- Sin setters públicos; mutaciones vía `Create()`, `Update()`, `Activate()`, `Deactivate()`, `Publish(IQuestionCounter)`, `Archive()` retornando `Result`
- **Rationale**: Constitución I-II (Domain First, Clean Arch) + B (invariantes) exigen protección de invariantes en agregado; `ValueObject` garantiza igualdad estructural; `Enumeration` aporta comportamiento rico vs `enum` nativo y es persistible via `FromId`

### 2. Estados y Transiciones
- Máquina de estados explícita:
  - `DRAFT → ACTIVE` (vía `Publish` con gate ≥5 o `Activate` sin gate administrativo)
  - `ACTIVE ↔ INACTIVE` (`Deactivate`/`Activate`)
  - `INACTIVE/ACTIVE → ARCHIVED` (terminal salvo reactivación futura fuera de alcance)
  - `DRAFT → ARCHIVED` permitido
  - `Update` solo en `DRAFT`/`INACTIVE`
- Transiciones inválidas → `Error InvalidCategoryState` (`ErrorType.Validation` → 400/409)
- Protegidas por `rowversion` (`[Timestamp]` / `IsRowVersion`) para concurrencia optimista
- **Rationale**: Spec casos `Activate/Deactivate/Publish/Archive` + FR-003; constitución F exige `rowversion` para transiciones de estado; segundo `Publish` concurrente debe dar `409`

### 3. Gate Publish ≥5 Válidas y Definición de Válida
- `Category.Publish(IQuestionCounter counter)` cuenta vía `counter.CountValidAsync(CategoryId)`
- Válido ⇔ `AnswerOptions.Count==4 && ExactlyOne IsCorrect && Question.Status==Active && CategoryId igual && Difficulty/AcademicLevel/AgeRange compatibles` (FR-006/007)
- Si `<5` → `Result.Failure(CategoryErrors.NotPublishable)`
- `IQuestionCounter` abstrae SPEC-003: implementación `InMemoryQuestionCounter` stub (diccionario `CategoryId → List<QuestionStub>`) para `002`; en `SPEC-003` se reemplaza por `EfQuestionCounter` con `Specification<Question>` + `IsSatisfiedBy`
- **Rationale**: Constitución B (`≥5`, 4 opciones/1 correcta/activa) y FR-005/006 no-negociables; desacoplar conteo evita que `Category` conozca `Question` aggregate (bounded contexts separados). `IQuestionCounter` mantiene Clean Arch (Domain define port, Infrastructure implementa)

### 4. AgeRange y Tags
- `AgeRange : ValueObject(min,max)` con `min≥0 max≤120 min≤max`, igualdad por componentes, `IsCompatible(AgeRange questionAge)` verifica solapamiento
- `CategoryTags : ValueObject` envuelve `IReadOnlySet<string>` normalizado `lowercase+trim+deduplicado`, `≤10` tags, cada tag `2–30` chars; `GetEqualityComponents` ordena para igualdad determinística
- **Rationale**: Spec alcance `Edad mínima/máxima`, `Tags`; constitución pide ValueObjects para conceptos sin identidad; normalización evita duplicados case-insensitive

### 5. Persistencia y Especificaciones
- `OroQuizClashDbContext : AppDbContextBase` extendido con `DbSet<Category>`
- `CategoryTypeConfiguration : IEntityTypeConfiguration<Category>` con:
  - `HasKey(CatId→Guid)`
  - `OwnsOne(AgeRange)`, `OwnsOne(PublishConfiguration)`
  - `Property(RowVersion).IsRowVersion()`
  - `HasConversion` para `CategoryStatus`/`DifficultyLevel`
  - `HasIndex(Status)`, `HasIndex(KnowledgeArea, AcademicLevel)`
- Filtros vía `CategoryFilterSpecification : Specification<Category>` con `Where` combinados para `knowledgeArea/academicLevel/ageRange/difficulty/state/tags` + paginación
- **Rationale**: Constitución E (SQL Server primario, `AppDbContextBase`+Outbox en misma transacción, `Specification` para queries) y FR-009; `rowversion` para concurrencia `Publish`

### 6. CQRS Vertical Slice
- Cada caso en `Features/Categories/` con:
  - `*Command : ICommand<Result<*Response>>` (o `IQuery`)
  - `*Validator : IValidator<Command>` (BuildingBlocks `ValidationBehavior`)
  - `*Handler : ICommandHandler<Command,Result>`
  - `*Response` DTO
  - `*Endpoint : IEndpoint` thin (`ISender.SendAsync→Result.ToHttpResult()`)
- Ej. `CreateCategoryCommand(Name,Description,KnowledgeArea,AcademicLevel,AgeMin,AgeMax,DifficultyLevel,Tags)` → `PublishCategoryCommand(CategoryId)`
- **Rationale**: Constitución IV + III (no MediatR/AutoMapper) + research `001-game-configuration`; slice autocontenido bajo `Features/` facilita tests y evita carpetas genéricas

### 7. Identidad y Autorización
- Endpoints requieren `ADMIN`/`GAME_MANAGER` via JWT `roles` de OroIdentityServer (`Authority http://identity:5080`), policy `AdminOrGameManager` en `Program.cs:70-75`
- Sin user store local
- **Rationale**: Constitución VI+H

### 8. Testing
- Domain.Tests (xUnit) para `Category.Create/Publish` con stub `FakeQuestionCounter` (0-5 válidas)
- Application.Tests con NSubstitute `IRepository<Category,CategoryId>` + `IQuestionCounter`
- Infrastructure.Tests con `EfRepository` + `OroQuizClashDbContext` InMemory/Sqlite + `CategoryFilterSpecification`
- Api.Tests con `WebApplicationFactory` + JWT mock `ADMIN` (Aspire Testing si se usa AppHost)
- Concurrencia `Publish` con dos contextos paralelos → segundo `409`
- **Rationale**: Constitución Testing Strategy (Domain/Application sin Web/DB, Integration con DB real)

## Consequences
- Gate `Publish` protege invariante de calidad (≥5 preguntas válidas) — no se puede bypass
- `rowversion` garantiza que transiciones concurrentes fallan con `409 Conflict` predecible
- `IQuestionCounter` desacopla `Category` de `Question` — SPEC-003 puede evolucionar independientemente
- `Specification<Category>` permite filtrado compuesto y paginación sin duplicar lógica
- Domain events (`CategoryCreated/Updated/Published/Archived`) emitidos in-process via `AppDbContextBase.SaveChanges`

## Alternatives
- `Category` con `List<Question>` navigation: rechazado — acopla agregados, rompe boundary
- `Category` con `int ValidQuestionsCount` denormalizado: rechazado — se desincroniza, requiere event handler; posible optimización futura con proyección
- `CategoryStatus` como `enum` nativo: rechazado — pierde `FromName`/`GetAll` y conversión EF centralizada
- Permitir `Update` en `ACTIVE`: rechazado — complica gate; se usa `Deactivate→Update→Publish` para curación
- `ARCHIVED` re-publicable: rechazado — terminal por spec, simplifica
- MediatR/Sqlite sin rowversion: rechazado (prohibido por constitución, pierde concurrencia)