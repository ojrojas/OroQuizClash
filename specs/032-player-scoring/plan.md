# Implementation Plan: Player Scoring

**Branch**: `032-player-scoring` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/032-player-scoring/spec.md`

## Summary

Mostrar las cinco métricas autoritativas de puntuación — `Current Points`, `Secured Points`, `Potential Points`, `Round Points`, `Total Points` — en `QuizArena.Player` Angular 22 SPA (SPEC-027/029/030/031) como proyección de solo lectura de `PointTransaction` ledger server-side, actualizadas vía `GameRealtimeService` `ScoreUpdated/RoundCompleted/Reconnected → hydrate` (SPEC-012, Server Truth V). Extiende `ScorePanelComponent`/`PlayerGameStore` (10 elementos ya en 029) con `ScoringDisplay` `data-theme="player"` tokens cinematic, `aria-live="polite"` por métrica, responsive footer `280px 1fr` + center `Question` (031) sin cálculo cliente, `RowVersion` + idempotencia ledger ya existente (D/F), OIDC PKCE OroIdentityServer.

## Technical Context

**Language/Version**: TypeScript 5.8+ / **Angular 22** (standalone `input()` `signal()` `computed()` `@if/@for`, `provideRouter`, `HttpClient withFetch` `withInterceptors`, `rxMethod`) Node 22 LTS + C# 12 / .NET 10.0 (`net10.0`) para `oroclash-api` slices ya existentes (`GetMyPlayerState`/`GetPlayerScore`).

**Primary Dependencies**: `@angular/core/router/common/forms` 22.x, `@ngrx/signals` 22.x + `rxjs 7.x` (`rxMethod`, `tapResponse`, `debounceTime`, `switchMap`), `angular-auth-oidc-client` 17+ PKCE `authorization_code`+`refresh_token`, `@microsoft/signalr` 8.x `GameHub` `ScoreUpdated/RoundCompleted/GameFinished` `withAutomaticReconnect`, `design-system/tokens/design-tokens.css` (`data-theme="player"`); Backend `BuildingBlocks.Kernel.Domain` (`AggregateRoot`, `IBusinessRule`, `Result`, `Enumeration`), `BuildingBlocks.CQRS` (`IQuery` `GetMyPlayerState` `ISender`), `BuildingBlocks.Kernel.Infrastructure` (`AppDbContextBase`, `EfRepository`, `IUnitOfWork`, `Outbox`), `BuildingBlocks.ServiceDefaults` (OTel, health, `IEndpoint`, `GlobalExceptionHandler`).

**Storage**: SQL Server primaria `oroclash` (`Game` RowVersion + `GameRound` + `PointTransaction` ledger `UNIQUE (GameId,PlayerId,CreatedAt)` + `Answer` + `Reward` Outbox); PostgreSQL `identitydb` aislada OroIdentityServer; cliente solo memoria `PlayerGameStore` `DeepSignal` `{score, securedPoints, game.configuration, pointTransactions}` + `computed potentialReward/roundPoints/totalPoints` nunca `localStorage`.

**Testing**: Vitest 3 + Angular Testing Library + `provideHttpClientTesting` + `TestBed` para `ScorePanelComponent` (5 métricas autoritativas, `Secured checkpoint 3` vs null, `Potential "—"` fallback, `Round Points` "en juego", realtime `ScoreUpdated→hydrate` <1s, `aria-live polite`, `prefers-reduced-motion`) y `PlayerGameStore` (`score/ securedPoints` hydrate, `potentialReward` computed); xUnit v3 + NSubstitute + Testcontainers.MsSql para `GetMyPlayerState` ledger suma `totalPoints` + `ScoreUpdated` idempotencia + `TotalPoints` server-side; `dotnet test` TestingPlatform, `ng test --watch=false`, `ng lint` `@ngrx/eslint-plugin`, axe/Lighthouse WCAG.

**Target Platform**: Web SPA evergreen Chrome/Edge/Firefox/Safari responsive 375–1536 WCAG 2.2 AA, `ng serve` dev `ng build` prod `dist/` hosteado via `QuizArena.Player` static files o container `node:22-alpine` orquestado por `OroQuizClash.AppHost` (sqlserver/postgres/redis/rabbitmq/identity-api/oroclash-api/quizarena-player).

**Project Type**: web-application (Angular SPA `src/Player/QuizArena.Player` + modular monolith `Domain/Application/Infrastructure/Api`).

**Performance Goals**: SC-002 100% actualización <1s tras `ScoreUpdated/RoundCompleted` vía `hydrate`; SC-001 100% 5 métricas coinciden con ledger 0% cálculo cliente; SC-003 0% mutación cliente aceptada; SC-006 responsive 375-1536 sin scroll 100% 1col/2x2 targets ≥44px; SC-007 axe 0 violations 100%.

**Constraints**: Constitución V server truth (`Current/Secured/Round/Total` solo vía `GET /players/me` ledger, SignalR nunca fuente veredicto, `submittedAt<=expiresAt` decide `EXPIRED` pero no aplica a scoring); VI OroIdentityServer PKCE `jwks_uri` `sub=PlayerId` `must_change_password` gating; H `secureRoutes=[apiUrl]` Bearer solo `oroclash-api` + `X-Correlation-Id`; I RFC7807 `ProblemDetails` `CorrelationId/TraceId`; J REST `IEndpoint` thin `ISender`; WCAG `aria-live polite` `outline:2px` `prefers-reduced-motion`; Design System `data-theme="player"` sin literales 5 métricas tokens (`--color-primary`).

**Scale/Scope**: 5 métricas por jugador por partida, N rondas `MaxRounds` 5–15 default 10, `PointsPerRound` 100 default * dificultad 1..5, N jugadores por juego `MaxPlayers` 10 default aislados per `GameSession` (F), `Secured checkpoint` 1..N o null, `Potential Points` opcional.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Evidencia |
|-----------|--------|-----------|
| I. Domain First | ✅ PASS | Puntuación es `Game.AwardPoints/RemovePoints/SecurePoints/ConsumePoints` dominio con `IBusinessRule` `SufficientBalance` + `LossPolicy` + `WithdrawalPolicy` (SPEC-007 D). `ScorePanelComponent` no contiene cálculo autoritativo, solo proyección `Score`/`SecuredPoints` de `GetMyPlayerState`. |
| II. Clean Architecture | ✅ PASS | `Player (Angular ScorePanelComponent)` → `oroclash-api GetMyPlayerState IQuery` → `Application→Domain←Infrastructure`. Domain no referencia Angular. `ScoringDisplay` es view-model. |
| III. BuildingBlocks No Reinvention | ✅ PASS | Reusa `AggregateRoot/Result/IBusinessRule/Specification/IRepository/IEndpoint/ISender/AppDbContextBase/Outbox/ServiceDefaults` ya en 007/029. No MediatR/MassTransit/AutoMapper. Solo Angular `@ngrx/signals` (permitido frontend). |
| IV. Vertical Slice + CQRS | ✅ PASS | Reusa slice `GetMyPlayerState` (`Query` + `Handler` + `Response DTO` + `IEndpoint` thin `ISender`) y `GetPlayerScore` Query. Sin carpeta genérica. |
| V. Server Truth | ✅ PASS | `Current/Secured/Round/Total` solo vía `GET /players/me` ledger (`sum(PointTransaction)=totalPoints`); `ScoreUpdated`/`RoundCompleted` solo disparan `hydrate` `GET /players/me` con `serverNow` corrección; cliente nunca incrementa `Current Points` localmente; `Total Points` no sumado cliente. |
| VI. OroIdentityServer | ✅ PASS | PKCE `authorization_code`+`refresh_token` contra `/.well-known/openid-configuration`, `jwks_uri`, `sub=PlayerId`, `must_change_password` guard, sin user store local. `GET /players/me` requiere JWT. |
| A. Game Lifecycle | ✅ PASS | 9 estados `WAITING→IN_PROGRESS→ROUND_IN_PROGRESS↔ROUND_COMPLETED→FINISHED` protegidos; `isTerminal` bloquea visualización mutación pero scoring sigue visible final. |
| C. Configurable Rules | ✅ PASS | `PointsPerRound` 100, `LossPolicy`/`WithdrawalPolicy`/`RewardRules` no hardcodeados, solo proyección `Potential Points`; `TimeLimit` inmutable tras Start no aplica a scoring. |
| D. Ledger | ✅ PASS | 5 métricas `Current/Secured/Round/Potential/Total` derivadas de `PointTransaction` ledger `ANSWER_CORRECT/INCORRECT/ROUND_BONUS` etc. reconstruible `sum=total`; cliente nunca calcula `Current Points`. |
| F. Concurrency/Idempotency | ✅ PASS | `RowVersion` en `Game`/`GamePlayer`, `UNIQUE IdempotencyKey` + `UNIQUE (GameId,RoundId,PlayerId)` en `Answer`; scoring via `PointTransaction` idempotente por `Outbox` + `Answer` duplicate no duplica puntos; `RowVersion` protege `Score`. |
| G. Realtime/Outbox | ✅ PASS | `GameHub` `ScoreUpdated`/`RoundCompleted`/`Reconnected` → `hydrate` `GET /players/me`; Outbox→RabbitMQ nunca antes commit; `ScorePanel` no muta desde evento directo. |
| H. Security Delegated | ✅ PASS | JWT `jwks_uri`, `PlayerIdentityMismatch` 403 auditada, `X-Correlation-Id` prop., payload `Score` nunca manipulable cliente. |
| I. Validation/Errors/Obs | ✅ PASS | 3 niveles, RFC7807 `ProblemDetails` con `CorrelationId/TraceId` para `GET /players/me` 401/403/404, OTel `CorrelationId/TraceId/GameId/PlayerId`. |
| J. API & Frontend | ✅ PASS | REST `GET /api/games/{id}/players/me` con `Score/SecuredPoints` + `GET /api/games/{id}/rounds/current` etc. filtrado, DTOs boundary, `RequireAuthorization`, frontend presentation-only PKCE. |

**Resultado pre-Phase 0: PASS — sin violaciones.**

## Project Structure

### Documentation (this feature)

```text
specs/032-player-scoring/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── api-contracts.md       # GET /api/games/{id}/players/me (Score+SecuredPoints+Total) + GET /players/score
│   └── ui-contracts.md        # 5 métricas Current/Secured/Potential/Round/Total, realtime ScoreUpdated→hydrate, data-theme player a11y responsive
├── checklists/requirements.md
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/Player/QuizArena.Player/         # Angular 22 SPA (standalone) — extends SPEC-027 (Player App) + 029 (Game) + 030 (Rounds) + 031 (Answering)
├── src/app/
│   ├── app.routes.ts                # /game/:gameId (authGuard, mustChangePasswordGuard) already
│   ├── app.config.ts                # provideAuth PKCE, HttpClient interceptors already
│   ├── core/interceptors/           # correlationId, auth secureRoutes, error RFC7807 already
│   ├── core/realtime/               # game-realtime.service.ts withAutomaticReconnect → hydrate already (ScoreUpdated/RoundCompleted/Reconnected)
│   ├── stores/
│   │   ├── player-game.store.ts     # 10 elementos already (029): {player, game, gameSession, round, question, answer, score, securedPoints, timer, status} + computed potentialReward/roundPoints/totalPoints + hydrate GET /players/me, bindRealtime ScoreUpdated
│   │   └── player-rounds.store.ts   # Ladder Round 1..N already (030) — Secured checkpoint
│   ├── features/game/
│   │   ├── game.component.ts        # EXTEND: footer competitivo con 5 métricas scoring via <app-score-panel> + grid 280px 1fr (030) + center question 031
│   │   ├── score-panel.component.ts # EXTEND: muestra Current Points (Score.totalPoints) + Secured Points (SecuredPoints.securedPoints · checkpoint) + Potential Points (potentialReward) + Round Points (roundPoints) + Total Points (totalPoints) con aria-live polite, pulse prefers-reduced-motion, tokens data-theme player
│   │   ├── score-panel.component.css # NEW/EXTEND: tokens data-theme player var(--space-*/--color-*) footer grid 1col 375 / 2col ≥768 gap var(--space-3), min-height 44px, Secured badge, Round "en juego", Total bold
│   │   ├── timer.component.ts       # already (029) — no scoring pero comparte footer
│   │   └── player-rounds.component.ts # already (030)
│   └── features/shared/             # games.api.ts getMyState already (Score+SecuredPoints) + getPlayerScore if needed
│   └── shared/ui/                   # loading-skeleton, empty-state, error-state (CorrelationId) already reuse
├── src/environments/                # environment.apiUrl, identityAuthority, gameHubUrl already
tests/ (Vitest)
└── src/app/
    ├── stores/player-game.store.spec.ts # hydrate scoring 5 métricas, potentialReward "—" fallback, RoundPoints reset, Secured checkpoint null vs 3, realtime ScoreUpdated→hydrate
    └── features/game/score-panel.component.spec.ts # 5 métricas Current/Secured/Potential/Round/Total aria-live, checkpoint null vs 3, Potential "—", responsive 375/768, axe, prefers-reduced-motion

src/OroQuizClash.Domain/              # No changes (Game.Scoring via PointTransaction, Score, SecuredPoints)
src/OroQuizClash.Application/
└── Features/Games/
    ├── GetMyPlayerState.cs          # Query already — returns Score/SecuredPoints/Timer/Status for scoring hydrate (TotalPoints = sum PointTransaction)
    ├── GetPlayerScore.cs            # Query optional — returns PlayerScore ledger
    └── GetGame.cs                   # Query — returns GameConfiguration PointsPerRound para Potential
src/OroQuizClash.Infrastructure/
└── Persistence/Configurations/      # already (PointTransaction ledger, Game RowVersion, Outbox)
src/OroQuizClash.Api/
├── Program.cs                       # AddCqrs, AddDbContext, JWT identity Authority jwks_uri already
└── appsettings.json
OroQuizClash.AppHost/AppHost.cs      # quizarena-player container node:22-alpine → oroclash-api + identity-api already (SPEC-027)
tests/
├── OroQuizClash.Domain.Tests/       # Scoring ledger sum, AwardPoints/RemovePoints/SecurePoints, TotalPoints reconstruct
├── OroQuizClash.Application.Tests/  # GetMyPlayerStateHandler Score/SecuredPoints, PotentialPoints derivation
├── OroQuizClash.Api.Tests/          # Contract GET /players/me 5 métricas, ScoreUpdated idempotente, TotalPoints server-side
└── OroQuizClash.Architecture.Tests/ # Domain ↛ Angular, GetMyPlayerState uses sub, no client score calc
```

**Structure Decision**: Extender SPA `QuizArena.Player` existente (`PlayerGameStore` 10 elementos + `GameRealtimeService` + `GamesApi.getMyState` ya en 027/029/030/031) con `ScorePanelComponent` para 5 métricas `Current/Secured/Potential/Round/Total` (`aria-live polite` + `pulse` `prefers-reduced-motion` + `data-theme player` tokens) + `PlayerGameStore` `computed potentialReward/roundPoints/totalPoints` + realtime `ScoreUpdated/RoundCompleted/Reconnected → hydrate` (Server Truth V, `Total Points` nunca sumado cliente); reutiliza `oroclash-api` `GetMyPlayerState` ledger + `GameHub` → `hydrate` y `OroQuizClash.AppHost` ya orquesta todo; no nuevo agregado dominio.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Angular SPA separado Blazor Admin (ya en 027) | Mandato SPEC-027/029/030/031/032 cinematic Player vs Admin; Angular 22 + SignalStore ya justificado | Reusar Blazor Admin rompería FR-008 `data-theme="player"` cinematic y mandato Angular 22 |
| NgRx SignalStore para scoring 5 métricas per GameSession | Mandato nota 4 SPEC-027 + `PlayerGameStore` con `computed potentialReward/roundPoints/totalPoints` + `rxMethod hydrate` + `patchState` ledger; 5 métricas + Secured checkpoint + Potential fallback + Round reset derivados | `BehaviorSubject` manual duplica sincronización, carece de `DeepSignal` + `tapResponse` + debounce + computed memoization |
| SignalR `GameHub` `withAutomaticReconnect` → `hydrate` para scoring | Realtime obligatorio para `ScoreUpdated` tras `Correct` y `RoundCompleted` Secured; polling aumenta latencia y no escala | Polling REST sin SignalR no notifica Score sin delay; trusting event payload para `Current Points` viola V |
| Design System `data-theme="player"` tokens sin literales + 5 métricas | FR-007/008 cinematic premium WCAG AA 375-1536 + SC-006/007 5 métricas tokens (`--color-primary`) requieren tokens centralizados | Estilos literales por métrica rompen `design-system/MASTER.md` y no pasan axe/Lighthouse |
