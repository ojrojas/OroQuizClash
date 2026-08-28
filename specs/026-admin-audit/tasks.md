# Tasks: Admin Audit

**Input**: Design documents from `/specs/026-admin-audit/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: No TDD contract-test tasks requested in spec; validation via quickstart.md + Architecture Tests in Polish phase.

**Organization**: Tasks grouped by user story (P1, P1, P2) per strict checklist format `- [ ] Txxx [P]? [USy]? Description with file path`.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify existing Admin shell and prepare audit feature scaffolding

- [x] T001 Verify Admin shell baseline from SPEC-017 in `src/Admin/QuizArena.Admin/Program.cs` and `src/Admin/QuizArena.Admin.Client/Program.cs` (BFF YARP, OIDC, ServiceDefaults)
- [x] T002 Create audit feature directories `src/Admin/QuizArena.Admin/Components/Audit/` and `src/Admin/QuizArena.Admin.Client/Models/Audit/` and `src/Admin/QuizArena.Admin.Client/Pages/Audit/`
- [x] T003 Verify Design System tokens available at `design-system/tokens/design-tokens.css` and theme `administration` in `src/Admin/QuizArena.Admin/Components/App.razor`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared DTOs, service contracts, catalogs — MUST complete before any user story

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 [P] Create audit entry DTOs `AuditEntry`, `WhoView`, `WhereView`, `EntityView`, `ResultView` with 9 fields in `src/Admin/QuizArena.Admin.Client/Models/Audit/AuditEntry.cs`
- [x] T005 [P] Create audit filter DTO `AuditFilter` with 9 filters (Who/What/When/Where/Entity/Action/Result) + validation `WhenFrom<=WhenTo` in `src/Admin/QuizArena.Admin.Client/Models/Audit/AuditFilter.cs`
- [x] T006 [P] Create audit detail DTOs `AuditDetail`, `JsonDiffEntry`, `AuditViewAudit` + `PagedResult` in `src/Admin/QuizArena.Admin.Client/Models/Audit/AuditDetail.cs`
- [x] T007 Create/extend shared service contracts `IAuditService` (GetAudit/GetAuditDetail) in `src/Admin/QuizArena.Admin.Client/Services/IAuditService.cs`
- [x] T008 Create static catalogs `AuditCatalogs` for 7 entity types, 14 actions, 2 results, error codes in `src/Admin/QuizArena.Admin.Client/Services/AuditCatalogs.cs`
- [x] T009 Verify BFF forwarder catch-all `MapBffForwarder()` exists and covers `/bff/audit*` in `src/Admin/QuizArena.Admin/Services/BffForwarderExtensions.cs`

**Checkpoint**: Foundation ready — user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Consultar auditoría con filtros Who/What/When/Where/Entity/Action/Result (Priority: P1) 🎯 MVP

**Goal**: ADMIN consulta auditoría paginada con 9 campos (Who/What/When/Where/Entity/Previous/New/Action/Result) y 9 filtros combinados AND (Who/What/When/Where/Entity/Action/Result) con paginación Page/PageSize 20, TotalCount, skeleton, Empty/Error, sin cargar colecciones

**Independent Test**: Login ADMIN → /admin/audit → verificar 9 columnas paginadas; filtrar Who admin + When 7d + Entity Game + Action CREATE → solo entradas AND con TotalCount; filtro futuro → Empty; GAME_MANAGER mismo; no-auth → 403

**Acceptance Scenarios**: spec.md US1 scenarios 1–4

### Implementation for User Story 1

- [x] T010 [P] [US1] Extend `AuditFilter` validation for 9 filtros (Who 0–100, What 0–100, WhenFrom<=WhenTo, Where 0–100, EntityType 7, Action 14, Result 2, Page 1..N PageSize 1..100) in `src/Admin/QuizArena.Admin.Client/Models/Audit/AuditFilter.cs`
- [x] T011 [P] [US1] Implement `ClientAuditService.GetAuditAsync` calling `GET /bff/audit?who=&what=&whenFrom=&whenTo=&where=&entityType=&entityId=&action=&result=&page=` via HttpClient in `src/Admin/QuizArena.Admin.Client/Services/ClientAuditService.cs`
- [x] T012 [P] [US1] Implement `ServerAuditService` forwarding to `http://oroclash-api/api/audit*` with Bearer from HttpContext for GetAudit in `src/Admin/QuizArena.Admin/Services/ServerAuditService.cs`
- [x] T013 [P] [US1] Create `AuditTable.razor` component (9 columnas Who/What/When/Where/Entity/Previous/New/Action/Result, paginación, skeleton, Empty/Error, 44px) in `src/Admin/QuizArena.Admin/Components/Audit/AuditTable.razor`
- [x] T014 [P] [US1] Create `AuditFiltersBar.razor` component (9 filtros: Who, What, When Desde/Hasta, Where, EntityType, EntityId, Action, Result + validación From<=To) in `src/Admin/QuizArena.Admin/Components/Audit/AuditFiltersBar.razor`
- [x] T015 [US1] Create `AuditList.razor` paginated list (`GET /bff/audit?who=&whenFrom=&where=&entityType=&action=&result=&page=`) with 9 filtros combinados AND + paginación Page/PageSize in `src/Admin/QuizArena.Admin.Client/Pages/Audit/AuditList.razor`
- [x] T016 [US1] Handle validation errors per field (WhenFrom>WhenTo, Action fuera catálogo, EntityType fuera catálogo) without request and map 400 InvalidFilter to aria-live in `src/Admin/QuizArena.Admin/Components/Audit/AuditFiltersBar.razor`
- [x] T017 [US1] Handle Empty state for rango sin datos and preserve filters on retry in `src/Admin/QuizArena.Admin.Client/Pages/Audit/AuditList.razor`
- [x] T018 [US1] Wire DI for `IAuditService` via `AddAdminApiHttpClient` pattern in `src/Admin/QuizArena.Admin/Program.cs` and `src/Admin/QuizArena.Admin.Client/Program.cs`

**Checkpoint**: US1 fully functional and independently testable — listado 9 campos paginado con 9 filtros AND

---

## Phase 4: User Story 2 - Ver detalle con Previous/New Value y CorrelationId (Priority: P1)

**Goal**: Auditor ve detalle con Who/What/When/Where/Entity/Previous Value/New Value diff + Action/Result + CorrelationId/TraceId clicable, con truncado 10KB y enmascarado de secretos, sin fuga

**Independent Test**: Desde listado → click UPDATE de Category → verificar Previous {Name:Viejo} y New {Name:Nuevo} con diff, Where CorrelationId clicable; CREATE → Previous null; Failed ConcurrencyConflict → Result Failed + ErrorCode; copiar CorrelationId → correlacionar OTel

**Acceptance Scenarios**: spec.md US2 scenarios 1–4

### Implementation for User Story 2

- [x] T019 [P] [US2] Implement `ClientAuditService.GetAuditDetailAsync` calling `GET /bff/audit/{id}` via HttpClient in `src/Admin/QuizArena.Admin.Client/Services/ClientAuditService.cs`
- [x] T020 [P] [US2] Implement `ServerAuditService` forwarding detail to `http://oroclash-api/api/audit/{id}` with Bearer in `src/Admin/QuizArena.Admin/Services/ServerAuditService.cs`
- [x] T021 [P] [US2] Create `AuditDetail.razor` component (9 campos + Previous/New Value JSON diff con verde/rojo + truncado 10KB + enmascarado password/secret + CorrelationId clicable) in `src/Admin/QuizArena.Admin/Components/Audit/AuditDetail.razor`
- [x] T022 [US2] Create `AuditEntryDetail.razor` page (layout /admin/audit/{id} with 9 campos + diff + Where TraceId) in `src/Admin/QuizArena.Admin.Client/Pages/Audit/AuditEntryDetail.razor`
- [x] T023 [US2] Handle CREATE Previous null, DELETE New null, UPDATE both with diff, and Failed Result with ErrorCode/Detail without leak in `src/Admin/QuizArena.Admin/Components/Audit/AuditDetail.razor`
- [x] T024 [US2] Wire authorization with Entity-based filtering; handle 404 AuditEntryNotFound and 403 for unauthorized entity in `src/Admin/QuizArena.Admin.Client/Pages/Audit/AuditEntryDetail.razor`

**Checkpoint**: US1 and US2 both independently functional — listado + detalle diff con trazabilidad

---

## Phase 5: User Story 3 - Integrar con SPEC-014 Audit y filtros avanzados (Priority: P2)

**Goal**: Admin Audit consume trail append-only SPEC-014 AuditEntry + Outbox (inmutable, Previous/New snapshots, CorrelationId) sin duplicar, con auditoría de consultas opcional AuditViewAudit, y filtros avanzados por Entity/Action/Result

**Independent Test**: Operación CreateCategory/ApproveRedemption en 014 → aparece idéntica en /admin/audit con mismo Who/What/When/Where/Entity/Previous/New/Action/Result sin re-escritura; intento PUT/DELETE → 403 sin mutación; REWARD_MANAGER ve solo Reward/Redemption

**Acceptance Scenarios**: spec.md US3 scenarios 1–4

### Implementation for User Story 3

- [x] T025 [P] [US3] Verify SPEC-014 integration: consume oroclash-api /api/audit same AuditEntry (AppDbContextBase.SaveChanges + Outbox) without duplicating, read-only GET only in `src/Admin/QuizArena.Admin.Client/Services/ClientAuditService.cs`
- [x] T026 [P] [US3] Create `AuditViewAudit` logging for sensitive queries (actor, filters, CorrelationId, timestamp) without mutating trail in `src/Admin/QuizArena.Admin/Services/ServerAuditService.cs`
- [x] T027 [US3] Handle EntityType 7 + Action 14 + Result 2 catalogs validation and ensure inmutability (no Update/Delete) in `src/Admin/QuizArena.Admin.Client/Models/Audit/AuditFilter.cs` and `src/Admin/QuizArena.Admin.Client/Services/AuditCatalogs.cs`
- [x] T028 [US3] Wire entity-based authorization per role: ADMIN todo, GAME_MANAGER Game/Category/Question/GamePlayer, REWARD_MANAGER Reward/Redemption in `src/Admin/QuizArena.Admin.Client/Pages/Audit/AuditList.razor` and `src/Admin/QuizArena.Admin.Client/Pages/Audit/AuditEntryDetail.razor`
- [x] T029 [US3] Add a11y and responsive polish for audit list/detail (focus visible, aria-live per-filter errors, 375–1536 no scroll, 44px targets, token-based, diff truncado) in `src/Admin/QuizArena.Admin/Components/Audit/*` and `src/Admin/QuizArena.Admin.Client/Pages/Audit/*`

**Checkpoint**: All user stories independently functional — consulta + detalle + SPEC-014 inmutable + matriz por entidad

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Governance, a11y, paginación, auditoría y validación per quickstart.md

- [x] T030 [P] Run Design System token gate `node .opencode/skills/design-system/scripts/validate-tokens.cjs --dir src/Admin --strict` and fix literal violations in `src/Admin/QuizArena.Admin/Components/Audit/*`
- [x] T031 [P] Add/extend `AuditListTests` (9 campos, filtros combinados AND, WhenFrom<=WhenTo, catálogos, paginación, 403 por rol) in `tests/QuizArena.Admin.Tests/AuditListTests.cs`
- [x] T032 [P] Add/extend `AuditDetailTests` (Previous/New diff, CREATE Previous null, CorrelationId, enmascarado, Failed Result) in `tests/QuizArena.Admin.Tests/AuditDetailTests.cs`
- [x] T033 Verify `DesignSystemNoDirectDbTests` / `AdminBffTests` still pass for new audit services in `tests/OroQuizClash.Architecture.Tests/AdminBffTests.cs`
- [x] T034 Run quickstart.md validation scenarios V1-V4 (Aspire AppHost, ADMIN/GAME_MANAGER vs REWARD_MANAGER, 9 campos, diff, SPEC-014 inmutable, paginación ≥10k) per `specs/026-admin-audit/quickstart.md`
- [x] T035 [P] Cross-cutting polish: loading skeletons per lista/detalle, error `ProblemDetails` without leak, `CorrelationId` logged, responsive 375/768/1024/1440/1536 manual audit in `src/Admin/QuizArena.Admin/Components/Audit/*`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational
  - US1 and US2 are both P1 — can proceed in parallel or US1 first for MVP
  - US3 (P2) depends on US1 list being stable (detalle builds on listado)
- **Polish (Phase 6)**: Depends on all desired user stories

### User Story Dependencies

- **US1 (P1)**: After Foundational, no other story dependency — MVP slice (listado 9 campos)
- **US2 (P1)**: After Foundational, independent of US1 but shares AuditDetail; can run in parallel with US1 by different developers (merge care on AuditDetail)
- **US3 (P2)**: After Foundational + US1 (needs listado for SPEC-014 integration)

### Within Each User Story

- Models/catalogs before services, services before components, components before page integration
- Validation before integration, listado before detalle
- Filtros before paginación

### Parallel Opportunities

- T004, T005, T006 can run in parallel (different files)
- T010, T011, T012, T013, T014 can run in parallel within US1 (different files)
- T019, T020, T021 can run in parallel within US2
- T025, T026, T027 can run in parallel within US3
- T030, T031, T032 can run in parallel in Polish

---

## Parallel Example: User Story 1

```bash
# Launch US1 models/services/components together:
Task: "Extend AuditFilter validation in src/Admin/QuizArena.Admin.Client/Models/Audit/AuditFilter.cs"
Task: "Implement ClientAuditService.GetAuditAsync in src/Admin/QuizArena.Admin.Client/Services/ClientAuditService.cs"
Task: "Create AuditTable.razor in src/Admin/QuizArena.Admin/Components/Audit/AuditTable.razor"
Task: "Create AuditFiltersBar.razor in src/Admin/QuizArena.Admin/Components/Audit/AuditFiltersBar.razor"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004-T009) — shared DTOs + IAuditService + BFF verify
3. Complete Phase 3: US1 (T010-T018)
4. **STOP and VALIDATE**: Login ADMIN → /admin/audit → filtrar Who + When + Entity + Action per quickstart V1
5. Deploy/demo if ready — listado sin detalle

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. Add US1 → Test independently → Deploy/Demo (MVP! — SC-001/SC-002)
3. Add US2 → Test independently → Deploy/Demo (+ SC-003 detalle diff)
4. Add US3 → Test independently → Deploy/Demo (+ SC-004 SPEC-014 inmutable)

### Parallel Team Strategy

With multiple developers after Foundational:

- Developer A: US1 (T010-T018)
- Developer B: US2 (T019-T024) — coordinate on AuditDetail merge
- Developer C: US3 prep (T025-T028) after US1 stabilizes

---

## Notes

- [P] = different files, no dependencies — safe to parallelize
- [US*] = traceability to spec.md user stories
- Each user story independently completable and testable per its Independent Test
- Exact file paths per task — LLM can execute without additional context
- 9 campos: Who (sub/DisplayName/email/tenant) + What (descripción) + When (UTC) + Where (servicio/endpoint/IP/CorrelationId/TraceId) + Entity (tipo 7 + EntityId) + Previous Value (JSON o null) + New Value (JSON) + Action (14 catálogo) + Result (Success/Failed + ErrorCode)
- Filtros combinados AND: 9 filtros (Who/What/When/Where/Entity/Action/Result) + paginación Page 1..N PageSize 1..100, default 20, sin cargar colecciones; Previous/New Value JSON diff con truncado 10KB y enmascarado
- Constitución gates: Domain First (append-only), Clean Architecture, BuildingBlocks, CQRS (GetAuditEntries), Server Truth (Who/When/Previous/New), OroIdentityServer (VI/H), Security (H), Observability (I, CorrelationId), API & Frontend (J), net10.0 único
