# Implementation Plan: Admin Dashboard

**Branch**: `018-admin-dashboard` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/018-admin-dashboard/spec.md`

## Summary

Extensión del Dashboard de `QuizArena.Admin` (SPEC-017) para entregar la vista operacional global: 10 bloques de métricas (Juegos activos/programados/finalizados, Jugadores conectados/activos, Preguntas/Categorías, Premios/Canjes, Estadísticas generales) y 7 accesos rápidos (Crear juego, Configurar juego, Gestionar preguntas, Ver juegos activos, Ver jugadores, Gestionar premios, Consultar reportes) con drill-down y actualización sin recarga. Reutiliza 100% el shell Blazor Auto net10.0 + BFF YARP→`oroclash-api` + OIDC OroIdentityServer + tema `administration` de SPEC-016. Sin nueva app ni DB directa; todos los conteos se derivan de endpoints/APIs existentes vía BFF con estados aislados (loading/empty/error/retry) y polling manual + auto-refresh 30-60s.

## Technical Context

**Language/Version**: C# latest / **.NET 10 (`net10.0`) único target** (global.json 10.0.400, Directory.Build.props net10.0). Sin net11 multi-target.

**Primary Dependencies**:
- ASP.NET Core Blazor Web App Auto (proyectos existentes `QuizArena.Admin` + `QuizArena.Admin.Client`)
- `Yarp.ReverseProxy` (`AddHttpForwarderWithServiceDiscovery()` + `MapBffForwarder()` / `MapGameHubForwarder()` ya existentes en 017)
- `Microsoft.AspNetCore.Authentication.OpenIdConnect` + Cookie + `ConfigureCookieOidc` (refresh no-interactivo)
- `Microsoft.AspNetCore.SignalR.Client` (ya cableado; no nuevo hub para dashboard)
- `BuildingBlocks.ServiceDefaults` (OTel, health, resilience)
- Design System SPEC-016 — `design-system/tokens/design-tokens.css` + tema `administration` (tokens, componentes §9)

**Storage**: Ninguno. Dashboard MUST NOT tocar DB; todo via BFF `GET /bff/*` → `oroclash-api /api/*` (FR-015, SC-005). Agregados server-side.

**Testing**: xUnit + Architecture Tests (`DesignSystemNoDirectDbTests`, `AdminNoDirectDbTests` existentes); bUnit opcional para `MetricTile`/`QuickActionGrid` si aporta valor; `WebApplicationFactory` + OIDC mock para autorización por rol.

**Target Platform**: Web — Blazor Web App Auto (server+WASM) orquestado por `OroQuizClash.AppHost` 13.5.x (service discovery `oroclash-api`, `identity-api`).

**Project Type**: web-application — extensión de feature en 2 proyectos existentes (no nuevos proyectos).

**Performance Goals**: SC-001 10 indicadores <5s percibidos (carga inicial <2s, skeleton por bloque); SC-004 cambios backend visibles ≤30s manual / ≤60s auto; overhead BFF <100ms; sin cargar colecciones completas (paginación/precálculo server-side).

**Constraints**: BFF obligatorio (tokens nunca en navegador); WCAG 2.2 AA tema claro; responsive 375–1536 sin scroll horizontal; objetivos táctiles ≥44px; net10.0 único; OIDC solo OroIdentityServer; `must_change_password` gating antes de datos (Constitución VI/J).

**Scale/Scope**: Operadores internos (ADMIN ve 10 métricas+7 atajos, GAME_MANAGER filtra recompensas, REWARD_MANAGER solo Premios/Canjes/Estadísticas+2 atajos); decenas de sesiones concurrentes; 1 página Dashboard extendida + ~10 destinos existentes; ~3 nuevas interfaces/DTOs; polling ligero (1 req/30-60s visible tab).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Evidencia |
|-----------|--------|-----------|
| I. Domain First | ✅ PASS | Dashboard sin reglas de juego; solo proyecciones de conteos validados server-side; sin lógica de dominio en UI |
| II. Clean Architecture | ✅ PASS | Dashboard = capa presentación (Constitución J); `Web → BFF → Api → Application → Domain`; sin acceso directo a DB |
| III. BuildingBlocks | ✅ PASS | Reusa `BuildingBlocks.ServiceDefaults` + `BuildingBlocks.Kernel` primitives via API; no MediatR/MassTransit/AutoMapper añadido |
| IV. CQRS | N/A | Sin cambios de backend; consume queries existentes (GET list/reports) |
| V. Server Truth | ✅ PASS | Conteos autoridad del backend; UI re-consulta REST tras evento/polling; nunca inventa valores (FR-003 tooltip) |
| VI. OroIdentityServer | ✅ PASS | Sesión validada contra discovery; OIDC code+refresh; manejo `must_change_password`; sin login propio (FR-017) |
| H. Security | ✅ PASS | BFF YARP adjunta Bearer server-side; navegador nunca ve access_token; correlación y 401 detiene polling (edge case) |
| I. Observability | ✅ PASS | `AddServiceDefaults()` (OTel, /health); CorrelationId propagado; errores mapeados sin fuga (FR-018) |
| J. API & Frontend | ✅ PASS | Presentation-only; DTOs boundary; autorización por claims `roles` con políticas espejo; drill-down deniega sin fuga |
| Addendum 2 (UI/UX) | ✅ PASS | Consume SPEC-016 tema administration, tokens sin literales, estados Loading/Ready/Empty/Error por bloque (FR-007) |
| net10.0 único | ✅ PASS | Extiende proyectos net10.0 existentes; no nuevo TFM |

**Resultado pre-Phase 0: PASS — sin violaciones.**

## Project Structure

### Documentation (this feature)

```text
specs/018-admin-dashboard/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── dashboard-bff.md
│   └── navigation-map.md
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
│   │       └── Dashboard.razor         # EXTENDER: grid 10 MetricTile + QuickActionGrid + Refresh bar
│   ├── Components/Dashboard/           # NUEVO
│   │   ├── MetricTile.razor            # tarjeta métrica clicable con estados + aria-live + skeleton
│   │   ├── MetricsGrid.razor           # grilla responsive 10 bloques (1/2/3/4 cols)
│   │   ├── QuickActionCard.razor       # atajo con icono Lucide + descripción + disabled reason
│   │   ├── QuickActionGrid.razor       # 7 atajos, orden foco tras métricas
│   │   └── DashboardRefreshBar.razor   # botón Actualizar + timestamp + auto-refresh toggle
│   └── Services/
│       └── (reusa BffForwarderExtensions, CookieOidcRefresher, Server*Services de 017)
└── QuizArena.Admin.Client/             # WASM (net10.0) — existente
    ├── Models/Dashboard/               # NUEVO — DTOs snapshot (DashboardSnapshot, MetricValue, GeneralStatistics)
    ├── Services/
    │   ├── IDashboardService.cs        # NUEVO — contrato compartido (ya existe ServerDashboardService, se extiende)
    │   ├── DashboardService.cs         # NUEVO — ClientDashboardService: HttpClient → /bff/dashboard/snapshot
    │   └── QuickActionsCatalog.cs      # NUEVO — catálogo estático 7 atajos (id, etiqueta, icono, ruta, roles)
    └── Pages/Dashboard/                # lógica cliente si aplica (view-models)

OroQuizClash.AppHost/AppHost.cs         # sin cambios (admin ya registrado como quizarena-admin)

tests/OroQuizClash.Architecture.Tests/
├── AdminNoDirectDbTests.cs             # existente — cubre SC-005 para nuevo código
└── DashboardAuthorizationTests.cs      # NUEVO — verifica matriz 7 atajos × 3 roles + drill-down 403
```

**Structure Decision**: Extensión in-place de los dos proyectos Auto creados en 017. Sin nuevo proyecto/host. Las interfaces+DTOs viven en `QuizArena.Admin.Client` (referenciado por el server) con doble implementación `Client*` → `/bff/*` y `Server*` → `http://oroclash-api` (mismo patrón 017 R1). La página `Dashboard.razor` existente se refina para consumir `IDashboardService.GetSnapshotAsync()`; componentes nuevos bajo `Components/Dashboard/` siguen catálogo SPEC-016 (cards, skeletons, banners).

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) | — | — |

No se introduce nueva complejidad arquitectónica respecto a 017. YARP forwarder, OIDC y SignalR ya justificados en 017 Complexity Tracking; este feature los reutiliza sin nuevas dependencias.

## Constitution Check (Post-Design Re-Check)

*Re-evaluado tras Phase 1 (research.md + data-model.md + contracts/ + quickstart.md):*

| Gate | Estado |
|------|--------|
| I–VI, H, I, J | ✅ PASS — diseño BFF refuerza H (token nunca sale del servidor); V garantizado por re-consulta REST del snapshot + coherencia SC-003; VI sin login propio |
| Addendum 2 UI | ✅ PASS — contracts exigen consumo tokens SPEC-016, 4 estados por bloque (FR-007), WCAG AA, responsive 375-1536 |
| IV CQRS | ✅ PASS (N/A) — sin cambios backend; solo proyecciones read-model |
| Complejidad | ✅ PASS — 0 violaciones nuevas; reutiliza infra 017 |

**Resultado final: PASS — proceder a `/speckit.tasks` (Phase 2).**
