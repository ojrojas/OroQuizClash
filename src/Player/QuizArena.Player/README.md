# QuizArena.Player — Angular 22 + NgRx SignalStore

Player Experience (Game Show) — SPA standalone para el jugador. Consume `design-system/tokens/design-tokens.css` via CSS variables (`data-theme="player"`). Backend autoritativo `OroQuizClash` (.NET 10) + `OroIdentityServer` OIDC PKCE.

**Estado actual**: `036-player-rewards` implementado — `Ready for Review`. Incluye `028 Lobby` → `029 Game` → `030 Rounds` → `035 Withdrawal` → `036 Rewards` completos.

## Stack

- **Angular 22** standalone (`input()`/`signal()`/`computed()` `@if`/`@for`, `provideRouter`, `HttpClient withFetch` `withInterceptors`)
- **NgRx Signals 22** (`signalStore`, `withState`, `withComputed`, `withMethods`, `withProps`, `patchState`, `rxMethod`, `tapResponse`, `switchMap`) — contexto privado por `GameId`/`RewardId` (`providers: [PlayerGameStore]` / `PlayerRewardsStore`)
- **angular-auth-oidc-client 17** PKCE `authorization_code` + `refresh_token` contra `OroIdentityServer` `/.well-known/openid-configuration` (`jwks_uri`, `sub`, `must_change_password`)
- **@microsoft/signalr 8** `GameHub` `RoundStarted/QuestionAvailable/ScoreUpdated/RoundCompleted/GameFinished/PlayerWithdrawn` + `withAutomaticReconnect [0,2000,5000,10000,30000]` + rehydrate REST (Constitución V)
- **Design System** `design-system/tokens/design-tokens.css` + `overrides/player.md` (`angular.json` styles, `data-theme="player"`, WCAG 2.2 AA, 375-1536 responsive, targets ≥44px, `prefers-reduced-motion`)
- **RxJS 7**, `TypeScript 5.8`, `Vitest 3` + `Testing Library Angular`, `eslint 9` + `@ngrx/eslint-plugin`

## Estructura

```
src/app/
├── app.routes.ts                 # Rutas protegidas authGuard + mustChangePasswordGuard
├── app.config.ts                 # provideAuth PKCE + HttpClient interceptors
├── core/
│   ├── auth/                     # auth.guard.ts, player-identity.guard.ts, callback
│   ├── interceptors/             # correlation-id, auth (secureRoutes=[apiUrl]), error (RFC7807)
│   └── realtime/                 # game-realtime.service.ts (HubConnectionBuilder)
├── features/
│   ├── lobby/                    # lobby.component, lobby.store, game-detail, waiting-room
│   ├── game/                     # game.component, question, timer, score-panel, leaderboard, player-rounds, withdrawal, answer-interaction
│   ├── result/                   # result.component (fin de partida)
│   ├── rewards/                  # rewards-catalog, reward-detail, redemption-history, consolation-badge, rewards-display.model
│   └── shared/                   # games.api.ts, rewards.api.ts, models/player.models.ts
├── shared/ui/                    # loading-skeleton, empty-state, error-state (CorrelationId/TraceId)
└── stores/                       # player-game.store, player-rounds.store, player-rewards.store, answer-interaction.store
```

## Rutas (app.routes.ts:1)

| Ruta | Componente | Guard | Descripción |
|------|------------|-------|-------------|
| `/` | redirect → `/lobby` | — | — |
| `/lobby`, `/player/lobby` | `LobbyComponent` | `authGuard`, `mustChangePasswordGuard` | Catálogo de partidas disponibles |
| `/lobby/:gameId`, `/player/lobby/:gameId` | `GameDetailComponent` | auth | Detalle partida 8 cols + join |
| `/game/:gameId` | `GameComponent` | auth | Juego cinematic 3 áreas + withdrawal |
| `/result/:gameId` | `ResultComponent` | auth | Resultado final |
| `/rewards` | `RewardsCatalogComponent` | auth | Wallet + catálogo 4 métricas (036) |
| `/rewards/history` | `RedemptionHistoryComponent` | auth | Historial paginado (036) — **antes** de `:rewardId` |
| `/rewards/:rewardId` | `RewardDetailComponent` | auth | Detalle + canje 2 pasos (036) |
| `/auth/callback`, `/auth/logout-callback` | `CallbackComponent` | — | OIDC PKCE |

## Stores

**`player-game.store.ts`** — `PlayerGameStore` 10 elementos (`Player/Game/GameSession/Round/Question/Answer/Score/SecuredPoints/Timer/Status`) scoped por `gameId` (`providers: [PlayerGameStore]`). `hydrate` via `GET /api/games/{id}/players/me` (3 métricas `Current/Secured/Potential`), `remainingSeconds` `computed` + `interval(1000)` + `serverNow` drift <1s, `submitAnswer`/`withdraw` `rxMethod` `X-Idempotency-Key` `sessionStorage` per `gameId`/`roundId`, `bindRealtime` `PlayerWithdrawn/GameFinished` → hydrate, `isTerminal`/`canAnswer` per `PlayerParticipationStatus`.

**`player-rounds.store.ts`** — `PlayerRoundsStore` `LadderState {ladder, maxRounds, currentRoundNumber, secured, rewardRules, _animatingRound}` `hydrateLadder` `GET /players/me` → `buildLadder(1..N, completed/current/upcoming)` + `bindRealtimeLadder debounce 100ms`.

**`player-rewards.store.ts`** (036) — `PlayerRewardsStore` `State {wallet:{availablePoints,lastUpdated,gameId}, catalog:RewardView[], history:RedemptionItem[], redeemStatus, error, isHydrating}` `computed isRedeemable(rewardId)`, `remainingPointsFor`, `rewardStatus` (`deriveRewardStatus`), `hydrate(gameId)` `GET /api/rewards?gameId` + `hydrateHistory()` `GET /api/redemptions` + `redeem(rewardId)` `rxMethod` `sessionStorage idemp-redeem-{rewardId}` `X-Idempotency-Key` → `POST /api/rewards/{id}/redeem` ledger `REWARD_REDEMPTION`, `hydrateFor(gameId)` para wallet por partida.

**`answer-interaction.store.ts`** — interacción de respuesta por ronda (`selectedOptionId`, `isSubmitting`).

## Features

### Player Lobby (028) — `src/app/features/lobby/`
Available Games 8 cols (`Game Name, Category, Difficulty, Number of Rounds, Players, Start Time, Prize, Status`) via `GET /api/games?status=WAITING_FOR_PLAYERS` paginado (`LobbyStore` `games/totalCount/page/pageSize` `load` rxMethod + `GamesApi.getGames`). Table ≥1024px / cards ≤768px stacked 375px, `players.display "current/max"` `prize "—"` fallback, paginator, `Join Game` `sessionStorage idemp-join-{gameId}` `X-Idempotency-Key` → `POST /api/games/{id}/players` idempotente `UNIQUE (GameId,UserId)` → redirect `/game/:id`, `View Game Information` → `GET /api/games/{id}` modal, `Leave Lobby` client-side, `data-theme="player"` WCAG 2.2 AA `aria-live` 44px, `X-Correlation-Id`.

### Player Game (029) — `src/app/features/game/`
Cinematic 3 áreas `game.component.ts` `display:grid grid-template-areas "header header" "sidebar center" "footer footer"` (280px 1fr ≥1024px, stacked 375px). Header `Current Round "Ronda 3/10" + Current Level + Timer` `background: var(--player-gradient-premium)`, Sidebar `<app-player-rounds>`, Center `QuestionComponent` `Four Answers radiogroup aria-checked` `Submit` `isCorrect` solo `EVALUATED`, Footer `ScorePanel` `Potential Reward` `computed potentialReward` + `SecuredPoints` + `Withdrawal Action` modal. `data-theme="player"` tokens premium responsive 375-1536 `aria-live` `role=timer/dialog` 44px.

### Player Rounds (030) — `ladder.model.ts` + `player-rounds.store.ts`
Ladder vertical `1..N` `state completed|current|upcoming` `isFinal` `isSecured <=checkpoint` `currentReward/nextRewardFlag/securedFlag` placeholder "—". `GameComponent` `grid 280px 1fr` sidebar sticky, `role="list"` `aria-current="step"` escudo `Asegurado` + corona `Final` `transition <400ms scale 1.02` + `prefers-reduced-motion` none, `max-height:60vh overflow-y:auto` 44px axe, sync server solo post-hydrate (V).

### Player Withdrawal (035) — `withdrawal.component.ts/.css` + `withdrawal-display.model.ts`
`WithdrawalComponent` `app-withdrawal` diálogo 3 métricas autoritativas `GET /api/games/{id}/players/me` `Score.totalPoints` `SecuredPoints.securedPoints` `PotentialReward` ("—" si no configurado) sin cálculo cliente. 2 warnings `"If you continue and answer incorrectly, you may lose your accumulated points."` `role="alert" aria-live assertive` `var(--color-destructive)` + `"Withdraw now and secure X points?"` X=`Secured` `var(--color-warning)`. 2 pasos `Withdrawal Action` → `Confirmar` `X-Idempotency-Key` `idemp-withdraw-{gameId}` `sessionStorage` per `gameId` → `POST /api/games/{id}/withdraw` `Game.WithdrawPlayer` `WithdrawalPolicy KEEP_SECURED_SCORE` `PointTransaction WITHDRAWAL` `WITHDRAWN` `isTerminal true canAnswer false` `Current=Secured` ledger idempotente, `data-theme="player"` `max-width:400px` `prefers-reduced-motion`.

### Player Rewards (036) — `src/app/features/rewards/` ⭐ Nuevo
**Points Wallet** `Available Points` autoritativo `GET /api/rewards?gameId` `availablePoints` ← `GamePlayer.Score.CurrentPoints` (ledger `PointTransaction`) 0% cliente → **Rewards Catalog** `app-rewards-catalog` grid `1 col 375px` → `2 col 768px` → `4 col 1536px` `gap:var(--space-3)` cards `Required Points` `Reward Status` badge `Canjeable` (`var(--color-success)`) / `Puntos insuficientes` (`var(--color-warning)`) / `Agotada` (`var(--color-destructive)`) / `No disponible` + `Remaining` `Quedan 400 pts` vs `Te faltan 700 pts` (`deriveRewardStatus`/`formatRemaining` en `rewards-display.model.ts`). **Reward Detail** `app-reward-detail` `Available/Required/Remaining/Status` 4 métricas `role="group" aria-label="Puntuaciones"` + **Redeem 2 pasos** `Canjear` (≥44px, disabled si `!isRedeemable`) → diálogo `role="dialog" aria-modal="true" aria-label="Confirmar canje"` `max-width:400px` `position:fixed inset:0` resumen `Required/Remaining` + `Confirmar` 44px → `PlayerRewardsStore.redeem(rewardId)` `X-Idempotency-Key` `idemp-redeem-{rewardId}` `sessionStorage` per `rewardId` → `POST /api/rewards/{id}/redeem` `Reward.ReserveStock` + `Game.ConsumePoints` `PointTransaction REWARD_REDEMPTION` `UNIQUE (PlayerId,IdempotencyKey)` idempotente → **Confirmation** `¡Canje realizado!` `Consumidos 800 pts` `Restantes 400` `Referencia` `Estado Canjeada` + CTAs `Ver historial`/`Seguir explorando`. **Redemption History** `app-redemption-history` `GET /api/redemptions` `role="list"` orden `RequestedAt` desc paginado `Cargar más` 44px, empty-state `"Aún no has canjeado recompensas"` + CTA `/rewards`. **Consolation Reward** `app-consolation-badge` `RewardRedemption.CreateAsConsolation` `APPROVED` `points 0` badge `var(--color-info,#3B82F6)` diferenciado, no en catálogo `Canjeable`, no descuenta `Stock`. `RewardsApi` (`rewards.api.ts:1`) `getRewards(gameId?)`, `getWallet`, `getMyRedemptions`, `redeem(rewardId, idempotencyKey, gameId)` `X-Idempotency-Key` + `X-Correlation-Id` + `Authorization Bearer` (`secureRoutes=[apiUrl]`). `data-theme="player"` tokens `var(--space-*/--color-*/--radius-*)` `prefers-reduced-motion` WCAG 375-1536 axe 0.

## APIs (features/shared/)

**`games.api.ts`** — `getGames`, `joinGame(gameId, idempotencyKey)` `X-Idempotency-Key`, `getMyState(gameId)` `GET /players/me`, `submitAnswer`, `withdraw(gameId, key)`, `getLeaderboard`.

**`rewards.api.ts`** (036) — `getRewards(gameId?, includeUnavailable?)` `GET /api/rewards?gameId`, `getWallet(gameId)`, `getMyRedemptions()` `GET /api/redemptions`, `redeem(rewardId, idempotencyKey, gameId)` `POST /api/rewards/{id}/redeem` `X-Idempotency-Key` per `rewardId`.

**Interceptores** (`core/interceptors/`) — `correlation-id.interceptor.ts` `X-Correlation-Id` UUID, `auth.interceptor.ts` `Bearer` solo `apiUrl` (`secureRoutes`), `error.interceptor.ts` RFC7807 `ProblemDetails` `{type,title,status,detail,code,traceId,correlationId}` `401→oidc.authorize()` `429→Retry-After` `CorrelationId/TraceId` audit.

## Design System

Ver `design-system/MASTER.md` + `overrides/player.md` — tokens CSS sin literales, `data-theme="player"`, WCAG 2.2 AA 375-1536 responsive, 44px targets, `aria-live` Timer/Score/Status, `role="dialog"` `aria-modal` `aria-label` confirmaciones, `prefers-reduced-motion` reduce, `angular.json` styles `design-system/tokens/design-tokens.css`.

## Quickstart

```bash
cd src/Player/QuizArena.Player
npm install
# @ngrx/signals ya en package.json (@angular/core 22, @ngrx/signals 22, angular-auth-oidc-client, @microsoft/signalr)
cp src/environments/environment.example.ts src/environments/environment.ts
# environment.ts → apiUrl=http://localhost:5000/api, identityAuthority=http://localhost:5080, gameHubUrl=http://localhost:5000/hubs/game

# AppHost orquesta sqlserver/postgres/redis/rabbitmq/identity-api/oroclash-api/player
aspire start # o dotnet run --project OroQuizClash.AppHost

npm start # http://localhost:4200 proxy /api → 5000
# Login OIDC PKCE → /lobby → /game/:id → /rewards?gameId= → /rewards/:rewardId → /rewards/history
```

**Validación manual quickstart (036)**
- V1 Wallet 1200: `/rewards?gameId=` → `Available 1200` + cards `800 Canjeable Quedan 400` vs `1500 Puntos insuficientes Te faltan 300`
- V2 Detail: `/rewards/:id` → `Available 1200 Required 800 Remaining 400 Canjeable` `Canjear` habilitado vs `800 vs 1500` deshabilitado
- V3 Redeem: `Canjear` → `Confirmar canje` `role="dialog"` → `POST /rewards/{id}/redeem` `X-Idempotency-Key` → `Confirmation` `Restantes 400` idempotente
- V4 History: `/rewards/history` 3 rows `RequestedAt` desc / vacío CTA
- V5 Consolation: fin partida elegible → badge `Consolation` `var(--color-info)` en history

## Tests

```bash
npm test -- --watch=false          # Vitest 3 + Testing Library Angular (jsdom)
# specs: rewards-catalog, reward-detail, redemption-history, consolation-badge, player-rewards.store, player-game.store, withdrawal, player-rounds, question, timer
ng lint                            # eslint 9 + @ngrx/eslint-plugin withState/withComputed/withMethods ordering

dotnet build OroQuizClash.slnx      # 0 errors
dotnet test                         # Domain 272 + Application 131 + Architecture 79 + Api 113 + Admin 269 + Infra 27 = 0 failed
```

Specs: `specs/027-player-application/` (US1-US5) → `028-lobby` → `029-game` → `030-rounds` → `035-player-withdrawal` (WithdrawalAction 3 métricas) → `036-player-rewards` (US1 Wallet/Catalog, US2 Detail/Redeem, US3 History, US4 Consolation, FR-001..013, SC-001..008) `Ready for Review`.

## Constitución

`I Domain First`, `II Clean Architecture`, `III BuildingBlocks`, `IV Vertical Slice CQRS`, `V Server Truth` (saldo `Available` solo vía ledger `PointTransaction` `REWARD_REDEMPTION`/`WITHDRAWAL`, `Reward Status` derivado server), `VI OroIdentityServer` (OIDC PKCE `jwks_uri` `sub`, `must_change_password`), `C Configurable Rules` (`Reward.PointsRequired/Stock`, `ConsolationPolicy`), `D Ledger` (`REWARD_REDEMPTION`/`CONSOLATION`), `F Concurrency/Idempotency` (`RowVersion` per `Reward`/`GamePlayer` + `UNIQUE (PlayerId,IdempotencyKey)` `idemp-redeem-{rewardId}`), `H Security` (`secureRoutes` `X-Correlation-Id`), `I Validation/Errors` (RFC7807 `ProblemDetails` `CorrelationId/TraceId`).

