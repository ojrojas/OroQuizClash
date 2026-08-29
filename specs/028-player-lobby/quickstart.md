# Quickstart: Player Lobby (028)

**Branch**: `028-player-lobby` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)
Validation guide — runnable scenarios proving lobby works end-to-end.

## Prerequisites

- .NET 10 SDK 10.0.400 (`global.json`), Node 22 LTS, Angular CLI 22, `podman`/`docker` for OroIdentityServer.
- Repo root `OroQuizClash`. Aspire: `dotnet workload install aspire`.
- OroIdentityServer image: `podman build -f src/IdentityServer/IdentityServer/Dockerfile -t oroidentityserver:latest .`
- Secrets: `export symmetric_security_key="$(openssl rand -base64 32)"` `export seed_admin_password="Admin@123456"`.
- Player SPA already via SPEC-027: `src/Player/QuizArena.Player` with `angular-auth-oidc-client` PKCE, `proxy.conf.json` `/api`→5000.

## Setup

```bash
# 1. Infra + identity + API + player
aspire start
# wait: sqlserver, postgres, redis, rabbitmq, identity-api 5080/5086, oroclash-api 5000, quizarena-player 4200

# 2. Player SPA (if not via Aspire container)
cd src/Player/QuizArena.Player
npm install
cp src/environments/environment.example.ts src/environments/environment.ts
# environment.apiUrl=http://localhost:5000/api, identityAuthority=http://localhost:5080

# 3. Register quizarena-player OIDC public PKCE (once)
# Admin UI http://localhost:5080 (admin/Admin@123456) → Applications → Create
# clientId=quizarena-player, public, PKCE, redirectUris=http://localhost:4200/auth/callback, scopes openid profile email offline_access api

# 4. Seed games (via Admin or API as GAME_MANAGER)
# Create 3 WAITING_FOR_PLAYERS + 2 IN_PROGRESS/FINISHED for SC-001
TOKEN_MGR=$(curl -s -X POST http://localhost:5080/connect/token -d "grant_type=password&username=manager&password=Manager@123456&scope=openid profile email api" -u "quizarena-player:" | jq -r .access_token)
for i in 1 2 3; do
  curl -X POST http://localhost:5000/api/games -H "Authorization: Bearer $TOKEN_MGR" -H "Content-Type: application/json" -d "{\"name\":\"Lobby-Test-$i\",\"categoryId\":\"$CAT_ID\",\"minRounds\":5,\"maxRounds\":10,\"difficulty\":3}"
done
# Move 2 to IN_PROGRESS/FINISHED via StartGame etc. if needed
```

## Run

```bash
cd src/Player/QuizArena.Player
npm start # http://localhost:4200 (or Aspire http://localhost:4200 via container)
# Login as player (OIDC redirect) → /player/lobby
```

## Validation Scenarios

### V1 — Discover Available Games (US1, FR-001/FR-002, SC-001/SC-002)

1. Login `playerA` → `/player/lobby` → verify list shows 3 Available Games with 8 columns (Game Name, Category, Difficulty, Number of Rounds "5-10", Players "0/10", Start Time local, Prize "—" or name, Status WAITING_FOR_PLAYERS) ordered `CreatedAt desc`.
2. Verify 2 games in `IN_PROGRESS/FINISHED` not shown. Check network `GET /api/games?status=WAITING_FOR_PLAYERS&page=1&pageSize=20` 200 with `totalCount=3`.
3. Empty: update all to `FINISHED` → refresh lobby → `EmptyState "No hay partidas disponibles"` + Refresh CTA.
4. Paginate: seed 25 games → verify `pageSize 20` shows 20, paginator totalPages 2, page 2 shows 5.

**Expected**: 100% Available filter SC-001, 8 campos SC-002, paginación intacta.

### V2 — Join Game idempotente (US2, FR-004/005, SC-003/SC-004/SC-005)

1. Click `Join Game` on `Lobby-Test-1` → network `POST /api/games/{id}/players` with `X-Idempotency-Key` + `Authorization: Bearer` → 200 `GameSession ACTIVE` → redirect `/player/game/{id}` Players 0→1.
2. Double-click quickly → same `X-Idempotency-Key` (sessionStorage) → second 200 same `GameSession` no duplicate `GamePlayer` (verify DB `SELECT COUNT(*) FROM GamePlayers WHERE GameId=g1 AND UserId=sub` =1).
3. In second browser login `playerB` Join same `g1` → both succeed, Players 1→2, each `GameSession` isolated (scores 0).
4. Fill `g1` to `MaxPlayers` (10) → next Join → `409 GameFull` ProblemDetails with `CorrelationId` + friendly message, no `GameSession` created.
5. Change `g1` to `IN_PROGRESS` between list and Join → `400 GameNotWaitingForPlayers` → suggest Refresh.

**Expected**: Join <1s SC-003, idempotent 100% SC-004, rejection 100% SC-005.

### V3 — View Game Information (US3, FR-003, SC-007)

1. Click `View` on any row → modal/detail shows 8 campos + extended (`TimeLimit 30s, Points 100, Withdrawal KEEP_CURRENT_SCORE`) matching `GET /api/games/{id}` JSON.
2. While detail open, change game via Admin to `IN_PROGRESS` → close/reopen detail → Status updated (server truth, no stale).
3. Manipulate URL `/player/lobby/00000000-0000-0000-0000-000000000000` → `404 GameNotFound` `ErrorState` with `CorrelationId` Retry.

### V4 — Leave Lobby (US4, FR-007, SC-006)

1. From lobby (not joined) click `Leave Lobby` → navigated to `/` without any `POST` to `/players` (verify Network tab no write). Session OIDC preserved.
2. After joining `g1` then back to lobby, click `Leave Lobby` → still Active in `g1` (verify `GET /api/games/g1/players/me` status ACTIVE, not Withdrawn).

### V5 — Security & Observability (FR-009/014, SC-009)

1. `curl` `GET /api/games?status=WAITING_FOR_PLAYERS` without Bearer → 401 redirect OIDC.
2. Inspect lobby requests → each has `X-Correlation-Id` UUID; trigger `GameFull` error → `ErrorState` shows `CorrelationId/TraceId`.
3. Try `POST /players` with `playerId` in body different from `sub` → 403 `PlayerIdentityMismatch`.

### V6 — A11y & Responsive (FR-012, SC-008)

1. `axe` / Lighthouse on `/player/lobby` → 0 violations AA, contrast, focus visible.
2. Resize 375px → cards stacked 8 fields visible, no horizontal scroll, buttons ≥44px. `Tab`→ `Enter` joins, `aria-live` announces list.

## Test / Lint

```bash
cd src/Player/QuizArena.Player
npm test -- --watch=false # lobby store + join idempotence
npm run lint # @ngrx/eslint-plugin
dotnet test tests/OroQuizClash.Architecture.Tests -k Lobby
dotnet test tests/OroQuizClash.Api.Tests --filter GetGames
```

## Known snippet: proxy.conf.json

```json
{
  "/api": { "target": "http://localhost:5000", "secure": false, "changeOrigin": true },
  "/hubs": { "target": "http://localhost:5000", "secure": false, "ws": true, "changeOrigin": true }
}
```

## Cleanup

```bash
aspire stop
```
