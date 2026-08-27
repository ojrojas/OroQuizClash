# Feature Specification: Scoring System

**Feature Branch**: `007-scoring-system`

**Created**: 2026-08-27

**Status**: Draft

**Input**: User description: "007 — Scoring: Definir el sistema de puntuación. El scoring es parte del corazón del juego. Conceptos: CurrentPoints, SecuredPoints, PotentialPoints, RoundPoints, TotalPoints, Ledger. Cada modificación debe generar una transacción: PointTransaction. Tipos: ANSWER_CORRECT, ANSWER_INCORRECT, ROUND_BONUS, LEVEL_BONUS, GAME_BONUS, PENALTY, WITHDRAWAL, REWARD_REDEMPTION, CONSOLATION, ADJUSTMENT. Reglas: Nunca player.Points += 100 como operación aislada sin trazabilidad. Debe existir una operación de dominio que represente: AwardPoints, RemovePoints, SecurePoints, ConsumePoints. Dependencias: SPEC-005, SPEC-006, SPEC-008, SPEC-009, SPEC-010"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Points Awarded on Correct Answer (Priority: P1)

A player answers a question correctly during an active round. The system awards points based on the game configuration (difficulty, round number, time bonus if applicable). The points are recorded as a `PointTransaction` of type `ANSWER_CORRECT` in the player's ledger. The player's `CurrentPoints` and `RoundPoints` are updated accordingly.

**Why this priority**: This is the fundamental scoring action — without it, the game has no feedback loop. Every other scoring concept builds on this.

**Independent Test**: Can be fully tested by submitting a correct answer in an active round and verifying a `PointTransaction` is created with the correct amount, type, and that the player's balance reflects the change.

**Acceptance Scenarios**:

1. **Given** a game in progress with a player in an active round, **When** the player submits a correct answer, **Then** a `PointTransaction` of type `ANSWER_CORRECT` is created with the configured points for that round/difficulty, and the player's `CurrentPoints` increases by that amount.
2. **Given** a player with 500 `CurrentPoints`, **When** they answer correctly in a round worth 100 points, **Then** their `CurrentPoints` becomes 600 and a transaction records the +100 change.
3. **Given** a correct answer is submitted, **When** the scoring is applied, **Then** the transaction includes the game ID, round ID, player ID, timestamp, and resulting balance for full traceability.

---

### User Story 2 - Points Deducted on Incorrect Answer (Priority: P1)

A player answers a question incorrectly. Depending on the configured loss policy (`LOSE_ALL`, `LOSE_CURRENT_ROUND`, `LOSE_UNSECURED_POINTS`, `FALLBACK_TO_CHECKPOINT`), the system deducts points. The deduction is recorded as a `PointTransaction` of type `ANSWER_INCORRECT` or `PENALTY`.

**Why this priority**: Equally fundamental to correct-answer scoring — the risk of losing points is what creates game tension.

**Independent Test**: Can be tested by submitting an incorrect answer and verifying the correct loss policy is applied, a transaction is created, and the balance is updated.

**Acceptance Scenarios**:

1. **Given** a game with `LOSE_CURRENT_ROUND` policy and a player with 200 `RoundPoints`, **When** the player answers incorrectly, **Then** a `PointTransaction` of type `ANSWER_INCORRECT` deducts 200 points and `RoundPoints` resets to 0.
2. **Given** a game with `LOSE_ALL` policy and a player with 500 `CurrentPoints`, **When** the player answers incorrectly, **Then** all unsecured points are deducted via a single transaction.
3. **Given** a game with `FALLBACK_TO_CHECKPOINT` policy and a player with 300 `SecuredPoints` and 200 unsecured, **When** the player answers incorrectly, **Then** balance falls back to 300 and a transaction records the -200 deduction.

---

### User Story 3 - Points Secured at Round/Level Completion (Priority: P1)

When a round or level completes successfully, the player's accumulated `RoundPoints` become `SecuredPoints`. This is recorded as a `SecurePoints` domain operation. Secured points are protected from loss policies (except `LOSE_ALL`). A `ROUND_BONUS` or `LEVEL_BONUS` transaction may also be generated.

**Why this priority**: Securing points is the core risk/reward mechanic — players must decide whether to continue risking points or secure them.

**Independent Test**: Can be tested by completing a round and verifying `RoundPoints` are transferred to `SecuredPoints`, a transaction is created, and the secured balance is protected from subsequent losses.

**Acceptance Scenarios**:

1. **Given** a player with 300 `RoundPoints` at round completion, **When** the round is completed, **Then** 300 points are moved to `SecuredPoints` via a `SecurePoints` operation and a transaction records the securing.
2. **Given** a player with 500 `SecuredPoints` in a `LOSE_UNSECURED_POINTS` game, **When** they answer incorrectly in the next round, **Then** their `SecuredPoints` remain at 500.
3. **Given** a level completion with a configured level bonus of 50 points, **When** the level is completed, **Then** a `LEVEL_BONUS` transaction of +50 is created.

---

### User Story 4 - Points Consumed for Reward Redemption (Priority: P2)

A player redeems a reward using their eligible points. The system deducts points via a `ConsumePoints` domain operation, creating a `REWARD_REDEMPTION` transaction. The redemption is atomic — if insufficient points exist, the operation fails without partial deduction.

**Why this priority**: Reward redemption gives points real-world value and is a key retention mechanic, but depends on the reward system (SPEC-009).

**Independent Test**: Can be tested by attempting a redemption with sufficient and insufficient points, verifying atomic deduction and transaction creation.

**Acceptance Scenarios**:

1. **Given** a player with 1000 eligible points and a reward costing 300, **When** they redeem the reward, **Then** a `REWARD_REDEMPTION` transaction of -300 is created and balance becomes 700.
2. **Given** a player with 200 eligible points and a reward costing 300, **When** they attempt redemption, **Then** the operation fails with `InsufficientPoints` error and no transaction is created.
3. **Given** two concurrent redemption requests for the same player, **When** both arrive simultaneously, **Then** only one succeeds if points are insufficient for both (optimistic concurrency).

---

### User Story 5 - Player Withdrawal with Policy-Based Scoring (Priority: P2)

A player withdraws from an active game. The withdrawal policy (`LOSE_ALL`, `KEEP_CURRENT_SCORE`, `KEEP_SECURED_SCORE`, `KEEP_CHECKPOINT_SCORE`) determines what points they retain. A `WITHDRAWAL` transaction records the final scoring adjustment.

**Why this priority**: Withdrawal is a critical player decision point that interacts with the securing mechanic.

**Independent Test**: Can be tested by withdrawing a player under each policy and verifying the correct points are retained/lost with proper transaction recording.

**Acceptance Scenarios**:

1. **Given** a player with 500 `CurrentPoints` (300 secured, 200 unsecured) and `KEEP_SECURED_SCORE` policy, **When** they withdraw, **Then** a `WITHDRAWAL` transaction records -200 and final score is 300.
2. **Given** a player with 500 `CurrentPoints` and `LOSE_ALL` policy, **When** they withdraw, **Then** a `WITHDRAWAL` transaction records -500 and final score is 0.
3. **Given** a game in terminal state (FINISHED/CANCELLED), **When** a player attempts withdrawal, **Then** the operation is rejected.

---

### User Story 6 - Game Completion Bonus (Priority: P2)

When a game finishes successfully (all rounds completed), players receive a `GAME_BONUS` based on their performance. The bonus is recorded as a `PointTransaction` of type `GAME_BONUS`.

**Why this priority**: Completion bonuses reward full game participation but depend on the game lifecycle being complete.

**Independent Test**: Can be tested by finishing a game and verifying bonus transactions are created for eligible players.

**Acceptance Scenarios**:

1. **Given** a game finishing with all rounds completed, **When** the game transitions to FINISHED, **Then** each non-withdrawn player receives a `GAME_BONUS` transaction.
2. **Given** a player who withdrew before game completion, **When** the game finishes, **Then** they do NOT receive a `GAME_BONUS`.

---

### User Story 7 - Ledger Reconstruction & Audit (Priority: P2)

The complete point history of a player within a game can be reconstructed from the transaction ledger. The sum of all transactions equals the player's current balance. The ledger is append-only and immutable.

**Why this priority**: Auditability is a constitutional requirement and enables dispute resolution, debugging, and anti-cheat verification.

**Independent Test**: Can be tested by performing multiple scoring operations and verifying the ledger sum matches the current balance, and that no transaction can be modified or deleted.

**Acceptance Scenarios**:

1. **Given** a player with 10 transactions in a game, **When** the ledger is queried, **Then** the sum of all transaction amounts equals the player's `CurrentPoints`.
2. **Given** an existing transaction, **When** any actor attempts to modify or delete it, **Then** the operation is rejected (append-only).
3. **Given** a player's ledger, **When** queried, **Then** each transaction shows type, amount, timestamp, round reference, and resulting balance.

---

### User Story 8 - Administrative Point Adjustment (Priority: P3)

An administrator can manually adjust a player's points (e.g., to correct errors or handle disputes). Adjustments are recorded as `ADJUSTMENT` transactions with a mandatory reason. Adjustments can be positive or negative.

**Why this priority**: Administrative corrections are necessary for operational support but are rare events.

**Independent Test**: Can be tested by applying an adjustment and verifying the transaction is created with the reason, and the balance is updated.

**Acceptance Scenarios**:

1. **Given** an admin with appropriate permissions, **When** they apply a +100 adjustment with reason "System error correction", **Then** an `ADJUSTMENT` transaction is created and balance increases by 100.
2. **Given** an admin applying an adjustment, **When** no reason is provided, **Then** the operation is rejected.
3. **Given** a non-admin user, **When** they attempt an adjustment, **Then** the operation is rejected with authorization error.

---

### User Story 9 - Consolation Points (Priority: P3)

A player who did not win but met certain criteria (e.g., participated in minimum rounds, reached a certain level) may receive consolation points. These are independent from normal rewards and recorded as `CONSOLATION` transactions.

**Why this priority**: Consolation is a retention mechanic but depends on the reward/consolation system (SPEC-010).

**Independent Test**: Can be tested by finishing a game where a player meets consolation criteria and verifying the transaction is created.

**Acceptance Scenarios**:

1. **Given** a player who completed minimum rounds but did not win, **When** the game finishes and consolation criteria are met, **Then** a `CONSOLATION` transaction is created.
2. **Given** a player who withdrew before minimum rounds, **When** the game finishes, **Then** no consolation is awarded.

---

### Edge Cases

- What happens when a player's balance would go negative? (Balance MUST NOT go below zero; deductions are capped at available balance unless policy explicitly allows negative.)
- What happens when two scoring events occur simultaneously for the same player? (Optimistic concurrency prevents double-counting; one succeeds, the other retries or fails gracefully.)
- What happens when a game is force-finished mid-round? (Unsecured `RoundPoints` are handled per the configured loss/withdrawal policy.)
- What happens when a player has zero points and an incorrect answer triggers a deduction? (No negative balance; transaction records 0 deduction or is skipped.)
- What happens when the same answer submission is received twice (duplicate)? (Idempotency key prevents duplicate point allocation.)
- What happens when a reward redemption and a point deduction race concurrently? (Optimistic concurrency ensures only one operation succeeds if balance is insufficient for both.)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST record every point modification as an immutable `PointTransaction` ledger entry — no direct balance mutation is permitted.
- **FR-002**: System MUST support the following transaction types: `ANSWER_CORRECT`, `ANSWER_INCORRECT`, `ROUND_BONUS`, `LEVEL_BONUS`, `GAME_BONUS`, `PENALTY`, `WITHDRAWAL`, `REWARD_REDEMPTION`, `CONSOLATION`, `ADJUSTMENT`.
- **FR-003**: System MUST provide domain operations: `AwardPoints`, `RemovePoints`, `SecurePoints`, `ConsumePoints` — all generating ledger transactions.
- **FR-004**: System MUST track per-player scoring concepts: `CurrentPoints` (total available), `SecuredPoints` (protected from loss), `PotentialPoints` (maximum achievable in current round), `RoundPoints` (accumulated in current round), `TotalPoints` (lifetime/cumulative).
- **FR-005**: Player balance MUST be reconstructable from the sum of all ledger transactions.
- **FR-006**: System MUST apply the configured loss policy (`LOSE_ALL`, `LOSE_CURRENT_ROUND`, `LOSE_UNSECURED_POINTS`, `FALLBACK_TO_CHECKPOINT`) on incorrect answers.
- **FR-007**: System MUST apply the configured withdrawal policy (`LOSE_ALL`, `KEEP_CURRENT_SCORE`, `KEEP_SECURED_SCORE`, `KEEP_CHECKPOINT_SCORE`) on player withdrawal.
- **FR-008**: `SecurePoints` MUST transfer `RoundPoints` to `SecuredPoints`, making them protected from subsequent loss (except `LOSE_ALL`).
- **FR-009**: `ConsumePoints` MUST be atomic — insufficient balance MUST reject the entire operation without partial deduction.
- **FR-010**: Each `PointTransaction` MUST include: transaction ID, player reference, game reference, round reference (when applicable), type, amount (positive or negative), timestamp, resulting balance, and optional reason/description.
- **FR-011**: The ledger MUST be append-only — transactions cannot be modified or deleted after creation.
- **FR-012**: System MUST prevent duplicate point allocation via idempotency (duplicate answer submissions MUST NOT create duplicate transactions).
- **FR-013**: System MUST enforce optimistic concurrency on score updates to prevent race conditions.
- **FR-014**: Administrative adjustments MUST require elevated permissions and a mandatory reason.
- **FR-015**: Player balance MUST NOT go below zero unless explicitly permitted by a specific policy.
- **FR-016**: System MUST expose the player's score ledger for audit/query purposes.
- **FR-017**: Scoring operations MUST be server-authoritative — client-supplied point values MUST be rejected.

### Key Entities

- **PointTransaction**: Immutable ledger entry recording a single point modification. Attributes: ID, player reference, game reference, round reference, type (enumeration), amount, resulting balance, timestamp, reason/description, idempotency key.
- **PlayerScore (Score)**: Per-player scoring state within a game. Attributes: player reference, game reference, `CurrentPoints`, `SecuredPoints`, `RoundPoints`, `PotentialPoints`, `TotalPoints`, concurrency version.
- **PointTransactionType**: Enumeration of all valid transaction types (10 values).
- **ScoringPolicy**: Configuration-driven rules for how points are awarded, deducted, secured, and consumed. References game configuration for loss/withdrawal/consolation/reward policies.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every point change in the system produces exactly one immutable ledger transaction — 100% traceability with zero untracked mutations.
- **SC-002**: Player balance can be reconstructed from ledger history with 100% accuracy at any point in time.
- **SC-003**: Concurrent scoring operations for the same player never produce inconsistent balances (zero race-condition failures under load).
- **SC-004**: Duplicate answer submissions never result in duplicate point allocation (idempotency verified under concurrent duplicate requests).
- **SC-005**: All 10 transaction types are exercised and produce correct balance changes in automated tests.
- **SC-006**: All 4 loss policies and all 4 withdrawal policies produce correct point outcomes in automated tests.
- **SC-007**: Reward redemption with insufficient points fails atomically without partial deduction in 100% of cases.
- **SC-008**: Administrative adjustments are fully auditable with reason, actor, and timestamp in 100% of cases.
- **SC-009**: Scoring operations complete within acceptable response time for real-time gameplay (players perceive no delay in score updates).

## Assumptions

- The scoring system operates within the context of a single game — cross-game lifetime scoring (`TotalPoints`) is tracked but the ledger is per-game.
- Point values are determined by game configuration (`PointsPerRound`, difficulty multipliers) defined in SPEC-001 and applied by the round engine (SPEC-005).
- Answer evaluation (correct/incorrect) is handled by SPEC-006; this spec handles the scoring consequences of that evaluation.
- Reward redemption (SPEC-009) and consolation (SPEC-010) define eligibility criteria; this spec handles the point deduction/award mechanics.
- The `PotentialPoints` concept represents the maximum points achievable in the current round (used for UI display and strategy decisions).
- Scoring is always server-side; the client only displays scores and never computes authoritative values.
- The ledger is stored in the same database as game state (SQL Server primary, Oracle portable).
- No external payment or currency system is involved — points are virtual game currency only.

## Dependencies

- **SPEC-001 (Game Configuration)**: Provides `PointsPerRound`, loss/withdrawal/consolation/reward policies.
- **SPEC-005 (Round Engine)**: Triggers scoring events on round start/completion.
- **SPEC-006 (Answer Evaluation)**: Determines correct/incorrect, triggering `AwardPoints`/`RemovePoints`.
- **SPEC-008 (Difficulty Progression)**: Influences point multipliers based on difficulty level.
- **SPEC-009 (Rewards)**: Consumes points via `ConsumePoints` for redemption.
- **SPEC-010 (Consolation)**: Awards consolation points via `AwardPoints`.
