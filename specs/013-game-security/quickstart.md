# Quickstart: Game Security (SPEC-013)

**Date**: 2026-08-28 | **Plan**: [plan.md](plan.md) | **Contracts**: [contracts/security-policies.md](contracts/security-policies.md), [contracts/audit-api.md](contracts/audit-api.md), [contracts/rate-limiting.md](contracts/rate-limiting.md) | **Data model**: [data-model.md](data-model.md)

Guía de validación ejecutable del endurecimiento transversal. No contiene implementación — los detalles viven en `tasks.md` y el código.

## Prerrequisitos

- .NET SDK 10.0 (`global.json`), Podman/Docker para Aspire y OroIdentityServer.
- Solución: `OroQuizClash.slnx`. Proyectos: `Domain.Tests`, `Application.Tests`, `Api.Tests`, `Architecture.Tests`, `AppHost`.
- OroIdentityServer (`oroidentityserver:latest`) para JWT reales (seed `admin`/`Admin@123456`). Los tests automatizados usan dobles de identidad/JWT.

## 1. Validación automatizada (tests)

```bash
# Dominio: matriz permisos, anti-tampering
dotnet test tests/OroQuizClash.Domain.Tests --filter "FullyQualifiedName~Security or FullyQualifiedName~Permission or FullyQualifiedName~Role"

# Application: AuthorizationBehavior, AuditBehavior, idempotencia/anti-replay
dotnet test tests/OroQuizClash.Application.Tests --filter "FullyQualifiedName~Authorization or FullyQualifiedName~Audit or FullyQualifiedName~Idempotency"

# Api: 401/403/429, partición rate limiting, audit read auth
dotnet test tests/OroQuizClash.Api.Tests --filter "FullyQualifiedName~Security or FullyQualifiedName~Audit or FullyQualifiedName~RateLimit"

# Arquitectura: deny-by-default, audit append-only
dotnet test tests/OroQuizClash.Architecture.Tests --filter "FullyQualifiedName~Security"

# Suite completa
dotnet test OroQuizClash.slnx
```

**Resultados esperados**: todos verdes; en particular SC-001 (14×4 matriz), SC-002 (0% tampering efectivo), SC-003 (idempotencia sin duplicación), SC-006 (401 sin fuga).

## 2. Arranque del stack

```bash
export symmetric_security_key="$(openssl rand -base64 32)"
export seed_admin_password="Admin@123456"
aspire start
# Dashboard: https://localhost:17113 — API: recurso oroclash-api
```

## 3. Escenarios E2E (transversales)

Actores: `admin` (ADMIN), `manager` (GAME_MANAGER), `playerA/B` (PLAYER), `rewarder` (REWARD_MANAGER) creados en OroIdentityServer. Obtener JWT vía OIDC `http://localhost:5080/connect/token`. Notación: `$TOKEN_*`, `$API` = URL oroclash-api.

### 3a. RBAC — matriz 14×4 (SC-001/SC-006)

```bash
# PLAYER intenta Category.Publish → 403
curl -s -o /dev/null -w "%{http_code}\n" -X POST $API/api/categories -H "Authorization: Bearer $TOKEN_PLAYER" -H "Content-Type: application/json" -d '{"name":"Hack"}'
# → 403

# PLAYER intenta Game.Start → 403
curl -s -X POST $API/api/games/$GAME_ID/start -H "Authorization: Bearer $TOKEN_PLAYER" | grep -q "403" && echo "SC-001 pass"

# Sin token → 401 sin fuga de existencia
curl -s -o /dev/null -w "%{http_code}\n" $API/api/games/$GAME_ID/leaderboard
# → 401

# ADMIN puede todo (crea categoría)
curl -s -X POST $API/api/categories -H "Authorization: Bearer $TOKEN_ADMIN" -H "Content-Type: application/json" -d '{"name":"SecCat"}' | grep -q "201" && echo "ADMIN pass"
```

### 3b. Anti-tampering — servidor autoridad (SC-002)

```bash
# Enviar respuesta con score inventado, correctness, time, playerId ajeno, gameState
curl -s -X POST $API/api/games/$GAME_ID/answers -H "Authorization: Bearer $TOKEN_A" -H "Content-Type: application/json" \
  -d '{"answerOptionId":"<valid>","score":9999,"correctness":true,"elapsedTime":1,"playerId":"<other>","gameState":"FINISHED"}'
# → servidor ignora campos extra, evalúa con pregunta real y sub claim; puntaje no es 9999

# Verificar answerOptionId fuera de ronda → 400
curl -s -X POST $API/api/games/$GAME_ID/answers -H "Authorization: Bearer $TOKEN_A" -H "Content-Type: application/json" \
  -d '{"answerOptionId":"00000000-0000-0000-0000-000000000000"}' | grep -q "400" && echo "FR-009 pass"

# questionId manipulada → rechazo
```

### 3c. Idempotencia y anti-replay (SC-003/SC-004/SC-005)

```bash
# Idempotencia respuestas: mismo (GameId,PlayerId,RoundId) → segundo envío retorna mismo answerId sin nuevo ledger
curl -s -X POST $API/api/games/$GAME_ID/answers -H "Authorization: Bearer $TOKEN_A" -H "Content-Type: application/json" -d '{"answerOptionId":"<opt>"}' > /tmp/r1.json
curl -s -X POST $API/api/games/$GAME_ID/answers -H "Authorization: Bearer $TOKEN_A" -H "Content-Type: application/json" -d '{"answerOptionId":"<other>"}' > /tmp/r2.json
# → r1.answerId == r2.answerId, ledger sin duplicados

# Idempotency-Key para redeem
curl -s -X POST $API/api/rewards/$REWARD_ID/redeem -H "Authorization: Bearer $TOKEN_A" -H "Idempotency-Key: 11111111-1111-1111-1111-111111111111" -H "Content-Type: application/json" -d '{"gameId":"'$GAME_ID'"}'
curl -s -X POST $API/api/rewards/$REWARD_ID/redeem -H "Authorization: Bearer $TOKEN_A" -H "Idempotency-Key: 11111111-1111-1111-1111-111111111111" -H "Content-Type: application/json" -d '{"gameId":"'$GAME_ID'"}'
# → segundo retorna mismo redemptionId, sin segundo ledger

# Anti-replay: mismo Key con payload distinto → 400 ReplayDetected
curl -s -X POST $API/api/rewards/$REWARD_ID/redeem -H "Authorization: Bearer $TOKEN_A" -H "Idempotency-Key: 11111111-1111-1111-1111-111111111111" -H "Content-Type: application/json" -d '{"gameId":"other"}' | grep -q "400" && echo "SC-004 pass"

# Ráfaga 50 idénticas en 1s → solo 1 efecto
for i in $(seq 1 50); do curl -s -X POST $API/api/games/$GAME_ID/answers -H "Authorization: Bearer $TOKEN_A" -H "Content-Type: application/json" -d '{"answerOptionId":"<opt>"}' & done; wait
# → verificar ledger solo 1 transacción para esa ronda
```

### 3d. Rate limiting particionado (SC-009)

```bash
# Ráfaga 10 req/s en game-A por playerA → 429 a partir de 6ª
for i in $(seq 1 10); do curl -s -o /dev/null -w "%{http_code} " -X POST $API/api/games/$GAME_A/answers -H "Authorization: Bearer $TOKEN_A" -H "Content-Type: application/json" -d '{"answerOptionId":"<opt>"}'; done
# → primeras 5: 200/400 (según ronda), resto 429 con Retry-After

# Verificar aislamiento: mientras A es limitado en game-A, A en game-B y B en game-A no son limitados
curl -s -X POST $API/api/games/$GAME_B/answers -H "Authorization: Bearer $TOKEN_A" ... # → no 429
curl -s -X POST $API/api/games/$GAME_A/answers -H "Authorization: Bearer $TOKEN_B" ... # → no 429
```

### 3e. Auditoría (SC-007/SC-008)

```bash
# Consultar auditoría requiere Audit.Read (ADMIN)
curl -s $API/api/audit -H "Authorization: Bearer $TOKEN_ADMIN" | head -c 200
# → 200 con items

# PLAYER sin Audit.Read → 403 sin fuga
curl -s -o /dev/null -w "%{http_code}\n" $API/api/audit -H "Authorization: Bearer $TOKEN_PLAYER"
# → 403

# Correlación: crear juego con X-Correlation-ID y recuperarlo
CID=$(uuidgen)
curl -s -X POST $API/api/games -H "Authorization: Bearer $TOKEN_ADMIN" -H "X-Correlation-ID: $CID" -H "Content-Type: application/json" -d '{"name":"AuditGame",...}' 
curl -s "$API/api/audit?correlationId=$CID" -H "Authorization: Bearer $TOKEN_ADMIN" | grep -q "$CID" && echo "SC-007 pass"

# Inmutabilidad: intentar PUT/DELETE /api/audit/{id} → 405
```

## 4. Criterios de aceptación E2E (trazabilidad)

| Paso | SC | Verificación |
|------|----|--------------|
| 3a | SC-001/SC-006 | 14×4 matriz: permitidos 200/201, denegados 403, anónimos 401 |
| 3b | SC-002/SC-008 | Tampering ignorado, respuestas 200 con cálculo servidor, sin fuga |
| 3c | SC-003/SC-004/SC-005 | Duplicado retorna mismo id, sin ledger duplicado, replay 400 |
| 3d | SC-009 | Partición por juego/jugador, otros no afectados |
| 3e | SC-007/SC-008 | Audit append-only, correlación, 403 sin Audit.Read |

## 5. Limpieza

```bash
aspire stop
aspire destroy # opcional
```
