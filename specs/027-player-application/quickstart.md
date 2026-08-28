# Quickstart: Player Application (027)

**Branch**: `027-player-application` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

Validation guide — runnable scenarios proving the feature works end-to-end. No full implementation bodies; references to contracts/data-model.

## Prerequisites

- .NET 10 SDK 10.0.400 (`global.json`), Node 22 LTS, Angular CLI 22 (`npm i -g @angular/cli@22`), `podman` o `docker` para OroIdentityServer.
- Repo root: `OroQuizClash`. Aspire workload: `dotnet workload install aspire`.
- OroIdentityServer image: `podman build -f src/IdentityServer/IdentityServer/Dockerfile -t oroidentityserver:latest .`
- Secrets: `export symmetric_security_key="$(openssl rand -base64 32)"` y `export seed_admin_password="Admin@123456"` (solo local).

## Setup

```bash
# 1. Infra + identity + API
aspire start
# espera: sqlserver, postgres (identitydb), redis, rabbitmq, identity-api (5080/5086), oroclash-api

# 2. Player SPA
cd src/Player/QuizArena.Player
npm install
npm install @ngrx/signals @ngrx/signals/entities @ngrx/signals/rxjs-interop
# opcional lint signalstore
npm install @ngrx/eslint-plugin --save-dev

# 3. Env
cp src/environments/environment.example.ts src/environments/environment.ts
# editar environment.ts:
#   apiUrl: 'http://localhost:5000/api' (oroclash-api http endpoint de Aspire)
#   identityAuthority: 'http://localhost:5080' (identity-api http)
#   gameHubUrl: 'http://localhost:5000/hubs/game'

# 4. (Si BFF alternative) host
# dotnet build src/Player/QuizArena.Player.Host/QuizArena.Player.Host.csproj

# 5. Registrar cliente OIDC quizarena-player en OroIdentityServer
# vía Admin UI http://localhost:5080 (login admin/Admin@123456) → Applications → Create:
#   clientId=quizarena-player, public, PKCE, redirectUris=http://localhost:4200/auth/callback, scopes=openid profile email offline_access api
# o vía API: curl -H "Authorization: Bearer <admin_token>" -X POST http://localhost:5080/api/applications -d @player-client.json
```

## Run

```bash
# Terminal A: API + identity ya via aspire start
# Terminal B: Player SPA
cd src/Player/QuizArena.Player
npm start
# → http://localhost:4200 (proxy /api → oroclash-api si proxy.conf.json)

# (BFF alternative)
# dotnet run --project src/Player/QuizArena.Player.Host --urls http://localhost:4201
```

## Validation Scenarios

### V1 — Contexto privado aislado (US1, FR-001..FR-003, SC-001)

1. Login como `playerA` (usuario creado en identity-api) → redirect OIDC → `/lobby`.
2. Crear juego via API (o Admin) en `WAITING_FOR_PLAYERS` (`gameId=g1`).
3. En navegador A: `Join Game g1` → verificar SignalStore `player`=`playerA`, `gameSession.status=ACTIVE`, `score=0`, `securedPoints=0`, `timer=STOPPED`.
4. En navegador B (perfil distinto o incógnito) login `playerB` → `Join g1` → verificar `playerB` ve su propio `score`/`status` (no los de A).
5. En A seleccionar opción `o-1` (no enviar) → verificar B sigue con `answer.state=PENDING` sin ver `o-1`.
6. Reload A → verificar rehidratación `GET /api/games/g1/players/me` restaura 10 elementos sin afectar B.

**Expected**: `hydrated state` coincide 100% con `oroclash-api` ledger; ningún campo de A visible/mutable desde B.

### V2 — Simultaneidad (US2, FR-004/005, SC-002)

1. `POST /api/games/g1/start` (admin) → `RoundStarted` + `QuestionAvailable` a A y B.
2. En A y B enviar respuesta simultáneamente (dentro de `timeLimitSeconds`) con `selectedOptionId` distinto → verificar ambos `200 EVALUATED` con `isCorrect` independiente, `ScoreUpdated` por `playerId` filtrado, `remainingSeconds` diverge <1s (SC-004).
3. Inspeccionar `PointTransaction` en API → `totalPoints` reconstruible.

### V3 — Ciclo de vida (US3, FR-006..008)

1. Unirse → `GameSession ACTIVE` → esperar `IN_PROGRESS` → 5 rondas (`RoundStarted`→`QuestionAvailable`→answer→`ScoreUpdated`→`RoundCompleted` loop) → `GameFinished`.
2. En ronda 3 ejecutar `Withdraw` → verificar `playerStatus=WITHDRAWN`, `canAnswer=false`, siguiente `SubmitAnswer` → `403 PlayerNotActive`.
3. Verificar `CurrentRound` congelado tras terminal.

### V4 — Timer autoritativo + Secured Points (US4, FR-012/013, SC-004/005)

1. Ronda con `timeLimitSeconds=30`, `expiresAt` server. Observar countdown `remainingSeconds` va 30→0 sin saltos >1s (corrección cada 10s contra `serverNow`).
2. Enviar a `remaining≈5s` → 200 si `submittedAt <= expiresAt`, `400 AnswerWindowExpired` si tarde (server decide).
3. Alcanzar checkpoint ronda 5 → `SecuredPoints` salta `0→200`; responder mal ronda 6 → `totalPoints` cae a `securedPoints` según `FALLBACK_TO_CHECKPOINT`.

### V5 — Resiliencia y rehidratación (US5, FR-017/018, SC-007)

1. Durante `ROUND_IN_PROGRESS` desconectar WiFi 10s (o `connection.stop()` en devtools) → `withAutomaticReconnect` → `Reconnected` → `hydrate()` → `Timer` corregido, `Answer` no duplicada, `Score` consistente.
2. Dejar expirar `access_token` → `silentRenew`/`refresh_token` recupera sin logout; forzar `revoke` → redirect a `identity-api /connect/authorize`.
3. `must_change_password` user → visita `/game/g1` → `MustChangePasswordGuard` redirect a `identity-api /auth/change-password`.

### V6 — Seguridad (FR-014/015, SC-003)

1. Como `playerA`, `POST /api/games/g1/answers` con `playerId=playerB` → `403` auditado, estado B intacto.
2. Request con `totalPoints` manipulado en body → ignorado, server recalcula.
3. `curl` sin `Authorization` → `401`.

### V7 — Accesibilidad (FR-020/021, SC-008)

1. Lighthouse / axe: `aria-live` en Timer/Score, contraste AA, foco visible, 44px targets, 375–1536 sin scroll horizontal.
2. Teclado: `Tab` → opciones, `Space/Enter` selecciona, `Enter` envía.

## Test / Lint

```bash
cd src/Player/QuizArena.Player
npm test -- --watch=false                # Vitest: stores + components
npm run lint                             # @ngrx/eslint-plugin si instalado
# Backend no afectado salvo GetMyPlayerState slice
dotnet test tests/OroQuizClash.Architecture.Tests -k Player
```

## Known snippet: proxy.conf.json (dev)

```json
{
  "/api": { "target": "http://localhost:5000", "secure": false, "changeOrigin": true },
  "/hubs": { "target": "http://localhost:5000", "secure": false, "ws": true, "changeOrigin": true }
}
```

## Cleanup

```bash
aspire stop
# o podman compose down
```
