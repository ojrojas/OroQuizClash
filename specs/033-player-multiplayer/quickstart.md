# Quickstart: Player Multiplayer (033)

**Branch**: `033-player-multiplayer` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

Validation guide — 5 privados aislados per `sub` + 4 públicos sin fuga via `GET /players/me` `sub` + `GET /leaderboard`.

## Prerequisites

- .NET 10 SDK, Node 22 LTS, Angular CLI 22, `podman` para OroIdentityServer.
- `dotnet workload install aspire`, `podman build -f src/IdentityServer/IdentityServer/Dockerfile -t oroidentityserver:latest .`
- Secrets: `export symmetric_security_key="$(openssl rand -base64 32)"` `export seed_admin_password="Admin@123456"`.
- Player SPA ya con SPEC-027/029/030/031/032: `QuizArena.Player` `PlayerGameStore` scoped `providers: [PlayerGameStore]` per `GameComponent` + `ScorePanelComponent` 5 métricas + `LeaderboardComponent` + `GamesApi.getMyState/getLeaderboard/getPlayers` + `GameRealtimeService` `withAutomaticReconnect` `proxy.conf.json` `/api`→5000.
- Design System 016 ya en `angular.json` styles `design-system/tokens/design-tokens.css` `data-theme="player"`.

## Setup

```bash
aspire start
# wait: sqlserver, postgres, redis, rabbitmq, identity-api 5080/5086, oroclash-api 5000, quizarena-player 4200

cd src/Player/QuizArena.Player
npm install --legacy-peer-deps
cp src/environments/environment.example.ts src/environments/environment.ts
# apiUrl=http://localhost:5000/api, identityAuthority=http://localhost:5080, gameHubUrl=http://localhost:5000/hubs/game

# Register quizarena-player public PKCE (once) via Admin UI http://localhost:5080
# clientId=quizarena-player, public, PKCE, redirectUris=http://localhost:4200/auth/callback, scopes openid profile email offline_access api

# Create 2 test users via Admin UI: player-a@test.com / PlayerA123!, player-b@test.com / PlayerB123!
```

## Run

```bash
cd src/Player/QuizArena.Player
npm start # http://localhost:4200
# Login as player-a → lobby (028) Create game MaxPlayers 4 MaxRounds 5 → Join as player-a
# Login as player-b in incognito → Join same game → /player/game/:gameId (029) con 2 players
```

## Validation Scenarios

### V1 — Privados aislados per sub (US1, FR-001..005, SC-001)

1. Con `Game` `ROUND_IN_PROGRESS` con 4 jugadores A-D, login como A (JWT `sub=A`) → `GET /api/games/{id}/players/me` → verify `answer.selectedOptionId` de A (`opt-A`) y `score.totalPoints` de A (100), `gameSession.playerId==sub-A`, `Timer` con `expiresAt` propio.
2. Login como B en incognito (JWT `sub=B`) mismo `gameId` → `GET /players/me` → verify `answer` de B (`opt-C`) y `score` de B (250) distintos de A; inspeccionar payload de A no contiene `Answer` de B.
3. Intentar `POST /api/games/{id}/answers` con `playerId=B` en body mientras JWT `sub=A` → verify servidor usa `sub=A` y no muta `Answer` de B; 403 si intenta `GameSession` de otro.

**Expected**: SC-001 0% leak privado, 100% `sub` aislado.

### V2 — Públicos sin fuga (US2, FR-006..008, SC-002)

1. Con 4 jugadores `ACTIVE`, como A abrir `GET /api/games/{id}/leaderboard` → verify `entries` con `displayName` + `totalPoints` + `level` sin `selectedOptionId/isCorrect/Timer/Secured` de otros.
2. `GET /api/games/{id}/players` → verify `players` con `displayName/status` y `playersRemaining` 4, `Current Round` 3/10 sin `Answer` privado.
3. Con B `WITHDRAWN`, como A refrescar `Players Remaining` → verify 3 y `Leaderboard` actualiza sin `isCorrect` de B.
4. `axe` → verify `role="list"` `aria-live polite` para `Leaderboard` sin leak.

**Expected**: SC-002 0% fuga públicos.

### V3 — Session/Timer per jugador (US3, FR-004..005, SC-004)

1. Con A y B en misma `Game`, `GET /players/me` de A → `GameSession.currentRoundNumber 2` `RowVersion AAA=` y `Timer expiresAt 12:00:30Z`; B → `RowVersion BBB=` distinto.
2. A `POST /withdraw` → `GameSession` A `WITHDRAWN` `RowVersion++`, B sigue `ACTIVE` sin interferencia.
3. A desconecta `Reconnected` → `hydrate` → `Private Session` de B no resetea.

**Expected**: SC-004 100% per `playerId` sin interferencia.

### V4 — Concurrencia 4 instancias sin interferencia (US4, FR-009/010, SC-003/005)

1. Simular 4 browsers A-D cada uno `PlayerGameStore` scoped `providers: [PlayerGameStore]` per `GameComponent`, enviar `SubmitAnswer` simultáneo A `opt-A` y B `opt-C` → verify `storeA.answer().selectedOptionId==opt-A` y `storeB==opt-C` sin contaminación.
2. `ScoreUpdated` para A → B hace `hydrate` y ve su propio `Score` no el de A, `Leaderboard` público se actualiza con `totalPoints` genérico.

**Expected**: SC-003 100% aislado, SC-005 `QuestionAlreadyAnswered` per `playerId` aislado.

### V5 — Responsive + a11y + X-Correlation-Id (FR-011/012, SC-007/008)

1. Resize 375px → 1 col `Players/Leaderboard/Current Round` no scroll; 768px → 4 col `gap var(--space-3)` targets ≥44px.
2. Inspect CSS `data-theme="player"` → 0 literales `var(--space-*)`.
3. `axe` → 0 violations `role="list"` `aria-live polite`.
4. `GET /players/me` + `GET /leaderboard` header `X-Correlation-Id` UUID + `Authorization Bearer`; sin JWT → `401` OIDC.

**Expected**: SC-007 responsive 100% axe 0, SC-008 100% Correlation + JWT.

## Test / Lint

```bash
cd src/Player/QuizArena.Player
npm run test -- src/app/stores/player-game.store.spec.ts tests/integration/isolation.spec.ts --watch=false
npm run test -- src/app/features/game/leaderboard.component.spec.ts --watch=false
dotnet test tests/OroQuizClash.Api.Tests -k "PlayerMultiplayer or GetMyPlayerState or GetLeaderboard"
dotnet test tests/OroQuizClash.Architecture.Tests -k PlayerMultiplayer
```

## Cleanup

```bash
aspire stop
```
