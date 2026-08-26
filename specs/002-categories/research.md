# Research: Categories

**Feature**: `002-categories` | **Date**: 2026-08-26 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Summary

0 `NEEDS CLARIFICATION` en spec; investigación resuelve dependencias (`SPEC-003` counting), estado `Enumeration` vs `enum`, VOs, `IQuestionCounter` abstracción, concurrencia `rowversion`, y filtrado `Specification`. Todo alineado a constitución v1.1.0 + BuildingBlocks net10.0.

## Decisions

### 1. Category AggregateRoot y ValueObjects

- **Decision**: `Category : AggregateRoot<CategoryId>` con `CategoryId : StronglyTypedId<Guid>`, `CategoryStatus : Enumeration<CategoryStatus>` (DRAFT 1, ACTIVE 2, INACTIVE 3, ARCHIVED 4), y VOs `CategoryName`, `KnowledgeArea`, `AcademicLevel`, `AgeRange(min,max)`, `DifficultyLevel`, `CategoryTags`, `PublishConfiguration`. Sin setters públicos; mutaciones vía `Create()`, `Update()`, `Activate()`, `Deactivate()`, `Publish(IQuestionCounter)`, `Archive()`.
- **Rationale**: Constitución I-II (Domain First, Clean Arch) + B (invariantes) exigen protección de invariantes en agregado; `ValueObject` garantiza igualdad estructural; `Enumeration` aporta comportamiento rico vs `enum` nativo y es persistible via `FromId`.
- **Alternatives**: `Category` como Entity mutable con setters (rechazado — expone estado, rompe invariante publish gate); `CategoryStatus` como `enum` (rechazado — pierde `FromName`/`GetAll` y conversión EF centralizada, aunque aceptable con ADR).

### 2. Estados y Transiciones

- **Decision**: Máquina de estados explícita: `DRAFT → ACTIVE` (vía `Publish` con gate o `Activate` sin gate según flujo), `ACTIVE ↔ INACTIVE` (`Deactivate`/`Activate`), `INACTIVE/ACTIVE → ARCHIVED` (terminal salvo reactivación futura fuera de alcance), `DRAFT → ARCHIVED` permitido. `Update` solo en `DRAFT`/`INACTIVE`. Transiciones inválidas → `Error InvalidCategoryState` (`ErrorType.Validation` → 400/409). Protegidas por `rowversion` (`[Timestamp]` / `IsRowVersion`).
- **Rationale**: Spec casos `Activate/Deactivate/Publish/Archive` + FR-003; constitución F exige `rowversion` para transiciones de estado; segundo `Publish` concurrente debe dar `409`.
- **Alternatives**: Permitir `Update` en `ACTIVE` (rechazado — complica gate; se usa `Deactivate→Update→Publish` para curación); `ARCHIVED` re-publicable (rechazado — terminal por spec, simplifica).

### 3. Gate Publish ≥5 Válidas y Definición de Válida

- **Decision**: `Category.Publish(IQuestionCounter counter)` cuenta vía `counter.CountValidAsync(CategoryId)`; válido ⇔ `AnswerOptions.Count==4 && ExactlyOne IsCorrect && Question.Status==Active && CategoryId igual && Difficulty/AcademicLevel/AgeRange compatibles` (FR-006/007). Si `<5` → `Result.Failure(CategoryErrors.NotPublishable)`. `IQuestionCounter` abstrae SPEC-003: implementación `InMemoryQuestionCounter` stub (diccionario `CategoryId → List<QuestionStub>`) para `002`; en `SPEC-003` se reemplaza por `EfQuestionCounter` con `Specification<Question>` + `IsSatisfiedBy`.
- **Rationale**: Constitución B (`≥5`, 4 opciones/1 correcta/activa) y FR-005/006 no-negociables; desacoplar conteo evita que `Category` conozca `Question` aggregate (bounded contexts separados). `IQuestionCounter` mantiene Clean Arch (Domain define port, Infrastructure implementa).
- **Alternatives**: `Category` con `List<Question>` navigation (rechazado — acopla agregados, rompe boundary); `Category` con `int ValidQuestionsCount` denormalizado (rechazado — se desincroniza, requiere event handler; posible optimización futura con proyección).

### 4. AgeRange y Tags

- **Decision**: `AgeRange : ValueObject(min,max)` con `min≥0 max≤120 min≤max`, igualdad por componentes, `IsCompatible(AgeRange questionAge)` verifica solapamiento. `CategoryTags : ValueObject` envuelve `IReadOnlySet<string>` normalizado `lowercase+trim+deduplicado`, `≤10` tags, cada tag `2–30` chars; `GetEqualityComponents` ordena para igualdad determinística.
- **Rationale**: Spec alcance `Edad mínima/máxima`, `Tags`; constitución pide ValueObjects para conceptos sin identidad; normalización evita duplicados case-insensitive.
- **Alternatives**: `AgeRange` como dos `int` sueltos en `Category` (rechazado — pierde invariante y reutilización); `Tags` como `string Csv` (rechazado — pierde validación y consulta por tag via `Specification`).

### 5. Persistencia y Especificaciones

- **Decision**: `OroQuizClashDbContext : AppDbContextBase` extendido con `DbSet<Category>`; `CategoryTypeConfiguration : IEntityTypeConfiguration<Category>` con `HasKey(CatId→Guid)`, `OwnsOne(AgeRange)`, `OwnsOne(PublishConfiguration)`, `Property(RowVersion).IsRowVersion()`, `HasConversion` para `CategoryStatus`/`DifficultyLevel`, `HasIndex(Status)`, `HasIndex(KnowledgeArea, AcademicLevel)`. Filtros vía `CategoryFilterSpecification : Specification<Category>` con `Where` combinados para `knowledgeArea/academicLevel/ageRange/difficulty/state/tags` + paginación.
- **Rationale**: Constitución E (SQL Server primario, `AppDbContextBase`+Outbox en misma transacción, `Specification` para queries) y FR-009; `rowversion` para concurrencia `Publish`.
- **Alternatives**: Tabla separada para `AgeRange` (rechazado — owned type más simple y transaccional); `Specification` manual `IQueryable` (rechazado — duplica framework BuildingBlocks).

### 6. CQRS Vertical Slice

- **Decision**: Cada caso en `Features/Categories/` con `*Command : ICommand<Result<*Response>>` (o `IQuery`), `*Validator : IValidator<Command>` (BuildingBlocks `ValidationBehavior`), `*Handler : ICommandHandler<Command,Result>`, `*Response` DTO, `*Endpoint : IEndpoint` thin (`ISender.SendAsync→Result.ToHttpResult()`). Ej. `CreateCategoryCommand(Name,Description,KnowledgeArea,AcademicLevel,AgeMin,AgeMax,DifficultyLevel,Tags)` → `PublishCategoryCommand(CategoryId)`.
- **Rationale**: Constitución IV + III (no MediatR/AutoMapper) + research `001-game-configuration`; slice autocontenido bajo `Features/` facilita tests y evita carpetas genéricas.
- **Alternatives**: Carpetas `Commands/Queries/Handlers` separadas (rechazado — viola Vertical Slice); `MediatR` (prohibido).

### 7. Identidad y Autorización

- **Decision**: Endpoints requieren `ADMIN`/`GAME_MANAGER` via JWT `roles` de OroIdentityServer (`Authority http://identity:5080`), policy `AdminOrGameManager` ya en `src/OroQuizClash.Api/Program.cs:61` (reusa). Sin user store local.
- **Rationale**: Constitución VI+H.
- **Alternatives**: `PLAYER` puede leer `GET /api/categories` público (considerado pero spec dice admin gestiona; lectura puede ser `AllowAnonymous` o `PLAYER` — se documenta como `[AllowAnonymous]` para `GET` si se desea, pero v1 se deja `ADMIN/GAME_MANAGER` para escritura y lectura autenticada).

### 8. Testing

- **Decision**: Domain.Tests (xUnit) para `Category.Create/Publish` con stub `FakeQuestionCounter` (0-5 válidas); Application.Tests con NSubstitute `IRepository<Category,CategoryId>` + `IQuestionCounter`; Infrastructure.Tests con `EfRepository` + `OroQuizClashDbContext` InMemory/Sqlite + `CategoryFilterSpecification`; Api.Tests con `WebApplicationFactory` + JWT mock `ADMIN` (Aspire Testing si se usa AppHost). Concurrencia `Publish` con dos contextos paralelos → segundo `409`.
- **Rationale**: Constitución Testing Strategy (Domain/Application sin Web/DB, Integration con DB real).
- **Alternatives**: Solo integration tests (rechazado — no aísla regla gate).

## Resolved Clarifications

| Topic | Resolution |
|-------|------------|
| Área vs enum | `string` 2–100, no enum cerrado (spec “Área de conocimiento” libre). |
| Update en ACTIVE | No permitido; requiere `Deactivate` primero (FR-004). |
| Contador <5 tras Publish | Categoría sigue `ACTIVE` pero re-`Publish` falla hasta reponer; no se auto-desactiva. |

## References

- `draft/constitution.md` §6 (Question Invariants), §5 States
- `draft/game-concept.md` §2-3 (Category/Question)
- `BuildingBlocks` source `src/BuildingBlocks/` (net10.0, `Enumeration`, `ValueObject`, `Specification`)
- `specs/001-game-configuration/research.md` (patrón ValueObject owned, rowversion)
