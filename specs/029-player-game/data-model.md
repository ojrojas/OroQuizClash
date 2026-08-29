# Data Model: Player Game (029)

**Branch**: `029-player-game` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Overview
Modelo **solo lectura/proyección** en Angular 22 (Player Game screen) sobre `oroclash-api` `GET /api/games/{id}/players/me` (10 elementos) + mutaciones `POST /answers` `POST /withdraw`. Fuente autoritativa `OroQuizClash.Domain` (SQL Server). No nuevos agregados; `Potential Reward` es proyección `Reward`.

## Entities (Proyecciones cliente — TypeScript interfaces)

### 1. Game / GameSession
```ts
interface Game {
  gameId: string;          // GameId
  name: string;
  status: string;          // 9 estados
  maxRounds: number;
  configuration: GameConfiguration;
}
interface GameConfiguration {
  maxRounds: number;
  timeLimitPerQuestionSeconds: number;
  pointsPerRound: number;
  withdrawalPolicy: string; // KEEP_SECURED_SCORE etc.
  lossPolicy: string;
  rewardRules?: { rewardId?: string };
}
interface GameSession {
  gameSessionId: string;   // GamePlayerId
  playerId: string;        // sub
  gameId: string;
  status: string;          // ACTIVE/WITHDRAWN/ELIMINATED/WINNER
  currentRoundNumber: number;
  version: string;
}
```
- **Origen**: `Game` aggregate `GamePlayer` (`UNIQUE GameId+UserId`, `RowVersion`). `currentRoundNumber` texto "Ronda 3/10".
- **Relaciones**: `Game 1──N GameSession`.

### 2. Round
```ts
interface Round {
  roundId: string;
  gameId: string;
  roundNumber: number; // 1..max
  level: string;       // Basic..Expert Difficulty 1..5
  status: string;      // WAITING/IN_PROGRESS/COMPLETED
  questionId: string;
  startedAt: string;   // ISO UTC
  expiresAt: string;   // startedAt + timeLimit
  version: string;
}
```
- **Origen**: `GameRound` aggregate. `expiresAt` calculado server `startedAt + timeLimit`. `level` mapeado `Difficulty`.
- **Relaciones**: `Game 1──N Round 1──1 Question`.

### 3. Question / AnswerOption
```ts
interface Question {
  questionId: string;
  categoryId: string;
  text: string;
  answerOptions: AnswerOption[]; // exactamente 4
  difficulty: string;
}
interface AnswerOption { optionId: string; text: string; } // isCorrect nunca antes EVALUATED
```
- **Origen**: `Question` aggregate invariante B (4 opciones 1 correcta server-side).
- **Validación**: 4 opciones; cliente valida `selectedOptionId ∈ answerOptions`.

### 4. Answer
```ts
interface Answer {
  answerId: string | null;
  playerId: string;
  gameId: string;
  roundId: string;
  questionId: string;
  selectedOptionId: string | null;
  submittedAt: string | null; // server
  state: string; // PENDING/SUBMITTED/EVALUATED/EXPIRED
  isCorrect: boolean | null; // solo EVALUATED
  idempotencyKey: string; // UUID per player+round
}
```
- **Origen**: `Game.SubmitAnswer(submittedAt, expiresAt)` idempotente `X-Idempotency-Key` + `RowVersion`, `AnswerWindowExpired` si `submittedAt > expiresAt`.
- **Relaciones**: `Answer` per `GameSession` per `Round` `UNIQUE (GameId,RoundId,PlayerId)`.

### 5. Score / SecuredPoints / PointTransaction
```ts
interface Score { playerId: string; gameId: string; totalPoints: number; correctAnswers: number; currentLevel: string; }
interface SecuredPoints { playerId: string; gameId: string; securedPoints: number; checkpointRoundNumber: number|null; policy: string; }
interface PointTransaction { transactionId: string; type: string; points: number; roundNumber?: number; createdAt: string; }
```
- **Origen**: `PointTransaction` ledger (D) `sum(points)=totalPoints`; `SecuredPoints` derivado `KEEP_SECURED_SCORE` checkpoint.
- **Display**: "500 pts · 200 asegurados".

### 6. Potential Reward
```ts
interface PotentialReward { rewardId?: string; name: string; pointsRequired: number; display: string; } // "—" si no configurado
```
- **Origen**: `Reward` proyección `GameConfiguration.RewardRules.rewardId` → `Reward.Name` si ledger `points` próximo umbral.

### 7. Timer
```ts
interface Timer { timeLimitSeconds: number; expiresAt: string; remainingSeconds: number; state: 'RUNNING'|'STOPPED'|'EXPIRED'; serverNow: string; }
// remainingSeconds = max(0,floor((expiresAt - Date.now())/1000)) computed + interval + serverNow correction
```
- **Origen**: `Round.expiresAt` server; `serverNow` de `GET /players/me`/`QuestionAvailable`.
- **Validación**: decisión expiración solo server `submittedAt <= expiresAt`.

### 8. PlayerGameStatus
```ts
interface PlayerGameStatus { gameStatus: string; playerStatus: string; isTerminal: boolean; isExpired: boolean; canAnswer: boolean; }
// canAnswer = !isTerminal && round IN_PROGRESS && answer PENDING
```
- **Derivado**: `Game.status` + `GameSession.status` + `Round.status` + `Answer.state`.

## Relationships
```
Player (sub) 1──N GameSession N──1 Game 1──N Round 1──1 Question
                     │               │         1──4 AnswerOption
                     │               └── 1──N PointTransaction → Score/Secured/Potential
                     └── 1──1 Answer per Round (idempotente) + 1 Timer + 1 Status
GameSession ←→ Round via currentRoundNumber + expiresAt
Answer → Question (selectedOptionId) + IdempotencyKey
```

## State Transitions (cliente observa, servidor decide)
- **GameSession.status**: `ACTIVE → WITHDRAWN` (Withdraw) | `ACTIVE → ELIMINATED` (loss) | `ACTIVE → WINNER` (Finish) terminal.
- **Answer.state**: `PENDING → EVALUATED` (isCorrect) | `PENDING → EXPIRED` (timeout) idempotente.
- **Timer.state**: `STOPPED → RUNNING` (QuestionAvailable) → `STOPPED` (EVALUATED) | `EXPIRED` (0).
- **Game.status**: `WAITING → IN_PROGRESS → ROUND_IN_PROGRESS ↔ ROUND_COMPLETED → FINISHED`.

## Validation Rules
- `selectedOptionId` debe ∈ `answerOptions[*].optionId`.
- `X-Idempotency-Key` UUID v4 per `roundId` en `sessionStorage`; `X-Idempotency-Key` per `gameId` para withdraw.
- `currentRoundNumber` 1..maxRounds; `difficulty` 1..5.
- `Score/Secured` nunca editables cliente; `Potential` placeholder "—" si null.
- `remainingSeconds` clamped 0..timeLimit.

## Persistence (cliente)
- **En memoria**: `PlayerGameStore` `DeepSignal` scoped per `gameId` (aislado).
- **Efímero**: `sessionStorage` solo `idemp-{roundId}` y `idemp-withdraw-{gameId}` para reintento idempotente. Nunca `localStorage`.
- **Server**: SQL Server `GamePlayer` (rowversion UK), `GameRound`, `Answer` (UK GameId+RoundId+PlayerId IdempotencyKey), `PointTransaction` IX, `Reward` opcional, Outbox `PlayerJoined`/`AnswerSubmitted`.

## Indexes / Queries (server reference)
- `GamePlayer` UK `(GameId, PlayerId)` IX `PlayerId` RowVersion.
- `Answer` UK `(GameId, RoundId, PlayerId)` IX `IdempotencyKey` RowVersion.
- `PointTransaction` IX `(GameId, PlayerId, CreatedAt)`.
- `GetMyPlayerState` Query: `GameByIdWithPlayersSpecification` + `QuestionById` + `AnswerByRound` + `PointTransaction` ledger → `PlayerGameState` 10 elementos `AsNoTracking`.

## UI States
- `Loading` skeleton cinematic `aria-live="polite"`, `Empty` (no round), `Error` (ProblemDetails `detail` + `CorrelationId/TraceId` + Retry), `Expired` (Timer 0 `assertive`), `Terminal` (`WITHDRAWN/ELIMINATED/WINNER` block `canAnswer`).
