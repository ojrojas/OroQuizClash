# Implementation Plan: Admin Audit

**Branch**: `026-admin-audit` | **Date**: 2026-05-13 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/026-admin-audit/spec.md`

## Summary

Superficie administrativa de **solo lectura para trazabilidad** — 9 campos: Who (actor `sub`/DisplayName/email/tenant), What (descripción), When (timestamp UTC), Where (servicio/endpoint/IP/`CorrelationId`/`TraceId`), Entity (tipo + `EntityId` 7 tipos), Previous Value (JSON previo o null), New Value (JSON), Action (catálogo cerrado `CREATE`/`UPDATE`/…/`APPROVE`/…), Result (`Success`/`Failed` + `ErrorCode`) — paginada server-side (`page`/`pageSize` 20, `TotalCount`) con 9 filtros combinados AND (Who/What/When/Where/Entity/Action/Result) + diff Previous/New Value y `CorrelationId` clicable. Se integra con **SPEC-014 Audit** (trail append-only `AuditEntry` + Outbox `IOutboxWriter` en `AppDbContextBase.SaveChanges`, inmutable, `Previous`/`New` snapshots) sin duplicar ni re-escribir, con auditoría de consultas opcional (`AuditViewAudit`). Reutiliza 100% shell Blazor Auto net10.0 + BFF YARP→`oroclash-api` + OIDC OroIdentityServer + tema `administration` de SPEC-016. Reusa `AuditEntry` y queries existentes sin nuevo agregado; consumo exclusivo vía BFF, sin acceso directo a SQL/Oracle/`identitydb`.

## Technical Context

**Language/Version**: C# latest / **.NET 10 (`net10.0`) único target** (global.json 10.0.400, Directory.Build.props net10.0). Sin net11 multi-target.

**Primary Dependencies**:
- ASP.NET Core Blazor Web App Auto (proyectos existentes `QuizArena.Admin` + `QuizArena.Admin.Client`)
- `Yarp.ReverseProxy` (`AddHttpForwarderWithServiceDiscovery()` + `MapBffForwarder()` / `MapGameHubForwarder()` ya existentes en 017)
- `Microsoft.AspNetCore.Authentication.OpenIdConnect` + Cookie + `ConfigureCookieOidc` (refresh no-interactivo)
- `BuildingBlocks.ServiceDefaults` (OTel, health, resilience)
- Design System SPEC-016 — `design-system/tokens/design-tokens.css` + tema `administration` (tokens, componentes §9)

**Storage**: Ninguno en Admin. Auditoría persiste en `oroclash-api` (SQL Server primario, abstracción Oracle, `AuditEntry` append-only + Outbox, `CorrelationId`/`TraceId` OTel). Admin MUST NOT tocar DB; todo via `GET /bff/audit?who=&what=&whenFrom=&whenTo=&where=&entityType=&entityId=&action=&result=&page=` → `oroclash-api /api/audit*` con paginación y `CorrelationId` (FR-011, SC-008).

**Testing**: xUnit (`TestingPlatformDotnetTestSupport`); Architecture Tests (`DesignSystemNoDirectDbTests`, `AdminBffTests`); bUnit opcional para `AuditTable`/`AuditDetail`; `WebApplicationFactory` + OIDC mock para `ADMIN` vs `GAME_MANAGER` vs `REWARD_MANAGER` 403; pruebas de paginación server-side + `ProblemDetails` + `CorrelationId` + `Previous`/`New` diff en `tests/QuizArena.Admin.Tests`.

**Target Platform**: Web — Blazor Web App Auto (server+WASM) orquestado por `OroQuizClash.AppHost` 13.5.x (service discovery `oroclash-api`, `identity-api`).

**Project Type**: web-application — extensión de feature en 2 proyectos existentes (no nuevos proyectos).

**Performance Goals**: SC-001 listado 9 campos <2s con skeleton (90%); SC-002 filtros combinados 9 dimensiones paginados <2s (100%) sin cargar colecciones; SC-003 detalle `UPDATE` con diff `Previous`/`New` <2s; SC-006 flujo completo 4 pasos <2min (95%); overhead BFF <100ms; paginación ≥10k entradas sin degradación.

**Constraints**: BFF obligatorio (tokens nunca en navegador); WCAG 2.2 AA tema claro; responsive 375–1536 sin scroll horizontal; objetivos táctiles ≥44px; net10.0 único; OIDC solo OroIdentityServer; `must_change_password` gating antes de consultar (Constitución VI/J); solo lectura en v1 (trail append-only, sin edición/borrado, sin exportación CSV/PDF); `Previous`/`New` enmascarado si secreto.

**Scale/Scope**: Operadores internos `ADMIN` (todo), `GAME_MANAGER` (`Game`/`Category`/`Question`/`GamePlayer`), `REWARD_MANAGER` (`Reward`/`Redemption`); decenas de sesiones concurrentes; 1 listado paginado (9 filtros) + 1 detalle con diff + `CorrelationId` clicable; ~3 nuevos DTOs de lectura + 1 `AuditFilter`; paginación `page`/`pageSize` 20 por defecto.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principio | Estado | Evidencia |
|-----------|--------|-----------|
| I. Domain First | ✅ PASS | Auditoría es `AuditEntry` append-only en Domain (`014`); Admin solo proyecta, no muta; invariante `audit-append-only` protegido en `AppDbContextBase.SaveChanges` + Outbox |
| II. Clean Architecture | ✅ PASS | Admin = presentación (J); `Web → BFF → Api → Application → Domain`; sin acceso directo a DB; DTOs en boundary |
| III. BuildingBlocks | ✅ PASS | Reusa `BuildingBlocks.ServiceDefaults` + `BuildingBlocks.Kernel` primitives via API; no MediatR/MassTransit/AutoMapper añadido |
| IV. CQRS | ✅ PASS | Consume queries existentes `GetAuditEntries`, `GetAuditEntry` (BuildingBlocks.CQRS) — solo lectura |
| V. Server Truth | ✅ PASS | `Who`/`When`/`Previous`/`New`/`Result` autoridad del backend (014); UI nunca recalcula ni edita trail |
| VI. OroIdentityServer | ✅ PASS | OIDC `authorization_code` + `refresh_token` vs discovery; `must_change_password` gating; `Who` desde `sub`/`userinfo` (FR-001) |
| C. Configurable Rules | ✅ PASS | Consulta sin hardcodear; `Action`/`Entity`/`Result` catálogos cerrados (Constitución C) |
| D. Scoring via Ledger | ✅ PASS | No aplica directo, pero `Previous`/`New` para `PointTransaction`/`Reward` cohérente con ledger D |
| F. Concurrency | ✅ PASS | Lectura sin escritura — no requiere `rowversion`/`IdempotencyKey`; lecturas idempotentes y propagan `CorrelationId`/`TraceId` |
| H. Security | ✅ PASS | BFF YARP adjunta Bearer server-side; políticas `AdminOnly`/`AdminOrGameManager`/`RewardManagerOrAdmin` claim-based; 403 sin fuga (SC-007); no acceso directo a `identitydb` |
| I. Observability | ✅ PASS | `AddServiceDefaults()` (OTel, /health); CorrelationId/TraceId propagado; ProblemDetails RFC7807 sin fuga (FR-009); `Where` con `CorrelationId` clicable |
| J. API & Frontend | ✅ PASS | Presentation-only; DTOs boundary; paginación server-side; auth OIDC code+refresh |
| Addendum 2 (UI/UX) | ✅ PASS | Consume SPEC-016 tema administration, tokens sin literales, estados Loading/Ready/Empty/Error, skeleton, WCAG AA, responsive 375–1536, 44px |
| net10.0 único | ✅ PASS | Extiende proyectos net10.0 existentes; no nuevo TFM |

**Resultado pre-Phase 0: PASS — sin violaciones.**

## Project Structure

### Documentation (this feature)

```text
specs/026-admin-audit/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── audit-bff.md
│   └── audit-detail-bff.md
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
│   ├── Components/Audit/               # NUEVO — tabla + detalle diff
│   │   ├── AuditTable.razor            # listado 9 campos paginado
│   │   ├── AuditDetail.razor           # detalle con Previous/New Value diff
│   │   └── AuditFiltersBar.razor       # 9 filtros combinados
│   └── Services/
│       └── (reusa BffForwarderExtensions, CookieOidcRefresher, Server*Services)
└── QuizArena.Admin.Client/             # WASM (net10.0) — existente
    ├── Models/Audit/                   # NUEVO — DTOs AuditEntry, AuditFilter
    │   ├── AuditEntry.cs               # 9 campos + Previous/New + Result
    │   └── AuditFilter.cs              # 9 filtros + validación
    ├── Services/
    │   ├── IAuditService.cs            # existente — extender con GetAudit/GetEntry
    │   ├── ClientAuditService.cs       # WASM → /bff/audit* (cookie)
    │   └── AuditCatalogs.cs            # catálogos Action/Entity/Result
    └── Pages/Audit/                    # NUEVO — listado + detalle
        ├── AuditList.razor             # /admin/audit
        └── AuditEntryDetail.razor      # /admin/audit/{id}

OroQuizClash.Domain/Audit/              # ya existe (014) — AuditEntry append-only + Previous/New Value
OroQuizClash.Application/Features/Audit/ # ya existen queries GetAuditEntries, GetAuditEntry

tests/QuizArena.Admin.Tests/
├── AuditListTests.cs                   # NUEVO — 9 campos, filtros combinados, paginación
└── AuditDetailTests.cs                 # NUEVO — Previous/New diff, CorrelationId, 403

tests/OroQuizClash.Architecture.Tests/
└── (reusa DesignSystemNoDirectDbTests, AdminBffTests)
```

**Structure Decision**: Extensión in-place de los dos proyectos Auto creados en 017/018. Sin nuevo proyecto/host. `IAuditService` ya existe en `QuizArena.Admin.Client` (referenciado por server) con doble implementación `Client*` → `/bff/audit*` (cookie) y `Server*` → `http://oroclash-api/api/audit*` (Bearer del `HttpContext`). Reusa `AuditEntry` de `014` sin duplicar lógica; BFF forwarder catch-all ya cubre `/bff/audit*`. Solo lectura en v1 — no se añade mutación ni exportación.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) | — | — |

No se introduce nueva complejidad respecto a 014 y 017–025. YARP, OIDC, `AuditEntry` append-only, `Specification` paginada y `Previous`/`New` diff ya justificados en 014 y 017; este feature los reutiliza en modo lectura sin nuevas dependencias.

## Constitution Check (Post-Design Re-Check)

*Re-evaluado tras Phase 1 (research.md + data-model.md + contracts/ + quickstart.md):*

| Gate | Estado |
|------|--------|
| I–VI, C, D, F, H, I, J | ✅ PASS — auditoría 9 campos vía `AuditEntry` append-only + Outbox refuerza I/F; BFF + OIDC refuerzan H/VI; `CorrelationId` refuerza I |
| Addendum 2 UI | ✅ PASS — contracts exigen tokens SPEC-016, estados por pestaña, WCAG AA, responsive 375–1536, 44px |
| IV CQRS | ✅ PASS — reuse queries GetAuditEntries etc. sin duplicar |
| Complejidad | ✅ PASS — 0 violaciones nuevas |

**Resultado final: PASS — proceder a `/speckit.tasks` (Phase 2).**
