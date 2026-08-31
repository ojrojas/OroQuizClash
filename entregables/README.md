# Entregables — OroQuizClash / QuizArena

> **Proyecto:** OroQuizClash — QuizArena (Modular Monolith `net10.0` + OroIdentityServer Podman + QuizArena.Admin Blazor + QuizArena.Player Angular 22)  
> **Versión:** Constitución v1.1.0 — Specs 001–036 `Ready for Review`  
> **Fecha:** 31 de agosto de 2026  
> **Carpeta:** `entregables/` (5 documentos + scripts DDL)

| # | Documento | Archivo | Descripción corta |
|---|-----------|---------|-------------------|
| 1 | **Casos de Uso** | `01-Casos-de-Uso.md` | 16 casos de dominio (001-016) + Admin 017-026 + Player 027-036, actores, flujos principales/alternos, APIs, máquinas de estado, referencias por archivo. |
| 2 | **Entidad-Relación + Relaciones** | `02-Modelo-Entidad-Relacion.md` | ER mermaid `oroclash` (12 entidades + Outbox/Audit/Idempotency), tablas/columna/tipos/constraints/índices, cardinalidades EF Core `Field`, enumeraciones persistidas. |
| 3 | **Arquitectura de la Solución** | `03-Arquitectura-de-la-Solucion.md` | Constitución I-VI, capas `Domain/Application/Infrastructure/Api`, vertical slices CQRS, BuildingBlocks, frontend Player/Admin (incl. fixes 31-08 Rewards+Logout), orquestación Aspire `AppHost.cs`, seguridad/obs/testing. |
| 4 | **Base de Datos — Scripts y Semillas** | `04-Base-de-Datos-Scripts-y-Semillas.md` | DDL SQL Server normativo (CREATE TABLE + índices FK/UNIQUE), notas SQLite, scripts operativos (`dotnet ef migrations script`, limpieza volúmenes), semillas determinísticas `Seeder` 10 cats ×20 Q ×10 juegos, registro OIDC `quizarena-player`. |
| 5 | **Guía de Instalación** | `05-Guia-de-Instalacion.md` | Requisitos, happy path `aspire start` (15 min), instalación manual sin Aspire, configuración del sistema (7 pasos), tests/calidad, troubleshooting (tabla 16 filas incl. fixes 31-08), deployment `aspire publish`. |

## Correcciones incluidas (31-08-2026)

- **Recompensas — pantalla creación no dejaba crear (excepción):** `Domain/Rewards/Reward.cs:43` `description.Trim()` → ` (description ?? "").Trim()` (NullReference cuando `RewardForm.Description?` venía `null`); `Application/Features/Rewards/CreateReward.cs:66` compatibilidad V2 (fusiona `Cost→PointsRequired` + `AvailableTo→ExpirationDate`); `Admin/QuizArena.Admin.Client/Services/RewardsServiceCore.cs:136` deserialización robusta con fallback a legado para evitar excepción en UI.
- **Player — botón Cerrar sesión no hacía nada:** `Player/.../core/auth/auth.service.ts` `logout()` robusto (`logoffAndRevokeTokens` + fallback `logoffLocal` + `window.location.href=/auth/logout-callback` tras 400ms/1500ms); `LogoutCallbackComponent` asegura limpieza y `navigateByUrl('/')`. Requiere `postLogoutRedirectUris` registrado en `quizarena-player` en IdP (ver `05-Guia-de-Instalacion.md:2.6`).

## Validación

```bash
dotnet build OroQuizClash.slnx          # 0 errors
dotnet test                             # 864+ passed
ng build --project QuizArena.Player    # 109 kB transfer (Angular 22)
node .opencode/skills/design-system/scripts/validate-tokens.cjs --dir design-system/ # 0 literals
```

## Fuentes primarias

- Dominio: `src/OroQuizClash.Domain/Games/Game.cs:15`, `Rewards/Reward.cs:8`, `Categories/Category.cs:9`, `Questions/Question.cs:10`
- Infra: `src/OroQuizClash.Infrastructure/Persistence/OroQuizClashDbContext.cs:17`, `Persistence/Configurations/*TypeConfiguration.cs`
- App: `src/OroQuizClash.Application/Features/{Games,Rewards}/`
- AppHost: `OroQuizClash.AppHost/AppHost.cs:1-216`
- Player: `src/Player/QuizArena.Player/src/app/{app.config.ts,core/auth/auth.service.ts}`
- Admin: `src/Admin/QuizArena.Admin.Client/{Services/RewardsServiceCore.cs,Pages/Rewards/RewardCreate.razor}`
- Seeder: `src/Seeder/OroQuizClash.Seeder/{SeedData.cs,Worker.cs}`
- Docs SDD: `specs/001-036/{spec.md,plan.md}`, `.specify/memory/constitution.md`, `docs/adr/ADR-010..013`

*Para generar el DDL oficial: `dotnet ef migrations script` (ver `04-Base-de-Datos-Scripts-y-Semillas.md:3.1`). Para levantar la solución: `aspire start` (ver `05-Guia-de-Instalacion.md:2.4`).*
