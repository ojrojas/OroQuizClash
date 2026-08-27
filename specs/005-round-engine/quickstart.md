# Quickstart: Round Engine

**Feature**: `005-round-engine` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Contracts**: [contracts/round-engine.openapi.yaml](contracts/round-engine.openapi.yaml), [contracts/round-progression.openapi.yaml](contracts/round-progression.openapi.yaml) | **Data Model**: [data-model.md](data-model.md)

Guía de validación end-to-end para los 5 campos (`RoundNumber/Difficulty/Question/TimeLimit/Status`), flujo 8 pasos (`StartRound→SelectQuestion→PresentQuestion→WaitForAnswers→EvaluateAnswers→CalculateScores→CompleteRound→IncreaseDifficulty`), selección impredecible no repetida con 5 filtros, y progresión `Linear` 1→5 configurable.

## Prerequisites

- .NET 10 SDK, Podman, `dotnet ef` si aplica migraciones.
- `OroQuizClash.slnx` compilable (`dotnet build OroQuizClash.slnx`), `OroQuizClashDbContext` con `DbSet<Game>` + `GameRounds` (`HasMany` field `_rounds`, `RowVersion`, `UNIQUE (GameId,RoundNumber)`) + `DbSet<Category>` (002) + `DbSet<Question>` (003) + `OutboxMessages` + `PointTransaction` ledger ya migrado/EnsureCreated.
- `OroIdentityServer` `oroidentityserver:latest` corriendo (`podman build -f src/IdentityServer/IdentityServer/Dockerfile -t oroidentityserver:latest .` + `podman compose up -d` o vía `aspire start`), discovery `http://localhost:5080/.well-known/openid-configuration`, `Category` publicada con `IQuestionCounter` ≥5 válidas y banco con `Question` PUBLISHED 4/1 distribuidas por `Difficulty` 1..5, `AcademicLevel`/`AgeRange` variados (para filtros), `Game` en `IN_PROGRESS` (SPEC-004) con `MinRounds=5`, `MaxRounds=5`, `InitialDifficulty=1`, `TimeLimit=30`, `DifficultyStrategy=Linear`.
- `IQuestionSelectionStrategy` `Random` (SPEC-003) + `IDifficultyProgressionStrategy` `Linear` default registrados en `Program.cs` (`AddScoped<IDifficultyProgressionStrategy, LinearDifficultyStrategy>`).

## Setup

```bash
# 1. AppHost (Aspire) — levanta sqlserver/postgres/redis/rabbitmq/identity-server/api
aspire start
aspire wait oroclash-api
# Dashboard: https://localhost:17113

# 2. Tokens
curl -X POST http://localhost:5080/connect/token \
  -d "grant_type=password&username=admin&password=Admin@123456&scope=openid profile email" \
  -u "oroclash-api:secret" > /tmp/token-admin.json
export TOKEN_ADMIN=$(jq -r .access_token /tmp/token-admin.json)
curl -X POST http://localhost:5080/connect/token \
  -d "grant_type=password&username=player1&password=Player@123456&scope=openid profile email" \
  -u "oroclash-api:secret" > /tmp/token-player.json
export TOKEN_PLAYER=$(jq -r .access_token /tmp/token-player.json)
export TOKEN_PLAYER2=$(jq -r .access_token /tmp/token-player2.json)

# 3. Category publicada con 10 PUBLISHED (Difficulty 1..5, Academic Secundaria, Age 13-17)
# Si no existe, crear cat + 10 preguntas PUBLISHED (ver 003 quickstart)
# curl POST /api/categories ... -> $CAT_ID, POST /api/questions x10 + /publish x10
# Verificar validQuestionsCount≥5
CAT_ID=$(curl -s http://localhost:5000/api/categories -H "Authorization: Bearer $TOKEN_ADMIN" | jq -r '.items[0].id')

# 4. Crear Game MinRounds=5 MaxRounds=5 InitialDifficulty=1 Linear TimeLimit=30
curl -X POST http://localhost:5000/api/games -H "Authorization: Bearer $TOKEN_ADMIN" -H "Content-Type: application/json" -d "{
  \"name\":\"Round Engine Test\",\"categoryId\":\"$CAT_ID\",\"minRounds\":5,\"maxRounds\":5,\"initialDifficulty\":1,\"difficultyStrategy\":\"Linear\",\"timeLimitPerQuestion\":30,\"pointsPerRound\":10,\"minPlayers\":2,\"maxPlayers\":10,\"withdrawalPolicy\":\"KEEP_CURRENT_SCORE\",\"lossPolicy\":\"LOSE_CURRENT_ROUND\",\"consolationPolicy\":\"NONE\",\"scoringSystem\":\"STANDARD\",\"rewardRules\":{\"type\":\"POINTS\"}
}" | tee /tmp/game.json | jq
export GAME_ID=$(jq -r .id /tmp/game.json)

# Llevar a IN_PROGRESS: ready → open-lobby → join×2 → start
curl -X POST http://localhost:5000/api/games/$GAME_ID/ready -H "Authorization: Bearer $TOKEN_ADMIN" | jq
curl -X POST http://localhost:5000/api/games/$GAME_ID/open-lobby -H "Authorization: Bearer $TOKEN_ADMIN" | jq
curl -X POST http://localhost:5000/api/games/$GAME_ID/players -H "Authorization: Bearer $TOKEN_PLAYER" -H "Content-Type: application/json" -d "{}" | jq
curl -X POST http://localhost:5000/api/games/$GAME_ID/players -H "Authorization: Bearer $TOKEN_PLAYER2" -H "Content-Type: application/json" -d "{}" | jq
curl -X POST http://localhost:5000/api/games/$GAME_ID/start -H "Authorization: Bearer $TOKEN_ADMIN" | jq .status
# → "IN_PROGRESS"

# 5. Alternativa sin AppHost:
# dotnet run --project src/OroQuizClash.Api --urls http://localhost:5000
```

## Validation Scenarios

### P1 — Iniciar ronda y selección impredecible no repetida (Category/Difficulty/Academic/Age)

```bash
# StartRound 1 → ROUND_IN_PROGRESS con 5 campos
curl -X POST http://localhost:5000/api/games/$GAME_ID/rounds/start -H "Authorization: Bearer $TOKEN_ADMIN" | tee /tmp/round1.json | jq
# Esperado: 200, {id, gameId, roundNumber:1, difficulty:1, questionId (PUBLISHED), timeLimit:30, status:"ROUND_IN_PROGRESS", startedAt}
export ROUND1_ID=$(jq -r .id /tmp/round1.json)
export Q1_ID=$(jq -r .questionId /tmp/round1.json)
# Verificar 5 campos no nulos: roundNumber 1, difficulty 1, questionId != null, timeLimit 30, status ROUND_IN_PROGRESS

# No repetida: StartRound 2 debe seleccionar Q != Q1
curl -X POST http://localhost:5000/api/games/$GAME_ID/rounds/$ROUND1_ID/complete -H "Authorization: Bearer $TOKEN_ADMIN" | jq
curl -X POST http://localhost:5000/api/games/$GAME_ID/rounds/start -H "Authorization: Bearer $TOKEN_ADMIN" | tee /tmp/round2.json | jq
export Q2_ID=$(jq -r .questionId /tmp/round2.json)
# Q2_ID != Q1_ID

# Category filter: verificar que Q1 Category == Game.CategoryId (Game.Configuration.CategoryId == $CAT_ID)
curl -H "Authorization: Bearer $TOKEN_ADMIN" http://localhost:5000/api/questions/$Q1_ID | jq .categoryId
# → $CAT_ID

# Difficulty filter: Round 1 difficulty 1 → Q1 difficulty 1 (según progresión)
curl -H "Authorization: Bearer $TOKEN_ADMIN" http://localhost:5000/api/questions/$Q1_ID | jq .difficulty
# → 1 ; Round2 → 2, etc.

# Impredecible: crear otro juego GAME2 con misma config y comparar QuestionId en Round1 — no correlacionan (no secuencial)
# (crear GAME2 y hacer start → join → start → StartRound → questionId != Q1 con alta probabilidad; repetir 10 veces y verificar distribución)

# Concurrencia: dos StartRound simultáneos en IN_PROGRESS sin ronda activa → uno 200, otro 409 RoundAlreadyInProgress (rowversion)
# (probar con parallel curl)

# Banco agotado: si solo hay 2 preguntas que cumplen filtros pero MinRounds=5, Round3 con Previous=[Q1,Q2] ya no hay → 409 NoAvailableQuestion, no crea ronda fantasma
```

### P1 — Ciclo 8 pasos: PresentQuestion (sin IsCorrect) → Wait/Evaluate → CalculateScores → CompleteRound

```bash
# PresentQuestion para PLAYER (sin IsCorrect)
curl -H "Authorization: Bearer $TOKEN_PLAYER" http://localhost:5000/api/games/$GAME_ID/rounds/$ROUND1_ID/question | jq
# → {questionId, text, difficulty, timeLimit, roundNumber:1, status:"ROUND_IN_PROGRESS", answerOptions:[{id,text,displayOrder}] } sin isCorrect

# PresentQuestion para ADMIN sí expone isCorrect (vía GET /api/questions/$Q1_ID)
curl -H "Authorization: Bearer $TOKEN_ADMIN" http://localhost:5000/api/questions/$Q1_ID | jq '.answerOptions[] | {text, isCorrect}'
# → una con isCorrect true

# WaitForAnswers + Evaluate (dentro TimeLimit 30s) → SubmitAnswer correcta
CORRECT_ID=$(curl -s -H "Authorization: Bearer $TOKEN_ADMIN" http://localhost:5000/api/questions/$Q1_ID | jq -r '.answerOptions[] | select(.isCorrect==true) | .id')
curl -X POST http://localhost:5000/api/games/$GAME_ID/answers -H "Authorization: Bearer $TOKEN_PLAYER" -H "Content-Type: application/json" -d "{
  \"questionId\":\"$Q1_ID\",\"answerOptionId\":\"$CORRECT_ID\",\"roundId\":\"$ROUND1_ID\",\"idempotencyKey\":\"11111111-1111-1111-1111-111111111111\"
}" | jq .correct
# → true, points 10, roundStatus ROUND_IN_PROGRESS (hasta CompleteRound)

# Incorrecta
INCORRECT_ID=$(curl -s -H "Authorization: Bearer $TOKEN_ADMIN" http://localhost:5000/api/questions/$Q1_ID | jq -r '.answerOptions[] | select(.isCorrect==false) | .id' | head -n1)
curl -X POST http://localhost:5000/api/games/$GAME_ID/answers -H "Authorization: Bearer $TOKEN_PLAYER2" -H "Content-Type: application/json" -d "{
  \"questionId\":\"$Q1_ID\",\"answerOptionId\":\"$INCORRECT_ID\",\"roundId\":\"$ROUND1_ID\",\"idempotencyKey\":\"22222222-2222-2222-2222-222222222222\"
}" | jq .correct
# → false

# Fuera de TimeLimit (31s después de StartedAt) → 400 AnswerTimeout
# (esperar 31s o mock StartedAt, probar)
# Después de CompleteRound → SubmitAnswer bloqueado 400 NoActiveRound
curl -X POST http://localhost:5000/api/games/$GAME_ID/rounds/$ROUND1_ID/complete -H "Authorization: Bearer $TOKEN_ADMIN" | jq .status
# → ROUND_COMPLETED
curl -s -w " %{http_code}\n" -X POST http://localhost:5000/api/games/$GAME_ID/answers -H "Authorization: Bearer $TOKEN_PLAYER" -H "Content-Type: application/json" -d "{\"questionId\":\"$Q1_ID\",\"answerOptionId\":\"$CORRECT_ID\",\"roundId\":\"$ROUND1_ID\",\"idempotencyKey\":\"33333333-3333-3333-3333-333333333333\"}" | tail
# → 400 NoActiveRound

# Idempotencia: reenviar mismo idempotencyKey → mismo resultado sin duplicar PointTransaction
curl -X POST http://localhost:5000/api/games/$GAME_ID/answers -H "Authorization: Bearer $TOKEN_PLAYER" -d "{\"questionId\":\"$Q1_ID\",\"answerOptionId\":\"$CORRECT_ID\",\"roundId\":\"$ROUND1_ID\",\"idempotencyKey\":\"11111111-1111-1111-1111-111111111111\"}" | jq

# CalculateScores: verificar PointTransaction ledger (GET /api/games/$GAME_ID/leaderboard o /scores) suma points
```

### P1 — Progresión Linear 1→2→3→4→5 y configurable

```bash
# Con InitialDifficulty=1 Linear, 5 rondas: verificar dificultades 1,2,3,4,5
for i in 1 2 3 4 5; do
  curl -X POST http://localhost:5000/api/games/$GAME_ID/rounds/start -H "Authorization: Bearer $TOKEN_ADMIN" | tee /tmp/r.json | jq -e ".difficulty == $i" || echo "FAIL i=$i"
  RID=$(jq -r .id /tmp/r.json)
  curl -X POST http://localhost:5000/api/games/$GAME_ID/rounds/$RID/complete -H "Authorization: Bearer $TOKEN_ADMIN" | jq
done
# Cada round dificultad == i

# Cambiar estrategia a Progressive (si se expone via appsettings o override en StartRound request body difficultyStrategy)
curl -X POST http://localhost:5000/api/games/$GAME_ID/rounds/start -H "Authorization: Bearer $TOKEN_ADMIN" -H "Content-Type: application/json" -d "{\"difficultyStrategy\":\"Progressive\"}" | jq .difficulty
# → sigue curva Progressive (ej. 1,1,2,3,5) sin cambiar contrato

# Clamp: InitialDifficulty=5 Linear → Round1 5, Round2 5 (no 6)
# (crear GAME_HIGH con InitialDifficulty=5 y verificar)

# MinRounds guard: FinishGame con solo 3 rondas completadas → 400 InvalidGameState / NotEnoughRounds
curl -s -w " %{http_code}\n" -X POST http://localhost:5000/api/games/$GAME_ID/finish -H "Authorization: Bearer $TOKEN_ADMIN" | tail
# → 400 si <5

# Tras 5 rondas completadas, Finish → FINISHED
curl -X POST http://localhost:5000/api/games/$GAME_ID/finish -H "Authorization: Bearer $TOKEN_ADMIN" | jq .status
# → FINISHED

# Ver progresión via GET /api/games/{id}/rounds/progression (si existe)
curl -H "Authorization: Bearer $TOKEN_ADMIN" http://localhost:5000/api/games/$GAME_ID/rounds/progression | jq
```

### P2 — Invariantes 5 campos y flujo completo 8 pasos auditable

```bash
# Verificar cada GameRound tiene 5 campos no nulos y RoundNumber sin huecos
curl -H "Authorization: Bearer $TOKEN_ADMIN" http://localhost:5000/api/games/$GAME_ID/rounds -H "Authorization: Bearer $TOKEN_ADMIN" | jq '.items[] | {roundNumber, difficulty, questionId, timeLimit, status}'

# Flujo 8 pasos auditable: StartRound → SelectQuestion (impredecible) → PresentQuestion (sin IsCorrect) → WaitForAnswers (TimeLimit) → EvaluateAnswers (IsCorrect server-side) → CalculateScores (PointTransaction) → CompleteRound → IncreaseDifficulty (next)
# Ver logs OTel: RoundStarted/RoundCompleted con GameId/RoundId/RoundNumber/QuestionId/Difficulty/TimeLimit

# Concurrencia StartRound doble en ROUND_COMPLETED → uno 200, otro 409 + UNIQUE (GameId,RoundNumber) protege
# (parallel curl)

# Category inmutable: intentar cambiar GameConfiguration.CategoryId entre rondas (PUT /api/games/$GAME_ID) → 400 ConfigurationImmutable (SPEC-004)

# MinRounds=4 rechazo en CreateGame
curl -s -w " %{http_code}\n" -X POST http://localhost:5000/api/games -H "Authorization: Bearer $TOKEN_ADMIN" -H "Content-Type: application/json" -d "{\"name\":\"Bad Min\",\"categoryId\":\"$CAT_ID\",\"minRounds\":4,\"maxRounds\":10,\"initialDifficulty\":1,\"difficultyStrategy\":\"Linear\",\"timeLimitPerQuestion\":30,\"pointsPerRound\":10,\"minPlayers\":2,\"maxPlayers\":10,\"withdrawalPolicy\":\"KEEP_CURRENT_SCORE\",\"lossPolicy\":\"LOSE_CURRENT_ROUND\",\"consolationPolicy\":\"NONE\",\"scoringSystem\":\"STANDARD\",\"rewardRules\":{\"type\":\"POINTS\"}}" | tail
# → 400 MinRoundsTooLow
```

## Tests

```bash
# Unit + Architecture (sin infra, Domain First) — 5 campos, progresión Linear 1→5, filtros, no repetida, rowversion
dotnet test tests/OroQuizClash.Domain.Tests --filter RoundEngine
dotnet test tests/OroQuizClash.Architecture.Tests --filter Round

# Application (NSubstitute IRepository<Game>, IQuestionSelectionStrategy stub, IDifficultyProgressionStrategy)
dotnet test tests/OroQuizClash.Application.Tests --filter RoundEngine

# Infrastructure (EfRepository + OroQuizClashDbContext + GameTypeConfiguration + UNIQUE (GameId,RoundNumber) + selección impredecible, Testcontainers)
dotnet test tests/OroQuizClash.Infrastructure.Tests --filter RoundEngine

# Api (WebApplicationFactory + JWT mock ADMIN/PLAYER, PresentQuestion filtrado IsCorrect, E2E 5 rounds Linear 1→5, concurrencia 409)
dotnet test tests/OroQuizClash.Api.Tests --filter RoundEngine

# Todos
dotnet test
```

## Expected Outcomes (Success Criteria)

- **SC-001**: MinRounds<5 rechazo 100% con `InvalidGameConfiguration.MinRoundsTooLow` <1s 95%.
- **SC-002**: Cada ronda 5 campos no nulos y únicos (RoundNumber sin huecos, Difficulty 1..5, QuestionId PUBLISHED, TimeLimit 5–300, Status) 100%, verificado por GET y `UNIQUE (GameId,RoundNumber)`.
- **SC-003**: Flujo 8 pasos sin omitir 100% (audit RoundStarted/Completed + PointTransaction).
- **SC-004**: Selección impredecible server-side aleatoria <500ms p95 con 1k, distribución no correlacionada por RoundNumber/QuestionId.
- **SC-005**: 0% repetida intra-juego (PreviousQuestionIds exclusión) 100%, NoAvailableQuestion si agotado 100% sin ronda fantasma.
- **SC-006**: 100% Category == Game.CategoryId, 0% fuera 100% (2×100).
- **SC-007**: 100% Difficulty == Round.Difficulty 1..5, 0% distinta (1..5 distribuidas).
- **SC-008**: 100% Academic/Age compatibles, 0% desalineadas.
- **SC-009**: Linear 1→2→3→4→5 con Initial=1 Min=5 100% por juego; cambiar estrategia cambia secuencia sin romper invariantes 100%.

## Troubleshooting

- `400 InvalidGameConfiguration.MinRoundsTooLow`: MinRounds≥5 required (FR-001).
- `400 InvalidRound.InvalidFields`: RoundNumber/Difficulty/QuestionId/TimeLimit/Status nulo o duplicado (UNIQUE).
- `409 RoundAlreadyInProgress`: completar ronda actual `POST /rounds/{roundId}/complete` antes de siguiente `StartRound`.
- `409 NoAvailableQuestion`: banco agotado o filtros muy restrictivos; crear más PUBLISHED con Category/Difficulty/Academic/Age alineadas (SPEC-003).
- `400 CategoryMismatch/DifficultyMismatch/AcademicLevelMismatch`: pregunta no alineada a Round.Difficulty/TimeLimit; verificar banco.
- `409 DuplicateRoundNumber / ConcurrencyConflict`: recargar GET /api/games/{id} para rowVersion y reintentar (rowversion + UNIQUE).
- `403 PresentQuestion IsCorrect`: PLAYER no ve IsCorrect (filtrado); ADMIN vía GET /api/questions/{id} sí.
- `400 AnswerTimeout`: ServerTimestamp - StartedAt > TimeLimit; verificar TimeLimit y reloj servidor.
- `400 NoActiveRound`: SubmitAnswer solo en ROUND_IN_PROGRESS; verificar status.
- `401/403`: TOKEN con roles ADMIN/GAME_MANAGER para ciclo, PLAYER para SubmitAnswer, Authority http://localhost:5080.
- `404 GameNotFound/RoundNotFound/QuestionNotFound`: Id mal formado o no existe.
- `Difficulty clamp`: Linear con Initial=5 → Round2 5 (no 6), Progressive/Adaptive mapean dentro 1..5.
