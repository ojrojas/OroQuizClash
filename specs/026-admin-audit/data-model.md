# Data Model: Admin Audit

**Branch**: `026-admin-audit` | **Date**: 2026-05-13 | **Plan**: [plan.md](plan.md)

Modelo de datos **frontend** (proyecciones/DTOs) para trazabilidad. Sin persistencia propia: estructuras en `QuizArena.Admin.Client/Models/Audit/` que reflejan contratos `oroclash-api /api/audit*` (`AuditEntry` append-only de 014). Autoridad permanece en backend (Constitución V/I).

## 1. Entidades principales

### AuditEntry (vista listado)

```csharp
record AuditEntry(
    Guid AuditId,
    WhoView Who,                 // Who
    string What,                 // What
    DateTimeOffset When,         // When
    WhereView Where,             // Where
    EntityView Entity,           // Entity
    string? PreviousValue,       // Previous Value (JSON o null)
    string? NewValue,            // New Value (JSON)
    string Action,               // Action
    ResultView Result);           // Result

record WhoView(
    string ActorId,              // sub
    string DisplayName,
    string Email,
    string? TenantId);

record WhereView(
    string Service,              // ej. "oroclash-api"
    string Endpoint,             // ej. "POST /api/categories"
    string? IpAddress,
    string CorrelationId,        // X-Correlation-Id
    string? TraceId);            // TraceId OTel

record EntityView(
    string EntityType,           // Game, Category, Question, GamePlayer, Reward, RewardRedemption, Player
    Guid EntityId);

record ResultView(
    string Status,               // Success | Failed
    string? ErrorCode,           // ej. ConcurrencyConflict
    string? Detail);
```

**Invariantes**:
- `AuditId` único, inmutable.
- `When` UTC, `Where.CorrelationId` propagado desde `HttpContext`.
- `PreviousValue` `null` para `CREATE`, `NewValue` `null` para `DELETE`.
- `Result.Status ∈ {Success, Failed}`.

### AuditFilter (9 filtros)

```csharp
record AuditFilter(
    string? Who = null,          // sub o búsqueda parcial DisplayName/email
    string? What = null,         // búsqueda parcial descripción
    DateTimeOffset? WhenFrom = null,
    DateTimeOffset? WhenTo = null, // WhenFrom <= WhenTo si ambos
    string? Where = null,        // búsqueda parcial servicio/endpoint/CorrelationId
    string? EntityType = null,   // 7 tipos
    Guid? EntityId = null,
    string? Action = null,       // catálogo cerrado
    string? Result = null,       // Success | Failed
    string? ErrorCode = null,
    int Page = 1,
    int PageSize = 20);

enum AuditAction
{
    CREATE, UPDATE, DELETE,
    ACTIVATE, DEACTIVATE, ARCHIVE,
    APPROVE, REJECT, DELIVER, CANCEL,
    START, FINISH, JOIN, WITHDRAW
}

enum AuditResult { Success, Failed }
```

**Invariantes**:
- `WhenFrom <= WhenTo` si ambos.
- `Action ∈ catálogo cerrado` si se especifica.
- `Result ∈ {Success, Failed}`.
- `Page >=1`, `PageSize 1–100`.

### AuditDetail (detalle con diff)

```csharp
record AuditDetail : AuditEntry
{
    IReadOnlyList<JsonDiffEntry> Diff; // diff Previous vs New
}

record JsonDiffEntry(
    string Path,                 // ej. "$.Name"
    string? Previous,
    string? New,
    string ChangeType);          // Added, Removed, Modified
```

**Invariantes**:
- `Diff` calculado server-side o cliente a partir de `PreviousValue`/`NewValue` JSON.
- Truncado si >10KB con `IsTruncated=true` y botón “Ver JSON completo”.
- Campos sensibles enmascarados (`password`, `secret` → `***`).

### AuditViewAudit (auditoría de consultas, opcional)

```csharp
record AuditViewAudit(
    Guid ViewId,
    string ActorId,
    AuditFilter Filters,
    DateTimeOffset Timestamp,
    string CorrelationId);
```

No muta `AuditEntry`; solo lectura del trail.

## 2. DTOs de transporte (BFF boundary)

```csharp
record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

record GetAuditRequest(
    string? Who,
    string? What,
    DateTimeOffset? WhenFrom,
    DateTimeOffset? WhenTo,
    string? Where,
    string? EntityType,
    Guid? EntityId,
    string? Action,
    string? Result,
    int Page,
    int PageSize);

record GetAuditDetailRequest(Guid AuditId);
```

Paginación: `PagedResult<AuditEntry>` con `TotalCount`/`Page`/`PageSize`.

## 3. Catálogos estáticos

```csharp
static class AuditCatalogs
{
    static IReadOnlyList<string> EntityTypes => ["Game","Category","Question","GamePlayer","Reward","RewardRedemption","Player"];
    static IReadOnlyList<string> Actions => ["CREATE","UPDATE","DELETE","ACTIVATE","DEACTIVATE","ARCHIVE","APPROVE","REJECT","DELIVER","CANCEL","START","FINISH","JOIN","WITHDRAW"];
    static IReadOnlyList<string> Results => ["Success","Failed"];
    static IReadOnlyList<string> ErrorCodes => ["ConcurrencyConflict","RewardAlreadyExists","InvalidFilter","CategoryNotReady","RewardOutOfStock"];
}
```

## 4. Validación por niveles

- **API**: tipos/rangos → `400` + `FieldErrors` por campo; `Page` 1..N, `WhenFrom<=WhenTo`, `Action`/`Result`/`EntityType` en catálogo.
- **Aplicación**: `Validator` — `EntityId` existe si se filtra por él (`AuditEntryNotFound` → 404 o 200 vacío según política), filtros combinados coherentes, paginación.
- **Dominio**: invariantes `AuditEntry` append-only, sin edición; `Previous`/`New` inmutables; `Who`/`When` requeridos.

## 5. Relaciones

```text
AuditEntry ── 1:1 ──> WhoView (Actor sub)
AuditEntry ── 1:1 ──> WhereView (CorrelationId/TraceId)
AuditEntry ── 1:1 ──> EntityView (EntityType + EntityId)
AuditEntry ── 1:1 ──> ResultView (Success/Failed + ErrorCode)
AuditFilter ── filtra ──> AuditEntry (AND 9 campos)
AuditDetail ── extiende ──> AuditEntry + Diff (Previous vs New)
AuditViewAudit ── referencia ──> AuditFilter + WhoView (auditoría de consultas)
PagedResult<AuditEntry> ── pagina ──> AuditEntry
```

## 6. Reglas de autorización (proyección)

- `ADMIN` → `GET /bff/audit` + `GET /bff/audit/{id}` con 9 filtros + todas las entidades (7 tipos)
- `GAME_MANAGER` → `Entity ∈ {Game, Category, Question, GamePlayer}` con 9 filtros; `Reward`/`RewardRedemption` → 403
- `REWARD_MANAGER` → `Entity ∈ {Reward, RewardRedemption}` con 9 filtros; `Game`/`Category` → 403
- `PLAYER` → 403 en todas las rutas `/bff/audit*`
