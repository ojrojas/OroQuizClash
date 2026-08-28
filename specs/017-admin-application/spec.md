# Feature Specification: QuizArena Administration Application

**Feature Branch**: `017-admin-application`

**Created**: 2026-08-28

**Status**: Draft

**Input**: User description: "017 — Admin Application, se debe realizar con la tecnologia net10.0 utilizar blazor web renderization automatic creando el proyecto con el comando 'dotnet new blazor -f net10.0 -ai true -int Auto -o src/Admin/QuizArena.Admin'. Objetivo: Definir la arquitectura funcional y experiencia general de la aplicación web administrativa de QuizArena. La aplicación permitirá a usuarios autorizados administrar todos los elementos necesarios para configurar y operar el sistema. Deberá proporcionar navegación hacia: Dashboard, Games, Game Configuration, Categories, Question Bank, Players, Rewards, Live Games, Reports, Audit. La aplicación deberá consumir exclusivamente los servicios/API del backend y no deberá acceder directamente a la base de datos de dominio."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Acceso seguro y navegación administrativa (Priority: P1)

Un usuario autorizado (personal de operaciones/administración) inicia sesión en la aplicación administrativa mediante el proveedor de identidad corporativo, es redirigido de vuelta a la aplicación con una sesión válida y aterriza en el Dashboard. Desde ahí puede navegar a las 10 secciones del sistema: Dashboard, Games, Game Configuration, Categories, Question Bank, Players, Rewards, Live Games, Reports y Audit. Si su sesión expira mientras trabaja, la aplicación lo redirige al flujo de autenticación sin pérdida silenciosa de contexto y sin mostrar datos a usuarios no autenticados.

**Why this priority**: Sin acceso autenticado y navegación no existe aplicación administrativa; es el esqueleto sobre el que se construyen todas las demás historias y el primer slice demostrable.

**Independent Test**: Se puede probar completamente iniciando sesión con una cuenta autorizada, verificando el aterrizaje en Dashboard, navegando a cada una de las 10 secciones y confirmando que una cuenta sin sesión válida es redirigida al proveedor de identidad.

**Acceptance Scenarios**:

1. **Given** un usuario autorizado sin sesión activa, **When** intenta acceder a cualquier sección administrativa, **Then** la aplicación lo redirige al flujo de inicio de sesión del proveedor de identidad y, tras autenticarse, regresa a la aplicación con sesión válida.
2. **Given** un usuario autenticado, **When** entra a la aplicación, **Then** aterriza en el Dashboard y ve navegación hacia las 10 secciones.
3. **Given** un usuario cuya sesión expiró, **When** intenta ejecutar cualquier acción, **Then** la aplicación renueva o reestablece la sesión vía el proveedor de identidad sin mostrar errores internos ni datos sensibles.
4. **Given** un usuario autenticado cuyo rol no incluye permisos para una sección (p. ej., recompensas), **When** intenta acceder a ella, **Then** el sistema le deniega el acceso de forma clara y la sección no le ofrece funcionalidad.
5. **Given** el proveedor de identidad indica que el usuario debe cambiar su contraseña, **When** el usuario intenta usar la aplicación, **Then** la aplicación lo canaliza al flujo de cambio de contraseña del proveedor antes de permitir operaciones.

---

### User Story 2 - Administración de juegos y su configuración (Priority: P1)

Un operador crea, consulta y administra los juegos del sistema: lista juegos existentes con filtros y paginación, crea un nuevo juego completando su configuración completa (nombre, descripción, categoría, dificultad, rondas, preguntas por ronda, límite de tiempo, jugadores mín/máx, y parámetros de entrada/recompensa), edita configuraciones en borrador, y ejecuta acciones de ciclo de vida (iniciar, cancelar, finalizar, forzar finalización) cuando corresponde. Un juego en curso protege su configuración contra cambios accidentales.

**Why this priority**: Configurar y operar juegos es la razón de ser de la aplicación administrativa; sin juegos configurados no hay producto que operar.

**Independent Test**: Se puede probar creando un juego con sus 12 campos de configuración, verificando que aparece en el listado, editándolo en borrador, y ejecutando las acciones de ciclo de vida disponibles según su estado; entrega valor porque el operador puede preparar un juego completo sin ayuda.

**Acceptance Scenarios**:

1. **Given** un operador autenticado con permisos de gestión de juegos, **When** completa el formulario de configuración con valores válidos y guarda, **Then** el juego queda creado en estado borrador y visible en el listado de juegos.
2. **Given** un formulario de configuración con valores inválidos (p. ej., límite de tiempo menor al mínimo o mínimo de jugadores mayor al máximo), **When** el operador intenta guardar, **Then** el sistema muestra errores inline específicos por campo y no crea el juego.
3. **Given** un juego en borrador, **When** el operador lo inicia, **Then** el juego transiciona a estado activo y su configuración queda protegida contra edición.
4. **Given** un juego activo, **When** el operador intenta editar su configuración, **Then** el sistema bloquea los campos y explica que el juego está en curso.
5. **Given** el listado de juegos, **When** el operador filtra por estado/categoría y pagina, **Then** los resultados se actualizan correctamente y la lista responde con tiempos de carga perceptibles bajos.
6. **Given** un juego con problemas irrecoverables, **When** un administrador ejecuta la finalización forzada con confirmación explícita, **Then** el juego termina y queda registrado para auditoría.

---

### User Story 3 - Curación de contenido: categorías y banco de preguntas (Priority: P1)

Un curador de contenido administra el catálogo de conocimiento: crea y edita categorías (área de conocimiento, nivel académico, rango de edad, dificultad, tags), las publica cuando cumplen el gate de calidad (suficientes preguntas válidas), y administra el banco de preguntas creando preguntas con exactamente 4 opciones y 1 correcta, publicándolas, activándolas/desactivándolas o archivándolas. El curador ve en todo momento el estado de cada elemento y por qué no puede publicarse algo que no cumple el gate.

**Why this priority**: Los juegos consumen categorías y preguntas; sin contenido curado y publicado los juegos no pueden existir. Es contenido habilitante del negocio.

**Independent Test**: Se puede probar creando una categoría, creando preguntas válidas asociadas hasta cumplir el gate, publicando la categoría, y verificando estados/transiciones y mensajes de gate no cumplido; entrega valor porque el catálogo de contenido queda operativo.

**Acceptance Scenarios**:

1. **Given** un curador con permisos de contenido, **When** crea una categoría con atributos válidos, **Then** la categoría queda en borrador y visible en el listado con su estado.
2. **Given** una categoría con menos preguntas válidas que el gate de publicación, **When** el curador intenta publicarla, **Then** el sistema rechaza la acción explicando cuántas preguntas válidas faltan.
3. **Given** una categoría que cumple el gate, **When** el curador la publica, **Then** queda activa y disponible para configuración de juegos.
4. **Given** el editor de preguntas, **When** el curador crea una pregunta con 4 opciones marcando exactamente 1 correcta, **Then** la pregunta queda guardada en borrador; si intenta guardar con número incorrecto de opciones o sin respuesta correcta, el sistema lo impide con errores inline.
5. **Given** una pregunta usada por un juego en curso, **When** el curador intenta modificarla, **Then** el sistema la muestra en modo solo-lectura e indica que está en uso.
6. **Given** listados de categorías o preguntas, **When** el curador busca/filtra por texto, categoría, dificultad o estado, **Then** los resultados se filtran correctamente con paginación.

---

### User Story 4 - Monitoreo en vivo y supervisión de jugadores (Priority: P2)

Un supervisor observa los juegos en curso en tiempo real: ve qué juegos están activos, cuántos jugadores participan, en qué ronda van y su estado, sin necesidad de refrescar manualmente. Puede además consultar la situación de jugadores concretos (estado en un juego, historial de consuelos) para atención operativa. La información privada de cada jugador (sus respuestas, su puntuación detallada) NO se expone en la vista administrativa; solo agregados y estados.

**Why this priority**: Operar el sistema en vivo requiere visibilidad; pero depende de que existan juegos y contenido (P1 previas).

**Independent Test**: Se puede probar con un juego activo: verificar que la vista de juegos en vivo refleja cambios de estado (inicio de ronda, fin de juego) sin recarga manual, y consultar el estado de un jugador participante; entrega valor porque el supervisor puede intervenir informadamente.

**Acceptance Scenarios**:

1. **Given** uno o más juegos activos, **When** el supervisor abre Live Games, **Then** ve cada juego con jugadores activos/total, ronda actual y estado, actualizándose automáticamente ante eventos del sistema.
2. **Given** la vista en vivo, **When** un juego termina, **Then** la fila del juego refleja el estado final sin intervención manual.
3. **Given** una conexión interrumpida temporalmente, **When** se restablece, **Then** la vista se resincroniza con el estado real del backend e indica el estado de conexión.
4. **Given** el supervisor consulta un jugador, **When** abre su detalle, **Then** ve estado de participación e historial disponible, pero nunca las respuestas individuales ni información privada de la sesión del jugador.
5. **Given** la vista en vivo, **When** el supervisor decide detener un juego por causa justificada, **Then** el sistema solicita confirmación explícita con impacto (jugadores activos) antes de ejecutar.

---

### User Story 5 - Gestión de recompensas y redenciones (Priority: P2)

Un gestor de recompensas administra el catálogo de recompensas (crear, editar, activar/desactivar) y procesa las solicitudes de redención: revisa solicitudes pendientes, las aprueba o rechaza con registro de la decisión, y marca la entrega cuando corresponde. Ve el historial completo de redenciones con su estado.

**Why this priority**: Las recompensas son el incentivo del jugador y tienen implicaciones financieras; requieren supervisión humana, pero el sistema opera sin ellas en etapas iniciales.

**Independent Test**: Se puede probando creando una recompensa, activándola, y procesando una solicitud de redención de punta a punta (aprobar → entregar, o rechazar); entrega valor porque el ciclo de recompensas queda operable y auditado.

**Acceptance Scenarios**:

1. **Given** un gestor con permisos de recompensas, **When** crea y activa una recompensa, **Then** queda disponible en el catálogo con su estado visible.
2. **Given** una solicitud de redención pendiente, **When** el gestor la aprueba y luego la marca como entregada, **Then** el estado queda actualizado y registrado para auditoría.
3. **Given** una solicitud de redención, **When** el gestor la rechaza, **Then** el sistema registra la decisión y la solicitud no puede volver a procesarse.
4. **Given** el historial de redenciones, **When** el gestor filtra por estado, **Then** ve exactamente las solicitudes en ese estado con paginación.
5. **Given** un usuario sin permisos de recompensas, **When** intenta acceder a la sección Rewards, **Then** el acceso le es denegado.

---

### User Story 6 - Visibilidad operativa: Dashboard, reportes y auditoría (Priority: P2)

Un responsable de operación abre el Dashboard y ve de un vistazo los indicadores clave (juegos activos, jugadores, estado del banco de contenido, recompensas pagadas). Cuando necesita análisis más profundo, genera reportes por juego, categoría, pregunta, jugador y recompensas. Para cumplimiento y resolución de incidentes, consulta el registro de auditoría inmutable: quién hizo qué y cuándo, con filtros por actor/acción/fecha, sin posibilidad de editar o borrar entradas.

**Why this priority**: La visibilidad cierra el ciclo operativo pero consume datos generados por las operaciones P1/P2 previas.

**Independent Test**: Se puede probar verificando que el Dashboard muestra indicadores coherentes con el estado del sistema, generando cada tipo de reporte y consultando entradas de auditoría con filtros; entrega valor porque operaciones y cumplimiento tienen visibilidad completa.

**Acceptance Scenarios**:

1. **Given** un usuario autenticado, **When** abre el Dashboard, **Then** ve indicadores clave del sistema con datos reales y estados de carga/error manejados.
2. **Given** la sección Reports, **When** el usuario selecciona un tipo de reporte (juego, categoría, pregunta, jugador, recompensas, leaderboard) y un período, **Then** obtiene los datos correspondientes y puede exportarlos cuando aplique.
3. **Given** la sección Audit, **When** el usuario filtra por actor, acción o rango de fechas, **Then** obtiene las entradas coincidentes ordenadas cronológicamente.
4. **Given** cualquier entrada de auditoría, **When** el usuario intenta modificarla o eliminarla, **Then** el sistema no ofrece ninguna opción de mutación (registro inmutable).
5. **Given** un período sin datos, **When** el usuario consulta Dashboard o reportes, **Then** ve estados vacíos claros con acciones sugeridas, no errores.

---

### Edge Cases

- ¿Qué ocurre cuando la API de backend no está disponible? La aplicación muestra un estado de error claro con opción de reintento en cada sección afectada; nunca una pantalla en blanco ni detalles técnicos internos.
- ¿Qué ocurre cuando dos operadores editan la misma configuración simultáneamente? El segundo en guardar recibe un conflicto explícito y decide si recargar o sobrescribir según la regla de negocio del backend.
- ¿Qué ocurre con listas muy grandes (cientos/miles de juegos, preguntas, auditoría)? Todas las listas usan paginación y filtros; ninguna carga la colección completa.
- ¿Qué ocurre cuando un juego pasa a "en vivo" mientras un operador lo edita? La pantalla detecta el cambio de estado, bloquea los campos y notifica.
- ¿Qué ocurre si el token expira durante una operación de escritura? La operación falla de forma controlada, la sesión se renueva y el usuario puede reintentar sin datos corruptos.
- ¿Qué ocurre con acciones destructivas (cancelar juego, forzar finalización, rechazar redención)? Siempre requieren confirmación explícita con descripción del impacto.
- ¿Qué ocurre cuando el proveedor de identidad fuerza el cierre de sesión del usuario? La aplicación termina la sesión local y redirige al flujo de autenticación.
- ¿Qué ocurre si un reporte tarda en generarse? La aplicación muestra un estado de procesamiento sin bloquear la navegación.

## Requirements *(mandatory)*

### Functional Requirements

**Acceso y autorización**

- **FR-001**: La aplicación MUST autenticar usuarios exclusivamente vía el proveedor de identidad externo (flujo de redirección estándar con tokens de acceso y renovación); MUST NOT existir formulario de login propio ni almacén local de credenciales.
- **FR-002**: La aplicación MUST requerir sesión válida para acceder a cualquier sección; el acceso anónimo MUST estar prohibido.
- **FR-003**: La aplicación MUST aplicar autorización por rol: administración total (ADMIN), gestión de juegos/contenido/operación (GAME_MANAGER) y gestión de recompensas (REWARD_MANAGER), mostrando y permitiendo únicamente las secciones y acciones permitidas para el rol del usuario.
- **FR-004**: La aplicación MUST manejar el caso de "cambio de contraseña obligatorio" señalado por el proveedor de identidad, canalizando al usuario al flujo correspondiente antes de permitir operaciones.
- **FR-005**: La aplicación MUST renovar sesiones vencidas de forma transparente y, si no es posible, redirigir al flujo de autenticación preservando la seguridad.

**Navegación y experiencia general**

- **FR-006**: La aplicación MUST proporcionar navegación persistente hacia las 10 secciones: Dashboard, Games, Game Configuration, Categories, Question Bank, Players, Rewards, Live Games, Reports, Audit.
- **FR-007**: La aplicación MUST seguir el Design System compartido (SPEC-016): expresión "Administration" (tema claro, denso, profesional), tokens de diseño sin valores hardcodeados, y componentes según catálogo.
- **FR-008**: La aplicación MUST ser utilizable en escritorios (1024–1440px como objetivo principal) y MUST adaptarse responsivamente entre 375 y 1536px sin scroll horizontal, según SPEC-016.
- **FR-009**: La aplicación MUST cumplir accesibilidad WCAG 2.2 AA (contraste, teclado, estados de foco, sin dependencia exclusiva del color) según SPEC-016.
- **FR-010**: Toda pantalla interactiva MUST declarar y manejar estados de Carga, Listo, Vacío y Error (SPEC-016 matriz de estados).

**Gestión de juegos**

- **FR-011**: La aplicación MUST permitir listar juegos con filtros (estado, categoría) y paginación.
- **FR-012**: La aplicación MUST permitir crear un juego completando su configuración completa (nombre, descripción, categoría, dificultad, rondas, preguntas por ronda, límite de tiempo, jugadores mínimo/máximo, y parámetros de entrada y bolsa de recompensas) con validación inline de todos los campos.
- **FR-013**: La aplicación MUST permitir editar la configuración de juegos mientras estén en estado editable, y MUST bloquear la edición cuando el juego esté en curso, explicando el motivo.
- **FR-014**: La aplicación MUST exponer las acciones de ciclo de vida del juego disponibles según su estado (iniciar, cancelar, finalizar, forzar finalización), requiriendo confirmación explícita para acciones destructivas.
- **FR-015**: La aplicación MUST mostrar el detalle de un juego incluyendo sus rondas, puntuaciones agregadas y leaderboard público.

**Curación de contenido**

- **FR-016**: La aplicación MUST permitir crear, editar, listar, filtrar y paginar categorías con sus atributos (área, nivel académico, rango de edad, dificultad, tags).
- **FR-017**: La aplicación MUST ejecutar las transiciones de categoría (publicar, activar, desactivar, archivar) respetando el gate de publicación del backend y mostrando el motivo cuando el gate no se cumple.
- **FR-018**: La aplicación MUST permitir crear, editar, listar, filtrar y paginar preguntas con exactamente 4 opciones y exactamente 1 correcta, con validación inline.
- **FR-019**: La aplicación MUST ejecutar las transiciones de pregunta (publicar, activar, desactivar, archivar) y MUST mostrar en solo-lectura las preguntas en uso por juegos activos.

**Jugadores y monitoreo en vivo**

- **FR-020**: La aplicación MUST mostrar los juegos en curso con jugadores activos/total, ronda actual y estado, actualizándose automáticamente vía eventos en tiempo real del backend sin recarga manual.
- **FR-021**: La aplicación MUST permitir consultar el estado de participación de un jugador y su historial de consuelos.
- **FR-022**: La aplicación MUST NOT mostrar información privada de la sesión de un jugador (respuestas individuales, detalle de su temporizador o retiros); solo información pública/agregada.
- **FR-023**: La aplicación MUST indicar el estado de conexión en tiempo real y resincronizarse automáticamente tras una reconexión.

**Recompensas**

- **FR-024**: La aplicación MUST permitir crear, editar, activar y desactivar recompensas del catálogo.
- **FR-025**: La aplicación MUST permitir procesar solicitudes de redención: listar pendientes, aprobar, rechazar, cancelar y marcar entregadas, con registro de cada decisión.
- **FR-026**: La aplicación MUST mostrar el historial de redenciones con filtros por estado y paginación.

**Visibilidad operativa**

- **FR-027**: La aplicación MUST mostrar un Dashboard con indicadores clave del sistema (juegos activos, participación, estado del contenido, recompensas) derivados de los servicios del backend.
- **FR-028**: La aplicación MUST proporcionar reportes por juego, categoría, pregunta, jugador, recompensas y leaderboard, con selección de período cuando aplique.
- **FR-029**: La aplicación MUST proporcionar consulta del registro de auditoría con filtros por actor, acción y rango de fechas, en modo estrictamente solo-lectura.

**Integración y arquitectura**

- **FR-030**: La aplicación MUST consumir exclusivamente los servicios/API del backend para toda lectura y escritura de datos; MUST NOT acceder directamente a la base de datos de dominio ni a ningún almacén de datos fuera de la API.
- **FR-031**: La aplicación MUST tratar los errores de negocio del backend (códigos de error explícitos) como mensajes accionables para el usuario, sin filtrar detalles internos.
- **FR-032**: La aplicación MUST enviar credenciales de autorización en cada petición al backend y MUST manejar las denegaciones de acceso (sesión inválida o permisos insuficientes) con los flujos apropiados de re-autenticación o denegación clara.

### Key Entities *(include if feature involves data)*

Entidades vistas desde la aplicación administrativa (proyecciones de los datos del backend, sin lógica de negocio propia):

- **Game (Juego)**: identidad, nombre, categoría, estado del ciclo de vida, rondas configuradas, jugadores, fechas; configurable mientras es editable.
- **Game Configuration**: parámetros operativos del juego (rondas, preguntas por ronda, límite de tiempo, jugadores mín/máx, entrada, bolsa de recompensas).
- **Category (Categoría)**: atributos de conocimiento (área, nivel, edad, dificultad, tags), estado de publicación, conteo de preguntas válidas para el gate.
- **Question (Pregunta)**: enunciado, 4 opciones con 1 correcta, dificultad, estado de publicación, categoría asociada.
- **Player Status**: estado de participación de un jugador en un juego e historial de consuelos (solo datos públicos/agregados).
- **Reward (Recompensa)**: descripción, valor, estado del catálogo.
- **Redemption (Redención)**: solicitud de canje con estado (pendiente/aprobada/rechazada/entregada/cancelada) y decisiones registradas.
- **Report Summary**: agregados por juego/categoría/pregunta/jugador/recompensa/leaderboard para un período.
- **Audit Entry**: registro inmutable de actor, acción, entidad afectada y momento.
- **Dashboard KPIs**: indicadores agregados del estado del sistema.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Un operador completa la creación de un juego (todos los campos de configuración) en menos de 3 minutos desde el Dashboard.
- **SC-002**: El 100% de las 10 secciones es alcanzable en máximo 2 interacciones desde el inicio de sesión.
- **SC-003**: Cero accesos directos a base de datos desde la aplicación administrativa (verificable por inspección/proof arquitectónico automatizado).
- **SC-004**: El 90% de los flujos de creación y publicación de contenido (categoría + preguntas) se completa con éxito en el primer intento por usuarios de prueba.
- **SC-005**: La vista de juegos en vivo refleja cambios de estado del backend en menos de 5 segundos sin recarga manual.
- **SC-006**: Las 10 secciones son completamente utilizables en 1440px y 1024px, y ninguna presenta scroll horizontal entre 375px y 1536px.
- **SC-007**: El 100% de las pantallas pasa la verificación de accesibilidad AA (contraste, navegación por teclado, foco visible) en el tema claro administrativo.
- **SC-008**: El 100% de los intentos de acceso sin sesión válida o sin rol adecuado resulta en denegación sin fuga de datos.
- **SC-009**: Los operadores de prueba reportan una puntuación de usabilidad (SUS) ≥ 75 tras completar los flujos críticos.
- **SC-010**: Los listados con 100+ elementos se perciben cargados en menos de 2 segundos gracias a paginación y estados de carga.
- **SC-011**: Todas las acciones destructivas (cancelar/forzar final de juego, rechazar redención) requieren confirmación explícita en el 100% de los casos.

## Assumptions

- **Plataforma (mandato explícito del usuario)**: la aplicación se construye con .NET (`net10.0`) usando Blazor con renderizado/interactividad automática, creada mediante `dotnet new blazor -f net10.0 -ai true -int Auto -o src/Admin/QuizArena.Admin`. Este mandato prevalece sobre referencias previas de SPEC-016 a versiones posteriores del framework.
- **Backend existente**: la API de `QuizArena.Api` (SPEC-001..015) ya expone los endpoints de juegos, categorías, preguntas, recompensas, redenciones, reportes, auditoría y eventos en tiempo real; la aplicación administrativa los consume sin modificar la lógica de negocio (el backend permanece como autoridad).
- **Identidad externa**: OroIdentityServer provee login, cambio de contraseña y logout; la aplicación administrativa nunca reimplementa esas pantallas, solo redirige y maneja el retorno y las claims de autorización.
- **Roles y permisos**: se reutiliza el mapeo de roles de la constitución (ADMIN, GAME_MANAGER, REWARD_MANAGER); la gestión de cuentas/roles de usuario pertenece a la UI administrativa del propio proveedor de identidad y está fuera de alcance de esta aplicación.
- **Sección Players**: dado que el backend expone estado de jugador por juego e historial de consuelos (no un listado global de usuarios), la sección Players se resuelve con vistas orientadas a participación (jugadores por juego, estado, consuelos); la administración de identidades de jugadores queda fuera de alcance.
- **Dashboard**: los indicadores se derivan de los endpoints de reportes existentes; si algún KPI no tiene fuente directa, se usa el agregado disponible más cercano documentándolo en el plan.
- **Alcance excluido**: aplicación del jugador (SPEC-027+), creación/edición de reglas de negocio (viven en el dominio del backend), y cualquier acceso a datos que no sea vía API.
- **Auditoría**: el registro de auditoría es generado por el backend; la aplicación administrativa solo lo consulta.
- **Idioma de la interfaz**: español como idioma primario de la UI administrativa (coherente con la documentación del proyecto), sin requerimiento de multi-idioma en esta versión.
