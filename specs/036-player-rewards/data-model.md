# Data Model: Player Rewards (036)

**Branch**: `036-player-rewards` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Overview

Modelo para flujo `Points Wallet` → `Rewards Catalog` `Available/Required/Remaining/Reward Status` → `Reward Detail` → `Redeem` 2 pasos `X-Idempotency-Key` per `rewardId` → `RewardRedemption` `REQUESTED` ledger `REWARD_REDEMPTION` → `Confirmation` + `Redemption History` + `Consolation Reward`. Extiende SPA `QuizArena.Player` Angular 22 `PlayerRewardsStore` `redeem()` `rxMethod` y reutiliza dominio SQL Server `Reward`/`RewardRedemption`/`PointTransaction` + slices `GetRewards`/`RedeemReward`/`GetPlayerRedemptions`.

## Entities (Proyecciones cliente — TypeScript interfaces + Domain referencia)

### 1. Points Wallet (View, derivado de Ledger)

```ts
// View — saldo autoritativo server, nunca calculado cliente (V/D)
interface PointsWalletView {
  playerId: string; // sub
  gameId?: string; // opcional si wallet global o por partida
  availablePoints: number; // 1200 ej. — GamePlayer.Score.CurrentPoints
  totalEarned: number; // opcional sum ledger ANSWER_CORRECT/ROUND_BONUS etc.
  totalRedeemed: number; // opcional sum REWARD_REDEMPTION
  lastUpdated: string; // ISO
  source: 'GET /api/rewards?gameId' | 'GET /api/games/{id}/players/me score.totalPoints';
}
```
- **Origen**: `GamePlayer.Score.CurrentPoints` reconstruido desde `PointTransaction` ledger `ANSWER_CORRECT`/`REWARD_REDEMPTION`/`CONSOLATION`/`WITHDRAWAL` etc. vía `GetRewards.availablePoints` o `GetMyPlayerState.score.totalPoints`.
- **Validación**: `availablePoints >=0`; refrescado tras `Redeem` via `hydrate` `GET /rewards`; cliente nunca muta.
- **Relaciones**: `Player 1──1 Wallet` per `sub`; `Wallet` → `Rewards Catalog` para decidir `Reward Status`.

### 2. Reward (Domain + View)

```ts
// Domain (server)
interface RewardDomain {
  rewardId: string; // RewardId Guid
  name: string; // 3-100 chars
  description: string; // 3-500 chars
  pointsRequired: number; // Required Points >0 ej. 800
  stock: number; // >=0
  status: 'ACTIVE'|'INACTIVE'; // RewardStatus
  expirationDate: string | null; // ISO
  isAvailable: boolean; // Status ACTIVE && Stock>0 && not expired via IsAvailable(now)
  rowVersion: string; // per Reward
}

// View (client)
interface RewardView {
  rewardId: string;
  name: string;
  description: string;
  requiredPoints: number; // 800
  stock: number;
  status: string; // ACTIVE/INACTIVE
  expirationDate: string | null;
  isAvailable: boolean;
  rewardStatus: 'Canjeable'|'Puntos insuficientes'|'Agotada'|'No disponible'; // derivado
  remainingPoints: number | null; // Available - Required si canjeable, o Required - Available faltante
}
```
- **Origen**: `Reward` aggregate `RewardId` `Name` `Description` `PointsRequired` `Stock` `Status` `ExpirationDate` `RowVersion`.
- **Validación**: `RewardAvailableRule` (`Status ACTIVE` + `Stock>0` + `not expired`) para `ReserveStock`; `PointsRequiredPositiveRule` >0.
- **Relaciones**: `Reward 1──N RewardRedemption`; `Reward` → `Catalog` card.

### 3. RewardRedemption / Redemption (Domain + View)

```ts
interface RewardRedemptionDomain {
  redemptionId: string; // RewardRedemptionId Guid
  playerId: string; // sub
  rewardId: string; // RewardId
  gameId: string; // GameId
  points: number; // 800 consumed (0 si Consolation)
  status: 'REQUESTED'|'APPROVED'|'REJECTED'|'DELIVERED'|'CANCELLED'; // RedemptionStatus
  requestedAt: string; // ISO
  deliveredAt: string | null;
  idempotencyKey: string | null; // Guid per rewardId sessionStorage idemp-redeem-{rewardId}
  rowVersion: string; // per Redemption
  transitions: RedemptionTransition[]; // REQUESTED → APPROVED → DELIVERED etc.
}

interface RedemptionView {
  redemptionId: string;
  rewardId: string;
  rewardName: string; // join Reward
  gameId: string;
  points: number; // Required Points consumidos
  remainingPoints: number | null; // saldo resultante tras canje si disponible
  status: string; // Canjeada (=REQUESTED/DELIVERED) / En proceso / Rechazada / Consolation
  requestedAt: string;
  deliveredAt: string | null;
  reference: string; // redemptionId visible
  isConsolation: boolean;
}

interface RedemptionTransition {
  transitionId: string;
  status: string;
  actorId: string;
  at: string; // ISO
}
```
- **Origen**: `RewardRedemption` aggregate via `Create(playerId, rewardId, gameId, points, idempotencyKey)` o `CreateAsConsolation`; `Game.ConsumePoints` + `PointTransaction` `REWARD_REDEMPTION` ledger.
- **Validación**: `RedemptionTransitionRule` para `Approve/Reject/Deliver/Cancel`; `UNIQUE (PlayerId,IdempotencyKey)` idempotencia; `NotRedemptionOwner` 403 si cancela otro.
- **Relaciones**: `Player 1──N RewardRedemption` `Reward 1──N RewardRedemption` `Game 1──N RewardRedemption`.

### 4. Redemption History (Collection View)

```ts
interface RedemptionHistoryView {
  playerId: string;
  items: RedemptionView[]; // orden RequestedAt desc
  totalCount: number;
  page: number;
  pageSize: number;
  hasNext: boolean;
}
```
- **Origen**: `GetPlayerRedemptions` `List` por `PlayerId` `RedemptionsByPlayerSpecification` paginado.
- **Validación**: Orden desc `RequestedAt`; vacío → empty-state CTA.
- **Relaciones**: `Player 1──1 History` → `1──N RedemptionView`.

### 5. Consolation Reward (Domain subtype)

```ts
interface ConsolationRewardView extends RedemptionView {
  isConsolation: true;
  eligibilityReason: string; // ej. "Participación < umbral 500"
  sourceGameId: string;
  points: 0; // no consume saldo
  status: 'APPROVED'; // CreateAsConsolation directo APPROVED
}
```
- **Origen**: `RewardRedemption.CreateAsConsolation(playerId, rewardId, gameId)` + `PointTransaction` `CONSOLATION` si `ConsolationPolicy` FixedPoints/RewardBased.
- **Validación**: Elegibilidad via `GameConfiguration.ConsolationPolicy` + reglas de `Game.Finish`; no canjeable manualmente; `Points 0`; excluida de `Catalog` canjeable.
- **Relaciones**: `Game 1──0..1 ConsolationRedemption` per `Player` elegible.

### 6. PointTransaction REWARD_REDEMPTION / CONSOLATION (Domain, auditoría)

```ts
interface PointTransactionReward {
  transactionId: string;
  playerId: string;
  gameId: string;
  type: 'REWARD_REDEMPTION'|'CONSOLATION';
  points: number; // -800 si REWARD_REDEMPTION, +N si CONSOLATION FixedPoints
  resultingBalance: number; // available tras operación
  reason: string; // "Redemption {rewardId}" o "Consolation"
  createdAt: string;
}
```
- **Origen**: `Game.PointTransactions` ledger `REWARD_REDEMPTION`/`CONSOLATION` vía `ConsumePoints` o consolation handler.
- **Validación**: `points == -RequiredPoints` si canje; `resultingBalance == previousBalance + points`.
- **Relaciones**: `Game 1──N PointTransaction` per `playerId`.

## Relationships

```
Player (sub) 1──1 PointsWallet (Available 1200) ──derived──> RewardsCatalog 1──N Reward (Required 800, Stock, ACTIVE)
Player 1──N RewardRedemption N──1 Reward ; Game 1──N RewardRedemption
RewardRedemption --generates--> PointTransaction REWARD_REDEMPTION (-800) → Wallet Available 400
Player 1──1 RedemptionHistory 1──N RedemptionView (orden desc RequestedAt) incluye ConsolationRedemption 0..1 per Game
Reward 1──N RedemptionTransition (REQUESTED→APPROVED→DELIVERED)
GamePlayer 1──1 Score --ledger--> PointTransaction (ANSWER_CORRECT etc.) → AvailablePoints
Reward.IsAvailable(now) = ACTIVE && Stock>0 && not expired → Reward Status Canjeable/Puntos insuficientes/Agotada/No disponible
```

## Validation Rules (Resumen)

| Entity | Rule | Error Code |
|--------|------|------------|
| Reward | Name 3-100, PointsRequired >0, Stock >=0 | `Reward.InvalidName` `InvalidPointsRequired` `InvalidStock` |
| Reward | IsAvailable for ReserveStock (ACTIVE Stock>0 not expired) | `RewardUnavailable 409` |
| Game | SufficientBalance CurrentPoints >= Required | `InsufficientPoints 409` / `GameErrors.InsufficientPoints` |
| Redemption | UNIQUE (PlayerId,IdempotencyKey) | idempotente 200 sin duplicar |
| Redemption | Transition valid (REQUESTED→APPROVED→DELIVERED) | `InvalidRedemptionTransition 409` |
| Wallet | Available >=0, autoritativo server | `PlayerNotInGame 403` si wallet sin game context |

## State Transitions

```
Reward: ACTIVE ↔ INACTIVE (Activate/Deactivate) ; Stock decrements on ReserveStock
RewardRedemption: REQUESTED → APPROVED → DELIVERED (feliz) ; REQUESTED → REJECTED / CANCELLED (alternativo)
Consolation: (none) → APPROVED (via CreateAsConsolation directo) → (opcional DELIVERED)
```

