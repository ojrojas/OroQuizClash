# Data Model: Admin Dashboard

**Branch**: `018-admin-dashboard` | **Date**: 2026-08-28 | **Plan**: [plan.md](plan.md)

Modelo de datos del **frontend** (proyecciones/DTOs) para el dashboard. Sin persistencia: estructuras en `QuizArena.Admin.Client/Models/Dashboard/` que reflejan el contrato `GET /bff/dashboard/snapshot` (BFF → `QuizArena.Api`). Autoridad permanece en el backend (Constitución V).

## 1. Entidades principales

### DashboardSnapshot

Agregado de lectura que compone los 10 indicadores en un instante. Inmutable (record).

```csharp
record DashboardSnapshot(
    DateTimeOffset GeneratedAt,          // timestamp UTC del snapshot
    string? CorrelationId,               // propagado desde BFF (header X-Correlation-Id)
    IReadOnlyList<MetricValue> Metrics,  // exactamente 10 entradas (ver MetricId)
    GeneralStatistics Statistics          // sub-bloque de Estadísticas generales
);
```

**Invariantes**:
- `Metrics.Count == 10` (uno por cada `MetricId`).
- `GeneratedAt` en UTC; la UI lo localiza (`ToLocalTime()`).
- `CorrelationId` opcional, solo para diagnóstico (no se muestra al operador).

**Estados por bloque**: cada `MetricValue` lleva su propio `MetricState`.

### MetricValue

```csharp
enum MetricId
{
    ActiveGames, ScheduledGames, FinishedGames,
    ConnectedPlayers, ActivePlayers,
    AvailableQuestions, Categories,
    Rewards, Redemptions,
    GeneralStatistics
}

enum MetricState { Loading, Ready, Empty, Error }

record MetricValue(
    MetricId Id,
    string Label,                 // español: "Juegos activos", etc. (FR-009)
    int Count,                    // >=0; 0 → Empty si no hay datos
    MetricState State,
    string? ErrorCode,            // código de negocio del API (p. ej. RewardsUnavailable) si State==Error
    string? ErrorMessage,         // mensaje accionable (nunca stack trace) (FR-018)
    string? SourceLabel,          // fuente aproximada para tooltip (FR-003) ej. "SignalR presence"
    string? Tooltip,              // explicación cuando es aproximación (FR-003)
    bool Retryable,               // true si State==Error y reintento aislado permitido
    string? DrillDownRoute        // ruta de navegación (ver R4) — null si no autorizado
);
```

**Validación**:
- `Count >= 0`.
- `State == Empty` ↔ `Count == 0 && ErrorCode == null`.
- `State == Error` ↔ `ErrorCode != null || ErrorMessage != null`.
- `Retryable` solo true si `State == Error`.

**Derivación de conteos (server-side, research R1/R2)**:
- `ActiveGames`: estados `IN_PROGRESS`, `ROUND_IN_PROGRESS`, `ROUND_COMPLETED`.
- `ScheduledGames`: `READY`, `WAITING_FOR_PLAYERS` / fecha futura.
- `FinishedGames`: `FINISHED`, `FORCED_FINISHED`, `CANCELLED`.
- `ConnectedPlayers`: presencia online (SignalR / lastSeen); `ActivePlayers`: `PLAYING` en `IN_PROGRESS`.
- `AvailableQuestions`: `Question.Status == Active/Published` (4 opciones, 1 correcta).
- `Categories`: `Category.Status == Active`.
- `Rewards`: `Reward.Status == Active`.
- `Redemptions`: `RewardRedemption.Status == Pending` (o totales si definición operativa distinta — `SourceLabel` lo aclara).
- `GeneralStatistics`: agregado (ver abajo).

### GeneralStatistics

```csharp
record GeneralStatistics(
    int TotalGames,                 // total de juegos creados (todos los estados)
    int TotalParticipations,        // participaciones / jugadores registrados (ver fuente)
    double AvgQuestionsPerCategory, // preguntasActivas / categoríasActivas (0 si denominador 0)
    IReadOnlyList<StatisticBreakdown>? Breakdown  // extensible post-MVP
);

record StatisticBreakdown(string Key, string Label, string Value);
```

**Invariantes**: `TotalGames >=0`, `TotalParticipations >=0`, `AvgQuestionsPerCategory >=0`.

### QuickAction

Catálogo estático tipado (no DTO de API). Vive en `QuizArena.Admin.Client/Services/QuickActionsCatalog.cs`.

```csharp
enum QuickActionId
{
    CreateGame, ConfigureGame, ManageQuestions,
    ViewActiveGames, ViewPlayers, ManageRewards, ViewReports
}

record QuickAction(
    QuickActionId Id,
    string Label,                 // español
    string Description,           // corta (≤60ch) p.ej. "Crear un nuevo juego"
    string Icon,                  // Lucide icon name: "Plus", "Settings2", etc. (FR-012)
    string Route,                 // destino con query (FR-010)
    IReadOnlyList<AdminRole> AllowedRoles
);

enum AdminRole { Admin, GameManager, RewardManager }
```

Validados contra `AdminRoles` de `QuizArena.Admin` (Program.cs).

## 2. Estado de UI (view-state, no persistido)

### MetricTileState

```csharp
record MetricTileViewState(MetricValue Value, bool IsRetrying, DateTimeOffset? LastUpdated);
```

- `IsRetrying` controla spinner en botón Reintentar aislado.
- `aria-live="polite"` cuando `Value.State` cambia de `Loading→Ready/Error`.

### DashboardViewState

```csharp
record DashboardViewState(
    DashboardSnapshot? Snapshot,
    bool IsRefreshing,            // refresh global en curso
    bool AutoRefreshEnabled,      // toggle 30-60s (FR-008 opcional)
    DateTimeOffset? LastRefreshAt,
    bool SessionExpired           // true si último fetch 401 → banner re-autenticar
);
```

## 3. Relaciones

```text
DashboardSnapshot ── contiene 10 ──> MetricValue (1 por MetricId)
MetricValue (GeneralStatistics) ── contiene ──> GeneralStatistics
GeneralStatistics ── contiene N ──> StatisticBreakdown
DashboardViewState ── envuelve ──> DashboardSnapshot
QuickAction ── independiente (catálogo estático) ──> filtrado por AdminUserState.Roles
MetricValue.DrillDownRoute ── referencia ──> ruta de listado existente (Games/Players/Questions...)
```

## 4. Validación y errores transversales

- **ApiErrorView** reutilizado de 017 (`Error.Code`, `Title`, `Detail`, `FieldErrors` desde `ProblemDetails` RFC 7807); `MetricValue.ErrorCode/ErrorMessage` mapean desde él (FR-018).
- **Paginación**: no aplica al snapshot (agregados); listados destino usan `PagedResult<T>` existente.
- **Fechas**: `DateTimeOffset` UTC; UI localiza.
- **Inmutabilidad**: todos los records `init`-only, serializables JSON camelCase (`System.Text.Json`).

## 5. Reglas de autorización (proyección)

- **MetricValue.DrillDownRoute == null** si `AdminUserState` no tiene claim para el destino (FR-014); la tarjeta se renderiza no-clicable con mensaje "Sin permiso".
- **QuickAction.AllowedRoles** filtra `QuickActionGrid`; items filtrados ocultos o `aria-disabled` + reason (FR-011). La autoridad real la impone el destino con `[Authorize(Policy=...)]` (403).

## 6. Transiciones de estado

```text
MetricState: Loading ── success+count>0 ──> Ready
             Loading ── success+count==0 ──> Empty
             Loading ── failure ──> Error (Retryable=true)
             Error ── retry ──> Loading
             Ready/Empty ── refresh ──> Loading

DashboardViewState: Snapshot=null ── fetch ──> Snapshot (10 métricas, mixto Ready/Empty/Error)
                    SessionExpired=false ── 401 ──> true (detiene auto-refresh)
```

No hay máquina de dominio aquí; son estados de presentación derivados de `research R6`.
