# Quickstart: Question Bank

**Feature**: `003-question-bank` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Contracts**: [contracts/questions.openapi.yaml](contracts/questions.openapi.yaml), [contracts/question-selection.openapi.yaml](contracts/question-selection.openapi.yaml) | **Data Model**: [data-model.md](data-model.md)

Guía de validación end-to-end para el ciclo de vida de `Question` (QST-001..006), transiciones `Activate/Publish/Deactivate/Archive`, y selección con 7 parámetros (`Category, Difficulty, AcademicLevel, AgeRange, PreviousQuestions, Game, Round`).

## Prerequisites

- .NET 10 SDK, Podman, `dotnet ef` si aplica migraciones.
- `OroQuizClash.slnx` compilable (`dotnet build OroQuizClash.slnx`), `OroQuizClashDbContext` con `DbSet<Question>` + `DbSet<AnswerOption>` + `DbSet<Category>` (002) + `DbSet<Game>` (001) ya migrado/EnsureCreated.
- `OroIdentityServer` `oroidentityserver:latest` corriendo (`podman build -f src/IdentityServer/IdentityServer/Dockerfile -t oroidentityserver:latest .` + `podman compose up -d` o vía `aspire start`), discovery `http://localhost:5080/.well-known/openid-configuration`, `Category` ya existe (`POST /api/categories` DRAFT→PUBLISHED con 5 preguntas mínimo provisto por este feature).
- `IQuestionCounter` real `EfQuestionCounter` (reemplaza stub de 002) y `IQuestionSelectionStrategy` `RandomQuestionSelectionStrategy` (default) registrados en DI.

## Setup

```bash
# 1. AppHost (Aspire) — levanta sqlserver/postgres/redis/rabbitmq/identity-server/api
aspire start
aspire wait oroclash-api
# Dashboard: https://localhost:17113

# 2. Token ADMIN (o GAME_MANAGER) de OroIdentityServer
curl -X POST http://localhost:5080/connect/token \
  -d "grant_type=password&username=admin&password=Admin@123456&scope=openid profile email" \
  -u "oroclash-api:secret" > /tmp/token.json
export TOKEN=$(jq -r .access_token /tmp/token.json)

# 3. Crear categoría de prueba (SPEC-002) si no existe
curl -X POST http://localhost:5000/api/categories -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{
  "name":"Historia Universal","description":"Desde prehistoria","knowledgeArea":"Humanidades","academicLevel":"Secundaria","ageMin":13,"ageMax":17,"difficultyLevel":3,"tags":["historia"]
}' | tee /tmp/cat.json | jq
export CAT_ID=$(jq -r .id /tmp/cat.json)

# 4. Alternativa sin AppHost: dotnet run Api directo
# dotnet run --project src/OroQuizClash.Api --urls http://localhost:5000
# export TOKEN=...
```

## Validation Scenarios

### P1 — Crear pregunta con 4 alternativas y validación QST-001..004

```bash
# Crear válida → 201 DRAFT
curl -X POST http://localhost:5000/api/questions -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{
  \"text\":\"¿Capital de Francia?\",
  \"categoryId\":\"$CAT_ID\",
  \"difficulty\":2,
  \"academicLevel\":\"Secundaria\",
  \"ageMin\":13,
  \"ageMax\":17,
  \"answerOptions\":[
    {\"text\":\"Londres\",\"isCorrect\":false,\"displayOrder\":0},
    {\"text\":\"París\",\"isCorrect\":true,\"displayOrder\":1},
    {\"text\":\"Berlín\",\"isCorrect\":false,\"displayOrder\":2},
    {\"text\":\"Madrid\",\"isCorrect\":false,\"displayOrder\":3}
  ]
}" | tee /tmp/q1.json | jq
# Esperado: 201 Created, Location: /api/questions/{id}, body {id, status:"DRAFT", answerOptions:4, correct:1}
export Q1_ID=$(jq -r .id /tmp/q1.json)

# GET idéntico
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/questions/$Q1_ID | jq '.text, .status, (.answerOptions|length)'
# → "¿Capital de Francia?" "DRAFT" 4

# Rechazo QST-001: 3 opciones → 400 QuestionMustHaveFourOptions
curl -s -w " %{http_code}\n" -X POST http://localhost:5000/api/questions -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{
  \"text\":\"Bad\",\"categoryId\":\"$CAT_ID\",\"difficulty\":2,\"academicLevel\":\"Secundaria\",\"ageMin\":13,\"ageMax\":17,
  \"answerOptions\":[{\"text\":\"A\",\"isCorrect\":true},{\"text\":\"B\",\"isCorrect\":false},{\"text\":\"C\",\"isCorrect\":false}]
}" | tail
# → 400, code QuestionMustHaveFourOptions

# Rechazo QST-002: 0 correctas → 400 QuestionMustHaveOneCorrectAnswer
curl -s -w " %{http_code}\n" -X POST http://localhost:5000/api/questions -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{
  \"text\":\"Bad2\",\"categoryId\":\"$CAT_ID\",\"difficulty\":2,\"academicLevel\":\"Secundaria\",\"ageMin\":13,\"ageMax\":17,
  \"answerOptions\":[{\"text\":\"A\",\"isCorrect\":false},{\"text\":\"B\",\"isCorrect\":false},{\"text\":\"C\",\"isCorrect\":false},{\"text\":\"D\",\"isCorrect\":false}]
}" | tail
# → 400

# Rechazo QST-002: 2 correctas → 400
# (similar con dos isCorrect:true → 400 ExactlyOneCorrect)

# Rechazo QST-003: sin categoryId → 400 QuestionMustBelongToCategory
# Rechazo QST-004: sin difficulty → 400 QuestionMustHaveDifficulty
# Rechazo AgeRange incoherente ageMin>ageMax → 400 InvalidAgeRange

# Crear 4 preguntas más válidas para gate Category ≥5 (SPEC-002)
for i in 2 3 4 5; do
  curl -X POST http://localhost:5000/api/questions -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{
    \"text\":\"Pregunta $i texto de ejemplo para categoría\",
    \"categoryId\":\"$CAT_ID\",\"difficulty\":3,\"academicLevel\":\"Secundaria\",\"ageMin\":13,\"ageMax\":17,
    \"answerOptions\":[{\"text\":\"A$i\",\"isCorrect\":true,\"displayOrder\":0},{\"text\":\"B$i\",\"isCorrect\":false,\"displayOrder\":1},{\"text\":\"C$i\",\"isCorrect\":false,\"displayOrder\":2},{\"text\":\"D$i\",\"isCorrect\":false,\"displayOrder\":3}]
  }" | jq .id
done
```

### P1 — Ciclo de vida y publicación validada (QST-005, QST-006)

```bash
# Publish Q1 válida DRAFT → PUBLISHED 200 (ahora seleccionable)
curl -X POST http://localhost:5000/api/questions/$Q1_ID/publish -H "Authorization: Bearer $TOKEN" | jq .status
# → "PUBLISHED", publishedAt != null, QuestionPublishedDomainEvent en logs OTel

# Crear pregunta inválida (3 opciones) y try Publish → 400 QuestionNotPublishable (permanece DRAFT)
# (crear bad id: $BAD_ID)
curl -s -w " %{http_code}\n" -X POST http://localhost:5000/api/questions/$BAD_ID/publish -H "Authorization: Bearer $TOKEN" | tail
# → 400, status DRAFT no cambia

# QST-005: Published no puede quedar sin correcta
# Intentar Update Q1 Published dejando 0 correctas → 400 PublishedQuestionMustHaveCorrectAnswer
curl -s -w " %{http_code}\n" -X PUT http://localhost:5000/api/questions/$Q1_ID -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{
  \"text\":\"¿Capital de Francia?\",\"categoryId\":\"$CAT_ID\",\"difficulty\":2,\"academicLevel\":\"Secundaria\",\"ageMin\":13,\"ageMax\":17,
  \"answerOptions\":[{\"text\":\"Londres\",\"isCorrect\":false},{\"text\":\"París\",\"isCorrect\":false},{\"text\":\"Berlín\",\"isCorrect\":false},{\"text\":\"Madrid\",\"isCorrect\":false}]
}" | tail
# → 400

# Deactivate PUBLISHED → INACTIVE (deja de ser seleccionable y de contar para gate)
curl -X POST http://localhost:5000/api/questions/$Q1_ID/deactivate -H "Authorization: Bearer $TOKEN" | jq .status
# → "INACTIVE"

# Select debe excluirla ahora (ver siguiente sección)
# Activate INACTIVE → ACTIVE (no seleccionable aún)
curl -X POST http://localhost:5000/api/questions/$Q1_ID/activate -H "Authorization: Bearer $TOKEN" | jq .status
# → "ACTIVE"
# Publish nuevamente INACTIVE/ACTIVE → PUBLISHED
curl -X POST http://localhost:5000/api/questions/$Q1_ID/publish -H "Authorization: Bearer $TOKEN" | jq .status
# → "PUBLISHED"

# Archive PUBLISHED → ARCHIVED terminal
export QARCH_ID=$Q1_ID # o crear nueva para archivar
curl -X POST http://localhost:5000/api/questions/$QARCH_ID/archive -H "Authorization: Bearer $TOKEN" | jq .status
# → "ARCHIVED"
# ARCHIVED → Publish/Update rechazado 400 InvalidQuestionState
curl -s -w " %{http_code}\n" -X POST http://localhost:5000/api/questions/$QARCH_ID/publish -H "Authorization: Bearer $TOKEN" | tail
# → 400

# Concurrencia: dos Publish simultáneos en DRAFT con 4/1 → uno 200, otro 409 Conflict (rowversion)
# (probar con parallel curl o test QuestionConcurrencyTests patrón)
```

### P2 — Actualizar pregunta en estados permitidos

```bash
# Crear QUPD en DRAFT
curl -X POST http://localhost:5000/api/questions -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{
  \"text\":\"Texto original\",\"categoryId\":\"$CAT_ID\",\"difficulty\":2,\"academicLevel\":\"Secundaria\",\"ageMin\":13,\"ageMax\":17,
  \"answerOptions\":[{\"text\":\"A\",\"isCorrect\":true},{\"text\":\"B\",\"isCorrect\":false},{\"text\":\"C\",\"isCorrect\":false},{\"text\":\"D\",\"isCorrect\":false}]
}" | tee /tmp/qupd.json | jq
export QUPD_ID=$(jq -r .id /tmp/qupd.json)

# Update DRAFT válido → 200
curl -X PUT http://localhost:5000/api/questions/$QUPD_ID -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{
  \"text\":\"Texto actualizado\",\"categoryId\":\"$CAT_ID\",\"difficulty\":3,\"academicLevel\":\"Secundaria\",\"ageMin\":13,\"ageMax\":17,
  \"answerOptions\":[{\"text\":\"A2\",\"isCorrect\":false},{\"text\":\"B2\",\"isCorrect\":true},{\"text\":\"C2\",\"isCorrect\":false},{\"text\":\"D2\",\"isCorrect\":false}]
}" | jq .text
# → "Texto actualizado"

# Update con 3 opciones → 400 QST-001
# ARCHIVED → Update rechazado 400 InvalidQuestionState
```

### P2 — Selección de preguntas para Game/Round (7 parámetros)

```bash
# Precondición: tener 10 preguntas PUBLISHED en CAT_ID, difficulty 2..3, Academic Secundaria, Age 13-17
# Crear Game para contexto (SPEC-001) — o usar GameId ficticio para test de selección pura
export GAME_ID=$(curl -X POST http://localhost:5000/api/games -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{
  \"name\":\"Partida test\",\"categoryId\":\"$CAT_ID\",\"minRounds\":5,\"maxRounds\":10,\"initialDifficulty\":2,\"difficultyStrategy\":\"Linear\",\"timeLimitPerQuestion\":30,\"pointsPerRound\":10,\"withdrawalPolicy\":\"KEEP_CURRENT_SCORE\",\"lossPolicy\":\"LOSE_CURRENT_ROUND\",\"consolationPolicy\":\"NONE\",\"minPlayers\":2,\"maxPlayers\":10
}" | jq -r .id)

# Select 1 pregunta por Category+Difficulty excluyendo PreviousQuestions
curl -X POST http://localhost:5000/api/questions/select -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{
  \"categoryId\":\"$CAT_ID\",\"difficulty\":3,\"academicLevel\":\"Secundaria\",\"ageMin\":13,\"ageMax\":17,
  \"previousQuestionIds\":[\"$Q1_ID\"],\"gameId\":\"$GAME_ID\",\"roundNumber\":3,\"take\":1
}" | jq '.items | length, .items[0].categoryId'
# → 1, categoryId==CAT_ID, no contiene Q1_ID, difficulty==3, status==PUBLISHED, <500ms

# Segunda ronda: previous incluye las ya usadas → no repite
export QSEL1=$(curl -X POST http://localhost:5000/api/questions/select -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{
  \"categoryId\":\"$CAT_ID\",\"previousQuestionIds\":[\"$Q1_ID\"],\"gameId\":\"$GAME_ID\",\"roundNumber\":4,\"take\":1
}" | jq -r .items[0].id)
curl -X POST http://localhost:5000/api/questions/select -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{
  \"categoryId\":\"$CAT_ID\",\"previousQuestionIds\":[\"$Q1_ID\",\"$QSEL1\"],\"gameId\":\"$GAME_ID\",\"roundNumber\":5,\"take\":1
}" | jq -r .items[0].id
# → diferente a Q1 y QSEL1

# Filtro AcademicLevel+AgeRange
curl -X POST http://localhost:5000/api/questions/select -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{
  \"categoryId\":\"$CAT_ID\",\"academicLevel\":\"Secundaria\",\"ageMin\":13,\"ageMax\":17,\"previousQuestionIds\":[],\"gameId\":\"$GAME_ID\",\"take\":1
}" | jq

# Sin resultados → 404 NoAvailableQuestion (no fallback desalineada)
curl -s -w " %{http_code}\n" -X POST http://localhost:5000/api/questions/select -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d "{
  \"categoryId\":\"00000000-0000-0000-0000-000000000000\",\"previousQuestionIds\":[],\"gameId\":\"$GAME_ID\",\"take\":1
}" | tail
# → 404

# Alternativo vía Game/Round endpoint (si se expone)
curl -H "Authorization: Bearer $TOKEN" "http://localhost:5000/api/games/$GAME_ID/rounds/1/question?categoryId=$CAT_ID&difficulty=2" | jq

# Verificar que INACTIVE/ARCHIVED/DRAFT nunca aparecen en select (crear INV_ID en INACTIVE y verificar que select no lo retorna)
```

### Validación Category gate ≥5 (integración SPEC-002)

```bash
# Con 5 preguntas PUBLISHED en CAT_ID, PublishCategory debe pasar
curl -X POST http://localhost:5000/api/categories/$CAT_ID/publish -H "Authorization: Bearer $TOKEN" | jq .status
# → "ACTIVE" (o 200)
# Si se Deactivate una de las 5 válidas, Category sigue ACTIVE pero siguiente PublishCategory de nueva categoría con 4 debe fallar
```

## Tests

```bash
# Unit + Architecture (sin infra)
dotnet test tests/OroQuizClash.Domain.Tests --filter Question
dotnet test tests/OroQuizClash.Architecture.Tests

# Application (NSubstitute IRepository<Question>, ICategoryExistenceChecker, IQuestionSelectionStrategy stub)
dotnet test tests/OroQuizClash.Application.Tests --filter Question

# Infrastructure (EfRepository + OroQuizClashDbContext + Specification + rowversion + CHECK + QuestionCounter + Selection, Testcontainers MsSql)
dotnet test tests/OroQuizClash.Infrastructure.Tests --filter Question

# Api (WebApplicationFactory + JWT mock ADMIN, E2E lifecycle + selection)
dotnet test tests/OroQuizClash.Api.Tests --filter Question

# Todos
dotnet test
```

## Expected Outcomes (Success Criteria)

- **SC-001**: Create válida 4/1 + Category+Difficulty → 201 DRAFT <1s 95%, GET idéntico 100%.
- **SC-002**: ≠4 o 0/2 correctas → 400 `QuestionMustHaveFourOptions`/`ExactlyOneCorrect` 100%, 0% persiste.
- **SC-003**: Sin Category/Difficulty → 400 `MustBelongToCategory`/`MustHaveDifficulty` 100%.
- **SC-004**: Publish inválida → 400 `QuestionNotPublishable`, DRAFT no cambia 100%; Publish válida → 200 PUBLISHED + `QuestionPublishedDomainEvent` <2s 100%.
- **SC-005**: Mutar PUBLISHED sin correcta → 400 `PublishedQuestionMustHaveCorrectAnswer` 100%; Deactivate/Archive deja de contar/seleccionar 100%.
- **SC-006**: Select con 7 params (Category, Difficulty, AcademicLevel, AgeRange, Previous, Game, Round) retorna solo PUBLISHED alineadas no en Previous con 100% precisión sobre 1k, <500ms 95%, paginada.
- **SC-007**: Sin match → 404 `NoAvailableQuestion` 100% sin fallback desalineada.
- **SC-008**: Publish/Update concurrente → `409 Conflict` rowversion 100%.
- **SC-009**: Flujo crear 5 válidas → Category Publish gate pasa sin soporte 90% usability.

## Troubleshooting

- `400 QuestionMustHaveFourOptions`: verificar `answerOptions.length==4`.
- `400 QuestionMustHaveOneCorrectAnswer`: exactamente 1 `isCorrect:true` (contar, no 0 ni 2).
- `400 QuestionMustBelongToCategory` / `404 CategoryNotFound`: verificar `categoryId` existe y no está `ARCHIVED`.
- `400 QuestionMustHaveDifficulty`: `difficulty` 1..5 requerido.
- `400 QuestionNotPublishable`: 4/1 + Category+Difficulty+Academic/Age válidos; revisar `AgeRange` solapamiento y `AcademicLevel` compatible.
- `400 PublishedQuestionMustHaveCorrectAnswer`: no dejar PUBLISHED con 0/>1 correctas en `Update`.
- `400 InvalidQuestionState`: transición no permitida (ej. ARCHIVED→Publish, DRAFT→Deactivate); revisar máquina estados en `data-model.md`.
- `404 NoAvailableQuestion`: no hay PUBLISHED que cumpla filtros+Previous exclusión; verificar que preguntas estén `PUBLISHED` (no solo `ACTIVE`/`DRAFT`).
- `409 Conflict`: `rowversion` stale — recargar `GET` y reintentar.
- `401/403`: verificar `TOKEN` con `roles` ADMIN/GAME_MANAGER y `Authority http://localhost:5080`.

