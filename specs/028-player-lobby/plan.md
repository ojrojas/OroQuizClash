# Implementation Plan: Player Lobby

**Branch**: `028-player-lobby` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/028-player-lobby/spec.md`

## Summary

Lobby del jugador para descubrir Available Games (`WAITING_FOR_PLAYERS`) con 8 campos (Game Name, Category, Difficulty, Number of Rounds, Players, Start Time, Prize, Status) paginado server-side, y acciones `Join Game` idempotente (`POST /api/games/{id}/players` `X-Idempotency-Key` + `UNIQUE (GameId,UserId)`), `View Game Information` (`GET /api/games/{id}`) y `Leave Lobby` navegación client-side sin side-effect. Extiende `QuizArena.Player` Angular 22 SPA (SPEC-027) sobre `oroclash-api` `GetGames`/`JoinGame`/`GetGame` slices existentes, `design-system/tokens` `data-theme="player"`, OIDC PKCE contra OroIdentityServer, validación autoritativa server-side con `RowVersion` y observabilidad `X-Correlation-Id` + RFC 7807.

## Technical Context

**Language/Version**: C# 12 / .NET 10.0 (`net10.0`, `LangVersion latest`, `Nullable enable`) para `oroclash-api`; TypeScript 5.8+ / **Angular 22** (standalone, `input()`/`output()`, `@if`/`@for`, `provideRouter`, `HttpClient withFetch`, `withInterceptors`) Node 22 LTS para Player SPA.

**Primary Dependencies**: `BuildingBlocks.Kernel.Domain` (AggregateRoot, Entity, IBusinessRule, Result, Specification), `BuildingBlocks.CQRS` (IQuery/ICommand, ISender, IValidator, IPipelineBehavior), `BuildingBlocks.Kernel.Infrastructure` (AppDbContextBase, EfRepository, SpecificationEvaluator, IUnitOfWork, Outbox), `BuildingBlocks.ServiceDefaults` (OTel, health `/health` `/alive`, Resilience, IEndpoint, GlobalExceptionHandler); Angular: `@angular/core/router/common/forms` 22.x, `@ngrx/signals` + `rxjs 7.x` (rxMethod, tapResponse), `angular-auth-oidc-client` 17+ PKCE `authorization_code` + `refresh_token`, `@microsoft/signalr` 8.x (reusado en juego, no lobby), `design-system/tokens/design-tokens.css` (`data-theme="player"`).

**Storage**: SQL Server primaria `oroclash` (`Game` RowVersion + IX `Status, CreatedAt desc`, `GamePlayer` UK `GameId+UserId` RowVersion, `Reward` opcional, Outbox); PostgreSQL `identitydb` aislada OroIdentityServer; cliente solo memoria SignalStore + `sessionStorage` efímero `idemp-join-{gameId}` nunca `localStorage`.

**Testing**: xUnit v3 + NSubstitute + Testcontainers.MsSql/ Sqlite + Aspire.Hosting.Testing (WebApplicationFactory) + coverlet para API slices (`GetGames`, `JoinGame`); Vitest + Angular Testing Library + `provideHttpClientTesting` para lobby store/componente (skeleton/empty/error, paginación, Join idempotence, View detail, Leave); `dotnet test` TestingPlatform, `ng test --watch=false`, `ng lint` `@ngrx/eslint-plugin`, axe/Lighthouse para WCAG.

**Target Platform**: Web SPA evergreen Chrome/Edge/Firefox/Safari responsive 375–1536 WCAG 2.2 AA, `ng serve` dev `ng build` prod `dist/` hosteado via `QuizArena.Player` static files o container `node:22-alpine` orquestado por `OroQuizClash.AppHost` (sqlserver/postgres/redis/rabbitmq/identity-api/oroclash-api/quizarena-player).

**Project Type**: web-application (Angular SPA `src/Player/QuizArena.Player` + modular monolith `Domain/Application/Infrastructure/Api`).

**Performance Goals**: SC-003 Join <1s percibido 95% (idempotente), SC-006 Leave <500ms sin escritura, SC-001/002 Available Games 100% filtrado 8 campos <500ms p95, paginación 20 <300ms p95, listados <1s 95%.

**Constraints**: Constitución V server truth (cupo/status validados server-side RowVersion, timer no aplica lobby), VI OroIdentityServer única autoridad OIDC PKCE `jwks_uri` `sub`=`PlayerId` `must_change_password` gating, H `secureRoutes=[apiUrl]` Bearer solo `oroclash-api` + `X-Correlation-Id` UUID, I RFC 7807 ProblemDetails `CorrelationId/TraceId`, J REST `IEndpoint` thin `ISender`, WCAG 2.2 AA `aria-live` 44px sin scroll, <200ms validación pipeline.

**Scale/Scope**: 100 juegos disponibles, 20 por página (max 50), N jugadores concurrentes `MaxPlayers` 10 default, 8 columnas → tarjetas móvil, 8 vistas/estados lobby (loading/empty/error/ready + detail + paginator).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Evidencia |
|-----------|--------|-----------|
| I. Domain First | ✅ PASS | Lobby sin reglas autoritativas nuevas; `Game.JoinPlayer()` + `GameStatus.IsValidTransition` en `OroQuizClash.Domain` (existing). Lobby solo proyecta. |
| II. Clean Architecture | ✅ PASS | `Player (Angular)` → `oroclash-api` → `Application→Domain←Infrastructure`. Domain no referencia Angular. |
| III. BuildingBlocks No Reinvention | ✅ PASS | Reusa `AggregateRoot/Result/Specification/IRepository/IUnitOfWork/IEndpoint/ISender/AppDbContextBase/Outbox/ServiceDefaults`. No MediatR/MassTransit/AutoMapper. |
| IV. Vertical Slice + CQRS | ✅ PASS | Reusa slices `GetGames` (Query+Handler+Endpoint), `JoinGame` (Command+Validator+Handler+Endpoint), `GetGame` (Query). Sin slices nuevos si contrato ya cubre. |
| V. Server Truth | ✅ PASS | Cupo/status/UNIQUE validados server-side con server timestamps + RowVersion; cliente solo visualiza Available Games. Join revalida `WAITING_FOR_PLAYERS` + `Players.Count<Max`. |
| VI. OroIdentityServer | ✅ PASS | OIDC PKCE `authorization_code`+`refresh_token` contra `/.well-known/openid-configuration`, `jwks_uri`, `sub`=`PlayerId`, `must_change_password` guard, `post_logout`. Sin user store local. |
| A. Game Lifecycle | ✅ PASS | `WAITING_FOR_PLAYERS → IN_PROGRESS` protegida; Invalid transición → 400 `InvalidGameState`. Lobby solo lista WAITING_FOR_PLAYERS. |
| B. Category Invariants | ✅ PASS | `Category.IsPublished` filtrada; ≥5 preguntas válidas antes publicación (ya en Domain). |
| C. Configurable Rules | ✅ PASS | `MinRounds/MaxRounds/MaxPlayers/TimeLimit/Points/Withdrawal/Loss/RewardRules` inmutables tras Start, solo proyección en lobby. |
| D. Ledger | ✅ PASS | No aplica lobby (pre-juego); scores futuros vía `PointTransaction` ledger (D). |
| F. Concurrency/Idempotency | ✅ PASS | `RowVersion` en `Game` + `UNIQUE (GameId,UserId)` en `GamePlayer` + `X-Idempotency-Key` per `gameId` (sessionStorage) → AlreadyJoined idempotente 200. |
| G. Realtime/Outbox | ✅ PASS | Lobby no usa SignalR; `PlayerJoinedDomainEvent` vía Outbox→RabbitMQ opcional (topic, confirms) nunca antes commit. |
| H. Security Delegated | ✅ PASS | JWT `jwks_uri` validation, `PlayerIdentityMismatch` 403 auditada, `X-Correlation-Id`, rate limiting `GamePlayLimiter` existente, sin `IsCorrect` leak. |
| I. Validation/Errors/Obs | ✅ PASS | 3 niveles (Api transport, Application `JoinGameValidator`, Domain `MaxPlayersRule`), RFC 7807 `ProblemDetails` + `GlobalExceptionHandler` → 400/409, OTel `CorrelationId/TraceId/GameId/PlayerId`. |
| J. API & Frontend | ✅ PASS | REST `GET /games?status=WAITING_FOR_PLAYERS` paginado + `GET /games/{id}` + `POST /games/{id}/players`, DTOs boundary, pagination, `RequireAuthorization`, frontend presentation-only PKCE. |

**Resultado pre-Phase 0: PASS — sin violaciones.**

## Project Structure

### Documentation (this feature)

```text
specs/028-player-lobby/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── api-contracts.md       # GET /games?status=WAITING_FOR_PLAYERS paginado, GET /games/{id}, POST /games/{id}/players Join idempotente
│   └── ui-contracts.md        # Table 8 cols / cards 375px, states Loading/Empty/Error/Ready, View modal, Leave navigation, WCAG
├── checklists/requirements.md
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/Player/QuizArena.Player/         # Angular 22 SPA (standalone) — extends SPEC-027
├── src/app/
│   ├── app.routes.ts                # /lobby (authGuard, mustChangePasswordGuard) already, add lobby detail child
│   ├── app.config.ts                # provideAuth PKCE quizarena-player, HttpClient interceptors already
│   ├── core/interceptors/           # correlationId, auth (secureRoutes apiUrl), error (RFC7807, 401 silentRenew, 429)
│   ├── features/lobby/              # LOBBY NEW: lobby.component.ts (Available Games 8 cols table/cards, paginator, Join/View/Leave)
│   │   ├── lobby.component.ts
│   │   ├── game-detail.component.ts # View Game Information modal/page (8+extended fields)
│   │   └── lobby.store.ts           # optional SignalStore withState {games, totalCount, page, isLoading, error} + rxMethod load
│   └── features/shared/             # games.api.ts extends getGames(status,page,pageSize)/getGame/joinGame
│   └── shared/ui/                   # loading-skeleton, empty-state, error-state (CorrelationId) already
├── src/environments/                # environment.apiUrl, identityAuthority
tests/  (Angular Vitest)
└── src/app/features/lobby/lobby.store.spec.ts / lobby.component.spec.ts

src/OroQuizClash.Domain/              # No changes (Game, GamePlayer, GameStatus, RowVersion, UNIQUE already)
src/OroQuizClash.Application/
└── Features/Games/
    ├── GetGame.cs                   # GetGamesQuery + GetGameQuery + JoinGameCommand already (Vertical Slice, IEndpoint)
    ├── JoinGame.cs                  # Command+Validator+Handler+Endpoint POST /api/games/{id}/players (X-Idempotency-Key)
    └── Specifications/              # GameFilterSpecification (Status WHERE, OrderBy CreatedAt desc, Include Players, AsNoTracking, pagination)
src/OroQuizClash.Infrastructure/
└── Persistence/Configurations/      # GameTypeConfiguration (IX Status/CreatedAt), GamePlayerTypeConfiguration (UK GameId+UserId, RowVersion)
src/OroQuizClash.Api/
├── Program.cs                       # AddCqrs, AddDbContext, JWT (identity Authority jwks_uri), IRepository wiring already
└── appsettings.json
OroQuizClash.AppHost/AppHost.cs      # quizarena-player AddContainer node:22-alpine or AddNpmApp → oroclash-api + identity-api already (SPEC-027)
tests/
├── OroQuizClash.Domain.Tests/       # JoinPlayer, MaxPlayersRule
├── OroQuizClash.Application.Tests/  # GetGamesHandler, JoinGameHandler idempotence
├── OroQuizClash.Api.Tests/          # Contract Available Games 8 fields, Join idempotent, Leave no side-effect
└── OroQuizClash.Architecture.Tests/ # LobbyNoDomainLeak, no client trust (Domain ↛ Angular)
```

**Structure Decision**: Extender SPA Angular 22 `src/Player/QuizArena.Player` existente (SPEC-027) con feature `lobby/` aislada (scoped store, no global singleton), reutilizando `oroclash-api` slices `GetGames/JoinGame/GetGame` y `GameFilterSpecification` sin nuevos agregados; `Prize` como proyección `Reward` opcional. `OroQuizClash.AppHost` ya orquesta `quizarena-player` → `oroclash-api` → `identity-api`; no nuevo microservicio. BuildingBlocks permanece dependencia externa.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Angular SPA separado Blazor Admin (ya en 027) | Mandato SPEC-027/028 lobby jugador vs admin; Angular 22 + SignalStore ya justificado | Reusar Blazor Admin rompería FR-012 WCAG player theme `data-theme="player"` y mandato Angular |
| SignalStore opcional para lobby pagination | Reuse patrón 027 para lobby state paginado con `rxMethod` + `tapResponse` y `withComputed` para 8 campos display | `BehaviorSubject` manual duplica sincronización y carece de `patchState` granular |
| `X-Idempotency-Key` header per gameId | FR-005 F idempotencia Join bajo doble clic/pestañas + `UNIQUE (GameId,UserId)` + `RowVersion` | Sin header doble Join crea duplicado bajo race (UNIQUE lo rechaza pero sin mensaje idempotente 200) |

## Constitution Check (Post-Design Re-Check)

*Re-evaluado tras Phase 1 (research.md + data-model.md + contracts/ + quickstart.md):*

| Gate | Estado | Notas post-diseño |
|------|--------|-------------------|
| I–VI, H, I, J | ✅ PASS | Diseño refuerza V (Available Games filtrado server-side, Join revalida `WAITING_FOR_PLAYERS` + RowVersion, Leave sin API) y H (PKCE `secureRoutes` solo `oroclash-api`, `X-Correlation-Id` por request). Ningún nuevo agregado. |
| A–G | ✅ PASS | Lifecycle, Outbox, ledger preservados. Paginación `ApplyAsNoTracking` + IX `Status,CreatedAt`. |
| Complejidad | ✅ Justificada | 3 entradas ya justificadas en 027 + lobby store reuse, todas por mandato explícito (Angular 22, SignalStore, idempotency). |

**Resultado final: PASS — proceder a `/speckit.tasks`.**
