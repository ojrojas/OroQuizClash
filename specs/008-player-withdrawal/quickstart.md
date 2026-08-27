# Quickstart: Player Withdrawal Validation

**Feature**: 008-player-withdrawal
**Date**: 2026-08-27

This guide documents runnable validation scenarios proving the withdrawal feature works end-to-end. Implementation details live in `tasks.md`; contracts in `contracts/`; data model in `data-model.md`.

## Prerequisites

- .NET 10 SDK
- Access to the repo root `/home/oroja/Sources/OroQuizClash`
- No external DB required for domain/application unit tests (in-memory)

## Build & Test Commands

```bash
# Restore + build entire solution
dotnet build

# Run withdrawal domain tests
dotnet test tests/OroQuizClash.Domain.Tests/ --filter "FullyQualifiedName~Withdrawal"

# Run participation status tests
dotnet test tests/OroQuizClash.Domain.Tests/ --filter "FullyQualifiedName~Participation"

# Run application tests
dotnet test tests/OroQuizClash.Application.Tests/ --filter "FullyQualifiedName~Withdraw"

# Run architecture tests
dotnet test tests/OroQuizClash.Architecture.Tests/

# Run full suite
dotnet test
```

## Validation Scenarios

### Scenario 1: Voluntary Withdrawal Preserves Eligible Points (P1)

**Goal**: Verify withdrawal applies the configured policy and retains eligible points.

1. Create a game with `WithdrawalPolicy = KEEP_SECURED_SCORE`, join 2 players, start.
2. Award player 300 secured + 200 unsecured points.
3. Withdraw the player.
4. **Assert**: Final score = 300; WITHDRAWAL transaction of -200 exists; status = WITHDRAWN; `ExitedAt` set.

### Scenario 2: Double Withdrawal Rejected (P1)

**Goal**: Verify a player cannot withdraw twice.

1. Withdraw a player successfully.
2. Attempt to withdraw again.
3. **Assert**: Second attempt fails with `PlayerAlreadyWithdrawn`; no second transaction; state unchanged.

### Scenario 3: Withdrawal After Game End Rejected (P1)

**Goal**: Verify withdrawal is blocked in terminal game states.

1. Play a game to FINISHED state.
2. Attempt to withdraw a player.
3. **Assert**: Fails with `InvalidGameState`; player status unchanged.

### Scenario 4: Withdrawal After Elimination Rejected (P1)

**Goal**: Verify eliminated players cannot withdraw.

1. Eliminate a player via `EliminatePlayer`.
2. Attempt to withdraw that player.
3. **Assert**: Fails with `PlayerAlreadyEliminated`.

### Scenario 5: Participation Status Lifecycle (P2)

**Goal**: Verify status transitions ACTIVE → WITHDRAWN/ELIMINATED/WINNER.

1. Join a player → **Assert**: status = ACTIVE.
2. Withdraw → **Assert**: status = WITHDRAWN (terminal).
3. In another game, eliminate a player → **Assert**: status = ELIMINATED (terminal).
4. Finish a game with a top scorer → **Assert**: top scorer status = WINNER.

### Scenario 6: Winner Determination at Finish (P2)

**Goal**: Verify highest-scoring active player becomes WINNER.

1. 2-player game; player A scores 500, player B scores 300.
2. Finish the game.
3. **Assert**: Player A status = WINNER; player B remains ACTIVE (or their final non-winner status).
4. **Assert**: Withdrawn/eliminated players are never marked WINNER.

### Scenario 7: Withdrawn Player Excluded from Scoring (P2)

**Goal**: Verify withdrawn players receive no further scoring events.

1. Withdraw a player.
2. Complete rounds, finish game.
3. **Assert**: No round bonus, game bonus, or consolation transactions for the withdrawn player.

### Scenario 8: Game Continues for Remaining Players (P2)

**Goal**: Verify withdrawal does not interrupt remaining players.

1. 3-player game; withdraw one player.
2. Remaining players continue answering and scoring.
3. **Assert**: Remaining players' scoring works normally; game completes.

### Scenario 9: Concurrent Withdrawal + Finish (P2)

**Goal**: Verify optimistic concurrency prevents inconsistent states.

1. Fire a withdrawal and a game-finish concurrently.
2. **Assert**: Exactly one succeeds; the other gets a concurrency conflict (409) or appropriate rejection.
3. **Assert**: Final player state is consistent (either WITHDRAWN or game-FINISHED, never a corrupted mix).

### Scenario 10: Mid-Round Withdrawal (P2)

**Goal**: Verify withdrawal is allowed during an active round.

1. Start a round (question active).
2. Withdraw a player mid-round.
3. **Assert**: Withdrawal succeeds; no answer recorded for the withdrawn player; round continues for others.

## API Validation (manual / integration)

Reference `contracts/withdrawal.openapi.yaml`.

```bash
# Withdraw current player (identity from JWT sub claim)
POST /api/games/{gameId}/withdraw

# Get player participation status
GET /api/games/{gameId}/players/{playerId}/status
```

## Expected Outcomes Summary

| Scenario | Maps to Spec | Key Assertion |
|----------|-------------|---------------|
| 1 | US-1, FR-002/003 | Policy applied, points retained |
| 2 | US-2, FR-006 | No double withdrawal |
| 3 | US-2, FR-008 | No withdrawal after game end |
| 4 | US-2, FR-007 | No withdrawal after elimination |
| 5 | US-3, FR-011/012 | Status lifecycle correct |
| 6 | US-3, FR-012 | Winner determined correctly |
| 7 | US-4, FR-013 | Withdrawn excluded from scoring |
| 8 | US-4, FR-014 | Game continues for others |
| 9 | FR-016, SC-007 | No race-condition inconsistency |
| 10 | Edge case | Mid-round withdrawal allowed |
