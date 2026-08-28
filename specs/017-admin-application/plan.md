# Implementation Plan: QuizArena Administration Application

**Branch**: `017-admin-application` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/017-admin-application/spec.md`

## Summary

Aplicación web administrativa de QuizArena (10 secciones: Dashboard, Games, Game Configuration, Categories, Question Bank, Players, Rewards, Live Games, Reports, Audit) construida **exclusivamente en net10.0** con Blazor Web App de interactividad **Auto** (InteractiveServer + InteractiveWebAssembly). La comunicación con `QuizArena.Api` sigue el **patrón BFF** del sample oficial `BlazorWebAppOidcBffAutoYarpAspire` (dotnet/blazor-samples 10.0): interfaces de servicio compartidas en el proyecto cliente, implementación cliente que llama por HttpClient a endpoints del propio servidor (URL compartida), y el servidor reenviando al API mediante **YARP forwarder** con el access_token de la cookie OIDC. Autenticación delegada 100% en OroIdentityServer (OIDC authorization_code + refresh_token). Sin acceso directo a base de datos (FR-030). UI consume el Design System SPEC-016 (tema `administration`).

## Technical Context

**Language/Version**: C# latest / **.NET 10 (`net10.0`) — único target** (mandato explícito del usuario; alineado con `global.json` SDK 10.0.400 y `Directory.Build.props` TargetFramework net10.0). Sin net11.0 ni multi-targeting.

**Primary Dependencies**:
- ASP.NET Core Blazor Web App (template `dotnet new blazor -f net10.0 -ai true -int Auto`) → proyectos `QuizArena.Admin` (server) + `QuizArena.Admin.Client` (WASM)
- `Microsoft.AspNetCore.Authentication.OpenIdConnect` + Cookie (OIDC code flow vs OroIdentityServer/OpenIddict)
- `Yarp.ReverseProxy` vía `AddHttpForwarderWithServiceDiscovery()` (BFF forwarder + Aspire service discovery)
- `Microsoft.AspNetCore.SignalR.Client` (Live Games, cliente WASM contra hub reenviado)
- `BuildingBlocks.ServiceDefaults` (ProjectReference — OTel, health, resilience)
- Design System SPEC-016: `design-system/tokens/design-tokens.css` (referencia estática)

**Storage**: Ninguno. La aplicación MUST NOT tocar base de datos; todo dato proviene de `QuizArena.Api` vía BFF (FR-030, SC-003).

**Testing**: xUnit (convención repo, `TestingPlatformDotnetTestSupport`); tests de arquitectura (no-DB, BFF wiring) en `tests/OroQuizClash.Architecture.Tests`; bUnit opcional para componentes de UI si aporta valor.

**Target Platform**: Web — Blazor Web App Auto (server + WASM), orquestado por `OroQuizClash.AppHost` (Aspire 13.5.x).

**Project Type**: web-application (2 proyectos .NET + integración AppHost).

**Performance Goals**: SC-005 live <5s sin refresh; SC-010 listados <2s percibidos; BFF añade 1 hop — presupuesto <100ms overhead local.

**Constraints**: BFF obligatorio (tokens nunca en navegador); WCAG 2.2 AA (SPEC-016); responsive 375–1536; net10.0 único; OIDC solo vía OroIdentityServer (Constitución VI/J).

**Scale/Scope**: Usuarios internos (ADMIN/GAME_MANAGER/REWARD_MANAGER), decenas de sesiones concurrentes; 10 secciones, ~30 pantallas/vistas, ~10 interfaces de servicio consumiendo ~70 endpoints existentes del API.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Evidencia |
|-----------|--------|-----------|
| I. Domain First | ✅ PASS | UI sin reglas de negocio; solo proyecciones y llamadas al API (el backend permanece autoridad) |
| II. Clean Architecture | ✅ PASS | Frontend = capa de presentación (Constitución J); dependencia UI → BFF → API |
| III. BuildingBlocks | ✅ PASS | Reusa `BuildingBlocks.ServiceDefaults` (OTel/health/resilience); no se reinventa infraestructura |
| IV. CQRS | N/A | No hay cambios de backend en este feature |
| V. Server Truth | ✅ PASS | UI nunca infiere estado autorizado: tras evento realtime re-consulta REST (política GameHub FR-015/019) |
| VI. OroIdentityServer | ✅ PASS | OIDC authorization_code + refresh_token contra discovery; sin login propio ni user store local; `/Account/*` del proveedor |
| H. Security | ✅ PASS | BFF: access_token vive en cookie del servidor; YARP transform adjunta Bearer server-side; navegador nunca ve el token |
| I. Observabilidad | ✅ PASS | `AddServiceDefaults()` (OTel logs/traces/metrics, `/health`/`/alive`), CorrelationId propagado |
| J. API & Frontend | ✅ PASS | Frontend presentation-only; auth OIDC code+refresh; manejo de claim `must_change_password` |
| Addendum 2 (UI/UX) | ✅ PASS | Consume SPEC-016: `data-theme="administration"`, tokens sin literales, AA, 375/768/1024/1440, estados §9 |
| net10.0 único | ✅ PASS | Mandato usuario + global.json 10.0.400 + Directory.Build.props net10.0 |

**Resultado pre-Phase 0: PASS — sin violaciones.** (Re-evaluación post-diseño al final.)

## Project Structure

### Documentation (this feature)

```text
specs/017-admin-application/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── bff-endpoints.md
│   ├── service-interfaces.md
│   ├── oidc-config.md
│   └── realtime.md
├── checklists/requirements.md
└── tasks.md             # Phase 2 output (/speckit.tasks - NOT created here)
```

### Source Code (repository root)

```text
src/Admin/
├── QuizArena.Admin/                    # Blazor Web App host (net10.0) — creado por `dotnet new blazor`
│   ├── Components/
│   │   ├── App.razor                   # <html data-theme="administration"> + design-tokens.css
│   │   ├── Routes.razor                # AuthorizeRouteView + must_change_password gating
│   │   ├── Layout/ (MainLayout, NavMenu con secciones filtradas por rol)
│   │   └── Pages/                      # 10 secciones (Dashboard, Games, GameConfiguration, Categories, QuestionBank, Players, Rewards, LiveGames, Reports, Audit)
│   ├── Services/
│   │   ├── Server*Service.cs           # Implementaciones server-side de las interfaces compartidas (InteractiveServer)
│   │   ├── BffForwarderExtensions.cs   # MapForwarder catch-all /bff/{**} → /api/{**} + Bearer transform
│   │   └── CookieOidcRefresher.cs      # Refresh no-interactivo del access_token (patrón del sample)
│   └── Program.cs                      # OIDC + Cookie + YARP forwarder + SignalR forward + ServiceDefaults
└── QuizArena.Admin.Client/             # Proyecto WASM (net10.0)
    ├── Models/                         # DTOs compartidos (GameSummary, CategorySummary, ...)
    ├── Services/
    │   ├── I*Service.cs                # Interfaces compartidas (contrato único cliente/servidor)
    │   └── Client*Service.cs           # Implementaciones cliente: HttpClient → /bff/* (URL compartida)
    ├── Auth/                           # PersistentAuthenticationStateProvider (deserialización de claims)
    └── Program.cs                      # AddAuthorizationCore + CascadingAuthState + HttpClient BFF

OroQuizClash.AppHost/AppHost.cs         # + builder.AddProject<Projects.QuizArena_Admin>("quizarena-admin")
tests/OroQuizClash.Architecture.Tests/  # + AdminNoDirectDbTests (ya existe DesignSystemNoDirectDbTests) / BFF wiring tests
```

**Structure Decision**: Dos proyectos según template oficial Auto (server + `.Client`), siguiendo exactamente la estructura del sample `BlazorWebAppOidcBffAutoYarpAspire`: las **interfaces y DTOs viven en `QuizArena.Admin.Client`** (referenciado por el server), con doble implementación — `Client*Service` (WASM → `/bff/*` con cookie) y `Server*Service` (InteractiveServer → API vía service discovery con token del `HttpContext`). El servidor expone el forwarder catch-all `/bff/{**catch-all}` → `/api/{**catch-all}` y el forward del hub `/hubs/game`. Integración Aspire vía AppHost existente.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Dependencia YARP (`Yarp.ReverseProxy`) | Mandato del usuario: patrón BFF del sample oficial con forwarder | Llamar WASM→API directamente expondría el JWT al navegador (rompe Constitución H/BFF); wrappers minimal-API por endpoint duplicarían ~70 rutas y su mantenimiento |
| Forwarder catch-all `/bff/{**catch-all}` | Cubre todos los endpoints actuales y futuros con una sola declaración | Mapeos por ruta individuales: 70+ registros, drift garantizado al evolucionar el API; el API ya impone sus políticas de autorización por endpoint |
| Forward del hub SignalR a través del BFF | Live Games necesita WebSockets sin token en el cliente | Conexión directa WASM→hub requeriría entregar el access_token al navegador; conexión server-side singleton + re-broadcast añade un hub propio y autenticación servicio-a-servicio (más complejo) |

## Constitution Check (Post-Design Re-Check)

*Re-evaluado tras Phase 1 (research.md + data-model.md + contracts/ + quickstart.md):*

| Gate | Estado |
|------|--------|
| I–VI, H, I, J | ✅ PASS — el diseño BFF refuerza H (token nunca sale del servidor); V garantizado por política "re-consultar REST tras evento" en contracts/realtime.md |
| Addendum 2 UI | ✅ PASS — contracts exigen consumo de tokens SPEC-016 y estados §9 por pantalla |
| Complejidad | ✅ Justificada en Complexity Tracking (3 entradas, todas por mandato de patrón BFF del usuario) |

**Resultado final: PASS — proceder a `/speckit.tasks`.**
