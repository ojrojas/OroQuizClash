# API Contracts: Player Answering (031)

**Branch**: `031-player-answering` | **Date**: 2026-08-29 | **Spec**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)

No nuevo endpoint. Reusa `POST /api/games/{id}/answers` (`SubmitAnswer` ya en SPEC-006/029), `GET /api/games/{id}/players/me` (`GetMyPlayerState`), y opcional `GET /api/games/{id}/rounds/current/questions/current` (`GetCurrentQuestion`) filtrado.

## 1. POST /api/games/{gameId}/answers

**Reuse** `SubmitAnswer` Command (SPEC-006) — verbatim contrato ya existente, se documenta idempotencia y `isCorrect` filtrado.

### Request

```
POST /api/games/{gameId}/answers
Authorization: Bearer <JWT oroclash-api, sub=PlayerId>
X-Idempotency-Key: <uuid v4 per playerId+roundId, sessionStorage idemp-{roundId}>
X-Correlation-Id: <uuid v4>
Content-Type: application/json

{
  "roundId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "questionId": "3fa85f64-5717-4562-b3fc-2c963f66afa7",
  "selectedOptionId": "3fa85f64-5717-4562-b3fc-2c963f66afa8",
  "idempotencyKey": "same-as-header-optional"
}
```

- `gameId`: Guid `GameId`.
- Auth: JWT `jwks_uri`, `sub=PlayerId` (de `GameClaims.GetSub`), `must_change_password` gating 302 → `/auth/change-password`; sin JWT → 401.
- Idempotency: `X-Idempotency-Key` UUID v4 per `playerId+roundId` (`sessionStorage idemp-{roundId}`); reuso misma key para Retry. Server `UNIQUE IdempotencyKey` + `UNIQUE (GameId,RoundId,PlayerId)`.
- Validation: `selectedOptionId` debe ∈ `question.answerOptions[*].optionId` (4 opciones) sino `400 InvalidAnswer`.

### Responses

#### 200 OK — EVALUATED (dentro de `TimeLimit` y `canAnswer`)

```json
{
  "answerId": "ans-uuid",
  "playerId": "sub-123",
  "gameId": "game-uuid",
  "roundId": "round-uuid",
  "questionId": "q-uuid",
  "selectedOptionId": "opt-B-uuid",
  "submittedAt": "2026-08-29T12:00:10Z",
  "evaluatedAt": "2026-08-29T12:00:11Z",
  "state": "EVALUATED",
  "isCorrect": true,
  "idempotencyKey": "idemp-uuid",
  "scoreDelta": 100,
  "correlationId": "corr-uuid"
}
```

- `isCorrect` solo presente cuando `state==EVALUATED` (antes `null`). `EVALUATED` `isCorrect true` → `Correct`, `false` → `Incorrect` (UI resalta además `correctOptionId` de `GET /players/me` post-EVALUATED).
- `state` puede ser `SUBMITTED` (evaluación async corta) → cliente permanece `Evaluating` hasta `hydrate` polling `GET /players/me` que retornará `EVALUATED`.

#### 400 AnswerWindowExpired — Timeout

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "AnswerWindowExpired",
  "detail": "submittedAt 12:00:31Z > expiresAt 12:00:30Z",
  "status": 400,
  "code": "AnswerWindowExpired",
  "traceId": "00-...",
  "correlationId": "corr-uuid"
}
```

- UI → `Timeout` (`var(--color-warning)` "Tiempo agotado" `aria-live="assertive"`). `Timer` server `submittedAt` vs `expiresAt` decide.

#### 409 QuestionAlreadyAnswered — Locked re-send (idempotente)

```json
{
  "type": "https://httpstatuses.com/409",
  "title": "QuestionAlreadyAnswered",
  "detail": "Answer already submitted for this round",
  "status": 409,
  "code": "QuestionAlreadyAnswered",
  "traceId": "00-...",
  "correlationId": "corr-uuid"
}
```

- Si reenvío misma `X-Idempotency-Key` → retorna `200` mismo `Answer` (idempotente, no `409`). Si reenvío distinto `selectedOptionId` tras `Locked` pero misma `RoundId+PlayerId` sin misma key → `409`. No duplica `PointTransaction` ledger `COUNT` (verificado).

#### 403 PlayerIdentityMismatch / PlayerNotInGame

```json
{
  "type": "https://httpstatuses.com/403",
  "title": "PlayerIdentityMismatch",
  "status": 403,
  "code": "PlayerIdentityMismatch",
  "correlationId": "corr-uuid"
}
```

#### 404 GameNotFound / QuestionNotFound

RFC7807.

### Headers

- `X-Correlation-Id` echo en response; `X-Idempotency-Key` no necesita echo pero `Answer.idempotencyKey` lo confirma.

## 2. GET /api/games/{gameId}/players/me

**Reuse** `GetMyPlayerState` (029) — para `hydrateAnswer`.

Relevant fields for answering:

```json
{
  "question": {
    "questionId": "q-uuid",
    "text": "¿Capital de Francia?",
    "answerOptions": [
      { "optionId": "opt-A", "text": "París" },
      { "optionId": "opt-B", "text": "Londres" },
      { "optionId": "opt-C", "text": "Berlín" },
      { "optionId": "opt-D", "text": "Madrid" }
    ]
  },
  "answer": {
    "answerId": "ans-uuid",
    "selectedOptionId": "opt-A",
    "state": "EVALUATED",
    "isCorrect": true
  },
  "timer": { "expiresAt": "12:00:30Z", "remainingSeconds": 12, "state": "RUNNING", "serverNow": "12:00:18Z" },
  "status": { "canAnswer": true, "isTerminal": false }
}
```

- `question.answerOptions` **sin** `isCorrect` para `PLAYER` cuando `answer.state != EVALUATED` (filtrado SPEC-006, contract test verifies 0% leak). Tras `EVALUATED`, `isCorrect` se puede exponer o `correctOptionId` separado para resaltar `Correct` secondary en `Incorrect`.
- `answer.state` `PENDING` (no enviado) | `SUBMITTED` | `EVALUATED` (isCorrect true/false) | `EXPIRED` (Timeout). `Locked` es alias local `PENDING+selected` antes de `SUBMITTED`.
- `status.canAnswer = !isTerminal && round IN_PROGRESS && answer PENDING` (bloquea selector en `WITHDRAWN/ELIMINATED/FINISHED` o `EXPIRED`).

## 3. GET /api/games/{gameId}/rounds/current/questions/current (opcional)

Filtrado idem: 4 opciones sin `isCorrect` para `PLAYER`.

```
GET /api/games/{gameId}/rounds/current/questions/current
Authorization: Bearer
X-Correlation-Id
```

Response 200 `QuestionView` con `answerOptions[4]` sin `isCorrect` si `PENDING`.

## 4. Security & Validation

- `RequireAuthorization` `PLAYER` policy `Game.Play` (`sub` = `GameSession.playerId`).
- `X-Correlation-Id` prop. en todos requests; `GamePlayLimiter` rate limiting 429 `Retry-After` ya en Api si hydrate frecuente.
- `must_change_password` claim gating 302 redirect a `/auth/change-password` antes de `POST /answers`.

## References

- SPEC-006 `SubmitAnswer.cs` (ICommand+Handler+Validator+Endpoint `X-Idempotency-Key`, `AnswerWindowExpired`, `QuestionAlreadyAnswered`)
- SPEC-003 `Question` 4/1 invariante
- `src/OroQuizClash.Application/Features/Games/SubmitAnswer.cs` / `GetMyPlayerState.cs` `IEndpoint` `ISender` `GameClaims`
- `draft/constitution.md` V Server Truth, F Idempotency (`UNIQUE IdempotencyKey`), B 4/1, H `sub=PlayerId`
