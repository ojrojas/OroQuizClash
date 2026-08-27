# Feature Specification: Consolation

**Feature Branch**: `010-consolation`

**Created**: 2026-08-27

**Status**: Draft

**Input**: User description: "010 — Consolation. Definir el mecanismo de gratificación para participantes que no obtuvieron un premio normal. Reglas configurables: MinimumParticipationRounds, MinimumAnsweredQuestions, ConsolationPoints, ConsolationReward, EligibilityPolicy. La consolación no debe convertirse en saldo normal automáticamente, debe quedar registrada, no debe duplicarse, debe poder auditarse. Dependencias: SPEC-006, SPEC-007, SPEC-009"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Eligible Players Receive Consolation at Game Finish (Priority: P1)

When a game finishes, players who participated but did not win receive a consolation award according to the game's configured policy. The system evaluates eligibility based on the policy's rules (minimum rounds played, minimum questions answered), grants the configured consolation (points or reward), and records the award as a CONSOLATION ledger entry. Players who withdrew or were eliminated before meeting the minimum thresholds do not receive consolation. The same player can never receive consolation twice for the same game, and the award is fully auditable.

**Why this priority**: Consolation is the core value — it gives non-winning players a reason to keep playing and converts participation into tangible recognition. Without it, losing players have no reward path.

**Independent Test**: Can be fully tested by creating a game with a FixedPoints consolation policy, having 2 players (one winner, one non-winner with sufficient participation), finishing the game, and verifying the non-winner received a CONSOLATION transaction while the winner did not — all without needing a running reward catalog.

**Acceptance Scenarios**:

1. **Given** a game with FixedPoints policy (100 points) and a player who answered 3 questions across 2 rounds, **When** the game finishes and the player did not win, **Then** the player receives a CONSOLATION transaction of +100 points and the award is recorded in the ledger.
2. **Given** a game with FixedPoints policy and a player who answered only 1 question (below the minimum), **When** the game finishes, **Then** the player does not receive consolation and no CONSOLATION transaction is created.
3. **Given** a player who won the game, **When** the game finishes, **Then** the player does not receive consolation regardless of the policy.
4. **Given** a player who withdrew mid-game before meeting the minimum rounds, **When** the game finishes, **Then** the player does not receive consolation.
5. **Given** a player who was eliminated, **When** the game finishes, **Then** the player does not receive consolation.
6. **Given** a game finishes and a player is awarded consolation, **When** any duplicate attempt to award consolation for the same game and player occurs, **Then** the system refuses and the player has exactly one CONSOLATION transaction.

---

### User Story 2 - Reward-Based Consolation (Priority: P2)

When the consolation policy is set to grant a reward from the catalog instead of (or in addition to) points, the system creates a RewardRedemption for the eligible non-winning player using the game's configured consolation reward. The redemption is created in APPROVED status (admin pre-approved, no manual approval needed) and follows the existing reward lifecycle from SPEC-009. This connects the consolation mechanism to the rewards engine.

**Why this priority**: Reward-based consolation extends the consolation mechanism to the reward catalog, enabling badges, coupons, and other non-point prizes. It builds on US1's eligibility logic.

**Independent Test**: Can be tested by configuring a game with a reward-based consolation policy pointing to an existing reward, having eligible non-winning players finish, and verifying APPROVED redemptions are created for each eligible player without duplicate entries.

**Acceptance Scenarios**:

1. **Given** a game configured with a reward-based consolation policy referencing reward "Participation Badge", **When** an eligible non-winning player finishes, **Then** an APPROVED RewardRedemption is created for that player for the "Participation Badge" reward.
2. **Given** a game with both FixedPoints and reward-based consolation configured, **When** an eligible non-winning player finishes, **Then** the player receives both a CONSOLATION point transaction AND an APPROVED RewardRedemption.
3. **Given** a non-winning player who does not meet eligibility thresholds, **When** the game finishes, **Then** no reward-based consolation redemption is created.

---

### User Story 3 - Consolation History and Status (Priority: P3)

Players can view their consolation status and history across games. A player can see whether they received consolation in a specific game, what type (points or reward), and when it was awarded. Administrators can view consolation awards across all games for auditing purposes.

**Why this priority**: Visibility and auditability complete the consolation mechanism. Players see their rewards; administrators can verify fairness and troubleshoot.

**Independent Test**: Can be tested by querying consolation status for a player who received points consolation, verifying the response includes the policy type, points awarded, and timestamp — then querying the full history and verifying all past consolation awards are listed.

**Acceptance Scenarios**:

1. **Given** a player who received FixedPoints consolation in a game, **When** the player queries their consolation status for that game, **Then** the response shows: received = true, policy = FixedPoints, points = 100, timestamp.
2. **Given** a player who did not receive consolation in a game, **When** the player queries their consolation status, **Then** the response shows: received = false.
3. **Given** a player who played 5 games and received consolation in 3, **When** the player queries their consolation history, **Then** the response lists all 3 consolation awards with game reference, type, and timestamp.
4. **Given** an administrator, **When** querying consolation across all games, **Then** the administrator sees a complete list of all consolation awards with player, game, policy, and amount — filterable by game or player.

---

### Edge Cases

- What happens when a game has ConsolationPolicy.None? (No consolation is awarded; no transactions created.)
- What happens when all players in a game are eligible for consolation? (All non-winners receive it; if there is a tie for first, tied players do not receive consolation.)
- What happens when a player was both withdrawn AND did not meet minimum rounds? (Withdrawn players who don't meet thresholds are excluded; withdrawn players who DO meet thresholds still receive consolation.)
- What happens when the game has 0 rounds completed? (No consolation — requires minimum participation.)
- What happens when the same player is awarded consolation and then the game result is disputed? (Consolation is immutable once awarded; dispute resolution is out of scope.)
- What happens when the configured consolation reward does not exist in the catalog? (The game configuration validation rejects the setup; the error is caught at game creation.)
- What happens when a player's participation spans all rounds but they answered 0 questions? (Not eligible — MinimumAnsweredQuestions not met.)
- What happens when multiple games finish simultaneously? (Each game's consolation is evaluated independently; no cross-game interference.)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST evaluate consolation eligibility at game finish for each active, non-winning player using the game's configured ConsolationPolicy.
- **FR-002**: System MUST support the following ConsolationPolicy types: None (no consolation), FixedPoints (award a configurable point amount), ParticipationBased (award points scaled by participation level), RewardBased (grant a reward from the catalog).
- **FR-003**: System MUST evaluate eligibility rules: the player must have answered at least the configured MinimumAnsweredQuestions, must have participated in at least the configured MinimumParticipationRounds, and must not be the game winner.
- **FR-004**: System MUST award consolation as a CONSOLATION ledger entry (PointTransaction type CONSOLATION, SPEC-007) — separate from regular balance mutations and fully reconstructable from the ledger.
- **FR-005**: System MUST NOT allow a player to receive consolation more than once per game (idempotent).
- **FR-006**: System MUST NOT automatically merge consolation points into the player's regular earning balance; consolation is a distinct ledger entry with its own transaction type.
- **FR-007**: System MUST record every consolation award with: the player, the game, the policy type, the points or reward granted, and the timestamp — retrievable for audit.
- **FR-008**: System MUST support reward-based consolation (SPEC-009 dependency) by creating an APPROVED RewardRedemption for the configured consolation reward, bypassing the manual approval step.
- **FR-009**: System MUST allow the game configuration to specify: ConsolationPolicy, MinimumParticipationRounds, MinimumAnsweredQuestions, ConsolationPoints (for FixedPoints/ParticipationBased), and ConsolationRewardId (for RewardBased).
- **FR-010**: System MUST validate at game creation that the configured ConsolationRewardId references an existing, active reward in the catalog (when using RewardBased policy).
- **FR-011**: System MUST exclude eliminated players from consolation eligibility regardless of their participation level.
- **FR-012**: System MUST allow withdrawn players to receive consolation if they meet the minimum participation thresholds (withdrawal does not disqualify by itself).
- **FR-013**: System MUST evaluate eligibility using server timestamps and the game's actual round/question history, not client-supplied values.
- **FR-014**: System MUST expose a query to retrieve a player's consolation status for a specific game (received/not received, type, amount, timestamp).
- **FR-015**: System MUST expose a query to retrieve a player's full consolation history across games.
- **FR-016**: System MUST expose a query for administrators to view all consolation awards across games, filterable by game or player.

### Key Entities

- **ConsolationPolicy (existing Enumeration)**: Extended with additional values (None, FixedPoints, ParticipationBased, RewardBased). Configured per game. Determines the type of consolation awarded.
- **GameConfiguration (existing)**: Extended with consolation-specific fields: MinimumParticipationRounds, MinimumAnsweredQuestions, ConsolationPoints, ConsolationRewardId.
- **PointTransaction (existing, from SPEC-007)**: CONSOLATION transaction type already exists. Used to record point-based consolation awards.
- **RewardRedemption (existing, from SPEC-009)**: Used for reward-based consolation. Created in APPROVED status for eligible players.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of eligible non-winning players in a game with an active consolation policy receive the configured consolation within the same game-finish transaction.
- **SC-002**: 0% of ineligible players (below thresholds, winners, eliminated) receive consolation across all test scenarios.
- **SC-003**: Every consolation award is traceable in the point ledger as a CONSOLATION transaction and in the reward system as an APPROVED redemption (when applicable).
- **SC-004**: No player can receive consolation more than once for the same game — duplicate attempts are rejected with zero side effects.
- **SC-005**: Consolation evaluation does not extend game-finish processing by more than 100ms for games with up to 10 players.
- **SC-006**: Players can query their consolation history and see a complete, accurate record of all past consolation awards.
- **SC-007**: Administrators can audit consolation awards across all games with full visibility into player, game, policy, and amount.

## Assumptions

- The `ConsolationPolicy` Enumeration already exists (None, FixedPoints, Badge) and is part of `GameConfiguration`. This spec extends it with additional values and formalizes eligibility rules.
- `GameConfiguration` is immutable after game start; consolation configuration is set at game creation time.
- The CONSOLATION `PointTransactionType` already exists (SPEC-007, value 9). No new transaction types are introduced.
- The `RewardRedemption` model from SPEC-009 is reused for reward-based consolation. The `RewardId` is validated against the active reward catalog at game creation time.
- Eligibility is evaluated at `Game.Finish()` time, not during gameplay. The finish operation already has access to round history and player participation data.
- "Winner" is determined by the existing winner-determination logic in `Game.Finish()` (SPEC-008). If there is a tie, all tied players are winners and none receive consolation.
- The game concept §16 defines consolation as an independent rule: "Consolation is independent from normal rewards; eligibility via explicit business rule; MUST NOT be treated as successful completion."
- Performance target of 100ms for consolation evaluation is a guideline, not a hard SLA, since the operation runs within the existing `SaveChanges` transaction.
- Administrators can override the consolation policy per game at creation time. The system does not enforce a global default — each game explicitly configures its consolation approach.
- Participation is measured by the number of rounds a player was present in (not just the number of questions answered). A player who joins mid-game and answers questions in 2 rounds has participation of 2 rounds.

## Dependencies

- **SPEC-006 (Answer Evaluation)**: Provides the answer-submission data (correct/incorrect, questions answered) used to evaluate MinimumAnsweredQuestions eligibility.
- **SPEC-007 (Scoring System)**: Provides the CONSOLATION PointTransactionType and the ledger infrastructure for recording consolation awards. Consolation awards are ledger entries, not direct balance mutations.
- **SPEC-009 (Rewards & Point Redemption)**: Provides the RewardRedemption model and reward catalog for reward-based consolation. Consolation rewards are created as APPROVED redemptions bypassing manual approval.
