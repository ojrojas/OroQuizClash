# Implementation Plan: Player Rounds

**Branch**: `030-player-rounds` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/030-player-rounds/spec.md`

## Summary

Representación visual de progresión del jugador — escalera vertical Round 1..N (`N=maxRounds` ≥5 inmutable) con 6 estados visuales (Current Level `aria-current="step"` premium, Previous Levels `completed`, Current Reward, Next Reward, Secured Reward checkpoint con escudo, Final Reward fila N corona) y transición de ronda sincronizada con servidor (evento `RoundCompleted`/`QuestionAvailable` → `hydrate` `GET /api/games/{id}/players/me` autoritativo, nunca payload del evento, animación <400ms con `prefers-reduced-motion` y `aria-live`). Extiende `QuizArena.Player` Angular 22 SPA (SPEC-027/029) con `PlayerRoundsStore` (`signalStore` `LadderRow[]` `computed` current/next/secured/final) + `PlayerRoundsComponent` embebido en `GameComponent` (`features/game/player-rounds.component.ts`), reutilizando `GamesApi.getMyState`, `GameRealtimeService` `withAutomaticReconnect` → `hydrate`, `design-system/tokens` `data-theme="player"` cinematic, OIDC PKCE OroIdentityServer, validación server-side `RowVersion` y layout responsive 375–1536 WCAG 2.2 AA.

## Technical Context

**Language/Version**: TypeScript 5.8+ / **Angular 22** (standalone, `input()` `computed()`, `@if`/`@for`, `provideRouter`, `HttpClient withFetch` `withInterceptors`, `rxMethod`) Node 22 LTS + C# 12 / .NET 10.0 (`net10.0`) para `oroclash-api` slices ya existentes (no nuevo backend).

**Primary Dependencies**: `@angular/core/router/common/forms` 22.x, `@ngrx/signals` 18.1 + `@ngrx/signals/entities` + `rxjs 7.x` (`rxMethod`, `tapResponse`, `computed`, `interval`, `switchMap`), `angular-auth-oidc-client` 17+ PKCE `authorization_code` + `refresh_token`, `@microsoft/signalr` 8.x `GameHub` `RoundCompleted`/`QuestionAvailable`/`ScoreUpdated`/`GameFinished` `withAutomaticReconnect`, `design-system/tokens/design-tokens.css` (`data-theme="player"`); Backend `BuildingBlocks.Kernel.Domain` (Enumeration, Result, Specification), `BuildingBlocks.CQRS` (IQuery `GetMyPlayerState`/`GetGame`), `BuildingBlocks.Kernel.Infrastructure` (AppDbContextBase, EfRepository, Outbox), `BuildingBlocks.ServiceDefaults` (OTel, health, Resilience, IEndpoint, GlobalExceptionHandler).

**Storage**: SQL Server primaria `oroclash` (`Game` RowVersion + `GamePlayer`/`GameRound`/`PointTransaction` ledger `Reward` opcional Outbox, N filas RoundNumber `UNIQUE (GameId,RoundNumber)`); PostgreSQL `identitydb` aislada OroIdentityServer; cliente solo memoria `PlayerRoundsStore` `DeepSignal` + `sessionStorage` efímero `idemp-withdraw-{gameId}` para withdraw ya en 029 (ladder no requiere nuevo almacenamiento) nunca `localStorage`.

**Testing**: Vitest 3 + Angular Testing Library + `provideHttpClientTesting` + `TestBed` para `PlayerRoundsStore` (ladder N filas exactas, `aria-current`, completed/upcoming, Current/Next/Secured/Final desde ledger, transición solo tras hydrate, `prefers-reduced-motion`, `aria-live`) y `PlayerRoundsComponent` (role="list"/listitem, escudo, corona final, responsive, axe); xUnit v3 + NSubstitute + Testcontainers.MsSql para API `GetMyPlayerState` autoritativo (si ladder requiere nuevo campo, no previsto); `dotnet test` TestingPlatform, `ng test --watch=false`, `ng lint` `@ngrx/eslint-plugin`, axe/Lighthouse WCAG.

**Target Platform**: Web SPA evergreen Chrome/Edge/Firefox/Safari responsive 375–1536 WCAG 2.2 AA, `ng serve` dev `ng build` prod `dist/` hosteado via `QuizArena.Player` static files o container `node:22-alpine` orquestado por `OroQuizClash.AppHost` (sqlserver/postgres/redis/rabbitmq/identity-api/oroclash-api/quizarena-player).

**Project Type**: web-application (Angular SPA `src/Player/QuizArena.Player` + modular monolith `Domain/Application/Infrastructure/Api`).

**Performance Goals**: SC-001 100% N filas exactas `aria-current` <500ms tras hydrate; SC-003 100% Current/Next/Secured/Final coinciden ledger `sum` <1s; SC-005 100% transiciones solo tras hydrate 0% payload trust; SC-006 animación <400ms + `aria-live` 100% + `prefers-reduced-motion` 100%; SC-008 375–1536 sin scroll 100% WCAG AA 100%; SC-010 Difficulty por fila 100% autoritativo.

**Constraints**: Constitución V server truth (currentRoundNumber, Round.level, Reward ledger solo server; SignalR solo dispara hydrate); VI OroIdentityServer única autoridad PKCE `jwks_uri` `sub=PlayerId` `must_change_password` gating; H `secureRoutes=[apiUrl]` Bearer solo `oroclash-api` + `X-Correlation-Id`; I RFC7807 `ProblemDetails` `CorrelationId/TraceId`; J REST `IEndpoint` thin `ISender`; WCAG `aria-current` `aria-live polite` + `assertive` secured/terminal, targets ≥44px; Design System `data-theme="player"` sin literales cinematic gradiente final/corona.

**Scale/Scope**: `MaxRounds` 5–15 (default 10), 5 niveles Difficulty (Basic..Expert) + CategorySpecific, `LadderRow` N=5..15 filas por GameSession, 4 reward types por ladder, 1 transición por ronda (N-1 por juego), ~4 vistas/estados (loading/empty/error/terminal + ladder vertical 10–15 rows), N jugadores por juego `MaxPlayers` 10 default aislados per GameSession.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Evidencia |
|-----------|--------|-----------|
| I. Domain First | ✅ PASS | Ladder es solo proyección; reglas `Game.StartRound/CompleteRound`, `IDifficultyProgressionStrategy`, `PointTransaction` ledger, `WithdrawalPolicy` viven en `OroQuizClash.Domain` (SPEC-005/007). `PlayerRoundsStore` no contiene lógica autoritativa. |
| II. Clean Architecture | ✅ PASS | `Player (Angular)` → `oroclash-api` → `Application→Domain←Infrastructure`. Domain no referencia Angular. Ladder es `LadderRow` view-model derivado de `GameRound`/`Reward`/`SecuredPoints`. |
| III. BuildingBlocks No Reinvention | ✅ PASS | Reusa `Enumeration/Result/Specification/IRepository/IEndpoint/ISender/AppDbContextBase/Outbox/ServiceDefaults` ya en 029. No MediatR/MassTransit/AutoMapper. Solo Angular `@ngrx/signals` (permitido frontend). |
| IV. Vertical Slice + CQRS | ✅ PASS | Reusa slices `GetMyPlayerState` (Query 10 elementos → amplía a N LadderRow si necesita) + `GetGame` `IEndpoint` thin `ISender`; sin nuevo slice salvo proyección cliente; no carpeta genérica. |
| V. Server Truth | ✅ PASS | `currentRoundNumber`, `Round.level`, `Current/Next/Secured/Final` solo vía `GET /players/me` hydrate; evento SignalR nunca fuente de Score/isCorrect/Reward; `submittedAt <= expiresAt` ya en 029; transición solo tras hydrate exitoso. |
| VI. OroIdentityServer | ✅ PASS | PKCE `authorization_code`+`refresh_token` contra `/.well-known/openid-configuration`, `jwks_uri`, `sub=PlayerId`, `must_change_password` guard, sin user store local. Ladder hydrate requiere JWT. |
| A. Game Lifecycle | ✅ PASS | 9 estados `WAITING→IN_PROGRESS→ROUND_IN_PROGRESS↔ROUND_COMPLETED→FINISHED` protegidos; Invalid → 400; `currentRoundNumber` + `Round.status` derivados de lifecycle. |
| B. Category Invariants | ✅ PASS | 4 opciones 1 correcta, ≥5 por categoría; Question selection ya filtrada (invariante B) no afectada por ladder. |
| C. Configurable Rules | ✅ PASS | `MaxRounds/DifficultyStrategy/RewardRules/Withdrawal/Loss` inmutables tras Start, solo proyección en LadderRow; Secured según `KEEP_SECURED_SCORE` ledger. |
| D. Ledger | ✅ PASS | `Secured/Current/Next/Final` derivados `PointTransaction` ledger reconstruible `sum(points)=totalPoints`; cliente nunca calcula. |
| F. Concurrency/Idempotency | ✅ PASS | `RowVersion` en `Game`/`GameRound`, `X-Idempotency-Key` withdraw ya en 029; ladder `hydrate` idempotente con `switchMap` + `exponential backoff` en error. |
| G. Realtime/Outbox | ✅ PASS | `GameHub` `RoundCompleted/QuestionAvailable/ScoreUpdated/GameFinished` server-driven → `hydrate`; Outbox→RabbitMQ nunca antes commit; transición no usa payload directo. |
| H. Security Delegated | ✅ PASS | JWT `jwks_uri`, `PlayerIdentityMismatch` 403 auditada, `X-Correlation-Id` prop. ladder, rate limiting ya en Api. |
| I. Validation/Errors/Obs | ✅ PASS | 3 niveles, RFC7807 `ProblemDetails` 400/403/404/409 `CorrelationId/TraceId`, OTel `CorrelationId/TraceId/GameId/PlayerId/RoundId`. Ladder estados Loading/Empty/Error/Terminal con CorrelationId. |
| J. API & Frontend | ✅ PASS | REST `GET /players/me` 10 elementos + N LadderRow proyección, DTOs boundary, pagination not needed, `RequireAuthorization`, frontend presentation-only PKCE. |

**Resultado pre-Phase 0: PASS — sin violaciones.**

## Project Structure

### Documentation (this feature)

```text
specs/030-player-rounds/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── api-contracts.md       # GET /api/games/{id}/players/me (ladder N filas + reward ledger) reuse 029
│   └── ui-contracts.md        # Ladder vertical: Round 1..N, Current/Previous/Next/Secured/Final, transición sync server
├── checklists/requirements.md
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/Player/QuizArena.Player/         # Angular 22 SPA (standalone) — extends SPEC-027 (Player App) + 028 (Lobby) + 029 (Game)
├── src/app/
│   ├── app.routes.ts                # /game/:gameId (authGuard, mustChangePasswordGuard) already
│   ├── app.config.ts                # provideAuth PKCE, HttpClient interceptors already
│   ├── core/interceptors/           # correlationId, auth secureRoutes, error RFC7807 already
│   ├── core/realtime/               # game-realtime.service.ts withAutomaticReconnect → hydrate already (bind Laddder hydrate)
│   ├── stores/
│   │   ├── player-game.store.ts     # 10 elementos + computed remainingSeconds + methods hydrate/submitAnswer/withdraw/bindRealtime already (029)
│   │   └── player-rounds.store.ts   # NEW: signalStore Ladder (LadderRow[] N=MaxRounds, computed currentLevel/previousLevels/nextReward/securedReward/finalReward, method hydrateLadder(), bindRealtimeLadder(), transition animation state)
│   ├── features/game/
│   │   ├── game.component.ts        # EXTEND: embed <app-player-rounds> sidebar/right-panel cinematic junto a Question/Timer/Score ya en 029
│   │   ├── player-rounds.component.ts # NEW: ladder vertical role="list" Round 1..N filas role="listitem" aria-current="step" Current Level premium, Previous completed check, Next muted upcoming, Secured escudo, Final corona gradiente, transición <400ms prefers-reduced-motion, aria-live="polite"
│   │   ├── player-rounds.component.css # NEW: tokens data-theme="player" var(--space-*) var(--color-*) gradiente final, sin literales
│   │   ├── question.component.ts    # already (029)
│   │   ├── timer.component.ts       # already (029)
│   │   ├── score-panel.component.ts # already (029)
│   │   └── withdrawal.component.ts  # already (029)
│   └── features/shared/             # games.api.ts getMyState(gameId) already → amplía tipo PlayerGameState con ladder N
│   └── shared/ui/                   # loading-skeleton, empty-state, error-state (CorrelationId) already reuse
├── src/environments/                # environment.apiUrl, identityAuthority, gameHubUrl already
tests/ (Vitest)
└── src/app/
    ├── stores/player-rounds.store.spec.ts # ladder N filas, aria-current sync server, Current/Next/Secured/Final ledger, transition solo tras hydrate, reconnect jump
    └── features/game/player-rounds.component.spec.ts # role list, escudo checkpoint, corona final, prefers-reduced-motion, axe

src/OroQuizClash.Domain/              # No changes (Game, GameRound, PointTransaction, Reward, IDifficultyProgressionStrategy already)
src/OroQuizClash.Application/
└── Features/Games/
    ├── GetMyPlayerState.cs          # Query already — verifica que retorna maxRounds + currentRoundNumber + rounds[].level + RewardRules + SecuredPoints checkpoint para ladder (si falta campo, añadir DTO projection sin cambiar aggregate)
    └── GetGame.cs                   # Query already
src/OroQuizClash.Infrastructure/
└── Persistence/Configurations/      # already (GameTypeConfiguration RowVersion UNIQUE GameId+RoundNumber)
src/OroQuizClash.Api/
├── Program.cs                       # AddCqrs, AddDbContext, JWT identity Authority jwks_uri already
└── appsettings.json
OroQuizClash.AppHost/AppHost.cs      # quizarena-player container node:22-alpine → oroclash-api + identity-api already (SPEC-027)
tests/
├── OroQuizClash.Domain.Tests/       # RoundEngine Difficulty progression Linear 1→5, PointTransaction Secured checkpoint (ya en 005/007)
├── OroQuizClash.Application.Tests/  # GetMyPlayerState returns ladder-consistent N + current + RewardRules ledger
├── OroQuizClash.Api.Tests/          # Contract GET /players/me includes ladder fields, transition not trusting event payload
└── OroQuizClash.Architecture.Tests/ # Domain ↛ Angular, no MediatR, Server Truth hydrate gate
```

**Structure Decision**: Extender SPA `QuizArena.Player` existente (`PlayerGameStore` 10 elementos + `GameRealtimeService` ya en 027/029) con `PlayerRoundsStore` dedicado a `LadderRow[]` N=MaxRounds (proyección autoritativa) + `PlayerRoundsComponent` ladder vertical embebido en `GameComponent` como sidebar/panel cinematic (`role="list"` Round 1..N, Current Level premium `aria-current`, Previous completed, Next/Secured/Final con tokens `data-theme="player"` sin literales); reutiliza `oroclash-api` `GetMyPlayerState` + `GetGame` + `GameHub` → `hydrate` (Server Truth V) y `OroQuizClash.AppHost` ya orquesta todo; no nuevo agregado dominio salvo proyección cliente.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Angular SPA separado Blazor Admin (ya en 027) | Mandato SPEC-027/029/030 cinematic Player vs Admin; Angular 22 + SignalStore ya justificado | Reusar Blazor Admin rompería FR-013 `data-theme="player"` cinematic y mandato Angular |
| NgRx SignalStore para LadderRow[] N=5..15 per GameSession | Mandato nota 4 SPEC-027 + `PlayerRoundsStore` con `computed` Current/Previous/Next/Secured/Final + `rxMethod hydrateLadder/bindRealtimeLadder` + `patchState` idempotente; derived state compleja para 6 visual states | `BehaviorSubject` manual duplica sincronización, carece de `DeepSignal` + `tapResponse` + computed memoization |
| SignalR `GameHub` `withAutomaticReconnect` → `hydrate` para transición | Realtime obligatorio FR-009/010 para N jugadores sync Current Level en <500ms tras RoundCompleted; sin hydrate polling viola V | Polling REST aumenta latencia y no escala a N concurrentes; trusting event payload viola V Server Truth |
| Design System `data-theme="player"` tokens sin literales + ladder premium corona | FR-013/014 cinematic/immersive/premium WCAG AA 375-1536 + SC-009 80% premium perception; ladder final gradiente + Current premium requieren tokens centralizados | Estilos literales por componente rompen `design-system/MASTER.md` y no pasan axe/Lighthouse |
| PlayerRoundsStore separado de PlayerGameStore | Ladder es feature independiente (N filas + 4 rewards + transición) con ciclo de vida propio, scoped por gameId, testeable aislado; acoplar a PlayerGameStore inflaría 10 elementos a 15+ mixed concerns | Extender PlayerGameStore mezcla 10 elementos game + LadderRow N + rewards + transition animation, rompe Single Responsibility y testeabilidad (029 vs 030) |

## Constitution Check (Post-Design Re-Check)

*Re-evaluado tras Phase 1 (research.md + data-model.md + contracts/ + quickstart.md):*

| Gate | Estado | Notas post-diseño |
|------|--------|-------------------|
| I–VI, H, I, J | ✅ PASS | Diseño refuerza V (evento→hydrate REST nunca payload como verdad, ledger para rewards) y H (PKCE `secureRoutes` + `must_change_password`). Ningún nuevo agregado. Ladder es view-model. |
| A–G | ✅ PASS | Lifecycle, ledger, Outbox, SignalR preservados. N filas `UNIQUE (GameId,RoundNumber)` + hydrate <500ms + transición <400ms. |
| Complejidad | ✅ Justificada | 4 entradas ya justificadas en 027/029 + 1 nueva (`PlayerRoundsStore` separado) por SRP ladder vs game 10 elementos; todas por mandato explícito. |

**Resultado final: PASS — proceder a `/speckit.tasks`.**
