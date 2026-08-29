# Implementation Plan: Player Game

**Branch**: `029-player-game` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/029-player-game/spec.md`

## Summary

Pantalla principal de juego de QuizArena — experiencia Cinematic/Immersive/Premium/Competitive con 10 elementos (Current Round, Current Level, Question, Four Answers, Timer, Current Score, Secured Points, Potential Reward, Player Status, Withdrawal Action) proyectados desde `GET /api/games/{id}/players/me` (10-element hydrato) y mutaciones `POST /answers` + `POST /withdraw` idempotentes. Extiende `QuizArena.Player` Angular 22 SPA (SPEC-027) con `PlayerGameStore` `signalStore` 10 elementos scoped por `gameId`, `GameRealtimeService` SignalR `withAutomaticReconnect` → `hydrate` (Server Truth V), `design-system/tokens` `data-theme="player"`, OIDC PKCE contra OroIdentityServer, validación server-side con `RowVersion`/`AnswerWindowExpired` y layout por áreas responsive 375-1536 WCAG 2.2 AA.

## Technical Context

**Language/Version**: TypeScript 5.8+ / **Angular 22** (standalone, `input()`/`output()`, `@if`/`@for`, `provideRouter`, `HttpClient withFetch` `withInterceptors`, `rxMethod`) Node 22 LTS + C# 12 / .NET 10.0 (`net10.0`) para `oroclash-api` slices ya existentes.

**Primary Dependencies**: `@angular/core/router/common/forms` 22.x, `@ngrx/signals` + `rxjs 7.x` (`rxMethod`, `tapResponse`, `interval`, `switchMap`), `angular-auth-oidc-client` 17+ PKCE `authorization_code` + `refresh_token`, `@microsoft/signalr` 8.x `GameHub` `QuestionAvailable/ScoreUpdated/RoundCompleted/GameFinished` `withAutomaticReconnect`, `design-system/tokens/design-tokens.css` (`data-theme="player"`); Backend `BuildingBlocks.Kernel.Domain` (AggregateRoot, Result, Specification, IBusinessRule), `BuildingBlocks.CQRS` (IQuery/ICommand ISender), `BuildingBlocks.Kernel.Infrastructure` (AppDbContextBase, EfRepository, IUnitOfWork, Outbox), `BuildingBlocks.ServiceDefaults` (OTel, health, Resilience, IEndpoint, GlobalExceptionHandler).

**Storage**: SQL Server primaria `oroclash` (`Game` RowVersion + `GamePlayer`/`GameRound`/`Answer`/`PointTransaction` ledger, `Reward` opcional, Outbox, IX `Status/CreatedAt`); PostgreSQL `identitydb` aislada OroIdentityServer; cliente solo memoria SignalStore + `sessionStorage` efímero `idemp-{roundId}` / `idemp-withdraw-{gameId}` nunca `localStorage`.

**Testing**: Vitest + Angular Testing Library + `provideHttpClientTesting` + `TestBed` para Player Game store/componente (hydrato 10 elementos, `remainingSeconds` computed, `radiogroup` Four Answers, Timer drift, Withdrawal modal, `aria-live`); xUnit v3 + NSubstitute + Testcontainers.MsSql + Aspire.Hosting.Testing (WebApplicationFactory) para API slices (`SubmitAnswer` idempotente, `WithdrawPlayer`, `GetMyPlayerState`); `dotnet test` TestingPlatform, `ng test --watch=false`, `ng lint` `@ngrx/eslint-plugin`, axe/Lighthouse WCAG.

**Target Platform**: Web SPA evergreen Chrome/Edge/Firefox/Safari responsive 375-1536 WCAG 2.2 AA, `ng serve` dev `ng build` prod `dist/` hosteado via `QuizArena.Player` static files o container `node:22-alpine` orquestado por `OroQuizClash.AppHost` (sqlserver/postgres/redis/rabbitmq/identity-api/oroclash-api/quizarena-player).

**Project Type**: web-application (Angular SPA `src/Player/QuizArena.Player` + modular monolith `Domain/Application/Infrastructure/Api`).

**Performance Goals**: SC-003 Submit <1s 95% percibido; SC-004 Timer drift <1s 95% `remainingSeconds` 1/s; SC-005 ledger `Secured/Potential` 100% consistente <1s; SC-006 Withdraw <1s 95%; SC-008 375-1536 no scroll 100%, WCAG AA 100%; SC-009 `X-Correlation-Id` 100%.

**Constraints**: Constitución V server truth (`submittedAt <= expiresAt` `AnswerWindowExpired`, `isCorrect` server, `Score` ledger), VI OroIdentityServer única autoridad PKCE `jwks_uri` `sub=PlayerId` `must_change_password` gating, H `secureRoutes=[apiUrl]` Bearer solo `oroclash-api` + `X-Correlation-Id`, I RFC7807 `ProblemDetails` `CorrelationId/TraceId`, J REST `IEndpoint` thin `ISender`, WCAG `aria-live polite` Timer/Score `assertive` Expired 44px, <200ms validación pipeline, Design System `data-theme="player"` sin literales cinematic `gradientes/spacing/typography`.

**Scale/Scope**: N jugadores por juego `MaxPlayers` 10 default, 10 elementos por GameSession, 4 opciones por pregunta, 1 Timer por ronda, ~5 vistas/estados (loading/empty/error/expired/terminal + cinematic layout 3 áreas), `MaxRounds` 10 default.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Evidencia |
|-----------|--------|-----------|
| I. Domain First | ✅ PASS | Pantalla sin reglas autoritativas; `Game.SubmitAnswer(submittedAt, expiresAt)/WithdrawPlayer/Finish` + `PointTransaction` ledger en `OroQuizClash.Domain`; `PlayerGameStore` solo proyecta. |
| II. Clean Architecture | ✅ PASS | `Player (Angular)` → `oroclash-api` → `Application→Domain←Infrastructure`. Domain no referencia Angular. |
| III. BuildingBlocks No Reinvention | ✅ PASS | Reusa `AggregateRoot/Result/Specification/IRepository/IEndpoint/ISender/AppDbContextBase/Outbox/ServiceDefaults`. No MediatR/MassTransit/AutoMapper. |
| IV. Vertical Slice + CQRS | ✅ PASS | Reusa slices `GetMyPlayerState` (Query 10 elementos), `SubmitAnswer` (Command), `WithdrawPlayer` (Command), `GetGame` (Query) `IEndpoint` thin `ISender`. |
| V. Server Truth | ✅ PASS | `isCorrect`, `Score`, `Secured`, `Potential`, `remaining` evaluados server-side con server timestamps; Timer cliente es visual con corrección; SignalR no fuente de verdad (`hydrate` REST). |
| VI. OroIdentityServer | ✅ PASS | PKCE `authorization_code`+`refresh_token` contra `/.well-known/openid-configuration`, `jwks_uri`, `sub=PlayerId`, `must_change_password` guard, `post_logout`. Sin user store local. |
| A. Game Lifecycle | ✅ PASS | 9 estados `WAITING→IN_PROGRESS→ROUND_IN_PROGRESS↔ROUND_COMPLETED→FINISHED` protegidos; Invalid → 400. |
| B. Category Invariants | ✅ PASS | 4 opciones 1 correcta, ≥5 por categoría; `QuestionAvailable` ya filtrada (invariante B). |
| C. Configurable Rules | ✅ PASS | `TimeLimit/Points/Withdrawal/Loss/RewardRules` inmutables tras Start, solo proyección. |
| D. Ledger | ✅ PASS | `Score/Secured/Potential` derivados `PointTransaction` ledger reconstruible `sum(points)`. |
| F. Concurrency/Idempotency | ✅ PASS | `RowVersion` en `Game`/`Answer`, `X-Idempotency-Key` per `roundId`/`gameId` `UNIQUE` → AlreadyAnswered idempotente. |
| G. Realtime/Outbox | ✅ PASS | `GameHub` `QuestionAvailable/ScoreUpdated/RoundCompleted/GameFinished` server-driven → `hydrate`; Outbox→RabbitMQ nunca antes commit. |
| H. Security Delegated | ✅ PASS | JWT `jwks_uri`, `PlayerIdentityMismatch` 403 auditada, `X-Correlation-Id`, rate limiting `GamePlayLimiter`. |
| I. Validation/Errors/Obs | ✅ PASS | 3 niveles, RFC7807 `ProblemDetails` 400/403/404/409 `CorrelationId/TraceId`, OTel `CorrelationId/TraceId/GameId/PlayerId`. |
| J. API & Frontend | ✅ PASS | REST `GET /players/me` 10 elementos + `POST /answers` + `POST /withdraw`, DTOs boundary, pagination not needed, `RequireAuthorization`, frontend presentation-only PKCE. |

**Resultado pre-Phase 0: PASS — sin violaciones.**

## Project Structure

### Documentation (this feature)

```text
specs/029-player-game/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── api-contracts.md       # GET /players/me 10 elementos, POST /answers, POST /withdraw (reuse)
│   └── ui-contracts.md        # Cinematic 3 áreas: Header Round/Level/Timer, Center Question+Four Answers, Footer Score/Secured/Potential/Status/Withdraw
├── checklists/requirements.md
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/Player/QuizArena.Player/         # Angular 22 SPA (standalone) — extends SPEC-027 (Player App) + 028 (Lobby)
├── src/app/
│   ├── app.routes.ts                # /game/:gameId (authGuard, mustChangePasswordGuard) already
│   ├── app.config.ts                # provideAuth PKCE, HttpClient interceptors already
│   ├── core/interceptors/           # correlationId, auth secureRoutes, error RFC7807 already
│   ├── core/realtime/               # game-realtime.service.ts withAutomaticReconnect → hydrate already
│   ├── stores/                      # player-game.store.ts 10 elementos + computed remainingSeconds + methods hydrate/submitAnswer/withdraw/startTimerTick/bindRealtime already
│   ├── features/game/               # GAME SCREEN NEW/EXTEND:
│   │   ├── game.component.ts        # shell cinematic 3 áreas: Header (Current Round/Level/Timer), Center (Question+Four Answers), Footer (Score/Secured/Potential/Status/Withdraw)
│   │   ├── question.component.ts    # Four Answers radiogroup aria-checked, selectedOptionId signal, submitAnswer rxMethod, isCorrect only after EVALUATED
│   │   ├── timer.component.ts       # remainingSeconds computed, RUNNING/STOPPED/EXPIRED aria-live, warning <10s
│   │   ├── score-panel.component.ts # Current Score + Secured Points "500 pts · 200 asegurados" + Potential Reward "—" / next
│   │   └── withdrawal.component.ts  # Withdrawal Action modal confirm → store.withdraw() idempotente
│   └── features/shared/             # games.api.ts getMyState(submitAnswer/withdraw) already, games.api.ts already
│   └── shared/ui/                   # loading-skeleton, empty-state, error-state (CorrelationId) already
├── src/environments/                # environment.apiUrl, identityAuthority, gameHubUrl
tests/ (Vitest)
└── src/app/features/game/game.store.spec.ts / question.spec.ts / timer.spec.ts

src/OroQuizClash.Domain/              # No changes (Game.SubmitAnswer/WithdrawPlayer, PointTransaction ledger, RowVersion, AnswerWindowExpired already)
src/OroQuizClash.Application/
└── Features/Games/
    ├── GetMyPlayerState.cs          # Query 10 elementos already
    ├── SubmitAnswer.cs              # Command POST /answers already (X-Idempotency-Key, AnswerWindowExpired, QuestionAlreadyAnswered)
    ├── WithdrawPlayer.cs            # Command POST /withdraw already (X-Idempotency-Key, PlayerIdentityMismatch)
    └── GetGame.cs                   # Query GetGame already
src/OroQuizClash.Infrastructure/
└── Persistence/Configurations/      # GameTypeConfiguration, AnswerTypeConfiguration, PointTransactionTypeConfiguration already
src/OroQuizClash.Api/
├── Program.cs                       # AddCqrs, AddDbContext, JWT identity Authority jwks_uri already
└── appsettings.json
OroQuizClash.AppHost/AppHost.cs      # quizarena-player container node:22-alpine → oroclash-api + identity-api already (SPEC-027)
tests/
├── OroQuizClash.Domain.Tests/       # SubmitAnswer/Withdraw/Score ledger
├── OroQuizClash.Application.Tests/  # SubmitAnswerHandler idempotence, WithdrawPlayer
├── OroQuizClash.Api.Tests/          # Contract 10 elementos, SubmitAnswer idempotente, Withdraw, Timer not trusted
└── OroQuizClash.Architecture.Tests/ # No client trust, Domain ↛ Angular, no MediatR
```

**Structure Decision**: Extender SPA `QuizArena.Player` existente (`PlayerGameStore` 10 elementos scoped `gameId` + `GameRealtimeService` + `GamesApi` ya en SPEC-027) con feature `game/` cinematic 3 áreas (`Header` Round/Level/Timer, `Center` Question+Four Answers radiogroup, `Footer` Score/Secured/Potential/Status/Withdraw) usando `design-system/tokens` `data-theme="player"` sin nuevos agregados; reutilizar `oroclash-api` slices `GetMyPlayerState`/`SubmitAnswer`/`WithdrawPlayer` y `GameHub` eventos → `hydrate`; `OroQuizClash.AppHost` ya orquesta todo.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Angular SPA separado Blazor Admin (ya en 027) | Mandato SPEC-027/029 cinematic Player vs Admin; Angular 22 + SignalStore ya justificado | Reusar Blazor Admin rompería FR-013 `data-theme="player"` cinematic y mandato Angular |
| NgRx SignalStore para 10 elementos por GameSession | Mandato nota 4 SPEC-027 + `PlayerGameStore` 10 elementos privados con `computed` Timer y `rxMethod` submit/withdraw + `patchState` idempotente | `BehaviorSubject` manual duplica sincronización y carece de `DeepSignal` + `tapResponse` |
| SignalR `GameHub` con `withAutomaticReconnect` → `hydrate` | Realtime obligatorio FR-010 para N jugadores; drift correction Timer y score sin polling | Polling REST aumenta latencia y no escala a N concurrentes; trusting event payload viola V |
| Design System `data-theme="player"` tokens sin literales | FR-013/FR-014 cinematic/immersive/premium WCAG AA 375-1536 `Cinematic` requiere tokens centralizados | Estilos literales por componente rompen `design-system/MASTER.md` y no pasan axe/Lighthouse |

## Constitution Check (Post-Design Re-Check)

*Re-evaluado tras Phase 1 (research.md + data-model.md + contracts/ + quickstart.md):*

| Gate | Estado | Notas post-diseño |
|------|--------|-------------------|
| I–VI, H, I, J | ✅ PASS | Diseño refuerza V (evento→hydrate REST nunca payload como verdad, Timer serverNow correction) y H (PKCE `secureRoutes` + `must_change_password`). Ningún nuevo agregado. |
| A–G | ✅ PASS | Lifecycle, ledger, Outbox, SignalR preservados. 10 elementos hydrate <1s. |
| Complejidad | ✅ Justificada | 4 entradas ya justificadas en 027, todas por mandato explícito (Angular 22, SignalStore, SignalR, Design System). |

**Resultado final: PASS — proceder a `/speckit.tasks`.**
