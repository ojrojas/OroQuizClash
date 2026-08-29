# Data Model: Player Scoring (032)

**Branch**: `032-player-scoring` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Overview

Modelo **solo lectura/proyección** en Angular 22 (5 métricas scoring) sobre `oroclash-api` `GET /api/games/{id}/players/me` (`Score` + `SecuredPoints` + `GameConfiguration`) ya en 029 y `GameHub` `ScoreUpdated/RoundCompleted/Reconnected → hydrate` (SPEC-012). Fuente autoritativa `OroQuizClash.Domain` (SQL Server `PointTransaction` ledger `UNIQUE`, `Game` RowVersion, `GamePlayer.Score`). No nuevos agregados dominio.

## Entities (Proyecciones cliente — TypeScript interfaces + Domain referencia)

### 1. Score (Domain + view)

```ts
// Domain (server) — invariante D: ledger append-only
interface ScoreDomain {
  playerId: string;
  gameId: string;
  currentPoints: number;       // sum(PointTransaction) donde Type != WITHDRAWAL/CONSOLATION? 0..N
  roundPoints: number;         // acumulado ronda actual, reseteado en RoundCompleted per LossPolicy
  totalPoints: number;         // lifetime = currentPoints (en single game es mismo) o suma global
  correctAnswers: number;
  currentLevel: string;        // Basic..Expert per Difficulty 1..5
  version: string;             // RowVersion base64
}

// View (cliente) — sin cálculo, solo proyección
interface ScoreView {
  playerId: string;
  gameId: string;
  totalPoints: number;         // Total Points (SC-001/003)
  currentPoints: number;       // Current Points (SC-001)
  roundPoints: number;         // Round Points (SC-001)
  correctAnswers: number;
  currentLevel: string;
}

// Contract GET /api/games/{id}/players/me -> score
interface ScoreDto {
  playerId: string;
  gameId: string;
  totalPoints: number;
  correctAnswers: number;
  currentLevel: string;
  // Nota: 029 ya retorna totalPoints como CurrentPoints; RoundPoints derivado de Score.RoundPoints o score.totalPoints - securedPoints
}
```
- **Origen**: `GamePlayer.Score` (`PlayerScore` ValueObject con `CurrentPoints`, `RoundPoints`, `SecuredPoints`) + `PointTransaction` ledger `ANSWER_CORRECT/INCORRECT/ROUND_BONUS/LEVEL_BONUS/GAME_BONUS/WITHDRAWAL/CONSOLATION/ADJUSTMENT`. `Score` es `sum(PointTransaction.Points)`.
- **Validación**: `totalPoints >=0`; cliente nunca hace `totalPoints = current + secured`; `RoundPoints` 0 si `RoundCompleted` recién.
- **Relaciones**: `Game 1──N GamePlayer 1──1 Score`; `Score 1──N PointTransaction`.

### 2. SecuredPoints (Domain + view)

```ts
interface SecuredPointsDomain {
  playerId: string;
  gameId: string;
  securedPoints: number;           // protegidos por LossPolicy (except LOSE_ALL)
  checkpointRoundNumber: number | null; // null si sin checkpoint
  policy: string;                  // KEEP_SECURED_SCORE / LOSE_ALL etc.
}

interface SecuredPointsView {
  playerId: string;
  gameId: string;
  securedPoints: number;
  checkpointRoundNumber: number | null;
  policy: string;
}

// Contract GET /players/me -> securedPoints
interface SecuredPointsDto {
  playerId: string;
  gameId: string;
  securedPoints: number;
  checkpointRoundNumber: number | null;
  policy: string;
}
```
- **Origen**: `GamePlayer.Score.SecuredPoints` + `CheckpointRoundNumber` + `WithdrawalPolicy`/`LossPolicy` (SPEC-007 C). `SecurePoints` operation mueve `RoundPoints→SecuredPoints`.
- **Validación**: `securedPoints <= totalPoints`; `checkpointRoundNumber` 1..MaxRounds o null.
- **Relaciones**: `GamePlayer 1──1 SecuredPoints`.

### 3. PointTransaction (Domain)

```ts
interface PointTransaction {
  transactionId: string;
  playerId: string;
  gameId: string;
  roundId?: string | null;
  questionId?: string | null;
  type: 'ANSWER_CORRECT' | 'ANSWER_INCORRECT' | 'ROUND_BONUS' | 'LEVEL_BONUS' | 'GAME_BONUS' | 'PENALTY' | 'WITHDRAWAL' | 'REWARD_REDEMPTION' | 'CONSOLATION' | 'ADJUSTMENT';
  points: number;                  // +100 o -200
  resultingBalance: number;        // balance tras transacción
  createdAt: string;               // ISO UTC server
  reason?: string | null;
}
```
- **Origen**: `Game.PointTransactions` ledger append-only `UNIQUE (GameId,PlayerId,CreatedAt)` + `RowVersion`. `Total Points = sum(PointTransaction.points)` reconstruible.
- **Invariante**: Nunca `player.Points +=100` aislado; siempre `AwardPoints` domain operation genera `PointTransaction` + `Outbox`.
- **Relaciones**: `Game 1──N PointTransaction`; `PointTransaction` N──1 `GamePlayer`/`GameRound`.

### 4. PotentialPoints (View projection)

```ts
interface PotentialPointsView {
  points: number | null;           // null si no configurado → "—"
  rewardName?: string | null;      // "Pack Oro" si RewardRules próximo umbral
  threshold?: number | null;       // 500 pts si aplica
  displayText: string;             // "100 pts" o "Próximo: Pack Oro 500 pts" o "—"
}
```
- **Origen**: `Game.Configuration.PointsPerRound` (int 100 default) * dificultad 1..5 + `RewardRules` (`RewardId`, `RoundThreshold`, `PointsRequired`). Ej. `potentialReward` computed en `PlayerGameStore` (029) `currentRoundDisplay` + `potentialReward` `rewardName`.
- **Validación**: `points >=0`; si `points==null` → "—" `aria-label` "Potential no disponible".
- **Relaciones**: `GameConfiguration 1──1 PotentialPoints` (proyección).

### 5. TotalPoints (View)

```ts
interface TotalPointsView {
  totalPoints: number;             // 850 etc., autoritativo server
  displayText: string;             // "850 pts"
}
```
- **Origen**: `Score.totalPoints` o `sum(PointTransaction)` server-side; cliente no calcula `Current+Secured`.
- **Validación**: `totalPoints == sum(PointTransaction)` 100% (SC-003).

### 6. ScoringDisplayState (View-Model 032 central)

```ts
interface ScoringDisplayState {
  currentPoints: number;
  securedPoints: number;
  checkpointRoundNumber: number | null;
  potentialPoints: number | null;
  potentialDisplay: string;        // "100 pts" | "Próximo: Pack Oro 500 pts" | "—"
  roundPoints: number;
  totalPoints: number;
  isLoading: boolean;
  errorDetail?: string | null;
  correlationId?: string | null;
}
```
- **Mapeo**: `currentPoints = score().totalPoints ?? score().currentPoints`, `securedPoints = securedPoints().securedPoints`, `checkpoint = securedPoints().checkpointRoundNumber`, `potentialDisplay = potentialReward() ?? "—"` (029 computed), `roundPoints = score().roundPoints ?? 0`, `totalPoints = score().totalPoints`.
- **Reglas**: Solo lectura, `hydrate` via `GET /players/me` restaura 5 métricas; `ScoreUpdated`/`RoundCompleted` → `hydrate`.
- **Hydrate**: `GetMyPlayerState` `score` + `securedPoints` + `game.configuration` → `ScoringDisplayState`.

### 7. PlayerGameStatus / Timer (reuse 029, referencia)

```ts
interface PlayerGameStatus {
  gameStatus: string;
  playerStatus: string;
  isTerminal: boolean;
  canAnswer: boolean;
}
interface Timer {
  timeLimitSeconds: number;
  expiresAt: string;
  remainingSeconds: number;
  state: 'RUNNING' | 'STOPPED' | 'EXPIRED';
  serverNow: string;
}
```
- **Uso**: `isTerminal` bloquea `ScoringDisplay` mutación pero sigue mostrando `Total Points` final; `Timer` no aplica a scoring pero comparte footer.

## Relationships

```
Player (sub) 1──N GameSession N──1 Game 1──N GameRound 1──1 Question
Player 1──1 Score 1──N PointTransaction (per Game) — sum=totalPoints (D)
Player 1──1 SecuredPoints (checkpointRoundNumber, policy) — protegido por LossPolicy (C)
Game 1──1 GameConfiguration 1──N RewardRules ── PotentialPoints (proyección)
Score (totalPoints/roundPoints) + SecuredPoints (securedPoints/checkpoint) + GameConfiguration (PointsPerRound) ── ScoringDisplayState (view-model 5 métricas) ── ScorePanelComponent
GameRealtimeService (ScoreUpdated/RoundCompleted/Reconnected) ──▶ hydrate → GET /players/me → ScoringDisplayState restore
```

## State Transitions (cliente observa, servidor decide)

- **View**: `idle (0 transacciones)` -- `ScoreUpdated ANSWER_CORRECT +100` --> `Current 100 Round 100 Total 100`; `RoundCompleted` --> `Secured 100 checkpoint 1 Round 0`; `ScoreUpdated` con `WITHDRAWAL -50` --> `Current 50 Secured 100 Total 50` (KEEP_SECURED_SCORE).
- **Hydrate**: `ScoreUpdated` → `hydrateFor(gameId)` → `GET /players/me` → patch `score/securedPoints` → `ScoringDisplayState` render `aria-live polite` `pulse` 600ms.
- **Timer**: No aplica a scoring, pero `RoundCompleted` resetea `Round Points` per `LossPolicy`.

## Validation Rules

- `score.totalPoints >=0`, `securedPoints.securedPoints >=0`, `roundPoints >=0`, `totalPoints >= securedPoints` (salvo `ADJUSTMENT` negativo).
- `checkpointRoundNumber` 1..MaxRounds o null; null → sin badge.
- `potentialPoints` null → "—" placeholder, no NaN.
- `totalPoints == sum(PointTransaction.points)` 100% (D).
- `X-Correlation-Id` UUID v4 per `GET /players/me`.
- `isCorrect` no aplica a scoring, pero `ScoreUpdated` solo tras `EVALUATED` (V).
- Cliente nunca calcula `Current/Secured/Total` local; solo proyección.

## Persistence (cliente)

- **En memoria**: `PlayerGameStore` `DeepSignal` `ScoringDisplayState` scoped per `gameId+roundId` (`providedIn` `GameComponent` `providers: [PlayerGameStore]`), `computed` para `potentialReward/roundPoints/totalPoints`.
- **Efímero**: Ninguno para scoring; `sessionStorage` solo para `Answer` idempotencia (031), no para puntuación.
- **Server**: SQL Server `PointTransaction` ledger `UNIQUE (GameId,PlayerId,CreatedAt)` + `Game` RowVersion + `GamePlayer` `Score` ValueObject + `GameConfiguration` `PointsPerRound/RewardRules`. `GetMyPlayerState` Query: `GameByIdWithAnswersSpecification` → `PlayerGameState` con `Score/SecuredPoints` filtrado server.

## Indexes / Queries (server reference)

- `PointTransaction` IX `(GameId, PlayerId, CreatedAt)` + `RowVersion`; `Game` IX `Status`.
- `GetMyPlayerState` Query: `GameByIdWithAnswersSpecification` + `AsNoTracking`, suma `PointTransaction` → `Score` DTO.

## UI States

- `Loading` skeleton 5 métricas `aria-busy`.
- `Empty` 0 pts en 5 métricas.
- `ErrorState` `ProblemDetails detail` + `CorrelationId/TraceId` + `Retry`.
- `Current Points` `var(--color-primary)` `font-weight 700` `pulse` tras `ScoreUpdated`.
- `Secured Points` `var(--color-success)` badge `asegurado`.
- `Round Points` `var(--color-warning)` "en juego".
- `Potential Points` `var(--color-accent)` o "—".
- `Total Points` `var(--color-primary)` bold `font-size var(--font-size-lg)`.
