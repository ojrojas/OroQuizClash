# Research: Question Bank

**Feature**: `003-question-bank` | **Date**: 2026-08-26 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Summary

0 `NEEDS CLARIFICATION` en spec; investigación resuelve estado `QuestionStatus` vs `CategoryStatus`, `AnswerOption` como `Entity` vs `ValueObject`, validación 4/1 + QST-005/006, alineación `AcademicLevel`/`AgeRange`, selección `IQuestionSelectionStrategy` con 7 parámetros, persistencia `rowversion` + `CHECK ExactlyOneCorrect`, y wiring `IQuestionCounter` para gate `Category ≥5` (SPEC-002). Todo alineado a constitución v1.1.0 + BuildingBlocks net10.0 y a research de `001`/`002`.

## Decisions

### 1. Question AggregateRoot y AnswerOption Composition

- **Decision**: `Question : AggregateRoot<QuestionId>` con `QuestionId : StronglyTypedId<Guid>`; `AnswerOption : Entity<AnswerOptionId>` (`AnswerOptionId : StronglyTypedId<Guid>`) composición dentro del agregado (no ValueObject). `Question` expone `IReadOnlyList<AnswerOption> AnswerOptions { get; }` sin setter público; mutación solo vía `Question.Create(text, categoryId, difficulty, academicLevel, ageRange, options)`, `Update(...)`, `SetCorrectAnswer(optionId)`, `Publish()`, `Activate()`, `Deactivate()`, `Archive()`. Invariantes QST-001..004 validadas en `Create`/`Update`/`Publish` vía `IBusinessRule` + `Result`.
- **Rationale**: Constitución I-II (Domain First, Clean Arch) exige invariantes en agregado; `AnswerOption` necesita identidad para señalar `IsCorrect` estable (`SetCorrectAnswer`) y para auditoría; `Entity` permite `Id` estable y `DisplayOrder` A-D; `ValueObject` perdería identidad y complicaría reemplazo de correcta sin recrear colección. Colección fija 4 garantiza QST-001; método `SetCorrectAnswer` garantiza QST-002 atomic.
- **Alternatives**: `AnswerOption` como `ValueObject` con `Text+IsCorrect` (rechazado — sin Id estable, difícil referenciar correcta para QST-005); `Question` con `List<string>` opciones y `int CorrectIndex` (rechazado — expone estado mutable, no protege 4/1 en dominio, pierde `Entity` para EF owned).

### 2. Estados y Transiciones (QST-005, QST-006)

- **Decision**: `QuestionStatus : Enumeration<QuestionStatus>` con `DRAFT=1, ACTIVE=2, PUBLISHED=3, INACTIVE=4, ARCHIVED=5`. Máquina: `DRAFT --Update--> DRAFT`, `DRAFT --Activate--> ACTIVE`, `DRAFT/ACTIVE --Publish() [gate QST-001..004]--> PUBLISHED`, `PUBLISHED --Deactivate--> INACTIVE`, `ACTIVE/PUBLISHED/INACTIVE --Archive--> ARCHIVED` (terminal), `INACTIVE --Activate--> ACTIVE`, `INACTIVE --Update--> INACTIVE`, `INACTIVE --Publish--> PUBLISHED`. `Update` permitido solo en `DRAFT`/`INACTIVE` y opcionalmente en `PUBLISHED` si mantiene 4/1 + alineación (se documenta como `PUBLISHED` mutable solo con invariantes; alternativa estricta `PublishedQuestionImmutable` se deja como feature flag para plan futuro). Transiciones inválidas → `Error InvalidQuestionState` (409/400). Protegidas por `rowversion`. QST-005: `Update` que deje `PUBLISHED` con 0/>1 correctas → `Result.Failure(PublishedQuestionMustHaveCorrectAnswer)` sin mutar. QST-006: solo `PUBLISHED` (y `ACTIVE` si se distingue) con gate superado es seleccionable; `DRAFT/INACTIVE/ARCHIVED` excluidas en `Specification`.
- **Rationale**: Spec User Story 2/3 + FR-008 + Assumptions (DRAFT→ACTIVE→PUBLISHED con INACTIVE/ARCHIVED); constitución F exige `rowversion` para transiciones; QST-005/006 no negociables; `PUBLISHED` como estado distinto de `ACTIVE` permite distinguir validación previa vs visibilidad.
- **Alternatives**: Unificar `ACTIVE` y `PUBLISHED` en un solo estado (rechazado — pierde QST-006: validación previa vs activación; posible compat si `ACTIVE` implica validado, pero spec asume publish con gate); `PENDING_REVIEW` adicional (rechazado — fuera de alcance, podría añadirse luego sin romper).

### 3. Validación 4/1 y QST-005 (DB + Dominio)

- **Decision**: Doble validación: dominio `IBusinessRule` (`QuestionMustHaveFourOptionsRule(count==4)`, `ExactlyOneCorrectAnswerRule(options.Count(o=>o.IsCorrect)==1)`) en `Question.Create/Update/Publish`; persistencia refuerza con `CHECK CK_ExactlyOneCorrectPerQuestion` (trigger/sql `CHECK` sobre tabla `AnswerOptions` agrupada por `QuestionId` `HAVING SUM(CASE IsCorrect WHEN 1 THEN 1 ELSE 0 END)=1`) + `CHECK AnswerOptions.Count==4` vía constraint de aplicación (EF no lo modela nativo, se valida en `SaveChanges` interceptor o en `QuestionTypeConfiguration` guard). `PublishedMustHaveCorrectRule` verifica que `PUBLISHED` nunca quede con 0/>1.
- **Rationale**: Constitución E (DB debe reforzar invariantes: exactly one correct answer); dominio no basta si se muta directo en BD; `CHECK` garantiza integridad aunque se bypass dominio.
- **Alternatives**: Solo validación en dominio (rechazado — viola E); solo CHECK sin regla dominio (rechazado — mensaje error pobre ProblemDetails); usar `UNIQUE filtered index IsCorrect=1` por Question (considerado — viable con `WHERE IsCorrect=1` + unique `QuestionId`, pero CHECK es más expresivo paraExactlyOne).

### 4. Category/Difficulty/AcademicLevel/AgeRange y Alineación

- **Decision**: `Question.CategoryId : CategoryId` (FK lógico, verifica existencia vía `ICategoryExistenceChecker` port: `Task<bool> ExistsAsync(CategoryId, ct)` implementado en Infrastructure leyendo `IRepository<Category,CategoryId>` o `CategoryExistsSpecification`). `Difficulty : DifficultyLevel : Enumeration(1..5)` (compartido con Category.Game). `AcademicLevel : AcademicLevel : ValueObject(string)` 2–100 (Primaria, Secundaria, Bachillerato, Universidad, Postgrado) alineada a `Category.AcademicLevel`. `AgeRange : ValueObject(min,max)` 0–120 `min≤max`, `IsCompatible(categoryAgeRange)` verifica solapamiento (no requiere igualdad exacta). Alineación para contar como válida (SPEC-002 FR-007): `Question.Difficulty` compatible + `AcademicLevel` igual/compatible + `AgeRange` solapado; si desalineada → no cuenta y opcionalmente `Create` rechaza si spec lo exige (FR-007 dice no cuenta, no necessarily rechaza creación — se permite crear desalineada pero no es válida para publish gate).
- **Rationale**: Spec FR-007 + constitución B (Difficulty/AcademicLevel/AgeRange characteristics); `ValueObject` garantiza igualdad estructural; `ICategoryExistenceChecker` mantiene Clean Arch (Domain no conoce Infrastructure).
- **Alternatives**: `Question` con navigation `Category` EF (rechazado — acopla agregados, rompe boundary); `AcademicLevel` como `enum` cerrado (rechazado — spec dice strings controlados no cerrados); validar alineación como rechazo duro en `Create` (rechazado — spec dice desalineada no cuenta, no necessarily rechazada; plan permite crear pero marca `IsValidForCategory` false).

### 5. Selección IQuestionSelectionStrategy (7 Parámetros)

- **Decision**: Abstracción `IQuestionSelectionStrategy { Task<Result<IReadOnlyList<Question>>> SelectAsync(QuestionSelectionCriteria criteria, CancellationToken ct) }` con `QuestionSelectionCriteria : ValueObject` (`CategoryId?`, `Difficulty?`, `AcademicLevel?`, `AgeRange?`, `PreviousQuestionIds: IReadOnlyList<QuestionId>`, `GameId: GameId`, `RoundId/RoundNumber: int`). Implementación default `RandomQuestionSelectionStrategy` (selecciona aleatorio `ORDER BY NEWID()` / `NEWID()` en Specification + `Take(count)`). Alternativas `DifficultyAwareQuestionSelectionStrategy` (filtra por difficulty±1), `AdaptiveQuestionSelectionStrategy` (basado en `PlayerLevel` futuro). Todas usan `Specification<Question>` (`QuestionSelectionSpecification`) que compone `Where(Status==PUBLISHED)` + `Where(CategoryId==?)` + `Difficulty`/`AcademicLevel`/`AgeRange` + `Where(!PreviousIds.Contains(Id))` + `AsNoTracking` + `OrderByRandom/Take`. La selección es consultada por Game Engine (`Application.Features.Games.StartRound` o `SelectQuestions` Query) pasando `GameId`+`RoundNumber` para idempotencia.
- **Rationale**: Spec FR-013/014 exige contrato 7 params intercambiable; constitución B (Question selection SHOULD be behind IQuestionSelectionStrategy, prevents repetition within game) + C (strategy configurable); `Specification` mantiene BuildingBlocks. `Random` como default simplifica MVP sin sobre-ingeniería.
- **Alternatives**: Selección embebida en `Game` aggregate (rechazado — `Game` no debe conocer `Question` storage); stored procedure (rechazado — lógica en DB, viola Domain First); selección client-side (rechazado — viola Authoritative Domain Engine).

### 6. Persistencia, Índices y rowversion

- **Decision**: `OroQuizClashDbContext : AppDbContextBase` extendido con `DbSet<Question>`; `QuestionTypeConfiguration : IEntityTypeConfiguration<Question>` con `HasKey(Id→Guid)`, `Property(RowVersion).IsRowVersion().IsConcurrencyToken()`, `OwnsMany/HasMany` para `AnswerOptions` (tabla `AnswerOptions` con `Id`, `QuestionId` FK, `Text`, `IsCorrect`, `DisplayOrder`), `HasConversion` para `QuestionStatus`/`DifficultyLevel`, `OwnsOne(AgeRange)`. Índices: `IX_Question_CategoryId_Status`, `IX_Question_Difficulty`, `IX_Question_AcademicLevel`, `IX_Question_Status_Published` filtered `WHERE Status=3` (PUBLISHED), `IX_AnswerOptions_QuestionId_IsCorrect`. `CHECK`/`Trigger` para ExactlyOneCorrect. `Specification<T>` para `ValidQuestionSpecification(CategoryId)` (4/1 + Active) usada por `IQuestionCounter`.
- **Rationale**: Constitución E (SQL Server primario, índices siguen query patterns, `rowversion`, `Specification` con `ApplyAsNoTracking`), F (concurrencia), B (ExactlyOneCorrect DB constraint). `Api.Tests` usa Testcontainers MsSql para verificar concurrencia.
- **Alternatives**: `AnswerOptions` como `JSON` column en `Question` (considerado — simplifica read, pero complica `CHECK` y query por IsCorrect; se descarta para MVP, posible ADR futuro); `AnswerOptions` como owned ValueObject collection sin Id (rechazado — pierde estabilidad Id para QST-005).

### 7. CQRS Vertical Slice, Validación 3 Niveles, Errores y Observabilidad

- **Decision**: Cada caso en `Features/Questions/` con `*Command : ICommand<Result<*Response>>` (Create/Update/Activate/Deactivate/Publish/Archive) y `*Query` (GetQuestions/GetQuestionById/SelectQuestions). `*Validator : IValidator<Command>` (BuildingBlocks `Validator<T>`) para validación aplicación (3–500 text, 4 opciones, 1 correcta presence, CategoryId required); `ValidationBehavior` en pipeline; dominio `CheckRule` para invariantes. Errores mapeados a `ProblemDetails` via `GlobalExceptionHandler` + `Result.ToHttpResult()`: `400 QuestionMustHaveFourOptions/ExactlyOneCorrect/MustBelongToCategory/MustHaveDifficulty/InvalidAgeRange/InvalidAcademicLevel`, `400 QuestionNotPublishable`, `400 PublishedQuestionMustHaveCorrectAnswer`, `404 QuestionNotFound/CategoryNotFound`, `409 InvalidQuestionState/Concurrency`. Observabilidad via `ServiceDefaults` OTel (logs con `CorrelationId/QuestionId/CategoryId/GameId/RoundId`), audit append-only para mutaciones.
- **Rationale**: Constitución I (3 niveles), IV (Vertical Slice), III (BuildingBlocks CQRS), I (Error codes, ProblemDetails).
- **Alternatives**: FluentValidation externo (rechazado — BuildingBlocks Validator suficiente); central `QuestionService` (rechazado — viola slice).

### 8. Identidad y Autorización

- **Decision**: Endpoints requieren `ADMIN`/`GAME_MANAGER` via JWT `roles` OroIdentityServer (`Authority http://identity:5080`), policy `AdminOrGameManager` reuse de `Program.cs:61`. `PerformedBy` audit con `sub` claim. `GET /api/questions` y `/api/questions/select` requieren al menos `PLAYER` o `GAME_MANAGER` (lectura), pero escritura/Publish requieren `ADMIN/GAME_MANAGER`.
- **Rationale**: Constitución VI+H.
- **Alternatives**: `AllowAnonymous` para lectura (rechazado — spec asume gestión curada, lectura debe ser autenticada para evitar scrap; posible relajación futura).

### 9. IQuestionCounter para SPEC-002 Gate

- **Decision**: `IQuestionCounter` ya definido en 002 (`Task<int> CountValidAsync(CategoryId, ct)`) ahora implementado realmente por `EfQuestionCounter` (`IRepository<Question,QuestionId>` + `ValidQuestionSpecification`). Reusa `Specification` de este feature. `Category.Publish` sigue usando `IQuestionCounter` sin cambios.
- **Rationale**: Desacopla Category de Question storage; implementación real reemplaza `InMemoryQuestionCounter` stub de 002.
- **Alternatives**: Eventual consistency via domain event `QuestionPublished` increment counter (considerado — podría ser proyección futura, pero sync count es suficiente para MVP y evita eventual lag).

### 10. Testing Strategy

- **Decision**: Domain.Tests (xUnit) para `Question.Create` con 4/1, `Publish` gate, `SetCorrectAnswer` mantiene 1, `AgeRange` validation; Application.Tests con NSubstitute `IRepository<Question,QuestionId>` + `ICategoryExistenceChecker` + `IQuestionSelectionStrategy` stub; Infrastructure.Tests con `EfRepository` + `OroQuizClashDbContext` Sqlite/MsSql + `QuestionFilterSpecification` + `QuestionCounter` + rowversion concurrency; Api.Tests con `WebApplicationFactory` + JWT mock ADMIN + E2E lifecycle; Architecture.Tests verifica Domain no ref Infra/Web.
- **Rationale**: Constitución Testing Strategy.
- **Alternatives**: Solo integration tests (rechazado — no aísla regla 4/1).

## Resolved Clarifications

| Topic | Resolution |
|-------|------------|
| Estado PUBLISHED vs ACTIVE | `QuestionStatus` 5 valores: DRAFT(1), ACTIVE(2), PUBLISHED(3), INACTIVE(4), ARCHIVED(5). `ACTIVE` (visible) ≠ `PUBLISHED` (validado con gate). Solo `PUBLISHED` seleccionable; `ACTIVE` sin `PUBLISHED` no lo es. Si el equipo desea unificar, `ACTIVE` implica `PUBLISHED`; se deja como configurable sin romper QST-006. |
| Update en PUBLISHED | Permitido solo si mantiene 4/1 + alineación; si viola QST-005 → `PublishedQuestionMustHaveCorrectAnswer`. Alternativa estricta (inmutable) se documenta como flag y no bloquea MVP. |
| Alineación desalineada | Crear desalineada se permite pero no cuenta como válida para `Category.Publish` ni para selección con filtro alineado; `Create` no rechaza solo por desalineación salvo `AgeRange` inválido. |
| Selección sin CategoryId | Permitido: si `CategoryId` null, filtra solo por `Difficulty/AcademicLevel/AgeRange` provistos; no exige categoría. |
| AnswerOption Id estabilidad | `AnswerOptionId` estable; `SetCorrectAnswer(optionId)` garantiza 1 correcta sin recrear colección. |

## References

- `draft/constitution.md` §6 (Question Invariants), §5 States, §8 Game Configuration ↔ Category ↔ Question
- `draft/game-concept.md` §2-3 (Question 4 opciones/1 correcta/≥5 para Category)
- `draft/oroidentityserver-specification.md` (OIDC discovery, JWT)
- `BuildingBlocks` source `src/BuildingBlocks/` (net10.0, `Enumeration`, `ValueObject`, `Specification`, `AppDbContextBase`, `StronglyTypedId`)
- `specs/001-game-configuration/research.md`, `specs/002-categories/research.md` (patrón VO owned, rowversion, Specification, IQuestionCounter)
- `src/OroQuizClash.Domain/Games`, `src/OroQuizClash.Domain/Categories` (existing aggregates for reference)

