# Tasks: Categories

**Input**: Design documents from `/specs/002-categories/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/
**Feature Branch**: `002-categories` | **Constitution**: v1.1.0 (I-VI)

**Organization**: Tasks grouped by user story — each story independently testable and deliverable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify existing modular monolith and prepare Categories scaffolding (reuses 001-game-configuration foundation)

- [x] T001 Verify existing project structure per `specs/002-categories/plan.md` (`src/OroQuizClash.Domain`, `src/OroQuizClash.Application`, `src/OroQuizClash.Infrastructure`, `src/OroQuizClash.Api` in `OroQuizClash.slnx`)
- [x] T002 Create Categories folder structure `src/OroQuizClash.Domain/Categories/` and `src/OroQuizClash.Application/Features/Categories/` in `src/OroQuizClash.Domain/Categories/` and `src/OroQuizClash.Application/Features/Categories/`
- [x] T003 Verify `BuildingBlocks` dependencies and `Directory.Packages.props` central versions for `net10.0` in `Directory.Build.props`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared domain/infrastructure that MUST be complete before ANY user story — Category base, persistence, question counting port, error handling

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Implement `CategoryId` StronglyTypedId in `src/OroQuizClash.Domain/Categories/CategoryId.cs`
- [x] T005 [P] Create `CategoryStatus` Enumeration `DRAFT/ACTIVE/INACTIVE/ARCHIVED` in `src/OroQuizClash.Domain/Categories/CategoryStatus.cs`
- [x] T006 [P] Create ValueObjects `AgeRange`, `CategoryTags`, `KnowledgeArea`, `AcademicLevel`, `DifficultyLevel`, `PublishConfiguration` in `src/OroQuizClash.Domain/Categories/ValueObjects/` (one file per VO, e.g., `AgeRange.cs`)
- [x] T007 Extend `OroQuizClashDbContext` with `DbSet<Category>` and ensure `ApplyConfiguration(new OutboxEntityTypeConfiguration())` remains in `src/OroQuizClash.Infrastructure/Persistence/OroQuizClashDbContext.cs`
- [x] T008 Create EF configuration `CategoryTypeConfiguration : IEntityTypeConfiguration<Category>` with `HasKey`, `StronglyTypedId` converter, `OwnsOne(AgeRange)`, `Property(RowVersion).IsRowVersion()`, `HasConversion` for `CategoryStatus`/`DifficultyLevel`, indexes in `src/OroQuizClash.Infrastructure/Persistence/Configurations/CategoryTypeConfiguration.cs`
- [x] T009 Define `IQuestionCounter` port `Task<int> CountValidAsync(CategoryId, CancellationToken)` in `src/OroQuizClash.Domain/Categories/IQuestionCounter.cs` and stub `InMemoryQuestionCounter` with `Dictionary<CategoryId,List<QuestionStub>>` valid check (4 opts/1 correct/active/alineada) in `src/OroQuizClash.Infrastructure/Counters/InMemoryQuestionCounter.cs`
- [x] T010 Create shared `CategoryErrors` with `Error` factory helpers (`InvalidCategoryConfiguration`, `CategoryNotPublishable`, `CategoryNotReady`, `InvalidCategoryState`, `CategoryNotFound`) in `src/OroQuizClash.Domain/Categories/CategoryErrors.cs`
- [x] T011 Register `IRepository<Category,CategoryId>` via `EfRepository<Category,CategoryId>` and `IQuestionCounter` in `src/OroQuizClash.Api/Program.cs` (add `AddScoped<IRepository<Category,CategoryId>>` and `AddScoped<IQuestionCounter, InMemoryQuestionCounter>`)
- [x] T012 Add architecture test for Category aggregate isolated from Infrastructure/Web in `tests/OroQuizClash.Architecture.Tests/CategoryDependenciesTests.cs`

**Checkpoint**: Foundation ready — `dotnet build OroQuizClash.slnx` passes, `CategoryId`/`AgeRange` VOs unit-testable, `CategoryTypeConfiguration` creates `Categories` + `OutboxMessages` via `EnsureCreated`, `IQuestionCounter` injectable

---

## Phase 3: User Story 1 — Crear y actualizar categoría (Priority: P1) 🎯 MVP

**Goal**: Admin crea categoría en `DRAFT` con todos los campos y la actualiza solo en `DRAFT`/`INACTIVE`, validando `Name` 3–100, `AgeRange` 0–120 `min≤max`, `Tags` normalizados

**Independent Test**: `POST /api/categories` con payload válido → `201` + `Location` + `GET /api/categories/{id}` idéntico; `PUT /api/categories/{id}` en `DRAFT` → `200` mutado; sin nombre o `ageMin 17 > ageMax 13` → `400`; `ARCHIVED→Update` → `400`

### Tests for User Story 1 (write FIRST, ensure FAIL before implementation)

- [x] T013 [P] [US1] Contract test for `POST /api/categories` valid/invalid + `PUT /api/categories/{id}` in `tests/OroQuizClash.Api.Tests/Contracts/CategoryCreateUpdateContractTests.cs` (WebApplicationFactory, JWT mock `ADMIN`, asserts 201 vs 400 per `contracts/categories.openapi.yaml`)
- [x] T014 [P] [US1] Domain unit tests for `Category.Create` and `Category.Update` in `tests/OroQuizClash.Domain.Tests/Categories/CategoryCreateUpdateTests.cs` (Arrange/Act/Assert Name empty, AgeRange invertido, Tags deduplicados, Update en ARCHIVED)
- [x] T015 [P] [US1] Application handler test for `CreateCategoryHandler` + `UpdateCategoryHandler` with `IRepository` NSubstitute in `tests/OroQuizClash.Application.Tests/Features/Categories/CategoryHandlersTests.cs`

### Implementation for User Story 1

- [x] T016 [P] [US1] Implement `IBusinessRule` classes `CategoryNameRule`, `AgeRangeCoherentRule`, `CategoryTagsValidRule`, `DifficultyLevelValidRule` in `src/OroQuizClash.Domain/Categories/Rules/` (one file per rule, e.g., `AgeRangeCoherentRule.cs`)
- [x] T017 [US1] Implement `Category` AggregateRoot in `src/OroQuizClash.Domain/Categories/Category.cs` with `static Result<Category> Create(...)`, `Result Update(...)` (solo `DRAFT`/`INACTIVE`, `CheckRule`), `RowVersion`, `Status=DRAFT`, `CategoryCreated/UpdatedDomainEvent` (depends on T004-T006, T016)
- [x] T018 [P] [US1] Create `CategoryCreatedDomainEvent` and `CategoryUpdatedDomainEvent : DomainEvent` in `src/OroQuizClash.Domain/Categories/Events/` (one file per event)
- [x] T019 [US1] Implement Vertical Slices `CreateCategory` and `UpdateCategory` in `src/OroQuizClash.Application/Features/Categories/CreateCategory.cs` and `UpdateCategory.cs` each with `Command : ICommand<Result<Response>>`, `Validator : IValidator<Command>` (Name 3–100, Description 0–500, KnowledgeArea 2–100, AgeMin 0–120, Tags ≤10), `Handler : ICommandHandler<...>` (`IRepository<Category,CategoryId>` + `IUnitOfWork`), `Response` DTO, `Endpoint : IEndpoint` thin `ISender.SendAsync→Result.ToHttpResult()` (depends on T017)
- [x] T020 [US1] Wire integration test for persistence `EfRepository<Category>` + `CategoryTypeConfiguration` + `rowversion` + `Specification` in `tests/OroQuizClash.Infrastructure.Tests/Persistence/CategoryPersistenceTests.cs` (Testcontainers, asserts owned `AgeRange`, `Tags` conversion, `RowVersion`)

**Checkpoint**: US1 fully functional — `dotnet test --filter CategoryCreateUpdate` passes, `POST /api/categories` valid→201 DRAFT <1s, invalid→400, `PUT` en DRAFT→200, `quickstart.md` P1 scenario green

---

## Phase 4: User Story 2 — Ciclo de vida y publicación guardada por invariante de preguntas (Priority: P1)

**Goal**: `Activate/Deactivate/Publish/Archive` con `PublishCategory` gateado a ≥5 válidas (4 opts/1 correcta/activa/alineada) vía `IQuestionCounter`, protegido por `rowversion` (segundo `Publish` concurrente →409), emitiendo `CategoryPublishedDomainEvent`

**Independent Test**: `DRAFT` con 0 válidas → `POST /publish` →400 `CategoryNotPublishable` (DRAFT); 4 válidas →400; 5ª válida →200 `ACTIVE`; pregunta 3 opts/0 correctas/inactiva/desalineada →no cuenta; `ACTIVE→Deactivate→INACTIVE→Archive→ARCHIVED`, `ARCHIVED→Publish` →400; dos `Publish` concurrentes → uno 200, otro 409

### Tests for User Story 2 (write FIRST)

- [x] T021 [P] [US2] Domain unit tests for `Category.Publish` gate with `FakeQuestionCounter` (0,4,5 válidas) and state transitions `Activate/Deactivate/Archive` in `tests/OroQuizClash.Domain.Tests/Categories/CategoryLifecycleTests.cs` (Arrange `InMemoryQuestionCounter.Seed(id,4)` → Assert Publish Fail, Seed 5 → Publish Success + `CategoryPublishedDomainEvent`)
- [x] T022 [P] [US2] Contract tests for lifecycle endpoints `POST /api/categories/{id}/activate|deactivate|publish|archive` in `tests/OroQuizClash.Api.Tests/Contracts/CategoryLifecycleContractTests.cs` (asserts 200 vs 400 `CategoryNotPublishable` vs 409)
- [x] T023 [P] [US2] Concurrency test for duplicate `PublishCategory` with stale `RowVersion` in `tests/OroQuizClash.Infrastructure.Tests/Persistence/CategoryConcurrencyTests.cs` (two contexts, second `SaveChanges` → `DbUpdateConcurrencyException` →409)

### Implementation for User Story 2

- [x] T024 [P] [US2] Implement `IBusinessRule` `CategoryMustHaveFiveValidQuestionsRule` and `CategoryStateTransitionRule` in `src/OroQuizClash.Domain/Categories/Rules/` (e.g., `CategoryMustHaveFiveValidQuestionsRule.cs`)
- [x] T025 [US2] Extend `Category` with `Activate()`, `Deactivate()`, `Publish(IQuestionCounter)` gate, `Archive()` in `src/OroQuizClash.Domain/Categories/Category.cs` (validates `Status∈{DRAFT,INACTIVE}` for `Publish`, `count≥5`, transitions to `ACTIVE`, `RowVersion` check; `Activate`/`Deactivate`/`Archive` con matriz estados)
- [x] T026 [P] [US2] Create `CategoryPublishedDomainEvent` and `CategoryArchivedDomainEvent : DomainEvent` in `src/OroQuizClash.Domain/Categories/Events/` (e.g., `CategoryPublishedDomainEvent.cs`)
- [x] T027 [US2] Implement Vertical Slices `ActivateCategory`, `DeactivateCategory`, `PublishCategory`, `ArchiveCategory` in `src/OroQuizClash.Application/Features/Categories/` (one file per slice: `PublishCategory.cs` with `PublishCategoryCommand(CategoryId) : ICommand<Result<CategoryResponse>>`, `Handler` loads via `IRepository`, calls `category.Publish(questionCounter)`, `SaveChangesAsync`; `ActivateCategory.cs` etc. thin `IEndpoint` `POST /api/categories/{id}/activate`)
- [x] T028 [US2] Update `InMemoryQuestionCounter` to validate alignment `Difficulty/AcademicLevel/AgeRange` vs `Category` in `src/OroQuizClash.Infrastructure/Counters/InMemoryQuestionCounter.cs` (ensures `FR-007` — pregunta desalineada no cuenta)

**Checkpoint**: US2 green — `Publish` gate ≥5 100% rechazado <5, 5→ACTIVE <2s, `ACTIVE→INACTIVE→ARCHIVED` 100%, `ARCHIVED→Publish` 400, concurrencia 409, `quickstart.md` P1 Publish scenario green

---

## Phase 5: User Story 3 — Consulta y filtrado de categorías (Priority: P2)

**Goal**: `GET /api/categories` filtrado por `knowledgeArea/academicLevel/age/difficulty/state/tag` con paginación y `GET /api/categories/{id}` con `validQuestionsCount` derivado, usado por `CreateGame` para validar `CategoryId` publicado

**Independent Test**: Crear 3 categorías (Humanidades/Secundaria/ACTIVE, Ciencias/Universidad/INACTIVE, Humanidades/Secundaria/ACTIVE) → `GET ?knowledgeArea=Humanidades&academicLevel=Secundaria&state=ACTIVE` →2 (A,C); `?tag=álgebra` → incluye; `CreateGame` con `ARCHIVED`/`INACTIVE` →400 `CategoryNotReady`

### Tests for User Story 3 (write FIRST)

- [x] T029 [P] [US3] Contract tests for `GET /api/categories` filtering and pagination + `GET /api/categories/{id}` in `tests/OroQuizClash.Api.Tests/Contracts/CategoryQueryContractTests.cs` (asserts filtrado 100% precisión con 20 items, paginación `page/pageSize`)
- [x] T030 [P] [US3] Specification test for `CategoryFilterSpecification` (knowledgeArea, academicLevel, state, tag, ageRange) in `tests/OroQuizClash.Infrastructure.Tests/Specifications/CategoryFilterSpecificationTests.cs` (`IsSatisfiedBy` + EF translation)

### Implementation for User Story 3

 - [x] T034 [P] [US3] Implement `CategoryFilterSpecification : Specification<Category>` and `CategoryByIdSpecification` with `Where` combinados + `Pagination` + `AsNoTracking` in `src/OroQuizClash.Infrastructure/Specifications/CategoryFilterSpecification.cs`
 - [x] T034 [US3] Implement Vertical Slice `GetCategories` query in `src/OroQuizClash.Application/Features/Categories/GetCategories.cs` with `GetCategoriesQuery(KnowledgeArea?, AcademicLevel?, AgeMin?, AgeMax?, DifficultyLevel?, State?, Tag?, Page, PageSize) : IQuery<Result<PaginatedResponse>>`, `Handler` (`IRepository` + `CategoryFilterSpecification` + `IQuestionCounter` para `validQuestionsCount`), `Validator`, `Endpoint : IEndpoint` `GET /api/categories`
 - [x] T034 [US3] Implement Vertical Slice `GetCategoryById` in `src/OroQuizClash.Application/Features/Categories/GetCategoryById.cs` with `GetCategoryByIdQuery(CategoryId) : IQuery<Result<CategoryResponse>>`, `Handler` (`FirstOrDefaultAsync` + `CategoryByIdSpecification`), `Endpoint` `GET /api/categories/{id}`
 - [x] T034 [US3] Extend `CreateGameHandler` integration point (002→001) — ensure `ICategoryValidator` or `IQuestionCounter` check for `CategoryNotReady` when `Category Status != ACTIVE` or `<5` valid in `src/OroQuizClash.Application/Features/Games/CreateGame.cs` (add `CategoryStatus` guard, returns `CategoryNotReady` if filtered query shows not `ACTIVE`)

**Checkpoint**: US3 green — filtrado `state=ACTIVE` 100% precisión, `tag=álgebra` incluido, paginación sin filtrar filtrados, `GetCategoryById` con `validQuestionsCount`, `CreateGame` con `ARCHIVED` →400

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Hardening, docs, quickstart validation, DoD checklist

- [x] T035 [P] Add `ProblemDetails` mapping tests for all `CategoryErrors` codes (`InvalidCategoryConfiguration`, `CategoryNotPublishable`, `InvalidCategoryState`, `CategoryNotFound`) in `tests/OroQuizClash.Api.Tests/Errors/CategoryErrorsMappingTests.cs`
- [x] T036 [P] Run `quickstart.md` end-to-end validation (create→update→publish gate 0/4/5 →activate/deactivate/archive→filter→concurrencia) and fix gaps in `specs/002-categories/quickstart.md` (execute curl scenarios, confirm <1s create, <2s publish, 409 on concurrent)
- [x] T037 [P] Add structured logging fields `CategoryId`/`Status`/`Command`/`Duration` via `LoggingBehavior` verification in `tests/OroQuizClash.Application.Tests/Pipeline/CategoryLoggingBehaviorTests.cs`
- [x] T038 [P] Update `docs/adr/ADR-011-categories.md` documenting decisions from `research.md` (AggregateRoot+ValueObjects, DRAFT→ACTIVE→INACTIVE→ARCHIVED, IQuestionCounter port, rowversion)
- [x] T039 Security hardening: verify `POST/PUT/POST publish` require `ADMIN/GAME_MANAGER` (no anonymous), rate limiting via `ServiceDefaults`, `correlationId` propagation, no sensitive data logged in `src/OroQuizClash.Api/Program.cs` and `CategoryEndpoints`
- [x] T040 Performance smoke: `dotnet test --filter SC-001` timing assert crear <1s y publish <2s (95% p95) in `tests/OroQuizClash.Api.Tests/Performance/CategoryPerformanceTests.cs`
- [x] T041 Final `dotnet build OroQuizClash.slnx && dotnet test` green, `dotnet format` clean, update `specs/002-categories/spec.md` Status to `Ready for Review` and sync `specs/002-categories/checklists/requirements.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately (reuses 001 foundation)
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (Category base + IQuestionCounter + RowVersion)
- **User Stories (Phase 3+)**: Depend on Foundational; US1 (P1) delivers MVP (CRUD DRAFT), US2 (P1) depends on US1's `Category.cs` (extends publish gate), US3 (P2) depends on US1's `Category.cs` + US2's `Publish` (filter ACTIVE)
- **Polish (Final Phase)**: Depends on all desired stories complete

### User Story Dependencies

- **US1 (P1)**: Can start after Foundational — no dependencies on other stories; delivers MVP (categoría creable y actualizable en DRAFT)
- **US2 (P1)**: Depends on US1's `Category.cs` — adds `Publish` gate ≥5 and lifecycle `Activate/Deactivate/Archive`; independently testable after US1 (publish con 0/4/5)
- **US3 (P2)**: Depends on US1's `Category.cs` + `CategoryFilterSpecification` — adds `GET` filtrado; independently testable (filter con 20 items)

### Within Each User Story

- Tests FIRST → FAIL → Implementation → PASS
- ValueObjects/Enumerations → Rules → Aggregate → EF Config → Slice (Validator → Handler → Endpoint) → Specification → Integration tests
- `IQuestionCounter` stub seeded before `Publish` tests

### Parallel Opportunities

- Phase 2: T005+T006 (Enumeration+VOs) parallel; T008 (EF config) + T009 (IQuestionCounter) parallel
- Phase 3: T013-T015 (contract/domain/handler tests) parallel; T016 (Rules) parallel; T018 (events) parallel
- Phase 4: T021-T023 (domain/contract/concurrency tests) parallel; T024 (rules) + T026 (events) parallel
- Phase 5: T029-T030 (query/spec tests) parallel
- Phase 6: T035-T038 (errors/quickstart/logging/ADR) parallel

### Parallel Example: User Story 1

```bash
# Tests in parallel:
Task T013: Contract test in tests/OroQuizClash.Api.Tests/Contracts/CategoryCreateUpdateContractTests.cs
Task T014: Domain unit tests in tests/OroQuizClash.Domain.Tests/Categories/CategoryCreateUpdateTests.cs
Task T015: Handler test in tests/OroQuizClash.Application.Tests/Features/Categories/CategoryHandlersTests.cs

# Models in parallel:
Task T005: CategoryStatus in src/OroQuizClash.Domain/Categories/CategoryStatus.cs
Task T006: AgeRange + CategoryTags in src/OroQuizClash.Domain/Categories/ValueObjects/
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup + Phase 2: Foundational
2. Complete Phase 3: US1 (Create/Update en DRAFT) — delivers MVP: catálogo creable
3. **STOP and VALIDATE**: `dotnet test --filter CategoryCreateUpdate` + `quickstart.md` P1 create/update (201 vs 400) + `GET` idéntico
4. Deploy/demo if ready — `POST /api/categories` sin gate aún

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. Add US1 → Test independently → Demo MVP (CRUD DRAFT)
3. Add US2 → Test Publish gate 0/4/5 + lifecycle + 409 → Demo (gate no-bypass)
4. Add US3 → Test filtrado `state=ACTIVE&knowledgeArea=X` → Demo (descubrimiento + Game integration)
5. Polish → quickstart + performance + ADR + security hardening

### Parallel Team Strategy

With multiple developers after Foundational:
- Developer A: US1 (Category + Create/Update)
- Developer B: US2 (Publish gate + Activate/Deactivate/Archive) — after A's `Category.cs` merges
- Developer C: US3 (GetCategories filtering) — after A's `Category.cs` + `CategoryFilterSpecification` merges
- All stories integrate via same `Category` aggregate without conflicts if `Category.cs` changes are coordinated

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story independently completable and testable per spec `Independent Test`
- Verify tests fail before implementing (TDD per constitution Testing Strategy)
- Commit after each task or logical group; stop at any checkpoint to validate story
- File paths are absolute per plan.md; adjust if `src/` prefix changes
- `BuildingBlocks` must not be modified; only referenced via ProjectReference
- `IQuestionCounter` stub `InMemoryQuestionCounter` se reemplaza por `EfQuestionCounter` en SPEC-003 sin tocar `Category` aggregate
- `OroIdentityServer` Podman `oroidentityserver:latest` — tests usan JWT mock `ADMIN` o Testcontainers con `admin/Admin@123456`

