# Tasks: Admin Game Configuration

**Input**: Design documents from `/specs/019-admin-game-configuration/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: No TDD contract-test tasks requested in spec; validation via quickstart.md + Architecture Tests in Polish phase.

**Organization**: Tasks grouped by user story (P1, P1, P2) per strict checklist format `- [ ] Txxx [P]? [USy]? Description with file path`.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify existing Admin shell and prepare game-configuration feature scaffolding

- [X] T001 Verify Admin shell baseline from SPEC-017 in `src/Admin/QuizArena.Admin/Program.cs` and `src/Admin/QuizArena.Admin.Client/Program.cs` (BFF YARP, OIDC, ServiceDefaults)
- [X] T002 Create game-configuration feature directories `src/Admin/QuizArena.Admin/Components/GameConfiguration/` and `src/Admin/QuizArena.Admin.Client/Models/GameConfiguration/` and `src/Admin/QuizArena.Admin.Client/Pages/GameConfiguration/`
- [X] T003 Verify Design System tokens available at `design-system/tokens/design-tokens.css` and theme `administration` in `src/Admin/QuizArena.Admin/Components/App.razor`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared DTOs, service contract, catalogs — MUST complete before any user story

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 [P] Create admin state enum `GameStateView` (8 states Draft..Cancelled) with mapping to domain via `GameStateViewMap` in `src/Admin/QuizArena.Admin.Client/Models/GameConfiguration/GameStateView.cs`
- [X] T005 [P] Create policy catalogs `DifficultyStrategy`, `WithdrawalPolicy`, `LossPolicy`, `ScoringSystem`, `SecuredPointsPolicy` with display names in `src/Admin/QuizArena.Admin.Client/Models/GameConfiguration/PolicyCatalogs.cs`
- [X] T006 [P] Create shared DTOs `GameConfiguration`, `GameSummary`, `GameDetail`, `GameStateTransition`, `GameAuditEntry`, `CreateGameRequest`/`UpdateGameRequest` in `src/Admin/QuizArena.Admin.Client/Models/GameConfiguration/GameConfiguration.cs`
- [X] T007 Create shared service contract `IGameConfigurationService` with Create/Update/List/Get/Transition methods in `src/Admin/QuizArena.Admin.Client/Services/IGameConfigurationService.cs`
- [X] T008 Create static catalogs `GameCatalogs` for selects (difficulty 1–5, strategies, withdrawal/loss/scoring) in `src/Admin/QuizArena.Admin.Client/Services/GameCatalogs.cs`
- [X] T009 Verify BFF forwarder catch-all `MapBffForwarder()` exists and covers `/bff/games*` and transitions in `src/Admin/QuizArena.Admin/Services/BffForwarderExtensions.cs`

**Checkpoint**: Foundation ready — user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Crear y configurar una partida completa antes de iniciar (Priority: P1) 🎯 MVP

**Goal**: Admin crea juego con 16 campos válidos (nombre, descripción, categoría, rondas, maxPlayers, tiempo, dificultad/progresión, puntuación, puntos asegurados, reglas retiro/finalización, premios final/consolación, fecha/hora inicio) → Draft→Configured con validación completa y persistencia transaccional

**Independent Test**: Login ADMIN → /admin/games/new → completar 16 campos válidos (categoría Active ≥5 preguntas, rondas 5–10, tiempo 5–300, dificultad 1–5) → guardar → verificar listado muestra Configured con valores coherentes; categoría inválida → CategoryNotReady sin crear; REWARD_MANAGER → Access Denied

### Implementation for User Story 1

- [X] T010 [P] [US1] Extend `GameConfigurationForm` validation for 16 fields (Name 3–100, Description 0–500, CategoryId required, NumberOfRounds 5–10, MaxPlayers ≥2, TimePerQuestion 5–300, InitialDifficulty 1–5, ScheduledAt futura ≥5m) in `src/Admin/QuizArena.Admin.Client/Models/GameConfiguration/GameConfigurationForm.cs`
- [X] T011 [P] [US1] Implement `ClientGameConfigurationService.CreateAsync`/`UpdateAsync` calling `POST/PUT /bff/games` via HttpClient in `src/Admin/QuizArena.Admin.Client/Services/ClientGameConfigurationService.cs`
- [X] T012 [P] [US1] Implement `ServerGameConfigurationService` with `HttpClient http://oroclash-api` + Bearer from HttpContext for Create/Update in `src/Admin/QuizArena.Admin/Services/ServerGameConfigurationService.cs`
- [X] T013 [P] [US1] Create `GameConfigurationForm.razor` component (16 inputs, per-field errors, aria-live, 44px targets, Draft→Configured auto-transition display) in `src/Admin/QuizArena.Admin/Components/GameConfiguration/GameConfigurationForm.razor`
- [X] T014 [P] [US1] Create `ScheduledAtPicker.razor` UTC date/time selector with ≥5m future validation in `src/Admin/QuizArena.Admin/Components/GameConfiguration/ScheduledAtPicker.razor`
- [X] T015 [P] [US1] Create `GameStateBadge.razor` for 8 states with color mapping and tooltip per `GameStateView` in `src/Admin/QuizArena.Admin/Components/GameConfiguration/GameStateBadge.razor`
- [X] T016 [US1] Create `GameCreate.razor` page (form + submit → 201 + rowVersion, error FieldErrors, preserve draft on 401) in `src/Admin/QuizArena.Admin.Client/Pages/GameConfiguration/GameCreate.razor`
- [X] T017 [US1] Create `GameEdit.razor` page (load by id, bind 16 fields, editable while Draft/Configured/Scheduled, block after Ready/Running) in `src/Admin/QuizArena.Admin.Client/Pages/GameConfiguration/GameEdit.razor`
- [X] T018 [US1] Wire DI for `IGameConfigurationService` via `AddAdminApiHttpClient` pattern in `src/Admin/QuizArena.Admin/Program.cs` and `src/Admin/QuizArena.Admin.Client/Program.cs`

**Checkpoint**: US1 fully functional and independently testable — 16-field creation/edition with Draft→Configured and validation per spec scenarios 1–5

---

## Phase 4: User Story 2 - Programar, preparar y controlar el ciclo de vida previo a la ejecución (Priority: P1)

**Goal**: Configured → Scheduled (fecha futura) → Ready → Running (inmutable) ↔ Paused (congela timer) → Finished / Cancelled con comandos dedicados, guardas, concurrencia rowversion y auditoría append-only

**Independent Test**: Tomar juego Configured → asignar ScheduledAt futura → Scheduled; Ready → Running → Paused → Resume → Finished; Draft→Cancelled; ScheduledAt pasada → rechazo; 8 transiciones válidas auditadas, inválidas 422 sin mutación

### Implementation for User Story 2

- [X] T019 [P] [US2] Implement `ClientGameConfigurationService` transition methods `ScheduleAsync`/`ReadyAsync`/`StartAsync`/`PauseAsync`/`ResumeAsync`/`FinishAsync`/`CancelAsync` calling `POST /bff/games/{id}/*` in `src/Admin/QuizArena.Admin.Client/Services/ClientGameConfigurationService.cs`
- [X] T020 [P] [US2] Implement `ServerGameConfigurationService` transitions with `If-Match` RowVersion forwarding to `http://oroclash-api/api/games/{id}/*` in `src/Admin/QuizArena.Admin/Services/ServerGameConfigurationService.cs`
- [X] T021 [P] [US2] Create `GameTransitionsBar.razor` with buttons Schedule/Ready/Start/Pause/Resume/Finish/Cancel enabled by current `GameStateView` and `RowVersion` in `src/Admin/QuizArena.Admin/Components/GameConfiguration/GameTransitionsBar.razor`
- [X] T022 [US2] Create `GamesList.razor` paginated list (`GET /bff/games?status=&category=&search=&page=`) with filters by 8 states and category, skeleton, no full collection load in `src/Admin/QuizArena.Admin.Client/Pages/GameConfiguration/GamesList.razor`
- [X] T023 [US2] Create `GameDetail.razor` showing 16-field read view, immutable highlight after Ready/Running, ScheduledAt display, history `GameStateTransition` and `RowVersion` in `src/Admin/QuizArena.Admin.Client/Pages/GameConfiguration/GameDetail.razor`
- [X] T024 [US2] Handle `RowVersion` optimistic concurrency: send `If-Match`, map `409 ConcurrencyConflict` to field error with reload option in `src/Admin/QuizArena.Admin/Components/GameConfiguration/GameConfigurationForm.razor`
- [X] T025 [US2] Wire authorization `AdminOrGameManager` on configuration pages; REWARD_MANAGER gets Access Denied UI + 403 on API in `src/Admin/QuizArena.Admin.Client/Pages/GameConfiguration/GamesList.razor` and `src/Admin/QuizArena.Admin.Client/Pages/GameConfiguration/GameCreate.razor`

**Checkpoint**: US1 and US2 both independently functional — creation/edition + 8-state lifecycle with audit

---

## Phase 5: User Story 3 - Validación avanzada, premios y reglas de negocio configurables (Priority: P2)

**Goal**: Configurar puntuación, puntos asegurados, reglas retiro/finalización y premios final/consolación con validación de dominio, feedback por campo y solo-lectura tras Running

**Independent Test**: Editar Draft/Configured → seleccionar Adaptive/ProgressiveBonus/KEEP_SECURED_SCORE/FALLBACK_TO_CHECKPOINT + Rewards Active → guardar ok; luego premios inactivos o puntos asegurados incoherentes → 400 con campo señalado; tras Running → solo lectura y 422 on API put

### Implementation for User Story 3

- [X] T026 [P] [US3] Implement scoring/secured-points/withdrawal/loss catalog selects with validation (policies en catálogo cerrado, puntos asegurados ≤ rondas, scores coherentes) in `src/Admin/QuizArena.Admin/Components/GameConfiguration/GameConfigurationForm.razor`
- [X] T027 [P] [US3] Implement Reward selectors for `FinalRewardId`/`ConsolationRewardId` fetching `GET /bff/rewards?status=Active` with `RewardUnavailable` handling and distinct-check in `src/Admin/QuizArena.Admin/Components/GameConfiguration/GameConfigurationForm.razor`
- [X] T028 [US3] Enforce immutability after Ready/Running/Paused: render form read-only and block `UpdateAsync` via disabled submit + API 422 mapping in `src/Admin/QuizArena.Admin.Client/Pages/GameConfiguration/GameEdit.razor`
- [X] T029 [US3] Implement category guard validation (≥5 valid questions, 4 opciones/1 correcta) with `CategoryNotReady` field error before `Configured` transition in `src/Admin/QuizArena.Admin.Client/Services/ClientGameConfigurationService.cs`
- [X] T030 [US3] Add a11y and responsive polish for configuration form (focus visible, aria-live per-field errors, 375–1536 no scroll, 44px targets) in `src/Admin/QuizArena.Admin/Components/GameConfiguration/GameConfigurationForm.razor`

**Checkpoint**: All user stories independently functional — full 16-field configurable engine with lifecycle

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Governance, a11y, concurrency, audit, and validation per quickstart.md

- [X] T031 [P] Run Design System token gate `node design-system/validate-tokens.cjs --dir src/Admin --strict` and fix literal violations in `src/Admin/QuizArena.Admin/Components/GameConfiguration/*`
- [X] T032 [P] Add/extend `GameConfigurationTests` (16 fields validation, immutability after Running, rowversion) in `tests/QuizArena.Admin.Tests/GameConfigurationTests.cs`
- [X] T033 [P] Add/extend `GameStateTransitionTests` (8 states, guards, invalid 422, concurrency 409, auth 403) in `tests/QuizArena.Admin.Tests/GameStateTransitionTests.cs`
- [X] T034 Verify `DesignSystemNoDirectDbTests` / `AdminBffTests` still pass for new configuration services in `tests/OroQuizClash.Architecture.Tests/AdminBffTests.cs`
- [X] T035 Run quickstart.md validation scenarios V1-V4 (Aspire AppHost, ADMIN/GAME_MANAGER vs REWARD_MANAGER, 16 fields, 8 transitions, rewards, concurrency, pagination) per `specs/019-admin-game-configuration/quickstart.md`
- [X] T036 [P] Cross-cutting polish: loading skeletons timing, error `ProblemDetails` without leak, `CorrelationId` logged, responsive 375/768/1024/1440/1536 manual audit in `src/Admin/QuizArena.Admin/Components/GameConfiguration/*`

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

- **US1 (P1)**: After Foundational, no other story dependency — MVP slice (create/configure Draft→Configured)
- **US2 (P1)**: After Foundational, independent of US1 but shares GameEdit/Detail; can run in parallel with US1 by different developers (merge care on GameConfigurationForm)
- **US3 (P2)**: After Foundational + US1 (needs 16-field form for advanced validation)

### Within Each User Story

- Models/catalogs before services, services before components, components before page integration
- Validation before integration, immutability before transitions

### Parallel Opportunities

- T004, T005, T006 can run in parallel (different files)
- T010, T011, T012, T013, T014, T015 can run in parallel within US1 (different files)
- T019, T020, T021 can run in parallel within US2
- T026, T027 can run in parallel within US3
- T031, T032, T033 can run in parallel in Polish

---

## Parallel Example: User Story 1

```bash
# Launch US1 models/services/components together:
Task: "Extend GameConfigurationForm validation in src/Admin/QuizArena.Admin.Client/Models/GameConfiguration/GameConfigurationForm.cs"
Task: "Implement ClientGameConfigurationService.CreateAsync in src/Admin/QuizArena.Admin.Client/Services/ClientGameConfigurationService.cs"
Task: "Create GameConfigurationForm.razor in src/Admin/QuizArena.Admin/Components/GameConfiguration/GameConfigurationForm.razor"
Task: "Create ScheduledAtPicker.razor in src/Admin/QuizArena.Admin/Components/GameConfiguration/ScheduledAtPicker.razor"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004-T009) — shared enums/catalogs/DTOs + IGameConfigurationService + BFF verify
3. Complete Phase 3: US1 (T010-T018)
4. **STOP and VALIDATE**: Login ADMIN → create game with 16 fields → verify Configured + persisted values per quickstart V1
5. Deploy/demo if ready — creation/configuration without lifecycle

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. Add US1 → Test independently → Deploy/Demo (MVP! — SC-001/SC-005)
3. Add US2 → Test independently → Deploy/Demo (+ SC-002/SC-009 8-state lifecycle)
4. Add US3 → Test independently → Deploy/Demo (+ SC-003/SC-004 advanced rules)

### Parallel Team Strategy

With multiple developers after Foundational:

- Developer A: US1 (T010-T018)
- Developer B: US2 (T019-T025) — coordinate on GameConfigurationForm merge
- Developer C: US3 prep (T026-T027) after US1 stabilizes

---

## Notes

- [P] = different files, no dependencies — safe to parallelize
- [US*] = traceability to spec.md user stories
- Each user story independently completable and testable per its Independent Test
- Exact file paths per task — LLM can execute without additional context
- 16 fields: Name, Description, CategoryId, NumberOfRounds, MaxPlayers, TimePerQuestion, InitialDifficulty, DifficultyProgression, Scoring/PointsPerRound, SecuredPoints, WithdrawalPolicy, FinishPolicy, FinalRewardId, ConsolationRewardId, ScheduledAt, Status+RowVersion
- 8 states: Draft→Configured→Scheduled→Ready→Running↔Paused→Finished + Cancelled (mapped to domain DRAFT/READY/WAITING/IN_PROGRESS)
- Constitution gates: Domain First, Configurable Rules (C), Game Lifecycle (A), Concurrency (F), BFF + OIDC + ServiceDefaults
