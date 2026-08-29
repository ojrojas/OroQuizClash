# Contracts: REST API consumed by Player Lobby (028)

**Branch**: `028-player-lobby` | **Date**: 2026-08-28
Base URL: `{{oroclash-api}}/api` (Aspire `http://oroclash-api` or `proxy.conf.json` `/api`). Auth: `Authorization: Bearer <access_token>` (OIDC PKCE). Headers: `X-Correlation-Id` (UUID), `X-Idempotency-Key` for Join. Errors: RFC 7807 `ProblemDetails` with `traceId/correlationId`.

Reuse existing slices; no new endpoints for v1.

## 1. List Available Games (Lobby)

**GET** `/api/games?status=WAITING_FOR_PLAYERS&page=1&pageSize=20&categoryId=&search=`

- **Auth**: `PLAYER` (any authenticated) `RequireAuthorization`
- **Query**: `status=WAITING_FOR_PLAYERS` (required for Available Games), `categoryId?`, `search?` (Name), `page?` default 1, `pageSize?` default 20 max 50, `sort=CreatedAt_desc` fixed
- **200 OK**: `PaginatedGamesResponse`
```json
{
  "items": [
    {
      "gameId": "01H...",
      "name": "Quiz Noche",
      "categoryId": "cat-1",
      "categoryName": "Historia",
      "difficulty": 3,
      "difficultyName": "Intermediate",
      "minRounds": 5,
      "maxRounds": 10,
      "numberOfRoundsDisplay": "5-10",
      "players": { "current": 3, "max": 10, "display": "3/10" },
      "startTime": "2026-08-28T12:00:00Z",
      "prize": "Pack Oro",
      "status": "WAITING_FOR_PLAYERS",
      "version": "AAAA..."
    }
  ],
  "totalCount": 42,
  "page": 1,
  "pageSize": 20,
  "totalPages": 3
}
```
- **Errors**: `401 Unauthorized` (no JWT) → redirect OIDC; `400 InvalidPage` if page<1; `429 GamePlayLimiter`
- **Notes**: `prize` = `Reward.Name` via `RewardRules` else "—". `players.current` = `Game.Players.Count`. Server `Specification` ensures `Status` index + `CreatedAt desc` + `AsNoTracking`.

## 2. Get Game Detail (View Information)

**GET** `/api/games/{gameId}`

- **Auth**: `PLAYER` `RequireAuthorization`
- **200 OK**: `GameDetailResponse`
```json
{
  "gameId": "g-1",
  "name": "Quiz Noche",
  "categoryId": "cat-1",
  "categoryName": "Historia",
  "difficulty": 3,
  "minRounds": 5,
  "maxRounds": 10,
  "timeLimitPerQuestionSeconds": 30,
  "pointsPerRound": 100,
  "withdrawalPolicy": "KEEP_SECURED_SCORE",
  "lossPolicy": "FALLBACK_TO_CHECKPOINT",
  "players": { "current": 3, "max": 10 },
  "playersList": [{ "playerId": "sub-1", "displayName": "Ana" }],
  "startTime": "2026-08-28T12:00:00Z",
  "prize": "Pack Oro",
  "status": "WAITING_FOR_PLAYERS",
  "configuration": { "...": "..." }
}
```
- **Errors**: `404 GameNotFound` (ProblemDetails `code=GameNotFound`, `correlationId`), `401`
- **Notes**: No `Answer`/`Score` of other players (FR-013).

## 3. Join Game

**POST** `/api/games/{gameId}/players`

- **Auth**: `PLAYER` `RequireAuthorization` (`sub` = `PlayerId`)
- **Headers**: `X-Idempotency-Key: uuid-join-{gameId}` (sessionStorage per gameId), `X-Correlation-Id`
- **Body**: `{ "idempotencyKey": "uuid" }` (optional mirror; server prefers header)
- **200 OK**: `GameSession`
```json
{
  "gameSessionId": "gs-1",
  "playerId": "sub-123",
  "gameId": "g-1",
  "status": "ACTIVE",
  "joinedAt": "2026-08-28T12:01:00Z",
  "version": "BBBB..."
}
```
- **200 Idempotent replay**: same `X-Idempotency-Key` returns same 200 without new `GamePlayer` (no count increment)
- **Errors**: `400 GameNotWaitingForPlayers` (status changed between list and join), `409 GameFull` (`Players.Count >= MaxPlayers`), `409 AlreadyJoined` treated as 200 idempotent if same key else 409, `403 PlayerIdentityMismatch` if `sub` mismatch, `401`, `429`

## 4. Leave Lobby (client only)

- **No API call**. `Router.navigate(['/'])` or `location.back()`. Must not invoke `POST /withdraw` nor mutate `GameSession`. Verify no `fetch` to `/players` on Leave.

## Common DTOs

```ts
interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  detail: string;
  code: string; // GameFull, GameNotWaitingForPlayers, GameNotFound, PlayerIdentityMismatch
  traceId: string;
  correlationId: string;
}
```

## Interceptors (Angular, reuse SPEC-027)

- `correlationIdInterceptor`: `X-Correlation-Id: crypto.randomUUID()` per request.
- `authInterceptor`: `Authorization: Bearer <access_token>` if `url.startsWith(apiUrl)`.
- `errorInterceptor`: map `ProblemDetails` → `throwError`, 401 → `silentRenew` else redirect `connect/authorize`, 429 handle `Retry-After`, surface `CorrelationId/TraceId` in `ErrorState`.
