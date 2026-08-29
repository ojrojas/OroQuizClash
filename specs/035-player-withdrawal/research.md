# Research: Player Withdrawal (035)

**Branch**: `035-player-withdrawal` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Summary

0 `NEEDS CLARIFICATION` en spec; investigación consolida 5 decisiones para flujo retiro voluntario `Withdrawal Action` → diálogo modal con 3 métricas autoritativas (`Current Points` `Secured Points` · checkpoint `Potential Points` “—” si no configurado) + 2 warnings _"If you continue..."_ y _"Withdraw now and secure X points?"_ (X=`Secured`) → confirmación 2 pasos `role="dialog"` `aria-modal` → `POST /api/games/{id}/withdraw` `X-Idempotency-Key` `sessionStorage` per `gameId` `idemp-withdraw-{gameId}` → `GameSession` `WITHDRAWN` `RowVersion` per `GamePlayerId` `isTerminal true` `canAnswer false` `Current` → `Secured` (`KEEP_SECURED_SCORE`) ledger idempotente. Extiende `QuizArena.Player` Angular 22 SPA (SPEC-027/029/032/033) con `WithdrawalComponent` `app-withdrawal` + `PlayerGameStore` `withdraw()` `rxMethod` ya en 029 + `GameComponent` `showWithdrawConfirm`.

## Decisions

### 1. Tres métricas autoritativas en diálogo `Current/Secured/Potential` per `sub`

- **Decision**: Diálogo `WithdrawalComponent` `app-withdrawal` muestra `Current Points` `Score.totalPoints` (ej. 400) + `Secured Points` `SecuredPoints.securedPoints` `checkpoint 2` → `formatSecured` `"{n} pts · checkpoint {m}"` o `"{n} pts"` si null + `Potential Points` `PotentialReward` de `PlayerGameStore` (029/032) `PointsPerRound` o "—" `aria-label` "Potential no disponible", todos de `GET /api/games/{id}/players/me` `Score`/`SecuredPoints`/`GameConfiguration` sin cálculo cliente (V/D). `GameComponent` `Withdrawal Action` botón `min-height:44px` `aria-label="Retirarse"` abre `showWithdrawConfirm=true` modal `position:fixed` `inset:0` `background:rgba(0,0,0,0.5)` centrado `max-width:400px` `padding:var(--space-6)` `border-radius:var(--radius-lg)`.

- **Rationale**: FR-001 + SC-001 + Constitución D (Ledger `Score`) + V (Server Truth).

- **Alternatives**: Calcular `Secured` cliente-side `Current - 200` (rechazado — viola V, `Secured` es `SecuredPoints` server per `WithdrawalPolicy`).

### 2. Dos warnings exactos + confirmación 2 pasos `role="dialog"`

- **Decision**: Diálogo muestra warning 1: `"If you continue and answer incorrectly, you may lose your accumulated points."` con `role="alert"` `aria-live="assertive"` `var(--color-destructive)` y warning 2 dinámico: `"Withdraw now and secure X points?"` donde X=`SecuredPoints.securedPoints` (ej. 200) con `aria-live="polite"`. Flujo 2 pasos: paso 1 `Withdrawal Action` abre diálogo; paso 2 `Confirmar` (≥44px `min-height:44px` `min-width:44px` `aria-label="Confirmar retiro"`) envía `POST /withdraw` `X-Idempotency-Key` UUID per `gameId` `sessionStorage` `idemp-withdraw-{gameId}` + `Authorization Bearer`; `Cancelar` (≥44px) / `Escape` / click fuera (si `backdrop` clic) cierra `showWithdrawConfirm=false` sin llamada (F). `Confirmar` deshabilitado si `isTerminal` ya.

- **Rationale**: FR-002/003 + SC-002/003 + Constitución F (Idempotency `X-Idempotency-Key`).

- **Alternatives**: Single clic `Withdrawal Action` directo sin diálogo (rechazado — riesgo accidental, viola FR-003).

### 3. `PlayerWithdrawn` terminal `WITHDRAWN` `isTerminal true` `canAnswer false` `Current=Secured`

- **Decision**: `WithdrawPlayerHandler` valida `!IsTerminal` + `!IsWithdrawn` + `!IsEliminated` + `IsActive` (`PlayerNotWithdrawn` + `PlayerAlreadyEliminated` + `WithdrawalPolicy` `KEEP_SECURED_SCORE` → `deduction = Current - Secured` `Score.CurrentPoints = SecuredPoints`), genera `PointTransaction` `WITHDRAWAL` `-deduction` `ResultingBalance` `WITHDRAWN`, `GamePlayer.Status→WITHDRAWN` `RowVersion` per `GamePlayerId` `RowVersion++` (F). Cliente `PlayerGameStore.withdraw()` `rxMethod` `X-Idempotency-Key` `idemp-withdraw-{gameId}` `sessionStorage` `crypto.randomUUID()` + `patchState({gameSession,status})` `isTerminal true` `canAnswer false` tras `hydrate`. Segunda confirmación misma `X-Idempotency-Key` retorna mismo `GameSession` `WITHDRAWN` sin nuevo ledger (idempotente, `PlayerAlreadyWithdrawn` 403 si distinto key sin misma idempotencia? manejado por `IdempotencyBehavior`).

- **Rationale**: FR-004/005 + SC-004/005/006 + Constitución C (`WithdrawalPolicy` `KEEP_SECURED_SCORE`) + F (`RowVersion` per `GamePlayerId`).

- **Alternatives**: `Current` queda 400 tras `WITHDRAWN` (rechazado — viola `KEEP_SECURED_SCORE`, `Current` debe ser `Secured` 200).

### 4. `X-Correlation-Id` + `ErrorState` + JWT gating + `isTerminal` block `QuestionComponent`

- **Decision**: `correlationIdInterceptor` `X-Correlation-Id` per `POST /withdraw` + `GamesApi.withdraw` `X-Idempotency-Key` `idemp-withdraw-{gameId}`; `authInterceptor` `secureRoutes=[apiUrl]` `Bearer` solo `oroclash-api`; `errorInterceptor` RFC7807 `PlayerAlreadyWithdrawn 403`/`InvalidGameState 400` + `CorrelationId/TraceId` `ErrorState` `Retry` reusa misma `X-Idempotency-Key`. `PlayerGameStore.status.isTerminal` `canAnswer false` bloquea `QuestionComponent` `aria-disabled` y `Withdrawal Action` `disabled` si `isTerminal`. `GameRealtimeService` `withAutomaticReconnect` `PlayerWithdrawn`/`GameFinished` → `hydrate` `GET /players/me` per `sub` `WITHDRAWN`.

- **Rationale**: FR-008/009 + SC-008 + Constitución H/I.

- **Alternatives**: Permitir `POST /answers` tras `WITHDRAWN` (rechazado — viola FR-005, `PlayerWithdrawn` `canAnswer false`).

### 5. `data-theme="player"` tokens responsive + `prefers-reduced-motion`

- **Decision**: Diálogo `max-width:400px` centrado `position:fixed` `inset:0` `background:rgba(0,0,0,0.5)` `display:flex; align-items:center; justify-content:center;` con `background:white` no literal sino `var(--color-surface)`?? pero modal es `background: var(--color-surface)` `border:1px solid var(--color-border)` `border-radius:var(--radius-lg)` `padding:var(--space-6)` `gap:var(--space-3)` `min-height:44px` por botón. `Withdrawal Action` botón `min-height:44px` `min-width:44px` `var(--color-primary)` etc. `@media (prefers-reduced-motion: reduce) { *{animation:none;}}`.

- **Rationale**: FR-007 + SC-007 + SPEC-016.

- **Alternatives**: `max-width` literal 400px sin token (rechazado — viola Design System, pero `400px` es literal necesario para diálogo, se justifica como `max-width:400px` no tokenizable).

## Resolved Clarifications

| Topic | Resolution |
|-------|------------|
| `X-Idempotency-Key` per `gameId` vs `roundId` | Per `gameId` `idemp-withdraw-{gameId}` `sessionStorage` (no per `roundId` como `Answer`), idempotente `WITHDRAWAL` ledger. |
| `Current` tras `WITHDRAWN` `KEEP_SECURED_SCORE` | `CurrentPoints = SecuredPoints` (200) tras `hydrate`; `Current 400` → `200` si `WITHDRAWN`. |
| `Secured 0` `LOSE_ALL` | Warning "Withdraw now and secure 0 points?" válido, sin romper layout. |
| `Potential` "—" | Si no `RewardRules`, `Potential` "—" `aria-label` "Potential no disponible". |
| `Game FINISHED` retiro | Rechazado 400 `InvalidGameState` con `ProblemDetails` `CorrelationId`, diálogo `ErrorState`. |
| `ELIMINATED` retiro | Rechazado 403 `PlayerAlreadyEliminated`, `Withdrawal Action` `disabled` si `isTerminal`. |
| `RowVersion` per `GamePlayerId` vs global | Per `GamePlayerId` `RowVersion`, `Withdraw` de A no afecta B (concurrente 2 `Withdraw`). |

## References

- `draft/constitution.md` §I–VI, §A-J, §C `WithdrawalPolicy` `KEEP_SECURED_SCORE`, §D `WITHDRAWAL` ledger, §F `RowVersion` per `GamePlayerId` `X-Idempotency-Key`, §G `GameHub`.
- `draft/game-concept.md` §Withdrawal §Scoring §Game/Round Lifecycle.
- `design-system/MASTER.md` + `overrides/player.md` + `tokens/design-tokens.css` Cinematic `data-theme="player"` WCAG 375-1536.
- `src/Player/QuizArena.Player` `stores/player-game.store.ts` `withdraw()` `rxMethod` `X-Idempotency-Key` `features/game/withdrawal.component.ts` `features/game/game.component.ts` `showWithdrawConfirm` `features/shared/games.api.ts` `withdraw()` `getMyState` (SPEC-029/032).
- `src/OroQuizClash.Application/Features/Games/` `WithdrawPlayer` `POST /withdraw` `X-Idempotency-Key` `PlayerAlreadyWithdrawn` `RowVersion` per `GamePlayerId`.
- `OroQuizClash.AppHost/AppHost.cs` `quizarena-player` container `node:22-alpine`.
- `specs/008-player-withdrawal/` `specs/029-player-game/` `specs/033-player-multiplayer/` (previos).
