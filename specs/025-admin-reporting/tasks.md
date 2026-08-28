# Tasks: Admin Reporting

**Input**: Design documents from `/specs/025-admin-reporting/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: No TDD contract-test tasks requested in spec; validation via quickstart.md + Architecture Tests in Polish phase.

**Organization**: Tasks grouped by user story (P1, P1, P2) per strict checklist format `- [ ] Txxx [P]? [USy]? Description with file path`.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify existing Admin shell and prepare reporting feature scaffolding

- [x] T001 Verify Admin shell baseline from SPEC-017 in `src/Admin/QuizArena.Admin/Program.cs` and `src/Admin/QuizArena.Admin.Client/Program.cs` (BFF YARP, OIDC, ServiceDefaults)
- [x] T002 Create reporting feature directories `src/Admin/QuizArena.Admin/Components/Reports/` and `src/Admin/QuizArena.Admin.Client/Models/Reports/` and `src/Admin/QuizArena.Admin.Client/Pages/Reports/`
- [x] T003 Verify Design System tokens available at `design-system/tokens/design-tokens.css` and theme `administration` in `src/Admin/QuizArena.Admin/Components/App.razor`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared DTOs, service contracts, catalogs — MUST complete before any user story

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 [P] Create report filter DTO `ReportFilter` with 6 dimensions (Fecha Desde/Hasta, Categoría, Juego, Jugador, Nivel 1–5, Resultado) + validation `From<=To` in `src/Admin/QuizArena.Admin.Client/Models/Reports/ReportFilter.cs`
- [x] T005 [P] Create report snapshot DTOs `ReportSnapshot`, `OperationalMetrics`, `PerformanceMetrics`, `RewardsMetrics` + `PagedResult` in `src/Admin/QuizArena.Admin.Client/Models/Reports/ReportSnapshot.cs`
- [x] T006 [P] Create metric DTOs `GameMetric`, `PlayerMetric`, `QuestionMetric`, `AnswerMetric`, `ScoreMetric`, `WithdrawalMetric`, `RewardMetric`, `ConsolationMetric` in `src/Admin/QuizArena.Admin.Client/Models/Reports/ReportMetrics.cs`
- [x] T007 Create/extend shared service contracts `IReportsService` (GetOperational/GetPerformance/GetRewards/GetFull) in `src/Admin/QuizArena.Admin.Client/Services/IReportsService.cs`
- [x] T008 Create static catalogs `ReportCatalogs` for 9 game statuses, 5 levels, 7 result values, 10 transaction types, 6 reward types in `src/Admin/QuizArena.Admin.Client/Services/ReportCatalogs.cs`
- [x] T009 Verify BFF forwarder catch-all `MapBffForwarder()` exists and covers `/bff/reports*` in `src/Admin/QuizArena.Admin/Services/BffForwarderExtensions.cs`

**Checkpoint**: Foundation ready — user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Consultar reportes operativos (Priority: P1) 🎯 MVP

**Goal**: ADMIN consulta operativo (Games por 9 estados, Players únicos/activos, Questions por categoría/nivel, Categories en uso) con filtros Fecha/Categoría/Juego paginado <2s, skeleton, Empty/Error, sin cargar colecciones

**Independent Test**: Login ADMIN → /admin/reports → Operativo → verificar Games byStatus, Players Unique, Questions byCategory con filtros Fecha 30d + Categoría Historia + Juego → recalcula server-side; filtro futuro → Empty; GAME_MANAGER mismo; no-auth → 403

**Acceptance Scenarios**: spec.md US1 scenarios 1–4

### Implementation for User Story 1

- [x] T010 [P] [US1] Extend `ReportFilter` validation for operational: From<=To, Level 1–5, Result in 9 GameStatuses, Page 1..N PageSize 1..100 in `src/Admin/QuizArena.Admin.Client/Models/Reports/ReportFilter.cs`
- [x] T011 [P] [US1] Implement `ClientReportsService.GetOperationalAsync` calling `GET /bff/reports/operational?from=&to=&category=&game=&player=&level=&result=&page=` via HttpClient in `src/Admin/QuizArena.Admin.Client/Services/ClientReportsService.cs`
- [x] T012 [P] [US1] Implement `ServerReportsService` forwarding to `http://oroclash-api/api/reports/operational*` with Bearer from HttpContext in `src/Admin/QuizArena.Admin/Services/ServerReportsService.cs`
- [x] T013 [P] [US1] Create `ReportsDashboard.razor` component (3 tabs Operativo/Rendimiento/Recompensas, skeleton per tab) in `src/Admin/QuizArena.Admin/Components/Reports/ReportsDashboard.razor`
- [x] T014 [P] [US1] Create `MetricCard.razor` component (total + desglose ByStatus/ByCategory, CalculatedAt) in `src/Admin/QuizArena.Admin/Components/Reports/MetricCard.razor`
- [x] T015 [P] [US1] Create `ReportFiltersBar.razor` component (6 filtros: Fecha Desde/Hasta, Categoría, Juego, Jugador, Nivel, Resultado + validación From<=To + 44px) in `src/Admin/QuizArena.Admin/Components/Reports/ReportFiltersBar.razor`
- [x] T016 [US1] Create `Reports.razor` page (layout /admin/reports with Operativo tab + filtros combinados AND + paginación Page/PageSize) in `src/Admin/QuizArena.Admin.Client/Pages/Reports/Reports.razor`
- [x] T017 [US1] Handle validation errors per field (From>To, Level 0/6, Result fuera catálogo) without request and map 400 InvalidFilter to aria-live in `src/Admin/QuizArena.Admin/Components/Reports/ReportFiltersBar.razor`
- [x] T018 [US1] Wire DI for `IReportsService` via `AddAdminApiHttpClient` pattern in `src/Admin/QuizArena.Admin/Program.cs` and `src/Admin/QuizArena.Admin.Client/Program.cs`

**Checkpoint**: US1 fully functional and independently testable — operativo con 6 filtros combinados y paginación

---

## Phase 4: User Story 2 - Analizar rendimiento de juego (Priority: P1)

**Goal**: ADMIN/GAME_MANAGER analiza Answers (totales/Correct/Incorrect + tasa), Scores (total/promedio/distribución ledger 10 tipos), Withdrawals (conteo/tasa/política) con filtros Fecha/Nivel/Resultado/Categoría/Juego/Jugador paginados <2s, sin cálculo en cliente

**Independent Test**: /admin/reports → Rendimiento → filtrar Nivel 3 + Correct → Answers tasa + Scores promedio; filtrar Jugador `ana` + fechas → solo ese jugador; Withdrawn → lista paginada; Nivel 99 → validación sin petición

**Acceptance Scenarios**: spec.md US2 scenarios 1–4

### Implementation for User Story 2

- [x] T019 [P] [US2] Implement `ClientReportsService.GetPerformanceAsync` calling `GET /bff/reports/performance?from=&to=&level=&result=&player=&category=&game=&page=` with pagination in `src/Admin/QuizArena.Admin.Client/Services/ClientReportsService.cs`
- [x] T020 [P] [US2] Implement `ServerReportsService` forwarding performance to `http://oroclash-api/api/reports/performance*` with Bearer in `src/Admin/QuizArena.Admin/Services/ServerReportsService.cs`
- [x] T021 [P] [US2] Create `PerformancePanel.razor` component (AnswerMetric Correct/Incorrect + AccuracyRate, ScoreMetric Total/Average/ByTransactionType ledger, WithdrawalMetric ByPolicy) in `src/Admin/QuizArena.Admin/Components/Reports/PerformancePanel.razor`
- [x] T022 [US2] Extend `Reports.razor` Rendimiento tab integration with ReportFiltersBar + MetricCard for Answers/Scores/Withdrawals, loading skeletons per métrica and ProblemDetails without leak in `src/Admin/QuizArena.Admin.Client/Pages/Reports/Reports.razor`
- [x] T023 [US2] Handle Level 1–5 and Result catalog validation server-side and client-side (errors.level, errors.result) in `src/Admin/QuizArena.Admin.Client/Models/Reports/ReportFilter.cs`
- [x] T024 [US2] Wire authorization `AdminOrGameManager` on performance metrics; REWARD_MANAGER gets 403 on those metrics + API in `src/Admin/QuizArena.Admin.Client/Pages/Reports/Reports.razor` and `src/Admin/QuizArena.Admin/Components/Reports/ReportsDashboard.razor`

**Checkpoint**: US1 and US2 both independently functional — operativo + rendimiento con filtros Nivel/Resultado y ledger

---

## Phase 5: User Story 3 - Analizar economía de recompensas (Priority: P2)

**Goal**: ADMIN/REWARD_MANAGER analiza Rewards (6 tipos, 3 estados), Redemptions (5 estados, coste), Consolation Rewards (IsConsolation:true separado, no contado como normal) con filtros Fecha/Categoría/Juego/Jugador/Nivel/Resultado paginados <2s, coherente con ledger REWARD_REDEMPTION/CONSOLATION

**Independent Test**: /admin/reports → Recompensas → totales byType/byStatus + coste; filtrar Jugador → solo sus canjes con IsConsolation badge; Nivel 2 + Approved → filtra por juego con ese nivel; REWARD_MANAGER acceso; GAME_MANAGER 403

**Acceptance Scenarios**: spec.md US3 scenarios 1–4

### Implementation for User Story 3

- [x] T025 [P] [US3] Implement `ClientReportsService.GetRewardsAsync`/`GetFullAsync` calling `GET /bff/reports/rewards?from=&to=&category=&game=&player=&level=&result=&page=` and `GET /bff/reports/full` with pagination in `src/Admin/QuizArena.Admin.Client/Services/ClientReportsService.cs`
- [x] T026 [P] [US3] Implement `ServerReportsService` for rewards/full forwarding with Bearer in `src/Admin/QuizArena.Admin/Services/ServerReportsService.cs`
- [x] T027 [P] [US3] Create `RewardsPanel.razor` component (RewardMetric byType/byStatus + RedemptionMetric byStatus/byType + TotalCost + ConsolationMetric TotalConsolations/TotalCostConsolation separado) in `src/Admin/QuizArena.Admin/Components/Reports/RewardsPanel.razor`
- [x] T028 [US3] Enforce Consolation independent display: IsConsolation badge and not counting as normal reward, TotalCost vs TotalCostConsolation separado in `src/Admin/QuizArena.Admin/Components/Reports/RewardsPanel.razor`
- [x] T029 [US3] Add a11y and responsive polish for reports dashboard (focus visible, aria-live per-filter errors, 375–1536 no scroll, 44px targets, token-based) in `src/Admin/QuizArena.Admin/Components/Reports/*` and `src/Admin/QuizArena.Admin.Client/Pages/Reports/*`

**Checkpoint**: All user stories independently functional — operativo + rendimiento + recompensas con 12 métricas y 6 filtros

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Governance, a11y, paginación, auditoría y validación per quickstart.md

- [x] T030 [P] Run Design System token gate `node .opencode/skills/design-system/scripts/validate-tokens.cjs --dir src/Admin --strict` and fix literal violations in `src/Admin/QuizArena.Admin/Components/Reports/*`
- [x] T031 [P] Add/extend `ReportsOperationalTests` (Games/Players/Questions/Categories + filtros 6 dimensiones, From<=To, Level 1–5, Result catálogo, IsConsolation) in `tests/QuizArena.Admin.Tests/ReportsOperationalTests.cs`
- [x] T032 [P] Add/extend `ReportsRewardsTests` (Rewards/Redemptions/Consolation, 403 por rol, Nivel validación, paginación) in `tests/QuizArena.Admin.Tests/ReportsRewardsTests.cs`
- [x] T033 Verify `DesignSystemNoDirectDbTests` / `AdminBffTests` still pass for new reports services in `tests/OroQuizClash.Architecture.Tests/AdminBffTests.cs`
- [x] T034 Run quickstart.md validation scenarios V1-V4 (Aspire AppHost, ADMIN/GAME_MANAGER vs REWARD_MANAGER, 12 métricas, 6 filtros combinados, paginación masiva) per `specs/025-admin-reporting/quickstart.md`
- [x] T035 [P] Cross-cutting polish: loading skeletons per pestaña, error `ProblemDetails` without leak, `CorrelationId` logged, responsive 375/768/1024/1440/1536 manual audit in `src/Admin/QuizArena.Admin/Components/Reports/*`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational
  - US1 and US2 are both P1 — can proceed in parallel or US1 first for MVP
  - US3 (P2) depends on US1 filters being stable (rewards builds on operational)
- **Polish (Phase 6)**: Depends on all desired user stories

### User Story Dependencies

- **US1 (P1)**: After Foundational, no other story dependency — MVP slice (operativo)
- **US2 (P1)**: After Foundational, independent of US1 but shares ReportFiltersBar/ReportsDashboard; can run in parallel with US1 by different developers (merge care on Reports.razor)
- **US3 (P2)**: After Foundational + US1 (needs filtros for recompensas)

### Within Each User Story

- Models/catalogs before services, services before components, components before page integration
- Validation before integration, ledger before economía
- Operativo before rendimiento

### Parallel Opportunities

- T004, T005, T006 can run in parallel (different files)
- T010, T011, T012, T013, T014, T015 can run in parallel within US1 (different files)
- T019, T020, T021 can run in parallel within US2
- T025, T026, T027 can run in parallel within US3
- T030, T031, T032 can run in parallel in Polish

---

## Parallel Example: User Story 1

```bash
# Launch US1 models/services/components together:
Task: "Extend ReportFilter validation in src/Admin/QuizArena.Admin.Client/Models/Reports/ReportFilter.cs"
Task: "Implement ClientReportsService.GetOperationalAsync in src/Admin/QuizArena.Admin.Client/Services/ClientReportsService.cs"
Task: "Create MetricCard.razor in src/Admin/QuizArena.Admin/Components/Reports/MetricCard.razor"
Task: "Create ReportFiltersBar.razor in src/Admin/QuizArena.Admin/Components/Reports/ReportFiltersBar.razor"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004-T009) — shared DTOs + IReportsService + BFF verify
3. Complete Phase 3: US1 (T010-T018)
4. **STOP and VALIDATE**: Login ADMIN → /admin/reports → Operativo con filtros Fecha/Categoría/Juego per quickstart V1
5. Deploy/demo if ready — operativo sin rendimiento/recompensas

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. Add US1 → Test independently → Deploy/Demo (MVP! — SC-001/SC-002)
3. Add US2 → Test independently → Deploy/Demo (+ SC-003 rendimiento con Nivel/Resultado)
4. Add US3 → Test independently → Deploy/Demo (+ SC-004 recompensas con IsConsolation)

### Parallel Team Strategy

With multiple developers after Foundational:

- Developer A: US1 (T010-T018)
- Developer B: US2 (T019-T024) — coordinate on Reports.razor merge
- Developer C: US3 prep (T025-T028) after US1 stabilizes

---

## Notes

- [P] = different files, no dependencies — safe to parallelize
- [US*] = traceability to spec.md user stories
- Each user story independently completable and testable per its Independent Test
- Exact file paths per task — LLM can execute without additional context
- 12 métricas: Games (9 estados), Players (únicos/activos), Questions (por categoría/nivel), Categories (en uso), Answers (totales/Correct/Incorrect + tasa), Scores (total/promedio/distribución ledger 10 tipos), Withdrawals (conteo/tasa/política), Rewards (6 tipos, 3 estados), Redemptions (5 estados, coste), Consolation (IsConsolation:true separado)
- 6 filtros combinados AND: Fecha (Desde<=Hasta), Categoría, Juego, Jugador, Nivel (1–5), Resultado (catálogo cerrado), paginados Page 1..N PageSize 1..100, default 20, sin cargar colecciones
- Constitución gates: Domain First, Clean Architecture, BuildingBlocks, CQRS (GetReports), Server Truth (ledger), OroIdentityServer (VI/H), Scoring via Ledger (D), Security (H), Observability (I), API & Frontend (J), net10.0 único
