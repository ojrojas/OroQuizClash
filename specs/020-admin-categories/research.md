# Research: Admin Categories

**Branch**: `020-admin-categories` | **Date**: 2026-08-28 | **Plan**: [plan.md](plan.md)

Todos los NEEDS CLARIFICATION resueltos. El feature reutiliza agregados y slices de `002-categories` y patrón BFF/OIDC/Design System de 017–019; esta fase cierra las incógnitas propias de 020.

---

## R1. Máquina de 4 estados y guarda `ValidQuestionCount ≥5` (Constitución B)

**Decision**: 4 estados administrativos son vista directa del dominio `Category` (002) — no se crea nueva máquina paralela: `Draft` (borrador), `Active` (publicada y elegible), `Inactive` (pausada), `Archived` (terminal). Guardas:

- `Draft → Active` (`PublishCategory`) requiere `ValidQuestionCount ≥5` (preguntas con 4 opciones/1 correcta y `Status==Active`). Si <5 → `400 CategoryNotReady` con `errors.categoryId` indicando faltantes.
- `Active ↔ Inactive` (`DeactivateCategory`/`ActivateCategory`) sin guarda adicional; deja de ser elegible para nuevos juegos/preguntas pero mantiene preguntas existentes.
- `Active/Inactive → Archived` (`ArchiveCategory`) terminal; guarda `CategoryInUse` si existe `Game` en `Running`/`Scheduled`/`Ready` que referencia la categoría → `409 CategoryInUse` con lista de juegos bloqueantes.
- `Archived` inmutable: todo `PUT` rechazado con `422 InvalidCategoryState`.

`ValidQuestionCount` se expone como `GET /api/categories/{id}` campo derivado (conteo server-side) y como columna en listado. `RowVersion` (`rowversion` SQL Server) protege transiciones y edición simultánea (SC-008).

**Rationale**: Constitución B exige ≥5 preguntas válidas antes de publicación y que la selección evite repetición; reusar invariantes de 002 evita duplicar lógica y garantiza que `Active` sea realmente jugable.

**Alternatives considered**:
- Crear nueva máquina de 4 estados paralela en Admin: rechazado — duplica invariantes y desincroniza con dominio.
- Permitir `Active` con <5 preguntas y filtrar en UI: rechazado — viola B y deja juegos no configurables (FR-001 fallaría en 019).

---

## R2. Modelo de 10 campos — reutilización de `CategoryForm` existente

**Decision**: Reutilizar `QuizArena.Admin.Client/Models/ContentModels.cs: CategoryForm` (ya usado en `Pages/Categories.razor` de 017) y extenderlo con campos faltantes para cubrir los 10 del spec:

| Campo spec | Propiedad en `CategoryForm` | Detalle |
|------------|-----------------------------|---------|
| Nombre | `Name` | 3–100, único case-insensitive entre no archivadas |
| Descripción | `Description` | 0–500 |
| Área de conocimiento | `KnowledgeArea` | 2–100, texto libre (Matemáticas, Historia… no catálogo cerrado) |
| Nivel académico | `AcademicLevel` | 2–100 |
| Rango de edad | `AgeMin`/`AgeMax` | 0–120, `AgeMin ≤ AgeMax` |
| Dificultad | `Difficulty` | 1–5 |
| Público objetivo | `TargetAudience` (nueva) | 2–100 |
| Estado | `Status` (derivado) | 4 estados |
| Metadatos | `Tags`/`Color`/`Icon` (parte de `CategoryForm`) | tags 0–10 (2–30), color `#RRGGBB`, icono Lucide |
| Reglas de progresión | `ProgressionRule` (nueva) | `Linear/Progressive/Adaptive/CategorySpecific` |

Campos nuevos (`TargetAudience`, `ProgressionRule`, `Color`/`Icon` si no existían) se añaden como propiedades opcionales con validación en `CategoryForm.Validate()` y se espejan en validador de aplicación (FluentValidation) y en invariantes de dominio (`Category.Update`).

**Rationale**: 002 ya implementa `Category` como Aggregate con `CategoryForm` validado; crear un segundo modelo duplicaría invariantes (unicidad, tags, edad). Extender el existente cubre los 10 campos con cambios mínimos y preserva `rowversion`.

**Alternatives considered**:
- Crear nuevo DTO `AdminCategory` separado: rechazado — sincronización frágil, doble validación.
- Mantener 002 sin cambios y mapear metadatos en UI: rechazado — deja campos huérfanos sin validación de dominio.

---

## R3. Metadatos (tags, color, icono) y Área libre

**Decision**:
- **Tags**: `0–10` tags, `2–30` chars cada uno, sin duplicados case-insensitive. Validación en `CategoryForm.Validate()` → `400 InvalidCategoryData` con `errors.tags`. Persistencia como JSON `MetadataJson` en dominio si el backend no tiene columnas dedicadas (fallback documentado en UI con tooltip).
- **Color**: hex opcional `^#([0-9A-Fa-f]{6})$` (6 dígitos). Si se especifica, se muestra como swatch en badge; si no, usa `color` por defecto del tema `administration`.
- **Icono**: Lucide icon name opcional (p. ej., `calculator`, `book`, `flask` para Matemáticas, Historia, Ciencia). Validación contra lista `iconography.md` (16 iconos del Design System); desconocido → advertencia, no bloqueo.
- **Área de conocimiento**: texto libre `2–100` (no catálogo cerrado). Los 8 ejemplos (Matemáticas, Historia, Ciencia, Tecnología, Geografía, Literatura, Programación, Finanzas) son datos de seed/demo, no enumeración. Filtrado por área es búsqueda `LIKE` case-insensitive.

**Rationale**: El spec lista 8 ejemplos pero no dice que sean catálogo cerrado; tratarlos como texto libre con seed evita limitar casos futuros y mantiene validación simple (SC-004).

**Alternatives considered**:
- Catálogo cerrado de 8 áreas: rechazado — cerraría puerta a nuevas categorías y requeriría migración.
- Metadatos sin validación: rechazado — tags duplicados y colores inválidos rompen UI.

---

## R4. Catálogo cerrado `Reglas de progresión` (Constitución C)

**Decision**: Catálogo estático tipado en `CategoryCatalogs.cs` (client) y enumeración de dominio `ProgressionRule`:

`Linear | Progressive | Adaptive | CategorySpecific`

Select en `CategoryForm.razor` poblado desde `CategoryCatalogs.ProgressionRules` (label español, value canónico). Guardar fuera de catálogo → `400 InvalidCategoryData` con `errors.progressionRule`.

**Rationale**: Constitución C exige progresión via strategy, no hardcoded. Catálogo cerrado garantiza auditabilidad.

**Alternatives considered**:
- Free-form string: rechazado — viola C.
- Catálogo dinámico desde backend: rechazado — invariante de dominio, no dato.

---

## R5. BFF, auditoría y unicidad nombre

**Decision**:
- **BFF**: `ClientCategoriesService` → `HttpClient.BaseAddress = HostEnvironment.BaseAddress` → rutas `/bff/categories*` (cookie viaja); `ServerCategoriesService` → `http://oroclash-api/api/categories*` con `Bearer` del `HttpContext`. Forwarder catch-all `/bff/{**catch-all}` → `/api/{**catch-all}` ya existe (017) y cubre `POST /bff/categories`, `PUT /bff/categories/{id}`, `POST /bff/categories/{id}/publish|deactivate|archive`, `GET /bff/categories?status=&area=&search=&page=&pageSize=`.
- **Unicidad**: índice único case-insensitive sobre `Name` donde `Status != Archived` (filtra archivadas). Segundo intento con mismo nombre → `409 CategoryAlreadyExists` con `errors.name`.
- **Auditoría**: append-only via Outbox (`CategoryAuditEntry`) en `SaveChanges` (Constitución I). Cada creación/edición/transición persiste `CategoryId/ActorId/Timestamp/From/To/ChangedFields/CorrelationId`.

**Rationale**: Reutiliza patrón BFF de 017 (sample `BlazorWebAppOidcBffAutoYarpAspire`), evita exponer `access_token` y preserva `CorrelationId` (FR-012).

**Alternatives considered**:
- Llamar WASM → API directo: rechazado — expone JWT.
- Índice único global sin filtrar archivadas: rechazado — impediría recrear "Matemáticas" tras archivado.

---

## R6. Listado paginado con 8 ejemplos y filtros

**Decision**: `CategoriesList.razor` consume `GET /bff/categories` paginado (`PagedResult<CategorySummary>`), con filtros `status` (`Draft/Active/Inactive/Archived`), `area` (texto libre, ej. "Ciencia"), `search` (nombre), `page`/`pageSize` (20). Seed de 8 categorías de ejemplo (Matemáticas, Historia, Ciencia, Tecnología, Geografía, Literatura, Programación, Finanzas) via `POST /api/categories` o `seedData.json` en AppHost. Skeleton por bloque (SC-009).

**Rationale**: El spec lista 8 ejemplos como datos de prueba/demos, no como enumeración. Paginación evita cargar todo y permite escalar a ≥50 categorías.

**Alternatives considered**:
- Cargar todas las categorías en memoria y filtrar en cliente: rechazado — no escala, viola SC-009.

---

## Consolidated Decisions Summary

| # | Decisión | Fuente |
|---|----------|--------|
| 1 | 4 estados + guarda `ValidQuestionCount ≥5` + `CategoryInUse` | FR-005/008, Constitución B |
| 2 | Extender `CategoryForm` existente con 3 campos nuevos | FR-001..004, 002 |
| 3 | Metadatos tags/color/icono + área libre (8 ejemplos como seed) | FR-003, ejemplos |
| 4 | Catálogo cerrado 4 progresiones | FR-004, Constitución C |
| 5 | BFF catch-all + índice único nombre + auditoría Outbox | FR-010..012 |
| 6 | Listado paginado con filtros + seed 8 categorías | FR-016, SC-009 |

Sin NEEDS CLARIFICATION pendientes. Listo para Phase 1.
