# Quickstart: Admin Categories — Validation Guide

**Branch**: `020-admin-categories` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)
**Data Model**: [data-model.md](data-model.md) | **Contracts**: [contracts/categories-bff.md](contracts/categories-bff.md), [contracts/category-states.md](contracts/category-states.md)

Guía runnable para validar creación/gestión + ciclo de vida 4 estados. Cada escenario es independiente y referencia contrato/modelo.

## Prerequisites

- `net10.0` SDK 10.0.400 (`global.json` 10.0.400)
- `OroQuizClash.AppHost` corriendo: `dotnet run --project OroQuizClash.AppHost` (levanta `oroclash-api`, `identity-api` Postgres 5432, `quizarena-admin`)
- Cliente OIDC `quizarena-admin` registrado en OroIdentityServer (confidential, `authorization_code+refresh_token`, redirect `/signin-oidc`, scopes `openid profile email offline_access {ApiScope}`) — ver `017/contracts/oidc-config.md`
- Usuarios: `admin/Admin@123456` (ADMIN), GAME_MANAGER y REWARD_MANAGER (matriz `AdminNavigation`)
- 8 categorías de ejemplo opcionalmente seed via `POST /bff/categories` o AppHost seed; cada una con ≥5 preguntas válidas para publicar (4 opciones/1 correcta)
- Design tokens en `src/Admin/QuizArena.Admin/wwwroot/design-tokens.css` (gate `validate-tokens`)

## Setup

```bash
dotnet restore
dotnet build
dotnet run --project OroQuizClash.AppHost
# Esperar Aspire dashboard https://localhost:15888 → recursos healthy
# Admin URL → quizarena-admin (ver Aspire)
node design-system/validate-tokens.cjs --dir src/Admin --strict
```

## Validation Scenarios

### V1 — Crear y gestionar categorías base (US1, FR-001..004, FR-006)

**Referencia**: `spec.md US1`, `data-model.md Category`, `contracts/categories-bff.md`.

1. Login ADMIN → `/admin/categories` → "Crear categoría".
2. Completar 10 campos válidos: nombre 3–100, descripción ≤500, área 2–100 (ej. Matemáticas), nivel 2–100, AgeMin/AgeMax 0–120 con min≤max, dificultad 1–5, público objetivo 2–100, tags 0–10 (2–30), color `#RRGGBB` opcional, progresión `Linear` → Guardar.
3. Verificar `201 Created`, `status` `Draft`, `rowVersion` presente, y que la categoría aparece en listado.
4. Intentar nombre duplicado case-insensitive "matemáticas" con otra categoría no archivada → verificar `409 CategoryAlreadyExists` con `errors.name`.
5. Intentar AgeMin 20/AgeMax 10 o dificultad 0/6 → verificar `400 InvalidCategoryData` por campo sin pantalla en blanco.
6. Login REWARD_MANAGER → intentar `/admin/categories/new` → verificar `Access Denied` y `403` por API directa `POST /bff/categories`.

**Expected**: SC-001 <2m (90%), SC-004 <2s rechazo, SC-005 coherencia 100% al recargar detalle, SC-006 autorización 100%.

**API check**:
```bash
curl -k https://localhost:XXXX/bff/categories -H "Cookie: .AspNetCore.Cookies=..." | jq
curl -k -X POST https://localhost:XXXX/bff/categories -H "Content-Type: application/json" -H "Cookie: ..." -d '{"name":"Matemáticas","knowledgeArea":"Matemáticas","academicLevel":"Secundaria","ageMin":12,"ageMax":18,"difficulty":3,"targetAudience":"Estudiantes","progressionRule":"Linear"}' | jq
```

### V2 — Publicar y dar vida a la categoría (US2, FR-005/008)

**Referencia**: `contracts/category-states.md`.

1. Crear categoría en `Draft` sin preguntas → intentar `POST /bff/categories/{id}/publish` → verificar `400 CategoryNotReady` faltan 5.
2. Añadir 5 preguntas válidas (cada una 4 opciones/1 correcta, `POST /bff/questions` con `categoryId`) → ejecutar `Publish` → verificar `Active` y `validQuestionCount` 5, y que aparece como elegible en selector al crear juegos (`GET /bff/categories?status=Active` la incluye).
3. Ejecutar `Deactivate` (`Active → Inactive`) → verificar deja de ser elegible para nuevos juegos; luego `Activate` (`Inactive → Active`) → verificar vuelve a ser elegible.
4. Crear categoría con 8 ejemplos (Matemáticas, Historia, Ciencia, Tecnología, Geografía, Literatura, Programación, Finanzas) → verificar `GET /bff/categories?area=Ciencia` y `search=Matemáticas` filtran correctamente (<2s, skeleton).
5. Con juegos en `Running` que usan la categoría, intentar `Archive` → verificar `409 CategoryInUse` con lista de juegos bloqueantes; sin juegos activos, `Archive` → `Archived` terminal y solo visible con filtro `Archived`.

**Expected**: SC-002 100% publish con ≥5 y rechazo con <5, SC-003 `Active↔Inactive` y `Archived` terminal, SC-009 paginado <2s.

### V3 — Metadatos, público objetivo y reglas de progresión (US3, FR-003/004)

1. Editar categoría en `Draft` → definir `TargetAudience` "Profesionales Finanzas", tags ["álgebra","cálculo"] y color `#2563EB`, progresión `Adaptive` → Guardar con éxito.
2. Intentar tags 11 o tag de 1 char o duplicado case-insensitive "Álgebra"/"álgebra" → verificar `400 InvalidCategoryData` por campo.
3. Intentar progresión fuera de catálogo "Random" → verificar `400` con `errors.progressionRule`.
4. Llevar categoría a `Archived` → verificar formulario solo lectura y que `PUT /bff/categories/{id}` retorna `422 InvalidCategoryState`.

**Expected**: SC-003 inmutabilidad 100% tras `Archived`, SC-004 validación por campo <2s.

### V4 — Concurrencia, paginación y accesibilidad (SC-007..009)

1. Abrir misma categoría en `Draft` en dos pestañas ADMIN → editar distinto campo simultáneamente → verificar uno persiste y otro recibe `409 ConcurrencyConflict` con opción de recargar (SC-008).
2. Crear 50 categorías (seed script) → verificar `GET /bff/categories?page=1&pageSize=20` pagina con `totalCount` y filtros `status`/`area` <2s con skeleton (SC-009).
3. Viewport 375–1536 → verificar formulario y listado sin scroll horizontal, objetivos ≥44px, `aria-live` en errores, foco visible (SC-007).

## Automated Checks (CI)

```bash
dotnet test tests/OroQuizClash.Architecture.Tests -k "DesignSystemNoDirectDbTests or AdminBffTests"
dotnet test tests/QuizArena.Admin.Tests -k "CategoryTests or CategoryStateTransitionTests"
node design-system/validate-tokens.cjs --dir src/Admin --strict
# Axe a11y opcional sobre /admin/categories/new con auth mock
```

## Troubleshooting

- **CategoryNotReady**: verificar `GET /bff/categories/{id}` → `validQuestionCount` <5; crear preguntas con 4 opciones/1 correcta y `Status Active` hasta llegar a 5.
- **CategoryAlreadyExists**: nombre duplicado case-insensitive entre no archivadas → cambiar nombre o archivar la existente.
- **401 al guardar**: cookie expirada → re-autenticar; el formulario conserva borrador local (FR-009 edge case).
- **409 ConcurrencyConflict**: recargar detalle para nuevo `RowVersion` y reintentar.
- **422 InvalidCategoryState tras Archived**: esperado — categoría terminal inmutable; crear nueva categoría para nuevo ciclo.
- **CategoryInUse al archivar**: `GET /bff/games?categoryId={id}&status=Running` lista juegos bloqueantes; finalizar/cancelar esos juegos primero.
