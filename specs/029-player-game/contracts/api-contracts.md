# Contracts: REST API consumed by Player Game (029)

**Branch**: `029-player-game` | **Date**: 2026-08-28
Base URL: `{{oroclash-api}}/api` (`http://oroclash-api` Aspire or `proxy.conf.json` `/api`). Auth: `Authorization: Bearer <access_token>` PKCE `sub=PlayerId`. Headers: `X-Correlation-Id` UUID, `X-Idempotency-Key` for mutations. Errors: RFC7807 `ProblemDetails` with `traceId/correlationId`.

Reuse existing slices from SPEC-027; no new endpoints for v1.

## 1. Get My Player State (hydrate 10 elementos)

**GET** `/api/games/{gameId}/players/me`

- **Auth**: `PLAYER` `RequireAuthorization` (`sub` must be participant)
- **200 OK**: `PlayerGameState` 10 elementos
```json
{
  "player": { "playerId": "sub-123", "displayName": "Ana" },
  "game": { "gameId": "g1", "name": "Quiz Noche", "status": "ROUND_IN_PROGRESS", "maxPlayers": 10, "configuration": { "maxRounds": 10, "timeLimitPerQuestionSeconds": 30, "pointsPerRound": 100 } },
  "gameSession": { "gameSessionId": "gs1", "playerId": "sub-123", "gameId": "g1", "status": "ACTIVE", "currentRoundNumber": 3 },
  "round": { "roundId": "r3", "gameId": "g1", "roundNumber": 3, "level": "Intermediate", "status": "IN_PROGRESS", "questionId": "q42", "startedAt": "2026-08-28T12:00:00Z", "expiresAt": "2026-08-28T12:00:30Z" },
  "question": { "questionId": "q42", "text": "¿Capital...?", "answerOptions": [{ "optionId": "o1", "text": "A" }, { "optionId": "o2", "text": "B" }, { "optionId": "o3", "text": "C" }, { "optionId": "o4", "text": "D" }], "difficulty": "Intermediate" },
  "answer": { "answerId": null, "state": "PENDING", "selectedOptionId": null, "isCorrect": null, "idempotencyKey": "k3" },
  "score": { "playerId": "sub-123", "gameId": "g1", "totalPoints": 250, "correctAnswers": 2, "currentLevel": "Intermediate" },
  "securedPoints": { "playerId": "sub-123", "gameId": "g1", "securedPoints": 100, "checkpointRoundNumber": 2, "policy": "KEEP_SECURED_SCORE" },
  "timer": { "timeLimitSeconds": 30, "expiresAt": "2026-08-28T12:00:30Z", "remainingSeconds": 18, "state": "RUNNING", "serverNow": "2026-08-28T12:00:12Z" },
  "status": { "gameStatus": "ROUND_IN_PROGRESS", "playerStatus": "ACTIVE", "isTerminal": false, "canAnswer": true }
}
```
- **Errors**: `404 GameNotFound` / `NotParticipant` / `401`
- **Notes**: `timer.remainingSeconds` snapshot; cliente deriva `computed` con `expiresAt` + `interval` + `serverNow` correction. Rehydrate en cada `QuestionAvailable`/`ScoreUpdated`/etc.

## 2. Submit Answer

**POST** `/api/games/{gameId}/answers`
```json
{ "roundId": "r3", "questionId": "q42", "selectedOptionId": "o2", "idempotencyKey": "uuid-round3" }
```
- **Headers**: `X-Idempotency-Key: uuid-round3`
- **200 OK** `200 EVALUATED`: `{ "answerId": "a1", "state": "EVALUATED", "isCorrect": true, "submittedAt": "2026-08-28T12:00:15Z" }`
- **200 Idempotent replay**: same key returns same 200 no new ledger
- **Errors**: `400 AnswerWindowExpired` (`submittedAt > expiresAt`), `409 QuestionAlreadyAnswered` (same round diff key), `400 InvalidAnswer` (option not in question), `403 PlayerNotActive` (terminal), `429 GamePlayLimiter`

## 3. Withdraw Player

**POST** `/api/games/{gameId}/withdraw`
```json
{ "idempotencyKey": "uuid-withdraw" }
```
- **Headers**: `X-Idempotency-Key`
- **200 OK**: `GameSession` `status: WITHDRAWN` + `securedPoints` per `KEEP_SECURED_SCORE`
- **Errors**: `409 AlreadyTerminal` / `403 NotParticipant` / `403 PlayerIdentityMismatch`

## 4. Get Game (fallback detail)

**GET** `/api/games/{gameId}` → `Game` (for `Potential Reward` `RewardRules` resolution, no `Score` leak)

## Interceptors

- `correlationIdInterceptor`: `X-Correlation-Id: crypto.randomUUID()` per request.
- `authInterceptor`: `Authorization: Bearer <access_token>` if `url.startsWith(apiUrl)`.
- `errorInterceptor`: map `ProblemDetails` → `throwError`, 401 → `silentRenew` else redirect, 429 `Retry-After`, surface `CorrelationId/TraceId` in `ErrorState`.
