# Research: Game Configuration

**Feature**: `001-game-configuration` | **Date**: 2026-08-26 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Summary

Todos los `NEEDS CLARIFICATION` del Technical Context fueron resueltos por spec/constitución; no quedan unknowns bloqueantes. Esta fase documenta decisiones, rationale y alternativas para cada área técnica.

## Decisions

### 1. Agregado Game y ValueObject GameConfiguration

- **Decision**: `Game : AggregateRoot<GameId>` con `GameId : StronglyTypedId<Guid>` y `GameConfiguration : ValueObject` inmutable (sin setters), construido vía `Game.Create(GameConfiguration)` que aplica `IBusinessRule` y retorna `Result<Game>`. `Game.Start()` valida `GameStatus == DRAFT|READY` y bloquea mutaciones posteriores; cualquier intento de mutar expone `Error InvalidGameState/ConfigurationImmutable`.
- **Rationale**: Constitución I (Domain First) + III (BuildingBlocks Kernel) y FR-011 exigen comportamiento explícito sin anemic model; `ValueObject` garantiza igualdad por valor e inmutabilidad; `CheckRule(new XRule(...))` de BuildingBlocks evita excepciones para fallos esperados.
- **Alternatives considered**: Entidad mutable con setters públicos (rechazado — expone estado, viola invariantes); `GameConfiguration` como entidad separada con tabla propia (rechazado — es parte del agregado, no tiene identidad; como owned type es más simple y transaccional); factory externa `GameFactory` (rechazado — comportamiento pertenece al agregado).

### 2. Enumeraciones y Políticas

- **Decision**: `GameStatus` (Enumeration), `DifficultyProgressionStrategy` (Enumeration: `Linear`, `Progressive`, `Adaptive`, `CategorySpecific`), `LossPolicy` (`LOSE_ALL`, `LOSE_CURRENT_ROUND`, `LOSE_UNSECURED_POINTS`, `FALLBACK_TO_CHECKPOINT`), `WithdrawalPolicy` (`LOSE_ALL`, `KEEP_CURRENT_SCORE`, `KEEP_SECURED_SCORE`, `KEEP_CHECKPOINT_SCORE`), `ConsolationPolicy`, `ScoringSystem`, `RewardRules` como `Enumeration` o `ValueObject` según complejidad; se validan por pertenencia a conjunto cerrado.
- **Rationale**: Constitución Additional Constraints C exige políticas configurables vía strategy/policy, no hardcodeadas; `Enumeration` de BuildingBlocks da tipado fuerte y extensibilidad sin strings mágicos.
- **Alternatives considered**: `string` libre (rechazado — no validable); `enum` C# nativo (posible pero pierde comportamiento rico de Enumeration — aceptable solo si se aísla con ADR, preferible Enumeration).

### 3. Validación en dos niveles

- **Decision**: Nivel 1 — `Validator<CreateGameCommand>` (BuildingBlocks CQRS `ValidationBehavior`) para contrato (no vacío, rangos sintácticos, `Guid` no vacío). Nivel 2 — `IBusinessRule` en `Game.Create` para invariantes de dominio (CFG-002..007, coherencia `min ≤ max`, categoría válida). Ambos retornan `Result` con `Error` tipificados mapeados a RFC7807 vía `GlobalExceptionHandler`.
- **Rationale**: Constitución I requiere que invariantes de dominio no dependan solo de validación API/Application; FR-012 lo exige explícitamente; separación evita duplicación y mantiene testabilidad.
- **Alternatives considered**: Solo FluentValidation en handler (rechazado — duplica lógica de dominio); solo `IBusinessRule` sin pipeline (rechazado — errores de transporte se confunden con errores de dominio).

### 4. Categoría válida (CFG-004)

- **Decision**: `Game` guarda solo `CategoryId : StronglyTypedId<Guid>`; `CreateGameHandler` inyecta `IRepository<Category, CategoryId>` o `IRepository<Game,...>` + consulta vía `Specification<Category>` (`CategoryByIdSpecification`) para verificar `Category.Status == Published` y `Questions.Count ≥5`. Si no existe o no está publicada, retorna `Error CategoryNotFound/CategoryNotReady`.
- **Rationale**: Constitución Additional Constraints B exige invariantes de pregunta/categoría; SPEC-002/003 son dependencias; validación al momento de creación, no retroactiva.
- **Alternatives considered**: Guardar snapshot de categoría dentro de Game (rechazado — acoplamiento innecesario, categoría evoluciona independientemente); validar solo existencia de Guid (rechazado — viola CFG-004).

### 5. Tiempo por pregunta y rangos

- **Decision**: `TimeLimitPerQuestion : TimeSpan` (o `int Seconds` con ValueObject `QuestionTimeLimit`) validado `>0` y `5–300s` (rango operativo razonable, extensible por ADR). `MinRounds ≥5` inclusivo, `MinRounds ≤ MaxRounds`, `MinPlayers ≥1`, `MinPlayers ≤ MaxPlayers`, `InitialDifficulty` pertenece a conjunto configurado.
- **Rationale**: FR-006 + Assumptions de spec; rango 5–300s evita partidas triviales o bloqueantes; límites inclusivos eliminan ambigüedad.
- **Alternatives considered**: Sin límite superior (rechazado — permite DoS por partidas infinitas); `TimeSpan` nullable (rechazado — CFG-006 exige límite definido).

### 6. Persistencia y concurrencia

- **Decision**: `OroQuizClashDbContext : AppDbContextBase` con `DbSet<Game>`, `ApplyConfiguration(new OutboxEntityTypeConfiguration())`, owned types para `GameConfiguration`, `RowVersion : byte[]` con `[Timestamp]` / `IsConcurrencyToken()`. `EfRepository<Game, GameId>` + `SpecificationEvaluator`. Transacción única `SaveChangesAsync` incluye agregado + Outbox + domain events; `rowversion` protege `StartGame` y cualquier mutación; duplicados se tratan como idempotentes vía `IdempotencyKey` si se añade en evolución.
- **Rationale**: Constitución E/F/G exige SQL Server primario/Oracle portable, abstracciones sin leak, `AppDbContextBase` lifecycle de domain events + Outbox transaccional, concurrencia optimista; FR-013 lo requiere.
- **Alternatives considered**: `DbContext` directo sin `AppDbContextBase` (rechazado — pierde despacho de domain events y Outbox); tabla separada para `GameConfiguration` (rechazado — owned type es más eficiente y mantiene atomicidad del agregado).

### 7. CQRS Vertical Slice

- **Decision**: `Features/Games/CreateGame.cs` contiene `CreateGameCommand : ICommand<Result<CreateGameResponse>>`, `CreateGameValidator : Validator<CreateGameCommand>`, `CreateGameHandler : ICommandHandler<CreateGameCommand, Result<CreateGameResponse>>`, `CreateGameResponse` (DTO, no entidad), `CreateGameEndpoint : IEndpoint` (thin: `ISender.SendAsync` → `Result.ToCreatedResult()`).
- **Rationale**: Constitución IV + III (no MediatR/AutoMapper) y buildingblocks.md; slice autocontenido bajo `Features/` facilita tests y evita carpetas genéricas.
- **Alternatives considered**: Carpetas separadas `Commands/Queries/Handlers` (rechazado — viola Vertical Slice y constitución); MediatR (prohibido explícitamente).

### 8. Identidad via OroIdentityServer (Podman)

- **Decision**: OroQuizClash no crea `User`; `CreateGameEndpoint` exige JWT bearer validado contra `http://identity:5080/.well-known/openid-configuration` → `jwks_uri`, con `Authority` en `AddAuthentication().AddJwtBearer()`. Roles requeridos `ADMIN`/`GAME_MANAGER` mapeados desde claims `roles`/`tenant_id`/`is_master_admin`; `GamePlayer` referencia `sub` externo. Container `oroidentityserver:latest` se orquesta vía Podman compose o Aspire `AddContainer("identity-server", "oroidentityserver:latest")` con env `SymmetricSecurityKey`, `ConnectionStrings__identitydb`, `SEED_ADMIN_*`, volumen `identity-dp-keys`; DB `identitydb` aislada.
- **Rationale**: Constitución VI + H (delegación total); `draft/oroidentityserver-specification.md` §Integration y §Containerized Deployment definen contrato exacto; evita duplicar password hashing/JWT signing.
- **Alternatives considered**: ASP.NET Identity local (prohibido — viola VI); validar JWT sin discovery (rechazado — frágil, no rota keys).

### 9. Contratos y errores

- **Decision**: Contrato OpenAPI para `POST /api/games` con schema `CreateGameRequest` (nombre, categoryId, min/max rounds, dificultad, estrategia, timeLimit, scoring, policies, min/max players) y `CreateGameResponse` (`gameId`), errores RFC7807 con códigos `InvalidGameConfiguration`, `CategoryNotReady`, `InvalidGameState`, `ConfigurationImmutable`, `CategoryNotFound`.
- **Rationale**: Constitución I.E.8 (ProblemDetails) y J (DTOs, no exponer entidades); FR-012 exige `Error` tipificados.
- **Alternatives considered**: Exponer `Game` directamente (rechazado — leak de invariantes y `RowVersion`).

### 10. Testing

- **Decision**: Domain.Tests (xUnit) para `Game.Create` con `Arrange/Act/Assert` por cada CFG; Application.Tests con NSubstitute para `IRepository`; Infrastructure.Tests con `EfRepository` + Testcontainers/Aspire; Api.Tests con `WebApplicationFactory` + JWT mock o container real `oroidentityserver:latest` con `admin/Admin@123456`; Architecture.Tests con NetArchTest/ArchUnit para verificar Domain sin refs a ASP.NET/EF/RabbitMQ.
- **Rationale**: Constitución Testing Strategy exige suites mínimas y AAA; Domain/Application sin Web/DB.
- **Alternatives considered**: Solo integration tests (rechazado — no aísla reglas críticas); mocks de `DbContext` (rechazado — no prueba EF real).

## Resolved Clarifications

| Topic | Resolution |
|-------|------------|
| `maxRondas` opcional | Tratado como requerido con `maxRounds ≥ minRounds`; si negocio lo hace opcional, default `= minRounds` vía regla de aplicación sin romper CFG-002. Documentado en Assumptions. |
| Unicidad nombre | No requerida en v1; solo no vacío 3–100 chars. |
| Límite superior tiempo | 300s como política por defecto, extensible por ADR. |
| Categoría despublicada tras creación | Validación solo al crear; juegos existentes no se invalidan. |

## References

- `draft/constitution.md` §5, §7, §8, §12, §13
- `draft/oroidentityserver-specification.md` §Containerized Deployment, §Integration
- `draft/libraries/buildingblocks.md` — Kernel/CQRS/Infrastructure/ServiceDefaults contracts
- `BuildingBlocks` source en `src/BuildingBlocks/` (net10.0)
