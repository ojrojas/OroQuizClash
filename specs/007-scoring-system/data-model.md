# Data Model: Scoring System

**Feature**: 007-scoring-system
**Date**: 2026-08-27

## Entities

### PointTransaction (EXTEND existing)

Immutable ledger entry recording a single point modification.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Id | PointTransactionId (Guid) | PK | StronglyTypedId |
| GameId | GameId (Guid) | FK → Game, NOT NULL | Aggregate parent |
| PlayerId | Guid | NOT NULL, INDEX | User who receives/deducts points |
| RoundId | GameRoundId (Guid) | NULLABLE | Null for game-level transactions (GAME_BONUS, WITHDRAWAL, ADJUSTMENT) |
| QuestionId | QuestionId (Guid) | NULLABLE | Only for ANSWER_CORRECT/ANSWER_INCORRECT |
| AnswerId | AnswerId (Guid) | NULLABLE | Only for ANSWER_CORRECT/ANSWER_INCORRECT |
| Type | PointTransactionType | NOT NULL | Enumeration (10 values) |
| Points | int | NOT NULL | Positive = award, negative = deduction |
| ResultingBalance | int | NOT NULL, DEFAULT 0 | Player's CurrentPoints after this transaction |
| Reason | string? | NULLABLE, MAX 500 | Mandatory for ADJUSTMENT, optional for others |
| CreatedAt | DateTimeOffset | NOT NULL | Server timestamp |

**Invariants**:
- Append-only: no UPDATE or DELETE after creation
- `Points` MUST NOT be 0 (except WITHDRAWAL with KEEP_CURRENT_SCORE policy)
- `Reason` MUST be non-empty when `Type == ADJUSTMENT`
- `ResultingBalance` MUST equal sum of all prior transactions for the same player + this transaction's Points

**Indexes**:
- `IX_PointTransaction_GameId_PlayerId` (query player ledger)
- `IX_PointTransaction_GameId_PlayerId_CreatedAt` (ordered ledger)
- `IX_PointTransaction_Type` (filter by type)

---

### PlayerScore (NEW — ValueObject owned by GamePlayer)

Denormalized scoring state for fast access. Always consistent with ledger.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| CurrentPoints | int | NOT NULL, ≥ 0 | Total available points (secured + unsecured) |
| SecuredPoints | int | NOT NULL, ≥ 0 | Points protected from loss (except LOSE_ALL) |
| RoundPoints | int | NOT NULL, ≥ 0 | Points accumulated in current round (not yet secured) |
| PotentialPoints | int | NOT NULL, ≥ 0 | Maximum achievable in current round (informational) |
| TotalPoints | int | NOT NULL, ≥ 0 | Lifetime cumulative (never decreases) |

**Invariants**:
- `CurrentPoints == SecuredPoints + RoundPoints` (always)
- `CurrentPoints ≥ 0` (balance cannot go negative)
- `SecuredPoints ≤ CurrentPoints`
- `TotalPoints ≥ CurrentPoints` (TotalPoints only increases)
- `PotentialPoints` resets to 0 when no round is active

**Storage**: Owned entity on `GamePlayer` (EF Core `OwnsOne`)

---

### GamePlayer (EXTEND existing)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Id | GamePlayerId (Guid) | PK | Existing |
| GameId | GameId (Guid) | FK → Game | Existing |
| UserId | Guid | NOT NULL | Existing |
| JoinedAt | DateTimeOffset | NOT NULL | Existing |
| DisplayName | string? | NULLABLE | Existing |
| Score | PlayerScore | NOT NULL | NEW — owned ValueObject |
| IsWithdrawn | bool | NOT NULL, DEFAULT false | NEW — withdrawal flag |
| WithdrawnAt | DateTimeOffset? | NULLABLE | NEW — withdrawal timestamp |

---

### PointTransactionType (EXTEND existing Enumeration)

| Id | Name | Description |
|----|------|-------------|
| 1 | ANSWER_CORRECT | Points awarded for correct answer |
| 2 | ANSWER_INCORRECT | Points deducted for incorrect answer |
| 3 | ROUND_BONUS | Bonus for completing a round |
| 4 | LEVEL_BONUS | Bonus for advancing difficulty level |
| 5 | GAME_BONUS | Bonus for completing the game |
| 6 | PENALTY | Penalty (e.g., timeout, rule violation) |
| 7 | WITHDRAWAL | Points adjustment on player withdrawal |
| 8 | REWARD_REDEMPTION | Points consumed for reward |
| 9 | CONSOLATION | Consolation points awarded |
| 10 | ADJUSTMENT | Administrative manual adjustment |

---

## Domain Operations (on Game aggregate)

### AwardPoints(playerId, amount, type, roundId?, questionId?, answerId?, reason?)

- **Preconditions**: Game active (IN_PROGRESS/ROUND_IN_PROGRESS/ROUND_COMPLETED), player exists and not withdrawn, amount > 0
- **Effects**: Creates `PointTransaction(+amount)`, updates `PlayerScore.CurrentPoints += amount`, `PlayerScore.RoundPoints += amount` (if round-scoped), `PlayerScore.TotalPoints += amount`
- **Events**: `ScoreUpdatedDomainEvent`

### RemovePoints(playerId, amount, type, roundId?, questionId?, answerId?, reason?)

- **Preconditions**: Game active, player exists and not withdrawn, amount > 0
- **Effects**: Applies loss policy to determine actual deduction. Creates `PointTransaction(-actualDeduction)`, updates `PlayerScore` accordingly. Deduction capped at available balance (never negative).
- **Events**: `ScoreUpdatedDomainEvent`

### SecurePoints(playerId, roundId)

- **Preconditions**: Round completed, player exists and not withdrawn, `RoundPoints > 0`
- **Effects**: Moves `RoundPoints` to `SecuredPoints`. Creates `PointTransaction(0, ROUND_BONUS)` or no transaction if just securing (securing is not a point modification, just a reclassification). Optionally awards round bonus if configured.
- **Events**: `PointsSecuredDomainEvent`

### ConsumePoints(playerId, amount, type, reason)

- **Preconditions**: Player exists, `CurrentPoints ≥ amount`, game in valid state for consumption
- **Effects**: Creates `PointTransaction(-amount)`, updates `PlayerScore.CurrentPoints -= amount`, `PlayerScore.SecuredPoints -= amount` (consumed from secured first)
- **Events**: `ScoreUpdatedDomainEvent`
- **Failure**: Returns `InsufficientPoints` error if balance < amount (atomic, no partial deduction)

### WithdrawPlayer(playerId)

- **Preconditions**: Game active (not terminal), player exists and not already withdrawn
- **Effects**: Applies withdrawal policy. Creates `WITHDRAWAL` transaction. Marks player as withdrawn. Updates `PlayerScore`.
- **Events**: `ScoreUpdatedDomainEvent`

---

## State Transitions

### PlayerScore lifecycle within a game:

```
JoinGame → PlayerScore(0,0,0,0,0)
StartRound → PotentialPoints = PointsPerRound × DifficultyMultiplier
SubmitAnswer(correct) → RoundPoints += points, CurrentPoints += points, TotalPoints += points
SubmitAnswer(incorrect) → Apply LossPolicy → deduct from RoundPoints/CurrentPoints/SecuredPoints
CompleteRound → SecurePoints: RoundPoints → SecuredPoints, award ROUND_BONUS if configured
StartRound(next) → PotentialPoints updated, RoundPoints = 0
Finish → Award GAME_BONUS to non-withdrawn players
Withdraw → Apply WithdrawalPolicy → deduct, mark withdrawn
```

### Loss Policy Effects:

| Policy | Effect on Incorrect Answer |
|--------|---------------------------|
| LOSE_ALL | `CurrentPoints = 0`, `SecuredPoints = 0`, `RoundPoints = 0` |
| LOSE_CURRENT_ROUND | `RoundPoints = 0`, `CurrentPoints -= RoundPoints` |
| LOSE_UNSECURED_POINTS | `CurrentPoints = SecuredPoints`, `RoundPoints = 0` |
| FALLBACK_TO_CHECKPOINT | `CurrentPoints = SecuredPoints`, `RoundPoints = 0` (checkpoint = last secured) |

### Withdrawal Policy Effects:

| Policy | Effect on Withdrawal |
|--------|---------------------|
| LOSE_ALL | Final score = 0, deduct all |
| KEEP_CURRENT_SCORE | Final score = CurrentPoints, no deduction |
| KEEP_SECURED_SCORE | Final score = SecuredPoints, deduct unsecured |
| KEEP_CHECKPOINT_SCORE | Final score = SecuredPoints (checkpoint), deduct unsecured |

---

## Relationships

```
Game (AggregateRoot)
├── GamePlayer (Entity) [1..*]
│   └── PlayerScore (ValueObject, owned)
├── GameRound (Entity) [1..*]
├── Answer (Entity) [1..*]
└── PointTransaction (Entity) [1..*]  ← ledger (append-only)
```

## Validation Rules

| Rule | Applies To | Description |
|------|-----------|-------------|
| BalanceCannotGoNegativeRule | RemovePoints, ConsumePoints | Deduction capped at available balance |
| SufficientBalanceRule | ConsumePoints | Must have ≥ amount before consumption |
| AdjustmentReasonRequiredRule | AdjustScore | Reason mandatory (3-500 chars) |
| ScoringStateValidRule | All operations | Game must be in valid state for scoring |
| PlayerNotWithdrawnRule | AwardPoints, RemovePoints, SecurePoints | Cannot score withdrawn players |
