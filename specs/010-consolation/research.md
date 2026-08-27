# Phase 0 Research: Consolation

**Date**: 2026-08-27 | **Plan**: [plan.md](plan.md)

All unknowns resolved. No NEEDS CLARIFICATION remains.

---

## R1. ConsolationPolicy enumeration extension

**Decision**: Extend existing `ConsolationPolicy` with two new values: `ParticipationBased(4, "ParticipationBased")` and `RewardBased(5, "RewardBased")`. Rename `Badge(3)` to `RewardBased` since Badge was never used in the codebase (zero references outside the enum definition). Final set: None(1), FixedPoints(2), RewardBased(3), ParticipationBased(4). The old Badge(3) value is replaced by RewardBased(3) — since Badge was never persisted or tested, no migration concern.

**Rationale**: ParticipationBased introduces scaled points based on participation level. RewardBased grants a catalog reward (SPEC-009). Renaming Badge→RewardBased makes the name self-documenting and consistent with the spec terminology. None and FixedPoints are kept for backward compatibility with existing tests and game configurations.

**Alternatives considered**:
- *Keep Badge, add RewardBased as separate value*: rejected — Badge was unused and confusing; two reward-type policies is redundant.
- *Use string-based policy instead of Enumeration*: rejected — constitution mandates Enumeration for configurable rules; existing pattern uses `Enumeration<T>`.

---

## R2. GameConfiguration extension

**Decision**: Add four properties to `GameConfiguration`:
- `MinimumParticipationRounds` (int, default 0) — minimum rounds a player must have been present in
- `MinimumAnsweredQuestions` (int, default 0) — minimum questions a player must have answered
- `ConsolationPoints` (int, default 0) — point amount for FixedPoints/ParticipationBased policies
- `ConsolationRewardId` (RewardId?, default null) — reward reference for RewardBased policy

**Rationale**: GameConfiguration is a ValueObject (immutable after game start). New fields are added to the constructor with defaults, and `GetEqualityComponents` is extended. Existing callers that don't pass these parameters get sensible defaults (0 = no minimum, no reward). The EF owned-entity configuration auto-discovers new properties.

**Alternatives considered**:
- *Separate ConsolationConfiguration ValueObject*: rejected — adds indirection without benefit; the fields are tightly coupled to the game configuration.
- *Global defaults + per-game override*: rejected — constitution says game configuration is immutable after start; per-game configuration at creation is sufficient.

---

## R3. Eligibility rules

**Decision**: New `ConsolationEligibilityRule` (IBusinessRule) that checks:
1. Player is active (not eliminated)
2. Player is not a winner
3. Player has participated in at least `MinimumParticipationRounds` rounds
4. Player has answered at least `MinimumAnsweredQuestions` questions
5. ConsolationPolicy is not None

The rule receives all parameters via constructor and evaluates at `Game.Finish()` time using the game's actual round history and player state.

**Rationale**: Centralizes eligibility logic in a single testable rule (constitution: domain rules as IBusinessRule). The rule is evaluated per-player within `Finish()`, keeping the orchestration in the domain behavior.

**Alternatives considered**:
- *Eligibility as a domain service*: rejected — IBusinessRule is the established pattern for business rules in this codebase.
- *Eligibility evaluated during gameplay*: rejected — spec says evaluation at game finish; premature optimization.

---

## R4. Winner determination order (bug fix)

**Decision**: Refactor `Game.Finish()` to determine winners BEFORE awarding consolation. Current code awards game bonus + consolation BEFORE marking winners, which inflates winner scores. New order:
1. Calculate game bonus for all active players
2. Determine winners based on post-bonus scores
3. Evaluate consolation eligibility for non-winners
4. Award consolation (points or reward)
5. Mark winners
6. Set game status to Finished

**Rationale**: The current code has a subtle bug: consolation points are added before `finalMaxScore` is recalculated for winner determination, meaning a consolation recipient could accidentally become a winner. The spec explicitly says "winners do not receive consolation." Correcting the order fixes this.

**Alternatives considered**:
- *Keep current order, add "not yet won" check to consolation eligibility*: rejected — fragile; score inflation from game bonus already complicates winner detection.

---

## R5. Reward-based consolation mechanism

**Decision**: For `RewardBased` policy, `Game.Finish()` creates a `RewardRedemption.Create(playerId, rewardId, gameId, 0)` in APPROVED status directly. Points = 0 since the reward itself is the consolation (not a point deduction). The `RewardRedemption` is added to the game's outbox/domain events for persistence. The handler in the application layer does NOT need to load the Reward aggregate — the reward reference is stored on the `GameConfiguration.ConsolationRewardId` and validated at game creation time.

Wait — actually, `RewardRedemption.Create` requires a `points` parameter and the existing flow deducts points via `Game.ConsumePoints`. For consolation, we're not deducting points — we're granting a reward. Options:
1. Create `RewardRedemption` directly with status APPROVED, points = 0, no game ledger deduction.
2. Use a special "consolation redemption" path that skips point deduction.

Option 1 is cleaner: create the `RewardRedemption` as APPROVED with the game as reference, no point transaction. The redemption is the audit trail. Stock is NOT decremented for consolation rewards (they're granted, not purchased). This requires adding a `CreateWithoutPoints` factory method or making the existing `Create` accept 0 points.

**Rationale**: Consolation rewards are system-granted, not player-purchased. The existing `RewardRedemption.Create` validates `points > 0`. We need a separate path. Adding a static `CreateAsConsolation(playerId, rewardId, gameId)` factory method on `RewardRedemption` that sets status=APPROVED, points=0, no idempotency key, and records the initial transition is the cleanest approach.

**Alternatives considered**:
- *Force points > 0 with dummy value*: rejected — pollutes the ledger with fake transactions.
- *Skip RewardRedemption, just log*: rejected — spec requires auditability via the reward system.

---

## R6. Participation counting

**Decision**: "Participation in a round" = the player was a `GamePlayer` with `IsActive == true` when the round was completed. The number of completed rounds where the player was active = their participation count. "Answered questions" = count of `PlayerAnswer` entries (or `PointTransaction` of type `ANSWER_CORRECT`/`ANSWER_INCORRECT`) for that player. Both are computed from the existing game state in `Finish()`.

**Rationale**: Uses existing data without new tracking. `GamePlayer.IsActive` already tracks participation status (SPEC-008). Answer count is derivable from `PointTransactions` or the round's answer records.

**Alternatives considered**:
- *New ParticipationTracking entity*: rejected — over-engineering; existing data suffices.
- *Store participation count on GamePlayer*: rejected — redundant; derivable.

---

## R7. Consolation configuration validation

**Decision**: At game creation time, validate:
- If `ConsolationPolicy` is `FixedPoints` or `ParticipationBased`: `ConsolationPoints` must be > 0
- If `ConsolationPolicy` is `RewardBased`: `ConsolationRewardId` must reference an existing active reward (checked via repository in the application handler, not domain)
- If `ConsolationPolicy` is `None`: no consolation fields needed
- `MinimumParticipationRounds` >= 0; `MinimumAnsweredQuestions` >= 0

Domain validation (IBusinessRule) for the numeric constraints; application-level validation for the reward existence check.

**Rationale**: Follows the three-level validation pattern (API → Application → Domain). Domain handles invariant constraints; application handles external references (reward catalog).

---

## R8. Query endpoints

**Decision**: Two new vertical slices:
- `GetPlayerConsolationStatus(GameId, PlayerId)` → returns: received (bool), policy, points, rewardName, timestamp
- `GetPlayerConsolationHistory(PlayerId)` → returns: list of (GameId, GameName, policy, points, rewardName, timestamp)

Both are `IQuery` slices with `RequireAuthorization`. The status query checks if the player has a CONSOLATION transaction in the game's ledger. The history query aggregates across all games.

**Rationale**: Follows house pattern (vertical slices, IQuery, IEndpoint). The data is already in the ledger — queries just read it.

---

## R9. Existing test impact

**Decision**: Existing `ScoringConsolationTests` (3 tests) must be updated:
- `Finish_FixedPointsPolicy_EligibleNonWinner_ReceivesConsolation` — may need adjustment if eligibility rules change the eligible set
- `Finish_NonePolicy_NoConsolation` — no change needed
- `Finish_Winner_NoConsolation` — no change needed

The test helper `ScoringTestBase.Config()` needs to pass the new `GameConfiguration` parameters with defaults. Since the new parameters have defaults, existing callers compile without changes.

**Rationale**: Backward-compatible constructor defaults ensure zero breakage in existing tests and handlers.
