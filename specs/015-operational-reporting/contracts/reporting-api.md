# Contract: Reporting API — SPEC-015

**Date**: 2026-08-28 | **Plan**: [../plan.md](../plan.md) | **Spec**: [../spec.md](../spec.md) | **Auth**: `Report.Read` (y `Audit.Read` para trazas)

Endpoints de solo lectura, sin `DomainEvent` ni `SaveChanges`. Todos requieren `Authorization` y respetan `Global`/`Game`/`Category`/`Period`. Respuestas con `page`/`pageSize`/`total` cuando aplica paginación.

## Endpoints

### GET /api/reports/games/{gameId}

`GameReport` por juego. Requiere `Report.Read`.

**Path**: `gameId` (guid)

**Response 200:**

```json
{
  "gameId": "guid",
  "name": "string",
  "start": "2026-08-28T10:00:00Z",
  "end": "2026-08-28T11:00:00Z|null",
  "players": [{ "playerId": "guid", "displayName": "string|null", "status": "ACTIVE|WITHDRAWN|ELIMINATED|WINNER" }],
  "rounds": [{ "roundId": "guid", "roundNumber": 1, "questionId": "guid" }],
  "winner": { "playerId": "guid", "displayName": "string|null" }|null,
  "totalQuestions": 5,
  "totalRounds": 5
}
```

**Errores**: 404 `GameNotFound` si `gameId` inexistente, 403 sin `Report.Read`, 401 sin JWT.

### GET /api/reports/players/{playerId}

`PlayerReport` agregado. Requiere `Report.Read` (o `Game.Play` si es propio `playerId` == `sub`, según política).

**Query params (filtros combinables):**

| Param | Tipo | Descripción |
|-------|------|-------------|
| `gameId` | guid | Solo ese juego |
| `categoryId` | guid | Solo juegos de esa categoría |
| `from` | date-time UTC | `Period` inicio inclusive |
| `to` | date-time UTC | `Period` fin inclusive |

**Response 200:**

```json
{
  "playerId": "guid",
  "gamesPlayed": 4,
  "gamesWon": 2,
  "gamesLost": 1,
  "gamesWithdrawn": 1,
  "questionsAnswered": 20,
  "correctAnswers": 14,
  "accuracy": 70.0,
  "pointsEarned": 350,
  "pointsRedeemed": 100
}
```

`accuracy` = `correct/answered*100` o `null` si 0.

**Errores**: 400 si `from`>`to`, 403 sin permiso.

### GET /api/reports/questions/{questionId}

`QuestionReport` por pregunta.

**Query params**: `gameId` (opcional), `categoryId` (opcional), `from`/`to` (Period)

**Response 200:**

```json
{
  "questionId": "guid",
  "categoryId": "guid",
  "categoryName": "string",
  "difficulty": "BASIC|INTERMEDIATE|ADVANCED|EXPERT",
  "timesPresented": 100,
  "correctAnswers": 80,
  "incorrectAnswers": 20,
  "accuracy": 80.0,
  "averageResponseTime": 4.2
}
```

`averageResponseTime` en segundos, `null` si sin evaluadas.

### GET /api/reports/categories/{categoryId}

`CategoryReport`.

**Query params**: `from`/`to` (Period)

**Response 200:**

```json
{
  "categoryId": "guid",
  "categoryName": "string",
  "questions": 12,
  "games": 10,
  "players": 25,
  "averageScore": 45.2,
  "averageAccuracy": 68.5
}
```

`players` = únicos. Promedios `null` si sin datos.

### GET /api/reports/rewards/{rewardId}  &  GET /api/reports/rewards

`RewardReport` por recompensa y listado global.

**Query params**: `from`/`to` (Period), `categoryId` (si recompensa vinculada)

**Response 200 (por rewardId):**

```json
{
  "rewardId": "guid",
  "rewardName": "string",
  "availableStock": 30,
  "redemptions": 20,
  "pointsConsumed": 2000,
  "pending": 8,
  "delivered": 12
}
```

`GET /api/reports/rewards` retorna `{ items: [RewardReport], total, page, pageSize }` paginado.

### GET /api/reports/leaderboard (extendido SPEC-011)

`Leaderboard` con filtros adicionales. Requiere `Report.Read`.

**Query params:**

| Param | Tipo | Descripción |
|-------|------|-------------|
| `gameId` | guid | Solo ese juego |
| `categoryId` | guid | Solo juegos de esa categoría |
| `from` | date-time | Period inicio |
| `to` | date-time | Period fin |

**Response 200:** mismo shape que `GET /api/games/{id}/leaderboard` (SPEC-011: `players: [LeaderboardEntry]` con `Rank`/`Points`/etc.), filtrado por periodo/categoría.

**Errores**: 400 si `from`>`to`, 403 sin `Report.Read`.

## Paginación y validación común

- Todos los listados usan `page` (default 1) / `pageSize` (default 20, max 100) + `total`.
- `from`/`to` validan `from` ≤ `to` (FR-007) → 400 `ValidationFailed`.
- Sin filtros = `Global` (SC-004).
- Filtros combinables son intersección (`AND`).

## Seguridad

- Todos requieren `Authorization` + `Report.Read` (mapeado a `ADMIN`/`GAME_MANAGER`/`REWARD_MANAGER` según `SecurityPolicies`); `PLAYER` puede ver su propio `PlayerReport` y `Leaderboard` si la política lo permite (ver `contracts/security-policies.md` de SPEC-013).
- Ningún endpoint crea `PointTransaction`/`AuditEntry` de escritura; 0 side-effects (SC-005).
- Respuestas nunca exponen `IsCorrect` previo ni secretos.

