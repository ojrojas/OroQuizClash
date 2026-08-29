# Data Model: Player Answering (031)

**Branch**: `031-player-answering` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Overview

Modelo **solo lectura/proyección** en Angular 22 (selector 4 opciones + 8 estados) sobre `oroclash-api` `POST /api/games/{id}/answers` (idempotente `X-Idempotency-Key`) + `GET /api/games/{id}/players/me` (hydrate `Question`/`Answer`/`Timer`/`Status`) y `GET /questions/current` filtrado. Fuente autoritativa `OroQuizClash.Domain` (SQL Server `Question` 4/1 `CHECK exactly one correct`, `Answer` `UNIQUE (GameId,RoundId,PlayerId)` + `UNIQUE IdempotencyKey`, `PointTransaction` ledger). No nuevos agregados dominio salvo view-model `AnswerInteractionState` (8 estados) derivado de `Question` + `Answer` + `Timer` + `Status`. Complementa `PlayerGameStore` 10 elementos (029) y `PlayerRoundsStore` ladder (030) sin duplicar.

## Entities (Proyecciones cliente — TypeScript interfaces + Domain referencia)

### 1. Question / AnswerOption (Domain + view)

```ts
// Domain (server) — invariante B: exactamente 4, exactamente 1 correcta
interface QuestionDomain {
  questionId: string;        // QuestionId StronglyTypedId<Guid>
  categoryId: string;
  text: string;              // 1..500
  answerOptions: AnswerOptionDomain[4];
  difficulty: string;         // Basic..Expert
  status: 'PUBLISHED' | 'DRAFT';
  displayOrder?: number;
}
interface AnswerOptionDomain {
  optionId: string;          // OptionId UUID
  text: string;              // 1..200
  displayOrder: number;      // 0..3
  isCorrect: boolean;         // server-only, filtrado para PLAYER antes de EVALUATED
}

// View (cliente) — sin isCorrect pre-EVALUATED
interface AnswerOptionView {
  optionId: string;
  text: string;
  displayOrder: number;
  // isCorrect never available before EVALUATED (filtrado)
}

// Contract GET /api/games/{id}/players/me -> question
interface QuestionView {
  questionId: string;
  categoryId: string;
  text: string;
  answerOptions: AnswerOptionView[4]; // exactly 4 ordered by displayOrder
  difficulty: string;
}
```
- **Origen**: `Question : AggregateRoot<QuestionId>` `Question.Create(text, categoryId, difficulty, options[4])` con `IBusinessRule ExactlyFourOptions` + `ExactlyOneCorrect` + `CategoryMustHaveAtLeast5` antes de `PUBLISHED`. DB `CHECK (correctCount==1)` + `UNIQUE QuestionId+OptionId`.
- **Validación**: `answerOptions.length===4` sino `ErrorState` "Pregunta inválida (se requieren 4 opciones)" con `CorrelationId`; `text` vacío → placeholder "Opción sin texto" fallback.
- **Relaciones**: `GameRound 1──1 Question`; `Question 1──4 AnswerOption`.

### 2. Answer (Domain)

```ts
interface Answer {
  answerId: string | null;          // AnswerSubmissionId (Guid) | null si PENDING local
  playerId: string;                 // sub (JWT) — de GameClaims.GetSub, no body
  gameId: string;
  roundId: string;
  questionId: string;
  selectedOptionId: string | null;  // null si PENDING idle
  submittedAt: string | null;       // server ISO UTC, null si no enviado
  evaluatedAt: string | null;       // server ISO UTC, null si no EVALUATED
  state: 'PENDING' | 'SUBMITTED' | 'EVALUATED' | 'EXPIRED' | 'LOCKED'; // LOCKED view alias para PENDING+isLocked locally
  isCorrect: boolean | null;        // null si !EVALUATED, solo expuesta tras EVALUATED para PLAYER
  idempotencyKey: string;           // UUID v4 per playerId+roundId sessionStorage idemp-{roundId}
  rowVersion: string;               // RowVersion
}
```
- **Origen**: `Game.SubmitAnswer(playerId, roundId, questionId, selectedOptionId, submittedAt, expiresAt)` dominio con `IBusinessRule AnswerWindowNotExpired (submittedAt<=expiresAt)` → `AnswerWindowExpired 400` sino `Correct/Incorrect` ledger, + `QuestionAlreadyAnswered 409` idempotente (retorna mismo `Answer` sin duplicar `PointTransaction`). `RowVersion` en `Game`/`Answer` para optimistic concurrency.
- **Invariante**: `UNIQUE (GameId, RoundId, PlayerId)` + `UNIQUE IdempotencyKey` (`UNIQUE (PlayerId, RoundId, IdempotencyKey)`). `IdempotencyKey` per `playerId+roundId` en `sessionStorage idemp-{roundId}`.
- **Estado**: `PENDING` (no enviado) → `SUBMITTED` (enviado, isCorrect null) → `EVALUATED` (isCorrect true/false) | `EXPIRED` (isCorrect null, Timeout) ; `LOCKED` es alias local para `PENDING+selected+lockedOptionId` antes de `SUBMITTED`.

### 3. AnswerOptionState / AnswerInteractionState (View-Model 031 central)

```ts
type AnswerOptionState = 'Idle' | 'Hover' | 'Selected' | 'Locked' | 'Evaluating' | 'Correct' | 'Incorrect' | 'Timeout';
type AnswerPhase = 'idle' | 'selected' | 'locked' | 'evaluating' | 'correct' | 'incorrect' | 'timeout';

interface AnswerInteractionState {
  gameId: string | null;
  roundId: string | null;
  questionId: string | null;
  selectedOptionId: string | null;   // currently Selected (one unique)
  lockedOptionId: string | null;     // Locked inmutable per round
  phase: AnswerPhase;                // derived from selected/locked/isEvaluating/answer.state
  isEvaluating: boolean;             // true during POST /answers pending
  isLocked: boolean;                 // lockedOptionId != null
  canSelect: boolean;                // !isLocked && !isEvaluating && status.canAnswer && !isTerminal && answer.state PENDING
  errorDetail?: string;
  correlationId?: string;
}

// Derived per AnswerOptionView for rendering
interface AnswerOptionStateDerived {
  option: AnswerOptionView;
  state: AnswerOptionState;          // mapped from interactionState + answer.isCorrect + timer
  ariaChecked: boolean;
  ariaDisabled: boolean;
  ariaPosInSet: number; // 1..4
  ariaSetSize: 4;
}
```

- **Mapeo estados** (FR-002):
  ```
  Idle: !selected && !locked && !evaluating && !terminal
  Hover: Idle + mouse/focus (CSS :hover)
  Selected: selectedOptionId===optionId && !isLocked
  Locked: lockedOptionId===optionId && !isEvaluating && answer.state PENDING/SUBMITTED
  Evaluating: isEvaluating==true && lockedOptionId===optionId (spinner aria-busy)
  Correct: answer.state==EVALUATED && isCorrect==true && (lockedOptionId===optionId || option isCorrect autoritativo)
  Incorrect: answer.state==EVALUATED && isCorrect==false && lockedOptionId===optionId (rojo) + secondary Correct on isCorrect option
  Timeout: answer.state==EXPIRED || AnswerWindowExpired 400 (warning)
  ```
- **Reglas**: solo una `Selected` a la vez (seleccionar nueva mueve `Selected`); `Locked` inmutable (`canSelect=false`); `Evaluating` deshabilita Enviar + debounce 150ms; `Correct/Incorrect/Timeout` terminales (no re-select).
- **Hydrate**: `GetMyPlayerState` `answer.selectedOptionId` + `state` restaura `lockedOptionId`/`phase` tras recarga (server truth). `QuestionAvailable` nueva pregunta resetea `selected=locked=null phase=idle`.

### 4. PlayerGameStatus / Timer (reuse 029, referencia)

```ts
interface PlayerGameStatus {
  gameStatus: string;          // WAITING_FOR_PLAYERS | IN_PROGRESS | ROUND_IN_PROGRESS | ROUND_COMPLETED | FINISHED ...
  playerStatus: string;        // ACTIVE | WITHDRAWN | ELIMINATED | WINNER
  isTerminal: boolean;         // WITHDRAWN/ELIMINATED/WINNER/FINISHED
  canAnswer: boolean;          // !isTerminal && round IN_PROGRESS && answer PENDING
}
interface Timer {
  timeLimitSeconds: number;    // 5..300
  expiresAt: string;           // ISO UTC server
  remainingSeconds: number;    // computed max(0,floor((expiresAt - now)/1000))
  state: 'RUNNING' | 'STOPPED' | 'EXPIRED';
  serverNow: string;           // ISO UTC for drift correction
}
```
- **Decide Timeout**: `submittedAt <= expiresAt` server (FR-008). Cliente `Timer` solo visual con `interval(1000)` + `serverNow` corrección; decisión solo server. Si `Timer EXPIRED` y en `Selected` sin `Locked`, forzar `Timeout` local.

### 5. PointTransaction / Score (reuse 007, referencia)

```ts
interface PointTransaction {
  transactionId: string;
  type: 'ANSWER_CORRECT' | 'ANSWER_INCORRECT' | 'ROUND_BONUS' | 'WITHDRAWAL' | ...;
  points: number;
  roundNumber?: number;
  resultingBalance: number;
  createdAt: string;
}
interface Score {
  totalPoints: number;
  correctAnswers: number;
  currentLevel: string;
}
```
- **Generado** tras `Correct` (`ANSWER_CORRECT`) o `Incorrect` (`ANSWER_INCORRECT` + `LossPolicy`).

## Relationships

```
Player (sub) 1──N GameSession N──1 Game 1──N GameRound 1──1 Question 1──4 AnswerOption (UNIQUE QuestionId+OptionId, CHECK exactly one correct)
                                          │
Player 1──N Answer (per GameRound) N──1 GameRound — UNIQUE (GameId,RoundId,PlayerId) + UNIQUE IdempotencyKey (PlayerId+RoundId)
Answer.selectedOptionId ──▶ AnswerOption.optionId (must ∈ answerOptions)
AnswerInteractionState (view-model) ── derived from ── Question.answerOptions[4] + Answer (selectedOptionId/state/isCorrect) + Timer (expiresAt) + PlayerGameStatus (canAnswer/isTerminal)
AnswerOptionStateDerived ── 1 per AnswerOptionView (4) ── mapped from AnswerInteractionState + Answer.isCorrect (only post-EVALUATED)
GameRealtimeService (QuestionAvailable/ScoreUpdated) ──▶ hydrateAnswer() ──▶ GET /players/me ──▶ AnswerInteractionState restore
```

## State Transitions (cliente observa, servidor decide)

- **View Phase**: `idle (no selected)` --select--> `selected (one option aria-checked true)` --confirm--> `locked (aria-disabled, canSelect false)` --POST /answers--> `evaluating (aria-busy spinner)` --200 EVALUATED isCorrect--> `correct` / `incorrect` (incorrect + correct secondary) | --400 AnswerWindowExpired / EXPIRED--> `timeout` (terminal).
- **Interaction Guard**: `selectOption(optionId)` ignora si `isLocked || isEvaluating || !canAnswer` (terminal/PENDING). Debounce 150ms coalesce double-click.
- **Server States**: `Answer.state PENDING` → `SUBMITTED` (enviado) → `EVALUATED` (isCorrect true/false) | `EXPIRED` (isCorrect null) . `Locked` local es `PENDING+lockedOptionId` antes de `SUBMITTED`.
- **Hydrate Transitions**: `QuestionAvailable` nueva ronda → reset `selected/locked null phase idle`; `ScoreUpdated` post-Correct → `phase correct`; reconnect → `hydrateAnswer()` restaura `locked/evaluating/correct` desde `Answer.state`.
- **Timer Transition**: `RUNNING` → `EXPIRED` (remaining 0) fuerza `Timeout` si estaba `selected` sin `Locked`.

## Validation Rules

- `question.answerOptions.length === 4` sino `ErrorState` "Pregunta inválida (se requieren 4 opciones)" con `CorrelationId`, `canSelect=false`.
- `answerOptions` orden `displayOrder 0..3` sin gaps; `optionId` UUID v4; `text` 1..200 non-empty (empty → placeholder "Opción sin texto").
- `selectedOptionId` debe ∈ `answerOptions[*].optionId` sino validación local "Selecciona una opción" sin llamada.
- `lockedOptionId` inmutable tras `confirmLock()`; intento cambiar → ignorado local + 409 server.
- `X-Idempotency-Key` UUID v4 per `roundId` en `sessionStorage idemp-{roundId}`; reuso misma key para Retry.
- `X-Correlation-Id` UUID v4 per `POST /answers`.
- `canSelect = canAnswer && !isLocked && !isEvaluating && !isTerminal && answer.state PENDING && timer.state != EXPIRED`.
- `isCorrect` no expuesto para `PLAYER` antes de `EVALUATED` (filtrado server, contract test verifies 0% leak).
- `SubmittedAt` server, no `Date.now()` cliente; `expiresAt` server decide `Timeout`.

## Persistence (cliente)

- **En memoria**: `AnswerInteractionStore` `DeepSignal` `AnswerInteractionState` scoped per `gameId+roundId` (aislado, `providedIn` component `providers: [AnswerInteractionStore]` o `PlayerGameStore` extension), `computed` for `canSelect/isLocked/isEvaluating/phase`.
- **Efímero**: `sessionStorage` solo `idemp-{roundId}` UUID per `Round` para idempotencia Retry sin duplicar. Nunca `localStorage`.
- **Server**: SQL Server `Question` `CHECK exactly one correct` + `UNIQUE OptionId` + `Answer` `UNIQUE (GameId,RoundId,PlayerId)` + `UNIQUE IdempotencyKey` + `Game` RowVersion + `GameRound` `QuestionId` FK. `SubmitAnswer` creates `Answer` + `PointTransaction` ledger atomic `SaveChanges` + Outbox. `GetMyPlayerState` Query: `GameByIdWithAnswersSpecification` + `QuestionById` + `AnswerByRound` → `PlayerGameState` con `question` filtrada `isCorrect` null para `PLAYER` si `PENDING`.

## Indexes / Queries (server reference)

- `Question` IX `CategoryId, Difficulty` + `Status PUBLISHED`.
- `AnswerOption` UK `(QuestionId, OptionId)` + `CHECK correctCount==1`.
- `Answer` UK `(GameId, RoundId, PlayerId)` IX `IdempotencyKey` RowVersion; `PointTransaction` IX `(GameId, PlayerId, CreatedAt)`.
- `GetCurrentQuestion` / `GetMyPlayerState` Query: `QuestionByIdSpecification` + filtrar `isCorrect` per role `PLAYER` vs `ADMIN`, `AsNoTracking`.

## UI States

- `Idle` default card `background var(--color-surface)` `border var(--color-border)`.
- `Hover` `:hover` `border var(--color-primary)` `box-shadow var(--shadow-hover)` `scale(1.01)` (respects `prefers-reduced-motion`).
- `Selected` `background var(--color-primary-subtle)` `border var(--color-primary)` check `var(--color-primary)` `aria-checked true`.
- `Locked` `opacity 0.9` `aria-disabled` deshabilita otras, `Selected` `disabled`.
- `Evaluating` `pulse var(--color-primary)` `spinner` `aria-busy` `aria-live="polite"` "Evaluando…".
- `Correct` `background var(--color-success) color var(--color-success-contrast)` `aria-live="assertive"` "¡Correcto! +X pts" check anim <300ms.
- `Incorrect` `background var(--color-error)` + secondary `Correct` on isCorrect option.
- `Timeout` `background var(--color-warning)` "Tiempo agotado" `aria-live="assertive"`.
- `ErrorState` `ProblemDetails detail` + `CorrelationId/TraceId` + `Retry` reusa `X-Idempotency-Key`.
