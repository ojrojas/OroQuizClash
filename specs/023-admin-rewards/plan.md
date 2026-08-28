# Implementation Plan: Admin Rewards

**Branch**: `023-admin-rewards` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/023-admin-rewards/spec.md`

## Summary

Superficie administrativa para **gestionar el catálogo de premios y su disponibilidad** (7 campos: nombre, descripción, tipo 6 valores `Monetary`/`Physical`/`Digital`/`Voucher`/`Experience`/`Consolation`, costo 1–100000, inventario `Stock` ≥0 (0=ilimitado según política), disponibilidad `AvailableFrom`/`AvailableTo` con `From<To`, estado `Active`/`Inactive`/`Archived`, `Elegible` = `Active` + stock + fechas) y **operar canjes** (`RewardRedemption`) con ciclo `Requested → Approved/Rejected → Delivered/Cancelled`, filtros, `RowVersion`/`IdempotencyKey` y auditoría append-only. Reutiliza 100% shell Blazor Auto net10.0 + BFF YARP→`oroclash-api` + OIDC OroIdentityServer + tema `administration` de SPEC-016. Extiende `Reward`/`RewardRedemption` de dominio de `009-reward-redemption` (Constitución C/D) sin duplicar agregados; consumo exclusivo vía BFF, sin acceso directo a SQL/Oracle/`identitydb`.

## Technical Context

**Language/Version**: C# latest / **.NET 10 (`net10.0`) único target** (global.json 10.0.400, Directory.Build.props net10.0). Sin net11 multi-target.

**Primary Dependencies**:
- ASP.NET Core Blazor Web App Auto (proyectos existentes `QuizArena.Admin` + `QuizArena.Admin.Client`)
- `Yarp.ReverseProxy` (`AddHttpForwarderWithServiceDiscovery()` + `MapBffForwarder()` / `MapGameHubForwarder()` ya existentes en 017)
- `Microsoft.AspNetCore.Authentication.OpenIdConnect` + Cookie + `ConfigureCookieOidc` (refresh no-interactivo)
- `BuildingBlocks.ServiceDefaults` (OTel, health, resilience)
- Design System SPEC-016 — `design-system/tokens/design-tokens.css` + tema `administration` (tokens, componentes §9)

**Storage**: Ninguno en Admin. Premios y canjes persisten en `oroclash-api` (SQL Server primario, abstracción Oracle, `rowversion` + `PointTransaction` ledger). Admin MUST NOT tocar DB; todo via `POST/PUT/GET /bff/rewards*` + `/bff/redemptions*` → `oroclash-api /api/rewards*`/`/api/redemptions*` con `rowversion` y `IdempotencyKey` (FR-018, SC-005).

**Testing**: xUnit (`TestingPlatformDotnetTestSupport`); Architecture Tests (`DesignSystemNoDirectDbTests`, `AdminBffTests`); bUnit opcional para `RewardForm`/`RedemptionRow`; `WebApplicationFactory` + OIDC mock para `RewardManagerOrAdmin` vs `GAME_MANAGER` 403; pruebas `rowversion`/`IdempotencyKey` + `RewardOutOfStock` + `InsufficientPoints` en `tests/QuizArena.Admin.Tests`.

**Target Platform**: Web — Blazor Web App Auto (server+WASM) orquestado por `OroQuizClash.AppHost` 13.5.x (service discovery `oroclash-api`, `identity-api`).

**Project Type**: web-application — extensión de feature en 2 proyectos existentes (no nuevos proyectos).

**Performance Goals**: SC-001 creación premio `Physical` <2m (90%); SC-004 `Requested→Approved` con stock + puntos <2s; SC-009 listado premios (≥50, 6 tipos) y canjes (por estado/tipo/fecha) paginado <2s con skeleton; overhead BFF <100ms.

**Constraints**: BFF obligatorio (tokens nunca en navegador); WCAG 2.2 AA tema claro; responsive 375–1536 sin scroll horizontal; objetivos táctiles ≥44px; net10.0 único; OIDC solo OroIdentityServer; `must_change_password` gating antes de administrar (Constitución VI/J); `rowversion` + `IdempotencyKey` para idempotencia; `Consolation` independiente (Constitución C).

**Scale/Scope**: Operadores internos `ADMIN`/`REWARD_MANAGER` (gestionan), `GAME_MANAGER` denegado; decenas de sesiones; 1 formulario 7 campos + listado premios (6 tipos) + listado canjes (5 estados) + auditoría; ~4 nuevas DTOs; `Cost` 1–100000, `Stock` ≥0.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Evidencia |
|-----------|--------|-----------|
| I. Domain First | ✅ PASS | Catálogo y canjes (`RewardAlreadyExists`, `RewardOutOfStock`, `InsufficientPoints`, `InvalidRedemptionState`) impuestos en dominio `Reward`/`RewardRedemption` (009); UI solo proyecta |
| II. Clean Architecture | ✅ PASS | Admin = presentación (J); `Web → BFF → Api → Application → Domain`; sin acceso directo a DB |
| III. BuildingBlocks | ✅ PASS | Reusa `BuildingBlocks.ServiceDefaults` + `BuildingBlocks.Kernel` primitives via API; no MediatR/MassTransit/AutoMapper añadido |
| IV. CQRS | ✅ PASS | Consume `CreateReward`, `UpdateReward`, `ActivateReward`, `RequestRedemption`, `ApproveRedemption`, `DeliverRedemption` slices existentes (BuildingBlocks.CQRS) |
| V. Server Truth | ✅ PASS | `Cost` vs `PointTransaction` ledger, `Stock` y `AvailableFrom/To` autoridad del backend; UI nunca calcula elegibilidad |
| VI. OroIdentityServer | ✅ PASS | OIDC `authorization_code` + `refresh_token` vs discovery; `must_change_password` gating; sin login propio (FR-020) |
| C. Configurable Rules | ✅ PASS | 6 tipos `RewardType` + `Cost`/`Stock`/`Availability` validados en dominio; `Consolation` independiente (Constitución C) |
| D. Scoring via Ledger | ✅ PASS | `Cost` descontado desde `PointTransaction` ledger, no mutación directa (009) |
| F. Concurrency | ✅ PASS | `rowversion` optimista + `IdempotencyKey` protegen `Approve` concurrentes con stock 1 (SC-008) |
| H. Security | ✅ PASS | BFF YARP adjunta Bearer server-side; `RewardManagerOrAdmin` claim-based; GAME_MANAGER 403 sin fuga (SC-006) |
| I. Observability | ✅ PASS | `AddServiceDefaults()` (OTel, /health); CorrelationId propagado; ProblemDetails RFC7807 sin fuga (FR-017) |
| J. API & Frontend | ✅ PASS | Presentation-only; DTOs boundary; paginación; auth OIDC code+refresh |
| Addendum 2 (UI/UX) | ✅ PASS | Consume SPEC-016 tema administration, tokens sin literales, estados Loading/Ready/Empty/Error por campo (FR-014) |
| net10.0 único | ✅ PASS | Extiende proyectos net10.0 existentes; no nuevo TFM |

**Resultado pre-Phase 0: PASS — sin violaciones.**

## Project Structure

### Documentation (this feature)

```text
specs/023-admin-rewards/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── rewards-bff.md
│   └── redemptions-bff.md
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
│   ├── Components/Rewards/             # NUEVO — form + state controls
│   │   ├── RewardForm.razor            # formulario 7 campos con validación por campo
│   │   ├── RewardStateBadge.razor      # badge 3 estados + Elegible
│   │   ├── RedemptionRow.razor         # fila canje con acciones Approve/Reject/Deliver
│   │   └── RewardAvailabilityBadge.razor # badge stock/fechas
│   └── Services/
│       └── (reusa BffForwarderExtensions, CookieOidcRefresher, Server*Services)
└── QuizArena.Admin.Client/             # WASM (net10.0) — existente
    ├── Models/Rewards/                 # NUEVO — DTOs Reward, RewardRedemption, RewardType
    │   ├── Reward.cs                   # 7 campos + Stock/Elegible + RowVersion
    │   ├── RewardType.cs               # 6 tipos + mapping
    │   └── Redemption.cs               # 5 estados canje + RowVersion
    ├── Services/
    │   ├── IRewardsService.cs          # existente — extender con Activate/Deactivate si falta
    │   ├── IRedemptionsService.cs      # existente — extender con Approve/Reject/Deliver/Cancel
    │   ├── ClientRewardsService.cs     # WASM → /bff/rewards* (cookie)
    │   ├── ClientRedemptionsService.cs # WASM → /bff/redemptions* (cookie)
    │   └── RewardCatalogs.cs           # catálogos 6 tipos + costo/stock
    └── Pages/Rewards/                  # NUEVO — páginas Create, Edit, Detail, List + Redemptions
        ├── RewardCreate.razor
        ├── RewardEdit.razor
        ├── RewardDetail.razor
        ├── RewardsList.razor
        └── RedemptionsList.razor

OroQuizClash.Domain/Rewards/            # ya existe (009) — Reward aggregate + RewardRedemption + Cost/Stock
OroQuizClash.Application/Features/Rewards/ # ya existen slices CreateReward, UpdateReward, ApproveRedemption

tests/QuizArena.Admin.Tests/
├── RewardTests.cs                      # NUEVO — 7 campos validación + unicidad + rowversion
└── RedemptionTests.cs                  # NUEVO — 5 estados canje, guards, stock, InsufficientPoints, 403

tests/OroQuizClash.Architecture.Tests/
└── (reusa DesignSystemNoDirectDbTests, AdminBffTests)
```

**Structure Decision**: Extensión in-place de los dos proyectos Auto creados en 017/018. Sin nuevo proyecto/host. `IRewardsService`/`IRedemptionsService` ya existen en `QuizArena.Admin.Client` (referenciados por server) con doble implementación `Client*` → `/bff/rewards*`/`/bff/redemptions*` (cookie) y `Server*` → `http://oroclash-api/api/rewards*` (Bearer del `HttpContext`). Reusa agregados de `009-reward-redemption` (sin duplicar lógica). BFF forwarder catch-all ya cubre `/bff/rewards*` y `/bff/redemptions*`.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) | — | — |

No se introduce nueva complejidad respecto a 017–022. YARP, OIDC, `rowversion`/`IdempotencyKey`, ledger y 6 tipos ya justificados en 009 y 017; este feature los reutiliza sin nuevas dependencias.

## Constitution Check (Post-Design Re-Check)

*Re-evaluado tras Phase 1 (research.md + data-model.md + contracts/ + quickstart.md):*

| Gate | Estado |
|------|--------|
| I–VI, C, D, F, H, I, J | ✅ PASS — catálogo 6 tipos + `Cost`/`Stock`/`Availability` + canjes `Requested→Delivered` con `rowversion`/`IdempotencyKey` refuerzan C/D/F; BFF + OIDC refuerzan H/VI |
| Addendum 2 UI | ✅ PASS — contracts exigen tokens SPEC-016, estados por campo, WCAG AA, responsive 375–1536, 44px |
| IV CQRS | ✅ PASS — reuse slices `CreateReward` etc. sin duplicar |
| Complejidad | ✅ PASS — 0 violaciones nuevas |

**Resultado final: PASS — proceder a `/speckit.tasks` (Phase 2).**
