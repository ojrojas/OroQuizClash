# Realtime: Private Session Isolation (Player)

**Normative flow (Addendum 2 §11):** `Backend State → Realtime Event → Client State → UI`

## Principles

1. Each player has a **private session**: `Session A/B/C → Angular Screen A/B/C` on the same `GameId` (FR-025).
2. The client **never infers** authoritative state from animation or local timer; timeout/evaluation only via server event (§11, server truth V).
3. UI never calls DB — Angular → `QuizArena.Api` → SignalR hub; Admin Blazor likewise (FR-023).

## Event Routing (SPEC-012 SignalR groups)

| Scope | Events | Group target | Rendered in |
|-------|--------|--------------|-------------|
| GLOBAL | `GameStarted`, `RoundStarted`, `RoundCompleted`, `GameFinished` | `game:{gameId}` | All player screens + Admin live-games |
| PLAYER-SPECIFIC | `PlayerQuestionPresented`, `PlayerAnswerAccepted`, `PlayerAnswerEvaluated`, `PlayerScoreUpdated`, `PlayerWithdrawalAccepted`, `PlayerEliminated`, `PlayerRewardAvailable` | `player:{playerId}` / `connection:{connectionId}` | ONLY that player's screen |

## Public vs Private Information

| Public (leaderboard/lobby) | Private (per-player only) |
|----------------------------|---------------------------|
| Round number, players remaining | My current question presentation |
| Aggregate scores/rank/name | My answer, my locked state |
| Game status, reward pool | My score detail, secured points |
| Winner announcement (end) | My timer, my withdraw state, my reward |

## UI Rules

- `AnswerOption.correct` styling gated on `PlayerAnswerEvaluated` receipt — never on local timeout.
- Timer expiry visual (`expired` state) only after server event; local countdown is display-only.
- Withdraw button enabled only after `PlayerScoreUpdated` confirms round ≥1 secured.
- Reconnection: on SignalR reconnect, client requests state snapshot (GET) before resuming events; stale UI shows `Reconnecting` Badge, inputs disabled.
- Admin screens consume GLOBAL only; PLAYER-SPECIFIC groups are never joined by Admin hubs (privacy).

## State Sync Contract

- Events are idempotent (carry `roundNumber`/`eventId`); UI discards out-of-order/stale events.
- Optimistic UI allowed ONLY for answer selection (`AnswerSelected`); reverts on missing `PlayerAnswerAccepted` within 2s (Toast error).
