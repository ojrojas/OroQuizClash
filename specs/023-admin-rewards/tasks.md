# Tasks: Admin Rewards

**Input**: Design documents from `/specs/023-admin-rewards/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: No TDD contract-test tasks requested in spec; validation via quickstart.md + Architecture Tests in Polish phase.

**Organization**: Tasks grouped by user story (P1, P1, P2) per strict checklist format `- [x] Txxx [P]? [USy]? Description with file path`.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify existing Admin shell and prepare rewards feature scaffolding

- [x] T001 Verify Admin shell baseline from SPEC-017 in `src/Admin/QuizArena.Admin/Program.cs` and `src/Admin/QuizArena.Admin.Client/Program.cs` (BFF YARP, OIDC, ServiceDefaults)
- [x] T002 Create rewards feature directories `src/Admin/QuizArena.Admin/Components/Rewards/` and `src/Admin/QuizArena.Admin.Client/Models/Rewards/` and `src/Admin/QuizArena.Admin.Client/Pages/Rewards/`
- [x] T003 Verify Design System tokens available at `design-system/tokens/design-tokens.css` and theme `administration` in `src/Admin/QuizArena.Admin/Components/App.razor`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared DTOs, service contracts, catalogs — MUST complete before any user story

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 [P] Create reward type enum `RewardType` (6 types Monetary/Physical/Digital/Voucher/Experience/Consolation) with mapping via `RewardTypeMap` in `src/Admin/QuizArena.Admin.Client/Models/Rewards/RewardType.cs`
- [x] T005 [P] Create reward DTOs `Reward`, `RewardSummary`, `RewardDetail`, `RewardStateView`, `RewardAuditEntry` in `src/Admin/QuizArena.Admin.Client/Models/Rewards/Reward.cs`
- [x] T006 [P] Create redemption DTOs `RewardRedemption`, `RedemptionStateView`, `RedemptionFilter`, `Stock`/`Availability` logic in `src/Admin/QuizArena.Admin.Client/Models/Rewards/Redemption.cs`
- [x] T007 Create/extend shared service contracts `IRewardsService` (Create/Update/List/Get/Activate/Deactivate/Archive) in `src/Admin/QuizArena.Admin.Client/Services/IRewardsService.cs` and `IRedemptionsService` (Get/Approve/Reject/Deliver/Cancel) in `src/Admin/QuizArena.Admin.Client/Services/IRedemptionsService.cs`
- [x] T008 Create static catalogs `RewardCatalogs` for 6 types, cost 1–100000, stock ≥0, dates From<To in `src/Admin/QuizArena.Admin.Client/Services/RewardCatalogs.cs`
- [x] T009 Verify BFF forwarder catch-all `MapBffForwarder()` exists and covers `/bff/rewards*` and `/bff/redemptions*` in `src/Admin/QuizArena.Admin/Services/BffForwarderExtensions.cs`

**Checkpoint**: Foundation ready — user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Gestionar el catálogo de premios (Priority: P1) 🎯 MVP

**Goal**: REWARD_MANAGER crea/edita premio con 7 campos (nombre, descripción, tipo 6 valores, costo 1–100000, stock ≥0, disponibilidad From<To, estado) → Active/Inactive/Archived con unicidad case-insensitive y validación por campo

**Independent Test**: Login REWARD_MANAGER → /admin/rewards → "Crear premio" Physical costo 500 stock 10 fechas 2026-09-01→2026-12-31 → guardar → verificar 201 Active isEligible true; tipo fuera 6/costo 0/stock -1/fechas From≥To → 400 InvalidRewardData; nombre duplicado → 409 RewardAlreadyExists; Active→Inactive→Active

### Implementation for User Story 1

- [x] T010 [P] [US1] Extend `RewardForm` validation for 7 fields (Name 3–100 unique, Type 6, Cost 1–100000, Stock ≥0, AvailableFrom<AvailableTo, Status) in `src/Admin/QuizArena.Admin.Client/Models/Rewards/RewardForm.cs`
- [x] T011 [P] [US1] Implement `ClientRewardsService.CreateAsync`/`UpdateAsync` calling `POST/PUT /bff/rewards` via HttpClient in `src/Admin/QuizArena.Admin.Client/Services/ClientRewardsService.cs`
- [x] T012 [P] [US1] Implement `ServerRewardsService` with `HttpClient http://oroclash-api` + Bearer from HttpContext for Create/Update in `src/Admin/QuizArena.Admin/Services/ServerRewardsService.cs`
- [x] T013 [P] [US1] Create `RewardForm.razor` component (7 inputs: name, description, type 6, cost, stock, From/To, per-field errors, aria-live, 44px) in `src/Admin/QuizArena.Admin/Components/Rewards/RewardForm.razor`
- [x] T014 [P] [US1] Create `RewardStateBadge.razor` for 3 states Active/Inactive/Archived + IsEligible badge in `src/Admin/QuizArena.Admin/Components/Rewards/RewardStateBadge.razor`
- [x] T015 [P] [US1] Create `RewardAvailabilityBadge.razor` showing stock/fechas and isEligible tooltip in `src/Admin/QuizArena.Admin/Components/Rewards/RewardAvailabilityBadge.razor`
- [x] T016 [US1] Create `RewardCreate.razor` page (form + submit → 201 + rowVersion, FieldErrors, preserve draft on 401) in `src/Admin/QuizArena.Admin.Client/Pages/Rewards/RewardCreate.razor`
- [x] T017 [US1] Create `RewardEdit.razor` page (load by id, bind 7 fields, editable while Active/Inactive, block after Archived) in `src/Admin/QuizArena.Admin.Client/Pages/Rewards/RewardEdit.razor`
- [x] T018 [US1] Wire DI for `IRewardsService` via `AddAdminApiHttpClient` pattern in `src/Admin/QuizArena.Admin/Program.cs` and `src/Admin/QuizArena.Admin.Client/Program.cs`

**Checkpoint**: US1 fully functional and independently testable — 7-field catalog with 6 types and validation per spec scenarios 1–5

---

## Phase 4: User Story 2 - Operar canjes y su ciclo de vida (Priority: P1)

**Goal**: REWARD_MANAGER consulta canjes paginados (filtros estado 5, tipo, player, fechas) y opera `Requested → Approved/Rejected → Delivered/Cancelled` con RowVersion/IdempotencyKey, stock transaccional y auditoría; GAME_MANAGER 403

**Independent Test**: Jugador canjea Physical Active stock 5 costo 100 → aparece Requested → Approve → Approved stock 4 + audit; stock 0 → Approve 409 RewardOutOfStock; Requested → Reject con motivo → Rejected; Approved → Deliver → Delivered; GAME_MANAGER → 403

### Implementation for User Story 2

- [x] T019 [P] [US2] Implement `ClientRedemptionsService` methods `GetRedemptionsAsync`/`ApproveAsync`/`RejectAsync`/`DeliverAsync`/`CancelAsync` calling `GET/POST /bff/redemptions/{id}/*` with If-Match + X-Idempotency-Key in `src/Admin/QuizArena.Admin.Client/Services/ClientRedemptionsService.cs`
- [x] T020 [P] [US2] Implement `ServerRedemptionsService` forwarding to `http://oroclash-api/api/redemptions/{id}/*` with Bearer + IdempotencyKey in `src/Admin/QuizArena.Admin/Services/ServerRedemptionsService.cs`
- [x] T021 [P] [US2] Create `RedemptionRow.razor` component (rewardName, playerName, cost, status, actions Approve/Reject/Deliver/Cancel with confirm dialog, RowVersion) in `src/Admin/QuizArena.Admin/Components/Rewards/RedemptionRow.razor`
- [x] T022 [US2] Create `RedemptionsList.razor` paginated list (`GET /bff/redemptions?status=&type=&playerId=&search=&from=&to=&page=`) with filters by 5 states, type, player, dates, skeleton in `src/Admin/QuizArena.Admin.Client/Pages/Rewards/RedemptionsList.razor`
- [x] T023 [US2] Create `RewardsList.razor` paginated list (`GET /bff/rewards?type=&status=&search=&onlyEligible=&page=`) with filters by 6 types, status, availability, search in `src/Admin/QuizArena.Admin.Client/Pages/Rewards/RewardsList.razor`
- [x] T024 [US2] Handle RowVersion optimistic concurrency: send If-Match, map 409 ConcurrencyConflict to field error with reload option in `src/Admin/QuizArena.Admin/Components/Rewards/RewardForm.razor`
- [x] T025 [US2] Wire authorization `RewardManagerOrAdmin` on rewards/redemptions pages; GAME_MANAGER gets Access Denied UI + 403 on API in `src/Admin/QuizArena.Admin.Client/Pages/Rewards/RewardsList.razor` and `src/Admin/QuizArena.Admin.Client/Pages/Rewards/RedemptionsList.razor`

**Checkpoint**: US1 and US2 both independently functional — catalog + redemptions lifecycle with audit

---

## Phase 5: User Story 3 - Controlar disponibilidad, inventario y tipos con coherencia (Priority: P2)

**Goal**: Definir stock 0=ilimitado vs limitado según tipo, disponibilidad From<To, elegibilidad Active+stock+fechas, costo vs PointTransaction ledger (InsufficientPoints), y Consolation independiente (solo via ConsolationEligibility, no como premio normal)

**Independent Test**: Voucher stock 0 ilimitado → siempre Active; Physical stock 2 → 2 approves → stock 0 → tercer canje 409 RewardOutOfStock; Digital fuera de fechas → Fuera de disponibilidad no elegible; Monetary costo 1000 vs jugador 500 → InsufficientPoints; Consolation solo via regla → InvalidRewardType si se intenta canjear como normal

### Implementation for User Story 3

- [x] T026 [P] [US3] Implement stock logic (0=ilimitado for Digital/Voucher/Consolation vs limited for Physical/Monetary) with tooltip and validation Stock ≥0 in `src/Admin/QuizArena.Admin/Components/Rewards/RewardForm.razor`
- [x] T027 [P] [US3] Implement availability From<To validation and isEligible calculation (Active && (Stock==0?ilimitado:Stock>0) && (now∈[From,To] if defined)) with badge in `src/Admin/QuizArena.Admin/Components/Rewards/RewardAvailabilityBadge.razor`
- [x] T028 [US3] Enforce Consolation independent type: block normal redemption for Consolation type with InvalidRewardType and handle IsConsolation flag via ConsolationEligibility check in `src/Admin/QuizArena.Admin.Client/Services/ClientRedemptionsService.cs`
- [x] T029 [US3] Add a11y and responsive polish for rewards form/list (focus visible, aria-live per-field errors, 375–1536 no scroll, 44px targets) in `src/Admin/QuizArena.Admin/Components/Rewards/RewardForm.razor`

**Checkpoint**: All user stories independently functional — full catalog + redemptions with availability and type coherence

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Governance, a11y, concurrency, audit, and validation per quickstart.md

- [x] T030 [P] Run Design System token gate `node design-system/validate-tokens.cjs --dir src/Admin --strict` and fix literal violations in `src/Admin/QuizArena.Admin/Components/Rewards/*`
- [x] T031 [P] Add/extend `RewardTests` (7 fields validation, 6 types, uniqueness, cost/stock/dates, rowversion) in `tests/QuizArena.Admin.Tests/RewardTests.cs`
- [x] T032 [P] Add/extend `RedemptionTests` (5 states, guards, RewardOutOfStock, InsufficientPoints, InvalidRedemptionState, concurrency 409, auth 403) in `tests/QuizArena.Admin.Tests/RedemptionTests.cs`
- [x] T033 Verify `DesignSystemNoDirectDbTests` / `AdminBffTests` still pass for new rewards services in `tests/OroQuizClash.Architecture.Tests/AdminBffTests.cs`
- [x] T034 Run quickstart.md validation scenarios V1-V4 (Aspire AppHost, ADMIN/REWARD_MANAGER vs GAME_MANAGER, 7 fields + 6 types, redemptions lifecycle, availability) per `specs/023-admin-rewards/quickstart.md`
- [x] T035 [P] Cross-cutting polish: loading skeletons timing, error `ProblemDetails` without leak, `CorrelationId` logged, responsive 375/768/1024/1440/1536 manual audit in `src/Admin/QuizArena.Admin/Components/Rewards/*`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational
  - US1 and US2 are both P1 — can proceed in parallel or US1 first for MVP
  - US3 (P2) depends on US1 form being stable (availability builds on it)
- **Polish (Phase 6)**: Depends on all desired user stories

### User Story Dependencies

- **US1 (P1)**: After Foundational, no other story dependency — MVP slice (catalog 7 fields)
- **US2 (P1)**: After Foundational, independent of US1 but shares RewardDetail/RedemptionRow; can run in parallel with US1 by different developers (merge care on RewardForm)
- **US3 (P2)**: After Foundational + US1 (needs 7-field form for availability)

### Within Each User Story

- Models/catalogs before services, services before components, components before page integration
- Validation before integration, lifecycle before audit

### Parallel Opportunities

- T004, T005, T006 can run in parallel (different files)
- T010, T011, T012, T013, T014, T015 can run in parallel within US1 (different files)
- T019, T020, T021 can run in parallel within US2
- T026, T027 can run in parallel within US3
- T030, T031, T032 can run in parallel in Polish

---

## Parallel Example: User Story 1

```bash
# Launch US1 models/services/components together:
Task: "Extend RewardForm validation in src/Admin/QuizArena.Admin.Client/Models/Rewards/RewardForm.cs"
Task: "Implement ClientRewardsService.CreateAsync in src/Admin/QuizArena.Admin.Client/Services/ClientRewardsService.cs"
Task: "Create RewardForm.razor in src/Admin/QuizArena.Admin/Components/Rewards/RewardForm.razor"
Task: "Create RewardStateBadge.razor in src/Admin/QuizArena.Admin/Components/Rewards/RewardStateBadge.razor"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004-T009) — shared enums/DTOs + IRewards/IRedemptions + BFF verify
3. Complete Phase 3: US1 (T010-T018)
4. **STOP and VALIDATE**: Login REWARD_MANAGER → create reward Physical → verify Active + isEligible per quickstart V1
5. Deploy/demo if ready — catalog without redemptions

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. Add US1 → Test independently → Deploy/Demo (MVP! — SC-001/SC-002)
3. Add US2 → Test independently → Deploy/Demo (+ SC-004/SC-005 redemptions lifecycle)
4. Add US3 → Test independently → Deploy/Demo (+ SC-009 availability and type coherence)

### Parallel Team Strategy

With multiple developers after Foundational:

- Developer A: US1 (T010-T018)
- Developer B: US2 (T019-T025) — coordinate on RewardForm merge
- Developer C: US3 prep (T026-T027) after US1 stabilizes

---

## Notes

- [P] = different files, no dependencies — safe to parallelize
- [US*] = traceability to spec.md user stories
- Each user story independently completable and testable per its Independent Test
- Exact file paths per task — LLM can execute without additional context
- 7 fields: Name, Description, Type 6, Cost 1–100000, Stock ≥0, AvailableFrom/AvailableTo From<To, Status+RowVersion+IsEligible
- 6 types: Monetary, Physical, Digital, Voucher, Experience, Consolation (closed catalog)
- 5 redemption states: Requested→Approved→Delivered and Requested→Rejected, Requested/Approved→Cancelled (rowversion + IdempotencyKey)
- Constitution gates: Domain First, Configurable Rules (C), Scoring via Ledger (D), Concurrency (F), BFF + OIDC + ServiceDefaults
