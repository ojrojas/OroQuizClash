# Feature Specification: Admin Categories

**Feature Branch**: `020-admin-categories`

**Created**: 2026-08-28

**Status**: Draft

**Input**: User description: "020 — Admin Categories Objetivo Administrar las categorías de conocimiento utilizadas por los juegos. Descripción Cada categoría deberá permitir configurar: Nombre. Descripción. Área de conocimiento. Nivel académico. Rango de edad. Dificultad. Público objetivo. Estado. Metadatos. Reglas de progresión. Ejemplos: Matemáticas, Historia, Ciencia, Tecnología, Geografía, Literatura, Programación, Finanzas"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Crear y gestionar categorías base (Priority: P1)

Como administrador (ADMIN) o gestor de juegos (GAME_MANAGER) autenticado, quiero crear una categoría de conocimiento definiendo los 10 atributos configurables (nombre, descripción, área de conocimiento, nivel académico, rango de edad, dificultad, público objetivo, estado, metadatos, reglas de progresión) para que pueda ser usada al configurar juegos y preguntas.

**Why this priority**: Es el núcleo de 020 — sin categorías no hay banco de preguntas ni juegos configurables (Constitución B: categoría debe existir y estar Active con ≥5 preguntas). Constituye el MVP estricto y desbloquea 019/003.

**Independent Test**: Login ADMIN → /admin/categories → "Crear categoría" → completar 10 campos válidos (nombre 3–100, área 2–100, nivel 2–100, edad 0–120, dificultad 1–5, etc.) → guardar → verificar categoría aparece en listado con estado `Draft` y valores persistidos. No requiere publicar ni asignar preguntas.

**Acceptance Scenarios**:

1. **Given** un ADMIN en creación, **When** completa campos obligatorios con valores válidos (nombre 3–100, área 2–100, nivel 2–100, AgeMin/AgeMax 0–120 con min≤max, dificultad 1–5, público objetivo seleccionado) y guarda, **Then** el sistema crea la categoría en `Draft` y muestra confirmación con ID y resumen.
2. **Given** un intento con nombre duplicado exacto (case-insensitive) existente en estado no archivado, **When** guarda, **Then** el sistema rechaza con `CategoryAlreadyExists` y señala el campo nombre.
3. **Given** un intento con AgeMin 20, AgeMax 10 (rango invertido) o dificultad 0/6, **When** guarda, **Then** el sistema rechaza con `InvalidCategoryData` por campo y no crea la categoría.
4. **Given** una categoría en `Draft` con 10 atributos válidos, **When** se recarga la edición, **Then** todos los campos muestran valores guardados y son editables mientras no esté `Archived`.
5. **Given** un REWARD_MANAGER (sin permiso `Category.Write`), **When** intenta acceder a "Crear categoría", **Then** ve `Access Denied` y no puede crear/editar.

---

### User Story 2 - Publicar, organizar y dar vida a la categoría (Priority: P1)

Como administrador, quiero llevar una categoría de `Draft → Active` (publicar), y luego `Active ↔ Inactive → Archived`, asignando preguntas y viendo el cumplimiento del umbral ≥5 preguntas válidas (4 opciones/1 correcta), para que la categoría sea elegible para juegos y se pueda filtrar/buscar entre 8 categorías de ejemplo (Matemáticas, Historia, Ciencia, etc.).

**Why this priority**: El objetivo incluye Estado y la Constitución B exige ≥5 preguntas válidas antes de publicación. Sin publicar, la categoría no sirve para configurar juegos (FR-001 fallaría en 019). Co-prioritario con US1 para valor operacional.

**Independent Test**: Crear categoría en `Draft` → añadir 5 preguntas válidas (4 opciones/1 correcta, nivel/edad coherentes) → publicar → verificar `Active`; intentar publicar con 4 preguntas → rechazo `CategoryNotReady`; archivar (`Active → Archived`) y verificar que no aparece para nuevos juegos pero permanece para históricos.

**Acceptance Scenarios**:

1. **Given** una categoría en `Draft` con 5 preguntas válidas (validadas por `Question`), **When** el ADMIN ejecuta "Publicar", **Then** transita a `Active` y aparece como elegible en el selector de categoría al crear juegos y preguntas.
2. **Given** una categoría en `Draft` con 4 preguntas válidas, **When** intenta publicar, **Then** el sistema rechaza con `CategoryNotReady` indicando faltan 1 y no transita.
3. **Given** una categoría en `Active`, **When** el ADMIN ejecuta "Desactivar", **Then** transita a `Inactive` y deja de ser elegible para nuevos juegos/preguntas, pero mantiene preguntas existentes.
4. **Given** una categoría en `Active`/`Inactive` sin juegos activos asociados, **When** el ADMIN ejecuta "Archivar", **Then** transita a `Archived` (terminal visible solo con filtro `Archived`); si tiene juegos en `Running`/`Scheduled`, el archivado es rechazado con `CategoryInUse`.
5. **Given** el listado de categorías con 8 ejemplos (Matemáticas, Historia, Ciencia, Tecnología, Geografía, Literatura, Programación, Finanzas), **When** el ADMIN filtra por área "Ciencia" o busca "Matemáticas", **Then** el listado pagina y filtra correctamente (<2s, skeleton, sin cargar todo).

---

### User Story 3 - Configurar metadatos, público objetivo y reglas de progresión (Priority: P2)

Como administrador avanzado, quiero definir metadatos (tags, color, icono), público objetivo y reglas de progresión por categoría con validación y feedback por campo, para que la progresión de dificultad y la segmentación por edad/nivel sean explícitas y auditables.

**Why this priority**: Eleva la categoría de "nombre + área" a "motor configurable" (Constitución C: progresión via strategy). Depende de US1/US2 y es P2 porque el valor base ya se entregó.

**Independent Test**: Editar categoría en `Draft`/`Active` → definir `Público objetivo` (p. ej., "Estudiantes Secundaria"), `Metadatos` (tags ≤10, 2–30 chars, color hex opcional, icono Lucide), `Reglas de progresión` (`Linear`/`Progressive`/`Adaptive`/`CategorySpecific`) → guardar con éxito; luego intentar tags inválidos o progresión fuera de catálogo → ver errores por campo; verificar que tras `Archived` es solo lectura.

**Acceptance Scenarios**:

1. **Given** una categoría en `Draft`, **When** selecciona `Público objetivo` (texto 2–100, p. ej., "Profesionales Finanzas") y guarda, **Then** el sistema persiste el público y lo muestra en el detalle.
2. **Given** `Metadatos` con tags ["álgebra", "cálculo"] y color `#2563EB`, **When** guarda, **Then** el sistema valida tags (≤10, 2–30, sin duplicados case-insensitive) y color hex 6 dígitos; si viola, rechaza con `InvalidCategoryData` por campo.
3. **Given** `Reglas de progresión` seleccionada, **When** guarda, **Then** solo se permiten `Linear`, `Progressive`, `Adaptive`, `CategorySpecific`; valores fuera de catálogo son rechazados.
4. **Given** una categoría con `Nivel académico` "Universitario" y `Rango edad` 18–25, **When** crea una pregunta asociada, **Then** la pregunta hereda/valida coherencia con esos rangos (si la pregunta define 10–12, es advertencia pero no bloqueo en MVP — documentado en UI).
5. **Given** una categoría en `Archived`, **When** intenta editar metadatos o reglas, **Then** el formulario es solo lectura y el intento por API es rechazado con `InvalidCategoryState`.

---

### Edge Cases

- ¿Qué ocurre si dos ADMIN crean "Matemáticas" simultáneamente con mismo nombre? El segundo recibe `CategoryAlreadyExists` (índice único case-insensitive entre no archivadas) sin sobrescritura.
- ¿Qué ocurre si se edita `Área de conocimiento` de una categoría `Active` que ya tiene 10 preguntas? Permitido; si el área cambia, las preguntas existentes mantienen su categoría pero muestran advertencia de desalineación hasta revalidación.
- ¿Qué ocurre si `Rango de edad` se cambia a 0–5 para categoría "Programación" con preguntas de nivel "Avanzado"? Guardado permitido pero el sistema advierte "Rango/nivel incoherente" sin bloquear (validación de negocio, no invariante).
- ¿Qué ocurre si se intenta archivar una categoría con juegos en `Running`/`Scheduled`? Rechazado con `CategoryInUse` y lista de juegos bloqueantes.
- ¿Qué ocurre si `Metadatos` incluye 11 tags o tag de 1 char? Rechazado con `InvalidCategoryData` por campo, sin persistencia parcial.
- ¿Qué ocurre si el ADMIN pierde sesión mientras edita? El guardado falla con 401, el formulario conserva borrador local y muestra "Sesión expirada — re-autenticar" sin pérdida de datos ingresados.
- ¿Qué ocurre con concurrencia (dos ADMIN editando misma categoría `Draft`)? `rowversion` detecta conflicto y uno recibe `ConcurrencyConflict` con opción de recargar.

## Requirements *(mandatory)*

### Functional Requirements

**Creación y definición (10 campos)**

- **FR-001**: El sistema MUST permitir crear una categoría con `Nombre` (3–100, requerido, único case-insensitive entre no archivadas), `Descripción` (0–500, opcional), `Área de conocimiento` (2–100, requerida, ej. Matemáticas, Ciencia, Tecnología...), `Nivel académico` (2–100, requerido) y `Dificultad` (1–5, requerida) y `Público objetivo` (2–100, requerido) con validación por campo.
- **FR-002**: El sistema MUST permitir definir `Rango de edad` (`AgeMin`/`AgeMax` enteros 0–120, con `AgeMin ≤ AgeMax`, requeridos) y validar coherencia opcional con `Nivel académico` (advertencia, no bloqueo en MVP).
- **FR-003**: El sistema MUST permitir definir `Estado` como uno de `Draft`, `Active`, `Inactive`, `Archived` con estado inicial `Draft` y transiciones controladas (ver FR-005), y `Metadatos` (tags 0–10, 2–30 chars c/u sin duplicados case-insensitive, color hex opcional `#RRGGBB`, icono Lucide opcional) con validación por campo.
- **FR-004**: El sistema MUST permitir definir `Reglas de progresión` (requerida, uno de `Linear`, `Progressive`, `Adaptive`, `CategorySpecific`) con catálogo cerrado y validado en dominio.
- **FR-005**: El sistema MUST aplicar la máquina de estados: `Draft → Active` (requiere ≥5 preguntas válidas), `Active ↔ Inactive`, `Active/Inactive → Archived` (terminal); `Archived` no es elegible para nuevos juegos/preguntas; toda transición inválida MUST ser rechazada con `InvalidCategoryState` y sin mutación parcial, protegida por `rowversion`.

**Edición, validación y elegibilidad**

- **FR-006**: El sistema MUST permitir editar los 10 campos mientras la categoría está en `Draft`, `Active` o `Inactive`; al alcanzar `Archived` los campos MUST volverse inmutables (solo lectura en UI y rechazados por API).
- **FR-007**: El sistema MUST validar en tres niveles: API (contrato), Aplicación (requisitos — unicidad nombre, área/nivel 2–100, edad 0–120, dificultad 1–5, tags, progresión en catálogo, `Archived` con `CategoryInUse`), y Dominio (invariantes — ≥5 preguntas para `Active`, tags, `rowversion`). Los invariantes MUST NOT depender solo de UI.
- **FR-008**: El sistema MUST exponer `ValidQuestionCount` (preguntas con 4 opciones/1 correcta y estado `Active`) y usarlo como guarda para `Draft → Active`; si <5, publicar es rechazado con `CategoryNotReady` indicando faltantes.
- **FR-009**: El sistema MUST mostrar errores por campo con códigos accionables (`CategoryAlreadyExists`, `CategoryNotReady`, `CategoryInUse`, `InvalidCategoryData`, `InvalidCategoryState`, `ConcurrencyConflict`) y MUST preservar borrador local en caso de 401 sin pérdida de datos hasta re-autenticar.

**Autorización y auditoría**

- **FR-010**: El sistema MUST restringir creación/edición/publicación/archivado a roles `ADMIN` y `GAME_MANAGER` (política `AdminOrGameManager`); `REWARD_MANAGER` y `PLAYER` MUST recibir `Access Denied` en UI y 403 por API sin fuga. `OroIdentityServer` es la única autoridad (Constitución VI).
- **FR-011**: El sistema MUST auditar de forma append-only cada creación, modificación y transición de estado (actor `sub`, timestamp UTC, `CategoryId`, estado anterior/nuevo, diff de campos clave, `CorrelationId`) sin mutar historial.
- **FR-012**: El sistema MUST propagar `CorrelationId` y mapear `Result` → HTTP (`ProblemDetails` RFC 7807) sin exponer detalles internos.

**Integración y presentación**

- **FR-013**: El sistema MUST consumir exclusivamente la API/BFF (`QuizArena.Api` via `QuizArena.Admin` BFF) para todos los datos de categorías; MUST NOT acceder directamente a SQL Server/Oracle/`identitydb`.
- **FR-014**: El sistema MUST reutilizar el shell de navegación, tema `administration`, tokens y componentes del Design System (SPEC-016) sin valores hardcodeados y MUST residir en `src/Admin/QuizArena.Admin` (Blazor Auto net10.0) y `src/Admin/QuizArena.Admin.Client`.
- **FR-015**: El sistema MUST exigir sesión válida via `OroIdentityServer` (OIDC `authorization_code` + `refresh_token`) y manejar `must_change_password` y expiración antes de permitir administrar categorías.
- **FR-016**: El sistema MUST listar categorías con paginación y filtros por estado, área de conocimiento y búsqueda por nombre, y MUST ofrecer detalle con `ValidQuestionCount` y historial de transiciones, indicando elegibilidad para juegos.

### Key Entities *(include if feature involves data)*

- **Category**: Agregado de conocimiento. Atributos: `CategoryId`, `Name` (3–100, único), `Description` (0–500), `KnowledgeArea` (2–100, ej. Matemáticas, Historia, Ciencia...), `AcademicLevel` (2–100), `AgeMin`/`AgeMax` (0–120), `Difficulty` (1–5), `TargetAudience` (2–100), `Status` (`Draft`/`Active`/`Inactive`/`Archived`), `Metadata` (tags, color, icono), `ProgressionRule` (`Linear`/`Progressive`/`Adaptive`/`CategorySpecific`), `ValidQuestionCount` (derivado), `RowVersion`. Invariante: `Active` requiere ≥5 preguntas válidas, `Archived` inmutable.
- **Category State Machine**: Estados `Draft → Active ↔ Inactive → Archived` con guardas (`Active` requiere ≥5 válidas, `Archived` rechaza si tiene juegos en `Running`/`Scheduled`). Protegida por `rowversion`.
- **Question Reference**: `Question` con 4 opciones/1 correcta, estado `Active`, pertenece a `Category` `Active`. Contribuye a `ValidQuestionCount` (Constitución B).
- **Game Reference**: `Game` en estados `Running`/`Scheduled` que referencia `CategoryId`; bloquea `Archived` si existe ( `CategoryInUse` ).
- **Category Audit Entry**: Registro append-only: `CategoryId`, `ActorId` (sub), `Timestamp`, `FromState`, `ToState`, `ChangedFields` (diff), `CorrelationId`, `Result`.
- **ProgressionRule**: Enumeración de dominio mapeada desde `Reglas de progresión` y validada contra catálogo Constitución C.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Un ADMIN completa la creación válida de una categoría (10 campos) en menos de 2 minutos en el 90% de los intentos desde "Crear categoría" hasta confirmación `Draft`.
- **SC-002**: El 100% de las publicaciones `Draft → Active` con ≥5 preguntas válidas se ejecutan con éxito y se vuelven elegibles en selectores; el 100% con <5 son rechazadas con `CategoryNotReady` sin mutación.
- **SC-003**: El 100% de las ediciones tras `Archived` son bloqueadas en UI (solo lectura) y rechazadas por API con `InvalidCategoryState`; `Active ↔ Inactive` funciona en el 100% de los casos válidos.
- **SC-004**: El 100% de los intentos con nombre duplicado, área/nivel inválidos, edad invertida o tags inválidos son rechazados con mensaje por campo en <2s percibidos, sin pantalla en blanco.
- **SC-005**: La categoría persiste de forma transaccional y es reconstruible: el detalle recargado muestra exactamente los 10 valores guardados (coherencia 100% en pruebas paginadas).
- **SC-006**: La autorización se respeta en el 100% de los casos: `REWARD_MANAGER` ve `Access Denied` en "Crear/Editar/Publicar" y cualquier intento por API retorna 403 sin fuga; `ADMIN`/`GAME_MANAGER` operan sin fricción.
- **SC-007**: El formulario y listado cumplen WCAG 2.2 AA en tema `administration` (contraste, foco visible, navegación teclado, `aria-live` en errores) y son utilizables entre 375 y 1536px sin scroll horizontal y con objetivos táctiles ≥44px.
- **SC-008**: Concurrencia: bajo edición simultánea de la misma categoría en `Draft`, uno recibe `ConcurrencyConflict` con opción de recargar y el otro persiste; no hay sobrescritura silenciosa en el 100% de las pruebas de colisión.
- **SC-009**: El listado pagina correctamente (≥50 categorías con 8 ejemplos) y filtra por estado/área/búsqueda en <2s percibidos con skeleton, sin cargar colecciones completas.
- **SC-010**: El 90% de los operadores completa la tarea "crear categoría Matemáticas → añadir 5 preguntas válidas → publicar → usar en juego" sin ayuda externa en el primer intento.

## Assumptions

- **Reutiliza SPEC-017/002**: La app Blazor net10.0 Auto, shell de 10 secciones, BFF YARP, OIDC y `Category` de dominio ya existen (002-categories con invariantes B: 4 opciones/1 correcta, ≥5 preguntas). 020 extiende la superficie administrativa de UI + estados de categoría, sin crear nueva app ni duplicar autenticación.
- **Estados**: `Draft` (borrador), `Active` (publicada y elegible), `Inactive` (pausada, no elegible para nuevos juegos), `Archived` (terminal, solo lectura, no elegible). `Active` requiere ≥5 preguntas válidas; `Archived` bloquea edición y es terminal.
- **Unicidad**: `Name` único case-insensitive entre categorías no archivadas para evitar confusión en selectores; duplicados entre archivadas se permiten.
- **Área de conocimiento**: Texto libre 2–100 con ejemplos del enunciado (Matemáticas, Historia, Ciencia, Tecnología, Geografía, Literatura, Programación, Finanzas) — no catálogo cerrado en MVP, validado como texto.
- **Metadatos**: `tags` 0–10, 2–30 chars, sin duplicados case-insensitive; `color` hex opcional `#RRGGBB`; `icono` Lucide opcional. Si el backend no soporta metadatos ricos, se persiste como JSON y se muestra con tooltip.
- **Público objetivo**: Texto 2–100 (ej. "Estudiantes Secundaria", "Profesionales Finanzas") — no enumeración cerrada en MVP.
- **Reglas de progresión**: Uno de `Linear`, `Progressive`, `Adaptive`, `CategorySpecific` (Constitución C); valores fuera de catálogo rechazados.
- **Rango edad / Nivel académico**: Validación de rango 0–120 y `AgeMin ≤ AgeMax`; coherencia con nivel es advertencia, no bloqueo en MVP (documentado en UI).
- **Idioma**: Español para etiquetas, coherente con SPEC-017/019, sin i18n en v1.
- **Sin acceso directo a datos**: Todo conteo/validación via BFF; no lectura directa a SQL Server, Oracle ni `identitydb` (Constitución H).
