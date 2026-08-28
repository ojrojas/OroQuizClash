# Contracts: Realtime GameHub for Player (027)

**Branch**: `027-player-application` | **Date**: 2026-08-28

## Hub

- **URL**: `{{oroclash-api}}/hubs/game` (prod) o `http://localhost:5000/hubs/game` (dev). Via Aspire `http://oroclash-api/hubs/game` si discovery.
- **Transport**: WebSockets (fallback ServerSentEvents/LongPolling). `withAutomaticReconnect([0,2000,5000,10000,30000])`.
- **Auth**: `accessTokenFactory: () => oauthService.getAccessToken()` (PKCE) o Cookie si BFF. Hub `[Authorize]` valida JWT contra OroIdentityServer `jwks_uri`.
- **Groups**: `game-{gameId}` (server `Groups.AddToGroupAsync` al `JoinGame` y al `OnConnectedAsync` leyendo `gameId` de query o de `players/me`). Cliente invoca `JoinGameGroup(gameId)` si no es automático.
- **Client lib**: `@microsoft/signalr` 8.x.

## Server → Client events (hub methods)

All events carry `gameId` + `correlationId` + `serverNow` ISO. Payloads son notificaciones — **el cliente debe rehidratar vía REST `GET .../players/me`** para estado autoritativo (Constitución V).

### RoundStarted
```ts
interface RoundStartedEvent {
  gameId: string;
  round: { roundId: string; roundNumber: number; level: string; startedAt: string; expiresAt: string; };
  correlationId: string;
  serverNow: string;
}
```
Hub: `Clients.Group($"game-{gameId}").SendAsync("RoundStarted", payload)`

### QuestionAvailable
```ts
interface QuestionAvailableEvent {
  gameId: string;
  roundId: string;
  question: { questionId: string; text: string; answerOptions: { optionId: string; text: string }[]; difficulty: string; };
  expiresAt: string; // authoritative
  correlationId: string;
  serverNow: string;
}
```
- Cliente: `patchState(store, { round, question, answer: pending, timer: { expiresAt, state: 'RUNNING' } })` luego rehidratación opcional para `serverNow`.

### ScoreUpdated
```ts
interface ScoreUpdatedEvent {
  gameId: string;
  playerId: string; // solo el afectado debe aplicar; otros ignoran o actualizan leaderboard
  score: { totalPoints: number; correctAnswers: number; currentLevel: string; };
  securedPoints: { securedPoints: number; checkpointRoundNumber: number | null; };
  roundId: string;
  correlationId: string;
  serverNow: string;
}
```
- Cliente filtra `if (payload.playerId !== me.playerId) return` (FR-002). Luego `patchState({ score, securedPoints })` o `rehydrate()`.

### RoundCompleted
```ts
interface RoundCompletedEvent {
  gameId: string;
  roundId: string;
  roundNumber: number;
  correlationId: string;
  serverNow: string;
}
```
- Cliente: `patchState({ round: { status: 'COMPLETED' }, timer: { state: 'STOPPED' } })` + opcional leaderboard refresh.

### GameFinished
```ts
interface GameFinishedEvent {
  gameId: string;
  status: 'FINISHED' | 'CANCELLED' | 'FORCED_FINISHED';
  finalScores: { playerId: string; totalPoints: number; playerStatus: string; }[]; // opcional
  correlationId: string;
  serverNow: string;
}
```
- Cliente: `patchState({ status: { gameStatus: payload.status, isTerminal: true, canAnswer: false }, timer: { state: 'STOPPED' } })`.

### LeaderboardUpdated (opcional, SPEC-011)
```ts
interface LeaderboardUpdatedEvent {
  gameId: string;
  leaderboard: { rank: number; playerId: string; totalPoints: number }[];
  correlationId: string;
}
```

## Client → Server invocations

### JoinGameGroup
```ts
await connection.invoke('JoinGameGroup', gameId: string);
```
- Server validates `Context.UserIdentifier === sub` is participant of `gameId`, then `Groups.AddToGroupAsync`.

### LeaveGameGroup
```ts
await connection.invoke('LeaveGameGroup', gameId: string);
```

## Angular Service (sketch)

```ts
@Injectable({ providedIn: 'root' })
export class GameRealtimeService {
  private conn: HubConnection | null = null;
  private gameId = signal<string | null>(null);

  events$ = new Subject<GameEvent>(); // RoundStarted | QuestionAvailable | ...

  constructor(private oauth: OAuthService) {}

  async connect(gameId: string) {
    if (this.conn?.state === HubConnectionState.Connected && this.gameId() === gameId) return;
    await this.disconnect();
    this.gameId.set(gameId);
    this.conn = new HubConnectionBuilder()
      .withUrl(`${apiUrl}/hubs/game?gameId=${gameId}`, { accessTokenFactory: () => this.oauth.getAccessToken() })
      .withAutomaticReconnect([0,2000,5000,10000,30000])
      .configureLogging(LogLevel.Information)
      .build();
    this.conn.on('RoundStarted', p => this.events$.next({ type: 'RoundStarted', payload: p }));
    this.conn.on('QuestionAvailable', p => this.events$.next({ type: 'QuestionAvailable', payload: p }));
    this.conn.on('ScoreUpdated', p => this.events$.next({ type: 'ScoreUpdated', payload: p }));
    this.conn.on('RoundCompleted', p => this.events$.next({ type: 'RoundCompleted', payload: p }));
    this.conn.on('GameFinished', p => this.events$.next({ type: 'GameFinished', payload: p }));
    this.conn.onreconnected(() => this.events$.next({ type: 'Reconnected' }));
    await this.conn.start();
    await this.conn.invoke('JoinGameGroup', gameId);
  }

  async disconnect() { await this.conn?.stop(); this.conn = null; }

  // Rehydrate policy: caller (store) does GET .../players/me on each event
}
```

## Rehydrate Policy (FR-005/FR-017)

- **Never trust event payload for Score/SecuredPoints/Answer correctness** — toast/optimistic UI from event, authoritative state from REST rehydrate inside `rxMethod`.
- **Reconnected** → `hydrate()` con backoff.
- **ScoreUpdated for other player** → ignorado para privados, opcional refresh leaderboard.
