# Quickstart: Operational Reporting (SPEC-015)

**Date**: 2026-08-28 | **Plan**: [plan.md](plan.md) | **Contracts**: [contracts/reporting-api.md](contracts/reporting-api.md), [contracts/reporting-queries.md](contracts/reporting-queries.md) | **Data model**: [data-model.md](data-model.md)

Guía de validación ejecutable de reportes de solo lectura. No contiene implementación — los detalles viven en `tasks.md` y el código. Valida que los reportes son `IQuery` sin side-effects y que los filtros `Global`/`Game`/`Category`/`Period` son combinables.

## Prerrequisitos

- .NET SDK 10.0 (`global.json`), Podman/Docker para Aspire y OroIdentityServer.
- Solución: `OroQuizClash.slnx`. Proyectos: `Domain.Tests`, `Application.Tests`, `Api.Tests`, `Architecture.Tests`, `AppHost`.
- OroIdentityServer (`oroidentityserver:latest`) para JWT reales (seed `admin`/`Admin@123456`). Tests automatizados usan dobles de identidad.

## 1. Validación automatizada (tests)

```bash
# Dominio: cálculos Accuracy, Winner, AverageResponseTime
dotnet test tests/OroQuizClash.Domain.Tests --filter "FullyQualifiedName~Reporting or FullyQualifiedName~Accuracy or FullyQualifiedName~Leaderboard"

# Application: handlers de reporte con datos InMemory, filtros y no side-effects
dotnet test tests/OroQuizClash.Application.Tests --filter "FullyQualifiedName~Report"

# Api: filtros Global/Game/Category/Period y 403 sin Report.Read
dotnet test tests/OroQuizClash.Api.Tests --filter "FullyQualifiedName~Report"

# Arquitectura: IQuery sin SaveChanges, Specification usada
dotnet test tests/OroQuizClash.Architecture.Tests --filter "FullyQualifiedName~Reporting"

# Suite completa
dotnet test OroQuizClash.slnx
```

**Resultados esperados**: todos verdes; en particular SC-005 (0 side-effects) y SC-006 (IQuery+Specification) verificados por inspección.

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

Actores: `admin` (ADMIN con `Report.Read`), `playerA/B` (PLAYER) creados en OroIdentityServer. Obtener JWT vía OIDC `http://localhost:5080/connect/token` (`password` grant para pruebas). Notación: `$TOKEN_ADMIN`, `$TOKEN_A`, `$API` = URL oroclash-api.

### 3a. GameReport + Leaderboard (US1, SC-001/SC-009)

```bash
# Crear 2-3 juegos en estados distintos con 5 rondas cada uno (vía SPEC-004 flow: ready → open-lobby → start → startRound ×5 → finish)
# Guardar $GAME_FINISHED, $GAME_INPROGRESS

# GameReport por gameId
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/reports/games/$GAME_FINISHED" | jq .
# → { gameId, name, start, end, players: [4], rounds: [5], winner: {playerId}, totalQuestions:5 }

# GameReport para juego inexistente → 404
curl -s -o /dev/null -w "%{http_code}\n" -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/reports/games/00000000-0000-0000-0000-000000000000"
# → 404

# Leaderboard Global
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/reports/leaderboard" | jq '.players | length'
# → ranking global

# Leaderboard filtrado por Game
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/reports/leaderboard?gameId=$GAME_FINISHED" | jq .

# Leaderboard filtrado por Period (2026-08-01 a 2026-08-28)
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/reports/leaderboard?from=2026-08-01T00:00:00Z&to=2026-08-28T23:59:59Z" | jq .

# Verificar 0 side-effects: contar PointTransaction antes y después (vía GET /api/games/$GAME_ID/leaderboard o directamente audit)
# → no incrementa
```

### 3b. PlayerReport (US2, SC-002)

```bash
# Player con 5 juegos (2 ganadas,1 perdida,1 retirada,1 en curso), 20 respuestas (14 correctas, 350 pts, 100 canjeados)
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/reports/players/$PLAYER_A?from=2026-07-28T00:00:00Z&to=2026-08-28T23:59:59Z" | jq .
# → { gamesPlayed:4, gamesWon:2, gamesLost:1, gamesWithdrawn:1, questionsAnswered:20, correctAnswers:14, accuracy:70.0, pointsEarned:350, pointsRedeemed:100 }

# Filtro Game
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/reports/players/$PLAYER_A?gameId=$GAME_FINISHED" | jq .

# Filtro Category
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/reports/players/$PLAYER_A?categoryId=$CAT_ID" | jq .

# Dos ejecuciones idénticas → mismo resultado, 0 side-effects
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/reports/players/$PLAYER_A" > /tmp/r1.json
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/reports/players/$PLAYER_A" > /tmp/r2.json
diff /tmp/r1.json /tmp/r2.json && echo "SC-005 pass"
```

### 3c. QuestionReport (US3, SC-003)

```bash
# Pregunta "Capital de Francia?" con 100 presentaciones y 80 aciertos
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/reports/questions/$Q1_ID" | jq .
# → { timesPresented:100, correctAnswers:80, incorrectAnswers:20, accuracy:80.0, averageResponseTime:4.2 }

# Pregunta nunca presentada
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/reports/questions/$Q_NEW" | jq .
# → { timesPresented:0, averageResponseTime:null }

# Filtro Category + Period
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/reports/questions/$Q1_ID?categoryId=$CAT_ID&from=2026-08-21T00:00:00Z&to=2026-08-28T23:59:59Z" | jq .
```

### 3d. CategoryReport + RewardReport (US4, SC-007/SC-008)

```bash
# Category "Ciencia" con 12 preguntas, 10 juegos, 25 jugadores
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/reports/categories/$CAT_ID?from=2026-07-28T00:00:00Z&to=2026-08-28T23:59:59Z" | jq .
# → { questions:12, games:10, players:25, averageScore:45.2, averageAccuracy:68.5 }

# Reward "Voucher 100pts" con Stock 50, 20 canjes (12 DELIVERED, 8 PENDING)
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/reports/rewards/$REWARD_ID" | jq .
# → { availableStock:30, redemptions:20, pointsConsumed:2000, pending:8, delivered:12 }

# RewardReport fuera de periodo
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/reports/rewards/$REWARD_ID?from=2020-01-01T00:00:00Z&to=2020-01-02T00:00:00Z" | jq .
# → { redemptions:0, pointsConsumed:0 }

# Listado global de RewardReports paginado
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/reports/rewards?page=1&pageSize=10" | jq .
```

### 3e. Filtros combinables y no-mutación (US5, SC-004/SC-005/SC-006)

```bash
# Global sin filtros
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/reports/leaderboard" | jq '.players | length'

# Game + Period
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/reports/leaderboard?gameId=$GAME_FINISHED&from=2026-08-01T00:00:00Z&to=2026-08-28T23:59:59Z" | jq .

# Category + Period para QuestionReport
curl -s -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/reports/questions/$Q1_ID?categoryId=$CAT_ID&from=2026-08-01T00:00:00Z&to=2026-08-28T23:59:59Z" | jq .

# Period inválido from > to → 400
curl -s -o /dev/null -w "%{http_code}\n" -H "Authorization: Bearer $TOKEN_ADMIN" "$API/api/reports/players/$PLAYER_A?from=2026-08-28T00:00:00Z&to=2026-08-01T00:00:00Z"
# → 400

# Sin Report.Read → 403
curl -s -o /dev/null -w "%{http_code}\n" -H "Authorization: Bearer $TOKEN_PLAYER_NO_REPORT" "$API/api/reports/games/$GAME_FINISHED"
# → 403

# Verificar IQuery + Specification por inspección (SC-006): handlers usan Specification y ApplyAsNoTracking, sin SaveChanges
grep -r "IQuery" src/OroQuizClash.Application/Features/Reporting --include="*.cs" | head
```

## 4. Criterios de aceptación E2E (trazabilidad a Success Criteria)

| Paso | SC | Verificación |
|------|----|--------------|
| 3a | SC-001/SC-009 | GameReport 100% vs Game/Rounds/PointTransaction; Leaderboard determinista filtrado |
| 3b | SC-002/SC-004 | PlayerReport 4/2/1/1, 20/14/70%, 350/100, intersección correcta por Game/Category/Period |
| 3c | SC-003 | 100/80/20/80% y avg 4.2s <1% error, fácil/difícil detectada |
| 3d | SC-007/SC-008 | Category 12/10/25, avg <1% error; Reward 30/20/2000/8/12 exactos |
| 3e | SC-004/SC-005/SC-006 | Combinaciones intersección 100%, 0 side-effects en 2 ejecuciones, IQuery+Specification, 400 si from>to |

## 5. Limpieza

```bash
aspire stop
aspire destroy # opcional
```

