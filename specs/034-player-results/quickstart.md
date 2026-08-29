# Quickstart: Player Results (034)

**Branch**: `034-player-results` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

Validation guide — 4 pantallas finales `YOU WON`/`YOU WALKED AWAY`/`GAME OVER`/`GAME FINISHED` autoritativas `GET /players/me` + `GET /leaderboard`.

## Prerequisites

- .NET 10 SDK, Node 22 LTS, Angular CLI 22, `podman` para OroIdentityServer.
- `dotnet workload install aspire`, `podman build -f src/IdentityServer/IdentityServer/Dockerfile -t oroidentityserver:latest .`
- Secrets: `export symmetric_security_key="$(openssl rand -base64 32)"` `export seed_admin_password="Admin@123456"`.
- Player SPA ya con SPEC-027/029/032/033: `QuizArena.Player` `ResultComponent` placeholder `app-result` `route /player/game/:gameId/result` + `PlayerGameStore` 10 elementos + `GamesApi.getMyState/getLeaderboard` + `GameRealtimeService` `GameFinished` `proxy.conf.json` `/api`→5000.
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
# clientId=quizarena-player, public, PKCE, redirectUris=http://localhost:4200/auth/callback/http://localhost:4200/player/game/:gameId/result, scopes openid profile email offline_access api
```

## Run

```bash
cd src/Player/QuizArena.Player
npm start # http://localhost:4200
# Login as player-a → lobby (028) Create game MaxPlayers 4 MaxRounds 2 → Join as player-a + player-b + player-c + player-d → Start → Play → Finish
```

## Validation Scenarios

### V1 — YOU WON Rank 1 (US1, FR-001/002/003, SC-001)

1. Create game `PointsPerRound=100`, `RewardRules=[Pack Oro 500]`, Start → 4 players A-D → Play 2 rounds donde A gana 850 pts (Rank 1) + `Reward` `Pack Oro`.
2. Open `/player/game/:id/result` como A (JWT `sub=A` `WINNER`) → verify `YOU WON` `Final Score 850 pts` `Prize Pack Oro` `aria-live assertive` confetti, sin `YOU WALKED AWAY`/`GAME OVER`.

**Expected**: SC-001 100% `YOU WON` `Final Score` ledger `Prize` si corresponde.

### V2 — YOU WALKED AWAY Secured + Available Rewards (US2, FR-004/005, SC-002)

1. Con `Player` `WITHDRAWN` `Secured 200 checkpoint 2` `Available Rewards [Pack Plata 300]` (si `PointsPerRound=100` y `Secured 200` no alcanza `Pack Oro 500` pero sí `Pack Plata 300` si `Secured>=300`? Ajustar) → `Secured 300` para test.
2. Open `/player/game/:id/result` como withdrawn player → verify `YOU WALKED AWAY` `Secured Points 200 pts · checkpoint 2` + `Available Rewards` lista `Pack Plata` filtrable `pointsRequired <= Secured`, vacía → "Sin recompensas disponibles".

**Expected**: SC-002 100% `Secured` `checkpoint` + `Available Rewards` filtrable.

### V3 — GAME OVER Consolation (US3, FR-006, SC-003)

1. Con `Player` `ELIMINATED` por `LOSE_ALL` `Final Score 0` `ConsolationPolicy FixedPoints 50` → verify `GAME OVER` `Final Score 0 pts` `Consolation Reward 50 pts` o "Sin consolación" si no aplica.

**Expected**: SC-003 100% `GAME OVER` `Consolation`.

### V4 — GAME FINISHED posición 2..N (US4, FR-007/008, SC-004)

1. Con `Game` `FINISHED` y `Player` `FINISHED` posición 3 `Final Score 400` `Reward Pack Bronce 300` → verify `GAME FINISHED` `Final Position 3 de 4` `aria-label` "Puesto 3 de 4" `Final Score 400 pts` `Reward Pack Bronce` o "Sin recompensa" si no alcanzó.

**Expected**: SC-004 100% `Final Position` `Rank` per `sub`.

### V5 — Redirect si !IsTerminal + a11y + Correlation (FR-011/012 SC-006/007/008)

1. Con `Game` `IN_PROGRESS` `PlayerStatus ACTIVE` → direccionar `/player/game/:id/result` → verify redirect a `/player/game/:id` con "Partida aún en curso".
2. Resize 375px → 1 col no scroll; 768px → sin scroll; `data-theme="player"` 0 literales; `axe` 0 violations `role="status"` `aria-live assertive`.
3. `GET /players/me` + `GET /leaderboard` header `X-Correlation-Id` + `Authorization Bearer`; sin JWT → `401` OIDC.

**Expected**: SC-006 redirect 100%, SC-007 responsive axe 0, SC-008 100% Correlation + JWT, SC-009 `prefers-reduced-motion` reduce.

## Test / Lint

```bash
cd src/Player/QuizArena.Player
npm run test -- src/app/features/result/result.component.spec.ts src/app/stores/player-game.store.spec.ts --watch=false
dotnet test tests/OroQuizClash.Api.Tests -k "Result or GetMyPlayerState or GetLeaderboard"
dotnet test tests/OroQuizClash.Architecture.Tests -k PlayerResults
```

## Cleanup

```bash
aspire stop
```
