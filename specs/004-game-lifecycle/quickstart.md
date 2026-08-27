# Quickstart: Game Lifecycle

**Feature**: `004-game-lifecycle` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Contracts**: [contracts/game-lifecycle.openapi.yaml](contracts/game-lifecycle.openapi.yaml), [contracts/game-events.openapi.yaml](contracts/game-events.openapi.yaml) | **Data Model**: [data-model.md](data-model.md)

Guía de validación end-to-end para el ciclo de 9 estados (`DRAFT→READY→WAITING_FOR_PLAYERS→IN_PROGRESS→ROUND_IN_PROGRESS↔ROUND_COMPLETED→FINISHED` + `CANCELLED`/`FORCED_FINISHED`) con gates (config válida, ≥5 válidas, `players≥MinPlayers`), ronda exclusiva, respuestas solo en `ROUND_IN_PROGRESS`, configuración inmutable y finalización solo desde válidos, con `rowversion` y 9 eventos.

## Prerequisites

- .NET 10 SDK, Podman, `dotnet ef` si aplica migraciones.
- `OroQuizClash.slnx` compilable (`dotnet build OroQuizClash.slnx`), `OroQuizClashDbContext` con `DbSet<Game>` + `GameRound`/`GamePlayer` (`HasMany`) + `DbSet<Category>` (002) + `DbSet<Question>` (003) + `OutboxMessages` ya migrado/EnsureCreated.
- `OroIdentityServer` `oroidentityserver:latest` corriendo (`podman build -f src/IdentityServer/IdentityServer/Dockerfile -t oroidentityserver:latest .` + `podman compose up -d` o vía `aspire start`), discovery `http://localhost:5080/.well-known/openid-configuration`, `Category` publicada con `IQuestionCounter` ≥5 válidas y `Question` PUBLISHED 4/1 (para `StartRound` selección).
- `IQuestionSelectionStrategy` `Random` (003) registrado; `ICategoryValidator`/`IQuestionCounter` reales (no stub) para `MarkReady` gate.

## Setup

```bash
# 1. AppHost (Aspire) — levanta sqlserver/postgres/redis/rabbitmq/identity-server/api
aspire start
aspire wait oroclash-api
# Dashboard: https://localhost:17113

# 2. Tokens
# ADMIN (ciclo: Create/MarkReady/Start/Cancel)
curl -X POST http://localhost:5080/connect/token \
  -d "grant_type=password&username=admin&password=Admin@123456&scope=openid profile email" \
  -u "oroclash-api:secret" > /tmp/token-admin.json
export TOKEN_ADMIN=$(jq -r .access_token /tmp/token-admin.json)
# PLAYER (Join/SubmitAnswer)
curl -X POST http://localhost:5080/connect/token \
  -d "grant_type=password&username=player1&password=Player@123456&scope=openid profile email" \
  -u "oroclash-api:secret" > /tmp/token-player.json
export TOKEN_PLAYER=$(jq -r .access_token /tmp/token-player.json)
export TOKEN_PLAYER2=$(jq -r .access_token /tmp/token-player2.json) # crear player2 via /api/users o reuse

# 3. Category publicada con 5 PUBLISHED (SPEC-002/003 quickstart)
# Si no existe, crear cat y 5 preguntas PUBLISHED:
# curl POST /api/categories ... -> $CAT_ID, luego POST /api/questions x5 + /publish x5, verificar GET /api/categories/$CAT_ID validQuestionsCount==5

# 4. Alternativa sin AppHost:
# dotnet run --project src/OroQuizClash.Api --urls http://localhost:5000
```

## Validation Scenarios

### P1 — Crear y preparar hasta WAITING_FOR_PLAYERS (DRAFT→READY→WAITING)

```bash
# Crear DRAFT con config válida SPEC-001
curl -X POST http://localhost:5000/api/games -H "Authorization: Bearer $TOKEN_ADMIN" -H "Content-Type: application/json" -d "{
  \"name\":\"Quiz Secundaria Historia\",\"categoryId\":\"$CAT_ID\",\"minRounds\":5,\"maxRounds\":10,\"initialDifficulty\":2,\"difficultyStrategy\":\"Linear\",\"timeLimitPerQuestion\":30,\"pointsPerRound\":10,\"minPlayers\":2,\"maxPlayers\":10,\"withdrawalPolicy\":\"KEEP_CURRENT_SCORE\",\"lossPolicy\":\"LOSE_CURRENT_ROUND\",\"consolationPolicy\":\"NONE\",\"scoringSystem\":\"STANDARD\",\"rewardRules\":{\"type\":\"POINTS\",\"threshold\":100}
}" | tee /tmp/game.json | jq
# Esperado: 201, Location: /api/games/{id}, body {id, status:\"DRAFT\", configuration:{...}, rowVersion}
export GAME_ID=$(jq -r .id /tmp/game.json)

# Crear con config inválida MinRounds=3 → 400 InvalidGameConfiguration (regla 1)
curl -s -w " %{http_code}\n" -X POST http://localhost:5000/api/games -H "Authorization: Bearer $TOKEN_ADMIN" -H "Content-Type: application/json" -d "{
  \"name\":\"Bad\",\"categoryId\":\"$CAT_ID\",\"minRounds\":3,\"maxRounds\":10,\"initialDifficulty\":2,\"difficultyStrategy\":\"Linear\",\"timeLimitPerQuestion\":30,\"pointsPerRound\":10,\"minPlayers\":2,\"maxPlayers\":10,\"withdrawalPolicy\":\"KEEP_CURRENT_SCORE\",\"lossPolicy\":\"LOSE_CURRENT_ROUND\",\"consolationPolicy\":\"NONE\",\"scoringSystem\":\"STANDARD\",\"rewardRules\":{\"type\":\"POINTS\"}
}" | tail

# MarkReady DRAFT→READY (gate categoría ≥5 válidas)
curl -X POST http://localhost:5000/api/games/$GAME_ID/ready -H "Authorization: Bearer $TOKEN_ADMIN" | jq .status
# → "READY", GameReady event en logs OTel

# MarkReady con categoría no publicable (si se archivó cat) → 400 CategoryNotReady, permanece DRAFT
# (probar con CAT_BAD con 0 válidas: POST /api/games con CAT_BAD → game2 DRAFT, POST /api/games/$GAME2_ID/ready → 400)

# OpenLobby READY→WAITING_FOR_PLAYERS
curl -X POST http://localhost:5000/api/games/$GAME_ID/open-lobby -H "Authorization: Bearer $TOKEN_ADMIN" | jq .status
# → "WAITING_FOR_PLAYERS"

# JoinPlayer 1 → PlayerJoined, sigue WAITING_FOR_PLAYERS
curl -X POST http://localhost:5000/api/games/$GAME_ID/players -H "Authorization: Bearer $TOKEN_PLAYER" -H "Content-Type: application/json" -d "{\"userId\": \"$(jq -r .sub /tmp/token-player.json | head -c 36)\"}" | jq .players
# → 1 player

# JoinPlayer 2 → 2 players, ya cumple MinPlayers=2
curl -X POST http://localhost:5000/api/games/$GAME_ID/players -H "Authorization: Bearer $TOKEN_PLAYER2" -H "Content-Type: application/json" -d "{}" | jq '.players|length'
# → 2

# Join duplicado mismo userId → 409 PlayerAlreadyJoined
curl -s -w " %{http_code}\n" -X POST http://localhost:5000/api/games/$GAME_ID/players -H "Authorization: Bearer $TOKEN_PLAYER" -H "Content-Type: application/json" -d "{}" | tail
# → 409

# Join después de Start (ver siguiente) → 400 InvalidGameState (lobby cerrado, no late join)
```

### P1 — Iniciar y ciclo de rondas (WAITING→IN_PROGRESS→ROUND loop)

```bash
# StartGame WAITING_FOR_PLAYERS→IN_PROGRESS (players≥MinPlayers)
curl -X POST http://localhost:5000/api/games/$GAME_ID/start -H "Authorization: Bearer $TOKEN_ADMIN" | jq .status
# → "IN_PROGRESS", GameStarted event, StartedAt != null, bloquea Update

# Start con jugadores insuficientes (crear game2 con MinPlayers=3 pero solo 2 joins) → 400 NotEnoughPlayers

# StartRound IN_PROGRESS→ROUND_IN_PROGRESS (selecciona PUBLISHED no usada)
curl -X POST http://localhost:5000/api/games/$GAME_ID/rounds/start -H "Authorization: Bearer $TOKEN_ADMIN" | tee /tmp/round1.json | jq
# → 200, {id, roundNumber:1, questionId, status:\"ROUND_IN_PROGRESS\", startedAt}
export ROUND1_ID=$(jq -r .id /tmp/round1.json)

# StartRound de nuevo sin CompleteRound → 400 RoundAlreadyInProgress
curl -s -w " %{http_code}\n" -X POST http://localhost:5000/api/games/$GAME_ID/rounds/start -H "Authorization: Bearer $TOKEN_ADMIN" | tail
# → 400

# SubmitAnswer solo en ROUND_IN_PROGRESS → 200 con correct/points, idempotente con idempotencyKey
curl -X POST http://localhost:5000/api/games/$GAME_ID/answers -H "Authorization: Bearer $TOKEN_PLAYER" -H "Content-Type: application/json" -d "{
  \"questionId\":\"$(jq -r .questionId /tmp/round1.json)\",\"answerOptionId\":\"$(curl -s http://localhost:5000/api/questions/$(jq -r .questionId /tmp/round1.json) -H \"Authorization: Bearer $TOKEN_PLAYER\" | jq -r '.answerOptions[] | select(.isCorrect==true) | .id')\",\"roundId\":\"$ROUND1_ID\",\"idempotencyKey\":\"11111111-1111-1111-1111-111111111111\"
}" | jq .correct
# → true/false + points

# Duplicado mismo idempotencyKey → mismo resultado sin duplicar PointTransaction

# SubmitAnswer en IN_PROGRESS sin ronda activa (si se completa ronda y no se inició siguiente) → 400 NoActiveRound (ver siguiente)

# CompleteRound ROUND_IN_PROGRESS→ROUND_COMPLETED
curl -X POST http://localhost:5000/api/games/$GAME_ID/rounds/$ROUND1_ID/complete -H "Authorization: Bearer $TOKEN_ADMIN" | jq .status
# → "ROUND_COMPLETED" (o IN_PROGRESS sin ronda activa según modelado, pero GET /api/games/$GAME_ID debe mostrar currentRound==null y status IN_PROGRESS o ROUND_COMPLETED)

# Siguiente StartRound permitido → ROUND_IN_PROGRESS roundNumber 2
curl -X POST http://localhost:5000/api/games/$GAME_ID/rounds/start -H "Authorization: Bearer $TOKEN_ADMIN" | jq .roundNumber
# → 2

# Iterar hasta MaxRounds=10, verificar que cada StartRound excluye PreviousQuestionIds (no repite questionId dentro del mismo Game)

# Concurrencia: dos StartRound simultáneos en IN_PROGRESS → uno 200, otro 409 Conflict (rowversion)
```

### P1 — Defensa de invariantes (config inmutable, NoActiveRound, solo finalizar desde válidos)

```bash
# UpdateGame después de IN_PROGRESS → 400 ConfigurationImmutable
curl -s -w " %{http_code}\n" -X PUT http://localhost:5000/api/games/$GAME_ID -H "Authorization: Bearer $TOKEN_ADMIN" -H "Content-Type: application/json" -d "{
  \"name\":\"Mutado\",\"categoryId\":\"$CAT_ID\",\"minRounds\":5,\"maxRounds\":10,\"initialDifficulty\":3,\"difficultyStrategy\":\"Linear\",\"timeLimitPerQuestion\":30,\"pointsPerRound\":10,\"minPlayers\":2,\"maxPlayers\":10,\"withdrawalPolicy\":\"KEEP_CURRENT_SCORE\",\"lossPolicy\":\"LOSE_CURRENT_ROUND\",\"consolationPolicy\":\"NONE\",\"scoringSystem\":\"STANDARD\",\"rewardRules\":{\"type\":\"POINTS\"}
}" | tail
# → 400, detail ConfigurationImmutable

# SubmitAnswer en ROUND_COMPLETED (sin ronda activa) → 400 NoActiveRound
curl -s -w " %{http_code}\n" -X POST http://localhost:5000/api/games/$GAME_ID/answers -H "Authorization: Bearer $TOKEN_PLAYER" -H "Content-Type: application/json" -d "{\"questionId\":\"$CAT_ID\",\"answerOptionId\":\"00000000-0000-0000-0000-000000000000\",\"idempotencyKey\":\"22222222-2222-2222-2222-222222222222\"}" | tail
# → 400

# FinishGame desde DRAFT/READY (crear game3 DRAFT y intentar finish) → 400 InvalidGameState
export GAME_DRAFT=$(curl -X POST http://localhost:5000/api/games -H "Authorization: Bearer $TOKEN_ADMIN" -H "Content-Type: application/json" -d "{\"name\":\"DraftOnly\",\"categoryId\":\"$CAT_ID\",\"minRounds\":5,\"maxRounds\":5,\"initialDifficulty\":1,\"difficultyStrategy\":\"Linear\",\"timeLimitPerQuestion\":30,\"pointsPerRound\":10,\"minPlayers\":2,\"maxPlayers\":10,\"withdrawalPolicy\":\"KEEP_CURRENT_SCORE\",\"lossPolicy\":\"LOSE_CURRENT_ROUND\",\"consolationPolicy\":\"NONE\",\"scoringSystem\":\"STANDARD\",\"rewardRules\":{\"type\":\"POINTS\"}}" | jq -r .id)
curl -s -w " %{http_code}\n" -X POST http://localhost:5000/api/games/$GAME_DRAFT/finish -H "Authorization: Bearer $TOKEN_ADMIN" | tail
# → 400

# Concurrencia: dos FinishGame simultáneos en ROUND_COMPLETED → uno 200, otro 409 + GET muestra FINISHED
```

### P2 — Finalización y cancelación controlada (FINISHED / CANCELLED / FORCED_FINISHED)

```bash
# FinishGame desde ROUND_COMPLETED con rondas completadas (haber hecho 5..10 rondas Start/Complete) → FINISHED
curl -X POST http://localhost:5000/api/games/$GAME_ID/finish -H "Authorization: Bearer $TOKEN_ADMIN" | jq .status
# → "FINISHED", GameFinished event, FinishedAt != null

# Después de FINISHED, cualquier transición → 400 InvalidGameState
curl -s -w " %{http_code}\n" -X POST http://localhost:5000/api/games/$GAME_ID/rounds/start -H "Authorization: Bearer $TOKEN_ADMIN" | tail
# → 400
curl -s -w " %{http_code}\n" -X POST http://localhost:5000/api/games/$GAME_ID/players -H "Authorization: Bearer $TOKEN_PLAYER" -H "Content-Type: application/json" -d "{}" | tail
# → 400

# CancelGame desde WAITING_FOR_PLAYERS (crear game4, ready, open-lobby, sin iniciar) → CANCELLED
export GAME_CANCEL=$(curl -X POST http://localhost:5000/api/games -H "Authorization: Bearer $TOKEN_ADMIN" -H "Content-Type: application/json" -d "{\"name\":\"CancelTest\",\"categoryId\":\"$CAT_ID\",\"minRounds\":5,\"maxRounds\":5,\"initialDifficulty\":1,\"difficultyStrategy\":\"Linear\",\"timeLimitPerQuestion\":30,\"pointsPerRound\":10,\"minPlayers\":2,\"maxPlayers\":10,\"withdrawalPolicy\":\"KEEP_CURRENT_SCORE\",\"lossPolicy\":\"LOSE_CURRENT_ROUND\",\"consolationPolicy\":\"NONE\",\"scoringSystem\":\"STANDARD\",\"rewardRules\":{\"type\":\"POINTS\"}}" | jq -r .id)
curl -X POST http://localhost:5000/api/games/$GAME_CANCEL/ready -H "Authorization: Bearer $TOKEN_ADMIN" | jq
curl -X POST http://localhost:5000/api/games/$GAME_CANCEL/open-lobby -H "Authorization: Bearer $TOKEN_ADMIN" | jq
curl -X POST http://localhost:5000/api/games/$GAME_CANCEL/cancel -H "Authorization: Bearer $TOKEN_ADMIN" -H "Content-Type: application/json" -d "{\"reason\":\"Organizador cancela por mantenimiento\"}" | jq .status
# → "CANCELLED", GameCancelled event

# Cancel desde FINISHED → 400 InvalidGameState
curl -s -w " %{http_code}\n" -X POST http://localhost:5000/api/games/$GAME_ID/cancel -H "Authorization: Bearer $TOKEN_ADMIN" -H "Content-Type: application/json" -d "{\"reason\":\"Ya terminado\"}" | tail
# → 400

# ForceFinish desde IN_PROGRESS/ROUND_IN_PROGRESS (crear game5, ready, open-lobby, join 2, start, startRound) → FORCED_FINISHED
export GAME_FORCE=$(curl -X POST http://localhost:5000/api/games -H "Authorization: Bearer $TOKEN_ADMIN" -H "Content-Type: application/json" -d "{\"name\":\"ForceTest\",\"categoryId\":\"$CAT_ID\",\"minRounds\":5,\"maxRounds\":10,\"initialDifficulty\":1,\"difficultyStrategy\":\"Linear\",\"timeLimitPerQuestion\":30,\"pointsPerRound\":10,\"minPlayers\":2,\"maxPlayers\":10,\"withdrawalPolicy\":\"KEEP_CURRENT_SCORE\",\"lossPolicy\":\"LOSE_CURRENT_ROUND\",\"consolationPolicy\":\"NONE\",\"scoringSystem\":\"STANDARD\",\"rewardRules\":{\"type\":\"POINTS\"}}" | jq -r .id)
curl -X POST http://localhost:5000/api/games/$GAME_FORCE/ready -H "Authorization: Bearer $TOKEN_ADMIN" | jq
curl -X POST http://localhost:5000/api/games/$GAME_FORCE/open-lobby -H "Authorization: Bearer $TOKEN_ADMIN" | jq
curl -X POST http://localhost:5000/api/games/$GAME_FORCE/players -H "Authorization: Bearer $TOKEN_PLAYER" -d "{}" | jq
curl -X POST http://localhost:5000/api/games/$GAME_FORCE/players -H "Authorization: Bearer $TOKEN_PLAYER2" -d "{}" | jq
curl -X POST http://localhost:5000/api/games/$GAME_FORCE/start -H "Authorization: Bearer $TOKEN_ADMIN" | jq
curl -X POST http://localhost:5000/api/games/$GAME_FORCE/rounds/start -H "Authorization: Bearer $TOKEN_ADMIN" | jq
curl -X POST http://localhost:5000/api/games/$GAME_FORCE/force-finish -H "Authorization: Bearer $TOKEN_ADMIN" -H "Content-Type: application/json" -d "{\"reason\":\"Timeout global, categoría archivada, sin preguntas disponibles\"}" | jq .status
# → "FORCED_FINISHED", GameForcedFinished event

# ForceFinish sin reason → 400 Validation (Reason 3-500)

# GET refleja terminal
curl -H "Authorization: Bearer $TOKEN_ADMIN" http://localhost:5000/api/games/$GAME_ID | jq .status
# → FINISHED

# Concurrencia rowversion: dos Cancel simultáneos → uno 200, otro 409
```

## Tests

```bash
# Unit + Architecture (sin infra, Domain First)
dotnet test tests/OroQuizClash.Domain.Tests --filter GameLifecycle
dotnet test tests/OroQuizClash.Architecture.Tests --filter Game

# Application (NSubstitute IRepository<Game>, ICategoryValidator, IQuestionCounter, IQuestionSelectionStrategy)
dotnet test tests/OroQuizClash.Application.Tests --filter GameLifecycle

# Infrastructure (EfRepository + OroQuizClashDbContext + GameTypeConfiguration + Specification + rowversion, Testcontainers)
dotnet test tests/OroQuizClash.Infrastructure.Tests --filter GameLifecycle

# Api (WebApplicationFactory + JWT mock ADMIN/PLAYER, E2E DRAFT→FINISHED, concurrencia rowversion)
dotnet test tests/OroQuizClash.Api.Tests --filter GameLifecycle

# Todos
dotnet test
```

## Expected Outcomes (Success Criteria)

- **SC-001**: Create válida → 201 DRAFT + GameCreated 100%, inválida → 400 sin persistir, <1s 95%.
- **SC-002**: MarkReady con ≥5 válidas → READY + GameReady <2s 100%; con <5 → 400 CategoryNotReady y permanece DRAFT 100%.
- **SC-003**: StartGame players≥Min → IN_PROGRESS + GameStarted 100%; <Min → 400 NotEnoughPlayers 100%; concurrent Start → 409 100% <500ms.
- **SC-004**: StartRound solo desde IN_PROGRESS/ROUND_COMPLETED → ROUND_IN_PROGRESS con PUBLISHED no usada <500ms 1k; desde ROUND_IN_PROGRESS → 400 RoundAlreadyInProgress 100%; siguiente tras ROUND_COMPLETED → ROUND_IN_PROGRESS 100%.
- **SC-005**: SubmitAnswer solo en ROUND_IN_PROGRESS 100% evaluada server-side + PointTransaction; en IN_PROGRESS sin ronda → 400 NoActiveRound 100%; duplicado idempotente no duplica puntos 100%.
- **SC-006**: UpdateGame después de IN_PROGRESS 0% muta, 100% 400 ConfigurationImmutable.
- **SC-007**: Finish solo desde válidos → FINISHED + GameFinished 100%; desde DRAFT/READY → 400; Cancel solo no-terminal, Forced solo IN_PROGRESS/ROUND_* 100%; terminal rechaza posteriores 100% + 409 concurrencia.
- **SC-008**: 90% organizadores completan Create→Ready→Wait→Join2→Start→StartRound→CompleteRound→Finish en primer intento sin soporte (quickstart usability).

## Troubleshooting

- `400 InvalidGameConfiguration` / `CategoryNotReady`: verificar CategoryId publicada (GET /api/categories/{id} validQuestionsCount≥5), MinRounds≥5, TimeLimit 5–300, Difficulty 1..5, MinPlayers≥1.
- `400 NotEnoughPlayers`: verificar players.length ≥ MinPlayers y <MaxPlayers (GET /api/games/{id} players).
- `400 RoundAlreadyInProgress`: completar ronda actual `POST /api/games/{id}/rounds/{roundId}/complete` antes de siguiente `StartRound`.
- `400 NoActiveRound`: SubmitAnswer solo en ROUND_IN_PROGRESS; verificar GET /api/games/{id} status == ROUND_IN_PROGRESS y roundId correcto.
- `400 ConfigurationImmutable`: Update solo en DRAFT/READY; después de Start → inmutable; recargar y crear nuevo Game si se requiere cambio.
- `400 InvalidGameState`: transición no permitida según matriz (ej. FINISHED→Start, DRAFT→Finish); verificar estado actual GET /api/games/{id} status.
- `409 Conflict` rowversion: recargar GET /api/games/{id} para nuevo rowVersion y reintentar; concurrencia StartGame/StartRound/Finish.
- `401/403`: verificar TOKEN con roles ADMIN/GAME_MANAGER para ciclo, PLAYER para Join/SubmitAnswer, Authority http://localhost:5080.
- `404 GameNotFound`: GameId mal formado o no existe.
- `NoAvailableQuestion` en StartRound: banco sin PUBLISHED no usada; crear más preguntas PUBLISHED en categoría (SPEC-003).
- `Reason 3-500` para Cancel/ForceFinish: verificar reason length.

