# Implementation Plan: Player Answering

**Branch**: `031-player-answering` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/031-player-answering/spec.md`

## Summary

Interacción del jugador con exactamente cuatro opciones — 8 estados visuales (`Idle→Hover→Selected→Locked→Evaluating→Correct/Incorrect/Timeout`), selección única con bloqueo inmutable (`Locked` no modificable, debounce 150ms), y veredicto autoritativo backend (`POST /api/games/{id}/answers` `X-Idempotency-Key` per `roundId` `sessionStorage`, `isCorrect` solo tras `EVALUATED`, `AnswerWindowExpired` → `Timeout`). Extiende `QuizArena.Player` Angular 22 SPA (SPEC-027/029/030) con `QuestionComponent`/`AnswerInteractionStore` (o extensión `PlayerGameStore` con `selectedOptionId/lockedOptionId/phase/isEvaluating/canSelect` `computed` + `rxMethod confirmLock/submitAnswer`) + `AnswerOptionComponent` `role="radiogroup"` 2x2 grid, reutilizando `GamesApi.submitAnswer`/`getMyState`, `GameRealtimeService` `withAutomaticReconnect` → `hydrate` (Server Truth V), `design-system/tokens` `data-theme="player"` cinematic, OIDC PKCE OroIdentityServer, `RowVersion` + idempotencia ledger.

## Technical Context

**Language/Version**: TypeScript 5.8+ / **Angular 22** (standalone `input()` `signal()` `computed()` `@if/@for`, `provideRouter`, `HttpClient withFetch` `withInterceptors`, `rxMethod`) Node 22 LTS + C# 12 / .NET 10.0 (`net10.0`) para `oroclash-api` slices ya existentes (`SubmitAnswer`/`GetMyPlayerState`).

**Primary Dependencies**: `@angular/core/router/common/forms` 22.x, `@ngrx/signals` 18.1 + `@ngrx/signals/entities` + `rxjs 7.x` (`rxMethod`, `tapResponse`, `debounceTime`, `switchMap`), `angular-auth-oidc-client` 17+ PKCE `authorization_code`+`refresh_token`, `@microsoft/signalr` 8.x `GameHub` `QuestionAvailable/ScoreUpdated/RoundCompleted` `withAutomaticReconnect`, `design-system/tokens/design-tokens.css` (`data-theme="player"`); Backend `BuildingBlocks.Kernel.Domain` (`AggregateRoot`, `IBusinessRule`, `Result`, `Enumeration`), `BuildingBlocks.CQRS` (`ICommand` `SubmitAnswer` `ISender`), `BuildingBlocks.Kernel.Infrastructure` (`AppDbContextBase`, `EfRepository`, `IUnitOfWork`, `Outbox`), `BuildingBlocks.ServiceDefaults` (OTel, health, `IEndpoint`, `GlobalExceptionHandler`).

**Storage**: SQL Server primaria `oroclash` (`Game` RowVersion + `GameRound` + `Answer` `UNIQUE (GameId,RoundId,PlayerId)` + `UNIQUE IdempotencyKey` + `PointTransaction` ledger, `Question` 4/1 `CHECK exactly one correct`, `Reward` opcional, Outbox); PostgreSQL `identitydb` aislada OroIdentityServer; cliente solo memoria `AnswerInteractionState` `DeepSignal` + `sessionStorage` efímero `idemp-{roundId}` per `Round` nunca `localStorage`.

**Testing**: Vitest 3 + Angular Testing Library + `provideHttpClientTesting` + `TestBed` para `QuestionComponent` (4 opciones, 8 estados `Idle`→`Timeout`, single `Selected`, `Locked` inmutable, `Evaluating` spinner `aria-busy`, `Correct/Incorrect/Timeout` tokens, debounce, `aria-checked/disabled`, keyboard `Tab/Space/Enter`, `prefers-reduced-motion`) y `AnswerInteractionStore` (`selected/locked/evaluating`, `canSelect`, `hydrate` restore `Locked`); xUnit v3 + NSubstitute + Testcontainers.MsSql para `SubmitAnswer` idempotente + `AnswerWindowExpired` + `QuestionAlreadyAnswered` + `isCorrect` server-only; `dotnet test` TestingPlatform, `ng test --watch=false`, `ng lint` `@ngrx/eslint-plugin`, axe/Lighthouse WCAG.

**Target Platform**: Web SPA evergreen Chrome/Edge/Firefox/Safari responsive 375–1536 WCAG 2.2 AA, `ng serve` dev `ng build` prod `dist/` hosteado via `QuizArena.Player` static files o container `node:22-alpine` orquestado por `OroQuizClash.AppHost` (sqlserver/postgres/redis/rabbitmq/identity-api/oroclash-api/quizarena-player).

**Project Type**: web-application (Angular SPA `src/Player/QuizArena.Player` + modular monolith `Domain/Application/Infrastructure/Api`).

**Performance Goals**: SC-005 100% veredicto backend `Correct/Incorrect/Timeout` <1s 95% percibido SC-007; SC-006 100% idempotencia misma `X-Idempotency-Key` sin duplicar ledger; SC-004 Locked inmutable 100% + 409 sin nuevo `PointTransaction`; SC-002 8 estados 100% tokens/`aria-live`; SC-008 375-1536 sin scroll 100% 1 col 375 / 2x2 ≥768 targets ≥44px.

**Constraints**: Constitución V server truth (`isCorrect` solo tras `EVALUATED`, `submittedAt <= expiresAt` decide `Timeout`, SignalR nunca fuente veredicto); VI OroIdentityServer PKCE `jwks_uri` `sub=PlayerId` `must_change_password` gating; H `secureRoutes=[apiUrl]` Bearer solo `oroclash-api` + `X-Correlation-Id`; I RFC7807 `ProblemDetails` `CorrelationId/TraceId`; J REST `IEndpoint` thin `ISender`; WCAG `role="radiogroup"` `aria-checked/posinset/setsize/disabled` `aria-live polite/assertive` `outline:2px` `prefers-reduced-motion`; Design System `data-theme="player"` sin literales 8 estados tokens (`--color-primary/success/error/warning`).

**Scale/Scope**: 4 opciones por pregunta (exactamente 4, 1 correcta), 8 estados por opción, 1 `Selected` única por pregunta, 1 `Locked` inmutable por `Round`, N rondas por juego `MaxRounds` 5–15 default 10, N jugadores por juego `MaxPlayers` 10 default aislados per `GameSession`, debounce 150ms, `X-Idempotency-Key` UUID per `playerId+roundId`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Evidencia |
|-----------|--------|-----------|
| I. Domain First | ✅ PASS | Respuesta es `Game.SubmitAnswer(selectedOptionId, submittedAt, expiresAt)` dominio con `IBusinessRule` `AnswerWindowExpired` + `QuestionAlreadyAnswered` + `ExactlyOneCorrect` (SPEC-006). `QuestionComponent` no contiene lógica autoritativa. |
| II. Clean Architecture | ✅ PASS | `Player (Angular QuestionComponent)` → `oroclash-api SubmitAnswer ICommand` → `Application→Domain←Infrastructure`. Domain no referencia Angular. `AnswerInteractionState` es view-model. |
| III. BuildingBlocks No Reinvention | ✅ PASS | Reusa `AggregateRoot/Result/IBusinessRule/Specification/IRepository/IEndpoint/ISender/AppDbContextBase/Outbox/ServiceDefaults` ya en 006/029. No MediatR/MassTransit/AutoMapper. Solo Angular `@ngrx/signals` (permitido frontend). |
| IV. Vertical Slice + CQRS | ✅ PASS | Reusa slice `SubmitAnswer` (`Command` + `Validator` + `Handler` + `Response DTO` + `IEndpoint` thin `ISender`) y `GetMyPlayerState`/`GetCurrentQuestion` Queries. Sin carpeta genérica. |
| V. Server Truth | ✅ PASS | `isCorrect`/`Correct/Incorrect/Timeout` solo vía `POST /answers` `EVALUATED`/`EXPIRED` con server `submittedAt`; `isCorrect` filtrado para `PLAYER` antes de `EVALUATED`; SignalR nunca fuente veredicto; `Timer` cliente visual con `serverNow` corrección. |
| VI. OroIdentityServer | ✅ PASS | PKCE `authorization_code`+`refresh_token` contra `/.well-known/openid-configuration`, `jwks_uri`, `sub=PlayerId`, `must_change_password` guard, sin user store local. `POST /answers` requiere JWT. |
| A. Game Lifecycle | ✅ PASS | 9 estados `WAITING→IN_PROGRESS→ROUND_IN_PROGRESS↔ROUND_COMPLETED→FINISHED` protegidos; `canAnswer = !isTerminal && round IN_PROGRESS && answer PENDING` bloquea selector en terminal. |
| B. Category Invariants | ✅ PASS | 4 opciones 1 correcta, ≥5 por categoría para `PUBLISHED`; `Question.Create` DB `CHECK exactly one correct` + publish requiere ≥5; selector valida exactamente 4 sino `ErrorState`. |
| C. Configurable Rules | ✅ PASS | `TimeLimit` 5..300 inmutable tras Start, solo proyección para `Timeout`; `LossPolicy`/`Points` no hardcodeados. |
| D. Ledger | ✅ PASS | `Correct`/`Incorrect` generan `PointTransaction` ledger `ANSWER_CORRECT/INCORRECT` reconstruible `sum=total`; cliente nunca calcula `Correct`. |
| F. Concurrency/Idempotency | ✅ PASS | `RowVersion` en `Game`/`Answer`, `X-Idempotency-Key` per `roundId` `UNIQUE IdempotencyKey` + `UNIQUE (GameId,RoundId,PlayerId)` → `QuestionAlreadyAnswered` idempotente 200 sin duplicar ledger; debounce 150ms. |
| G. Realtime/Outbox | ✅ PASS | `GameHub` `QuestionAvailable` → `hydrate` nueva pregunta; `ScoreUpdated` → `hydrate` tras `Correct`; Outbox→RabbitMQ nunca antes commit; selector no muta desde evento directo. |
| H. Security Delegated | ✅ PASS | JWT `jwks_uri`, `PlayerIdentityMismatch` 403 auditada, `X-Correlation-Id` prop., payload nunca incluye `isCorrect` pre-EVALUATED para `PLAYER`. |
| I. Validation/Errors/Obs | ✅ PASS | 3 niveles, RFC7807 `ProblemDetails` `AnswerWindowExpired 400`/`QuestionAlreadyAnswered 409`/`InvalidAnswer 400` con `CorrelationId/TraceId`, OTel `CorrelationId/TraceId/GameId/PlayerId/RoundId/QuestionId`. |
| J. API & Frontend | ✅ PASS | REST `POST /api/games/{id}/answers` + `GET /api/games/{id}/players/me` + `GET /api/games/{id}/rounds/current/questions/current` filtrado, DTOs boundary, `RequireAuthorization`, frontend presentation-only PKCE. |

**Resultado pre-Phase 0: PASS — sin violaciones.**

## Project Structure

### Documentation (this feature)

```text
specs/031-player-answering/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── api-contracts.md       # POST /api/games/{id}/answers + GET /players/me (Answer) + GET /questions/current
│   └── ui-contracts.md        # 4 opciones 8 estados Idle→Timeout, single Selected→Locked, radiogroup a11y, responsive premium
├── checklists/requirements.md
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/Player/QuizArena.Player/         # Angular 22 SPA (standalone) — extends SPEC-027 (Player App) + 029 (Game) + 030 (Rounds)
├── src/app/
│   ├── app.routes.ts                # /game/:gameId (authGuard, mustChangePasswordGuard) already
│   ├── app.config.ts                # provideAuth PKCE, HttpClient interceptors already
│   ├── core/interceptors/           # correlationId, auth secureRoutes, error RFC7807 already
│   ├── core/realtime/               # game-realtime.service.ts withAutomaticReconnect → hydrate already
│   ├── stores/
│   │   ├── player-game.store.ts     # 10 elementos + Answer + Timer + status canAnswer already (029) — extend with answering phase if needed
│   │   ├── player-rounds.store.ts   # Ladder Round 1..N already (030)
│   │   └── answer-interaction.store.ts # NEW (o ext. player-game.store): signalStore AnswerInteractionState {selectedOptionId, lockedOptionId, phase idle|selected|locked|evaluating|correct|incorrect|timeout, isEvaluating, canSelect}, computed, rxMethod confirmLock/submitAnswer(-> POST /answers X-Idempotency-Key), hydrateAnswer(), debounce 150ms
│   ├── features/game/
│   │   ├── game.component.ts        # EXTEND: embed <app-question> with 4 opciones + AnswerInteractionStore, grid 280px 1fr (030) + center question
│   │   ├── question.component.ts    # EXTEND: 4 opciones *ngFor AnswerOption, states Idle/Hover/Selected/Locked/Evaluating/Correct/Incorrect/Timeout, role="radiogroup" + role="radio" aria-checked/posinset/setsize/disabled, (click)/(keydown Space/Enter) -> select, Confirmar button 44px disabled !selected||isLocked||isEvaluating, spinner evaluating, Correct success/ Incorrect error + correcta secondary, Timeout warning, ErrorState CorrelationId
│   │   ├── question.component.css  # NEW/EXTEND: tokens data-theme="player" var(--space-*/--color-*) 1 col 375 / 2x2 ≥768 gap var(--space-3), min-height 44px, Hover var(--color-primary), Selected var(--color-primary-subtle) check, Locked opacity 0.7 disabled, Evaluating pulse var(--color-primary), Correct var(--color-success) + Incorrect var(--color-error) + Timeout var(--color-warning), prefers-reduced-motion none
│   │   ├── answer-option.component.ts # NEW optional: presentational AnswerOption card with 8 states, inputs option + state, outputs select
│   │   ├── player-rounds.component.ts # already (030) — selector vive junto a ladder en misma pantalla
│   │   ├── timer.component.ts       # already (029) — drives Timeout decision visual
│   │   └── score-panel.component.ts # already (029)
│   └── features/shared/             # games.api.ts getMyState + submitAnswer(gameId,dto X-Idempotency-Key) already + getCurrentQuestion() if needed
│   └── shared/ui/                   # loading-skeleton, empty-state, error-state (CorrelationId) already reuse
├── src/environments/                # environment.apiUrl, identityAuthority, gameHubUrl already
tests/ (Vitest)
└── src/app/
    ├── stores/answer-interaction.store.spec.ts # selected single, Locked inmutable, Evaluating→Correct/Incorrect/Timeout server, idempotency, hydrate restore
    └── features/game/question.component.spec.ts # 4 opciones Idle/Hover, Selected single radiogroup, Locked no change, Evaluating spinner, Correct/Incorrect/Timeout tokens, debounce, a11y axe

src/OroQuizClash.Domain/              # No changes (Game.SubmitAnswer, Question 4/1, Answer AlreadyAnswered, AnswerWindowExpired)
src/OroQuizClash.Application/
└── Features/Games/
    ├── SubmitAnswer.cs              # Command already (POST /answers idempotent, X-Idempotency-Key, AnswerWindowExpired, QuestionAlreadyAnswered, IsCorrect server)
    ├── GetMyPlayerState.cs          # Query already — returns question/answer/timer/status for AnswerInteractionState hydrate
    └── GetCurrentQuestion.cs        # Query optional — returns Question filtered isCorrect for PLAYER if needed
src/OroQuizClash.Infrastructure/
└── Persistence/Configurations/      # already (Question CHECK exactly one correct, Answer UNIQUE GameId+RoundId+PlayerId + IdempotencyKey, Game RowVersion)
src/OroQuizClash.Api/
├── Program.cs                       # AddCqrs, AddDbContext, JWT identity Authority jwks_uri already
└── appsettings.json
OroQuizClash.AppHost/AppHost.cs      # quizarena-player container node:22-alpine → oroclash-api + identity-api already (SPEC-027)
tests/
├── OroQuizClash.Domain.Tests/       # Question 4/1, SubmitAnswer Correct/Incorrect/Timeout AlreadyAnswered
├── OroQuizClash.Application.Tests/  # SubmitAnswerHandler idempotency, AnswerWindowExpired
├── OroQuizClash.Api.Tests/          # Contract POST /answers idempotent, isCorrect filtered, Question 4/1
└── OroQuizClash.Architecture.Tests/ # Domain ↛ Angular, SubmitAnswer uses sub, no client isCorrect trust
```

**Structure Decision**: Extender SPA `QuizArena.Player` existente (`PlayerGameStore` 10 elementos + `GameRealtimeService` + `GamesApi` ya en 027/029/030) con `AnswerInteractionStore` (o extensión `PlayerGameStore`) para 8 estados `Idle→Timeout` single `Selected→Locked` inmutable + `QuestionComponent` 4 opciones `role="radiogroup"` 2x2 grid (`role="radio"` `aria-checked/posinset/disabled`) + `POST /answers` `X-Idempotency-Key` idempotente (Server Truth V, `isCorrect` solo tras `EVALUATED`, `Timeout` `submittedAt<=expiresAt`); reutiliza `oroclash-api` `SubmitAnswer`/`GetMyPlayerState` + `GameHub` → `hydrate` y `OroQuizClash.AppHost` ya orquesta todo; no nuevo agregado dominio salvo view-model `AnswerInteractionState`.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Angular SPA separado Blazor Admin (ya en 027) | Mandato SPEC-027/029/030/031 cinematic Player vs Admin; Angular 22 + SignalStore ya justificado | Reusar Blazor Admin rompería FR-011 `data-theme="player"` cinematic y mandato Angular 22 |
| NgRx SignalStore para AnswerInteractionState 8 estados per Round | Mandato nota 4 SPEC-027 + `AnswerInteractionStore` con `computed canSelect` + `rxMethod confirmLock/submitAnswer` + debounce 150ms + `patchState` idempotente; 8 estados + single selection + Locked inmutable + Evaluating→Correct/Incorrect/Timeout derivados | `BehaviorSubject` manual duplica sincronización, carece de `DeepSignal` + `tapResponse` + debounce + computed memoization |
| SignalR `GameHub` `withAutomaticReconnect` → `hydrate` para Answer | Realtime obligatorio para `QuestionAvailable` nueva pregunta (reset `Idle`) y `ScoreUpdated` post `Correct`; polling aumenta latencia y no escala | Polling REST sin SignalR no notifica nueva pregunta sin delay; trusting event payload para `isCorrect` viola V |
| Design System `data-theme="player"` tokens sin literales + 8 estados | FR-011/012 cinematic premium WCAG AA 375-1536 + SC-008/009 8 estados tokens (`--color-primary/success/error/warning`) requieren tokens centralizados | Estilos literales por opción rompen `design-system/MASTER.md` y no pasan axe/Lighthouse |
| AnswerInteractionStore separado (o ext. mínima PlayerGameStore) | Selector es feature independiente (4 opciones + 8 estados + confirm/lock + evaluating) con ciclo de vida per `Round`, testeable aislado; acoplar todo en `PlayerGameStore` inflaría 10 elementos a 15+ mixed concerns | Extender `PlayerGameStore` directo mezcla AnswerInteractionState con `Score/Timer/Rounds`, rompe SRP y testeabilidad (029 vs 031) |

## Constitution Check (Post-Design Re-Check)

*Re-evaluado tras Phase 1 (research.md + data-model.md + contracts/ + quickstart.md):*

| Gate | Estado | Notas post-diseño |
|------|--------|-------------------|
| I–VI, H, I, J | ✅ PASS | Diseño refuerza V (SubmitAnswer server `isCorrect` filtrado, `Evaluating` hasta `EVALUATED`, `Timeout` `submittedAt<=expiresAt`) y H (PKCE `secureRoutes` + `must_change_password`, `X-Idempotency-Key`). Ningún nuevo agregado. |
| A–G | ✅ PASS | Lifecycle `PUBLISHED` 4/1, `canAnswer` terminal block, `RowVersion` + idempotency, Outbox→RabbitMQ preservados. 4 opciones `UNIQUE` + `CHECK exactly one correct`. |
| Complejidad | ✅ Justificada | 4 entradas ya justificadas en 027/029/030 + 1 nueva (`AnswerInteractionStore` 8 estados) por SRP; todas por mandato explícito (Angular 22, SignalStore, SignalR, Design System). |

**Resultado final: PASS — proceder a `/speckit.tasks`.**
