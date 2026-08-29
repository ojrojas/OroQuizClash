# Data Model: Player Rounds (030)

**Branch**: `030-player-rounds` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Overview

Modelo **solo lectura/proyección** en Angular 22 (ladder Round 1..N) sobre `oroclash-api` `GET /api/games/{id}/players/me` (N filas + rewards ledger) y eventos `GameHub` → `hydrate` autoritativo. Fuente autoritativa `OroQuizClash.Domain` (SQL Server `Game`/`GameRound`/`PointTransaction`/`Reward`). No nuevos agregados; `LadderRow` es view-model cliente derivado de `GameRound` + `RewardRule` + `SecuredPoints`. Complementa `PlayerGameStore` 10 elementos (029) sin duplicar.

## Entities (Proyecciones cliente — TypeScript interfaces)

### 1. Game / GameSession (reuse 029, amplía para ladder)

```ts
interface Game {
  gameId: string;          // GameId StronglyTypedId<Guid>
  name: string;
  status: string;          // 9 estados DRAFT..FINISHED
  maxRounds: number;       // N 5..15 inmutable tras StartGame
  configuration: GameConfiguration;
}

interface GameConfiguration {
  maxRounds: number;
  timeLimitPerQuestionSeconds: number;
  pointsPerRound: number;
  withdrawalPolicy: string; // KEEP_SECURED_SCORE | LOSE_ALL etc.
  lossPolicy: string;
  difficultyStrategy: string; // Linear | Progressive | Adaptive | CategorySpecific
  rewardRules?: RewardRule[]; // por RoundThreshold
  pointsPerRound?: number;
}

interface GameSession {
  gameSessionId: string;   // GamePlayerId
  playerId: string;        // sub (JWT)
  gameId: string;
  status: string;          // ACTIVE | WITHDRAWN | ELIMINATED | WINNER
  currentRoundNumber: number | null; // null en WAITING_FOR_PLAYERS
  version: string;         // RowVersion
  isTerminal: boolean;     // WITHDRAWN/ELIMINATED/WINNER/FINISHED
}
```
- **Origen**: `Game` aggregate `GamePlayer` (`UNIQUE GameId+UserId`, `RowVersion`). `maxRounds` define N ladder; `currentRoundNumber` es Current Level.
- **Relaciones**: `Game 1──N GameSession`; `GameSession` scoped per `sub`.

### 2. Round / GameRound (por fila ladder)

```ts
interface Round {
  roundId: string | null;  // null para placeholder futuro (aún no creado server)
  gameId: string;
  roundNumber: number;     // 1..N único por GameId
  level: string;           // Basic | Elementary | Intermediate | Advanced | Expert | CategorySpecific "Geografía — Hard"
  difficulty: number;      // 1..5 (si Enumeration)
  status: string;          // WAITING | IN_PROGRESS | COMPLETED | null (placeholder)
  questionId: string | null;
  startedAt: string | null; // ISO UTC
  expiresAt: string | null;
  completedAt: string | null;
}
```
- **Origen**: `GameRound` `Entity<GameRoundId>` dentro de `Game` (SPEC-005). `level` mapeado `IDifficultyProgressionStrategy NextDifficulty(game, completedRounds)`. Placeholder filas futuras (roundNumber > roundsCreated) tienen `roundId=null` y level proyectado por strategy.
- **Relaciones**: `Game 1──N Round`; `Round 1──1 Question` si `questionId`.

### 3. DifficultyLevel

```ts
type DifficultyLevel = 'Basic' | 'Elementary' | 'Intermediate' | 'Advanced' | 'Expert' | string; // CategorySpecific open
interface DifficultyStrategy {
  name: 'Linear' | 'Progressive' | 'Adaptive' | 'CategorySpecific';
  nextDifficulty(completedRounds: number): DifficultyLevel; // server truth, cliente solo proyecta
}
```
- **Origen**: `DifficultyLevel : Enumeration 1..5` (SPEC-001/005). Cliente nunca calcula, solo muestra `Round.level`.

### 4. RewardRule / Reward (recompensas por ronda)

```ts
interface RewardRule {
  rewardId?: string;
  roundThreshold: number;  // 1..N e.g. 5→500, 10→5000
  name: string;            // e.g. "Pack Oro 500 pts"
  pointsRequired: number;  // e.g. 500
  points?: number;         // alias
}
interface Reward {
  rewardId: string;
  name: string;
  pointsRequired: number;
  roundThreshold: number;
}
```
- **Origen**: `Reward` aggregate + `GameConfiguration.RewardRules` (SPEC-001/009). `Current Reward` = rule `roundThreshold===current`, `Next` = `current+1`, `Final` = `maxRounds`. Fallback `pointsPerRound * roundNumber` si regla vacía.

### 5. SecuredPoints / PointTransaction (ledger)

```ts
interface SecuredPoints {
  playerId: string;
  gameId: string;
  securedPoints: number;           // 0 si LOSE_ALL
  checkpointRoundNumber: number | null; // último round asegurado (e.g. 5)
  policy: string;                  // KEEP_SECURED_SCORE etc.
}
interface PointTransaction {
  transactionId: string;
  playerId: string;
  gameId: string;
  roundNumber?: number;
  type: string; // ANSWER_CORRECT | ANSWER_INCORRECT | ROUND_BONUS | LEVEL_BONUS | GAME_BONUS | PENALTY | WITHDRAWAL ...
  points: number;
  resultingBalance: number;
  createdAt: string;
  idempotencyKey?: string;
}
```
- **Origen**: `PointTransaction` ledger append-only (D) `sum(points)=totalPoints`; `SecuredPoints` derivado `KEEP_SECURED_SCORE` checkpoint. Usado para `Secured Reward` y filas `isSecured` (roundNumber <= checkpoint).

### 6. LadderState / LadderRow (view-model central 030)

```ts
interface LadderRow {
  roundNumber: number;           // 1..N
  level: string;                 // Difficulty text
  difficulty: number | null;     // 1..5
  state: 'completed' | 'current' | 'upcoming'; // vs currentRoundNumber
  isSecured: boolean;            // roundNumber <= checkpointRoundNumber && securedPoints>0
  isFinal: boolean;              // roundNumber === maxRounds
  currentReward: string | null;  // "600 pts" o null → "—"
  nextRewardFlag: boolean;       // true si roundNumber === currentRoundNumber+1
  securedFlag: boolean;          // true si roundNumber === checkpointRoundNumber
  isCurrentReward: boolean;      // roundNumber === currentRoundNumber
  ariaLabel: string;             // "Ronda 4 de 10, nivel Intermediate, recompensa 600 puntos, asegurado"
}

interface LadderState {
  gameId: string | null;
  maxRounds: number;
  currentRoundNumber: number | null;
  ladder: LadderRow[];           // size N
  secured: SecuredPoints | null;
  rewardRules: RewardRule[];
  status: 'loading' | 'empty' | 'ready' | 'error' | 'terminal';
  correlationId?: string;
  errorDetail?: string;
  _animatingRound: number | null; // para transición <400ms
  previousRoundNumber: number | null; // para detectar retroceso
}
```
- **Derivado**: `buildLadder(maxRounds, rounds[], rewardRules, secured, current, pointsPerRound)` puro, testeable. `state` lógica: `completed` si `< current`, `current` si `=== current`, `upcoming` si `> current`; `isFinal` siempre última fila; `isSecured` si `<= checkpoint`.
- **Transición**: `_animatingRound` set a `current` 350ms luego null (`effect` + `setTimeout`), `previousRoundNumber` detecta salto >1 (reconnect).

### 7. Timer / PlayerGameStatus (reuse 029, referencia)

```ts
interface Timer { timeLimitSeconds: number; expiresAt: string; remainingSeconds: number; state: 'RUNNING'|'STOPPED'|'EXPIRED'; serverNow: string; }
interface PlayerGameStatus { gameStatus: string; playerStatus: string; isTerminal: boolean; isExpired: boolean; canAnswer: boolean; }
```
- **No cambia** en 030, pero `isTerminal` bloquea transición ladder.

## Relationships

```
Player (sub) 1──N GameSession N──1 Game 1──N Round (N = maxRounds, UNIQUE GameId+RoundNumber)
                                     │         1──1 Question (si created)
                                     └── 1──N PointTransaction → SecuredPoints
                                     └── 1──N RewardRule (threshold 1..N) → LadderRow.currentReward/next/secured/final
GameSession.currentRoundNumber ──▶ LadderRow.state (completed/current/upcoming)
SecuredPoints.checkpointRoundNumber ──▶ LadderRow.isSecured / securedFlag
Game.maxRounds ──▶ LadderState.ladder size N + LadderRow.isFinal (Nth)
PlayerRoundsStore (LadderState) ←hydrate─ GameRealtimeService (RoundCompleted/QuestionAvailable/ScoreUpdated/GameFinished/Reconnected) ← GET /players/me (autoritative)
PlayerRoundsComponent ← LadderState (input computed) → aria-current, escudo, corona
```

## State Transitions (cliente observa, servidor decide)

- **LadderRow.state**: `upcoming → current` (hydrate `currentRoundNumber` incrementa) → `completed` (hydrate next increment). Retroceso posible si hydrate corrige (current decrece) → `current → upcoming` o `completed → current`.
- **LadderState.status**: `loading` (hydrate pending) → `ready` (N filas + current) | `empty` (current null WAITING) | `error` (hydrate fail CorrelationId) | `terminal` (isTerminal bloquea transición).
- **Transición animación**: `_animatingRound = current` 0→350ms → null; si `previousRoundNumber` diff >1, animación directa sin intermedios.
- **GameSession.status / Round.status**: igual que 029 (ACTIVE→WITHDRAWN terminal).

## Validation Rules

- `maxRounds` 5..15 (≥5 invariante SPEC-005); `ladder.length === maxRounds` sin huecos 1..N.
- `currentRoundNumber` null | 1..maxRounds; si `current` > N → error invariante.
- `LadderRow.level` no vacío (Basic..Expert o CategorySpecific).
- `RewardRule.roundThreshold` 1..N único; `pointsRequired` ≥0.
- `SecuredPoints.securedPoints` ≥0; `checkpointRoundNumber` null | 1..maxRounds; si `policy===LOSE_ALL` → `securedPoints===0`.
- `LadderRow.isFinal` solo `roundNumber===maxRounds`; `isSecured` solo si `checkpoint` no null y `roundNumber <= checkpoint`.
- `currentReward` placeholder "—" si `rewardRules` vacío o no threshold; no null break layout.
- `X-Correlation-Id` UUID v4 per hydrate; `aria-current` solo 1 fila.
- `remainingSeconds` no aplica ladder pero `reward` nunca calculado cliente.

## Persistence (cliente)

- **En memoria**: `PlayerRoundsStore` `DeepSignal` `LadderState` scoped per `gameId` (aislado, `providedIn` component `providers: [PlayerRoundsStore]`), `computed` memoization para `currentLevel/previousLevels`.
- **Efímero**: Ninguno nuevo (withdraw idempotency ya en 029 `sessionStorage idemp-withdraw-{gameId}`); ladder no persiste.
- **Server**: SQL Server `Game` RowVersion, `GameRound` `UNIQUE (GameId,RoundNumber)` `UNIQUE (GameId,QuestionId)` opcional, `PointTransaction` IX `(GameId,PlayerId,CreatedAt)`, `Reward` opcional, Outbox. `GetMyPlayerState` Query: `GameByIdWithPlayersSpecification` + `GameRounds` Include + `RewardRules` + `PointTransaction` ledger → `PlayerGameState` con `ladder` projection `AsNoTracking`.

## Indexes / Queries (server reference)

- `GamePlayer` UK `(GameId, PlayerId)` IX `PlayerId` RowVersion.
- `GameRound` UK `(GameId, RoundNumber)` IX `GameId` + `QuestionId`.
- `PointTransaction` IX `(GameId, PlayerId, CreatedAt)` para `SecuredPoints` sum.
- `Reward` IX `(RoundThreshold)` si RewardRules tabla.
- `GetMyPlayerState` Query extensible: retorna `maxRounds`, `currentRoundNumber`, `rounds: [{roundNumber, level, status, questionId}]`, `rewardRules`, `securedPoints {securedPoints, checkpointRoundNumber}`, `ledger` si necesario para Current/Next/Final derivation.

## UI States

- `Loading` skeleton ladder 5–10 filas `aria-busy` `aria-live="polite"`.
- `Empty` (`WAITING_FOR_PLAYERS` current null) → "Aún no inicia — N rondas por jugar" `role="status"` ladder muestra N filas `upcoming` sin Current.
- `Error` (`hydrate` fail) → `ProblemDetails detail` + `CorrelationId/TraceId` + Retry CTA → `hydrateLadder()`.
- `Terminal` (`WITHDRAWN/ELIMINATED/FINISHED` `isTerminal`) → bloquea `_animating`, muestra Secured/Final finales, filas `completed` hasta current, sin `upcoming` transición.
- `Ready` → ladder vertical N filas con Current premium `aria-current`, Previous check, Next upcoming muted, Secured escudo, Final corona gradiente.
