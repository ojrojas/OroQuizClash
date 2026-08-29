# API Contracts: Player Scoring (032)

**Branch**: `032-player-scoring` | **Date**: 2026-08-29 | **Spec**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)

No nuevo endpoint. Reusa `GET /api/games/{id}/players/me` (`GetMyPlayerState` ya en SPEC-007/029) y opcional `GET /api/games/{id}/players/score`.

## 1. GET /api/games/{gameId}/players/me

**Reuse** `GetMyPlayerState` (029) — para 5 métricas scoring.

### Request

```
GET /api/games/{gameId}/players/me
Authorization: Bearer <JWT oroclash-api, sub=PlayerId>
X-Correlation-Id: <uuid v4>
```

- `gameId`: Guid `GameId`.
- Auth: JWT `jwks_uri`, `sub=PlayerId` (de `GameClaims.GetSub`), `must_change_password` gating 302 → `/auth/change-password`; sin JWT → 401.

### Response 200 — 5 métricas autoritativas

```json
{
  "score": {
    "playerId": "sub-123",
    "gameId": "game-uuid",
    "totalPoints": 850,
    "correctAnswers": 5,
    "currentLevel": "Intermediate",
    "roundPoints": 50
  },
  "securedPoints": {
    "playerId": "sub-123",
    "gameId": "game-uuid",
    "securedPoints": 200,
    "checkpointRoundNumber": 3,
    "policy": "KEEP_SECURED_SCORE"
  },
  "game": {
    "gameId": "game-uuid",
    "configuration": {
      "pointsPerRound": 100,
      "rewardRules": [{ "rewardId": "pack-oro", "roundThreshold": 5, "name": "Pack Oro", "pointsRequired": 500 }]
    }
  },
  "timer": { "expiresAt": "12:00:30Z", "remainingSeconds": 12, "state": "RUNNING", "serverNow": "12:00:18Z" },
  "status": { "canAnswer": true, "isTerminal": false }
}
```

- `score.totalPoints` = `Total Points` (SC-001/003) = `sum(PointTransaction)` server-side (D). `score.roundPoints` = `Round Points` (50).
- `securedPoints.securedPoints` = `Secured Points` (200) + `checkpointRoundNumber` (3) → "200 · checkpoint 3"; null → sin badge (SC-004).
- `game.configuration.pointsPerRound` + `rewardRules` → `Potential Points` (100 o "Próximo: Pack Oro 500 pts" o "—" si no configurado, SC-005).
- `Current Points` = `score.totalPoints` (o `score.currentPoints` si se expone separado; en 029 `totalPoints` es `Current Points`).
- 5 métricas NUNCA calculadas cliente (V).

### Error Responses

- `401 Unauthorized` sin JWT → redirect OIDC.
- `403 PlayerNotInGame` / `PlayerIdentityMismatch` RFC7807.
- `404 GameNotFound` RFC7807.
- `429 Too Many Requests` `Retry-After: 1` `GamePlayLimiter` si hydrate frecuente.

### Headers

- `X-Correlation-Id` echo en response; `Authorization: Bearer` solo `apiUrl`.

## 2. GET /api/games/{gameId}/players/score (opcional)

Si existe `GetPlayerScore` slice, retorna ledger `PointTransaction[]` para audit:

```
GET /api/games/{gameId}/players/score
Authorization: Bearer
X-Correlation-Id
```

Response `200` `ScoreDto` + `PointTransaction[]` con `sum(points)=totalPoints` 100% (SC-003).

## 3. Realtime scoring (SPEC-012)

```
GameHub Hub: /hubs/game?gameId={gameId}
Events: ScoreUpdated, RoundCompleted, RoundStarted, GameFinished, Reconnected
Auth: Bearer via accessTokenFactory
Reconnect: withAutomaticReconnect [0,2000,5000,10000,30000] → hydrate GET /players/me
```

- `ScoreUpdated` payload no fuente verdad; solo dispara `hydrate`.
- `RoundCompleted` marca `Secured` y resetea `Round Points`.

## 4. Security & Validation

- `RequireAuthorization` `PLAYER` policy `Game.Play` (`sub` = `GameSession.playerId`).
- `X-Correlation-Id` prop. en todos requests; `GamePlayLimiter` 429 `Retry-After` ya en Api.
- `must_change_password` claim gating 302 redirect a `/auth/change-password` antes de `GET /players/me`.

## References

- SPEC-007 `Scoring System` (`PointTransaction` ledger `UNIQUE`, `Score` `SecuredPoints` `RowVersion`)
- SPEC-012 `Realtime Game Events` (`GameHub` `ScoreUpdated` `withAutomaticReconnect` → `hydrate`)
- `src/OroQuizClash.Application/Features/Games/GetMyPlayerState.cs` `GetPlayerScore.cs` `IEndpoint` `ISender` `GameClaims`
- `draft/constitution.md` V Server Truth, D Ledger `sum=total`, F Idempotency, H `sub=PlayerId`
