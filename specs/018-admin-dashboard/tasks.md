# Tasks: Admin Dashboard

**Input**: Design documents from `/specs/018-admin-dashboard/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: No TDD contract-test tasks requested in spec; validation via quickstart.md + Architecture Tests in Polish phase.

**Organization**: Tasks grouped by user story (P1, P1, P2) per strict checklist format `- [ ] Txxx [P]? [USy]? Description with file path`.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify existing Admin shell and prepare dashboard feature scaffolding

- [X] T001 Verify Admin shell baseline from SPEC-017 in `src/Admin/QuizArena.Admin/Program.cs` and `src/Admin/QuizArena.Admin.Client/Program.cs` (BFF YARP, OIDC, ServiceDefaults)
- [X] T002 Create dashboard feature directories `src/Admin/QuizArena.Admin/Components/Dashboard/` and `src/Admin/QuizArena.Admin.Client/Models/Dashboard/` and `src/Admin/QuizArena.Admin.Client/Services/`
- [X] T003 Verify Design System tokens available at `design-system/tokens/design-tokens.css` and theme `administration` in `src/Admin/QuizArena.Admin/Components/App.razor`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared DTOs, service contract, and static catalog — MUST complete before any user story

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 [P] Create dashboard DTOs `MetricId`, `MetricState`, `MetricValue`, `DashboardSnapshot`, `GeneralStatistics`, `StatisticBreakdown` in `src/Admin/QuizArena.Admin.Client/Models/Dashboard/DashboardSnapshot.cs`
- [X] T005 [P] Create `QuickActionId`, `QuickAction`, `AdminRole` catalog types in `src/Admin/QuizArena.Admin.Client/Services/QuickActionsCatalog.cs`
- [X] T006 Create shared service contract `IDashboardService` with `GetSnapshotAsync` and `GetMetricAsync` in `src/Admin/QuizArena.Admin.Client/Services/IDashboardService.cs`
- [X] T007 Register Client/Server dashboard services in DI via `AddAdminApiHttpClient` pattern in `src/Admin/QuizArena.Admin/Program.cs` and `src/Admin/QuizArena.Admin.Client/Program.cs`
- [X] T008 Implement `QuickActionsCatalog.All` static catalog (7 entries: create-game, configure-game, manage-questions, view-active-games, view-players, manage-rewards, view-reports with Lucide icons and AllowedRoles) in `src/Admin/QuizArena.Admin.Client/Services/QuickActionsCatalog.cs`
- [X] T009 Verify BFF forwarder catch-all `MapBffForwarder()` exists and covers `/bff/dashboard/snapshot` in `src/Admin/QuizArena.Admin/Services/BffForwarderExtensions.cs`

**Checkpoint**: Foundation ready — user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Vista operacional resumida del sistema (Priority: P1) 🎯 MVP

**Goal**: Dashboard shows 10 metric blocks (Juegos activos/programados/finalizados, Jugadores conectados/activos, Preguntas/Categorías, Premios/Canjes, Estadísticas generales) with Loading/Ready/Empty/Error states, isolated retry, no full-page reload

**Independent Test**: Login as ADMIN/GAME_MANAGER → open Dashboard → verify 10 cards with numeric values, Empty shows "0", one failing block shows skeleton then Error+Retry without blocking others (FR-007, SC-001/SC-009)

### Implementation for User Story 1

- [X] T010 [P] [US1] Implement `ServerDashboardService.GetSnapshotAsync` with single-shot fan-out `Task.WhenAll` over existing `oroclash-api` endpoints and per-block timeout 5s in `src/Admin/QuizArena.Admin/Services/ServerDashboardService.cs`
- [X] T011 [P] [US1] Implement `ClientDashboardService.GetSnapshotAsync` calling `GET /bff/dashboard/snapshot` via HttpClient in `src/Admin/QuizArena.Admin.Client/Services/ClientDashboardService.cs`
- [X] T012 [P] [US1] Implement `ServerDashboardService.GetMetricAsync` for isolated retry (or full-snapshot fallback) in `src/Admin/QuizArena.Admin/Services/ServerDashboardService.cs`
- [X] T013 [P] [US1] Create `MetricTile.razor` component (label, count, Loading skeleton, Empty, Error with Retry button, aria-live, role=status, aria-busy) in `src/Admin/QuizArena.Admin/Components/Dashboard/MetricTile.razor`
- [X] T014 [P] [US1] Create `MetricsGrid.razor` responsive CSS grid (1/2/3/4 cols, 375-1536 no scroll) rendering 10 MetricTile in `src/Admin/QuizArena.Admin/Components/Dashboard/MetricsGrid.razor`
- [X] T015 [US1] Create `DashboardRefreshBar.razor` with Actualizar button, GeneratedAt timestamp, SessionExpired banner in `src/Admin/QuizArena.Admin/Components/Dashboard/DashboardRefreshBar.razor`
- [X] T016 [US1] Integrate Dashboard page to fetch `IDashboardService.GetSnapshotAsync`, bind `DashboardViewState`, wire Actualizar + per-block retry, handle 401 → SessionExpired in `src/Admin/QuizArena.Admin/Components/Pages/Dashboard.razor`
- [X] T017 [US1] Implement player-presence fallback with `SourceLabel`/`Tooltip` for Connected vs Active in `src/Admin/QuizArena.Admin/Services/ServerDashboardService.cs`
- [X] T018 [US1] Handle must_change_password gating before showing metrics (reuse Routes.razor gating) in `src/Admin/QuizArena.Admin/Components/Pages/Dashboard.razor`

**Checkpoint**: US1 fully functional and independently testable — 10 blocks render correctly per spec acceptance scenarios 1-5

---

## Phase 4: User Story 2 - Accesos rápidos a operaciones críticas (Priority: P1)

**Goal**: 7 quick actions visible in Dashboard, ≤1 click navigation with context filter, icons Lucide 44px, role-based hide/disable with reason, URL deny without leak

**Independent Test**: As ADMIN click each of 7 actions → verify correct destination with filter (/games/new, /games?view=config, /questions, /games?status=Active, /players, /rewards, /reports); as REWARD_MANAGER verify only 2 visible and direct URL to /questions → 403 (FR-011, SC-002/SC-010)

### Implementation for User Story 2

- [X] T019 [P] [US2] Create `QuickActionCard.razor` (Lucide icon, label, description, 44px target, aria-disabled + title reason) in `src/Admin/QuizArena.Admin/Components/Dashboard/QuickActionCard.razor`
- [X] T020 [P] [US2] Create `QuickActionGrid.razor` (2/3/4 cols responsive, focus order after metrics, role-filtered via AuthenticationState) in `src/Admin/QuizArena.Admin/Components/Dashboard/QuickActionGrid.razor`
- [X] T021 [US2] Wire quick-action navigation with NavigationManager to 7 canonical routes (with query filters per contracts/navigation-map.md) in `src/Admin/QuizArena.Admin/Components/Dashboard/QuickActionGrid.razor`
- [X] T022 [US2] Apply role-based filtering (ADMIN 7, GAME_MANAGER 6 without ManageRewards, REWARD_MANAGER 2) via `AuthenticationState` claims `roles` in `src/Admin/QuizArena.Admin/Components/Dashboard/QuickActionGrid.razor`
- [X] T023 [US2] Add destination route authorization policies `[Authorize(Policy=AdminPolicies.*)]` and verify URL deny shows Access Denied without data leak in `src/Admin/QuizArena.Admin.Client/Pages/*.razor` and `src/Admin/QuizArena.Admin/Components/Pages/*.razor`
- [X] T024 [US2] Integrate QuickActionGrid into Dashboard.razor below MetricsGrid with logical DOM order (metrics → quick actions) in `src/Admin/QuizArena.Admin/Components/Pages/Dashboard.razor`

**Checkpoint**: US1 and US2 both independently functional — metrics + navigation coexist without regression

---

## Phase 5: User Story 3 - Drill-down, actualización y contexto operacional (Priority: P2)

**Goal**: Each metric tile drill-down navigates to filtered detail with count coherence, Dashboard stays updated via manual button and auto-refresh 30-60s (visibility pause, 401 stop), mobile 375px usable

**Independent Test**: Click "Juegos activos" N → verify /games?status=Active shows N items (SC-003); create scheduled game → click Actualizar → +1 visible ≤30s; leave tab visible 60s → auto-refresh ≤60s; 401 → polling stops + SessionExpired banner; 375px no horizontal scroll (FR-013/014/008)

### Implementation for User Story 3

- [X] T025 [P] [US3] Make `MetricTile.razor` clickable with `DrillDownRoute` anchor/button + disabled when null (no permission) in `src/Admin/QuizArena.Admin/Components/Dashboard/MetricTile.razor`
- [X] T026 [P] [US3] Map MetricId → drill-down route filter per contracts/navigation-map.md (Active/Scheduled/Finished, online/active, Active, Pending, general) in `src/Admin/QuizArena.Admin/Services/DashboardRouteMap.cs`
- [X] T027 [US3] Verify count coherence `MetricValue.Count == PagedResult.TotalCount` of destination listing (same server-side query) in `src/Admin/QuizArena.Admin/Services/ServerDashboardService.cs`
- [X] T028 [US3] Implement auto-refresh `PeriodicTimer` 45s with `document.visibilityState` JS interop pause/resume and `IAsyncDisposable` cleanup in `src/Admin/QuizArena.Admin/Components/Pages/Dashboard.razor`
- [X] T029 [US3] Implement 401 handling: stop PeriodicTimer, set `SessionExpired=true`, show banner with re-authenticate action without retry loop in `src/Admin/QuizArena.Admin/Components/Pages/Dashboard.razor`
- [X] T030 [US3] Add responsive polish and a11y for drill-down: focus visible, keyboard Enter, aria-live polite on metric changes, role=status in `src/Admin/QuizArena.Admin/Components/Dashboard/MetricsGrid.razor` and `src/Admin/QuizArena.Admin/Components/Dashboard/MetricTile.razor`
- [X] T031 [US3] Ensure drill-down preserves authorization: null route when unauthorized, destination shows Access Denied in `src/Admin/QuizArena.Admin/Components/Dashboard/MetricTile.razor` and destination pages

**Checkpoint**: All user stories independently functional — dashboard is operational center (SC-004, SC-003, SC-006)

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Governance, a11y, performance, and validation per quickstart.md

- [X] T032 [P] Run Design System token gate `node design-system/validate-tokens.cjs --dir src/Admin --strict` and fix literal violations in `src/Admin/QuizArena.Admin/Components/Dashboard/*`
- [X] T033 [P] Add/extend `DashboardAuthorizationTests` (7 shortcuts × 3 roles + 10 drill-down routes 403) in `tests/OroQuizClash.Architecture.Tests/DashboardAuthorizationTests.cs`
- [X] T034 Verify `AdminNoDirectDbTests` / `DesignSystemNoDirectDbTests` still pass for new dashboard services in `tests/OroQuizClash.Architecture.Tests/AdminNoDirectDbTests.cs`
- [X] T035 Run quickstart.md validation scenarios V1-V3 (Aspire AppHost, ADMIN/GAME_MANAGER/REWARD_MANAGER logins, snapshot coherence, quick actions, drill-down, refresh/401/mobile) per `specs/018-admin-dashboard/quickstart.md`
- [X] T036 [P] Cross-cutting polish: loading skeletons timing, error messages actionable (no internal details, CorrelationId logged), responsive 375/768/1024/1440/1536 manual audit in `src/Admin/QuizArena.Admin/Components/Dashboard/*`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational
  - US1 and US2 are both P1 — can proceed in parallel or US1 first for MVP
  - US3 (P2) depends on US1 metrics being stable (drill-down needs counts)
- **Polish (Phase 6)**: Depends on all desired user stories

### User Story Dependencies

- **US1 (P1)**: After Foundational, no other story dependency — MVP slice
- **US2 (P1)**: After Foundational, independent of US1 but shares Dashboard.razor; can run in parallel with US1 by different developers (merge care on Dashboard.razor)
- **US3 (P2)**: After Foundational + US1 (needs MetricTile + counts for drill-down coherence and refresh bar)

### Within Each User Story

- Models/DTOs before services, services before components, components before page integration
- Isolated retry depends on GetSnapshotAsync contract

### Parallel Opportunities

- T004, T005 can run in parallel (different files)
- T010, T011, T012, T013, T014 can run in parallel within US1 (different files)
- T019, T020 can run in parallel within US2
- T025, T026 can run in parallel within US3
- T032, T033 can run in parallel in Polish

---

## Parallel Example: User Story 1

```bash
# Launch US1 models/services/components together:
Task: "Implement ServerDashboardService.GetSnapshotAsync in src/Admin/QuizArena.Admin/Services/ServerDashboardService.cs"
Task: "Implement ClientDashboardService.GetSnapshotAsync in src/Admin/QuizArena.Admin.Client/Services/ClientDashboardService.cs"
Task: "Create MetricTile.razor in src/Admin/QuizArena.Admin/Components/Dashboard/MetricTile.razor"
Task: "Create MetricsGrid.razor in src/Admin/QuizArena.Admin/Components/Dashboard/MetricsGrid.razor"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004-T009) — shared DTOs + IDashboardService + QuickActionsCatalog
3. Complete Phase 3: US1 (T010-T018)
4. **STOP and VALIDATE**: Login as ADMIN → verify 10 blocks with correct states per quickstart V1
5. Deploy/demo if ready — dashboard is informative without quick actions

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. Add US1 → Test independently → Deploy/Demo (MVP! — SC-001/SC-009)
3. Add US2 → Test independently → Deploy/Demo (+ SC-002/SC-010 navigation center)
4. Add US3 → Test independently → Deploy/Demo (+ SC-003/SC-004 live operational)

### Parallel Team Strategy

With multiple developers after Foundational:

- Developer A: US1 (T010-T018)
- Developer B: US2 (T019-T024) — coordinate on Dashboard.razor merge
- Developer C: US3 prep (T025-T026 mapping) after US1 stabilizes

---

## Notes

- [P] = different files, no dependencies — safe to parallelize
- [US*] = traceability to spec.md user stories
- Each user story independently completable and testable per its Independent Test
- Exact file paths per task — LLM can execute without additional context
- 10 metrics: Active/Scheduled/Finished, Connected/Active players, Questions/Categories, Rewards/Redemptions, GeneralStatistics
- 7 quick actions: catalog via QuickActionsCatalog.All, Lucide icons, no emojis
- Constitution gates: no direct DB, BFF via YARP, OIDC only OroIdentityServer, BuildingBlocks.ServiceDefaults
