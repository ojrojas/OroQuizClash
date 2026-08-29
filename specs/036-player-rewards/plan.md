# Implementation Plan: Player Rewards

**Branch**: `036-player-rewards` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/036-player-rewards/spec.md`

## Summary

Flujo de recompensas `Points Wallet` → `Rewards Catalog` con `Available/Required/Remaining Points` + `Reward Status` → `Reward Detail` → `Redeem` 2 pasos `role="dialog"` `aria-modal` → `POST /api/rewards/{rewardId}/redeem` `X-Idempotency-Key` `Authorization Bearer` → `RewardRedemption` `REQUESTED` ledger idempotente `PointTransaction` `REWARD_REDEMPTION` → `Confirmation` + `Redemption History` + `Consolation Reward` automática. Extiende `QuizArena.Player` Angular 22 SPA (SPEC-027/029/032/035) con `RewardsCatalogComponent` + `RewardDetailComponent` + `RedemptionHistoryComponent` + `PlayerRewardsStore` `redeem()` `rxMethod` + `RewardsApi` `getRewards/getWallet/getMyRedemptions/redeem` + `design-system/tokens` `data-theme="player"` + OIDC PKCE, reutilizando `oroclash-api` slices `GetRewards` `RedeemReward` `GetPlayerRedemptions` + `Game.ConsumePoints` ledger `REWARD_REDEMPTION` + `Reward.ReserveStock` ya existentes.

## Technical Context

**Language/Version**: TypeScript 5.8+ / **Angular 22** (standalone `input()` `signal()` `computed()` `@if/@for`, `provideRouter`, `HttpClient withFetch` `withInterceptors`, `rxMethod`) Node 22 LTS + C# 12 / .NET 10.0 (`net10.0`) para `oroclash-api` slices ya existentes (`RedeemReward` `POST /rewards/{id}/redeem` `X-Idempotency-Key` + `GetRewards` `AvailablePoints` + `GetPlayerRedemptions`).

**Primary Dependencies**: `@angular/core/router/common/forms` 22.x, `@ngrx/signals` 22.x + `rxjs 7.x` (`rxMethod`, `tapResponse`, `switchMap`), `angular-auth-oidc-client` 17+ PKCE `authorization_code`+`refresh_token`, `design-system/tokens/design-tokens.css` (`data-theme="player"`); Backend `BuildingBlocks.Kernel.Domain` (`AggregateRoot`, `IBusinessRule`, `Result`, `Enumeration`), `BuildingBlocks.CQRS` (`ICommand` `RedeemReward` `IQuery` `GetRewards`/`GetPlayerRedemptions` `ISender`), `BuildingBlocks.Kernel.Infrastructure` (`AppDbContextBase`, `EfRepository`, `IUnitOfWork`, `Outbox`), `BuildingBlocks.ServiceDefaults` (OTel, health, `IEndpoint`, `GlobalExceptionHandler`).

**Storage**: SQL Server primaria `oroclash` (`Game` RowVersion + `GamePlayer` `RowVersion` per `GamePlayerId` + `Score` via `PointTransaction` ledger `REWARD_REDEMPTION`/`CONSOLATION` + `Reward` `RowVersion` + `RewardRedemption` `RowVersion` + `UNIQUE (PlayerId,IdempotencyKey)` + Outbox); PostgreSQL `identitydb` aislada OroIdentityServer; cliente solo memoria `PlayerRewardsStore` `{wallet, catalog, detail, history, redeemStatus}` + `sessionStorage` efímero `idemp-redeem-{rewardId}` `X-Idempotency-Key` per `rewardId` nunca `localStorage`.

**Testing**: Vitest 3 + Angular Testing Library + `provideHttpClientTesting` + `TestBed` para `RewardsCatalogComponent` (Available/Required/Remaining/Status `aria-live` `Canjeable` vs `Puntos insuficientes` 1200 vs 0, `Reward Detail` `Remaining 400` + `X-Idempotency-Key` 2 pasos `Confirmar`/`Cancelar` no llamada, `Redemption History` vacía vs 3 entradas, `Consolation` badge) y `PlayerRewardsStore` `redeem()` idempotente `InsufficientPoints` `RewardUnavailable`; xUnit v3 + NSubstitute + Testcontainers.MsSql para `RedeemReward` ledger `REWARD_REDEMPTION` + `InsufficientPoints` `RewardUnavailable` + idempotencia misma key sin duplicar `PointTransaction`; `dotnet test` TestingPlatform, `ng test --watch=false`, `ng lint` `@ngrx/eslint-plugin`, axe/Lighthouse WCAG.

**Target Platform**: Web SPA evergreen Chrome/Edge/Firefox/Safari responsive 375–1536 WCAG 2.2 AA, `ng serve` dev `ng build` prod `dist/` hosteado via `QuizArena.Player` static files o container `node:22-alpine` orquestado por `OroQuizClash.AppHost` (sqlserver/postgres/redis/rabbitmq/identity-api/oroclash-api/quizarena-player).

**Project Type**: web-application (Angular SPA `src/Player/QuizArena.Player` + modular monolith `Domain/Application/Infrastructure/Api`).

**Performance Goals**: SC-001 100% `Available Points` autoritativo 0% cliente; SC-002 flujo Wallet→Confirmation <90s; SC-003 100% canje solo backend 0 cliente; SC-005 100% idempotente misma key sin duplicar `REWARD_REDEMPTION`; SC-008 95% errores accionables <1s; responsive 375-1536 sin scroll 100% targets ≥44px; axe 0 violations.

**Constraints**: Constitución V server truth (`Available` solo vía `GET /rewards`/`GET /players/me` ledger `REWARD_REDEMPTION` `ConsumePoints`, `RowVersion` decide, cliente nunca deduce `Remaining` autoritativo); VI OroIdentityServer PKCE `jwks_uri` `sub=PlayerId` `must_change_password` gating; H `secureRoutes=[apiUrl]` Bearer solo `oroclash-api` + `X-Correlation-Id` + `InsufficientPoints 409` audit; I RFC7807 `ProblemDetails` `CorrelationId/TraceId`; J REST `IEndpoint` thin `ISender`; WCAG `role="dialog"` `aria-modal` `aria-live assertive/polite` `outline:2px` `prefers-reduced-motion`; Design System `data-theme="player"` sin literales `var(--space-*)` 4 métricas + `Reward Status` tokens (`--color-success` `warning` `destructive`).

**Scale/Scope**: `Available Points` 0..N por jugador, `Rewards Catalog` 0..N recompensas `Required 1..N` `Stock 0..N`, `Remaining = Available - Required` (0 si canjeable, faltante si no), `X-Idempotency-Key` UUID per `rewardId` `sessionStorage` `idemp-redeem-{rewardId}`, `Redemption History` paginado, `Consolation` 0..1 por partida elegible.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Evidencia |
|-----------|--------|-----------|
| I. Domain First | ✅ PASS | Canje es `Reward.ReserveStock(now)` + `Game.ConsumePoints(playerId, requiredPoints)` dominio con `RewardAvailableRule` + `SufficientBalanceRule` + `RewardRedemption.Create` + `PointTransaction` `REWARD_REDEMPTION`; `Consolation` es `RewardRedemption.CreateAsConsolation`. Componentes Angular no contienen lógica de deducción. |
| II. Clean Architecture | ✅ PASS | `Player (RewardsCatalog/Detail/HistoryComponent)` → `oroclash-api RedeemReward ICommand / GetRewards IQuery` → `Application→Domain←Infrastructure`. Domain no referencia Angular. |
| III. BuildingBlocks No Reinvention | ✅ PASS | Reusa `AggregateRoot/Result/IBusinessRule/Specification/IRepository/IEndpoint/ISender/AppDbContextBase/Outbox/ServiceDefaults` ya en 009/023. No MediatR/MassTransit/AutoMapper. Solo Angular `@ngrx/signals` (permitido frontend). |
| IV. Vertical Slice + CQRS | ✅ PASS | Reusa slices `RedeemReward` (`Command` + `Validator` + `Handler` + `Response DTO` + `IEndpoint` thin `ISender`) + `GetRewards` + `GetPlayerRedemptions` + `GetPlayerScore` para Wallet. Sin carpeta genérica. |
| V. Server Truth | ✅ PASS | `Available`/`Required`/`Remaining`/`Reward Status` solo vía `GET /rewards?gameId` + `GET /redemptions` ledger; `Redeem` `ConsumePoints` server `RowVersion` per `Reward`/`GamePlayer`; cliente nunca calcula saldo autoritativo. |
| VI. OroIdentityServer | ✅ PASS | PKCE `authorization_code`+`refresh_token` contra `/.well-known/openid-configuration`, `jwks_uri`, `sub=PlayerId`, `must_change_password` guard, sin user store local. `POST /rewards/{id}/redeem` requiere JWT. |
| C. Configurable Rules | ✅ PASS | `Reward.PointsRequired` + `Reward.Stock` + `RewardStatus` + `RedemptionStatus` no hardcodeados; `ConsolationPolicy` configurable; solo proyección `Remaining = Available - Required` y `Reward Status` canjeable/insuficiente. |
| D. Ledger | ✅ PASS | `Redeem` genera `PointTransaction` `REWARD_REDEMPTION` ledger `ResultingBalance` reconstruible; `Consolation` genera `CONSOLATION` ledger; cliente nunca crea transacciones. |
| F. Concurrency/Idempotency | ✅ PASS | `RowVersion` per `Reward` + `RowVersion` per `GamePlayer` + `X-Idempotency-Key` `idemp-redeem-{rewardId}` `UNIQUE (PlayerId,IdempotencyKey)` → reintento misma key idempotente 200 sin duplicar `RewardRedemption` ni `PointTransaction`. |
| G. Realtime/Outbox | ✅ PASS | Outbox→RabbitMQ `RewardRedeemed` nunca antes commit; no SignalR obligatorio para rewards (polling `hydrate` tras redeem); `GameFinished` no fuente de veredicto reward. |
| H. Security Delegated | ✅ PASS | JWT `jwks_uri`, `NotRedemptionOwner` 403 auditada, `X-Correlation-Id` propagado, `Available` nunca manipulable cliente, `secureRoutes` Bearer solo `oroclash-api`. |
| I. Validation/Errors/Obs | ✅ PASS | 3 niveles, RFC7807 `ProblemDetails` `RewardUnavailable 409`/`InsufficientPoints 409`/`RedemptionNotFound 404`/`InvalidPointsRequired 400` con `CorrelationId/TraceId`, OTel `CorrelationId/TraceId/RewardId/PlayerId`. |
| J. API & Frontend | ✅ PASS | REST `GET /api/rewards` + `GET /api/redemptions` + `POST /api/rewards/{id}/redeem` `X-Idempotency-Key`, DTOs boundary, `RequireAuthorization`, frontend `app-rewards-catalog`/`app-reward-detail`/`app-redemption-history` presentation-only PKCE. |

**Resultado pre-Phase 0: PASS — sin violaciones.**

## Project Structure

### Documentation (this feature)

```text
specs/036-player-rewards/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── api-contracts.md       # GET /rewards Available/Required/Remaining/Status + POST /rewards/{id}/redeem X-Idempotency-Key + GET /redemptions History + Consolation
│   └── ui-contracts.md        # Points Wallet → Catalog 4 métricas + Detail + Redeem 2 pasos + Confirmation + History + Consolation, data-theme player
├── checklists/requirements.md
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/Player/QuizArena.Player/         # Angular 22 SPA (standalone) — extends SPEC-027 (Player App) + 028 (Lobby) + 029 (Game) + 032 (Scoring) + 035 (Withdrawal)
├── src/app/
│   ├── app.routes.ts                # EXTEND: /rewards (catalog), /rewards/:rewardId (detail), /rewards/history (history) con authGuard, mustChangePasswordGuard
│   ├── app.config.ts                # provideAuth PKCE, HttpClient interceptors already
│   ├── core/interceptors/           # correlationId, auth secureRoutes, error RFC7807 already
│   ├── stores/
│   │   └── player-rewards.store.ts  # NEW: signalStore withState {wallet, catalog, selectedReward, history, redeemStatus, consolation} + computed isRedeemable/remainingPoints + redeem() rxMethod POST /rewards/{id}/redeem X-Idempotency-Key idemp-redeem-{rewardId}, hydrate via GET /rewards + GET /redemptions
│   ├── features/rewards/
│   │   ├── rewards-catalog.component.ts # NEW: RewardsCatalogComponent selector app-rewards-catalog standalone lista Rewards con Available Points header + card Required Points + Reward Status badge Canjeable/Puntos insuficientes/Agotada, min-height 44px
│   │   ├── reward-detail.component.ts   # NEW: RewardDetailComponent selector app-reward-detail con Available/Required/Remaining/Status + Redeem 2 pasos role="dialog" aria-modal + Confirmation
│   │   ├── redemption-history.component.ts # NEW: RedemptionHistoryComponent selector app-redemption-history lista paginada fecha descendente + empty-state
│   │   └── consolation-badge.component.ts # NEW: ConsolationBadgeComponent para Consolation Reward status diferenciado
│   └── features/shared/             # EXTEND: rewards.api.ts getRewards(gameId) + getWallet() + getMyRedemptions() + redeem(rewardId, idempotencyKey, gameId) ya parcial, extender con X-Idempotency-Key
│   └── shared/ui/                   # loading-skeleton, empty-state, error-state (CorrelationId) already reuse
├── src/environments/                # environment.apiUrl, identityAuthority already
tests/ (Vitest)
└── src/app/
    ├── stores/player-rewards.store.spec.ts # redeem() idempotente Available 1200 Required 800 Remaining 400, InsufficientPoints 409, RewardUnavailable 409, Consolation Approved
    └── features/rewards/rewards-catalog.component.spec.ts # catalog 4 métricas Available/Required/Remaining/Status aria-live, Detail Remaining 400, 2 pasos Confirmar X-Idempotency-Key, History empty vs 3, Consolation badge
```

**Structure Decision**: Extender SPA `QuizArena.Player` existente con `PlayerRewardsStore` `signalStore` + `RewardsApi` `getRewards/getMyRedemptions/redeem` `X-Idempotency-Key` `idemp-redeem-{rewardId}` `sessionStorage` per `rewardId` + 3 componentes standalone `rewards-catalog`/`reward-detail`/`redemption-history` + rutas `/rewards` protegidas; reutiliza `oroclash-api` slices `GetRewards` `RedeemReward` `GetPlayerRedemptions` + dominio `Reward.ReserveStock`/`Game.ConsumePoints` ledger `REWARD_REDEMPTION`/`CONSOLATION` ya existentes; sin nuevo agregado dominio, solo proyecciones `Available/Required/Remaining/Status` per `sub`.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Angular SPA separado Blazor Admin (ya en 027) | Mandato SPEC-027/028/029/032/035/036 cinematic Player vs Admin; Angular 22 + SignalStore ya justificado | Reusar Blazor Admin rompería FR-012 `data-theme="player"` cinematic y mandato Angular 22 |
| NgRx SignalStore para RewardsCatalog/Redeem 2 pasos per RewardId | Mandato nota 4 SPEC-027 + `PlayerRewardsStore` con `redeem()` `rxMethod` `X-Idempotency-Key` `idemp-redeem-{rewardId}` + `patchState` `redeemStatus` `remainingPoints` ledger; 2 pasos `showConfirm` + `Confirmar` ≥44px + `history` paginado derivados | `BehaviorSubject` manual duplica sincronización, carece de `DeepSignal` + `tapResponse` + computed `isRedeemable`/`remainingPoints` |
| Design System `data-theme="player"` tokens sin literales + 4 métricas Available/Required/Remaining/Status | FR-002/003/010 `data-theme="player"` cinematic premium WCAG AA 375-1536 + SC-001 4 métricas tokens (`--color-success` `warning` `destructive`) requieren tokens centralizados | Estilos literales por métrica rompen `design-system/MASTER.md` y no pasan axe/Lighthouse |

