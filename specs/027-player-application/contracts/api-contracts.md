# Contracts: REST API consumed by Player (027)

**Branch**: `027-player-application` | **Date**: 2026-08-28

Base URL: `{{oroclash-api}}/api` (Aspire service discovery `http://oroclash-api` o `proxy.conf.json` `/api` en dev). Auth: `Authorization: Bearer <access_token>` (OIDC). Headers: `X-Correlation-Id` (UUID), `X-Idempotency-Key` donde aplique. Errores: RFC 7807 `ProblemDetails` con `traceId`.

All DTOs are projections; backend `IEndpoint` slices remain authoritative. Frontend never sends `Score`/`isCorrect`/`remaining` — server recomputes.

## 1. Join Game (Lobby)

**POST** `/api/games/{gameId}/players`
- **Auth**: `PLAYER` (any authenticated)
- **Body**: `{ "idempotencyKey": "uuid" }` (optional; server generates if absent)
- **200 OK**: `GameSession` (joined)
```json
{ "gameSessionId": "01H...", "playerId": "sub-123", "gameId": "game-1", "status": "ACTIVE", "joinedAt": "2026-08-28T12:00:00Z", "currentRoundNumber": 0, "version": "AAAA..." }
```
- **Errors**: `400 GameNotWaitingForPlayers` | `409 AlreadyJoined` (idempotent returns 200) | `409 GameFull` | `401/403`

## 2. Get My Player State (hidratación — 10 elementos)

**GET** `/api/games/{gameId}/players/me`
- **Auth**: `PLAYER` (must be participant)
- **200 OK**: `PlayerGameState` (agregado de contexto privado)
```json
{
  "player": { "playerId": "sub-123", "displayName": "Ana", "email": "ana@...", "tenantId": "t-1" },
  "game": { "gameId": "game-1", "name": "Quiz Noche", "status": "ROUND_IN_PROGRESS", "categoryId": "cat-1", "configuration": { "timeLimitPerQuestionSeconds": 30, "pointsPerRound": 100, "withdrawalPolicy": "KEEP_SECURED_SCORE", "lossPolicy": "FALLBACK_TO_CHECKPOINT", "minRounds": 5, "maxRounds": 10 }, "maxPlayers": 10, "minPlayers": 2 },
  "gameSession": { "gameSessionId": "gs-1", "playerId": "sub-123", "gameId": "game-1", "status": "ACTIVE", "joinedAt": "...", "currentRoundNumber": 3, "version": "AAAA..." },
  "round": { "roundId": "r-3", "gameId": "game-1", "roundNumber": 3, "level": "Intermediate", "status": "IN_PROGRESS", "questionId": "q-42", "startedAt": "...", "expiresAt": "2026-08-28T12:03:30Z", "version": "BBBB..." },
  "question": { "questionId": "q-42", "categoryId": "cat-1", "text": "¿Capital de...?", "answerOptions": [{ "optionId": "o-1", "text": "A" }, { "optionId": "o-2", "text": "B" }, { "optionId": "o-3", "text": "C" }, { "optionId": "o-4", "text": "D" }], "difficulty": "Intermediate" },
  "answer": { "answerId": null, "playerId": "sub-123", "gameId": "game-1", "roundId": "r-3", "questionId": "q-42", "selectedOptionId": null, "submittedAt": null, "state": "PENDING", "isCorrect": null, "idempotencyKey": "uuid-round3" },
  "score": { "playerId": "sub-123", "gameId": "game-1", "totalPoints": 250, "correctAnswers": 2, "currentLevel": "Intermediate" },
  "securedPoints": { "playerId": "sub-123", "gameId": "game-1", "securedPoints": 100, "checkpointRoundNumber": 2, "policy": "KEEP_SECURED_SCORE" },
  "timer": { "timeLimitSeconds": 30, "expiresAt": "2026-08-28T12:03:30Z", "remainingSeconds": 18, "state": "RUNNING", "serverNow": "2026-08-28T12:03:12Z" },
  "status": { "gameStatus": "ROUND_IN_PROGRESS", "playerStatus": "ACTIVE", "isTerminal": false, "canAnswer": true }
}
```
- **Errors**: `404 GameNotFound` | `404 NotParticipant` | `401`
- **Notes**: `timer.remainingSeconds` calculado server-side snapshot; cliente deriva con `computed` + `expiresAt`.
- **If not exists**: propose new slice `GetMyPlayerStateQuery` (IQuery + Handler + Endpoint GET /api/games/{id}/players/me) — Vertical Slice, thin endpoint → `ISender`.

## 3. Get Current Round / Question (alternativas granulares)

**GET** `/api/games/{gameId}/rounds/current` → `Round` (o 204 si aún no iniciada)
**GET** `/api/games/{gameId}/questions/current` → `Question` (4 opciones, sin isCorrect)

Usadas como fallback si `players/me` no se implementa en v1; `players/me` es preferido (hidratación atómica).

## 4. Submit Answer

**POST** `/api/games/{gameId}/answers`
```json
{ "roundId": "r-3", "questionId": "q-42", "selectedOptionId": "o-2", "idempotencyKey": "uuid-round3" }
```
- **Headers**: `X-Idempotency-Key: uuid-round3` (mirrors body)
- **200 OK**: `Answer` evaluada
```json
{ "answerId": "a-1", "playerId": "sub-123", "gameId": "game-1", "roundId": "r-3", "questionId": "q-42", "selectedOptionId": "o-2", "submittedAt": "2026-08-28T12:03:15Z", "state": "EVALUATED", "isCorrect": true, "idempotencyKey": "uuid-round3" }
```
- **200 (idempotent replay)**: mismo body que primer 200, sin nuevo ledger entry
- **Errors**: `400 InvalidAnswer` (option not in question) | `409 QuestionAlreadyAnswered` (sin replay, si idempotencyKey distinta) | `400 AnswerWindowExpired` (server `submittedAt > expiresAt`) | `403 PlayerNotActive` (terminal) | `409 InvalidGameState` | `429 GamePlayLimiter`

## 5. Withdraw

**POST** `/api/games/{gameId}/withdraw`
```json
{ "idempotencyKey": "uuid-withdraw" }
```
- **200 OK**: `GameSession` con `status: WITHDRAWN` + `securedPoints` según política
- **Errors**: `409 AlreadyTerminal` | `403 NotParticipant`

## 6. Get Leaderboard (opcional, SPEC-011)

**GET** `/api/games/{gameId}/leaderboard` → `{ entries: LeaderboardEntry[] }`
```json
{ "entries": [{ "rank": 1, "playerId": "sub-123", "displayName": "Ana", "totalPoints": 500, "correctAnswers": 5, "currentLevel": "Advanced", "playerStatus": "ACTIVE" }] }
```

## 7. Get Game (metadata)

**GET** `/api/games/{gameId}` → `Game`

## Common DTOs

```ts
// ProblemDetails RFC7807
interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  detail: string;
  code: string; // GameNotFound, InvalidGameState, ...
  traceId: string;
  correlationId: string;
}
```

## Interceptors (Angular)

- `correlationIdInterceptor`: genera `X-Correlation-Id` UUID por request, propaga a `X-Trace-Id` en respuesta para debugging.
- `authInterceptor`: `Authorization: Bearer <access_token>` solo si `request.url.startsWith(apiUrl)`.
- `errorInterceptor`: mapea `ProblemDetails` → `throwError(() => toAppError(problem))`, maneja 401 → `oauthService.refresh()` o redirect a `/auth/callback`, 429 → retry con backoff.
- `idempotencyInterceptor`: para `POST .../answers` y `.../withdraw`, genera `X-Idempotency-Key` UUID si no existe y lo persiste en `sessionStorage` por `roundId`.
