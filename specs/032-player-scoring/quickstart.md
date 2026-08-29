# Quickstart: Player Scoring (032)

**Branch**: `032-player-scoring` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

Validation guide — 5 métricas autoritativas (`Current/Secured/Potential/Round/Total Points`) actualizadas vía `ScoreUpdated → hydrate`.

## Prerequisites

- .NET 10 SDK, Node 22 LTS, Angular CLI 22, `podman` para OroIdentityServer.
- `dotnet workload install aspire`, `podman build -f src/IdentityServer/IdentityServer/Dockerfile -t oroidentityserver:latest .`
- Secrets: `export symmetric_security_key="$(openssl rand -base64 32)"` `export seed_admin_password="Admin@123456"`.
- Player SPA ya con SPEC-027/029/030/031: `QuizArena.Player` `PlayerGameStore` 10 elementos + `ScorePanelComponent` + `GamesApi.getMyState` + `GameRealtimeService` `withAutomaticReconnect` `proxy.conf.json` `/api`→5000.
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
```

## Run

```bash
cd src/Player/QuizArena.Player
npm start # http://localhost:4200
# Login as player → lobby (028) Create/Join game MaxRounds 10 → /player/game/:gameId (029/030/031) con ScorePanel 5 métricas
```

## Validation Scenarios

### V1 — 5 métricas autoritativas (US1, FR-001/002, SC-001)

1. Create game con `PointsPerRound=100`, `RewardRules=[Pack Oro 500]`, Start → Join player → `StartRound` con `Question` 4 opciones, `AwardPoints` 100 via `AnswerEvaluated` correcta.
2. Open `/player/game/:id` → verify `Current Points` 100, `Secured Points` 0, `Potential Points` "100 pts" o "Próximo: Pack Oro 500 pts", `Round Points` 100, `Total Points` 100 coinciden con `GET /players/me` `score.totalPoints` `securedPoints.securedPoints` `roundPoints` (Server Truth V, 0% cálculo cliente).
3. Inspect `GET /players/me` `score.totalPoints == sum(PointTransaction)` ledger; `Total Points` no calculado cliente.

**Expected**: SC-001 100% 5 métricas coinciden con ledger 0% cliente.

### V2 — Realtime ScoreUpdated → hydrate <1s (US2, FR-003, SC-002)

1. Con `Score` 100 `Round 100`, enviar `SubmitAnswer` correcta desde otro dispositivo → servidor `ScoreUpdated` + `ANSWER_CORRECT +100`.
2. Verify UI `Current Points` 200 `Round Points` 200 `Total Points` 200 en <1s sin recarga, solo vía `hydrate` `GET /players/me` (no payload del evento), animación `pulse` 600ms.
3. `RoundCompleted` → verify `Secured Points` 200 `checkpoint 1` y `Round Points` 0 tras `hydrate`.
4. Disconnect SignalR (kill `oroclash-api` 2s) → `Reconnected` → verify `hydrate` sincroniza 5 métricas sin acción usuario.

**Expected**: SC-002 100% <1s realtime, SC-003 0% mutación cliente aceptada.

### V3 — Secured checkpoint y Potential "—" (US3, FR-005/006, SC-004/005)

1. Con `Secured 200 checkpoint 3` `Round 80` `LossPolicy=LOSE_UNSECURED_POINTS` → verify `Secured` "200 · checkpoint 3" badge `asegurado`, `Round Points` "80 en juego".
2. Sin `checkpoint` (null) → verify `Secured` solo "200 pts" sin badge.
3. Sin `RewardRules` → verify `Potential Points` "—" `aria-label` "Potential no disponible" sin romper grid 375/768.

**Expected**: SC-004 checkpoint 100%, SC-005 Potential "—" 100%.

### V4 — Responsive + a11y premium (US4, FR-008/009/010, SC-006/007/008)

1. Resize 375px → 1 col `gap var(--space-3)` targets ≥44px no scroll; 768px → 5 col `repeat(5,1fr)`; 1280/1536 → sin scroll.
2. Inspect CSS `data-theme="player"` → 0 literales `var(--space-*) var(--color-*)`.
3. `axe` → 0 violations `role="group"` `aria-live polite` `aria-label` por métrica.
4. `prefers-reduced-motion: reduce` → `pulse` deshabilitado.

**Expected**: SC-006 responsive 100%, SC-007 axe 0, SC-008 reduced-motion 100%.

### V5 — X-Correlation-Id + JWT (FR-004/011, I/H)

1. `GET /players/me` header `X-Correlation-Id` UUID + `Authorization Bearer`; sin JWT → `401` redirect OIDC.
2. `ErrorState` muestra `CorrelationId/TraceId` + `Retry` reusa `hydrate`.

**Expected**: 100% `X-Correlation-Id` + JWT required.

## Test / Lint

```bash
cd src/Player/QuizArena.Player
npm run test -- src/app/features/game/score-panel.component.spec.ts src/app/stores/player-game.store.spec.ts --watch=false
npx ng lint # @ngrx/eslint-plugin
dotnet test tests/OroQuizClash.Api.Tests -k GetMyPlayerState
dotnet test tests/OroQuizClash.Architecture.Tests -k PlayerScoring
```

## Cleanup

```bash
aspire stop
```
