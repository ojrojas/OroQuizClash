# Tasks: Scoring System

**Input**: Design documents from `/specs/007-scoring-system/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Included — constitution mandates automated tests (Domain Unit, Application, Integration, API, Architecture) and spec SC-005/SC-006 require policy coverage in automated tests.

**Organization**: Tasks are grouped by user story (US1–US9 from spec.md). US1–US3 are P1 and form the MVP core.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify baseline and prepare workspace (project already exists from specs 001–006)

- [x] T001 Verify solution builds and existing tests pass: run `dotnet build` and `dotnet test tests/OroQuizClash.Domain.Tests/ tests/OroQuizClash.Architecture.Tests/` from repo root

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain primitives, enumerations, rules, strategies, and persistence extensions that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T002 [P] Extend `PointTransactionType` Enumeration adding GAME_BONUS=5, PENALTY=6, WITHDRAWAL=7, REWARD_REDEMPTION=8, CONSOLATION=9, ADJUSTMENT=10 in `src/OroQuizClash.Domain/Games/Enumerations/PointTransactionType.cs`
- [x] T003 [P] Create `PlayerScore` ValueObject (CurrentPoints, SecuredPoints, RoundPoints, PotentialPoints, TotalPoints) with invariant `CurrentPoints == SecuredPoints + RoundPoints`, non-negative balance, and internal mutation methods (Award, Deduct, Secure, ResetRound, SetPotential) in `src/OroQuizClash.Domain/Games/ValueObjects/PlayerScore.cs`
- [x] T004 [P] Extend `PointTransaction` Entity adding `ResultingBalance` (int) and `Reason` (string?, max 500) properties; update internal constructor per data-model.md in `src/OroQuizClash.Domain/Games/PointTransaction.cs`
- [x] T005 [P] Extend `GamePlayer` Entity adding `Score` (PlayerScore owned), `IsWithdrawn` (bool), `WithdrawnAt` (DateTimeOffset?) with internal `MarkWithdrawn()` method in `src/OroQuizClash.Domain/Games/GamePlayer.cs`
- [x] T006 [P] Add scoring error codes to `GameErrors`: `InsufficientPoints`, `InvalidScoringState`, `AdjustmentReasonRequired`, `PlayerAlreadyWithdrawn`, `InvalidAdjustmentAmount` in `src/OroQuizClash.Domain/Shared/Errors/GameErrors.cs`
- [x] T007 [P] Create `BalanceCannotGoNegativeRule` (deduction ≤ CurrentPoints) in `src/OroQuizClash.Domain/Games/Rules/BalanceCannotGoNegativeRule.cs`
- [x] T008 [P] Create `SufficientBalanceRule` (CurrentPoints ≥ amount for ConsumePoints) in `src/OroQuizClash.Domain/Games/Rules/SufficientBalanceRule.cs`
- [x] T009 [P] Create `AdjustmentReasonRequiredRule` (reason 3–500 chars) in `src/OroQuizClash.Domain/Games/Rules/AdjustmentReasonRequiredRule.cs`
- [x] T010 [P] Create `ScoringStateValidRule` (game status allows scoring operation) in `src/OroQuizClash.Domain/Games/Rules/ScoringStateValidRule.cs`
- [x] T011 [P] Create `PlayerNotWithdrawnRule` (player.IsWithdrawn == false) in `src/OroQuizClash.Domain/Games/Rules/PlayerNotWithdrawnRule.cs`
- [x] T012 [P] Create `ILossPolicyStrategy` interface (CalculateDeduction(PlayerScore) → int) and 4 implementations: `LoseAllStrategy`, `LoseCurrentRoundStrategy`, `LoseUnsecuredPointsStrategy`, `FallbackToCheckpointStrategy` per data-model.md loss policy table in `src/OroQuizClash.Domain/Games/Strategies/ILossPolicyStrategy.cs`
- [x] T013 [P] Create `IWithdrawalPolicyStrategy` interface (CalculateDeduction(PlayerScore) → int) and 4 implementations: `WithdrawLoseAllStrategy`, `WithdrawKeepCurrentStrategy`, `WithdrawKeepSecuredStrategy`, `WithdrawKeepCheckpointStrategy` per data-model.md withdrawal policy table in `src/OroQuizClash.Domain/Games/Strategies/IWithdrawalPolicyStrategy.cs`
- [x] T014 [P] Create `ScoreUpdatedDomainEvent` (GameId, PlayerId, Points, ResultingBalance, Type) in `src/OroQuizClash.Domain/Games/Events/ScoreUpdatedDomainEvent.cs`
- [x] T015 [P] Create `PointsSecuredDomainEvent` (GameId, PlayerId, SecuredAmount, TotalSecured) in `src/OroQuizClash.Domain/Games/Events/PointsSecuredDomainEvent.cs`
- [x] T016 Extend `GamePlayerTypeConfiguration` with `OwnsOne(PlayerScore)` owned entity mapping (columns: CurrentPoints, SecuredPoints, RoundPoints, PotentialPoints, TotalPoints) and IsWithdrawn/WithdrawnAt columns in `src/OroQuizClash.Infrastructure/Persistence/Configurations/GamePlayerTypeConfiguration.cs`
- [x] T017 Extend `PointTransactionTypeConfiguration` (or `GameTypeConfiguration` if PointTransaction is configured there) adding ResultingBalance and Reason (max 500) columns + index on (GameId, PlayerId, CreatedAt) in `src/OroQuizClash.Infrastructure/Persistence/Configurations/`
- [x] T018 Verify EF schema: run `dotnet build` and confirm `EnsureCreated` succeeds with new columns (Sqlite local) via existing Infrastructure test fixture

**Checkpoint**: All domain primitives, strategies, and persistence mappings exist. User story implementation can begin.

---

## Phase 3: User Story 1 - Points Awarded on Correct Answer (Priority: P1) 🎯 MVP

**Goal**: Correct answer → `AwardPoints` domain operation → `ANSWER_CORRECT` transaction + `PlayerScore` update

**Independent Test**: Submit correct answer in active round → verify `PointTransaction(ANSWER_CORRECT)` with correct amount, `ResultingBalance`, and `PlayerScore.CurrentPoints/RoundPoints/TotalPoints` increased

### Tests for User Story 1

- [x] T019 [P] [US1] Domain unit tests for `Game.AwardPoints`: correct transaction created, PlayerScore updated, ResultingBalance correct, events raised in `tests/OroQuizClash.Domain.Tests/Games/ScoringAwardTests.cs`

### Implementation for User Story 1

- [x] T020 [US1] Implement `Game.AwardPoints(Guid playerId, int amount, PointTransactionType type, GameRoundId? roundId, QuestionId? questionId, AnswerId? answerId, string? reason)` — validates state/player, creates PointTransaction with ResultingBalance, updates PlayerScore, raises ScoreUpdatedDomainEvent in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T021 [US1] Refactor `Game.SubmitAnswer` scoring section: replace direct PointTransaction creation with call to `AwardPoints` (correct) — preserve existing Answer creation and events in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T022 [US1] Update `Game.StartRound` to set `PlayerScore.PotentialPoints = PointsPerRound × DifficultyMultiplier` and reset `RoundPoints` for each active player in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T023 [US1] Run US1 tests and verify pass: `dotnet test tests/OroQuizClash.Domain.Tests/ --filter "ScoringAward"`

**Checkpoint**: US1 complete — correct answers award points via ledger with full traceability. Existing SubmitAnswer tests still pass.

---

## Phase 4: User Story 2 - Points Deducted on Incorrect Answer (Priority: P1)

**Goal**: Incorrect answer → `RemovePoints` with configured LossPolicy → `ANSWER_INCORRECT`/`PENALTY` transaction + balance deduction

**Independent Test**: Submit incorrect answer under each of the 4 loss policies → verify correct deduction per data-model.md policy table, transaction created, balance never negative

### Tests for User Story 2

- [x] T024 [P] [US2] Domain unit tests for `Game.RemovePoints` with all 4 LossPolicy variants (LOSE_ALL, LOSE_CURRENT_ROUND, LOSE_UNSECURED_POINTS, FALLBACK_TO_CHECKPOINT), zero-balance edge case, negative prevention in `tests/OroQuizClash.Domain.Tests/Games/ScoringLossPolicyTests.cs`

### Implementation for User Story 2

- [x] T025 [US2] Implement `Game.RemovePoints(Guid playerId, PointTransactionType type, GameRoundId? roundId, QuestionId? questionId, AnswerId? answerId, string? reason)` — resolves ILossPolicyStrategy from Configuration.LossPolicy, calculates deduction, creates negative PointTransaction, updates PlayerScore in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T026 [US2] Refactor `Game.SubmitAnswer` incorrect-answer path: call `RemovePoints` instead of creating zero-point transaction in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T027 [US2] Run US2 tests and verify pass: `dotnet test tests/OroQuizClash.Domain.Tests/ --filter "ScoringLossPolicy"`

**Checkpoint**: US2 complete — all 4 loss policies produce correct deductions with ledger traceability.

---

## Phase 5: User Story 3 - Points Secured at Round/Level Completion (Priority: P1)

**Goal**: Round completion → `SecurePoints` moves RoundPoints → SecuredPoints; ROUND_BONUS and LEVEL_BONUS transactions awarded

**Independent Test**: Complete round with accumulated RoundPoints → verify SecuredPoints increased, RoundPoints reset, secured points survive subsequent loss under LOSE_UNSECURED_POINTS

### Tests for User Story 3

- [x] T028 [P] [US3] Domain unit tests for `Game.SecurePoints`: reclassification correctness, ROUND_BONUS award, LEVEL_BONUS on difficulty increase, secured protection under loss policies in `tests/OroQuizClash.Domain.Tests/Games/ScoringSecureTests.cs`

### Implementation for User Story 3

- [x] T029 [US3] Implement `Game.SecurePoints(Guid playerId)` — moves RoundPoints to SecuredPoints, awards ROUND_BONUS if ScoringSystem configures it, raises PointsSecuredDomainEvent in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T030 [US3] Extend `Game.CompleteRound` to call `SecurePoints` for each active (non-withdrawn) player and detect difficulty level increase → award LEVEL_BONUS via `AwardPoints` in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T031 [US3] Run US3 tests and verify pass: `dotnet test tests/OroQuizClash.Domain.Tests/ --filter "ScoringSecure"`

**Checkpoint**: US1+US2+US3 (all P1) complete — core scoring loop fully functional. MVP ready.

---

## Phase 6: User Story 4 - Points Consumed for Reward Redemption (Priority: P2)

**Goal**: `ConsumePoints` atomic deduction for reward redemption with insufficient-balance rejection

**Independent Test**: Consume with sufficient balance → REWARD_REDEMPTION transaction + deduction; insufficient balance → InsufficientPoints error, no transaction, no partial deduction

### Tests for User Story 4

- [x] T032 [P] [US4] Domain unit tests for `Game.ConsumePoints`: success, insufficient balance (atomic rejection), exact-balance edge case, concurrency semantics in `tests/OroQuizClash.Domain.Tests/Games/ScoringConsumeTests.cs`

### Implementation for User Story 4

- [x] T033 [US4] Implement `Game.ConsumePoints(Guid playerId, int amount, string reason)` — validates SufficientBalanceRule, creates REWARD_REDEMPTION transaction (-amount), deducts from SecuredPoints first, updates PlayerScore in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T034 [US4] Run US4 tests and verify pass: `dotnet test tests/OroQuizClash.Domain.Tests/ --filter "ScoringConsume"`

**Checkpoint**: US4 complete — atomic point consumption ready for SPEC-009 reward integration.

---

## Phase 7: User Story 5 - Player Withdrawal with Policy-Based Scoring (Priority: P2)

**Goal**: `WithdrawPlayer` applies WithdrawalPolicy → WITHDRAWAL transaction → player marked withdrawn, no further scoring

**Independent Test**: Withdraw under each of the 4 withdrawal policies → verify correct retention/deduction per data-model.md, WITHDRAWAL transaction created, subsequent scoring operations rejected

### Tests for User Story 5

- [x] T035 [P] [US5] Domain unit tests for `Game.WithdrawPlayer`: all 4 WithdrawalPolicy variants, terminal-state rejection, double-withdrawal rejection, withdrawn player excluded from scoring in `tests/OroQuizClash.Domain.Tests/Games/ScoringWithdrawalTests.cs`

### Implementation for User Story 5

- [x] T036 [US5] Implement `Game.WithdrawPlayer(Guid playerId)` — validates state (non-terminal, not already withdrawn), resolves IWithdrawalPolicyStrategy, creates WITHDRAWAL transaction, marks player withdrawn in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T037 [US5] Create `WithdrawPlayer` Vertical Slice: Command + Validator + Handler (loads Game, calls `game.WithdrawPlayer()`, saves) + Endpoint (`POST /api/games/{id}/withdraw`, RequireAuthorization) in `src/OroQuizClash.Application/Features/Games/WithdrawPlayer.cs`
- [x] T038 [US5] Run US5 tests and verify pass: `dotnet test tests/OroQuizClash.Domain.Tests/ --filter "ScoringWithdrawal"`

**Checkpoint**: US5 complete — withdrawal scoring with all 4 policies functional.

---

## Phase 8: User Story 6 - Game Completion Bonus (Priority: P2)

**Goal**: Game finish → GAME_BONUS transaction for each non-withdrawn player

**Independent Test**: Finish game → verify GAME_BONUS for active players, no bonus for withdrawn players

### Tests for User Story 6

- [x] T039 [P] [US6] Domain unit tests for GAME_BONUS in `Game.Finish`: awarded to non-withdrawn players, excluded for withdrawn, correct amount in `tests/OroQuizClash.Domain.Tests/Games/ScoringGameBonusTests.cs`

### Implementation for User Story 6

- [x] T040 [US6] Extend `Game.Finish` to award GAME_BONUS via `AwardPoints` for each non-withdrawn player in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T041 [US6] Run US6 tests and verify pass: `dotnet test tests/OroQuizClash.Domain.Tests/ --filter "ScoringGameBonus"`

**Checkpoint**: US6 complete — game completion bonuses awarded consistently.

---

## Phase 9: User Story 7 - Ledger Reconstruction & Audit (Priority: P2)

**Goal**: Query endpoints exposing score breakdown, paginated ledger, and leaderboard; balance verifiable as sum of transactions

**Independent Test**: After multiple scoring operations, `GET /score/{playerId}` returns breakdown matching ledger sum; `GET /ledger` returns paginated transactions; `GET /leaderboard` ranks players

### Tests for User Story 7

- [x] T042 [P] [US7] Domain unit test for ledger reconstruction: sum of transactions == PlayerScore.CurrentPoints after mixed operations in `tests/OroQuizClash.Domain.Tests/Games/ScoringLedgerTests.cs`
- [x] T043 [P] [US7] Application tests for GetPlayerScore/GetScoreLedger/GetLeaderboard handlers with mocked IRepository in `tests/OroQuizClash.Application.Tests/Features/Games/ScoringQueryTests.cs`

### Implementation for User Story 7

- [x] T044 [US7] Rewrite `GetPlayerScore` Vertical Slice: extend ScoreResponse with CurrentPoints, SecuredPoints, RoundPoints, PotentialPoints, TotalPoints, IsWithdrawn per contracts/scoring-query.openapi.yaml in `src/OroQuizClash.Application/Features/Games/GetPlayerScore.cs`
- [x] T045 [US7] Create `GetScoreLedger` Vertical Slice: Query (GameId, PlayerId, Page, PageSize, Type?) + Handler (paginated, ordered CreatedAt desc) + Response (LedgerPage per contract) + Endpoint (`GET /api/games/{id}/score/{playerId}/ledger`) in `src/OroQuizClash.Application/Features/Games/GetScoreLedger.cs`
- [x] T046 [US7] Create `GetLeaderboard` Vertical Slice: Query (GameId) + Handler (players ranked by CurrentPoints desc) + Response (Leaderboard per contract) + Endpoint (`GET /api/games/{id}/leaderboard`) in `src/OroQuizClash.Application/Features/Games/GetLeaderboard.cs`
- [x] T047 [US7] Run US7 tests and verify pass: `dotnet test tests/OroQuizClash.Application.Tests/ --filter "ScoringQuery"`

**Checkpoint**: US7 complete — full scoring observability via query endpoints.

---

## Phase 10: User Story 8 - Administrative Point Adjustment (Priority: P3)

**Goal**: Admin applies ADJUSTMENT transaction with mandatory reason; requires AdminOrGameManager policy

**Independent Test**: Admin adjustment with reason → ADJUSTMENT transaction + balance update; empty reason → 400; non-admin → 403

### Tests for User Story 8

- [x] T048 [P] [US8] Domain unit tests for `Game.AdjustPoints`: positive/negative adjustment, reason validation, zero-amount rejection in `tests/OroQuizClash.Domain.Tests/Games/ScoringAdjustmentTests.cs`
- [x] T049 [P] [US8] Application test for AdjustScore handler (success, reason validation, error mapping) in `tests/OroQuizClash.Application.Tests/Features/Games/AdjustScoreTests.cs`

### Implementation for User Story 8

- [x] T050 [US8] Implement `Game.AdjustPoints(Guid playerId, int amount, string reason, Guid adminUserId)` — validates AdjustmentReasonRequiredRule + non-zero amount, creates ADJUSTMENT transaction, updates PlayerScore in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T051 [US8] Create `AdjustScore` Vertical Slice: Command (GameId, PlayerId, Points, Reason) + Validator (reason 3–500, points ≠ 0) + Handler + Response per contracts/scoring-adjust.openapi.yaml + Endpoint (`POST /api/games/{id}/score/{playerId}/adjust`, RequireAuthorization("AdminOrGameManager")) in `src/OroQuizClash.Application/Features/Games/AdjustScore.cs`
- [x] T052 [US8] Run US8 tests and verify pass: `dotnet test tests/OroQuizClash.Domain.Tests/ --filter "ScoringAdjustment"`

**Checkpoint**: US8 complete — auditable administrative adjustments with authorization.

---

## Phase 11: User Story 9 - Consolation Points (Priority: P3)

**Goal**: Game finish → CONSOLATION transaction for eligible non-winning players per ConsolationPolicy

**Independent Test**: Finish game with ConsolationPolicy.FixedPoints → eligible players (min rounds completed, not withdrawn) receive CONSOLATION transaction; ineligible receive nothing

### Tests for User Story 9

- [x] T053 [P] [US9] Domain unit tests for consolation: eligibility (min rounds, not withdrawn), ConsolationPolicy.None awards nothing, FixedPoints awards configured amount in `tests/OroQuizClash.Domain.Tests/Games/ScoringConsolationTests.cs`

### Implementation for User Story 9

- [x] T054 [US9] Extend `Game.Finish` to evaluate ConsolationPolicy eligibility and award CONSOLATION points via `AwardPoints` for eligible non-winning players in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T055 [US9] Run US9 tests and verify pass: `dotnet test tests/OroQuizClash.Domain.Tests/ --filter "ScoringConsolation"`

**Checkpoint**: All 9 user stories complete.

---

## Phase 12: Polish & Cross-Cutting Concerns

**Purpose**: Architecture tests, observability, full validation

- [x] T056 [P] Create architecture tests verifying scoring types (PlayerScore, strategies, PointTransaction) respect dependency rules (Domain → no Infra/Web refs) in `tests/OroQuizClash.Architecture.Tests/ScoringDependenciesTests.cs`
- [x] T057 Add OTel structured logging for scoring operations: CorrelationId, GameId, PlayerId, Points, Type, ResultingBalance in handlers (AdjustScore, WithdrawPlayer)
- [x] T058 Run full test suite and verify zero regressions: `dotnet test tests/OroQuizClash.Domain.Tests/ tests/OroQuizClash.Application.Tests/ tests/OroQuizClash.Architecture.Tests/`
- [x] T059 Run quickstart.md validation scenarios 1–9 via domain/application tests
- [x] T060 Verify `dotnet build` from repo root with 0 errors, 0 warnings

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — baseline verification
- **Phase 2 (Foundational)**: Depends on Phase 1 — BLOCKS all user stories
- **Phase 3 (US1)**: Depends on Phase 2 — 🎯 MVP
- **Phase 4 (US2)**: Depends on Phase 2 + US1 (refactors SubmitAnswer scoring path)
- **Phase 5 (US3)**: Depends on US1 (uses AwardPoints for bonuses)
- **Phase 6 (US4)**: Depends on Phase 2 only (independent of US1–US3)
- **Phase 7 (US5)**: Depends on Phase 2 only (independent of US1–US4)
- **Phase 8 (US6)**: Depends on US1 (uses AwardPoints)
- **Phase 9 (US7)**: Depends on US1 (needs PlayerScore + transactions to query)
- **Phase 10 (US8)**: Depends on Phase 2 only (independent)
- **Phase 11 (US9)**: Depends on US1 (uses AwardPoints) + US6 (extends Finish)
- **Phase 12 (Polish)**: Depends on all user stories complete

### User Story Dependencies

```text
Phase 2 (Foundational)
├── US1 (P1) ──┬── US2 (P1) [refactors SubmitAnswer]
│              ├── US3 (P1) [uses AwardPoints]
│              ├── US6 (P2) [uses AwardPoints]
│              ├── US7 (P2) [queries PlayerScore]
│              └── US9 (P3) [uses AwardPoints, extends Finish]
├── US4 (P2) [independent]
├── US5 (P2) [independent]
└── US8 (P3) [independent]
```

### Parallel Opportunities

- All Phase 2 tasks T002–T015 marked [P] (different files)
- US4, US5, US8 are fully independent of US1–US3 and can run in parallel after Phase 2
- Tests within each story marked [P] can be written in parallel with other stories' implementations

---

## Parallel Example: After Phase 2

```bash
# These can run in parallel (independent stories):
Task: "US1 — AwardPoints + SubmitAnswer refactor" (Phase 3)
Task: "US4 — ConsumePoints" (Phase 6)
Task: "US5 — WithdrawPlayer" (Phase 7)
Task: "US8 — AdjustScore" (Phase 10)

# Then sequentially (US2/US3 depend on US1):
Task: "US2 — RemovePoints + loss policies" (Phase 4)
Task: "US3 — SecurePoints + bonuses" (Phase 5)
```

---

## Implementation Strategy

### MVP First (US1 + US2 + US3 — all P1)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: Foundational (T002–T018) — CRITICAL, blocks all
3. Complete Phase 3: US1 AwardPoints (T019–T023)
4. Complete Phase 4: US2 RemovePoints (T024–T027)
5. Complete Phase 5: US3 SecurePoints (T028–T031)
6. **STOP and VALIDATE**: Core scoring loop works end-to-end

### Incremental Delivery

1. MVP (US1–US3) → core scoring loop
2. Add US4 (ConsumePoints) → reward redemption ready
3. Add US5 (Withdrawal) → player exit mechanics
4. Add US6 (Game Bonus) → completion rewards
5. Add US7 (Ledger queries) → observability
6. Add US8 (Adjustments) → operational support
7. Add US9 (Consolation) → retention mechanic
8. Polish → architecture tests, logging, full validation

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- US2 refactors SubmitAnswer (created in SPEC-006) — existing tests MUST continue passing
- `Game.cs` is modified by multiple stories — tasks touching it are sequential within a story
- Commit after each phase checkpoint
