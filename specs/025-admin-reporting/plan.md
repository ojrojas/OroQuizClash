# Implementation Plan: Admin Reporting

**Branch**: `025-admin-reporting` | **Date**: 2026-05-13 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/025-admin-reporting/spec.md`

## Summary

Superficie administrativa de **solo lectura para reporting analítico** — 12 métricas: Games (9 estados), Players (únicos/activos), Questions/Categories, Answers (totales, Correct 5k, Incorrect 5k, tasa), Scores (promedio/distribución `PointTransaction` ledger), Withdrawals (conteo/tasa/política), Rewards (6 tipos, 3 estados), Redemptions (5 estados, coste), Consolation Rewards (`IsConsolation:true` separado) — con 6 filtros combinados AND (Fecha `Desde<=Hasta`, Categoría, Juego, Jugador, Nivel 1–5, Resultado catálogo cerrado) paginados server-side (`page`/`pageSize` 20, `TotalCount`) y autorización por rol (`ADMIN` todo, `GAME_MANAGER` operativo/rendimiento, `REWARD_MANAGER` recompensas). Reutiliza 100% shell Blazor Auto net10.0 + BFF YARP→`oroclash-api` + OIDC OroIdentityServer + tema `administration` de SPEC-016. Reusa agregados `Game`/`Question`/`Category`/`GamePlayer`/`PointTransaction`/`Reward`/`RewardRedemption` y queries existentes sin duplicar dominio; consumo exclusivo vía BFF, sin acceso directo a SQL/Oracle/`identitydb`.

## Technical Context

**Language/Version**: C# latest / **.NET 10 (`net10.0`) único target** (global.json 10.0.400, Directory.Build.props net10.0). Sin net11 multi-target.

**Primary Dependencies**:
- ASP.NET Core Blazor Web App Auto (proyectos existentes `QuizArena.Admin` + `QuizArena.Admin.Client`)
- `Yarp.ReverseProxy` (`AddHttpForwarderWithServiceDiscovery()` + `MapBffForwarder()` / `MapGameHubForwarder()` ya existentes en 017)
- `Microsoft.AspNetCore.Authentication.OpenIdConnect` + Cookie + `ConfigureCookieOidc` (refresh no-interactivo)
- `BuildingBlocks.ServiceDefaults` (OTel, health, resilience)
- Design System SPEC-016 — `design-system/tokens/design-tokens.css` + tema `administration` (tokens, componentes §9)

**Storage**: Ninguno en Admin. Reportes agregan datos persistidos en `oroclash-api` (SQL Server primario, abstracción Oracle, `PointTransaction` ledger, `Game`/`GamePlayer`/`Question`/`Category`/`Reward`, `UserSession` en `identitydb` vía OroIdentityServer). Admin MUST NOT tocar DB; todo via `GET /bff/reports*` → `oroclash-api /api/reports*` y `/bff/players*`/`/bff/games*` existentes con filtros/paginación y `CorrelationId` (FR-014, SC-008).

**Testing**: xUnit (`TestingPlatformDotnetTestSupport`); Architecture Tests (`DesignSystemNoDirectDbTests`, `AdminBffTests`); bUnit opcional para `ReportsDashboard`/`MetricCard`; `WebApplicationFactory` + OIDC mock para `ADMIN` vs `GAME_MANAGER` vs `REWARD_MANAGER` 403; pruebas de agregación server-side + `ProblemDetails` + `CorrelationId` + `IsConsolation` en `tests/QuizArena.Admin.Tests`.

**Target Platform**: Web — Blazor Web App Auto (server+WASM) orquestado por `OroQuizClash.AppHost` 13.5.x (service discovery `oroclash-api`, `identity-api`).

**Project Type**: web-application — extensión de feature en 2 proyectos existentes (no nuevos proyectos).

**Performance Goals**: SC-001 operativo (4 métricas) <2s con skeleton (90%); SC-002 filtros combinados paginados <2s (100%) sin cargar colecciones; SC-003 rendimiento (Answers/Correct/Scores) <2s con tasa correcta; SC-004 recompensas distingue `IsConsolation` <2s; SC-006 flujo completo 5 pasos <2min (95%); overhead BFF <100ms; agregación ≥10k juegos sin degradación.

**Constraints**: BFF obligatorio (tokens nunca en navegador); WCAG 2.2 AA tema claro; responsive 375–1536 sin scroll horizontal; objetivos táctiles ≥44px; net10.0 único; OIDC solo OroIdentityServer; `must_change_password` gating antes de consultar (Constitución VI/J); solo lectura en v1 (sin exportación CSV/PDF); `Consolation` independiente (Constitución C); Nivel 1–5 validado por campo.

**Scale/Scope**: Operadores internos `ADMIN` (todo), `GAME_MANAGER` (Games/Players/Questions/Categories/Answers/Scores/Withdrawals), `REWARD_MANAGER` (Rewards/Redemptions/Consolation); decenas de sesiones concurrentes; 1 dashboard con 3 pestañas (Operativo, Rendimiento, Recompensas) + 6 filtros combinados + 12 métricas agregadas; ~5 nuevos DTOs de lectura + 1 `ReportFilter`; paginación `page`/`pageSize` 20 por defecto.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Evidencia |
|-----------|--------|-----------|
| I. Domain First | ✅ PASS | Reportes proyectan `Game`, `Question`, `Category`, `GamePlayer`, `PointTransaction`, `Reward`/`RewardRedemption` sin mutar; invariantes y ledger permanecen en dominio (009, C/D); Admin solo agrega lectura |
| II. Clean Architecture | ✅ PASS | Admin = presentación (J); `Web → BFF → Api → Application → Domain`; sin acceso directo a DB; DTOs en boundary |
| III. BuildingBlocks | ✅ PASS | Reusa `BuildingBlocks.ServiceDefaults` + `BuildingBlocks.Kernel` primitives via API; no MediatR/MassTransit/AutoMapper añadido |
| IV. CQRS | ✅ PASS | Consume queries existentes `GetGames`, `GetPlayers`, `GetQuestions`, `GetScores`, `GetRewards`, `GetReports` (BuildingBlocks.CQRS) — solo lectura |
| V. Server Truth | ✅ PASS | Todas las métricas (Games/Answers/Scores/Withdrawals/Rewards) agregadas server-side; tasas y promedios calculados en backend; UI nunca recalcula |
| VI. OroIdentityServer | ✅ PASS | OIDC `authorization_code` + `refresh_token` vs discovery; `must_change_password` gating; `sub`/`tenant_id` para Players (FR-009) |
| C. Configurable Rules | ✅ PASS | Consulta sin hardcodear; Nivel 1–5, `Consolation` independiente visible con `IsConsolation` (Constitución C) |
| D. Scoring via Ledger | ✅ PASS | `PointTransaction` ledger expone `ANSWER_CORRECT`/`PENALTY`/`CONSOLATION`/`REWARD_REDEMPTION`; Scores = suma server-side, no mutación |
| F. Concurrency | ✅ PASS | Lectura sin escritura — no requiere `rowversion`/`IdempotencyKey`; lecturas idempotentes y propagan `CorrelationId` |
| H. Security | ✅ PASS | BFF YARP adjunta Bearer server-side; políticas `AdminOnly`/`AdminOrGameManager`/`RewardManagerOrAdmin` claim-based; 403 sin fuga (SC-007) |
| I. Observability | ✅ PASS | `AddServiceDefaults()` (OTel, /health); CorrelationId propagado; ProblemDetails RFC7807 sin fuga (FR-014) |
| J. API & Frontend | ✅ PASS | Presentation-only; DTOs boundary; paginación server-side; auth OIDC code+refresh |
| Addendum 2 (UI/UX) | ✅ PASS | Consume SPEC-016 tema administration, tokens sin literales, estados Loading/Ready/Empty/Error por pestaña, skeleton, WCAG AA, responsive 375–1536, 44px |
| net10.0 único | ✅ PASS | Extiende proyectos net10.0 existentes; no nuevo TFM |

**Resultado pre-Phase 0: PASS — sin violaciones.**

## Project Structure

### Documentation (this feature)

```text
specs/025-admin-reporting/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── reports-bff.md
│   └── report-filters-bff.md
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
│   ├── Components/Reports/             # NUEVO — dashboard + tarjetas métricas
│   │   ├── ReportsDashboard.razor      # 3 pestañas (Operativo, Rendimiento, Recompensas)
│   │   ├── MetricCard.razor            # tarjeta métrica + desglose
│   │   └── ReportFiltersBar.razor      # 6 filtros combinados + validación From<=To
│   └── Services/
│       └── (reusa BffForwarderExtensions, CookieOidcRefresher, Server*Services)
└── QuizArena.Admin.Client/             # WASM (net10.0) — existente
    ├── Models/Reports/                 # NUEVO — DTOs ReportSnapshot, ReportFilter, Metrics
    │   ├── ReportSnapshot.cs           # 12 métricas + CalculatedAt
    │   ├── ReportFilter.cs             # 6 filtros + validación
    │   └── ReportMetrics.cs            # Game/Player/Question/Answer/Score etc.
    ├── Services/
    │   ├── IReportsService.cs          # existente — extender con GetReports/GetMetrics
    │   ├── ClientReportsService.cs     # WASM → /bff/reports* (cookie)
    │   └── ReportCatalogs.cs           # catálogos Resultado/Nivel + GameStatuses
    └── Pages/Reports/                  # NUEVO — página reportes con filtros
        └── Reports.razor               # /admin/reports

OroQuizClash.Domain/Reports/            # ya existe (015) — agregaciones via Specification (no nuevo agregado)
OroQuizClash.Application/Features/Reports/ # ya existen queries GetGamesReport, GetPlayerReport, GetScoreReport

tests/QuizArena.Admin.Tests/
├── ReportsOperationalTests.cs          # NUEVO — Games/Players/Questions/Categories + filtros
└── ReportsRewardsTests.cs              # NUEVO — Rewards/Redemptions/Consolation, 403, Nivel 1–5

tests/OroQuizClash.Architecture.Tests/
└── (reusa DesignSystemNoDirectDbTests, AdminBffTests)
```

**Structure Decision**: Extensión in-place de los dos proyectos Auto creados en 017/018. Sin nuevo proyecto/host. `IReportsService` ya existe en `QuizArena.Admin.Client` (referenciado por server) con doble implementación `Client*` → `/bff/reports*` (cookie) y `Server*` → `http://oroclash-api/api/reports*` (Bearer del `HttpContext`). Reusa queries `GetGames`, `GetScores`, `GetRewards` sin duplicar lógica; BFF forwarder catch-all ya cubre `/bff/reports*`. Solo lectura en v1 — no se añade mutación ni exportación.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) | — | — |

No se introduce nueva complejidad respecto a 017–024. YARP, OIDC, `PointTransaction` ledger, `Specification` paginada y 12 métricas ya justificados en 015 y 017–024; este feature los reutiliza en modo lectura sin nuevas dependencias.

## Constitution Check (Post-Design Re-Check)

*Re-evaluado tras Phase 1 (research.md + data-model.md + contracts/ + quickstart.md):*

| Gate | Estado |
|------|--------|
| I–VI, C, D, F, H, I, J | ✅ PASS — reporting 12 métricas vía ledger y Game/Question refuerza V/D; BFF + OIDC refuerzan H/VI; solo lectura evita F |
| Addendum 2 UI | ✅ PASS — contracts exigen tokens SPEC-016, estados por pestaña, WCAG AA, responsive 375–1536, 44px |
| IV CQRS | ✅ PASS — reuse queries GetReports etc. sin duplicar |
| Complejidad | ✅ PASS — 0 violaciones nuevas |

**Resultado final: PASS — proceder a `/speckit.tasks` (Phase 2).**
