# API Contracts: Player Withdrawal (035)

**Branch**: `035-player-withdrawal` | **Date**: 2026-08-29 | **Spec**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)

Retiro voluntario `POST /withdraw` `X-Idempotency-Key` per `gameId` + `GET /players/me` 3 métricas `Current/Secured/Potential`.

## 1. POST /api/games/{gameId}/withdraw

**Reuse** `WithdrawPlayer` (SPEC-008) — idempotente `X-Idempotency-Key` per `gameId`.

### Request

```
POST /api/games/{gameId}/withdraw
Authorization: Bearer <JWT oroclash-api, sub=PlayerId>
X-Idempotency-Key: <uuid v4 per playerId+gameId sessionStorage idemp-withdraw-{gameId}>
X-Correlation-Id: <uuid v4>
Content-Type: application/json

{}
or { "idempotencyKey": "same-as-header" }
```

- `gameId`: Guid `GameId`.
- Auth: JWT `jwks_uri`, `sub=PlayerId` (de `GameClaims.GetSub`), `must_change_password` gating 302 → `/auth/change-password`; sin JWT → 401.
- Idempotency: `X-Idempotency-Key` UUID per `playerId+gameId` `sessionStorage` `idemp-withdraw-{gameId}`; reuso misma key para Retry.

### Responses

#### 200 OK — WITHDRAWN (primero o idempotente reuso misma key)

```json
{
  "gameSessionId": "gp-uuid",
  "playerId": "sub-123",
  "gameId": "game-uuid",
  "status": "WITHDRAWN",
  "currentRoundNumber": 2,
  "rowVersion": "AQIDBA==",
  "securedPoints": 200,
  "currentPoints": 200
}
```

- Segundo `POST /withdraw` misma `X-Idempotency-Key` → mismo `GameSession` `WITHDRAWN` sin nuevo `PointTransaction` ledger `COUNT` (idempotente).

#### 403 PlayerAlreadyWithdrawn — ya WITHDRAWN distinto key sin misma idempotencia

```json
{
  "type": "https://httpstatuses.com/403",
  "title": "PlayerAlreadyWithdrawn",
  "detail": "Player already withdrawn",
  "status": 403,
  "code": "PlayerAlreadyWithdrawn",
  "traceId": "00-...",
  "correlationId": "corr-uuid"
}
```

- Si reintento `POST /withdraw` ya `WITHDRAWN` sin misma `X-Idempotency-Key` → `403` no duplicar ledger.

#### 403 PlayerAlreadyEliminated — ELIMINATED no puede retirarse

```json
{
  "type": "https://httpstatuses.com/403",
  "title": "PlayerAlreadyEliminated",
  "status": 403,
  "code": "PlayerAlreadyEliminated",
  "correlationId": "corr-uuid"
}
```

#### 400 InvalidGameState — Game FINISHED/CANCELLED

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "InvalidGameState",
  "detail": "Cannot withdraw from terminal game",
  "status": 400,
  "code": "InvalidGameState",
  "correlationId": "corr-uuid"
}
```

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

### Headers

- `X-Correlation-Id` echo en response; `X-Idempotency-Key` confirmada via `GameSession` `rowVersion`.

## 2. GET /api/games/{gameId}/players/me (3 métricas para diálogo)

**Reuse** `GetMyPlayerState` (029/032) — para `Current/Secured/Potential` antes de confirmar.

Relevant fields:

```json
{
  "score": { "playerId": "sub-123", "gameId": "game-uuid", "totalPoints": 400, "correctAnswers": 3, "currentLevel": "Intermediate" },
  "securedPoints": { "playerId": "sub-123", "gameId": "game-uuid", "securedPoints": 200, "checkpointRoundNumber": 2, "policy": "KEEP_SECURED_SCORE" },
  "game": { "gameId": "game-uuid", "configuration": { "pointsPerRound": 100 } },
  "status": { "canAnswer": true, "isTerminal": false, "playerStatus": "ACTIVE", "gameStatus": "ROUND_IN_PROGRESS" }
}
```

- `score.totalPoints` = `Current Points` 400; `securedPoints.securedPoints` 200 `checkpoint 2` → "200 pts · checkpoint 2"; `Potential Points` `PointsPerRound` 100 o "—" si no configurado.
- `status.isTerminal` `canAnswer` para habilitar `Withdrawal Action` (`!isTerminal`).

## 3. Security & Validation

- `RequireAuthorization` `PLAYER` policy `Game.Play` (`sub` = `GameSession.playerId`).
- `X-Correlation-Id` prop. en todos requests; `GamePlayLimiter` 429 `Retry-After` ya en Api si `POST /withdraw` frecuente.
- `must_change_password` claim gating 302 redirect a `/auth/change-password` antes de `POST /withdraw`.

## References

- SPEC-008 `WithdrawPlayer.cs` (`ICommand` `WithdrawPlayer` `X-Idempotency-Key` `PlayerAlreadyWithdrawn` `RowVersion` per `GamePlayerId`)
- `src/OroQuizClash.Application/Features/Games/WithdrawPlayer.cs` `GetMyPlayerState.cs` `IEndpoint` `ISender` `GameClaims` `X-Idempotency-Key`
- `draft/constitution.md` V Server Truth per `sub` `Secured` ledger, F `RowVersion` per `GamePlayerId` `X-Idempotency-Key`, C `WithdrawalPolicy` `KEEP_SECURED_SCORE`, H `sub=PlayerId`
