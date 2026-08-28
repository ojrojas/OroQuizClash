# Implementation Plan: Admin Game Configuration

**Branch**: `019-admin-game-configuration` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/019-admin-game-configuration/spec.md`

## Summary

Superficie administrativa para **crear y configurar partidas antes de iniciar** (16 campos: nombre, descripción, categoría, rondas, jugadores máximos, tiempo por pregunta, dificultad inicial/progresión, puntuación, puntos asegurados, reglas retiro/finalización, premios final/consolación, fecha/hora inicio, estado) y orquestar **8 estados** `Draft → Configured → Scheduled → Ready → Running ↔ Paused → Finished` + `Cancelled` con validación de 3 niveles (API/Aplicación/Dominio), inmutabilidad tras `Ready`/`Running`, concurrencia optimista `rowversion` y auditoría append-only. Reutiliza 100% el shell Blazor Auto net10.0 + BFF YARP→`oroclash-api` + OIDC OroIdentityServer + tema `administration` de SPEC-016. Extiende `Game`/`GameConfiguration` de dominio de `001-game-configuration` sin duplicar agregados; consumo exclusivamente vía BFF, sin acceso directo a SQL Server/Oracle/`identitydb`.

## Technical Context

**Language/Version**: C# latest / **.NET 10 (`net10.0`) único target** (global.json 10.0.400, Directory.Build.props net10.0). Sin net11 multi-target.

**Primary Dependencies**:
- ASP.NET Core Blazor Web App Auto (proyectos existentes `QuizArena.Admin` + `QuizArena.Admin.Client`)
- `Yarp.ReverseProxy` (`AddHttpForwarderWithServiceDiscovery()` + `MapBffForwarder()` / `MapGameHubForwarder()` ya existentes en 017)
- `Microsoft.AspNetCore.Authentication.OpenIdConnect` + Cookie + `ConfigureCookieOidc` (refresh no-interactivo)
- `BuildingBlocks.ServiceDefaults` (OTel, health, resilience)
- Design System SPEC-016 — `design-system/tokens/design-tokens.css` + tema `administration` (tokens, componentes §9)

**Storage**: Ninguno en Admin. Configuración persiste en `oroclash-api` (SQL Server primario, abstracción Oracle). Admin MUST NOT tocar DB; todo via `POST/PUT /bff/games*` → `oroclash-api /api/games*` con `rowversion` y `IdempotencyKey` (FR-016, SC-005).

**Testing**: xUnit (`TestingPlatformDotnetTestSupport`); Architecture Tests (`DesignSystemNoDirectDbTests`, `AdminBffTests`); bUnit opcional para `GameConfigurationForm`/`StateTransitions`; `WebApplicationFactory` + OIDC mock para autorización `AdminOrGameManager` vs `REWARD_MANAGER` 403; pruebas de concurrencia `rowversion` en `tests/QuizArena.Admin.Tests`.

**Target Platform**: Web — Blazor Web App Auto (server+WASM) orquestado por `OroQuizClash.AppHost` 13.5.x (service discovery `oroclash-api`, `identity-api`).

**Project Type**: web-application — extensión de feature en 2 proyectos existentes (no nuevos proyectos).

**Performance Goals**: SC-001 creación en <3m (90%); SC-004 rechazo categoría inválida <2s percibidos; SC-009 listado paginado con skeleton <2s; overhead BFF <100ms; transiciones <1s + operatividad sin recarga completa.

**Constraints**: BFF obligatorio (tokens nunca en navegador); WCAG 2.2 AA tema claro; responsive 375–1536 sin scroll horizontal; objetivos táctiles ≥44px; net10.0 único; OIDC solo OroIdentityServer; `must_change_password` gating antes de configurar (Constitución VI/J); configuración inmutable tras `Running` (Constitución C); optimismo `rowversion`.

**Scale/Scope**: Operadores internos `ADMIN`/`GAME_MANAGER` (crean/configuran), `REWARD_MANAGER` denegado; decenas de sesiones concurrentes; 1 formulario de 16 campos + listado paginado/filtrado + detalle con historial; ~4 nuevas interfaces/DTOs; catálogos cerrados (5 estrategias dificultad, 4 withdrawal, 4 loss, 2 scoring).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Evidencia |
|-----------|--------|-----------|
| I. Domain First | ✅ PASS | Validaciones e inmutabilidad impuestas en dominio `Game`/`GameConfiguration` (001); UI solo proyecta y valida contrato, sin reglas de juego |
| II. Clean Architecture | ✅ PASS | Admin = presentación (Constitución J); `Web → BFF → Api → Application → Domain`; sin acceso directo a DB |
| III. BuildingBlocks | ✅ PASS | Reusa `BuildingBlocks.ServiceDefaults` + `BuildingBlocks.Kernel` primitives via API; no MediatR/MassTransit/AutoMapper añadido |
| IV. CQRS | ✅ PASS | Consume `CreateGame`, `UpdateGame`, `ScheduleGame`, `ReadyGame`, `StartGame`, `PauseGame`, `FinishGame`, `CancelGame` slices existentes (BuildingBlocks.CQRS) |
| V. Server Truth | ✅ PASS | Configuración y transiciones autoridad del backend (server timestamps, `rowversion`); UI nunca inventa estado/tiempo |
| VI. OroIdentityServer | ✅ PASS | OIDC `authorization_code` + `refresh_token` vs discovery; `must_change_password` gating; sin login propio (FR-018) |
| A. Game Lifecycle | ✅ PASS | 8 estados admin mapeados a estados dominio (Draft→DRAFT, Configured→READY, Scheduled→WAITING, Running/Paused→IN_PROGRESS etc.); transiciones inválidas rechazadas |
| C. Configurable Rules | ✅ PASS | Catálogos cerrados Constitución C (rounds ≥5, withdrawal/loss/consolation, 5 niveles dificultad, 4 estrategias) validados en dominio |
| F. Concurrency | ✅ PASS | `rowversion` optimista protege edición simultánea en `Draft` (SC-008) + idempotencia |
| H. Security | ✅ PASS | BFF YARP adjunta Bearer server-side; `AdminOrGameManager` claim-based; REWARD_MANAGER 403 sin fuga (SC-006) |
| I. Observability | ✅ PASS | `AddServiceDefaults()` (OTel, /health); CorrelationId propagado; ProblemDetails RFC7807 sin fuga (FR-015) |
| J. API & Frontend | ✅ PASS | Presentation-only; DTOs boundary; paginación; auth OIDC code+refresh |
| Addendum 2 (UI/UX) | ✅ PASS | Consume SPEC-016 tema administration, tokens sin literales, estados Loading/Ready/Empty/Error por campo (FR-012) |
| net10.0 único | ✅ PASS | Extiende proyectos net10.0 existentes; no nuevo TFM |

**Resultado pre-Phase 0: PASS — sin violaciones.**

## Project Structure

### Documentation (this feature)

```text
specs/019-admin-game-configuration/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── game-configuration-bff.md
│   └── state-transitions.md
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
│   ├── Components/GameConfiguration/   # NUEVO — wizard/form + state controls
│   │   ├── GameConfigurationForm.razor # formulario 16 campos con validación por campo
│   │   ├── GameStateBadge.razor        # badge estado 8 colores + tooltip
│   │   ├── GameTransitionsBar.razor    # botones Schedule/Ready/Start/Pause/Resume/Finish/Cancel
│   │   └── ScheduledAtPicker.razor     # selector UTC futuro ≥5m
│   └── Services/
│       └── (reusa BffForwarderExtensions, CookieOidcRefresher, Server*Services)
└── QuizArena.Admin.Client/             # WASM (net10.0) — existente
    ├── Models/GameConfiguration/       # NUEVO — DTOs GameConfiguration, GameStateView, Policy enums
    │   ├── GameConfigurationForm.cs    # validación 16 campos (3 niveles espejo dominio)
    │   ├── GameStateView.cs            # 8 estados admin + mapping a dominio
    │   └── PolicyCatalogs.cs           # Withdrawal/Loss/Scoring/Difficulty catalogs
    ├── Services/
    │   ├── IGameConfigurationService.cs # NUEVO — contrato compartido Create/Update/Get + transitions
    │   ├── ClientGameConfigurationService.cs # WASM → /bff/games* (cookie)
    │   └── GameCatalogs.cs             # catálogos estáticos para selects
    └── Pages/GameConfiguration/        # NUEVO — páginas Create, Edit, Detail, List
        ├── GameCreate.razor
        ├── GameEdit.razor
        ├── GameDetail.razor
        └── GamesList.razor

OroQuizClash.Domain/Games/              # ya existe (001) — Game aggregate + GameConfiguration value object
OroQuizClash.Application/Features/Games/# ya existen slices CreateGame, UpdateGame, etc.

tests/QuizArena.Admin.Tests/
├── GameConfigurationTests.cs           # NUEVO — 16 campos validación + inmutabilidad + rowversion
└── GameStateTransitionTests.cs         # NUEVO — 8 estados, guardas, autorización

tests/OroQuizClash.Architecture.Tests/
└── (reusa DesignSystemNoDirectDbTests, AdminBffTests)
```

**Structure Decision**: Extensión in-place de los dos proyectos Auto creados en 017/018. Sin nuevo proyecto/host. `IGameConfigurationService` vive en `QuizArena.Admin.Client` (referenciado por server) con doble implementación `Client*` → `/bff/games*` (cookie) y `Server*` → `http://oroclash-api/api/games*` (Bearer del `HttpContext`). Reusa agregados de dominio de `001-game-configuration` (sin duplicar lógica). BFF forwarder catch-all ya cubre `/bff/games*`.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) | — | — |

No se introduce nueva complejidad respecto a 017/018. YARP, OIDC, `rowversion` y catálogos ya justificados en 001 y 017; este feature los reutiliza sin nuevas dependencias.

## Constitution Check (Post-Design Re-Check)

*Re-evaluado tras Phase 1 (research.md + data-model.md + contracts/ + quickstart.md):*

| Gate | Estado |
|------|--------|
| I–VI, A, C, F, H, I, J | ✅ PASS — validación 3 niveles, inmutabilidad, `rowversion` y catálogos cerrados refuerzan I/A/C/F; BFF + OIDC refuerzan H/VI |
| Addendum 2 UI | ✅ PASS — contracts exigen tokens SPEC-016, estados por campo, WCAG AA, responsive 375–1536, 44px |
| IV CQRS | ✅ PASS — reuse slices `CreateGame` etc. sin duplicar |
| Complejidad | ✅ PASS — 0 violaciones nuevas |

**Resultado final: PASS — proceder a `/speckit.tasks` (Phase 2).**
