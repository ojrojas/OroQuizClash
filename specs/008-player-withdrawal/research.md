# Research: Player Withdrawal

**Feature**: 008-player-withdrawal
**Date**: 2026-08-27

## R1: IsWithdrawn → PlayerParticipationStatus Migration

**Decision**: Replace `GamePlayer.IsWithdrawn` (bool) + `WithdrawnAt` with `ParticipationStatus` (Enumeration: ACTIVE/WITHDRAWN/ELIMINATED/WINNER) + `ExitedAt` (DateTimeOffset?). Keep `IsWithdrawn` as a computed convenience property (`ParticipationStatus == Withdrawn`) to avoid breaking existing SPEC-007 code.

**Rationale**:
- SPEC-007 already uses `IsWithdrawn` in `CompleteRound`, `Finish`, `AwardPoints`, and query endpoints.
- A computed property preserves backward compatibility while the Enumeration becomes the source of truth.
- EF maps the Enumeration as an int column; `ExitedAt` replaces `WithdrawnAt` (covers both withdrawal and elimination timestamps).

**Alternatives considered**:
- Keep bool + add separate flags (IsEliminated, IsWinner): Rejected — multiple booleans allow invalid combinations (withdrawn AND eliminated).
- Separate PlayerParticipation entity: Rejected — participation is inseparable from GamePlayer; adds join complexity without benefit.

## R2: Participation Status Transitions

**Decision**: State machine with protected transitions:

```
ACTIVE → WITHDRAWN   (voluntary withdrawal)
ACTIVE → ELIMINATED  (forced exit by game rules)
ACTIVE → WINNER      (game finish, highest score)
WITHDRAWN → (terminal, no outgoing transitions)
ELIMINATED → (terminal, no outgoing transitions)
WINNER → (terminal, no outgoing transitions)
```

**Rationale**:
- FR-012 requires WITHDRAWN and ELIMINATED to be terminal participation states.
- WINNER is assigned only at game finish, only to ACTIVE players.
- Multiple WINNERS possible in case of ties (all tied top-scorers receive WINNER status).

**Alternatives considered**:
- Single winner only (tie-break by earliest finish): Rejected — no tie-break rule specified; awarding all tied players is fairer and simpler.
- WINNER as separate entity/flag: Rejected — it's a participation outcome, belongs on the status enum.

## R3: Elimination Scope

**Decision**: Introduce `Game.EliminatePlayer(Guid playerId, string reason)` as a domain operation that transitions ACTIVE → ELIMINATED, but NO automatic elimination triggers are implemented in this spec. Elimination rules (e.g., incorrect answer under LOSE_ALL in tournament mode) are deferred to SPEC-009/010.

**Rationale**:
- The spec explicitly lists ELIMINATED as a status and forbids withdrawal after elimination, but does not define what causes elimination.
- Providing the domain operation + validation now enables future specs without rework.
- Avoids inventing elimination rules not in the spec.

**Alternatives considered**:
- Auto-eliminate on LOSE_ALL incorrect answer: Rejected — contradicts SPEC-007 where LOSE_ALL just zeroes the score; player continues playing.
- Skip ELIMINATED entirely: Rejected — spec explicitly requires it as a status and validation case.

## R4: Mid-Round Withdrawal Behavior

**Decision**: Withdrawal is allowed at any point during an active game, including while a round is in progress. The withdrawing player's current question is simply abandoned — no answer is recorded, no extra penalty beyond the withdrawal policy.

**Rationale**:
- Spec edge case explicitly states this behavior.
- The round continues for remaining players.
- The withdrawal policy already handles the point consequences.

**Alternatives considered**:
- Block withdrawal mid-round: Rejected — spec explicitly allows it.
- Record an incorrect answer for the abandoned question: Rejected — would double-penalize (policy + loss policy).

## R5: Last Active Player Behavior

**Decision**: When all but one player withdraw, the game continues with the single active player. When ALL players withdraw, the game remains in its current state until an admin force-finishes or cancels it (existing SPEC-004 operations).

**Rationale**:
- Spec edge case: "game continues with that single active player."
- No automatic game-end rule is specified for zero active players; existing Cancel/ForceFinish operations cover it.
- Avoids inventing new game lifecycle transitions.

**Alternatives considered**:
- Auto-finish when zero active players: Rejected — not specified; would require new lifecycle transition rules.
- Auto-cancel: Rejected — same reason; admin decision preserved.

## R6: Winner Determination Timing

**Decision**: Winners are determined inside `Game.Finish()` — after GAME_BONUS/CONSOLATION awards (SPEC-007), before the status transition to FINISHED. All non-withdrawn, non-eliminated players with the maximum score receive WINNER status.

**Rationale**:
- Winner determination needs final scores (after all bonuses).
- Players who withdrew or were eliminated are excluded from winning (FR-013).
- Note: GAME_BONUS is awarded to all non-withdrawn players, which can change relative rankings — winner is determined AFTER bonuses to reflect final scores.

**Alternatives considered**:
- Determine winner before bonuses: Rejected — bonuses are part of final score; ranking should reflect the true final state.
- Separate DetermineWinners command: Rejected — winner is an outcome of game completion, not a separate action.

## R7: Withdrawal Validation Order

**Decision**: Validation follows the spec flow exactly: ValidateGameState → ValidatePlayer → (new) ValidateParticipationStatus → CalculateSecuredPoints → PlayerWithdrawn → FinishPlayerParticipation.

Validation sequence and errors:
1. Game terminal state → `InvalidGameState` (FR-008)
2. Player not in game → `PlayerNotInGame` (FR-010)
3. Player already withdrawn → `PlayerAlreadyWithdrawn` (FR-006)
4. Player eliminated → `PlayerAlreadyEliminated` (FR-007)
5. Participation finished → `ParticipationAlreadyFinished` (FR-009)

**Rationale**:
- Matches the spec flow diagram.
- Game-level validation first (cheapest, most common failure).
- Specific error codes per FR-015.

**Alternatives considered**:
- Single generic "cannot withdraw" error: Rejected — FR-015 requires specific reasons.

## R8: ParticipationAlreadyFinished Semantics

**Decision**: "Participation finished" means the player's status is WINNER (participation ended via game completion). WITHDRAWN and ELIMINATED have their own specific errors. In practice, once a game finishes, the terminal-game check (step 1) catches most cases; the participation-finished check covers the edge case where status is queried independently.

**Rationale**:
- After `Game.Finish()`, the game is terminal → step 1 rejects withdrawal anyway.
- The participation-finished rule exists for completeness and defense-in-depth.
- Keeps all 5 rejection cases testable per FR-015.

**Alternatives considered**:
- Merge with PlayerAlreadyWithdrawn: Rejected — spec lists them as separate cases.

## R9: Concurrent Withdrawal + Game Finish

**Decision**: Rely on optimistic concurrency (`Game.RowVersion`). Both operations load the aggregate, mutate, and save — the second save fails with `DbUpdateConcurrencyException` → mapped to 409 Conflict. Client retries and gets the updated state (e.g., withdrawal retry after finish → rejected with "game finished").

**Rationale**:
- Constitution Constraint F mandates optimistic concurrency.
- SC-007 requires zero inconsistent states — rowversion guarantees this.
- Retry-then-reject pattern is standard and user-friendly.

**Alternatives considered**:
- Pessimistic locking: Rejected — constitution preference for optimistic.
- Eventual consistency with compensation: Rejected — over-engineered for this case.

## R10: PlayerWithdrawnDomainEvent

**Decision**: New `PlayerWithdrawnDomainEvent(GameId, PlayerId, RetainedPoints, PolicyName)` raised inside `WithdrawPlayer`. Remaining players are notified via existing real-time infrastructure (SignalR in future specs); this spec only raises the in-process domain event.

**Rationale**:
- US4 requires remaining players to be informed.
- Domain event is the constitutional pattern (Principle G) — in-process dispatch at SaveChanges.
- SignalR hub integration is out of scope (no hub exists yet); the event enables it later.

**Alternatives considered**:
- Direct SignalR notification from handler: Rejected — couples Application to real-time infra; domain event is the decoupled pattern.
- No event: Rejected — US4 requires notification capability.
