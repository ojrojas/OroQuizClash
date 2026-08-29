# Quickstart: Player Answering (031)

**Branch**: `031-player-answering` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

Validation guide — 4 opciones con 8 estados (`Idle→Hover→Selected→Locked→Evaluating→Correct/Incorrect/Timeout`), single selection inmutable, veredicto backend.

## Prerequisites

- .NET 10 SDK, Node 22 LTS, Angular CLI 22, `podman` para OroIdentityServer.
- `dotnet workload install aspire`, `podman build -f src/IdentityServer/IdentityServer/Dockerfile -t oroidentityserver:latest .`
- Secrets: `export symmetric_security_key="$(openssl rand -base64 32)"` `export seed_admin_password="Admin@123456"`.
- Player SPA ya con SPEC-027/029/030: `QuizArena.Player` `PlayerGameStore`/`PlayerRoundsStore` 10 elementos + ladder + `GamesApi.submitAnswer` + `GameRealtimeService` `withAutomaticReconnect` `proxy.conf.json` `/api`→5000.
- Design System 016 ya en `angular.json` styles `design-system/tokens/design-tokens.css` `data-theme="player"`.

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
# Login as player → lobby (028) Create/Join game MaxRounds 10 → /player/game/:gameId (029/030) con pregunta 4 opciones
```

## Validation Scenarios

### V1 — 4 opciones Idle/Hover sin leak (US1, FR-001/002/010-013, SC-001/002)

1. Start game `Waiting→IN_PROGRESS`, `StartRound` con `Question` publicada 4 opciones A-D `text` distinto `displayOrder 0..3`, 1 correcta server (e.g. B).
2. Open `/player/game/:id` → verify `role="radiogroup"` `aria-label="Opciones de respuesta"` con 4 `role="radio"` `aria-checked="false"` `aria-posinset 1..4` `aria-setsize 4` orden A-D, estado `Idle`, `data-theme="player"`, hover mueve a `Hover` `border var(--color-primary)` `scale 1.01` (check `prefers-reduced-motion` reduce none).
3. Inspect `GET /players/me` `question.answerOptions` → sin campo `isCorrect` (0% leak). Question corrupta 3 opciones → `ErrorState` "Pregunta inválida (se requieren 4 opciones)" con `CorrelationId` y selector deshabilitado.
4. Screen reader Tab → cada opción anuncia "Opción X, texto, sin seleccionar, 1 de 4".

**Expected**: SC-001 100% exactamente 4 sin leak, SC-002 `Idle/Hover` tokens correctos 100%.

### V2 — Single Selected → Locked inmutable (US2, FR-003/004, SC-003/004)

1. Click opción B → verify `Selected` `aria-checked="true"` solo B (A,C,D `false`), check premium `var(--color-primary)`; click otra C antes de lock → `Selected` mueve B→C único 100% single.
2. Click `Confirmar` (44px) sin selección → mensaje "Selecciona una opción" `role="alert"` sin llamada.
3. Con B `Selected` click `Confirmar` → `Locked` `aria-disabled true` en otras, B `disabled` `isLocked true`, intentar seleccionar C → ignorado local (no cambia `Selected`); recarga + `hydrate` `GET /players/me` → mismo `Locked` B desde servidor; reintento `POST /answers` con otra opción → `409 QuestionAlreadyAnswered` sin nuevo ledger `COUNT`.

**Expected**: SC-003 single única 100%, SC-004 Locked inmutable 100% 0% modificación + 409.

### V3 — Evaluating → Correct/Incorrect/Timeout backend (US3, FR-005..009, SC-005/007/006)

1. Con B `Locked` dentro `TimeLimit` (`Timer RUNNING 12s`), `POST /api/games/{id}/answers` con `selectedOptionId B` + `X-Idempotency-Key` UUID per `roundId` `sessionStorage idemp-{roundId}` → verify `Evaluating` `aria-busy true` spinner `Evaluating…` `aria-live="polite"`, botón Confirmar deshabilitado, otras `aria-disabled`.
2. Backend `EVALUATED isCorrect true` (B correcta) → `Correct` `var(--color-success)` check animado <300ms `aria-live="assertive"` "¡Correcto! +100 pts" `score` incrementado ledger; `isCorrect false` → `Incorrect` `var(--color-error)` cross + correcta secondary resalta verde (B `Incorrect` rojo + correcta `Correct` secondary).
3. Fuera ventana (`Timer EXPIRED` `submittedAt > expiresAt`) → `Timeout` `var(--color-warning)` "Tiempo agotado" `aria-live="assertive"` sin `Correct`; `canAnswer false` bloquea re-envío.
4. Doble `POST /answers` misma `X-Idempotency-Key` concurrente → ambos `200` mismo `answerId` sin duplicar `PointTransaction` ledger `COUNT`.

**Expected**: SC-005 100% veredicto backend, `Evaluating` hasta servidor 100%; SC-007 95% <1s `Correct/Incorrect`, 100% `Timeout` fuera ventana; SC-006 idempotencia 100%.

### V4 — ErrorState + Retry idempotente (FR-014/009, SC-006)

1. Kill `oroclash-api` durante `Evaluating` → `500` con `X-Correlation-Id` → permanece `Evaluating` 3s → `ErrorState` con `detail` + `CorrelationId/TraceId` + `Retry`; `Retry` reusa misma `X-Idempotency-Key` sin duplicar ledger (verify `COUNT` ledger unchanged).
2. `409 QuestionAlreadyAnswered` tras `Locked` → satura a `Locked` sin nuevo ledger, no 500.

### V5 — Responsive + a11y premium (US4, FR-011/012/010, SC-008/009/010)

1. Resize 375px → 1 col `gap var(--space-3)` targets ≥44px no scroll horizontal; 768px → 2x2 grid 1fr 1fr; 1280/1536 → sin scroll, gap tokens.
2. Inspect CSS `data-theme="player"` → 0 literales `var(--space-*) var(--color-*) var(--radius-*)` usa `design-system/tokens`.
3. `axe` / Lighthouse → 0 violations: `radiogroup` `aria-checked` `aria-posinset/setsize` `aria-disabled` `aria-live` `aria-busy` foco `outline:2px solid var(--color-primary)` contraste AA.
4. `prefers-reduced-motion: reduce` → hover/selected/evaluating sin scale/pulse; keyboard `Tab/Shift+Tab` + `Space/Enter` selecciona 100% funcional.
5. `POST /answers` header `X-Correlation-Id` UUID + `Authorization Bearer`; sin JWT → `401` redirect OIDC.

**Expected**: SC-008 responsive 100% 375-1536, SC-009 WCAG AA 100% axe 0, SC-010 100% `X-Correlation-Id` + JWT required.

## Test / Lint

```bash
cd src/Player/QuizArena.Player
npm test -- --watch=false # question.component 4 opciones 8 estados, selected single locked inmutable, evaluating correct/incorrect/timeout, debounce, a11y axe, answer-interaction.store
npm run lint
dotnet test tests/OroQuizClash.Api.Tests -k SubmitAnswer
dotnet test tests/OroQuizClash.Architecture.Tests -k PlayerAnswering
```

## Cleanup

```bash
aspire stop
```
