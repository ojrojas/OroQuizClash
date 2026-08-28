# Contract: Question Bank BFF

**Branch**: `021-admin-question-bank` | **Date**: 2026-08-28

Contrato de creación/edición y ciclo de vida de preguntas. El cliente WASM nunca toca el API directo (BFF obligatorio — Constitución H, FR-016).

## 1. Endpoints BFF

Todos `RequireAuthorization: AnyAdminRole` para lectura y `AdminOrGameManager` para escritura (403 si `REWARD_MANAGER`). Forwarder YARP `/bff/{**catch-all}` → `http://oroclash-api/api/{**catch-all}` ya existe (017).

```
POST   /bff/questions               → POST   /api/questions
GET    /bff/questions               → GET    /api/questions?categoryId=&difficulty=&status=&search=&page=&pageSize=
GET    /bff/questions/{id}          → GET    /api/questions/{id}
PUT    /bff/questions/{id}          → PUT    /api/questions/{id}            (If-Match: RowVersion)
POST   /bff/questions/{id}/activate   → POST /api/questions/{id}/activate   { rowVersion }
POST   /bff/questions/{id}/deactivate → POST /api/questions/{id}/deactivate { rowVersion }
DELETE /bff/questions/{id}          → DELETE /api/questions/{id}            (If-Match: RowVersion)
GET    /bff/categories/{id}         → GET    /api/categories/{id}            (para ValidQuestionCount)
```

Auth: cookie de sesión; forwarder adjunta `Authorization: Bearer {access_token}` server-side. `X-Correlation-Id` propagado.

## 2. Create — POST /bff/questions

**Request** `Content-Type: application/json`

```json
{
  "text": "¿Cuál es la capital de Francia?",
  "categoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "difficulty": 3,
  "academicLevel": "Secundaria",
  "ageMin": 12,
  "ageMax": 18,
  "timePerQuestion": 30,
  "explanation": "París es la capital desde 508.",
  "options": [
    { "text": "Londres", "isCorrect": false },
    { "text": "París", "isCorrect": true },
    { "text": "Berlín", "isCorrect": false },
    { "text": "Roma", "isCorrect": false }
  ]
}
```

**Response 201 Created** `Location: /bff/questions/{id}`

```json
{
  "id": "9f8a7b6c-...-...",
  "text": "¿Cuál es la capital de Francia?",
  "categoryId": "3fa85f64-...",
  "difficulty": 3,
  "academicLevel": "Secundaria",
  "ageMin": 12,
  "ageMax": 18,
  "timePerQuestion": 30,
  "explanation": "París es la capital desde 508.",
  "status": "Draft",
  "options": [
    { "id":"...", "text":"Londres", "isCorrect": false, "position":"A" },
    { "id":"...", "text":"París", "isCorrect": true, "position":"B" },
    { "id":"...", "text":"Berlín", "isCorrect": false, "position":"C" },
    { "id":"...", "text":"Roma", "isCorrect": false, "position":"D" }
  ],
  "rowVersion": "AAAAAAAAB9E=",
  "createdAt": "2026-08-28T12:00:00Z"
}
```

Si la pregunta es válida y la categoría no archivada, puede crearse directamente como `Active` (según flujo del backend).

**Errores** `400 ProblemDetails` con `FieldErrors`:

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "InvalidQuestionData",
  "status": 400,
  "detail": "Exactly 4 options and 1 correct required.",
  "errors": { "options": ["Exactly 4 answer options are required."] }
}
```

`CategoryNotFound`/`CategoryNotReady` si categoría inexistente/archivada.

## 3. Update — PUT /bff/questions/{id}

**Headers** `If-Match: W/"AAAAAAAAB9E="`

**Request** idem Create + `rowVersion` implícito via `If-Match`.

**Response 200 OK** con `QuestionResponse` actualizado y nuevo `rowVersion`.

**Errores**:
- `400 InvalidQuestionData` con `errors.{field}` (texto, dificultad, edad, tiempo, opciones)
- `409 ConcurrencyConflict` → `{ "code":"ConcurrencyConflict", "detail":"La pregunta fue modificada por otro operador. Recargue." }`
- `409 QuestionInUse` si está en uso en juego `Running`/`Finished` y se intenta editar texto/opciones
- `403` si `REWARD_MANAGER`
- `404 QuestionNotFound`

## 4. Activate / Deactivate

```http
POST /bff/questions/{id}/activate
If-Match: W/"AAAAAAAAB9E="
{}

POST /bff/questions/{id}/deactivate
If-Match: W/"AAAAAAAAB9E="
{}
```

**200 OK** con nuevo `status` y `rowVersion`. `Active` cuenta para `ValidQuestionCount`, `Inactive` no.

**Errores**:
- `400 InvalidQuestionData` si al activar no cumple 4/1 o categoría archivada
- `409 QuestionInUse` si se intenta desactivar/eliminar en uso

## 5. Delete — DELETE /bff/questions/{id}

**Headers** `If-Match: W/"AAAAAAAAB9E="`

**Response 204 No Content** si nunca usada; `409 QuestionInUse` si usada en juego activo.

## 6. Read — GET /bff/questions

**Query** `categoryId`, `difficulty 1–5`, `status=Draft|Active|Inactive|Archived`, `search` (texto), `page`, `pageSize`.

**Response 200**

```json
{
  "items": [ { "id":"...", "text":"¿Cuál es...?", "categoryId":"...", "categoryName":"Matemáticas", "difficulty":3, "status":"Active", "timePerQuestion":30, "rowVersion":"AAAAAAAAB9E=" } ],
  "totalCount": 124,
  "page": 1,
  "pageSize": 20,
  "totalPages": 7
}
```

## 7. Read — GET /bff/questions/{id}

**Response 200** `QuestionResponse` con 4 `options` (A–D) y `correctAnswerIndex`, `explanation`, `inUseByLiveGame` flag, e `history`.

## 8. Contrato cliente (C#)

Vive en `QuizArena.Admin.Client/Services/IQuestionsService.cs` (existente, extender si falta):

```csharp
public interface IQuestionsService
{
    Task<PagedResult<QuestionSummary>> GetQuestionsAsync(QuestionFilter filter, CancellationToken ct = default);
    Task<QuestionDetail> GetQuestionAsync(Guid id, CancellationToken ct = default);
    Task<QuestionDetail> CreateQuestionAsync(CreateQuestionRequest request, CancellationToken ct = default);
    Task<QuestionDetail> UpdateQuestionAsync(Guid id, UpdateQuestionRequest request, CancellationToken ct = default);
    Task<QuestionDetail> ActivateAsync(Guid id, string rowVersion, CancellationToken ct = default);
    Task<QuestionDetail> DeactivateAsync(Guid id, string rowVersion, CancellationToken ct = default);
    Task DeleteAsync(Guid id, string rowVersion, CancellationToken ct = default);
}
```

Implementaciones:
- `ClientQuestionsService` (WASM): `HttpClient.PostAsJsonAsync("/bff/questions", req)` etc.
- `ServerQuestionsService` (InteractiveServer): `HttpClient.PostAsJsonAsync("http://oroclash-api/api/questions", req)` con `Bearer`.

## 9. Validación de contrato

- `AdminBffTests` — no URLs absolutas en cliente, solo `/bff/*`
- `DesignSystemNoDirectDbTests` — sin `DbContext` en Admin
- `QuestionTests` — 9 campos + 4/1 + `QuestionInUse` + `rowversion`
