# Contract: Rate Limiting — SPEC-013

**Date**: 2026-08-28 | **Plan**: [../plan.md](../plan.md) | **Spec**: [../spec.md](../spec.md)

Protección operativa (FR-014/015). Single-node, particionado por actor/recurso, no global glotón.

## Políticas

| Política | Partición | Límite default | Ventana | Aplica a |
|----------|-----------|----------------|---------|----------|
| `GamePlayLimiter` | `sub` + `gameId` (de ruta) | 5 permisos | 1s | `POST /api/games/{id}/answers`, `POST /api/games/{id}/withdraw`, `POST /api/games/{id}/players` |
| `SensitiveLimiter` | `sub` | 10 req | 10s | `POST /api/games`, `POST /api/rewards/{id}/redeem`, `POST /api/categories/{id}/publish` |
| `ReadLimiter` | IP | 100 req | 10s | `GET /api/games/{id}/leaderboard`, `GET /api/audit` |

Configurable vía `Security:RateLimit:GamePlay:PermitLimit` / `WindowSeconds` en `appsettings.json` (default arriba).

## Comportamiento

- `FixedWindowRateLimiter` con `QueueLimit=0` (sin cola).
- `OnRejected` → 429 Too Many Requests con headers:

```http
HTTP/1.1 429 Too Many Requests
Retry-After: 1
X-RateLimit-Limit: 5
X-RateLimit-Remaining: 0
X-RateLimit-Reset: 2026-08-28T10:00:01Z
```

- Cuerpo ProblemDetails:

```json
{
  "type": "https://httpstatuses.com/429",
  "title": "Too Many Requests",
  "status": 429,
  "detail": "Rate limit exceeded. Retry after 1s.",
  "code": "RateLimitExceeded"
}
```

- El intento limitado queda auditado con `Result=RateLimited` y `Reason=RateLimitExceeded`.
- Éxito de la misma ventana retorna headers `X-RateLimit-Remaining`.

## Aislamiento (FR-015/SC-009)

- Partición por `gameId`: ráfaga en `game-A` no consume permisos de `game-B`.
- Partición por `sub`: ráfaga de player X no afecta a player Y en mismo juego.
- Verificación: test con 20 juegos × 4 jugadores, ráfaga en uno, medir `429` Rate en otros <5% y latencia p95 sin degradación.

## Anti-replay interacción

Reintento legítimo con mismo `Idempotency-Key` dentro de ventana no cuenta como nuevo permiso si es idempotente (retorna original sin re-ejecutar). Replay con payload distinto → 400 `ReplayDetected` (no 429).

## Configuración

```json
{
  "Security": {
    "IdempotencyWindowHours": 24,
    "RateLimit": {
      "GamePlay": { "PermitLimit": 5, "WindowSeconds": 1 },
      "Sensitive": { "PermitLimit": 10, "WindowSeconds": 10 },
      "Read": { "PermitLimit": 100, "WindowSeconds": 10 }
    }
  }
}
```

Single-node `PartitionedRateLimiter.Create` (sin Redis). Para multi-nodo futuro, backplane distribuido queda fuera de alcance.

