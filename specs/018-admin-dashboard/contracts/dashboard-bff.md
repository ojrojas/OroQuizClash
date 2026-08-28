# Contract: Dashboard BFF

**Branch**: `018-admin-dashboard` | **Date**: 2026-08-28

Contrato de la única llamada del dashboard. El cliente WASM nunca toca el API directamente (BFF obligatorio — Constitución H, FR-015).

## 1. Endpoint BFF

```
GET /bff/dashboard/snapshot
→ forwarder YARP /bff/{**catch-all} → http://oroclash-api/api/dashboard/snapshot
(or fan-out server-side si el endpoint no existe — research R1)
RequireAuthorization: true (401 si sesión expirada)
Correlation: header X-Correlation-Id propagado
```

- **Auth**: cookie de sesión del servidor; el forwarder adjunta `Authorization: Bearer {access_token}` server-side (igual que `MapBffForwarder` existente).
- **Caching**: `Cache-Control: no-store` (datos operacionales).
- **Timeout**: 5s por bloque interno; el snapshot responde en ≤2s (SC-001) con bloques en `Error` si un downstream excede timeout.

## 2. Request

```http
GET /bff/dashboard/snapshot HTTP/1.1
Cookie: .AspNetCore.Cookies=...
Accept: application/json
```

Sin query params en v1. Extensibilidad: `?metrics=ActiveGames,ScheduledGames` opcional post-MVP para reintento aislado.

### Reintento aislado (opcional v1, recomendado)

```
GET /bff/dashboard/snapshot/{metricId}
→ GET /bff/dashboard/snapshot?metrics={metricId}
```

Permite que el botón "Reintentar" de un `MetricTile` en `Error` re-consulte solo ese bloque sin refrescar los 9 restantes. Si no se implementa en v1, el reintento dispara `GET /bff/dashboard/snapshot` completo y la UI actualiza solo el bloque fallido (aceptable para MVP).

## 3. Response — 200 OK

`Content-Type: application/json`

```json
{
  "generatedAt": "2026-08-28T12:00:00Z",
  "correlationId": "00-abc123-def456-01",
  "metrics": [
    {
      "id": "ActiveGames",
      "label": "Juegos activos",
      "count": 12,
      "state": "Ready",
      "sourceLabel": null,
      "tooltip": null,
      "retryable": false,
      "drillDownRoute": "/games?status=Active"
    },
    {
      "id": "ScheduledGames",
      "label": "Juegos programados",
      "count": 0,
      "state": "Empty",
      "sourceLabel": null,
      "tooltip": null,
      "retryable": false,
      "drillDownRoute": "/games?status=Scheduled"
    },
    {
      "id": "ConnectedPlayers",
      "label": "Jugadores conectados",
      "count": 87,
      "state": "Ready",
      "sourceLabel": "SignalR presence",
      "tooltip": "Aproximación: conexiones SignalR activas (backend no expone /players/online)",
      "retryable": false,
      "drillDownRoute": "/players?view=online"
    },
    {
      "id": "ActivePlayers",
      "label": "Jugadores activos",
      "count": 42,
      "state": "Ready",
      "sourceLabel": "PLAYING en IN_PROGRESS",
      "tooltip": null,
      "retryable": false,
      "drillDownRoute": "/players?view=active"
    },
    {
      "id": "AvailableQuestions",
      "label": "Preguntas disponibles",
      "count": 342,
      "state": "Ready",
      "sourceLabel": null,
      "tooltip": null,
      "retryable": false,
      "drillDownRoute": "/questions?status=Active"
    }
  ],
  "statistics": {
    "totalGames": 1240,
    "totalParticipations": 8923,
    "avgQuestionsPerCategory": 28.5,
    "breakdown": [
      { "key": "categoriesActive", "label": "Categorías activas", "value": "12" }
    ]
  }
}
```

**Notas**:
- `metrics` siempre 10 entradas (enum `MetricId`); orden canónico del spec (activos→programados→finalizados→conectados→activos→preguntas→categorías→premios→canjes→estadísticas).
- `state` enum string: `Loading` nunca se serializa (solo view-state cliente); servidor retorna `Ready|Empty|Error`.
- `count` siempre `>=0`; `Empty` implica `count==0`.
- `Error` example:

```json
{
  "id": "Rewards",
  "label": "Premios",
  "count": 0,
  "state": "Error",
  "errorCode": "RewardsUnavailable",
  "errorMessage": "No se pudieron cargar los premios. Reintente en unos segundos.",
  "retryable": true,
  "drillDownRoute": null
}
```
- `drillDownRoute: null` si el rol del caller no autoriza el destino (FR-014).

## 4. Errores — envelope RFC 7807 passthrough

Si el forwarder falla (downstream 5xx) pero otros bloques se pudieron componer, el snapshot responde **200** con bloques individuales en `Error`. Si la autenticación falla:

```http
HTTP/1.1 401 Unauthorized
Content-Type: application/problem+json

{
  "type": "https://httpstatuses.com/401",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Sesión expirada. Vuelva a autenticarse."
}
```

La UI detiene el polling y muestra banner "Sesión expirada" (edge case).

Si el BFF no logra componer ningún bloque (API caído):

```http
HTTP/1.1 503 Service Unavailable
Content-Type: application/problem+json

{
  "type": "https://httpstatuses.com/503",
  "title": "Service Unavailable",
  "status": 503,
  "detail": "No se pudo componer el snapshot del dashboard."
}
```

## 5. Contrato cliente (C#)

Vive en `QuizArena.Admin.Client/Services/IDashboardService.cs` (compartido server/cliente):

```csharp
public interface IDashboardService
{
    Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken ct = default);
    // Opcional v1:
    Task<MetricValue> GetMetricAsync(MetricId id, CancellationToken ct = default);
}
```

Implementaciones:
- `ClientDashboardService` (WASM): `HttpClient.GetFromJsonAsync<DashboardSnapshot>("/bff/dashboard/snapshot", ct)`
- `ServerDashboardService` (server): `HttpClient.GetFromJsonAsync<DashboardSnapshot>("http://oroclash-api/api/dashboard/snapshot")` o fan-out `Task.WhenAll` si el endpoint no existe; mapea a `DashboardSnapshot` y firma por rol (filtra `drillDownRoute` según `HttpContext.User`).

## 6. Validación de contrato

- **BFF no-DB**: `AdminNoDirectDbTests` + `DesignSystemNoDirectDbTests` existentes cubren que `ServerDashboardService` no referencia `DbContext`.
- **Coherencia SC-003**: test de contrato verifica `metric.count == PagedResult.TotalCount` del listado destino con mismo filtro.
- **Autorización**: test `DashboardAuthorizationTests` — 3 roles × 10 métricas + 7 atajos.
