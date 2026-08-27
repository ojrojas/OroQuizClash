# Tasks: Consolation

**Input**: Design documents from `/specs/010-consolation/`

**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/consolation.openapi.yaml, quickstart.md

**Tests**: Included — automated tests are MANDATORY per constitution Testing Strategy.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Baseline verification before any change

- [x] T001 Verify solution builds and existing tests pass: run `dotnet build` and `dotnet test tests/OroQuizClash.Domain.Tests/`, `dotnet test tests/OroQuizClash.Application.Tests/`, `dotnet test tests/OroQuizClash.Architecture.Tests/` from repo root (baseline: 225 domain, 44 application, 34 architecture tests passing)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Extend existing entities (ConsolationPolicy, GameConfiguration, RewardRedemption) and add eligibility rule. Both aggregates serve multiple stories, so the full domain layer is foundational.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T002 [P] Extend `ConsolationPolicy` Enumeration: add `ParticipationBased(4, "ParticipationBased")` and `RewardBased(5, "RewardBased")` static readonly fields in `src/OroQuizClash.Domain/Games/Enumerations/ConsolationPolicy.cs`
- [x] T003 [P] Extend `GameConfiguration` ValueObject: add properties `MinimumParticipationRounds` (int, default 0), `MinimumAnsweredQuestions` (int, default 0), `ConsolationPoints` (int, default 0), `ConsolationRewardId` (Guid?, default null) with validation rules in constructor; extend `GetEqualityComponents` in `src/OroQuizClash.Domain/Games/ValueObjects/GameConfiguration.cs`
- [x] T004 [P] Add error codes to `GameErrors`: `InvalidConsolationConfiguration` (Validation), `ConsolidationRewardNotFound` (NotFound) in `src/OroQuizClash.Domain/Shared/Errors/GameErrors.cs`
- [x] T005 [P] Add `CreateAsConsolation(Guid playerId, Guid rewardId, Guid gameId)` static factory method on `RewardRedemption`: creates APPROVED redemption with Points=0, no idempotency key, initial transition with system actor, raises `RewardRedeemedDomainEvent` in `src/OroQuizClash.Domain/Rewards/RewardRedemption.cs`
- [x] T006 [P] Create `ConsolationEligibilityRule` (IBusinessRule): checks isActive, isWinner, playerParticipationRounds >= minimumParticipationRounds, playerAnsweredQuestions >= minimumAnsweredQuestions, policy != None in `src/OroQuizClash.Domain/Games/Rules/ConsolationEligibilityRule.cs`
- [x] T007 Update existing `ScoringConsolationTests` to pass new `GameConfiguration` parameters (MinimumParticipationRounds=0, MinimumAnsweredQuestions=0, ConsolationPoints=100, null) — backward-compatible defaults should make this minimal; verify all 3 existing tests still pass in `tests/OroQuizClash.Domain.Tests/Games/ScoringConsolationTests.cs`
- [x] T008 Update `ScoringTestBase.Config()` test helper to accept and pass the new `GameConfiguration` parameters with defaults in `tests/OroQuizClash.Domain.Tests/Games/ScoringTestBase.cs`
- [x] T009 Verify build after foundational changes: `dotnet build` must compile with 0 errors and existing tests must pass

**Checkpoint**: Foundation ready — user story implementation can now begin

---

## Phase 3: User Story 1 - Eligible Players Receive Consolation at Game Finish (Priority: P1) 🎯 MVP

**Goal**: Players who participated but did not win receive consolation at game finish according to the configured policy. Eligibility is evaluated using configurable minimum thresholds. Winner determination happens BEFORE consolation (bug fix).

**Independent Test**: Create a game with FixedPoints policy, have 2 players (one winner, one non-winner with sufficient participation), finish, and verify the non-winner received a CONSOLATION transaction while the winner did not.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T010 [P] [US1] Domain unit tests for `ConsolationEligibilityRule`: eligible (active, non-winner, meets thresholds), ineligible (winner), ineligible (below MinRounds), ineligible (below MinAnswered), ineligible (eliminated), ineligible (policy=None), eligible (withdrawn but meets thresholds) in `tests/OroQuizClash.Domain.Tests/Games/ConsolationEligibilityRuleTests.cs`
- [x] T011 [P] [US1] Domain unit tests for refactored `Game.Finish()` consolidation: FixedPoints awards to eligible non-winners only, None awards nothing, ParticipationBased awards scaled points, winner never receives consolation, eliminated player excluded, withdrawn player eligible if meets thresholds, no double consolidation (idempotent), game bonus awarded BEFORE consolation, winners determined BEFORE consolation in `tests/OroQuizClash.Domain.Tests/Games/ConsolidationFinishTests.cs`

### Implementation for User Story 1

- [x] T012 [US1] Implement `ConsolationEligibilityRule` logic: `IsBroken()` returns true when NOT eligible (policy==None OR !isActive OR isWinner OR rounds < minimum OR questions < minimum) in `src/OroQuizClash.Domain/Games/Rules/ConsolationEligibilityRule.cs`
- [x] T013 [US1] Refactor `Game.Finish()` consolidation logic: (1) award game bonus to active players, (2) determine winners from post-bonus scores, (3) for each non-winner active player evaluate `ConsolationEligibilityRule`, (4) if eligible: FixedPoints → CONSOLATION transaction, ParticipationBased → scaled CONSOLATION transaction, (5) mark winners, (6) set FINISHED status in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T014 [US1] Run US1 tests and verify pass: `dotnet test tests/OroQuizClash.Domain.Tests/ --filter "ConsolationEligibilityRule|ConsolidationFinish"`

**Checkpoint**: User Story 1 fully functional — eligible non-winners receive consolation (MVP)

---

## Phase 4: User Story 2 - Reward-Based Consolation (Priority: P2)

**Goal**: When the policy is RewardBased, the system creates an APPROVED RewardRedemption for the configured consolation reward for each eligible non-winning player. The redemption bypasses manual approval.

**Independent Test**: Configure a game with RewardBased policy referencing an existing reward, finish with eligible non-winners, and verify APPROVED redemptions are created with correct reward reference and no stock decrement.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T015 [P] [US2] Domain unit tests for `RewardRedemption.CreateAsConsolation`: creates APPROVED redemption, Points=0, no stock change, initial transition recorded, event raised; validates playerId/rewardId/gameId not empty in `tests/OroQuizClash.Domain.Tests/Rewards/ConsolationRewardTests.cs`
- [x] T016 [P] [US2] Domain integration tests for reward-based consolidation in `Game.Finish()`: RewardBased policy creates APPROVED RewardRedemption for eligible non-winners, no redemption for winner, no redemption for ineligible, stock unchanged in `tests/OroQuizClash.Domain.Tests/Games/ConsolidationFinishTests.cs` (extend existing file)

### Implementation for User Story 2

- [x] T017 [US2] Implement `CreateAsConsolation` factory method: static method returning `Result<RewardRedemption>`, sets Status=APPROVED, Points=0, creates initial transition, raises `RewardRedeemedDomainEvent`, does NOT touch stock in `src/OroQuizClash.Domain/Rewards/RewardRedemption.cs`
- [x] T018 [US2] Extend `Game.Finish()` to handle RewardBased policy: for each eligible non-winner when policy is RewardBased, call `RewardRedemption.CreateAsConsolation(playerId, Configuration.ConsolationRewardId.Value, Id.Value)` and store the redemption for persistence in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T019 [US2] Run US2 tests and verify pass: `dotnet test tests/OroQuizClash.Domain.Tests/ --filter "ConsolationReward|ConsolidationFinish"`

**Checkpoint**: User Stories 1 AND 2 both work — point-based and reward-based consolidation operational

---

## Phase 5: User Story 3 - Consolation History and Status (Priority: P3)

**Goal**: Players can query their consolidation status for a specific game and their full consolidation history. Administrators can view all consolidation awards.

**Independent Test**: Query consolidation status for a player who received FixedPoints consolidation and verify the response includes policy, points, and timestamp. Query full history and verify all past awards are listed.

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T020 [P] [US3] Application tests for `GetPlayerConsolationStatusHandler`: player with CONSOLATION transaction returns received=true with policy/points/timestamp; player without returns received=false; game not found returns error; player not in game returns error in `tests/OroQuizClash.Application.Tests/Features/Games/ConsolidationStatusHandlerTests.cs`
- [x] T021 [P] [US3] Application tests for `GetPlayerConsolationHistoryHandler`: returns list of all CONSOLATION transactions across games with game reference, policy, points, timestamp; empty list for player with no consolation in `tests/OroQuizClash.Application.Tests/Features/Games/ConsolidationHistoryHandlerTests.cs`

### Implementation for User Story 3

- [x] T022 [US3] Create `GetPlayerConsolationStatus` Vertical Slice: Query (GameId, PlayerId) + Handler (load game via GameByIdWithAnswersSpecification, filter PointTransactions for CONSOLATION type + playerId, map to response per contracts/consolation.openapi.yaml ConsolationStatusResponse) + Endpoint (`GET /api/games/{gameId}/players/{playerId}/consolation`, RequireAuthorization) in `src/OroQuizClash.Application/Features/Games/GetPlayerConsolationStatus.cs`
- [x] T023 [US3] Create `GetPlayerConsolationHistory` Vertical Slice: Query (PlayerId) + Handler (query all games containing player, filter CONSOLATION transactions, map to ConsolationHistoryResponse per contract) + Endpoint (`GET /api/players/{playerId}/consolation-history`, RequireAuthorization) in `src/OroQuizClash.Application/Features/Games/GetPlayerConsolationHistory.cs`
- [x] T024 [US3] Run US3 tests and verify pass: `dotnet test tests/OroQuizClash.Application.Tests/ --filter "ConsolidationStatus|ConsolidationHistory"`

**Checkpoint**: All three user stories independently functional — full consolation engine operational

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Architecture enforcement, regression verification, quickstart validation

- [x] T025 [P] Create architecture tests: ConsolationPolicy has 5 values, GameConfiguration has new properties, ConsolationEligibilityRule implements IBusinessRule, Game.Finish exists, GetPlayerConsolationStatus/History implement IQueryHandler, no MediatR/MassTransit/AutoMapper in consolidation code in `tests/OroQuizClash.Architecture.Tests/ConsolidationDependenciesTests.cs`
- [x] T026 Run full test suite and verify zero regressions: `dotnet test tests/OroQuizClash.Domain.Tests/`, `dotnet test tests/OroQuizClash.Application.Tests/`, `dotnet test tests/OroQuizClash.Architecture.Tests/`
- [x] T027 Run quickstart.md validation scenarios 1–10 via domain/application tests
- [x] T028 Verify `dotnet build` from repo root with 0 errors

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Foundational — core consolidation logic
- **US2 (Phase 4)**: Depends on Foundational — extends US1's Finish() with reward-based path
- **US3 (Phase 5)**: Depends on Foundational — queries read existing ledger data; independent of US1/US2 implementation
- **Polish (Phase 6)**: Depends on all user stories complete

### User Story Dependencies

- **US1 (P1)**: After Foundational — MVP, no story dependencies
- **US2 (P2)**: After Foundational — extends US1's `Game.Finish()` consolidation logic; independently testable via seeded scenarios
- **US3 (P3)**: After Foundational — reads existing ledger data; queries are independent of consolidation implementation details

### Within Each User Story

- Tests written FIRST and must FAIL before implementation
- Domain behavior before application slices
- Story tests green before moving to next priority

### Parallel Opportunities

- T002–T006 (Foundational enums/config/rule/errors): all [P], 5 parallel tasks
- T010+T011 (US1 tests), T015+T016 (US2 tests), T020+T021 (US3 tests): parallel within story
- After Foundational, US1/US2/US3 can be staffed in parallel

---

## Parallel Example: After Phase 2

```bash
# Launch all US1 tests together (write first, must fail):
Task: "Domain tests ConsolationEligibilityRule in tests/OroQuizClash.Domain.Tests/Games/ConsolationEligibilityRuleTests.cs"
Task: "Domain tests Game.Finish() consolidation in tests/OroQuizClash.Domain.Tests/Games/ConsolidationFinishTests.cs"

# Launch US2 tests together:
Task: "Domain tests CreateAsConsolation in tests/OroQuizClash.Domain.Tests/Rewards/ConsolationRewardTests.cs"
Task: "Domain integration tests reward-based in tests/OroQuizClash.Domain.Tests/Games/ConsolidationFinishTests.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: Foundational (T002–T009) — CRITICAL, blocks all stories
3. Complete Phase 3: User Story 1 (T010–T014)
4. **STOP and VALIDATE**: FixedPoints consolidation works end-to-end (quickstart scenarios 1–4, 6, 8–9)
5. Deploy/demo if ready — non-winners already receive point-based consolation

### Incremental Delivery

1. Setup + Foundational → extended enums + config + eligibility rule ready
2. Add US1 → FixedPoints/ParticipationBased consolidation → **MVP!**
3. Add US2 → reward-based consolidation → full policy support
4. Add US3 → history/status queries → player visibility + audit
5. Polish → architecture tests + full regression

### Parallel Team Strategy

With multiple developers after Foundational:

- Developer A: US1 (eligibility rule + Game.Finish refactor)
- Developer B: US2 (CreateAsConsolation + reward integration)
- Developer C: US3 (query slices)

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to user story for traceability
- Tests are mandatory (constitution Testing Strategy) — write first, verify failing, then implement
- `GameConfiguration` constructor has backward-compatible defaults — existing callers compile unchanged
- Renamed `Badge(3)` → `RewardBased(3)` — Badge was never used/tested in the codebase
- `Game.Finish()` refactored: winners determined BEFORE consolidation (fixes current bug)
- Commit after each task or logical group; stop at any checkpoint to validate the story independently
