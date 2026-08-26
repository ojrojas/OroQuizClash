# Quickstart: Game Configuration

**Feature**: `001-game-configuration` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Contracts**: [contracts/create-game.openapi.yaml](contracts/create-game.openapi.yaml)

Guía de validación end-to-end para `CreateGame` con configuración inmutable (CFG-001..007).

## Prerequisites

- .NET 10 SDK, Podman (o Docker), `dotnet ef` si aplica migraciones.
- Repositorios: `src/BuildingBlocks/*` compilables (`dotnet build OroQuizClash.slnx`).
- OroIdentityServer image `oroidentityserver:latest` disponible localmente:
  ```bash
  podman build -f src/IdentityServer/IdentityServer/Dockerfile -t oroidentityserver:latest .
  # o usar imagen pre-built en el equipo
  ```
- PostgreSQL `identitydb` + OroIdentityServer en Podman:
  ```bash
  podman compose -f draft/oroidentityserver-specification.md#docker-compose --build up -d
  # o: podman run --rm -p 5080:5080 -e ConnectionStrings__identitydb="Host=localhost;Port=5432;Database=identitydb;Username=postgres;Password=postgres" -e SymmetricSecurityKey="$(openssl rand -base64 32)" -e SEED_ADMIN_USERNAME="admin" -e SEED_ADMIN_PASSWORD="Admin@123456" oroidentityserver:latest
  # verificar: curl http://localhost:5080/.well-known/openid-configuration | jq .jwks_uri
  ```
- Crear proyectos OroQuizClash si aún no existen (scaffold plan Structure):
  ```bash
  dotnet new classlib -n OroQuizClash.Domain -o src/OroQuizClash.Domain
  dotnet new classlib -n OroQuizClash.Application -o src/OroQuizClash.Application
  dotnet new classlib -n OroQuizClash.Infrastructure -o src/OroQuizClash.Infrastructure
  dotnet new web -n OroQuizClash.Api -o src/OroQuizClash.Api
  dotnet sln OroQuizClash.slnx add src/OroQuizClash.Domain/OroQuizClash.Domain.csproj src/OroQuizClash.Application/OroQuizClash.Application.csproj src/OroQuizClash.Infrastructure/OroQuizClash.Infrastructure.csproj src/OroQuizClash.Api/OroQuizClash.Api.csproj
  dotnet add src/OroQuizClash.Domain reference src/BuildingBlocks/BuildingBlocks.Kernel.Domain/BuildingBlocks.Kernel.Domain.csproj
  dotnet add src/OroQuizClash.Application reference src/OroQuizClash.Domain/OroQuizClash.Domain.csproj src/BuildingBlocks/BuildingBlocks.CQRS/BuildingBlocks.CQRS.csproj src/BuildingBlocks/BuildingBlocks.Kernel.Domain/BuildingBlocks.Kernel.Domain.csproj
  dotnet add src/OroQuizClash.Infrastructure reference src/OroQuizClash.Domain/OroQuizClash.Domain.csproj src/BuildingBlocks/BuildingBlocks.Kernel.Infrastructure/BuildingBlocks.Kernel.Infrastructure.csproj
  dotnet add src/OroQuizClash.Api reference src/OroQuizClash.Application/OroQuizClash.Application.csproj src/OroQuizClash.Infrastructure/OroQuizClash.Infrastructure.csproj src/BuildingBlocks/BuildingBlocks.ServiceDefaults/BuildingBlocks.ServiceDefaults.csproj
  ```

## Setup

```bash
# 1. Restaurar y compilar
dotnet restore
dotnet build OroQuizClash.slnx

# 2. AppHost (Aspire) — levanta SQL Server (OroQuizClash), PostgreSQL (identitydb), RabbitMQ opcional, y Api
dotnet run --project OroQuizClash.AppHost/OroQuizClash.AppHost.csproj
# Dashboard Aspire: https://localhost:17113 (puerto impreso en consola)
# Alternativa sin Aspire: podman compose + dotnet run --project src/OroQuizClash.Api

# 3. Obtener token OIDC de OroIdentityServer (admin seed)
curl -X POST http://localhost:5080/connect/token \
  -d "grant_type=password&username=admin&password=Admin@123456&scope=openid profile email offline_access" \
  -u "my-app:my-secret" # o client configurado via /api/applications
# export TOKEN=$(... | jq -r .access_token)

# 4. Crear categoría válida (stub SPEC-002/003) — mínimo para CFG-004
# Si SPEC-002/003 aún no existe, usar stub/seeder que inserte Category Published con ≥5 preguntas válidas:
# INSERT INTO Categories (...) + Questions (...) o via endpoint temporal
# Obtener CATEGORY_ID para usar en CreateGame
```

## Validation Scenarios

### P1 — Crear juego válido (debe retornar 201)

```bash
curl -X POST http://localhost:5000/api/games \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{
    "name":"Quiz Clash Masters","categoryId":"'"$CATEGORY_ID"'","minRounds":5,"maxRounds":10,
    "initialDifficulty":1,"difficultyStrategy":"Linear","timeLimitPerQuestionSeconds":30,
    "scoringSystem":"Standard","lossPolicy":"LOSE_UNSECURED_POINTS","withdrawalPolicy":"KEEP_SECURED_SCORE",
    "consolationPolicy":"FixedPoints","rewardRules":{"type":"Points","threshold":1000},
    "minPlayers":2,"maxPlayers":10
  }' | jq
# Esperado: 201 Created, Location: /api/games/{gameId}, body { gameId, status:"DRAFT", configuration:{...} }
# Verificar persistencia: GET /api/games/{gameId} retorna misma configuración (SC-006)
```

### P1 — Rechazos por CFG-001..007 (cada uno debe retornar 400 ProblemDetails)

```bash
# Sin nombre
curl -X POST http://localhost:5000/api/games -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"name":"","categoryId":"'"$CATEGORY_ID"'","minRounds":5,"maxRounds":10,"initialDifficulty":1,"difficultyStrategy":"Linear","timeLimitPerQuestionSeconds":30,"scoringSystem":"Standard","lossPolicy":"LOSE_ALL","withdrawalPolicy":"KEEP_CURRENT_SCORE","consolationPolicy":"None","rewardRules":{"type":"Points","threshold":500},"minPlayers":2,"maxPlayers":10}' | jq .code
# => InvalidGameConfiguration.InvalidName

# minRounds=3 (CFG-002)
curl -X POST http://localhost:5000/api/games -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"name":"Bad","categoryId":"'"$CATEGORY_ID"'","minRounds":3,"maxRounds":10,"initialDifficulty":1,"difficultyStrategy":"Linear","timeLimitPerQuestionSeconds":30,"scoringSystem":"Standard","lossPolicy":"LOSE_ALL","withdrawalPolicy":"KEEP_CURRENT_SCORE","consolationPolicy":"None","rewardRules":{"type":"Points","threshold":500},"minPlayers":2,"maxPlayers":10}' | jq .code
# => InvalidGameConfiguration.MinRoundsTooLow

# Sin estrategia (CFG-005)
# Sin timeLimit o 0 (CFG-006) => InvalidGameConfiguration.InvalidTimeLimit

# Sin lossPolicy/withdrawalPolicy (CFG-007) => InvalidGameConfiguration.PoliciesRequired

# Categoría inexistente (CFG-004)
curl -X POST http://localhost:5000/api/games -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"name":"BadCat","categoryId":"00000000-0000-0000-0000-000000000000","minRounds":5,"maxRounds":10,"initialDifficulty":1,"difficultyStrategy":"Linear","timeLimitPerQuestionSeconds":30,"scoringSystem":"Standard","lossPolicy":"LOSE_ALL","withdrawalPolicy":"KEEP_CURRENT_SCORE","consolationPolicy":"None","rewardRules":{"type":"Points","threshold":500},"minPlayers":2,"maxPlayers":10}' | jq .code
# => CategoryNotFound / CategoryNotReady
```

### P1 — Inmutabilidad tras iniciar (CFG-003)

```bash
# Iniciar juego
curl -X POST http://localhost:5000/api/games/$GAME_ID/start -H "Authorization: Bearer $TOKEN" | jq .status
# => WAITING_FOR_PLAYERS o IN_PROGRESS

# Intentar mutar configuración (debe fallar 400/409)
curl -X PUT http://localhost:5000/api/games/$GAME_ID -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{"name":"Hacked"}' | jq .code
# => InvalidGameState.ConfigurationImmutable (0% mutación post-inicio, SC-003)
```

### P2 — Rangos incoherentes

```bash
# minRounds > maxRounds, minPlayers > maxPlayers
# => InvalidGameConfiguration.InvalidRange (SC-004)
```

## Tests

```bash
# Unit + Architecture (sin infra)
dotnet test tests/OroQuizClash.Domain.Tests --filter "GameCreate"
dotnet test tests/OroQuizClash.Architecture.Tests

# Application (NSubstitute para IRepository)
dotnet test tests/OroQuizClash.Application.Tests --filter "CreateGameHandler"

# Infrastructure (Testcontainers/Aspire, rowversion + Specification)
dotnet test tests/OroQuizClash.Infrastructure.Tests

# Api (WebApplicationFactory + JWT mock o container real oroidentityserver:latest)
dotnet test tests/OroQuizClash.Api.Tests --filter "CreateGameEndpoint"

# Todos
dotnet test
```

## Expected Outcomes (Success Criteria)

- **SC-001**: 100% rechazos con `ProblemDetails.code` tipificado, sin persistir agregado.
- **SC-002**: creación válida <2s p95.
- **SC-003**: 0% mutación post-inicio.
- **SC-004**: 100% rechazos por rangos inválidos.
- **SC-006**: `GET /api/games/{gameId}` devuelve configuración idéntica.

## Troubleshooting

- `401 Unauthorized`: verificar `Authority=http://identity:5080` en `OroQuizClash.Api`, `/.well-known/openid-configuration` accesible, `SymmetricSecurityKey` compartido si validación simétrica, claim `roles` incluye `ADMIN`/`GAME_MANAGER`.
- `CategoryNotReady`: verificar seeder SPEC-002/003 o stub con `Status=Published` y ≥5 preguntas válidas (4 opciones, 1 correcta, categoría activa).
- `rowversion` conflicto 409: retry con `GET` fresco antes de `POST /start`.
