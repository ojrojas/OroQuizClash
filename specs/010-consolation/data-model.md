# Phase 1 Data Model: Consolation

**Date**: 2026-08-27 | **Plan**: [plan.md](plan.md) | **Research**: [research.md](research.md)

No new aggregates. Extends existing entities: `ConsolationPolicy` (Enumeration), `GameConfiguration` (ValueObject), `Game` (AggregateRoot behavior). Adds one rule and two query slices.

---

## Extended: ConsolationPolicy (Enumeration)

| Value | Id | Name | Behavior |
|-------|----|------|----------|
| None | 1 | `None` | No consolation awarded |
| FixedPoints | 2 | `FixedPoints` | Award `ConsolationPoints` to eligible non-winners |
| RewardBased | 3 | `RewardBased` | Grant `ConsolationRewardId` as APPROVED redemption |
| ParticipationBased | 4 | `ParticipationBased` | Award scaled points: `ConsolationPoints × (playerRounds / totalRounds)` |

- Renamed `Badge(3)` → `RewardBased(3)` (Badge was never used/tested)
- `None` is the default for backward compatibility

### State transitions: N/A (Enumeration, no lifecycle)

---

## Extended: GameConfiguration (ValueObject)

New properties added to existing constructor (with defaults for backward compatibility):

| Property | Type | Default | Validation | Notes |
|----------|------|---------|------------|-------|
| MinimumParticipationRounds | int | 0 | ≥ 0 | Min rounds player must be present in |
| MinimumAnsweredQuestions | int | 0 | ≥ 0 | Min questions player must have answered |
| ConsolationPoints | int | 0 | > 0 if policy is FixedPoints/ParticipationBased | Point amount for fixed/scaled consolation |
| ConsolationRewardId | RewardId? | null | Required if policy is RewardBased | Reward catalog reference |

- Constructor extended with optional parameters (defaults preserve existing callers)
- `GetEqualityComponents` extended with new fields
- EF owned entity configuration auto-discovers new properties (no Infrastructure changes)

### Validation rules (applied at game creation)

- If `ConsolationPolicy ∈ {FixedPoints, ParticipationBased}` → `ConsolationPoints > 0`
- If `ConsolationPolicy == RewardBased` → `ConsolationRewardId` must reference an existing active reward (application-level check)
- `MinimumParticipationRounds ≥ 0`, `MinimumAnsweredQuestions ≥ 0`

---

## New: ConsolationEligibilityRule (IBusinessRule)

Evaluates per-player at `Game.Finish()` time.

| Parameter | Source |
|-----------|--------|
| isActive | `GamePlayer.IsActive` |
| isWinner | Determined before consolation evaluation |
| playerParticipationRounds | Count of completed rounds where player was active |
| playerAnsweredQuestions | Count of ANSWER_CORRECT + ANSWER_INCORRECT transactions |
| minimumParticipationRounds | `GameConfiguration.MinimumParticipationRounds` |
| minimumAnsweredQuestions | `GameConfiguration.MinimumAnsweredQuestions` |
| policy | `GameConfiguration.ConsolationPolicy` |

**IsBroken** returns true (NOT eligible) when:
- `policy == None`, OR
- `!isActive` (eliminated), OR
- `isWinner`, OR
- `playerParticipationRounds < minimumParticipationRounds`, OR
- `playerAnsweredQuestions < minimumAnsweredQuestions`

---

## Extended: Game.Finish() — refactored flow

Current (buggy): game bonus → consolation → winner determination
New (correct): game bonus → winner determination → consolation evaluation → consolation award

```text
1. Award game bonus to all active players
2. Determine winners (max post-bonus score, ties all win)
3. For each non-winner active player:
   a. Evaluate ConsolationEligibilityRule
   b. If eligible:
      - FixedPoints: create CONSOLATION transaction (+ConsolationPoints)
      - ParticipationBased: create CONSOLATION transaction (+scaled points)
      - RewardBased: create APPROVED RewardRedemption for ConsolationRewardId
4. Mark winners (ParticipationStatus = WINNER)
5. Set status = FINISHED, FinishedAt = UTC now
6. Raise GameFinishedDomainEvent
```

---

## Extended: RewardRedemption — consolation factory

New static factory method on `RewardRedemption`:

```text
RewardRedemption.CreateAsConsolation(playerId, rewardId, gameId)
```

- Status: APPROVED (bypasses manual approval)
- Points: 0 (no point deduction — reward is system-granted)
- Initial transition: (APPROVED, system actor)
- Stock: NOT decremented (consolation rewards are granted, not purchased)
- `RewardRedeemedDomainEvent` raised

---

## Extended: GameErrors

| Error | Code | HTTP | Trigger |
|-------|------|------|---------|
| InvalidConsolationConfiguration | `Game.InvalidConsolationConfiguration` | 400 | ConsolationPoints ≤ 0 when policy requires it; ConsolationRewardId null when policy is RewardBased |
| ConsolidationRewardNotFound | `Game.ConsolidationRewardNotFound` | 400 | RewardBased policy with non-existent reward id (application-level) |

---

## Relationships

```text
Game 1 ──── 1 GameConfiguration (owned VO, includes ConsolationPolicy + eligibility fields)
Game 1 ──── * PointTransaction (CONSOLATION type for point-based consolation)
Game 1 ──── * RewardRedemption (APPROVED, for reward-based consolation, created in Finish())
Game 1 ──── * GamePlayer ( IsActive for participation, Score for eligibility)
```

## Persistence (EF configurations)

- `GameConfiguration`: no schema change — new properties auto-discovered by EF owned entity convention
- `ConsolationPolicy`: no schema change — persisted as int via existing Enumeration conversion
- No new tables, no migrations needed

## Specifications (Infrastructure)

No new specifications needed — consolation queries read from the existing `PointTransactions` collection on `Game` (loaded via `GameByIdWithAnswersSpecification`). The `GetPlayerConsolationStatus` handler filters `game.PointTransactions.Where(pt => pt.Type == PointTransactionType.Consolation && pt.PlayerId == playerId)`.
