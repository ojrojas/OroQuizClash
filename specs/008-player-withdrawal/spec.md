# Feature Specification: Player Withdrawal

**Feature Branch**: `008-player-withdrawal`

**Created**: 2026-08-27

**Status**: Draft

**Input**: User description: "008 — Player Withdrawal: Permitir que un jugador decida voluntariamente terminar su participación y conservar los puntos elegibles. Flujo: Jugador → Withdraw → ValidateGameState → ValidatePlayer → CalculateSecuredPoints → PlayerWithdrawn → FinishPlayerParticipation. Regla central: Continuar jugando = riesgo; Retirarse = conservar puntos elegibles. Estados: ACTIVE, WITHDRAWN, ELIMINATED, WINNER. No permitido: retirarse después de finalizar su participación, después de recibir una eliminación, después de terminar el juego, o dos veces. Dependencias: SPEC-004, SPEC-007, SPEC-009, SPEC-010"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Voluntary Withdrawal Preserves Eligible Points (Priority: P1)

A player in an active game decides to stop playing and withdraw. The system validates the request, calculates the points the player is eligible to keep (according to the game's configured withdrawal policy), marks the player as withdrawn, and ends their participation. The player keeps their eligible points and is no longer able to play rounds, answer questions, or receive scoring events.

**Why this priority**: This is the core feature — the risk/reward decision ("continue playing = risk, withdraw = keep eligible points") is a central game mechanic that gives players agency over their earned points.

**Independent Test**: Can be fully tested by having an active player with accumulated points withdraw and verifying they retain the policy-eligible points, their participation ends, and a withdrawal record is created.

**Acceptance Scenarios**:

1. **Given** a player in an active game with 500 points (300 secured, 200 unsecured) and a "keep secured score" withdrawal policy, **When** the player withdraws, **Then** the player keeps 300 points, is marked as WITHDRAWN, and a withdrawal record documents the deduction of 200 points.
2. **Given** a player who withdraws successfully, **When** the withdrawal completes, **Then** the player's participation is finished: they cannot answer questions, join rounds, or receive further point awards/deductions.
3. **Given** a player who withdraws, **When** the withdrawal is processed, **Then** the player sees a clear confirmation of their final retained points and the policy applied.
4. **Given** a player with a "keep current score" policy and 500 points, **When** the player withdraws, **Then** the player keeps all 500 points.

---

### User Story 2 - Withdrawal Validation Prevents Invalid Withdrawals (Priority: P1)

The system rejects withdrawal attempts that violate the game rules: withdrawing twice, withdrawing after the game has ended, withdrawing after being eliminated, or withdrawing after participation has already finished. Each rejection returns a clear, specific reason.

**Why this priority**: Equally critical to the happy path — without strict validation, players could exploit withdrawal (e.g., withdraw after winning, double-withdraw to duplicate point retention).

**Independent Test**: Can be tested by attempting withdrawals in each forbidden state and verifying each is rejected with the correct reason and no state change.

**Acceptance Scenarios**:

1. **Given** a player who has already withdrawn, **When** they attempt to withdraw again, **Then** the request is rejected with "already withdrawn" and no state changes.
2. **Given** a game that has finished (terminal state), **When** a player attempts to withdraw, **Then** the request is rejected with "game already finished".
3. **Given** a player who has been eliminated, **When** they attempt to withdraw, **Then** the request is rejected with "player already eliminated".
4. **Given** a player whose participation has already finished, **When** they attempt to withdraw, **Then** the request is rejected with "participation already finished".
5. **Given** a player not in the game, **When** they attempt to withdraw, **Then** the request is rejected with "player not in game".

---

### User Story 3 - Player Participation Status Lifecycle (Priority: P2)

Each player in a game has a participation status that transitions through a well-defined lifecycle: ACTIVE (playing), WITHDRAWN (voluntary exit), ELIMINATED (forced exit by game rules), WINNER (finished first/highest). The status is visible to the player and other players, and governs what actions the player can still take.

**Why this priority**: The status model underpins withdrawal, elimination, and winner determination — needed for correct game flow and transparency, but the withdrawal action itself (US1/US2) delivers value first.

**Independent Test**: Can be tested by verifying each status transition occurs only from valid prior states and that status is queryable for any player in a game.

**Acceptance Scenarios**:

1. **Given** a player who joins a game, **When** the game starts, **Then** their participation status is ACTIVE.
2. **Given** an ACTIVE player who withdraws, **When** the withdrawal completes, **Then** their status becomes WITHDRAWN permanently.
3. **Given** a player's participation status, **When** queried, **Then** it reflects exactly one of ACTIVE, WITHDRAWN, ELIMINATED, or WINNER.
4. **Given** a WITHDRAWN or ELIMINATED player, **When** any game action is attempted on their behalf, **Then** it is rejected (they are no longer participating).

---

### User Story 4 - Withdrawal Impact on Remaining Players (Priority: P2)

When a player withdraws, the game continues for the remaining active players. The withdrawn player is excluded from round participation, leaderboard contention for future bonuses, and any end-of-game awards. Remaining players are informed that a player has left.

**Why this priority**: Multiplayer continuity is essential for a correct game experience but depends on the core withdrawal mechanic.

**Independent Test**: Can be tested by withdrawing one player from a multi-player game and verifying remaining players can continue playing and the withdrawn player is excluded from subsequent game events.

**Acceptance Scenarios**:

1. **Given** a 3-player game, **When** one player withdraws, **Then** the remaining 2 players can continue answering and scoring normally.
2. **Given** a withdrawn player, **When** a new round starts, **Then** the withdrawn player is not expected to answer and receives no round points or bonuses.
3. **Given** a withdrawn player, **When** the game finishes, **Then** they receive no game-completion bonus and are not eligible for winning.
4. **Given** a game where all but one player have withdrawn, **When** the remaining player continues, **Then** the game proceeds with that single active player.

---

### Edge Cases

- What happens when a player attempts to withdraw while a round is in progress? (Withdrawal is allowed; their current-round unanswered question is simply abandoned — no penalty beyond the configured withdrawal policy.)
- What happens when the last active player withdraws? (The game continues in a state with no active players; game completion/force-finish rules from SPEC-004 govern the outcome.)
- What happens when a player with zero points withdraws? (Withdrawal succeeds; a withdrawal record is created documenting zero deduction; final score is zero.)
- What happens when two players withdraw simultaneously? (Each withdrawal is processed independently and atomically; both succeed if both were ACTIVE.)
- What happens when a withdrawal request arrives concurrently with game finish? (Only one operation succeeds; the other is rejected with a conflict, and the player's final state is consistent.)
- What happens when a player is eliminated and then the game finishes? (ELIMINATED status is preserved; they are excluded from winner determination and completion bonuses.)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow an ACTIVE player in a non-terminal game to voluntarily withdraw from the game.
- **FR-002**: System MUST calculate the player's eligible retained points using the game's configured withdrawal policy upon withdrawal.
- **FR-003**: System MUST record the withdrawal as an auditable event including the player, game, applied policy, points deducted, points retained, and timestamp.
- **FR-004**: System MUST transition the player's participation status to WITHDRAWN upon successful withdrawal.
- **FR-005**: System MUST end the player's participation upon withdrawal — no further answers, rounds, scoring, or bonuses for that player.
- **FR-006**: System MUST reject withdrawal from a player who is already WITHDRAWN (no double withdrawal).
- **FR-007**: System MUST reject withdrawal from a player who is ELIMINATED.
- **FR-008**: System MUST reject withdrawal when the game is in a terminal state (finished, cancelled, or force-finished).
- **FR-009**: System MUST reject withdrawal from a player whose participation has already finished.
- **FR-010**: System MUST reject withdrawal from a player who is not part of the game.
- **FR-011**: System MUST track each player's participation status as one of: ACTIVE, WITHDRAWN, ELIMINATED, WINNER.
- **FR-012**: Participation status transitions MUST be protected — WITHDRAWN and ELIMINATED are terminal participation states (no further transitions except WINNER determination for ACTIVE players at game end).
- **FR-013**: System MUST exclude withdrawn players from round participation, future scoring events, game-completion bonuses, and winner determination.
- **FR-014**: System MUST allow the game to continue for remaining ACTIVE players after a withdrawal.
- **FR-015**: Each rejection MUST return a clear, specific reason distinguishing: already withdrawn, already eliminated, game finished, participation finished, player not in game.
- **FR-016**: Withdrawal MUST be atomic — the policy calculation, point adjustment, status change, and participation end happen as a single consistent operation.
- **FR-017**: Withdrawal MUST be server-authoritative — only the authenticated player themselves (or an administrator) can trigger their withdrawal; client-supplied withdrawal outcomes are never trusted.

### Key Entities

- **PlayerParticipation**: Represents a player's involvement in a specific game. Attributes: player reference, game reference, participation status (ACTIVE/WITHDRAWN/ELIMINATED/WINNER), joined timestamp, withdrawn timestamp (when applicable), final retained points.
- **WithdrawalRecord**: Auditable record of a withdrawal event. Attributes: player reference, game reference, applied withdrawal policy, points before withdrawal, points deducted, points retained, timestamp.
- **ParticipationStatus**: The four participation states (ACTIVE, WITHDRAWN, ELIMINATED, WINNER) with protected transitions.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of successful withdrawals retain exactly the policy-eligible points — zero calculation discrepancies across all configured withdrawal policies.
- **SC-002**: 100% of forbidden withdrawal attempts (double, post-elimination, post-game-end, post-participation) are rejected with the correct specific reason and zero state corruption.
- **SC-003**: Withdrawn players receive zero subsequent scoring events, bonuses, or round participation in 100% of cases.
- **SC-004**: Players can complete the withdrawal decision in under 5 seconds (from request to confirmation).
- **SC-005**: Every withdrawal produces exactly one auditable withdrawal record with full traceability (player, policy, points, timestamp).
- **SC-006**: Remaining players experience no interruption — game continues normally within the same round/turn cadence after a withdrawal.
- **SC-007**: Concurrent withdrawal and game-finish operations never produce inconsistent player states (zero race-condition failures).
- **SC-008**: Player participation status is queryable and accurate at any point during or after the game for all players.

## Assumptions

- The withdrawal policy (how many points are retained) is configured per game at creation time (SPEC-001/004) and applied at withdrawal (calculation mechanics defined in SPEC-007).
- Point deduction/retention mechanics and ledger recording are handled by the scoring system (SPEC-007); this spec defines the withdrawal decision flow and participation lifecycle.
- Elimination (ELIMINATED status) is triggered by game rules outside this spec (e.g., incorrect answer under LOSE_ALL policy or tournament rules) — this spec only ensures eliminated players cannot withdraw.
- Winner determination (WINNER status) happens at game completion based on final scores — detailed winner rules are part of game completion (SPEC-004) and reward eligibility (SPEC-009).
- A player can withdraw at any point during an active game, including mid-round (their current question is abandoned without extra penalty beyond the withdrawal policy).
- Withdrawal is irreversible — once WITHDRAWN, a player cannot rejoin the same game.
- The game can continue with fewer players down to a single active player; game-level rules for minimum players apply only at game start.
- Administrative force-removal of a player is out of scope for this spec (covered by game administration rules).

## Dependencies

- **SPEC-004 (Game Lifecycle)**: Provides game states (terminal vs. active), game completion, and the context in which withdrawal occurs.
- **SPEC-007 (Scoring System)**: Provides withdrawal policy application, point deduction/retention mechanics, and WITHDRAWAL ledger transactions.
- **SPEC-009 (Rewards)**: Consumes participation status for reward eligibility (withdrawn/eliminated players excluded).
- **SPEC-010 (Consolation)**: Consumes participation status for consolation eligibility.
