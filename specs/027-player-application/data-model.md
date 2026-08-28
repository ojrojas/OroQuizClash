# Data Model: Player Application (027)

**Branch**: `027-player-application` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Overview

Modelo de datos **solo lectura/proyección** en el cliente Angular 22 (SignalStore). La fuente autoritativa permanece en `OroQuizClash.Domain` (SQL Server + Oracle abstraction). El cliente proyecta 10 elementos de contexto privado por `GameSession` y los sincroniza vía REST + SignalR. No se introduce nuevo agregado de dominio salvo `GetMyPlayerState` query (proyección).

## Entities (Proyecciones cliente — TypeScript interfaces)

### 1. Player
```ts
interface Player {
  playerId: string;        // sub del JWT (StronglyTypedId GamePlayerId)
  displayName: string;
  email: string;
  tenantId?: string;
  roles: string[];         // del token (roles claim)
  mustChangePassword: boolean;
}
```
- **Origen**: OroIdentityServer `connect/userinfo` + `sub` claim. No persistido en oroclash DB (Constitución VI/H).
- **Validación**: `playerId` requerido; `must_change_password` gating bloquea juego.
- **Relaciones**: 1 Player → N GameSession (distintos juegos).

### 2. Game
```ts
interface Game {
  gameId: string;          // GameId (StronglyTypedId)
  name: string;
  status: GameStatus;      // Enumeration: DRAFT | READY | WAITING_FOR_PLAYERS | IN_PROGRESS | ROUND_IN_PROGRESS | ROUND_COMPLETED | FINISHED | CANCELLED | FORCED_FINISHED
  categoryId: string;
  categoryName: string;
  configuration: GameConfiguration;
  maxPlayers: number;      // default 10
  minPlayers: number;      // default 2
}

interface GameConfiguration {
  categoryId: string;
  minRounds: number;       // ≥5
  maxRounds: number;
  initialDifficulty: DifficultyLevel; // 5 niveles (Basic..Expert)
  progressionStrategy: 'Linear' | 'Progressive' | 'Adaptive' | 'CategorySpecific';
  timeLimitPerQuestionSeconds: number; // e.g. 30
  pointsPerRound: number;
  withdrawalPolicy: 'LOSE_ALL' | 'KEEP_CURRENT_SCORE' | 'KEEP_SECURED_SCORE' | 'KEEP_CHECKPOINT_SCORE';
  lossPolicy: 'LOSE_ALL' | 'LOSE_CURRENT_ROUND' | 'LOSE_UNSECURED_POINTS' | 'FALLBACK_TO_CHECKPOINT';
}
```
- **Origen**: `Game` aggregate (Domain). Inmutable tras `Start`.
- **Validación**: `status` transiciones validadas por dominio; cliente solo visualiza.
- **Relaciones**: 1 Game → N GameSession, N Round.

### 3. GameSession (GamePlayer — participación)
```ts
interface GameSession {
  gameSessionId: string;   // GamePlayerId
  playerId: string;        // sub
  gameId: string;
  status: PlayerStatus;    // ACTIVE | WITHDRAWN | ELIMINATED | WINNER (terminal: withdrawn/eliminated)
  joinedAt: string;        // ISO 8601 UTC server timestamp
  currentRoundNumber: number; // última ronda alcanzada; congelado si terminal
  version: string;         // rowversion base64 (concurrencia optimista)
}
type PlayerStatus = 'ACTIVE' | 'WITHDRAWN' | 'ELIMINATED' | 'WINNER';
```
- **Origen**: `GamePlayer` aggregate. Único por `playerId+gameId` (UK). Terminal no vuelve a ACTIVE.
- **Validación**: creación solo en `WAITING_FOR_PLAYERS`; `version` para `If-Match`/concurrencia.

### 4. Round
```ts
interface Round {
  roundId: string;         // GameRoundId
  gameId: string;
  roundNumber: number;     // 1..maxRounds
  level: DifficultyLevel;
  status: RoundStatus;     // WAITING | IN_PROGRESS | COMPLETED
  questionId: string;
  startedAt: string;       // server timestamp
  expiresAt: string;       // server timestamp (startedAt + timeLimit)
  version: string;
}
type RoundStatus = 'WAITING' | 'IN_PROGRESS' | 'COMPLETED';
```
- **Origen**: `GameRound` aggregate. Compartida por jugadores activos.
- **Relaciones**: 1 Round → 1 Question; 1 Game → N Round.

### 5. Question
```ts
interface Question {
  questionId: string;
  categoryId: string;
  text: string;
  answerOptions: AnswerOption[]; // exactamente 4
  complexity: string;
  academicLevel: string;
  ageRange: string;
  knowledgeCategory: string;
  difficulty: DifficultyLevel;
}
interface AnswerOption {
  optionId: string;
  text: string;
  // isCorrect NUNCA expuesto antes de evaluación (solo tras EVALUATED)
}
```
- **Origen**: `Question` + `AnswerOption` aggregates. Invariante: 4 opciones, 1 correcta server-side.
- **Validación**: categoría activa, ≥5 preguntas por categoría antes de publicación (Constraint B).

### 6. Answer
```ts
interface Answer {
  answerId: string;        // AnswerSubmissionId
  playerId: string;
  gameId: string;
  roundId: string;
  questionId: string;
  selectedOptionId: string | null; // null si EXPIRED sin envío
  submittedAt: string | null; // server timestamp, null si no enviada
  state: AnswerState;      // PENDING | SUBMITTED | EVALUATED | EXPIRED
  isCorrect: boolean | null; // solo si EVALUATED
  idempotencyKey: string;  // UUID v4 por jugador+ronda
}
type AnswerState = 'PENDING' | 'SUBMITTED' | 'EVALUATED' | 'EXPIRED';
```
- **Origen**: `Game.SubmitAnswer()` (Domain). Idempotente por `idempotencyKey` (player+round).
- **Validación**: solo si `PlayerStatus=ACTIVE` y `GameStatus=IN_PROGRESS/ROUND_IN_PROGRESS` y dentro de `expiresAt`.

### 7. Score
```ts
interface Score {
  playerId: string;
  gameId: string;
  totalPoints: number;     // derivado ledger PointTransaction
  correctAnswers: number;
  currentLevel: DifficultyLevel;
  transactions: PointTransaction[]; // opcional, para auditoría/detalle
}
interface PointTransaction {
  transactionId: string;
  type: 'ANSWER_CORRECT' | 'ANSWER_INCORRECT' | 'ROUND_BONUS' | 'LEVEL_BONUS' | 'GAME_BONUS' | 'PENALTY' | 'WITHDRAWAL' | 'REWARD_REDEMPTION' | 'CONSOLATION' | 'ADJUSTMENT';
  points: number;          // +/-
  roundNumber?: number;
  createdAt: string;
}
```
- **Origen**: `PointTransaction` ledger (Constraint D). Reconstruible.
- **Validación**: `totalPoints = sum(transactions.points)`.

### 8. SecuredPoints (Checkpoint)
```ts
interface SecuredPoints {
  playerId: string;
  gameId: string;
  securedPoints: number;   // 0 si sin checkpoint
  checkpointRoundNumber: number | null;
  policy: WithdrawalPolicy | LossPolicy;
}
```
- **Origen**: derivado de políticas `KEEP_SECURED_SCORE`/`KEEP_CHECKPOINT_SCORE`/`FALLBACK_TO_CHECKPOINT` + `SecurePoints` domain behavior.
- **Validación**: solo actualizado por eventos autoritativos (checkpoint alcanzado, pérdida).

### 9. Timer
```ts
interface Timer {
  timeLimitSeconds: number; // de GameConfiguration
  expiresAt: string;       // server timestamp ISO UTC
  remainingSeconds: number; // computed: max(0, floor((expiresAt - now)/1000))
  state: 'RUNNING' | 'STOPPED' | 'EXPIRED';
  serverNow: string;       // último server timestamp conocido para drift correction
}
```
- **Origen**: `Round.expiresAt` (server). Cliente calcula `remainingSeconds` con `computed` + `interval`.
- **Validación**: decisión expiración solo server (`submittedAt <= expiresAt`).

### 10. Status (combinado)
```ts
interface PlayerGameStatus {
  gameStatus: GameStatus;
  playerStatus: PlayerStatus;
  isTerminal: boolean;     // computed: playerStatus in ('WITHDRAWN','ELIMINATED') || gameStatus in ('FINISHED','CANCELLED','FORCED_FINISHED')
  canAnswer: boolean;      // computed: !isTerminal && roundStatus==='IN_PROGRESS' && answerState==='PENDING'
}
```
- **Derivado** de `Game.status` + `GameSession.status` + `Round.status` + `Answer.state`.

## Relationships

```
Player (sub) 1──N GameSession N──1 Game 1──N Round 1──1 Question
                    │                    │
                    │ 1──1 Answer (per Round per Player)
                    │ 1──1 Score (ledger)
                    │ 1──1 SecuredPoints
                    └──1 Timer (per Round) + Status
GameSession ←→ Round via currentRoundNumber
Answer → Question (selectedOptionId ∈ Question.answerOptions)
Score ← PointTransaction (ledger)
```

## State Transitions (cliente observa, servidor decide)

**GameSession.status**: `ACTIVE → WITHDRAWN` (WithdrawPlayer) | `ACTIVE → ELIMINATED` (loss policy) | `ACTIVE → WINNER` (Finish). Terminal no retorna a ACTIVE.

**Answer.state**: `PENDING → SUBMITTED` (envío) → `EVALUATED` (server eval, isCorrect) | `PENDING → EXPIRED` (timeout). Idempotente: segundo SUBMITTED con misma idempotencyKey retorna mismo resultado sin efecto.

**Timer.state**: `STOPPED → RUNNING` (QuestionAvailable) → `STOPPED` (evaluated) | `EXPIRED` (timeout).

**Game.status**: `WAITING_FOR_PLAYERS → IN_PROGRESS → ROUND_IN_PROGRESS ↔ ROUND_COMPLETED (loop) → FINISHED/CANCELLED/FORCED_FINISHED`.

## Validation Rules (cliente — refleja dominio)

- `Answer.selectedOptionId` requerido y debe pertenecer a `Question.answerOptions[*].optionId`.
- `Answer.idempotencyKey` UUID v4 único por `playerId+roundId`; reintentos usan misma key.
- `GameSession` creación bloqueada si `Game.status ≠ WAITING_FOR_PLAYERS` o `players.length ≥ maxPlayers`.
- `Score`/`SecuredPoints` nunca editables por cliente (solo visualización).
- `Timer.remainingSeconds` clamped 0..timeLimitSeconds.

## Persistence (cliente)

- **En memoria**: SignalStore `DeepSignal` por GameSession (aislado, FR-003).
- **Efímero**: `sessionStorage` solo para `idempotencyKey` del round actual (para reintento tras reload sin duplicar). Nunca `localStorage` entre identidades.
- **Server**: SQL Server `GamePlayer` (rowversion), `GameRound`, `PointTransaction`, `AnswerSubmission` (idempotency), Outbox.

## Indexes / Queries (server — reference)

- `GamePlayer` UK `(GameId, PlayerId)`, IX `PlayerId`.
- `PointTransaction` IX `(GameId, PlayerId, CreatedAt)`.
- `AnswerSubmission` UK `(GameId, RoundId, PlayerId)` + IX `IdempotencyKey`.

