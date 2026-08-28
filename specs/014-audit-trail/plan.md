# Implementation Plan: Audit Trail

**Branch**: `014-audit-trail` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/014-audit-trail/spec.md`

## Summary

Trazabilidad transversal completa: extender la infraestructura de auditoría de SPEC-013 (`AuditEntry` append-only, `AuditBehavior`, `GET /api/audit`) para cubrir los 16 `Action` de dominio (`GameCreated` … `AdministrativeAdjustment`) con el modelo conceptual `AuditRecord` (11 campos: Id/Timestamp/Actor/Action/Resource/ResourceId/GameId/PlayerId/CorrelationId/Data/Result). Cada operación relevante genera exactamente un `AuditRecord` immutable vía pipeline centralizado (behavior), consultable de forma paginada por `GameId`/`PlayerId`/`Action`/`Resource`/`CorrelationId`/ventana `Timestamp`, sin que la auditoría condicione nunca la lógica de negocio. Reutilización total de BuildingBlocks y single-node.

## Technical Context

**Language/Version**: C# 12 / .NET 10.0 (`net10.0`, `LangVersion latest`, `Nullable enable`, `ImplicitUsings enable`)

**Primary Dependencies**: `BuildingBlocks.Kernel.Domain` (Entity, AggregateRoot, Enumeration, IRepository, Specification, Result/Error, IBusinessRule), `BuildingBlocks.CQRS` (ICommand/IQuery, ISender, IPipelineBehavior — `AuditBehavior`), `BuildingBlocks.Kernel.Infrastructure` (AppDbContextBase, EfRepository, IUnitOfWork), `BuildingBlocks.ServiceDefaults` (IEndpoint, GlobalExceptionHandler, OTel `Activity`/`CorrelationId`), `Microsoft.EntityFrameworkCore` 10, `Microsoft.AspNetCore.Authentication.JwtBearer` (OroIdentityServer `sub`/`role`), `OroIdentityServer` Podman (claims)

**Storage**: SQL Server primario (Aspire) / SQLite local (`oroclash.db`, `EnsureCreatedAsync`); EF Core 10; tabla `AuditEntries` ya existente desde SPEC-013 (extender columnas `ResourceId`, `GameId`, `PlayerId`, `Data` si faltan) con índices `(GameId)`, `(PlayerId)`, `(Action)`, `(CorrelationId)`, `(Timestamp)`; sin migraciones, append-only

**Testing**: xUnit v3 + NSubstitute + coverlet + `Microsoft.AspNetCore.Mvc.Testing` (opcional para `GET /api/audit` 403/200); Domain tests para inmutabilidad, Application tests para `AuditBehavior` (16 actions), Api tests para búsqueda paginada y trazabilidad, Architecture tests para append-only y transversalidad

**Target Platform**: Linux containers (Podman), .NET Aspire AppHost, ASP.NET Core minimal APIs (IEndpoint)

**Project Type**: web-service — modular monolith transversal (Domain / Application / Infrastructure / Api)

**Performance Goals**: SC-007 — búsqueda por `GameId`/`CorrelationId` con 1000 registros <500 ms p95; inserción `AuditRecord` overhead <50 ms p95 sobre operación de negocio; paginación estable sin duplicados/pérdidas (SC-003)

**Constraints**: Constitución I/II/III/VI/H/I: auditoría centralizada, no dispersa; append-oriented + immutable (sin Update/Delete); searchable + traceable por `CorrelationId`; nunca condiciona negocio (FR-008); `Timestamp` siempre servidor UTC; `Data` sanitizado (sin `IsCorrect` previo, sin secretos); `Audit.Read` requerido para consulta; single-node (sin backplane distribuido); BuildingBlocks obligatorio, sin MediatR/MassTransit/AutoMapper

**Scale/Scope**: Transversal a SPEC 001–013 y futuros; catálogo cerrado 16 `Action` × 11 campos; búsqueda por 7 filtros combinables (GameId/PlayerId/Action/Resource/ResourceId/CorrelationId/Timestamp) + paginación; volumen de referencia 20 juegos × 50 eventos = 1000 registros

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Principle | Status | Evidence / Mitigación |
|------|-----------|--------|------------------------|
| I. Domain First | Reglas en dominio, auditoría no contiene lógica de negocio | ✅ PASS | `AuditRecord`/`AuditEntry` es solo registro; handlers de dominio no consultan auditoría para decidir (FR-008, SC-006). |
| II. Clean Architecture | `Web→Application→Domain←Infrastructure` | ✅ PASS | Domain define `AuditRecord`/`AuditAction` (Enumeration) sin conocer EF; Application define `IAuditWriter`/`AuditBehavior`; Api expone `GET /api/audit`; Infrastructure persiste `AuditEntries`. |
| III. BuildingBlocks No Reinvention | Reuso de plataforma | ✅ PASS | Reuso `Entity`/`AggregateRoot`, `Enumeration` (AuditAction), `Result`, `IRepository`/`Specification`, `IPipelineBehavior` (`AuditBehavior`), `IEndpoint`; sin MediatR/MassTransit/AutoMapper. |
| IV. Vertical Slice + CQRS | Slices autocontenidos | ✅ PASS | Auditoría es transversal vía `IPipelineBehavior`, no slice disperso; cada feature slice sigue bajo `Features/{Feature}/`; `GetAuditEntries` es slice autocontenido bajo `Features/Audit/`. |
| V. Authoritative Domain Engine | Server truth, auditoría no es fuente de verdad | ✅ PASS | `Timestamp` servidor UTC, `Actor` de `sub` claim, auditoría es observabilidad (best-effort) y nunca gate de negocio (FR-008, US2 escenario 4). |
| VI. OroIdentityServer | Identidad delegada | ✅ PASS | `Actor` siempre `sub` de JWT validado vía `Identity:Authority`; sin tabla local de usuarios. |
| A. Game Lifecycle | State machine | ✅ PASS | `GameCreated`/`GameConfigured`/`GameStarted`/`GameFinished` auditados pero la máquina de estados permanece en `Game` (no en audit). |
| D. Scoring via Ledger | Ledger vs audit | ✅ PASS | `PointsAwarded`/`PointsRemoved` auditados complementan `PointTransaction` ledger, no lo reemplazan (Assumptions). |
| E. Persistence | EF, append-only, sin fuga | ✅ PASS | `AuditEntry` con `IEntityTypeConfiguration` append-only, sin `Update`/`Delete`, índices por `GameId`/`PlayerId`/`Action`/`CorrelationId`/`Timestamp`; `DbContext` deriva de `AppDbContextBase`. |
| F. Concurrency & Idempotency | Idempotencia no depende de audit | ✅ PASS | Reintento de red genera segundo `AuditRecord` con mismo `CorrelationId` pero no se usa para decidir idempotencia (FR del edge case, SPEC-013 `IdempotencyKey` manda). |
| G. Real-Time/Outbox | Auditoría local, no RabbitMQ para estado | ✅ PASS | `AuditRecord` se persiste en misma transacción que agregados (o best-effort post-commit) sin Outbox/RabbitMQ para estado de juego. |
| H. Security Delegated | Audit.Read protege consulta | ✅ PASS | `GET /api/audit` requiere `Audit.Read` (y `Report.Read` para subconjuntos) per SPEC-013; sin permiso → 403 sin fuga. |
| I. Validation/Errors/Observability | Audit es observabilidad | ✅ PASS | Cada `AuditRecord` incluye `Result` (Succeeded/Failed/Denied) con `Data` sanitizado; `CorrelationId` propagado vía `X-Correlation-ID`/`Activity` de `ServiceDefaults`; sin secretos en `Data`. |

**Gate Result: PASS — no violations. No complexity-tracking entries required.**

## Project Structure

### Documentation (this feature)

```text
specs/014-audit-trail/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── audit-api.md           # GET /api/audit contrato extendido (16 Action, filtros)
│   └── audit-events.md        # Catálogo 16 Action + mapeo a comandos/handlers
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── BuildingBlocks/                                   # EXISTING — platform (no modificar)
├── OroQuizClash.Domain/                              # EXTEND — catálogo de acciones + extensión de AuditEntry
│   ├── Audit/
│   │   ├── AuditAction.cs                            # NEW — Enumeration 16 valores (GameCreated…AdministrativeAdjustment)
│   │   └── AuditEntry.cs                             # EXTEND — añadir ResourceId/GameId/PlayerId/Data (mapeo conceptual AuditRecord)
│   └── Games/… / Rewards/…                          # EXISTING — originan eventos, no dependen de Audit
├── OroQuizClash.Application/                         # EXTEND — behavior transversal + consulta audit
│   ├── Behaviors/
│   │   └── AuditBehavior.cs                          # EXTEND — mapear 16 Action a AuditRecord, capturar GameId/PlayerId/ResourceId/Data/CorrelationId
│   └── Features/Audit/
│       ├── GetAuditEntries.cs                        # EXTEND — filtros GameId/PlayerId/Action/Resource/ResourceId/CorrelationId/Timestamp + paginación
│       └── GetAuditEntryById.cs                      # EXISTING (SPEC-013) — se mantiene
├── OroQuizClash.Infrastructure/                      # EXTEND — configuración EF
│   └── Persistence/Configurations/
│       ├── AuditEntryTypeConfiguration.cs            # EXTEND — columnas nuevas + índices (GameId, PlayerId, Action, CorrelationId, Timestamp)
│       └── OroQuizClashDbContext.cs                  # EXISTING — DbSet<AuditEntries> ya existe
└── OroQuizClash.Api/                                 # EXISTING — endpoint GET /api/audit ya protegido por Audit.Read
tests/
├── OroQuizClash.Domain.Tests/                        # EXTEND — inmutabilidad, catálogo 16, no mutación
├── OroQuizClash.Application.Tests/                   # EXTEND — AuditBehavior mapea 16 Action, CorrelationId, Data sanitizada
├── OroQuizClash.Api.Tests/                           # EXTEND — búsqueda paginada por GameId/PlayerId/CorrelationId, 403 sin Audit.Read
└── OroQuizClash.Architecture.Tests/                  # EXTEND — append-only, transversalidad, Audit no referenciado por handlers de dominio
```

**Structure Decision**: Modular monolith existente (opción web-service). Sin nuevos proyectos: se extiende `AuditEntry` y `AuditBehavior` de SPEC-013 y se añade `AuditAction` enumeration. `OroQuizClash.Api` y `OroQuizClash.Infrastructure` solo se extienden con columnas/índices y consultas.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
