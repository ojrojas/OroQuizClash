# Research: Scoring System

**Feature**: 007-scoring-system
**Date**: 2026-08-27

## R1: PlayerScore Modeling — ValueObject vs. Computed from Ledger

**Decision**: `PlayerScore` as a ValueObject owned by `GamePlayer`, denormalized from ledger for fast access, but always verifiable via ledger reconstruction.

**Rationale**: 
- The constitution requires balance reconstructable from ledger (FR-005), but querying the ledger for every score display is expensive at scale.
- A denormalized `PlayerScore` ValueObject on `GamePlayer` provides O(1) access to current/secured/round points.
- The ledger remains the source of truth; `PlayerScore` is a cache that MUST always equal `Sum(PointTransactions)`.
- Domain invariants enforce consistency: every `PointTransaction` creation MUST update `PlayerScore` atomically within the same aggregate operation.

**Alternatives considered**:
- Pure computed (no stored score): Rejected — too expensive for real-time gameplay with 50+ transactions per player.
- Separate `Score` aggregate: Rejected — violates aggregate boundary; score is inseparable from game context.

## R2: Loss/Withdrawal Policy Implementation — Strategy Pattern vs. Switch

**Decision**: Strategy pattern with `ILossPolicyStrategy` and `IWithdrawalPolicyStrategy` interfaces, resolved within the domain aggregate.

**Rationale**:
- Constitution Constraint C mandates "configurable rules via strategy/policy abstractions — NOT hardcoded."
- 4 loss policies × 4 withdrawal policies = 8 distinct behaviors; a switch statement would violate OCP.
- Strategies are stateless and can be resolved from the `GameConfiguration` enumeration value.
- Strategies live in Domain layer (not Application) because they encode business invariants.

**Alternatives considered**:
- Switch/case in `Game.RemovePoints()`: Rejected — violates OCP and constitution Constraint C.
- External domain service: Rejected — policies are aggregate invariants, must live inside aggregate boundary.
- Strategy as injected dependency: Rejected — aggregate must be self-contained; strategies resolved internally from config.

## R3: PointTransaction Extension — ResultingBalance and Reason

**Decision**: Extend `PointTransaction` with `ResultingBalance` (int) and `Reason` (string?, nullable).

**Rationale**:
- FR-010 requires "resulting balance" per transaction for audit trail.
- FR-014 requires "mandatory reason" for adjustments; nullable for other types.
- `ResultingBalance` enables point-in-time balance reconstruction without summing all prior transactions.
- Existing transactions (from SPEC-006) will have `ResultingBalance = 0` and `Reason = null` — backward compatible.

**Alternatives considered**:
- Separate audit table: Rejected — duplicates data, complicates queries.
- Computed balance (no storage): Rejected — expensive for ledger queries with pagination.

## R4: PotentialPoints Definition

**Decision**: `PotentialPoints` = `PointsPerRound × DifficultyMultiplier` for the current round (maximum achievable if answer is correct).

**Rationale**:
- Used for UI display ("you can earn X points this round").
- Computed from `GameConfiguration.PointsPerRound` and current round difficulty.
- Not stored in ledger — derived value on `PlayerScore` updated at round start.
- Resets to 0 when no round is active.

**Alternatives considered**:
- Include bonus potential: Rejected — bonuses are not known until round/level completion.
- Store as transaction: Rejected — not a point modification, just informational.

## R5: Integration with Existing SubmitAnswer (SPEC-006)

**Decision**: Extend `Game.SubmitAnswer()` to call `AwardPoints`/`RemovePoints` internally instead of directly creating `PointTransaction`.

**Rationale**:
- Current implementation creates `PointTransaction` directly in `SubmitAnswer` — this bypasses the new scoring operations.
- Refactoring to use `AwardPoints`/`RemovePoints` ensures loss policies are applied and `PlayerScore` is updated.
- The `Answer` entity creation remains unchanged; only the scoring side-effect is refactored.
- Backward compatible: existing tests that verify `PointTransaction` creation still pass.

**Alternatives considered**:
- Keep direct creation + add separate policy application: Rejected — splits scoring logic across two paths.
- Event-driven (AnswerEvaluated → scoring handler): Rejected — adds async complexity for a synchronous invariant.

## R6: ConsumePoints Atomicity and Concurrency

**Decision**: `ConsumePoints` validates balance within the aggregate, creates transaction, and updates `PlayerScore` atomically. Optimistic concurrency via `Game.RowVersion` prevents race conditions.

**Rationale**:
- FR-009 requires atomic operation — no partial deduction.
- Two concurrent redemptions: first succeeds, second gets `DbUpdateConcurrencyException` → retry or fail.
- Balance check + deduction happen in the same domain operation (no TOCTOU gap).
- `RowVersion` on `Game` aggregate protects all child entities including `PointTransaction` and `GamePlayer.PlayerScore`.

**Alternatives considered**:
- Pessimistic locking: Rejected — constitution prefers optimistic concurrency (Constraint F).
- Separate Score aggregate with own RowVersion: Rejected — breaks aggregate boundary.

## R7: Withdrawal Scoring Integration

**Decision**: `Game.WithdrawPlayer(playerId)` applies the configured `WithdrawalPolicy` via strategy, creates a `WITHDRAWAL` transaction for any deducted points, and marks the player as withdrawn.

**Rationale**:
- Withdrawal is a domain action (Constitution Principle I).
- The policy determines how many points are retained vs. deducted.
- A `WITHDRAWAL` transaction records the deduction (or 0 if keeping all).
- Player status changes to prevent further scoring operations.

**Alternatives considered**:
- Application service handles withdrawal scoring: Rejected — business rule must be in domain.
- No transaction if keeping all points: Rejected — FR-001 requires every point modification recorded; a 0-amount transaction provides audit trail of the withdrawal event.

## R8: Round/Level/Game Bonus Timing

**Decision**: 
- `ROUND_BONUS`: Awarded during `CompleteRound()` for each active player.
- `LEVEL_BONUS`: Awarded when difficulty level increases (detected in `StartRound()` comparing previous round difficulty).
- `GAME_BONUS`: Awarded during `Finish()` for each non-withdrawn player.

**Rationale**:
- Bonuses are triggered by state transitions, not by separate commands.
- This ensures bonuses are always applied consistently (no forgotten bonus).
- Amount determined by `GameConfiguration` (PointsPerRound, ScoringSystem).

**Alternatives considered**:
- Separate `AwardBonus` command: Rejected — adds complexity and risk of missing bonus.
- Event-driven bonus handler: Rejected — synchronous invariant within aggregate.

## R9: Administrative Adjustment Authorization

**Decision**: `AdjustScore` command requires `AdminOrGameManager` policy (existing in `Program.cs`). Domain validates reason is non-empty (3-500 chars, same pattern as `Cancel`/`ForceFinish`).

**Rationale**:
- FR-014 requires elevated permissions + mandatory reason.
- Reuses existing `AdminOrGameManager` authorization policy already configured in the API.
- Reason validation follows established domain pattern (min 3, max 500 chars).

**Alternatives considered**:
- Separate `ADMIN` only policy: Rejected — `GAME_MANAGER` should also be able to correct scoring errors.
- No length validation: Rejected — inconsistent with existing reason validation patterns.

## R10: Ledger Query Pagination

**Decision**: `GetScoreLedger` returns paginated transactions (default page size 50, max 200) ordered by `CreatedAt` descending.

**Rationale**:
- A game can have ~50 transactions per player × 10 players = 500 transactions.
- Pagination prevents large payloads.
- Descending order shows most recent activity first (standard ledger UX).

**Alternatives considered**:
- No pagination (return all): Rejected — potential large payloads for long games.
- Cursor-based pagination: Rejected — over-engineering for this scale; offset pagination sufficient.
