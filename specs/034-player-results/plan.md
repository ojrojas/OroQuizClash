# Implementation Plan: Player Results

**Branch**: `034-player-results` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/034-player-results/spec.md`

## Summary

Cuatro pantallas finales autoritativas `YOU WON` (`WINNER` Rank 1 `Final Score` + `Prize` confetti), `YOU WALKED AWAY` (`WITHDRAWN` `Secured Points` · checkpoint + `Available Rewards` `pointsRequired <= Secured`), `GAME OVER` (`ELIMINATED` `Final Score` + `Consolation Reward` `CONSOLATION` si `ConsolationPolicy` cumple), `GAME FINISHED` (`FINISHED` posición 2..N `Final Position` + `Final Score` + `Reward` si threshold) en `QuizArena.Player` Angular 22 SPA (SPEC-027/029/032/033) como proyección de sólo lectura de `GetMyPlayerState` per `sub` + `GetLeaderboard` `Rank`/`Prize`/`Consolation` (Server Truth V), `ResultComponent` `app-result` `route /player/game/:gameId/result` con `authGuard` + redirect si `!IsTerminal`, `data-theme="player"` tokens 4 gradientes cinematic `prefers-reduced-motion`, `RowVersion` per `GamePlayer` + ledger `sum(PointTransaction)`.

## Technical Context

**Language/Version**: TypeScript 5.8+ / **Angular 22** (standalone `input()` `signal()` `computed()` `@if/@for`, `provideRouter`, `HttpClient withFetch` `withInterceptors`, `rxMethod`) Node 22 LTS + C# 12 / .NET 10.0 (`net10.0`) para `oroclash-api` slices ya existentes (`GetMyPlayerState` per `sub`, `GetLeaderboard` `Rank`, `GetGame` `GameStatus`).

**Primary Dependencies**: `@angular/core/router/common/forms` 22.x, `@ngrx/signals` 22.x + `rxjs 7.x` (`rxMethod`, `tapResponse`, `switchMap`), `angular-auth-oidc-client` 17+ PKCE `authorization_code`+`refresh_token`, `@microsoft/signalr` 8.x `GameHub` `GameFinished` `withAutomaticReconnect`, `design-system/tokens/design-tokens.css` (`data-theme="player"`); Backend `BuildingBlocks.Kernel.Domain` (`AggregateRoot`, `IBusinessRule`, `Result`, `Enumeration`), `BuildingBlocks.CQRS` (`IQuery` `GetMyPlayerState`/`GetLeaderboard` `ISender`), `BuildingBlocks.Kernel.Infrastructure` (`AppDbContextBase`, `EfRepository`, `IUnitOfWork`, `Outbox`), `BuildingBlocks.ServiceDefaults` (OTel, health, `IEndpoint`, `GlobalExceptionHandler`).

**Storage**: SQL Server primaria `oroclash` (`Game` RowVersion + `GamePlayer` `RowVersion` per `GamePlayerId` + `GameRound` + `Answer` `UNIQUE (GameId,RoundId,PlayerId)` + `PointTransaction` ledger `UNIQUE (GameId,PlayerId,CreatedAt)` + `Reward`/`RewardRedemption` + `Consolation` + Outbox); PostgreSQL `identitydb` aislada OroIdentityServer; cliente solo memoria `PlayerGameStore` `DeepSignal` `{score, securedPoints, game, gameSession, leaderboard}` + `computed finalScore/finalPosition/prize/consolation/availableRewards` nunca `localStorage`.

**Testing**: Vitest 3 + Angular Testing Library + `provideHttpClientTesting` + `TestBed` para `ResultComponent` (4 pantallas `YOU WON`/`YOU WALKED AWAY`/`GAME OVER`/`GAME FINISHED` per `PlayerStatus` + `Rank` 1 vs 3, `Final Score` ledger, `Prize`/`Consolation`/`Available Rewards` filtrable, redirect si `!IsTerminal`, `aria-live assertive`, `prefers-reduced-motion`) y `PlayerGameStore` (`ResultState` `WINNER/WITHDRAWN/ELIMINATED/FINISHED`); xUnit v3 + NSubstitute + Testcontainers.MsSql para `GetMyPlayerState` per `sub` `Rank` 1..N + `Prize`/`Consolation` award + `Final Score` `sum(PointTransaction)`; `dotnet test` TestingPlatform, `ng test --watch=false`, `ng lint` `@ngrx/eslint-plugin`, axe/Lighthouse WCAG.

**Target Platform**: Web SPA evergreen Chrome/Edge/Firefox/Safari responsive 375–1536 WCAG 2.2 AA, `ng serve` dev `ng build` prod `dist/` hosteado via `QuizArena.Player` static files o container `node:22-alpine` orquestado por `OroQuizClash.AppHost` (sqlserver/postgres/redis/rabbitmq/identity-api/oroclash-api/quizarena-player).

**Project Type**: web-application (Angular SPA `src/Player/QuizArena.Player` + modular monolith `Domain/Application/Infrastructure/Api`).

**Performance Goals**: SC-001 100% `YOU WON` `Final Score` ledger + `Prize` 0% leak; SC-005 100% `Final Score/Position/Prize` autoritativo 0% cliente calc; SC-006 redirect `!IsTerminal` 100% a `/game/:id`; SC-007 375-1536 sin scroll 100% 1col/2col/4col targets ≥44px; SC-007 axe 0 violations.

**Constraints**: Constitución V server truth (`Final Score` `sum(PointTransaction)` + `Final Position` `Leaderboard Rank` per `sub`, `Prize`/`Consolation` solo si `totalPoints >= pointsRequired`/`ConsolationPolicy` cumple, SignalR `GameFinished` no fuente veredicto); VI OroIdentityServer PKCE `jwks_uri` `sub=PlayerId` `must_change_password` gating; H `secureRoutes=[apiUrl]` Bearer solo `oroclash-api` + `X-Correlation-Id` + `PlayerIdentityMismatch 403` audit; I RFC7807 `ProblemDetails` `CorrelationId/TraceId`; J REST `IEndpoint` thin `ISender`; WCAG `role="status"` `aria-live assertive/polte` `outline:2px` `prefers-reduced-motion`; Design System `data-theme="player"` sin literales 4 pantallas tokens (`--color-success` `warning` `destructive` `accent`).

**Scale/Scope**: 4 estados finales (`WINNER`/`WITHDRAWN`/`ELIMINATED`/`FINISHED` 2..N), `Final Score` 0..N (`sum` ledger), `Final Position` 1..`MaxPlayers` 10 default, `Prize`/`Consolation`/`Available Rewards` filtrable `pointsRequired <= Secured`, N rondas `MaxRounds` 5–15 default 10, `ResultComponent` `route /player/game/:gameId/result` per `sub`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Evidencia |
|-----------|--------|-----------|
| I. Domain First | ✅ PASS | Resultado es `Game.Finish()` + `LeaderboardBuilder.Build(game)` + `ConsolationPolicy` + `RewardRedemption` dominio con `IBusinessRule` `GameFinished` → `Rank` 1..N + `Winner` + `Consolation` (SPEC-007/010/011). `ResultComponent` no calcula `Winner`/`Rank`/`Prize` autoritativo. |
| II. Clean Architecture | ✅ PASS | `Player (Angular ResultComponent)` → `oroclash-api GetMyPlayerState/GetLeaderboard IQuery` → `Application→Domain←Infrastructure`. Domain no referencia Angular. `ResultState` es view-model. |
| III. BuildingBlocks No Reinvention | ✅ PASS | Reusa `AggregateRoot/Result/IBusinessRule/Specification/IRepository/IEndpoint/ISender/AppDbContextBase/Outbox/ServiceDefaults` ya en 007/011. No MediatR/MassTransit/AutoMapper. Solo Angular `@ngrx/signals` (permitido frontend). |
| IV. Vertical Slice + CQRS | ✅ PASS | Reusa slices `GetMyPlayerState` per `sub` + `GetLeaderboard` público `Rank` + `GetGame` `GameStatus` (`Query`+`Handler`+`Response DTO`+`IEndpoint` thin `ISender`). Sin carpeta genérica. |
| V. Server Truth | ✅ PASS | `Final Score` `sum(PointTransaction)` + `Final Position` `Leaderboard Rank` per `sub` + `Prize`/`Consolation` `totalPoints >= pointsRequired`/`ConsolationPolicy` solo vía `GET /players/me` + `GET /leaderboard` ledger; `GameFinished` solo dispara `hydrate`; cliente nunca calcula `Rank`/`Prize`. |
| VI. OroIdentityServer | ✅ PASS | PKCE `authorization_code`+`refresh_token` contra `/.well-known/openid-configuration`, `jwks_uri`, `sub=PlayerId`, `must_change_password` guard, sin user store local. `GET /players/me` + `GET /leaderboard` requieren JWT. |
| A. Game Lifecycle | ✅ PASS | 9 estados `WAITING→IN_PROGRESS→ROUND_IN_PROGRESS↔ROUND_COMPLETED→FINISHED` protegidos; `ResultComponent` muestra exactamente 1 de 4 pantallas per `GameStatus.IsTerminal` + `PlayerStatus` `WINNER/WITHDRAWN/ELIMINATED/FINISHED` + `Rank` 1 vs 2..N; redirect si `!IsTerminal`. |
| C. Configurable Rules | ✅ PASS | `WithdrawalPolicy` `KEEP_SECURED_SCORE` etc. + `ConsolationPolicy` `FixedPoints`/`ParticipationBased`/`RewardBased` + `RewardRules` `PointsRequired` no hardcodeados, solo proyección `Available Rewards` `pointsRequired <= Secured` y `Consolation` si policy cumple. |
| D. Ledger | ✅ PASS | `Final Score` `Score.totalPoints` `sum(PointTransaction)` reconstruible; `Prize`/`Consolation` `RewardRedemption` ledger `REWARD_REDEMPTION/CONSOLATION` si aplica; cliente nunca calcula `Final Score`. |
| F. Concurrency/Idempotency | ✅ PASS | `RowVersion` per `GamePlayer` + `UNIQUE (GameId,RoundId,PlayerId)` aísla `Answer`; `Result` es `GET` idempotente no `POST`; `Leaderboard Rank` es view no mutación. |
| G. Realtime/Outbox | ✅ PASS | `GameHub` `GameFinished` → `hydrate` `GET /players/me` privado per `sub` + `GET /leaderboard` público `Rank`; Outbox→RabbitMQ nunca antes commit; `ResultComponent` no muta desde evento directo. |
| H. Security Delegated | ✅ PASS | JWT `jwks_uri`, `PlayerIdentityMismatch` 403 auditada, `X-Correlation-Id` prop., `Result` nunca incluye `Answer` de otro; `Prize`/`Consolation` per `sub` solo. |
| I. Validation/Errors/Obs | ✅ PASS | 3 niveles, RFC7807 `ProblemDetails` con `CorrelationId/TraceId` para `GET /players/me` 401/403/404 + `GET /leaderboard` 401, OTel `CorrelationId/TraceId/GameId/PlayerId/Rank`. |
| J. API & Frontend | ✅ PASS | REST `GET /api/games/{id}/players/me` privado + `GET /api/games/{id}/leaderboard` público `Rank`, DTOs boundary, `RequireAuthorization`, frontend `app-result` `route /game/:gameId/result` `authGuard` `mustChangePasswordGuard` presentation-only PKCE. |

**Resultado pre-Phase 0: PASS — sin violaciones.**

## Project Structure

### Documentation (this feature)

```text
specs/034-player-results/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── api-contracts.md       # GET /players/me privado per sub Final Score + GET /leaderboard Rank + GET /rewards Available
│   └── ui-contracts.md        # 4 pantallas YOU WON/WALKED AWAY/GAME OVER/GAME FINISHED, ResultComponent route /result, data-theme player a11y
├── checklists/requirements.md
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/Player/QuizArena.Player/         # Angular 22 SPA (standalone) — extends SPEC-027 (Player App) + 029 (Game) + 032 (Scoring) + 033 (Multiplayer)
├── src/app/
│   ├── app.routes.ts                # EXTEND: /player/game/:gameId/result `canActivate: [authGuard, mustChangePasswordGuard]` → `ResultComponent` already placeholder
│   ├── app.config.ts                # provideAuth PKCE, HttpClient interceptors already
│   ├── core/interceptors/           # correlationId, auth secureRoutes, error RFC7807 already
│   ├── core/realtime/               # game-realtime.service.ts withAutomaticReconnect → hydrate already (GameFinished)
│   ├── stores/
│   │   └── player-game.store.ts     # 10 elementos already (029) {game, gameSession, round, question, answer, score, securedPoints, timer, status} + computed resultState/WINNER/WITHDRAWN/ELIMINATED/FINISHED + Rank + hydrate GET /players/me + GET /leaderboard
│   ├── features/result/
│   │   └── result.component.ts      # EXTEND: 4 pantallas `YOU WON` (`WINNER` Rank1 Final Score+Prize confetti) / `YOU WALKED AWAY` (`WITHDRAWN` Secured + Available Rewards) / `GAME OVER` (`ELIMINATED` Final Score+Consolation) / `GAME FINISHED` (`FINISHED` Rank 2..N Final Position+Final Score+Reward) role="status" aria-live assertive, redirect si !IsTerminal
│   │   └── result.component.css     # NEW: tokens data-theme player var(--space-*/--color-*) 4 pantallas gradients var(--color-success) warning destructive accent, 1col 375 / 2col ≥768 gap var(--space-3), min-height 44px, confetti pulse prefers-reduced-motion none
│   ├── features/game/
│   │   ├── game.component.ts        # already (033) header Leaderboard + footer ScorePanel
│   │   └── score-panel.component.ts # already (032) 5 métricas
│   └── features/shared/             # games.api.ts getMyState (privado) + getLeaderboard (Rank) + getRewards (Available) already
│   └── shared/ui/                   # loading-skeleton, empty-state, error-state (CorrelationId) already reuse
├── src/environments/                # environment.apiUrl, identityAuthority, gameHubUrl already
tests/ (Vitest)
└── src/app/
    ├── stores/player-game.store.spec.ts # ResultState WINNER/WITHDRAWN/ELIMINATED/FINISHED per Rank, Final Score ledger, Final Position Rank
    └── features/result/result.component.spec.ts # 4 pantallas YOU WON/WALKED/GAME OVER/FINISHED per PlayerStatus, Final Score ledger, Prize/Consolation, Available Rewards, redirect !IsTerminal, axe

src/OroQuizClash.Domain/              # No changes (Game.Finish() Rank, LeaderboardBuilder, ConsolationPolicy, RewardRedemption)
src/OroQuizClash.Application/
└── Features/Games/
    ├── GetMyPlayerState.cs          # Query already — returns Private State per sub (Score/SecuredPoints/GameSession/GameStatus) filtrado
    ├── GetLeaderboard.cs            # Query already — returns Leaderboard Rank 1..N per sub
    └── GetGame.cs                   # Query — returns GameStatus IsTerminal
src/OroQuizClash.Infrastructure/
└── Persistence/Configurations/      # already (PointTransaction ledger, GamePlayer RowVersion, Reward/Consolation, Outbox)
src/OroQuizClash.Api/
├── Program.cs                       # AddCqrs, AddDbContext, JWT identity Authority jwks_uri already
└── appsettings.json
OroQuizClash.AppHost/AppHost.cs      # quizarena-player container node:22-alpine → oroclash-api + identity-api already (SPEC-027)
tests/
├── OroQuizClash.Domain.Tests/       # Game.Finish Rank, Leaderboard, ConsolationPolicy, Final Score ledger
├── OroQuizClash.Application.Tests/  # GetMyPlayerStateHandler per sub Final Score/Position/Prize, GetLeaderboard Rank
├── OroQuizClash.Api.Tests/          # Contract GET /players/me 4 pantallas, GET /leaderboard Rank, Final Score ledger
└── OroQuizClash.Architecture.Tests/ # Domain ↛ Angular, GetMyPlayerState uses sub, no client Winner calc
```

**Structure Decision**: Extender SPA `QuizArena.Player` existente (`ResultComponent` `app-result` `route /player/game/:gameId/result` placeholder ya en 027 + `PlayerGameStore` 10 elementos `Score/SecuredPoints/Game/Leaderboard` ya en 029/033) con 4 pantallas `YOU WON`/`YOU WALKED AWAY`/`GAME OVER`/`GAME FINISHED` per `PlayerStatus` `WINNER/WITHDRAWN/ELIMINATED/FINISHED` + `Leaderboard Rank` 1..N (`aria-live assertive` + `role="status"` `data-theme player` 4 gradients tokens `prefers-reduced-motion`) + redirect si `!IsTerminal`; reutiliza `oroclash-api` `GetMyPlayerState` per `sub` + `GetLeaderboard` `Rank`/`Prize`/`Consolation` + `GameHub GameFinished→hydrate` (Server Truth V, `Final Score` ledger nunca calculado cliente); no nuevo agregado dominio.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Angular SPA separado Blazor Admin (ya en 027) | Mandato SPEC-027/029/032/033/034 cinematic Player vs Admin; Angular 22 + SignalStore ya justificado | Reusar Blazor Admin rompería FR-012 `data-theme="player"` cinematic y mandato Angular 22 |
| NgRx SignalStore para ResultState 4 pantallas per GameSession | Mandato nota 4 SPEC-027 + `PlayerGameStore` con `computed resultState/WINNER/WITHDRAWN/ELIMINATED/FINISHED` + `Rank` + `Final Score` `sum` ledger; 4 estados + redirect `!IsTerminal` derivados | `BehaviorSubject` manual duplica sincronización, carece de `DeepSignal` + `tapResponse` + computed memoization |
| SignalR `GameHub` `withAutomaticReconnect` → `hydrate` para Result | Realtime obligatorio para `GameFinished` → `ResultComponent` `YOU WON/GAME FINISHED`; polling aumenta latencia y no escala | Polling REST sin SignalR no notifica `GameFinished` sin delay; trusting event payload para `Rank` viola V |
| Design System `data-theme="player"` tokens sin literales + 4 pantallas | FR-012 cinematic premium WCAG AA 375-1536 + SC-007 4 pantallas tokens (`--color-success` `warning` `destructive` `accent`) requieren tokens centralizados | Estilos literales por pantalla rompen `design-system/MASTER.md` y no pasan axe/Lighthouse |
