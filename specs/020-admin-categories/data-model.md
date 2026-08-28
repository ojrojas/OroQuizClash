# Data Model: Admin Categories

**Branch**: `020-admin-categories` | **Date**: 2026-08-28 | **Plan**: [plan.md](plan.md)

Modelo de datos **frontend** (proyecciones/DTOs) para categorías administrativas. Sin persistencia propia: estructuras en `QuizArena.Admin.Client/Models/Categories/` que reflejan contratos `oroclash-api /api/categories*` (SPEC-002). Autoridad permanece en backend (Constitución V).

## 1. Entidades principales

### Category

Agregado de conocimiento. Inmutable tras `Archived`.

```csharp
enum CategoryStateView
{
    Draft,      // borrador
    Active,     // publicada y elegible (≥5 preguntas)
    Inactive,   // pausada
    Archived    // terminal, solo lectura
}

record Category(
    Guid CategoryId,
    string Name,                    // 3–100, único case-insensitive entre no archivadas
    string? Description,            // 0–500
    string KnowledgeArea,           // 2–100 (Matemáticas, Historia, Ciencia, Tecnología, Geografía, Literatura, Programación, Finanzas — texto libre)
    string AcademicLevel,           // 2–100
    int AgeMin,                     // 0–120
    int AgeMax,                     // 0–120, AgeMin ≤ AgeMax
    int Difficulty,                 // 1–5
    string TargetAudience,          // 2–100
    CategoryStateView Status,
    CategoryMetadata Metadata,      // tags, color, icono
    ProgressionRule Progression,    // Linear/Progressive/Adaptive/CategorySpecific
    int ValidQuestionCount,         // derivado server-side (4 opciones/1 correcta, Active)
    string RowVersion               // base64 rowversion
);

record CategoryMetadata(
    IReadOnlyList<string> Tags,     // 0–10, 2–30 chars, sin duplicados case-insensitive
    string? Color,                  // #RRGGBB opcional
    string? Icon                    // Lucide icon name opcional
);

enum ProgressionRule
{
    Linear,
    Progressive,
    Adaptive,
    CategorySpecific
}
```

**Invariantes**:
- `Name` único case-insensitive donde `Status != Archived`.
- `AgeMin ≤ AgeMax`, ambos `0–120`.
- `Difficulty ∈ [1,5]`.
- `Tags` ≤10, cada uno `2–30`, sin duplicados.
- `ProgressionRule` en catálogo cerrado.
- `Active` requiere `ValidQuestionCount ≥5`.
- `Archived` inmutable y no elegible.

### CategoryForm (validación 3 niveles espejo dominio)

```csharp
record CategoryForm(
    string Name,
    string? Description,
    string KnowledgeArea,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    int Difficulty,
    string TargetAudience,
    IReadOnlyList<string> Tags,
    string? Color,
    string? Icon,
    ProgressionRule Progression);

IReadOnlyDictionary<string,string[]> Validate() // por campo
```

Validación: `Name 3–100`, `KnowledgeArea/AcademicLevel/TargetAudience 2–100`, `Age 0–120` y `min≤max`, `Difficulty 1–5`, `Tags` reglas, `Color` hex, `Progression` en catálogo.

### CategorySummary (listado paginado)

```csharp
record CategorySummary(
    Guid Id,
    string Name,
    string KnowledgeArea,
    CategoryStateView Status,
    int ValidQuestionCount,
    string? Color,
    string RowVersion);
```

### CategoryDetail (detalle + historial)

```csharp
record CategoryDetail : CategorySummary
{
    string? Description;
    string AcademicLevel;
    int AgeMin;
    int AgeMax;
    int Difficulty;
    string TargetAudience;
    CategoryMetadata Metadata;
    ProgressionRule Progression;
    IReadOnlyList<CategoryStateTransition> History;
}
```

### CategoryStateTransition / Audit

```csharp
record CategoryStateTransition(
    CategoryStateView From,
    CategoryStateView To,
    DateTimeOffset Timestamp,
    string ActorId,
    string? Reason);

record CategoryAuditEntry(
    Guid CategoryId,
    string ActorId, // sub
    DateTimeOffset Timestamp,
    CategoryStateView FromState,
    CategoryStateView ToState,
    IReadOnlyDictionary<string,string> ChangedFields,
    string CorrelationId,
    string Result);
```

## 2. DTOs de transporte (BFF boundary)

```csharp
record CreateCategoryRequest(
    string Name,
    string? Description,
    string KnowledgeArea,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    int Difficulty,
    string TargetAudience,
    IReadOnlyList<string> Tags,
    string? Color,
    string? Icon,
    ProgressionRule Progression);

record UpdateCategoryRequest : CreateCategoryRequest
{
    string RowVersion; // If-Match
}

record CategoryResponse : CategoryDetail; // camelCase JSON

record CategoryFilter(
    CategoryStateView? Status = null,
    string? KnowledgeArea = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);
```

Paginación: `PagedResult<CategorySummary> { Items, TotalCount, Page, PageSize }`.

## 3. Catálogos estáticos

```csharp
static class CategoryCatalogs
{
    static IReadOnlyList<string> ProgressionRules => ["Linear","Progressive","Adaptive","CategorySpecific"];
    static IReadOnlyList<string> ExampleAreas => ["Matemáticas","Historia","Ciencia","Tecnología","Geografía","Literatura","Programación","Finanzas"];
    static IReadOnlyList<int> Difficulties => [1,2,3,4,5];
}
```

## 4. Validación por niveles

- **API**: tipos/rangos → `400` + `FieldErrors` por campo.
- **Aplicación**: `Validator` — unicidad nombre, área/nivel 2–100, edad, dificultad, tags, progresión, `Archived` con `CategoryInUse`.
- **Dominio**: invariantes `CategoryNotReady` (≥5), `CategoryAlreadyExists`, `InvalidCategoryState`, `ConcurrencyConflict`.

## 5. Relaciones

```text
Category ── contiene 1 ──> CategoryStateView (4 estados)
Category ── contiene 1 ──> CategoryMetadata (tags/color/icon)
Category ── referencia 0..N ──> Question (4 opciones/1 correcta, Active) → ValidQuestionCount
Category ── referenciada por 0..N ──> Game (Running/Scheduled) → bloquea Archived
Category ── contiene N ──> CategoryStateTransition / CategoryAuditEntry
CategorySummary ── deriva ──> PagedResult (listado filtrado por status/area/search)
```

## 6. Transiciones de estado

```text
Draft → Active [guard: ValidQuestionCount ≥5]
Active ↔ Inactive [sin guarda]
Active/Inactive → Archived [guard: sin Game en Running/Scheduled/Ready que la referencie]
Archived → * (ninguna, terminal)

Inválidas → InvalidCategoryState, sin mutación parcial, protegidas por rowversion.
```

## 7. Reglas de autorización (proyección)

- `ADMIN`/`GAME_MANAGER` (`AdminOrGameManager`) → `Create/Update/Publish/Deactivate/Archive`.
- `REWARD_MANAGER` → `403 Access Denied`.
