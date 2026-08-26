# Quickstart: Categories

**Feature**: `002-categories` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Contracts**: [contracts/categories.openapi.yaml](contracts/categories.openapi.yaml)

Guía de validación end-to-end para el ciclo de vida de `Category` y gate `PublishCategory ≥5 válidas`.

## Prerequisites

- .NET 10 SDK, Podman, `dotnet ef` si aplica migraciones.
- `OroQuizClash.slnx` compilable (`dotnet build OroQuizClash.slnx`), `OroQuizClashDbContext` con `DbSet<Category>` + `DbSet<Game>` ya migrado/EnsureCreated.
- `OroIdentityServer` `oroidentityserver:latest` corriendo (`podman build -f src/IdentityServer/IdentityServer/Dockerfile -t oroidentityserver:latest .` + `podman compose up -d` o vía `aspire start`), discovery `http://localhost:5080/.well-known/openid-configuration`.
- `IQuestionCounter` stub `InMemoryQuestionCounter` (para 002) o `SPEC-003` real con 5 preguntas válidas de ejemplo (cada una 4 opciones, 1 correcta, `Active`, `CategoryId` igual, `Difficulty`/`AcademicLevel`/`AgeRange` compatibles).

## Setup

```bash
# 1. AppHost (Aspire) — levanta sqlserver/postgres/redis/rabbitmq/identity-server/api
aspire start
aspire wait oroclash-api
# Dashboard: https://localhost:17113

# 2. Token ADMIN (or GAME_MANAGER) de OroIdentityServer
curl -X POST http://localhost:5080/connect/token \
  -d "grant_type=password&username=admin&password=Admin@123456&scope=openid profile email" \
  -u "oroclash-api:secret" > /tmp/token.json
export TOKEN=$(jq -r .access_token /tmp/token.json)

# 3. Alternativa sin AppHost: dotnet run Api directo
# dotnet run --project src/OroQuizClash.Api --urls http://localhost:5000
# export TOKEN=...
```

## Validation Scenarios

### P1 — Crear y actualizar categoría (DRAFT)

```bash
# Crear válida
curl -X POST http://localhost:5000/api/categories -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{
  "name":"Historia Universal","description":"Desde prehistoria","knowledgeArea":"Humanidades","academicLevel":"Secundaria","ageMin":13,"ageMax":17,"difficultyLevel":3,"tags":["historia","secundaria"],"publishConfiguration":{"requiresModeration":false}
}' | tee /tmp/cat.json | jq
# Esperado: 201 Created, Location: /api/categories/{id}, body {id,name,status:"DRAFT",ageMin:13,tags:["historia","secundaria"]}
export CAT_ID=$(jq -r .id /tmp/cat.json)

# GET idéntico
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/categories/$CAT_ID | jq .name
# → "Historia Universal"

# Update en DRAFT → 200
curl -X PUT http://localhost:5000/api/categories/$CAT_ID -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{
  "name":"Historia Universal","description":"Actualizada","knowledgeArea":"Humanidades","academicLevel":"Secundaria","ageMin":13,"ageMax":17,"difficultyLevel":4,"tags":["historia","universal"],"publishConfiguration":{"requiresModeration":false}
}' | jq .difficultyLevel
# → 4

# Rechazo edad invertida
curl -s -w "%{http_code}\n" -X POST http://localhost:5000/api/categories -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{
  "name":"Bad","knowledgeArea":"X","academicLevel":"Primaria","ageMin":17,"ageMax":13,"difficultyLevel":2,"tags":[]
}' | tail
# → 400 InvalidCategoryConfiguration.InvalidAgeRange

# ARCHIVED → Update rechazado
# (previamente archivar categoría, luego PUT debe dar 400 InvalidCategoryState)
```

### P1 — Publish gate ≥5 válidas

```bash
# Publish con 0 válidas → 400 CategoryNotPublishable
curl -s -w "%{http_code}\n" -X POST http://localhost:5000/api/categories/$CAT_ID/publish -H "Authorization: Bearer $TOKEN" | tail
# → 400, body {code:"CategoryNotPublishable", detail:"Requires ≥5 valid questions"}

# Crear 4 preguntas válidas para $CAT_ID vía SPEC-003 (cada una 4 opciones, 1 correcta, Active, alineada)
# Si SPEC-003 no existe, el stub InMemoryQuestionCounter se siembra en test:
#   InMemoryQuestionCounter.Seed($CAT_ID, 4) // 4 válidas
# Luego publish sigue fallando:
curl -s -X POST http://localhost:5000/api/categories/$CAT_ID/publish -H "Authorization: Bearer $TOKEN" | jq .code
# → CategoryNotPublishable

# Añadir la 5ª válida → Publish OK 200 → ACTIVE
#   InMemoryQuestionCounter.Seed($CAT_ID, 5) // o POST /api/questions 5 veces
curl -X POST http://localhost:5000/api/categories/$CAT_ID/publish -H "Authorization: Bearer $TOKEN" | jq .status
# → "ACTIVE"
# Verificar evento CategoryPublishedDomainEvent emitido (logs OTel con CategoryId)

# Pregunta desalineada (ej. AgeRange 30-40 fuera de 13-17) no cuenta → si se invalida una de las 5, siguiente Publish debe fallar hasta reponer
```

### P1 — Transiciones y concurrencia (rowversion)

```bash
# ACTIVE → Deactivate → INACTIVE
curl -X POST http://localhost:5000/api/categories/$CAT_ID/deactivate -H "Authorization: Bearer $TOKEN" | jq
# → 200, status INACTIVE

# INACTIVE → Archive → ARCHIVED (terminal)
curl -X POST http://localhost:5000/api/categories/$CAT_ID/archive -H "Authorization: Bearer $TOKEN" | jq .status
# → ARCHIVED

# ARCHIVED → Publish rechazado 400 InvalidCategoryState
curl -s -w "%{http_code}\n" -X POST http://localhost:5000/api/categories/$CAT_ID/publish -H "Authorization: Bearer $TOKEN" | tail
# → 400

# Concurrencia: dos Publish simultáneos en INACTIVE con 5 válidas → uno 200, otro 409 Conflict (rowversion)
# (probar con parallel curl o test GameConcurrencyTests patrón)
```

### P2 — Filtrado y paginación

```bash
# Crear 3 categorías distintas
# A: Humanidades/Secundaria/ACTIVE tags [historia]
# B: Ciencias/Universidad/INACTIVE tags [fisica]
# C: Humanidades/Secundaria/ACTIVE tags [historia, sec]

curl -H "Authorization: Bearer $TOKEN" "http://localhost:5000/api/categories?knowledgeArea=Humanidades&academicLevel=Secundaria&state=ACTIVE&page=1&pageSize=10" | jq '.items | length'
# → 2 (A y C)

curl -H "Authorization: Bearer $TOKEN" "http://localhost:5000/api/categories?tag=historia&state=ACTIVE" | jq '.items[].name'
# → Historia Universal ...

# Game Configuration validación: POST /api/games con categoryId ARCHIVED o con <5 válidas → 400 CategoryNotReady
```

## Tests

```bash
# Unit + Architecture (sin infra)
dotnet test tests/OroQuizClash.Domain.Tests --filter Category
dotnet test tests/OroQuizClash.Architecture.Tests

# Application (NSubstitute IQuestionCounter, IRepository)
dotnet test tests/OroQuizClash.Application.Tests --filter Category

# Infrastructure (EfRepository + OroQuizClashDbContext + Specification + rowversion)
dotnet test tests/OroQuizClash.Infrastructure.Tests --filter Category

# Api (WebApplicationFactory + JWT mock ADMIN + InMemoryQuestionCounter)
dotnet test tests/OroQuizClash.Api.Tests --filter Category

# Todos
dotnet test
```

## Expected Outcomes (Success Criteria)

- **SC-001**: Crear válida → 201 DRAFT <1s 95%, GET idéntico 100%.
- **SC-002**: Publish <5 → 400 CategoryNotPublishable, estado no cambia 100%.
- **SC-003**: 5 válidas → Publish 200 ACTIVE <2s, evento emitido 100%.
- **SC-004**: Pregunta 3 opciones/0-2 correctas/inactiva/desalineada → 0% cuenta, no bypass.
- **SC-005**: Transición inválida → 400/409, segundo Publish concurrente → 409 100%.
- **SC-006**: Filtrado por área/nivel/estado/tag 100% precisión en 20 items.
- **SC-007**: Flujo crear→5 preguntas→publicar completado sin soporte 90% usability.

## Troubleshooting

- `400 InvalidCategoryConfiguration.InvalidAgeRange`: verificar `ageMin≤ageMax` y `0–120`.
- `CategoryNotPublishable`: verificar `IQuestionCounter.CountValidAsync` retorna ≥5; cada pregunta debe tener 4 opciones, 1 correcta, `Active`, `CategoryId` igual y `Difficulty/AcademicLevel/AgeRange` compatibles.
- `409 Conflict`: `rowversion` desalineado — recargar `GET` y reintentar con nuevo `RowVersion`.
- `401/403`: verificar `TOKEN` con `roles` ADMIN/GAME_MANAGER y `Authority http://localhost:5080`.
- `404`: `CategoryId` mal formado o no existe.
