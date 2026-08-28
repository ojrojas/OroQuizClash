# Research: Admin Question Bank

**Branch**: `021-admin-question-bank` | **Date**: 2026-08-28 | **Plan**: [plan.md](plan.md)

Todos los NEEDS CLARIFICATION resueltos. El feature reutiliza agregados y slices de `003-question-bank` y `020-admin-categories` y patrón BFF/OIDC/Design System de 017–020; esta fase cierra las incógnitas propias de 021.

---

## R1. Invariante 4 respuestas + 1 correcta (Constitución B)

**Decision**: Cada `Question` debe tener exactamente **4 `AnswerOption`** (A–D) y exactamente **1 `IsCorrect`**. Implementación:

- **Dominio** (`OroQuizClash.Domain/Questions/Question.cs`, `AnswerOption.cs`): `IBusinessRule` `QuestionMustHaveFourOptions` y `QuestionMustHaveOneCorrectAnswer` + `CHECK` en SQL Server (`CHECK (CorrectCount =1)`) y `ROWVERSION`. `Question.Update` preserva invariante.
- **Aplicación**: `CreateQuestionValidator`/`UpdateQuestionValidator` (FluentValidation) valida `Options.Count==4`, `Count(IsCorrect)==1`, `Text 1–200` por opción, `Text 10–500` para pregunta, antes de tocar dominio.
- **API**: `400 InvalidQuestionData` con `errors.options` si viola.
- **UI**: `QuestionForm.razor` con 4 inputs `Answer A–D` (cada uno con checkbox/radio `Correct`) + validación en `QuestionForm.Validate()` (misma regla) + `aria-live` por campo. No se permite guardar con 3 o 2 correctas.

**Rationale**: Constitución B exige exactamente 4 opciones/1 correcta y pertenece a categoría activa; reusar invariantes de 003 evita duplicar lógica y garantiza que `Active` sea realmente jugable. Base de datos impone restricción además de dominio.

**Alternatives considered**:
- Crear nueva entidad `QuestionBank` separada: rechazado — duplica invariantes y desincroniza con `Category.ValidQuestionCount`.
- Permitir 2–6 opciones y filtrar en UI: rechazado — viola B y deja `ValidQuestionCount` ambiguo.

---

## R2. Modelo de 9 campos — reutilización de `QuestionForm` existente

**Decision**: Reutilizar `QuizArena.Admin.Client/Models/ContentModels.cs: QuestionForm` (ya usado en `Pages/QuestionBank.razor` de 017) y extenderlo con campos faltantes para cubrir los 9 del spec + 4 respuestas:

| Campo spec | Propiedad en `QuestionForm` | Detalle |
|------------|-----------------------------|---------|
| Texto pregunta | `Text` | 10–500 |
| Asociar a categoría | `CategoryId` | FK `Category` no archivada |
| Dificultad | `Difficulty` | 1–5 |
| Nivel académico | `AcademicLevel` | 2–100 |
| Rango de edad | `AgeMin`/`AgeMax` | 0–120, `AgeMin ≤ AgeMax` |
| 4 respuestas | `Options` (4 `OptionForm`) | cada una `Text 1–200` |
| Respuesta correcta | `Options[i].IsCorrect` (1 de 4) | invariante 4/1 |
| Explicación | `Explanation` | 0–1000 |
| Tiempo | *nuevo* `TimePerQuestion` | 5–300s (si el backend no lo tiene por pregunta, se persiste como `TimeLimit` por pregunta y se usa como default por juego) |
| Estado | `Status` (derivado) | `Draft/Active/Inactive/Archived` |

Campos nuevos (`TimePerQuestion`, `Explanation` ya existe) se añaden como propiedades opcionales con validación en `QuestionForm.Validate()` y se espejan en validador de aplicación y en invariantes de dominio.

**Rationale**: 003 ya implementa `Question` como Aggregate con `QuestionForm` validado; crear un segundo modelo duplicaría invariante 4/1. Extender el existente cubre los 9 campos + 4 respuestas con cambios mínimos y preserva `rowversion`.

**Alternatives considered**:
- Crear nuevo DTO `AdminQuestion` separado: rechazado — sincronización frágil.
- Mantener 003 sin cambios y mapear tiempo/explicación en UI: rechazado — deja campos huérfanos sin validación de dominio.

---

## R3. Ciclo de vida `Draft ↔ Active ↔ Inactive → Archived/Deleted` y `QuestionInUse`

**Decision**: 4 estados administrativos son vista directa del dominio `Question` (003):

- `Draft → Active` (`ActivateQuestion`) requiere invariante 4/1 y categoría no archivada.
- `Active → Inactive` (`DeactivateQuestion`) y `Inactive → Active` sin guarda adicional; `Inactive` no cuenta para `ValidQuestionCount`.
- `Draft/Inactive → Deleted` (`DeleteQuestion` o `ArchiveQuestion`) terminal; guarda `QuestionInUse` si existe `Game` en `Running`/`Finished` que referencia la pregunta (via `GameQuestion` o `QuestionId` en `GameRound`) → `409 QuestionInUse` con lista de juegos bloqueantes.
- `Active` no es terminal; `Archived/Deleted` es terminal y no elegible.

`RowVersion` protege transiciones y edición simultánea (SC-008).

**Rationale**: Reusar estados de 003 evita duplicar máquina y garantiza que `Active` sea realmente elegible para selección de preguntas (Constitución B).

**Alternatives considered**:
- Crear nueva máquina de 4 estados paralela en Admin: rechazado — duplica invariantes.
- Permitir `Active` sin 4/1: rechazado — viola B.

---

## R4. Asociación a categoría y `ValidQuestionCount` con `CategoryMinQuestions` configurable

**Decision**:
- **Asociación**: `CategoryId` FK a `Category` en estado no archivado (`Draft/Active/Inactive`). Si `Archived`, crear/editar pregunta rechazado con `CategoryNotFound`/`CategoryNotReady`.
- **`ValidQuestionCount`**: derivado server-side (`COUNT(*) WHERE CategoryId=@id AND Status='Active' AND CorrectCount=1 AND OptionCount=4`). Expuesto como `GET /api/categories/{id}` campo `validQuestionCount` y como `GET /api/questions/stats?categoryId=...`.
- **`CategoryMinQuestions`**: parámetro del sistema con valor inicial 5 (enunciado). Expuesto como `GET /api/categories/min-questions` o `GET /api/system/config` con `categoryMinQuestions: 5`. Admin UI muestra `ValidQuestionCount / CategoryMinQuestions` y deshabilita "Publicar" si <5. Si el sistema permite configurar el mínimo (si no, se documenta como 5 fijo), cambiarlo a 3 hace que categorías con 3 ya sean publicables (SC-010).
- **Coherencia**: al activar/desactivar/eliminar una pregunta, el `ValidQuestionCount` de su categoría se actualiza transaccionalmente y la elegibilidad de la categoría se re-evalúa (si baja de 5, la categoría `Active` muestra advertencia "No elegible").

**Rationale**: El spec dice "Una categoría no podrá ser publicada ... si no cumple el mínimo configurable ... inicial 5" — se modela como parámetro del sistema, no como campo por categoría, para permitir configurabilidad sin migración.

**Alternatives considered**:
- Hardcodear 5 en UI: rechazado — no permitiría configurabilidad futura.
- Guardar mínimo por categoría: rechazado — añade complejidad sin beneficio (el spec dice "mínimo configurable" del sistema, no por categoría).

---

## R5. Estadísticas del banco (FR-008)

**Decision**: Agregados server-side via `GET /api/questions/stats`:

```json
{
  "total": 124,
  "byCategory": [{ "categoryId":"...", "categoryName":"Matemáticas", "count": 24 }],
  "byDifficulty": [{ "difficulty":1, "count":12 }, ...],
  "byStatus": [{ "status":"Active", "count": 80 }, ...],
  "avgTimePerQuestion": 28.5,
  "validQuestionCountPerCategory": [{ "categoryId":"...", "valid":5, "required":5 }]
}
```

Si el endpoint no existe, fallback es agregación en `ServerQuestionsService` via `Task.WhenAll` sobre `GET /api/questions?categoryId=&difficulty=&status=&pageSize=1` (solo conteos, sin cargar todo). UI `QuestionStatsPanel.razor` muestra tarjetas por categoría/dificultad/estado y tiempo promedio, actualizadas sin recarga completa.

**Rationale**: El banco puede tener ≥100 preguntas; cargar todo para estadísticas violaría SC-009.

**Alternatives considered**:
- Cargar todas las preguntas y agregar en cliente: rechazado — no escala.
- Endpoint nuevo obligatorio antes de entregar UI: rechazado — fallback con conteos preserva el contrato.

---

## R6. BFF, auditoría y paginación

**Decision**:
- **BFF**: `ClientQuestionsService` → `HttpClient.BaseAddress = HostEnvironment.BaseAddress` → rutas `/bff/questions*` (cookie viaja); `ServerQuestionsService` → `http://oroclash-api/api/questions*` con `Bearer` del `HttpContext`. Forwarder catch-all `/bff/{**catch-all}` → `/api/{**catch-all}` ya existe (017) y cubre `POST /bff/questions`, `PUT /bff/questions/{id}`, `POST /bff/questions/{id}/activate|deactivate`, `DELETE /bff/questions/{id}`, `GET /bff/questions?categoryId=&difficulty=&status=&search=&page=&pageSize=`, `GET /bff/questions/stats`.
- **Auditoría**: append-only via Outbox (`QuestionAuditEntry`) en `SaveChanges` (Constitución I). Cada creación/edición/cambio de estado/eliminación persiste `QuestionId/CategoryId/ActorId/Timestamp/ChangedFields/CorrelationId`.
- **Listado**: `QuestionsList.razor` consume `GET /bff/questions` paginado (`PagedResult<QuestionSummary>`), filtros `category`, `difficulty 1–5`, `status`, `search` (texto), con paginación y skeleton.

**Rationale**: Reutiliza patrón BFF de 017 (sample `BlazorWebAppOidcBffAutoYarpAspire`), evita exponer `access_token` y preserva `CorrelationId` (FR-015).

**Alternatives considered**:
- Llamar WASM → API directo: rechazado — expone JWT.
- Paginación en cliente: rechazado — no escala.

---

## Consolidated Decisions Summary

| # | Decisión | Fuente |
|---|----------|--------|
| 1 | Invariante 4/1 con 4 `AnswerOption` + 1 `IsCorrect` + CHECK | FR-002, Constitución B |
| 2 | Extender `QuestionForm` existente con `TimePerQuestion` + 4 opciones | FR-001..004, 003 |
| 3 | 4 estados `Draft/Active/Inactive/Archived` + `QuestionInUse` | FR-006/007 |
| 4 | `ValidQuestionCount` + `CategoryMinQuestions` configurable (inicial 5) | FR-009/010, enunciado |
| 5 | Estadísticas server-side `GET /api/questions/stats` con fallback | FR-008, SC-009 |
| 6 | BFF catch-all + auditoría Outbox + listado paginado | FR-014..019 |

Sin NEEDS CLARIFICATION pendientes. Listo para Phase 1.
