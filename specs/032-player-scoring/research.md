# Research: Player Scoring (032)

**Branch**: `032-player-scoring` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Summary

0 `NEEDS CLARIFICATION` en spec; investigación consolida 5 decisiones para mostrar 5 métricas autoritativas (`Current/Secured/Potential/Round/Total Points`) en `QuizArena.Player` Angular 22 SPA (SPEC-027/029/030/031) como proyección de solo lectura de `PointTransaction` ledger server-side (D), actualizadas vía `ScoreUpdated/RoundCompleted/Reconnected → hydrate` (SPEC-012, Server Truth V), con `data-theme="player"` tokens y `prefers-reduced-motion` (SPEC-016), sin cálculo cliente.

## Decisions

### 1. Cinco métricas con `ScorePanelComponent` + `PlayerGameStore` existente

- **Decision**: Reutilizar `stores/player-game.store.ts` `signalStore` con `state: {score: Score, securedPoints: SecuredPoints, game: Game}` + `computed`: `currentPoints = score().totalPoints`, `securedPoints = securedPoints().securedPoints`, `checkpoint = securedPoints().checkpointRoundNumber`, `roundPoints = score().roundPoints ?? (score().totalPoints - securedPoints().securedPoints)`, `totalPoints = score().totalPoints` (o `sum(PointTransaction)` server-side), `potentialReward = game()?.configuration?.rewardRules` próximo umbral o `PointsPerRound` fallback "—". `ScorePanelComponent` `selector app-score-panel` standalone con 5 `<div role="status" aria-live="polite" aria-label="Current Points 350 puntos">` + `Secured` con `checkpoint 3` badge + `Potential` con "—" fallback + `Round` con "en juego" + `Total` bold. `GameComponent` footer `grid` 5 métricas ya en 029 lo reutiliza sin duplicar.

- **Rationale**: FR-001/005/006/007 + SC-001/004/005 + Constitución D (Ledger) + V (Server Truth) + SPEC-007/029 `GetMyPlayerState` ya retorna `Score/SecuredPoints`.

- **Alternatives**: Crear nuevo `ScoringStore` separado (rechazado — `PlayerGameStore` ya tiene 10 elementos con `Score/SecuredPoints`, duplicaría hydrate y realtime binding); hardcodear `Total Points = Current + Secured` cliente-side (rechazado — viola V, ledger es `sum(PointTransaction)` server-side).

- **Accessibility**: `aria-live="polite"` por métrica, `aria-label` descriptivo "Current Points 350 puntos", `Tab` navega métricas, `prefers-reduced-motion` deshabilita `pulse`.

### 2. Realtime `ScoreUpdated/RoundCompleted/Reconnected → hydrate` (SPEC-012)

- **Decision**: `GameRealtimeService` `withAutomaticReconnect [0,2000,5000,10000,30000]` eventos `ScoreUpdated`/`RoundCompleted`/`RoundStarted`/`GameFinished`/`Reconnected` → `PlayerGameStore.hydrateFor(gameId)` `GET /players/me` (no payload del evento). `hydrate` actualiza `score/securedPoints/game` con `serverNow` corrección para `Timer` pero no para scoring (scoring no depende de tiempo). `ScorePanelComponent` observa `store.score()`/`securedPoints()` computados, animación `pulse 600ms` en `Current Points` tras `ScoreUpdated` con `@media prefers-reduced-motion reduce animation none`.

- **Rationale**: FR-003 + SC-002 + Constitución G (Realtime/Outbox) + V (Server Truth).

- **Alternatives**: Confiar en `ScoreUpdated` payload para `Current Points` (rechazado — payload no fuente verdad, viola V); polling `interval(1000)` para scoring (rechazado — no escala, aumenta latencia).

### 3. Server Truth ledger: `Total Points = sum(PointTransaction)` + `Secured` protegido

- **Decision**: Backend `GetMyPlayerStateHandler` retorna `Score` con `CurrentPoints = sum(PointTransaction where PlayerId)` (`AwardPoints` + `RemovePoints` etc.) y `SecuredPoints` con `SecuredPoints` + `checkpointRoundNumber` + `Policy` (`KEEP_SECURED_SCORE` etc.) derivado de `GamePlayer.Score` (SPEC-007 D). `Total Points` es `Score.TotalPoints` autoritativo (o `score.totalPoints` ya suma). Cliente nunca hace `Total = Current + Secured`. `Secured checkpoint null` → sin badge. `RoundPoints` es `Score.RoundPoints` reseteado en `RoundCompleted` per `LossPolicy` (ej. `LOSE_UNSECURED_POINTS` mantiene `Secured`).

- **Rationale**: FR-002 + SC-003 + Constitución D (Ledger) + F (Idempotency).

- **Alternatives**: Calcular `Total Points` cliente-side sumando `Current + Secured` (rechazado — viola D, `Total` es ledger sum con `GAME_BONUS` etc.).

### 4. Responsive `data-theme="player"` tokens sin literales + `prefers-reduced-motion`

- **Decision**: `ScorePanelComponent` `footer` con `display:grid; gap:var(--space-3);` `@media (min-width:768px) {grid-template-columns:repeat(5,1fr);}` 1 col 375 / 5 col ≥768 (o 1col/2col si `Potential` "—"). Métricas `min-height:44px` `min-width:44px` `padding:var(--space-3) var(--space-4)` `border:1px solid var(--color-border)` `border-radius:var(--radius-md)` `background:var(--color-surface)`. `Secured` badge `background:var(--color-primary-subtle)` `Round` "en juego" `color:var(--color-warning)` `Total` `font-weight:700` `color:var(--color-primary)`. `@media (prefers-reduced-motion: reduce) { * {transition:none; animation:none;}}` + `data-theme="player"` sin literales.

- **Rationale**: FR-007/008/010 + SC-006/007/008 + SPEC-016 `data-theme="player"` cinematic + WCAG AA.

- **Alternatives**: Flex row única siempre (rechazado — no cabe 5 métricas en 375px); Tailwind literales (rechazado — viola Design System).

### 5. `X-Correlation-Id` + `ErrorState` + JWT gating para `GET /players/me`

- **Decision**: `correlationIdInterceptor` (`X-Correlation-Id: crypto.randomUUID()` per `GET /players/me` hydrate) + `authInterceptor` `secureRoutes=[apiUrl]` + `errorInterceptor` RFC7807 ya en 027/029. `PlayerGameStore.hydrate` error 401 → `silentRenew` / redirect OIDC; 403 `PlayerNotInGame` audit; 404 `GameNotFound`; `ErrorState` `detail` + `CorrelationId/TraceId` + `Retry` reusa `hydrate`. `must_change_password` guard redirect antes de `GET /players/me`.

- **Rationale**: FR-004/011/012 + SC-007 + Constitución H/I (Security/ProblemDetails).

- **Alternatives**: Sin `X-Correlation-Id` en scoring (rechazado — trazabilidad OTel requerida).

## Resolved Clarifications

| Topic | Resolution |
|-------|------------|
| `Total Points` cálculo | `Score.TotalPoints` server-side `sum(PointTransaction)`; cliente no suma `Current+Secured`. |
| `Potential Points` sin config | Placeholder "—" con `aria-label` "Potential no disponible". |
| `Secured checkpoint null` | Sin badge "checkpoint", solo "{secured} pts". |
| `Round Points` vs `Current` | `RoundPoints` es `Score.RoundPoints` autoritativo, reseteado en `RoundCompleted` per `LossPolicy`; visual "en juego". |
| `Secured` protegido | `LossPolicy` `LOSE_UNSECURED_POINTS` no afecta `Secured`; `RoundCompleted` `SecurePoints` operation mueve `RoundPoints→Secured`. |
| Debounce scoring | No debounce; scoring es idempotente por ledger, `hydrate` no debounce. |

## References

- `draft/constitution.md` §I–VI, §A-J, §V Server Truth `sum(PointTransaction)=totalPoints`, §D Ledger `PointTransaction` 10 tipos, §G Realtime `ScoreUpdated`, §H `sub=PlayerId`.
- `draft/game-concept.md` §Scoring D, §Withdrawal C.
- `design-system/MASTER.md` + `overrides/player.md` + `tokens/design-tokens.css` Cinematic `data-theme="player"` WCAG 375-1536.
- `src/Player/QuizArena.Player` `stores/player-game.store.ts` `features/game/score-panel.component.ts` `game.component.ts` `features/shared/games.api.ts` `getMyState` `core/realtime/game-realtime.service.ts` `withAutomaticReconnect` (SPEC-027/029/030/031).
- `src/OroQuizClash.Application/Features/Games/` `GetMyPlayerState` `GetPlayerScore` `IEndpoint` `GameClaims`.
- `OroQuizClash.AppHost/AppHost.cs` `quizarena-player` container `node:22-alpine`.
- `specs/007-scoring-system/` `specs/029-player-game/` `specs/030-player-rounds/` `specs/031-player-answering/` (previos).
