# Tasks: Admin Categories

**Input**: Design documents from `/specs/020-admin-categories/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: No TDD contract-test tasks requested in spec; validation via quickstart.md + Architecture Tests in Polish phase.

**Organization**: Tasks grouped by user story (P1, P1, P2) per strict checklist format `- [ ] Txxx [P]? [USy]? Description with file path`.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify existing Admin shell and prepare categories feature scaffolding

- [X] T001 Verify Admin shell baseline from SPEC-017 in `src/Admin/QuizArena.Admin/Program.cs` and `src/Admin/QuizArena.Admin.Client/Program.cs` (BFF YARP, OIDC, ServiceDefaults)
- [X] T002 Create categories feature directories `src/Admin/QuizArena.Admin/Components/Categories/` and `src/Admin/QuizArena.Admin.Client/Models/Categories/` and `src/Admin/QuizArena.Admin.Client/Pages/Categories/`
- [X] T003 Verify Design System tokens available at `design-system/tokens/design-tokens.css` and theme `administration` in `src/Admin/QuizArena.Admin/Components/App.razor`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared DTOs, service contract, catalogs — MUST complete before any user story

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 [P] Create admin state enum `CategoryStateView` (4 states Draft/Active/Inactive/Archived) with mapping via `CategoryStateViewMap` in `src/Admin/QuizArena.Admin.Client/Models/Categories/CategoryStateView.cs`
- [X] T005 [P] Create metadata DTOs `CategoryMetadata` (tags, color, icon) and `ProgressionRule` enum in `src/Admin/QuizArena.Admin.Client/Models/Categories/CategoryMetadata.cs`
- [X] T006 [P] Create shared DTOs `Category`, `CategorySummary`, `CategoryDetail`, `CategoryStateTransition`, `CategoryAuditEntry`, `CreateCategoryRequest`/`UpdateCategoryRequest` in `src/Admin/QuizArena.Admin.Client/Models/Categories/Category.cs`
- [X] T007 Create/extend shared service contract `ICategoriesService` with Create/Update/List/Get/Publish/Deactivate/Activate/Archive in `src/Admin/QuizArena.Admin.Client/Services/ICategoriesService.cs`
- [X] T008 Create static catalogs `CategoryCatalogs` for progression rules and example areas (Matemáticas–Finanzas) in `src/Admin/QuizArena.Admin.Client/Services/CategoryCatalogs.cs`
- [X] T009 Verify BFF forwarder catch-all `MapBffForwarder()` exists and covers `/bff/categories*` and transitions in `src/Admin/QuizArena.Admin/Services/BffForwarderExtensions.cs`

**Checkpoint**: Foundation ready — user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Crear y gestionar categorías base (Priority: P1) 🎯 MVP

**Goal**: Admin crea categoría con 10 campos válidos (nombre, descripción, área, nivel, edad, dificultad, público objetivo, estado, metadatos, progresión) → Draft con validación completa, unicidad case-insensitive y persistencia transaccional

**Independent Test**: Login ADMIN → /admin/categories → "Crear categoría" → completar 10 campos válidos (nombre 3–100, área 2–100, edad 0–120, dificultad 1–5) → guardar → verificar listado muestra Draft coherente; nombre duplicado → 409 CategoryAlreadyExists; REWARD_MANAGER → Access Denied

### Implementation for User Story 1

- [X] T010 [P] [US1] Extend `CategoryForm` validation for 10 fields (Name 3–100 unique, KnowledgeArea/AcademicLevel/TargetAudience 2–100, AgeMin/AgeMax 0–120 min≤max, Difficulty 1–5, Tags 0–10, Progression in catalog) in `src/Admin/QuizArena.Admin.Client/Models/Categories/CategoryForm.cs`
- [X] T011 [P] [US1] Implement `ClientCategoriesService.CreateAsync`/`UpdateAsync` calling `POST/PUT /bff/categories` via HttpClient in `src/Admin/QuizArena.Admin.Client/Services/ClientCategoriesService.cs`
- [X] T012 [P] [US1] Implement `ServerCategoriesService` with `HttpClient http://oroclash-api` + Bearer from HttpContext for Create/Update in `src/Admin/QuizArena.Admin/Services/ServerCategoriesService.cs`
- [X] T013 [P] [US1] Create `CategoryForm.razor` component (10 inputs, per-field errors, aria-live, 44px targets, Draft display) in `src/Admin/QuizArena.Admin/Components/Categories/CategoryForm.razor`
- [X] T014 [P] [US1] Create `CategoryStateBadge.razor` for 4 states with color mapping and ValidQuestionCount tooltip in `src/Admin/QuizArena.Admin/Components/Categories/CategoryStateBadge.razor`
- [X] T015 [US1] Create `CategoryCreate.razor` page (form + submit → 201 + rowVersion, FieldErrors, preserve draft on 401) in `src/Admin/QuizArena.Admin.Client/Pages/Categories/CategoryCreate.razor`
- [X] T016 [US1] Create `CategoryEdit.razor` page (load by id, bind 10 fields, editable while Draft/Active/Inactive, block after Archived) in `src/Admin/QuizArena.Admin.Client/Pages/Categories/CategoryEdit.razor`
- [X] T017 [US1] Wire DI for `ICategoriesService` via `AddAdminApiHttpClient` pattern in `src/Admin/QuizArena.Admin/Program.cs` and `src/Admin/QuizArena.Admin.Client/Program.cs`

**Checkpoint**: US1 fully functional and independently testable — 10-field creation/edition with Draft and validation per spec scenarios 1–5

---

## Phase 4: User Story 2 - Publicar, organizar y dar vida a la categoría (Priority: P1)

**Goal**: Draft → Active (publish requiere ≥5 preguntas válidas 4 opciones/1 correcta) ↔ Inactive → Archived (terminal, bloquea si tiene juegos Running/Scheduled) con 8 ejemplos (Matemáticas–Finanzas) y listado paginado con filtros

**Independent Test**: Crear categoría Draft → añadir 5 preguntas válidas → Publish → Active; 4 preguntas → 400 CategoryNotReady; Active↔Inactive; Active/Inactive → Archived con juegos activos → 409 CategoryInUse; filtro área/búsqueda pagina <2s

### Implementation for User Story 2

- [X] T018 [P] [US2] Implement `ClientCategoriesService` transition methods `PublishAsync`/`DeactivateAsync`/`ActivateAsync`/`ArchiveAsync` calling `POST /bff/categories/{id}/*` with If-Match RowVersion in `src/Admin/QuizArena.Admin.Client/Services/ClientCategoriesService.cs`
- [X] T019 [P] [US2] Implement `ServerCategoriesService` transitions with `If-Match` RowVersion forwarding to `http://oroclash-api/api/categories/{id}/*` in `src/Admin/QuizArena.Admin/Services/ServerCategoriesService.cs`
- [X] T020 [P] [US2] Create `CategoryTransitionsBar.razor` with buttons Publish/Deactivate/Activate/Archive enabled by `CategoryStateView` and `ValidQuestionCount` in `src/Admin/QuizArena.Admin/Components/Categories/CategoryTransitionsBar.razor`
- [X] T021 [US2] Create `CategoriesList.razor` paginated list (`GET /bff/categories?status=&area=&search=&page=`) with filters by 4 states and knowledge area (8 ejemplos), skeleton, no full load in `src/Admin/QuizArena.Admin.Client/Pages/Categories/CategoriesList.razor`
- [X] T022 [US2] Create `CategoryDetail.razor` showing 10-field read view, `ValidQuestionCount` badge, immutable highlight after Archived, history `CategoryStateTransition` in `src/Admin/QuizArena.Admin.Client/Pages/Categories/CategoryDetail.razor`
- [X] T023 [US2] Handle `RowVersion` optimistic concurrency: send `If-Match`, map `409 ConcurrencyConflict` to field error with reload option in `src/Admin/QuizArena.Admin/Components/Categories/CategoryForm.razor`
- [X] T024 [US2] Wire authorization `AdminOrGameManager` on categories pages; REWARD_MANAGER gets Access Denied UI + 403 on API in `src/Admin/QuizArena.Admin.Client/Pages/Categories/CategoriesList.razor` and `src/Admin/QuizArena.Admin.Client/Pages/Categories/CategoryCreate.razor`

**Checkpoint**: US1 and US2 both independently functional — creation/edition + 4-state lifecycle with 8 examples

---

## Phase 5: User Story 3 - Configurar metadatos, público objetivo y reglas de progresión (Priority: P2)

**Goal**: Configurar metadatos (tags 0–10, color #RRGGBB, icono Lucide), público objetivo y reglas de progresión (Linear/Progressive/Adaptive/CategorySpecific) con validación por campo y solo-lectura tras Archived

**Independent Test**: Editar Draft/Active → definir TargetAudience, Tags ["álgebra","cálculo"] + color #2563EB + progresión Adaptive → guardar ok; luego tags 11/duplicados/color inválido/progresión fuera de catálogo → 400 por campo; tras Archived → solo lectura y 422

### Implementation for User Story 3

- [X] T025 [P] [US3] Implement metadata inputs (tags 0–10, 2–30, no duplicates, color hex, icon Lucide) with per-field validation in `src/Admin/QuizArena.Admin/Components/Categories/CategoryForm.razor`
- [X] T026 [P] [US3] Implement progression rule select (Linear/Progressive/Adaptive/CategorySpecific) with closed catalog validation in `src/Admin/QuizArena.Admin/Components/Categories/CategoryForm.razor`
- [X] T027 [US3] Enforce immutability after Archived: render form read-only and block `UpdateAsync` via disabled submit + API 422 mapping in `src/Admin/QuizArena.Admin.Client/Pages/Categories/CategoryEdit.razor`
- [X] T028 [US3] Implement area KnowledgeArea free-text with 8 examples as seed/demo and search filter in `src/Admin/QuizArena.Admin.Client/Services/CategoryCatalogs.cs`
- [X] T029 [US3] Add a11y and responsive polish for categories form/list (focus visible, aria-live per-field errors, 375–1536 no scroll, 44px targets) in `src/Admin/QuizArena.Admin/Components/Categories/CategoryForm.razor`

**Checkpoint**: All user stories independently functional — full 10-field configurable categories with lifecycle

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Governance, a11y, concurrency, audit, and validation per quickstart.md

- [X] T030 [P] Run Design System token gate `node design-system/validate-tokens.cjs --dir src/Admin --strict` and fix literal violations in `src/Admin/QuizArena.Admin/Components/Categories/*`
- [X] T031 [P] Add/extend `CategoryTests` (10 fields validation, uniqueness, tags, rowversion) in `tests/QuizArena.Admin.Tests/CategoryTests.cs`
- [X] T032 [P] Add/extend `CategoryStateTransitionTests` (4 states, guards ≥5, CategoryInUse, concurrency 409, auth 403) in `tests/QuizArena.Admin.Tests/CategoryStateTransitionTests.cs`
- [X] T033 Verify `DesignSystemNoDirectDbTests` / `AdminBffTests` still pass for new categories services in `tests/OroQuizClash.Architecture.Tests/AdminBffTests.cs`
- [X] T034 Run quickstart.md validation scenarios V1-V4 (Aspire AppHost, ADMIN/GAME_MANAGER vs REWARD_MANAGER, 10 fields, 4-state lifecycle, 8 examples, concurrency, pagination) per `specs/020-admin-categories/quickstart.md`
- [X] T035 [P] Cross-cutting polish: loading skeletons timing, error `ProblemDetails` without leak, `CorrelationId` logged, responsive 375/768/1024/1440/1536 manual audit in `src/Admin/QuizArena.Admin/Components/Categories/*`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational
  - US1 and US2 are both P1 — can proceed in parallel or US1 first for MVP
  - US3 (P2) depends on US1 form being stable (metadata builds on it)
- **Polish (Phase 6)**: Depends on all desired user stories

### User Story Dependencies

- **US1 (P1)**: After Foundational, no other story dependency — MVP slice (create 10 fields Draft)
- **US2 (P1)**: After Foundational, independent of US1 but shares CategoryEdit/Detail; can run in parallel with US1 by different developers (merge care on CategoryForm)
- **US3 (P2)**: After Foundational + US1 (needs 10-field form for metadata)

### Within Each User Story

- Models/catalogs before services, services before components, components before page integration
- Validation before integration, immutability before transitions

### Parallel Opportunities

- T004, T005, T006 can run in parallel (different files)
- T010, T011, T012, T013, T014 can run in parallel within US1 (different files)
- T018, T019, T020 can run in parallel within US2
- T025, T026 can run in parallel within US3
- T030, T031, T032 can run in parallel in Polish

---

## Parallel Example: User Story 1

```bash
# Launch US1 models/services/components together:
Task: "Extend CategoryForm validation in src/Admin/QuizArena.Admin.Client/Models/Categories/CategoryForm.cs"
Task: "Implement ClientCategoriesService.CreateAsync in src/Admin/QuizArena.Admin.Client/Services/ClientCategoriesService.cs"
Task: "Create CategoryForm.razor in src/Admin/QuizArena.Admin/Components/Categories/CategoryForm.razor"
Task: "Create CategoryStateBadge.razor in src/Admin/QuizArena.Admin/Components/Categories/CategoryStateBadge.razor"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004-T009) — shared enums/metadata/DTOs + ICategoriesService + BFF verify
3. Complete Phase 3: US1 (T010-T017)
4. **STOP and VALIDATE**: Login ADMIN → create category with 10 fields → verify Draft + persisted values per quickstart V1
5. Deploy/demo if ready — creation without lifecycle

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. Add US1 → Test independently → Deploy/Demo (MVP! — SC-001/SC-005)
3. Add US2 → Test independently → Deploy/Demo (+ SC-002/SC-009 4-state + 8 examples)
4. Add US3 → Test independently → Deploy/Demo (+ SC-003/SC-004 metadata)

### Parallel Team Strategy

With multiple developers after Foundational:

- Developer A: US1 (T010-T017)
- Developer B: US2 (T018-T024) — coordinate on CategoryForm merge
- Developer C: US3 prep (T025-T026) after US1 stabilizes

---

## Notes

- [P] = different files, no dependencies — safe to parallelize
- [US*] = traceability to spec.md user stories
- Each user story independently completable and testable per its Independent Test
- Exact file paths per task — LLM can execute without additional context
- 10 fields: Name, Description, KnowledgeArea, AcademicLevel, AgeMin/AgeMax, Difficulty, TargetAudience, Status+RowVersion, Metadata (tags/color/icon), ProgressionRule
- 8 examples: Matemáticas, Historia, Ciencia, Tecnología, Geografía, Literatura, Programación, Finanzas (seed/demo, not closed catalog)
- 4 states: Draft→Active↔Inactive→Archived (guards ≥5, CategoryInUse, rowversion)
- Constitution gates: Domain First, Category Invariants (B), Configurable Rules (C), Concurrency (F), BFF + OIDC + ServiceDefaults
