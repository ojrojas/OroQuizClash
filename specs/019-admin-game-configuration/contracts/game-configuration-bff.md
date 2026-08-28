# Contract: Game Configuration BFF

**Branch**: `019-admin-game-configuration` | **Date**: 2026-08-28

Contrato de creación/edición y listado de juegos. El cliente WASM nunca toca el API directo (BFF obligatorio — Constitución H, FR-016).

## 1. Endpoints BFF

Todos `RequireAuthorization: AnyAdminRole` para lectura y `AdminOrGameManager` para escritura (403 si `REWARD_MANAGER`). El forwarder YARP `/bff/{**catch-all}` → `http://oroclash-api/api/{**catch-all}` ya existe (017).

```
POST   /bff/games                 → POST   /api/games
GET    /bff/games                 → GET    /api/games?status=&categoryId=&search=&page=&pageSize=
GET    /bff/games/{id}            → GET    /api/games/{id}
PUT    /bff/games/{id}            → PUT    /api/games/{id}           (If-Match: RowVersion)
GET    /bff/categories?status=Active → GET /api/categories?status=Active
GET    /bff/rewards?status=Active → GET /api/rewards?status=Active
```

Auth: cookie de sesión; forwarder adjunta `Authorization: Bearer {access_token}` server-side. `X-Correlation-Id` propagado.

## 2. Create — POST /bff/games

**Request** `Content-Type: application/json`

```json
{
  "name": "Quiz Noche Estrellada",
  "description": "Trivia de astronomía",
  "categoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "numberOfRounds": 7,
  "maxPlayers": 50,
  "timePerQuestion": 30,
  "initialDifficulty": 3,
  "difficultyProgression": "Adaptive",
  "scoringSystem": "ProgressiveBonus",
  "pointsPerRound": 100,
  "securedPoints": "KeepCheckpoint",
  "withdrawalPolicy": "KEEP_SECURED_SCORE",
  "finishPolicy": "FALLBACK_TO_CHECKPOINT",
  "finalRewardId": "b2e3c4d5-...-...",
  "consolationRewardId": "c3f4d5e6-...-...",
  "scheduledAt": "2026-09-01T20:00:00Z"
}
```

**Response 201 Created** `Location: /bff/games/{id}`

```json
{
  "id": "9f8a7b6c-...-...",
  "name": "Quiz Noche Estrellada",
  "description": "Trivia de astronomía",
  "categoryId": "3fa85f64-...",
  "numberOfRounds": 7,
  "maxPlayers": 50,
  "timePerQuestion": 30,
  "initialDifficulty": 3,
  "difficultyProgression": "Adaptive",
  "scoringSystem": "ProgressiveBonus",
  "withdrawalPolicy": "KEEP_SECURED_SCORE",
  "finishPolicy": "FALLBACK_TO_CHECKPOINT",
  "finalRewardId": "b2e3c4d5-...",
  "consolationRewardId": "c3f4d5e6-...",
  "scheduledAt": "2026-09-01T20:00:00Z",
  "status": "Configured",
  "rowVersion": "AAAAAAAAB9E=",
  "createdAt": "2026-08-28T12:00:00Z"
}
```

Si la configuración mínima no está completa, `status` puede quedar `Draft`.

**Errores** `400 ProblemDetails` con `FieldErrors`:

```json
{
  "type": "https://httpstatuses.com/400",
  "title": "InvalidConfiguration",
  "status": 400,
  "detail": "Category has insufficient valid questions.",
  "errors": { "categoryId": ["CategoryNotReady: requires ≥5 valid questions"] }
}
```

`409 ConcurrencyConflict` no aplica en creación; `401` si sesión expirada.

## 3. Update — PUT /bff/games/{id}

**Headers** `If-Match: W/"AAAAAAAAB9E="` o campo `rowVersion` en body (el BFF lo mapea).

**Request** idem Create + `rowVersion`.

**Response 200 OK** con body `GameResponse` actualizado y nuevo `rowVersion`.

**Errores**:
- `400 CategoryNotReady | RewardUnavailable | InvalidConfiguration` con `errors.{field}`
- `409 ConcurrencyConflict` → `{ "code":"ConcurrencyConflict", "detail":"El juego fue modificado por otro operador. Recargue." }`
- `403` si `REWARD_MANAGER`
- `422 InvalidGameState` si intenta editar tras `Ready`/`Running` (FR-010)

## 4. Read — GET /bff/games

**Query** `status=Draft|Configured|Scheduled|Ready|Running|Paused|Finished|Cancelled`, `categoryId`, `search`, `page`, `pageSize`.

**Response 200**

```json
{
  "items": [ { "id":"...", "name":"...", "status":"Configured", "scheduledAt": null, ... } ],
  "totalCount": 124,
  "page": 1,
  "pageSize": 20,
  "totalPages": 7
}
```

## 5. Read — GET /bff/games/{id}

**Response 200** `GameResponse` con `history: [{from:"Draft", to:"Configured", timestamp:"...", actorId:"sub", reason:null}]` y campos inmutables resaltados (`isImmutable: true` si `status ∈ [Ready,Running,Paused]`).

## 6. Contrato cliente (C#)

Vive en `QuizArena.Admin.Client/Services/IGameConfigurationService.cs`:

```csharp
public interface IGameConfigurationService
{
    Task<GameResponse> CreateAsync(CreateGameRequest request, CancellationToken ct = default);
    Task<GameResponse> UpdateAsync(Guid id, UpdateGameRequest request, CancellationToken ct = default);
    Task<PagedResult<GameSummary>> ListAsync(GameFilter filter, CancellationToken ct = default);
    Task<GameResponse> GetAsync(Guid id, CancellationToken ct = default);
    Task<GameResponse> TransitionAsync(Guid id, GameTransition transition, CancellationToken ct = default);
}
public record GameTransition(string ToState, DateTimeOffset? ScheduledAt = null, string? RowVersion = null);
```

Implementaciones:
- `ClientGameConfigurationService` (WASM): `HttpClient.PostAsJsonAsync("/bff/games", req)` etc.
- `ServerGameConfigurationService` (InteractiveServer): `HttpClient.PostAsJsonAsync("http://oroclash-api/api/games", req)` con `Bearer` del `HttpContext`.

## 7. Validación de contrato

- `AdminBffTests` — no URLs absolutas en cliente, solo `/bff/*`
- `DesignSystemNoDirectDbTests` — sin `DbContext` en Admin
- `GameConfigurationTests` — 16 campos validación + `ConcurrencyConflict`
