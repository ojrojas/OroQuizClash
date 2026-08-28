# Contract: Category States

**Branch**: `020-admin-categories` | **Date**: 2026-08-28

Máquina de 4 estados (FR-005) con guardas `ValidQuestionCount ≥5` y `CategoryInUse`.

## 1. Endpoints de transición

```
POST /bff/categories/{id}/publish    → POST /api/categories/{id}/publish    { rowVersion }
POST /bff/categories/{id}/deactivate → POST /api/categories/{id}/deactivate { rowVersion }
POST /bff/categories/{id}/activate   → POST /api/categories/{id}/activate   { rowVersion }
POST /bff/categories/{id}/archive    → POST /api/categories/{id}/archive    { rowVersion }
```

Todos `RequireAuthorization: AdminOrGameManager`. `RowVersion` via `If-Match` o body.

## 2. Diagrama de transiciones permitidas

```
Draft ──► Active ◄──► Inactive ──► Archived
           ▲            ▲
           └────────────┘
```

- `Draft → Active` (`Publish`) requiere `ValidQuestionCount ≥5` (4 opciones/1 correcta, `Active`).
- `Active → Inactive` (`Deactivate`) y `Inactive → Active` (`Activate`) sin guarda adicional.
- `Active/Inactive → Archived` (`Archive`) terminal; guarda `CategoryInUse` si existe `Game` en `Running`/`Scheduled`/`Ready` que referencia la categoría.
- `Archived` no es elegible para nuevos juegos/preguntas; toda edición rechazada con `422 InvalidCategoryState`.

## 3. Request/Response por transición

**Publish**

```http
POST /bff/categories/{id}/publish
If-Match: W/"AAAAAAAAB9E="
{}
```

**200 OK**

```json
{ "id":"...", "status":"Active", "validQuestionCount": 5, "rowVersion":"AAAAAAAAB9I=" }
```

**Deactivate/Activate/Archive** — idem con mismo `If-Match`.

**400 CategoryNotReady** si <5 preguntas:

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "CategoryNotReady",
  "status": 400,
  "detail": "Category requires at least 5 valid questions.",
  "errors": { "categoryId": ["Need 1 more valid question"] }
}
```

## 4. Errores

| Código | HTTP | Cuando |
|--------|------|--------|
| `CategoryNotReady` | 400 | `Draft → Active` con `ValidQuestionCount <5` |
| `CategoryInUse` | 409 | `→ Archived` con juegos activos que la usan (lista de juegos bloqueantes en `detail`) |
| `CategoryAlreadyExists` | 409 | nombre duplicado case-insensitive entre no archivadas |
| `InvalidCategoryState` | 422 | transición no permitida (p. ej., `Archived → Active` sin reactivación, o editar tras `Archived`) |
| `ConcurrencyConflict` | 409 | `RowVersion` desactualizado |
| `InvalidCategoryData` | 400 | tags, edad invertida, dificultad fuera de rango, progresión fuera de catálogo |
| `Unauthorized` | 401 | sesión expirada → banner re-autenticar |
| `Forbidden` | 403 | `REWARD_MANAGER` → Access Denied |

Todos `application/problem+json` con `errors.{field}`.

## 5. Invariantes

- Transición es atómica (agregado + Outbox + audit) — sin mutación parcial.
- `RowVersion` incrementado en cada transición.
- Auditoría append-only: `CategoryAuditEntry` con `From/To/ActorId/Timestamp/CorrelationId`.
- `Name` único case-insensitive donde `Status != Archived` (índice filtrado).

## 6. Contrato cliente

```csharp
public enum CategoryStateView { Draft, Active, Inactive, Archived }

public Task<CategoryDetail> PublishAsync(Guid id, string rowVersion, CancellationToken ct = default);
public Task<CategoryDetail> DeactivateAsync(Guid id, string rowVersion, CancellationToken ct = default);
public Task<CategoryDetail> ActivateAsync(Guid id, string rowVersion, CancellationToken ct = default);
public Task<CategoryDetail> ArchiveAsync(Guid id, string rowVersion, CancellationToken ct = default);
```

Page `CategoryTransitionsBar.razor` habilita botones según `Status` actual y `ValidQuestionCount`; `Archived` deshabilita todos.
