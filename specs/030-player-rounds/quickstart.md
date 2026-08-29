# Quickstart: Player Rounds (030)

**Branch**: `030-player-rounds` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

Validation guide — ladder vertical Round 1..N con 6 estados (Current/Previous/Current Reward/Next/Secured/Final) y transición sync server.

## Prerequisites

- .NET 10 SDK, Node 22 LTS, Angular CLI 22, `podman` para OroIdentityServer.
- `dotnet workload install aspire`, `podman build -f src/IdentityServer/IdentityServer/Dockerfile -t oroidentityserver:latest .`
- Secrets: `export symmetric_security_key="$(openssl rand -base64 32)"` `export seed_admin_password="Admin@123456"`.
- Player SPA ya con SPEC-027/029: `src/Player/QuizArena.Player` `PlayerGameStore` 10 elementos + `GameRealtimeService` + `proxy.conf.json` `/api`→5000.
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
# Login as player → lobby (028) Create/Join game MaxRounds 10 Linear → /player/game/:gameId (029) con sidebar ladder (030)
```

## Validation Scenarios

### V1 — Ladder N filas + Current/Previous + Difficulty (US1, FR-001..004, SC-001/002/010)

1. Create game `MaxRounds 10` `Linear` `InitialDifficulty 1` (Basic), join `playerA`, `StartRound` → `currentRoundNumber=1` `Level Basic`.
2. Open `/player/game/:id` → verify ladder `role="list"` 10 filas `Round 1`..`Round 10` exacto N, sin duplicados, fila 1 `aria-current="step"` Current Level premium `class="current"` glow, filas 2..10 `upcoming` muted, cada fila muestra `Level` (1-2 Basic, 3-4 Elementary etc.) y `RoundNumber`.
3. Advance to `Round 4` (`StartRound`+`CompleteRound` loop `IDifficultyProgressionStrategy` Linear 1→2→3→4): verify fila 4 `current`, 1-3 `completed` ✓ `opacity 0.7`, 5-10 `upcoming`, `difficulty` textual (Basic..Intermediate) por fila coincide `Round.level` autoritativo. Check network `GET /players/me` 200 `maxRounds:10 currentRoundNumber:4 rounds[3].level:Intermediate` y `X-Correlation-Id` prop. Screen reader `aria-label` "Ronda 4 de 10, nivel Intermediate".
4. Change game `MaxRounds 15` `CategorySpecific` → verify ladder 15 filas, level "Geografía — Hard" sin hardcodear.

**Expected**: SC-001 100% N filas exactas `aria-current` = `currentRoundNumber`, SC-002 Previous `completed` 100% + upcoming muted 100%, SC-010 Difficulty 100% autoritativo.

### V2 — Current/Next/Secured/Final rewards ledger (US2, FR-005..007, SC-003/004)

1. Game `RewardRules`: `Round 5→500 pts Pack Plata`, `Round 10→5000 pts Pack Oro` `KEEP_SECURED_SCORE` checkpoint 5.
2. Play to `Round 6` `Score 700` `Secured 500 checkpoint 5` (ledger `PointTransaction` sum). Verify ladder: fila 6 badge `Current Reward: 600 pts` (o `pointsPerRound*6` fallback), fila 7 badge `Next Reward` muted upcoming + flecha `800 pts`, fila 5 escudo `Asegurado 500 pts` + filas 1-5 `class="secured"` `background success-subtle`, fila 10 corona gradiente `Final Reward: 5000 pts` siempre visible. Placeholder test: `RewardRules=[]` → filas muestran "—" `aria-label="Sin recompensa"` sin romper layout.
3. `LossPolicy LOSE_ALL` game → verify `Secured 0 checkpoint null` → sin escudo, `Secured summary` "Sin monto asegurado" 100%.

**Expected**: SC-003 100% 4 rewards coinciden ledger+RewardRules reconstruible, SC-004 Secured escudo 100% `KEEP_SECURED_SCORE` y 0 si `LOSE_ALL`.

### V3 — Transición sync server hydrate, error, reconnect (US3, FR-008..011, SC-005..007)

1. `ROUND_IN_PROGRESS` ronda 4 `Answer EVALUATED`, wait server `RoundCompleted {roundNumber:4}` + `QuestionAvailable {5, expiresAt}` → observe network `GET /players/me` hydrate (no payload trust), then ladder Previous includes 4 ✓ completed, Current passes to 5 animación 300ms `class="animating"` pulso + `aria-live="polite"` "Avanzaste a ronda 5" <500ms. Verify no `currentReward` update antes hydrate (audit log).
2. Simulate hydrate 500 con `X-Correlation-Id` → verify Current stays 4, `ErrorState` shows `detail` + `CorrelationId/TraceId` + Retry, no false advance 0%. Retry CTA → hydrate 200 → advances.
3. Disconnect SignalR, advance server 2 rounds offline (4→6), reconnect `withAutomaticReconnect` → verify hydrate directo a `current=6` sin animar 5 intermedio falso, `previousTransition` jump detected.
4. Check `prefers-reduced-motion: reduce` media query → transition none.

**Expected**: SC-005 100% transiciones solo tras hydrate 0% payload, SC-006 <400ms 100% + aria-live 100% + reduced-motion 100%, SC-007 0% false advance 100% Error + Retry.

### V4 — Responsive, A11y, Cinematic premium (US4, FR-013/014, SC-008/009)

1. Resize 375px → ladder `max-height:40vh overflow-y:auto` scrolleable interna, 15 filas sin scroll horizontal, targets ≥44px. ≥1024px sidebar sticky. Check CSS 0 literales `var(--space-*) var(--color-*)` `data-theme="player"`.
2. `axe` / Lighthouse → 0 violations AA contrast tokens, foco `outline:2px solid var(--color-primary)`, `aria-current` único, `role list/listitem`, `aria-live` announce.
3. Qualitative: `final` gradiente corona visible premium, Current glow premium — 80% users rate Cinematic/Premium (SC-009).

**Expected**: SC-008 100% responsive + WCAG, SC-009 80% premium.

### V5 — Empty/Terminal (FR-016)

1. `WAITING_FOR_PLAYERS` `currentRoundNumber null` → `Empty` "Aún no inicia — N rondas por jugar" ladder N filas `upcoming` sin Current.
2. `POST /withdraw` → `WITHDRAWN` `isTerminal` → ladder bloquea animación, shows Secured/Final finales.

## Test / Lint

```bash
cd src/Player/QuizArena.Player
npm test -- --watch=false # player-rounds.store (ladder N, aria-current, rewards ledger, hydrate gate, reconnect jump) + player-rounds.component (list, escudo, corona, prefers-reduced-motion, axe)
npm run lint
dotnet test tests/OroQuizClash.Application.Tests -k GetMyPlayerState
dotnet test tests/OroQuizClash.Architecture.Tests -k PlayerRounds
```

## Cleanup

```bash
aspire stop
```
