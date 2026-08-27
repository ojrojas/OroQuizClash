# Tasks: Rewards & Point Redemption

**Input**: Design documents from `/specs/009-reward-redemption/`

**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/rewards.openapi.yaml, quickstart.md

**Tests**: Included — automated tests are MANDATORY per constitution Testing Strategy (Domain Unit Tests, Application Tests, Architecture Tests).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Baseline verification before any change

- [x] T001 Verify solution builds and existing tests pass: run `dotnet build` and `dotnet test tests/OroQuizClash.Domain.Tests/`, `dotnet test tests/OroQuizClash.Application.Tests/`, `dotnet test tests/OroQuizClash.Architecture.Tests/` from repo root (baseline: 182 domain, 30 application, 25 architecture tests passing)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Complete `Rewards` domain model + persistence + DI wiring. Both aggregates serve multiple stories, so the full domain layer is foundational.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T002 [P] Create `RewardId` StronglyTypedId in `src/OroQuizClash.Domain/Rewards/RewardId.cs`
- [x] T003 [P] Create `RewardRedemptionId` StronglyTypedId in `src/OroQuizClash.Domain/Rewards/RewardRedemptionId.cs`
- [x] T004 [P] Create `RedemptionTransitionId` StronglyTypedId in `src/OroQuizClash.Domain/Rewards/RedemptionTransitionId.cs`
- [x] T005 [P] Create `RewardStatus` Enumeration (Active=1 "ACTIVE", Inactive=2 "INACTIVE") with `IsActive` property in `src/OroQuizClash.Domain/Rewards/RewardStatus.cs`
- [x] T006 [P] Create `RedemptionStatus` Enumeration (Requested=1 "REQUESTED", Approved=2 "APPROVED", Rejected=3 "REJECTED", Delivered=4 "DELIVERED", Cancelled=5 "CANCELLED") with `IsTerminal` property (true for REJECTED/DELIVERED/CANCELLED) in `src/OroQuizClash.Domain/Rewards/RedemptionStatus.cs`
- [x] T007 [P] Create `RewardErrors` static error class per data-model.md error table (RewardNotFound, InvalidRewardName, InvalidRewardDescription, InvalidPointsRequired, InvalidStock, RewardUnavailable, RewardAlreadyActive, RewardAlreadyInactive, RedemptionNotFound, InvalidRedemptionTransition, NotRedemptionOwner) in `src/OroQuizClash.Domain/Rewards/RewardErrors.cs`
- [x] T008 [P] Create `RewardNameValidRule` (3–100 chars, not whitespace) in `src/OroQuizClash.Domain/Rewards/Rules/RewardNameValidRule.cs`
- [x] T009 [P] Create `PointsRequiredPositiveRule` (cost > 0) in `src/OroQuizClash.Domain/Rewards/Rules/PointsRequiredPositiveRule.cs`
- [x] T010 [P] Create `StockNotNegativeRule` (stock ≥ 0) in `src/OroQuizClash.Domain/Rewards/Rules/StockNotNegativeRule.cs`
- [x] T011 [P] Create `RewardAvailableRule` (status ACTIVE && Stock > 0 && not expired at given now) in `src/OroQuizClash.Domain/Rewards/Rules/RewardAvailableRule.cs`
- [x] T012 [P] Create `RedemptionTransitionRule` (validates allowed state transitions per data-model.md state machine: REQUESTED→APPROVED/REJECTED/CANCELLED, APPROVED→DELIVERED/CANCELLED; terminal states immutable) in `src/OroQuizClash.Domain/Rewards/Rules/RedemptionTransitionRule.cs`
- [x] T013 [P] Create catalog domain events `RewardCreatedDomainEvent` (RewardId), `RewardUpdatedDomainEvent` (RewardId), `RewardStatusChangedDomainEvent` (RewardId, StatusName) in `src/OroQuizClash.Domain/Rewards/Events/RewardCreatedDomainEvent.cs`, `src/OroQuizClash.Domain/Rewards/Events/RewardUpdatedDomainEvent.cs`, `src/OroQuizClash.Domain/Rewards/Events/RewardStatusChangedDomainEvent.cs`
- [x] T014 [P] Create redemption domain events `RewardRedeemedDomainEvent` (RedemptionId, RewardId, PlayerId, GameId, Points) and `RedemptionStatusChangedDomainEvent` (RedemptionId, StatusName, ActorId) in `src/OroQuizClash.Domain/Rewards/Events/RewardRedeemedDomainEvent.cs`, `src/OroQuizClash.Domain/Rewards/Events/RedemptionStatusChangedDomainEvent.cs`
- [x] T015 Create `Reward` AggregateRoot per data-model.md: fields (Name, Description, PointsRequired, Stock, Status, ExpirationDate?, CreatedAt, UpdatedAt?, RowVersion) + behavior (`Create` with rules + RewardCreatedDomainEvent, `Update`, `Activate`/`Deactivate` with RewardAlreadyActive/Inactive errors + RewardStatusChangedDomainEvent, `ReserveStock(now)` using RewardAvailableRule then decrement, `ReleaseStock`, computed `IsAvailable(now)`) in `src/OroQuizClash.Domain/Rewards/Reward.cs` (depends on T002, T005, T007, T008–T011, T013)
- [x] T016 Create `RedemptionTransition` Entity (Status, ActorId, At) and `RewardRedemption` AggregateRoot per data-model.md: fields (PlayerId, RewardId, GameId, Points, Status, RequestedAt, DeliveredAt?, IdempotencyKey?, RowVersion, Transitions collection) + behavior (`Create` appends initial transition + RewardRedeemedDomainEvent, `Approve(managerId)`, `Reject(managerId)`, `Deliver(managerId)` sets DeliveredAt, `Cancel(playerId)` with NotRedemptionOwner check — all guarded by RedemptionTransitionRule, appending transition + raising RedemptionStatusChangedDomainEvent) in `src/OroQuizClash.Domain/Rewards/RedemptionTransition.cs` and `src/OroQuizClash.Domain/Rewards/RewardRedemption.cs` (depends on T003, T004, T006, T007, T012, T014)
- [x] T017 Create `RewardTypeConfiguration`: table `Rewards`, id conversion, `Status` int conversion via `RewardStatus.FromId`, Name max 100 / Description max 500, `RowVersion` IsRowVersion+IsConcurrencyToken, index on Status in `src/OroQuizClash.Infrastructure/Persistence/Configurations/RewardTypeConfiguration.cs` (depends on T015)
- [x] T018 Create `RewardRedemptionTypeConfiguration`: table `RewardRedemptions`, id conversions (RewardRedemptionId, RewardId, GameId), `Status` int conversion via `RedemptionStatus.FromId`, `Transitions` as child table `RedemptionTransitions` (FK cascade), unique filtered index on `IdempotencyKey` (non-null), indexes on PlayerId / RewardId / Status, `RowVersion` concurrency in `src/OroQuizClash.Infrastructure/Persistence/Configurations/RewardRedemptionTypeConfiguration.cs` (depends on T016)
- [x] T019 Add `DbSet<Reward> Rewards` and `DbSet<RewardRedemption> RewardRedemptions` to `src/OroQuizClash.Infrastructure/Persistence/OroQuizClashDbContext.cs` (depends on T015, T016)
- [x] T020 Register `IRepository<Reward, RewardId>` and `IRepository<RewardRedemption, RewardRedemptionId>` (EfRepository) and add `AdminOrRewardManager` authorization policy (roles ADMIN or REWARD_MANAGER, same claim pattern as existing `AdminOrGameManager`) in `src/OroQuizClash.Api/Program.cs` (depends on T015, T016)
- [x] T021 [P] Create `RewardSpecifications` (AvailableRewards(now): active + stock > 0 + not expired ordered by PointsRequired; RewardById; AllRewards) and `RedemptionSpecifications` (RedemptionById; RedemptionsByPlayer; RedemptionsByStatus; RedemptionByIdempotencyKey(playerId, key)) in `src/OroQuizClash.Infrastructure/Specifications/RewardSpecifications.cs` and `src/OroQuizClash.Infrastructure/Specifications/RedemptionSpecifications.cs` (depends on T015, T016)
- [x] T022 Verify build after foundational changes: `dotnet build` must compile with 0 errors

**Checkpoint**: Foundation ready — user story implementation can now begin

---

## Phase 3: User Story 1 - Player Redeems a Reward with Points (Priority: P1) 🎯 MVP

**Goal**: A player browses the reward catalog and redeems a reward: atomic validation (sufficient eligible points RWD-001/002, in stock RWD-004, not expired RWD-005, active FR-009) + point deduction via game ledger + stock decrement + REQUESTED redemption record (RWD-003), with idempotent duplicate handling (FR-017).

**Independent Test**: Seed an active reward with stock, give a player points in a game, redeem, and verify points deducted (REWARD_REDEMPTION ledger entry), stock −1, redemption REQUESTED — plus each rejection rule leaving zero side effects.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T023 [P] [US1] Domain unit tests for Reward availability & stock: create defaults to ACTIVE, ReserveStock success decrements, ReserveStock rejects inactive / zero stock / expired (RewardUnavailable) with stock unchanged, ReleaseStock increments, IsAvailable matrix (status × stock × expiration), Update validates fields in `tests/OroQuizClash.Domain.Tests/Rewards/RewardAvailabilityTests.cs`
- [x] T024 [P] [US1] Application tests for RedeemRewardHandler: success (REQUESTED redemption, stock −1, REWARD_REDEMPTION transaction −cost, balance reduced), insufficient points (InsufficientPoints, zero side effects), out of stock, expired, inactive reward (RewardUnavailable), reward not found, game not found, player not in game, duplicate idempotencyKey returns original redemption without second deduction, catalog query returns only available rewards + balance when gameId provided in `tests/OroQuizClash.Application.Tests/Features/Rewards/RedeemRewardHandlerTests.cs`

### Implementation for User Story 1

- [x] T025 [US1] Create `GetRewards` Vertical Slice: Query (optional GameId, IncludeUnavailable) + Handler (available rewards via RewardSpecifications; IncludeUnavailable restricted to managers returns AllRewards; when GameId present include requesting player's CurrentPoints balance from GamePlayer) + Response per contracts/rewards.openapi.yaml RewardCatalogResponse + Endpoint (`GET /api/rewards`, RequireAuthorization) in `src/OroQuizClash.Application/Features/Rewards/GetRewards.cs`
- [x] T026 [US1] Create `RedeemReward` Vertical Slice: Command (RewardId, GameId, PlayerId, IdempotencyKey?) + Validator + Handler (idempotency check via RedemptionSpecifications.RedemptionByIdempotencyKey → return existing; load Reward → ReserveStock(serverUtcNow); load Game → ConsumePoints(playerId, cost, reason incl. redemption id); create RewardRedemption; single SaveChangesAsync) + Response per contract + Endpoint (`POST /api/rewards/{rewardId}/redeem`, RequireAuthorization, player from `sub` claim) in `src/OroQuizClash.Application/Features/Rewards/RedeemReward.cs` (depends on T025 only for shared response conventions — no code dependency)
- [x] T027 [US1] Run US1 tests and verify pass: `dotnet test tests/OroQuizClash.Domain.Tests/ --filter "RewardAvailability"` and `dotnet test tests/OroQuizClash.Application.Tests/ --filter "RedeemReward"`

**Checkpoint**: User Story 1 fully functional — players can browse and redeem rewards (MVP)

---

## Phase 4: User Story 2 - Redemption Review, Delivery, and Resolution (Priority: P2)

**Goal**: Reward managers approve/reject requested redemptions and mark approved ones delivered; players cancel pending redemptions; rejection/cancellation refund points (ledger ADJUSTMENT) and release stock; every transition records actor + timestamp (RWD-006); terminal states immutable.

**Independent Test**: Given a seeded REQUESTED redemption: approve → deliver verifies DELIVERED + DeliveredAt + audit trail; reject/cancel verify exact point refund + stock release; non-owner cancel refused; transitions on terminal states refused.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T028 [P] [US2] Domain unit tests for redemption lifecycle: Create → REQUESTED with initial transition (actor = player), Approve only from REQUESTED, Reject only from REQUESTED, Deliver only from APPROVED sets DeliveredAt, Cancel allowed from REQUESTED/APPROVED by owner only (NotRedemptionOwner for others), terminal states (REJECTED/DELIVERED/CANCELLED) reject all transitions (InvalidRedemptionTransition), every transition appends (Status, ActorId, At) record in `tests/OroQuizClash.Domain.Tests/Rewards/RedemptionLifecycleTests.cs`
- [x] T029 [P] [US2] Domain unit tests for `Game.RefundPoints`: credits player balance (secured via Award roundScoped:false), appends positive ADJUSTMENT transaction with reason and correct ResultingBalance, raises ScoreUpdatedDomainEvent, rejects amount ≤ 0 (InvalidAdjustmentAmount) and unknown player (PlayerNotInGame), works regardless of game state in `tests/OroQuizClash.Domain.Tests/Games/GameRefundPointsTests.cs`
- [x] T030 [P] [US2] Application tests for processing handlers: ApproveRedemption (REQUESTED→APPROVED, manager recorded), RejectRedemption (refund transaction + stock released + single SaveChanges), DeliverRedemption (APPROVED→DELIVERED + DeliveredAt), CancelRedemption by owner from REQUESTED and APPROVED (refund + stock release), cancel by non-owner refused, invalid transitions return error with no state change, redemption not found, GetPlayerRedemptions returns only own history, manager GetRedemptions filters by status in `tests/OroQuizClash.Application.Tests/Features/Rewards/RedemptionProcessingHandlerTests.cs`

### Implementation for User Story 2

- [x] T031 [US2] Implement `Game.RefundPoints(Guid playerId, int amount, string reason)` returning `Result<PointTransaction>`: validate amount > 0 + player in game, credit `Score.Award(amount, roundScoped: false)`, append positive ADJUSTMENT transaction, raise ScoreUpdatedDomainEvent — mirroring ConsumePoints/AdjustPoints style without game-state check in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T032 [US2] Create `ApproveRedemption` Vertical Slice: Command (RedemptionId, ManagerId) + Handler (load redemption, Approve, SaveChanges) + Response per contract + Endpoint (`POST /api/redemptions/{redemptionId}/approve`, RequireAuthorization("AdminOrRewardManager")) in `src/OroQuizClash.Application/Features/Rewards/ApproveRedemption.cs`
- [x] T033 [US2] Create `RejectRedemption` Vertical Slice: Command (RedemptionId, ManagerId) + Handler (load redemption + reward + funding game; Reject; `game.RefundPoints(playerId, points, "Refund for redemption {id} (REJECTED)")`; `reward.ReleaseStock()`; single SaveChangesAsync) + Endpoint (`POST /api/redemptions/{redemptionId}/reject`, AdminOrRewardManager) in `src/OroQuizClash.Application/Features/Rewards/RejectRedemption.cs`
- [x] T034 [US2] Create `DeliverRedemption` Vertical Slice: Command (RedemptionId, ManagerId) + Handler (load, Deliver, SaveChanges) + Endpoint (`POST /api/redemptions/{redemptionId}/deliver`, AdminOrRewardManager) in `src/OroQuizClash.Application/Features/Rewards/DeliverRedemption.cs`
- [x] T035 [US2] Create `CancelRedemption` Vertical Slice: Command (RedemptionId, PlayerId) + Handler (ownership via Cancel(playerId); load reward + funding game; `game.RefundPoints(..., "(CANCELLED)")`; `reward.ReleaseStock()`; single SaveChangesAsync) + Endpoint (`POST /api/redemptions/{redemptionId}/cancel`, RequireAuthorization, player from `sub`) in `src/OroQuizClash.Application/Features/Rewards/CancelRedemption.cs`
- [x] T036 [US2] Create `GetPlayerRedemptions` Vertical Slice: Query (PlayerId) + Handler (RedemptionsByPlayer, newest first) + Response per contract + Endpoint (`GET /api/redemptions`, RequireAuthorization, player from `sub`) in `src/OroQuizClash.Application/Features/Rewards/GetPlayerRedemptions.cs`
- [x] T037 [US2] Create `GetRedemptions` Vertical Slice (manager): Query (optional Status) + Handler (RedemptionsByStatus / all) + detail query returning full transition history (RedemptionDetailResponse per contract) + Endpoints (`GET /api/redemptions/all` + `GET /api/redemptions/{redemptionId}`, AdminOrRewardManager for /all; detail allows owner or manager) in `src/OroQuizClash.Application/Features/Rewards/GetRedemptions.cs`
- [x] T038 [US2] Run US2 tests and verify pass: `dotnet test tests/OroQuizClash.Domain.Tests/ --filter "RedemptionLifecycle|RefundPoints"` and `dotnet test tests/OroQuizClash.Application.Tests/ --filter "RedemptionProcessing"`

**Checkpoint**: User Stories 1 AND 2 both work — redemptions flow through the full lifecycle with refunds and audit

---

## Phase 5: User Story 3 - Reward Catalog Management (Priority: P3)

**Goal**: Reward managers create, update, activate, and deactivate rewards; invalid catalog data rejected; deactivated rewards hidden from players but pending redemptions still processable; no deletion of rewards with history.

**Independent Test**: Create a reward via management slices and verify it appears in the player catalog; deactivate and verify it is hidden and unredeemable while a pending redemption still approves/delivers; restock from 0 re-enables redemption.

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T039 [P] [US3] Domain unit tests for catalog management: Create validates name (3–100), description (3–500), pointsRequired > 0, stock ≥ 0, expiration in future; Update changes provided fields + sets UpdatedAt (pending redemptions unaffected — Points immutable on redemption); Activate only from INACTIVE (RewardAlreadyActive otherwise), Deactivate only from ACTIVE (RewardAlreadyInactive otherwise) in `tests/OroQuizClash.Domain.Tests/Rewards/RewardCatalogTests.cs`
- [x] T040 [P] [US3] Application tests for catalog handlers: CreateRewardHandler (success returns ACTIVE reward; validation failures), UpdateRewardHandler (partial update, not found), ActivateRewardHandler/DeactivateRewardHandler (transitions + already-active/inactive errors + not found), GetRewards includeUnavailable returns full catalog with status for managers in `tests/OroQuizClash.Application.Tests/Features/Rewards/RewardCatalogHandlerTests.cs`

### Implementation for User Story 3

- [x] T041 [US3] Create `CreateReward` Vertical Slice: Command (Name, Description, PointsRequired, Stock, ExpirationDate?) + Validator + Handler (Reward.Create, SaveChanges) + Response per contract + Endpoint (`POST /api/rewards`, AdminOrRewardManager) in `src/OroQuizClash.Application/Features/Rewards/CreateReward.cs`
- [x] T042 [US3] Create `UpdateReward` Vertical Slice: Command (RewardId, optional fields) + Validator + Handler (load, Update, SaveChanges) + Endpoint (`PUT /api/rewards/{rewardId}`, AdminOrRewardManager) in `src/OroQuizClash.Application/Features/Rewards/UpdateReward.cs`
- [x] T043 [US3] Create `ActivateReward` Vertical Slice: Command (RewardId) + Handler (load, Activate, SaveChanges) + Endpoint (`POST /api/rewards/{rewardId}/activate`, AdminOrRewardManager) in `src/OroQuizClash.Application/Features/Rewards/ActivateReward.cs`
- [x] T044 [US3] Create `DeactivateReward` Vertical Slice: Command (RewardId) + Handler (load, Deactivate, SaveChanges) + Endpoint (`POST /api/rewards/{rewardId}/deactivate`, AdminOrRewardManager) in `src/OroQuizClash.Application/Features/Rewards/DeactivateReward.cs`
- [x] T045 [US3] Run US3 tests and verify pass: `dotnet test tests/OroQuizClash.Domain.Tests/ --filter "RewardCatalog"` and `dotnet test tests/OroQuizClash.Application.Tests/ --filter "RewardCatalog"`

**Checkpoint**: All three user stories independently functional — full rewards engine operational

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Architecture enforcement, regression verification, quickstart validation

- [x] T046 [P] Create architecture tests: Domain/Rewards has no references to Infrastructure/Web/EF, Reward and RewardRedemption have no public setters, RewardStatus/RedemptionStatus derive from Enumeration, slices implement BuildingBlocks ICommand/IQuery + IEndpoint, no MediatR/MassTransit/AutoMapper in Rewards features, Application Features/Rewards does not reference concrete EfRepository in `tests/OroQuizClash.Architecture.Tests/RewardDependenciesTests.cs`
- [x] T047 Run full test suite and verify zero regressions: `dotnet test tests/OroQuizClash.Domain.Tests/`, `dotnet test tests/OroQuizClash.Application.Tests/`, `dotnet test tests/OroQuizClash.Architecture.Tests/`
- [x] T048 Run quickstart.md validation scenarios 1–10 via domain/application tests (scenario mapping in quickstart.md "Expected Test Coverage Mapping")
- [x] T049 Verify `dotnet build` from repo root with 0 errors

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories (both aggregates serve US1+US2+US3)
- **US1 (Phase 3)**: Depends on Foundational — no dependencies on other stories
- **US2 (Phase 4)**: Depends on Foundational — operates on redemptions created by US1 flow but independently testable via seeded REQUESTED redemptions
- **US3 (Phase 5)**: Depends on Foundational — independently testable; US1's GetRewards slice already serves the player catalog, US3 adds management
- **Polish (Phase 6)**: Depends on all user stories complete

### User Story Dependencies

- **US1 (P1)**: After Foundational — MVP, no story dependencies
- **US2 (P2)**: After Foundational — needs US1's `RedeemReward` only for end-to-end flow; handler tests seed REQUESTED redemptions directly
- **US3 (P3)**: After Foundational — management slices independent; catalog visibility assertions reuse US1's GetRewards

### Within Each User Story

- Tests written FIRST and must FAIL before implementation
- Domain behavior (Foundational) before application slices
- Application slices before endpoint verification
- Story tests green before moving to next priority

### Parallel Opportunities

- T002–T014 (Foundational ids/enums/errors/rules/events): all [P], 13 parallel tasks
- T023+T024 (US1 tests), T028+T029+T030 (US2 tests), T039+T040 (US3 tests): parallel within story
- T025+T026 (US1 slices) share no files — parallelizable
- T032–T037 (US2 slices): one file each — parallelizable after T031
- T041–T044 (US3 slices): one file each — parallelizable
- After Foundational, US1/US2/US3 can be staffed in parallel

---

## Parallel Example: After Phase 2

```bash
# Launch all US1 tests together (write first, must fail):
Task: "Domain tests Reward availability & stock in tests/OroQuizClash.Domain.Tests/Rewards/RewardAvailabilityTests.cs"
Task: "Application tests RedeemRewardHandler in tests/OroQuizClash.Application.Tests/Features/Rewards/RedeemRewardHandlerTests.cs"

# Launch US2 slices together (after T031 Game.RefundPoints):
Task: "ApproveRedemption slice in src/OroQuizClash.Application/Features/Rewards/ApproveRedemption.cs"
Task: "RejectRedemption slice in src/OroQuizClash.Application/Features/Rewards/RejectRedemption.cs"
Task: "DeliverRedemption slice in src/OroQuizClash.Application/Features/Rewards/DeliverRedemption.cs"
Task: "CancelRedemption slice in src/OroQuizClash.Application/Features/Rewards/CancelRedemption.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: Foundational (T002–T022) — CRITICAL, blocks all stories
3. Complete Phase 3: User Story 1 (T023–T027)
4. **STOP and VALIDATE**: redemption works end-to-end (quickstart scenarios 1–5)
5. Deploy/demo if ready — points already convert to prizes

### Incremental Delivery

1. Setup + Foundational → domain model + persistence ready
2. Add US1 → redeem works → **MVP!**
3. Add US2 → approval/delivery/refund lifecycle → operational completeness
4. Add US3 → catalog management → no more seeded data needed
5. Polish → architecture tests + full regression

### Parallel Team Strategy

With multiple developers after Foundational:

- Developer A: US1 (redemption + catalog query)
- Developer B: US2 (processing + refunds) — needs T031 (Game.RefundPoints) first
- Developer C: US3 (catalog management)

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to user story for traceability
- Tests are mandatory (constitution Testing Strategy) — write first, verify failing, then implement
- Cross-aggregate atomicity (RWD-003) lives in handlers: one `SaveChangesAsync` per operation; concurrency via RowVersion on Reward + Game
- Refunds always use `Game.RefundPoints` (positive ADJUSTMENT) — never mutate ledger entries
- Commit after each task or logical group; stop at any checkpoint to validate the story independently
