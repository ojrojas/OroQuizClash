# Quickstart: Realtime Game Events (SPEC-012)

**Date**: 2026-08-27 | **Plan**: [plan.md](plan.md) | **Contracts**: [contracts/gamehub.md](contracts/gamehub.md), [contracts/realtime.payloads.yaml](contracts/realtime.payloads.yaml) | **Data model**: [data-model.md](data-model.md)

Guía de validación ejecutable del contrato de tiempo real. No contiene implementación — los detalles viven en `tasks.md` y el código. Extiende el quickstart de SPEC-011 (multiplayer) con validación de los 9 eventos y la regla "DB = fuente de verdad".

## Prerrequisitos

- .NET SDK 10.0 (`global.json`), Podman/Docker para Aspire y OroIdentityServer.
- Solución: `OroQuizClash.slnx`. Proyectos relevantes: `OroQuizClash.Domain.Tests`, `OroQuizClash.Application.Tests`, `OroQuizClash.Api.Tests`, `OroQuizClash.Architecture.Tests`, `OroQuizClash.AppHost`.
- Identidad: OroIdentityServer (`oroidentityserver:latest`) es obligatorio para el flujo E2E con JWT reales (seed `admin`/`Admin@123456`). Los tests automatizados no requieren el contenedor (usan dobles de identidad y `Hub` test doubles).
- El hub de SPEC-011 ya está cableado (`/hubs/game`, `RequireAuthorization`, `AddSignalR`, `MapHub`).

## 1. Validación automatizada (tests)

```bash
# Dominio: payloads anti-trampa (QuestionPresented sin IsCorrect, PlayerAnswered sin opción/correctitud)
dotnet test tests/OroQuizClash.Domain.Tests --filter "FullyQualifiedName~Realtime"

# Aplicación: mapeo domain event → broadcast (los 9 eventos), best-effort (broadcast failure ≠ excepción), LeaderboardUpdated snapshot
dotnet test tests/OroQuizClash.Application.Tests --filter "FullyQualifiedName~Realtime|FullyQualifiedName~Broadcast"

# Api: hub auth/grupos (JoinGameGroup rechaza no-miembros, acepta organizadores), contrato de nombres de mensaje
dotnet test tests/OroQuizClash.Api.Tests --filter "FullyQualifiedName~GameHub|FullyQualifiedName~Realtime"

# Arquitectura: hub no referencia Domain directamente, IGameNotificationsBroadcaster vive en Application
dotnet test tests/OroQuizClash.Architecture.Tests --filter "FullyQualifiedName~Realtime"

# Suite completa (gate final — debe incluir la suite de SPEC-011 sin regresiones)
dotnet test OroQuizClash.slnx
```

**Resultados esperados**: todos los tests verdes; en particular:

- `QuestionPresented` nunca contiene `isCorrect`/`correctOptionId` (SC-006).
- `PlayerAnswered` nunca contiene `answerOptionId`/`correct`/`points` (FR-005).
- Broadcast failure en cualquier handler → logueado, sin propagar excepción, operación persiste (SC-004 / FR-016).
- `LeaderboardUpdated` coincide 100% con `GET /api/games/{gameId}/leaderboard` en el mismo momento (SC-008).

## 2. Arranque del stack completo (E2E)

```bash
# Secretos locales requeridos por el AppHost
export symmetric_security_key="$(openssl rand -base64 32)"
export seed_admin_password="Admin@123456"

# Levanta sqlserver + postgres + redis + rabbitmq + identity-server + oroclash-api
aspire start
# Dashboard: https://localhost:17113 — API: recurso oroclash-api (https://localhost:5001 o la URL del dashboard)
```

Alternativa mínima sin Aspire (SQLite local, requiere identity server alcanzable para JWT):

```bash
dotnet run --project src/OroQuizClash.Api
```

## 3. Escenario E2E: partida con validación de tiempo real

Actores: `admin` (organizador) + 3 jugadores (A, B, C) creados en OroIdentityServer. Obtener JWT vía OIDC (`/connect/token`, password grant para pruebas) contra `http://localhost:5080`. Notación: `$TOKEN_ORG`, `$TOKEN_A`, `$TOKEN_B`, `$TOKEN_C`; `$API` = URL base de oroclash-api.

### 3a. Preparar juego y conectar clientes realtime

```bash
# Crear juego, ready, open-lobby (SPEC-001/004) — como en SPEC-011 quickstart
curl -s -X POST $API/api/games -H "Authorization: Bearer $TOKEN_ORG" -H "Content-Type: application/json" \
  -d '{"name":"Clash 012","categoryId":"<categoria-publicada>","minRounds":5,"maxRounds":5,"minPlayers":2,"maxPlayers":10,"timeLimitPerQuestionSeconds":30,"pointsPerRound":10}'
# → 201, guardar $GAME_ID. Luego: POST /api/games/$GAME_ID/ready y POST /api/games/$GAME_ID/open-lobby

# Conectar 3 clientes SignalR a /hubs/game con JWT y unirse al grupo (ver contracts/gamehub.md)
# Cada cliente: new HubConnectionBuilder().withUrl("$API/hubs/game", { accessTokenFactory: () => $TOKEN_X }).build()
# Luego: await connection.invoke("JoinGameGroup", $GAME_ID)
# Suscribirse a los 9 mensajes: GameStarted, PlayerJoined, RoundStarted, QuestionPresented,
#                               PlayerAnswered, ScoreUpdated, LeaderboardUpdated, RoundCompleted, GameFinished

# Aislamiento negativo: intentar JoinGameGroup con un usuario D que NO es jugador ni organizador → HubException
```

### 3b. Lobby en vivo

```bash
curl -s -X POST $API/api/games/$GAME_ID/players -H "Authorization: Bearer $TOKEN_A"
curl -s -X POST $API/api/games/$GAME_ID/players -H "Authorization: Bearer $TOKEN_B"
curl -s -X POST $API/api/games/$GAME_ID/players -H "Authorization: Bearer $TOKEN_C"
# → Clientes conectados observan 3× PlayerJoined (SC-001). Verificar que D (no-miembro) no recibe nada (SC-005).
```

### 3c. Inicio del juego

```bash
curl -s -X POST $API/api/games/$GAME_ID/start -H "Authorization: Bearer $TOKEN_ORG"
# → Todos los conectados reciben GameStarted { gameId } (SC-001).
```

### 3d. Ronda en vivo (repetir por cada ronda)

```bash
curl -s -X POST $API/api/games/$GAME_ID/rounds/start -H "Authorization: Bearer $TOKEN_ORG"
# → Todos reciben RoundStarted { roundId, roundNumber } y acto seguido QuestionPresented
#   con { questionId, text, answerOptions: [{id,text}] } — verificar que NINGUNA opción trae isCorrect (SC-006)

# Respuestas simultáneas (lanzar en paralelo)
curl -s -X POST $API/api/games/$GAME_ID/answers -H "Authorization: Bearer $TOKEN_A" -H "Content-Type: application/json" -d '{"answerOptionId":"<opcion-A>"}' &
curl -s -X POST $API/api/games/$GAME_ID/answers -H "Authorization: Bearer $TOKEN_B" -H "Content-Type: application/json" -d '{"answerOptionId":"<opcion-B>"}' &
curl -s -X POST $API/api/games/$GAME_ID/answers -H "Authorization: Bearer $TOKEN_C" -H "Content-Type: application/json" -d '{"answerOptionId":"<opcion-C>"}' &
wait
# → Por cada respuesta, los demás reciben PlayerAnswered { playerId, roundId } sin opción ni correctitud (SC-006),
#   luego ScoreUpdated { playerId, points, totalPoints } y LeaderboardUpdated { entries } (SC-001).
#   Verificar que LeaderboardUpdated.entries coincide con GET /api/games/$GAME_ID/leaderboard (SC-008).

curl -s -X POST $API/api/games/$GAME_ID/rounds/complete -H "Authorization: Bearer $TOKEN_ORG"
# → Todos reciben RoundCompleted { roundId, roundNumber } y LeaderboardUpdated refrescado.
```

### 3e. Fin del juego

```bash
# Tras completar las rondas configuradas:
curl -s -X POST $API/api/games/$GAME_ID/finish -H "Authorization: Bearer $TOKEN_ORG"
# → Todos reciben GameFinished { status, entries } con leaderboard final (SC-001).
```

### 3f. Resiliencia — desconexión y fuente de verdad

```bash
# 1) Desconectar al cliente de A en medio de la ronda 2, avanzar rondas 2-3, reconectar a A:
#    A re-consulta: GET /api/games/$GAME_ID, GET /api/games/$GAME_ID/rounds/current,
#                   GET /api/games/$GAME_ID/questions/current, GET /api/games/$GAME_ID/players/<id>/state,
#                   GET /api/games/$GAME_ID/leaderboard
# → A recupera el estado completo sin haber recibido los eventos intermedios (SC-003).

# 2) Simular caída de SignalR (detener el hub o desconectar todos): jugar una ronda completa
#    solo vía REST — debe completarse sin errores (SC-004 / FR-018).

# 3) Verificar que cada evento recibido coincide con la consulta REST equivalente
#    (p. ej. LeaderboardUpdated.entries vs GET /leaderboard en el mismo instante — SC-008).
```

## 4. Criterios de aceptación E2E (trazabilidad a Success Criteria)

| Paso | Success Criterion | Verificación |
|------|-------------------|--------------|
| 3b, 3c, 3d, 3e | SC-001 | Los 9 tipos aparecen sin recarga para todos los conectados |
| 3d (QuestionPresented) | SC-006 | `answerOptions` sin `isCorrect` (inspección + test anti-trampa) |
| 3d (PlayerAnswered) | SC-006 | payload sin `answerOptionId`/`correct`/`points` |
| 3a (D no-miembro) | SC-005 | D no recibe ningún evento de $GAME_ID (juego simultáneo) |
| 3f-1 | SC-003 | Reconexión + re-consulta REST recupera estado completo en 1 ronda de consultas |
| 3f-2 | SC-004 | Ronda jugada sin SignalR → 100% de operaciones con éxito |
| 3d (LeaderboardUpdated) | SC-008 | `entries` idéntico a `GET /leaderboard` en el mismo momento |
| SC-002/SC-007 | SC-002, SC-007 | Percepción <2s en red normal; con 20 juegos × 4 jugadores sin fugas (validación manual) |

## 5. Limpieza

```bash
aspire stop          # detiene el stack (volúmenes persistentes conservan datos)
aspire destroy       # opcional: elimina recursos y volúmenes
```
