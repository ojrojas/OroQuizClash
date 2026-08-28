# Contract: Question Statistics

**Branch**: `021-admin-question-bank` | **Date**: 2026-08-28

Estadísticas agregadas del banco y guarda `CategoryMinQuestions` (FR-008/009).

## 1. Endpoints BFF

```
GET /bff/questions/stats                 → GET /api/questions/stats
GET /bff/categories/{id}                 → GET /api/categories/{id} (incluye validQuestionCount)
GET /bff/system/config                   → GET /api/system/config (incluye categoryMinQuestions)
```

Todos `RequireAuthorization: AnyAdminRole`.

## 2. GET /bff/questions/stats

**Response 200** `Content-Type: application/json`

```json
{
  "total": 124,
  "byCategory": [
    { "categoryId": "3fa85f64-...", "categoryName": "Matemáticas", "count": 24 },
    { "categoryId": "...", "categoryName": "Historia", "count": 18 }
  ],
  "byDifficulty": [
    { "difficulty": 1, "count": 12 },
    { "difficulty": 2, "count": 24 },
    { "difficulty": 3, "count": 30 },
    { "difficulty": 4, "count": 28 },
    { "difficulty": 5, "count": 30 }
  ],
  "byStatus": [
    { "status": "Active", "count": 80 },
    { "status": "Inactive", "count": 20 },
    { "status": "Draft", "count": 24 }
  ],
  "avgTimePerQuestion": 28.5,
  "validPerCategory": [
    { "categoryId": "3fa85f64-...", "categoryName": "Matemáticas", "valid": 5, "required": 5 }
  ]
}
```

- `total`: total de preguntas en el banco.
- `byCategory`/`byDifficulty`/`byStatus`: conteos agregados server-side.
- `avgTimePerQuestion`: promedio `TimePerQuestion` 5–300.
- `validPerCategory`: `valid` = `ValidQuestionCount` (preguntas `Active` con 4/1), `required` = `CategoryMinQuestions` (inicial 5, configurable). Si `valid < required`, la categoría no es publicable.

Si el endpoint `/api/questions/stats` no existe, el BFF hace fallback via `ServerQuestionsService` con `Task.WhenAll` sobre `GET /api/questions?categoryId=&difficulty=&status=&pageSize=1` (solo conteos, sin cargar todo).

## 3. GET /bff/categories/{id}

Incluye `validQuestionCount` y `required`:

```json
{
  "id": "3fa85f64-...",
  "name": "Matemáticas",
  "status": "Draft",
  "validQuestionCount": 4,
  "required": 5,
  "rowVersion": "AAAAAAAAB9E="
}
```

UI muestra `4/5` y deshabilita "Publicar" si `valid < required` con `CategoryNotReady`.

## 4. GET /bff/system/config

```json
{
  "categoryMinQuestions": 5
}
```

Valor inicial 5, configurable sin migración. Cambiar a 3 hace que categorías con 3 ya muestren `3/3` y habiliten publicar; subir a 7 hace que categorías con 5 muestren `5/7` y deshabiliten.

## 5. Invariantes

- Estadísticas son agregados server-side; la UI no carga todo el banco.
- `CategoryMinQuestions` es parámetro del sistema, no por categoría (preserva compatibilidad).
- `ValidQuestionCount` se actualiza transaccionalmente al activar/desactivar/eliminar una pregunta (FR-010).

## 6. Contrato cliente

```csharp
public record QuestionStatistics(
    int Total,
    IReadOnlyList<CountByCategory> ByCategory,
    IReadOnlyList<CountByDifficulty> ByDifficulty,
    IReadOnlyList<CountByStatus> ByStatus,
    double AvgTimePerQuestion,
    IReadOnlyList<ValidPerCategory> ValidPerCategory);

public interface IQuestionsService
{
    Task<QuestionStatistics> GetStatisticsAsync(CancellationToken ct = default);
    Task<SystemConfig> GetSystemConfigAsync(CancellationToken ct = default);
}
```

Page `QuestionStats.razor` / `QuestionStatsPanel.razor` consume `GetStatisticsAsync` y muestra tarjetas por categoría/dificultad/estado y gauge `valid/required`.

## 7. Errores

| Código | HTTP | Cuando |
|--------|------|--------|
| `Unauthorized` | 401 | sesión expirada |
| `Forbidden` | 403 | `REWARD_MANAGER` |

Todos `application/problem+json`.
