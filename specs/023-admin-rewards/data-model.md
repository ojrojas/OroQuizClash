# Data Model: Admin Rewards

**Branch**: `023-admin-rewards` | **Date**: 2026-08-28 | **Plan**: [plan.md](plan.md)

Modelo de datos **frontend** (proyecciones/DTOs) para premios y canjes. Sin persistencia propia: estructuras en `QuizArena.Admin.Client/Models/Rewards/` que reflejan contratos `oroclash-api /api/rewards*`/`/api/redemptions*` (SPEC-009). Autoridad permanece en backend (Constitución V).

## 1. Entidades principales

### Reward (Catálogo)

Agregado de catálogo. Inmutable tras `Archived`.

```csharp
enum RewardType
{
    Monetary,
    Physical,
    Digital,
    Voucher,
    Experience,
    Consolation
}

enum RewardStateView
{
    Active,     // elegible si stock y fechas lo permiten
    Inactive,   // no elegible para nuevos canjes
    Archived    // terminal, solo lectura
}

record Reward(
    Guid RewardId,
    string Name,                    // 3–100, único case-insensitive entre no archivados
    string? Description,            // 0–500
    RewardType Type,                // 6 valores cerrados
    int Cost,                       // 1–100000
    int Stock,                      // ≥0, 0 = ilimitado según política
    DateTimeOffset? AvailableFrom,  // opcional
    DateTimeOffset? AvailableTo,    // opcional, From < To si ambos
    RewardStateView Status,
    bool IsEligible,                // derivado: Active && (Stock==0?ilimitado:Stock>0) && (now ∈ [From,To] si se definen)
    string RowVersion               // base64 rowversion
);
```

**Invariantes**:
- `Name` único case-insensitive donde `Status != Archived`.
- `Cost ∈ [1,100000]`, `Stock ≥0`.
- `AvailableFrom < AvailableTo` si ambos se definen.
- `Type ∈ {6}`.
- `IsEligible` derivado, no persistido.

### RewardForm (validación 3 niveles espejo dominio)

```csharp
record RewardForm(
    string Name,
    string? Description,
    RewardType Type,
    int Cost,
    int Stock,
    DateTimeOffset? AvailableFrom,
    DateTimeOffset? AvailableTo);

IReadOnlyDictionary<string,string[]> Validate() // por campo
```

Validación: `Name 3–100`, `Description 0–500`, `Type ∈ {6}`, `Cost 1–100000`, `Stock ≥0`, `AvailableFrom < AvailableTo` si ambos.

### RewardSummary (listado paginado)

```csharp
record RewardSummary(
    Guid Id,
    string Name,
    RewardType Type,
    int Cost,
    int Stock,
    RewardStateView Status,
    bool IsEligible,
    string RowVersion);
```

### RewardDetail (detalle + historial)

```csharp
record RewardDetail : RewardSummary
{
    string? Description;
    DateTimeOffset? AvailableFrom;
    DateTimeOffset? AvailableTo;
    IReadOnlyList<RewardStateTransition> History;
}
```

### RewardStateTransition / Audit

```csharp
record RewardStateTransition(
    RewardStateView From,
    RewardStateView To,
    DateTimeOffset Timestamp,
    string ActorId,
    string? Reason);

record RewardAuditEntry(
    Guid RewardId,
    string ActorId, // sub
    DateTimeOffset Timestamp,
    RewardStateView FromState,
    RewardStateView ToState,
    string Action, // Created/Updated/Activated/Deactivated/Archived
    string? Reason,
    string CorrelationId,
    string Result,
    string IdempotencyKey);
```

### RewardRedemption (Canje)

Entidad de canje. Parte del agregado `Reward` o independiente según 009.

```csharp
enum RedemptionStateView
{
    Requested,  // solicitado por jugador
    Approved,   // aprobado por operador, descuenta stock/puntos
    Rejected,   // rechazado con motivo
    Delivered,  // entregado
    Cancelled   // cancelado
}

record RewardRedemption(
    Guid RedemptionId,
    Guid RewardId,
    string RewardName,              // denormalizado
    RewardType RewardType,
    Guid PlayerId,                  // sub
    string PlayerName,
    int Cost,                       // snapshot del costo al solicitar
    RedemptionStateView Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? RejectedAt,
    DateTimeOffset? DeliveredAt,
    string? Reason,                 // para Rejected/Cancelled
    bool IsConsolation,             // true si es canje de consolación (tipo Consolation)
    string RowVersion);

record RedemptionStateTransition(
    RedemptionStateView From,
    RedemptionStateView To,
    DateTimeOffset Timestamp,
    string ActorId,
    string? Reason);
```

**Invariantes**:
- `Requested → Approved → Delivered` y `Requested → Rejected`, `Requested`/`Approved` → `Cancelled`.
- `Approved` descuenta `Stock` (si limitado) y `PointTransaction` de forma transaccional.
- `IsConsolation` solo true si `RewardType == Consolation` y jugador es elegible via `ConsolationEligibility`.

### RedemptionFilter (búsqueda paginada)

```csharp
record RedemptionFilter(
    RedemptionStateView? Status = null,
    RewardType? Type = null,
    Guid? PlayerId = null,
    string? Search = null, // reward name / player name
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 20);
```

## 2. DTOs de transporte (BFF boundary)

```csharp
record CreateRewardRequest(
    string Name,
    string? Description,
    RewardType Type,
    int Cost,
    int Stock,
    DateTimeOffset? AvailableFrom,
    DateTimeOffset? AvailableTo);

record UpdateRewardRequest : CreateRewardRequest
{
    string RowVersion; // If-Match
}

record RewardResponse : RewardDetail; // camelCase JSON

record CreateRedemptionRequest(
    Guid RewardId,
    Guid PlayerId); // Cost snapshot lo pone el servidor

record ApproveRedemptionRequest(string RowVersion, string IdempotencyKey);
record RejectRedemptionRequest(string RowVersion, string IdempotencyKey, string Reason);
record DeliverRedemptionRequest(string RowVersion, string IdempotencyKey);
record CancelRedemptionRequest(string RowVersion, string IdempotencyKey, string? Reason);

record RewardFilter(
    RewardType? Type = null,
    RewardStateView? Status = null,
    string? Search = null,
    bool? OnlyEligible = null,
    int Page = 1,
    int PageSize = 20);
```

Paginación: `PagedResult<RewardSummary> { Items, TotalCount, Page, PageSize }` y `PagedResult<RewardRedemption>`.

## 3. Catálogos estáticos

```csharp
static class RewardCatalogs
{
    static IReadOnlyList<string> Types => ["Monetary","Physical","Digital","Voucher","Experience","Consolation"];
    static IReadOnlyList<string> Statuses => ["Active","Inactive","Archived"];
    static IReadOnlyList<string> RedemptionStatuses => ["Requested","Approved","Rejected","Delivered","Cancelled"];
}
```

## 4. Validación por niveles

- **API**: tipos/rangos → `400` + `FieldErrors` por campo.
- **Aplicación**: `Validator` — unicidad nombre, costo 1–100000, stock ≥0, fechas `From<To`, tipo en catálogo, `RowVersion`, `CategoryInUse` no aplica aquí pero `RewardInUse` para archivar con canjes `Approved` sin entregar.
- **Dominio**: invariantes `RewardAlreadyExists`, `RewardOutOfStock`, `RewardInUse`, `InvalidRedemptionState`, `InsufficientPoints`, `ConcurrencyConflict`.

## 5. Relaciones

```text
Reward ── contiene 1 ──> RewardType (6 valores)
Reward ── contiene 1 ──> RewardStateView (3 estados)
Reward ── referencia 0..N ──> RewardRedemption (via RewardId)
Reward ── contiene N ──> RewardStateTransition / RewardAuditEntry
RewardRedemption ── referencia 1 ──> Reward (FK)
RewardRedemption ── referencia 1 ──> Player (sub)
RewardSummary ── deriva ──> PagedResult (listado filtrado por type/status/availability/search)
```

## 6. Transiciones de estado

```text
Reward: Inactive ↔ Active → Archived
  Inactive → Active [guard: costo/tipo válido, fechas From<To]
  Active → Inactive [sin guarda]
  Active/Inactive → Archived [guard: sin canjes Approved sin entregar si política lo exige]

Redemption:
  Requested → Approved [guard: Stock (si limitado) y InsufficientPoints]
  Requested → Rejected [con Reason]
  Approved → Delivered [guard: solo si Approved]
  Requested/Approved → Cancelled [con Reason]

Inválidas → InvalidRedemptionState/InvalidRewardState, sin mutación parcial, protegidas por rowversion + IdempotencyKey.
```

## 7. Reglas de autorización (proyección)

- `ADMIN`/`REWARD_MANAGER` (`RewardManagerOrAdmin`) → `Create/Update/Activate/Deactivate/Archive` y `Approve/Reject/Deliver/Cancel`.
- `GAME_MANAGER` → `403 Access Denied` en `Rewards` y `Redemptions`.
