# Data Model: Player Results (034)

**Branch**: `034-player-results` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Overview

Modelo **solo lectura/proyección** para 4 pantallas finales `YOU WON`/`YOU WALKED AWAY`/`GAME OVER`/`GAME FINISHED` en Angular 22 `ResultComponent` `route /player/game/:gameId/result` como proyección de `GetMyPlayerState` per `sub` (`Score` `SecuredPoints` `GameSession` `Game`) + `GetLeaderboard` `Rank`/`Prize`/`Consolation` (Server Truth V). Fuente autoritativa `OroQuizClash.Domain` (SQL Server `PointTransaction` ledger, `Game` `LeaderboardBuilder`, `Reward`/`Consolation`). No nuevos agregados dominio.

## Entities (Proyecciones cliente — TypeScript interfaces + Domain referencia)

### 1. ResultState (Domain + view)

```ts
// Domain (server) — per sub + Leaderboard Rank
type ResultStateDomain = 'WINNER' | 'WITHDRAWN' | 'ELIMINATED' | 'FINISHED'; // GamePlayer.ParticipationStatus + Game.Status.IsTerminal + Leaderboard Rank 1

// View (cliente) — 4 pantallas
type ResultStateView = 'won' | 'walked' | 'over' | 'finished' | 'playing'; // playing = !IsTerminal → redirect

interface ResultState {
  state: ResultStateView;
  isTerminal: boolean;
  rank: number | null; // 1..N per Leaderboard per sub
  totalPlayers: number;
}

// Contract GET /players/me + GET /leaderboard -> ResultState
// Derived: if PlayerStatus==WINNER && GameStatus==FINISHED && Rank==1 → won
//          if PlayerStatus==WITHDRAWN → walked
//          if PlayerStatus==ELIMINATED → over
//          if GameStatus==FINISHED && PlayerStatus==FINISHED && Rank 2..N → finished
//          else playing → redirect
```
- **Origen**: `Game.Status.IsTerminal` + `GamePlayer.ParticipationStatus` (`WINNER/WITHDRAWN/ELIMINATED/FINISHED`) + `LeaderboardBuilder.Build(game)` `Rank` per `sub`.
- **Validación**: Exactamente 1 de 4 pantallas per `sub` si `IsTerminal`; `playing` si `!IsTerminal` → redirect.
- **Relaciones**: `Game 1──N GamePlayer` → `ResultState` per `sub` + `Leaderboard Rank`.

### 2. FinalScore (Domain + view)

```ts
// Domain (server) — ledger sum
interface FinalScoreDomain {
  playerId: string; // sub
  gameId: string;
  totalPoints: number; // sum(PointTransaction) per playerId
}

// View (cliente) — autoritativo per sub
interface FinalScoreView {
  totalPoints: number; // 850 etc.
  displayText: string; // "850 pts"
}

// Contract GET /players/me -> score.totalPoints
interface ScoreDto { playerId: string; gameId: string; totalPoints: number; correctAnswers: number; currentLevel: string; }
```
- **Origen**: `GamePlayer.Score.CurrentPoints` `sum(PointTransaction)` per `playerId`.
- **Validación**: `totalPoints >=0`; no `Current+Secured` cliente.
- **Relaciones**: `GamePlayer 1──1 Score` → `FinalScore` per `sub`.

### 3. Prize / Reward (Domain + view)

```ts
// Domain (server)
interface PrizeDomain {
  rewardId: string;
  name: string; // "Pack Oro"
  pointsRequired: number; // 500
  type: string; // REWARD
  status?: string; // DELIVERED if Winner
}

// View (cliente) — YOU WON / GAME FINISHED
interface PrizeView {
  rewardId: string;
  name: string;
  pointsRequired: number;
  displayText: string; // "Pack Oro"
}

// Contract GET /leaderboard -> LeaderboardEntry Reward + GET /rewards
// Derived: if totalPoints >= pointsRequired de RewardRules
```
- **Origen**: `GameConfiguration.RewardRules` `RewardId/RoundThreshold/PointsRequired` + `LeaderboardBuilder` + `RewardRedemption` per `sub` si `WINNER`.
- **Validación**: `pointsRequired <= totalPoints` para `Prize`; null → ocultar bloque.
- **Relaciones**: `Game 1──N Reward` → `Prize` per `sub` si `Winner`.

### 4. SecuredPoints (Domain + view)

```ts
interface SecuredPointsDomain {
  playerId: string;
  gameId: string;
  securedPoints: number;
  checkpointRoundNumber: number | null;
  policy: string; // KEEP_SECURED_SCORE etc.
}

interface SecuredPointsView {
  securedPoints: number;
  checkpointRoundNumber: number | null;
  policy: string;
  displayText: string; // "200 pts · checkpoint 2" or "200 pts"
}

// Contract GET /players/me -> securedPoints
interface SecuredPointsDto { playerId: string; gameId: string; securedPoints: number; checkpointRoundNumber: number | null; policy: string; }
```
- **Origen**: `GamePlayer.Score.SecuredPoints` per `sub`.
- **Validación**: `securedPoints <= totalPoints`; `checkpoint` 1..MaxRounds or null.
- **Relaciones**: `GamePlayer 1──1 SecuredPoints` → `YOU WALKED AWAY`.

### 5. AvailableRewards (View)

```ts
interface AvailableRewardsView {
  rewards: Array<{ rewardId: string; name: string; pointsRequired: number }>;
  displayCount: number;
}

// Contract GET /api/rewards (público) filtrable pointsRequired <= securedPoints.securedPoints
```
- **Origen**: `GET /api/rewards` lista `Reward[]` filtrable.
- **Validación**: `reward.pointsRequired <= securedPoints.securedPoints` para `YOU WALKED AWAY`; vacía → "Sin recompensas disponibles".
- **Relaciones**: `Reward 1──N AvailableRewards` per `sub`.

### 6. ConsolationReward (Domain + view)

```ts
interface ConsolationRewardDomain {
  rewardId?: string | null;
  name?: string | null;
  points?: number | null; // FixedPoints 50 etc.
  type: 'CONSOLATION';
}

// View (cliente) — GAME OVER
interface ConsolationRewardView {
  rewardId?: string | null;
  name: string; // "Pack Consuelo" or "50 pts"
  displayText: string; // "Pack Consuelo" or "Sin consolación"
}

// Contract GET /players/me -> Consolation per ConsolationPolicy per sub
```
- **Origen**: `ConsolationPolicy` `FixedPoints/ParticipationBased/RewardBased` per `sub` si `ELIMINATED` y elegible (SPEC-010).
- **Validación**: Null → "Sin consolación".
- **Relaciones**: `GamePlayer 1──1 ConsolationReward` si `ELIMINATED` y elegible.

### 7. FinalPosition (View)

```ts
interface FinalPositionView {
  position: number; // 1..N
  totalPlayers: number; // N
  displayText: string; // "3"
  ariaLabel: string; // "Puesto 3 de 4"
}

// Contract GET /leaderboard -> LeaderboardEntry Rank per sub
interface LeaderboardEntryDto { playerId: string; displayName: string; totalPoints: number; level: string; position: number; }
```
- **Origen**: `LeaderboardBuilder.Build(game)` `Rank` per `sub` orden `totalPoints` desc + `CorrectAnswers` + `AchievedAt`.
- **Validación**: `position 1..totalPlayers`; `Rank 1` → `YOU WON`, 2..N → `GAME FINISHED`.
- **Relaciones**: `Game 1──N LeaderboardEntry` → `FinalPosition` per `sub`.

### 8. Leaderboard (View público)

```ts
interface LeaderboardView {
  entries: Array<{ playerId: string; displayName: string; totalPoints: number; level: string; position: number }>;
}

// Contract GET /leaderboard (público)
```
- **Origen**: `GetLeaderboard` público sin privados.
- **Validación**: Sin `SelectedOptionId/isCorrect/Timer/Secured` de otros.
- **Relaciones**: `Game 1──N LeaderboardEntry` público.

## Relationships

```
Player (sub) 1──1 GameSession N──1 Game 1──N GameRound 1──1 Question
Player 1──1 Score (totalPoints ledger) + SecuredPoints (securedPoints/checkpoint) per sub → FinalScore/Secured
Game 1──N Reward + ConsolationPolicy → Prize/ConsolationReward per sub si Winner/Eliminated
Game 1──N LeaderboardEntry (Rank 1..N per sub) → FinalPosition per sub
Game.Status.IsTerminal + GamePlayer.ParticipationStatus (WINNER/WITHDRAWN/ELIMINATED/FINISHED) + Rank → ResultState (won/walked/over/finished/playing)
ResultState per sub → ResultComponent 4 pantallas (YOU WON/WALKED/GAME OVER/FINISHED) per sub
GameRealtimeService (GameFinished) ──▶ hydrate → GET /players/me per sub + GET /leaderboard Rank
```

## State Transitions (cliente observa, servidor decide)

- **View**: `playing (!IsTerminal)` -- `GameFinished` --> `ResultState` per `sub`: `WINNER Rank1` → `won`; `WITHDRAWN` → `walked`; `ELIMINATED` → `over`; `FINISHED` Rank 2..N → `finished` → `ResultComponent` render 1 de 4.
- **Redirect**: `playing` `GET /result` → redirect `router.navigate(['/player/game', gameId])` + `ErrorState` "Partida aún en curso".
- **Hydrate**: `GameFinished` → `hydrateFor(gameId)` → `GET /players/me` per `sub` + `GET /leaderboard` Rank.

## Validation Rules

- `ResultState` exactamente 1 de 4 per `sub` si `IsTerminal`; `playing` si `!IsTerminal` → redirect.
- `FinalScore` `totalPoints >=0`; `FinalPosition` 1..N per `Leaderboard` per `sub`.
- `Prize` null → ocultar bloque `Prize` sin error; `Consolation` null → "Sin consolación".
- `SecuredPoints` checkpoint null → "200 pts" sin badge.
- `AvailableRewards` `reward.pointsRequired <= securedPoints.securedPoints` para `YOU WALKED AWAY`.
- `X-Correlation-Id` UUID v4 per `GET /players/me` + `GET /leaderboard`.
- `ResultComponent` `route /player/game/:gameId/result` `canActivate` `authGuard` + `mustChangePasswordGuard`.

## Persistence (cliente)

- **En memoria**: `PlayerGameStore` `DeepSignal` `ResultState` per `sub` scoped `providers: [PlayerGameStore]` per `ResultComponent` o `GameComponent`, `computed` para `resultState/finalScore/finalPosition/prize`.
- **Efímero**: Ninguno para result; `ResultComponent` no `sessionStorage`.
- **Server**: SQL Server `PointTransaction` ledger + `Game` `LeaderboardBuilder` + `Reward`/`Consolation` + `GamePlayer` `Score`/`SecuredPoints`/`ParticipationStatus` + `GameStatus` `IsTerminal`. `GetMyPlayerState` + `GetLeaderboard` Queries.

## Indexes / Queries (server reference)

- `Leaderboard` no tabla, es view `Build(game)` `OrderBy totalPoints desc`.
- `GetMyPlayerState` Query: `GameByIdWithAnswersSpecification` + `AsNoTracking`; `GetLeaderboard` Query: `GameById` + `Players` + `PointTransactions` sum.

## UI States

- `YOU WON` `var(--color-success)` gradiente `success` + confetti `pulse` `aria-live assertive` "YOU WON".
- `YOU WALKED AWAY` `var(--color-warning)` `Secured Points` + `Available Rewards` list `role="list"`.
- `GAME OVER` `var(--color-destructive)` `Final Score` + `Consolation Reward` "Sin consolación" si null.
- `GAME FINISHED` `var(--color-accent)` `Final Position` `Final Score` + `Reward` "Sin recompensa" si null.
- `Loading` skeleton `aria-busy`, `ErrorState` `CorrelationId/TraceId` `Retry`, `Redirect` "Partida aún en curso" `ErrorState`.
- `Responsive` 1col 375 / 2col ≥768 `gap var(--space-3)` `min-height 44px`.
```

