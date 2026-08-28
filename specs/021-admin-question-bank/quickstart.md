# Quickstart: Admin Question Bank — Validation Guide

**Branch**: `021-admin-question-bank` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)
**Data Model**: [data-model.md](data-model.md) | **Contracts**: [contracts/question-bank-bff.md](contracts/question-bank-bff.md), [contracts/question-stats.md](contracts/question-stats.md)

Guía runnable para validar creación/gestión + ciclo de vida + estadísticas con `CategoryMinQuestions` configurable (inicial 5). Cada escenario es independiente y referencia contrato/modelo.

## Prerequisites

- `net10.0` SDK 10.0.400 (`global.json` 10.0.400)
- `OroQuizClash.AppHost` corriendo: `dotnet run --project OroQuizClash.AppHost` (levanta `oroclash-api`, `identity-api` Postgres 5432, `quizarena-admin`)
- Cliente OIDC `quizarena-admin` registrado en OroIdentityServer (confidential, `authorization_code+refresh_token`, redirect `/signin-oidc`, scopes `openid profile email offline_access {ApiScope}`) — ver `017/contracts/oidc-config.md`
- Usuarios: `admin/Admin@123456` (ADMIN), GAME_MANAGER y REWARD_MANAGER (matriz `AdminNavigation`)
- Categoría `Active` con ≥5 preguntas válidas para publicar — crear 8 categorías de ejemplo (Matemáticas, Historia, etc.) vía `020-admin-categories` y poblar preguntas; cada pregunta con 4 opciones/1 correcta (Constitución B)
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

### V1 — Crear y gestionar el núcleo de preguntas (US1, FR-001..005)

**Referencia**: `spec.md US1`, `data-model.md Question`, `contracts/question-bank-bff.md`.

1. Login ADMIN → `/admin/questions` → "Crear pregunta".
2. Completar 9 campos válidos: texto 10–500, categoría `Active` existente, dificultad 1–5, nivel 2–100, AgeMin/AgeMax 0–120 con min≤max, 4 opciones `Answer A–D` (texto 1–200 cada una) con exactamente 1 marcada correcta, explicación 0–1000 opcional, tiempo 5–300 → Guardar.
3. Verificar `201 Created`, `status` `Draft`/`Active`, `options` 4 con 1 `isCorrect`, y que la pregunta aparece en listado con 4 respuestas y 1 correcta.
4. Intentar con solo 3 opciones o 2 correctas o texto de opción vacío → verificar `400 InvalidQuestionData` con `errors.options` y que no se crea.
5. Intentar con categoría inexistente o inactiva/archivada → verificar `400 CategoryNotFound`/`CategoryNotReady` con `errors.categoryId`.
6. Editar pregunta existente → cambiar texto y respuesta correcta de B a D → Guardar → verificar detalle muestra 4 opciones con nueva correcta.
7. Login REWARD_MANAGER → intentar `/admin/questions/new` → verificar `Access Denied` y `403` por API directa `POST /bff/questions`.

**Expected**: SC-001 <3m (90%), SC-002 100% 4/1 invariante, SC-004 <2s rechazo por campo, SC-005 coherencia 100% al recargar detalle, SC-006 autorización 100%.

**API check**:
```bash
curl -k https://localhost:XXXX/bff/questions -H "Cookie: .AspNetCore.Cookies=..." | jq
curl -k -X POST https://localhost:XXXX/bff/questions -H "Content-Type: application/json" -H "Cookie: ..." -d '{"text":"¿Capital de Francia con texto largo?","categoryId":"...","difficulty":3,"academicLevel":"Secundaria","ageMin":12,"ageMax":18,"timePerQuestion":30,"options":[{"text":"Londres","isCorrect":false},{"text":"París","isCorrect":true},{"text":"Berlín","isCorrect":false},{"text":"Roma","isCorrect":false}]}' | jq
```

### V2 — Operar el ciclo de vida y consultar el banco (US2, FR-006..008)

**Referencia**: `contracts/question-bank-bff.md`, `contracts/question-stats.md`.

1. Tomar pregunta en `Active` → "Desactivar" (`POST /bff/questions/{id}/deactivate`) → verificar `Inactive` y que `ValidQuestionCount` de su categoría baja en 1 y deja de contar para selección en juegos.
2. Reactivar `Inactive → Active` (`POST /bff/questions/{id}/activate`) → verificar vuelve a contar.
3. Crear pregunta en `Draft` nunca usada en juego `Running`/`Finished` → "Eliminar" (`DELETE /bff/questions/{id}` con `If-Match`) → verificar desaparece del listado; intentar eliminar una pregunta en uso en juego activo → verificar `409 QuestionInUse` sin mutación.
4. Con 20 preguntas en 3 categorías y dificultades 1–5, abrir `/admin/questions/stats` → verificar `total` por categoría, por dificultad, por estado (`Active`/`Inactive`/`Draft`) y tiempo promedio, actualizados sin recarga completa.
5. Con categoría con 4 preguntas `Active`, intentar `POST /bff/categories/{id}/publish` → verificar `400 CategoryNotReady` faltan 1 y `validPerCategory` muestra `4/5` y botón "Publicar" deshabilitado.

**Expected**: SC-003 100% transiciones y borrado con guarda, SC-009 paginado <2s, SC-010 flujo 5 preguntas → publicar.

### V3 — Atributos avanzados y mínimo configurable (US3, FR-003/004/009)

1. Editar pregunta en `Draft` → definir `Dificultad 5`, `Nivel Universitario`, `Edad 18–25`, `Tiempo 45`, `Explicación` 200 chars → Guardar con éxito.
2. Intentar `Nivel` vacío o `AgeMin 20/AgeMax 10` o `Tiempo 0/301` o `Explicación` 1001 chars → verificar `400 InvalidQuestionData` por campo `AcademicLevel`/`AgeMax`/`TimePerQuestion`/`Explanation` en <2s.
3. Consultar `GET /bff/system/config` → verificar `categoryMinQuestions: 5` inicial; si el sistema expone configuración y se cambia a 3, verificar categoría con 3 ya muestra `3/3` y habilita publicar; al subir a 7, categoría con 5 muestra `5/7` y deshabilita.

**Expected**: SC-004 validación por campo <2s, SC-010 mínimo configurable sin romper existentes.

### V4 — Concurrencia, paginación y accesibilidad (SC-007..009)

1. Abrir misma pregunta en `Draft` en dos pestañas ADMIN → editar distinto campo simultáneamente → verificar uno persiste y otro recibe `409 ConcurrencyConflict` con opción de recargar (SC-008).
2. Crear 100 preguntas (seed script) → verificar `GET /bff/questions?categoryId=&difficulty=&status=&page=1&pageSize=20` pagina con `totalCount` y filtros <2s con skeleton (SC-009).
3. Viewport 375–1536 → verificar formulario y listado sin scroll horizontal, objetivos ≥44px, `aria-live` en errores, foco visible (SC-007).

## Automated Checks (CI)

```bash
dotnet test tests/OroQuizClash.Architecture.Tests -k "DesignSystemNoDirectDbTests or AdminBffTests"
dotnet test tests/QuizArena.Admin.Tests -k "QuestionTests or QuestionStateTransitionTests"
node design-system/validate-tokens.cjs --dir src/Admin --strict
# Axe a11y opcional sobre /admin/questions/new con auth mock
```

## Troubleshooting

- **CategoryNotReady**: verificar `GET /bff/categories/{id}` → `validQuestionCount` <5; crear preguntas con 4 opciones/1 correcta y `Status Active` hasta llegar a 5.
- **QuestionInUse al eliminar/editar**: `GET /bff/games?questionId={id}&status=Running` lista juegos bloqueantes; la edición puede requerir clonar la pregunta.
- **401 al guardar**: cookie expirada → re-autenticar; el formulario conserva borrador local (FR-012 edge case).
- **409 ConcurrencyConflict**: recargar detalle para nuevo `RowVersion` y reintentar.
- **InvalidQuestionData con 4/1**: verificar exactamente 4 opciones, cada texto 1–200, y exactamente 1 `isCorrect` true.
