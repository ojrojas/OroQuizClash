# Feature Specification: Rewards & Point Redemption

**Feature Branch**: `009-reward-redemption`

**Created**: 2026-08-27

**Status**: Draft

**Input**: User description: "009 — Rewards. Gestionar premios y canjes mediante puntos. Reward: Id, Name, Description, PointsRequired, Stock, Status, ExpirationDate. RewardRedemption: Player, Reward, Points, Status, RequestedAt, DeliveredAt. Estados: REQUESTED, APPROVED, REJECTED, DELIVERED, CANCELLED. Reglas: RWD-001 el jugador debe tener puntos suficientes; RWD-002 los puntos deben ser elegibles para canje; RWD-003 la redención debe ser atómica; RWD-004 no puede redimirse un premio agotado; RWD-005 no puede redimirse un premio expirado; RWD-006 una redención debe ser auditable. Dependencias: SPEC-007, SPEC-008, SPEC-010"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Player Redeems a Reward with Points (Priority: P1)

A player who has accumulated points from gameplay browses the reward catalog, sees which rewards they can afford with their available points, and redeems a reward they want. The system immediately validates that the player has enough eligible points, that the reward is still available (in stock, not expired, still active), and then atomically deducts the points, reduces the reward stock, and records the redemption request. The player sees a confirmation and can track the request from that moment on.

**Why this priority**: Redemption is the core value of the rewards system — it is the moment where points earned in gameplay convert into tangible value for the player. Without it, the catalog and the approval workflow have no purpose. A viable MVP exists as soon as a player can exchange points for a reward safely.

**Independent Test**: Can be fully tested by seeding an active reward with stock, giving a player enough points, submitting a redemption, and verifying that points are deducted, stock decreases by one, and a redemption record in REQUESTED state exists — delivering the complete "points → prize" value in isolation.

**Acceptance Scenarios**:

1. **Given** an active reward costing 100 points with stock available and a player with 150 eligible points, **When** the player redeems the reward, **Then** the system deducts 100 points, reduces stock by one, and creates a redemption in REQUESTED state with a timestamp.
2. **Given** a player with 50 eligible points and a reward costing 100 points, **When** the player attempts to redeem it, **Then** the redemption is rejected with a clear "insufficient points" outcome and neither points nor stock change.
3. **Given** a reward with zero stock, **When** any player attempts to redeem it, **Then** the redemption is rejected with an "out of stock" outcome and nothing changes.
4. **Given** a reward whose expiration date is in the past, **When** a player attempts to redeem it, **Then** the redemption is rejected with an "expired reward" outcome and nothing changes.
5. **Given** two players each holding exactly enough points for the last unit of a reward, **When** both attempt to redeem it at the same time, **Then** exactly one redemption succeeds and the other receives an "out of stock" outcome.
6. **Given** a player with 100 eligible points, **When** the player redeems a 100-point reward and then immediately attempts a second redemption of the same reward, **Then** the second redemption is rejected for insufficient points because the first redemption committed those points.

---

### User Story 2 - Redemption Review, Delivery, and Resolution (Priority: P2)

Once a redemption is requested, a reward manager reviews it and approves or rejects it. When approved and the prize is handed over, the manager marks it as delivered, completing the lifecycle. If the request is rejected, the player's points are automatically returned. The player can also cancel their own request while it has not been delivered, which also returns the points. Every transition is recorded with who performed it and when.

**Why this priority**: Approval and delivery turn a point deduction into a fulfilled prize and protect the business from invalid or fraudulent requests. It is essential for real operations but builds directly on top of the redemption capability from US1.

**Independent Test**: Can be tested by taking an existing REQUESTED redemption, approving it, marking it delivered, and verifying the DELIVERED state with delivery timestamp; separately, rejecting or cancelling a request and verifying the exact point refund — each path verifiable given a seeded redemption.

**Acceptance Scenarios**:

1. **Given** a redemption in REQUESTED state, **When** a reward manager approves it, **Then** the redemption moves to APPROVED and the transition is recorded with the manager's identity and timestamp.
2. **Given** a redemption in APPROVED state, **When** a reward manager marks it delivered, **Then** the redemption moves to DELIVERED with a delivery timestamp, and the point deduction becomes final.
3. **Given** a redemption in REQUESTED state, **When** a reward manager rejects it, **Then** the redemption moves to REJECTED and the player's points are automatically returned in full and recorded.
4. **Given** a redemption in REQUESTED or APPROVED state, **When** the owning player cancels it, **Then** the redemption moves to CANCELLED, the points are returned in full and recorded, and the stock unit is released.
5. **Given** a redemption in DELIVERED, REJECTED, or CANCELLED state, **When** anyone attempts to change its state, **Then** the transition is refused as the lifecycle is terminal.
6. **Given** a redemption in REQUESTED state, **When** a player who does not own it attempts to cancel it, **Then** the action is refused.

---

### User Story 3 - Reward Catalog Management (Priority: P3)

A reward manager administers the reward catalog: creating new rewards with a name, description, point cost, initial stock, and optional expiration date; updating details of existing rewards; and deactivating rewards so they can no longer be redeemed. Players only ever see rewards that are active, in stock, and not expired, together with their own available point balance so they know what they can afford.

**Why this priority**: Catalog management makes the system operable without seeding data, but the redemption flow (US1) can be demonstrated with pre-existing rewards, so this is the natural third slice.

**Independent Test**: Can be tested by creating a reward through the management flow, verifying it appears in the player-facing catalog; then deactivating it and verifying it disappears from the player-facing catalog while existing pending redemptions remain intact.

**Acceptance Scenarios**:

1. **Given** a reward manager, **When** they create a reward with name, description, point cost, stock, and expiration date, **Then** the reward exists in ACTIVE state and becomes visible in the player catalog.
2. **Given** an active reward, **When** a reward manager deactivates it, **Then** players can no longer redeem it, it no longer appears as redeemable in the catalog, and existing pending redemptions can still be processed to completion.
3. **Given** a reward, **When** a reward manager updates its description or restocks it, **Then** the changes are reflected immediately in the catalog.
4. **Given** a reward with pending or delivered redemptions, **When** a reward manager attempts to delete it, **Then** the deletion is refused — rewards with redemption history are never destroyed, only deactivated.
5. **Given** a player browsing the catalog, **When** they request the list of rewards, **Then** they see each reward's name, description, point cost, availability, and their own available point balance.

---

### Edge Cases

- What happens when two redemption requests race for the last stock unit? Exactly one succeeds; the other fails cleanly with no partial effects (RWD-003, RWD-004).
- What happens when a player's points are committed to a pending redemption and they try to redeem again? Committed points are not eligible; the second request fails for insufficient eligible points (RWD-002).
- What happens when a reward expires while a redemption is still pending? Expiration blocks new redemptions; already-requested redemptions continue through approval/delivery or rejection normally.
- What happens when a rejected or cancelled redemption had reduced stock? The stock unit is released and becomes redeemable again.
- What happens when the same redemption request is submitted twice (duplicate)? The duplicate is treated as the same request — no double deduction, no double stock reduction.
- What happens when a reward manager tries to approve a redemption whose reward was deactivated after the request? Pending redemptions remain processable; deactivation does not invalidate requests already made.
- What happens when a reward's point cost is changed while redemptions are pending? Pending redemptions keep the point amount recorded at request time; the new cost applies only to future redemptions.
- What happens when stock is restocked from zero? The reward becomes redeemable again immediately for new requests.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow reward managers to create rewards with a name, description, required points, initial stock, and an optional expiration date; new rewards start in an active state.
- **FR-002**: System MUST allow reward managers to update reward details (name, description, required points, stock, expiration date) and to activate/deactivate rewards; rewards with redemption history MUST NOT be deletable, only deactivatable.
- **FR-003**: System MUST present players a catalog containing only rewards that are active, in stock, and not expired, showing each reward's name, description, required points, and availability, alongside the player's available point balance.
- **FR-004**: System MUST validate at redemption time that the player has sufficient points to cover the reward's cost (RWD-001).
- **FR-005**: System MUST consider only eligible points for redemption (RWD-002): points available in the player's balance that are not already committed to pending (requested or approved, undelivered) redemptions.
- **FR-006**: System MUST execute redemption atomically (RWD-003): point deduction, stock reduction, and redemption record creation MUST all succeed together or have no effect at all.
- **FR-007**: System MUST reject redemption of a reward with zero available stock (RWD-004).
- **FR-008**: System MUST reject redemption of a reward whose expiration date has passed (RWD-005).
- **FR-009**: System MUST reject redemption of a deactivated reward.
- **FR-010**: System MUST deduct the reward's point cost from the player's balance as a ledger entry at redemption time, so the deduction is reconstructable from transaction history and committed points cannot be spent twice.
- **FR-011**: System MUST record every redemption with the player, the reward, the points spent, the status, and the request timestamp, and MUST track the redemption through the lifecycle states REQUESTED, APPROVED, REJECTED, DELIVERED, and CANCELLED.
- **FR-012**: System MUST allow reward managers to approve or reject requested redemptions, and to mark approved redemptions as delivered with a delivery timestamp.
- **FR-013**: System MUST automatically return the full point amount to the player when a redemption is rejected or cancelled, recording the return as a ledger entry, and MUST release the reserved stock unit.
- **FR-014**: System MUST allow the owning player to cancel their own redemption while it is in REQUESTED or APPROVED state; delivered, rejected, and cancelled redemptions are terminal and immutable.
- **FR-015**: System MUST make every redemption auditable (RWD-006): each status transition MUST record who performed it (player or reward manager) and when, and the full history of a redemption MUST be retrievable.
- **FR-016**: System MUST allow players to view their own redemption history with current status and timestamps; reward managers MUST be able to view and filter all redemptions for processing.
- **FR-017**: System MUST treat duplicate redemption submissions as the same request (idempotent), never producing a double point deduction or double stock reduction.
- **FR-018**: System MUST enforce that only the owning player can redeem with their points or cancel their redemption, and only reward managers can manage the catalog or process redemptions; one player MUST NOT be able to spend another player's points.
- **FR-019**: System MUST reject invalid catalog data: non-positive point costs, negative stock, and empty names are not permitted.

### Key Entities

- **Reward**: A prize available for exchange with points. Attributes: identity, name, description, points required, available stock, status (active/inactive), and optional expiration date. Managed by reward managers; consumed by redemptions.
- **RewardRedemption**: A single exchange of points for a reward by a player. Attributes: the player, the reward, points spent, lifecycle status (REQUESTED, APPROVED, REJECTED, DELIVERED, CANCELLED), request timestamp, delivery timestamp, and a complete transition history for audit. Related to the point ledger through the deduction and refund entries it produces.
- **PointTransaction (existing, from SPEC-007)**: The ledger entry used to record the redemption deduction and any refund, keeping the player's balance reconstructable.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A player can complete a redemption — from opening the reward catalog to receiving confirmation — in under 30 seconds.
- **SC-002**: 100% of redemption attempts that violate any rule (insufficient points, points already committed, out of stock, expired, deactivated reward) are refused with a clear reason, and none of them alter points or stock.
- **SC-003**: Under concurrent contention for the last stock unit, exactly one redemption succeeds — zero oversells across repeated contention trials.
- **SC-004**: 100% of redemptions are fully traceable: who requested, who processed each transition, and when, retrievable for any redemption at any time (RWD-006).
- **SC-005**: Every rejected or cancelled redemption returns the exact point amount originally deducted, with the refund visible in the player's balance and history immediately after resolution.
- **SC-006**: No sequence of redemptions, cancellations, rejections, or deliveries can produce a negative player balance or allow the same points to be spent twice.
- **SC-007**: Reward managers can create a new reward and see it available in the player catalog within the same session, without technical intervention.

## Assumptions

- Points are deducted and committed at request time (not at approval time), so a player cannot spend the same points on multiple concurrent requests; approval/rejection/cancellation operate on the already-committed amount.
- Rejected and cancelled redemptions always refund the full point amount automatically; partial refunds are out of scope.
- Reward status is an administrative active/inactive condition; stock exhaustion and expiration are evaluated at redemption time and do not require a manager to change the reward's status.
- Expiration blocks new redemptions only; it never invalidates or auto-cancels redemptions already requested.
- The physical or digital fulfillment of the prize itself (shipping, coupon generation, gift card issuance) is out of scope — the system tracks the redemption up to the DELIVERED state, which a reward manager confirms once fulfillment happens outside the system.
- No real-money payments are involved; rewards are exchanged exclusively for points earned in gameplay.
- A player may hold multiple pending redemptions simultaneously, as long as each is covered by eligible points at request time.
- Pending redemptions keep the point cost recorded at request time; later changes to the reward's cost do not affect them.
- Authentication and role enforcement (player vs. reward manager) rely on the existing external identity provider and its claims, consistent with the rest of the platform.
- The consolation mechanism (SPEC-010) will grant rewards through this same Reward/RewardRedemption model but is specified separately; this feature must keep the model extensible to system-initiated (non-player) redemptions in the future.

## Dependencies

- **SPEC-007 (Scoring System)**: Provides the point ledger, the player's reconstructable balance, and the redemption transaction type used for deductions and refunds. Redemption eligibility is computed from this ledger.
- **SPEC-008 (Player Withdrawal)**: Points retained after withdrawal (per the configured withdrawal policy) remain part of the player's balance and stay redeemable; redemption is independent of a player's participation status in any game.
- **SPEC-010 (Consolation — forward dependency, not yet specified)**: Consolation prizes will reuse this feature's Reward/RewardRedemption model; this specification keeps the model open to system-initiated redemptions but does not implement consolation eligibility.
