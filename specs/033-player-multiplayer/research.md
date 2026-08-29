# Research: Player Multiplayer (033)

**Branch**: `033-player-multiplayer` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Summary

0 `NEEDS CLARIFICATION` en spec; investigación consolida 5 decisiones para aislamiento multiplayer — 5 estados privados (`Private Game State`, `Private Answer State`, `Private Score State`, `Private Timer`, `Private Session` per `sub=PlayerId+GameId/RoundId`) vía `GET /api/games/{id}/players/me` `sub` + `UNIQUE (GameId,RoundId,PlayerId)` + `RowVersion` per `GamePlayer`, y 4 vistas públicas (`Players`, `Players Remaining`, `Leaderboard` `totalPoints/level`, `Current Round` 3/10) sin `SelectedOptionId/isCorrect/Timer` de otros — en `QuizArena.Player` Angular 22 SPA (SPEC-027/029/030/031/032) con `PlayerGameStore` scoped `providers: [PlayerGameStore]` per `GameComponent` (no `providedIn: 'root'`), `GameRealtimeService` per `gameId+sub` `withAutomaticReconnect` → `hydrate` (Server Truth V).

## Decisions

### 1. Cinco estados privados aislados per `sub` via `GET /players/me`

- **Decision**: `GetMyPlayerStateHandler` usa `GameClaims.GetSub(http.User)` `sub` para filtrar `GamePlayer`/`Answer`/`Score`/`Timer`/`GameSession` solo del requester: `Answer` con `SelectedOptionId/isCorrect` solo si `answer.PlayerId==sub` y `state==EVALUATED` sino `IsCorrect=null` (SPEC-006); `Score`+`SecuredPoints` per `sub` via `PointTransaction` ledger `UNIQUE (GameId,PlayerId)`; `Timer` per `GameRound` con `serverNow` corrección pero per `playerId+roundId` (aunque misma `Round` el `Timer` es mismo `expiresAt` pero no compartido en memoria); `GameSession` per `GamePlayerId` `RowVersion` per `GamePlayer` (no global `Game` `RowVersion`). Angular `PlayerGameStore` scoped `providers: [PlayerGameStore]` per `GameComponent` instancia (no `providedIn: 'root'` singleton) aísla `Answer/Score/Timer` en memoria: 4 browsers A-D con JWT `sub=A/B/C/D` cada uno tiene `PlayerGameStore` con `signalStore` separado `isolation.spec.ts` verifica no contaminación.

- **Rationale**: FR-001..005 + SC-001/003/004/005 + Constitución V (Server Truth per `sub`) + F (`UNIQUE` + `RowVersion` per `GamePlayer`) + H (`sub=PlayerId` JWT).

- **Alternatives**: `providedIn: 'root'` singleton `PlayerGameStore` compartido (rechazado — `Answer` de A contaminaría B, `isolation.spec.ts` fallaría, fuga directa); exponer `Answer` de todos en `GET /players/me` con `playerId` filtro cliente (rechazado — violación V, cliente no confiable).

- **Accessibility**: No aplica a privados, pero `Private Timer` `aria-live polite` local per jugador.

### 2. Cuatro vistas públicas sin fuga via `GET /leaderboard` + `GET /players` + `GET /rounds/current`

- **Decision**: `GetLeaderboardHandler` retorna `LeaderboardEntry[]` con `playerId/displayName/totalPoints/level/position` ordenado por `totalPoints` desc, sin `SelectedOptionId/isCorrect/Timer/SecuredPoints` detallado; `GetGamePlayersHandler` retorna `Players[]` con `playerId/displayName/status IsActive` + `PlayersRemaining = Players.count(p=>p.IsActive)` (`ACTIVE` count); `GetCurrentRoundHandler` retorna `Round` público `RoundNumber/Level/Status` genérico sin `Question` detallada para otros. Contratos en `contracts/api-contracts.md` §1-3 verifican 0% leak: `GET /leaderboard` con 2 JWTs en paralelo no expone `IsCorrect`, `GET /players/me` de A no contiene `Answer` de B.

- **Rationale**: FR-006..008 + SC-002 + Constitución D (Ledger solo `totalPoints` público) + J (REST thin `IEndpoint`).

- **Alternatives**: Incluir `IsCorrect`/`SelectedOptionId` en `Leaderboard` para "transparencia" (rechazado — viola fairness, permite espiar `Answer` de otro, trivia `SecurePoints`).

### 3. `PlayerGameStore` scoped per `GameComponent` + `isolation.spec.ts` 4 instancias

- **Decision**: `GameComponent` `providers: [PlayerGameStore, PlayerRoundsStore]` ya en 029 scoped per instancia, no `providedIn: 'root'`. Test `isolation.spec.ts` crea 4 `TestBed` configuraciones A-D con `provideHttpClientTesting` mock `getMyState` per `sub` con `score 100 vs 250` y `answer opt-A vs opt-C`, verifica `storeA.answer().selectedOptionId !== storeB.answer().selectedOptionId` y `storeA.score().totalPoints !== storeB.score().totalPoints` sin contaminación cross-store. `GameRealtimeService` per `gameId` con `accessTokenFactory` per `sub` cada instancia tiene `HubConnection` con `?gameId` + `Authorization: Bearer` per `sub`.

- **Rationale**: FR-009 + SC-003 + Constitución F (concurrency `RowVersion` per `GamePlayer`).

- **Alternatives**: `providedIn: 'root'` singleton (rechazado — ya justificado); `BehaviorSubject` manual por jugador (rechazado — no escala a 10, carece de `DeepSignal`).

### 4. Realtime `ScoreUpdated/LeaderboardUpdated/RoundCompleted/Reconnected → hydrate` per jugador

- **Decision**: `GameRealtimeService` `withAutomaticReconnect [0,2000,5000,10000,30000]` eventos `ScoreUpdated`/`LeaderboardUpdated`/`RoundCompleted`/`GameFinished`/`Reconnected` → `PlayerGameStore.hydrateFor(gameId)` `GET /players/me` privado per `sub` (no payload del evento) + `GET /leaderboard` público (si se usa `LeaderboardComponent` con su propio `hydrateLeaderboard`). `hydrate` actualiza `score/securedPoints/answer/timer` per `sub` y `Players/Leaderboard/CurrentRound` públicos genéricos. `ScoreUpdated` payload ignorado (V).

- **Rationale**: FR-010 + SC-006 + Constitución G (Realtime/Outbox) + V (Server Truth).

- **Alternatives**: Confiar en `ScoreUpdated` payload para `Current Points` de A en B (rechazado — viola V, `isolation` fallaría).

### 5. `X-Correlation-Id` + `ErrorState` + JWT gating per `sub` + `Design System`

- **Decision**: `correlationIdInterceptor` (`X-Correlation-Id: crypto.randomUUID()` per `GET /players/me` + `GET /leaderboard`) + `authInterceptor` `secureRoutes=[apiUrl]` + `errorInterceptor` RFC7807 ya en 027/029. `GetMyPlayerState`/`GetLeaderboard` requieren `RequireAuthorization`, `GameClaims.GetSub` `sub`, `must_change_password` gating 302 → `/auth/change-password`; sin JWT → 401 OIDC. `LeaderboardComponent` `Players` `role="list"` `aria-live polite` `data-theme="player"` tokens `var(--space-3)` `gap` `min-height 44px` responsive 375 1col / 768 4col sin literales, `prefers-reduced-motion` reduce.

- **Rationale**: FR-011..013 + SC-007/008 + Constitución H/I/J + SPEC-016.

- **Alternatives**: Sin `X-Correlation-Id` en `Leaderboard` (rechazado — trazabilidad OTel).

## Resolved Clarifications

| Topic | Resolution |
|-------|------------|
| `Players Remaining` cuenta | `Players.count(p=>p.IsActive)` `ACTIVE` count, no `WITHDRAWN/ELIMINATED` |
| `Leaderboard` contenido | `totalPoints/level/displayName/position` orden `totalPoints` desc, sin `IsCorrect/SelectedOptionId/Timer/Secured` |
| `Private Timer` compartido | Per `playerId+roundId` vía `GameRound.expiresAt` mismo para todos si misma ronda pero memoria `PlayerGameStore` scoped no compartida |
| `RowVersion` global vs per GamePlayer | Per `GamePlayerId` `RowVersion`, `Withdraw` de A no afecta `GameSession` de B |
| `GameComponent` providers | `providers: [PlayerGameStore]` per instancia `GameComponent` scoped, no `providedIn: 'root'` |
| `SignalR` per jugador | `HubConnection` per `gameId` + `accessTokenFactory` per `sub` con JWT `sub`, `hydrate` privado per `sub` |

## References

- `draft/constitution.md` §I–VI, §A-J, §V Server Truth per `sub`, §F `UNIQUE (GameId,RoundId,PlayerId)` `RowVersion` per `GamePlayer`, §G Realtime `ScoreUpdated`, §H `sub=PlayerId`.
- `draft/game-concept.md` §Multiplayer §Scoring §Game/Round Lifecycle.
- `design-system/MASTER.md` + `overrides/player.md` + `tokens/design-tokens.css` Cinematic `data-theme="player"` WCAG 375-1536.
- `src/Player/QuizArena.Player` `stores/player-game.store.ts` scoped `features/game/leaderboard.component.ts` `features/shared/games.api.ts` `getMyState/getLeaderboard/getPlayers` `core/realtime/game-realtime.service.ts` `withAutomaticReconnect` (SPEC-027/029/030/031/032).
- `src/OroQuizClash.Application/Features/Games/` `GetMyPlayerState` privado `sub` + `GetLeaderboard` público `IEndpoint` `GameClaims`.
- `OroQuizClash.AppHost/AppHost.cs` `quizarena-player` container `node:22-alpine`.
- `specs/011-multiplayer/` `specs/029-player-game/` `specs/032-player-scoring/` (previos).
