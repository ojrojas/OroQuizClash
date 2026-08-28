# Tasks: Admin Question Bank

**Input**: Design documents from `/specs/021-admin-question-bank/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: No TDD contract-test tasks requested in spec; validation via quickstart.md + Architecture Tests in Polish phase.

**Organization**: Tasks grouped by user story (P1, P1, P2) per strict checklist format `- [ ] Txxx [P]? [USy]? Description with file path`.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify existing Admin shell and prepare question-bank feature scaffolding

- [X] T001 Verify Admin shell baseline from SPEC-017 in `src/Admin/QuizArena.Admin/Program.cs` and `src/Admin/QuizArena.Admin.Client/Program.cs` (BFF YARP, OIDC, ServiceDefaults)
- [X] T002 Create question-bank feature directories `src/Admin/QuizArena.Admin/Components/Questions/` and `src/Admin/QuizArena.Admin.Client/Models/Questions/` and `src/Admin/QuizArena.Admin.Client/Pages/Questions/`
- [X] T003 Verify Design System tokens available at `design-system/tokens/design-tokens.css` and theme `administration` in `src/Admin/QuizArena.Admin/Components/App.razor`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared DTOs, service contract, catalogs — MUST complete before any user story

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 [P] Create question state enum `QuestionStateView` (4 states Draft/Active/Inactive/Archived) with mapping via `QuestionStateViewMap` in `src/Admin/QuizArena.Admin.Client/Models/Questions/QuestionStateView.cs`
- [X] T005 [P] Create answer DTOs `AnswerOption`, `OptionForm` and `Question` aggregate DTOs in `src/Admin/QuizArena.Admin.Client/Models/Questions/Question.cs`
- [X] T006 [P] Create shared DTOs `QuestionSummary`, `QuestionDetail`, `QuestionStateTransition`, `QuestionAuditEntry`, `CreateQuestionRequest`/`UpdateQuestionRequest`, `QuestionFilter`, `QuestionStatistics`, `SystemConfig` in `src/Admin/QuizArena.Admin.Client/Models/Questions/Question.cs`
- [X] T007 Create/extend shared service contract `IQuestionsService` with Create/Update/List/Get/Activate/Deactivate/Delete + Stats/Config in `src/Admin/QuizArena.Admin.Client/Services/IQuestionsService.cs`
- [X] T008 Create static catalogs `QuestionCatalogs` for difficulty 1–5, academic levels, time 5–300, age 0–120 in `src/Admin/QuizArena.Admin.Client/Services/QuestionCatalogs.cs`
- [X] T009 Verify BFF forwarder catch-all `MapBffForwarder()` exists and covers `/bff/questions*` and stats in `src/Admin/QuizArena.Admin/Services/BffForwarderExtensions.cs`

**Checkpoint**: Foundation ready — user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Crear y gestionar el núcleo de preguntas (Priority: P1) 🎯 MVP

**Goal**: Admin crea pregunta con texto 10–500, categoría Active, dificultad 1–5, nivel 2–100, edad 0–120, tiempo 5–300, explicación 0–1000, y exactamente 4 respuestas A–D con 1 correcta → Draft/Active con validación 4/1 y persistencia transaccional

**Independent Test**: Login ADMIN → /admin/questions → "Crear pregunta" → completar 9 campos + 4 opciones 1 correcta → guardar → verificar listado muestra pregunta con 4 respuestas 1 correcta; 3 opciones/2 correctas → 400 InvalidQuestionData; categoría inexistente → CategoryNotReady; REWARD_MANAGER → Access Denied

### Implementation for User Story 1

- [X] T010 [P] [US1] Extend `QuestionForm` validation for 9 fields + 4 answers (Text 10–500, CategoryId required, Difficulty 1–5, AcademicLevel 2–100, Age 0–120 min≤max, Time 5–300, Explanation 0–1000, Options 4/1) in `src/Admin/QuizArena.Admin.Client/Models/Questions/QuestionForm.cs`
- [X] T011 [P] [US1] Implement `ClientQuestionsService.CreateAsync`/`UpdateAsync` calling `POST/PUT /bff/questions` via HttpClient in `src/Admin/QuizArena.Admin.Client/Services/ClientQuestionsService.cs`
- [X] T012 [P] [US1] Implement `ServerQuestionsService` with `HttpClient http://oroclash-api` + Bearer from HttpContext for Create/Update in `src/Admin/QuizArena.Admin/Services/ServerQuestionsService.cs`
- [X] T013 [P] [US1] Create `QuestionForm.razor` component (9 inputs + 4 answer rows A–D with radio Correct, per-field errors, aria-live, 44px targets) in `src/Admin/QuizArena.Admin/Components/Questions/QuestionForm.razor`
- [X] T014 [P] [US1] Create `AnswerOptionRow.razor` component (position A–D, text input, correct radio, validation) in `src/Admin/QuizArena.Admin/Components/Questions/AnswerOptionRow.razor`
- [X] T015 [P] [US1] Create `QuestionStateBadge.razor` for 4 states with color mapping in `src/Admin/QuizArena.Admin/Components/Questions/QuestionStateBadge.razor`
- [X] T016 [US1] Create `QuestionCreate.razor` page (form + submit → 201 + rowVersion, FieldErrors, preserve draft on 401) in `src/Admin/QuizArena.Admin.Client/Pages/Questions/QuestionCreate.razor`
- [X] T017 [US1] Create `QuestionEdit.razor` page (load by id, bind 9 fields + 4 answers, re-validate 4/1 on save, rowVersion) in `src/Admin/QuizArena.Admin.Client/Pages/Questions/QuestionEdit.razor`
- [X] T018 [US1] Wire DI for `IQuestionsService` via `AddAdminApiHttpClient` pattern in `src/Admin/QuizArena.Admin/Program.cs` and `src/Admin/QuizArena.Admin.Client/Program.cs`

**Checkpoint**: US1 fully functional and independently testable — 4/1 creation/edition with 9 fields and validation per spec scenarios 1–5

---

## Phase 4: User Story 2 - Operar el ciclo de vida y consultar el banco (Priority: P1)

**Goal**: Active ↔ Inactive (toggle ValidQuestionCount) + Delete (cuando no está en uso QuestionInUse) + estadísticas agregadas por categoría/dificultad/estado/tiempo + guarda CategoryMinQuestions (inicial 5) para publicar categoría

**Independent Test**: Tomar pregunta Active → Deactivate → Inactive (no cuenta para ValidQuestionCount); Reactivate → Active; Delete nunca usada → desaparece; usada en juego Running → 409 QuestionInUse; estadísticas → ver agregados por categoría/dificultad; categoría con 4 preguntas → publish 400 CategoryNotReady

### Implementation for User Story 2

- [X] T019 [P] [US2] Implement `ClientQuestionsService` transition methods `ActivateAsync`/`DeactivateAsync`/`DeleteAsync` calling `POST/DELETE /bff/questions/{id}/*` with If-Match RowVersion in `src/Admin/QuizArena.Admin.Client/Services/ClientQuestionsService.cs`
- [X] T020 [P] [US2] Implement `ServerQuestionsService` transitions with `If-Match` RowVersion forwarding to `http://oroclash-api/api/questions/{id}/*` in `src/Admin/QuizArena.Admin/Services/ServerQuestionsService.cs`
- [X] T021 [P] [US2] Create `QuestionTransitionsBar.razor` with buttons Activate/Deactivate/Delete enabled by `QuestionStateView` and `InUseByLiveGame` in `src/Admin/QuizArena.Admin/Components/Questions/QuestionTransitionsBar.razor`
- [X] T022 [US2] Create `QuestionsList.razor` paginated list (`GET /bff/questions?categoryId=&difficulty=&status=&search=&page=`) with filters by 4 states, category, difficulty, search, skeleton, no full load in `src/Admin/QuizArena.Admin.Client/Pages/Questions/QuestionsList.razor`
- [X] T023 [US2] Create `QuestionDetail.razor` showing 9 fields + 4 answers highlighting correct + explanation + `InUseByLiveGame` flag + history in `src/Admin/QuizArena.Admin.Client/Pages/Questions/QuestionDetail.razor`
- [X] T024 [US2] Create `QuestionStatsPanel.razor` showing total, byCategory, byDifficulty, byStatus, avgTime, validPerCategory (valid/required) via `GET /bff/questions/stats` in `src/Admin/QuizArena.Admin/Components/Questions/QuestionStatsPanel.razor`
- [X] T025 [US2] Handle `RowVersion` optimistic concurrency: send `If-Match`, map `409 ConcurrencyConflict` to field error with reload option in `src/Admin/QuizArena.Admin/Components/Questions/QuestionForm.razor`
- [X] T026 [US2] Wire authorization `AdminOrGameManager` on questions pages; REWARD_MANAGER gets Access Denied UI + 403 on API in `src/Admin/QuizArena.Admin.Client/Pages/Questions/QuestionsList.razor` and `src/Admin/QuizArena.Admin.Client/Pages/Questions/QuestionCreate.razor`

**Checkpoint**: US1 and US2 both independently functional — creation/edition + lifecycle + stats with CategoryMinQuestions guard

---

## Phase 5: User Story 3 - Configurar atributos avanzados y validar invariantes cruzadas (Priority: P2)

**Goal**: Configurar dificultad, nivel académico, edad, tiempo, explicación con feedback por campo y validar que categoría no sea publicable si `ValidQuestionCount < CategoryMinQuestions` (inicial 5 configurable) + coherencia `QuestionInUse` para edición en uso

**Independent Test**: Editar Draft → definir dificultad 5, nivel Universitario, edad 18–25, tiempo 45, explicación 200 → guardar ok; luego nivel vacío/edad invertida/tiempo 0/301/explicación 1001 → 400 por campo; cambiar mínimo configurable 5→3 y verificar categoría con 3 ya publicable

### Implementation for User Story 3

- [X] T027 [P] [US3] Implement difficulty/academicLevel/age/time/explanation selects/inputs with closed catalog validation (dificultad 1–5, nivel 2–100, edad 0–120 min≤max, tiempo 5–300, explicación 0–1000) in `src/Admin/QuizArena.Admin/Components/Questions/QuestionForm.razor`
- [X] T028 [US3] Implement category association with `CategoryMinQuestions` guard: show `ValidQuestionCount/Required` and disable Publish if <5, plus warning "Rango/nivel incoherente" without blocking in `src/Admin/QuizArena.Admin/Components/Questions/QuestionForm.razor`
- [X] T029 [US3] Enforce `QuestionInUse` guard: block edit/delete when `InUseByLiveGame` true, map 409 to field error with clone option in `src/Admin/QuizArena.Admin.Client/Pages/Questions/QuestionEdit.razor`
- [X] T030 [US3] Add a11y and responsive polish for question form/list (focus visible, aria-live per-field errors, 375–1536 no scroll, 44px targets) in `src/Admin/QuizArena.Admin/Components/Questions/QuestionForm.razor`

**Checkpoint**: All user stories independently functional — full 9-field + 4/1 bank with lifecycle and cross-category validation

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Governance, a11y, concurrency, audit, and validation per quickstart.md

- [X] T031 [P] Run Design System token gate `node design-system/validate-tokens.cjs --dir src/Admin --strict` and fix literal violations in `src/Admin/QuizArena.Admin/Components/Questions/*`
- [X] T032 [P] Add/extend `QuestionTests` (9 fields + 4/1 + rowversion + QuestionInUse) in `tests/QuizArena.Admin.Tests/QuestionTests.cs`
- [X] T033 [P] Add/extend `QuestionStateTransitionTests` (4 states, guards, CategoryNotReady, QuestionInUse, concurrency 409, auth 403) in `tests/QuizArena.Admin.Tests/QuestionStateTransitionTests.cs`
- [X] T034 Verify `DesignSystemNoDirectDbTests` / `AdminBffTests` still pass for new questions services in `tests/OroQuizClash.Architecture.Tests/AdminBffTests.cs`
- [X] T035 Run quickstart.md validation scenarios V1-V4 (Aspire AppHost, ADMIN/GAME_MANAGER vs REWARD_MANAGER, 9 fields + 4/1, lifecycle, stats, CategoryMinQuestions, concurrency, pagination) per `specs/021-admin-question-bank/quickstart.md`
- [X] T036 [P] Cross-cutting polish: loading skeletons timing, error `ProblemDetails` without leak, `CorrelationId` logged, responsive 375/768/1024/1440/1536 manual audit in `src/Admin/QuizArena.Admin/Components/Questions/*`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational
  - US1 and US2 are both P1 — can proceed in parallel or US1 first for MVP
  - US3 (P2) depends on US1 form being stable (advanced fields build on it)
- **Polish (Phase 6)**: Depends on all desired user stories

### User Story Dependencies

- **US1 (P1)**: After Foundational, no other story dependency — MVP slice (create 4/1 with 9 fields)
- **US2 (P1)**: After Foundational, independent of US1 but shares QuestionEdit/Detail; can run in parallel with US1 by different developers (merge care on QuestionForm)
- **US3 (P2)**: After Foundational + US1 (needs 9-field form for advanced validation)

### Within Each User Story

- Models/catalogs before services, services before components, components before page integration
- Validation before integration, lifecycle before stats

### Parallel Opportunities

- T004, T005, T006 can run in parallel (different files)
- T010, T011, T012, T013, T014, T015 can run in parallel within US1 (different files)
- T019, T020, T021 can run in parallel within US2
- T027, T028 can run in parallel within US3
- T031, T032, T033 can run in parallel in Polish

---

## Parallel Example: User Story 1

```bash
# Launch US1 models/services/components together:
Task: "Extend QuestionForm validation in src/Admin/QuizArena.Admin.Client/Models/Questions/QuestionForm.cs"
Task: "Implement ClientQuestionsService.CreateAsync in src/Admin/QuizArena.Admin.Client/Services/ClientQuestionsService.cs"
Task: "Create QuestionForm.razor in src/Admin/QuizArena.Admin/Components/Questions/QuestionForm.razor"
Task: "Create AnswerOptionRow.razor in src/Admin/QuizArena.Admin/Components/Questions/AnswerOptionRow.razor"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004-T009) — shared enums/DTOs + IQuestionsService + BFF verify
3. Complete Phase 3: US1 (T010-T018)
4. **STOP and VALIDATE**: Login ADMIN → create question with 4/1 → verify 9 fields + persisted 4 answers per quickstart V1
5. Deploy/demo if ready — creation without lifecycle

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. Add US1 → Test independently → Deploy/Demo (MVP! — SC-001/SC-005)
3. Add US2 → Test independently → Deploy/Demo (+ SC-003/SC-009 lifecycle + stats)
4. Add US3 → Test independently → Deploy/Demo (+ SC-004/SC-010 advanced + CategoryMinQuestions)

### Parallel Team Strategy

With multiple developers after Foundational:

- Developer A: US1 (T010-T018)
- Developer B: US2 (T019-T026) — coordinate on QuestionForm merge
- Developer C: US3 prep (T027-T028) after US1 stabilizes

---

## Notes

- [P] = different files, no dependencies — safe to parallelize
- [US*] = traceability to spec.md user stories
- Each user story independently completable and testable per its Independent Test
- Exact file paths per task — LLM can execute without additional context
- 9 fields: Text, CategoryId, Difficulty, AcademicLevel, AgeMin/AgeMax, TimePerQuestion, Explanation, Status+RowVersion + 4 answers (A–D) 1 correcta
- Invariante 4/1: exactamente 4 AnswerOption con 1 IsCorrect, validado en 3 niveles
- 4 estados: Draft→Active↔Inactive→Archived/Deleted + QuestionInUse guard + CategoryMinQuestions (inicial 5 configurable)
- Constitution gates: Domain First, Question & Category Invariants (B), Concurrency (F), BFF + OIDC + ServiceDefaults
