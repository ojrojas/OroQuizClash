# Implementation Plan: Admin Players

**Branch**: `024-admin-players` | **Date**: 2026-05-13 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/024-admin-players/spec.md`

## Summary

Superficie administrativa de **solo lectura para consultar participantes** — 9 áreas: perfil (`sub` de OroIdentityServer, nombre, email, tenant, identificación), estado derivado (`GamePlayer` + `UserSession`), historial de partidas, participaciones, resultados (posición, `PointTransaction` ledger), puntuaciones (total + desglose por tipo), premios del catálogo, canjes `Requested→Delivered` con `IsConsolation`, y estadísticas agregadas server-side — con búsqueda/paginación server-side, filtros combinados (texto, estado, rango de fechas, tipo) y autorización por rol (`ADMIN` todo, `GAME_MANAGER` perfil/historial/estadísticas, `REWARD_MANAGER` premios/canjes). Reutiliza 100% shell Blazor Auto net10.0 + BFF YARP→`oroclash-api` + OIDC OroIdentityServer + tema `administration` de SPEC-016. Reusa agregados `Game`/`GamePlayer`/`PointTransaction`/`Reward`/`RewardRedemption` y slices de lectura existentes (`GetPlayer`, `GetPlayerScore`, `GetRewards` etc.) sin duplicar dominio; consumo exclusivo vía BFF, sin acceso directo a SQL/Oracle/`identitydb`.

## Technical Context

**Language/Version**: C# latest / **.NET 10 (`net10.0`) único target** (global.json 10.0.400, Directory.Build.props net10.0). Sin net11 multi-target.

**Primary Dependencies**:
- ASP.NET Core Blazor Web App Auto (proyectos existentes `QuizArena.Admin` + `QuizArena.Admin.Client`)
- `Yarp.ReverseProxy` (`AddHttpForwarderWithServiceDiscovery()` + `MapBffForwarder()` / `MapGameHubForwarder()` ya existentes en 017)
- `Microsoft.AspNetCore.Authentication.OpenIdConnect` + Cookie + `ConfigureCookieOidc` (refresh no-interactivo)
- `BuildingBlocks.ServiceDefaults` (OTel, health, resilience)
- Design System SPEC-016 — `design-system/tokens/design-tokens.css` + tema `administration` (tokens, componentes §9)

**Storage**: Ninguno en Admin. Jugadores, participaciones, puntuaciones, premios y canjes persisten en `oroclash-api` (SQL Server primario, abstracción Oracle, `PointTransaction` ledger, `GamePlayer`, `UserSession` en `identitydb` vía OroIdentityServer). Admin MUST NOT tocar DB; todo via `GET /bff/players*`, `/bff/players/{id}/games`, `/bff/players/{id}/scores`, `/bff/players/{id}/rewards`, `/bff/players/{id}/redemptions`, `/bff/players/{id}/statistics` → `oroclash-api /api/players*` con paginación, filtros y `CorrelationId` (FR-014, SC-008).

**Testing**: xUnit (`TestingPlatformDotnetTestSupport`); Architecture Tests (`DesignSystemNoDirectDbTests`, `AdminBffTests`); bUnit opcional para `PlayerDetail`/`PlayerTable`; `WebApplicationFactory` + OIDC mock para `ADMIN` vs `GAME_MANAGER` vs `REWARD_MANAGER` 403; pruebas de paginación server-side + `ProblemDetails` + `CorrelationId` en `tests/QuizArena.Admin.Tests`.

**Target Platform**: Web — Blazor Web App Auto (server+WASM) orquestado por `OroQuizClash.AppHost` 13.5.x (service discovery `oroclash-api`, `identity-api`).

**Project Type**: web-application — extensión de feature en 2 proyectos existentes (no nuevos proyectos).

**Performance Goals**: SC-001 perfil completo <30s (90%); SC-002 búsqueda jugadores <2s con skeleton (100%); SC-003 historial/participaciones (≥200) paginadas <2s; SC-004 puntuaciones ledger <2s; SC-005 flujo completo 5 pasos <2min (95%); overhead BFF <100ms.

**Constraints**: BFF obligatorio (tokens nunca en navegador); WCAG 2.2 AA tema claro; responsive 375–1536 sin scroll horizontal; objetivos táctiles ≥44px; net10.0 único; OIDC solo OroIdentityServer; `must_change_password` gating antes de consultar (Constitución VI/J); solo lectura en v1 (sin mutación de perfil/puntos); `Consolation` independiente (Constitución C).

**Scale/Scope**: Operadores internos `ADMIN` (todo), `GAME_MANAGER` (perfil/historial/estadísticas), `REWARD_MANAGER` (premios/canjes + perfil básico); decenas de sesiones concurrentes; 1 listado paginado (búsqueda) + 1 detalle con 6 pestañas (perfil, historial, participaciones, resultados, puntuaciones, premios/canjes, estadísticas) + filtros combinados; ~6 nuevos DTOs de lectura; paginación `page`/`pageSize` 20 por defecto.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Evidencia |
|-----------|--------|-----------|
| I. Domain First | ✅ PASS | Consultas proyectan `GamePlayer`, `Game`, `PointTransaction`, `Reward`/`RewardRedemption` sin mutar; invariantes y ledger permanecen en dominio (009, C/D); Admin solo lectura |
| II. Clean Architecture | ✅ PASS | Admin = presentación (J); `Web → BFF → Api → Application → Domain`; sin acceso directo a DB; DTOs en boundary |
| III. BuildingBlocks | ✅ PASS | Reusa `BuildingBlocks.ServiceDefaults` + `BuildingBlocks.Kernel` primitives via API; no MediatR/MassTransit/AutoMapper añadido |
| IV. CQRS | ✅ PASS | Consume queries existentes `GetPlayers`, `GetPlayer`, `GetPlayerGames`, `GetPlayerScore`, `GetPlayerRewards`, `GetPlayerStatistics` (BuildingBlocks.CQRS) |
| V. Server Truth | ✅ PASS | Puntuaciones reconstruidas desde `PointTransaction` ledger server-side; estadísticas y elegibilidad calculadas en backend; UI nunca recalcula |
| VI. OroIdentityServer | ✅ PASS | OIDC `authorization_code` + `refresh_token` vs discovery; `must_change_password` gating; perfil base desde `sub`/`userinfo` (FR-001) |
| C. Configurable Rules | ✅ PASS | Consulta sin hardcodear reglas; `Consolation` independiente visible con `IsConsolation` (Constitución C) |
| D. Scoring via Ledger | ✅ PASS | `PointTransaction` ledger expone desglose por tipo; balance = suma server-side, no mutación directa |
| F. Concurrency | ✅ PASS | Lectura sin escritura — no requiere `rowversion`/`IdempotencyKey` en este feature; lecturas idempotentes y propagan `CorrelationId` |
| H. Security | ✅ PASS | BFF YARP adjunta Bearer server-side; políticas `AdminOnly`/`AdminOrGameManager`/`RewardManagerOrAdmin` claim-based; 403 sin fuga (SC-007) |
| I. Observability | ✅ PASS | `AddServiceDefaults()` (OTel, /health); CorrelationId propagado; ProblemDetails RFC7807 sin fuga (FR-013) |
| J. API & Frontend | ✅ PASS | Presentation-only; DTOs boundary; paginación server-side; auth OIDC code+refresh |
| Addendum 2 (UI/UX) | ✅ PASS | Consume SPEC-016 tema administration, tokens sin literales, estados Loading/Ready/Empty/Error por pestaña, skeleton, WCAG AA, responsive 375–1536, 44px |
| net10.0 único | ✅ PASS | Extiende proyectos net10.0 existentes; no nuevo TFM |

**Resultado pre-Phase 0: PASS — sin violaciones.**

## Project Structure

### Documentation (this feature)

```text
specs/024-admin-players/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── players-bff.md
│   └── player-detail-bff.md
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
│   ├── Components/Players/             # NUEVO — detalle + tablas por pestaña
│   │   ├── PlayerProfileCard.razor     # perfil + estado (sub, nombre, email, tenant)
│   │   ├── PlayerHistoryTable.razor    # historial paginado
│   │   ├── PlayerScoreLedger.razor     # desglose PointTransaction
│   │   └── PlayerStatisticsPanel.razor # métricas agregadas
│   └── Services/
│       └── (reusa BffForwarderExtensions, CookieOidcRefresher, Server*Services)
└── QuizArena.Admin.Client/             # WASM (net10.0) — existente
    ├── Models/Players/                 # NUEVO — DTOs Player, PlayerProfile, History, Score
    │   ├── Player.cs                   # listado + filtros
    │   ├── PlayerDetail.cs             # perfil + estado + contadores
    │   └── PlayerStatistics.cs         # métricas
    ├── Services/
    │   ├── IPlayersService.cs          # existente — extender con GetPlayers/GetPlayer/GetHistory/GetScores/GetStatistics
    │   ├── ClientPlayersService.cs     # WASM → /bff/players* (cookie)
    │   └── PlayerCatalogs.cs           # catálogos estado/tipo transacción
    └── Pages/Players/                  # NUEVO — listado + detalle con pestañas
        ├── PlayersList.razor           # /admin/players
        └── PlayerDetail.razor          # /admin/players/{id}

OroQuizClash.Domain/Players/            # ya existe — GamePlayer, PointTransaction, RewardRedemption
OroQuizClash.Application/Features/Players/ # ya existen queries GetPlayers, GetPlayerScore, GetPlayerStatistics

tests/QuizArena.Admin.Tests/
├── PlayerProfileTests.cs               # NUEVO — perfil/estado solo lectura, paginación
└── PlayerStatisticsTests.cs            # NUEVO — ledger desglose, Consolation, filtros, 403

tests/OroQuizClash.Architecture.Tests/
└── (reusa DesignSystemNoDirectDbTests, AdminBffTests)
```

**Structure Decision**: Extensión in-place de los dos proyectos Auto creados en 017/018. Sin nuevo proyecto/host. `IPlayersService` ya existe en `QuizArena.Admin.Client` (referenciado por server) con doble implementación `Client*` → `/bff/players*` (cookie) y `Server*` → `http://oroclash-api/api/players*` (Bearer del `HttpContext`). Reusa agregados `GamePlayer`/`PointTransaction`/`Reward` sin duplicar lógica; BFF forwarder catch-all ya cubre `/bff/players*`. Solo lectura en v1 — no se añade mutación.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) | — | — |

No se introduce nueva complejidad respecto a 017–023. YARP, OIDC, `PointTransaction` ledger, paginación y 6 pestañas ya justificados en 009 y 017–023; este feature los reutiliza en modo lectura sin nuevas dependencias.

## Constitution Check (Post-Design Re-Check)

*Re-evaluado tras Phase 1 (research.md + data-model.md + contracts/ + quickstart.md):*

| Gate | Estado |
|------|--------|
| I–VI, C, D, F, H, I, J | ✅ PASS — consulta 9 áreas vía ledger y GamePlayer refuerza V/D; BFF + OIDC refuerzan H/VI; solo lectura evita F |
| Addendum 2 UI | ✅ PASS — contracts exigen tokens SPEC-016, estados por pestaña, WCAG AA, responsive 375–1536, 44px |
| IV CQRS | ✅ PASS — reuse queries GetPlayers etc. sin duplicar |
| Complejidad | ✅ PASS — 0 violaciones nuevas |

**Resultado final: PASS — proceder a `/speckit.tasks` (Phase 2).**
