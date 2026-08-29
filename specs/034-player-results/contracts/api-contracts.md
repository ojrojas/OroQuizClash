# API Contracts: Player Results (034)

**Branch**: `034-player-results` | **Date**: 2026-08-29 | **Spec**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)

No nuevo endpoint. Reusa `GET /api/games/{id}/players/me` privado per `sub` + `GET /api/games/{id}/leaderboard` público `Rank`/`Prize` + `GET /api/rewards` `Available`.

## 1. GET /api/games/{gameId}/players/me (Privado per sub — Final Score + Secured)

**Reuse** `GetMyPlayerState` (029) — para 4 pantallas.

### Request

```
GET /api/games/{gameId}/players/me
Authorization: Bearer <JWT oroclash-api, sub=PlayerId>
X-Correlation-Id: <uuid v4>
```

### Response 200 — Para `ResultComponent` 4 estados

```json
{
  "player": { "playerId": "sub-A", "displayName": "Player A" },
  "game": { "gameId": "game-uuid", "status": "FINISHED", "configuration": { "maxRounds": 10 } },
  "gameSession": { "gameSessionId": "gp-A", "playerId": "sub-A", "status": "WINNER", "currentRoundNumber": 5, "rowVersion": "AAA=" },
  "score": { "playerId": "sub-A", "gameId": "game-uuid", "totalPoints": 850, "correctAnswers": 5, "currentLevel": "Expert" },
  "securedPoints": { "playerId": "sub-A", "gameId": "game-uuid", "securedPoints": 200, "checkpointRoundNumber": 2, "policy": "KEEP_SECURED_SCORE" },
  "status": { "gameStatus": "FINISHED", "playerStatus": "WINNER", "isTerminal": true, "canAnswer": false }
}
```

- `gameSession.status == WINNER` && `game.status==FINISHED` && `Leaderboard Rank==1` → `YOU WON` `Final Score 850` `Prize` (ver §2).
- `gameSession.status == WITHDRAWN` → `YOU WALKED AWAY` `Secured Points 200` `checkpoint 2` + `Available Rewards` (ver §3).
- `gameSession.status == ELIMINATED` → `GAME OVER` `Final Score 120` + `Consolation Reward` (ver §3).
- `game.status==FINISHED` && `gameSession.status==FINISHED` `Rank` 2..N → `GAME FINISHED` `Final Position 3` `Final Score 400` + `Reward`.
- Si `isTerminal==false` && `canAnswer==true` → `ResultComponent` redirige a `/player/game/:id` "Partida aún en curso".

### Error Responses

- `401` sin JWT → OIDC, `403 PlayerNotInGame`, `404 GameNotFound`, `429 Retry-After: 1`.

## 2. GET /api/games/{gameId}/leaderboard (Público — Final Position + Prize)

**Reuse** `GetLeaderboard` — para `Final Position` 1..N + `Prize` threshold.

### Request

```
GET /api/games/{gameId}/leaderboard
Authorization: Bearer <JWT>
X-Correlation-Id: <uuid v4>
```

### Response 200 — Rank 1..N

```json
{
  "entries": [
    { "playerId": "sub-A", "displayName": "Player A", "totalPoints": 850, "level": "Expert", "position": 1, "rank": 1, "points": 850, "securedPoints": 200, "status": "WINNER" },
    { "playerId": "sub-B", "displayName": "Player B", "totalPoints": 400, "level": "Intermediate", "position": 3, "rank": 3, "points": 400, "securedPoints": 100, "status": "FINISHED" }
  ]
}
```

- `position`/`Rank` 1..N per `sub` orden `totalPoints` desc + `CorrectAnswers` + `AchievedAt` (SPEC-011).
- `Prize` si `totalPoints >= RewardRules.pointsRequired` (ej. 850 >=500 → `Pack Oro`).
- Sin `SelectedOptionId/isCorrect/Timer` de otros.

## 3. GET /api/rewards (Público — Available Rewards para YOU WALKED AWAY)

**Reuse** `GetRewards` — para `Available Rewards` filtrable.

### Request

```
GET /api/rewards
Authorization: Bearer <JWT>
X-Correlation-Id: <uuid v4>
```

### Response 200 — Lista filtrable

```json
[
  { "rewardId": "pack-plata", "name": "Pack Plata", "pointsRequired": 300, "type": "REWARD" },
  { "rewardId": "pack-oro", "name": "Pack Oro", "pointsRequired": 500, "type": "REWARD" }
]
```

- `YOU WALKED AWAY` filtra `reward.pointsRequired <= securedPoints.securedPoints` (ej. 200 → 0 disponibles → "Sin recompensas disponibles").
- `Consolation Reward` para `GAME OVER` es `Reward` `CONSOLATION` si `ConsolationPolicy` otorga.

## 4. Realtime `GameFinished → hydrate` para Result

```
GameHub Hub: /hubs/game?gameId={gameId}
Event: GameFinished { gameId, status: "FINISHED", leaderboard: [...] }
Auth: Bearer per sub
Reconnect: withAutomaticReconnect [0,2000,5000,10000,30000] → hydrate GET /players/me per sub + GET /leaderboard Rank
```

- `GameFinished` no fuente verdad para `Final Position`; solo dispara `hydrate`.

## 5. Security & Validation

- `RequireAuthorization` `PLAYER` `Game.Play` (`sub` = `GameSession.playerId`).
- `X-Correlation-Id` prop. en todos requests; `GamePlayLimiter` 429 `Retry-After`.
- `must_change_password` gating 302 → `/auth/change-password` antes de `GET /players/me`.
