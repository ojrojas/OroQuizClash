# Implementation Plan: Player Application

**Branch**: `027-player-application` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/027-player-application/spec.md`

## Summary

Aplicación web exclusiva del participante de QuizArena, construida en **Angular 22** con gestión de estado privado por instancia mediante **NgRx SignalStore** (`@ngrx/signals`). Cada jugador ejecuta una instancia independiente que mantiene un contexto privado aislado de 10 elementos (`Player`, `Game`, `Game Session`, `Round`, `Question`, `Answer`, `Score`, `Secured Points`, `Timer`, `Status`) sincronizado de forma autoritativa con `oroclash-api` (.NET 10 modular monolith). Soporta participación simultánea de N jugadores en el mismo juego sin interferencia, timer autoritativo, rehidratación resiliente y notificaciones server-driven vía SignalR. Autenticación delegada 100% a OroIdentityServer (OIDC `authorization_code` + `refresh_token` + `PKCE` para cliente público SPA). Consume `design-system/tokens/design-tokens.css` vía CSS variables (`design-system/MASTER.md` + `overrides/player.md`). Sin acceso directo a BD; sin lógica autoritativa en cliente.

## Technical Context

**Language/Version**: TypeScript 5.8+ / **Angular 22** (standalone components, control flow `@if`/`@for`, `input()`/`output()`, inject, `HttpClient` con `withFetch`, `provideRouter`). Node 22 LTS para tooling. Backend BFF/host: `net10.0` (si se requiere BFF — ver research R1).

**Primary Dependencies**:
- `@angular/core`, `@angular/router`, `@angular/common`, `@angular/forms` (22.x)
- `@ngrx/signals` + `@ngrx/signals/entities` + `@ngrx/signals/rxjs-interop` (instalación obligatoria nota 4: `npm install @ngrx/signals @ngrx/signals/entities @ngrx/signals/rxjs-interop`)
- `rxjs` 7.x (`rxMethod`, `tapResponse`, `interval`, `switchMap`)
- `@angular-architects/oauth2-oidc` *o* `angular-auth-oidc-client` 17+ para OIDC PKCE `authorization_code` + `refresh_token` contra OroIdentityServer discovery (decision en research R1); alternativa BFF usa `Microsoft.AspNetCore.Authentication.OpenIdConnect` + `Yarp.ReverseProxy` (mismo patrón que SPEC-017)
- `@microsoft/signalr` 8.x (`@microsoft/signalr` npm) para `GameHub` (`RoundStarted`, `QuestionAvailable`, `ScoreUpdated`, `RoundCompleted`, `GameFinished`)
- Design System: `design-system/tokens/design-tokens.css` (variables CSS, tema `player`)

**Storage**: Ninguno en cliente (oroclash DB primaria SQL Server + Oracle abstraction server-side; `identitydb` PostgreSQL aislada). Cliente persiste solo en memoria SignalStore + `sessionStorage` efímero para `AnswerSubmissionId` idempotente si se requiere reintento; nunca `localStorage` compartido entre identidades (FR-002/FR-003, edge case mismo dispositivo).

**Testing**: Vitest/Jest + Angular Testing Library + `provideMockStore` pattern para SignalStore; Karma opcional. Tests de stores: `inject()` + `patchState` assertions. Backend slices existentes: xUnit (`TestingPlatformDotnetTestSupport`). Arquitectura tests verifican aislamiento (no Domain → Angular).

**Target Platform**: Web SPA — Chrome/Edge/Firefox/Safari evergreen, responsive 375–1536px, WCAG 2.2 AA. Servida vía `ng serve` (dev) y `ng build --configuration production` → `dist/` hosteada por `QuizArena.Player` (ASP.NET Core static files) o contenedor nginx. Orquestada opcionalmente por `OroQuizClash.AppHost` (Aspire).

**Project Type**: web-application (Angular SPA + opcional BFF host `QuizArena.Player` net10.0).

**Performance Goals**: SC-004 timer drift <1s (95%); SC-005 score/secured <1s percibido; SC-006 E2E 5 rondas <3min (90%); SC-007 reconexión 10s → rehidratación sin pérdida 100%; listados/estado <1s (95%).

**Constraints**: Constitución V (server truth: correctitud/puntos/tiempo evaluados server-side), VI (OroIdentityServer única autoridad, `jwks_uri`, `must_change_password` gating), H (tokens nunca calculados en cliente), I (RFC 7807, CorrelationId/TraceId), WCAG 2.2 AA, 375-1536 sin scroll horizontal, objetivos ≥44px, `net10.0` para BFF/host si aplica.

**Scale/Scope**: Hasta `MaxPlayers` por juego (default 10, configurable); N instancias concurrentes por juego; 10 elementos de contexto por instancia; ~8 vistas/estados (loading/empty/ready/error/expired/terminal + lobby/game/result); SignalR fan-out por juego.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Evidencia |
|-----------|--------|-----------|
| I. Domain First | ✅ PASS | Cliente sin reglas autoritativas; `Game.Start/SubmitAnswer/Withdraw/Finish`, scoring ledger y políticas de retiro en `OroQuizClash.Domain` (existing). SignalStore solo proyecta estado autoritativo. |
| II. Clean Architecture | ✅ PASS | Dependencia `Player (Angular)` → `oroclash-api` (Web→Application→Domain←Infra). Sin referencia Domain→Angular. BFF/host (si aplica) es capa Web fina. |
| III. BuildingBlocks | ✅ PASS | Reusa `BuildingBlocks.ServiceDefaults` (OTel/health/resilience) en BFF/host y `BuildingBlocks.CQRS`/`EventBus`/`Kernel` en backend. No se reintroduce MediatR/MassTransit/AutoMapper. |
| IV. Vertical Slice + CQRS | ✅ PASS / N/A | Backend slices existentes (`CreateGame`, `JoinGame`, `SubmitAnswer`, `WithdrawPlayer`, `GetPlayerScore`, `GetCurrentRound`, `GetCurrentQuestion`, `GetLeaderboard`) consumidos vía `IEndpoint` + `ISender`. Sin slices nuevos si el API ya expone lo necesario (ver research R2). |
| V. Server Truth | ✅ PASS | FR-009/010/012: correctitud, puntos y tiempo evaluados server-side con server timestamps. Timer cliente es visual con corrección periódica; decisión expiración solo server. SignalR no es fuente de verdad (FR-005). |
| VI. OroIdentityServer | ✅ PASS | OIDC `authorization_code` + `refresh_token` (+ PKCE para SPA pública) contra `/.well-known/openid-configuration`, validación `jwks_uri`, claims `sub`/`roles`/`tenant_id`/`must_change_password`, `/connect/logout`. Sin user store local. |
| A. Game Lifecycle | ✅ PASS | Estados `DRAFT`..`FORCED_FINISHED` consumidos; transiciones inválidas rechazadas por dominio. |
| C. Configurable Rules | ✅ PASS | `TimeLimitPerQuestion`, `PointsPerRound`, políticas `KEEP_*`/`FALLBACK` configurables, inmutables tras Start. |
| D. Scoring Ledger | ✅ PASS | `Score`/`Secured Points` derivados de `PointTransaction` ledger, reconstruible. |
| F. Concurrency/Idempotency | ✅ PASS | `AnswerSubmissionId`/`IdempotencyKey` para `SubmitAnswer`; concurrencia optimista `rowversion` en `GamePlayer`/`Game`. |
| G. Realtime/Outbox | ✅ PASS | `GameHub` SignalR broadcaster server-driven; Outbox → RabbitMQ para integration events. |
| H. Security | ✅ PASS | JWT Bearer validado contra OroIdentityServer; `PlayerId` = `sub`; 401/403 + auditoría en intentos de suplantación. Rate limiting `GamePlayLimiter` existente. |
| I. Validation/Errors/Obs | ✅ PASS | Validación 3 niveles, RFC 7807 `ProblemDetails`, `CorrelationId`/`TraceId` OTel, audit append-only. |
| J. API & Frontend | ✅ PASS | REST `/api/games/{id}/...` + SignalR; frontend presentation-only; OIDC PKCE/BFF, `must_change_password` gating. |

**Resultado pre-Phase 0: PASS — sin violaciones.**

## Project Structure

### Documentation (this feature)

```text
specs/027-player-application/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── api-contracts.md       # REST consumed by Player (games/players/rounds/questions/answers/leaderboard)
│   ├── realtime-contracts.md  # SignalR GameHub events for Player
│   ├── auth-contracts.md      # OIDC PKCE/BFF configuration vs OroIdentityServer
│   └── signal-stores.md       # NgRx SignalStore slices for 10-element private context
├── checklists/requirements.md
└── tasks.md             # Phase 2 output (/speckit.tasks - NOT created here)
```

### Source Code (repository root)

```text
src/Player/
├── QuizArena.Player/                 # Angular 22 SPA (standalone, signals)
│   ├── angular.json                  # @angular/cli 22, builder @angular/build:application
│   ├── package.json                  # @angular/* 22, @ngrx/signals, rxjs, @microsoft/signalr, oauth2-oidc
│   ├── src/
│   │   ├── main.ts                   # bootstrapApplication(AppComponent, appConfig)
│   │   ├── app/
│   │   │   ├── app.component.ts      # shell: data-theme="player" + design-tokens.css
│   │   │   ├── app.routes.ts         # /lobby, /game/:id, /result/:id, /auth/callback, guards
│   │   │   ├── app.config.ts         # provideRouter, provideHttpClient(withFetch, withInterceptors), provideOAuthClient / provideAuth
│   │   │   ├── core/
│   │   │   │   ├── auth/             # OIDC PKCE config, AuthGuard, must_change_password guard, token interceptor
│   │   │   │   ├── interceptors/     # correlation-id, error (RFC7807), retry
│   │   │   │   └── realtime/         # GameHub SignalR service (withAutomaticReconnect, rehydrate)
│   │   │   ├── features/
│   │   │   │   ├── lobby/            # JoinGame, waiting room
│   │   │   │   ├── game/             # Round, Question (4 options), Answer, Timer, Score/Secured display
│   │   │   │   ├── result/           # Score final, Secured Points, Status terminal
│   │   │   │   └── shared/           # models DTO, api clients (GamesApi, RoundsApi, AnswersApi)
│   │   │   ├── stores/               # NgRx SignalStore slices (player.store.ts, game.store.ts, game-session.store.ts, round.store.ts, question.store.ts, answer.store.ts, score.store.ts, timer.store.ts)
│   │   │   └── shared/
│   │   │       ├── ui/               # Design System components (player theme, WCAG AA, 375-1536, 44px targets)
│   │   │       └── tokens/           # design-tokens.css import
│   │   └── environments/             # apiUrl, identityAuthority, signalR hub url
│   └── tests/
│       ├── stores/                   # *.store.spec.ts (SignalStore unit)
│       └── integration/              # realtime rehydrate, timer drift correction
├── QuizArena.Player.Host/            # (Opcional) ASP.NET Core net10.0 host para BFF/static files — si research R1 elige BFF
│   ├── Program.cs                    # AddServiceDefaults, OIDC Cookie + YARP forwarder /bff/{**} → oroclash-api, SPA fallback
│   └── QuizArena.Player.Host.csproj  # Microsoft.AspNetCore.Authentication.OpenIdConnect, Yarp.ReverseProxy (si BFF)

OroQuizClash.AppHost/AppHost.cs       # + builder.AddNpmApp("quizarena-player", "../src/Player/QuizArena.Player") o AddProject<Projects.QuizArena_Player_Host>

tests/
├── Player.Tests/                     # Angular store/component tests (Vitest)
└── OroQuizClash.Architecture.Tests/  # + PlayerNoDomainDependencyTests ([TBD] — verifica Domain ↛ Angular)
```

**Structure Decision**: SPA Angular 22 standalone en `src/Player/QuizArena.Player` como única fuente de la experiencia del jugador, separada de `src/Admin/QuizArena.Admin` (Blazor). Stores NgRx SignalStore aislados por `GameSession` (FR-003). Si research R1 elige BFF, se añade `QuizArena.Player.Host` net10.0 mínimo (mismo patrón YARP que SPEC-017) para no exponer tokens en el navegador; si elige PKCE público, el SPA autentica directo contra OroIdentityServer sin host adicional (tokens en memoria con `refresh_token` + `sessionStorage` efímero). En ambos casos `OroQuizClash.AppHost` orquesta `identity-api` → `oroclash-api` → `quizarena-player`.

## Constitution Check (Post-Design Re-Check)

*Re-evaluado tras Phase 1 (research.md + data-model.md + contracts/ + quickstart.md):*

| Gate | Estado | Notas post-diseño |
|------|--------|-------------------|
| I–VI, H, I, J | ✅ PASS | Diseño refuerza V (evento→rehidratación REST, nunca estado de evento como verdad) y H (PKCE en memoria + `secureRoutes` solo a `oroclash-api`; BFF alternativo documentado). Ningún nuevo agregado de dominio salvo `GetMyPlayerState` proyección (no viola I). |
| A–G | ✅ PASS | Ledger, lifecycle, Outbox, SignalR preservados. Timer derivado de `expiresAt` sin lógica en dominio. |
| Complejidad | ✅ Justificada | 3(+1 condicional) entradas en Complexity Tracking, todas por mandato explícito (Angular 22, SignalStore nota 4, SignalR multiplayer). |

**Resultado final: PASS — proceder a `/speckit.tasks`.**

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Angular SPA separado de Blazor Admin | Mandato explícito SPEC-027: naturaleza distinta del producto; experiencia del jugador aislada vs administración | Reusar Blazor Admin para jugador rompería FR-002/FR-003 (contexto privado por instancia con SignalStore) y mandato Angular 22 |
| NgRx SignalStore (`@ngrx/signals`) como gestión de estado | Mandato nota 4 + skill instalada; 10 elementos de contexto privados con reactividad granular, `patchState`/`rxMethod` y `withComputed` para Timer derivado | Servicios `BehaviorSubject` manuales duplican lógica de sincronización, carecen de `DeepSignal` tracking y `rxMethod`/`tapResponse` para efectos idempotentes |
| SignalR `@microsoft/signalr` en Angular | Realtime obligatorio FR-005/FR-017 para N jugadores simultáneos; reconexión automática y rehidratación | Polling REST aumenta latencia y carga (SC-004/SC-005 <1s imposibles) y no escala a N concurrentes |
| (Condicional) BFF host `QuizArena.Player.Host` si R1 elige BFF | Evita exponer `access_token`/`refresh_token` en el navegador (Constitución H) | SPA PKCE público expone tokens en memoria; BFF mantiene tokens en cookie httpOnly server-side — tradeoff evaluado en research R1 |
