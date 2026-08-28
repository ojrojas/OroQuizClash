# Quickstart: Validación QuizArena Administration Application

**Branch**: `017-admin-application` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)
**Contratos**: [bff-endpoints.md](contracts/bff-endpoints.md) | [service-interfaces.md](contracts/service-interfaces.md) | [oidc-config.md](contracts/oidc-config.md) | [realtime.md](contracts/realtime.md) | **Data model**: [data-model.md](data-model.md)

Guía de validación end-to-end. La implementación vive en `src/Admin/QuizArena.Admin[.Client]` + `OroQuizClash.AppHost`; los detalles de implementación pertenecen a `tasks.md`.

## Prerequisites

- .NET SDK 10.0.x (`global.json` 10.0.400, rollForward latestFeature) — **net10.0 único**
- Aspire CLI + Podman/Docker (infraestructura del AppHost: sqlserver, postgres, redis, rabbitmq)
- Imagen `localhost/oroidentityserver:latest` construida (ver comentarios de `OroQuizClash.AppHost/AppHost.cs`)
- Cliente OIDC `quizarena-admin` registrado en OroIdentityServer (contrato [oidc-config.md](contracts/oidc-config.md) §1)
- Secretos de entorno: `symmetric_security_key`, `seed_admin_password`, client_secret del admin

## Scenario 0 — Creación del proyecto (mandato del usuario)

```bash
dotnet new blazor -f net10.0 -ai true -int Auto -o src/Admin/QuizArena.Admin
dotnet sln OroQuizClash.slnx add src/Admin/QuizArena.Admin src/Admin/QuizArena.Admin.Client
```

**Expected**: dos proyectos net10.0 (`QuizArena.Admin` + `QuizArena.Admin.Client`); el AppHost puede referenciar `Projects.QuizArena_Admin`; `dotnet build` compila.

## Scenario 1 — Arranque del grafo Aspire con Admin

```bash
aspire start   # o: dotnet run --project OroQuizClash.AppHost
```

**Expected**: dashboard Aspire muestra `quizarena-admin` junto a `oroclash-api` e `identity-api`; `/health` del admin responde; `quizarena-admin` espera a `oroclash-api` e `identity-api` (WaitFor).

## Scenario 2 — Login OIDC y navegación (US1)

1. Abrir la URL del admin → redirección a OroIdentityServer `/connect/authorize`.
2. Login con cuenta seed `admin` (rol ADMIN) → retorno al Dashboard.
3. Recorrer las 10 secciones del NavMenu.

**Expected**: sesión vía cookie (sin token visible en el navegador); 10 secciones navegables en ≤2 interacciones (SC-002); con cuenta GAME_MANAGER/REWARD_MANAGER el NavMenu muestra solo sus secciones; logout redirige al proveedor.

## Scenario 3 — BFF en acción (FR-030, SC-003)

1. DevTools → Network: las llamadas de datos son a `/bff/*` del propio origen; **ninguna** llamada directa al API ni a la DB.
2. Verificar header `Authorization: Bearer …` **solo server-side** (logs/OTel), nunca en el navegador.
3. Ejecutar:

```bash
dotnet test tests/OroQuizClash.Architecture.Tests --filter "NoDirectDb"
```

**Expected**: 0 referencias EF Core/DbContext/ADO.NET en `src/Admin/**`; ProblemDetails del API se muestran como errores accionables (p. ej., publicar categoría sin gate → mensaje con faltantes).

## Scenario 4 — Flujo operativo crítico (US2/US3, SC-001/SC-004)

1. Crear categoría + ≥5 preguntas válidas → publicar categoría (gate OK).
2. Crear juego con los campos de configuración completos → iniciar → verificar estado.
3. Intentar editar el juego activo → campos bloqueados con explicación.

**Expected**: creación de juego <3 min; validación inline por campo; transiciones correctas; confirmación explícita en acciones destructivas (SC-011).

## Scenario 5 — Live Games realtime (US4, SC-005)

1. Con un juego activo, abrir Live Games.
2. Provocar transiciones (start round, finish) desde otra sesión o API.

**Expected**: filas actualizan en <5s sin recarga manual (eventos vía `/hubs/game` reenviado); al cortar/red restaurar la red aparece `Reconnecting` y se resincroniza vía REST; sin datos privados de jugadores (solo agregados).

## Scenario 6 — Recompensas y redenciones (US5)

1. Crear/activar recompensa; procesar una redención pendiente (aprobar → entregar) y otra (rechazar).

**Expected**: estados actualizados y registrados en Audit; redención rechazada no re-procesable; sección invisible/denegada para GAME_MANAGER.

## Scenario 7 — Dashboard, Reports, Audit (US6)

1. Dashboard con KPIs reales; estados Empty/Error manejados.
2. Generar cada tipo de reporte; filtrar Audit por actor/acción/fecha.

**Expected**: reportes coherentes con el estado del sistema; Audit estrictamente solo-lectura (sin acciones de mutación).

## Scenario 8 — Design System, responsive y accesibilidad (SPEC-016)

```bash
node .opencode/skills/design-system/scripts/validate-tokens.cjs --dir src/Admin
```

**Expected**: 0 literales hex fuera de tokens; `<html data-theme="administration">`; usable en 1024/1440 sin scroll horizontal 375–1536; contraste AA + navegación por teclado + foco visible en el tema claro; estados Loading/Ready/Empty/Error en toda pantalla interactiva.

## Scenario 9 — Sesión y edge cases

1. Expirar sesión (esperar o revocar) → renovación silenciosa vía refresh_token o re-login limpio.
2. Usuario con `must_change_password` → redirección al flujo de cambio antes de operar.
3. API caído → estados de error con reintento en cada sección (sin pantallas en blanco).

**Expected**: FR-004/005 y edge cases del spec cubiertos; sin fuga de detalles internos (FR-031).

## Definition of Done (este feature)

- Scenarios 0–9 PASS
- `dotnet test` verde (arquitectura + unitarios nuevos)
- `validate-tokens --dir src/Admin` PASS
- Sin [NEEDS CLARIFICATION] pendientes; constitución re-verificada en [plan.md](plan.md)
