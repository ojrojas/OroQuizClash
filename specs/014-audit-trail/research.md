# Research: Audit Trail (SPEC-014)

**Date**: 2026-08-28 | **Plan**: [plan.md](plan.md) | **Spec**: [spec.md](spec.md)

Phase 0 — resolución de decisiones técnicas. No quedó ningún NEEDS CLARIFICATION en el Technical Context; las decisiones siguientes resuelven los puntos de diseño identificados tras inspeccionar la infraestructura de SPEC-013 (`AuditEntry`, `AuditBehavior`, `GetAuditEntries`, `OroQuizClashDbContext`, `BuildingBlocks.ServiceDefaults`).

## R1 — Reuso de `AuditEntry` (SPEC-013) vs nueva entidad `AuditRecord`

- **Decision**: Reutilizar y extender `AuditEntry` (SPEC-013) para implementar el concepto `AuditRecord` del spec, en lugar de crear una segunda tabla `AuditRecords`. Añadir columnas faltantes `ResourceId` (string), `GameId` (Guid?), `PlayerId` (Guid?), `Data` (string JSON sanitizado) manteniendo compatibilidad con columnas existentes (`ActorId`/`Actor` , `Action`, `Resource`, `CorrelationId`, `Timestamp`, `Result`, `Reason`/`Details`). Alias conceptual: `AuditRecord` ≡ `AuditEntry` extendida.
- **Rationale**: SPEC-013 ya provee la base append-only, `AuditBehavior` centralizado y `GET /api/audit`. Duplicar crearía dos trails divergentes y violaría transversalidad (FR-010). Extender preserva compatibilidad (columnas nuevas nullable) y cumple FR-001 con un único store.
- **Alternatives considered**:
  - Nueva entidad `AuditRecord` separada: rechazado — duplicación, migración y doble consulta.
  - Vista mapeada `AuditRecord` → `AuditEntries`: rechazado — complejidad sin beneficio; extensión directa es más simple.

## R2 — Catálogo cerrado de 16 `Action` como `Enumeration`

- **Decision**: Crear `AuditAction : Enumeration<AuditAction>` en `Domain/Audit/AuditAction.cs` con 16 valores: `GameCreated(1)`, `GameConfigured(2)`, `GameStarted(3)`, `PlayerJoined(4)`, `RoundStarted(5)`, `QuestionPresented(6)`, `AnswerSubmitted(7)`, `AnswerEvaluated(8)`, `PointsAwarded(9)`, `PointsRemoved(10)`, `PlayerWithdrawn(11)`, `PlayerEliminated(12)`, `GameFinished(13)`, `RewardRedeemed(14)`, `ConsolationGranted(15)`, `AdministrativeAdjustment(16)`. `AuditEntry.Action` almacena `AuditAction.Name` (string) para legibilidad y búsqueda.
- **Rationale**: FR-002 exige catálogo cerrado sin omisiones; `Enumeration` da tipado fuerte en `AuditBehavior` (switch sobre `TRequest` → `AuditAction`) y evita strings mágicos dispersos. `FromName`/`FromId` permiten validación.
- **Alternatives considered**:
  - `enum` C# puro: rechazado — BuildingBlocks prefiere `Enumeration` para ValueObject y extensibilidad (métodos).
  - Tabla `AuditActions` normalizada: rechazado — catálogo es estático, no requiere FK.

## R3 — Captura de `GameId`/`PlayerId`/`ResourceId`/`Data`/`CorrelationId` en `AuditBehavior`

- **Decision**: Extender `AuditBehavior<TRequest,TResponse>` de SPEC-013 para extraer, además de `ActorId`/`Permission`/`Resource`/`CorrelationId` ya capturados:
  - `GameId`: de `request` vía reflexión `GameId`/`Id` si el tipo es `GameId` o Guid cuyo nombre contiene `GameId`; si no, `null` (eventos sin juego).
  - `PlayerId`: de `sub` claim si `Action` es de jugador (`AnswerSubmitted` etc.) o de propiedad `PlayerId` en request; `null` para eventos de sistema.
  - `ResourceId`: `Id` del recurso afectado (`GameId`, `RoundId`, `AnswerId`, `RewardId`) según `Action`.
  - `Data`: JSON sanitizado con detalles mínimos (ej. `delta`, `questionId`, `reason`) construido por cada handler o por `AuditBehavior` vía `request` → `JsonSerializer` con filtro (sin `IsCorrect` previo, sin secretos). `Data` se trunca a 1000 chars.
  - `CorrelationId`: ya propagado vía `X-Correlation-ID` / `Activity.Current.Id` / `TraceIdentifier` (SPEC-013). Cada flujo (ej. ronda) comparte el mismo `CorrelationId` porque viene de la petición origen y se propaga por `IHttpContextAccessor` durante toda la pipeline.
- **Rationale**: FR-001/FR-003/FR-007 exigen esos campos y trazabilidad; centralizar extracción en `AuditBehavior` cumple FR-010 (sin código ad-hoc por feature) y mantiene FR-008 (no consulta audit para decidir). Extracción por reflexión es genérica y testable.
- **Alternatives considered**:
  - Cada handler crea su `AuditEntry` manualmente: rechazado — viola transversalidad y dispersa lógica.
  - `IAuditDataProvider` por comando: rechazado — sobrediseño para 16 casos; reflexión + convención de nombres es suficiente para v1.

## R4 — Append-only e inmutabilidad — enforcement

- **Decision**: Mantener `AuditEntry` como `AggregateRoot<Guid>` sin métodos `Update`/`Delete`; `IRepository<AuditEntry,Guid>` solo expone `AddAsync`/`GetByIdAsync`/`ListAsync`/`FirstOrDefaultAsync` en la práctica (no se expone `Update`/`Delete` en `GetAuditEntries` slice). `AuditEntryTypeConfiguration` no configura concurrency token mutable; tabla sin triggers de update. Tests de arquitectura verifican que ningún handler de dominio referencia `IRepository<AuditEntry>` para escribir salvo `AuditBehavior`, y que `AuditEntry` no tiene métodos públicos `Update`.
- **Rationale**: FR-004/FR-005 exigen append-only e immutable; la falta de API de mutación + tests de arquitectura es la garantía más simple sin recurrir a triggers de BD o row-level security.
- **Alternatives considered**:
  - Trigger BD `INSTEAD OF UPDATE`: rechazado — lógica en BD oculta y no portable SQLite/SQL Server.
  - Event sourcing con store separado: rechazado — sobrediseño para SC-007 (1000 registros).

## R5 — Búsqueda paginada — Specification + índices

- **Decision**: Extender `AuditEntrySpecification` (SPEC-013) para soportar filtros combinables `GameId`/`PlayerId`/`Action`/`Resource`/`ResourceId`/`CorrelationId`/`Timestamp` (`from`/`to`) con `Where` dinámico, `ApplyOrderBy(e => e.Timestamp)` ascendente para trazabilidad, y `ApplyPaging`. Índices en `AuditEntryTypeConfiguration`: `HasIndex(e => e.GameId)`, `HasIndex(e => e.PlayerId)`, `HasIndex(e => e.Action)`, `HasIndex(e => e.CorrelationId)`, `HasIndex(e => e.Timestamp)` además de los ya existentes `Resource`/`ActorId`. Paginación impone `pageSize` máximo 100. Búsqueda es solo lectura (no genera `AuditRecord`).
- **Rationale**: FR-006 exige búsqueda paginada con 7 filtros combinables; `Specification` ya es el patrón del proyecto y evita SQL ad-hoc. Índices cubren SC-007 (<500 ms p95 para 1000 registros).
- **Alternatives considered**:
  - Full-text search / Elasticsearch: rechazado — volumen de referencia no lo justifica.
  - `IQueryable` directo sin Specification: rechazado — inconsistente con el resto del proyecto.

## R6 — Trazabilidad por `CorrelationId` — propagación

- **Decision**: Reutilizar propagación ya existente en `BuildingBlocks.ServiceDefaults` (`X-Correlation-ID` middleware → `Activity.Current.Id` → `IHttpContextAccessor` → `AuditBehavior`). Cada `AuditRecord` del mismo flujo comparte el `CorrelationId` de la petición origen; no se genera un `CorrelationId` nuevo por evento auditado. Búsqueda por `CorrelationId` retorna todos los `AuditRecord` de ese flujo ordenados por `Timestamp`.
- **Rationale**: FR-007 exige traza completa con una sola búsqueda; propagación por `Activity` es estándar OTel y ya está en `ServiceDefaults`, sin código extra. Best-effort: si `X-Correlation-ID` no viene, se genera uno por `TraceIdentifier` y se usa para toda la pipeline.
- **Alternatives considered**:
  - `CorrelationId` por evento (nuevo Guid por `AuditRecord`): rechazado — rompería trazabilidad (no se podría agrupar).
  - Almacenado en `Game` aggregate: rechazado — `CorrelationId` es de infraestructura, no de dominio.

## R7 — Transversalidad y no condicionar negocio — best-effort

- **Decision**: `AuditBehavior` se ejecuta después de `next(cancellationToken)` (post-handler) y persiste `AuditEntry` en la misma `OroQuizClashDbContext` pero fuera de la transacción de negocio ya confirmada; si la persistencia de audit falla, se captura, se loguea como `Warning` y no se revierte el resultado de negocio (SC del edge case US2). Handlers de dominio nunca inyectan `IRepository<AuditEntry>` ni consultan auditoría para decidir (verificable por Architecture tests: ningún handler de `Games`/`Rewards` referencia `AuditEntry`). La auditoría es observabilidad, no gate.
- **Rationale**: FR-008 y edge case de US2 exigen que el fallo de audit no revierta puntos ya acreditados; la transversalidad (FR-010) exige centralización en `AuditBehavior`, no código disperso.
- **Alternatives considered**:
  - Auditoría en misma transacción que agregados (outbox): rechazado — si audit falla, haría fallar la transacción de negocio, violando el edge case.
  - Auditoría asíncrona vía `DomainEvent` + `IEventBus`: rechazado — añadiría eventual consistency y complejidad para SC-001 (100% de eventos deben estar inmediatamente consultables tras la operación en tests).

