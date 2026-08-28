# Research: Admin Rewards

**Branch**: `023-admin-rewards` | **Date**: 2026-08-28 | **Plan**: [plan.md](plan.md)

Todos los NEEDS CLARIFICATION resueltos. El feature reutiliza agregados y slices de `009-reward-redemption` y patrón BFF/OIDC/Design System de 017–022; esta fase cierra las incógnitas propias de 023.

---

## R1. Catálogo cerrado de 6 tipos (Constitución C)

**Decision**: 6 tipos `RewardType` como enumeración cerrada en dominio y UI: `Monetary`, `Physical`, `Digital`, `Voucher`, `Experience`, `Consolation`.

- **Dominio** (`OroQuizClash.Domain/Rewards/RewardType.cs`): `Enumeration` con `FromName`/`ToName` y validación `RewardTypeMustBeValid`.
- **Aplicación**: `CreateRewardValidator`/`UpdateRewardValidator` (FluentValidation) valida `Type ∈ {6}` antes de tocar dominio.
- **API**: `400 InvalidRewardData` con `errors.type` si fuera de catálogo.
- **UI**: `RewardForm.razor` con `QuizSelect` poblado desde `RewardCatalogs.Types` (label español, value canónico). Guardar fuera de catálogo → `400`.

**Rationale**: El spec lista 6 tipos explícitos; tratarlos como texto libre permitiría valores inválidos y rompería la distinción `Consolation` independiente (Constitución C).

**Alternatives considered**:
- Texto libre con validación regex: rechazado — no garantiza catálogo cerrado.
- Catálogo dinámico desde backend: rechazado — tipos son invariantes de dominio, no datos.

---

## R2. Modelo de 7 campos de catálogo — reutilización de `Reward` existente

**Decision**: Reutilizar `OroQuizClash.Domain/Rewards/Reward.cs` (ya implementado en `009`) y su DTO de Admin `QuizArena.Admin.Client/Models/Rewards/Reward.cs` (existente, usado en `Pages/Rewards.razor` de 017) y extenderlo con campos faltantes para cubrir los 7 del spec:

| Campo spec | Propiedad en `Reward`/`RewardForm` | Detalle |
|------------|------------------------------------|---------|
| Nombre | `Name` | 3–100, único case-insensitive entre no archivados |
| Descripción | `Description` | 0–500 |
| Tipo | `Type` | 6 valores cerrados |
| Costo en puntos | `Cost` | 1–100000 |
| Disponibilidad | `AvailableFrom`/`AvailableTo` | opcionales, `From < To` si ambos |
| Inventario | `Stock` | ≥0, 0 = ilimitado según política |
| Estado | `Status` | `Active`/`Inactive`/`Archived` |

Campos nuevos (`AvailableFrom`/`To`, `Stock` ya existía como `Stock`/`Inventory`) se añaden como propiedades opcionales con validación en `RewardForm.Validate()` y se espejan en validador de aplicación y en invariantes de dominio (`Reward.Update`).

**Rationale**: 009 ya implementa `Reward` como Aggregate con `Stock` y `Cost` y `RowVersion`; crear un segundo modelo duplicaría invariantes (unicidad, `RewardOutOfStock`).

**Alternatives considered**:
- Crear nuevo DTO `AdminReward` separado: rechazado — sincronización frágil.
- Mantener 009 sin cambios y mapear disponibilidad en UI: rechazado — deja campos huérfanos sin validación de dominio.

---

## R3. Inventario `Stock` 0 = ilimitado vs limitado según tipo y disponibilidad temporal

**Decision**:
- **Stock**: `Stock ≥0`, donde `0` significa **ilimitado** para tipos `Digital`/`Voucher`/`Experience`/`Consolation` (según política) y **sin stock** para `Physical`/`Monetary` si la política lo define así. La política se documenta en UI con tooltip: "Stock 0 = ilimitado para Digital/Voucher/Consolation; para Physical/Monetary, 0 = sin stock".
- **Disponibilidad**: `AvailableFrom`/`AvailableTo` opcionales; sin fechas, siempre disponible si `Active` y stock. Con fechas, elegible solo si `now ∈ [From, To]`.
- **Elegible**: `Status == Active && (Stock == 0 ? ilimitado : Stock >0) && (now ∈ [From,To] si se definen)`. `Active` no garantiza elegible si `Stock==0` para `Physical` o fuera de fechas.
- **Validación**: `Stock <0` → `400 InvalidRewardData` con `errors.stock`; `From ≥ To` → `400` con `errors.availability`.

**Rationale**: El spec dice "Definir disponibilidad. Definir inventario. Definir tipo de premio." sin especificar si stock 0 es ilimitado o sin stock. La investigación de `009` y el uso real (Digital ilimitado, Physical limitado) sugiere la distinción por tipo documentada en UI es la más flexible y compatible con categorías existentes.

**Alternatives considered**:
- Stock 0 siempre = sin stock: rechazado — haría imposible premios Digital ilimitados sin stock infinito.
- Stock 0 siempre = ilimitado: rechazado — haría imposible representar Physical sin stock.

---

## R4. Ciclo de vida de canjes `Requested → Approved/Rejected → Delivered/Cancelled` (Constitución C/D)

**Decision**: 5 estados de canje `RewardRedemption` ya existentes en `009` ( `Requested`, `Approved`, `Rejected`, `Delivered`, `Cancelled` ) se reutilizan sin cambios:

- `Requested → Approved` (`ApproveRedemption`) requiere `Stock` (si limitado) y `InsufficientPoints` check vs `PointTransaction` ledger; descuenta `Stock` y `Score` de forma transaccional; genera `Stock 5→4` y `PointTransaction` `REWARD_REDEMPTION`.
- `Requested → Rejected` (`RejectRedemption`) con `Reason` requerido, no descuenta stock/puntos (o reembolsa si se había reservado según política de `009` — documentado en UI).
- `Approved → Delivered` (`DeliverRedemption`) con `DeliveredAt`/`DeliveredBy`; `Delivered`/`Rejected`/`Cancelled` son terminales.
- `Requested`/`Approved` → `Cancelled` con motivo.
- Toda transición inválida → `409 InvalidRedemptionState` sin mutación parcial, protegida por `rowversion` + `IdempotencyKey`.

**Rationale**: Constitución C exige lifecycle `REQUESTED→APPROVED→REJECTED→DELIVERED→CANCELLED` y D exige ledger; reusar 009 evita duplicar máquina y garantiza que `Approve` descuente stock/puntos de forma transaccional.

**Alternatives considered**:
- Crear nueva máquina de 4 estados paralela en Admin: rechazado — duplica invariantes.
- Permitir `Active` sin stock: rechazado — viola `RewardOutOfStock`.

---

## R5. BFF, auditoría y paginación

**Decision**:
- **BFF**: `ClientRewardsService` → `HttpClient.BaseAddress = HostEnvironment.BaseAddress` → rutas `/bff/rewards*` y `ClientRedemptionsService` → `/bff/redemptions*` (cookie viaja); `ServerRewardsService`/`ServerRedemptionsService` → `http://oroclash-api/api/rewards*`/`/api/redemptions*` con `Bearer` del `HttpContext`. Forwarder catch-all `/bff/{**catch-all}` → `/api/{**catch-all}` ya existe (017) y cubre `POST /bff/rewards`, `PUT /bff/rewards/{id}`, `POST /bff/rewards/{id}/activate|deactivate`, `GET /bff/rewards?status=&type=&search=&page=&pageSize=`, `GET /bff/redemptions?status=&type=&player=&from=&to=&page=`, `POST /bff/redemptions/{id}/approve|reject|deliver|cancel`.
- **Auditoría**: append-only via Outbox (`RewardAuditEntry`/`RedemptionAuditEntry`) en `SaveChanges` (Constitución I). Cada creación/edición/activación y cada transición de canje persiste `RewardId`/`RedemptionId`/`ActorId`/`Timestamp`/`From`/`To`/`ChangedFields`/`CorrelationId`.
- **Listado**: `RewardsList.razor` y `RedemptionsList.razor` consumen `GET /bff/rewards` y `GET /bff/redemptions` paginados (`PagedResult`), filtros por `type`, `status`, `disponibilidad`, `search`, `player`, `from`/`to`, con paginación y skeleton.

**Rationale**: Reutiliza patrón BFF de 017 (sample `BlazorWebAppOidcBffAutoYarpAspire`), evita exponer `access_token` y preserva `CorrelationId` (FR-017).

**Alternatives considered**:
- Llamar WASM → API directo: rechazado — expone JWT.
- Listado sin paginación: rechazado — no escala con ≥50 premios.

---

## R6. Consistencia `Cost` vs `PointTransaction` y `Stock` vs `Inventory` (Constitución D)

**Decision**:
- **Costo**: `Cost` se valida contra `PointTransaction` ledger (`GET /api/players/{id}/score` o `GET /api/rewards/{id}/can-afford`) al intentar canjear; si `InsufficientPoints`, el canje no se crea en `Requested` o se crea y se rechaza inmediatamente con `InsufficientPoints` según política de `009` (documentado en UI: "Rechazado — puntos insuficientes").
- **Stock**: `Stock` (si limitado) se descuenta en `Approve` de forma transaccional (`Reward.Stock--` + `PointTransaction` `REWARD_REDEMPTION` + `Redemption.Status=Approved` en misma transacción Outbox). `Stock` no se descuenta en `Requested` (solo reserva si la política lo exige, documentado con tooltip).
- **Disponibilidad**: `AvailableFrom`/`To` se valida en `CanRedeem` antes de crear `Requested`; si fuera de fechas, `RewardUnavailable`.

**Rationale**: Constitución D exige ledger y que `Cost` se descuente desde `PointTransaction`, no mutación directa. Reusar 009 garantiza atomicidad.

**Alternatives considered**:
- Descontar puntos en `Requested`: rechazado — si luego se rechaza, hay que reembolsar y complica el ledger.
- No validar stock hasta `Delivered`: rechazado — permitiría `Approved` sin stock.

---

## Consolidated Decisions Summary

| # | Decisión | Fuente |
|---|----------|--------|
| 1 | Catálogo cerrado 6 tipos `RewardType` | FR-001, Constitución C |
| 2 | Reutilizar `Reward`/`RewardForm` existente con 7 campos | FR-001..005, 009 |
| 3 | `Stock` 0 = ilimitado vs limitado según tipo + `AvailableFrom`/`To` opcionales | FR-002/003 |
| 4 | Reutilizar máquina `Requested→Approved→Delivered` de 009 con `RowVersion`/`IdempotencyKey` | FR-007..009 |
| 5 | BFF catch-all + auditoría Outbox + paginación | FR-015..021 |
| 6 | `Cost` vs `PointTransaction` ledger + `Stock` transaccional | FR-010/012, Constitución D |

Sin NEEDS CLARIFICATION pendientes. Listo para Phase 1.
