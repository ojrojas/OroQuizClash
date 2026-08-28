# Feature Specification: Admin Question Bank

**Feature Branch**: `021-admin-question-bank`

**Created**: 2026-08-28

**Status**: Draft

**Input**: User description: "021 — Admin Question Bank Objetivo Administrar el banco de preguntas y respuestas de QuizArena. Descripción Permitirá: Crear preguntas. Editar preguntas. Eliminar preguntas. Activar/desactivar preguntas. Asociar preguntas a categorías. Definir dificultad. Definir nivel académico. Definir rango de edad. Configurar cuatro respuestas. Definir respuesta correcta. Agregar explicación. Configurar tiempo. Consultar estadísticas. Cada pregunta deberá tener exactamente: Question, Answer A, Answer B, Answer C, Answer D, Correct Answer. Una categoría no podrá ser publicada para juego si no cumple el mínimo configurable de preguntas, cuyo valor inicial será 5 preguntas."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Crear y gestionar el núcleo de preguntas (Priority: P1)

Como administrador (ADMIN) o gestor de juegos (GAME_MANAGER) autenticado, quiero crear una pregunta definiendo texto, categoría asociada, 4 respuestas (A–D) con exactamente 1 correcta, y atributos básicos (dificultad, nivel académico, rango edad, tiempo, explicación) para poblar el banco y habilitar la publicación de categorías.

**Why this priority**: Es el núcleo de 021 — sin preguntas con invariante 4/1 no hay juego posible y la categoría no alcanza el umbral de 5 para publicar (Constitución B). Constituye el MVP estricto.

**Independent Test**: Login ADMIN → /admin/questions → "Crear pregunta" → completar texto ≥10, seleccionar categoría Active, definir dificultad 1–5, nivel, edad, 4 opciones (1 correcta), explicación opcional, tiempo 5–300 → guardar → verificar pregunta aparece en listado con 4 respuestas y 1 correcta persistida. No requiere activar/desactivar ni estadísticas.

**Acceptance Scenarios**:

1. **Given** un ADMIN en creación, **When** completa texto 10–500, categoría Active existente, dificultad 1–5, nivel académico 2–100, AgeMin/AgeMax 0–120 con min≤max, 4 opciones (texto 1–200 cada una) con exactamente 1 marcada correcta, explicación 0–1000 opcional y tiempo 5–300 y guarda, **Then** el sistema crea la pregunta en estado `Draft`/`Active` según flujo y muestra confirmación con ID.
2. **Given** un intento con solo 3 opciones o 2 correctas o texto de opción vacío, **When** guarda, **Then** el sistema rechaza con `InvalidQuestionData` señalando `Options` y no crea la pregunta.
3. **Given** un intento con categoría inexistente o inactiva, **When** guarda, **Then** el sistema rechaza con `CategoryNotReady`/`CategoryNotFound` y no crea la pregunta.
4. **Given** una pregunta existente en `Draft`/`Active`, **When** el ADMIN edita el texto y cambia la respuesta correcta de B a D y guarda, **Then** el sistema persiste la edición y mantiene exactamente 4 opciones con 1 correcta.
5. **Given** un REWARD_MANAGER (sin permiso `Question.Write`), **When** intenta acceder a "Crear pregunta", **Then** ve `Access Denied` y no puede crear/editar.

---

### User Story 2 - Operar el ciclo de vida y consultar el banco (Priority: P1)

Como administrador, quiero activar/desactivar, eliminar y consultar estadísticas del banco (total por categoría, por dificultad, por estado) para operar el contenido y decidir publicación de categorías.

**Why this priority**: El objetivo incluye Activar/desactivar, Eliminar y Consultar estadísticas — sin operar el ciclo de vida, el banco queda estático y no se puede curar. Co-prioritario con US1 para valor operacional.

**Independent Test**: Tomar pregunta en `Active` → Desactivar → verificar `Inactive` y que no cuenta para `ValidQuestionCount` de la categoría; Reactivar → `Active`; Eliminar (si nunca usada en juego `Running`) → desaparece del listado; consultar estadísticas → ver agregados por categoría/dificultad.

**Acceptance Scenarios**:

1. **Given** una pregunta en `Active`, **When** el ADMIN ejecuta "Desactivar", **Then** transita a `Inactive`, deja de contar para `ValidQuestionCount` y para selección de preguntas en juegos.
2. **Given** una pregunta en `Inactive`, **When** ejecuta "Activar", **Then** transita a `Active` (si mantiene 4 opciones/1 correcta y categoría Active) y vuelve a contar para `ValidQuestionCount`.
3. **Given** una pregunta en `Draft`/`Inactive` nunca usada en juego `Running`/`Finished`, **When** el ADMIN ejecuta "Eliminar", **Then** la pregunta se elimina (o archiva) y desaparece del listado; si está en uso en juego activo, el borrado es rechazado con `QuestionInUse`.
4. **Given** el banco con 20 preguntas distribuidas en 3 categorías y dificultades 1–5, **When** el ADMIN abre "Estadísticas", **Then** ve total por categoría, por dificultad, por estado (`Active`/`Inactive`/`Draft`) y por tiempo promedio, actualizados sin recarga completa.
5. **Given** una categoría con 4 preguntas `Active`, **When** el ADMIN intenta publicarla para juego, **Then** el sistema rechaza con `CategoryNotReady` indicando faltan 1 (mínimo configurable 5).

---

### User Story 3 - Configurar atributos avanzados y validar invariantes cruzadas (Priority: P2)

Como administrador avanzado, quiero definir dificultad, nivel académico, rango de edad y tiempo por pregunta con validación y feedback por campo, y validar que una categoría no sea publicable si no cumple el mínimo configurable (inicial 5, configurable), para que la progresión y segmentación sean explícitas y auditables.

**Why this priority**: Eleva la pregunta de "texto + 4 opciones" a "contenido curado" (Constitución B: características de dificultad). Depende de US1/US2 y es P2 porque el valor base ya se entregó.

**Independent Test**: Editar pregunta en `Draft` → definir dificultad 3, nivel "Universitario", edad 18–25, tiempo 45, explicación 200 chars → guardar con éxito; luego intentar nivel vacío o edad invertida o tiempo 0/301 o explicación 1001 chars → ver errores por campo; cambiar mínimo configurable de categoría de 5 a 3 (si el sistema lo expone) y verificar que categoría con 3 ya es publicable.

**Acceptance Scenarios**:

1. **Given** una pregunta en `Draft`, **When** selecciona `Dificultad 5` y `Nivel académico Universitario` y guarda, **Then** el sistema persiste los valores y los muestra en el detalle.
2. **Given** `Rango de edad` 10–12 para categoría con `AgeMin 0–5`, **When** guarda, **Then** el sistema permite guardar pero muestra advertencia "Rango/nivel incoherente con categoría" sin bloquear (validación de negocio, no invariante — documentado en UI).
3. **Given** `Tiempo` 0 o 301, **When** guarda, **Then** el sistema rechaza con `InvalidQuestionData` por campo `TimePerQuestion` (rango 5–300).
4. **Given** `Explicación` de 1001 chars, **When** guarda, **Then** el sistema rechaza con `InvalidQuestionData` por campo `Explanation` (0–1000).
5. **Given** una categoría con 5 preguntas `Active` y mínimo configurable inicial 5, **When** el ADMIN consulta si es publicable, **Then** ve `ValidQuestionCount 5/5` y botón "Publicar" habilitado; si el mínimo se configura a 3 (si el sistema lo expone), una categoría con 3 ya muestra `3/3` y habilita publicar.

---

### Edge Cases

- ¿Qué ocurre si dos ADMIN crean preguntas idénticas (mismo texto) para la misma categoría? Permitido (no hay unicidad de texto), pero el sistema advierte duplicado si el texto es idéntico case-insensitive para revisión.
- ¿Qué ocurre si se edita una pregunta que está en uso en un juego `Running` (`QuestionInUse`)? La edición de texto/opciones es rechazada con `QuestionInUse` o se crea una nueva versión (según política de versionado); el texto original permanece para el juego en curso.
- ¿Qué ocurre si se elimina una pregunta que es la quinta válida de una categoría `Active`? La categoría permanece `Active` pero `ValidQuestionCount` baja a 4 y muestra advertencia "Requiere 5 para seguir publicable"; si se desactiva otra, la categoría deja de ser elegible para nuevos juegos.
- ¿Qué ocurre si `Tiempo` se cambia a 5 en una pregunta ya usada en juegos finalizados? Permitido; los juegos históricos mantienen el tiempo original (inmutabilidad de instancia).
- ¿Qué ocurre si el ADMIN pierde sesión mientras crea una pregunta? El guardado falla con 401, el formulario conserva borrador local y muestra "Sesión expirada — re-autenticar" sin pérdida de datos ingresados.
- ¿Qué ocurre con concurrencia (dos ADMIN editando misma pregunta `Draft`)? `rowversion` detecta conflicto y uno recibe `ConcurrencyConflict` con opción de recargar.
- ¿Qué ocurre si una categoría tiene exactamente 5 preguntas pero una es desactivada? `ValidQuestionCount` baja a 4 y la categoría sigue `Active` pero muestra estado "No elegible" hasta recuperar 5.

## Requirements *(mandatory)*

### Functional Requirements

**Creación y definición (núcleo 4+1)**

- **FR-001**: El sistema MUST permitir crear una pregunta con `Texto` (10–500, requerido), `Categoría` (requerida, debe existir y estar en estado no archivado) y `Dificultad` (1–5, requerida) con validación por campo.
- **FR-002**: El sistema MUST exigir y persistir exactamente **cuatro** respuestas (`Answer A`, `Answer B`, `Answer C`, `Answer D`) cada una con `Texto` 1–200 (requerido) y exactamente **una** marcada como `Correct Answer` (requerido, 1 de 4). Invariante de dominio, no solo de UI.
- **FR-003**: El sistema MUST permitir definir `Nivel académico` (2–100, requerido), `Rango de edad` (`AgeMin`/`AgeMax` 0–120 con `AgeMin ≤ AgeMax`, requeridos) y `Tiempo` (`TimePerQuestion` 5–300s, requerido) con validación por campo.
- **FR-004**: El sistema MUST permitir definir `Explicación` (0–1000, opcional) mostrada tras responder, y `Estado` inicial `Draft` (o `Active` si pasa validación completa) con `RowVersion` para concurrencia.
- **FR-005**: El sistema MUST permitir `Editar` los 9 campos (texto, categoría, dificultad, nivel, edad, tiempo, explicación, 4 respuestas, correcta) mientras la pregunta está en `Draft`/`Active`/`Inactive`; toda edición MUST re-validar invariante 4/1 y MUST preservar `RowVersion`.

**Ciclo de vida y operaciones**

- **FR-006**: El sistema MUST permitir `Activar` (`Inactive`/`Draft` → `Active`) y `Desactivar` (`Active` → `Inactive`) con validación de invariante 4/1 y categoría no archivada; el estado `Active` cuenta para `ValidQuestionCount` de la categoría, `Inactive`/`Draft` no.
- **FR-007**: El sistema MUST permitir `Eliminar` (o archivar) una pregunta en `Draft`/`Inactive` que nunca fue usada en juego `Running`/`Finished`; si está en uso, el borrado MUST ser rechazado con `QuestionInUse` sin mutación parcial, protegido por `rowversion`.
- **FR-008**: El sistema MUST permitir `Consultar estadísticas` del banco: total por categoría, por dificultad (1–5), por estado (`Draft`/`Active`/`Inactive`), por categoría con `ValidQuestionCount`, y tiempo promedio; agregados server-side sin cargar colecciones completas.

**Invariante cruzada con categorías**

- **FR-009**: El sistema MUST aplicar la guarda `Categoría no publicable si `ValidQuestionCount < MínimoConfigurable` (inicial 5, configurable por el sistema) y exponer `MínimoConfigurable` como parámetro del sistema (preservando compatibilidad con categorías existentes). Si <5, publicar categoría MUST ser rechazado con `CategoryNotReady` indicando faltantes, y la UI MUST mostrar `ValidQuestionCount/MínimoConfigurable` y deshabilitar "Publicar".
- **FR-010**: El sistema MUST mantener coherencia: al activar/desactivar/eliminar una pregunta, el `ValidQuestionCount` de su categoría MUST actualizarse de forma transaccional y la elegibilidad de la categoría para juegos MUST re-evaluarse (si baja de 5, la categoría `Active` muestra advertencia "No elegible").

**Validación, autorización y auditoría**

- **FR-011**: El sistema MUST validar en tres niveles: API (contrato), Aplicación (requisitos — categoría existe, 4/1, dificultad 1–5, nivel 2–100, edad 0–120, tiempo 5–300, explicación 0–1000), y Dominio (invariantes — exactamente 4 opciones/1 correcta, `CategoryNotReady`, `rowversion`). Los invariantes MUST NOT depender solo de UI.
- **FR-012**: El sistema MUST mostrar errores por campo con códigos accionables (`InvalidQuestionData`, `CategoryNotFound`, `CategoryNotReady`, `QuestionInUse`, `ConcurrencyConflict`) y MUST preservar borrador local en caso de 401 sin pérdida de datos hasta re-autenticar.
- **FR-013**: El sistema MUST restringir creación/edición/eliminación/activación a roles `ADMIN` y `GAME_MANAGER` (política `AdminOrGameManager`); `REWARD_MANAGER` y `PLAYER` MUST recibir `Access Denied` en UI y 403 por API sin fuga. `OroIdentityServer` es la única autoridad (Constitución VI).
- **FR-014**: El sistema MUST auditar de forma append-only cada creación, modificación, cambio de estado y eliminación (actor `sub`, timestamp UTC, `QuestionId`, `CategoryId`, diff de campos clave, `CorrelationId`) sin mutar historial.
- **FR-015**: El sistema MUST propagar `CorrelationId` y mapear `Result` → HTTP (`ProblemDetails` RFC 7807) sin exponer detalles internos.

**Integración y presentación**

- **FR-016**: El sistema MUST consumir exclusivamente la API/BFF (`QuizArena.Api` via `QuizArena.Admin` BFF) para todos los datos de preguntas/categorías/estadísticas; MUST NOT acceder directamente a SQL Server/Oracle/`identitydb`.
- **FR-017**: El sistema MUST reutilizar el shell de navegación, tema `administration`, tokens y componentes del Design System (SPEC-016) sin valores hardcodeados y MUST residir en `src/Admin/QuizArena.Admin` (Blazor Auto net10.0) y `src/Admin/QuizArena.Admin.Client`.
- **FR-018**: El sistema MUST exigir sesión válida via `OroIdentityServer` (OIDC `authorization_code` + `refresh_token`) y manejar `must_change_password` y expiración antes de permitir administrar el banco.
- **FR-019**: El sistema MUST listar preguntas con paginación y filtros por categoría, dificultad, estado y búsqueda por texto, y MUST ofrecer detalle con 4 respuestas resaltando la correcta y explicación, indicando si la pregunta está en uso en juegos activos.

### Key Entities *(include if feature involves data)*

- **Question**: Agregado de contenido. Atributos: `QuestionId`, `Text` (10–500), `CategoryId` (FK `Category` no archivada), `Difficulty` (1–5), `AcademicLevel` (2–100), `AgeMin`/`AgeMax` (0–120), `TimePerQuestion` (5–300), `Explanation` (0–1000), `Status` (`Draft`/`Active`/`Inactive`/`Archived`), `Answers` (exactamente 4 `AnswerOption`), `CorrectAnswerIndex` (0–3), `RowVersion`. Invariante: exactamente 4 opciones/1 correcta, categoría válida.
- **AnswerOption**: ValueObject de pregunta. Atributos: `OptionId`, `Text` (1–200), `IsCorrect` (bool), `Position` (A–D). Invariante: exactamente 1 `IsCorrect` por pregunta.
- **Category Reference**: `CategoryId` referencia a `Category` en estado no archivado; `ValidQuestionCount` derivado de preguntas `Active` con 4/1. Guarda para `CategoryNotReady` si <5 (o mínimo configurable).
- **QuestionStatistics**: Agregado de lectura: total por categoría, por dificultad, por estado, por `ValidQuestionCount`, tiempo promedio. Derivado server-side, sin cargar todo el banco.
- **Question Audit Entry**: Registro append-only: `QuestionId`, `ActorId` (sub), `Timestamp`, `Action` (`Created`/`Updated`/`Activated`/`Deactivated`/`Deleted`), `ChangedFields` (diff), `CorrelationId`, `Result`.
- **Mínimo Configurable**: Parámetro del sistema `CategoryMinQuestions` (inicial 5, configurable) que define el umbral `ValidQuestionCount` para que una categoría sea publicable/elegible para juegos.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Un ADMIN completa la creación válida de una pregunta (texto + 4 respuestas 1 correcta + categoría + atributos) en menos de 3 minutos en el 90% de los intentos desde "Crear pregunta" hasta confirmación.
- **SC-002**: El 100% de las preguntas creadas/ editadas mantienen invariante 4 opciones/1 correcta; el 100% de los intentos con 3 opciones, 0 o 2 correctas, o textos vacíos son rechazados con `InvalidQuestionData` por campo.
- **SC-003**: El 100% de las transiciones `Active ↔ Inactive` y `Delete` (cuando no está en uso) se ejecutan con éxito; el 100% de los intentos de eliminar una pregunta en uso son rechazados con `QuestionInUse` sin mutación.
- **SC-004**: El 100% de los intentos con categoría inexistente/archivada, dificultad fuera de 1–5, nivel vacío, edad invertida o tiempo fuera de 5–300 son rechazados con mensaje por campo en <2s percibidos, sin pantalla en blanco.
- **SC-005**: La pregunta persiste de forma transaccional y es reconstruible: el detalle recargado muestra exactamente los 9 campos + 4 respuestas con 1 correcta (coherencia 100% en pruebas paginadas).
- **SC-006**: La autorización se respeta en el 100% de los casos: `REWARD_MANAGER` ve `Access Denied` en "Crear/Editar/Borrar" y cualquier intento por API retorna 403 sin fuga; `ADMIN`/`GAME_MANAGER` operan sin fricción.
- **SC-007**: El formulario y listado cumplen WCAG 2.2 AA en tema `administration` (contraste, foco visible, navegación teclado, `aria-live` en errores) y son utilizables entre 375 y 1536px sin scroll horizontal y con objetivos táctiles ≥44px.
- **SC-008**: Concurrencia: bajo edición simultánea de la misma pregunta en `Draft`, uno recibe `ConcurrencyConflict` con opción de recargar y el otro persiste; no hay sobrescritura silenciosa en el 100% de las pruebas de colisión.
- **SC-009**: El listado pagina correctamente (≥100 preguntas) y filtra por categoría/dificultad/estado/búsqueda en <2s percibidos con skeleton, sin cargar colecciones completas.
- **SC-010**: El 90% de los operadores completa la tarea "crear categoría Matemáticas → crear 5 preguntas válidas (4/1) → publicar categoría → usar en juego" sin ayuda externa en el primer intento, y `MínimoConfigurable` sigue siendo 5 inicial pero es configurable sin romper categorías existentes.

## Assumptions

- **Reutiliza SPEC-017/003/020**: La app Blazor net10.0 Auto, shell de 10 secciones, BFF YARP, OIDC y `Question`/`Category` de dominio ya existen (003-question-bank con invariante 4/1, 020-admin-categories con 4 estados). 021 extiende la superficie administrativa de UI + operaciones del banco, sin crear nueva app ni duplicar autenticación.
- **Estados de pregunta**: `Draft` (borrador), `Active` (publicada y cuenta para `ValidQuestionCount`), `Inactive` (desactivada, no cuenta), `Archived`/`Deleted` (terminal). `Active` requiere 4/1 y categoría no archivada; `Inactive` es reversible.
- **Invariante 4/1**: Cada pregunta activa debe tener exactamente 4 `AnswerOption` con 1 `IsCorrect`. La base de datos impone restricción `CHECK` y el dominio valida via `IBusinessRule` (Constitución B).
- **Categoría**: Debe existir y estar en estado no archivado (`Draft`/`Active`/`Inactive`); si está `Archived`, no se permite crear/asociar preguntas. Se valida en cada guardado (FR-001/002).
- **Tiempo**: 5–300s por pregunta, configurable por pregunta (no global). Si el backend define tiempo global por juego, el tiempo de pregunta se usa como valor por defecto y se muestra con tooltip.
- **Explicación**: Texto 0–1000 opcional, mostrado tras responder (no durante el juego en `Running`). No es requerida para activar.
- **Mínimo configurable**: Parámetro del sistema `CategoryMinQuestions` con valor inicial 5 (enunciado). Es configurable sin migración de datos: cambiarlo a 3 hace que categorías con 3 ya sean publicables; subirlo a 7 hace que categorías con 5 muestren "No elegible" hasta alcanzar 7. Se expone como configuración del sistema, no como campo por categoría.
- **Estadísticas**: Agregados server-side (total por categoría, por dificultad, por estado, tiempo promedio) derivados de `GET /api/questions/stats` o agregaciones de listado paginado; la UI no carga todo el banco.
- **Edición en uso**: Una pregunta usada en juego `Running`/`Finished` puede editarse solo si se crea nueva versión o se clona; la edición directa es rechazada con `QuestionInUse` para preservar integridad histórica (variante: se permite editar si el juego está `Finished` y se audita).
- **Idioma**: Español para etiquetas, coherente con SPEC-017/020, sin i18n en v1.
- **Sin acceso directo a datos**: Todo conteo/validación via BFF; no lectura directa a SQL Server, Oracle ni `identitydb` (Constitución H).
