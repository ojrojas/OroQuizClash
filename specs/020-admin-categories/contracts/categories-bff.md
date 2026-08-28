# Contract: Categories BFF

**Branch**: `020-admin-categories` | **Date**: 2026-08-28

Contrato de creación/edición y listado de categorías. El cliente WASM nunca toca el API directo (BFF obligatorio — Constitución H, FR-013).

## 1. Endpoints BFF

Todos `RequireAuthorization: AnyAdminRole` para lectura y `AdminOrGameManager` para escritura (403 si `REWARD_MANAGER`). Forwarder YARP `/bff/{**catch-all}` → `http://oroclash-api/api/{**catch-all}` ya existe (017).

```
POST   /bff/categories              → POST   /api/categories
GET    /bff/categories              → GET    /api/categories?status=&area=&search=&page=&pageSize=
GET    /bff/categories/{id}         → GET    /api/categories/{id}
PUT    /bff/categories/{id}         → PUT    /api/categories/{id}            (If-Match: RowVersion)
POST   /bff/categories/{id}/publish   → POST /api/categories/{id}/publish    { rowVersion }
POST   /bff/categories/{id}/deactivate → POST /api/categories/{id}/deactivate { rowVersion }
POST   /bff/categories/{id}/archive   → POST /api/categories/{id}/archive   { rowVersion }
```

Auth: cookie de sesión; forwarder adjunta `Authorization: Bearer {access_token}` server-side. `X-Correlation-Id` propagado.

## 2. Create — POST /bff/categories

**Request** `Content-Type: application/json`

```json
{
  "name": "Matemáticas",
  "description": "Álgebra y cálculo",
  "knowledgeArea": "Matemáticas",
  "academicLevel": "Secundaria",
  "ageMin": 12,
  "ageMax": 18,
  "difficulty": 3,
  "targetAudience": "Estudiantes Secundaria",
  "tags": ["álgebra", "cálculo"],
  "color": "#2563EB",
  "icon": "calculator",
  "progressionRule": "Linear"
}
```

**Response 201 Created** `Location: /bff/categories/{id}`

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Matemáticas",
  "knowledgeArea": "Matemáticas",
  "academicLevel": "Secundaria",
  "ageMin": 12,
  "ageMax": 18,
  "difficulty": 3,
  "targetAudience": "Estudiantes Secundaria",
  "tags": ["álgebra", "cálculo"],
  "color": "#2563EB",
  "icon": "calculator",
  "progressionRule": "Linear",
  "status": "Draft",
  "validQuestionCount": 0,
  "rowVersion": "AAAAAAAAB9E=",
  "createdAt": "2026-08-28T12:00:00Z"
}
```

**Errores** `400`/`409` `ProblemDetails` con `FieldErrors`:

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "InvalidCategoryData",
  "status": 400,
  "detail": "Category name already exists.",
  "errors": { "name": ["CategoryAlreadyExists"] }
}
```

`409 CategoryAlreadyExists` si nombre duplicado case-insensitive entre no archivadas.

## 3. Update — PUT /bff/categories/{id}

**Headers** `If-Match: W/"AAAAAAAAB9E="`

**Request** idem Create + `rowVersion` implícito via `If-Match`.

**Response 200 OK** con `CategoryResponse` actualizado y nuevo `rowVersion`.

**Errores**:
- `400 InvalidCategoryData` con `errors.{field}` (edad invertida, tags, dificultad, progresión fuera de catálogo)
- `409 ConcurrencyConflict` → `{ "code":"ConcurrencyConflict", "detail":"La categoría fue modificada por otro operador. Recargue." }`
- `409 CategoryAlreadyExists` si cambia nombre a duplicado
- `403` si `REWARD_MANAGER`
- `422 InvalidCategoryState` si intenta editar tras `Archived`

## 4. Read — GET /bff/categories

**Query** `status=Draft|Active|Inactive|Archived`, `area=Matemáticas`, `search=Historia`, `page`, `pageSize`.

**Response 200**

```json
{
  "items": [ { "id":"...", "name":"Matemáticas", "knowledgeArea":"Matemáticas", "status":"Draft", "validQuestionCount": 0, "rowVersion":"AAAAAAAAB9E=" } ],
  "totalCount": 8,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

## 5. Read — GET /bff/categories/{id}

**Response 200** `CategoryResponse` con `history: [{from:"Draft", to:"Active", timestamp:"...", actorId:"sub"}]` y `validQuestionCount`.

## 6. Contrato cliente (C#)

Vive en `QuizArena.Admin.Client/Services/ICategoriesService.cs` (existente, extender si falta):

```csharp
public interface ICategoriesService
{
    Task<PagedResult<CategorySummary>> GetCategoriesAsync(CategoryFilter filter, CancellationToken ct = default);
    Task<CategoryDetail> GetCategoryAsync(Guid id, CancellationToken ct = default);
    Task<CategoryDetail> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken ct = default);
    Task<CategoryDetail> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default);
    Task<CategoryDetail> PublishAsync(Guid id, string rowVersion, CancellationToken ct = default);
    Task<CategoryDetail> DeactivateAsync(Guid id, string rowVersion, CancellationToken ct = default);
    Task<CategoryDetail> ActivateAsync(Guid id, string rowVersion, CancellationToken ct = default);
    Task<CategoryDetail> ArchiveAsync(Guid id, string rowVersion, CancellationToken ct = default);
}
```

Implementaciones:
- `ClientCategoriesService` (WASM): `HttpClient.PostAsJsonAsync("/bff/categories", req)` etc.
- `ServerCategoriesService` (InteractiveServer): `HttpClient.PostAsJsonAsync("http://oroclash-api/api/categories", req)` con `Bearer`.

## 7. Validación de contrato

- `AdminBffTests` — no URLs absolutas en cliente, solo `/bff/*`
- `DesignSystemNoDirectDbTests` — sin `DbContext` en Admin
- `CategoryTests` — 10 campos validación + `CategoryAlreadyExists` + `rowversion`
