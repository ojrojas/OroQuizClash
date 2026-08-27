# Phase 0 Research: Rewards & Point Redemption

**Date**: 2026-08-27 | **Plan**: [plan.md](plan.md)

All unknowns resolved. No NEEDS CLARIFICATION remains.

---

## R1. Funding source for redemptions — per-game player balance

**Decision**: A redemption is funded by the player's point balance **in a specific game**. `RedeemRewardCommand` carries `GameId`; the deduction uses the existing `Game.ConsumePoints(playerId, amount, reason)` (SPEC-007 US4), which creates a `REWARD_REDEMPTION` `PointTransaction` and updates `GamePlayer.Score`.

**Rationale**:
- Points in this codebase are per-game: `PointTransaction.GameId` is non-nullable, `PlayerScore` lives on `GamePlayer`, and the ledger is scoped to a game. There is no cross-game player wallet.
- SPEC-007 already implemented and tested `ConsumePoints` with `PointTransactionType.RewardRedemption` precisely for this feature ("Points Consumed for Reward Redemption", US4).
- Game concept §15 flow is "Withdrawal → Redeem points": players redeem points retained from a game (SPEC-008 dependency).
- The `Reward` catalog itself stays global and decoupled from games (game concept §15: "The reward should not be coupled to the game") — only the funding side references a game.

**Alternatives considered**:
- *Global player wallet aggregate (sum across games)*: rejected — requires a new cross-game ledger, breaks "balance reconstructable from ledger" per game, and deduction would need to choose which game's balance to drain. Out of scope and inconsistent with SPEC-007's design.
- *Redemption only after game finished/withdrawal*: rejected — SPEC-007's `ConsumePoints` imposes no game-state restriction, and the 009 spec does not require it; keeping it unrestricted preserves consistency with the already-tested operation.

---

## R2. Eligible points definition (RWD-002)

**Decision**: Eligible points = the player's `CurrentPoints` balance in the funding game at request time, consumed secured-first per SPEC-007's `PlayerScore.Consume` semantics. No separate "committed points" tracking is needed.

**Rationale**: Points are deducted **at request time** (see R3), so any pending redemption has already reduced the balance. A second redemption attempt is validated against the already-reduced balance — double-spending is intrinsically impossible. This makes RWD-002 enforceable by the existing `SufficientBalanceRule` inside `ConsumePoints`.

**Alternatives considered**:
- *Deduct at approval time, reserve at request time*: rejected — requires a reservation/commit ledger on top of the point ledger, plus re-validation at approval; adds state without adding business value for this domain.
- *Eligibility = SecuredPoints only*: rejected — SPEC-007's tested `ConsumePoints` validates against `CurrentPoints` and consumes secured-first; redefining eligibility would diverge from the implemented and tested SPEC-007 behavior.

---

## R3. Atomicity strategy (RWD-003)

**Decision**: The `RedeemRewardHandler` performs, within a single `IUnitOfWork.SaveChangesAsync()` transaction:
1. Load `Reward` → `reward.ReserveStock(now)` (validates active, in stock, not expired; decrements stock).
2. Load `Game` → `game.ConsumePoints(playerId, cost, reason)` (validates player-in-game + balance; deducts; creates ledger transaction).
3. Create `RewardRedemption` (REQUESTED) referencing player, reward, game, points, idempotency key.
4. One `SaveChangesAsync()` — domain events dispatch inside the same transaction (AppDbContextBase).

Any failure aborts before save; nothing is persisted partially. Concurrent redemptions racing for the last stock unit or the same balance conflict on `Reward.RowVersion` / `Game.RowVersion` → `DbUpdateConcurrencyException` → 409, with zero partial effects.

**Rationale**: Constitution §E requires transactions to protect multi-aggregate state changes; §F mandates optimistic concurrency for reward redemptions. Both aggregates already carry `RowVersion` patterns (Game has it; Reward gets it following Category/Question).

**Alternatives considered**:
- *Domain service merging both aggregates*: rejected — Application-layer orchestration with per-aggregate invariants is the established house pattern (e.g., `SubmitAnswer`, `AdjustScore` handlers) and keeps aggregates independent.
- *Pessimistic locking*: rejected — constitution prefers optimistic concurrency; SQLite/SQL Server dual-provider makes portable pessimistic locks awkward.

---

## R4. Refund mechanism for REJECTED / CANCELLED

**Decision**: New domain operation `Game.RefundPoints(Guid playerId, int amount, string reason)` returning `Result<PointTransaction>` — credits the player's balance (`PlayerScore.Award(amount, roundScoped: false)`) and appends a positive `ADJUSTMENT` transaction with reason `"Refund for redemption {redemptionId} ({REJECTED|CANCELLED})"`. Invoked by `RejectRedemption` / `CancelRedemption` handlers together with `Reward.ReleaseStock()`, in one transaction.

**Rationale**:
- Ledger is append-only (SPEC-007 FR-011) and the constitution fixes the 10 transaction types — no new `PointTransactionType` is introduced; `ADJUSTMENT` with a descriptive reason is the sanctioned reversible-entry mechanism.
- The existing `AdjustPoints` is the *administrative* operation (SPEC-007 FR-014, requires admin actor); player-initiated cancellation refunds must not require an admin, so a dedicated `RefundPoints` keeps business intent explicit and authorization semantics clean.
- `RefundPoints` validates `amount > 0` and player-in-game; it does not check game state (mirrors `ConsumePoints`/`AdjustPoints`), so refunds work even after the funding game finishes.

**Alternatives considered**:
- *Reuse `AdjustPoints` with the acting user as admin*: rejected — conflates admin adjustment with system refund; `AdjustPoints` signature demands `adminUserId`.
- *Negative REWARD_REDEMPTION reversal transaction*: rejected — no reversal semantics exist in the ledger; a positive ADJUSTMENT is simpler and fully auditable via reason.

---

## R5. Reward status model

**Decision**: `RewardStatus` Enumeration with two values: `ACTIVE(1)`, `INACTIVE(2)`. New rewards are created ACTIVE. `Deactivate()` and `Activate()` are reversible transitions. No delete operation exists — rewards with history are never destroyed (satisfies FR-002 trivially). Stock exhaustion and expiration are **not** statuses: they are evaluated at redemption time (`RewardAvailableRule`).

**Rationale**: Matches the user input (Reward.Status field) and the house pattern (`CategoryStatus` Enumeration). Keeping exhaustion/expiration out of the status avoids status-sync bugs (e.g., restock would need to "un-expire" a status).

**Alternatives considered**:
- *Statuses ACTIVE/INACTIVE/EXPIRED/OUT_OF_STOCK*: rejected — derived conditions stored as state drift out of sync with stock/expiration changes.
- *Soft delete flag*: rejected — deactivation covers the need; deletion is prohibited by spec when history exists, and no delete is exposed at all.

---

## R6. Redemption audit model (RWD-006)

**Decision**: `RewardRedemption` holds a collection of `RedemptionTransition` child entities: `(Status, ActorId, At)`. Every state change (creation included) appends one transition with the acting user's id (player `sub` or manager `sub`). Denormalized `RequestedAt` and `DeliveredAt` timestamps are kept on the aggregate root per the user-input entity definition. Full history is retrievable via the redemption's transitions.

**Rationale**: FR-015 requires who+when per transition and retrievable full history. A transition collection is append-only, simple to persist (child table), and matches the constitution's audit requirement without a separate audit service.

**Alternatives considered**:
- *Only per-state timestamps (RequestedAt/ApprovedAt/...)*: rejected — loses actor information and cannot distinguish e.g. who cancelled.
- *External audit table/service*: rejected — unnecessary abstraction (constitution: no unjustified abstractions); the ledger + transitions already provide full auditability.

---

## R7. Idempotency for duplicate redemption submissions (FR-017)

**Decision**: `RedeemRewardCommand` accepts an optional client-generated `IdempotencyKey` (Guid). `RewardRedemption` stores it; a unique filtered index on `IdempotencyKey` (non-null) enforces uniqueness. The handler checks for an existing redemption with the same key + player and returns it (success, no new deduction) instead of creating a duplicate. Without a key, each submission is a distinct request.

**Rationale**: Follows the SPEC-006 answer-submission idempotency pattern (`ValidateIdempotencyRule`, idempotency key on submission). Protects against network retries double-deducting points.

**Alternatives considered**:
- *Mandatory idempotency key*: rejected — burdens clients that don't need it; optional key matches existing house pattern.
- *Deduplication by (player, reward, time window)*: rejected — ambiguous and untestable; explicit key is deterministic.

---

## R8. Authorization model (FR-018)

**Decision**:
- Catalog read + redeem + own-history + cancel: any authenticated player (JWT `sub` = PlayerId; ownership enforced server-side).
- Catalog management (create/update/activate/deactivate) + redemption processing (approve/reject/deliver) + manager listing: new policy `AdminOrRewardManager` requiring role claim `ADMIN` or `REWARD_MANAGER`, registered in `Program.cs` exactly like the existing `AdminOrGameManager` policy.

**Rationale**: Constitution §H maps `Reward.Read/Redeem/Manage` to OroIdentityServer roles and enforces via JWT claims + local policies. The codebase already implements this pattern for `AdminOrGameManager`.

**Alternatives considered**:
- *Reuse `AdminOrGameManager` for reward management*: rejected — GAME_MANAGER should not imply reward authority; constitution lists REWARD_MANAGER as a distinct role.

---

## R9. Expiration semantics (RWD-005)

**Decision**: `Reward.ExpirationDate` is an optional `DateTimeOffset?`. Availability is evaluated with server UTC now at redemption time (server truth, principle V): expired ⇒ `RewardUnavailable` error. Expiration blocks **new** redemptions only; pending redemptions continue through approval/delivery/rejection normally (spec edge case).

**Rationale**: Server timestamps are mandated by constitution §V; the spec explicitly states expiration never invalidates already-requested redemptions.

**Alternatives considered**:
- *Background job auto-cancelling expired pending redemptions*: rejected — out of scope, adds worker infrastructure; spec says pending redemptions continue normally.

---

## R10. Stock semantics (RWD-004)

**Decision**: `Reward.Stock` is the count of remaining redeemable units (int ≥ 0). `ReserveStock()` validates `Stock > 0` (plus active + not expired) then decrements; `ReleaseStock()` increments (called on reject/cancel). Restocking happens via `UpdateReward` (set a new stock value ≥ 0). A reward with `Stock == 0` simply fails `RewardAvailableRule` — no separate status.

**Rationale**: Matches user input (Stock field) and RWD-004. Release-on-reject keeps stock accurate (spec edge case "stock unit is released").

**Alternatives considered**:
- *Separate reservation entity counting pending units*: rejected — the decrement-at-request + release-on-reject model is equivalent and simpler; concurrency is already handled by RowVersion.

---

## R11. Catalog query shape (FR-003)

**Decision**: `GET /api/rewards` returns rewards that are active, in stock, and not expired (player view), each with name, description, points required, remaining stock and expiration. Optional query param `gameId`: when present, the response includes the requesting player's available point balance in that game (from `GamePlayer.Score.CurrentPoints`). Optional `includeUnavailable=true` (manager policy) returns the full catalog with status for administration.

**Rationale**: Balances are per-game (R1), so a balance can only be shown relative to a funding game; making `gameId` optional keeps the catalog browsable without game context.

**Alternatives considered**:
- *Aggregate balance across all games in catalog response*: rejected — inconsistent with R1 (no cross-game wallet).
- *Separate endpoint for balance*: rejected — SPEC-007 already exposes `GetPlayerScore` per game; the optional param avoids an extra round-trip for the redemption screen.

---

## R12. Domain events & integration candidates

**Decision**: In-process domain events raised inside aggregates: `RewardCreatedDomainEvent`, `RewardUpdatedDomainEvent`, `RewardStatusChangedDomainEvent`, `RewardRedeemedDomainEvent` (redemption created), `RedemptionStatusChangedDomainEvent` (approve/reject/deliver/cancel). Dispatch happens in `SaveChanges` (AppDbContextBase). `RewardRedeemed` is a documented integration-event candidate (constitution §G) via Outbox when an EventBus is configured; current `NullEventBus` remains.

**Rationale**: Matches SPEC-007/008 event pattern and constitution §G flow (Command → Domain op → Domain events → Transaction + Outbox).

**Alternatives considered**:
- *Direct RabbitMQ publication from handlers*: rejected — forbidden by constitution (no external publication before commit).
