# Data Model: Game Security (SPEC-013)

**Date**: 2026-08-28 | **Plan**: [plan.md](plan.md) | **Research**: [research.md](research.md)

Transversal — extiende el modelo sin nuevos agregados de juego. Leyenda: **EXISTING** (sin cambios), **EXTEND** (atributo/comportamiento nuevo), **NEW** (entidad/tabla nueva), **DERIVED** (proyección).

## Entidades

### Permission (Enumeration) — NEW (Domain/Authorization)

Catálogo cerrado de 14 permisos (FR-003). `Enumeration` con `Id` int y `Name` string.

| Id | Name | Descripción |
|----|------|-------------|
| 1 | `Category.Read` | Lectura categorías |
| 2 | `Category.Write` | Crear/editar categorías |
| 3 | `Category.Publish` | Publicar categoría |
| 4 | `Question.Read` | Lectura preguntas |
| 5 | `Question.Write` | Crear/editar preguntas |
| 6 | `Question.Publish` | Publicar pregunta |
| 7 | `Game.Create` | Crear juego |
| 8 | `Game.Start` | Iniciar/preparar, abrir lobby, iniciar/completar ronda, forzar cierre |
| 9 | `Game.Play` | Unirse, responder, retirarse, consultar estado propio/leaderboard de su juego |
| 10 | `Reward.Read` | Consultar recompensas |
| 11 | `Reward.Redeem` | Canjear recompensa (sobre puntos propios) |
| 12 | `Reward.Manage` | Crear/gestionar recompensas |
| 13 | `Report.Read` | Reportes operativos |
| 14 | `Audit.Read` | Consulta auditoría |

Reglas: no se crea permiso fuera de este catálogo sin ADR; `Permission` se usa en `[RequiresPermission]` y en `AuthorizationBehavior`.

### Role (Enumeration) — NEW (Domain/Authorization)

| Id | Name | Permisos por defecto (FR-003) |
|----|------|-------------------------------|
| 1 | `ADMIN` | Todos (1–14) |
| 2 | `GAME_MANAGER` | 1,2,3,4,5,6,7,8,13 (Category/Question/Game + Report) |
| 3 | `PLAYER` | 1 (limitado visibilidad), 9,10,11 |
| 4 | `REWARD_MANAGER` | 10,12,13,14 |

`ADMIN` puede actuar en nombre de otro cuando la operación lo documenta (FR-007); `GAME_MANAGER` como observador/autorizado en `Game.Play` ajeno.

### AuditEntry (Entity<Guid>) — NEW (Domain/Audit, tabla `AuditEntries`)

Registro append-only (FR-016/017). Solo `Add`, no `Update`/`Delete`.

| Campo | Tipo | Notas |
|-------|------|-------|
| `Id` | `Guid` | PK, `Guid.NewGuid()` |
| `Timestamp` | `DateTimeOffset` | Reloj servidor, `UtcNow` en Behavior |
| `ActorId` | `string` | `sub` claim o `anonymous` |
| `ActorRoles` | `string` | Snapshot de roles al momento |
| `Action` | `string` | Nombre del comando/query (ej. `SubmitAnswer`) |
| `Permission` | `string` | Permiso evaluado (ej. `Game.Play`) |
| `Resource` | `string` | Identificador de recurso (ej. `Game:guid`, `Round:guid`) |
| `CorrelationId` | `string` | `X-Correlation-ID` / `Activity.Current.Id` |
| `TenantId` | `string?` | Claim `tenant_id` si existe |
| `Result` | `string` | `Success` \| `Denied` \| `ValidationFailed` \| `RateLimited` \| `ReplayDetected` |
| `Reason` | `string?` | Código de error sin secretos (ej. `PlayerIdentityMismatch`, `RateLimitExceeded`) |
| `Details` | `string?` | JSON mínimo sin PII/secreto |

Índices: `(Timestamp)`, `(Resource)`, `(CorrelationId)`, `(ActorId)`.
Config EF: `AuditEntryTypeConfiguration` — `ToTable("AuditEntries")`, `ValueGeneratedNever` para Id, `IsRequired` para todos excepto `Reason`/`Details`, sin concurrency token (append-only).

### IdempotencyRecord (Entity<Guid>) — NEW (Infrastructure/Services, tabla `IdempotencyRecords`)

Soporta anti-replay genérico (R3). Para respuestas se sigue usando índice natural `(GameId,PlayerId,RoundId)`.

| Campo | Tipo | Notas |
|-------|------|-------|
| `Id` | `Guid` | PK |
| `Key` | `string` | `Idempotency-Key` header o hash del comando, único por `ActorId` |
| `ActorId` | `string` | `sub` |
| `CreatedAt` | `DateTimeOffset` | Ventana 24h (configurable) |
| `ResponseHash` | `string` | Hash del payload para detectar replay con payload distinto |
| `Response` | `string` | JSON del resultado original (para retorno idempotente) |

Índice único `(Key, ActorId)` con filtro, TTL por purga configurable.

### Game / GamePlayer / Answer / PointTransaction — EXISTING

Sin cambios de esquema. Comportamiento extendido interpretado:
- `Game.SubmitAnswer` ya ignora `Score`/`Correctness`/`Time` y usa `AnswerOption.IsCorrect` + `DateTimeOffset.UtcNow` + `sub` (verificado en T014 previa).
- `GamePlayer` ya aísla por `UserId == sub`.

### RewardRedemption — EXISTING (EXTEND interpretación)

Ya tiene `IdempotencyKey` nullable con índice único filtrado `(PlayerId, IdempotencyKey)` — se preserva.

## Relaciones

```text
Principal (JWT sub/roles) ──> AuthorizationBehavior ──> Permission × Role matrix
Command/Query ──> ValidationBehavior ──> RateLimiting (partitioned by sub+gameId) ──> IdempotencyService ──> AuthorizationBehavior ──> Handler
Handler ──> AuditBehavior ──> AuditEntry (1 per attempt, append-only, correlated by CorrelationId)
Game (1) ──< (N) AuditEntry via Resource Game:guid (derivado, no FK estricto)
```

## Validaciones y transiciones

- **Denegar por defecto**: cualquier `Permission` no mapeada → 403 sin existencia (FR-004).
- **Alcance por recurso**: `Game.Play` requiere `game.Players.Contains(sub)` o `IsOrganizer` (FR-005).
- **Anti-tampering**: `SubmitAnswerRequest` DTO no expone `score`/`correctness`/`gameState`; si llegan, se ignoran (FR-006).
- **Idempotencia**: segundo `SubmitAnswer` mismo `(GameId,PlayerId,RoundId)` → retorna `Answer` original sin nueva `PointTransaction` (FR-012).
- **Audit inmutabilidad**: `AuditEntry` sin `Update`/`Delete` en repositorio; intento de mutar → excepción.

## Invariantes

1. Ninguna operación sensible se ejecuta sin `AuditEntry` (éxito o rechazo).
2. Ningún campo cliente `Score`/`Correctness`/`Time`/`PlayerId`/`GameState` sobrevive hasta dominio.
3. Rate limiting aislado por juego/jugador — no cross-game.
4. `AuditEntries` nunca se borran/modifican por API normal.

