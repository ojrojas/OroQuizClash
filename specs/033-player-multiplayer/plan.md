# Implementation Plan: Player Multiplayer

**Branch**: `033-player-multiplayer` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/033-player-multiplayer/spec.md`

## Summary

Aislamiento multiplayer sin fuga — 5 estados privados (`Private Game State`, `Private Answer State`, `Private Score State`, `Private Timer`, `Private Session` per `sub=PlayerId+GameId/RoundId`) aislados vía `GET /api/games/{id}/players/me` `sub` + `UNIQUE (GameId,RoundId,PlayerId)` + `RowVersion` per `GamePlayer`, y 4 vistas públicas (`Players`, `Players Remaining`, `Leaderboard` `totalPoints/level`, `Current Round` 3/10) sin `SelectedOptionId/isCorrect/Timer` de otros. Extiende `QuizArena.Player` Angular 22 SPA (SPEC-027/029/030/031/032) con `PlayerGameStore` scoped `providers: [PlayerGameStore]` per `GameComponent` (no `providedIn: 'root'`), `GamesApi.getMyState/getLeaderboard/getPlayers`, `GameRealtimeService` `withAutomaticReconnect` → `hydrate` (Server Truth V), `design-system/tokens` `data-theme="player"` `isolation.spec.ts` 4 instancias A-D.

## Technical Context

**Language/Version**: TypeScript 5.8+ / **Angular 22** (standalone `input()` `signal()` `computed()` `@if/@for`, `provideRouter`, `HttpClient withFetch` `withInterceptors`, `rxMethod`) Node 22 LTS + C# 12 / .NET 10.0 (`net10.0`) para `oroclash-api` slices ya existentes (`GetMyPlayerState` privado per `sub`, `GetLeaderboard` público, `GetGamePlayers`).

**Primary Dependencies**: `@angular/core/router/common/forms` 22.x, `@ngrx/signals` 22.x + `rxjs 7.x` (`rxMethod`, `tapResponse`, `switchMap`), `angular-auth-oidc-client` 17+ PKCE `authorization_code`+`refresh_token`, `@microsoft/signalr` 8.x `GameHub` `ScoreUpdated/LeaderboardUpdated/RoundCompleted` `withAutomaticReconnect`, `design-system/tokens/design-tokens.css` (`data-theme="player"`); Backend `BuildingBlocks.Kernel.Domain` (`AggregateRoot`, `IBusinessRule`, `Result`, `Enumeration`), `BuildingBlocks.CQRS` (`IQuery` `GetMyPlayerState`/`GetLeaderboard` `ISender`), `BuildingBlocks.Kernel.Infrastructure` (`AppDbContextBase`, `EfRepository`, `IUnitOfWork`, `Outbox`), `BuildingBlocks.ServiceDefaults` (OTel, health, `IEndpoint`, `GlobalExceptionHandler`).

**Storage**: SQL Server primaria `oroclash` (`Game` RowVersion + `GamePlayer` `RowVersion` per `GamePlayerId` + `GameRound` + `Answer` `UNIQUE (GameId,RoundId,PlayerId)` + `PointTransaction` ledger `UNIQUE (GameId,PlayerId,CreatedAt)` + Outbox); PostgreSQL `identitydb` aislada OroIdentityServer; cliente solo memoria `PlayerGameStore` `DeepSignal` scoped per `GameComponent` `providers: [PlayerGameStore]` (no `providedIn: 'root'`) + `GameRealtimeService` per `gameId+sub` HubConnection.

**Testing**: Vitest 3 + Angular Testing Library + `provideHttpClientTesting` + `TestBed` para `isolation.spec.ts` (4 instancias `PlayerGameStore` A-D scoped sin contaminación `Answer/Score/Timer`), `Players`/`Leaderboard`/`Current Round` público sin privados, `Private Timer`/`Session` per `playerId`; xUnit v3 + NSubstitute + Testcontainers.MsSql para `GetMyPlayerState` privado per `sub` 2 JWTs paralelo 0% leak + `GetLeaderboard` sin `isCorrect` + `PlayersRemaining` count `IsActive`; `dotnet test` TestingPlatform, `ng test --watch=false`, `ng lint` `@ngrx/eslint-plugin`, axe/Lighthouse WCAG.

**Target Platform**: Web SPA evergreen Chrome/Edge/Firefox/Safari responsive 375–1536 WCAG 2.2 AA, `ng serve` dev `ng build` prod `dist/` hosteado via `QuizArena.Player` static files o container `node:22-alpine` orquestado por `OroQuizClash.AppHost` (sqlserver/postgres/redis/rabbitmq/identity-api/oroclash-api/quizarena-player).

**Project Type**: web-application (Angular SPA `src/Player/QuizArena.Player` + modular monolith `Domain/Application/Infrastructure/Api`).

**Performance Goals**: SC-006 100% `Score` update <1s vía `ScoreUpdated→hydrate`; SC-001 0% leak privado `GET /players/me` 2 JWTs paralelo; SC-002 0% fuga `Leaderboard` sin `SelectedOptionId/isCorrect`; SC-003 100% stores A-D aislados `isolation.spec.ts`; SC-007 375-1536 sin scroll 100% 1col/2col/4col targets ≥44px.

**Constraints**: Constitución V server truth (`Private State` solo vía `GET /players/me` `sub`, `isCorrect` solo tras `EVALUATED`, SignalR nunca fuente veredicto, `Leaderboard` público sin `Answer`); VI OroIdentityServer PKCE `jwks_uri` `sub=PlayerId` `must_change_password` gating; F `UNIQUE (GameId,RoundId,PlayerId)` + `RowVersion` per `GamePlayer` (no global `Game`); H `secureRoutes=[apiUrl]` Bearer solo `oroclash-api` + `X-Correlation-Id` + `PlayerIdentityMismatch 403` audit; I RFC7807 `ProblemDetails` `CorrelationId/TraceId`; J REST `IEndpoint` thin `ISender`; WCAG `role="list"` `aria-live polite` `outline:2px` `prefers-reduced-motion`; Design System `data-theme="player"` sin literales 9 métricas tokens (`--color-primary`).

**Scale/Scope**: 4 jugadores A-D ejemplo (escalable a `MaxPlayers` 10 default), 5 privados per jugador (`Game/Answer/Score/Timer/Session`) + 4 públicos (`Players` `Players Remaining` `Leaderboard` `Current Round`), N rondas `MaxRounds` 5–15 default 10, `PointsPerRound` 100 default, `isolation.spec.ts` 4 instancias concurrentes.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Evidencia |
|-----------|--------|-----------|
| I. Domain First | ✅ PASS | Aislamiento es `Game.GetPlayerState(sub)` + `Game.SubmitAnswer(sub)` dominio con `IBusinessRule` `PlayerNotInGame` + `QuestionAlreadyAnswered` per `UNIQUE (GameId,RoundId,PlayerId)` + `GamePlayer` per `sub` (SPEC-011). `PlayerGameStore` scoped no contiene lógica de aislamiento autoritativa. |
| II. Clean Architecture | ✅ PASS | `Player (Angular PlayerGameStore scoped)` → `oroclash-api GetMyPlayerState/GetLeaderboard IQuery` → `Application→Domain←Infrastructure`. Domain no referencia Angular. `Private/Public` view-models. |
| III. BuildingBlocks No Reinvention | ✅ PASS | Reusa `AggregateRoot/Result/IBusinessRule/Specification/IRepository/IEndpoint/ISender/AppDbContextBase/Outbox/ServiceDefaults` ya en 011/029. No MediatR/MassTransit/AutoMapper. Solo Angular `@ngrx/signals` (permitido frontend). |
| IV. Vertical Slice + CQRS | ✅ PASS | Reusa slices `GetMyPlayerState` privado `sub` + `GetLeaderboard` público + `GetGamePlayers` público (`Query`+`Handler`+`Response DTO`+`IEndpoint` thin `ISender`). Sin carpeta genérica. |
| V. Server Truth | ✅ PASS | `Private State` solo vía `GET /players/me` `sub` + `isCorrect` filtrado `EVALUATED`; `Leaderboard` público `totalPoints/level` sin `Answer`; `ScoreUpdated` solo dispara `hydrate` `GET /players/me`; cliente nunca ve `Answer` de B en payload de A. |
| VI. OroIdentityServer | ✅ PASS | PKCE `authorization_code`+`refresh_token` contra `/.well-known/openid-configuration`, `jwks_uri`, `sub=PlayerId`, `must_change_password` guard, sin user store local. `GET /players/me` + `GET /leaderboard` requieren JWT. |
| A. Game Lifecycle | ✅ PASS | 9 estados `WAITING→IN_PROGRESS→ROUND_IN_PROGRESS↔ROUND_COMPLETED→FINISHED` protegidos; `Players Remaining` cuenta `IsActive` (`ACTIVE`); `Current Round` 3/10 genérico. |
| D. Ledger | ✅ PASS | `Private Score` `Score/SecuredPoints` `PointTransaction` ledger per `playerId` no expuesto en `Leaderboard` detallado; `Leaderboard` solo `totalPoints/level` públicos. |
| F. Concurrency/Idempotency | ✅ PASS | `UNIQUE (GameId,RoundId,PlayerId)` + `RowVersion` per `GamePlayer` (no global `Game`) aísla `Answer/Session`; `GamePlayerStore` scoped per `GameComponent` no `providedIn: 'root'` evita contaminación A→B; `LeaderboardUpdated` no fuente verdad. |
| G. Realtime/Outbox | ✅ PASS | `GameHub` `ScoreUpdated/LeaderboardUpdated/RoundCompleted/Reconnected` → `hydrate` `GET /players/me` privado + `GET /leaderboard` público; Outbox→RabbitMQ nunca antes commit; `PlayerGameStore` no muta desde evento directo. |
| H. Security Delegated | ✅ PASS | JWT `jwks_uri`, `PlayerIdentityMismatch` 403 auditada si `sub` intenta acceder `GameSession` de otro, `X-Correlation-Id` prop., payload `GET /players/me` nunca incluye `Answer` de B para A. |
| I. Validation/Errors/Obs | ✅ PASS | 3 niveles, RFC7807 `ProblemDetails` con `CorrelationId/TraceId` para `GET /players/me` 401/403/404 + `GET /leaderboard` 401, OTel `CorrelationId/TraceId/GameId/PlayerId`. |
| J. API & Frontend | ✅ PASS | REST `GET /api/games/{id}/players/me` privado + `GET /api/games/{id}/leaderboard` público + `GET /api/games/{id}/players` público, DTOs boundary, `RequireAuthorization`, frontend presentation-only PKCE. |

**Resultado pre-Phase 0: PASS — sin violaciones.**

## Project Structure

### Documentation (this feature)

```text
specs/033-player-multiplayer/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── api-contracts.md       # GET /players/me privado per sub + GET /leaderboard público + GET /players público
│   └── ui-contracts.md        # 5 privados aislados per Store scoped + 4 públicos Players/Remaining/Leaderboard/CurrentRound, data-theme player a11y
├── checklists/requirements.md
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/Player/QuizArena.Player/         # Angular 22 SPA (standalone) — extends SPEC-027 (Player App) + 029 (Game) + 030 (Rounds) + 031 (Answering) + 032 (Scoring)
├── src/app/
│   ├── app.routes.ts                # /game/:gameId (authGuard, mustChangePasswordGuard) already
│   ├── app.config.ts                # provideAuth PKCE, HttpClient interceptors already
│   ├── core/interceptors/           # correlationId, auth secureRoutes, error RFC7807 already
│   ├── core/realtime/               # game-realtime.service.ts withAutomaticReconnect → hydrate already (ScoreUpdated/LeaderboardUpdated/Reconnected)
│   ├── stores/
│   │   ├── player-game.store.ts     # 10 elementos already (029) scoped providers: [PlayerGameStore] per GameComponent (no providedIn root) → Private State per sub, hydrate GET /players/me, bindRealtime ScoreUpdated
│   │   └── player-rounds.store.ts   # Ladder Round 1..N already (030)
│   ├── features/game/
│   │   ├── game.component.ts        # EXTEND: header/sidebar con Players/Leaderboard/CurrentRound públicos + footer ScorePanel 5 métricas privadas + center Question
│   │   ├── score-panel.component.ts # already (032) 5 métricas privadas Current/Secured/Potential/Round/Total aria-live polite
│   │   ├── player-rounds.component.ts # already (030)
│   │   ├── question.component.ts    # already (031) Private Answer
│   │   └── leaderboard.component.ts # NEW/EXTEND: muestra Players/Players Remaining/Leaderboard/CurrentRound públicos role="list" aria-live polite, tokens data-theme player
│   └── features/shared/             # games.api.ts getMyState (privado) + getLeaderboard (público) + getPlayers (público) already
│   └── shared/ui/                   # loading-skeleton, empty-state, error-state (CorrelationId) already reuse
├── src/environments/                # environment.apiUrl, identityAuthority, gameHubUrl already
tests/ (Vitest)
└── src/app/
    ├── stores/player-game.store.spec.ts # isolation 4 instancias A-D scoped sin contaminación
    └── features/game/leaderboard.component.spec.ts # Players/Remaining/Leaderboard/CurrentRound públicos sin privados, isolation
    └── tests/integration/isolation.spec.ts # 4 browsers A-D concurrent SubmitAnswer sin leak

src/OroQuizClash.Domain/              # No changes (Game.GetPlayerState per sub, GameLeaderboard, GamePlayer RowVersion)
src/OroQuizClash.Application/
└── Features/Games/
    ├── GetMyPlayerState.cs          # Query already — returns Private State per sub (Score/Answer/Timer/Session) filtrado
    ├── GetLeaderboard.cs            # Query already — returns Leaderboard público sin Answer/Timer
    ├── GetGamePlayers.cs            # Query — returns Players/PlayersRemaining público
    └── GetGame.cs                   # Query — returns Current Round público
src/OroQuizClash.Infrastructure/
└── Persistence/Configurations/      # already (GamePlayer RowVersion per GamePlayerId, Answer UNIQUE, PointTransaction ledger, Outbox)
src/OroQuizClash.Api/
├── Program.cs                       # AddCqrs, AddDbContext, JWT identity Authority jwks_uri already
└── appsettings.json
OroQuizClash.AppHost/AppHost.cs      # quizarena-player container node:22-alpine → oroclash-api + identity-api already (SPEC-027)
tests/
├── OroQuizClash.Domain.Tests/       # Game.GetPlayerState per sub isolation, Leaderboard no leak
├── OroQuizClash.Application.Tests/  # GetMyPlayerStateHandler per sub 2 JWTs paralelo, GetLeaderboard sin IsCorrect
├── OroQuizClash.Api.Tests/          # Contract GET /players/me privado per sub 0% leak, GET /leaderboard público sin privados, PlayersRemaining count IsActive
└── OroQuizClash.Architecture.Tests/ # Domain ↛ Angular, GetMyPlayerState uses sub, no client private leak
```

**Structure Decision**: Extender SPA `QuizArena.Player` existente (`PlayerGameStore` scoped `providers: [PlayerGameStore]` per `GameComponent` no `providedIn: 'root'` ya en 029) con `GET /players/me` privado per `sub` (`GameClaims.GetSub`) + `GET /leaderboard`/`GET /players` públicos sin `Answer/Score` privado + `LeaderboardComponent`/`GameComponent` header `Players/Players Remaining/Leaderboard/Current Round` (`role="list"` `aria-live polite` tokens) + realtime `ScoreUpdated/LeaderboardUpdated→hydrate` (Server Truth V, `Private State` nunca en `Leaderboard`); reutiliza `oroclash-api` `GetMyPlayerState`/`GetLeaderboard` + `GameHub` → `hydrate` y `OroQuizClash.AppHost` ya orquesta todo; no nuevo agregado dominio salvo `LeaderboardEntry` view-model público.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Angular SPA separado Blazor Admin (ya en 027) | Mandato SPEC-027/029/030/031/032/033 cinematic Player vs Admin; Angular 22 + SignalStore ya justificado | Reusar Blazor Admin rompería FR-012 `data-theme="player"` cinematic y mandato Angular 22 |
| NgRx SignalStore scoped per GameComponent para Private State | Mandato nota 4 SPEC-027 + `PlayerGameStore` con `providers: [PlayerGameStore]` per `GameComponent` (no `providedIn: 'root'`) para aislar `Answer/Score/Timer/Session` per `sub` bajo concurrencia 10 jugadores; `isolation.spec.ts` 4 instancias sin contaminación requiere DeepSignal memoization + tapResponse | `providedIn: 'root'` singleton compartiría `Answer/Score` entre A y B (fuga directa); `BehaviorSubject` manual duplica sincronización y carece de `DeepSignal` + computed |
| SignalR `GameHub` `withAutomaticReconnect` → `hydrate` por jugador | Realtime obligatorio para `ScoreUpdated` + `LeaderboardUpdated` per `gameId` con `accessTokenFactory` per `sub`; 4 Angular A-D con HubConnection per `sub` + `hydrate` privado per `sub` | Polling REST sin SignalR no notifica Score/Leaderboard sin delay; trusting event payload para `Answer isCorrect` de otro viola V |
| Design System `data-theme="player"` tokens sin literales + Players/Leaderboard | FR-012 cinematic premium WCAG AA 375-1536 + SC-007 4 públicos tokens (`--color-primary`) requieren tokens centralizados | Estilos literales por métrica rompen `design-system/MASTER.md` y no pasan axe/Lighthouse |
