# Implementation Plan: Game Security

**Branch**: `013-game-security` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/013-game-security/spec.md`

## Summary

Endurecimiento transversal de seguridad para toda la plataforma: centralizar autorización RBAC (4 roles × 14 permisos con matriz FR-003, deny-by-default y alcance por recurso), hacer cumplir servidor como única autoridad (ignorar Score/Correctness/Time/PlayerId/GameState del cliente), y proteger operaciones sensibles con validación de 3 niveles, idempotencia/anti-replay por ventana, rate limiting particionado por jugador/juego/IP y audit trail append-only correlacionado. Se apoya en OroIdentityServer como única fuente de identidad (Constitución VI), sin nuevo IdP, reutilizando BuildingBlocks ValidationBehavior/LoggingBehavior y extendiendo infraestructura existente (políticas ASP.NET Core, RateLimiter, EF Core interceptor de auditoría). Single-node inicial, sin backplane distribuido.

## Technical Context

**Language/Version**: C# 12 / .NET 10.0 (`net10.0`, `LangVersion latest`, `Nullable enable`, `ImplicitUsings enable`)

**Primary Dependencies**: `BuildingBlocks.Kernel.Domain` (Enumeration, IBusinessRule, Result/Error, IRepository/Specification), `BuildingBlocks.CQRS` (ICommand/IQuery, ISender, IPipelineBehavior — ValidationBehavior/LoggingBehavior), `BuildingBlocks.Kernel.Infrastructure` (AppDbContextBase, EfRepository, UnitOfWork), `BuildingBlocks.ServiceDefaults` (IEndpoint, GlobalExceptionHandler, OTel/CorrelationId), `Microsoft.AspNetCore.Authentication.JwtBearer` (JWT de OroIdentityServer, `jwks_uri`), `Microsoft.AspNetCore.Authorization` (policies), `Microsoft.AspNetCore.RateLimiting` (partitioned limiter), `FluentValidation` (vía ValidationBehavior existente), `OroIdentityServer` Podman (claims `sub`/`role`/`roles`/`tenant_id`)

**Storage**: SQL Server primario (Aspire) / SQLite local (`oroclash.db`, `EnsureCreatedAsync` sin migraciones); EF Core 10; entidad nueva `AuditEntry` (append-only, sin updates/deletes); índice único idempotencia ya existente `(GameId, PlayerId, RoundId)` para respuestas y `(PlayerId, IdempotencyKey)` para `RewardRedemption`; nuevo almacenamiento de ventana anti-replay/idempotencia genérica (tabla `IdempotencyRecord` o caché en memoria para single-node)

**Testing**: xUnit v3 + NSubstitute + coverlet + `Microsoft.AspNetCore.Mvc.Testing` WebApplicationFactory; Domain tests para anti-tampering y matriz de permisos; Application tests para políticas e idempotencia; Api integration tests para autorización (401/403) y rate limiting; Architecture tests para deny-by-default y aislamiento de capas

**Target Platform**: Linux containers (Podman), .NET Aspire AppHost, ASP.NET Core minimal APIs (IEndpoint) + SignalR hub (`/hubs/game` ya existente)

**Project Type**: web-service — modular monolith transversal (Domain / Application / Infrastructure / Api) — cross-cutting

**Performance Goals**: SC-005 — 50 envíos idénticos en 1s → 1 efecto persistido; SC-009 — ráfaga en un juego no degrada otros (>95% throughput inocente); validación/autorización p95 <50 ms; audit write no bloquea camino crítico (>99% operaciones <200 ms adicionales)

**Constraints**: Constitución V/VI/H (OroIdentityServer única autoridad, no local user store, no trust en Score/Correctness/Time/PlayerId/GameState cliente); deny-by-default; validación 3 niveles (API/Application/Domain); sin duplicar BuildingBlocks; single-node rate limiting (sin Redis backplane); audit append-only inmutable; no log de secretos

**Scale/Scope**: Transversal a SPEC 001–012 y futuros; matriz 14 permisos × 4 roles; operaciones sensibles: CreateGame/StartGame/StartRound/SubmitAnswer/WithdrawPlayer/RedeemReward/PublishCategory|Question + lecturas protegidas Report.Read/Audit.Read; audit retenida vida del juego+

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Principle | Status | Evidence / Mitigación |
|------|-----------|--------|------------------------|
| I. Domain First | Reglas en dominio, no en controllers | ✅ PASS | Anti-tampering (evaluación de respuesta, máquina de estados, scoring ledger) permanece en `Game`/`Answer`/`PointTransaction`; policies no contienen reglas de negocio, solo gating. |
| II. Clean Architecture | `Web→Application→Domain←Infra` | ✅ PASS | Domain expone `Authorization` como conceptos de política centralizados, no conoce ASP.NET Core; Application define `IAuthorizationPolicy`/`IAuditWriter` ports; Api implementa políticas JWT + RateLimiter; Infrastructure persiste `AuditEntry`/`IdempotencyRecord`. |
| III. BuildingBlocks No Reinvention | Reuso de plataforma | ✅ PASS | Reuso `Entity`, `Enumeration`, `Result/Error`, `IBusinessRule`, `IRepository/Specification`, `ICommand/IQuery/ISender`, `ValidationBehavior/LoggingBehavior`, `IEndpoint`, `GlobalExceptionHandler`; no MediatR/MassTransit/AutoMapper. |
| IV. Vertical Slice + CQRS | Slices autocontenidos | ✅ PASS | Cada slice mantiene `Command/Query+Validator+Handler+Endpoint` bajo `Features/{Feature}/`; autorización vía `IPipelineBehavior<AuthorizationBehavior>` o atributo de política centralizado, no carpeta genérica. |
| V. Authoritative Domain Engine | Server truth + delegación identidad | ✅ PASS | Mandato directo del spec: FR-006/FR-007/FR-008 ignoran Score/Correctness/Time/PlayerId/GameState cliente; `sub` claim es única fuente de PlayerId; autenticación delegada a OroIdentityServer per VI. |
| VI. OroIdentityServer | Podman OIDC única autoridad | ✅ PASS | JWT validado vía `Identity:Authority`/`jwks_uri`; sin tabla local de usuarios/credenciales, sin reimplementar login; roles via `role`/`roles` claims. |
| A. Game Lifecycle | State machine protegida | ✅ PASS | Transiciones validadas en dominio; GameState cliente ignorado (FR-008). |
| D. Scoring via Ledger | Puntos via ledger | ✅ PASS | Score siempre derivado de `PointTransaction` ledger; Correctness de `AnswerOption.IsCorrect` almacenada, no de cliente. |
| E. Persistence | Integridad sin fuga | ✅ PASS | `AuditEntry` append-only con `ValueGenerated` y sin updates/deletes; índices únicos de idempotencia ya existentes se preservan; `DbContext` deriva de `AppDbContextBase` con Outbox. |
| F. Concurrency & Idempotency | Optimistic + idempotencia | ✅ PASS | Idempotencia por `(GameId,PlayerId,RoundId)` y `IdempotencyKey` + ventana anti-replay; rate limiting particionado no rompe concurrencia optimista (`RowVersion`). |
| G. Real-Time/Outbox | No fuente de verdad | ✅ PASS | Audit trail se escribe en misma transacción que agregados (o vía interceptor post-SaveChanges) sin usar RabbitMQ para estado de juego; SignalR no se usa para seguridad. |
| H. Security Delegated | JWT + policies | ✅ PASS | JWT bearer obligatorio, policies mapean 14 permisos del spec a roles OroIdentityServer; `RequireAuthorization()` en todos los endpoints salvo `health`/`alive`. |
| I. Validation/Errors/Observability | 3 niveles + audit | ✅ PASS | Validación API/Application/Domain se mantiene; errores 401/403/429/400 mapeados a ProblemDetails sin fuga; OTel/CorrelationId propagado y audit incluye `CorrelationId`. |

**Gate Result: PASS — no violations. No complexity-tracking entries required.**

## Project Structure

### Documentation (this feature)

```text
specs/013-game-security/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── security-policies.md   # Matriz roles×permisos + políticas centralizadas
│   ├── audit-api.md           # Contrato lectura AuditEntry (Report.Read/Audit.Read)
│   └── rate-limiting.md       # Configuración y headers (Retry-After, X-RateLimit-*)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── BuildingBlocks/                                   # EXISTING — platform (no modificar)
├── OroQuizClash.Domain/                              # EXTEND — conceptos de autorización/auditoría sin lógica infra
│   ├── Games/Errors/GameErrors.cs                    # EXTEND — reutiliza PlayerIdentityMismatch, InvalidGameState, etc.
│   ├── Authorization/
│   │   ├── Permission.cs                             # NEW — Enumeration de 14 permisos (Category.Read...Audit.Read)
│   │   └── Role.cs                                   # NEW — Enumeration de 4 roles (ADMIN/GAME_MANAGER/PLAYER/REWARD_MANAGER)
│   └── Audit/
│       └── AuditEntry.cs                             # NEW — Entity append-only (actor, action, resource, timestamp, result, reason, correlationId)
├── OroQuizClash.Application/                         # EXTEND — autorización, validación, idempotencia, auditoría
│   ├── Behaviors/
│   │   ├── AuthorizationBehavior.cs                  # NEW — IPipelineBehavior que evalúa Permission requerida por comando
│   │   ├── AuditBehavior.cs                          # NEW — IPipelineBehavior que escribe AuditEntry post-handler
│   │   └── RateLimitingBehavior.cs                   # NEW (opcional) — o delega a middleware Api
│   └── Features/
│       ├── Games/SubmitAnswer.cs                     # EXTEND — ignora Score/Correctness/Time/PlayerId cliente (ya lo hace), asegura sub claim
│       └── Audit/
│           └── GetAuditEntries.cs                    # NEW — Query + Handler + Endpoint para Audit.Read (paginado, filtros)
├── OroQuizClash.Infrastructure/                      # EXTEND — persistencia audit/idempotencia
│   ├── Persistence/
│   │   ├── Configurations/AuditEntryTypeConfiguration.cs  # NEW
│   │   └── OroQuizClashDbContext.cs                  # EXTEND — DbSet<AuditEntry>, IdempotencyRecord si genérico
│   └── Services/
│       └── IdempotencyService.cs                     # NEW — ventana anti-replay + store in-memory/EF para single-node
└── OroQuizClash.Api/                                 # EXTEND — políticas, rate limiting, middleware
    ├── Authorization/
    │   └── SecurityPolicies.cs                       # NEW — 14 policies mapeadas (RequireRole+Permission), registro en AddAuthorization
    └── Middleware/
        └── CorrelationIdMiddleware.cs                # EXISTING via ServiceDefaults — se propaga a audit
tests/
├── OroQuizClash.Domain.Tests/                        # EXTEND — matriz permisos, anti-tampering
├── OroQuizClash.Application.Tests/                   # EXTEND — AuthorizationBehavior, AuditBehavior, idempotencia
├── OroQuizClash.Api.Tests/                           # EXTEND — 401/403/429, rate limiting particionado, audit read auth
└── OroQuizClash.Architecture.Tests/                  # EXTEND — deny-by-default, audit append-only, RateLimiting no bloquea otros juegos
```

**Structure Decision**: Modular monolith existente (opción web-service). Sin nuevos proyectos: se extienden Domain (Permission/Role/AuditEntry), Application (Behaviors de autorización/auditoría + feature Audit), Infrastructure (config EF de AuditEntry + IdempotencyService), Api (políticas y RateLimiting). Reutiliza `BuildingBlocks.ServiceDefaults` para OTel/CorrelationId.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
