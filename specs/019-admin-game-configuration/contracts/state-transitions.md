# Contract: State Transitions

**Branch**: `019-admin-game-configuration` | **Date**: 2026-08-28

Máquina de 8 estados administrativos (FR-008/009) mapeada a dominio (research R1). Todas las transiciones son comandos dedicados via BFF.

## 1. Endpoints de transición

```
POST /bff/games/{id}/schedule   → POST /api/games/{id}/schedule   { scheduledAt, rowVersion }
POST /bff/games/{id}/ready      → POST /api/games/{id}/ready      { rowVersion }
POST /bff/games/{id}/start      → POST /api/games/{id}/start      { rowVersion }
POST /bff/games/{id}/pause      → POST /api/games/{id}/pause      { rowVersion }
POST /bff/games/{id}/resume     → POST /api/games/{id}/resume     { rowVersion }
POST /bff/games/{id}/finish     → POST /api/games/{id}/finish     { rowVersion }
POST /bff/games/{id}/cancel     → POST /api/games/{id}/cancel     { rowVersion, reason? }
```

Todos `RequireAuthorization: AdminOrGameManager`. `RowVersion` via `If-Match` o body.

## 2. Diagrama de transiciones permitidas

```
Draft ──► Configured ──► Scheduled ──► Ready ──► Running ◄──► Paused ──► Finished
  │           │            │                              (Resume)        ▲
  └──────────►│◄───────────┘                              │              │
             ▼                                            └──────────────┘
         Cancelled ◄───────────────────────────────────── Running/Paused (con auditoría)
```

- `Draft → Configured` automática al completar configuración mínima válida (≥5 rondas, categoría ≥5 preguntas, tiempo 5–300, dificultad 1–5, policies en catálogo).
- `Configured → Scheduled` requiere `scheduledAt` futura ≥ now+5m.
- `Scheduled → Ready` requiere `scheduledAt` alcanzable y categoría sigue válida.
- `Ready → Running` (`StartGame`) bloquea edición (inmutable).
- `Running ↔ Paused` congela timer y preserva `RoundNumber`/`QuestionId` (edge case).
- `Running/Paused → Finished` terminal.
- `Draft/Configured/Scheduled → Cancelled` terminal; `Running/Paused → Cancelled` solo con auditoría si hay `GamePlayer` en `PLAYING`.

## 3. Request/Response por transición

**Schedule**

```http
POST /bff/games/{id}/schedule
Content-Type: application/json
If-Match: W/"AAAAAAAAB9E="

{ "scheduledAt": "2026-09-01T20:00:00Z" }
```

**200 OK**

```json
{ "id":"...", "status":"Scheduled", "scheduledAt":"2026-09-01T20:00:00Z", "rowVersion":"AAAAAAAAB9I=" }
```

**Ready/Start/Pause/Resume/Finish/Cancel**

```http
POST /bff/games/{id}/pause
If-Match: W/"AAAAAAAAB9I="
{} 
```

**200 OK** con nuevo `status` y `rowVersion`.

## 4. Errores

| Código | HTTP | Cuando |
|--------|------|--------|
| `InvalidGameState` | 422 | Transición no permitida (p. ej., `Finished → Running`, `Draft → Ready`) |
| `CategoryNotReady` | 400 | `Scheduled/Ready` con categoría <5 preguntas |
| `RewardUnavailable` | 400 | premio inactivo sin stock |
| `ConcurrencyConflict` | 409 | `RowVersion` desactualizado |
| `ValidationError` | 400 | `scheduledAt` en pasado o <5m |
| `Unauthorized` | 401 | sesión expirada → banner re-autenticar |
| `Forbidden` | 403 | `REWARD_MANAGER` → Access Denied |

Todos `application/problem+json` con `errors.{field}` señalando campo.

## 5. Invariantes

- Transición es atómica (agregado + Outbox + audit) — sin mutación parcial.
- `RowVersion` incrementado en cada transición exitosa.
- Auditoría append-only: `GameAuditEntry` con `From/To/ActorId/Timestamp/CorrelationId`.

## 6. Contrato cliente

```csharp
public enum GameStateView { Draft, Configured, Scheduled, Ready, Running, Paused, Finished, Cancelled }

public Task<GameResponse> ScheduleAsync(Guid id, DateTimeOffset scheduledAt, string rowVersion, CancellationToken ct = default);
public Task<GameResponse> ReadyAsync(Guid id, string rowVersion, CancellationToken ct = default);
public Task<GameResponse> StartAsync(Guid id, string rowVersion, CancellationToken ct = default);
public Task<GameResponse> PauseAsync(Guid id, string rowVersion, CancellationToken ct = default);
public Task<GameResponse> ResumeAsync(Guid id, string rowVersion, CancellationToken ct = default);
public Task<GameResponse> FinishAsync(Guid id, string rowVersion, CancellationToken ct = default);
public Task<GameResponse> CancelAsync(Guid id, string rowVersion, CancellationToken ct = default);
```

Page `GameTransitionsBar.razor` habilita botones según `Status` actual; estados terminales deshabilitan todos.
