# Quickstart: Scoring System Validation

**Feature**: 007-scoring-system
**Date**: 2026-08-27

This guide documents runnable validation scenarios proving the scoring system works end-to-end. Implementation details live in `tasks.md`; contracts in `contracts/`; data model in `data-model.md`.

## Prerequisites

- .NET 10 SDK
- Access to the repo root `/home/oroja/Sources/OroQuizClash`
- No external DB required for domain/application unit tests (in-memory)
- SQL Server (or Sqlite fallback) for integration tests

## Build & Test Commands

```bash
# Restore + build entire solution
dotnet build

# Run domain unit tests (scoring operations, policies, ledger invariants)
dotnet test tests/OroQuizClash.Domain.Tests/ --filter "FullyQualifiedName~Scoring"

# Run application tests (handlers)
dotnet test tests/OroQuizClash.Application.Tests/ --filter "FullyQualifiedName~Score"

# Run architecture tests (dependency rules)
dotnet test tests/OroQuizClash.Architecture.Tests/

# Run full suite
dotnet test
```

## Validation Scenarios

### Scenario 1: Correct Answer Awards Points (P1)

**Goal**: Verify `AwardPoints` creates an `ANSWER_CORRECT` transaction and updates `PlayerScore`.

1. Create a game, join a player, start game, start a round.
2. Submit a correct answer.
3. **Assert**: A `PointTransaction` of type `ANSWER_CORRECT` exists with `Points = PointsPerRound × DifficultyMultiplier`.
4. **Assert**: `PlayerScore.CurrentPoints`, `RoundPoints`, and `TotalPoints` increased by that amount.
5. **Assert**: `PointTransaction.ResultingBalance` equals the new `CurrentPoints`.

### Scenario 2: Incorrect Answer Applies Loss Policy (P1)

**Goal**: Verify `RemovePoints` applies the configured `LossPolicy`.

1. Create a game with `LossPolicy = LOSE_CURRENT_ROUND`, award some round points.
2. Submit an incorrect answer.
3. **Assert**: `ANSWER_INCORRECT` transaction created with negative points.
4. **Assert**: `RoundPoints` reset to 0; `SecuredPoints` unchanged.
5. Repeat for each of the 4 loss policies and verify the deduction table in `data-model.md`.

### Scenario 3: Secure Points on Round Completion (P1)

**Goal**: Verify `SecurePoints` moves `RoundPoints` → `SecuredPoints`.

1. Accumulate round points, then complete the round.
2. **Assert**: `RoundPoints == 0`, `SecuredPoints` increased by prior `RoundPoints`.
3. **Assert**: `CurrentPoints` unchanged (reclassification, not a net change).
4. **Assert**: Secured points survive a subsequent incorrect answer under `LOSE_UNSECURED_POINTS`.

### Scenario 4: Consume Points Atomicity (P2)

**Goal**: Verify `ConsumePoints` is atomic and rejects insufficient balance.

1. Player has 200 points; attempt to consume 300.
2. **Assert**: Operation fails with `InsufficientPoints`; no transaction created; balance unchanged.
3. Player has 500 points; consume 300.
4. **Assert**: `REWARD_REDEMPTION` transaction of -300; balance = 200.

### Scenario 5: Withdrawal Applies Policy (P2)

**Goal**: Verify `WithdrawPlayer` applies `WithdrawalPolicy`.

1. Player has 300 secured + 200 unsecured; policy = `KEEP_SECURED_SCORE`.
2. Withdraw the player.
3. **Assert**: `WITHDRAWAL` transaction of -200; final score = 300; player marked withdrawn.
4. **Assert**: Withdrawn player cannot receive further scoring operations.

### Scenario 6: Ledger Reconstruction (P2)

**Goal**: Verify balance reconstructable from ledger.

1. Perform a sequence of award/deduct/secure/consume operations.
2. **Assert**: `Sum(all PointTransaction.Points for player) == PlayerScore.CurrentPoints`.
3. **Assert**: Each transaction's `ResultingBalance` matches the running sum.

### Scenario 7: Administrative Adjustment (P3)

**Goal**: Verify `AdjustScore` requires reason and elevated permissions.

1. As admin, apply +100 adjustment with valid reason.
2. **Assert**: `ADJUSTMENT` transaction created; balance +100; reason stored.
3. Attempt adjustment with empty reason → **Assert**: rejected (400).
4. Attempt adjustment as non-admin → **Assert**: rejected (403).

### Scenario 8: Concurrency — No Double Deduction (P2)

**Goal**: Verify optimistic concurrency prevents race conditions.

1. Player has 300 points; fire two concurrent `ConsumePoints(200)`.
2. **Assert**: Exactly one succeeds; the other gets concurrency conflict (409) or insufficient balance.
3. **Assert**: Final balance is consistent (100, not -100 or 300).

### Scenario 9: Idempotency — No Duplicate Scoring (P1)

**Goal**: Verify duplicate answer submission does not double-award.

1. Submit the same answer twice for the same player+round.
2. **Assert**: Only one `PointTransaction` exists; balance reflects a single award.

## API Validation (manual / integration)

Reference `contracts/scoring-query.openapi.yaml` and `contracts/scoring-adjust.openapi.yaml`.

```bash
# Get score breakdown
GET /api/games/{gameId}/score/{playerId}

# Get ledger (paginated)
GET /api/games/{gameId}/score/{playerId}/ledger?page=1&pageSize=50

# Get leaderboard
GET /api/games/{gameId}/leaderboard

# Admin adjustment (requires AdminOrGameManager JWT)
POST /api/games/{gameId}/score/{playerId}/adjust
{ "points": 100, "reason": "System error correction" }
```

## Expected Outcomes Summary

| Scenario | Maps to Spec | Key Assertion |
|----------|-------------|---------------|
| 1 | US-1, FR-001/003 | Award → transaction + balance |
| 2 | US-2, FR-006 | Loss policy applied correctly |
| 3 | US-3, FR-008 | Secure reclassifies, protects |
| 4 | US-4, FR-009 | Atomic consume, insufficient fails |
| 5 | US-5, FR-007 | Withdrawal policy applied |
| 6 | US-7, FR-005 | Ledger sum == balance |
| 7 | US-8, FR-014 | Adjustment requires reason + auth |
| 8 | FR-013, SC-003 | No race-condition inconsistency |
| 9 | FR-012, SC-004 | No duplicate point allocation |
