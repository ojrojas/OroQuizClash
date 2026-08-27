# Data Model: Player Withdrawal

**Feature**: 008-player-withdrawal
**Date**: 2026-08-27

## Entities

### PlayerParticipationStatus (NEW — Enumeration)

Participation lifecycle states for a player within a game.

| Id | Name | Description |
|----|------|-------------|
| 1 | ACTIVE | Player is actively participating |
| 2 | WITHDRAWN | Player voluntarily exited |
| 3 | ELIMINATED | Player forced out by game rules |
| 4 | WINNER | Player finished with highest score |

**Transition rules**:

```
ACTIVE → WITHDRAWN    (Game.WithdrawPlayer)
ACTIVE → ELIMINATED   (Game.EliminatePlayer)
ACTIVE → WINNER       (Game.Finish — max score among active players)
WITHDRAWN → ∅         (terminal)
ELIMINATED → ∅        (terminal)
WINNER → ∅            (terminal)
```

**Invariants**:
- Only ACTIVE players can transition
- No transitions out of WITHDRAWN, ELIMINATED, or WINNER
- Exactly one status per player at any time

---

### GamePlayer (EXTEND existing)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Id | GamePlayerId (Guid) | PK | Existing |
| GameId | GameId (Guid) | FK → Game | Existing |
| UserId | Guid | NOT NULL | Existing |
| JoinedAt | DateTimeOffset | NOT NULL | Existing |
| DisplayName | string? | NULLABLE | Existing |
| Score | PlayerScore | NOT NULL | Existing (SPEC-007) |
| ParticipationStatus | PlayerParticipationStatus | NOT NULL, DEFAULT ACTIVE | NEW — replaces IsWithdrawn |
| ExitedAt | DateTimeOffset? | NULLABLE | NEW — set on withdrawal/elimination |

**Removed fields** (replaced):
- ~~IsWithdrawn (bool)~~ → kept as computed property: `ParticipationStatus == WITHDRAWN`
- ~~WithdrawnAt (DateTimeOffset?)~~ → replaced by `ExitedAt`

**Invariants**:
- `ParticipationStatus` starts as ACTIVE on join
- `ExitedAt` is set if and only if status is WITHDRAWN or ELIMINATED
- WINNER status does not set `ExitedAt` (participation ended via game completion)

---

### WithdrawalRecord (conceptual — realized via existing structures)

The spec's WithdrawalRecord is realized by the combination of:
- `PointTransaction` of type WITHDRAWAL (audit: player, game, points deducted, resulting balance, timestamp, policy in Reason)
- `GamePlayer.ParticipationStatus = WITHDRAWN` + `ExitedAt` (audit: when)
- `PlayerWithdrawnDomainEvent` (notification: retained points, policy)

No new table/entity needed — the ledger + player state provide full traceability (FR-003, SC-005).

---

## Domain Operations

### Game.WithdrawPlayer(playerId) — EXTEND existing

**Validation sequence** (FR-006 through FR-010):

| Step | Check | Error |
|------|-------|-------|
| 1 | Game not in terminal state | InvalidGameState |
| 2 | Player exists in game | PlayerNotInGame |
| 3 | Status != WITHDRAWN | PlayerAlreadyWithdrawn |
| 4 | Status != ELIMINATED | PlayerAlreadyEliminated |
| 5 | Status == ACTIVE | ParticipationAlreadyFinished |

**Effects** (atomic, FR-016):
1. Resolve withdrawal policy strategy from GameConfiguration
2. Calculate deduction, apply to PlayerScore
3. Create WITHDRAWAL PointTransaction
4. Set ParticipationStatus = WITHDRAWN, ExitedAt = now
5. Raise PlayerWithdrawnDomainEvent + ScoreUpdatedDomainEvent

### Game.EliminatePlayer(playerId, reason) — NEW

**Validation**:
- Game not terminal
- Player exists
- Status == ACTIVE (otherwise PlayerAlreadyWithdrawn / PlayerAlreadyEliminated)

**Effects**:
1. Set ParticipationStatus = ELIMINATED, ExitedAt = now
2. Raise PlayerEliminatedDomainEvent (optional, for future notification)

**Note**: No automatic triggers in this spec — operation exposed for SPEC-009/010.

### Game.Finish() — EXTEND existing

After GAME_BONUS/CONSOLATION awards (SPEC-007 logic), add:
1. Determine max score among ACTIVE players (non-withdrawn, non-eliminated)
2. Set ParticipationStatus = WINNER for all ACTIVE players with max score
3. Then transition game to FINISHED

---

## Events

### PlayerWithdrawnDomainEvent (NEW)

| Field | Type | Notes |
|-------|------|-------|
| GameId | Guid | Game reference |
| PlayerId | Guid | Withdrawing player |
| RetainedPoints | int | Points kept after policy |
| PolicyName | string | Applied withdrawal policy |

---

## Relationships

```
Game (AggregateRoot)
└── GamePlayer (Entity) [1..*]
    ├── PlayerScore (ValueObject, owned) — SPEC-007
    └── ParticipationStatus (Enumeration) — THIS SPEC
```

## Validation Rules

| Rule | Applies To | Description |
|------|-----------|-------------|
| PlayerAlreadyEliminatedRule | WithdrawPlayer | Cannot withdraw an eliminated player |
| ParticipationAlreadyFinishedRule | WithdrawPlayer | Cannot withdraw if participation already finished (non-ACTIVE) |
| (existing) PlayerNotWithdrawnRule | AwardPoints, RemovePoints, SecurePoints | Scoring excluded for non-ACTIVE players |

## Exclusion Matrix (FR-013)

| Action | ACTIVE | WITHDRAWN | ELIMINATED | WINNER |
|--------|--------|-----------|------------|--------|
| Answer questions | ✅ | ❌ | ❌ | ❌ |
| Receive round points | ✅ | ❌ | ❌ | ❌ |
| Secure points | ✅ | ❌ | ❌ | ❌ |
| Round bonus | ✅ | ❌ | ❌ | ❌ |
| Game bonus | ✅ | ❌ | ❌ | ❌ |
| Consolation | ✅ (if eligible) | ❌ | ❌ | ❌ |
| Winner determination | ✅ | ❌ | ❌ | — |
| Withdraw | ✅ | ❌ | ❌ | ❌ |
