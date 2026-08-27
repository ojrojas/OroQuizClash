# Quickstart: Consolation Validation

**Feature**: 010-consolation
**Date**: 2026-08-27

This guide documents runnable validation scenarios proving the consolation feature works end-to-end.

## Prerequisites

- .NET 10 SDK
- Access to the repo root `/home/oroja/Sources/OroQuizClash`
- No external DB required for domain/application unit tests (in-memory)

## Build & Test Commands

```bash
# Restore + build
dotnet build

# Run consolation domain tests
dotnet test tests/OroQuizClash.Domain.Tests/ --filter "Consolation"

# Run consolidation query application tests
dotnet test tests/OroQuizClash.Application.Tests/ --filter "Consolation"

# Run architecture tests
dotnet test tests/OroQuizClash.Architecture.Tests/

# Full regression
dotnet test tests/OroQuizClash.Domain.Tests/ && \
dotnet test tests/OroQuizClash.Application.Tests/ && \
dotnet test tests/OroQuizClash.Architecture.Tests/
```

## Validation Scenarios

### Scenario 1: FixedPoints Consolation for Eligible Non-Winner (P1)

**Goal**: Verify eligible non-winner receives fixed-point consolation.

1. Create game with FixedPoints policy, ConsolationPoints = 100, MinRounds = 2, MinAnswered = 1.
2. Join 2 players; play 3 rounds; one player answers all correctly (winner), one answers partially.
3. Finish game.
4. **Assert**: Winner has no CONSOLATION transaction. Loser has CONSOLATION +100. Both have GAME_BONUS transactions.

### Scenario 2: Ineligible Player Below Thresholds (P1)

1. Create game with MinRounds = 3, MinAnswered = 2.
2. Player joins but only plays 1 round and answers 1 question.
3. Finish game.
4. **Assert**: No CONSOLATION transaction for that player.

### Scenario 3: Winner Does Not Receive Consolation (P1)

1. Create game with FixedPoints policy.
2. One player wins clearly.
3. **Assert**: Winner has 0 CONSOLATION transactions.

### Scenario 4: Eliminated Player Excluded (P1)

1. Create game with FixedPoints policy.
2. Player is eliminated mid-game.
3. Finish game.
4. **Assert**: Eliminated player has no CONSOLATION transaction.

### Scenario 5: Withdrawn Player Still Eligible (P2)

1. Create game with FixedPoints policy, MinRounds = 2.
2. Player participates in 3 rounds, then withdraws.
3. Finish game.
4. **Assert**: Withdrawn player receives CONSOLATION (meets minimum thresholds).

### Scenario 6: ParticipationBased Scaled Points (P2)

1. Create game with ParticipationBased policy, ConsolationPoints = 100, total 5 rounds.
2. Player participates in 3 of 5 rounds.
3. Finish game.
4. **Assert**: CONSOLATION transaction = 100 × (3/5) = 60 points.

### Scenario 7: RewardBased Consolation (P2)

1. Create game with RewardBased policy, ConsolationRewardId = reward "Participation Badge".
2. Eligible non-winning player finishes.
3. **Assert**: APPROVED RewardRedemption created for that player; reward stock unchanged.

### Scenario 8: No Double Consolidation (P1)

1. Finish a game with FixedPoints policy.
2. Attempt to re-finish (or duplicate the finish event).
3. **Assert**: Exactly 1 CONSOLATION transaction per eligible player.

### Scenario 9: None Policy (P1)

1. Create game with None policy.
2. Finish game.
3. **Assert**: 0 CONSOLATION transactions for all players.

### Scenario 10: Configuration Validation (P3)

1. Create game with FixedPoints policy and ConsolationPoints = 0.
2. **Assert**: Game creation rejected (InvalidConsolationConfiguration).
3. Create game with RewardBased policy and null ConsolationRewardId.
4. **Assert**: Game creation rejected.

## Expected Test Coverage Mapping

| Spec requirement | Test location |
|------------------|---------------|
| FR-001 eligible players receive consolation | Domain `ScoringConsolationTests` |
| FR-003 eligibility rules (rounds, questions, winner) | Domain `ConsolationEligibilityRuleTests` |
| FR-004 CONSOLATION ledger entry | Domain `ScoringConsolationTests` |
| FR-005 no double consolidation | Domain `ScoringConsolationTests` |
| FR-008 reward-based consolation | Domain `ConsolationRewardTests` |
| FR-009 configuration fields | Domain `GameConfigurationTests` |
| FR-014 player status query | Application `ConsolationStatusHandlerTests` |
| FR-015 player history query | Application `ConsolationHistoryHandlerTests` |
| SC-002 0% ineligible awarded | Domain eligibility rule tests |
| SC-004 no duplicates | Domain `ScoringConsolationTests` |
