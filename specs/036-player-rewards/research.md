# Research: Player Rewards (036)

**Branch**: `036-player-rewards` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Summary

0 `NEEDS CLARIFICATION` en spec; investigación consolida 5 decisiones para flujo `Points Wallet` → `Rewards Catalog` `Available/Required/Remaining Points` + `Reward Status` → `Reward Detail` → `Redeem` 2 pasos `role="dialog"` `aria-modal` → `POST /api/rewards/{rewardId}/redeem` `X-Idempotency-Key` `sessionStorage` per `rewardId` → `RewardRedemption` `REQUESTED` + `PointTransaction` `REWARD_REDEMPTION` ledger idempotente → `Confirmation` + `Redemption History` + `Consolation Reward`. Extiende `QuizArena.Player` Angular 22 SPA con `PlayerRewardsStore` `redeem()` `rxMethod` y reutiliza slices `GetRewards`/`RedeemReward`/`GetPlayerRedemptions` + dominio `Reward.ReserveStock` + `Game.ConsumePoints` ya existentes.

## Decisions

### 1. Cuatro métricas autoritativas `Available/Required/Remaining/Reward Status` per `sub`

- **Decision**: `Points Wallet` header muestra `Available Points` desde `GamePlayer.Score.CurrentPoints` (ledger `PointTransaction`) expuesto vía `GET /api/rewards?gameId` `availablePoints` o `GET /api/games/{id}/players/me` `score.totalPoints`; cada card en `RewardsCatalogComponent` `app-rewards-catalog` muestra `Required Points` `Reward.PointsRequired` + `Reward Status` `Canjeable` si `Available >= Required && Reward.IsAvailable(now)` (`Status ACTIVE && Stock>0 && not expired`) sino `Puntos insuficientes` si falta saldo o `Agotada`/`No disponible` si no stock/inactiva. `RewardDetailComponent` `app-reward-detail` re-expone `Available 1200` + `Required 800` + `Remaining Points` `max(0, Available - Required) = 400` si canjeable o `Required - Available = 700` faltantes si no canjeable, calculado como proyección de lectura derivada de valores autoritativos sin lógica de negocio en cliente (V/D). Todos los valores provienen del servidor; `Remaining` nunca es fuente para debitar.

- **Rationale**: FR-001/002/003 + SC-001 + Constitución V (Server Truth) + D (Ledger) + C (Reward lifecycle).

- **Alternatives**: Calcular `Available` cliente-side sumando transacciones locales (rechazado — viola V, saldo es `Score.CurrentPoints` reconstruido server-side desde `PointTransaction`).

### 2. Redeem 2 pasos `role="dialog"` + `X-Idempotency-Key` per `rewardId`

- **Decision**: Flujo 2 pasos: paso 1 `Canjear` botón `min-height:44px` `aria-label="Canjear recompensa"` habilitado solo si `isRedeemable` (`Available >= Required && IsAvailable`); paso 2 diálogo confirmación `role="dialog"` `aria-modal="true"` `aria-label="Confirmar canje"` con resumen `Required 800` + `Remaining 400` + `Disponible 1200` y warnings idénticos a withdrawal pero adaptados ("¿Confirmar canje de X por 800 puntos?"). Confirmación ejecuta `POST /api/rewards/{rewardId}/redeem` `X-Idempotency-Key` UUID per `rewardId` `sessionStorage` `idemp-redeem-{rewardId}` + `Authorization Bearer` + `X-Correlation-Id` + body `{gameId, idempotencyKey}`. `Cancelar`/`Escape`/click fuera cierra sin llamada (F). `Confirmar` reutiliza misma key para reintentos.

- **Rationale**: FR-004/005 + SC-004 + Constitución F (Idempotency `X-Idempotency-Key` `UNIQUE (PlayerId,IdempotencyKey)`).

- **Alternatives**: Single clic directo sin diálogo (rechazado — riesgo accidental, viola FR-005 y SC-004).

### 3. Backend único procesador `Reward.ReserveStock` → `Game.ConsumePoints` → `RewardRedemption.Create` ledger idempotente

- **Decision**: `RedeemRewardHandler` flujo atómico: si `IdempotencyKey` existe y `RewardRedemption` con mismo `PlayerId+IdempotencyKey` ya existe → retornar existente idempotente 200 sin duplicar; sino `Reward.ReserveStock(now)` valida `RewardAvailableRule` (ACTIVE, Stock>0, no expirada) decrementa `Stock` 1; luego `Game.ConsumePoints(playerId, reward.PointsRequired)` valida `SufficientBalanceRule` (`CurrentPoints >= Required`) crea `PointTransaction` `REWARD_REDEMPTION` `-Required` `ResultingBalance`; si falla, `ReleaseStock()` rollback; luego `RewardRedemption.Create(playerId, rewardId, gameId, points, idempotencyKey)` `REQUESTED` + transition + `RewardRedeemedDomainEvent` → `UoW.SaveChanges` (transacción + Outbox `RewardRedeemed` → RabbitMQ). Reintento misma key no crea segundo `RewardRedemption` ni segundo `PointTransaction`. Errores mapeados: `RewardUnavailable 409`, `InsufficientPoints 409` (desde `GameErrors`), `RewardNotFound 404`, `InvalidGameState 400`.

- **Rationale**: FR-006/011 + SC-003/005 + Constitución D (Ledger) + F (RowVersion per `Reward` + per `GamePlayer` + `UNIQUE`).

- **Alternatives**: Descontar puntos en cliente y enviar confirmación (rechazado — viola V, backend es única autoridad).

### 4. Redemption History paginada + Empty State + Consolation diferenciada

- **Decision**: `RedemptionHistoryComponent` `app-redemption-history` consume `GET /api/redemptions` (`GetPlayerRedemptions` `sub` derivado de JWT) lista ordenada por `RequestedAt` descendente, paginada server-side; cada fila muestra nombre recompensa, `Required Points`, `Remaining Points` (saldo resultante si disponible), `Reward Status` `Canjeada`/`REQUESTED`/`APPROVED`/`REJECTED`/`DELIVERED`/`CANCELLED` + `Consolation` badge diferenciado (`color:var(--color-info)`) + fecha `RequestedAt` y `reference` `RedemptionId`. Estado vacío: mensaje "Aún no has canjeado recompensas" + CTA a `/rewards`. `Consolation Reward` se otorga vía `RewardRedemption.CreateAsConsolation` `APPROVED` `Points 0` al finalizar partida según `ConsolationPolicy` (RewardBased/FixedPoints), visible en History con `Status = APPROVED` + origen `GameId` y no listada en Catalog como canjeable (filtrada).

- **Rationale**: FR-008/010 + SC-006/007 + Constitución C (Consolation independent) + I (RFC7807).

- **Alternatives**: Guardar historal solo en `localStorage` cliente (rechazado — viola V, fuente es server).

### 5. `data-theme="player"` tokens responsive + `X-Correlation-Id` + JWT gating

- **Decision**: `RewardsCatalog` grid `1 col 375px` → `2 col 768px` → `4 col 1536px` sin scroll, cards `padding:var(--space-3)` `border:1px solid var(--color-border)` `border-radius:var(--radius-md)` `background:var(--color-surface)` tokens only, targets ≥44px, `Reward Status` badge `var(--color-success)` si Canjeable / `var(--color-warning)` si faltante / `var(--color-destructive)` si Agotada. Diálogo confirmación `max-width:400px` centrado `position:fixed` `inset:0` `background:rgba(0,0,0,0.5)` `display:flex; align-items:center; justify-content:center` con `gap var(--space-3)`. `correlationIdInterceptor` `X-Correlation-Id` per `POST /redeem` + `authInterceptor` `secureRoutes=[apiUrl]` `Bearer` solo `oroclash-api`; `errorInterceptor` RFC7807 mapea `RewardUnavailable 409`/`InsufficientPoints 409` + `CorrelationId/TraceId` `ErrorState` `Retry` reusa misma `X-Idempotency-Key`. `PlayerRewardsStore.redeem()` `isTerminal`-like no aplica pero `isRedeemable` bloquea `Canjear` si no canjeable.

- **Rationale**: FR-012/013 + SC-008 + Constitución H/I/J + SPEC-016 `data-theme="player"` + WCAG 2.2 AA `prefers-reduced-motion`.

- **Alternatives**: Permitir `GET /rewards` sin JWT para catálogo público (rechazado — FR-012 exige auth para todo rewards).

## Resolved Clarifications

| Topic | Resolution |
|-------|------------|
| `Remaining Points` cálculo canjeable vs faltante | Si `Available >= Required`: `Remaining = Available - Required` (0 si exacto); si no canjeable: mostrar faltante `Required - Available` + `Reward Status = Puntos insuficientes`. Cálculo es proyección lectura, no usado para debitar. |
| `X-Idempotency-Key` per `rewardId` vs per `gameId` | Per `rewardId` `idemp-redeem-{rewardId}` `sessionStorage` (no per `gameId`); mismo rewardId reintento idempotente, diferente rewardId distinta key. |
| `Available Points` fuente para Rewards | `GetRewards?gameId` `availablePoints` + `GetMyPlayerState` `score.totalPoints` son autoritativos; wallet refresca tras `Redeem` via `hydrate` `GET /rewards` + `GET /redemptions`. |
| `Consolation Reward` coexistencia | Exclusión mutua con recompensa estándar por partida si ya canjeada; `CreateAsConsolation` `Points 0` no consume saldo y se marca `APPROVED` directa; no descuenta `Stock` si consolación no es stock-based según config. |
| `Reward Status` valores | `Canjeable` / `Puntos insuficientes` / `Agotada` / `No disponible` (Inactive/Expired) mapeados desde `Reward.IsAvailable(now)` + `Available >= Required`. |
| `History` paginación | Server-side paginado `page`/`pageSize`/`totalCount` pero UI muestra primera página + "Ver más" progresivo, orden `RequestedAt` desc. |
| `Stock 0` canje | Rechazado `RewardUnavailable 409` con `ProblemDetails` `CorrelationId`, badge `Agotada`, botón deshabilitado. |

## References

- `draft/constitution.md` §I–VI, §A-J, §C `Reward` `RewardRedemption` lifecycle, §D ledger `REWARD_REDEMPTION`/`CONSOLATION`, §F `RowVersion` per `Reward` + `UNIQUE (PlayerId,IdempotencyKey)`.
- `draft/game-concept.md` §Rewards §Scoring §Game lifecycle.
- `design-system/MASTER.md` + `overrides/player.md` + `tokens/design-tokens.css` Cinematic `data-theme="player"` WCAG 375-1536.
- `src/OroQuizClash.Domain/Rewards/` `Reward.ReserveStock`/`ReleaseStock` `RewardRedemption.Create`/`CreateAsConsolation` `RedemptionStatus` `RewardStatus`.
- `src/OroQuizClash.Application/Features/Rewards/` `RedeemReward` `POST /rewards/{id}/redeem` `X-Idempotency-Key` `ConsumePoints` `GetRewards` `GetPlayerRedemptions`.
- `src/OroQuizClash.Domain/Games/Game.ConsumePoints` `SufficientBalanceRule`.
- `src/Player/QuizArena.Player` `stores/player-game.store.ts` `stores/player-rewards.store.ts` futuro + `features/shared/games.api.ts` + `features/rewards/` futuro.
- `OroQuizClash.AppHost/AppHost.cs` `quizarena-player` container `node:22-alpine`.

