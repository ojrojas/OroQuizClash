# API Contracts: Player Rounds (030)

**Branch**: `030-player-rounds` | **Date**: 2026-08-28 | **Spec**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)

No nuevo endpoint. Ladder reuse `GET /api/games/{id}/players/me` ya en SPEC-029 (Server Truth V). Esta doc define proyección necesaria para `LadderRow[]` N=MaxRounds y 4 rewards.

## 1. GET /api/games/{gameId}/players/me

**Reuse** `GetMyPlayerState` Query (SPEC-029) — verificar que retorna campos ladder. Si falta campo, ampliar DTO projection sin cambiar aggregate.

### Request

```
GET /api/games/{gameId}/players/me
Authorization: Bearer <JWT oroclash-api>
X-Correlation-Id: <uuid>
```

- `gameId`: `GameId` strongly typed (Guid).
- Auth: JWT `jwks_uri`, `sub=PlayerId`, `must_change_password` gating 302 → `/auth/change-password`.
- Errors: 401 sin JWT, 403 `PlayerIdentityMismatch` si `sub` no en `GamePlayer`, 404 `GameNotFound`.

### Response 200 — PlayerGameState (10 elementos 029 + ladder projection)

```json
{
  "game": {
    "gameId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "Quiz Oro #42",
    "status": "IN_PROGRESS",
    "maxRounds": 10,
    "configuration": {
      "maxRounds": 10,
      "timeLimitPerQuestionSeconds": 30,
      "pointsPerRound": 100,
      "withdrawalPolicy": "KEEP_SECURED_SCORE",
      "lossPolicy": "LOSE_UNSECURED_POINTS",
      "difficultyStrategy": "Linear",
      "rewardRules": [
        { "roundThreshold": 5, "name": "Pack Plata 500 pts", "pointsRequired": 500 },
        { "roundThreshold": 10, "name": "Pack Oro 5000 pts", "pointsRequired": 5000 }
      ]
    }
  },
  "gameSession": {
    "gameSessionId": "...",
    "playerId": "sub-123",
    "gameId": "3fa85f64...",
    "status": "ACTIVE",
    "currentRoundNumber": 6,
    "isTerminal": false,
    "version": "AAAAAAAAB9E="
  },
  "rounds": [
    { "roundId": "...", "roundNumber": 1, "level": "Basic", "difficulty": 1, "status": "COMPLETED", "questionId": "...", "startedAt": "2026-08-28T12:00:00Z", "expiresAt": "2026-08-28T12:00:30Z", "completedAt": "2026-08-28T12:00:32Z" },
    { "roundId": "...", "roundNumber": 2, "level": "Basic", "difficulty": 1, "status": "COMPLETED" },
    { "roundId": "...", "roundNumber": 6, "level": "Intermediate", "difficulty": 3, "status": "IN_PROGRESS", "expiresAt": "2026-08-28T12:06:30Z" }
  ],
  "currentRound": { "roundNumber": 6, "level": "Intermediate", "status": "IN_PROGRESS", "expiresAt": "2026-08-28T12:06:30Z" },
  "question": null,
  "answer": null,
  "score": { "totalPoints": 700, "correctAnswers": 5, "currentLevel": "Intermediate" },
  "securedPoints": { "securedPoints": 500, "checkpointRoundNumber": 5, "policy": "KEEP_SECURED_SCORE" },
  "potentialReward": { "rewardId": null, "name": "Pack Oro 5000 pts", "pointsRequired": 5000, "display": "—" },
  "timer": { "timeLimitSeconds": 30, "expiresAt": "2026-08-28T12:06:30Z", "remainingSeconds": 22, "state": "RUNNING", "serverNow": "2026-08-28T12:06:08Z" },
  "status": { "gameStatus": "IN_PROGRESS", "playerStatus": "ACTIVE", "isTerminal": false, "isExpired": false, "canAnswer": true },
  "ladder": null,
  "correlationId": "corr-uuid",
  "_links": { "self": "/api/games/{id}/players/me" }
}
```

- `rounds`: `GameRound` 1..current (COMPLETED/IN_PROGRESS) autoritativos; filas futuras (7..10) no tienen `Round` aún — cliente `buildLadder` crea placeholders `level` proyectado por `IDifficultyProgressionStrategy` (server podría enviar `projectedLevels` opcional).
- `ladder` server no necesario; cliente `buildLadder(maxRounds, rounds, rewardRules, securedPoints, currentRoundNumber)` puro.
- `securedPoints`: ledger-derived (D) `sum` `KEEP_SECURED_SCORE` checkpoint 5 = 500; si `LOSE_ALL` → `securedPoints:0 checkpoint:null`.
- `game.configuration.rewardRules`: para Current/Next/Final derivation.
- `correlationId` header `X-Correlation-Id` prop + body.

### Client derivation (no nueva API)

```ts
function buildLadder(maxRounds: number, rounds: Round[], rewardRules: RewardRule[], secured: SecuredPoints, current: number|null, pointsPerRound?: number): LadderRow[] {
  return Array.from({length: maxRounds}, (_,i)=>{
    const n=i+1;
    const r=rounds.find(x=>x.roundNumber===n);
    const state = current==null ? 'upcoming' : n < current ? 'completed' : n===current ? 'current' : 'upcoming';
    const reward = rewardRules.find(x=>x.roundThreshold===n);
    return {
      roundNumber:n, level: r?.level ?? projectedLevel(n, rounds), difficulty: r?.difficulty ?? null,
      state, isSecured: (secured?.checkpointRoundNumber ?? 0) >= n && (secured?.securedPoints ?? 0)>0,
      isFinal: n===maxRounds,
      currentReward: n===current ? (reward?.pointsRequired ?? pointsPerRound ? pointsPerRound*n : null) : null,
      nextRewardFlag: n=== (current??0)+1,
      securedFlag: n===secured?.checkpointRoundNumber,
      isCurrentReward: n===current, ariaLabel: `Ronda ${n} de ${maxRounds}, nivel ${r?.level ?? ''}`
    };
  });
}
```

### Errors (RFC7807)

```json
// 400 InvalidGameState (current > maxRounds)
{ "type":"https://httpstatuses.com/400","title":"InvalidGameState","detail":"currentRoundNumber exceeds maxRounds","status":400,"traceId":"00-...","correlationId":"corr-uuid" }
// 404 GameNotFound
{ "type":"https://httpstatuses.com/404","title":"GameNotFound","status":404,"traceId":"00-...","correlationId":"corr-uuid" }
// 403 PlayerNotInGame
{ "type":"https://httpstatuses.com/403","title":"PlayerNotInGame","detail":"Player not joined","status":403,"traceId":"00-...","correlationId":"corr-uuid" }
```

- `X-Correlation-Id` requerido en request/response; `TraceId` OTel.

## 2. SignalR GameHub (reuse 029)

Events que disparan `hydrateLadder` (payload ignorado para rewards/level):

- `RoundCompleted { gameId, roundNumber, completedAt }`
- `QuestionAvailable { gameId, roundNumber, level, expiresAt }`
- `ScoreUpdated { gameId, playerId, totalPoints, securedPoints }` (para current/next/secured refresh)
- `GameFinished { gameId }` → terminal
- `Reconnected` (client) → hydrate inmediato

Todos requieren `hydrate GET /players/me` antes de mutar `LadderRow.state`.

## 3. No nuevos Commands

`POST /answers` `POST /withdraw` ya en 029; ladder es solo lectura. No `POST /ladder`.

## 4. Security

- `RequireAuthorization` `PLAYER` policy `Game.Play`; `sub` = `GameSession.playerId`.
- `X-Correlation-Id` prop; rate limiting `GamePlayLimiter` ya en Api si hydrate frecuente (debounce 300ms cliente).
- `must_change_password` claim gating 302.

## 5. Validation (server, si amplía)

- `maxRounds` 5..15, `currentRoundNumber` 1..maxRounds | null.
- `RewardRule.roundThreshold` 1..maxRounds unique, `pointsRequired` ≥0.

## References

- SPEC-029 `contracts/api-contracts.md` (GET /players/me 10 elementos).
- `src/OroQuizClash.Application/Features/Games/GetMyPlayerState.cs` `IEndpoint` `ISender`.
- `draft/constitution.md` V Server Truth, D Ledger, VI OIDC.
