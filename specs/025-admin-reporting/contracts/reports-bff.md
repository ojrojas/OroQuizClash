# Contract: Reports BFF

**Branch**: `025-admin-reporting` | **Date**: 2026-05-13

Contrato de reporting analítico (12 métricas). El cliente WASM nunca toca el API directo (BFF obligatorio — Constitución H, FR-014).

## 1. Endpoints BFF

Todos `RequireAuthorization` con políticas (`ADMIN` todo; `GAME_MANAGER` operativo/rendimiento; `REWARD_MANAGER` recompensas). Forwarder YARP `/bff/{**catch-all}` → `http://oroclash-api/api/{**catch-all}` ya existe (017). `PLAYER` → 403.

```
GET    /bff/reports/operational         → GET    /api/reports/operational?from=&to=&categoryId=&categoryName=&gameId=&gameName=&playerId=&playerSearch=&level=&result=&page=&pageSize=
GET    /bff/reports/performance         → GET    /api/reports/performance?from=&to=&categoryId=&gameId=&playerId=&level=&result=&page=&pageSize=
GET    /bff/reports/rewards             → GET    /api/reports/rewards?from=&to=&categoryId=&gameId=&playerId=&level=&result=&page=&pageSize=
GET    /bff/reports/full                → GET    /api/reports/full?from=&to=&categoryId=&gameId=&playerId=&level=&result=
```

Auth: cookie de sesión; forwarder adjunta `Authorization: Bearer {access_token}` server-side. `X-Correlation-Id` propagado. Solo `GET` (solo lectura, FR-016).

## 2. Operational — GET /bff/reports/operational

**Query** `from`/`to` (ISO 8601), `categoryId`/`categoryName`, `gameId`/`gameName`, `playerId`/`playerSearch`, `level` (1–5), `result` (catálogo `GameStatus` 9), `page`, `pageSize` (default 20, max 100).

**Response 200**

```json
{
  "operational": {
    "games": { "totalGames": 1240, "byStatus": { "FINISHED": 800, "CANCELLED": 100, "IN_PROGRESS": 340 } },
    "players": { "uniquePlayers": 542, "activePlayers": 320, "distributionByTenant": { "tenant-1": 200 } },
    "questions": { "totalQuestions": 5400, "byCategory": { "Historia": 1200 }, "byLevel": { "1": 1000 } },
    "categories": { "totalCategories": 12, "categoriesInUse": 10, "questionsPerCategory": { "Historia": 450 } }
  },
  "totalCount": 1240,
  "calculatedAt": "2026-05-13T10:00:00Z"
}
```

**Errores** `400 InvalidFilter` con `errors.from`/`errors.level`/`errors.result` si `from>to` o `level` fuera 1–5 o `result` fuera de catálogo.

## 3. Performance — GET /bff/reports/performance

**Response 200**

```json
{
  "performance": {
    "answers": { "totalAnswers": 45200, "correctAnswers": 31200, "incorrectAnswers": 14000, "accuracyRate": 0.69 },
    "scores": { "totalPoints": 1250000, "averageScore": 245.5, "distribution": { "0-100": 20 }, "byTransactionType": { "ANSWER_CORRECT": 800 } },
    "withdrawals": { "totalWithdrawals": 320, "byPolicy": { "LOSE_ALL": 200 }, "rate": 0.08 }
  },
  "totalCount": 45200,
  "calculatedAt": "2026-05-13T10:00:00Z"
}
```

Scores reconstruidos desde `PointTransaction` ledger (D).

## 4. Rewards — GET /bff/reports/rewards

**Response 200**

```json
{
  "rewards": {
    "rewards": { "totalRewards": 120, "byType": { "Voucher": 40 }, "byStatus": { "Active": 80 } },
    "redemptions": { "totalRedemptions": 340, "byStatus": { "Approved": 200 }, "byType": { "Voucher": 100 }, "totalCost": 34000 },
    "consolations": { "totalConsolations": 45, "totalCostConsolation": 4500, "byEligibility": { "eligible": 30 } }
  },
  "totalCount": 340,
  "calculatedAt": "2026-05-13T10:00:00Z"
}
```

`consolations` con `IsConsolation:true` separado, no contado en `rewards`.

## 5. Full — GET /bff/reports/full

Combina las 3 secciones en un snapshot con `calculatedAt`.

```json
{
  "operational": { "...": "..." },
  "performance": { "...": "..." },
  "rewards": { "...": "..." },
  "calculatedAt": "2026-05-13T10:00:00Z"
}
```

## 6. Paginación

Listas desglosadas (ej. `GET /bff/reports/operational` con `page`/`pageSize`) → `PagedResult`:

```json
{
  "items": [ { "gameId": "...", "category": "Historia", "level": 3 } ],
  "totalCount": 1240,
  "page": 1,
  "pageSize": 20,
  "totalPages": 62
}
```

No cargar colecciones completas (FR-011).

## 7. Errores

| Código | HTTP | Cuando |
|--------|------|--------|
| `InvalidFilter` | 400 | `from>to`, `level` 0/6, `result` fuera de catálogo |
| `CategoryNotFound` | 404/400 | `categoryId` no existe (según política) |
| `Unauthorized` | 401 | sesión expirada |
| `Forbidden` | 403 | `PLAYER` o `GAME_MANAGER` en `/rewards` etc. |
| `ProblemDetails` | RFC7807 | `type`, `title`, `status`, `detail`, `errors.{field}`, `traceId` |

Todos `application/problem+json` con `CorrelationId` propagado.

## 8. Contrato cliente (C#)

Vive en `QuizArena.Admin.Client/Services/IReportsService.cs` (existente, extender):

```csharp
public interface IReportsService
{
    Task<ReportSnapshot> GetOperationalAsync(ReportFilter filter, CancellationToken ct = default);
    Task<ReportSnapshot> GetPerformanceAsync(ReportFilter filter, CancellationToken ct = default);
    Task<ReportSnapshot> GetRewardsAsync(ReportFilter filter, CancellationToken ct = default);
    Task<ReportSnapshot> GetFullAsync(ReportFilter filter, CancellationToken ct = default);
    Task<PagedResult<ReportRow>> GetOperationalRowsAsync(ReportFilter filter, CancellationToken ct = default);
}
```

Implementaciones:
- `ClientReportsService` (WASM): `HttpClient.GetFromJsonAsync("/bff/reports/operational?...")` etc.
- `ServerReportsService` (InteractiveServer): `HttpClient.GetFromJsonAsync("http://oroclash-api/api/reports/operational?...")` con `Bearer`.

## 9. Validación de contrato

- `AdminBffTests` — no URLs absolutas en cliente, solo `/bff/*`
- `DesignSystemNoDirectDbTests` — sin `DbContext` en Admin
- `ReportsOperationalTests` — Games/Players/Questions con filtros 6 dimensiones, `From<=To`, `Level` 1–5, `IsConsolation` separado
