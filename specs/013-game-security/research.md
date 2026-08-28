# Research: Game Security (SPEC-013)

**Date**: 2026-08-28 | **Plan**: [plan.md](plan.md) | **Spec**: [spec.md](spec.md)

Phase 0 — resolución de decisiones técnicas. No quedó ningún NEEDS CLARIFICATION en el Technical Context; las decisiones siguientes resuelven los puntos de diseño identificados tras inspeccionar el código existente (`Program.cs` políticas actuales, `SubmitAnswer`/`RedeemReward` idempotencia, `ValidationBehavior`, `OroQuizClashDbContext`, `BuildingBlocks.ServiceDefaults`).

## R1 — Mapeo de 14 permisos a políticas ASP.NET Core

- **Decision**: Crear `Permission` y `Role` como `Enumeration` en Domain (14 valores, 4 roles) y registrar 14 políticas nombradas en `Api/Authorization/SecurityPolicies.cs` (`Category.Read`, `Category.Write`, ... `Audit.Read`) que mapean a `RequireAssertion` sobre claims `role`/`roles` (ya patrón de `AdminOrGameManager`). Además, añadir `AuthorizationBehavior<TRequest,TResponse> : IPipelineBehavior` en Application que lee un atributo/marcador `RequiresPermission(Permission)` en el `ICommand`/`IQuery` y valida contra `ClaimsPrincipal` antes de handler — centraliza lógica y permite testing sin WebApplicationFactory.
- **Rationale**: FR-019 exige políticas declarativas centralizadas, no ad-hoc por endpoint. Usar policies ASP.NET Core satisface el check de infraestructura (`[Authorize(Policy=...)]` en `IEndpoint`), y el Behavior duplica la validación en Application para que tests de Application no dependan de Api. Patrón ya existente para `GameClaims.IsOrganizer`.
- **Alternatives considered**:
  - Solo policies en Api sin Behavior: rechazado — Application tests no podrían verificar matriz 14×4 sin arrancar host.
  - Solo Behavior sin policies: rechazado — perdería integración con `RequireAuthorization()` y OpenAPI.
  - Casbin/PolicyServer externo: rechazado — sobrediseño para 14 permisos; BuildingBlocks no lo provee.

## R2 — Servidor como autoridad — cómo ignorar campos cliente

- **Decision**: Auditoría de cada slice: `SubmitAnswer` ya ignora Score/Correctness/Time y usa `qId => question.IsCorrect` + `DateTimeOffset.UtcNow` (verificado en `Game.SubmitAnswer`); `CreateGame`/`StartGame`/`FinishGame` ya ignoran GameState cliente y usan máquina de estados; `JoinGame`/`SubmitAnswer` ya usan `sub` claim (SPEC-011). Para cerrar FR-006/007 se añade `PlayerId` resolver centralizado en `GameClaims.GetSub` y se eliminan campos `score`/`correctness`/`elapsedTime`/`gameState` de cualquier `Request` DTO donde aún existan (o se marcan `[JsonIgnore]` y se loguea warning si llegan). Validación en handler rechaza `questionId`/`answerOptionId` fuera de ronda actual (FR-009) usando `Game.CurrentRound` + `QuestionByIdSpecification`.
- **Rationale**: El dominio ya es autoritativo; el riesgo es DTO que aún bindea campos extra y alguien los use por error. Limpiar DTOs + test anti-tampering garantiza SC-002.
- **Alternatives considered**:
  - Sanitizar en middleware que borra campos: rechazado — frágil y oculta contrato; mejor no exponerlos en DTO.
  - Validar Time con NTP: rechazado — basta reloj servidor (FR-006).

## R3 — Idempotencia y anti-replay — ventana y store

- **Decision**: Reutilizar índices únicos existentes para idempotencia natural: respuestas `(GameId, PlayerId, RoundId)` (ya en `Answer` + `ValidateIdempotencyRule`), `RewardRedemption` `(PlayerId, IdempotencyKey)` con índice filtrado. Para operaciones genéricas donde se requiera `Idempotency-Key` header (creación de juego/canje), introducir tabla `IdempotencyRecord { Id, Key, PlayerId, CreatedAt, ResponseHash }` con `Key` único por actor y ventana de 24h (configurable `Security:IdempotencyWindowHours` default 24). Anti-replay: si `Key` ya existe con mismo hash → retorna original; si mismo `Key` con hash distinto o fuera de ventana (>24h) → 400 `ReplayDetected`. Implementado en `IdempotencyService` + `IdempotencyBehavior` (pipeline) o como `IRepository<IdempotencyRecord>` para single-node; sin Redis.
- **Rationale**: FR-012/013 exigen idempotencia y rechazo de replay sin duplicar efecto. Ventana 24h balancea reintentos de red vs almacenamiento; single-node evita dependencia externa (asunción del spec).
- **Alternatives considered**:
  - Solo header `Idempotency-Key` para todo: rechazado — respuestas ya tienen clave natural más simple.
  - Store en memoria `IMemoryCache` sin persistencia: rechazado — perdería idempotencia tras reinicio; EF tabla es durable y transaccional con `SaveChanges`.

## R4 — Rate limiting — particionado por juego/jugador

- **Decision**: `Microsoft.AspNetCore.RateLimiting` (built-in .NET 8/10) con `PartitionedRateLimiter.Create`: partición por `sub` claim (player) + `gameId` (de ruta) para `SubmitAnswer`/`RedeemReward`, y por IP para endpoints anónimos (`health`). Política `GamePlayLimiter`: `FixedWindow` 5 req/s por jugador-juego (configurable `Security:RateLimit:GamePlay:PermitLimit` default 5, `Window` 1s) con `QueueLimit=0` y `OnRejected` → 429 + `Retry-After`. Para SC-009, pruebas de partición verifican que ráfaga en juego A no afecta juego B (límites independientes). Registrar en `Program.cs` y aplicar vía `.RequireRateLimiting("GamePlayLimiter")` en endpoints sensibles o como `RateLimitingBehavior` para comandos.
- **Rationale**: FR-014/015 exigen limiting por actor/recurso aislado; `PartitionedRateLimiter` es la solución nativa single-node, sin Redis, y expone headers `X-RateLimit-*` estándar.
- **Alternatives considered**:
  - `AspNetCoreRateLimit` (lib externa): rechazado — duplicaría lo nativo.
  - `ConcurrencyLimiter` global: rechazado — penalizaría a inocentes (viola SC-009).
  - Nginx/Reverse-proxy limiting: rechazado — debe ser en app para partición por `gameId`.

## R5 — Validación de entrada — 3 niveles sin fuga

- **Decision**: Mantener `ValidationBehavior<TRequest,TResponse>` existente (FluentValidation) para Application; añadir validadores en cada slice que ya existen (reutilizar). En Api, `IEndpoint` ya delega a `ISender` y mapea `Result` → ProblemDetails; asegurar que `ValidationBehavior` retorna `Result.Failure` con `Error.Validation` → 400 sin detalles internos (FR-011/020). Domain invariants (`IBusinessRule`) permanecen como última defensa (ej. `ValidateIdempotencyRule`, `PlayerAlreadyJoined`).
- **Rationale**: Constitución I requiere 3 niveles; BuildingBlocks ya provee Behavior — no reinventar.
- **Alternatives considered**:
  - DataAnnotations solo: rechazado — no cubre reglas de negocio.
  - Manual `if` en handlers: rechazado — dispersa validación.

## R6 — Audit trail — append-only y correlación

- **Decision**: Nueva entidad `AuditEntry` (Guid Id, DateTimeOffset Timestamp (server), string ActorId, string Action, string Permission, string Resource, string Result, string Reason, string CorrelationId, string? TenantId) en `Domain/Audit`. Configuración EF: tabla `AuditEntries`, `Id` PK, índice `(Timestamp, Resource)`, `CorrelationId`, `ActorId`; sin `Update`/`Delete` — repositorio solo expone `Add`. Escritura vía `AuditBehavior : IPipelineBehavior` que intercepta todo `ICommand`/`IQuery` sensible (o vía EF `SaveChangesInterceptor` que lee `ChangeTracker` + `IHttpContextAccessor` para `sub`/`CorrelationId`). `CorrelationId` se propaga vía `BuildingBlocks.ServiceDefaults` (ya inyecta `X-Correlation-ID` → `Activity`/`ILogger`). Lectura via `GetAuditEntries` Query (paginado, filtra por `Audit.Read`/`Report.Read`).
- **Rationale**: FR-016/017/018 exigen append-only, inmutable, correlacionado y consultable; Behavior es transaccional (misma `SaveChanges` que agregados) y testable; interceptor capturaría también cambios directos pero Behavior es más explícito.
- **Alternatives considered**:
  - Log file / Seq solo: rechazado — no consultable por `Audit.Read` ni transaccional.
  - Outbox/RabbitMQ para audit: rechazado — audit debe ser sincrónico y local para SC-007.

## R7 — Rate limiting vs idempotencia — orden de ejecución

- **Decision**: Orden de Behaviors: `ValidationBehavior` → `RateLimiting` (o middleware antes de CQRS) → `IdempotencyBehavior` → `AuthorizationBehavior` → `AuditBehavior` → Handler. Así, payload malformado se rechaza antes de consumir cuota; replay/idempotencia se detecta antes de autorización (ahorra trabajo) pero auditoría registra incluso rechazos.
- **Rationale**: Minimiza costo y garantiza que todos los rechazos quedan auditados.
- **Alternatives considered**:
  - Audit antes de todo: rechazado — duplicaría lógica.
  - Rate limiting después de handler: rechazado — no protegería.

