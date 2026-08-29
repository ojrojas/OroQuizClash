# Quickstart: Player Withdrawal (035)

**Branch**: `035-player-withdrawal` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

Validation guide — `Withdrawal Action` → diálogo `Current/Secured/Potential` + 2 warnings → `Confirmar` `POST /withdraw` `X-Idempotency-Key` per `gameId` → `PlayerWithdrawn` `WITHDRAWN` `isTerminal`.

## Prerequisites

- .NET 10 SDK, Node 22 LTS, Angular CLI 22, `podman` para OroIdentityServer.
- `dotnet workload install aspire`, `podman build -f src/IdentityServer/IdentityServer/Dockerfile -t oroidentityserver:latest .`
- Secrets: `export symmetric_security_key="$(openssl rand -base64 32)"` `export seed_admin_password="Admin@123456"`.
- Player SPA ya con SPEC-027/029/032/033: `QuizArena.Player` `PlayerGameStore` `withdraw()` `rxMethod` `X-Idempotency-Key` `idemp-withdraw-{gameId}` + `GameComponent` `Withdrawal Action` `showWithdrawConfirm` `role="dialog"` + `ScorePanel` 5 métricas + `GamesApi.withdraw` `getMyState` + `GameRealtimeService` `withAutomaticReconnect` `proxy.conf.json` `/api`→5000.
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
# Login as player → lobby (028) Create game MaxPlayers 4 MaxRounds 5 → Join → /player/game/:gameId (029) con Withdrawal Action
```

## Validation Scenarios

### V1 — Visualizar Current/Secured/Potential antes de retiro (US1, FR-001, SC-001)

1. Create game `PointsPerRound=100`, Start → Join player → `StartRound` con `Question`, `AwardPoints` 100 → `Current 100` `Secured 0` o `Secured 100 checkpoint 1` tras `RoundCompleted` `SecurePoints`.
2. Open `/player/game/:id` → pulsar `Withdrawal Action` (≥44px) → verify diálogo `role="dialog"` `aria-modal` con `Current Points 400 pts` (ej.), `Secured Points 200 pts · checkpoint 2`, `Potential Points 100 pts` o "—" si no configurado, coincidentes con `GET /players/me` `score.totalPoints` `securedPoints.securedPoints` `potentialReward` (Server Truth V).

**Expected**: SC-001 100% 3 métricas ledger 0% cliente.

### V2 — Confirmación con warnings exactos (US2, FR-002/003, SC-002/003)

1. Con diálogo abierto `Secured 200`, verify warning 1: "If you continue and answer incorrectly, you may lose your accumulated points." `role="alert"` `aria-live assertive` y warning 2: "Withdraw now and secure 200 points?" dinámico `Secured` valor.
2. Pulsar `Cancelar` → verify cierra sin `POST /withdraw` y vuelve a `canAnswer=true`; `Escape` → cierra; click `backdrop` → cierra.
3. Pulsar `Confirmar` (≥44px `aria-label="Confirmar retiro"`) → verify `POST /api/games/{id}/withdraw` con `X-Idempotency-Key` UUID per `gameId` `sessionStorage` `idemp-withdraw-{gameId}` + `Authorization Bearer` + `X-Correlation-Id`.

**Expected**: SC-002 warnings exactos 100%, SC-003 confirmación 2 pasos 100% `Cancelar` no envía.

### V3 — PlayerWithdrawn terminal isTerminal (US3, FR-004/005, SC-005/006)

1. Con `Current 400 Secured 200 KEEP_SECURED_SCORE`, confirmar `Withdraw` → verify `200` `WITHDRAWN` `RowVersion++`, `PlayerGameStore.status.isTerminal true` `canAnswer false`, `QuestionComponent` bloqueado `aria-disabled`, `Score` `Current` 200 tras `hydrate` `GET /players/me`.
2. Reintentar `POST /withdraw` misma `X-Idempotency-Key` → verify mismo `GameSession` `WITHDRAWN` sin nuevo `PointTransaction` ledger `COUNT` (idempotente); distinto key pero ya `WITHDRAWN` → `403 PlayerAlreadyWithdrawn`.
3. Intentar `POST /answers` tras `WITHDRAWN` → verify `403 PlayerNotActive` y `Question` `aria-disabled`.
4. Recargar `/player/game/:id` → verify `WITHDRAWN` persistente `hydrate` `GET /players/me` y `Question` bloqueada.

**Expected**: SC-004 idempotente 100% sin duplicar ledger, SC-005 `isTerminal` `canAnswer false` 100%, SC-006 `Current=Secured` 100% `KEEP_SECURED_SCORE`.

### V4 — Responsive + a11y premium (US4, FR-006/007, SC-007)

1. Resize 375px → diálogo 3 métricas + 2 warnings apilados 1 col `gap var(--space-3)` targets ≥44px no scroll; 768px → centrado `max-width:400px` `padding:var(--space-6)` sin scroll.
2. Inspect CSS `data-theme="player"` → 0 literales `var(--space-*) var(--color-*)`.
3. `axe` → 0 violations `role="dialog"` `aria-modal` `aria-label` "Confirmar retiro", warnings `role="alert"` `aria-live assertive`, foco `outline:2px`.
4. `prefers-reduced-motion: reduce` → `scale` deshabilitado.

**Expected**: SC-007 responsive 100% axe 0.

### V5 — X-Correlation-Id + JWT (FR-008/009, I/H)

1. `POST /withdraw` header `X-Correlation-Id` UUID + `Authorization Bearer`; sin JWT → `401` redirect OIDC.
2. `ErrorState` muestra `CorrelationId/TraceId` + `Retry` reusa misma `X-Idempotency-Key` per `gameId`.

**Expected**: 100% Correlation + JWT.

## Test / Lint

```bash
cd src/Player/QuizArena.Player
npm run test -- src/app/features/game/withdrawal.component.spec.ts src/app/stores/player-game.store.spec.ts --watch=false
dotnet test tests/OroQuizClash.Api.Tests -k WithdrawPlayer
dotnet test tests/OroQuizClash.Architecture.Tests -k PlayerWithdrawal
```

## Cleanup

```bash
aspire stop
```
