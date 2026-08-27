# Research: Game Lifecycle

**Feature**: `004-game-lifecycle` | **Date**: 2026-08-27 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Summary

0 `NEEDS CLARIFICATION` en spec; investigación consolida 8 decisiones para los 9 estados y 10 transiciones: máquina `Enumeration`, invariantes 1-6 en Domain, `rowversion` + `IRepository<Game>` con `Specification`, 9 `DomainEvent` dentro de `AppDbContextBase.SaveChanges` + `Outbox` opcional, selección de pregunta vía `IQuestionSelectionStrategy` (SPEC-003), idempotencia `Join/SubmitAnswer`, y wiring `IQuestionCounter` para `MarkReady` gate. Todo alineado a constitución v1.1.0 + BuildingBlocks net10.0 y research de `001/002/003`.

## Decisions

### 1. Game AggregateRoot y 9 estados como Enumeration

- **Decision**: `Game : AggregateRoot<GameId>` con `GameId : StronglyTypedId<Guid>` existente; `GameStatus : Enumeration<GameStatus>` ampliado a 9 valores `DRAFT(1), READY(2), WAITING_FOR_PLAYERS(3), IN_PROGRESS(4), ROUND_IN_PROGRESS(5), ROUND_COMPLETED(6), FINISHED(7), CANCELLED(8), FORCED_FINISHED(9)` con helpers `IsTerminal (FINISHED/CANCELLED/FORCED_FINISHED)`, `IsStarted (≥WAITING_FOR_PLAYERS)`, `IsRoundActive (ROUND_IN_PROGRESS)`, `CanTransitionTo(to)` y matriz estática `IsValidTransition(from,to)`. Estado fino de ronda se refleja también en `GameStatus` para simplificar orquestación (alternativa `Game.Status=IN_PROGRESS` + `CurrentRound.Status` se documenta pero se adopta `GameStatus` como source of truth grueso).
- **Rationale**: Constitución A exige máquina explícita con 9 estados y `IsValidTransition` rechazando `FINISHED→StartGame`; `Enumeration` aporta `FromId`/`FromName`/`GetAll` y comportamiento rico vs `enum` nativo, persistible vía `HasConversion(s=>s.Id, id=>GameStatus.FromId(id))`.
- **Alternatives**: `enum GameStatus` nativo (rechazado — pierde comportamiento y conversión centralizada, aunque aceptable con ADR); `Game` + `Round` como agregados separados (rechazado — complica invariante "no dos rondas activas" y `players≥MinPlayers` atómico; `GameRound` como `Entity` composición dentro de `Game` garantiza transacción única).

### 2. Transiciones y Gates (Reglas 1-6) en Domain con IBusinessRule + Result

- **Decision**: Cada transición es método `Result` en `Game` que aplica `IBusinessRule` y retorna `Error` tipificado, emitiendo `DomainEvent` solo si éxito:
  - `MarkReady(ICategoryValidator, IQuestionCounter)` → `DRAFT→READY` gate `Category published + CountValid≥5` + `GameConfigurationValid` (MinRounds≥5 etc. de SPEC-001) → `GameReadyDomainEvent`.
  - `OpenLobby()` → `READY→WAITING_FOR_PLAYERS` → `LobbyOpenedDomainEvent` (opcional, puede reuse `GameReady`).
  - `JoinPlayer(UserId)` → solo `WAITING_FOR_PLAYERS`, valida `PlayerAlreadyJoined` / `GameFull` (≤MaxPlayers) → `PlayerJoinedDomainEvent`.
  - `Start()` → `WAITING_FOR_PLAYERS→IN_PROGRESS` gate `players≥MinPlayers` → `GameStartedDomainEvent`, bloquea `GameConfiguration` inmutable (`IsStarted` futuro `UpdateConfiguration` → `ConfigurationImmutable`).
  - `StartRound(IQuestionSelectionStrategy, GameRoundId, QuestionId)` → `IN_PROGRESS` o `ROUND_COMPLETED` → `ROUND_IN_PROGRESS` gate `PreviousRoundNotCompleted` (no `ROUND_IN_PROGRESS` activo), selecciona `QuestionId` PUBLISHED no usada, incrementa `RoundNumber`, crea `GameRound` → `RoundStartedDomainEvent`.
  - `CompleteRound()` → `ROUND_IN_PROGRESS→ROUND_COMPLETED` → `RoundCompletedDomainEvent`.
  - `Finish()` → `IN_PROGRESS`/`ROUND_COMPLETED` (y `ROUND_IN_PROGRESS` si política) → `FINISHED` → `GameFinishedDomainEvent`.
  - `Cancel(reason)` → `DRAFT/READY/WAITING_FOR_PLAYERS/IN_PROGRESS/ROUND_*` → `CANCELLED` (terminal) → `GameCancelledDomainEvent`.
  - `ForceFinish(reason)` → `IN_PROGRESS/ROUND_*` → `FORCED_FINISHED` → `GameForcedFinishedDomainEvent`.
  Transiciones inválidas → `Error InvalidGameState` (`ErrorType.Validation` → 400/409), protegidas por `RowVersion` (segundo intento → `DbUpdateConcurrencyException` → 409).
- **Rationale**: Constitución I (Domain First) + A (state machine) + FR-003..FR-008; `Result` con `Error` mapeado a `ProblemDetails` (400 validation, 404 not found, 409 conflict) permite API thin.
- **Alternatives**: Lógica en Application handlers (rechazado — viola Domain First); `GameService` anémico con setters (rechazado — expone estado mutable, pierde `RaiseDomainEvent` dentro de `SaveChanges`).

### 3. Validación de Configuración y Categoría para MarkReady (Regla 1)

- **Decision**: `MarkReady` reutiliza validación de `Game.Create` (SPEC-001: `MinRounds≥5`, `MaxRounds≥MinRounds`, `TimeLimit 5–300`, `Difficulty 1..5`, `MinPlayers≥1`) + verifica `ICategoryValidator.IsPublished(CategoryId)` y `IQuestionCounter.CountValidAsync(CategoryId)≥5` (SPEC-002/003). Si falla → `CategoryNotReady` / `InvalidGameConfiguration` sin cambiar estado. `CreateGame` ya valida config pero `MarkReady` revalida por si categoría se archivó o preguntas se invalidaron entre `Create` y `Ready`.
- **Rationale**: Regla 1 "No puede iniciar sin configuración válida" + constitución B (≥5 válidas); `ICategoryValidator` y `IQuestionCounter` son ports del Domain, implementados en Infrastructure leyendo `IRepository<Category>` / `IRepository<Question>` con `Specification`; mantiene Clean Arch.
- **Alternatives**: Validar solo en `CreateGame` (rechazado — deja ventana de inconsistencia si categoría se despublica luego); validar solo categoría sin contar preguntas (rechazado — viola B).

### 4. Persistencia: Game + GameRound/GamePlayer composición, RowVersion, Specification

- **Decision**: `OroQuizClashDbContext : AppDbContextBase` ya con `DbSet<Game>`; `GameTypeConfiguration : IEntityTypeConfiguration<Game>` con `HasKey(Id→Guid)`, `HasConversion` para `GameStatus` y `DifficultyStrategy`/`LossPolicy` etc. dentro de `GameConfiguration` como `OwnsOne`, `Property(RowVersion).IsRowVersion().IsConcurrencyToken()`, `HasMany(g=>g.Rounds).WithOne().HasForeignKey("GameId").OnDelete(Cascade)` + `HasMany(g=>g.Players)`, `HasIndex(Status)`, `HasIndex(CategoryId)`, `HasIndex(Status, CreatedBy)`. `GameRoundTypeConfiguration` y `GamePlayerTypeConfiguration` con `HasKey` + `StronglyTypedId` converter + `HasIndex(GameId, RoundNumber).IsUnique()` para idempotencia `StartRound`. `OutboxEntityTypeConfiguration` ya en `AppDbContextBase`.
- **Rationale**: Constitución E (SQL Server primario, `AppDbContextBase`+Outbox misma transacción, `rowversion` para concurrencia, `Specification` para filtros) y F (optimistic concurrency preferido). `Specification<Game>` (`GameFilterSpecification`) para `GetGames` con `Where(Status==)`, `Where(CategoryId==)`, `Paginación` + `AsNoTracking`.
- **Alternatives**: Tabla separada por cada entidad con FK físico cross-aggregate (rechazado — `GameRound` no es agregado separado, composición más simple y transaccional); `RowVersion` como `long` (rechazado — SQL Server `rowversion` es `byte[]` idiomático).

### 5. Selección de Pregunta en StartRound vía IQuestionSelectionStrategy (SPEC-003)

- **Decision**: `Game.StartRound(IQuestionSelectionStrategy selector, QuestionSelectionCriteria criteria)` delega a `selector.SelectAsync(criteria)` donde `criteria` se construye con `Game.Configuration.CategoryId`, `Difficulty` progresiva, `AcademicLevel`/`AgeRange`, `PreviousQuestionIds = Rounds.Select(r=>r.QuestionId)`, `GameId`, `RoundNumber = Rounds.Count+1`, `Take=1`. Si `selector` retorna `NoAvailableQuestion` → `StartRound` falla con `NoAvailableQuestion` y no crea `ROUND_IN_PROGRESS` (evita fantasma); el llamante puede entonces `Finish` o `ForceFinish`. `IQuestionSelectionStrategy` ya implementado en 003 (`Random` default, `DifficultyAware` fallback ±1).
- **Rationale**: Constitución B (selección detrás de `IQuestionSelectionStrategy`, evita repetición dentro de `Game`) + SPEC-003 FR-013/014 (7 params); `Game` no conoce `Question` storage, solo `QuestionId` (FK lógico), mantiene Clean Arch; estrategia intercambiable sin cambiar `Game`.
- **Alternatives**: `Game` con `List<Question>` navigation (rechazado — acopla agregados, rompe boundary); selección client-side (rechazado — viola Authoritative Domain Engine).

### 6. Eventos de Dominio y Outbox

- **Decision**: 9 `DomainEvent` dentro de `Game` (`RaiseDomainEvent`): `GameCreatedDomainEvent(GameId)` en `Create`, `GameReadyDomainEvent(GameId)` en `MarkReady`, `PlayerJoinedDomainEvent(GameId, UserId)` en `JoinPlayer`, `GameStartedDomainEvent(GameId)` en `Start`, `RoundStartedDomainEvent(GameId, RoundId, RoundNumber, QuestionId)` en `StartRound`, `RoundCompletedDomainEvent(GameId, RoundId)` en `CompleteRound`, `GameFinishedDomainEvent(GameId)` en `Finish`, `GameCancelledDomainEvent(GameId, Reason)` en `Cancel`, `GameForcedFinishedDomainEvent(GameId, Reason)` en `ForceFinish`. Todos dispatch in-process en `AppDbContextBase.SaveChanges`; si se requiere integración externa (ej. `GameFinished` para estadísticas/recompensas SPEC-011) se publica `IntegrationEvent` (`GameFinishedIntegrationEvent`) vía `IOutboxWriter`+`OutboxProcessor`→RabbitMQ (topic, publisher confirms, manual ack, retries) nunca antes de commit.
- **Rationale**: Constitución IV (DomainEvent in-process) + G (Outbox→RabbitMQ, nunca antes de commit) + I (audit + OTel); `GameCancelled/ForcedFinished` auditados con `Reason`.
- **Alternatives**: Publicar RabbitMQ directamente en handler (rechazado — viola G, pierde atomicidad); no emitir `PlayerJoined` (rechazado — necesario para SPEC-011 y SignalR futuro).

### 7. CQRS Vertical Slice, Validación 3 Niveles, Errores y Observabilidad

- **Decision**: Cada transición en `Features/Games/` con `*Command : ICommand<Result<*Response>>` (o `IQuery` para `GetGame`), `*Validator : IValidator<Command>` (BuildingBlocks `ValidationBehavior`) para validación aplicación (ej. `CancelCommand.Reason` 3–500), `*Handler : ICommandHandler<Command,Result>` que carga `Game` vía `IRepository<Game,GameId>`, llama método de dominio, `SaveChangesAsync`, retorna `Response` + maneja `DbUpdateConcurrencyException`→`ConcurrencyConflict` (409), `*Response` DTO inmutable, `*Endpoint : IEndpoint` thin (`ISender.SendAsync→Result.ToHttpResult()` → `201` para `Create`, `200` para resto). Errores mapeados a `ProblemDetails` via `GlobalExceptionHandler`: `400 InvalidGameConfiguration/CategoryNotReady/NotEnoughPlayers/RoundAlreadyInProgress/NoActiveRound/ConfigurationImmutable/InvalidGameState/Validation`, `404 GameNotFound/CategoryNotFound/QuestionNotFound`, `409 ConcurrencyConflict/GameFull/PlayerAlreadyJoined`. OTel `ServiceDefaults` con logs `CorrelationId/GameId/PlayerId/RoundId`.
- **Rationale**: Constitución I (3 niveles), IV (Vertical Slice), III (BuildingBlocks CQRS), I (Error codes).
- **Alternatives**: FluentValidation externo (rechazado — BuildingBlocks `Validator` suficiente); central `GameService` (rechazado — viola slice).

### 8. Identidad y Autorización + Idempotencia

- **Decision**: Endpoints `CreateGame/MarkReady/StartGame/Cancel/ForceFinish` requieren `ADMIN`/`GAME_MANAGER` via JWT `roles` OroIdentityServer (`Authority http://identity:5080`, policy `AdminOrGameManager` reuse de `Program.cs`); `JoinGame/SubmitAnswer` requieren `PLAYER` (policy `Player`); `GetGame` requiere `PLAYER` o superior (autenticado). `PerformedBy` audit con `sub` claim. Idempotencia: `JoinGame` por `PlayerId` (segundo join mismo jugador idempotente → ya unido), `SubmitAnswer` por `IdempotencyKey` (`PlayerId+RoundId`) sin duplicar `PointTransaction` (verifica `AnswerSubmissionId` existente), `StartRound/CompleteRound` por `GameId+RoundNumber` único. Concurrencia `rowversion` protege doble `StartGame`/`StartRound`.
- **Rationale**: Constitución VI+H (no local user store, `sub` es `UserId`, `GamePlayer` no es credencial) + F (idempotency + optimistic concurrency).
- **Alternatives**: `AllowAnonymous` para `JoinGame` (rechazado — viola H, todos los game APIs requieren JWT salvo health); `Late join` en `IN_PROGRESS` (rechazado — fuera de alcance v1, se rechaza `InvalidGameState`).

## Resolved Clarifications

| Topic | Resolution |
|-------|------------|
| Estado FINO vs GRUESO | `GameStatus` es source of truth grueso (9 valores); `GameRound.Status` fino (`ROUND_IN_PROGRESS`/`ROUND_COMPLETED`) se refleja también en `Game.Status` para orquestación simple. Se documenta en `Assumptions`; plan técnico adopta `Game.Status` como grueso y `Game.CurrentRound` como fino sin romper matriz. |
| Late join después de IN_PROGRESS | No permitido en v1; `JoinGame` solo `WAITING_FOR_PLAYERS` → `InvalidGameState`. Podría habilitarse luego con nueva spec sin romper ciclo. |
| NoAvailableQuestion en StartRound | No crea ronda fantasma; `StartRound` falla `NoAvailableQuestion` y el juego permanece `IN_PROGRESS` o `ROUND_COMPLETED`, permitiendo `Finish`/`ForceFinish`. |
| Reason para Cancel/ForceFinish | Requerido 3–500 chars; validado en `CancelCommandValidator` y `ForceFinishCommandValidator`; vacío → `400 Validation`. |

## References

- `draft/constitution.md` §5 (State Machine 9 estados), §8 (Game Configuration), §6 (Question Invariants B)
- `draft/game-concept.md` §3-5 (Game/Round lifecycle, 9 estados), §12-14 (Withdrawal/Loss)
- `draft/oroidentityserver-specification.md` (OIDC discovery, JWT `roles`/`sub`)
- `BuildingBlocks` source `src/BuildingBlocks/` (net10.0, `Enumeration`, `ValueObject`, `Specification`, `AppDbContextBase`, `StronglyTypedId`, `Result`)
- `specs/001-game-configuration/plan.md`, `specs/002-categories/research.md`, `specs/003-question-bank/research.md` (patrón Enumeration/ValueObject, rowversion, Specification, IQuestionCounter/IQuestionSelectionStrategy)
- `src/OroQuizClash.Domain/Games/Game.cs` + `GameStatus.cs` (existentes, 9 estados ya definidos)

