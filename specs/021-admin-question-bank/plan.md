# Implementation Plan: Admin Question Bank

**Branch**: `021-admin-question-bank` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/021-admin-question-bank/spec.md`

## Summary

Superficie administrativa para **gestionar el banco de preguntas** de QuizArena: crear/editar con `Texto`, `Categoría` (no archivada), `Dificultad` (1–5), `Nivel académico`, `Rango edad` (0–120), `Tiempo` (5–300s), `Explicación` (0–1000), y exactamente **4 respuestas** (`Answer A–D`) con **1 correcta**; ciclo de vida `Draft ↔ Active ↔ Inactive → Archived/Deleted` (invariante 4/1), guarda `CategoryNotReady` y `QuestionInUse`, y **estadísticas** agregadas (por categoría/dificultad/estado/tiempo) con **mínimo configurable** `CategoryMinQuestions` inicial 5. Reutiliza 100% shell Blazor Auto net10.0 + BFF YARP→`oroclash-api` + OIDC OroIdentityServer + tema `administration` de SPEC-016. Extiende `Question`/`Category` de dominio de `003-question-bank`/`020-admin-categories` (Constitución B) sin duplicar agregados; consumo exclusivo vía BFF, sin acceso directo a SQL/Oracle/`identitydb`.

## Technical Context

**Language/Version**: C# latest / **.NET 10 (`net10.0`) único target** (global.json 10.0.400, Directory.Build.props net10.0). Sin net11 multi-target.

**Primary Dependencies**:
- ASP.NET Core Blazor Web App Auto (proyectos existentes `QuizArena.Admin` + `QuizArena.Admin.Client`)
- `Yarp.ReverseProxy` (`AddHttpForwarderWithServiceDiscovery()` + `MapBffForwarder()` / `MapGameHubForwarder()` ya existentes en 017)
- `Microsoft.AspNetCore.Authentication.OpenIdConnect` + Cookie + `ConfigureCookieOidc` (refresh no-interactivo)
- `BuildingBlocks.ServiceDefaults` (OTel, health, resilience)
- Design System SPEC-016 — `design-system/tokens/design-tokens.css` + tema `administration` (tokens, componentes §9)

**Storage**: Ninguno en Admin. Preguntas persisten en `oroclash-api` (SQL Server primario, abstracción Oracle, `rowversion` + `CHECK` 4/1). Admin MUST NOT tocar DB; todo via `POST/PUT/GET/DELETE /bff/questions*` → `oroclash-api /api/questions*` con `rowversion` y `ValidQuestionCount` (FR-016, SC-005).

**Testing**: xUnit (`TestingPlatformDotnetTestSupport`); Architecture Tests (`DesignSystemNoDirectDbTests`, `AdminBffTests`); bUnit opcional para `QuestionForm`/`QuestionStateBadge`; `WebApplicationFactory` + OIDC mock para `AdminOrGameManager` vs `REWARD_MANAGER` 403; pruebas `rowversion`/`QuestionInUse` en `tests/QuizArena.Admin.Tests`.

**Target Platform**: Web — Blazor Web App Auto (server+WASM) orquestado por `OroQuizClash.AppHost` 13.5.x (service discovery `oroclash-api`, `identity-api`).

**Project Type**: web-application — extensión de feature en 2 proyectos existentes (no nuevos proyectos).

**Performance Goals**: SC-001 creación <3m (90%); SC-004 rechazo inválido <2s; SC-009 listado paginado con 100 preguntas + filtros <2s con skeleton; estadísticas agregadas <1s sin cargar todo; overhead BFF <100ms.

**Constraints**: BFF obligatorio (tokens nunca en navegador); WCAG 2.2 AA tema claro; responsive 375–1536 sin scroll horizontal; objetivos táctiles ≥44px; net10.0 único; OIDC solo OroIdentityServer; `must_change_password` gating antes de administrar (Constitución VI/J); `rowversion` optimista; invariante 4/1 estricta.

**Scale/Scope**: Operadores internos `ADMIN`/`GAME_MANAGER` (crean/gestionan), `REWARD_MANAGER` denegado; decenas de sesiones; 1 formulario 9 campos + 4 respuestas + listado paginado con filtros + detalle con 4 respuestas + estadísticas; ~3 nuevas DTOs; `CategoryMinQuestions` inicial 5 configurable.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Evidencia |
|-----------|--------|-----------|
| I. Domain First | ✅ PASS | Invariante 4/1 y guardas `CategoryNotReady`/`QuestionInUse` impuestas en dominio `Question` (003); UI solo proyecta |
| II. Clean Architecture | ✅ PASS | Admin = presentación (J); `Web → BFF → Api → Application → Domain`; sin acceso directo a DB |
| III. BuildingBlocks | ✅ PASS | Reusa `BuildingBlocks.ServiceDefaults` + `BuildingBlocks.Kernel` primitives via API; no MediatR/MassTransit/AutoMapper añadido |
| IV. CQRS | ✅ PASS | Consume `CreateQuestion`, `UpdateQuestion`, `ActivateQuestion`, `DeactivateQuestion`, `DeleteQuestion` slices existentes (BuildingBlocks.CQRS) |
| V. Server Truth | ✅ PASS | 4/1, `ValidQuestionCount` y `QuestionInUse` autoridad del backend (server timestamps, `rowversion`); UI nunca inventa conteo |
| VI. OroIdentityServer | ✅ PASS | OIDC `authorization_code` + `refresh_token` vs discovery; `must_change_password` gating; sin login propio (FR-018) |
| B. Question & Category Invariants | ✅ PASS | Exactamente 4 opciones/1 correcta, `ValidQuestionCount` ≥5 para `Active`, `QuestionInUse` bloquea borrado con juegos activos |
| C. Configurable Rules | ✅ PASS | `CategoryMinQuestions` configurable (inicial 5) validado en dominio; no hardcode |
| F. Concurrency | ✅ PASS | `rowversion` optimista protege edición simultánea en `Draft` (SC-008) + `QuestionInUse` |
| H. Security | ✅ PASS | BFF YARP adjunta Bearer server-side; `AdminOrGameManager` claim-based; REWARD_MANAGER 403 sin fuga (SC-006) |
| I. Observability | ✅ PASS | `AddServiceDefaults()` (OTel, /health); CorrelationId propagado; ProblemDetails RFC7807 sin fuga (FR-015) |
| J. API & Frontend | ✅ PASS | Presentation-only; DTOs boundary; paginación; auth OIDC code+refresh |
| Addendum 2 (UI/UX) | ✅ PASS | Consume SPEC-016 tema administration, tokens sin literales, estados Loading/Ready/Empty/Error por campo (FR-012) |
| net10.0 único | ✅ PASS | Extiende proyectos net10.0 existentes; no nuevo TFM |

**Resultado pre-Phase 0: PASS — sin violaciones.**

## Project Structure

### Documentation (this feature)

```text
specs/021-admin-question-bank/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── question-bank-bff.md
│   └── question-stats.md
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
│   ├── Components/Questions/           # NUEVO — form + state controls
│   │   ├── QuestionForm.razor          # formulario 9 campos + 4 respuestas (A–D) + explicación
│   │   ├── QuestionStateBadge.razor    # badge 4 estados + ValidQuestionCount
│   │   └── QuestionStatsPanel.razor    # panel estadísticas agregadas
│   └── Services/
│       └── (reusa BffForwarderExtensions, CookieOidcRefresher, Server*Services)
└── QuizArena.Admin.Client/             # WASM (net10.0) — existente
    ├── Models/Questions/               # NUEVO — DTOs Question, AnswerOption, QuestionStatistics
    │   ├── Question.cs                 # 9 campos + 4 respuestas + RowVersion
    │   ├── QuestionStateView.cs        # 4 estados + mapping
    │   └── QuestionStatistics.cs       # agregados por categoría/dificultad/estado
    ├── Services/
    │   ├── IQuestionsService.cs        # existente — extender con Activate/Deactivate/Delete/Stats si falta
    │   ├── ClientQuestionsService.cs   # WASM → /bff/questions* (cookie)
    │   └── QuestionCatalogs.cs         # catálogos dificultad/nivel/tiempo
    └── Pages/Questions/                # NUEVO — páginas Create, Edit, Detail, List + Stats
        ├── QuestionCreate.razor
        ├── QuestionEdit.razor
        ├── QuestionDetail.razor
        ├── QuestionsList.razor
        └── QuestionStats.razor

OroQuizClash.Domain/Questions/          # ya existe (003) — Question aggregate + 4/1 invariant
OroQuizClash.Application/Features/Questions/ # ya existen slices CreateQuestion, UpdateQuestion, ActivateQuestion

tests/QuizArena.Admin.Tests/
├── QuestionTests.cs                    # NUEVO — 9 campos + 4/1 + rowversion
└── QuestionStateTransitionTests.cs     # NUEVO — Active↔Inactive, QuestionInUse, CategoryNotReady

tests/OroQuizClash.Architecture.Tests/
└── (reusa DesignSystemNoDirectDbTests, AdminBffTests)
```

**Structure Decision**: Extensión in-place de los dos proyectos Auto creados en 017/018. Sin nuevo proyecto/host. `IQuestionsService` ya existe en `QuizArena.Admin.Client` (referenciado por server) con doble implementación `Client*` → `/bff/questions*` (cookie) y `Server*` → `http://oroclash-api/api/questions*` (Bearer del `HttpContext`). Reusa agregados de `003-question-bank`/`020-admin-categories` (sin duplicar lógica). BFF forwarder catch-all ya cubre `/bff/questions*` y `CategoryMinQuestions` se expone como `GET /bff/categories/{id}?include=stats` o `GET /bff/questions/stats`.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) | — | — |

No se introduce nueva complejidad respecto a 017–020. YARP, OIDC, `rowversion`, `ValidQuestionCount` y 4/1 ya justificados en 003 y 020; este feature los reutiliza sin nuevas dependencias.

## Constitution Check (Post-Design Re-Check)

*Re-evaluado tras Phase 1 (research.md + data-model.md + contracts/ + quickstart.md):*

| Gate | Estado |
|------|--------|
| I–VI, B, C, F, H, I, J | ✅ PASS — invariante 4/1, `ValidQuestionCount`/`QuestionInUse` y `CategoryMinQuestions` refuerzan B/C/F; BFF + OIDC refuerzan H/VI |
| Addendum 2 UI | ✅ PASS — contracts exigen tokens SPEC-016, estados por campo, WCAG AA, responsive 375–1536, 44px |
| IV CQRS | ✅ PASS — reuse slices `CreateQuestion` etc. sin duplicar |
| Complejidad | ✅ PASS — 0 violaciones nuevas |

**Resultado final: PASS — proceder a `/speckit.tasks` (Phase 2).**
