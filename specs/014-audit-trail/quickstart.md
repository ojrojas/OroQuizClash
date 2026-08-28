# Quickstart: Audit Trail (SPEC-014)

**Date**: 2026-08-28 | **Plan**: [plan.md](plan.md) | **Contracts**: [contracts/audit-api.md](contracts/audit-api.md), [contracts/audit-events.md](contracts/audit-events.md) | **Data model**: [data-model.md](data-model.md)

Guía de validación ejecutable de la auditoría transversal. No contiene implementación — los detalles viven en `tasks.md` y el código. Extiende el quickstart de SPEC-013 (que ya validaba `AuditEntry` de seguridad) con los 16 `Action` de dominio.

## Prerrequisitos

- .NET SDK 10.0 (`global.json`), Podman/Docker para Aspire y OroIdentityServer.
- Solución: `OroQuizClash.slnx`. Proyectos: `Domain.Tests`, `Application.Tests`, `Api.Tests`, `Architecture.Tests`, `AppHost`.
- OroIdentityServer (`oroidentityserver:latest`) para JWT reales (seed `admin`/`Admin@123456`). Los tests automatizados usan dobles de identidad.

## 1. Validación automatizada (tests)

```bash
# Dominio: inmutabilidad y catálogo 16 Action
dotnet test tests/OroQuizClash.Domain.Tests --filter "FullyQualifiedName~Audit"

# Application: AuditBehavior mapea 16 Action, CorrelationId, Data sanitizada, no condiciona negocio
dotnet test tests/OroQuizClash.Application.Tests --filter "FullyQualifiedName~Audit"

# Api: búsqueda paginada por GameId/PlayerId/Action/CorrelationId, 403 sin Audit.Read
dotnet test tests/OroQuizClash.Api.Tests --filter "FullyQualifiedName~Audit"

# Arquitectura: append-only, transversalidad, Audit no referenciado por handlers de dominio
dotnet test tests/OroQuizClash.Architecture.Tests --filter "FullyQualifiedName~Audit"

# Suite completa (gate final, debe incluir SPEC-013 sin regresiones)
dotnet test OroQuizClash.slnx
```

**Resultados esperados**: todos verdes; en particular:

- `AuditEntry` no expone `Update`/`Delete` (SC-002).
- `AuditBehavior` genera exactamente un `AuditRecord` por cada uno de los 16 `Action` (SC-001).
- Handlers de dominio no referencian `IRepository<AuditEntry>` (SC-006).
- Búsqueda por `GameId`/`CorrelationId` con 1000 registros no degrada (SC-007).

## 2. Arranque del stack (E2E)

```bash
export symmetric_security_key="$(openssl rand -base64 32)"
export seed_admin_password="Admin@123456"
aspire start
# Dashboard: https://localhost:17113 — API: recurso oroclash-api (https://localhost:5001 o URL del dashboard)
```

Alternativa mínima sin Aspire (SQLite local, requiere identity server alcanzable para JWT):

```bash
dotnet run --project src/OroQuizClash.Api
```

## 3. Escenarios E2E (transversales)

Actores: `admin` (ADMIN con `Audit.Read`), `manager` (GAME_MANAGER), `playerA/B` (PLAYER) creados en OroIdentityServer. Obtener JWT vía OIDC `http://localhost:5080/connect/token` (`password` grant para pruebas). Notación: `$TOKEN_ADMIN`, `$TOKEN_PLAYER`, `$API` = URL oroclash-api. `CID` = CorrelationId fresco por flujo.

### 3a. Ciclo de vida — 6 eventos (US1)

```bash
CID=$(uuidgen)
# Crear y configurar juego, iniciar, unir jugador, iniciar ronda
curl -s -H "X-Correlation-ID: $CID" -H "Authorization: Bearer $TOKEN_ADMIN" -X POST $API/api/games -H "Content-Type: application/json" -d '{"name":"AuditGame","categoryId":"...","minRounds":5}' | jq .gameId
# → guarda $GAME_ID
curl -s -H "X-Correlation-ID: $CID" -H "Authorization: Bearer $TOKEN_ADMIN" -X POST $API/api/games/$GAME_ID/players -H "Content-Type: application/json" -d '{}' # en realidad Join usa token de player
curl -s -H "X-Correlation-ID: $CID" -H "Authorization: Bearer $TOKEN_ADMIN" -X POST $API/api/games/$GAME_ID/rounds/start -H "Content-Type: application/json" -d '{}'

# Verificar trail por GameId
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/audit?gameId=$GAME_ID&pageSize=100" | jq '.items | map(.action)'
# → contiene GameCreated, GameConfigured, GameStarted, PlayerJoined, RoundStarted, QuestionPresented

# Verificar traza por CorrelationId (mismo flujo)
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/audit?correlationId=$CID" | jq '.items | length'
# → 6+ registros, ordenados por timestamp

# Intentar modificar audit → 405
curl -s -X PUT $API/api/audit/$SOME_ID -H "Authorization: Bearer $TOKEN_ADMIN" -d '{}' | grep -q "405" && echo "SC-002 pass"
```

### 3b. Jugadas y puntuación — 4 eventos (US2)

```bash
CID2=$(uuidgen)
# SubmitAnswer (playerA responde)
curl -s -H "X-Correlation-ID: $CID2" -H "Authorization: Bearer $TOKEN_PLAYER" -X POST $API/api/games/$GAME_ID/answers -H "Content-Type: application/json" -d '{"answerOptionId":"..."}'

# Verificar AnswerSubmitted / AnswerEvaluated / PointsAwarded o PointsRemoved comparten CorrelationId
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/audit?correlationId=$CID2" | jq '.items | map(.action)'
# → ["AnswerSubmitted","AnswerEvaluated","PointsAwarded"] o ["AnswerSubmitted","AnswerEvaluated","PointsRemoved"]

# Verificar Data no contiene IsCorrect previo y Result es Succeeded
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/audit?action=AnswerSubmitted&gameId=$GAME_ID" | jq '.items[0].data | contains("IsCorrect")'
# → false (SC-012)

# Repetir con mismo Idempotency-Key — debe generar segundo AuditRecord pero no duplicar PointsAwarded (idempotencia de negocio manda)
```

### 3c. Salidas y cierre — 6 eventos (US3)

```bash
# Withdraw, Eliminate (si aplica), Finish, Redeem, Consolation, Adjustment
curl -s -H "Authorization: Bearer $TOKEN_PLAYER" -X POST $API/api/games/$GAME_ID/withdraw -H "Content-Type: application/json" -d '{}'
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" -X POST $API/api/games/$GAME_ID/finish -H "Content-Type: application/json" -d '{}'
curl -s -H "Authorization: Bearer $TOKEN_PLAYER" -X POST $API/api/rewards/xxx/redeem -H "Content-Type: application/json" -d '{"gameId":"'$GAME_ID'"}'

curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/audit?gameId=$GAME_ID&action=PlayerWithdrawn" | jq '.items | length'
# → >=1

curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/audit?gameId=$GAME_ID&action=RewardRedeemed" | jq '.items[0].playerId'
# → playerId del canjeador

# AdministrativeAdjustment (admin corrige puntos)
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" -X POST $API/api/admin/adjustments -H "Content-Type: application/json" -d '{"gameId":"'$GAME_ID'","playerId":"...","delta":10,"reason":"incidencia"}'
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/audit?action=AdministrativeAdjustment&gameId=$GAME_ID" | jq '.items[0].actor'
# → admin sub
```

### 3d. Búsqueda y trazabilidad — transversal (US4)

```bash
# Preparar 20 juegos con 50 eventos cada uno (script o bucle)
# Ya con 1000 registros, probar búsquedas

# Por GameId
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/audit?gameId=$GAME_ID&pageSize=100" | jq '.items | length'
# → 50, todos con gameId=$GAME_ID, ordenados por timestamp

# Por PlayerId
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/audit?playerId=$PLAYER_ID" | jq '.items | map(select(.playerId=="'$PLAYER_ID'")) | length'
# → filtra jugadas de ese jugador

# Por Action + Resource
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/audit?action=AnswerEvaluated&resource=Answer" | jq '.items | map(.action) | unique'
# → ["AnswerEvaluated"]

# Por CorrelationId (traza completa de una ronda)
CID3=$(uuidgen)
# ... ejecutar flujo completo con ese CID, luego
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/audit?correlationId=$CID3" | jq '.items | map(.action)'
# → secuencia en orden cronológico

# Paginación
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/audit?gameId=$GAME_ID&page=1&pageSize=10" | jq '.total, (.items|length)'
# → total=50, length=10; page=2 sin duplicados

# Solo lectura no genera audit (SC-005)
BEFORE=$(curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/audit?gameId=$GAME_ID&pageSize=100" | jq '.total')
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/audit?gameId=$GAME_ID&pageSize=100" > /dev/null
AFTER=$(curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/audit?gameId=$GAME_ID&pageSize=100" | jq '.total')
# → BEFORE == AFTER

# Sin permiso → 403 sin fuga
curl -s -H "Authorization: Bearer $TOKEN_PLAYER" "$API/api/audit?gameId=$GAME_ID" | grep -q "403" && echo "US4-5 pass"
```

## 4. Criterios de aceptación E2E (trazabilidad a Success Criteria)

| Paso | SC | Verificación |
|------|----|--------------|
| 3a | SC-001 | 6/6 Action del ciclo de vida presentes con 11 campos |
| 3b | SC-001/SC-006 | 4/4 Action de jugada/puntos, sin dependencia audit→negocio |
| 3c | SC-001/SC-002 | 6/6 Action terminales, 16 total en partida completa |
| 3a–3c + PUT | SC-002 | PUT/DELETE audit → 405, registro original intacto |
| 3d (GameId/PlayerId/CorrelationId/paginación) | SC-003/SC-004 | Búsquedas retornan conjuntos exactos, ordenados, paginados sin pérdidas |
| 3d (solo lectura) | SC-005 | Contador no aumenta tras GET |
| Env vs Borrar audit | SC-006 | Handlers no importan `IRepository<AuditEntry>` (arch test) y borrar audit no altera `Game.SubmitAnswer` |
| Carga 1000 registros | SC-007 | Búsqueda <500 ms p95, inserción <50 ms overhead (medir con `time` y logs) |

## 5. Limpieza

```bash
aspire stop
aspire destroy # opcional
```

