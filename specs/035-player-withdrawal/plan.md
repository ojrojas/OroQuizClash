# Implementation Plan: Player Withdrawal

**Branch**: `035-player-withdrawal` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/035-player-withdrawal/spec.md`

## Summary

Flujo de retiro voluntario `Withdrawal Action` → diálogo modal con 3 métricas autoritativas (`Current Points`, `Secured Points` · checkpoint, `Potential Points` “—” si no configurado) + 2 warnings _"If you continue..."_ y _"Withdraw now and secure X points?"_ (X=`Secured`) → confirmación 2 pasos `role="dialog"` `aria-modal` → `POST /api/games/{id}/withdraw` `X-Idempotency-Key` `sessionStorage` per `gameId` `Authorization Bearer` → `GameSession` `WITHDRAWN` `RowVersion` per `GamePlayerId` `isTerminal true` `canAnswer false` `Current` → `Secured` (`KEEP_SECURED_SCORE`) con `PointTransaction` `WITHDRAWAL` ledger idempotente sin duplicar. Extiende `QuizArena.Player` Angular 22 SPA (SPEC-027/029/032/033) con `WithdrawalComponent` `app-withdrawal` + `PlayerGameStore` `withdraw()` `rxMethod` ya en 029 + `GameComponent` `Withdrawal Action` `showWithdrawConfirm` `min-height:44px` + `GamesApi.withdraw` `X-Idempotency-Key` + `GameRealtimeService` `isTerminal` block `QuestionComponent` `aria-disabled`, `design-system/tokens` `data-theme="player"` `prefers-reduced-motion`, OIDC PKCE.

## Technical Context

**Language/Version**: TypeScript 5.8+ / **Angular 22** (standalone `input()` `signal()` `computed()` `@if/@for`, `provideRouter`, `HttpClient withFetch` `withInterceptors`, `rxMethod`) Node 22 LTS + C# 12 / .NET 10.0 (`net10.0`) para `oroclash-api` slices ya existentes (`WithdrawPlayer` `POST /withdraw` `X-Idempotency-Key` + `GetMyPlayerState`).

**Primary Dependencies**: `@angular/core/router/common/forms` 22.x, `@ngrx/signals` 22.x + `rxjs 7.x` (`rxMethod`, `tapResponse`, `switchMap`), `angular-auth-oidc-client` 17+ PKCE `authorization_code`+`refresh_token`, `@microsoft/signalr` 8.x `GameHub` `GameFinished` `withAutomaticReconnect`, `design-system/tokens/design-tokens.css` (`data-theme="player"`); Backend `BuildingBlocks.Kernel.Domain` (`AggregateRoot`, `IBusinessRule`, `Result`, `Enumeration`), `BuildingBlocks.CQRS` (`ICommand` `WithdrawPlayer` `ISender`), `BuildingBlocks.Kernel.Infrastructure` (`AppDbContextBase`, `EfRepository`, `IUnitOfWork`, `Outbox`), `BuildingBlocks.ServiceDefaults` (OTel, health, `IEndpoint`, `GlobalExceptionHandler`).

**Storage**: SQL Server primaria `oroclash` (`Game` RowVersion + `GamePlayer` `RowVersion` per `GamePlayerId` + `GameRound` + `Answer` `UNIQUE (GameId,RoundId,PlayerId)` + `PointTransaction` ledger `WITHDRAWAL` `UNIQUE (GameId,PlayerId,CreatedAt)` + Outbox); PostgreSQL `identitydb` aislada OroIdentityServer; cliente solo memoria `PlayerGameStore` `DeepSignal` `{score, securedPoints, game, gameSession, status}` + `sessionStorage` efímero `idemp-withdraw-{gameId}` `X-Idempotency-Key` per `GameId` nunca `localStorage`.

**Testing**: Vitest 3 + Angular Testing Library + `provideHttpClientTesting` + `TestBed` para `WithdrawalComponent` (3 métricas `Current/Secured/Potential` + 2 warnings exactos + `role="dialog"` `aria-modal` `Confirmar` `X-Idempotency-Key` `Cancel`/`Escape` no llamada, `WITHDRAWN` `isTerminal` `canAnswer false` `aria-disabled`, `prefers-reduced-motion`) y `PlayerGameStore` `withdraw()` idempotente `Secured 200` `RowVersion++`; xUnit v3 + NSubstitute + Testcontainers.MsSql para `WithdrawPlayer` `KEEP_SECURED_SCORE` + `PlayerAlreadyWithdrawn 403` + `InvalidGameState 400` + `Current=Secured` ledger; `dotnet test` TestingPlatform, `ng test --watch=false`, `ng lint` `@ngrx/eslint-plugin`, axe/Lighthouse WCAG.

**Target Platform**: Web SPA evergreen Chrome/Edge/Firefox/Safari responsive 375–1536 WCAG 2.2 AA, `ng serve` dev `ng build` prod `dist/` hosteado via `QuizArena.Player` static files o container `node:22-alpine` orquestado por `OroQuizClash.AppHost` (sqlserver/postgres/redis/rabbitmq/identity-api/oroclash-api/quizarena-player).

**Project Type**: web-application (Angular SPA `src/Player/QuizArena.Player` + modular monolith `Domain/Application/Infrastructure/Api`).

**Performance Goals**: SC-004 100% idempotente `X-Idempotency-Key` sin duplicar `WITHDRAWAL` ledger; SC-005 100% `WITHDRAWN` `isTerminal true` `canAnswer false` bloquea `POST /answers` 403; SC-006 `Current=Secured` 100% `KEEP_SECURED_SCORE`; SC-007 responsive 375-1536 sin scroll 100% 1col/2col/4col targets ≥44px; SC-007 axe 0 violations.

**Constraints**: Constitución V server truth (`Secured` solo vía `GET /players/me` ledger `WITHDRAWAL` `deduction` per `WithdrawalPolicy`, `RowVersion` per `GamePlayerId` decide, SignalR `GameFinished` no fuente veredicto); VI OroIdentityServer PKCE `jwks_uri` `sub=PlayerId` `must_change_password` gating; H `secureRoutes=[apiUrl]` Bearer solo `oroclash-api` + `X-Correlation-Id` + `PlayerAlreadyWithdrawn 403` audit; I RFC7807 `ProblemDetails` `CorrelationId/TraceId`; J REST `IEndpoint` thin `ISender`; WCAG `role="dialog"` `aria-modal` `aria-live assertive/polte` `outline:2px` `prefers-reduced-motion`; Design System `data-theme="player"` sin literales diálogo `var(--space-*)` 3 métricas + 2 warnings tokens (`--color-warning` `destructive`).

**Scale/Scope**: 1 `Withdrawal Action` por jugador por partida, `Secured Points` 0..N `checkpoint 1..MaxRounds` or null, `Potential Points` opcional, `X-Idempotency-Key` UUID per `gameId` `sessionStorage` `idemp-withdraw-{gameId}`, `MaxPlayers` 10 default aislados per `GameSession` (F).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Evidencia |
|-----------|--------|-----------|
| I. Domain First | ✅ PASS | Retiro es `Game.WithdrawPlayer(playerId)` dominio con `IBusinessRule` `PlayerNotWithdrawn` + `PlayerAlreadyEliminated` + `WithdrawalPolicy` `KEEP_SECURED_SCORE` → `Score` `Current=Secured` + `PointTransaction` `WITHDRAWAL` (SPEC-008 C). `WithdrawalComponent` no contiene lógica de deducción autoritativa. |
| II. Clean Architecture | ✅ PASS | `Player (Angular WithdrawalComponent)` → `oroclash-api WithdrawPlayer ICommand` → `Application→Domain←Infrastructure`. Domain no referencia Angular. `WithdrawalAction` es view-model. |
| III. BuildingBlocks No Reinvention | ✅ PASS | Reusa `AggregateRoot/Result/IBusinessRule/Specification/IRepository/IEndpoint/ISender/AppDbContextBase/Outbox/ServiceDefaults` ya en 008/029. No MediatR/MassTransit/AutoMapper. Solo Angular `@ngrx/signals` (permitido frontend). |
| IV. Vertical Slice + CQRS | ✅ PASS | Reusa slice `WithdrawPlayer` (`Command` + `Validator` + `Handler` + `Response DTO` + `IEndpoint` thin `ISender`) + `GetMyPlayerState` `Score/SecuredPoints` para diálogo 3 métricas. Sin carpeta genérica. |
| V. Server Truth | ✅ PASS | `Secured` `Current` `Potential` solo vía `GET /players/me` ledger; `Withdraw` `deduction` per `WithdrawalPolicy` server `RowVersion` per `GamePlayerId`; `isTerminal`/`canAnswer` solo vía `GET /players/me` `WITHDRAWN`; cliente nunca calcula `Secured` X para warning. |
| VI. OroIdentityServer | ✅ PASS | PKCE `authorization_code`+`refresh_token` contra `/.well-known/openid-configuration`, `jwks_uri`, `sub=PlayerId`, `must_change_password` guard, sin user store local. `POST /withdraw` requiere JWT. |
| A. Game Lifecycle | ✅ PASS | `WITHDRAWN` es estado terminal `PlayerParticipationStatus` `IsTerminal true` per `GamePlayer` no global `Game`; `GameStatus` `FINISHED` no requerido para `Withdraw`; `Withdraw` prohibido si `IsTerminal` ya. |
| C. Configurable Rules | ✅ PASS | `WithdrawalPolicy` `KEEP_SECURED_SCORE` etc. no hardcodeado, solo proyección `Secured` X para warning "Withdraw now and secure X points?"; `LossPolicy` no hardcodeado para `Potential`. |
| D. Ledger | ✅ PASS | `WITHDRAWN` genera `PointTransaction` `WITHDRAWAL` ledger `deduction` `ResultingBalance` reconstruible `Current=Secured`; cliente nunca calcula `deduction`. |
| F. Concurrency/Idempotency | ✅ PASS | `RowVersion` per `GamePlayerId` + `X-Idempotency-Key` `idemp-withdraw-{gameId}` `UNIQUE` per `GamePlayer` → `PlayerAlreadyWithdrawn` idempotente 200 sin duplicar ledger; `Answer` per `playerId` no colisiona. |
| G. Realtime/Outbox | ✅ PASS | `GameHub` `PlayerWithdrawn` (o `GameFinished` con `WITHDRAWN`) → `hydrate` `GET /players/me` `WITHDRAWN`; Outbox→RabbitMQ nunca antes commit; `WithdrawalComponent` no muta desde evento directo. |
| H. Security Delegated | ✅ PASS | JWT `jwks_uri`, `PlayerIdentityMismatch` 403 auditada si `sub` intenta `Withdraw` de otro, `X-Correlation-Id` prop., `Secured` nunca manipulable cliente. |
| I. Validation/Errors/Obs | ✅ PASS | 3 niveles, RFC7807 `ProblemDetails` `PlayerAlreadyWithdrawn 403`/`PlayerAlreadyEliminated 403`/`InvalidGameState 400` con `CorrelationId/TraceId`, OTel `CorrelationId/TraceId/GameId/PlayerId`. |
| J. API & Frontend | ✅ PASS | REST `POST /api/games/{id}/withdraw` `X-Idempotency-Key` + `GET /api/games/{id}/players/me` 3 métricas, DTOs boundary, `RequireAuthorization`, frontend `app-withdrawal` `role="dialog"` presentation-only PKCE. |

**Resultado pre-Phase 0: PASS — sin violaciones.**

## Project Structure

### Documentation (this feature)

```text
specs/035-player-withdrawal/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── api-contracts.md       # POST /withdraw X-Idempotency-Key + GET /players/me 3 métricas
│   └── ui-contracts.md        # Withdrawal Action → diálogo 3 métricas + 2 warnings + Confirmar/Cancel, PlayerWithdrawn terminal, data-theme player
├── checklists/requirements.md
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/Player/QuizArena.Player/         # Angular 22 SPA (standalone) — extends SPEC-027 (Player App) + 029 (Game) + 032 (Scoring)
├── src/app/
│   ├── app.routes.ts                # /game/:gameId (authGuard, mustChangePasswordGuard) already
│   ├── app.config.ts                # provideAuth PKCE, HttpClient interceptors already
│   ├── core/interceptors/           # correlationId, auth secureRoutes, error RFC7807 already
│   ├── core/realtime/               # game-realtime.service.ts withAutomaticReconnect → hydrate already (PlayerWithdrawn)
│   ├── stores/
│   │   └── player-game.store.ts     # 10 elementos already (029) {game, gameSession, round, question, answer, score, securedPoints, timer, status} + withdraw() rxMethod POST /withdraw X-Idempotency-Key idemp-withdraw-{gameId}, hydrate GET /players/me 3 métricas
│   ├── features/game/
│   │   ├── game.component.ts        # EXTEND: Withdrawal Action botón min-height:44px + diálogo modal showWithdrawConfirm role="dialog" aria-modal 3 métricas Current/Secured/Potential + 2 warnings + Confirmar/Cancel
│   │   └── withdrawal.component.ts  # EXTEND: diálogo WithdrawalComponent app-withdrawal standalone con 3 métricas + warnings + Confirmar (≥44px) + X-Idempotency-Key, isTerminal block
│   │   └── score-panel.component.ts # already (032) 5 métricas Current/Secured/Potential
│   └── features/shared/             # games.api.ts withdraw(gameId, idempotencyKey) + getMyState 3 métricas already
│   └── shared/ui/                   # loading-skeleton, empty-state, error-state (CorrelationId) already reuse
├── src/environments/                # environment.apiUrl, identityAuthority, gameHubUrl already
tests/ (Vitest)
└── src/app/
    ├── stores/player-game.store.spec.ts # withdraw() idempotente PlayerWithdrawn isTerminal canAnswer false Secured 200 RowVersion++
    └── features/game/withdrawal.component.spec.ts # diálogo 3 métricas Current/Secured/Potential + warnings exactos + Confirmar X-Idempotency-Key + Cancel/Escape no llamada + PlayerWithdrawn terminal
```

**Structure Decision**: Extender SPA `QuizArena.Player` existente (`PlayerGameStore` `withdraw()` `rxMethod` `POST /withdraw` `X-Idempotency-Key` `idemp-withdraw-{gameId}` ya en 029 + `GameComponent` `Withdrawal Action` `showWithdrawConfirm` `role="dialog"` `min-height:44px` ya en 029) con `WithdrawalComponent` `app-withdrawal` `3 métricas` `Current/Secured/Potential` + `2 warnings` `role="alert"` + `Confirmar/Cancel` `≥44px` `Confirmar` → `POST /withdraw` `X-Idempotency-Key` `sessionStorage` per `gameId` `PlayerWithdrawn` `isTerminal true` `canAnswer false` `Current=Secured` (`KEEP_SECURED_SCORE`) ledger idempotente; reutiliza `oroclash-api` `WithdrawPlayer` `X-Idempotency-Key` `PlayerAlreadyWithdrawn` `RowVersion` per `GamePlayerId` + `GetMyPlayerState` 3 métricas + `GameHub` `PlayerWithdrawn`→`hydrate` (Server Truth V, `Secured` X nunca calculado cliente); no nuevo agregado dominio.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Angular SPA separado Blazor Admin (ya en 027) | Mandato SPEC-027/029/032/033/035 cinematic Player vs Admin; Angular 22 + SignalStore ya justificado | Reusar Blazor Admin rompería FR-007 `data-theme="player"` cinematic y mandato Angular 22 |
| NgRx SignalStore para WithdrawalAction 2 pasos per GameSession | Mandato nota 4 SPEC-027 + `PlayerGameStore` con `withdraw()` `rxMethod` `X-Idempotency-Key` `idemp-withdraw-{gameId}` + `patchState` `isTerminal`/`canAnswer` `RowVersion` ledger; 2 pasos `showWithdrawConfirm` + `Confirmar` ≥44px derivados | `BehaviorSubject` manual duplica sincronización, carece de `DeepSignal` + `tapResponse` + computed `isTerminal` |
| SignalR `GameHub` `withAutomaticReconnect` → `hydrate` para Withdrawn | Realtime obligatorio para `PlayerWithdrawn` `isTerminal` block `QuestionComponent` `aria-disabled`; polling aumenta latencia y no escala | Polling REST sin SignalR no notifica `PlayerWithdrawn` sin delay; trusting event payload para `isTerminal` viola V |
| Design System `data-theme="player"` tokens sin literales + diálogo 3 métricas + warnings | FR-007 cinematic premium WCAG AA 375-1536 + SC-007 3 métricas + 2 warnings tokens (`--color-warning` `destructive`) requieren tokens centralizados | Estilos literales por métrica rompen `design-system/MASTER.md` y no pasan axe/Lighthouse |
