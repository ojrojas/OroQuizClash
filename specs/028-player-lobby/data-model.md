# Data Model: Player Lobby (028)

**Branch**: `028-player-lobby` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Overview
Modelo **solo lectura/proyección** en Angular 22 (lobby) sobre `oroclash-api`. La fuente autoritativa es `OroQuizClash.Domain` (SQL Server). No se crean nuevos agregados; Prize es proyección de `Reward` si existe. Paginación server-side vía `Specification`.

## Entities (Proyecciones cliente — TypeScript interfaces)

### 1. Game (Lobby Projection)
```ts
interface GameSummary {
  gameId: string;            // GameId StronglyTypedId<Guid>
  name: string;              // Game Name
  categoryId: string;
  categoryName: string;      // Category.Name
  difficulty: number;        // InitialDifficulty 1..5
  difficultyName: string;    // Basic..Expert
  minRounds: number;         // ≥5
  maxRounds: number;
  numberOfRoundsDisplay: string; // "5-10" o "5"
  players: { current: number; max: number; display: string; }; // "3/10"
  startTime: string;         // ISO 8601 UTC (CreatedAt)
  startTimeLocal: string;    // derivado cliente: Intl.DateTimeFormat
  prize: string;             // Reward.Name o "—"
  status: string;            // "WAITING_FOR_PLAYERS" (Available Games)
  version: string;           // RowVersion base64 (If-Match opcional)
}
```
- **Origen**: `Game` aggregate `GameResponse` vía `GetGamesQuery` `GameFilterSpecification(Where Status==WAITING_FOR_PLAYERS, Include Players, OrderBy CreatedAt desc, ApplyAsNoTracking, pagination)`.
- **Validación**: `status` solo WAITING_FOR_PLAYERS para Available Games; `categoryName` de `Category` publicada; `difficulty` 1..5.
- **Relaciones**: 1 Game → N GamePlayer (count), N GameRound (min/max).

### 2. PaginatedGames
```ts
interface PaginatedGames {
  items: GameSummary[];
  totalCount: number;
  page: number;
  pageSize: number;          // default 20, max 50
  totalPages: number;
}
```
- **Origen**: `GetGamesHandler` retorna `PaginatedGamesResponse` con `totalCount` para paginación.
- **Validación**: `page >=1`, `pageSize 1..50` (cap).

### 3. GameDetail (View Game Information)
```ts
interface GameDetail extends GameSummary {
  timeLimitPerQuestionSeconds: number;
  pointsPerRound: number;
  withdrawalPolicy: string;   // KEEP_CURRENT_SCORE etc.
  lossPolicy: string;
  playersList: { playerId: string; displayName: string }[]; // opcional, count
  configuration: GameConfiguration;
}
```
- **Origen**: `GET /api/games/{id}` `GetGameHandler` `GameByIdSpecification(Include Rounds, Players)` AsNoTracking.
- **Validación**: solo lectura; no expone `Answer`/`Score` ajenos (FR-013).

### 4. GamePlayer / GameSession (Join Game result)
```ts
interface GameSession {
  gameSessionId: string;     // GamePlayerId
  playerId: string;          // sub JWT
  gameId: string;
  status: string;            // ACTIVE
  joinedAt: string;
  version: string;
}
```
- **Origen**: `JoinGameHandler` `Game.JoinPlayer(userId)` → `GamePlayer` `UNIQUE (GameId,UserId)` `RowVersion`.
- **Validación**: creación solo si `GameStatus==WAITING_FOR_PLAYERS` y `Players.Count < MaxPlayers`; idempotente por `X-Idempotency-Key` + `UNIQUE`.
- **Relaciones**: `Game (1) ──HasMany──> GamePlayer (0..MaxPlayers)` `UNIQUE`.

### 5. ProblemDetails (RFC 7807)
```ts
interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  detail: string;
  code: string;              // GameNotFound, GameFull, GameNotWaitingForPlayers, AlreadyJoined (mapped to 200 idempotent), PlayerIdentityMismatch
  traceId: string;
  correlationId: string;
}
```
- **Origen**: `GlobalExceptionHandler` + `Result.ToHttpResult()`.

## Relationships
```
Player (sub) 1──N GameSession N──1 Game 1──1 Category
                              │         1──N GamePlayer (count)
                              └── 0..1 Reward (Prize) via GameConfiguration.RewardRules
Available Games: Game Where Status==WAITING_FOR_PLAYERS (filtered Specification)
PaginatedGames: page/pageSize → items: GameSummary[]
GameDetail: GameSummary + Configuration + PlayersList
```

## State Transitions (lobby observa, servidor decide)
- **Game.Status**: `WAITING_FOR_PLAYERS → IN_PROGRESS → ROUND_IN_PROGRESS ↔ ROUND_COMPLETED → FINISHED/CANCELLED/FORCED_FINISHED` (Game Lifecycle A). Lobby solo muestra `WAITING_FOR_PLAYERS`; `IN_PROGRESS` ya no es joinable.
- **Join**: `no GamePlayer → ACTIVE` (JoinPlayer) | `Already exists → 200 same GameSession` (idempotente).

## Validation Rules (cliente refleja dominio)
- `Join Game` habilitado solo si `status==WAITING_FOR_PLAYERS && current < max`; server revalida.
- `GameId` requerido GUID; `X-Idempotency-Key` UUID v4 por `gameId` en `sessionStorage`.
- `page` ≥1, `pageSize` 1..50; orden fijo `CreatedAt desc`.
- `Prize` never required; placeholder "—" si null.

## Persistence (cliente)
- **En memoria**: `LobbyStore` o `signalStore` `withState { games: GameSummary[], totalCount, page, isLoading, error }` scoped por lobby.
- **Efímero**: `sessionStorage idemp-join-{gameId}` para reintento sin duplicar. Nunca `localStorage`.
- **Server**: SQL Server `Game` (RowVersion, index Status/CreatedAt), `GamePlayer` (UK GameId+UserId, RowVersion), `Reward` opcional, Outbox `PlayerJoinedDomainEvent`.

## Indexes / Queries (server reference)
- `Game` IX `Status, CreatedAt desc` (lobby order), IX `Configuration.CategoryId`.
- `GamePlayer` UK `(GameId, PlayerId)` + IX `PlayerId` + RowVersion.
- `GameFilterSpecification`: `Where(Status==WAITING...)` + `OrderByDescending(CreatedAt)` + `Skip((page-1)*pageSize).Take(pageSize)` + `Include(Players)` `AsNoTracking`.
- `GameByIdSpecification`: `Where(Id==gameId)` + `Include(Rounds, Players)` `AsNoTracking`.

## UI States
- `Loading` (skeleton), `Empty` ("No hay partidas disponibles" + Refresh), `Ready` (tabla/tarjetas 8 campos), `Error` (ProblemDetails detail + CorrelationId/TraceId + Retry, aria-live assertive).
