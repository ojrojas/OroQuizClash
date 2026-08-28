# Implementation Plan: Admin Categories

**Branch**: `020-admin-categories` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/020-admin-categories/spec.md`

## Summary

Superficie administrativa para **gestionar categorías de conocimiento** usadas por juegos y preguntas: 10 campos (nombre, descripción, área de conocimiento, nivel académico, rango edad, dificultad, público objetivo, estado, metadatos, reglas de progresión) con ejemplos Matemáticas–Finanzas, y máquina de 4 estados `Draft → Active ↔ Inactive → Archived` (guardas `Active` requiere `ValidQuestionCount ≥5`, `Archived` bloquea si tiene juegos en `Running`/`Scheduled`). Validación 3 niveles (API/Aplicación/Dominio), unicidad nombre case-insensitive, `rowversion` y auditoría append-only. Reutiliza 100% shell Blazor Auto net10.0 + BFF YARP→`oroclash-api` + OIDC OroIdentityServer + tema `administration` de SPEC-016. Extiende `Category` de dominio de `002-categories` (Constitución B) sin duplicar agregados; consumo exclusivo vía BFF, sin acceso directo a SQL/Oracle/`identitydb`.

## Technical Context

**Language/Version**: C# latest / **.NET 10 (`net10.0`) único target** (global.json 10.0.400, Directory.Build.props net10.0). Sin net11 multi-target.

**Primary Dependencies**:
- ASP.NET Core Blazor Web App Auto (proyectos existentes `QuizArena.Admin` + `QuizArena.Admin.Client`)
- `Yarp.ReverseProxy` (`AddHttpForwarderWithServiceDiscovery()` + `MapBffForwarder()` / `MapGameHubForwarder()` ya existentes en 017)
- `Microsoft.AspNetCore.Authentication.OpenIdConnect` + Cookie + `ConfigureCookieOidc` (refresh no-interactivo)
- `BuildingBlocks.ServiceDefaults` (OTel, health, resilience)
- Design System SPEC-016 — `design-system/tokens/design-tokens.css` + tema `administration` (tokens, componentes §9)

**Storage**: Ninguno en Admin. Categorías persisten en `oroclash-api` (SQL Server primario, abstracción Oracle). Admin MUST NOT tocar DB; todo via `POST/PUT/GET /bff/categories*` → `oroclash-api /api/categories*` con `rowversion` (FR-013, SC-005).

**Testing**: xUnit (`TestingPlatformDotnetTestSupport`); Architecture Tests (`DesignSystemNoDirectDbTests`, `AdminBffTests`); bUnit opcional para `CategoryForm`/`CategoryStateBadge`; `WebApplicationFactory` + OIDC mock para `AdminOrGameManager` vs `REWARD_MANAGER` 403; pruebas `rowversion` concurrencia en `tests/QuizArena.Admin.Tests`.

**Target Platform**: Web — Blazor Web App Auto (server+WASM) orquestado por `OroQuizClash.AppHost` 13.5.x (service discovery `oroclash-api`, `identity-api`).

**Project Type**: web-application — extensión de feature en 2 proyectos existentes (no nuevos proyectos).

**Performance Goals**: SC-001 creación <2m (90%); SC-004 rechazo categoría inválida/duplicada <2s percibidos; SC-009 listado paginado con 8 ejemplos + filtros <2s con skeleton; overhead BFF <100ms; transiciones <1s.

**Constraints**: BFF obligatorio (tokens nunca en navegador); WCAG 2.2 AA tema claro; responsive 375–1536 sin scroll horizontal; objetivos táctiles ≥44px; net10.0 único; OIDC solo OroIdentityServer; `must_change_password` gating antes de administrar (Constitución VI/J); `rowversion` optimista.

**Scale/Scope**: Operadores internos `ADMIN`/`GAME_MANAGER` (crean/gestionan), `REWARD_MANAGER` denegado; decenas de sesiones; 1 formulario 10 campos + listado paginado con 8 ejemplos + detalle con `ValidQuestionCount`; ~3 nuevas DTOs; catálogo cerrado 1 progresión.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Evidencia |
|-----------|--------|-----------|
| I. Domain First | ✅ PASS | Validaciones e invariantes (≥5 preguntas, unicidad, tags, progresión) impuestas en dominio `Category` (002); UI solo proyecta |
| II. Clean Architecture | ✅ PASS | Admin = presentación (J); `Web → BFF → Api → Application → Domain`; sin acceso directo a DB |
| III. BuildingBlocks | ✅ PASS | Reusa `BuildingBlocks.ServiceDefaults` + `BuildingBlocks.Kernel` primitives via API; no MediatR/MassTransit/AutoMapper añadido |
| IV. CQRS | ✅ PASS | Consume `CreateCategory`, `UpdateCategory`, `PublishCategory`, `ArchiveCategory` slices existentes (BuildingBlocks.CQRS) |
| V. Server Truth | ✅ PASS | `ValidQuestionCount` y transiciones autoridad del backend (server timestamps, `rowversion`); UI nunca inventa conteo |
| VI. OroIdentityServer | ✅ PASS | OIDC `authorization_code` + `refresh_token` vs discovery; `must_change_password` gating; sin login propio (FR-015) |
| B. Question & Category Invariants | ✅ PASS | `Active` requiere ≥5 preguntas válidas (4 opciones/1 correcta); `CategoryInUse` bloquea `Archived` con juegos activos |
| C. Configurable Rules | ✅ PASS | `Reglas de progresión` catálogo cerrado (4 valores) validado en dominio (Constitución C) |
| F. Concurrency | ✅ PASS | `rowversion` optimista protege edición simultánea en `Draft` (SC-008) |
| H. Security | ✅ PASS | BFF YARP adjunta Bearer server-side; `AdminOrGameManager` claim-based; REWARD_MANAGER 403 sin fuga (SC-006) |
| I. Observability | ✅ PASS | `AddServiceDefaults()` (OTel, /health); CorrelationId propagado; ProblemDetails RFC7807 sin fuga (FR-012) |
| J. API & Frontend | ✅ PASS | Presentation-only; DTOs boundary; paginación; auth OIDC code+refresh |
| Addendum 2 (UI/UX) | ✅ PASS | Consume SPEC-016 tema administration, tokens sin literales, estados Loading/Ready/Empty/Error por campo (FR-009) |
| net10.0 único | ✅ PASS | Extiende proyectos net10.0 existentes; no nuevo TFM |

**Resultado pre-Phase 0: PASS — sin violaciones.**

## Project Structure

### Documentation (this feature)

```text
specs/020-admin-categories/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── categories-bff.md
│   └── category-states.md
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
│   ├── Components/Categories/          # NUEVO — form + state controls
│   │   ├── CategoryForm.razor          # formulario 10 campos con validación por campo
│   │   ├── CategoryStateBadge.razor    # badge 4 estados + ValidQuestionCount
│   │   └── CategoryTransitionsBar.razor# botones Publish/Deactivate/Archive
│   └── Services/
│       └── (reusa BffForwarderExtensions, CookieOidcRefresher, Server*Services)
└── QuizArena.Admin.Client/             # WASM (net10.0) — existente
    ├── Models/Categories/              # NUEVO — DTOs Category, CategoryStateView, Metadata, Progression
    │   ├── Category.cs                 # 10 campos + ValidQuestionCount + RowVersion
    │   ├── CategoryStateView.cs        # 4 estados + mapping
    │   └── CategoryMetadata.cs         # tags, color, icono, reglas
    ├── Services/
    │   ├── ICategoriesService.cs       # existente — extender con Publish/Deactivate/Archive si falta
    │   ├── ClientCategoriesService.cs  # WASM → /bff/categories* (cookie)
    │   └── CategoryCatalogs.cs         # catálogos progresión + áreas ejemplo
    └── Pages/Categories/               # NUEVO — páginas Create, Edit, Detail, List
        ├── CategoryCreate.razor
        ├── CategoryEdit.razor
        ├── CategoryDetail.razor
        └── CategoriesList.razor

OroQuizClash.Domain/Categories/         # ya existe (002) — Category aggregate + invariants
OroQuizClash.Application/Features/Categories/ # ya existen slices CreateCategory, UpdateCategory, PublishCategory

tests/QuizArena.Admin.Tests/
├── CategoryTests.cs                    # NUEVO — 10 campos validación + unicidad + rowversion
└── CategoryStateTransitionTests.cs     # NUEVO — 4 estados, guardas ≥5, CategoryInUse

tests/OroQuizClash.Architecture.Tests/
└── (reusa DesignSystemNoDirectDbTests, AdminBffTests)
```

**Structure Decision**: Extensión in-place de los dos proyectos Auto creados en 017/018. Sin nuevo proyecto/host. `ICategoriesService` ya existe en `QuizArena.Admin.Client` (referenciado por server) con doble implementación `Client*` → `/bff/categories*` (cookie) y `Server*` → `http://oroclash-api/api/categories*` (Bearer del `HttpContext`). Reusa agregados de `002-categories` (sin duplicar lógica). BFF forwarder catch-all ya cubre `/bff/categories*`.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) | — | — |

No se introduce nueva complejidad respecto a 017–019. YARP, OIDC, `rowversion` y catálogos ya justificados en 002 y 017; este feature los reutiliza sin nuevas dependencias.

## Constitution Check (Post-Design Re-Check)

*Re-evaluado tras Phase 1 (research.md + data-model.md + contracts/ + quickstart.md):*

| Gate | Estado |
|------|--------|
| I–VI, B, C, F, H, I, J | ✅ PASS — validación 3 niveles, `ValidQuestionCount` y `CategoryInUse` refuerzan B/C/F; BFF + OIDC refuerzan H/VI |
| Addendum 2 UI | ✅ PASS — contracts exigen tokens SPEC-016, estados por campo, WCAG AA, responsive 375–1536, 44px |
| IV CQRS | ✅ PASS — reuse slices `CreateCategory` etc. sin duplicar |
| Complejidad | ✅ PASS — 0 violaciones nuevas |

**Resultado final: PASS — proceder a `/speckit.tasks` (Phase 2).**
