# Contract: Report Filters BFF

**Branch**: `025-admin-reporting` | **Date**: 2026-05-13

Contrato de filtros combinados (6 dimensiones) para reporting. Complementa `reports-bff.md`.

## 1. Filtros combinados — Query String

Todos los endpoints `GET /bff/reports/*` aceptan los mismos 6 filtros + paginación, combinados como `AND`:

```
?from=2026-04-01T00:00:00Z&to=2026-05-13T23:59:59Z&categoryId=...&categoryName=Historia&gameId=...&gameName=Quiz&playerId=...&playerSearch=ana&level=3&result=FINISHED&page=1&pageSize=20
```

| Parámetro | Tipo | Valores válidos | Vacío = |
|-----------|------|-----------------|---------|
| `from` | `DateTimeOffset` ISO8601 | `null` o `from<=to` | sin límite inferior |
| `to` | `DateTimeOffset` ISO8601 | `null` o `from<=to` | sin límite superior |
| `categoryId` | `Guid` | existente en `Category` | todas |
| `categoryName` | `string` 0–100 | búsqueda parcial case-insensitive | todas |
| `gameId` | `Guid` | existente en `Game` | todos |
| `gameName` | `string` 0–100 | búsqueda parcial | todos |
| `playerId` | `Guid` | `sub` | todos |
| `playerSearch` | `string` 0–100 | nombre/email parcial | todos |
| `level` | `int` | `1`..`5` | todos |
| `result` | `string` | `DRAFT`/`READY`/`WAITING_FOR_PLAYERS`/`IN_PROGRESS`/`ROUND_IN_PROGRESS`/`ROUND_COMPLETED`/`FINISHED`/`CANCELLED`/`FORCED_FINISHED`/`JOINED`/`WITHDRAWN`/`Approved`/`Rejected`/`Correct`/`Incorrect` según métrica | todos |
| `page` | `int` | `>=1` | `1` |
| `pageSize` | `int` | `1`..`100` | `20` |

**Validación**: `from>to` → `400 InvalidFilter` con `errors.from`/`errors.to`; `level` 0/6 → `400` con `errors.level`; `result` fuera de catálogo → `400` con `errors.result`; sin petición si inválido (FR-013).

## 2. Contrato cliente (C#) — ReportFilter

Vive en `QuizArena.Admin.Client/Models/Reports/ReportFilter.cs`:

```csharp
public sealed record ReportFilter(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    Guid? CategoryId = null,
    string? CategoryName = null,
    Guid? GameId = null,
    string? GameName = null,
    Guid? PlayerId = null,
    string? PlayerSearch = null,
    int? Level = null,
    string? Result = null,
    int Page = 1,
    int PageSize = 20)
{
    public IReadOnlyDictionary<string,string[]> Validate()
    {
        var errors = new Dictionary<string,string[]>();
        if (From.HasValue && To.HasValue && From.Value > To.Value)
            errors["DateRange"] = ["Desde debe ser ≤ Hasta."];
        if (Level is <1 or >5)
            errors[nameof(Level)] = ["Nivel debe estar entre 1 y 5."];
        if (Result is not null && !ReportCatalogs.IsValidResult(Result))
            errors[nameof(Result)] = ["Resultado no válido."];
        return errors;
    }
}
```

`ReportCatalogs.IsValidResult` valida contra catálogos por métrica.

## 3. Ejemplos de uso

**Operativo filtrado**:
```
GET /bff/reports/operational?from=2026-04-01T00:00:00Z&to=2026-05-13T23:59:59Z&categoryName=Historia&level=3&page=1&pageSize=20
→ 200 con `operational.games.byStatus.FINISHED = 120`
```

**Rendimiento por jugador**:
```
GET /bff/reports/performance?playerSearch=ana&level=3&result=Correct&page=1
→ 200 con `performance.answers.correctAnswers = 450`
```

**Recompensas por jugador con IsConsolation**:
```
GET /bff/reports/rewards?playerId={sub}&result=Approved&page=1
→ 200 con `rewards.redemptions.byStatus.Approved = 12` y `consolations.totalConsolations = 3` separado
```

## 4. Validación de contrato

- `ReportsOperationalTests` — `From<=To` sin petición si inválido
- `ReportsRewardsTests` — `Level` 1–5 y `Result` catálogo cerrado, 403 por rol
