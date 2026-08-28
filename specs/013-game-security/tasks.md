# Tasks: Game Security

**Input**: Design documents from `/specs/013-game-security/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included — required by constitution (Domain/Application/Api/Architecture) and quickstart.md scenarios

**Organization**: Tasks grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify baseline transversal infrastructure before extending security

- [X] T001 Verify existing JWT auth and policies in `src/OroQuizClash.Api/Program.cs` — `AddAuthentication(JwtBearer)`, `AddAuthorizationBuilder` with `AdminOrGameManager`/`AdminOrRewardManager`, `UseAuthentication`/`UseAuthorization`, `RequireAuthorization` on endpoints
- [X] T002 [P] Review existing idempotency in `src/OroQuizClash.Domain/Games/Rules/ValidateIdempotencyRule.cs` and `src/OroQuizClash.Infrastructure/Specifications/RedemptionSpecifications.cs` plus `RewardRedemptionTypeConfiguration.cs` index `(PlayerId, IdempotencyKey)`
- [X] T003 [P] Review existing validation pipeline in `src/BuildingBlocks/BuildingBlocks.CQRS/Behaviors/ValidationBehavior.cs` and `src/OroQuizClash.Api/Middleware` plus `BuildingBlocks.ServiceDefaults` correlation ID propagation

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core security building blocks that MUST complete before ANY user story — central risk if skipped, all stories depend on these

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 Create `Permission` enumeration in `src/OroQuizClash.Domain/Authorization/Permission.cs` with 14 values (Category.Read...Audit.Read) as `Enumeration` with Id/Name
- [X] T005 [P] Create `Role` enumeration in `src/OroQuizClash.Domain/Authorization/Role.cs` with 4 values (ADMIN/GAME_MANAGER/PLAYER/REWARD_MANAGER) mapping to permissions per FR-003
- [X] T006 [P] Create `AuditEntry` entity in `src/OroQuizClash.Domain/Audit/AuditEntry.cs` as `Entity<Guid>` with fields Id/Timestamp/ActorId/ActorRoles/Action/Permission/Resource/CorrelationId/TenantId/Result/Reason/Details (append-only)
- [X] T007 Create `AuditEntryTypeConfiguration` in `src/OroQuizClash.Infrastructure/Persistence/Configurations/AuditEntryTypeConfiguration.cs` — ToTable("AuditEntries"), indexes on Timestamp/Resource/CorrelationId/ActorId, no Update/Delete
- [X] T008 [P] Create `IdempotencyRecord` entity in `src/OroQuizClash.Domain/Audit/IdempotencyRecord.cs` with Key/ActorId/CreatedAt/ResponseHash/Response + `IdempotencyRecordTypeConfiguration.cs` in `src/OroQuizClash.Infrastructure/Persistence/Configurations/IdempotencyRecordTypeConfiguration.cs` unique index (Key,ActorId)
- [X] T009 Extend `OroQuizClashDbContext` in `src/OroQuizClash.Infrastructure/Persistence/OroQuizClashDbContext.cs` with `DbSet<AuditEntry>` and `DbSet<IdempotencyRecord>` and `OnModelCreating` registrations
- [X] T010 Create `AuthorizationBehavior` pipeline in `src/OroQuizClash.Application/Behaviors/AuthorizationBehavior.cs` — reads `[RequiresPermission(Permission)]` marker on ICommand/IQuery, evaluates `ClaimsPrincipal` via `IHttpContextAccessor`, deny-by-default, audits evaluated permission
- [X] T011 [P] Create `AuditBehavior` pipeline in `src/OroQuizClash.Application/Behaviors/AuditBehavior.cs` — writes `AuditEntry` post-handler (success/denied/validation/rate-limited/replay) with server Timestamp/CorrelationId, sanitized (no secrets)
- [X] T012 [P] Create `SecurityPolicies` central registry in `src/OroQuizClash.Api/Authorization/SecurityPolicies.cs` — 14 policies named per Permission, plus helper `RequirePermissionAttribute` and registration in `Program.cs` `AddAuthorizationBuilder`
- [X] T013 Configure rate limiting in `src/OroQuizClash.Api/Program.cs` using `Microsoft.AspNetCore.RateLimiting` `PartitionedRateLimiter` — policies `GamePlayLimiter` (5/s by sub+gameId), `SensitiveLimiter` (10/10s by sub), `ReadLimiter` (100/10s by IP), configurable via `Security:RateLimit` and `Security:IdempotencyWindowHours`

**Checkpoint**: Foundation ready — RBAC primitives, audit/idempotency storage, pipeline behaviors and policies exist; user stories can now begin in parallel

---

## Phase 3: User Story 1 — Autorización por roles y permisos (Priority: P1) 🎯 MVP

**Goal**: Hacer cumplir matriz 14 permisos × 4 roles con deny-by-default y alcance por recurso; toda operación sin permiso es 403 sin fuga

**Independent Test**: Autenticar con cada rol vía OroIdentityServer JWT (ADMIN/GAME_MANAGER/PLAYER/REWARD_MANAGER), ejecutar operaciones de cada dominio y verificar matriz FR-003; sin token → 401

### Tests for User Story 1

- [X] T014 [P] [US1] Domain matrix test in `tests/OroQuizClash.Domain.Tests/Authorization/PermissionRoleMatrixTests.cs` — asserts ADMIN has 14, GAME_MANAGER has Category/Question/Game/Report, PLAYER has Category.Read/Game.Play/Reward.Read/Redeem, REWARD_MANAGER has Reward.Manage/Report.Read/Audit.Read
- [X] T015 [P] [US1] Application authorization test in `tests/OroQuizClash.Application.Tests/Behaviors/AuthorizationBehaviorTests.cs` — mock ClaimsPrincipal with/without Permission, verifies Allow/Deny and deny-by-default when no marker
- [X] T016 [P] [US1] Api integration test in `tests/OroQuizClash.Api.Tests/Authorization/RbacContractTests.cs` — WebApplicationFactory with JWTs for each role, asserts 200/201 when permitted, 403 when not, 401 when anonymous for endpoints from `contracts/security-policies.md`

### Implementation for User Story 1

- [X] T017 [P] [US1] Annotate commands/queries with permission markers in `src/OroQuizClash.Application/Features/Categories/CreateCategory.cs`, `src/OroQuizClash.Application/Features/Questions/CreateQuestion.cs`, `src/OroQuizClash.Application/Features/Games/CreateGame.cs` etc. — add `[RequiresPermission(Permission.CategoryWrite)]` etc. per contract table
- [X] T018 [US1] Implement resource-level check in `src/OroQuizClash.Application/Behaviors/AuthorizationBehavior.cs` for `Game.Play` — verifies `game.Players.Any(p=>p.UserId==sub)` or `GameClaims.IsOrganizer`, else 403 without existence leak (FR-005)
- [X] T019 [US1] Protect endpoints in `src/OroQuizClash.Api/Endpoints` and `src/OroQuizClash.Application/Features/Games/*Endpoint.cs` — apply `[Authorize(Policy="Game.Play")]` etc. or `RequireAuthorization("Game.Play")` via `SecurityPolicies`, keep `health`/`alive` anonymous per assumptions
- [X] T020 [US1] Ensure deny-by-default in `src/OroQuizClash.Api/Authorization/SecurityPolicies.cs` — any endpoint without explicit Policy returns 403, not 200

**Checkpoint**: At this point, User Story 1 is independently functional — RBAC matrix enforced end-to-end, testable via `dotnet test --filter PermissionRoleMatrix`

---

## Phase 4: User Story 2 — Servidor como única autoridad — anti-manipulación (Priority: P1)

**Goal**: Ignorar Score/Correctness/Time/PlayerId/GameState del cliente; toda decisión autoritativa en servidor, PlayerId solo de `sub` claim, questionId/answerOptionId validados contra ronda actual

**Independent Test**: Enviar SubmitAnswer con score/correctness/elapsedTime/playerId/gameState manipulados; verificar puntaje y estado resultantes coinciden con cálculo servidor (pregunta real + reloj servidor + máquina de estados)

### Tests for User Story 2

- [X] T021 [P] [US2] Domain anti-tampering test in `tests/OroQuizClash.Domain.Tests/Games/SubmitAnswerAuthorityTests.cs` — asserts `Game.SubmitAnswer` ignores client Score/Correctness/Time and uses server `AnswerOption.IsCorrect` + `DateTimeOffset.UtcNow` + `Game.Status`
- [X] T022 [P] [US2] Application test in `tests/OroQuizClash.Application.Tests/Features/Games/SubmitAnswerAuthorityTests.cs` — sends `SubmitAnswerCommand` with extra `Score`/`PlayerId` fields (or via raw JSON), verifies handler resolves `PlayerId` from `ClaimsPrincipal` and ignores body playerId unless ADMIN
- [X] T023 [P] [US2] Api contract test in `tests/OroQuizClash.Api.Tests/Authorization/AntiTamperingContractTests.cs` — posts `POST /api/games/{id}/answers` with `{"answerOptionId":"...","score":9999,"gameState":"FINISHED"}` and verifies 200 with server-calculated points, not 9999, and rejects `questionId` fuera de ronda with 400

### Implementation for User Story 2

- [X] T024 [P] [US2] Sanitize DTOs in `src/OroQuizClash.Application/Features/Games/SubmitAnswer.cs` — remove `Score`/`Correctness`/`ElapsedTime`/`GameState` from `SubmitAnswerRequest` (or mark `[JsonIgnore]`), keep only `AnswerOptionId` (+ optional `IdempotencyKey` if needed for US3)
- [X] T025 [US2] Enforce PlayerId from token in `src/OroQuizClash.Application/Features/Games/SubmitAnswer.cs` and `src/OroQuizClash.Application/Features/Games/WithdrawPlayer.cs` — handler uses `GameClaims.GetSub(User)` exclusively; body `playerId` rejected unless `IsOrganizer` and operation documents impersonation (FR-007)
- [X] T026 [US2] Validate answerOptionId belongs to current round in `src/OroQuizClash.Domain/Games/Game.cs` `SubmitAnswer` — check `CurrentRound` + `QuestionByIdSpecification` + `AnswerOptions` contains `answerOptionId`, else `Error.Validation("InvalidAnswerOption")` per FR-009
- [X] T027 [US2] Ensure `QuestionPresented` filtering already in `src/OroQuizClash.Application/Features/Games/Notifications/RoundFlowBroadcastHandlers.cs` remains without `IsCorrect` and `PlayerAnswered` without `AnswerOptionId` — add assertion in `tests/OroQuizClash.Application.Tests/Features/Games/RealtimePayloadFilteringTests.cs` if not already

**Checkpoint**: At this point, User Stories 1 AND 2 are functional — authorization + server authority independently verified

---

## Phase 5: User Story 3 — Resiliencia operativa transversal (Priority: P2)

**Goal**: Validación 3 niveles, idempotencia por (GameId,PlayerId,RoundId) e `Idempotency-Key`, anti-replay por ventana, rate limiting particionado por juego/jugador

**Independent Test**: Ráfaga 50 idénticas en 1s → 1 efecto; payloads malformados → 400; replay fuera ventana → 400; límite excedido → 429 con Retry-After, sin degradar otros juegos

### Tests for User Story 3

- [X] T028 [P] [US3] Domain validation test in `tests/OroQuizClash.Domain.Tests/Games/ValidateInputTests.cs` — asserts malformed payloads (longitudes/rangos) fail via `IBusinessRule` without internal leak
- [X] T029 [P] [US3] Application idempotency test in `tests/OroQuizClash.Application.Tests/Services/IdempotencyServiceTests.cs` — same `Idempotency-Key` + same hash returns original response, same key + different hash → `ReplayDetected`, outside 24h window → new effect
- [X] T030 [P] [US3] Application test in `tests/OroQuizClash.Application.Tests/Features/Games/SubmitAnswerIdempotencyTests.cs` — second `SubmitAnswer` same `(GameId,PlayerId,RoundId)` returns original `AnswerId` without new `PointTransaction`
- [X] T031 [P] [US3] Api rate limiting test in `tests/OroQuizClash.Api.Tests/RateLimiting/GamePlayRateLimitTests.cs` — WebApplicationFactory, 10 req/s to `POST /api/games/{id}/answers` for same sub+gameId → first 5 succeed/429, other game not affected, headers `Retry-After`/`X-RateLimit-*` present

### Implementation for User Story 3

- [X] T032 [P] [US3] Create `IdempotencyService` in `src/OroQuizClash.Infrastructure/Services/IdempotencyService.cs` — implements window check (24h configurable), stores `IdempotencyRecord`, returns cached response if hash matches, rejects divergent hash as replay
- [X] T033 [P] [US3] Create `IdempotencyBehavior` in `src/OroQuizClash.Application/Behaviors/IdempotencyBehavior.cs` — IPipelineBehavior that checks `Idempotency-Key` header via `IHttpContextAccessor`, delegates to `IdempotencyService` before handler, short-circuits on replay
- [X] T034 [US3] Wire rate limiting in `src/OroQuizClash.Api/Program.cs` — `AddRateLimiter` with `PartitionedRateLimiter` policies `GamePlayLimiter`/`SensitiveLimiter`/`ReadLimiter` and `.RequireRateLimiting(...)` on endpoints per `contracts/rate-limiting.md`, plus `OnRejected` → 429 ProblemDetails with `Retry-After`
- [X] T035 [US3] Strengthen validation in `src/OroQuizClash.Application/Features/Games/SubmitAnswer.cs` `SubmitAnswerValidator` and `src/OroQuizClash.Application/Features/Categories/CreateCategory.cs` etc. — ensure FluentValidation rejects malformed inputs with `Error.Validation` → 400 without internal details per FR-011

**Checkpoint**: All resilience mechanisms independently functional — validation, idempotency, anti-replay, rate limiting verified

---

## Phase 6: User Story 4 — Auditoría y trazabilidad (Priority: P2)

**Goal**: Registro append-only inmutable de todo intento (éxito/denegación/validación/rate-limit/replay) con actor/permiso/recurso/timestamp/correlationId, consultable solo con Audit.Read/Report.Read

**Independent Test**: Ejecutar partida completa con éxitos y rechazos, consultar `GET /api/audit?correlationId=...` → secuencia ordenada completa; sin Audit.Read → 403; intento PUT/DELETE sobre audit → 405

### Tests for User Story 4

- [X] T036 [P] [US4] Application audit test in `tests/OroQuizClash.Application.Tests/Behaviors/AuditBehaviorTests.cs` — asserts every `ICommand` execution (success/denied) writes one `AuditEntry` with correct fields and `CorrelationId`
- [X] T037 [P] [US4] Api integration test in `tests/OroQuizClash.Api.Tests/Audit/AuditApiTests.cs` — verifies `GET /api/audit` returns 200 for ADMIN, 403 for PLAYER, supports `correlationId` filter and pagination, and `POST/PUT/DELETE /api/audit` → 405
- [X] T038 [P] [US4] Architecture append-only test in `tests/OroQuizClash.Architecture.Tests/AuditImmutabilityTests.cs` — asserts `AuditEntry` has no public Update/Delete methods and `OroQuizClashDbContext` exposes only Add, no repository Update for audit

### Implementation for User Story 4

- [X] T039 [P] [US4] Implement `AuditBehavior` write path in `src/OroQuizClash.Application/Behaviors/AuditBehavior.cs` — after handler result, create `AuditEntry` with server `UtcNow`, `ActorId` from `sub`, `ActorRoles`, `Action`, `Permission`, `Resource`, `CorrelationId` from `IHttpContextAccessor`/`Activity.Current`, `Result`/`Reason` sanitized
- [X] T040 [US4] Create `GetAuditEntries` query in `src/OroQuizClash.Application/Features/Audit/GetAuditEntries.cs` — `GetAuditEntriesQuery` with filters `correlationId`/`actorId`/`action`/`resource`/`result`/`from`/`to`/`page`/`pageSize`, handler queries `IRepository<AuditEntry,Guid>` with `AuditEntrySpecification`, requires `Audit.Read` policy
- [X] T041 [US4] Create audit endpoints in `src/OroQuizClash.Api/Endpoints/AuditEndpoints.cs` or `src/OroQuizClash.Application/Features/Audit/GetAuditEntriesEndpoint.cs` — `GET /api/audit` and `GET /api/audit/{id}` with `[Authorize(Policy="Audit.Read")]` and `RequireAuthorization`, deny-by-default, no PUT/DELETE
- [X] T042 [US4] Propagate correlation ID in `src/OroQuizClash.Api/Program.cs` — ensure `BuildingBlocks.ServiceDefaults` `X-Correlation-ID` middleware is enabled and `AuditBehavior` reads it; add tests that same `X-Correlation-ID` header value appears in `AuditEntry.CorrelationId`

**Checkpoint**: All user stories independently functional — audit trail complete, immutable, correlacionado

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Hardening transversal, docs, y validación end-to-end

- [X] T043 [P] Update `src/OroQuizClash.Api/Program.cs` to ensure all endpoints have `RequireAuthorization` except `health`/`alive` per assumptions — audit via endpoint scan test
- [X] T044 [P] Add global sanitization in `src/OroQuizClash.Api/Middleware/GlobalExceptionHandler.cs` or `BuildingBlocks.ServiceDefaults.Middleware.GlobalExceptionHandler` — ensure 401/403/429/400 responses never leak resource existence, tokens or stack traces per FR-020/SC-008
- [X] T045 [P] Architecture test in `tests/OroQuizClash.Architecture.Tests/SecurityDependencyTests.cs` — asserts `OroQuizClash.Domain` does not reference `Microsoft.AspNetCore`, `OroQuizClash.Api` RateLimiting is isolated per game/player (no global limiter)
- [X] T046 Run `dotnet build OroQuizClash.slnx` and `dotnet test OroQuizClash.slnx` — fix any regressions from transversal security behaviors
- [X] T047 Run `specs/013-game-security/quickstart.md` E2E validation — execute RBAC matrix (14×4), anti-tampering (score/time/playerId/gameState), idempotency 50-ráfaga, rate limiting particionado, audit correlation scenarios and verify against SC-001–SC-009
- [X] T048 [P] Update `specs/013-game-security/contracts/*.md` if payloads diverged — ensure audit and rate-limiting headers match actual `Program.cs` and `AuditBehavior` implementation

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational
  - US1 (P1) can start after Foundational — No dependencies on other stories
  - US2 (P1) can start after Foundational — No dependencies on US1 for core logic, but RBAC should be validated first
  - US3 (P2) can start after Foundational — independent of US1/US2, reuses idempotency index
  - US4 (P2) can start after Foundational — independent, but benefits from US1-US3 for full E2E correlation
- **Polish (Phase 7)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational — No dependencies
- **User Story 2 (P1)**: Can start after Foundational — No dependencies on US1, but benefits from RBAC being correct (tampering tests require auth)
- **User Story 3 (P2)**: Can start after Foundational — No dependencies on US1/US2, but rate limiting tests require auth
- **User Story 4 (P2)**: Can start after Foundational — No dependencies, but audit verification needs other stories' operations to generate entries

### Within Each User Story

- Tests (if included) MUST be written and FAIL before implementation
- Permission/Role enumerations before AuthorizationBehavior
- Idempotency service before behavior
- Handlers before endpoints
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel (T002, T003)
- All Foundational tasks marked [P] can run in parallel (T004 vs T005 vs T006 vs T008 vs T010 vs T011 vs T012) except T007 depends on T006 and T009 depends on T006/T008
- Once Foundational completes, all user stories can start in parallel (if staffed)
- All tests for a user story marked [P] can run in parallel (T014 vs T015 vs T016)
- Different user stories can be worked on in parallel by different team members
- Polish tasks T043, T044, T045, T048 can run in parallel before T046/T047

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together:
Task: "Domain matrix test in tests/OroQuizClash.Domain.Tests/Authorization/PermissionRoleMatrixTests.cs"
Task: "Application authorization test in tests/OroQuizClash.Application.Tests/Behaviors/AuthorizationBehaviorTests.cs"
Task: "Api integration test in tests/OroQuizClash.Api.Tests/Authorization/RbacContractTests.cs"

# Launch all models for User Story 1 together:
Task: "Create Permission enumeration in src/OroQuizClash.Domain/Authorization/Permission.cs"
Task: "Create Role enumeration in src/OroQuizClash.Domain/Authorization/Role.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004-T013) — RBAC primitives, audit/idempotency storage, behaviors, policies, rate limiting
3. Complete Phase 3: User Story 1 (T014-T020)
4. **STOP and VALIDATE**: Test User Story 1 independently via `dotnet test --filter PermissionRoleMatrix` — RBAC matrix 14×4
5. Deploy/demo if ready — transversal authorization as MVP

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. Add US1 → test independently → deploy/demo (RBAC)
3. Add US2 → test independently → deploy/demo (+ server authority)
4. Add US3 → test independently → deploy/demo (+ resilience)
5. Add US4 → test independently → deploy/demo (+ audit)
6. Each story adds value without breaking previous stories; `dotnet test` stays green throughout

### Parallel Team Strategy

With multiple developers after Foundational:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: US1 (T014-T020) — RBAC
   - Developer B: US2 (T021-T027) — anti-tampering
   - Developer C: US3 (T028-T035) — resilience
   - Developer D: US4 (T036-T042) — audit
3. Stories complete and integrate independently, then team collaborates on Polish (T043-T048)

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability (US1=P1, US2=P1, US3=P2, US4=P2)
- Each user story is independently completable and testable — stop at any checkpoint to validate
- Verify tests fail before implementing (TDD where applicable)
- Commit after each task or logical group; keep `Permission`/`Role` as single source of truth for policy names
- Avoid: vague tasks, same-file conflicts, cross-story dependencies that break independence; transversal != scattered — keep behaviors centralized
