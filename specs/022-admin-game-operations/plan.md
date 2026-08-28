# Implementation Plan: Admin Game Operations

**Branch**: `022-admin-game-operations` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/022-admin-game-operations/spec.md`

## Summary

Superficie de **operación en vivo** para partidas en ejecución: **10 indicadores** (`Game Status`, `Current Round`, `Current Question` A–D, `Players`/`Connected`/`Answered`/`Waiting`, `Scores` ledger, `Current Level`, `Game Timer` congelable) en vista `/admin/live/{gameId}` (y listado `/admin/live`), y **4 acciones controladas** `Pause` (`Running→Paused`), `Resume` (`Paused→Running`), `Cancel` y `Force Finish` (terminales) con confirmación, `RowVersion`/`IdempotencyKey` y **auditoría append-only**. Actualización sin recarga (polling 3–5s o WebSocket via BFF `MapForwarder("/hubs/game")` ya existente). Reutiliza 100% shell Blazor Auto net10.0 + BFF YARP→`oroclash-api` + OIDC OroIdentityServer + tema `administration` de SPEC-016. Extiende `Game`/`GameRound`/`GamePlayer` de dominio y `LiveGamesService` de `012-realtime-game-events` sin duplicar hub; consumo exclusivo vía BFF, sin acceso directo a SQL/Oracle/`identitydb`.

## Technical Context

**Language/Version**: C# latest / **.NET 10 (`net10.0`) único target** (global.json 10.0.400, Directory.Build.props net10.0). Sin net11 multi-target.

**Primary Dependencies**:
- ASP.NET Core Blazor Web App Auto (proyectos existentes `QuizArena.Admin` + `QuizArena.Admin.Client`)
- `Yarp.ReverseProxy` (`AddHttpForwarderWithServiceDiscovery()` + `MapBffForwarder()` + `MapGameHubForwarder()` ya existentes en 017)
- `Microsoft.AspNetCore.SignalR.Client` (ya cableado para `LiveGamesService` via BFF forwarder)
- `Microsoft.AspNetCore.Authentication.OpenIdConnect` + Cookie + `ConfigureCookieOidc` (refresh no-interactivo)
- `BuildingBlocks.ServiceDefaults` (OTel, health, resilience)
- Design System SPEC-016 — `design-system/tokens/design-tokens.css` + tema `administration` (tokens, componentes §9)

**Storage**: Ninguno en Admin. Estado vivo persiste en `oroclash-api` (SQL Server primario, abstracción Oracle, `rowversion` + `PointTransaction` ledger). Admin MUST NOT tocar DB; todo via `GET /bff/games/{id}` + `leaderboard`/`players`/`questions/current` y `POST /bff/games/{id}/pause|resume|cancel|force-finish` → `oroclash-api` + hub `/hubs/game` reenviado (FR-016, SC-005).

**Testing**: xUnit (`TestingPlatformDotnetTestSupport`); Architecture Tests (`DesignSystemNoDirectDbTests`, `AdminBffTests`); bUnit opcional para `LiveGameView`/`LiveScoresTable`; `WebApplicationFactory` + OIDC mock para `AdminOrGameManager` vs `REWARD_MANAGER` 403; pruebas `rowversion`/`IdempotencyKey` en `tests/QuizArena.Admin.Tests`.

**Target Platform**: Web — Blazor Web App Auto (server+WASM) orquestado por `OroQuizClash.AppHost` 13.5.x (service discovery `oroclash-api`, `identity-api`, `hubs/game`).

**Project Type**: web-application — extensión de feature en 2 proyectos existentes (no nuevos proyectos).

**Performance Goals**: SC-001 vista con 10 indicadores <3s percibidos (carga <2s con skeleton); SC-002 contadores/scores ≤3s tras respuesta; Game Timer sincronizado con servidor y congelado en `Paused`; overhead BFF <100ms; polling 3–5s vs WebSocket push.

**Constraints**: BFF obligatorio (tokens nunca en navegador); WCAG 2.2 AA tema claro; responsive 375–1536 sin scroll horizontal; objetivos táctiles ≥44px; net10.0 único; OIDC solo OroIdentityServer; `must_change_password` gating antes de supervisar (Constitución VI/J); `rowversion` + `IdempotencyKey` para idempotencia.

**Scale/Scope**: Operadores internos `ADMIN`/`GAME_MANAGER` (supervisan/controlan), `REWARD_MANAGER` denegado; decenas de sesiones; 1 vista live con 10 indicadores + 4 acciones auditadas; ~3 nuevas DTOs; hub `GameHub` ya existente (`GameStarted`, `QuestionAvailable`, `ScoreUpdated`, `RoundCompleted`, `GameFinished`).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Evidencia |
|-----------|--------|-----------|
| I. Domain First | ✅ PASS | Supervisión y transiciones (`Pause`/`Resume`/`Cancel`/`ForceFinish`) validadas en dominio `Game`/`GameRound` (001/005); UI solo proyecta |
| II. Clean Architecture | ✅ PASS | Admin = presentación (J); `Web → BFF → Api → Application → Domain`; sin acceso directo a DB |
| III. BuildingBlocks | ✅ PASS | Reusa `BuildingBlocks.ServiceDefaults` + `BuildingBlocks.Kernel` primitives via API; no MediatR/MassTransit/AutoMapper añadido |
| IV. CQRS | ✅ PASS | Consume `PauseGame`, `ResumeGame`, `CancelGame`, `ForceFinishGame` slices existentes (BuildingBlocks.CQRS) |
| V. Server Truth | ✅ PASS | `Scores` ledger, `Game Timer` derivado de `StartedAt` server-side, `Current Question` autoridad del backend; UI nunca calcula puntos/tiempo |
| VI. OroIdentityServer | ✅ PASS | OIDC `authorization_code` + `refresh_token` vs discovery; `must_change_password` gating; sin login propio (FR-018) |
| A. Game Lifecycle | ✅ PASS | 8 estados admin mapeados a dominio `Game`/`GameRound` (`DRAFT`–`IN_PROGRESS`–`ROUND_*`–`FINISHED`); transiciones inválidas rechazadas con `InvalidGameState` |
| D. Scoring via Ledger | ✅ PASS | `Scores` reconstruidos desde `PointTransaction` ledger (Constitución D), no mutación directa |
| F. Concurrency | ✅ PASS | `rowversion` optimista + `IdempotencyKey` protegen `Pause`/`Resume` concurrentes (SC-008) |
| H. Security | ✅ PASS | BFF YARP adjunta Bearer server-side; `AdminOrGameManager` claim-based; REWARD_MANAGER 403 sin fuga (SC-004) |
| I. Observability | ✅ PASS | `AddServiceDefaults()` (OTel, /health); CorrelationId propagado; ProblemDetails RFC7807 sin fuga (FR-014/015) |
| J. API & Frontend | ✅ PASS | Presentation-only; DTOs boundary; paginación; auth OIDC code+refresh |
| Addendum 2 (UI/UX) | ✅ PASS | Consume SPEC-016 tema administration, tokens sin literales, estados Loading/Ready/Empty/Error por indicador (FR-009) |
| net10.0 único | ✅ PASS | Extiende proyectos net10.0 existentes; no nuevo TFM |

**Resultado pre-Phase 0: PASS — sin violaciones.**

## Project Structure

### Documentation (this feature)

```text
specs/022-admin-game-operations/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── live-game-bff.md
│   └── live-operations.md
├── checklists/requirements.md
└── tasks.md             # Phase 2 output (/speckit.tasks - NOT created here)
```

### Source Code (repository root)

```text
src/Admin/
├── QuizArena.Admin/                    # Blazor Web App host (net10.0) — existente de 017
│   ├── Components/
│   │   ├── App.razor                   # ya con <html data-theme="administration">
│   │   ├── Routes.razor                # must_change_password gating existente
│   │   ├── Layout/MainLayout, NavMenu  # NavMenu ya filtra por rol
│   │   └── Pages/
│   │       └── (reusa Dashboard de 018)
│   ├── Components/LiveGame/            # NUEVO — vista en vivo
│   │   ├── LiveGameHeader.razor        # GameStatus badge + CurrentRound/Level + GameTimer
│   │   ├── LiveQuestionCard.razor      # CurrentQuestion con 4 opciones + skeleton
│   │   ├── LivePlayersPanel.razor      # Players/Connected/Answered/Waiting con aria-live
│   │   ├── LiveScoresTable.razor       # Scores ledger + CurrentLevel
│   │   └── LiveOperationsBar.razor     # 4 acciones con confirmación + RowVersion
│   └── Services/
│       └── (reusa BffForwarderExtensions, CookieOidcRefresher, ServerLiveGamesService)
└── QuizArena.Admin.Client/             # WASM (net10.0) — existente
    ├── Models/LiveGame/                # NUEVO — DTOs LiveGameView, LiveScores, GameOperation
    │   ├── LiveGameView.cs             # 10 indicadores + RowVersion
    │   ├── LiveScores.cs               # PlayerId/DisplayName/Score/SecuredPoints/Level
    │   └── GameOperation.cs            # Pause/Resume/Cancel/ForceFinish + IdempotencyKey
    ├── Services/
    │   ├── ILiveGameOperationsService.cs # NUEVO — contrato Pause/Resume/Cancel/ForceFinish
    │   ├── ClientLiveGameOperationsService.cs # WASM → /bff/games/{id}/* (cookie)
    │   └── LiveGamePoller.cs           # polling 3–5s fallback si WebSocket no disponible
    └── Pages/LiveGame/                 # NUEVO — páginas Live
        ├── LiveGames.razor             # ya existe — enriquecer con 10 indicadores
        └── LiveGameDetail.razor        # NUEVA — /admin/live/{gameId} con live view

OroQuizClash.Domain/Games/              # ya existe (001/005) — Game aggregate + GameRound + PointTransaction
OroQuizClash.Application/Features/Games/ # ya existen slices PauseGame, ResumeGame, CancelGame, ForceFinishGame (si falta, se añade)

tests/QuizArena.Admin.Tests/
├── LiveGameViewTests.cs                # NUEVO — 10 indicadores + coherencia ledger
└── LiveOperationsTests.cs              # NUEVO — 4 acciones, guardas, concurrencia, auditoría, 403

tests/OroQuizClash.Architecture.Tests/
└── (reusa DesignSystemNoDirectDbTests, AdminBffTests)
```

**Structure Decision**: Extensión in-place de los dos proyectos Auto creados en 017/018. Sin nuevo proyecto/host. `ILiveGameOperationsService` vive en `QuizArena.Admin.Client` (referenciado por server) con doble implementación `Client*` → `/bff/games/{id}/*` (cookie) y `Server*` → `http://oroclash-api/api/games/{id}/*` (Bearer del `HttpContext`). Reusa `LiveGamesService` y hub `GameHub` de `012-realtime-game-events` ( `MapForwarder("/hubs/game")` ya existe) para push; fallback a polling 3–5s si WebSocket no disponible. BFF forwarder catch-all ya cubre `/bff/games/{id}` y `/bff/games/{id}/leaderboard`.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) | — | — |

No se introduce nueva complejidad respecto a 017–021. YARP, OIDC, SignalR, `rowversion`/`IdempotencyKey` y ledger ya justificados en 001, 005, 012 y 017; este feature los reutiliza sin nuevas dependencias.

## Constitution Check (Post-Design Re-Check)

*Re-evaluado tras Phase 1 (research.md + data-model.md + contracts/ + quickstart.md):*

| Gate | Estado |
|------|--------|
| I–VI, A, D, F, H, I, J | ✅ PASS — supervisión 10 indicadores + `Scores` ledger + `Game Timer` server-side + `rowversion`/`IdempotencyKey` refuerzan V/D/F; BFF + OIDC refuerzan H/VI |
| Addendum 2 UI | ✅ PASS — contracts exigen tokens SPEC-016, estados por indicador, WCAG AA, responsive 375–1536, 44px |
| IV CQRS | ✅ PASS — reuse slices `PauseGame` etc. sin duplicar |
| Complejidad | ✅ PASS — 0 violaciones nuevas |

**Resultado final: PASS — proceder a `/speckit.tasks` (Phase 2).**
