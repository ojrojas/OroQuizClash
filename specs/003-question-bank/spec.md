# Feature Specification: Question Bank

**Feature Branch**: `003-question-bank`

**Created**: 2026-08-26

**Status**: Draft

**Input**: User description: "SPEC-003 — Question Bank Objetivo Gestionar el banco de preguntas y sus cuatro alternativas. Modelo conceptual Question ├── Category ├── Difficulty ├── AcademicLevel ├── AgeRange └── AnswerOptions ├── A ├── B ├── C └── D Reglas QST-001 Toda pregunta debe tener exactamente 4 respuestas. QST-002 Debe existir exactamente 1 respuesta correcta. QST-003 Una pregunta debe pertenecer a una categoría. QST-004 Una pregunta debe tener dificultad. QST-005 Una pregunta publicada no puede quedar sin respuesta correcta. QST-006 Una pregunta debe ser validada antes de estar disponible para el juego. Funcionalidades CreateQuestion UpdateQuestion ActivateQuestion DeactivateQuestion PublishQuestion ArchiveQuestion Selección El SPEC debe establecer que el sistema puede seleccionar preguntas considerando: Category Difficulty AcademicLevel AgeRange PreviousQuestions Game Round La estrategia concreta se definirá en el plan técnico. Dependencias SPEC-002"

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Crear pregunta con 4 alternativas y validación de invariantes (Priority: P1)

Como administrador de contenido (ADMIN / GAME_MANAGER), quiero crear una pregunta asignándola a una categoría y dificultad, definiendo exactamente 4 alternativas con una sola correcta, y especificando AcademicLevel y AgeRange, para alimentar el banco de preguntas jugable.

**Why this priority**: Es el núcleo de QST-001 a QST-004; sin creación válida no existe banco ni publicación de categorías (SPEC-002 exige ≥5 válidas) ni partida (SPEC-001). Entrega valor independiente como CRUD de banco.

**Independent Test**: Enviar `CreateQuestion` con payload válido (categoría existente, dificultad, AcademicLevel, AgeRange, 4 AnswerOptions con 1 IsCorrect=true) → `201` con `QuestionId`, `Status=DRAFT` y persistencia verificable vía `GetQuestionById`. Enviar con 3 opciones, 0/2 correctas, sin categoría o sin dificultad → `400` con `Error` tipificado. Crear 5 preguntas válidas y verificar que `Category.validQuestionsCount` incrementa.

**Acceptance Scenarios**:

1. **Given** categoría existente `ACTIVE` (SPEC-002), dificultad `Intermediate`, `AcademicLevel=Secundaria`, `AgeRange=13-17`, y 4 alternativas `A..D` con solo `B` correcta, **When** se envía `CreateQuestion`, **Then** el sistema crea `Question` en `DRAFT`, con `QuestionId:StronglyTypedId`, `4 AnswerOptions` persistidas y `IsCorrect` exactamente en 1.
2. **Given** intento con 3 alternativas, **When** se envía, **Then** el sistema rechaza con `InvalidQuestionConfiguration` / `QuestionMustHaveFourOptions` (QST-001) y no persiste.
3. **Given** intento con 4 alternativas pero 0 correctas o 2 correctas, **When** se envía, **Then** se rechaza con `QuestionMustHaveOneCorrectAnswer` (QST-002).
4. **Given** intento sin `CategoryId` o con `CategoryId` inexistente/archivada, **When** se envía, **Then** se rechaza con `CategoryNotFound` / `QuestionMustBelongToCategory` (QST-003).
5. **Given** intento sin `Difficulty` (nulo o fuera de 1..5), **When** se envía, **Then** se rechaza con `QuestionMustHaveDifficulty` (QST-004).
6. **Given** payload con `AcademicLevel` y `AgeRange` alineados a la categoría, **When** se crea, **Then** se persiste y es consultable; si desalineados (ej. categoría 13-17 pero pregunta 20-25), **When** se crea, **Then** se rechaza o no cuenta como válida según regla de alineación (ver FR-007).

---

### User Story 2 — Ciclo de vida y publicación validada de preguntas (Priority: P1)

Como administrador, quiero activar, desactivar, publicar y archivar preguntas, pero el sistema debe impedir que una pregunta publicada quede sin respuesta correcta y debe exigir validación previa antes de estar disponible para el juego, para garantizar integridad y jugabilidad.

**Why this priority**: Cubre QST-005 y QST-006 — reglas no negociables de disponibilidad para el juego y de categoría publicable. Sin ellas se rompería la invariante de categoría (≥5 válidas) y el motor seleccionaría preguntas inválidas.

**Independent Test**: Crear pregunta en `DRAFT` con 4/1 válida → `PublishQuestion` → si pasa validación transita a `PUBLISHED`/`ACTIVE` y emite `QuestionPublishedDomainEvent`; intentar `PublishQuestion` con pregunta sin correcta o con 3 opciones → `400` con `QuestionNotPublishable`. Crear, publicar, luego intentar editar para quitar la correcta → rechazado por QST-005. Consultar `IQuestionSelectionStrategy` / `GetAvailableQuestions` y verificar que solo preguntas `PUBLISHED`+`ACTIVE` validadas aparecen.

**Acceptance Scenarios**:

1. **Given** pregunta en `DRAFT` válida (4 opciones, 1 correcta, categoría y dificultad presentes), **When** se invoca `PublishQuestion`, **Then** transita a `PUBLISHED` (o `ACTIVE` según nomenclatura) y queda disponible para selección en juegos.
2. **Given** pregunta en `DRAFT` inválida (0 correctas, 3 opciones), **When** se intenta `PublishQuestion`, **Then** se rechaza con `QuestionNotPublishable` / `QuestionNotValidated` y permanece en `DRAFT` (QST-006).
3. **Given** pregunta `PUBLISHED` con 1 correcta, **When** se intenta `UpdateQuestion` para dejar 0 o 2 correctas o quitar la correcta, **Then** se rechaza con `PublishedQuestionMustHaveCorrectAnswer` (QST-005) y no se persiste el cambio.
4. **Given** pregunta `PUBLISHED`, **When** se invoca `DeactivateQuestion`, **Then** transita a `INACTIVE` y deja de estar disponible para selección y deja de contar como válida para `Category.Publish`.
5. **Given** pregunta `PUBLISHED` o `INACTIVE`, **When** se invoca `ArchiveQuestion`, **Then** transita a `ARCHIVED` (terminal) y no permite `Publish`/`Update` sin reactivación explícita; histórico permanece auditable.
6. **Given** pregunta `DRAFT` → `ActivateQuestion`, **When** se invoca, **Then** transita a `ACTIVE` pero NO implica `PUBLISHED`; solo `PUBLISHED+ACTIVE` es seleccionable para juego (validación previa superada).
7. **Given** dos admins intentan `PublishQuestion` concurrentemente sobre la misma pregunta, **When** ambos envían, **Then** uno gana y el segundo recibe `409 Conflict` por `rowversion` / estado ya publicado.

---

### User Story 3 — Actualizar pregunta en estados permitidos (Priority: P2)

Como administrador, quiero actualizar texto de pregunta, alternativas, categoría, dificultad, AcademicLevel y AgeRange mientras la pregunta no esté publicada/archivada de forma que comprometa invariantes, para corregir errores y mantener calidad.

**Why this priority**: Permite curaduría sin recrear; es P2 porque depende de US1 pero es esencial para operativa diaria. Debe respetar QST-001..005 en cada actualización.

**Independent Test**: Crear pregunta `DRAFT` → `UpdateQuestion` cambiando texto y alternativa `C` → `200` y verifica persistencia; intentar actualizar pregunta `PUBLISHED` para dejar 3 opciones → `400`; intentar actualizar `ARCHIVED` → `404`/`InvalidQuestionState`.

**Acceptance Scenarios**:

1. **Given** pregunta en `DRAFT` o `INACTIVE`, **When** se envía `UpdateQuestion` con payload válido (4 opciones, 1 correcta, categoría/dificultad válidas), **Then** la pregunta se actualiza, `RowVersion` incrementa y `UpdatedAt` se registra.
2. **Given** pregunta en `PUBLISHED`, **When** se intenta `UpdateQuestion` que mantiene 4/1 y alineación, **Then** se permite (o requiere despublicar primero según política — ver Assumptions) — el plan técnico decidirá; si la política es estricta, se rechaza con `PublishedQuestionImmutable` y se exige `Deactivate` previo.
3. **Given** intento de `UpdateQuestion` que viola QST-001..004 (ej. 3 opciones, sin categoría), **When** se envía, **Then** se rechaza con error de dominio sin mutar el agregado.
4. **Given** pregunta `ARCHIVED`, **When** se intenta `UpdateQuestion`, **Then** se rechaza con `InvalidQuestionState`.

---

### User Story 4 — Selección de preguntas para Game/Round considerando múltiples criterios (Priority: P2)

Como motor de juego (Game Engine) y como administrador que configura una partida, quiero que el sistema pueda seleccionar preguntas filtrando por `Category`, `Difficulty`, `AcademicLevel`, `AgeRange`, excluyendo `PreviousQuestions` ya usadas en el `Game`/`Round`, para que cada ronda reciba una pregunta nueva, alineada y sin repetición innecesaria.

**Why this priority**: Es el contrato de selección exigido explícitamente en el SPEC; sin él no hay `StartRound`/`GetCurrentQuestion`. La estrategia concreta (Random, DifficultyAware, Adaptive) se define en el plan técnico, pero el SPEC debe garantizar la capacidad de filtrado y exclusión. Es P2 porque depende de banco poblado y publicable, pero es crítico para jugabilidad.

**Independent Test**: Poblar 10 preguntas `PUBLISHED` en categoría `X`, `Difficulty=2`, `AcademicLevel=Secundaria`, `AgeRange=13-17` → invocar `SelectQuestions` / `GetAvailableQuestions` con `categoryId=X, difficulty=2, academicLevel=Secundaria, ageRange=13-17, previousQuestionIds=[id1,id2], gameId, roundNumber` → retorna preguntas filtradas que no incluyen `previousQuestionIds`, alineadas a criterios, en <500ms para dataset de 1k preguntas. Verificar que preguntas `INACTIVE`/`ARCHIVED`/`DRAFT` nunca se retornan.

**Acceptance Scenarios**:

1. **Given** banco con preguntas `PUBLISHED`+`ACTIVE` de categorías `A` y `B`, **When** el motor solicita `SelectQuestion(categoryId=A, difficulty=3)`, **Then** solo retorna preguntas de `A` con `Difficulty=3`.
2. **Given** juego `G1` ya usó preguntas `[Q1,Q2]` en rondas previas, **When** se solicita siguiente pregunta para `G1`, **Then** el sistema excluye `Q1,Q2` del resultado (`PreviousQuestions` filter).
3. **Given** filtro por `AcademicLevel=Universitario` y `AgeRange=18-25`, **When** se selecciona, **Then** solo preguntas con esos valores (o compatibles según valor objeto) se retornan.
4. **Given** `GameId` y `RoundNumber` provistos, **When** se selecciona, **Then** el sistema puede aplicar contexto de juego/ronda (ej. dificultad incremental) — la estrategia específica es delegada a `IQuestionSelectionStrategy` (plan técnico), pero el contrato MUST aceptar `Game` y `Round` como parámetros.
5. **Given** no existen preguntas que cumplan criterios y exclusiones, **When** se solicita, **Then** retorna `404`/`NoAvailableQuestion` con mensaje explícito, sin seleccionar desalineada.
6. **Given** solicitud concurrente para el mismo `Game`/`Round`, **When** dos hilos piden pregunta, **Then** solo una es asignada al `Round` (idempotencia por `RoundId` o distribución sin duplicado).

---

### Edge Cases

- ¿Qué sucede cuando `AnswerOptions` contiene textos vacíos, duplicados o >500 chars? Rechazo por validación de aplicación (no vacío, longitud, unicidad de texto opcional).
- ¿Qué sucede cuando `CategoryId` apunta a categoría `ARCHIVED` o inexistente? `CreateQuestion`/`UpdateQuestion` rechaza; pregunta existente cuya categoría se archiva deja de ser seleccionable.
- ¿Qué sucede cuando `AcademicLevel` o `AgeRange` no están en enumeraciones/rangos válidos (ej. `AgeRange min > max`, `min<0`, `max>120`)? Rechazo con `InvalidAcademicLevel`/`InvalidAgeRange`.
- ¿Qué sucede cuando una pregunta `PUBLISHED` pierde su respuesta correcta por mutación directa en BD? La capa de dominio debe re-validar en `Publish` y en selección; selección debe excluir preguntas que ya no cumplen 4/1.
- ¿Qué sucede cuando se intenta `ActivateQuestion` sobre pregunta ya `ACTIVE` o `PublishQuestion` sobre ya `PUBLISHED`? Idempotente o `409` según política de transición (ver FR-005).
- ¿Qué sucede cuando se archiva una pregunta que está siendo usada en un `Game` en curso? No afecta el `GameRound` ya creado (copia snapshot), pero deja de estar disponible para futuros juegos/rondas.
- ¿Qué sucede cuando `PreviousQuestions` contiene 100+ IDs y el banco tiene 101 preguntas? Selección debe paginar/filtrar eficientemente vía `Specification<Question>` sin cargar todo en memoria.
- ¿Qué sucede cuando dos admins editan la misma pregunta concurrentemente? Segundo recibe `409` por `rowversion`.
- ¿Qué sucede cuando `Difficulty` es 1..5 pero `Category` tiene `DifficultyLevel` distinto? Debe validar alineación; pregunta desalineada no cuenta como válida para `Category.Publish`.
- ¿Qué sucede cuando se borra el texto de una `AnswerOption` correcta dejando pregunta publicada sin correcta? QST-005 bloquea la mutación.
- ¿Qué sucede cuando se solicita selección con `Category=null`? Si el plan permite sin categoría, retorna preguntas de cualquier categoría publicada; si no, requiere categoría (ver Assumptions).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema MUST exigir que toda `Question` tenga exactamente 4 `AnswerOption` (QST-001); intentos con ≠4 MUST rechazarse con `QuestionMustHaveFourOptions` en validación de aplicación y dominio.
- **FR-002**: El sistema MUST exigir exactamente 1 `AnswerOption.IsCorrect == true` por pregunta (QST-002); 0 o >1 MUST rechazarse con `QuestionMustHaveOneCorrectAnswer`; esta invariante MUST aplicarse en `CreateQuestion`, `UpdateQuestion` y `PublishQuestion`.
- **FR-003**: El sistema MUST exigir que toda `Question` pertenezca a una `Category` existente y no archivada (QST-003); `CategoryId` nulo o inexistente MUST rechazarse con `CategoryNotFound` / `QuestionMustBelongToCategory`; la relación se modela como `Question.CategoryId:StronglyTypedId`.
- **FR-004**: El sistema MUST exigir que toda `Question` tenga `Difficulty` definida y válida (1..5, enumeración o VO compatible con constitución C) (QST-004); valor nulo o fuera de rango MUST rechazarse con `QuestionMustHaveDifficulty`.
- **FR-005**: El sistema MUST impedir que una pregunta en estado `PUBLISHED` quede sin respuesta correcta (QST-005); cualquier `UpdateQuestion` que deje la pregunta publicada con 0 o >1 correctas MUST rechazarse con `PublishedQuestionMustHaveCorrectAnswer` sin mutar el agregado; transición `PUBLISHED→INACTIVE` permitida sin violar QST-005.
- **FR-006**: El sistema MUST exigir validación previa antes de que una pregunta esté disponible para el juego (QST-006); solo preguntas en estado `PUBLISHED` (y `ACTIVE` si se distingue publicación de activación) con invariantes 4/1 superadas MUST ser retornadas por selección; preguntas `DRAFT`/`INACTIVE`/`ARCHIVED` MUST excluirse de `GetAvailableQuestions`.
- **FR-007**: El sistema MUST validar alineación de `AcademicLevel` y `AgeRange` de la pregunta contra su `Category` cuando se cuenta como válida para publicación de categoría (SPEC-002 FR-006/FR-007); pregunta desalineada MUST no contar como válida aunque cumpla 4/1.
- **FR-008**: El sistema MUST exponer casos de uso `CreateQuestion`, `UpdateQuestion` (solo `DRAFT`/`INACTIVE` y opcionalmente `PUBLISHED` si mantiene invariantes), `ActivateQuestion` (`DRAFT`/`INACTIVE`→`ACTIVE`), `DeactivateQuestion` (`ACTIVE`/`PUBLISHED`→`INACTIVE`), `PublishQuestion` (`DRAFT`/`ACTIVE`→`PUBLISHED` con gate QST-001..004), `ArchiveQuestion` (`INACTIVE`/`PUBLISHED`→`ARCHIVED` terminal), como transiciones explícitas protegidas por concurrencia optimista (`rowversion`) y retornando `Result` con `Error` tipificados.
- **FR-009**: El sistema MUST modelar `Question` como `AggregateRoot<QuestionId>` con `QuestionText` (3–500), `CategoryId`, `Difficulty` (Enumeration/VO 1..5), `AcademicLevel` (Enumeration/VO), `AgeRange` (ValueObject min/max), `AnswerOptions: IReadOnlyList<AnswerOption>` (4), `Status: Enumeration(DRAFT,ACTIVE,PUBLISHED,INACTIVE,ARCHIVED)` o equivalente, `RowVersion:byte[]`, `CreatedAt/UpdatedAt`, `PublishedAt`; mutaciones solo vía comportamiento (`Question.Create()`, `Update()`, `Publish()`, `Activate()`, `Deactivate()`, `Archive()`) sin setters públicos, aplicando `IBusinessRule`.
- **FR-010**: El sistema MUST modelar `AnswerOption` como `Entity<AnswerOptionId>` o `ValueObject` inmutable dentro del agregado `Question` con `Text` (1–500, no vacío), `IsCorrect:bool`, `Order` (A-D); la colección MUST ser de tamaño fijo 4 y el agregado MUST exponer método `SetCorrectAnswer(optionId)` que garantiza exactamente 1 correcta.
- **FR-011**: El sistema MUST exponer cada caso vía Vertical Slice CQRS (`ICommand`/`IQuery` + `Validator` (FluentValidation) + `Handler` + `Response DTO` + `IEndpoint` thin `ISender.SendAsync → Result.ToHttpResult`), con `ValidationBehavior` + `IBusinessRule`, y mapear `Error` a `ProblemDetails` (`400` validación/regla, `404` not found, `409` conflicto).
- **FR-012**: El sistema MUST persistir preguntas consultables vía `Specification<Question>` (filtros por `CategoryId`, `Difficulty`, `AcademicLevel`, `AgeRange`, `Status`, búsqueda por texto) con paginación y `ApplyAsNoTracking` para lectura, y exponerlas vía CQRS Queries `GetQuestions`/`GetQuestionById`/`GetAvailableQuestions`; `DbContext` MUST derivar de `AppDbContextBase` y participar en transacción con dominio.
- **FR-013**: El sistema MUST proveer capacidad de selección de preguntas para el motor de juego considerando obligatoriamente: `Category`, `Difficulty`, `AcademicLevel`, `AgeRange`, `PreviousQuestions` (lista de `QuestionId` ya usadas en el juego), `Game` (contexto `GameId`), `Round` (contexto `RoundNumber`/`RoundId`); el contrato MUST ser `IQuestionSelectionStrategy.SelectAsync(criteria)` o `IQuestionSelector` / Query `SelectQuestion` que acepta esos 7 parámetros y retorna `Question` o `NoAvailableQuestion`.
- **FR-014**: El sistema MUST garantizar que la selección excluye preguntas `INACTIVE`/`ARCHIVED`/`DRAFT` y desalineadas, excluye `PreviousQuestions`, y que la estrategia concreta (Random, DifficultyAware, Adaptive, CategorySpecific) sea intercambiable detrás de la abstracción sin cambiar el contrato; la estrategia por defecto y su configuración MUST ser documentadas en el plan técnico pero el SPEC garantiza el contrato.
- **FR-015**: El sistema MUST emitir `QuestionCreated`/`QuestionUpdated`/`QuestionPublished`/`QuestionDeactivated`/`QuestionArchived` como `DomainEvent` (in-process, `AppDbContextBase.SaveChanges`) y, cuando sea necesario para integración (ej. proyección de contador de categoría), publicar `IntegrationEvent` vía Outbox (`IOutboxWriter` + `OutboxProcessor` → RabbitMQ) — nunca antes del commit.
- **FR-016**: El sistema MUST auditar todas las mutaciones (creación, actualización, publicación, archivado, selección) en registro append-only con `CorrelationId`, `QuestionId`, `CategoryId`, `PerformedBy` (sub de OroIdentityServer), `Timestamp`; y registrar métricas/logs estructurados vía `BuildingBlocks.ServiceDefaults`.

### Key Entities *(include if feature involves data)*

- **Question (AggregateRoot<QuestionId>)**: Agregado raíz del banco. Atributos: `QuestionId:StronglyTypedId<Guid>`, `Text: string (3–500)`, `CategoryId:StronglyTypedId<Guid>` (FK a Category), `Difficulty: Enumeration/VO (1..5)` (Basic/Elementary/Intermediate/Advanced/Expert o 1..5), `AcademicLevel: Enumeration/VO` (ej. Primaria, Secundaria, Universidad, Postgrado), `AgeRange: ValueObject(min:int, max:int)` (0–120, min≤max), `Status: Enumeration(DRAFT,ACTIVE,PUBLISHED,INACTIVE,ARCHIVED)`, `AnswerOptions: IReadOnlyList<AnswerOption>` (4), `RowVersion:byte[]`, `CreatedAt`, `UpdatedAt`, `PublishedAt`. Comportamiento: `Question.Create(text, categoryId, difficulty, academicLevel, ageRange, options)` valida QST-001..004; `Update(...)` re-valida; `Publish()` valida QST-006 y transita a PUBLISHED; `Activate()/Deactivate()/Archive()` transitan con guardas. Invariantes: 4 opciones, 1 correcta, categoría y dificultad obligatorias, publicada implica validada, publicada no puede quedar sin correcta.

- **AnswerOption (Entity<AnswerOptionId> dentro de Question)**: Alternativa de respuesta. Atributos: `AnswerOptionId:StronglyTypedId<Guid>`, `QuestionId`, `Text: string (1–500, no vacío)`, `IsCorrect: bool`, `DisplayOrder: int (0..3 → A-D)`. Comportamiento: pertenece exclusivamente a una Question (composición); mutación solo vía `Question` (agregado). Invariante: exactamente 1 por Question con `IsCorrect==true`.

- **Category (referencia externa, SPEC-002)**: No modelada aquí salvo `CategoryId`. Se asume `Category{Id, Status(DRAFT/ACTIVE/INACTIVE/ARCHIVED), ValidQuestionsCount}`. Relación `Category 1—* Question` lógica; Question valida existencia vía `IRepository<Category>` o `ICategoryExistenceChecker`. Invariante de categoría: ≥5 preguntas válidas (4/1, activa, alineada) para publicar — Question provee el conteo.

- **Difficulty / AcademicLevel / AgeRange (ValueObjects/Enumerations)**: `Difficulty` 1..5; `AcademicLevel` enumeration controlada (Primaria, Secundaria, Bachillerato, Universidad, etc.) alineada a `Category.AcademicLevel`; `AgeRange` ValueObject con `Min`/`Max` y validación 0–120, `Min≤Max`, rango unitario permitido (10–10). Se usan tanto en `Question` como en `Category` para validar alineación en conteo de válidas.

- **QuestionSelectionCriteria (ValueObject / DTO de consulta)**: Parámetros de selección. Atributos: `CategoryId?`, `Difficulty?`, `AcademicLevel?`, `AgeRange?`, `PreviousQuestionIds: IReadOnlyList<QuestionId>`, `GameId:StronglyTypedId`, `RoundId/Number`. No persistido; usado por `IQuestionSelectionStrategy.SelectAsync(criteria)` y por `Specification<Question>`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% de intentos `CreateQuestion` con 4 opciones y exactamente 1 correcta, categoría y dificultad válidas, resultan en `201` con `Status=DRAFT` y `GET /api/questions/{id}` idéntico, en <1s en 95% de casos.
- **SC-002**: 100% de intentos con ≠4 opciones o con 0/2 correctas son rechazados con `QuestionMustHaveFourOptions` / `QuestionMustHaveOneCorrectAnswer` y 0% de preguntas inválidas persisten (verificable por conteo de válidas en categoría).
- **SC-003**: 100% de intentos sin `CategoryId` o sin `Difficulty` son rechazados con `QuestionMustBelongToCategory` / `QuestionMustHaveDifficulty` y sin efectos colaterales.
- **SC-004**: 100% de intentos `PublishQuestion` sobre pregunta inválida (3 opciones, 0 correctas, desalineada) son rechazados con `QuestionNotPublishable` y estado permanece `DRAFT`; con pregunta válida, `PublishQuestion` transita a `PUBLISHED` y emite `QuestionPublishedDomainEvent` en 100% de casos en <2s.
- **SC-005**: 100% de intentos de mutar una pregunta `PUBLISHED` para dejarla sin correcta son rechazados (QST-005) y la pregunta sigue contando como válida para categoría; `DeactivateQuestion`/`ArchiveQuestion` transitan correctamente y la pregunta deja de contar/seleccionar en 100% de casos.
- **SC-006**: Selección con criterios `Category=A, Difficulty=3, AcademicLevel=Secundaria, AgeRange=13-17, PreviousQuestions=[Q1,Q2], Game=G1, Round=R3` retorna solo preguntas `PUBLISHED+ACTIVE` alineadas que no están en `PreviousQuestions` con precisión 100% sobre dataset de 1k preguntas, en <500ms en 95% de casos, paginada y sin cargar todo en memoria.
- **SC-007**: Cuando no existe pregunta que cumpla criterios y exclusiones, el sistema retorna `NoAvailableQuestion` (404 o 204 según contrato) en 100% de casos sin fallback a pregunta desalineada o ya usada.
- **SC-008**: Operaciones concurrentes `PublishQuestion` y `UpdateQuestion` sobre la misma pregunta son protegidas por `rowversion`: segundo escritor recibe `409 Conflict` en 100% de pruebas de concurrencia.
- **SC-009**: 90% de administradores completan el flujo crear 5 preguntas válidas → verificar que categoría con 5 pasa gate de publicación (SPEC-002) sin consultar soporte, medido por test de usabilidad del checklist de banco.

## Assumptions

- `SPEC-002` (Categories) existe y provee `Category` con `Status` y capacidad de contar válidas; si no está implementado, `Question` valida contra stub `ICategoryExistenceChecker` con categorías de ejemplo.
- Estados de pregunta: se adopta `DRAFT → ACTIVE → PUBLISHED` con `INACTIVE` y `ARCHIVED` como estados de desactivación/terminal; si el plan técnico unifica `ACTIVE` y `PUBLISHED` (ej. `PUBLISHED` implica `ACTIVE`), se documentará como variante sin romper QST-006 — el SPEC acepta ambos siempre que solo validadas sean seleccionables.
- `UpdateQuestion` en `PUBLISHED` se asume permitido solo si mantiene 4/1 y alineación; si el negocio exige inmutabilidad estricta de publicadas, el plan documentará que se exige `Deactivate` previo. Ambas variantes satisfacen QST-005.
- `AcademicLevel` y `AgeRange` se tratan como enumeraciones/VOs controlados (ej. Primaria/Secundaria/Universidad; 0–120 años) validados por longitud/rango; valores exactos se alinearán a `Category.AcademicLevel/AgeRange` definidos en SPEC-002.
- `AnswerOption.Text` 1–500 chars, no vacío, trim; `DisplayOrder` A-D es informativo y se ordena al persistir; textos duplicados entre opciones de la misma pregunta se rechazan por aplicación (evitar confusión).
- `Difficulty` 1..5 coherente con constitución C (Basic..Expert) y con Game Configuration SPEC-001.
- Selección: el contrato acepta 7 parámetros (Category, Difficulty, AcademicLevel, AgeRange, PreviousQuestions, Game, Round) pero cada uno puede ser opcional salvo `PreviousQuestions` y `Game` que son requeridos para exclusión; si se invoca sin `CategoryId`, el comportamiento por defecto es filtrar solo por los criterios provistos.
- Identidad vía OroIdentityServer (`oroidentityserver:latest`) — crear/gestionar preguntas requiere JWT con rol `ADMIN` o `GAME_MANAGER` (políticas `Question.Read/Write/Publish` mapeadas a claims `roles/permissions`).
- `ArchiveQuestion` es terminal; reactivación futura fuera de alcance y requeriría nueva spec.
- Nombre/unicidad de pregunta no se exige globalmente; solo se valida longitud/contenido; deduplicación por texto idéntico no se impone en v1.

## Dependencies

- `SPEC-002` — Categories (Category lifecycle, rule ≥5 válidas, `CategoryId` existence, `IQuestionCounter` / `IRepository<Question>` for valid count).
- `SPEC-001` — Game Configuration (consume preguntas publicadas; selección por `Game`/`Round`).
- `BuildingBlocks.Kernel.Domain` — `AggregateRoot`, `Entity`, `ValueObject`, `StronglyTypedId`, `Enumeration`, `IBusinessRule`, `Result`, `IDomainEvent`.
- `BuildingBlocks.CQRS` — `ICommand`/`IQuery`, `ICommandHandler`/`IQueryHandler`, `ISender`, `IPipelineBehavior`, `ValidationBehavior`.
- `BuildingBlocks.Kernel.Infrastructure` — `IRepository`, `IUnitOfWork`, `AppDbContextBase`, `Specification<T>`, `IOutboxWriter`.
- `BuildingBlocks.EventBus` — `IntegrationEvent`, `IEventBus`, OutboxProcessor → RabbitMQ (cuando aplique).
- `BuildingBlocks.ServiceDefaults` — `IEndpoint`, `Result → HTTP`, `GlobalExceptionHandler`, `ProblemDetails`, OpenTelemetry, health checks.
- OroIdentityServer — autenticación/autorización via OIDC discovery `/.well-known/openid-configuration`, JWT `jwks_uri`, claims `sub/roles/tenant_id`.

## Out of Scope

- Estrategia concreta de selección (Random, DifficultyAware, Adaptive, CategorySpecific) más allá del contrato de 7 parámetros — se define en plan técnico/ADR-008.
- Lógica de evaluación de respuestas, cálculo de puntaje, avance de dificultad por ronda, retiro/pérdida/consolación/premios — pertenecen a motor de juego (SPEC-001 y specs futuras).
- UI específica (Angular/Web) más allá del contrato REST necesario (`POST/PUT/GET /api/questions`, `POST /api/questions/{id}/publish|activate|deactivate|archive`, `GET /api/questions/select` o `POST /api/games/{gameId}/questions/select`).
- Importación masiva de preguntas (bulk import CSV/Excel) y edición masiva — podrían añadirse en spec futura sin romper este contrato.
- Versionado histórico de preguntas más allá de `RowVersion` y audit append-only.

## References

- Constitución v1.1.0 — Principios I-III, Additional Constraints B (4 opciones/1 correcta, ≥5 para publicar), C (estrategias configurables), E/F (persistencia/concurrencia), H (OroIdentityServer).
- `draft/constitution.md` §6 (Question Invariants), §5 (States), §8 (Game Configuration ↔ Category ↔ Question).
- `draft/game-concept.md` §2-3 (Question con 4 opciones, 1 correcta, ≥5 para publicar; categorías con AcademicLevel/AgeRange).
- SPEC-002 — Categories (definición de categoría publicable y conteo de válidas).
- SPEC-001 — Game Configuration (dependencia de categoría válida y estrategia de dificultad).

