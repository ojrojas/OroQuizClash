# Feature Specification: Admin Dashboard

**Feature Branch**: `018-admin-dashboard`

**Created**: 2026-08-28

**Status**: Draft

**Input**: User description: "018 — Admin Dashboard Objetivo Proporcionar una vista operacional del estado global de QuizArena. Descripción El dashboard deberá mostrar información resumida sobre: Juegos activos. Juegos programados. Juegos finalizados. Jugadores conectados. Jugadores activos. Preguntas disponibles. Categorías. Premios. Canjes. Estadísticas generales. También deberá permitir acceder rápidamente a: Crear juego, Configurar juego, Gestionar preguntas, Ver juegos activos, Ver jugadores, Gestionar premios, Consultar reportes"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Vista operacional resumida del sistema (Priority: P1)

Como administrador u operador autenticado, quiero abrir el Dashboard y ver de un vistazo el estado global de QuizArena — conteos de juegos por estado, jugadores, contenido y recompensas — para decidir qué acción tomar sin navegar sección por sección.

**Why this priority**: Es la primera pantalla tras el login y la promesa central de 018: convertir 10 consultas dispersas en un snapshot operacional en segundos. Sin este slice no hay "dashboard". Es el MVP estricto.

**Independent Test**: Entrar autenticado al Dashboard y verificar que cada uno de los 10 bloques (activos/programados/finalizados, conectados/activos, preguntas/categorías/premios/canjes/estadísticas) muestra un valor coherente con el backend, con estados de carga/error/vacío correctos. No requiere usar los accesos rápidos.

**Acceptance Scenarios**:

1. **Given** un usuario autenticado con rol ADMIN o GAME_MANAGER, **When** abre el Dashboard, **Then** ve 10 tarjetas/indicadores: Juegos activos, Juegos programados, Juegos finalizados, Jugadores conectados, Jugadores activos, Preguntas disponibles, Categorías, Premios, Canjes y Estadísticas generales, cada uno con valor numérico y etiqueta clara.
2. **Given** el backend no tiene datos para un indicador (p. ej., 0 juegos programados), **When** se renderiza el bloque, **Then** muestra "0" con estado Vacío informativo (no error) y texto sugerido.
3. **Given** el backend tarda o falla para un indicador, **When** se carga el Dashboard, **Then** ese bloque muestra estado de Carga (skeleton) y luego Error con botón Reintentar aislado, sin bloquear los demás bloques.
4. **Given** el Dashboard cargado, **When** el usuario permanece 60 segundos sin recargar, **Then** los valores siguen reflejando la última lectura válida y ofrecen actualización manual (auto-refresh opcional pero no obligatorio para P1).
5. **Given** un usuario con rol REWARD_MANAGER (sin permisos de juegos), **When** abre el Dashboard, **Then** ve solo los bloques que le competen (al menos Premios/Canjes/Estadísticas generales) o ve los demás enmascarados con mensaje de permiso, sin fuga de datos.

---

### User Story 2 - Accesos rápidos a operaciones críticas (Priority: P1)

Como operador, quiero desde el Dashboard ejecutar con un clic las 7 acciones más frecuentes para no perder tiempo navegando: Crear juego, Configurar juego, Gestionar preguntas, Ver juegos activos, Ver jugadores, Gestionar premios y Consultar reportes.

**Why this priority**: El objetivo explícito incluye "permitir acceder rápidamente". Sin atajos el dashboard es solo informativo; con ellos se vuelve centro de operación. Es co-prioritario con US1 y demostrable sin drill-down profundo.

**Independent Test**: Desde el Dashboard, clicar cada uno de los 7 accesos y verificar que navega a la sección/destino correcto con el contexto adecuado (p. ej., Crear juego → formulario vacío; Ver juegos activos → listado filtrado a activos). No requiere que los indicadores de US1 estén perfectos.

**Acceptance Scenarios**:

1. **Given** el Dashboard cargado, **When** el usuario ve la zona de accesos rápidos, **Then** encuentra 7 acciones etiquetadas: Crear juego, Configurar juego, Gestionar preguntas, Ver juegos activos, Ver jugadores, Gestionar premios, Consultar reportes, cada una con icono y descripción corta.
2. **Given** un usuario hace clic en "Crear juego", **When** la navegación ocurre, **Then** llega al formulario de creación de juego vacío y listo para crear.
3. **Given** un usuario hace clic en "Configurar juego", **When** navega, **Then** llega a la vista de configuración (listado de juegos configurables o selector de juego para configurar).
4. **Given** un usuario hace clic en "Ver juegos activos", **When** navega, **Then** llega al listado de juegos filtrado por estado Activo sin aplicar otro filtro.
5. **Given** un usuario hace clic en "Consultar reportes", **When** navega, **Then** llega a la sección de reportes con selector de tipo visible.
6. **Given** un usuario sin permiso para una acción (p. ej., REWARD_MANAGER sobre "Gestionar preguntas"), **When** ve los accesos rápidos, **Then** ese atajo está deshabilitado u oculto con explicación de permiso, y no navega si se intenta acceder por URL directa.

---

### User Story 3 - Drill-down, actualización y contexto operacional (Priority: P2)

Como operador avanzado, quiero que cada indicador del Dashboard sea navegable a su detalle (p. ej., "Juegos activos → Live Games") y que el Dashboard se mantenga razonablemente actualizado para tomar decisiones informadas durante operación en vivo.

**Why this priority**: Eleva el dashboard de informativo a operativo. Depende de US1/US2 y es P2 porque el valor base ya se entregó.

**Independent Test**: Clicar un indicador (p. ej., Jugadores conectados) y verificar navegación al listado relevante; forzar un cambio en el backend (crear un juego programado) y verificar que tras refrescar/manual o auto el valor se actualiza en < 30s.

**Acceptance Scenarios**:

1. **Given** el indicador "Juegos activos" muestra N, **When** el usuario hace clic en la tarjeta, **Then** navega a Live Games / listado filtrado a activos y ve exactamente N elementos (coherencia).
2. **Given** el indicador "Jugadores activos", **When** se hace clic, **Then** navega a la vista de jugadores (Players o Live) correspondiente.
3. **Given** el Dashboard abierto, **When** el backend cambia (p. ej., finaliza un juego), **Then** tras una actualización manual (botón Actualizar) o automática cada 30-60s, los contadores reflejan el nuevo valor sin recargar toda la app.
4. **Given** una sesión con permisos parciales, **When** el usuario navega vía drill-down a una entidad no autorizada, **Then** recibe denegación clara sin datos expuestos.
5. **Given** el usuario está en viewport móvil (375px), **When** abre el Dashboard, **Then** tanto indicadores como accesos rápidos son utilizables sin scroll horizontal y con objetivos táctiles ≥44px.

---

### Edge Cases

- ¿Qué ocurre si el backend no puede calcular "Jugadores conectados" vs "Jugadores activos" en tiempo real? Cada métrica usa la definición disponible más cercana del backend (p. ej., PlayersOnline vs participantes en juegos en curso) y lo documenta en tooltip/ayuda; nunca muestra valor inventado.
- ¿Qué ocurre si el usuario pierde sesión mientras el Dashboard hace polling/auto-refresh? La petición falla con 401, el polling se detiene y se muestra aviso de sesión expirada con acción de re-autenticar, sin bucle de reintentos.
- ¿Qué ocurre con 1000+ juegos/preguntas al agregar estadísticas generales? Los agregados se calculan server-side y se entregan paginados/precálculo; el Dashboard nunca carga colecciones completas.
- ¿Qué ocurre si el proveedor de identidad señala `must_change_password` mientras se ve el Dashboard? El usuario es canalizado al flujo de cambio antes de interactuar con cualquier métrica o atajo.
- ¿Qué ocurre si el backend tarda >5s en un indicador? Ese bloque permanece en Carga con indicador de progreso accesible y no bloquea la interacción con los demás bloques ni con los atajos.
- ¿Qué ocurre si un indicador depende de un servicio caído (p. ej., premios)? Los demás bloques siguen operativos y el fallido ofrece reintento aislado y mensaje accionable sin detalles internos.
- ¿Qué ocurre en modo claro administrativo con contraste insuficiente? Todos los bloques cumplen WCAG 2.2 AA en tema claro, verificado por el gate de tokens y audit de accesibilidad.

## Requirements *(mandatory)*

### Functional Requirements

**Snapshot operacional — métricas (10 bloques)**

- **FR-001**: El sistema MUST mostrar en el Dashboard un bloque "Juegos activos" con el conteo de juegos en estados `IN_PROGRESS` / `ROUND_IN_PROGRESS` / `ROUND_COMPLETED` derivados del backend, actualizado sin recarga manual completa.
- **FR-002**: El sistema MUST mostrar "Juegos programados" (juegos en `READY` / `WAITING_FOR_PLAYERS` / con fecha futura) y "Juegos finalizados" (`FINISHED` / `FORCED_FINISHED` / `CANCELLED` agrupados o desglosados) con conteos separados.
- **FR-003**: El sistema MUST mostrar "Jugadores conectados" y "Jugadores activos" como dos métricas distinguibles: conectados = sesiones/presencia online; activos = participantes en juegos en curso. Si el backend no distingue, el sistema MUST documentar la aproximación y mostrar ambas con la fuente disponible más cercana sin duplicar valores de forma engañosa.
- **FR-004**: El sistema MUST mostrar "Preguntas disponibles" (preguntas activas/publicadas) y "Categorías" (categorías activas totales) con conteos puntuales.
- **FR-005**: El sistema MUST mostrar "Premios" (recompensas activas en catálogo) y "Canjes" (redenciones pendientes o totales según definicón operativa) con conteos.
- **FR-006**: El sistema MUST mostrar "Estadísticas generales" como bloque agregado (al menos 3 sub-métricas: total de juegos creados, total de jugadores registrados/participaciones, y tasa o promedio operativo ej. preguntas por categoría); el contenido exacto es configurable pero MUST ser derivado del backend.
- **FR-007**: Cada bloque de métrica MUST declarar y manejar estados de Carga, Listo, Vacío (0) y Error con reintento aislado, y MUST ser accesible (nombre, valor, rol, aria-live para cambios).
- **FR-008**: El sistema MUST ofrecer actualización de métricas sin recarga completa de la página (botón Actualizar siempre; auto-refresh 30-60s opcional pero MUST respetar pausa si la pestaña no está visible y detener polling en 401).

**Accesos rápidos (7 atajos)**

- **FR-009**: El sistema MUST exponer 7 accesos rápidos visibles en el Dashboard: "Crear juego", "Configurar juego", "Gestionar preguntas", "Ver juegos activos", "Ver jugadores", "Gestionar premios", "Consultar reportes", cada uno navegable en ≤1 clic desde el Dashboard.
- **FR-010**: Cada atajo MUST navegar al destino correcto con contexto útil: Crear juego → formulario de creación vacío; Configurar juego → listado/configuración de juegos; Gestionar preguntas → Question Bank; Ver juegos activos → listado filtrado a activos; Ver jugadores → Players/Live según disponibilidad; Gestionar premios → Rewards (catálogo o redenciones); Consultar reportes → Reports.
- **FR-011**: El sistema MUST aplicar autorización por rol a los atajos: ocultar o deshabilitar con explicación los atajos no permitidos (ADMIN ve los 7; GAME_MANAGER no ve Gestionar premios; REWARD_MANAGER ve solo Gestionar premios y Consultar reportes) y MUST denegar acceso directo por URL a secciones no autorizadas.
- **FR-012**: Cada atajo MUST tener icono (Lucide, no emoji), etiqueta y descripción corta, y MUST cumplir objetivo táctil ≥44px y orden de foco lógico (accesos rápidos después de métricas principales en el DOM).

**Drill-down y navegación contextual**

- **FR-013**: Cada tarjeta de métrica clicable MUST navegar a su vista detallada correspondiente (p. ej., Juegos activos → Live Games/filtro activos; Preguntas → Question Bank; Categorías → Categories) manteniendo coherencia de conteos entre origen y destino.
- **FR-014**: El sistema MUST preservar autorización en drill-down: si el usuario no tiene permiso para el destino, MUST mostrar denegación clara sin fuga de datos.

**Integración y arquitectura**

- **FR-015**: El Dashboard MUST consumir exclusivamente servicios/API del backend para todas las métricas y atajos (a través del BFF de QuizArena.Admin); MUST NOT acceder directamente a bases de datos de dominio ni a `identitydb`.
- **FR-016**: El Dashboard MUST reutilizar el shell de navegación, tema claro administrativo, tokens de diseño y componentes del Design System (SPEC-016) sin valores hardcodeados; MUST residir en `src/Admin/QuizArena.Admin` dentro del Blazor Auto existente.
- **FR-017**: El Dashboard MUST validar sesión via OroIdentityServer en cada carga y manejar `must_change_password` y expiración de sesión antes de mostrar datos operativos.
- **FR-018**: El sistema MUST registrar identificadores de correlación y NO exponer detalles internos en errores de métricas; los errores de negocio del backend se muestran como mensajes accionables.

### Key Entities *(include if feature involves data)*

- **Dashboard Snapshot**: Agregado de lectura que compone los 10 indicadores operativos en un instante. Atributos: timestamp de generación, fuente (servicio/API), estado por bloque (loading/ready/empty/error).
- **Game State Counts**: Proyección derivada del ciclo de vida de juegos (`DRAFT`, `READY`, `WAITING_FOR_PLAYERS`, `IN_PROGRESS`, `ROUND_IN_PROGRESS`, `ROUND_COMPLETED`, `FINISHED`, `CANCELLED`, `FORCED_FINISHED`). Sub-agregados: Activos, Programados, Finalizados.
- **Player Presence**: Dos métricas distinguibles: **Conectados** (sesiones/signalR presence online) y **Activos** (jugadores con estado `PLAYING` en juegos `IN_PROGRESS`). Fuente: servicios de presencia/juego.
- **Content Inventory**: **Preguntas disponibles** (estado `Active/Published` con 4 opciones/1 correcta) y **Categorías** (estado `Active`).
- **Reward Inventory**: **Premios** (catálogo activo) y **Canjes** (redenciones por estado `Pending/Approved/Delivered`).
- **General Statistics**: Agregado configurable (total de juegos, participaciones/jugadores, promedios operativos). Derivado de reportes/auditoría.
- **Quick Action**: Entidad de navegación: id, etiqueta, icono, ruta destino, roles permitidos, descripción.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Un operador autenticado percibe el estado global (10 indicadores) en menos de 5 segundos tras abrir el Dashboard (Carga inicial <2s para 100+ juegos, skeleton por bloque, sin pantalla en blanco).
- **SC-002**: El 100% de los 7 accesos rápidos navega al destino correcto en 1 clic y con filtro/contexto esperado, verificado en auditoría de navegación.
- **SC-003**: Cada métrica muestra coherencia origen-destino: el conteo en la tarjeta coincide con el número de elementos en la vista de detalle filtrada en el 100% de los casos de prueba.
- **SC-004**: El Dashboard refleja cambios del backend (p. ej., juego programado → activo) en ≤30s tras actualización manual y ≤60s con auto-refresh, sin recarga completa.
- **SC-005**: Cero accesos directos a base de datos desde el Dashboard; 100% de las lecturas via BFF/API verificable por `DesignSystemNoDirectDbTests` y revisión de dependencias.
- **SC-006**: El Dashboard es utilizable sin scroll horizontal entre 375 y 1536px, con 0 violaciones de objetivos táctiles <44px y orden de foco lógico (métricas → atajos → paginación).
- **SC-007**: Cumple WCAG 2.2 AA en tema claro administrativo (contraste, foco visible, navegación por teclado, aria-live en cambios de métricas) en auditoría automatizada + manual.
- **SC-008**: 90% de operadores completa la tarea "abrir Dashboard → identificar juegos activos → navegar a ver jugadores" en <30 segundos en primer intento.
- **SC-009**: El 100% de los bloques maneja Carga/Vacío/Error con mensaje accionable y reintento aislado; ningún error de un bloque bloquea los demás.
- **SC-010**: Los atajos respetan autorización por rol en el 100% de los casos: usuario sin permiso ve atajo deshabilitado/oculto y el acceso directo por URL es denegado sin fuga.

## Assumptions

- **Reutilización de SPEC-017**: La aplicación administrativa Blazor net10.0 Auto, shell de navegación de 10 secciones y BFF YARP ya existen (SPEC-017). Esta feature extiende el Dashboard existente; no crea una nueva app ni duplica autenticación.
- **Backend como fuente**: Las 10 métricas se derivan de endpoints/APIs ya expuestos o de agregaciones de reportes existentes; si algún conteo no tiene endpoint directo, se usa el agregado disponible más cercano y se documenta en el plan (sin crear lógica de dominio en el frontend).
- **Semántica de jugadores**: "Conectados" = presencia/sesión online (SignalR/Hub o heartbeat) y "Activos" = participantes en juegos en curso; si la distinción no existe en el backend, ambas tarjetas muestran la mejor aproximación con tooltip que explica la fuente.
- **Estadísticas generales**: Contenido mínimo viable (total de juegos, jugadores/participaciones, promedio de preguntas por categoría) derivado de reportes/auditoría; detalles adicionales configurables post-MVP sin cambiar el contrato.
- **Permisos**: Reutiliza roles de SPEC-017 (ADMIN ve todo, GAME_MANAGER ve operaciones/juegos/preguntas/jugadores, REWARD_MANAGER ve premios/canjes/reportes); la tarjeta de métrica y su atajo siguen la misma matriz.
- **Idioma**: Español para etiquetas del Dashboard (coherente con SPEC-017), sin i18n en v1.
- **Sin acceso a datos fuera de API**: Todo conteo proviene del backend vía BFF; no hay lectura directa a SQL Server, Oracle ni PostgreSQL identitydb.
- **Auto-refresh no crítico para MVP**: El botón Actualizar es obligatorio; el auto-refresh 30-60s con pausa en pestaña oculta es deseable pero no bloqueante si se justifica en el plan.
