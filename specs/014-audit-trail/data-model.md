# Data Model: Audit Trail (SPEC-014)

**Date**: 2026-08-28 | **Plan**: [plan.md](plan.md) | **Research**: [research.md](research.md)

Extensión transversal de la auditoría de SPEC-013. Sin nuevos agregados de juego; se extiende `AuditEntry` para implementar el concepto `AuditRecord`. Leyenda: **EXISTING** (ya en SPEC-013), **EXTEND** (columna/propiedad nueva), **NEW** (Enumeration nueva).

## Entidades

### AuditEntry / AuditRecord (AggregateRoot<Guid>) — EXTEND (existe en SPEC-013, se extiende aquí)

Tabla `AuditEntries`. Append-only, sin `Update`/`Delete`. Mapea 1:1 al `AuditRecord` conceptual del spec.

| Campo | Tipo | Estado | Notas |
|-------|------|--------|-------|
| `Id` | `Guid` | EXISTING | PK, `Guid.NewGuid()` |
| `Timestamp` | `DateTimeOffset` | EXISTING | UTC servidor al persistir |
| `ActorId` | `string` | EXISTING | `sub` claim o `system`/`anonymous` |
| `ActorRoles` | `string` | EXISTING | Snapshot `role`/`roles` |
| `Action` | `string` | EXISTING | `AuditAction.Name` (16 valores) |
| `Permission` | `string` | EXISTING | Permiso evaluado (SPEC-013) |
| `Resource` | `string` | EXISTING | Tipo: `Game`, `Round`, `Player`, `Answer`, `Reward`, `Consolation` |
| `ResourceId` | `string?` | **EXTEND** | Id del recurso (`GameId`, `RoundId`, `AnswerId`, `RewardId`) |
| `GameId` | `Guid?` | **EXTEND** | Juego asociado (FR-003) |
| `PlayerId` | `Guid?` | **EXTEND** | Jugador asociado (FR-003) |
| `CorrelationId` | `string` | EXISTING | Traza (`X-Correlation-ID`/`Activity.Id`/`TraceIdentifier`) |
| `TenantId` | `string?` | EXISTING | `tenant_id` claim |
| `Result` | `string` | EXISTING | `Succeeded`/`Failed`/`Denied`/`RateLimited`/`ReplayDetected` |
| `Reason` | `string?` | EXISTING | Código de error sin secretos |
| `Details` | `string?` | EXISTING | Alias de `Data` — JSON sanitizado (delta, motivo) |
| `Data` | `string?` | **EXTEND** (alias) | Mapea a `Details` para compatibilidad con spec (FR-001 `Data`) |
| `TenantId` | `string?` | EXISTING | Duplicado arriba |

Config EF (`AuditEntryTypeConfiguration` EXTEND):
- `ToTable("AuditEntries")`, `HasKey(e => e.Id)`, `ValueGeneratedNever()`
- `HasIndex(e => e.Timestamp)`, `HasIndex(e => e.Resource)`, `HasIndex(e => e.CorrelationId)`, `HasIndex(e => e.ActorId)` (existentes) + **nuevos** `HasIndex(e => e.GameId)`, `HasIndex(e => e.PlayerId)`, `HasIndex(e => e.Action)`
- Columnas nuevas nullable, `HasMaxLength(200)` para `ResourceId`, `HasColumnName` si se aliasa `Data`→`Details`.

Comportamiento: solo `AuditEntry.Create(... )` y `repository.AddAsync`; sin métodos `Update`/`Delete` expuestos. `OroQuizClashDbContext.AuditEntries` es el único acceso.

### AuditAction (Enumeration) — NEW

Catálogo cerrado 16 valores (FR-002). `Enumeration<AuditAction>` con `Id`/`Name`.

| Id | Name | Origen (comando/handler) |
|----|------|--------------------------|
| 1 | `GameCreated` | `CreateGame` |
| 2 | `GameConfigured` | `UpdateGame`/`ConfigureGame` |
| 3 | `GameStarted` | `StartGame` |
| 4 | `PlayerJoined` | `JoinGame` |
| 5 | `RoundStarted` | `StartRound` |
| 6 | `QuestionPresented` | `StartRound` (misma transacción) |
| 7 | `AnswerSubmitted` | `SubmitAnswer` (recepción) |
| 8 | `AnswerEvaluated` | `SubmitAnswer` (evaluación) |
| 9 | `PointsAwarded` | `ScoreUpdated` (delta >0) |
| 10 | `PointsRemoved` | `ScoreUpdated` (delta <0) |
| 11 | `PlayerWithdrawn` | `WithdrawPlayer` |
| 12 | `PlayerEliminated` | `EliminatePlayer` / regla |
| 13 | `GameFinished` | `FinishGame` |
| 14 | `RewardRedeemed` | `RedeemReward` |
| 15 | `ConsolationGranted` | `GrantConsolation` |
| 16 | `AdministrativeAdjustment` | `AdjustPoints`/`Admin` ops |

`AuditAction.All` y `FromName`/`FromId` para validación.

### Game / Player / Round / Answer / Reward — EXISTING

Sin cambios de esquema. Solo son referenciados por `AuditEntry.GameId`/`PlayerId`/`ResourceId` y por `AuditBehavior` para extraer `Data`. No tienen FK estricta a `AuditEntries` (referencia lógica, no constraint).

### CorrelationId (ValueObject conceptual) — EXISTING via ServiceDefaults

Propagado por `BuildingBlocks.ServiceDefaults` (`X-Correlation-ID` middleware → `Activity.Current.Id`). No es entidad; se almacena como `string` en `AuditEntry.CorrelationId` y se usa para agrupar.

## Relaciones

```text
Command/Query (16 tipos) ──AuditBehavior──> AuditEntry (1 por intento, append-only)
AuditEntry (N) ──ResourceId──> Game/Player/Round/Answer/Reward (N, referencia lógica)
AuditEntry.CorrelationId (1) ──< (N) AuditEntry (agrupación de traza)
AuditEntry.GameId (N) ──> Game (1)  (índice, no FK estricta)
AuditEntry.PlayerId (N) ──> GamePlayer (1)
```

## Validaciones y transiciones

- `AuditEntry.Create` requiere `Action` ∈ 16, `Timestamp` no futuro, `Actor` no vacío, `Resource` no vacío.
- `Action` fuera de catálogo → `ArgumentException` (falla rápida en `AuditBehavior` y se loguea, no se persiste registro inválido).
- `GameId`/`PlayerId` nullable: si el evento no está asociado a juego/jugador, se persiste `null` (FR-003).
- `Data` se trunca a 1000 chars y se sanitiza (sin `IsCorrect` previo, sin tokens) antes de persistir.
- No hay transiciones de estado para `AuditEntry`; solo `Created` → consultable.

## Invariantes

1. Un `AuditEntry` nunca se actualiza ni borra (FR-004/005).
2. Todo intento relevante genera exactamente un `AuditEntry` (FR-009), incluso si el intento fue denegado.
3. `CorrelationId` idéntico para todos los `AuditEntry` de un mismo flujo (FR-007).
4. Ningún handler de dominio lee `AuditEntries` para decidir (FR-008, SC-006).
5. `Timestamp` siempre UTC servidor, nunca cliente.

