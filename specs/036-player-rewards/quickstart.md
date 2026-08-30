# Quickstart: Player Rewards (036)

**Branch**: `036-player-rewards` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

Validation guide — `Points Wallet` → `Rewards Catalog` `Available/Required/Remaining/Reward Status` → `Reward Detail` → `Redeem` 2 pasos `X-Idempotency-Key` per `rewardId` → `Confirmation` + `Redemption History` + `Consolation`.

## Prerequisites

- .NET 10 SDK, Node 22 LTS, Angular CLI 22, `podman` para OroIdentityServer.
- `dotnet workload install aspire`, `podman build -f src/IdentityServer/IdentityServer/Dockerfile -t oroidentityserver:latest .`
- Secrets: `export symmetric_security_key="$(openssl rand -base64 32)"` `export seed_admin_password="Admin@123456"`.
- Rewards seed: via `OroQuizClash.AppHost` o `POST /api/rewards` admin `Reward.Create("Pack Oro", "...", 800, 10)` + `Activate`.
- Player SPA ya con SPEC-027/028/029/032/035: `QuizArena.Player` `PlayerRewardsStore` `RewardsApi` + `RewardsCatalog/Detail/History` + `design-system/tokens` `data-theme="player"`.

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

# Seed rewards (once, via admin JWT)
curl -X POST http://localhost:5000/api/rewards -H "Authorization: Bearer <admin-jwt>" -H "Content-Type: application/json" -d '{"name":"Pack Oro","description":"Skin premium","pointsRequired":800,"stock":10}'
curl -X POST http://localhost:5000/api/rewards -H "Authorization: Bearer <admin-jwt>" -d '{"name":"Poción Extra","description":"Vida +1","pointsRequired":1500,"stock":5}'
# Activate via POST /api/rewards/{id}/activate if needed
```

## Run

```bash
cd src/Player/QuizArena.Player
npm start # http://localhost:4200
# Login as player → Create game MaxPlayers 4 MaxRounds 5 → Join → jugar rondas para obtener puntos → /rewards (033/032 scoring ya)
```

## Validation Scenarios

### V1 — Wallet + Catalog 4 métricas (US1, FR-001/002, SC-001)

1. Crear game `PointsPerRound=100`, jugar 12 respuestas correctas → `Available 1200`.
2. Abrir `/rewards?gameId={gameId}` → verify `Available Points 1200` header `aria-live polite` coincidente con `GET /rewards?gameId` `availablePoints` y `GET /players/me` `score.totalPoints` (0% cliente).
3. Verify catalog cards:
   - `Pack Oro Required 800` `Reward Status Canjeable` `Remaining Quedan 400 pts` si 1200>=800.
   - `Poción Extra Required 1500` `Puntos insuficientes` `Te faltan 300 pts` si 1200<1500.
4. Probar con jugador 0 pts → todas `Puntos insuficientes`.

**Expected**: SC-001 100% 4 métricas ledger 0% cliente.

### V2 — Reward Detail + Redeem 2 pasos (US2, FR-003/004/005, SC-002/004)

1. Con `Available 1200`, abrir `/rewards/{packOroId}` → verify `Available 1200` `Required 800` `Remaining 400` `Reward Status Canjeable` `Canjear` habilitado 44px.
2. Con `Available 800 Required 1500` → verify `Reward Status Puntos insuficientes` `Te faltan 700` `Canjear` deshabilitado con mensaje.
3. Con canjeable, pulsar `Canjear` → verify diálogo `role="dialog"` `aria-modal` `Confirmar canje` con resumen `Required 800 Remaining 400` + warning `¿Confirmar canje de Pack Oro por 800 puntos?` `role="alert"` `aria-live assertive`.
4. Pulsar `Cancelar` → verify cierra sin `POST /rewards/{id}/redeem`; `Escape` → cierra; backdrop → cierra.
5. Pulsar `Confirmar` (44px `aria-label="Confirmar canje"`) → verify `POST /api/rewards/{id}/redeem` con `X-Idempotency-Key` UUID per `rewardId` `sessionStorage` `idemp-redeem-{rewardId}` + `Authorization Bearer` + `X-Correlation-Id` + body `{gameId, idempotencyKey}`.

**Expected**: SC-002 flujo <90s, SC-004 0% canjes accidentales 2 pasos.

### V3 — Confirmation + ledger idempotente (US2, FR-006/007/009/011, SC-003/005)

1. Tras `Confirmar` con `Available 1200 Required 800`, verify `200 OK` `REQUESTED` `redemptionId` + `Confirmation` `¡Canje realizado!` `Consumidos 800` `Restantes 400` `Referencia` + `Available` actualizado 400 tras `hydrate` `GET /rewards?gameId`.
2. Reintentar `POST /rewards/{id}/redeem` misma `X-Idempotency-Key` → verify mismo `redemptionId` `REQUESTED` sin nuevo `PointTransaction` `COUNT` ni nuevo `RewardRedemption` (idempotente).
3. Intentar canje con saldo manipulado DevTools (editar `Available` a 9999 en cliente) → backend responde `409 InsufficientPoints` si real available 400 < 800, UI refresca `Available 400` autoritativo.
4. Intentar canje `Poción Extra 1500` con `Available 800` → `409 InsufficientPoints` con `ErrorState` `CorrelationId` + `Retry` reusa misma key; `Agotada` (Stock 0) → `409 RewardUnavailable`.

**Expected**: SC-003 100% backend, SC-005 100% idempotente sin duplicar.

### V4 — Redemption History (US3, FR-008, SC-006)

1. Con jugador con 3 canjes, abrir `/rewards/history` → verify lista orden `RequestedAt` desc con cada fila `Pack Oro` `800 pts` `Canjeada` `RequestedAt` `reference`, paginada.
2. Con jugador sin canjes → verify `empty-state` "Aún no has canjeado recompensas" + CTA `Explorar recompensas` → `/rewards`.

**Expected**: SC-006 100% correcto + vacío accionable.

### V5 — Consolation Reward (US4, FR-010, SC-007)

1. Finalizar partida con `ConsolationPolicy=RewardBased` y puntaje bajo umbral estándar pero elegible consolación → backend crea `RewardRedemption.CreateAsConsolation` `APPROVED` `points 0` + `CONSOLATION` ledger si `FixedPoints`.
2. Verify en `/rewards?gameId` `Available` actualizado si crédito + en `/rewards/history` fila con `Consolation` badge `background:var(--color-info)` `isConsolation` + `eligibilityReason`.
3. Verify no aparece en catalog como `Canjeable` y no descuenta stock si consolación no stock-based.

**Expected**: SC-007 100% acreditada diferenciado.

### V6 — Responsive + a11y premium (FR-012/013, SC-008)

1. Resize 375px → catalog 1 col + detail stacked `gap var(--space-3)` targets ≥44px no scroll; 768px → 2 col; 1536px → 4 col.
2. Inspect CSS `data-theme="player"` → 0 literales `var(--space-*) var(--color-*)`.
3. `axe` → 0 violations `role="dialog"` `aria-modal` `aria-label` "Confirmar canje", warnings `role="alert"` `aria-live assertive`, foco `outline:2px`.
4. `prefers-reduced-motion: reduce` → `animation:none`.

**Expected**: responsive 100% axe 0.

### V7 — X-Correlation-Id + JWT (FR-012/013, I/H)

1. `POST /redeem` header `X-Correlation-Id` UUID + `Authorization Bearer`; sin JWT → `401` redirect OIDC; con `must_change_password` → gating.
2. `ErrorState` muestra `CorrelationId/TraceId` + `Retry` reusa misma `X-Idempotency-Key` per `rewardId`.

**Expected**: 100% Correlation + JWT.

## Test / Lint

```bash
cd src/Player/QuizArena.Player
npm run test -- src/app/features/rewards/rewards-catalog.component.spec.ts src/app/stores/player-rewards.store.spec.ts --watch=false
dotnet test tests/OroQuizClash.Application.Tests -k RedeemReward
dotnet test tests/OroQuizClash.Api.Tests -k Rewards
dotnet test tests/OroQuizClash.Architecture.Tests -k Rewards
```

## Cleanup

```bash
aspire stop
```

