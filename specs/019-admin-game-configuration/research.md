# Research: Admin Game Configuration

**Branch**: `019-admin-game-configuration` | **Date**: 2026-08-28 | **Plan**: [plan.md](plan.md)

Todos los NEEDS CLARIFICATION resueltos. El feature reutiliza agregados y slices de `001-game-configuration` y patrón BFF/OIDC/Design System de 017/018; esta fase cierra las incógnitas propias de 019.

---

## R1. Mapeo de 8 estados administrativos a estados de dominio (Constitución A)

**Decision**: 8 estados administrativos son vista de presentación; el dominio permanece con los estados Constitución A (`DRAFT`, `READY`, `WAITING_FOR_PLAYERS`, `IN_PROGRESS`, `ROUND_IN_PROGRESS`, `ROUND_COMPLETED`, `FINISHED`, `CANCELLED`, `FORCED_FINISHED`). Mapeo canónico:

| Admin | Dominio | Guarda |
|-------|---------|--------|
| `Draft` | `DRAFT` | creación inicial, configuración incompleta |
| `Configured` | `READY` (config válida sin `ScheduledAt`) | ≥5 rondas, tiempo 5–300, dificultad 1–5, categoría ≥5 preguntas |
| `Scheduled` | `WAITING_FOR_PLAYERS` o `READY` + `ScheduledAt` futura | `ScheduledAt` UTC ≥ now+5m, categoría sigue válida |
| `Ready` | `READY` + listo para `Start` | `ScheduledAt` alcanzada o pronta, validaciones superadas |
| `Running` | `IN_PROGRESS` | `StartGame` ejecutado, `RowVersion` incrementado |
| `Paused` | `IN_PROGRESS` con flag `IsPaused` (o `ROUND_IN_PROGRESS` pausado) | solo desde `Running` con ronda activa; congela timer |
| `Finished` | `FINISHED` | terminal desde `Running`/`Paused` |
| `Cancelled` | `CANCELLED` / `FORCED_FINISHED` | desde `Draft/Configured/Scheduled` (y `Running/Paused` con auditoría si hay jugadores) |

`Configured` es `READY` sin fecha; `Scheduled` es `READY` con fecha futura. El backend puede persistir un campo admin `AdminState` además del `GameStatus` de dominio para distinguir `Draft` vs `Configured` sin alterar invariantes de 001. Transiciones admin generan comandos de dominio correspondientes (`UpdateGame`, `ScheduleGame`, `ReadyGame`, `StartGame`, `PauseGame`, `ResumeGame`, `FinishGame`, `CancelGame`).

**Rationale**: Constitución A exige al menos 9 estados de dominio; el enunciado 019 lista 8 con nombres distintos pero solapados. Mapeo evita duplicar máquina de estados en dominio y respeta que `GameConfiguration` es inmutable tras `Start` (Constitución C).

**Alternatives considered**:
- Crear nueva máquina de 8 estados en dominio paralela: rechazado — duplica invariantes y rompe 001.
- Usar solo estados de dominio sin admin: rechazado — pierde `Draft`/`Configured`/`Paused` explícitos requeridos por el negocio.

---

## R2. Modelo de 16 campos — reutilización de `GameConfigurationForm` existente

**Decision**: Reutilizar `QuizArena.Admin.Client/Models/GameModels.cs: GameConfigurationForm` (ya usado en `Pages/GameConfiguration.razor` de 017) y extenderlo con campos faltantes para cubrir los 16 del spec:

| Campo spec | Propiedad en `GameConfigurationForm` | Detalle |
|------------|---------------------------------------|---------|
| Nombre | `Name` | 3–100 |
| Descripción | `Description` | 0–500 |
| Categoría | `CategoryId` | FK `Category` Active ≥5 preguntas |
| Número de rondas | `Rounds` / `MinRounds`/`MaxRounds` | 5–10 (Constitución C min ≥5) |
| Número máximo de jugadores | `MaxPlayers` / `MinPlayers` | ≥2 ≤1000 |
| Tiempo por pregunta | `TimeLimitSeconds` / `TimePerQuestion` | 5–300s |
| Dificultad inicial | `Difficulty` | 1–5 |
| Progresión dificultad | `DifficultyStrategy` | `Linear/Progressive/Adaptive/CategorySpecific` |
| Puntuación | `ScoringSystem` + `PointsPerRound` | `Standard/ProgressiveBonus` |
| Puntos asegurados | `WithdrawalPolicy`/`SecuredPoints` | `None/KEEP_CHECKPOINT/KEEP_SECURED` |
| Reglas de retiro | `WithdrawalPolicy` | 4 valores cerrados |
| Reglas de finalización | `LossPolicy` | 4 valores cerrados |
| Premio final | `RewardThreshold` + `FinalRewardId` | FK `Reward` Active |
| Premio consolación | `ConsolationRewardId` / `ConsolationPolicy` | FK `Reward` Active |
| Fecha/hora inicio | `ScheduledAt` (nueva prop) | UTC ≥ now+5m |
| Estado | `Status` (derivado, no editable) | 8 estados admin |

Los campos `Reward` y `ScheduledAt` se añaden al DTO existente sin romper el contrato (propiedades opcionales). La validación por campo se mantiene en `GameConfigurationForm.Validate()` y se espeja en validador de aplicación (FluentValidation) y en invariantes de dominio (`Game.UpdateConfiguration`).

**Rationale**: 001 ya implementa `GameConfiguration` como ValueObject inmutable tras inicio; crear un segundo modelo duplicaría invariantes. Extender el existente cubre los 16 campos con cambios mínimos y preserva `rowversion`.

**Alternatives considered**:
- Crear nuevo DTO `AdminGameConfiguration` separado: rechazado — sincronización de invariantes se vuelve frágil.
- Mantener 001 sin cambios y mapear premios/fecha en UI: rechazado — deja campos huérfanos sin validación de dominio.

---

## R3. Validación 3 niveles + inmutabilidad + concurrencia

**Decision**:
- **API (contrato)**: `Endpoint` valida JSON schema (tipos/rangos) y retorna `400 ProblemDetails` con `FieldErrors` por campo.
- **Aplicación**: `Validator` (FluentValidation) valida requisitos de caso de uso (categoría existe, ≥5 preguntas, `ScheduledAt` futura, policies en catálogo, premios existen y stock) antes de tocar dominio.
- **Dominio**: `Game`/`GameConfiguration` invariantes (`rondas ≥5`, `tiempo 5–300`, `dificultad 1–5`, `CategoryNotReady`, `InvalidGameState` si se intenta editar tras `Running`) son autoridad final; no dependen de UI.

**Inmutabilidad**: Al alcanzar `Ready`/`Running`/`Paused`, `GameConfigurationForm` se renderiza solo lectura y `IGameConfigurationService.UpdateAsync` es rechazada por dominio con `InvalidGameState` (Constitución C).

**Concurrencia**: Cada `Game` tiene `RowVersion` (`rowversion` SQL Server). `UpdateGame` exige `If-Match: W/"{RowVersion}"` (o campo `RowVersion` en body). Conflicto → `409 Conflict` → `ConcurrencyConflict` con `FieldErrors` y opción de recargar (SC-008).

**Rationale**: Constitución I (Domain First) y F (optimistic concurrency). Tres niveles evitan que la UI sea single point of failure.

**Alternatives considered**:
- Solo validación UI: rechazado — viola I y permite bypass por API.
- Pessimistic locking: rechazado — escala peor para edición de configuración.

---

## R4. Catálogos cerrados de políticas (Constitución C)

**Decision**: Catálogos estáticos tipados en `GameCatalogs.cs` (client) y enumeraciones de dominio (`WithdrawalPolicy`, `LossPolicy`, `DifficultyStrategy`, `ScoringSystem`):

- `DifficultyStrategy`: `Linear | Progressive | Adaptive | CategorySpecific`
- `WithdrawalPolicy`: `LOSE_ALL | KEEP_CURRENT_SCORE | KEEP_SECURED_SCORE | KEEP_CHECKPOINT_SCORE`
- `LossPolicy`: `LOSE_ALL | LOSE_CURRENT_ROUND | LOSE_UNSECURED_POINTS | FALLBACK_TO_CHECKPOINT`
- `ScoringSystem`: `Standard | ProgressiveBonus`
- `SecuredPoints`: mapeado a combinación de withdrawal/loss (p. ej., `None` = `LOSE_ALL` + `LOSE_ALL`)

Selects en `GameConfigurationForm.razor` poblados desde `GameCatalogs.All` (label español, value canónico). Guardar fuera de catálogo → `400 InvalidConfiguration` con campo señalado.

**Rationale**: Constitución C exige abstracciones strategy/policy, no hardcoded. Catálogo cerrado garantiza auditabilidad y evita configuración incoherente (SC-003).

**Alternatives considered**:
- Free-form string para políticas: rechazado — viola C y genera combinaciones inválidas.
- Catálogo dinámico desde backend: rechazado — políticas son invariantes de dominio, no datos.

---

## R5. Referencias cruzadas: Categoría ≥5 y Premios Active

**Decision**:
- **Categoría**: `GET /api/categories/{id}` con `ValidQuestionCount`. Si `Status != Active` o `ValidQuestionCount <5`, guardado rechazado `CategoryNotReady` (Toasts + field error). Listado de categorías filtrable por `Active`. `Question` con 4 opciones/1 correcta (Constitución B).
- **Premios**: `GET /api/rewards/{id}` con `Status==Active` y `Stock>0` (si aplica). `FinalRewardId` y `ConsolationRewardId` opcionales; si se definen y son iguales, rechazo `InvalidConfiguration` cuando la política exige distinción. Validación server-side doble (aplicación + dominio).

**Rationale**: Constitución B (≥5 preguntas antes de publicar) y C (Reward lifecycle). Guardas evitan configurar juegos no jugables.

**Alternatives considered**:
- Permitir categoría vacía y rellenar luego: rechazado — viola B y deja `Configured` falso.
- Premios inactivos permitidos: rechazado — `RewardUnavailable` es invariante.

---

## R6. BFF, auditoría y listado paginado

**Decision**:
- **BFF**: `ClientGameConfigurationService` → `HttpClient.BaseAddress = HostEnvironment.BaseAddress` → rutas `/bff/games*` (cookie viaja); `ServerGameConfigurationService` → `http://oroclash-api/api/games*` con `Bearer` del `HttpContext`. Forwarder catch-all `/bff/{**catch-all}` → `/api/{**catch-all}` ya existe (017) y cubre `POST /bff/games`, `PUT /bff/games/{id}`, `POST /bff/games/{id}/schedule|ready|start|pause|resume|finish|cancel`, `GET /bff/games?status=&category=&page=&pageSize=&search=`.
- **Auditoría**: Append-only via Outbox (`GameAuditEntry`) en `SaveChanges` (Constitución I). Cada transición persiste `FromState/ToState/ChangedFields/CorrelationId` (FR-014).
- **Listado**: `GamesList.razor` consume `GET /bff/games` paginado (`PagedResult<GameSummary>`), filtros `status` y `category`, búsqueda por nombre, skeleton por bloque (SC-009). Detalle `GameDetail.razor` muestra configuración inmutable resaltada y historial `GetAudit`.

**Rationale**: Reutiliza patrón BFF de 017 (sample `BlazorWebAppOidcBffAutoYarpAspire`), evita exponer `access_token` al navegador y preserva `CorrelationId` (FR-015).

**Alternatives considered**:
- Llamar WASM → API directo con token en memoria: rechazado — expone JWT y requiere CORS.
- Minimal APIs por endpoint sin YARP: rechazado — duplica 10+ rutas; YARP catch-all ya justificado en 017.

---

## Consolidated Decisions Summary

| # | Decisión | Fuente |
|---|----------|--------|
| 1 | 8 estados admin mapeados a estados dominio + `AdminState` opcional | FR-008/009, Constitución A |
| 2 | Extender `GameConfigurationForm` existente con `ScheduledAt` y `RewardId`s | FR-001..007, 001 |
| 3 | Validación 3 niveles + inmutabilidad tras `Ready`/`Running` + `rowversion` | FR-010/011, Constitución I/F |
| 4 | Catálogos cerrados tipados para 4 políticas + dificultad | FR-003..005, Constitución C |
| 5 | Guardas `CategoryNotReady` (≥5) y `RewardUnavailable` | FR-001/006, Constitución B/C |
| 6 | BFF catch-all + auditoría Outbox + listado paginado | FR-014..019 |

Sin NEEDS CLARIFICATION pendientes. Listo para Phase 1.
