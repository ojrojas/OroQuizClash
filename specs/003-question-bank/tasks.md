# Tasks: Question Bank

**Input**: Design documents from `/specs/003-question-bank/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/
**Feature Branch**: `003-question-bank` | **Constitution**: v1.1.0 (I-VI)

**Organization**: Tasks grouped by user story — each story independently testable and deliverable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify existing modular monolith (001 + 002) and prepare Question Bank scaffolding

- [x] T001 Verify existing project structure per `specs/003-question-bank/plan.md` (`src/OroQuizClash.Domain`, `src/OroQuizClash.Application`, `src/OroQuizClash.Infrastructure`, `src/OroQuizClash.Api` in `OroQuizClash.slnx`) and `dotnet build OroQuizClash.slnx` passes
- [x] T002 Create Questions folder structure `src/OroQuizClash.Domain/Questions/{ValueObjects,Rules,Events}` and `src/OroQuizClash.Application/Features/Questions/{Services}` and `src/OroQuizClash.Infrastructure/{Specifications,Selection,Counters,Configurations}` via `mkdir -p src/OroQuizClash.Domain/Questions/ValueObjects src/OroQuizClash.Domain/Questions/Rules src/OroQuizClash.Domain/Questions/Events src/OroQuizClash.Application/Features/Questions/Services src/OroQuizClash.Infrastructure/Specifications src/OroQuizClash.Infrastructure/Selection`
- [x] T003 Verify `BuildingBlocks` dependencies and `Directory.Packages.props` central versions for `net10.0` (`LangVersion latest`, `Nullable enable`) in `Directory.Build.props` and `src/BuildingBlocks/` references

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared domain/infrastructure that MUST be complete before ANY user story — Question base, AnswerOption composition, persistence, selection ports, error handling

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Implement `QuestionId` StronglyTypedId in `src/OroQuizClash.Domain/Questions/QuestionId.cs` (`sealed record QuestionId(Guid Value) : StronglyTypedId<Guid>(Value)`)
- [x] T005 [P] Implement `AnswerOptionId` StronglyTypedId in `src/OroQuizClash.Domain/Questions/AnswerOptionId.cs`
- [x] T006 [P] Create `QuestionStatus` Enumeration `DRAFT(1)/ACTIVE(2)/PUBLISHED(3)/INACTIVE(4)/ARCHIVED(5)` with `GetAll()`, `FromId()`, `CanTransitionTo()`, `IsAvailableForSelection` (only PUBLISHED) in `src/OroQuizClash.Domain/Questions/QuestionStatus.cs`
- [x] T007 [P] Create ValueObjects `QuestionText` (3–500), `DifficultyLevel` (1..5 Enumeration), `AcademicLevel` (2–100), `AgeRange` (min/max 0–120) in `src/OroQuizClash.Domain/Questions/ValueObjects/` (one file per VO: `QuestionText.cs`, `DifficultyLevel.cs`, `AcademicLevel.cs`, `AgeRange.cs`)
- [x] T008 Extend `OroQuizClashDbContext` with `DbSet<Question> Questions` and `DbSet<AnswerOption> AnswerOptions` and ensure `ApplyConfiguration(new OutboxEntityTypeConfiguration())` remains in `src/OroQuizClash.Infrastructure/Persistence/OroQuizClashDbContext.cs`
- [x] T009 Create EF configuration `QuestionTypeConfiguration : IEntityTypeConfiguration<Question>` with `HasKey`+StronglyTypedId converter, `OwnsOne(AgeRange)`, `Property(RowVersion).IsRowVersion()`, `HasConversion` for `QuestionStatus`/`DifficultyLevel`, `HasMany AnswerOptions` with cascade, `HasIndex(CategoryId,Status)`, filtered index `Status=3` (PUBLISHED), indexes for `Difficulty`/`AcademicLevel` in `src/OroQuizClash.Infrastructure/Persistence/Configurations/QuestionTypeConfiguration.cs`
- [x] T010 Create EF configuration `AnswerOptionTypeConfiguration : IEntityTypeConfiguration<AnswerOption>` with `HasKey`, `Property(Text).HasMaxLength(500).IsRequired()`, `IsCorrect` required, `DisplayOrder` 0..3 unique per Question, `HasIndex(QuestionId)` in `src/OroQuizClash.Infrastructure/Persistence/Configurations/AnswerOptionTypeConfiguration.cs` (or inline in QuestionTypeConfiguration via `OwnsMany`)
- [x] T011 Define `IQuestionCounter` port `Task<int> CountValidAsync(CategoryId categoryId, CancellationToken)` in `src/OroQuizClash.Domain/Questions/Services/IQuestionCounter.cs` and `ICategoryExistenceChecker` port `Task<bool> ExistsAsync(CategoryId, CancellationToken)` in `src/OroQuizClash.Domain/Questions/Services/ICategoryExistenceChecker.cs`
- [x] T012 Define `IQuestionSelectionStrategy` port `Task<Result<IReadOnlyList<Question>>> SelectAsync(QuestionSelectionCriteria criteria, CancellationToken)` and `QuestionSelectionCriteria` record in `src/OroQuizClash.Application/Features/Questions/Services/IQuestionSelectionStrategy.cs`
- [x] T013 Create shared `QuestionErrors` with `Error` factory helpers (`QuestionMustHaveFourOptions`, `QuestionMustHaveOneCorrectAnswer`, `QuestionMustBelongToCategory`, `CategoryNotFound`, `QuestionMustHaveDifficulty`, `QuestionNotPublishable`, `PublishedQuestionMustHaveCorrectAnswer`, `InvalidQuestionState`, `NoAvailableQuestion`, `InvalidAgeRange`) in `src/OroQuizClash.Domain/Questions/QuestionErrors.cs`
- [x] T014 Register `IRepository<Question,QuestionId>` via `EfRepository<Question,QuestionId>`, `IQuestionCounter`, `ICategoryExistenceChecker`, `IQuestionSelectionStrategy` (Random default) in `src/OroQuizClash.Api/Program.cs` (add `AddScoped<IRepository<Question,QuestionId>>`, `AddScoped<IQuestionCounter, EfQuestionCounter>`, `AddScoped<ICategoryExistenceChecker, CategoryExistenceChecker>`, `AddScoped<IQuestionSelectionStrategy, RandomQuestionSelectionStrategy>`)
- [ ] T015 Add architecture test verifying `OroQuizClash.Domain/Questions` does not reference Infrastructure/Web and no MediatR/MassTransit/AutoMapper in `tests/OroQuizClash.Architecture.Tests/QuestionDependenciesTests.cs`

**Checkpoint**: Foundation ready — `dotnet build OroQuizClash.slnx` passes, `QuestionId`/`QuestionStatus`/`AgeRange` VOs unit-testable, `QuestionTypeConfiguration` creates `Questions`+`AnswerOptions`+`OutboxMessages` via `EnsureCreated`, ports injectable, architecture test green

---

## Phase 3: User Story 1 — Crear pregunta con 4 alternativas y validación de invariantes (Priority: P1) 🎯 MVP

**Goal**: Admin crea pregunta en `DRAFT` con `Text` 3–500, `CategoryId` existente, `Difficulty` 1..5, `AcademicLevel`, `AgeRange`, y exactamente 4 `AnswerOptions` con 1 correcta (QST-001..004)

**Independent Test**: `POST /api/questions` con payload válido (4 opts, 1 correct, Category+Difficulty) → `201` + `Location` + `GET /api/questions/{id}` idéntico 4/1; 3 opts →400 `QuestionMustHaveFourOptions`; 0/2 correctas →400 `ExactlyOneCorrect`; sin Category/Difficulty →400; 5 válidas incrementan `Category.validQuestionsCount`

### Tests for User Story 1 (write FIRST, ensure FAIL before implementation)

- [ ] T016 [P] [US1] Contract test for `POST /api/questions` valid/invalid (4/1, 3 opts, 0/2 correct, missing Category/Difficulty) in `tests/OroQuizClash.Api.Tests/Contracts/QuestionCreateContractTests.cs` (WebApplicationFactory, JWT mock `ADMIN`, asserts 201 vs 400 per `contracts/questions.openapi.yaml`)
- [ ] T017 [P] [US1] Domain unit tests for `Question.Create` with QST-001..004 in `tests/OroQuizClash.Domain.Tests/Questions/QuestionCreateTests.cs` (Arrange `CategoryId` existing mock, Act `Question.Create` with 3 opts/0 correct/2 correct/no Category/no Difficulty, Assert `Result.Failure` with correct Error; valid → DRAFT with 4 opts 1 correct)
- [ ] T018 [P] [US1] Application handler test for `CreateQuestionHandler` with `IRepository<Question,QuestionId>` + `ICategoryExistenceChecker` NSubstitute in `tests/OroQuizClash.Application.Tests/Features/Questions/CreateQuestionHandlerTests.cs` (seed Category exists, assert handler returns `Result.Success` with `QuestionId`, verify `IUnitOfWork.SaveChangesAsync` called)
- [ ] T019 [P] [US1] Specification test for `ValidQuestionSpecification` (4/1 + PUBLISHED + CategoryId + aligned Difficulty/Academic/Age) in `tests/OroQuizClash.Infrastructure.Tests/Specifications/ValidQuestionSpecificationTests.cs` (`IsSatisfiedBy` true for valid, false for 3 opts/0 correct/DRAFT)

### Implementation for User Story 1

- [x] T020 [P] [US1] Implement `IBusinessRule` classes `QuestionMustHaveFourOptionsRule`, `ExactlyOneCorrectAnswerRule`, `QuestionMustBelongToCategoryRule`, `QuestionMustHaveDifficultyRule`, `AgeRangeCoherentRule`, `AcademicLevelValidRule`, `CategoryExistsRule` in `src/OroQuizClash.Domain/Questions/Rules/` (one file per rule, e.g., `ExactlyOneCorrectAnswerRule.cs` with `IsBroken()` and `Error QuestionErrors.ExactlyOneCorrect`)
- [x] T021 [US1] Implement `Question` AggregateRoot in `src/OroQuizClash.Domain/Questions/Question.cs` with `static Result<Question> Create(QuestionText, CategoryId, DifficultyLevel, AcademicLevel, AgeRange, IReadOnlyList<AnswerOptionData>)` (validates via `CheckRule`, creates 4 `AnswerOption` entities with `DisplayOrder` A-D, `Status=DRAFT`, `CreatedAt=UtcNow`, emits `QuestionCreatedDomainEvent`), `RowVersion`, private ctor, no setters (depends on T004-T007, T020)
- [x] T022 [P] [US1] Implement `AnswerOption` Entity in `src/OroQuizClash.Domain/Questions/AnswerOption.cs` with `AnswerOptionId Id`, `QuestionId`, `Text` 1–500, `IsCorrect`, `DisplayOrder` 0..3, ctor internal, equality by Id
- [x] T023 [P] [US1] Create `QuestionCreatedDomainEvent : DomainEvent` in `src/OroQuizClash.Domain/Questions/Events/QuestionCreatedDomainEvent.cs` (properties `QuestionId`, `CategoryId`, `Difficulty`)
- [x] T024 [US1] Implement Vertical Slice `CreateQuestion` in `src/OroQuizClash.Application/Features/Questions/CreateQuestion.cs` with `CreateQuestionCommand(Text, CategoryId, Difficulty, AcademicLevel, AgeMin, AgeMax, AnswerOptions[]) : ICommand<Result<CreateQuestionResponse>>`, `CreateQuestionValidator : IValidator<CreateQuestionCommand>` (Text 3–500, CategoryId required, Difficulty 1..5, AgeMin 0–120, AnswerOptions 4, each Text 1–500, exactly 1 IsCorrect), `CreateQuestionHandler : ICommandHandler<...>` (injects `IRepository<Question,QuestionId>`, `ICategoryExistenceChecker`, `IUnitOfWork`, validates existence via checker, calls `Question.Create`, adds via repository, `SaveChangesAsync`, returns `Response`), `CreateQuestionResponse(Id, Status)`, `CreateQuestionEndpoint : IEndpoint` (`POST /api/questions`, `ISender.SendAsync` → `Result.ToHttpResult()` 201 with `Location`) (depends on T021)
- [x] T025 [P] [US1] Create query slices `GetQuestionById` in `src/OroQuizClash.Application/Features/Questions/GetQuestionById.cs` (Query+Handler+Endpoint `GET /api/questions/{id}`) and `GetQuestions` filter/pagination in `src/OroQuizClash.Application/Features/Questions/GetQuestions.cs` (`GetQuestionsQuery` with `CategoryId?, Difficulty?, AcademicLevel?, Status?, Search?`, Handler via `QuestionFilterSpecification`) for verification of creation
- [x] T026 [US1] Implement `QuestionFilterSpecification : Specification<Question>` with `Where` combinados for `CategoryId`/`Difficulty`/`AcademicLevel`/`Status`/`SearchText` + `Pagination` + `AsNoTracking` + `Include(AnswerOptions)` in `src/OroQuizClash.Infrastructure/Specifications/QuestionFilterSpecification.cs`
- [ ] T027 [US1] Add integration test for persistence `EfRepository<Question>` + `QuestionTypeConfiguration` + `rowversion` + `AnswerOptions` cascade + `QuestionFilterSpecification` in `tests/OroQuizClash.Infrastructure.Tests/Persistence/QuestionPersistenceTests.cs` (Testcontainers MsSql/Sqlite, asserts owned `AgeRange`, `AnswerOptions` 4, `RowVersion` concurrency, `GetQuestions` filter)

**Checkpoint**: US1 fully functional — `dotnet test --filter QuestionCreate` passes, `POST /api/questions` valid→201 DRAFT <1s, invalid (3 opts/0 correct/no Category) →400, `GET /api/questions/{id}` idéntico, `quickstart.md` P1 create scenario green

---

## Phase 4: User Story 2 — Ciclo de vida y publicación validada de preguntas (Priority: P1)

**Goal**: `PublishQuestion` gateado a QST-001..004 (solo `PUBLISHED` seleccionable QST-006), `Activate/Deactivate/Archive` con transiciones protegidas por `rowversion` (segundo `Publish` →409), y `PUBLISHED` no puede quedar sin correcta QST-005, emitiendo `QuestionPublishedDomainEvent` y habilitando `Category.Publish ≥5`

**Independent Test**: `DRAFT` válida → `POST /publish` →200 `PUBLISHED` + selectable; `DRAFT` inválida (3 opts/0 correctas) →400 `QuestionNotPublishable` (sigue DRAFT); `PUBLISHED` → `Update` dejando 0/2 correctas →400 `PublishedQuestionMustHaveCorrectAnswer`; `PUBLISHED→Deactivate→INACTIVE` (no selectable, no cuenta), `ARCHIVED` terminal →400; dos `Publish` concurrentes → uno 200, otro 409

### Tests for User Story 2 (write FIRST)

- [ ] T028 [P] [US2] Domain unit tests for `Question.Publish` gate and `Activate/Deactivate/Archive` transitions in `tests/OroQuizClash.Domain.Tests/Questions/QuestionLifecycleTests.cs` (Arrange `Question` DRAFT 4/1 valid → `Publish()` success + `QuestionPublishedDomainEvent` + `PUBLISHED`; 3 opts → Fail `QuestionNotPublishable`; PUBLISHED update to 0 correct → Fail `PublishedQuestionMustHaveCorrectAnswer`; state matrix `DRAFT→Publish`, `PUBLISHED→Deactivate→INACTIVE`, `ARCHIVED→Publish` Fail)
- [ ] T029 [P] [US2] Contract tests for lifecycle endpoints `POST /api/questions/{id}/publish|activate|deactivate|archive` in `tests/OroQuizClash.Api.Tests/Contracts/QuestionLifecycleContractTests.cs` (JWT ADMIN, asserts 200 vs 400 `QuestionNotPublishable` vs 409 `InvalidQuestionState` per `contracts/questions.openapi.yaml`)
- [ ] T030 [P] [US2] Concurrency test for duplicate `PublishQuestion`/`UpdateQuestion` with stale `RowVersion` in `tests/OroQuizClash.Infrastructure.Tests/Persistence/QuestionConcurrencyTests.cs` (two `OroQuizClashDbContext` instances, second `SaveChangesAsync` → `DbUpdateConcurrencyException` →409)

### Implementation for User Story 2

- [x] T031 [P] [US2] Implement `IBusinessRule` `PublishedQuestionMustHaveCorrectRule` and `QuestionStateTransitionRule` + `QuestionCanUpdateRule` in `src/OroQuizClash.Domain/Questions/Rules/` (e.g., `PublishedQuestionMustHaveCorrectRule.cs` checks `Status==PUBLISHED implies CorrectCount==1`, `QuestionStateTransitionRule.cs` matrix DRAFT→PUBLISHED etc.)
- [x] T032 [US2] Extend `Question` with lifecycle methods `Result Activate()` (DRAFT/INACTIVE→ACTIVE), `Result Deactivate()` (ACTIVE/PUBLISHED→INACTIVE, `UpdatedAt`), `Result Publish()` (DRAFT/ACTIVE/INACTIVE→PUBLISHED gate `CheckRule(FourOptions+ExactlyOneCorrect+BelongToCategory+MustHaveDifficulty)` + `AgeRange/AcademicLevel` valid, sets `Status=PUBLISHED`, `PublishedAt=UtcNow`, emits `QuestionPublishedDomainEvent`), `Result Archive()` (ACTIVE/PUBLISHED/INACTIVE→ARCHIVED terminal, emits `QuestionArchivedDomainEvent`) in `src/OroQuizClash.Domain/Questions/Question.cs` (validates transition via `QuestionStateTransitionRule`, rowversion handled by EF)
- [x] T033 [P] [US2] Create domain events `QuestionPublishedDomainEvent`, `QuestionDeactivatedDomainEvent`, `QuestionArchivedDomainEvent` in `src/OroQuizClash.Domain/Questions/Events/` (one file per event: `QuestionPublishedDomainEvent.cs` with `QuestionId`, `CategoryId`)
- [x] T034 [US2] Implement Vertical Slices `PublishQuestion`, `ActivateQuestion`, `DeactivateQuestion`, `ArchiveQuestion` in `src/OroQuizClash.Application/Features/Questions/` (one file per slice: `PublishQuestion.cs` with `PublishQuestionCommand(QuestionId) : ICommand<Result<QuestionResponse>>`, `PublishQuestionValidator` (Id required), `PublishQuestionHandler` (load via `IRepository<Question,QuestionId>.GetByIdAsync`, call `question.Publish()`, `SaveChangesAsync`, returns 200; `ActivateQuestion.cs` etc. `POST /api/questions/{id}/activate` thin `IEndpoint`))
- [x] T035 [US2] Implement `EfQuestionCounter` for SPEC-002 gate using `IRepository<Question,QuestionId>` + `ValidQuestionSpecification(CategoryId)` (Status==PUBLISHED && 4/1 && CategoryId match && Academic/Age aligned) in `src/OroQuizClash.Infrastructure/Counters/QuestionCounter.cs` and wire to replace `InMemoryQuestionCounter` in `src/OroQuizClash.Api/Program.cs`
- [x] T036 [US2] Update `CategoryExistenceChecker` to query `IRepository<Category,CategoryId>` with `CategoryByIdSpecification` and `Status!=ARCHIVED` check in `src/OroQuizClash.Infrastructure/Services/CategoryExistenceChecker.cs`

**Checkpoint**: US2 green — `Publish` gate 100% (invalid→400, valid→200 PUBLISHED <2s, emits event), `PUBLISHED` keep-correct 100% (0/2 →400), `Deactivate/Archive` transitions 100%, `Category.Publish` with 5 PUBLISHED → ACTIVE, concurrencia 409, `quickstart.md` P1 publish scenario green

---

## Phase 5: User Story 3 — Actualizar pregunta en estados permitidos (Priority: P2)

**Goal**: Admin actualiza `Text`, `AnswerOptions`, `CategoryId`, `Difficulty`, `AcademicLevel`, `AgeRange` solo en `DRAFT`/`INACTIVE` (y condicionalmente `PUBLISHED` si mantiene 4/1), re-validando QST-001..005, con `RowVersion` increment y `UpdatedAt`

**Independent Test**: `DRAFT` → `PUT /api/questions/{id}` con payload válido 4/1 →200 mutado; `PUBLISHED` → `PUT` con 3 opts →400; `ARCHIVED` → `PUT` →400 `InvalidQuestionState`; `DRAFT→Update` con `AgeMin>AgeMax` →400

### Tests for User Story 3 (write FIRST)

- [ ] T037 [P] [US3] Domain unit tests for `Question.Update` in `tests/OroQuizClash.Domain.Tests/Questions/QuestionUpdateTests.cs` (DRAFT→Update valid → success+UpdatedAt; PUBLISHED→Update with 0 correct → Fail `PublishedQuestionMustHaveCorrectAnswer`; ARCHIVED→Update → Fail `InvalidQuestionState`; Category not found → Fail)
- [ ] T038 [P] [US3] Contract tests for `PUT /api/questions/{id}` lifecycle guards in `tests/OroQuizClash.Api.Tests/Contracts/QuestionUpdateContractTests.cs` (asserts 200 DRAFT/INACTIVE, 400 PUBLISHED with 3 opts, 404 not found, 409 stale RowVersion)
- [ ] T039 [P] [US3] Application handler test for `UpdateQuestionHandler` with rowversion conflict in `tests/OroQuizClash.Application.Tests/Features/Questions/UpdateQuestionHandlerTests.cs` (NSubstitute `IRepository` returns Question, handler calls `Update`, asserts `QuestionStateTransitionRule` enforced)

### Implementation for User Story 3

- [x] T040 [US3] Extend `Question.Update(QuestionText, CategoryId, DifficultyLevel, AcademicLevel, AgeRange, IReadOnlyList<AnswerOptionData>)` in `src/OroQuizClash.Domain/Questions/Question.cs` (checks `QuestionCanUpdateRule` (DRAFT/INACTIVE or PUBLISHED with 4/1 preserved), validates via `CheckRule` 4/1+Category+Difficulty+AgeRange, recreates `AnswerOptions` preserving `AnswerOptionId` where possible, sets `UpdatedAt`, emits `QuestionUpdatedDomainEvent`)
- [x] T041 [P] [US3] Create `QuestionUpdatedDomainEvent` in `src/OroQuizClash.Domain/Questions/Events/QuestionUpdatedDomainEvent.cs`
- [x] T042 [US3] Implement Vertical Slice `UpdateQuestion` in `src/OroQuizClash.Application/Features/Questions/UpdateQuestion.cs` with `UpdateQuestionCommand(Id, Text, CategoryId, Difficulty, AcademicLevel, AgeMin, AgeMax, AnswerOptions[], RowVersion) : ICommand<Result<QuestionResponse>>`, `UpdateQuestionValidator` (Text 3–500, 4 opts each 1–500, exactly 1 correct, CategoryId required, Difficulty 1..5, AgeMin/AgeMax 0–120 min≤max), `UpdateQuestionHandler` (load, call `question.Update`, `SaveChangesAsync`, handle `DbUpdateConcurrencyException` →409), `UpdateQuestionEndpoint : IEndpoint` (`PUT /api/questions/{id}`) (depends on T040)

**Checkpoint**: US3 green — `DRAFT→Update` 200 <1s, `PUBLISHED` keep-1 200 vs break-1 400, `ARCHIVED→Update` 400, `AgeMin>AgeMax` 400, concurrencia 409, `quickstart.md` P2 update scenario green

---

## Phase 6: User Story 4 — Selección de preguntas para Game/Round considerando múltiples criterios (Priority: P2)

**Goal**: Motor selecciona preguntas filtrando por `Category`, `Difficulty`, `AcademicLevel`, `AgeRange`, excluyendo `PreviousQuestionIds` del `Game`, con `GameId`+`RoundNumber` contexto, paginado sin full scan, solo `PUBLISHED` (QST-006), estrategia intercambiable (`Random` default, `DifficultyAware`, `Adaptive`) detrás de `IQuestionSelectionStrategy`

**Independent Test**: 10 `PUBLISHED` en `Cat X` `Difficulty=2` `Secundaria` `13-17` → `POST /api/questions/select` con `categoryId=X difficulty=2 academicLevel=Secundaria ageMin=13 ageMax=17 previous=[id1,id2] gameId+round` → retorna no-previos alineados <500ms 1k dataset; `INACTIVE/ARCHIVED/DRAFT` nunca retornadas; vacío →404 `NoAvailableQuestion`; `GameId`+`Round` presentes en criteria

### Tests for User Story 4 (write FIRST)

- [ ] T043 [P] [US4] Contract tests for `POST /api/questions/select` per `contracts/questions.openapi.yaml` and `GET /api/games/{gameId}/rounds/{roundNumber}/question` per `contracts/question-selection.openapi.yaml` in `tests/OroQuizClash.Api.Tests/Contracts/QuestionSelectionContractTests.cs` (asserts 200 with correct filters, excludes Previous, 404 NoAvailableQuestion when empty, 400 missing gameId)
- [ ] T044 [P] [US4] Specification tests for `QuestionSelectionSpecification` (Category+Difficulty+Academic+Age+Previous exclusion) in `tests/OroQuizClash.Infrastructure.Tests/Specifications/QuestionSelectionSpecificationTests.cs` (seed 1k questions, assert `IsSatisfiedBy` composition, `!Previous.Contains(Id)`, `Status==PUBLISHED` only)
- [ ] T045 [P] [US4] Strategy unit tests for `RandomQuestionSelectionStrategy.SelectAsync` and `DifficultyAware` variant in `tests/OroQuizClash.Application.Tests/Features/Questions/QuestionSelectionStrategyTests.cs` (mock `IRepository` with `ValidQuestionSpecification`, assert random returns non-previous, DifficultyAware filters ±1, empty → `NoAvailableQuestion`)

### Implementation for User Story 4

- [x] T046 [P] [US4] Implement `QuestionSelectionCriteria` ValueObject/record with `CategoryId?, Difficulty?, AcademicLevel?, AgeRange?, PreviousQuestionIds, GameId, RoundNumber, Take` and validation in `src/OroQuizClash.Domain/Questions/ValueObjects/QuestionSelectionCriteria.cs`
- [x] T047 [P] [US4] Implement `ValidQuestionSpecification : Specification<Question>` (Status==PUBLISHED && 4 opts && 1 correct && CategoryId==param && aligned) and reuse for `IQuestionCounter` in `src/OroQuizClash.Infrastructure/Specifications/ValidQuestionSpecification.cs`
- [x] T048 [US4] Implement `QuestionSelectionSpecification : Specification<Question>` with `Where(Status==PUBLISHED)` + optional `CategoryId` + optional `Difficulty` + optional `AcademicLevel` + optional `AgeRange` + `Where(!PreviousQuestionIds.Contains(Id))` + `AsNoTracking` + `Include(AnswerOptions)` + pagination via `OrderByRandom` in `src/OroQuizClash.Infrastructure/Specifications/QuestionSelectionSpecification.cs` (depends on T047)
- [x] T049 [P] [US4] Implement `RandomQuestionSelectionStrategy : IQuestionSelectionStrategy` using `IRepository<Question,QuestionId>` + `QuestionSelectionSpecification` + `ORDER BY NEWID()` (`Guid.NewGuid()` order) + `Take(criteria.Take)` in `src/OroQuizClash.Infrastructure/Selection/RandomQuestionSelectionStrategy.cs` (returns `Result<IReadOnlyList<Question>>` or `Error NoAvailableQuestion` if empty)
- [x] T050 [P] [US4] Implement `DifficultyAwareQuestionSelectionStrategy : IQuestionSelectionStrategy` (filters `Difficulty±1` fallback) in `src/OroQuizClash.Infrastructure/Selection/DifficultyAwareQuestionSelectionStrategy.cs` (optional, implements same port, registered as alternative)
- [x] T051 [US4] Implement Vertical Slice `SelectQuestions` in `src/OroQuizClash.Application/Features/Questions/SelectQuestions.cs` with `SelectQuestionsQuery(SelectQuestionsRequest) : IQuery<Result<SelectQuestionsResponse>>`, `SelectQuestionsValidator` (GameId required, Take 1..10), `SelectQuestionsHandler` (maps request to `QuestionSelectionCriteria`, delegates to `IQuestionSelectionStrategy.SelectAsync`, maps `Question` to `QuestionResponse` DTO), `SelectQuestionsEndpoint : IEndpoint` (`POST /api/questions/select` thin `ISender.SendAsync→Result.ToHttpResult()`) (depends on T048-T049)
- [x] T052 [P] [US4] Add alternative Game/Round endpoint `GET /api/games/{gameId}/rounds/{roundNumber}/question` delegating to same `SelectQuestionsHandler` in `src/OroQuizClash.Application/Features/Questions/SelectQuestions.cs` or `src/OroQuizClash.Application/Features/Games/GetQuestionForRound.cs` (query params `categoryId/difficulty/academicLevel/ageMin/ageMax`, passes `PreviousQuestionIds` from `GameRound` aggregation — stub with empty for now, wired for future `Game.StartRound`)
- [ ] T053 [US4] Add persistence integration test for `QuestionSelectionSpecification` over 1k rows + `IQuestionCounter` count valid and selection `<500ms` in `tests/OroQuizClash.Infrastructure.Tests/Persistence/QuestionSelectionPerformanceTests.cs` (Testcontainers MsSql, seed 1k, `SelectAsync` with Previous exclusion, assert <500ms p95, paginated without full scan via `EXPLAIN`)

**Checkpoint**: US4 green — `POST /api/questions/select` with Category+Difficulty+Academic+Age filters 100% precisión, Previous exclusion 100%, `PUBLISHED`-only, empty→404, `Random` <500ms 1k, `DifficultyAware` ±1 fallback, `quickstart.md` P2 selection scenario green

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Hardening, docs, quickstart validation, DoD checklist, performance & security

- [ ] T054 [P] Add `ProblemDetails` mapping tests for all `QuestionErrors` codes (`QuestionMustHaveFourOptions`, `ExactlyOneCorrect`, `MustBelongToCategory`, `MustHaveDifficulty`, `QuestionNotPublishable`, `PublishedQuestionMustHaveCorrectAnswer`, `InvalidQuestionState`, `NoAvailableQuestion`, `CategoryNotFound`, `InvalidAgeRange`) in `tests/OroQuizClash.Api.Tests/Errors/QuestionErrorsMappingTests.cs` (assert `400` vs `404` vs `409` per `GlobalExceptionHandler` → `Result.ToHttpResult()`)
- [ ] T055 [P] Run `quickstart.md` end-to-end validation (create 5 válidas→Publish 5→Category Publish gate→Activate/Deactivate/Archive→Select with Previous→concurrencia 409) and fix gaps in `specs/003-question-bank/quickstart.md` (execute curl scenarios, confirm <1s create/<2s publish/<500ms select, 409 on concurrent, audit logs)
- [ ] T056 [P] Add structured logging fields `QuestionId`/`CategoryId`/`GameId`/`RoundId`/`Command`/`Duration` via `LoggingBehavior` verification in `tests/OroQuizClash.Application.Tests/Pipeline/QuestionLoggingBehaviorTests.cs` (uses `ILogger` NSubstitute, asserts `LogInformation` with `QuestionId`)
- [ ] T057 [P] Update `docs/adr/ADR-008-question-selection-strategy.md` documenting decisions from `research.md` (IQuestionSelectionStrategy 7 params, Random default, DifficultyAware/Adaptive intercambiables, Specification composition, valid counting)
- [ ] T058 [P] Update `docs/adr/ADR-012-question-bank.md` documenting Question aggregate (4/1 + DB CHECK, 5 states DRAFT→PUBLISHED terminal ARCHIVED, rowversion, AnswerOption composition) in `docs/adr/ADR-012-question-bank.md`
- [ ] T059 Security hardening: verify `POST/PUT/POST publish|select` require `ADMIN/GAME_MANAGER` (no anonymous), `GET /api/questions` requires authenticated `PLAYER`+`ADMIN`, rate limiting via `BuildingBlocks.ServiceDefaults`, `correlationId` propagation, no sensitive data (answer IsCorrect not leaked to PLAYER before Round — ensure `SelectQuestionsResponse` respects game timing) in `src/OroQuizClash.Api/Program.cs` and `QuestionEndpoints` (`[Authorize(Policy="AdminOrGameManager")]` vs `[Authorize]`)
- [ ] T060 Performance smoke: `dotnet test --filter SC-001` timing assert crear <1s, publish <2s, select <500ms 1k (95% p95) in `tests/OroQuizClash.Api.Tests/Performance/QuestionPerformanceTests.cs` (measures `CreateQuestionHandler` + `RandomQuestionSelectionStrategy` with 1k seeded)
- [ ] T061 Add audit append-only verification for Question mutations (create/update/publish/archive/select) with `CorrelationId`/`PerformedBy sub` in `tests/OroQuizClash.Infrastructure.Tests/Audit/QuestionAuditTests.cs` (assert after `SaveChanges` audit row exists, OTel `TraceId` logged)
- [ ] T062 Final `dotnet build OroQuizClash.slnx && dotnet test` green, `dotnet format` clean, update `specs/003-question-bank/spec.md` Status to `Ready for Review` and sync `specs/003-question-bank/checklists/requirements.md` (re-run quality checklist, confirm SC-001..009)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately (reuses 001+002 foundation)
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (Question base + ports + RowVersion + IQuestionSelectionStrategy)
- **User Stories (Phase 3+)**: Depend on Foundational
  - **US1 (P1) Create**: No dependencies on other stories — delivers MVP (bank creable 4/1)
  - **US2 (P1) Lifecycle/Publish**: Depends on US1's `Question.cs` (extends with `Publish/Activate/Deactivate/Archive`) — adds gate QST-005/006 and Category ≥5
  - **US3 (P2) Update**: Depends on US1's `Question.cs` + US2's lifecycle guard (`QuestionCanUpdateRule`) — adds curaduría sin recrear
  - **US4 (P2) Selection**: Depends on US1's `Question`+`QuestionFilterSpecification` and US2's `ValidQuestionSpecification` (PUBLISHED) — adds Game/Round 7-param selection
- **Polish (Final Phase)**: Depends on all desired stories complete (US1+US2 for MVP, US4 required for game engine)

### User Story Dependencies

- **US1 (P1)**: Can start after Foundational — no deps on other stories; delivers MVP (pregunta DRAFT 4/1 creable)
- **US2 (P1)**: Depends on US1's `Question.cs` — adds Publish gate and lifecycle; independently testable after US1 (publish with 4/1 vs 3/0)
- **US3 (P2)**: Depends on US1's `Question.cs` + US2's `QuestionCanUpdateRule` — independently testable (update DRAFT vs PUBLISHED vs ARCHIVED)
- **US4 (P2)**: Depends on US1's `QuestionFilterSpecification` + US2's `ValidQuestionSpecification` — independently testable (filter+Previous exclusion); may run in parallel with US3 after Foundational if Question.cs is stable

### Within Each User Story

- Tests FIRST → FAIL → Implementation → PASS (TDD per constitution Testing Strategy)
- Models/VOs → Rules → Aggregate → EF Config → Slice (Validator → Handler → Endpoint) → Specification → Integration tests
- `IQuestionCounter` seeded before `Publish` tests; `PreviousQuestionIds` stubbed before `Select` tests
- Verify `Result.Failure` codes map to `ProblemDetails` (400/404/409) after each story

### Parallel Opportunities

- Phase 2: T005+T006+T007 (IDs/Status/VOs) parallel; T009+T010 (EF configs) parallel; T011+T012 (ports) parallel
- Phase 3: T016-T019 (contract/domain/handler/spec tests) parallel; T020 (Rules) parallel with T022-T023 (AnswerOption+Events); T025-T026 (GetById/GetQuestions + specs) parallel
- Phase 4: T028-T030 (domain/contract/concurrency tests) parallel; T031+T033 (rules+events) parallel; T035+T036 (Counter+ExistenceChecker) parallel
- Phase 5: T037-T039 (domain/contract/handler tests) parallel
- Phase 6: T043-T045 (contract/spec/strategy tests) parallel; T046+T047 (Criteria+ValidSpec) parallel; T049+T050 (Random+DifficultyAware) parallel
- Phase 7: T054-T058 (errors/quickstart/logging/ADRs) parallel; T059 (security) parallel with T060 (perf)
- Different user stories can be worked on in parallel by different developers after Foundational if `Question.cs` is shared (coordinate merges)

### Parallel Example: User Story 1 (Create)

```bash
# Tests in parallel (different files):
Task T016: Contract test in tests/OroQuizClash.Api.Tests/Contracts/QuestionCreateContractTests.cs
Task T017: Domain unit tests in tests/OroQuizClash.Domain.Tests/Questions/QuestionCreateTests.cs
Task T018: Handler test in tests/OroQuizClash.Application.Tests/Features/Questions/CreateQuestionHandlerTests.cs
Task T019: Specification test in tests/OroQuizClash.Infrastructure.Tests/Specifications/ValidQuestionSpecificationTests.cs

# Models in parallel:
Task T020: Rules in src/OroQuizClash.Domain/Questions/Rules/ExactlyOneCorrectAnswerRule.cs
Task T022: AnswerOption in src/OroQuizClash.Domain/Questions/AnswerOption.cs
Task T023: Event in src/OroQuizClash.Domain/Questions/Events/QuestionCreatedDomainEvent.cs
```

### Parallel Example: User Story 4 (Selection)

```bash
# Tests in parallel:
Task T043: Contract in tests/OroQuizClash.Api.Tests/Contracts/QuestionSelectionContractTests.cs
Task T044: Specification in tests/OroQuizClash.Infrastructure.Tests/Specifications/QuestionSelectionSpecificationTests.cs
Task T045: Strategy in tests/OroQuizClash.Application.Tests/Features/Questions/QuestionSelectionStrategyTests.cs

# Strategies in parallel:
Task T049: Random in src/OroQuizClash.Infrastructure/Selection/RandomQuestionSelectionStrategy.cs
Task T050: DifficultyAware in src/OroQuizClash.Infrastructure/Selection/DifficultyAwareQuestionSelectionStrategy.cs
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup + Phase 2: Foundational (Question base, ports, RowVersion)
2. Complete Phase 3: US1 (CreateQuestion 4/1 + GetById/GetQuestions) — delivers MVP: banco creable DRAFT con validación QST-001..004
3. **STOP and VALIDATE**: `dotnet test --filter QuestionCreate` + `quickstart.md` P1 create (valid→201 DRAFT 4/1 <1s, invalid 3 opts/0 correct/no Category →400) + `GET /api/questions/{id}` idéntico
4. Deploy/demo if ready — `POST /api/questions` sin publish/selection aún

### Incremental Delivery

1. Setup + Foundational → foundation ready (`QuestionId`, `QuestionStatus`, EF configs, ports)
2. Add US1 → Test independently → Demo MVP (CRUD DRAFT 4/1, QST-001..004)
3. Add US2 → Test Publish gate + Activate/Deactivate/Archive + QST-005/006 + Category ≥5 + 409 → Demo (gate no-bypass, PUBLISHED selectable)
4. Add US3 → Test Update DRAFT/INACTIVE vs PUBLISHED/ARCHIVED → Demo (curaduría)
5. Add US4 → Test Select with 7 params + Previous exclusion + NoAvailableQuestion → Demo (motor juego, <500ms 1k)
6. Polish → quickstart E2E + perf (<1s/<2s/<500ms) + security (ADMIN/GAME_MANAGER) + ADRs + audit + `dotnet build && dotnet test` green

### Parallel Team Strategy

With multiple developers after Foundational:
- Developer A: US1 (Question.Create + CreateQuestion slice + AnswerOption)
- Developer B: US2 (Question.Publish/Activate/Deactivate/Archive + PublishQuestion slice + EfQuestionCounter) — after A's `Question.cs` merges
- Developer C: US3 (Question.Update + UpdateQuestion slice) — after A's `Question.cs` + B's guards
- Developer D: US4 (QuestionSelectionSpecification + Random/DifficultyAware + SelectQuestions slice) — after A's `Question.cs` + ValidQuestionSpecification
- All stories integrate via same `Question` aggregate without conflicts if `Question.cs` changes are coordinated (rules → aggregate → slice order)

---

## Notes

- [P] tasks = different files, no dependencies — can run in parallel
- [Story] label maps task to specific user story for traceability to `spec.md` US1..US4
- Each user story independently completable and testable via `quickstart.md` curl + `dotnet test --filter USx`
- Verify tests FAIL before implementation (TDD), commit after each task or logical group
- QST-001..006 traced: QST-001→T020/T021/T024 (4 opts), QST-002→T020/T021/T024 (1 correct + DB CHECK T009), QST-003→T011/T024 (CategoryExists), QST-004→T007/T020 (Difficulty), QST-005→T031/T032/T034 (Published keep correct), QST-006→T032/T048-T051 (PUBLISHED only selection)
- Selection 7 params (Category, Difficulty, AcademicLevel, AgeRange, PreviousQuestions, Game, Round) traced to T012/T046-T051 with strategy intercambiable without contract change
- Avoid: vague tasks, same-file conflicts, cross-story dependencies that break independence; prefer explicit file paths per task
- Constitution gates: Domain First (T020-T021 rules in Domain), Clean Arch (T011 ports), BuildingBlocks reuse (T013 errors, T009 RowVersion), Vertical Slice (T024 slices), Authoritative (T048 server-side exclusion), OroIdentityServer JWT (T014/T059)

