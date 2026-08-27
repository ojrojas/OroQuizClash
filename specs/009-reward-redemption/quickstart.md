# Quickstart: Rewards & Point Redemption Validation

**Feature**: 009-reward-redemption
**Date**: 2026-08-27

This guide documents runnable validation scenarios proving the rewards feature works end-to-end. Implementation details live in `tasks.md`; contracts in `contracts/rewards.openapi.yaml`; data model in `data-model.md`.

## Prerequisites

- .NET 10 SDK
- Access to the repo root `/home/oroja/Sources/OroQuizClash`
- No external DB required for domain/application unit tests (in-memory)
- For live API validation: Aspire AppHost (`OroQuizClash.AppHost`) with API + optional OroIdentityServer for JWT

## Build & Test Commands

```bash
# Restore + build entire solution
dotnet build

# Run reward domain tests
dotnet test tests/OroQuizClash.Domain.Tests/ --filter "FullyQualifiedName~Rewards"

# Run game refund domain tests (Game.RefundPoints)
dotnet test tests/OroQuizClash.Domain.Tests/ --filter "FullyQualifiedName~Refund"

# Run rewards application tests
dotnet test tests/OroQuizClash.Application.Tests/ --filter "FullyQualifiedName~Rewards"

# Run architecture tests (includes RewardDependenciesTests)
dotnet test tests/OroQuizClash.Architecture.Tests/

# Full regression (domain + application + architecture)
dotnet test tests/OroQuizClash.Domain.Tests/ && \
dotnet test tests/OroQuizClash.Application.Tests/ && \
dotnet test tests/OroQuizClash.Architecture.Tests/
```

## Validation Scenarios

### Scenario 1: Successful Redemption (US1 / P1)

**Goal**: Points → prize exchange works atomically (RWD-001, RWD-003).

1. Seed an ACTIVE reward: cost 100, stock 5, no expiration.
2. Create a game, join a player, award 150 points (secured).
3. Redeem the reward with `gameId` of that game.
4. **Assert**: redemption status REQUESTED; reward stock = 4; player balance = 50; a `REWARD_REDEMPTION` transaction of −100 exists in the game ledger with the redemption id in the reason; redemption has one transition (REQUESTED, actor = player).

### Scenario 2: Rule Rejections (US1 / P1 — RWD-001, RWD-004, RWD-005, FR-009)

**Goal**: Every invalid redemption is refused with a distinct reason and zero side effects.

1. **Insufficient points**: player balance 50, reward cost 100 → `InsufficientPoints`; balance/stock unchanged; no ledger entry.
2. **Out of stock**: reward stock 0 → `RewardUnavailable`; nothing changes.
3. **Expired**: reward with `ExpirationDate` in the past → `RewardUnavailable`; nothing changes.
4. **Inactive**: deactivated reward → `RewardUnavailable`; nothing changes.
5. **Player not in funding game**: `gameId` of a game the player never joined → `PlayerNotInGame`.

### Scenario 3: Committed Points Cannot Be Double-Spent (RWD-002)

**Goal**: Deduction at request time prevents spending the same points twice.

1. Player balance 100; reward A cost 100, reward B cost 100, both in stock.
2. Redeem A → succeeds, balance 0.
3. Redeem B → fails `InsufficientPoints`.
4. **Assert**: exactly one ledger deduction; stock decremented only for A.

### Scenario 4: Last-Stock Contention (SC-003)

**Goal**: Two concurrent redemptions for the last unit — exactly one succeeds.

1. Reward stock 1; two players each with sufficient balance in the same game.
2. Submit both redemptions concurrently (integration-level or simulated via RowVersion conflict).
3. **Assert**: exactly one REQUESTED redemption; the other receives a conflict; stock never goes negative; no oversell.

### Scenario 5: Duplicate Submission Idempotency (FR-017)

**Goal**: Retried redemption with the same idempotency key does not double-deduct.

1. Redeem with `idempotencyKey = K` → 201, redemption R.
2. Repeat the identical request with `K`.
3. **Assert**: second call returns R (no new redemption); balance deducted once; stock decremented once.

### Scenario 6: Approval → Delivery (US2 / P2)

**Goal**: Happy-path lifecycle with audit trail (RWD-006).

1. Create a REQUESTED redemption (Scenario 1).
2. Manager approves → status APPROVED, transition recorded with manager id.
3. Manager delivers → status DELIVERED, `DeliveredAt` set, transition recorded.
4. **Assert**: full transition history = [REQUESTED(player), APPROVED(manager), DELIVERED(manager)]; point deduction remains final (no refund entries).

### Scenario 7: Rejection Refunds Points and Releases Stock (US2 / P2)

1. Redeem (balance 150 → 50, stock 5 → 4).
2. Manager rejects.
3. **Assert**: status REJECTED; balance restored to 150 via a positive `ADJUSTMENT` transaction whose reason references the redemption; stock back to 5; transition recorded with manager id.

### Scenario 8: Player Cancellation (US2 / P2)

1. Redeem; then owner cancels while REQUESTED → status CANCELLED, refund + stock release.
2. Redeem again; approve; then owner cancels while APPROVED → status CANCELLED, refund + stock release.
3. **Non-owner cancel**: another player attempts cancel → refused (403/NotOwner).
4. **Terminal immutability**: cancel/approve/reject/deliver on REJECTED/DELIVERED/CANCELLED → invalid transition.

### Scenario 9: Catalog Management (US3 / P3)

1. Manager creates reward (name, description, cost, stock, expiration) → ACTIVE, visible in player catalog.
2. Manager deactivates → no longer redeemable, hidden from player catalog; existing pending redemption still approvable/deliverable.
3. Manager restocks from 0 → reward becomes redeemable again.
4. Invalid payloads (empty name, cost 0, negative stock) → validation errors.
5. **Assert**: pending redemptions keep the point cost recorded at request time after a cost update.

### Scenario 10: Catalog Query with Balance (FR-003)

1. `GET /api/rewards?gameId={game}` as an authenticated player.
2. **Assert**: only active + in-stock + unexpired rewards returned; `availablePoints` equals the player's balance in that game; without `gameId`, no balance field.

## Expected Test Coverage Mapping

| Spec requirement | Test location |
|------------------|---------------|
| RWD-001 sufficient points | Domain `RewardRedemptionTests` + Application `RedeemRewardHandlerTests` |
| RWD-002 eligible/committed points | Application `RedeemRewardHandlerTests` (double-spend) |
| RWD-003 atomicity | Application handler tests (no partial effects on failure) |
| RWD-004 stock | Domain `RewardTests.ReserveStock` + handler tests |
| RWD-005 expiration | Domain `RewardTests.Availability` + handler tests |
| RWD-006 audit | Domain `RedemptionTransitionTests` |
| FR-013 refunds | Domain `GameRefundPointsTests` + Application reject/cancel tests |
| FR-017 idempotency | Application `RedeemRewardHandlerTests` (duplicate key) |
| FR-018 ownership/authorization | Application cancel tests + endpoint policies |
| SC-003 zero oversell | Concurrency scenario (RowVersion conflict) |
