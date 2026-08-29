# Quickstart: Player Game (029)

**Branch**: `029-player-game` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)
Validation guide — 10-element cinematic screen.

## Prerequisites

- .NET 10 SDK, Node 22 LTS, Angular CLI 22, `podman` for OroIdentityServer.
- `dotnet workload install aspire`, `podman build -f src/IdentityServer/IdentityServer/Dockerfile -t oroidentityserver:latest .`
- Secrets: `export symmetric_security_key="$(openssl rand -base64 32)"` `export seed_admin_password="Admin@123456"`.
- Player SPA via SPEC-027 already: `src/Player/QuizArena.Player` with `PlayerGameStore` 10 elementos, `proxy.conf.json` `/api`→5000.

## Setup

```bash
aspire start
# wait: sqlserver, postgres, redis, rabbitmq, identity-api 5080/5086, oroclash-api 5000, quizarena-player 4200

cd src/Player/QuizArena.Player
npm install
cp src/environments/environment.example.ts src/environments/environment.ts
# apiUrl=http://localhost:5000/api, identityAuthority=http://localhost:5080, gameHubUrl=http://localhost:5000/hubs/game

# Register quizarena-player public PKCE (once) via Admin UI http://localhost:5080
# clientId=quizarena-player, public, PKCE, redirectUris=http://localhost:4200/auth/callback, scopes openid profile email offline_access api
```

## Run

```bash
cd src/Player/QuizArena.Player
npm start # http://localhost:4200
# Login as player → Create/Join game via lobby (028) → /player/game/:gameId
```

## Validation Scenarios

### V1 — 10 elementos cinematic (US1, FR-001..008, SC-001)

1. Create game `WAITING→IN_PROGRESS` with `MaxRounds 10`, join as `playerA`, `StartRound` `Question` 4 opts, `Round 3` `Level Intermediate`, `Score 250` `Secured 100` `Potential Pack Oro`, `Timer RUNNING 12s remaining`, `Status ACTIVE`.
2. Open `/player/game/:id` → verify 10 elements visible: Current Round "Ronda 3/10", Current Level Intermediate, Question text, Four Answers A-D, Timer 12s `aria-live`, Current Score 250, Secured 100, Potential Pack Oro, Player Status ACTIVE, Withdrawal Action button. Check network `GET /players/me` 200 `timer.serverNow` and `X-Correlation-Id` header.
3. Verify no `isCorrect` leak before EVALUATED, 4 options exactly, `data-theme="player"` tokens premium.

**Expected**: SC-001 100% 10 elementos, 8 campos correctos, `data-theme` cinematic.

### V2 — Answer + Level/Potential progression (US2, SC-003)

1. Select option `o2` → `Submit` → `POST /answers` with `X-Idempotency-Key` → 200 `EVALUATED isCorrect true` → `Current Score 250→350` `Secured` per `KEEP_SECURED_SCORE` `Potential` next reward `Level Intermediate→Advanced` in <1s.
2. Retry same `X-Idempotency-Key` → same 200 no duplicate `PointTransaction` ledger `COUNT` unchanged.
3. Try re-send when `canAnswer=false` → local disabled no fetch.

### V3 — Timer & Status (US3, SC-004)

1. Observe Timer 30→0 decrement 1/s no jumps >1s for 30s; warning color <10s.
2. Wait `EXPIRED` without send → status `EXPIRED` `aria-live="assertive"` blocked; send → `400 AnswerWindowExpired` with `CorrelationId`.
3. While `ACTIVE` disconnect and `WITHDRAWN` → status terminal `WITHDRAWN` after `POST /withdraw`.

### V4 — Withdrawal idempotente (US4, SC-006)

1. Click Withdrawal Action → confirm modal → `POST /withdraw` → `WITHDRAWN` `canAnswer=false` other players `ACTIVE`; second withdraw same key → same 200 no new ledger.
2. Try withdraw with `sub` mismatch → `403 PlayerIdentityMismatch` audited.

### V5 — Responsive & A11y (SC-008) + Cinematic 80% (SC-007)

1. Resize 375px → Header stacked, Answers 1 col, Footer stacked, no horizontal scroll, targets ≥44px.
2. `axe` / Lighthouse → 0 violations AA contrast, focus visible, `aria-live` Timer/Score `polite` `EXPIRED` `assertive`, keyboard `Tab/Space/Enter` answers.
3. Qualitative: 80% test users rate Cinematic/Immersive/Premium/Competitive via tokens.

### V6 — Security & Observability (SC-009)

1. `curl` `/players/me` without Bearer → 401 redirect OIDC.
2. Each request has `X-Correlation-Id` UUID; trigger 400 `AnswerWindowExpired` → `ErrorState` shows `CorrelationId/TraceId`.

## Test / Lint

```bash
cd src/Player/QuizArena.Player
npm test -- --watch=false # game.store 10 elem + remainingSeconds + radiogroup + timer + withdraw
npm run lint
dotnet test tests/OroQuizClash.Architecture.Tests -k PlayerGame
```

## Cleanup

```bash
aspire stop
```
