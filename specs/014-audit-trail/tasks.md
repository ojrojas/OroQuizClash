# Tasks: Audit Trail

**Input**: Design documents from `/specs/014-audit-trail/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included — required by constitution (Domain/Application/Api/Architecture) and quickstart.md scenarios

**Organization**: Tasks grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify baseline audit infrastructure from SPEC-013 before extending to 16 actions

- [X] T001 Verify existing audit infra in `src/OroQuizClash.Domain/Audit/AuditEntry.cs`, `src/OroQuizClash.Application/Behaviors/AuditBehavior.cs`, `src/OroQuizClash.Application/Features/Audit/GetAuditEntries.cs`, `src/OroQuizClash.Infrastructure/Persistence/Configurations/AuditEntryTypeConfiguration.cs` and `src/OroQuizClash.Api/Authorization/SecurityPolicies.cs` (`Audit.Read`)
- [X] T002 [P] Review existing `OroQuizClashDbContext` in `src/OroQuizClash.Infrastructure/Persistence/OroQuizClashDbContext.cs` — `DbSet<AuditEntry>` and `DbSet<IdempotencyRecord>` with `EnsureCreatedAsync`
- [X] T003 [P] Review `BuildingBlocks.ServiceDefaults` correlation ID propagation (`X-Correlation-ID` → `Activity.Current.Id` → `IHttpContextAccessor`) used by `AuditBehavior`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core audit building blocks that MUST complete before ANY user story — central risk if skipped, all stories depend on these

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 Create `AuditAction` enumeration in `src/OroQuizClash.Domain/Audit/AuditAction.cs` with 16 values (GameCreated…AdministrativeAdjustment) as `Enumeration<AuditAction>` with Id/Name
- [X] T005 [P] Extend `AuditEntry` in `src/OroQuizClash.Domain/Audit/AuditEntry.cs` with `ResourceId` (string?), `GameId` (Guid?), `PlayerId` (Guid?), `Data` (string? alias for Details) — keep existing Id/Timestamp/ActorId/ActorRoles/Action/Permission/Resource/CorrelationId/TenantId/Result/Reason/Details
- [X] T006 Extend `AuditEntryTypeConfiguration` in `src/OroQuizClash.Infrastructure/Persistence/Configurations/AuditEntryTypeConfiguration.cs` — ToTable("AuditEntries"), indexes on `GameId`, `PlayerId`, `Action`, `CorrelationId`, `Timestamp` (plus existing Resource/ActorId), no Update/Delete
- [X] T007 Update `OroQuizClashDbContext` in `src/OroQuizClash.Infrastructure/Persistence/OroQuizClashDbContext.cs` if needed for new columns (no new DbSets — `AuditEntries` already exists)
- [X] T008 Extend `AuditBehavior` pipeline in `src/OroQuizClash.Application/Behaviors/AuditBehavior.cs` to map 16 `Action` via `AuditAction` (switch on `TRequest` type → `AuditAction`), extract `GameId`/`PlayerId`/`ResourceId`/`Data` via reflection, reuse `CorrelationId` propagation, keep best-effort (log Warning on failure, never revert business result)
- [X] T009 Create/extend `AuditEntrySpecification` in `src/OroQuizClash.Infrastructure/Specifications/AuditEntrySpecifications.cs` with filters `GameId`/`PlayerId`/`Action`/`Resource`/`ResourceId`/`CorrelationId`/`from`/`to` plus `ApplyOrderBy(e=>e.Timestamp)` asc and `ApplyPaging`
- [X] T010 [P] Verify `SecurityPolicies` in `src/OroQuizClash.Api/Authorization/SecurityPolicies.cs` already defines `Audit.Read` policy (ADMIN only) — no change, smoke-test `RequireAuthorization("Audit.Read")` on `GET /api/audit`

**Checkpoint**: Foundation ready — `AuditAction` catalog, extended `AuditEntry`, `AuditBehavior` maps 16 actions, searchable spec with indexes; user stories can now begin in parallel

---

## Phase 3: User Story 1 — Auditoría del ciclo de vida del juego (Priority: P1) 🎯 MVP

**Goal**: Registrar 6 eventos `GameCreated`, `GameConfigured`, `GameStarted`, `PlayerJoined`, `RoundStarted`, `QuestionPresented` append-only con 11 campos, inmutable, sin condicionar negocio

**Independent Test**: Crear/configurar/iniciar juego, unir jugador, iniciar ronda, presentar pregunta → `GET /api/audit?gameId=...` contiene 6 registros ordenados; `PUT`/`DELETE` sobre audit → 405

### Tests for User Story 1

- [X] T011 [P] [US1] Domain catalog test in `tests/OroQuizClash.Domain.Tests/Audit/AuditActionCatalogTests.cs` — asserts `AuditAction.All` has 16, contains `GameCreated`…`AdministrativeAdjustment`, `FromName` works
- [X] T012 [P] [US1] Domain immutability test in `tests/OroQuizClash.Domain.Tests/Audit/AuditEntryImmutabilityTests.cs` — asserts `AuditEntry` has no public Update/Delete methods, `Create` sets `Timestamp` server UTC
- [X] T013 [P] [US1] Application mapping test in `tests/OroQuizClash.Application.Tests/Behaviors/AuditBehaviorLifecycleTests.cs` — `CreateGameCommand` → `GameCreated`, `JoinGameCommand` → `PlayerJoined`, `StartRoundCommand` → `RoundStarted`+`QuestionPresented`, each writes one `AuditEntry` with `GameId`/`CorrelationId`

### Implementation for User Story 1

- [X] T014 [P] [US1] Implement `AuditAction` mapping for lifecycle commands in `src/OroQuizClash.Application/Behaviors/AuditBehavior.cs` — dictionary `Type → AuditAction` for `CreateGame`, `UpdateGame`/`ConfigureGame`, `StartGame`, `JoinGame`, `StartRound` (and `QuestionPresented` same tx)
- [X] T015 [US1] Ensure `AuditEntry.Create` for lifecycle events captures `GameId`/`ResourceId`/`PlayerId`/`Data` sanitized in `src/OroQuizClash.Application/Behaviors/AuditBehavior.cs` — e.g., `GameCreated` Data = `{name, categoryId}`, `PlayerJoined` PlayerId = joined player
- [X] T016 [US1] Verify `GetAuditEntries` query in `src/OroQuizClash.Application/Features/Audit/GetAuditEntries.cs` already supports `gameId` filter and paginación — add `GameId`/`Action` filter handling if needed for US1 events

**Checkpoint**: At this point, User Story 1 is independently functional — 6/16 lifecycle events audited end-to-end, testable via `dotnet test --filter AuditActionCatalog`

---

## Phase 4: User Story 2 — Auditoría de jugadas y puntuación (Priority: P1)

**Goal**: Registrar 4 eventos `AnswerSubmitted`, `AnswerEvaluated`, `PointsAwarded`, `PointsRemoved` con `CorrelationId` compartido de la jugada, sin que audit condicione evaluación/ledger

**Independent Test**: En ronda activa, SubmitAnswer → `AnswerSubmitted` (Actor=PlayerId), evaluación → `AnswerEvaluated` (Data corrección), `PointsAwarded`/`PointsRemoved` (Data delta) con mismo `CorrelationId`; borrar audit no altera `Game.SubmitAnswer` result

### Tests for User Story 2

- [X] T017 [P] [US2] Application test in `tests/OroQuizClash.Application.Tests/Behaviors/AuditBehaviorScoringTests.cs` — `SubmitAnswerCommand` → `AnswerSubmitted` + `AnswerEvaluated` + `PointsAwarded`/`PointsRemoved` with same `CorrelationId`, `PlayerId`/`GameId` correct
- [X] T018 [P] [US2] Domain no-dependency test in `tests/OroQuizClash.Architecture.Tests/AuditNoDomainDependencyTests.cs` — asserts no handler in `Domain/Games` references `IRepository<AuditEntry>` or `AuditBehavior`
- [X] T019 [P] [US2] Api contract test in `tests/OroQuizClash.Api.Tests/Audit/AuditScoringContractTests.cs` — `POST /api/games/{id}/answers` then `GET /api/audit?gameId=...&playerId=...&action=AnswerSubmitted` returns record with `Result=Denied` when validation fails, without exposing `IsCorrect`

### Implementation for User Story 2

- [X] T020 [P] [US2] Extend `AuditBehavior` for scoring commands in `src/OroQuizClash.Application/Behaviors/AuditBehavior.cs` — map `SubmitAnswerCommand` → 3 sequential `AuditEntry` (`AnswerSubmitted`, `AnswerEvaluated`, `PointsAwarded`/`PointsRemoved`) with shared `CorrelationId` extracted from `X-Correlation-ID`
- [X] T021 [US2] Ensure `Data` sanitization for scoring in `src/OroQuizClash.Application/Behaviors/AuditBehavior.cs` — `AnswerSubmitted` Data contains `answerOptionId` but never `IsCorrect`; `PointsAwarded` Data contains delta/balance, truncated to 1000 chars
- [X] T022 [US2] Verify best-effort: `AuditBehavior` try/catch around `dbContext.AuditEntries.Add` + `SaveChangesAsync` logs Warning and never reverts `Game.SubmitAnswer` success (already try/catch in base behavior)

**Checkpoint**: At this point, User Stories 1 AND 2 are functional — 10/16 events audited, scoring traceable, business logic decoupled from audit

---

## Phase 5: User Story 3 — Auditoría de salidas y cierre (Priority: P2)

**Goal**: Registrar 6 eventos `PlayerWithdrawn`, `PlayerEliminated`, `GameFinished`, `RewardRedeemed`, `ConsolationGranted`, `AdministrativeAdjustment` con `Actor` correcto y sin que lectura genere nuevo ajuste

**Independent Test**: Withdraw, Eliminate, Finish, Redeem, Consolation, Adjustment → `GET /api/audit?gameId=...&action=...` cada uno existe con `Actor` (player/system/ADMIN) y `GameId` correlacionado

### Tests for User Story 3

- [X] T023 [P] [US3] Application test in `tests/OroQuizClash.Application.Tests/Behaviors/AuditBehaviorTerminalTests.cs` — `WithdrawPlayerCommand` → `PlayerWithdrawn`, `FinishGameCommand` → `GameFinished`, `RedeemRewardCommand` → `RewardRedeemed`, `AdministrativeAdjustment` → `AdministrativeAdjustment` with `Actor=ADMIN`
- [X] T024 [P] [US3] Api contract test in `tests/OroQuizClash.Api.Tests/Audit/AuditTerminalContractTests.cs` — `POST /api/games/{id}/withdraw` then `GET /api/audit?action=PlayerWithdrawn&gameId=...` asserts `Actor=playerId` and `Result=Succeeded`

### Implementation for User Story 3

- [X] T025 [P] [US3] Extend `AuditBehavior` for terminal commands in `src/OroQuizClash.Application/Behaviors/AuditBehavior.cs` — map `WithdrawPlayerCommand`, `EliminatePlayer` (via domain rule), `FinishGameCommand`, `RedeemRewardCommand`, `GrantConsolationCommand`, `AdjustPointsCommand` to their `AuditAction`
- [X] T026 [US3] Ensure `AdministrativeAdjustment` `Data` includes delta/justificación in `src/OroQuizClash.Application/Behaviors/AuditBehavior.cs` — and that no `GetAuditEntries` query creates an `AuditRecord` (read-only)

**Checkpoint**: All terminal rewards/adjustments audited — 16/16 events now covered

---

## Phase 6: User Story 4 — Búsqueda y trazabilidad transversal (Priority: P2)

**Goal**: Búsqueda paginada por `GameId`/`PlayerId`/`Action`/`Resource`/`ResourceId`/`CorrelationId`/`Timestamp` (`from`/`to`), orden cronológico, sin side-effect de lectura, protegida por `Audit.Read`

**Independent Test**: Generar 1000 registros (20 juegos × 50 eventos) con `CorrelationId` compartido por flujo, luego buscar por cada filtro y paginación; `GET` no incrementa contador; sin `Audit.Read` → 403

### Tests for User Story 4

- [X] T027 [P] [US4] Application search test in `tests/OroQuizClash.Application.Tests/Features/Audit/GetAuditEntriesSearchTests.cs` — asserts `AuditEntrySpecification` filters by `GameId`, `PlayerId`, `Action`, `CorrelationId` and orders by `Timestamp`
- [X] T028 [P] [US4] Api integration test in `tests/OroQuizClash.Api.Tests/Audit/AuditSearchContractTests.cs` — `GET /api/audit?gameId=...`, `?playerId=...`, `?action=AnswerEvaluated`, `?correlationId=...`, `?page=1&pageSize=10` returns paginated `total` without duplicates; `GET` does not increase count
- [X] T029 [P] [US4] Api auth test in `tests/OroQuizClash.Api.Tests/Audit/AuditAuthTests.cs` — `GET /api/audit` without `Audit.Read` → 403, with `Audit.Read` → 200
- [X] T030 [P] [US4] Architecture test in `tests/OroQuizClash.Architecture.Tests/AuditSearchabilityTests.cs` — asserts `OroQuizClashDbContext` has index on `GameId`/`CorrelationId` via `AuditEntryTypeConfiguration`

### Implementation for User Story 4

- [X] T031 [P] [US4] Extend `GetAuditEntries` handler in `src/OroQuizClash.Application/Features/Audit/GetAuditEntries.cs` to handle new filters `GameId`/`PlayerId`/`ResourceId`/`Action`/`CorrelationId` (add params to `GetAuditEntriesQuery` if missing) and ensure `pageSize` max 100
- [X] T032 [US4] Harden `AuditEntrySpecification` in `src/OroQuizClash.Infrastructure/Specifications/AuditEntrySpecifications.cs` — add `Where(e => e.GameId == gameId)` etc. for new fields, keep `ApplyOrderBy(e=>e.Timestamp)` asc and `ApplyPaging`
- [X] T033 [US4] Verify `GetAuditEntriesEndpoint` in `src/OroQuizClash.Application/Features/Audit/GetAuditEntries.cs` exposes new query params and `RequireAuthorization("Audit.Read")` + `RequireRateLimiting("ReadLimiter")`, and does NOT call `AuditBehavior` (read-only, SC-005)

**Checkpoint**: All searchable/traceable requirements functional — 16 events queryable, paginated, correlated

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Hardening transversal y validación end-to-end

- [X] T034 [P] Verify append-only enforcement in `tests/OroQuizClash.Architecture.Tests/AuditImmutabilityTests.cs` — asserts `AuditEntry` has no public Update/Delete and `PUT`/`DELETE` on `/api/audit/{id}` → 405 (already from SPEC-013, re-run)
- [X] T035 [P] Add sanitization assertion in `tests/OroQuizClash.Application.Tests/Behaviors/AuditBehaviorSanitizationTests.cs` — asserts `Data` never contains `IsCorrect` pre-divulgación, tokens or secrets for all 16 actions
- [X] T036 [P] Performance smoke test in `tests/OroQuizClash.Api.Tests/Audit/AuditPerformanceTests.cs` — seed 1000 `AuditEntry` (20×50), `GET /api/audit?gameId=...` <500 ms, `AuditBehavior` overhead <50 ms (or unit test timing)
- [X] T037 Run `dotnet build OroQuizClash.slnx` and `dotnet test OroQuizClash.slnx` — fix any regressions from extending `AuditEntry` with 4 new columns
- [X] T038 Run `specs/014-audit-trail/quickstart.md` E2E validation — execute lifecycle + scoring + terminal flows, verify 16/16 via `GET /api/audit`, plus search/pagination/traceability scenarios
- [X] T039 [P] Update `specs/014-audit-trail/contracts/audit-api.md` and `audit-events.md` if implemented `Data` shapes or query params diverged from plan

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational
  - US1 (P1) can start after Foundational — No dependencies on other stories
  - US2 (P1) can start after Foundational — No dependencies on US1, but benefits from lifecycle audit pattern
  - US3 (P2) can start after Foundational — No dependencies on US1/US2 for terminal events
  - US4 (P2) can start after Foundational — No dependencies, but benefits from US1-US3 generating data to search
- **Polish (Phase 7)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational — No dependencies
- **User Story 2 (P1)**: Can start after Foundational — No dependencies on US1, but shares `AuditBehavior` mapping table
- **User Story 3 (P2)**: Can start after Foundational — No dependencies on US1/US2, covers remaining 6 actions
- **User Story 4 (P2)**: Can start after Foundational — No dependencies, but search verification needs US1-US3 data

### Within Each User Story

- Tests (if included) MUST be written and FAIL before implementation
- `AuditAction` enumeration before `AuditBehavior` mapping
- `AuditEntry` extension before `AuditEntryTypeConfiguration` indexes
- Behavior mapping before query/endpoint
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel (T002, T003)
- All Foundational tasks marked [P] can run in parallel (T004 vs T005) except T006 depends on T005, T008 depends on T004-T007
- Once Foundational completes, all user stories can start in parallel (if staffed)
- All tests for a user story marked [P] can run in parallel (T011 vs T012 vs T013)
- Different user stories can be worked on in parallel by different team members
- Polish tasks T034, T035, T036, T039 can run in parallel before T037/T038

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together:
Task: "Domain catalog test in tests/OroQuizClash.Domain.Tests/Audit/AuditActionCatalogTests.cs"
Task: "Domain immutability test in tests/OroQuizClash.Domain.Tests/Audit/AuditEntryImmutabilityTests.cs"
Task: "Application mapping test in tests/OroQuizClash.Application.Tests/Behaviors/AuditBehaviorLifecycleTests.cs"

# Launch all models for User Story 1 together:
Task: "Create AuditAction enumeration in src/OroQuizClash.Domain/Audit/AuditAction.cs"
Task: "Extend AuditEntry with ResourceId/GameId/PlayerId/Data in src/OroQuizClash.Domain/Audit/AuditEntry.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004-T010) — `AuditAction`, extended `AuditEntry`, `AuditBehavior` 16 mapping, searchable spec
3. Complete Phase 3: User Story 1 (T011-T016)
4. **STOP and VALIDATE**: Test User Story 1 independently via `dotnet test --filter AuditActionCatalog` — 6/16 lifecycle events audited
5. Deploy/demo if ready — MVP lifecycle traceability

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. Add US1 → test independently → deploy/demo (lifecycle)
3. Add US2 → test independently → deploy/demo (+ scoring)
4. Add US3 → test independently → deploy/demo (+ terminal, 16/16)
5. Add US4 → test independently → deploy/demo (+ search/trace)
6. Each story adds value without breaking previous stories; `dotnet test` stays green throughout

### Parallel Team Strategy

With multiple developers after Foundational:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: US1 (T011-T016) — lifecycle
   - Developer B: US2 (T017-T022) — scoring
   - Developer C: US3 (T023-T026) — terminal
   - Developer D: US4 (T027-T033) — search
3. Stories complete and integrate independently, then team collaborates on Polish (T034-T039)

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability (US1=P1, US2=P1, US3=P2, US4=P2)
- Each user story is independently completable and testable — stop at any checkpoint to validate
- Verify tests fail before implementing (TDD where applicable)
- Commit after each task or logical group; keep `AuditAction` as single source of truth for `Action` names
- Avoid: vague tasks, same-file conflicts, cross-story dependencies that break independence; transversal != scattered — keep behavior centralized in `AuditBehavior`
