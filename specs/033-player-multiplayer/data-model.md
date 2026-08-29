# Data Model: Player Multiplayer (033)

**Branch**: `033-player-multiplayer` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Overview

Modelo **solo lectura/proyección** multiplayer en Angular 22 — 5 estados privados per `sub=PlayerId+GameId/RoundId` (`Private Game/Answer/Score/Timer/Session`) aislados vía `GET /api/games/{id}/players/me` `sub` + `UNIQUE (GameId,RoundId,PlayerId)` + `RowVersion` per `GamePlayer`, y 4 vistas públicas (`Players`, `Players Remaining`, `Leaderboard` `totalPoints/level`, `Current Round` 3/10) sin `SelectedOptionId/isCorrect/Timer` de otros. Fuente autoritativa `OroQuizClash.Domain` (SQL Server `Game`/`GamePlayer`/`Answer`/`PointTransaction`). No nuevos agregados dominio.

## Entities (Proyecciones cliente — TypeScript interfaces + Domain referencia)

### 1. Private Game State (Domain + view)

```ts
// Domain (server) — per sub
interface PrivateGameStateDomain {
  game: {
    gameId: string;
    name: string;
    status: 'WAITING_FOR_PLAYERS'|'IN_PROGRESS'|'ROUND_IN_PROGRESS'|'ROUND_COMPLETED'|'FINISHED';
    configuration: GameConfiguration; // sin reward sensible
  };
  gameSession: {
    gameSessionId: string; // GamePlayerId
    playerId: string; // sub
    gameId: string;
    status: 'ACTIVE'|'WITHDRAWN'|'ELIMINATED'|'WINNER';
    currentRoundNumber: number;
    rowVersion: string; // base64 per GamePlayerId
  };
}

// View (cliente) — solo del requester
interface PrivateGameStateView {
  game: { gameId: string; name: string; status: string; configuration: any };
  gameSession: { gameSessionId: string; playerId: string; gameId: string; status: string; currentRoundNumber: number; rowVersion: string };
}

// Contract GET /players/me -> game + gameSession
interface PrivateGameStateDto {
  game: GameDto;
  gameSession: GameSessionDto;
}
```
- **Origen**: `Game` `GameId` + `GamePlayer` `GamePlayerId` `UserId=sub` `RowVersion` per `GamePlayerId`. `GetMyPlayerStateHandler` filtra `game.Players.First(p=>p.UserId==sub)`.
- **Validación**: `gameSession.playerId == sub` 100%; `RowVersion` per `GamePlayerId` no global `Game`.
- **Relaciones**: `Game 1──N GamePlayer 1──1 Private Game State` per `sub`.

### 2. Private Answer State (Domain + view)

```ts
interface PrivateAnswerStateDomain {
  answerId: string | null;
  playerId: string; // sub
  gameId: string;
  roundId: string;
  questionId: string;
  selectedOptionId: string | null;
  submittedAt: string | null; // server ISO UTC
  state: 'PENDING'|'SUBMITTED'|'EVALUATED'|'EXPIRED';
  isCorrect: boolean | null; // null si !EVALUATED
  idempotencyKey: string;
}

// View (cliente) — solo del requester
interface PrivateAnswerStateView {
  answerId: string | null;
  selectedOptionId: string | null;
  state: string;
  isCorrect: boolean | null; // filtrado si !EVALUATED
}

// Contract GET /players/me -> answer
interface AnswerDto {
  answerId: string | null;
  selectedOptionId: string | null;
  state: string;
  isCorrect: boolean | null;
}
```
- **Origen**: `Game.Answers` `UNIQUE (GameId,RoundId,PlayerId)` per `playerId`. `GetMyPlayerState` filtra `game.Answers.First(a=>a.PlayerId==sub && a.RoundId==currentRound.Id)`.
- **Validación**: `answer.PlayerId == sub` 100%; `isCorrect` null si `state != EVALUATED` para `PLAYER`; nunca `Answer` de B en payload de A.
- **Relaciones**: `GamePlayer 1──N Answer per Round` `UNIQUE`; `Answer 1──1 Question`.

### 3. Private Score State (Domain + view)

```ts
interface PrivateScoreStateDomain {
  score: { playerId: string; gameId: string; totalPoints: number; roundPoints: number; correctAnswers: number; currentLevel: string };
  securedPoints: { playerId: string; gameId: string; securedPoints: number; checkpointRoundNumber: number | null; policy: string };
}

// View (cliente) — solo del requester
interface PrivateScoreStateView {
  score: { totalPoints: number; roundPoints: number; correctAnswers: number; currentLevel: string };
  securedPoints: { securedPoints: number; checkpointRoundNumber: number | null; policy: string };
}

// Contract GET /players/me -> score + securedPoints
interface ScoreDto { playerId: string; gameId: string; totalPoints: number; correctAnswers: number; currentLevel: string; roundPoints?: number; }
interface SecuredPointsDto { playerId: string; gameId: string; securedPoints: number; checkpointRoundNumber: number | null; policy: string; }
```
- **Origen**: `GamePlayer.Score` (`PlayerScore` ValueObject) + `PointTransaction` ledger per `playerId` `sum(PointTransaction)` reconstruible.
- **Validación**: `score.playerId == sub`; `Leaderboard` público solo `totalPoints/level` sin `Answer` privado.
- **Relaciones**: `GamePlayer 1──1 Score`; `Score 1──N PointTransaction per Player`.

### 4. Private Timer (Domain + view)

```ts
interface PrivateTimerDomain {
  timeLimitSeconds: number; // 5..300
  expiresAt: string; // ISO UTC per GameRound
  remainingSeconds: number; // computed max(0,floor((expiresAt - now)/1000))
  state: 'RUNNING'|'STOPPED'|'EXPIRED';
  serverNow: string; // ISO UTC drift correction
}

// View (cliente) — per playerId+roundId
interface PrivateTimerView {
  timeLimitSeconds: number;
  expiresAt: string;
  remainingSeconds: number;
  state: string;
  serverNow: string;
}

// Contract GET /players/me -> timer
interface TimerDto { timeLimitSeconds: number; expiresAt: string; remainingSeconds: number; state: string; serverNow: string; }
```
- **Origen**: `GameRound` `StartedAt + TimeLimit` per `GameRound` con `serverNow` corrección; `GetMyPlayerState` retorna `Timer` per `currentRound`.
- **Validación**: `expiresAt` per `GameRound` no compartido en memoria; `serverNow` drift correction.
- **Relaciones**: `GameRound 1──1 Timer per Player` (view).

### 5. Private Session (Domain + view)

```ts
interface PrivateSessionDomain {
  gameSessionId: string; // GamePlayerId
  playerId: string; // sub
  gameId: string;
  status: string; // ACTIVE/WITHDRAWN/ELIMINATED/WINNER
  currentRoundNumber: number;
  rowVersion: string; // per GamePlayerId
}

// View (cliente) — solo del requester
interface PrivateSessionView {
  gameSessionId: string;
  playerId: string;
  gameId: string;
  status: string;
  currentRoundNumber: number;
  rowVersion: string;
}

// Contract GET /players/me -> gameSession
interface GameSessionDto { gameSessionId: string; playerId: string; gameId: string; status: string; currentRoundNumber: number; rowVersion: string; }
```
- **Origen**: `GamePlayer` `GamePlayerId` `UserId=sub` `RowVersion` per `GamePlayerId`. `Withdraw` usa `RowVersion` per `GamePlayerId`.
- **Validación**: `gameSession.playerId == sub`; `Withdraw` de A no afecta `RowVersion` de B.
- **Relaciones**: `Game 1──N GamePlayer 1──1 Private Session` per `sub`.

### 6. Players / Players Remaining (View público)

```ts
interface PlayersPublicView {
  players: Array<{ playerId: string; displayName: string; status: string; isActive: boolean }>;
  playersRemaining: number; // count IsActive (ACTIVE)
}

// Contract GET /api/games/{id}/players (público) o GET /game/{id}
interface PlayersDto { players: Array<{ playerId: string; displayName: string; status: string }>; playersRemaining: number; }
```
- **Origen**: `Game.Players` `IsActive = Status==ACTIVE`; `GetGamePlayersHandler` retorna `Players` sin `Answer/Score`.
- **Validación**: `PlayersRemaining = Players.filter(p=>p.status=='ACTIVE').length`.
- **Relaciones**: `Game 1──N GamePlayer` → `Players` público.

### 7. Leaderboard (View público)

```ts
interface LeaderboardPublicView {
  entries: Array<{ playerId: string; displayName: string; totalPoints: number; level: string; position: number }>;
}

// Contract GET /api/games/{id}/leaderboard (público)
interface LeaderboardDto { entries: LeaderboardEntry[]; }
interface LeaderboardEntry { playerId: string; displayName: string; totalPoints: number; level: string; position: number; }
```
- **Origen**: `Game.PointTransactions` per `playerId` `totalPoints` orden desc + `GamePlayer.Score.currentLevel` + `displayName`.
- **Validación**: Sin `SelectedOptionId/isCorrect/Timer/SecuredPoints` de otros; `totalPoints` público `level` público.
- **Relaciones**: `Game 1──N LeaderboardEntry` view.

### 8. Current Round (View público)

```ts
interface CurrentRoundPublicView {
  roundId: string;
  gameId: string;
  roundNumber: number;
  level: string;
  status: string; // WAITING/IN_PROGRESS/COMPLETED
  questionId: string; // sin AnswerOption detallada para otros
}

// Contract GET /api/games/{id}/rounds/current (público) o GET /players/me -> round
interface RoundDto { roundId: string; gameId: string; roundNumber: number; level: string; status: string; questionId: string; }
```
- **Origen**: `Game.CurrentRound` `GameRound` genérico.
- **Validación**: `RoundNumber` 1..MaxRounds; sin `Answer` privado.
- **Relaciones**: `Game 1──1 Current Round` público.

## Relationships

```
Player (sub) 1──N GameSession N──1 Game 1──N GameRound 1──1 Question
Player 1──1 Private Game State (Game + GameSession per sub, RowVersion per GamePlayerId)
Player 1──N Private Answer State per Round (UNIQUE GameId+RoundId+PlayerId, isCorrect filtrado)
Player 1──1 Private Score State (Score + SecuredPoints per sub, sum PointTransaction)
Player 1──1 Private Timer per Round (expiresAt + serverNow, scoped memory)
Player 1──1 Private Session (GameSession per sub, RowVersion per GamePlayerId)
Game 1──N Players (público IsActive count) 1──N LeaderboardEntry (público totalPoints/level) 1──1 Current Round (público RoundNumber/Level)
PlayerGameStore scoped per GameComponent (providers: [PlayerGameStore]) 1──1 Private State per sub (no shared root)
GameRealtimeService (ScoreUpdated/LeaderboardUpdated/Reconnected) ──▶ hydrate → GET /players/me privado per sub + GET /leaderboard público
```

## State Transitions (cliente observa, servidor decide)

- **Private**: `idle (PENDING, no Answer)` -- `SubmitAnswer sub=A` --> `SUBMITTED/EVALUATED` per `sub` A solo; B sigue `PENDING`.
- **Public**: `PlayersRemaining 4` -- `Withdraw sub=B` --> `3` público para todos vía `Players` `IsActive` count.
- **Hydrate**: `ScoreUpdated` para cualquier jugador → cada cliente `hydrateFor(gameId)` → `GET /players/me` per `sub` privado + `GET /leaderboard` público.

## Validation Rules

- `gameSession.playerId == sub` 100%; `answer.PlayerId == sub`; `score.playerId == sub`; `timer` per `playerId+roundId`.
- `PlayersRemaining = Players.filter(IsActive).length` 100%.
- `Leaderboard.entries[].totalPoints` público sin `IsCorrect/SelectedOptionId`.
- `UNIQUE (GameId,RoundId,PlayerId)` + `RowVersion` per `GamePlayerId` no global `Game`.
- `X-Correlation-Id` UUID v4 per `GET /players/me` + `GET /leaderboard`.
- `PlayerGameStore` scoped `providers: [PlayerGameStore]` per `GameComponent` no `providedIn: 'root'`.
- `isCorrect` filtrado null si `state != EVALUATED` para `PLAYER` en `Private Answer`.

## Persistence (cliente)

- **En memoria**: `PlayerGameStore` `DeepSignal` `Private State` scoped per `GameComponent` `providers: [PlayerGameStore]` (no root), `computed` para `potentialReward/roundPoints/totalPoints` per `sub`.
- **Efímero**: `sessionStorage` solo `idemp-{roundId}` per `Round` para `Answer` (031), no para multiplayer; `Leaderboard` no cache.
- **Server**: SQL Server `Game` RowVersion + `GamePlayer` RowVersion per `GamePlayerId` + `Answer` UNIQUE + `PointTransaction` ledger + Outbox. `GetMyPlayerState` Query: `GameByIdWithAnswersSpecification` → `PlayerGameState` per `sub`; `GetLeaderboard` Query: `GameById` → `LeaderboardEntry[]` sin privados.
```

## Indexes / Queries (server reference)

- `GamePlayer` IX `(GameId, UserId)` + `RowVersion` per `GamePlayerId`; `Answer` UK `(GameId,RoundId,PlayerId)` + `RowVersion`; `PointTransaction` IX `(GameId, PlayerId, CreatedAt)`.
- `GetMyPlayerState` Query: `GameByIdWithAnswersSpecification` + `AsNoTracking`; `GetLeaderboard` Query: `GameById` + `Players` + `PointTransactions` sum.

## UI States

- `Loading` skeleton `Players/Leaderboard/Current Round` `aria-busy`.
- `Empty` sin jugadores.
- `ErrorState` `ProblemDetails detail` + `CorrelationId/TraceId` + `Retry` per `GET /players/me`/`GET /leaderboard`.
- `Players` `role="list"` `aria-live polite` `Players Remaining` count.
- `Leaderboard` `role="list"` `aria-live polite` `position` + `totalPoints` + `level`.
- `Current Round` `role="status"` `aria-live polite` "Ronda 3/10".
- `Private` `Score/Answer/Timer/Session` nunca en `Leaderboard`.
```

