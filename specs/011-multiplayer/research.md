# Research: Multiplayer (SPEC-011)

**Date**: 2026-08-27 | **Plan**: [plan.md](plan.md) | **Spec**: [spec.md](spec.md)

Phase 0 — resolución de decisiones técnicas. No quedaron NEEDS CLARIFICATION en el Technical Context; las decisiones siguientes resuelven los puntos de diseño identificados tras inspeccionar el código existente (`Game`, `GamePlayer`, `Answer`, `PointTransaction`, slices de `Features/Games/`, configuraciones EF, tests).

## R1 — `CurrentRound` por jugador: campo explícito vs derivación

- **Decision**: Añadir `CurrentRoundNumber` (int, 0 = sin ronda iniciada) como campo persistido en `GamePlayer`, mutado exclusivamente por comportamiento del agregado: `Game.StartRound()` lo avanza al `RoundNumber` nuevo para todos los jugadores `Active`; `MarkWithdrawn()`/`MarkEliminated()` lo congelan (deja de avanzar en rondas posteriores).
- **Rationale**: FR-001 exige `CurrentRound` como atributo del jugador y FR-010 exige que se congele en la última ronda alcanzada al retirarse/eliminarse. La congelación por salida no es derivable de las rondas del juego (que siguen avanzando para el resto), por lo que se requiere estado explícito por jugador. Además alimenta `CurrentLevel` del leaderboard (dificultad de la ronda `CurrentRoundNumber`).
- **Alternatives considered**:
  - Derivar de `Answers` del jugador (última ronda respondida): rechazado — un jugador puede no responder nunca y aun así estar en la ronda en curso; la derivación no distingue "en ronda sin responder" de "fuera del juego".
  - Derivar de `Rounds.Count` del juego: rechazado — no contempla la congelación por retiro/eliminación.

## R2 — `AnswerState` por jugador: derivación vs campo desnormalizado

- **Decision**: `AnswerState` se deriva de las entidades `Answer` existentes: `Game.GetPlayerAnswerState(playerId)` retorna `AnswerStatus.NotAnswered` si no existe `Answer` del jugador para la ronda actual, o el `Status` del `Answer` existente (`Answered`/`Evaluated`/`Expired`). Sin campo nuevo en `GamePlayer`.
- **Rationale**: Ya existe exactamente un `Answer` por `(GameId, PlayerId, RoundId)` (regla de dominio + índice único DB). Desnormalizar el estado en `GamePlayer` crearía un invariante de sincronización duplicado sin beneficio; la derivación preserva la fuente única de verdad y satisface FR-009 (los estados son los de SPEC-006: `NOT_ANSWERED → ANSWERED → EVALUATED / EXPIRED`).
- **Alternatives considered**:
  - Campo `AnswerState` en `GamePlayer` actualizado en `SubmitAnswer`: rechazado — duplicación de estado y riesgo de divergencia; `Answer.Status` ya es el estado autoritativo con `AnswerImmutabilityRule`.

## R3 — Token de concurrencia por jugador: `Game.RowVersion` vs `GamePlayer.RowVersion`

- **Decision**: El token de concurrencia optimista del estado de jugador es el `RowVersion` del agregado `Game` (existente). No se añade `RowVersion` a `GamePlayer`.
- **Rationale**: `GamePlayer` es una entidad dentro del agregado `Game`; toda mutación de estado de jugador ocurre vía comportamiento del agregado (`SubmitAnswer`, `WithdrawPlayer`, `EliminatePlayer`, `StartRound`) y se persiste en un único `SaveChanges` sobre el agregado. El agregado es el límite de consistencia (Constitución II/IV): dos escrituras concurrentes sobre el mismo juego producen `DbUpdateConcurrencyException` → `ConcurrencyConflict` (409) en la perdedora, que es exactamente la semántica de FR-006. `Answer` ya tiene su propio `RowVersion` para inmutabilidad.
- **Alternatives considered**:
  - `RowVersion` por `GamePlayer`: rechazado — redundante dentro del límite del agregado; produciría conflictos dobles y semántica confusa (¿qué token gana?) sin mejorar la protección.
  - Bloqueo pesimista: rechazado — la Constitución F prefiere concurrencia optimista.
- **Desviación de implementación (T033)**: La validación E2E demostró que usar solo `Game.RowVersion` (o "tocar" el raíz para mutaciones de hijos) devuelve 409 a envíos simultáneos de jugadores distintos, violando SC-001: el agregado se carga y guarda completo, así que dos `SaveChanges` concurrentes sobre el mismo juego siempre colisionan aunque toquen filas distintas de `GamePlayer`. Implementación final: `GamePlayer.RowVersion` como token por fila de jugador (los envíos simultáneos de jugadores distintos escriben filas disjuntas y no colisionan; dos escrituras sobre el mismo jugador sí colisionan → 409). `Game.RowVersion` y `Answer.RowVersion` se mantienen para sus casos respectivos.

## R4 — Aislamiento entre jugadores: aplicación de identidad JWT

- **Decision**: El `PlayerId` de comandos de jugador se resuelve desde el claim `sub` del JWT en el endpoint (patrón ya usado en `JoinGameEndpoint`). `SubmitAnswerHandler` elimina el placeholder `playerId = Guid.Empty` y usa el `PlayerId` autenticado del comando. `WithdrawPlayerEndpoint` valida `sub == PlayerId` salvo rol organizador (`ADMIN`/`GAME_MANAGER`). Si un jugador intenta actuar sobre otro, se retorna el nuevo error `GameErrors.PlayerIdentityMismatch` (`Error.Forbidden` → 403) y el intento se registra en el log estructurado.
- **Rationale**: FR-003 exige que cada jugador solo afecte su propio estado y que la identidad autenticada corresponda al jugador afectado. El estado actual (`Guid.Empty` en `SubmitAnswer`) es un bug de aislamiento que este SPEC corrige. La excepción para organizadores preserva operaciones administrativas existentes (p. ej. retiro asistido).
- **Alternatives considered**:
  - Validar identidad solo en el dominio: rechazado — el dominio no conoce claims; la validación de identidad es responsabilidad de Application/endpoint, el dominio valida la pertenencia del jugador al juego (`ValidatePlayerRule`, ya existe).
  - Rechazar siempre el `PlayerId` del cuerpo sin distinción de rol: rechazado — rompería el flujo administrativo existente de `WithdrawPlayer`/`AdjustScore`.

## R5 — Idempotencia de envíos: frontera `(GameId, PlayerId, RoundId)`

- **Decision**: La frontera de idempotencia del envío de respuestas es `(GameId, PlayerId, RoundId)`, ya implementada en el dominio (`ValidateIdempotencyRule`: segundo envío retorna el `Answer` existente como éxito) y reforzada en DB (índice único `(GameId, PlayerId, RoundId)`). El campo opcional `IdempotencyKey` del comando se acepta para correlación del cliente pero no añade una frontera de deduplicación adicional.
- **Rationale**: FR-007 exige que duplicados del mismo jugador para la misma ronda retornen el resultado original sin duplicar respuesta ni puntos; la unicidad por jugador+ronda ya lo garantiza en todos los casos (el cliente no puede enviar dos respuestas distintas a la misma ronda). Una clave arbitraria persistida no aporta protección extra para respuestas y añadiría una columna sin uso real.
- **Alternatives considered**:
  - Persistir `IdempotencyKey` en `Answer` con índice único filtrado (patrón `RewardRedemption`): rechazado — necesario allí porque una redención puede reintentarse con la misma clave en distintos contextos; para respuestas, jugador+ronda ya es la clave natural.

## R6 — Leaderboard: fuentes de datos y desempate determinista

- **Decision**: `GetLeaderboard` se calcula desde el agregado `Game` (ya cargado con `GameByIdWithAnswersSpecification`: Players + Rounds + Answers + PointTransactions):
  - `Points` = `PlayerScore.CurrentPoints` (consistente con ledger por construcción — SPEC-007).
  - `CorrectAnswers` = conteo de `Answer` del jugador con `Correct == true`.
  - `CurrentLevel` = `Difficulty` de la ronda cuyo `RoundNumber == player.CurrentRoundNumber` (null/0 si aún no inició ronda; para retirados/eliminados, la dificultad de su ronda congelada — ver R1).
  - `Status` = `ParticipationStatus.Name`.
  - Orden determinista: `Points` desc → `CorrectAnswers` desc → consecución más temprana del saldo actual (menor `CreatedAt` de la transacción del ledger que estableció el `CurrentPoints` actual) → `JoinedAt` asc como estabilidad final. `Rank` = posición 1-based en ese orden.
- **Rationale**: FR-011 exige exactamente esos seis datos y un orden determinista con desempates; el ledger ya registra `ResultingBalance` + `CreatedAt`, lo que permite calcular la consecución más temprana sin persistir datos nuevos.
- **Alternatives considered**:
  - Desempate solo por `JoinedAt`: rechazado — premia el orden de unión, no el mérito; la consecución más temprana es el criterio competitivo estándar.
  - Tabla/vista materializada de leaderboard: rechazado — con ≤10 jugadores por juego el cálculo in-memory desde el agregado es suficiente y evita consistencia eventual.

## R7 — Notificaciones server-driven: SignalR con port en Application

- **Decision**: Añadir SignalR (incluido en el shared framework de ASP.NET Core, sin paquete nuevo): `GameHub` broadcast-only en `/hubs/game` (Api, `RequireAuthorization`, grupos `game-{gameId}`); port `IGameNotificationsBroadcaster` definido en Application; implementación `SignalRGameNotificationsBroadcaster` en Api usando `IHubContext<GameHub>`; handlers de dominio en Application (`IDomainEventHandler<>`, auto-registrados por `AddCqrs`) publican: `PlayerJoined` → jugador unido; `ScoreUpdated`/`AnswerEvaluated` → actualización de puntaje + leaderboard; `RoundCompleted` → leaderboard; `PlayerWithdrawn`/`PlayerEliminated`/`GameFinished` → cambio de estado de jugador. El hub no acepta comandos de juego (solo unirse a grupos); las mutaciones siguen siendo REST.
- **Rationale**: FR-014 exige notificaciones server-driven y la Constitución V permite SignalR explícitamente para `ScoreUpdated`/`LeaderboardUpdated` etc., siempre que no sea fuente de verdad. Los domain events necesarios ya existen (no se crean eventos nuevos). El port en Application preserva la inversión de dependencias (Application no referencia Api).
- **Alternatives considered**:
  - Solo polling (consultas REST): rechazado — incumple FR-014.
  - Despacho post-commit modificando `AppDbContextBase`: rechazado — implicaría modificar BuildingBlocks (plataforma compartida); los handlers se ejecutan pre-commit dentro de la transacción, y al ser hints de UI best-effort (el estado autoritativo es el persistido y consultable) el riesgo de notificación sin commit es aceptable y se documenta. Los eventos de integración externos sí usan Outbox post-commit (no afectados).
  - Hub con comandos (enviar respuestas vía SignalR): rechazado — duplicaría la superficie de validación; REST sigue siendo el canal autoritativo.

## R8 — Frescura del leaderboard durante una ronda

- **Decision**: El leaderboard muestra únicamente datos evaluados: los puntos provienen del ledger, que solo registra transacciones cuando `Answer.Status == EVALUATED` (SPEC-006/007 ya lo implementan — los puntos se otorgan al evaluar cada respuesta). Por tanto, durante una ronda en curso el leaderboard refleja las respuestas ya evaluadas de cada jugador y nunca datos parciales (recibidos sin evaluar) ni corruptos.
- **Rationale**: El escenario US4-S4 del spec ("snapshot consistente, nunca ranking parcial o corrupto") se satisface con la regla "solo datos evaluados": la evaluación es por respuesta y atómica (respuesta + transacción en la misma operación), así que ningún jugador aparece con puntos de una respuesta no evaluada.
- **Alternatives considered**:
  - Retener todos los puntos de la ronda hasta `CompleteRound`: rechazado — contradice SPEC-006 (`CalculateResult` crea la transacción al evaluar) y SPEC-007, y degradaría la retroalimentación en vivo que el leaderboard promete.

## R9 — Manejo de conflictos de concurrencia en handlers

- **Decision**: Todos los handlers de mutación de estado de jugador envuelven `SaveChangesAsync` en `try/catch (DbUpdateConcurrencyException)` → `Result.Failure(GameErrors.ConcurrencyConflict)` (409): se añade en `SubmitAnswerHandler`, `WithdrawPlayerHandler` y `AdjustScoreHandler` (los demás comandos del ciclo de vida ya lo tienen). El cliente recupera el estado autoritativo reconsultando (`GetPlayerState`/`GetLeaderboard`) y reintenta.
- **Rationale**: FR-006/SC-006 exigen que el perdedor de un conflicto reciba un error recuperable explícito; sin el catch, la excepción escalaría al `GlobalExceptionHandler` como 500.
- **Alternatives considered**:
  - Retry automático dentro del handler: rechazado — reintentar re-evaluaría la respuesta (el dominio ya la trata idempotentemente, pero el retry automático enmascara conflictos y complica la semántica de tiempo límite); el retry explícito del cliente es la pauta establecida en los slices existentes.

## R10 — Estrategia de testing de concurrencia

- **Decision**: Los tests de concurrencia e idempotencia (SC-001/SC-002/SC-006) se implementan en `OroQuizClash.Infrastructure.Tests` con EF Core Sqlite (provider relacional real, ya referenciado): (a) dos contextos cargan el mismo juego y envían respuestas de jugadores distintos → ambos guardan en secuencia y el segundo salva sin conflicto (agregados distintos no compiten; se verifica que ambos `Answer` + transacciones existen); (b) dos contextos mutan el mismo estado del mismo jugador → el segundo `SaveChanges` lanza `DbUpdateConcurrencyException` (rowversion stale); (c) envío duplicado mismo jugador+ronda → el dominio retorna el `Answer` existente y el índice único DB impide duplicados. Los tests de dominio cubren la lógica de participación (avance/congelación de `CurrentRoundNumber`, derivación de `AnswerState`, aislamiento) sin infraestructura.
- **Rationale**: La Constitución exige tests de concurrencia/idempotencia como integration tests; Sqlite soporta tokens de concurrencia y restricciones únicas, a diferencia del provider InMemory. El stub actual `GameConcurrencyTests` se reemplaza.
- **Alternatives considered**:
  - Testcontainers con SQL Server real: rechazado para v1 — no existe en el repo y Sqlite cubre la semántica necesaria; se puede añadir después sin rediseño.
  - Solo tests de dominio: rechazado — el rowversion y los índices únicos son comportamiento de persistencia que el dominio no ejercita.
