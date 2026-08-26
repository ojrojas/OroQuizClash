# Feature Specification: Game Configuration

**Feature Branch**: `001-game-configuration`

**Created**: 2026-08-26

**Status**: Ready for Review

**Input**: User description: "Game Configuration Objetivo Definir cómo se configura un juego antes de iniciar una partida. Alcance Debe permitir configurar: Nombre del juego. Categoría. Número mínimo de rondas. Número máximo de rondas. Dificultad inicial. Estrategia de incremento de dificultad. Tiempo máximo por pregunta. Sistema de puntuación. Política de pérdida. Política de retiro. Política de consolación. Reglas de premios. Cantidad máxima de jugadores. Cantidad mínima de jugadores. Reglas principales CFG-001 Un juego debe tener una configuración válida antes de iniciar. CFG-002 El mínimo de rondas debe ser 5. CFG-003 La configuración no puede modificarse una vez iniciado el juego. CFG-004 Debe existir una categoría válida. CFG-005 La configuración debe definir una estrategia de dificultad. CFG-006 Debe existir un límite de tiempo válido. CFG-007 Las políticas de pérdida y retiro deben estar definidas. Dependencias SPEC-002 SPEC-003 BuildingBlocks AggregateRoot ValueObject StronglyTypedId IBusinessRule Result IRepository CQRS Specifications Resultado esperado Un agregado Game que pueda crearse únicamente cuando la configuración sea válida."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Crear juego con configuración válida (Priority: P1)

Como administrador / creador de partida (GAME_MANAGER/ADMIN), quiero crear un juego definiendo nombre, categoría, rondas, dificultad, tiempo, puntuación y políticas, para que el juego quede listo para iniciar solo si la configuración es válida.

**Why this priority**: Es el flujo base sin el cual no existe partida; cubre CFG-001, CFG-002, CFG-004, CFG-005, CFG-006, CFG-007 y el resultado esperado del agregado Game.

**Independent Test**: Se puede probar creando un juego vía API con payload válido y verificando que el juego queda en estado DRAFT/READY y que un intento con configuración incompleta es rechazado con error de validación.

**Acceptance Scenarios**:

1. **Given** categoría válida existente, **When** el administrador envía `CreateGame` con nombre, categoría, minRondas=5, maxRondas=10, dificultad inicial, estrategia de incremento, tiempo por pregunta=30s, sistema de puntuación, políticas de pérdida/retiro/consolación, reglas de premios, minJugadores=2, maxJugadores=10, **Then** el sistema crea el agregado `Game` con configuración persistida, estado inicial DRAFT/READY y retorna el ID del juego.
2. **Given** intento de crear juego sin nombre o sin categoría, **When** se envía la solicitud, **Then** el sistema rechaza con error `InvalidGameConfiguration` / `CategoryNotReady` y no crea el juego.
3. **Given** intento con minRondas=3, **When** se envía, **Then** el sistema rechaza por violación de CFG-002 (mínimo 5).
4. **Given** intento sin estrategia de dificultad, **When** se envía, **Then** el sistema rechaza por CFG-005.
5. **Given** intento sin límite de tiempo o con valor no positivo, **When** se envía, **Then** el sistema rechaza por CFG-006.
6. **Given** intento sin política de pérdida o sin política de retiro, **When** se envía, **Then** el sistema rechaza por CFG-007.

---

### User Story 2 — Inmutabilidad de configuración tras iniciar (Priority: P1)

Como sistema, debo impedir que la configuración se modifique una vez iniciado el juego, para preservar integridad y equidad.

**Why this priority**: Garantiza CFG-003 y protege invariantes de dominio y concurrencia; sin esto el motor de juego sería no determinista.

**Independent Test**: Crear juego válido, iniciarlo (`StartGame`), luego intentar modificar cualquier campo de configuración y verificar rechazo.

**Acceptance Scenarios**:

1. **Given** juego en estado DRAFT/READY con configuración válida, **When** se inicia el juego (transición a WAITING_FOR_PLAYERS/IN_PROGRESS), **Then** el estado cambia y la configuración queda bloqueada.
2. **Given** juego ya iniciado, **When** se intenta actualizar nombre, rondas, dificultad, tiempo o cualquier política, **Then** el sistema rechaza con `InvalidGameState` / `ConfigurationImmutable` y no muta el agregado.

---

### User Story 3 — Validar dependencias de categoría y configuración de jugadores (Priority: P2)

Como administrador, quiero que el sistema valide que la categoría existe y está lista, y que los límites de jugadores y rondas sean coherentes, para evitar partidas imposibles de jugar.

**Why this priority**: Cubre CFG-004 y coherencia de límites (min/max rondas y min/max jugadores); depende implícitamente de SPEC-002 (categorías) y SPEC-003 (banco de preguntas).

**Independent Test**: Intentar crear juego con categoría inexistente/no publicada o con rangos incoherentes y verificar rechazo con mensajes específicos.

**Acceptance Scenarios**:

1. **Given** categoría inexistente o no publicada (sin ≥5 preguntas válidas según invariantes), **When** se crea el juego, **Then** el sistema rechaza con `CategoryNotReady` / `CategoryNotFound`.
2. **Given** minRondas > maxRondas, **When** se envía configuración, **Then** el sistema rechaza por rango inválido.
3. **Given** minJugadores > maxJugadores o minJugadores < 1, **When** se envía, **Then** el sistema rechaza por límites de jugadores inválidos.
4. **Given** reglas de premios ausentes o sistema de puntuación no definido, **When** se envía, **Then** el sistema rechaza por configuración incompleta (extiende CFG-001).

---

### Edge Cases

- ¿Qué sucede cuando `minRondas = 5` exactamente? MUST ser aceptado (límite inclusivo); `4` MUST ser rechazado.
- ¿Qué sucede cuando `maxRondas` es omitido o igual a `minRondas`? Si el modelo permite juego de longitud fija, `maxRondas == minRondas` es válido; si `maxRondas` es opcional, debe default a `minRondas` o ser validado como [NEEDS CLARIFICATION] según diseño final.
- ¿Qué sucede cuando se envían jugadores `min=0` o negativos? Rechazo por validación de dominio.
- ¿Qué sucede cuando dos solicitudes concurrentes intentan crear/iniciar el mismo juego con configuraciones distintas? Solo una transición es válida; la segunda debe fallar por control de concurrencia optimista (`rowversion`).
- ¿Qué sucede cuando el tiempo por pregunta es `0`, negativo o excede un máximo razonable (p. ej. >300s)? Rechazo por CFG-006; límite superior a definir por política.
- ¿Qué sucede cuando la categoría es válida pero posteriormente se despublica? La validación es al momento de creación; juegos ya creados no se invalidan retroactivamente, pero nuevos juegos deben rechazar la categoría.
- ¿Cómo maneja el sistema políticas no reconocidas (p. ej. `LossPolicy = "UNKNOWN"`)? Rechazo por validación de enumeración.
- ¿Qué sucede cuando el nombre del juego duplica otro existente? Permitido salvo que se defina unicidad; no se asume unicidad global salvo especificación futura.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema MUST permitir crear un agregado `Game` únicamente cuando la configuración completa sea válida (CFG-001).
- **FR-002**: El sistema MUST exigir que el número mínimo de rondas sea ≥ 5; valores inferiores MUST ser rechazados (CFG-002).
- **FR-003**: El sistema MUST impedir cualquier modificación de la configuración una vez que el juego ha transitado fuera de DRAFT/READY hacia un estado iniciado (CFG-003); la configuración es inmutable tras `StartGame`.
- **FR-004**: El sistema MUST validar que la categoría referenciada existe y está en estado válida/publicada al momento de crear el juego (CFG-004).
- **FR-005**: El sistema MUST exigir una estrategia de incremento de dificultad definida y válida (CFG-005) — p. ej. `Linear`, `Progressive`, `Adaptive`, `CategorySpecific` u otras registradas; valor nulo/desconocido MUST ser rechazado.
- **FR-006**: El sistema MUST exigir un límite de tiempo por pregunta válido y positivo (CFG-006) — expresado en segundos; cero o negativo MUST ser rechazado.
- **FR-007**: El sistema MUST exigir que las políticas de pérdida (`LossPolicy`) y de retiro (`WithdrawalPolicy`) estén definidas y sean valores válidos del dominio (CFG-007).
- **FR-008**: El sistema MUST permitir configurar nombre del juego (no vacío), categoría, minRondas, maxRondas, dificultad inicial, estrategia, tiempo por pregunta, sistema de puntuación, políticas de pérdida/retiro/consolación, reglas de premios, minJugadores y maxJugadores como parte de la configuración.
- **FR-009**: El sistema MUST validar coherencia de rangos: `minRondas ≤ maxRondas` y `minJugadores ≤ maxJugadores`; `minJugadores ≥ 1`; violaciones MUST ser rechazadas.
- **FR-010**: El sistema MUST validar que la dificultad inicial pertenece al conjunto configurado de dificultades y es compatible con la estrategia seleccionada.
- **FR-011**: El sistema MUST modelar la configuración como ValueObject(s) inmutables dentro del agregado `Game` (DRAFT) y exponer comportamiento explícito `Game.Create(configuration)` que aplica `IBusinessRule` y retorna `Result<Game>` sin exponer setters mutables.
- **FR-012**: El sistema MUST exponer la creación vía CQRS como `CreateGameCommand` + `CreateGameHandler` + `IRepository<Game, GameId>` + `IUnitOfWork`, con validación en pipeline (`ValidationBehavior`) y reglas de dominio (`IBusinessRule`), retornando `Result` con `Error` tipificados (`InvalidGameConfiguration`, `CategoryNotReady`, `InvalidGameState`, etc.) mapeados a ProblemDetails.
- **FR-013**: El sistema MUST persistir la configuración de forma consultable vía `Specification<Game>` y proteger la transición de inicio con concurrencia optimista (`rowversion`).

### Key Entities *(include if feature involves data)*

- **Game (AggregateRoot<GameId>)**: Agregado raíz que encapsula ciclo de vida y configuración. Atributos: `GameId (StronglyTypedId<Guid>)`, `Name`, `GameConfiguration`, `GameStatus` (`DRAFT`, `READY`, `WAITING_FOR_PLAYERS`, `IN_PROGRESS`, etc.), `RowVersion`. Comportamiento: `Game.Create(configuration)` valida reglas y crea instancia; `Game.Start()` verifica CFG-001 y bloquea configuración. Invariantes: no existe Game sin configuración válida; configuración inmutable tras iniciar.
- **GameConfiguration (ValueObject)**: Objeto de valor inmutable que agrupa toda la configuración previa a iniciar. Atributos: `CategoryId`, `MinRounds`, `MaxRounds`, `InitialDifficulty`, `DifficultyProgressionStrategy`, `TimeLimitPerQuestion`, `ScoringSystem`, `LossPolicy` (`LOSE_ALL`, `LOSE_CURRENT_ROUND`, `LOSE_UNSECURED_POINTS`, `FALLBACK_TO_CHECKPOINT`), `WithdrawalPolicy` (`LOSE_ALL`, `KEEP_CURRENT_SCORE`, `KEEP_SECURED_SCORE`, `KEEP_CHECKPOINT_SCORE`), `ConsolationPolicy`, `RewardRules`, `MinPlayers`, `MaxPlayers`, `Name`. Sin identidad propia; igualdad por valor.
- **Category (referencia externa, SPEC-002)**: Entidad gestionada en otro bounded context; Game solo guarda `CategoryId` y valida existencia/estado vía repositorio/specification. Invariante: categoría debe estar publicada y contener ≥5 preguntas válidas.
- **DifficultyStrategy (Enumeration/Strategy)**: Estrategia de incremento (`Linear`, `Progressive`, `Adaptive`, `CategorySpecific`). Define cómo evoluciona la dificultad por ronda.
- **Policies (ValueObjects/Enumerations)**: `LossPolicy`, `WithdrawalPolicy`, `ConsolationPolicy`, `ScoringSystem`, `RewardRules` — cada una con valores válidos del dominio y comportamiento asociado.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% de intentos de crear un juego con configuración incompleta o inválida son rechazados con error tipificado y sin persistir ningún agregado (verificable por pruebas de aceptación para cada CFG-001 a CFG-007).
- **SC-002**: Un juego con configuración válida (minRondas=5, categoría válida, estrategia y tiempo definidos, políticas presentes) se crea exitosamente en menos de 2 segundos desde la solicitud hasta la confirmación persistida, en el 95% de los casos en entorno de prueba.
- **SC-003**: 0% de juegos iniciados permiten mutación posterior de su configuración — todo intento de actualización tras `StartGame` es rechazado (auditable por pruebas de inmutabilidad).
- **SC-004**: El 100% de juegos creados con `minRondas < 5` o `minRondas > maxRondas` o `minJugadores > maxJugadores` son rechazados en validación, sin efectos colaterales.
- **SC-005**: Administradores completan la creación de un juego válido en el primer intento en al menos 90% de los casos cuando reciben mensajes de error claros para cada regla violada (medido por pruebas de usabilidad del flujo de configuración).
- **SC-006**: La reconstrucción de la configuración desde persistencia (via `IRepository` + `Specification`) devuelve valores idénticos a los enviados en el 100% de los casos para juegos creados correctamente.

## Assumptions

- Se asume que `SPEC-002` (categorías) y `SPEC-003` (preguntas) existen o se simulan vía stub para validar CFG-004; si no, la validación de categoría se hará contra un repositorio mock en esta fase.
- Se asume que la identidad y autorización ya están resueltas por OroIdentityServer (imagen Podman `oroidentityserver:latest` según constitución v1.1.0); la creación de juego requiere rol `ADMIN` o `GAME_MANAGER` autenticado vía JWT OIDC.
- Se asume que `BuildingBlocks` (`AggregateRoot`, `ValueObject`, `StronglyTypedId`, `IBusinessRule`, `Result/Error`, `IRepository`, `IUnitOfWork`, CQRS `ICommand`/`ISender`, `Specifications`) están disponibles como dependencia técnica y no se reinventan.
- Se asume que el tiempo por pregunta se expresa en segundos enteros positivos; el rango operativo razonable es 5–300s salvo que el negocio defina otro límite; valores fuera de rango son rechazados por CFG-006.
- Se asume que `maxRondas` es requerido y `maxRondas ≥ minRondas`; si el negocio decide hacerlo opcional, se documentará como variante sin romper CFG-002.
- Se asume que las políticas (`LossPolicy`, `WithdrawalPolicy`, `ConsolationPolicy`) y `ScoringSystem`/`RewardRules` son enumeraciones/VOs extensibles pero cerradas por validación; valores desconocidos son rechazados.
- Se asume que el nombre del juego no requiere unicidad global en esta versión; solo se valida no vacío y longitud razonable (p. ej. 3–100 caracteres) vía reglas de aplicación.
- Se asume que la inmutabilidad de configuración (CFG-003) se aplica estrictamente tras la transición de inicio; antes de iniciar, correcciones vía caso de uso explícito podrían permitirse pero no se especifican aquí y requerirían nueva spec si se desea.

## Dependencies

- `SPEC-002` — Gestión de categorías (para CFG-004).
- `SPEC-003` — Banco de preguntas (para validar categoría publicable ≥5 preguntas).
- `BuildingBlocks.Kernel.Domain` — `AggregateRoot`, `ValueObject`, `StronglyTypedId`, `IBusinessRule`, `Result`.
- `BuildingBlocks.CQRS` — `ICommand`, `ICommandHandler`, `ISender`, `IPipelineBehavior`.
- `BuildingBlocks.Kernel.Infrastructure` — `IRepository`, `IUnitOfWork`, `AppDbContextBase`, `Specification<T>`.

## Out of Scope

- Modificación de configuración tras iniciar (prohibida por CFG-003) — cualquier flujo de edición pre-inicio se tratará en spec futura si se requiere.
- Lógica de selección de preguntas, evaluación de respuestas, cálculo de puntaje en rondas y entrega de premios — pertenecen a specs de motor de juego posteriores.
- UI específica (Angular/Web) más allá del contrato API necesario para crear el juego.

## References

- Constitución v1.1.0 — Principios I-VI, Additional Constraints A/C/E/F/H.
- `draft/constitution.md` §8 (Game Configuration), §5 (State Machine), §7 (Difficulty), §13 (Incorrect Answers), §12 (Withdrawal).
- `draft/oroidentityserver-specification.md` — autenticación para `CreateGame`.
