# API Contracts: Player Multiplayer (033)

**Branch**: `033-player-multiplayer` | **Date**: 2026-08-29 | **Spec**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)

Aislamiento multiplayer: 5 privados per `sub` vía `GET /players/me` + 4 públicos sin fuga via `GET /leaderboard`/`GET /players`.

## 1. GET /api/games/{gameId}/players/me (Privado per sub)

**Reuse** `GetMyPlayerState` (029) — privado per `sub`.

### Request

```
GET /api/games/{gameId}/players/me
Authorization: Bearer <JWT oroclash-api, sub=PlayerId>
X-Correlation-Id: <uuid v4>
```

- `gameId`: Guid `GameId`.
- Auth: JWT `jwks_uri`, `sub=PlayerId` (de `GameClaims.GetSub`), `must_change_password` gating 302 → `/auth/change-password`; sin JWT → 401.

### Response 200 — Privado per sub (0% leak)

```json
{
  "player": { "playerId": "sub-A", "displayName": "Player A", "email": "a@test.com" },
  "game": { "gameId": "game-uuid", "name": "Game 1", "status": "ROUND_IN_PROGRESS", "configuration": { "maxRounds": 10 } },
  "gameSession": { "gameSessionId": "gp-A", "playerId": "sub-A", "gameId": "game-uuid", "status": "ACTIVE", "currentRoundNumber": 2, "rowVersion": "AAA=" },
  "round": { "roundId": "round-uuid", "gameId": "game-uuid", "roundNumber": 2, "level": "Intermediate", "status": "IN_PROGRESS", "questionId": "q-uuid" },
  "question": { "questionId": "q-uuid", "text": "¿Capital?", "answerOptions": [ {"optionId":"opt-A","text":"París"}, {"optionId":"opt-B","text":"Londres"} ] },
  "answer": { "answerId": "ans-A", "selectedOptionId": "opt-A", "state": "EVALUATED", "isCorrect": true },
  "score": { "playerId": "sub-A", "gameId": "game-uuid", "totalPoints": 100, "correctAnswers": 1, "currentLevel": "Intermediate" },
  "securedPoints": { "playerId": "sub-A", "gameId": "game-uuid", "securedPoints": 0, "checkpointRoundNumber": null, "policy": "KEEP_SECURED_SCORE" },
  "timer": { "timeLimitSeconds": 30, "expiresAt": "12:00:30Z", "remainingSeconds": 12, "state": "RUNNING", "serverNow": "12:00:18Z" },
  "status": { "gameStatus": "ROUND_IN_PROGRESS", "playerStatus": "ACTIVE", "isTerminal": false, "canAnswer": true }
}
```

- `gameSession.playerId == sub-A` 100%; `answer.selectedOptionId` solo de A; `isCorrect` solo si `EVALUATED` sino null (SPEC-006).
- Con JWT `sub=B` en mismo `gameId`, `answer.selectedOptionId` es de B (`opt-C`) y `score.totalPoints` de B (250), no A.
- Nunca `Answer` de B en payload de A; `Timer` per `playerId+roundId` (aunque mismo `expiresAt` si misma ronda, no compartido en memoria).

### Error Responses

- `401 Unauthorized` sin JWT → redirect OIDC.
- `403 PlayerNotInGame` / `PlayerIdentityMismatch` si `sub` no en `Game.Players` RFC7807 con `CorrelationId`.
- `404 GameNotFound` RFC7807.

### Headers

- `X-Correlation-Id` echo en response.

## 2. GET /api/games/{gameId}/leaderboard (Público sin privados)

**Reuse** `GetLeaderboard` (029/011) — público sin fuga.

### Request

```
GET /api/games/{gameId}/leaderboard
Authorization: Bearer <JWT>
X-Correlation-Id: <uuid v4>
```

### Response 200 — Público sin privados (0% fuga)

```json
{
  "entries": [
    { "playerId": "sub-A", "displayName": "Player A", "totalPoints": 100, "level": "Intermediate", "position": 2 },
    { "playerId": "sub-B", "displayName": "Player B", "totalPoints": 250, "level": "Intermediate", "position": 1 }
  ]
}
```

- Sin `selectedOptionId`, `isCorrect`, `Timer`, `SecuredPoints` detallado, `Answer` privado.
- Ordenado por `totalPoints` desc.
- Con 2 JWTs en paralelo, mismo payload público para A y B.

### Error Responses

- `401` sin JWT, `404` `GameNotFound`, `403` `PlayerNotInGame` si no en juego (según política, `Leaderboard` puede requerir estar en juego).
- `429` `Retry-After: 1` `GamePlayLimiter`.

## 3. GET /api/games/{gameId}/players (Público `Players`/`PlayersRemaining`)

**Reuse** `GetGamePlayers` o `GetGame` con `Players` lista — público.

### Request

```
GET /api/games/{gameId}/players
Authorization: Bearer <JWT>
X-Correlation-Id: <uuid v4>
```

### Response 200 — Público

```json
{
  "players": [
    { "playerId": "sub-A", "displayName": "Player A", "status": "ACTIVE" },
    { "playerId": "sub-B", "displayName": "Player B", "status": "ACTIVE" },
    { "playerId": "sub-C", "displayName": "Player C", "status": "WITHDRAWN" }
  ],
  "playersRemaining": 2
}
```

- `playersRemaining = players.filter(p=>p.status=='ACTIVE').length`.
- Sin `Answer/Score` privado.

### Error Responses

- `401` sin JWT, `404` `GameNotFound`.

## 4. GET /api/games/{gameId}/rounds/current (Público `Current Round`)

**Reuse** `GetCurrentRound` — público genérico.

### Request

```
GET /api/games/{gameId}/rounds/current
Authorization: Bearer <JWT>
X-Correlation-Id: <uuid v4>
```

### Response 200 — Público

```json
{
  "roundId": "round-uuid",
  "gameId": "game-uuid",
  "roundNumber": 2,
  "level": "Intermediate",
  "status": "IN_PROGRESS",
  "questionId": "q-uuid"
}
```

- Sin `Answer` privado.
- `Current Round` 3/10 genérico para todos.

## 5. Realtime multiplayer (SPEC-012)

```
GameHub Hub: /hubs/game?gameId={gameId}
Events: ScoreUpdated, LeaderboardUpdated, RoundCompleted, RoundStarted, GameFinished, Reconnected
Auth: Bearer via accessTokenFactory per sub
Reconnect: withAutomaticReconnect [0,2000,5000,10000,30000] → hydrate GET /players/me privado per sub + GET /leaderboard público
```

- `ScoreUpdated` payload no fuente verdad para privados; solo dispara `hydrate` `GET /players/me` per `sub`.
- `LeaderboardUpdated` payload puede ser `totalPoints` público, pero cliente hace `hydrate` `GET /leaderboard` para públicos.

## 6. Security & Validation

- `RequireAuthorization` `PLAYER` policy `Game.Play` (`sub` = `GameSession.playerId`).
- `X-Correlation-Id` prop. en todos requests; `GamePlayLimiter` 429 `Retry-After` ya en Api.
- `must_change_password` claim gating 302 redirect a `/auth/change-password` antes de `GET /players/me`.
- `GameClaims.GetSub(http.User)` `sub` no body; `PlayerIdentityMismatch` 403 auditada si `sub` intenta acceder `GameSession` de otro.

## References

- SPEC-011 `Multiplayer` base (`GamePlayer` `UNIQUE`, `RowVersion` per `GamePlayerId`, `Leaderboard` inicial)
- SPEC-007 `Scoring System` (`PointTransaction` ledger `UNIQUE` per `playerId`)
- SPEC-012 `Realtime Game Events` (`GameHub` `ScoreUpdated` `withAutomaticReconnect` → `hydrate`)
- `src/OroQuizClash.Application/Features/Games/GetMyPlayerState.cs` privado `sub` + `GetLeaderboard.cs` público + `GetGamePlayers.cs` `IEndpoint` `ISender` `GameClaims`
- `draft/constitution.md` V Server Truth per `sub`, F `UNIQUE (GameId,RoundId,PlayerId)` `RowVersion` per `GamePlayer`, H `sub=PlayerId`, D `Leaderboard` sin privados
