# Feature Specification: Categories

**Feature Branch**: `002-categories`

**Created**: 2026-08-26

**Status**: Ready for Review

**Input**: User description: "Categories Objetivo Gestionar categorías de conocimiento y sus niveles de orientación académica y etaria. Alcance Una categoría debe permitir definir: Nombre. Descripción. Área de conocimiento. Nivel académico. Edad mínima. Edad máxima. Nivel de dificultad. Estado. Tags. Configuración de publicación. Estados DRAFT ACTIVE INACTIVE ARCHIVED Regla fundamental Una categoría no puede publicarse si no tiene: >= 5 preguntas válidas Cada pregunta debe: tener 4 opciones tener exactamente 1 correcta estar activa cumplir las características de la categoría Casos principales CreateCategory UpdateCategory ActivateCategory DeactivateCategory PublishCategory ArchiveCategory Dependencias SPEC-003"

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Crear y actualizar categoría (Priority: P1)

Como administrador de contenido (ADMIN / GAME_MANAGER), quiero crear una categoría definiendo nombre, descripción, área de conocimiento, nivel académico, rango etario, dificultad, tags y configuración de publicación, y poder actualizarla mientras está en DRAFT/INACTIVE, para tener un catálogo curado y coherente.

**Why this priority**: Sin categorías no hay banco de preguntas ni partidas configurables; es el prerrequisito de SPEC-003 y del Game Configuration (CFG-004). Entrega valor independiente como CRUD de catálogo.

**Independent Test**: Crear categoría vía `CreateCategory` con payload válido → `201` con estado `DRAFT` y todos los campos persistidos; `GET /api/categories/{id}` devuelve lo creado. Actualizar categoría en `DRAFT` → `200` y verifica mutación; intentar crear sin nombre o con `edad mínima > edad máxima` → `400`.

**Acceptance Scenarios**:

1. **Given** payload con nombre “Historia Universal”, descripción, área “Humanidades”, nivel “Secundaria”, edad 13–17, dificultad “Intermediate”, tags ["historia","secundaria"], **When** se envía `CreateCategory`, **Then** se crea `Category` en `DRAFT` con `CategoryId` y valores iguales a los enviados.
2. **Given** categoría existente en `DRAFT`, **When** se envía `UpdateCategory` cambiando descripción y tags, **Then** la categoría se actualiza y mantiene el mismo `Id` y `Version`.
3. **Given** intento con nombre vacío o con `edad mínima (17) > edad máxima (13)`, **When** se envía, **Then** se rechaza con `InvalidCategoryConfiguration` y no se persiste.
4. **Given** categoría en `ARCHIVED`, **When** se intenta `UpdateCategory`, **Then** se rechaza con `InvalidCategoryState` (solo `DRAFT`/`INACTIVE` permiten edición directa; `ACTIVE` requiere transición controlada).

---

### User Story 2 — Ciclo de vida y publicación guardada por invariante de preguntas (Priority: P1)

Como administrador, quiero activar, desactivar, publicar y archivar categorías, pero el sistema debe impedir publicar si no hay ≥5 preguntas válidas (cada una 4 opciones, 1 correcta, activa y alineada a la categoría), para garantizar calidad y jugabilidad.

**Why this priority**: Es la regla de negocio no-negociable descrita en constitución B y en la regla fundamental; sin ella se rompería CFG-004 de Game Configuration y la invariante de pregunta.

**Independent Test**: Crear categoría, crear 4 preguntas válidas asociadas → `PublishCategory` → `400` con `CategoryNotReady`; crear la 5ª válida → `PublishCategory` → `200` y estado `ACTIVE`. Pregunta con 3 opciones o 2 correctas o inactiva → no cuenta como válida.

**Acceptance Scenarios**:

1. **Given** categoría en `DRAFT` con 0 preguntas, **When** se invoca `PublishCategory`, **Then** se rechaza con `CategoryNotPublishable` / `CategoryNotReady` y permanece en `DRAFT`.
2. **Given** categoría con 4 preguntas válidas, **When** se publica, **Then** se rechaza (requiere ≥5). **When** se añade la 5ª válida, **Then** publicar transita a `ACTIVE` y emite `CategoryPublishedDomainEvent`.
3. **Given** categoría en `ACTIVE` con ≥5 válidas pero una pregunta se desactiva o pasa a tener 0 correctas (SPEC-003), **When** se valida, **Then** esa pregunta deja de contar; si el conteo cae <5 la categoría sigue `ACTIVE` pero un nuevo intento de publicar/republicar se rechaza hasta reponer.
4. **Given** categoría `DRAFT` → `ActivateCategory` (sin publicar), **When** se invoca, **Then** transita a `ACTIVE` solo si ya cumple ≥5? **No** — `Activate` es administrativo (habilita visibilidad) pero `Publish` es el gate de calidad: si se intenta `Publish` sin ≥5 debe fallar; `Activate` desde `DRAFT` puede permitirse como `DRAFT→ACTIVE` sin gate solo si el flujo lo define, documentado en *Assumptions* como `Publish` es el único que exige ≥5.
5. **Given** categoría `ACTIVE`, **When** se invoca `DeactivateCategory`, **Then** transita a `INACTIVE`; **When** se invoca `ArchiveCategory` desde `INACTIVE` o `ACTIVE`, **Then** transita a `ARCHIVED` y no permite más `Publish`/`Update` sin reactivación.

---

### User Story 3 — Consulta y filtrado de categorías (Priority: P2)

Como jugador y administrador, quiero listar y filtrar categorías por área, nivel académico, rango etario, dificultad, estado y tags, para navegar el catálogo y para que `CreateGame` pueda validar `CategoryId` publicado.

**Why this priority**: Habilita descubrimiento y la validación de Game Configuration; no bloquea CRUD pero es esencial para UX y para SPEC-003/SPEC-001.

**Independent Test**: Crear 3 categorías con distintas áreas/niveles/tags/estados → `GET /api/categories?knowledgeArea=Humanidades&academicLevel=Secundaria&state=ACTIVE` devuelve solo la coincidente; `GET /api/categories/{id}` devuelve detalle con `validQuestionsCount`.

**Acceptance Scenarios**:

1. **Given** categorías `ACTIVE` e `INACTIVE`, **When** se lista con `state=ACTIVE`, **Then** solo se retornan `ACTIVE` paginadas.
2. **Given** categoría con tags ["matemáticas","álgebra"], **When** se filtra por `tag=álgebra`, **Then** se incluye.
3. **Given** juego intenta `CreateGame` con `categoryId` `ARCHIVED` o con <5 válidas, **When** valida, **Then** `CategoryNotReady` (integración con SPEC-001, verificable vía `Specification` de conteo).

---

### Edge Cases

- ¿Qué sucede cuando `edad mínima = edad máxima`? Válido (rango unitario, ej. 10–10).
- ¿Qué sucede cuando `edad mínima <0` o `>120` o `tags` duplicados/ vacíos? Rechazo o normalización (trim, deduplicar).
- ¿Qué sucede cuando 2 admins intentan `PublishCategory` concurrentemente? Solo uno gana; el segundo ve `409 Conflict` por `rowversion`/estado ya `ACTIVE`.
- ¿Qué sucede cuando una categoría `ARCHIVED` se intenta `Publish`? Rechazo `InvalidCategoryState`.
- ¿Qué sucede cuando una pregunta válida se edita y deja de cumplir (pasa a 2 correctas)? Deja de contar inmediatamente; el siguiente `Publish` puede fallar si cae <5.
- ¿Qué sucede cuando se intenta crear categoría con nombre duplicado? Permitido salvo unicidad definida; se asume permitido con `Slug` único opcional (ver *Assumptions*).
- ¿Qué sucede cuando `Nivel de dificultad` no está en {1..5}? Rechazo por enumeración.
- ¿Qué sucede cuando `área de conocimiento` es libre vs. enumeración? Se trata como `string` con validación de longitud, no enum cerrado.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema MUST permitir crear `Category` con nombre (3–100, no vacío), descripción (0–500), área de conocimiento, nivel académico, edad mínima/máxima, nivel de dificultad, tags y configuración de publicación; estado inicial `DRAFT`.
- **FR-002**: El sistema MUST validar coherencia etaria: `edad mínima ≥0`, `edad máxima ≤120`, `min ≤ max`; violaciones MUST rechazarse con `InvalidCategoryConfiguration`.
- **FR-003**: El sistema MUST representar el ciclo de vida `DRAFT → ACTIVE ↔ INACTIVE → ARCHIVED` (y `DRAFT→ARCHIVED` permitido) como transiciones explícitas; transiciones inválidas MUST rechazarse con `InvalidCategoryState`; transiciones MUST ser protegidas por concurrencia optimista (`rowversion`).
- **FR-004**: El sistema MUST exponer casos de uso `CreateCategory`, `UpdateCategory` (solo `DRAFT`/`INACTIVE`), `ActivateCategory` (`DRAFT`/`INACTIVE`→`ACTIVE`), `DeactivateCategory` (`ACTIVE`→`INACTIVE`), `PublishCategory` (`DRAFT`/`INACTIVE`→`ACTIVE` con gate), `ArchiveCategory` (`INACTIVE`/`ACTIVE`→`ARCHIVED`).
- **FR-005**: El sistema MUST impedir `PublishCategory` si la categoría no tiene ≥5 preguntas válidas (`FR-006`); si <5 MUST retornar `CategoryNotPublishable`/`CategoryNotReady` sin cambiar estado.
- **FR-006**: El sistema MUST definir “pregunta válida” como: exactamente 4 `AnswerOption`, exactamente 1 `IsCorrect==true`, `Question.Status==Active`, y `Question` alineada a la categoría (`CategoryId` igual y `Difficulty`/`AcademicLevel`/`AgeRange` compatibles); conteo vía `Specification<Question>` y `IQuestionCounter` (SPEC-003).
- **FR-007**: El sistema MUST validar cada pregunta contra las características de la categoría al contar como válida; preguntas desalineadas (ej. dificultad fuera de rango, edad no cubierta) MUST no contar.
- **FR-008**: El sistema MUST modelar `Category` como `AggregateRoot<CategoryId>` con `CategoryName`/`Description`/`KnowledgeArea`/`AcademicLevel`/`AgeRange`/`DifficultyLevel` como ValueObjects o Enumerations inmutables, y `CategoryStatus` como `Enumeration<DRAFT,ACTIVE,INACTIVE,ARCHIVED>`; mutaciones solo vía comportamiento (`Category.Update()`, `Publish()`, etc.) sin setters públicos, retornando `Result`.
- **FR-009**: El sistema MUST persistir categorías consultables vía `Specification<Category>` (filtros por área, nivel, edad, dificultad, estado, tags) y paginación, y exponerlas vía CQRS Query `GetCategories`/`GetCategoryById`.
- **FR-010**: El sistema MUST exponer cada caso vía Vertical Slice CQRS (`ICommand`/`IQuery` + `Validator` + `Handler` + `Response` + `IEndpoint` thin `ISender.SendAsync → Result.ToHttpResult`), con `ValidationBehavior` + `IBusinessRule`, y mapear `Error` a `ProblemDetails` (`400` validación, `404` not found, `409` conflicto).
- **FR-011**: El sistema MUST emitir `CategoryCreated`/`CategoryUpdated`/`CategoryPublished`/`CategoryArchived` como `DomainEvent` (in-process, `AppDbContextBase.SaveChanges`) y no requerir Outbox salvo que se publique integración futura.

### Key Entities *(include if feature involves data)*

- **Category (AggregateRoot<CategoryId>)**: Agregado raíz del catálogo. Atributos: `CategoryId:StronglyTypedId<Guid>`, `Name` (3–100), `Description`, `KnowledgeArea:ValueObject/string`, `AcademicLevel:Enumeration/VO` (ej. Primaria, Secundaria, Universidad), `AgeRange:ValueObject(min,max)`, `DifficultyLevel:Enumeration(int 1..5)`, `Status:Enumeration(DRAFT/ACTIVE/INACTIVE/ARCHIVED)`, `Tags:Set<string>`, `PublishConfiguration:VO`, `RowVersion:byte[]`, `CreatedAt`, `ValidQuestionsCount` (derivado vía conteo SPEC-003, no persistido como contador denormalizado salvo proyección). Comportamiento: `Create()`, `Update()`, `Activate()`, `Deactivate()`, `Publish(IQuestionCounter)`, `Archive()`. Invariante: `Publish` solo si `CountValidQuestions() ≥5`.

- **Question (referencia externa, SPEC-003)**: No se modela aquí, pero se asume `Question{Id, CategoryId, Status, AnswerOptions(4), IsCorrect(1), Difficulty, AcademicLevel, AgeRange}`. Validez definida en FR-006. Relación `Category 1—* Question` lógica, no FK física cross-aggregate si se desacopla.

- **AnswerOption (referencia externa)**: `AnswerOption{Id, Text, IsCorrect}`; invariante 4 opciones, 1 correcta.

- **Enumeration/VOs**: `CategoryStatus`, `KnowledgeArea`, `AcademicLevel`, `DifficultyLevel` (1..5), `AgeRange`.

### Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Crear categoría válida (nombre, área, nivel, 13–17, dificultad, tags) resulta en `201` con `state=DRAFT` y `GET` idéntico en 100% de intentos válidos, <1s en 95% de casos.
- **SC-002**: 100% de intentos `PublishCategory` con <5 válidas son rechazados con `CategoryNotPublishable` y estado no cambia (verificable con 0–4 preguntas).
- **SC-003**: Con exactamente 5 válidas (4 opciones/1 correcta/activas/alineadas), `PublishCategory` transita a `ACTIVE` y `CategoryPublishedDomainEvent` emitido en 100% de casos, <2s.
- **SC-004**: Pregunta con 3 opciones, 0/2 correctas, inactiva o desalineada no incrementa el contador válido (0% falsos positivos) y no permite bypass del gate.
- **SC-005**: Transiciones inválidas (`ARCHIVED→Publish`, `ACTIVE→Update` directo) son rechazadas `InvalidCategoryState` en 100% y protegidas contra condiciones de carrera (segundo `Publish` concurrente → `409`) en 100% de pruebas de concurrencia.
- **SC-006**: `GET /api/categories?state=ACTIVE&knowledgeArea=X` filtra correctamente (precisión 100% en dataset de prueba con 20 categorías) y pagina sin filtrar filtrados.
- **SC-007**: 90% de admins completan el flujo crear→añadir 5 preguntas→publicar sin consultar soporte, medido por test de usabilidad del checklist de publicación.

## Assumptions

- `SPEC-003` existe o se provee `IQuestionCounter` stub/mock que cuenta preguntas válidas por `CategoryId`; si no, `Publish` se valida contra `IRepository<Question>` mock con 5 registros válidos de ejemplo.
- `Question` y su `AnswerOption` se rigen por constitución B (4 opciones, 1 correcta, activa) y se validan en SPEC-003; `Category` solo cuenta, no crea preguntas.
- `AgeRange` 0–120 años, `edad mínima ≤ máxima`; rango unitario permitido (ej. 10–10). `Tags` se normalizan lowercase, trim, deduplicados, ≤10 por categoría, cada tag 2–30 chars.
- `Nombre` no requiere unicidad global en v1; se valida solo longitud/no vacío; `Slug` único podría añadirse luego sin romper.
- `Área de conocimiento` y `Nivel académico` se tratan como strings controlados (p. ej. “Matemáticas”, “Secundaria”) no enums cerrados, validados por longitud 2–100.
- `Nivel de dificultad` es `Enumeration` 1..5 (Básico..Experto) coherente con Game Configuration.
- `UpdateCategory` permitido solo en `DRAFT`/`INACTIVE`; `ACTIVE` requiere `Deactivate` primero (o flujo `Update` que cree borrador). `Archive` desde `INACTIVE`/`ACTIVE`; `ARCHIVED` es terminal salvo reactivación futura fuera de alcance.
- Identidad vía OroIdentityServer (`oroidentityserver:latest`) — crear/gestionar categorías requiere `ADMIN` o `GAME_MANAGER` (JWT `roles`).

## Dependencies

- `SPEC-003` — Banco de preguntas (definición de “válida” y `IQuestionCounter` / `IRepository<Question>`).
- `SPEC-001` — Game Configuration (consume `CategoryId` publicado; validación `CategoryNotReady`).
- `BuildingBlocks.Kernel.Domain` — `AggregateRoot`, `ValueObject`, `StronglyTypedId`, `Enumeration`, `IBusinessRule`, `Result`.
- `BuildingBlocks.CQRS` — `ICommand`/`IQuery`, `ISender`, `IPipelineBehavior`, `IValidator`.
- `BuildingBlocks.Kernel.Infrastructure` — `IRepository`, `IUnitOfWork`, `AppDbContextBase`, `Specification<T>`.

## Out of Scope

- Creación/edición de `Question`/`AnswerOption` en detalle (propio de SPEC-003).
- Reglas de selección de preguntas por dificultad/edad dentro de partidas (Game Engine).
- UI: solo contrato REST necesario (`POST/PUT/GET` categorías, `POST` transiciones `activate/publish/archive`); frontend Angular es presentación.

## References

- Constitución v1.1.0 — Principios I-III, B (invariantes pregunta/categoría), C (configurable), E/F (persistencia/concurrencia), H (OroIdentityServer).
- `draft/constitution.md` §6 (Question Invariants), §5 (States), §8 (Game Configuration依赖 categoría).
- `draft/game-concept.md` §2-3 (Category/Question con 4 opciones, 1 correcta, ≥5 para publicar).
