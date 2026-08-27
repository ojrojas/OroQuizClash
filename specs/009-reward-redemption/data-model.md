# Phase 1 Data Model: Rewards & Point Redemption

**Date**: 2026-08-27 | **Plan**: [plan.md](plan.md) | **Research**: [research.md](research.md)

Two new aggregates in a new `Rewards` bounded context, plus one extension to the existing `Game` aggregate. Conventions follow existing entities (`Category`, `Question`, `Game`): `AggregateRoot<TId>` + `RowVersion`, `Enumeration` for states, `StronglyTypedId`, `IBusinessRule` validation, domain events.

---

## Aggregate: Reward

**Root**: `Reward : AggregateRoot<RewardId>` — a prize exchangeable for points. Independent of games (catalog-level).

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Id | `RewardId` (Guid) | PK | StronglyTypedId |
| Name | string | required, 3–100 chars | `RewardNameValidRule` |
| Description | string | required, 3–500 chars | |
| PointsRequired | int | > 0 | `PointsRequiredPositiveRule`; cost of redemption |
| Stock | int | ≥ 0 | remaining redeemable units; `StockNotNegativeRule` |
| Status | `RewardStatus` | required | Enumeration, persisted as int id |
| ExpirationDate | DateTimeOffset? | optional | null = never expires |
| CreatedAt | DateTimeOffset | required | UTC at creation |
| UpdatedAt | DateTimeOffset? | optional | set on update/activation/deactivation |
| RowVersion | byte[] | concurrency | `IsRowVersion().IsConcurrencyToken()` |

### RewardStatus (Enumeration)

| Value | Id | Name |
|-------|----|------|
| Active | 1 | `ACTIVE` |
| Inactive | 2 | `INACTIVE` |

- Created rewards start `ACTIVE`.
- `Activate()` / `Deactivate()` are reversible; both record `UpdatedAt` and raise `RewardStatusChangedDomainEvent`.
- No delete operation exists (FR-002: rewards are only deactivated).

### Behavior

| Operation | Preconditions (rules) | Effects |
|-----------|----------------------|---------|
| `Reward.Create(name, description, pointsRequired, stock, expirationDate?)` | name/description valid; pointsRequired > 0; stock ≥ 0; expiration in future if provided | New reward, ACTIVE, `RewardCreatedDomainEvent` |
| `Update(name?, description?, pointsRequired?, stock?, expirationDate?)` | same field rules; stock ≥ 0 | Updates provided fields, sets `UpdatedAt`, `RewardUpdatedDomainEvent`. Pending redemptions keep their recorded points (FR edge case). |
| `Activate()` | Status == INACTIVE | Status → ACTIVE |
| `Deactivate()` | Status == ACTIVE | Status → INACTIVE; blocks new redemptions only |
| `ReserveStock(DateTimeOffset now)` | `RewardAvailableRule`: ACTIVE, Stock > 0, not expired at `now` | Stock −= 1 |
| `ReleaseStock()` | none (called on reject/cancel) | Stock += 1 |
| `IsAvailable(DateTimeOffset now)` (computed) | — | ACTIVE && Stock > 0 && (ExpirationDate null \|\| > now) |

### Validation rules (IBusinessRule)

- `RewardNameValidRule` — 3–100 chars, not whitespace.
- `PointsRequiredPositiveRule` — cost > 0.
- `StockNotNegativeRule` — stock ≥ 0 on create/update.
- `RewardAvailableRule` — active + in stock + not expired (redemption time, server UTC).

### Errors (`RewardErrors`)

| Error | Code | HTTP | Trigger |
|-------|------|------|---------|
| RewardNotFound | `RewardNotFound` | 404 | unknown reward id |
| InvalidRewardName | `Reward.InvalidName` | 400 | name rule broken |
| InvalidRewardDescription | `Reward.InvalidDescription` | 400 | description rule broken |
| InvalidPointsRequired | `Reward.InvalidPointsRequired` | 400 | cost ≤ 0 |
| InvalidStock | `Reward.InvalidStock` | 400 | stock < 0 |
| RewardUnavailable | `RewardUnavailable` | 409 | inactive, out of stock, or expired at redemption (RWD-004/005, FR-009) |
| RewardAlreadyActive / RewardAlreadyInactive | `Reward.InvalidStatusTransition` | 409 | redundant activation/deactivation |
| RedemptionNotFound | `RedemptionNotFound` | 404 | unknown redemption id |
| InvalidRedemptionTransition | `Redemption.InvalidTransition` | 409 | state-machine violation |
| NotRedemptionOwner | `Redemption.NotOwner` | 403 | player cancels another's redemption |

### Domain events

- `RewardCreatedDomainEvent(RewardId)`
- `RewardUpdatedDomainEvent(RewardId)`
- `RewardStatusChangedDomainEvent(RewardId, Status)`
- `RewardRedeemedDomainEvent(RedemptionId, RewardId, PlayerId, GameId, Points)`
- `RedemptionStatusChangedDomainEvent(RedemptionId, Status, ActorId)`

---

## Aggregate: RewardRedemption

**Root**: `RewardRedemption : AggregateRoot<RewardRedemptionId>` — one exchange of points for a reward.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| Id | `RewardRedemptionId` (Guid) | PK | StronglyTypedId |
| PlayerId | Guid | required | user `sub` of redeeming player |
| RewardId | `RewardId` | required, FK → Reward | |
| GameId | `GameId` | required | funding game (ledger traceability, R1) |
| Points | int | > 0 | cost recorded at request time; immutable afterwards |
| Status | `RedemptionStatus` | required | Enumeration, persisted as int id |
| RequestedAt | DateTimeOffset | required | UTC at creation |
| DeliveredAt | DateTimeOffset? | set on DELIVERED | |
| IdempotencyKey | Guid? | optional, unique (filtered) | FR-017 |
| RowVersion | byte[] | concurrency | |
| Transitions | collection of `RedemptionTransition` | ≥ 1 | audit history (RWD-006) |

### Child entity: RedemptionTransition

`RedemptionTransition : Entity<RedemptionTransitionId>` — append-only audit entry.

| Field | Type | Notes |
|-------|------|-------|
| Id | `RedemptionTransitionId` | PK |
| Status | `RedemptionStatus` | state reached |
| ActorId | Guid | player or manager `sub` |
| At | DateTimeOffset | UTC |

### RedemptionStatus (Enumeration)

| Value | Id | Name | Terminal |
|-------|----|------|----------|
| Requested | 1 | `REQUESTED` | no |
| Approved | 2 | `APPROVED` | no |
| Rejected | 3 | `REJECTED` | yes |
| Delivered | 4 | `DELIVERED` | yes |
| Cancelled | 5 | `CANCELLED` | yes |

### State machine

```text
              approve (manager)            deliver (manager)
REQUESTED ──────────────────────► APPROVED ──────────────────► DELIVERED
    │                                 │
    │ reject (manager)                │ cancel (owner)
    │ → refund + release stock        │ → refund + release stock
    ▼                                 ▼
 REJECTED                         CANCELLED
    ▲
    │ (also directly from REQUESTED via cancel by owner,
    │  with refund + release stock)
REQUESTED ── cancel (owner) ─────► CANCELLED
```

Allowed transitions (`RedemptionTransitionRule`):

| From | To | Actor | Side effects |
|------|----|-------|--------------|
| — | REQUESTED | player (owner) | created with first transition; stock already reserved + points already consumed by orchestrating handler |
| REQUESTED | APPROVED | manager | transition recorded |
| REQUESTED | REJECTED | manager | refund points + release stock (handler) |
| REQUESTED | CANCELLED | owner player | refund points + release stock (handler) |
| APPROVED | DELIVERED | manager | `DeliveredAt` set; deduction final |
| APPROVED | CANCELLED | owner player | refund points + release stock (handler) |

Any other transition ⇒ `InvalidRedemptionTransition` (terminal states immutable, FR-014).

### Behavior

| Operation | Preconditions | Effects |
|-----------|---------------|---------|
| `RewardRedemption.Create(playerId, rewardId, gameId, points, idempotencyKey?)` | points > 0 | Status REQUESTED, RequestedAt = UTC now, initial transition, `RewardRedeemedDomainEvent` |
| `Approve(managerId)` | Status == REQUESTED | Status → APPROVED, transition, `RedemptionStatusChangedDomainEvent` |
| `Reject(managerId)` | Status == REQUESTED | Status → REJECTED, transition, event |
| `Deliver(managerId)` | Status == APPROVED | Status → DELIVERED, DeliveredAt, transition, event |
| `Cancel(playerId)` | Status ∈ {REQUESTED, APPROVED}; `playerId` == PlayerId (`NotRedemptionOwner` otherwise) | Status → CANCELLED, transition, event |

Refunds and stock release are orchestration side effects in handlers (cross-aggregate), calling `Game.RefundPoints` + `Reward.ReleaseStock` (research R4).

---

## Extension: Game aggregate

New operation on `Game` (mirrors `ConsumePoints`/`AdjustPoints` style, no game-state check):

```text
RefundPoints(Guid playerId, int amount, string reason) : Result<PointTransaction>
```

| Step | Rule / Error |
|------|--------------|
| amount > 0 | `GameErrors.InvalidAdjustmentAmount` |
| player in game | `GameErrors.PlayerNotInGame` |
| credit `player.Score.Award(amount, roundScoped: false)` | — |
| append positive `ADJUSTMENT` transaction with reason | ledger append-only |
| raise `ScoreUpdatedDomainEvent` | — |

Deduction path reuses existing `Game.ConsumePoints` (SPEC-007) unchanged: validates `SufficientBalanceRule` against `CurrentPoints`, consumes secured-first, appends negative `REWARD_REDEMPTION` transaction.

---

## Relationships

```text
Reward 1 ──── * RewardRedemption * ──── 1 Game (funding)
                     │
                     └── * RedemptionTransition (audit)

Game 1 ──── * GamePlayer (Score: PlayerScore)
Game 1 ──── * PointTransaction (ledger: REWARD_REDEMPTION deduction / ADJUSTMENT refund)
```

- `RewardRedemption.PlayerId` is the user `sub` (matches `GamePlayer.UserId` / `PointTransaction.PlayerId`).
- Ledger linkage is by `(GameId, PlayerId)` + reason containing the redemption id — no FK from ledger to redemption (ledger stays game-scoped, SPEC-007).

## Persistence (EF configurations)

- `RewardTypeConfiguration`: table `Rewards`; id conversion; `Status` → int id conversion (`RewardStatus.FromId`); `Name` max 100, `Description` max 500; `RowVersion` `IsRowVersion().IsConcurrencyToken()`; index on `Status`.
- `RewardRedemptionTypeConfiguration`: table `RewardRedemptions`; id conversions (`RewardRedemptionId`, `RewardId`, `GameId`); `Status` → int id conversion; `Transitions` as child table `RedemptionTransitions` (FK → RewardRedemptions, cascade); unique filtered index on `IdempotencyKey` where not null; indexes on `(PlayerId)`, `(RewardId)`, `(Status)`.
- `OroQuizClashDbContext`: `DbSet<Reward> Rewards`, `DbSet<RewardRedemption> RewardRedemptions`.
- Dual provider: SQLite local / SQL Server Aspire — all column types portable (int, Guid, string, DateTimeOffset, byte[] rowversion).

## Specifications (Infrastructure)

- `RewardSpecifications`: `AvailableRewards(now)` (active + stock > 0 + not expired, ordered by PointsRequired), `RewardById(id)`, `AllRewards()` (manager view).
- `RedemptionSpecifications`: `RedemptionById(id)`, `RedemptionsByPlayer(playerId)`, `RedemptionsByStatus(status)`, `RedemptionByIdempotencyKey(playerId, key)`.
