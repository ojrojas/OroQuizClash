# Data Model: Player Withdrawal (035)

**Branch**: `035-player-withdrawal` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Overview

Modelo **solo lectura/proyección** para flujo retiro voluntario `Withdrawal Action` → diálogo `Current/Secured/Potential` + warnings → `POST /withdraw` `X-Idempotency-Key` per `gameId` → `PlayerWithdrawn` `WITHDRAWN` `isTerminal` `canAnswer false` `Current=Secured` ledger, en Angular 22 `WithdrawalComponent` `GameComponent` `PlayerGameStore` `withdraw()` `rxMethod` (SPEC-029). Fuente autoritativa `OroQuizClash.Domain` (SQL Server `GamePlayer` `RowVersion` per `GamePlayerId` + `PointTransaction` `WITHDRAWAL`).

## Entities (Proyecciones cliente — TypeScript interfaces + Domain referencia)

### 1. WithdrawalAction (Domain)

```ts
// Domain (server) — invariante F/C: WithdrawalPolicy per GameConfiguration
interface WithdrawalActionDomain {
  gameId: string;
  playerId: string; // sub
  idempotencyKey: string; // UUID per gameId sessionStorage idemp-withdraw-{gameId}
  deduction: number; // Current - Secured si KEEP_SECURED_SCORE
  resultingBalance: number; // Secured
  status: 'WITHDRAWN';
  rowVersion: string; // per GamePlayerId
}

// Command WithdrawPlayer (Application)
interface WithdrawPlayerCommand {
  gameId: string;
  playerId: string; // sub from JWT
  idempotencyKey?: string;
}
```
- **Origen**: `Game.WithdrawPlayer(playerId)` dominio con `IBusinessRule` `PlayerNotWithdrawn` + `PlayerAlreadyEliminated` + `IsActive` + `WithdrawalPolicy` `KEEP_SECURED_SCORE` → `Score.CurrentPoints = SecuredPoints` + `PointTransaction` `WITHDRAWAL` `-deduction`.
- **Validación**: `!IsTerminal` + `IsActive` + `!IsWithdrawn` + `!IsEliminated`; `RowVersion` per `GamePlayerId` + `X-Idempotency-Key` `UNIQUE` per `GamePlayer`.
- **Relaciones**: `Game 1──N GamePlayer` → `WithdrawalAction` per `GamePlayer` `WITHDRAWN`.

### 2. Score / SecuredPoints / PotentialPoints (View)

```ts
interface ScoreView {
  totalPoints: number; // Current Points 400 etc.
  currentPoints?: number;
}
interface SecuredPointsView {
  securedPoints: number; // 200
  checkpointRoundNumber: number | null; // 2 or null
  policy: string; // KEEP_SECURED_SCORE
}
interface PotentialPointsView {
  displayText: string; // "100 pts" or "—"
  points: number | null;
}
```
- **Origen**: `GamePlayer.Score` `CurrentPoints` `SecuredPoints` `PotentialReward` via `GetMyPlayerState` `Score`/`SecuredPoints`/`GameConfiguration`.
- **Validación**: `Current/Secured/Potential` per `sub` ledger no cliente calc; `Potential` "—" si no `RewardRules`.
- **Relaciones**: `GamePlayer 1──1 Score` → `WithdrawalAction` `Secured` X para warning 2.

### 3. GameSession (Domain + view)

```ts
interface GameSessionDomain {
  gameSessionId: string; // GamePlayerId
  playerId: string; // sub
  gameId: string;
  status: 'ACTIVE'|'WITHDRAWN'|'ELIMINATED'|'WINNER'|'FINISHED';
  currentRoundNumber: number;
  rowVersion: string; // per GamePlayerId
}

interface GameSessionView {
  gameSessionId: string;
  playerId: string;
  gameId: string;
  status: string;
  currentRoundNumber: number;
  rowVersion: string;
}
```
- **Origen**: `GamePlayer` `GamePlayerId` `UserId=sub` `RowVersion` per `GamePlayerId`.
- **Validación**: `status==WITHDRAWN` → `isTerminal true` `canAnswer false`; `RowVersion` per `GamePlayerId` no global `Game`.
- **Relaciones**: `Game 1──N GameSession` per `sub`.

### 4. PointTransaction WITHDRAWAL (Domain)

```ts
interface PointTransactionWithdrawal {
  transactionId: string;
  playerId: string; // sub
  gameId: string;
  type: 'WITHDRAWAL';
  points: number; // -deduction (ej. -200 si Current 400 Secured 200)
  resultingBalance: number; // 200
  createdAt: string; // ISO UTC
}
```
- **Origen**: `Game.PointTransactions` ledger `WITHDRAWAL` per `WithdrawalPolicy`.
- **Validación**: `points == -(Current - Secured)` si `KEEP_SECURED_SCORE`; `resultingBalance == Secured`.
- **Relaciones**: `Game 1──N PointTransaction` `WITHDRAWAL` per `playerId`.

### 5. GameStatus / PlayerGameStatus (view)

```ts
interface PlayerGameStatus {
  gameStatus: string; // IN_PROGRESS/ROUND_IN_PROGRESS/FINISHED
  playerStatus: string; // ACTIVE/WITHDRAWN/ELIMINATED/WINNER
  isTerminal: boolean; // true si WITHDRAWN/ELIMINATED/WINNER/FINISHED
  canAnswer: boolean; // false si WITHDRAWN
}
```
- **Origen**: `Game.Status` + `GamePlayer.ParticipationStatus`.
- **Validación**: `isTerminal true` si `WITHDRAWN`; `canAnswer false` si `WITHDRAWN`.
- **Relaciones**: `Game 1──N PlayerGameStatus` per `sub`.

## Relationships

```
Player (sub) 1──N GameSession N──1 Game 1──N GameRound
Player 1──1 Score (CurrentPoints 400) + SecuredPoints (200 checkpoint 2) + PotentialPoints (100) per sub → Withdrawal Dialog 3 métricas
Player 1──1 GameSession (RowVersion per GamePlayerId) → WithdrawalAction per GamePlayer WITHDRAWN
WithdrawalAction -- uses --> WithdrawalPolicy (KEEP_SECURED_SCORE) -- generates --> PointTransaction WITHDRAWAL (-200) → Score Current=Secured 200
PlayerGameStatus (isTerminal/canAnswer) per sub → QuestionComponent aria-disabled + Withdrawal Action disabled if isTerminal
GameRealtimeService (ScoreUpdated/GameFinished) ──▶ hydrate → GET /players/me per sub WITHDRAWN
```

## State Transitions (cliente observa, servidor decide)

- **View**: `ACTIVE canAnswer true Current 400 Secured 200` -- `Withdrawal Action` click --> `showWithdrawConfirm true` diálogo 3 métricas + 2 warnings --> `Confirmar` `POST /withdraw X-Idempotency-Key` --> `WITHDRAWN isTerminal true canAnswer false Current 200` `PlayerWithdrawn` `WITHDRAWAL` ledger.
- **Hydrate**: `POST /withdraw` 200 → `patchState({gameSession,status})` `isTerminal true` `canAnswer false` → `QuestionComponent` `aria-disabled` + `Withdrawal Action` `disabled`.
- **Idempotency**: Segunda `POST /withdraw` misma `X-Idempotency-Key` per `gameId` → mismo `GameSession` `WITHDRAWN` sin nuevo `WITHDRAWAL` ledger.

## Validation Rules

- `gameSession.playerId == sub` 100%; `Answer`/`Score` per `sub`.
- `Current/Secured/Potential` per `sub` ledger no cliente calc; `Secured` checkpoint null → "200 pts" sin badge.
- `Withdrawal` requiere 2 pasos: `Withdrawal Action` abre diálogo, `Confirmar` envía `POST /withdraw`; `Cancelar`/`Escape` no envía.
- `X-Idempotency-Key` UUID per `gameId` `sessionStorage` `idemp-withdraw-{gameId}` `UNIQUE` per `GamePlayer`.
- `RowVersion` per `GamePlayerId` no global `Game`; `Withdraw` de A no afecta B.
- `isTerminal true` si `WITHDRAWN`; `canAnswer false` si `WITHDRAWN`.
- `X-Correlation-Id` UUID v4 per `POST /withdraw`.

## Persistence (cliente)

- **En memoria**: `PlayerGameStore` `DeepSignal` `{score, securedPoints, game, gameSession, status}` scoped per `GameComponent` `providers: [PlayerGameStore]`, `computed` para `isTerminal/canAnswer`.
- **Efímero**: `sessionStorage` `idemp-withdraw-{gameId}` UUID per `GameId` para `POST /withdraw` idempotencia; nunca `localStorage`.
- **Server**: SQL Server `GamePlayer` `RowVersion` per `GamePlayerId` + `PointTransaction` `WITHDRAWAL` + `Game` `RowVersion` + Outbox. `WithdrawPlayerHandler` `IRepository<Game,GameId>` `Game.WithdrawPlayer(sub)` → `SaveChanges` + `Outbox`.

## Indexes / Queries (server reference)

- `GamePlayer` IX `(GameId, UserId)` + `RowVersion` per `GamePlayerId`; `PointTransaction` IX `(GameId, PlayerId, CreatedAt)`; `Game` IX `Status`.
- `GetMyPlayerState` Query: `GameByIdWithAnswersSpecification` + `AsNoTracking` para 3 métricas.

## UI States

- `Withdrawal Action` botón `min-height:44px` `aria-label="Retirarse"` `disabled` si `isTerminal`.
- `Dialog` `role="dialog"` `aria-modal="true"` `aria-label="Confirmar retiro"` con `Current/Secured/Potential` 3 métricas + 2 warnings `role="alert"` `aria-live assertive/polte`.
- `Confirmar` `min-height:44px` `aria-label="Confirmar retiro"` + `Cancelar` `min-height:44px`.
- `Loading` skeleton `aria-busy`, `ErrorState` `ProblemDetails` `CorrelationId/TraceId` `Retry` `X-Idempotency-Key` reuse.
- `PlayerWithdrawn` `isTerminal true` `canAnswer false` `aria-disabled` `WITHDRAWAL` ledger.
```

