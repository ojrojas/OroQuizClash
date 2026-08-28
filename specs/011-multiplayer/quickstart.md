# Quickstart: Multiplayer (SPEC-011)

**Date**: 2026-08-27 | **Plan**: [plan.md](plan.md) | **Contracts**: [contracts/multiplayer.openapi.yaml](contracts/multiplayer.openapi.yaml), [contracts/gamehub.md](contracts/gamehub.md) | **Data model**: [data-model.md](data-model.md)

Guía de validación ejecutable del contrato multiplayer. No contiene implementación — los detalles viven en `tasks.md` y el código.

## Prerrequisitos

- .NET SDK 10.0 (`global.json`), Podman/Docker para Aspire y OroIdentityServer.
- Solución: `OroQuizClash.slnx`. Proyectos relevantes: `OroQuizClash.Domain.Tests`, `OroQuizClash.Infrastructure.Tests`, `OroQuizClash.Application.Tests`, `OroQuizClash.Api.Tests`, `OroQuizClash.Architecture.Tests`, `OroQuizClash.AppHost`.
- Identidad: OroIdentityServer (`oroidentityserver:latest`) es obligatorio para el flujo E2E con JWT reales (seed `admin`/`Admin@123456`). Los tests automatizados no requieren el contenedor (usan dobles de identidad).

## 1. Validación automatizada (tests)

```bash
# Dominio: participación (CurrentRound avance/congelación, AnswerState derivado, aislamiento)
dotnet test tests/OroQuizClash.Domain.Tests --filter "FullyQualifiedName~Multiplayer"

# Infraestructura: concurrencia optimista + idempotencia bajo envíos simultáneos (EF Sqlite)
dotnet test tests/OroQuizClash.Infrastructure.Tests --filter "FullyQualifiedName~GameConcurrency"

# Aplicación: ranking determinista del leaderboard + identidad en SubmitAnswer/Withdraw + GetPlayerState
dotnet test tests/OroQuizClash.Application.Tests --filter "FullyQualifiedName~Leaderboard|FullyQualifiedName~SubmitAnswerIdentity|FullyQualifiedName~GetPlayerState"

# Arquitectura: reglas de dependencia del slice multiplayer
dotnet test tests/OroQuizClash.Architecture.Tests --filter "FullyQualifiedName~Multiplayer"

# Contratos API: shape de respuestas leaderboard/player-state
dotnet test tests/OroQuizClash.Api.Tests --filter "FullyQualifiedName~Multiplayer"

# Suite completa (gate final)
dotnet test OroQuizClash.slnx
```

**Resultados esperados**: todos los tests verdes; en particular:

- Envíos simultáneos de 2+ jugadores → todos evaluados, sin actualizaciones perdidas ni duplicadas (SC-001).
- Envío duplicado mismo jugador+ronda → mismo resultado, una sola `PointTransaction` (SC-002).
- Mutación concurrente del mismo estado → el perdedor recibe conflicto recuperable (SC-006).
- Leaderboard con empates → orden determinista Points desc → CorrectAnswers desc → consecución más temprana (SC-004).

## 2. Arranque del stack completo (E2E)

```bash
# Secretos locales requeridos por el AppHost
export symmetric_security_key="$(openssl rand -base64 32)"
export seed_admin_password="Admin@123456"

# Levanta sqlserver + postgres + redis + rabbitmq + identity-server + oroclash-api
aspire start
# Dashboard: https://localhost:17113 — API: recurso oroclash-api
```

Alternativa mínima sin Aspire (SQLite local, requiere identity server alcanzable para JWT):

```bash
dotnet run --project src/OroQuizClash.Api
```

## 3. Escenario E2E: partida multiplayer concurrente

Actores: `admin` (organizador) + 3 jugadores (A, B, C) creados en OroIdentityServer. Obtener JWT vía flujo OIDC (`/connect/token`, password grant para pruebas locales) contra `http://localhost:5080`. Notación: `$TOKEN_ORG`, `$TOKEN_A`, `$TOKEN_B`, `$TOKEN_C`; `$API` = URL base de oroclash-api.

```bash
# 1) Crear y preparar juego (organizador) — SPEC-001/004
curl -s -X POST $API/api/games -H "Authorization: Bearer $TOKEN_ORG" -H "Content-Type: application/json" \
  -d '{"name":"Clash 011","categoryId":"<categoria-publicada>","minRounds":5,"maxRounds":5,"minPlayers":2,"maxPlayers":10,"timeLimitPerQuestionSeconds":30,"pointsPerRound":10}'
# → 201, guardar gameId. Luego: POST /api/games/{gameId}/ready y POST /api/games/{gameId}/open-lobby

# 2) Unir 3 jugadores (cada uno con SU token)
curl -s -X POST $API/api/games/$GAME_ID/players -H "Authorization: Bearer $TOKEN_A"
curl -s -X POST $API/api/games/$GAME_ID/players -H "Authorization: Bearer $TOKEN_B"
curl -s -X POST $API/api/games/$GAME_ID/players -H "Authorization: Bearer $TOKEN_C"
# → cada uno 200 GameResponse; duplicado de A → 409 PlayerAlreadyJoined (idempotencia de unión)

# 3) Iniciar juego y ronda 1 (organizador)
curl -s -X POST $API/api/games/$GAME_ID/start  -H "Authorization: Bearer $TOKEN_ORG"
curl -s -X POST $API/api/games/$GAME_ID/rounds/start -H "Authorization: Bearer $TOKEN_ORG"
# → GameStatus ROUND_IN_PROGRESS; CurrentRound de A/B/C = 1 (ver paso 6)

# 4) Respuestas SIMULTÁNEAS (A, B, C a la vez — lanzar en paralelo)
curl -s -X POST $API/api/games/$GAME_ID/answers -H "Authorization: Bearer $TOKEN_A" -H "Content-Type: application/json" -d '{"answerOptionId":"<opcion>"}' &
curl -s -X POST $API/api/games/$GAME_ID/answers -H "Authorization: Bearer $TOKEN_B" -H "Content-Type: application/json" -d '{"answerOptionId":"<opcion>"}' &
curl -s -X POST $API/api/games/$GAME_ID/answers -H "Authorization: Bearer $TOKEN_C" -H "Content-Type: application/json" -d '{"answerOptionId":"<opcion>"}' &
wait
# → 3 respuestas independientes (200 SubmitAnswerResponse), correct/points/elapsedTime server-side

# 5) Verificar protecciones
# 5a) Idempotencia: A reenvía la misma respuesta → 200 con el MISMO resultado, sin puntos duplicados
# 5b) Aislamiento: A intenta retirarse con playerId de B → 403 PlayerIdentityMismatch
curl -s -X POST $API/api/games/$GAME_ID/withdraw -H "Authorization: Bearer $TOKEN_A" -H "Content-Type: application/json" -d '{"playerId":"<playerId-de-B>"}'
# 5c) Fuera de ronda: enviar respuesta tras CompleteRound → 400 QuestionNotActive

# 6) Estado individual del jugador (FR-015)
curl -s $API/api/games/$GAME_ID/players/<playerId-A>/state -H "Authorization: Bearer $TOKEN_A"
# → PlayerStateResponse: status ACTIVE, currentRound 1, answerState EVALUATED/EXPIRED, score del ledger

# 7) Leaderboard (FR-011)
curl -s $API/api/games/$GAME_ID/leaderboard -H "Authorization: Bearer $TOKEN_A"
# → LeaderboardResponse con Rank/Player/Points/CorrectAnswers/CurrentLevel/Status por jugador, orden determinista

# 8) Notificaciones SignalR (FR-014): conectar cliente a $API/hubs/game con JWT,
#     JoinGameGroup(gameId) y observar PlayerJoined/ScoreUpdated/LeaderboardUpdated/PlayerStatusChanged
#     durante los pasos 2-7 (ver contracts/gamehub.md)
```

## 4. Criterios de aceptación E2E (trazabilidad a Success Criteria)

| Paso | Success Criterion | Verificación |
|------|-------------------|--------------|
| 4 | SC-001 | 3 envíos simultáneos → 3 resultados evaluados, saldos correctos |
| 5a | SC-002 | Reenvío de A → mismo `answerId`, ledger sin duplicados (`GET /api/games/{id}/score/{playerId}/ledger`) |
| 5b | SC-003 | Intento cross-player → 403, estado de B intacto (paso 6 con B) |
| 7 | SC-004 | Leaderboard coincide con ledger y orden estable en consultas repetidas |
| 4 | SC-005 | Latencia de cada envío comparable al caso de 1 jugador (<2×) |
| 5c + retry | SC-006 | Conflicto 409 → re-consulta de estado (paso 6) devuelve estado autoritativo |
| 6, 7 | SC-007 | Respuestas <1s con juego activo |
| Tras `finish` | SC-008 | Leaderboard final estable con Status final (WINNER/WITHDRAWN/ELIMINATED) |

## 5. Limpieza

```bash
aspire stop          # detiene el stack (volúmenes persistentes conservan datos)
aspire destroy       # opcional: elimina recursos y volúmenes
```
