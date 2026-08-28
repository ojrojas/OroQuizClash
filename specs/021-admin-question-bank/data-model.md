# Data Model: Admin Question Bank

**Branch**: `021-admin-question-bank` | **Date**: 2026-08-28 | **Plan**: [plan.md](plan.md)

Modelo de datos **frontend** (proyecciones/DTOs) para el banco de preguntas. Sin persistencia propia: estructuras en `QuizArena.Admin.Client/Models/Questions/` que reflejan contratos `oroclash-api /api/questions*` (SPEC-003). Autoridad permanece en backend (Constitución V).

## 1. Entidades principales

### Question

Agregado de contenido. Inmutable tras ser usada en juego `Running` (variante `QuestionInUse`).

```csharp
enum QuestionStateView
{
    Draft,      // borrador
    Active,     // publicada y cuenta para ValidQuestionCount
    Inactive,   // desactivada
    Archived    // eliminada/archivada, terminal
}

record Question(
    Guid QuestionId,
    string Text,                    // 10–500
    Guid CategoryId,                // FK Category no archivada
    string CategoryName,            // denormalizado para listado
    int Difficulty,                 // 1–5
    string AcademicLevel,           // 2–100
    int AgeMin,                     // 0–120
    int AgeMax,                     // 0–120, AgeMin ≤ AgeMax
    int TimePerQuestion,            // 5–300s
    string? Explanation,            // 0–1000
    QuestionStateView Status,
    IReadOnlyList<AnswerOption> Answers, // exactamente 4
    int CorrectAnswerIndex,         // 0–3, señala la única IsCorrect
    string RowVersion               // base64 rowversion
);

record AnswerOption(
    Guid OptionId,
    string Text,                    // 1–200
    bool IsCorrect,                 // exactamente 1 por Question
    char Position                   // 'A'–'D'
);
```

**Invariantes**:
- `Answers.Count == 4`, cada `Text 1–200`, exactamente 1 `IsCorrect`.
- `CategoryId` referencia `Category` en estado no archivado.
- `Difficulty ∈ [1,5]`, `AcademicLevel 2–100`, `AgeMin ≤ AgeMax` en `0–120`, `TimePerQuestion 5–300`, `Explanation 0–1000`.
- `Active` requiere 4/1 y categoría no archivada.
- `RowVersion` para concurrencia optimista.

### QuestionForm (validación 3 niveles espejo dominio)

```csharp
record QuestionForm(
    string Text,
    Guid CategoryId,
    int Difficulty,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    IReadOnlyList<OptionForm> Options, // 4 OptionForm
    string? Explanation,
    int TimePerQuestion);

record OptionForm(string Text, bool IsCorrect);

IReadOnlyDictionary<string,string[]> Validate() // por campo
```

Validación: `Text 10–500`, `CategoryId != Guid.Empty`, `Difficulty 1–5`, `AcademicLevel 2–100`, `Age 0–120` y `min≤max`, `Options.Count==4` y `Count(IsCorrect)==1` y cada `Text 1–200`, `Time 5–300`, `Explanation 0–1000`.

### QuestionSummary (listado paginado)

```csharp
record QuestionSummary(
    Guid Id,
    string Text, // truncado 80 chars para listado
    Guid CategoryId,
    string CategoryName,
    int Difficulty,
    QuestionStateView Status,
    int TimePerQuestion,
    bool InUseByLiveGame, // true si usada en juego Running/Finished
    string RowVersion);
```

### QuestionDetail (detalle + historial)

```csharp
record QuestionDetail : QuestionSummary
{
    string AcademicLevel;
    int AgeMin;
    int AgeMax;
    string? Explanation;
    IReadOnlyList<AnswerOption> Answers;
    IReadOnlyList<QuestionStateTransition> History;
}
```

### Category Reference (read-model)

```csharp
record CategorySummary(
    Guid Id,
    string Name,
    CategoryStateView Status, // solo para filtro, no es el estado de la pregunta
    int ValidQuestionCount);
```

Relación: `Question.CategoryId → Category.Id` donde `Category.Status != Archived`.

### QuestionStatistics (agregado de lectura)

```csharp
record QuestionStatistics(
    int Total,
    IReadOnlyList<CountByCategory> ByCategory,
    IReadOnlyList<CountByDifficulty> ByDifficulty,
    IReadOnlyList<CountByStatus> ByStatus,
    double AvgTimePerQuestion,
    IReadOnlyList<ValidCountPerCategory> ValidPerCategory);

record CountByCategory(Guid CategoryId, string CategoryName, int Count);
record CountByDifficulty(int Difficulty, int Count);
record CountByStatus(QuestionStateView Status, int Count);
record ValidCountPerCategory(Guid CategoryId, string CategoryName, int Valid, int Required);
```

Derivado server-side, sin cargar todo el banco.

### QuestionStateTransition / Audit

```csharp
record QuestionStateTransition(
    QuestionStateView From,
    QuestionStateView To,
    DateTimeOffset Timestamp,
    string ActorId,
    string? Reason);

record QuestionAuditEntry(
    Guid QuestionId,
    string ActorId, // sub
    DateTimeOffset Timestamp,
    string Action, // Created/Updated/Activated/Deactivated/Deleted
    QuestionStateView FromState,
    QuestionStateView ToState,
    IReadOnlyDictionary<string,string> ChangedFields,
    string CorrelationId,
    string Result);
```

## 2. DTOs de transporte (BFF boundary)

```csharp
record CreateQuestionRequest(
    string Text,
    Guid CategoryId,
    int Difficulty,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    IReadOnlyList<OptionForm> Options,
    string? Explanation,
    int TimePerQuestion);

record UpdateQuestionRequest : CreateQuestionRequest
{
    string RowVersion; // If-Match
}

record QuestionResponse : QuestionDetail; // camelCase JSON

record QuestionFilter(
    Guid? CategoryId = null,
    int? Difficulty = null,
    QuestionStateView? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);
```

Paginación: `PagedResult<QuestionSummary> { Items, TotalCount, Page, PageSize }`.

## 3. Parámetro del sistema `CategoryMinQuestions`

```csharp
record SystemConfig(
    int CategoryMinQuestions // inicial 5, configurable
);
```

Expuesto como `GET /api/system/config` o `GET /api/categories/min-questions` con `categoryMinQuestions: 5`. UI muestra `ValidQuestionCount / CategoryMinQuestions` y deshabilita "Publicar" si <5.

## 4. Validación por niveles

- **API**: tipos/rangos → `400` + `FieldErrors` por campo.
- **Aplicación**: `Validator` — categoría existe y no archivada, 4/1, dificultad/nivel/edad/tiempo, explicación, `CategoryMinQuestions`.
- **Dominio**: invariantes `InvalidQuestionData`, `CategoryNotReady`, `QuestionInUse`, `ConcurrencyConflict`.

## 5. Relaciones

```text
Question ── referencia 1 ──> Category (no archivada) → ValidQuestionCount
Question ── contiene 4 ──> AnswerOption (exactamente 1 IsCorrect)
Question ── contiene 1 ──> QuestionStateView (4 estados)
Question ── referenciada por 0..N ──> Game (Running/Finished) → QuestionInUse
QuestionSummary ── deriva ──> PagedResult (listado filtrado por category/difficulty/status/search)
QuestionStatistics ── agrega ──> Question (por categoría/dificultad/estado)
```

## 6. Transiciones de estado

```text
Draft → Active [guard: 4/1 y categoría no archivada]
Active ↔ Inactive [sin guarda adicional]
Draft/Inactive → Deleted/Archived [guard: QuestionInUse si usada en juego Running/Finished]

Inválidas → InvalidQuestionState, sin mutación parcial, protegidas por rowversion.
```

## 7. Reglas de autorización (proyección)

- `ADMIN`/`GAME_MANAGER` (`AdminOrGameManager`) → `Create/Update/Delete/Activate/Deactivate`.
- `REWARD_MANAGER` → `403 Access Denied`.
