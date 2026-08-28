# Feature Specification: Admin Players

**Feature Branch**: `024-admin-players`

**Created**: 2026-05-13

**Status**: Draft

**Input**: User description: "024 — Admin Players Objetivo Administrar y consultar los participantes de QuizArena. Descripción Permitirá consultar: Perfil. Estado. Historial de partidas. Puntuaciones. Premios. Canjes. Estadísticas. Participaciones. Resultados. La aplicación administrativa deberá aplicar autorización según rol y permisos."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Consultar perfil y estado del jugador (Priority: P1)

Como administrador (ADMIN) o gestor de juego (GAME_MANAGER) autenticado, quiero consultar el perfil y el estado actual de cualquier participante para verificar identidad, estado operativo y elegibilidad.

**Why this priority**: Es la base de 024 — sin perfil y estado no se puede contextualizar ninguna otra consulta (historial, puntuaciones, premios). Es la vista mínima viable para soporte y auditoría.

**Independent Test**: Login ADMIN → `/admin/players` → buscar por nombre/email/sub → abrir detalle de un jugador → verificar que se muestra perfil (nombre, email, tenant, identificación), estado (activo, estado de juego, sesión), puntuación agregada y acceso a premios/canjes. Logout y login GAME_MANAGER → mismo flujo visible; login REWARD_MANAGER → acceso denegado o solo lectura limitada según política.

**Acceptance Scenarios**:

1. **Given** un ADMIN autenticado en el listado de jugadores, **When** busca por texto (nombre o email) y abre el detalle, **Then** el sistema muestra perfil completo (identidad vinculada a OroIdentityServer `sub`, nombre, email, tenant, tipo de identificación) y estado (estado del jugador en juego, última actividad, sesión vinculada si existe).
2. **Given** un jugador sin historial previo, **When** se consulta su perfil, **Then** el sistema muestra perfil y estado con secciones vacías pero sin error (puntuaciones 0, historial vacío, premios/canjes vacíos).
3. **Given** un jugador con múltiples estados (ej. activo, en partida, retirado), **When** se consulta, **Then** el sistema muestra el estado derivado del dominio (`GamePlayer` + `PointTransaction` + `UserSession`) sin editarlo desde Admin Players (solo lectura).
4. **Given** un GAME_MANAGER autenticado, **When** intenta consultar un jugador, **Then** tiene acceso; **Given** un usuario sin rol administrativo, **When** intenta acceder a `/admin/players` por URL directa o API, **Then** recibe 403/`Access Denied` sin fuga de datos.

---

### User Story 2 - Consultar historial, participaciones y resultados (Priority: P1)

Como operador, quiero consultar el historial de partidas, participaciones y resultados de un jugador para auditar su trayectoria y resolver incidencias.

**Why this priority**: Co-prioritario con US1 — el valor de administrar participantes es ver dónde ha jugado, cuánto ha puntuado y qué resultado obtuvo. Sin historial/participaciones, el perfil es aislado.

**Independent Test**: Desde el detalle del jugador → pestañas "Historial" / "Participaciones" → verificar listado paginado de partidas (juego, categoría, fecha, estado, rondas, tiempo), participaciones (rol, fecha de unión, estado de participación) y resultados (posición, puntuación obtenida, puntos asegurados, estado de finalización). Filtrar por rango de fechas y estado de juego.

**Acceptance Scenarios**:

1. **Given** un jugador con al menos 5 partidas finalizadas, **When** el operador abre "Historial de partidas", **Then** ve listado paginado (sin cargar colecciones completas) con juego, fecha, estado (`FINISHED`/`CANCELLED`), categoría y puntuación de esa partida, ordenado por fecha descendente.
2. **Given** el historial paginado, **When** el operador filtra por rango de fechas (Desde/Hasta) o por estado de juego o busca por nombre de juego/categoría, **Then** el sistema aplica filtros combinados y pagina correctamente, manteniendo el rendimiento percibido <2s.
3. **Given** un jugador con participaciones activas y pasadas, **When** abre "Participaciones", **Then** ve cada participación con juego, estado de participación (`JOINED`/`WITHDRAWN`/`FINISHED`), fecha de unión y resultado asociado cuando existe.
4. **Given** una participación finalizada, **When** abre "Resultados" de esa participación, **Then** ve resultado detallado (puntuación total, puntuación asegurada, posición en ranking, bonificaciones/penalizaciones del ledger, tiempo total) derivado del `PointTransaction` y del `Leaderboard`.

---

### User Story 3 - Consultar puntuaciones, premios, canjes y estadísticas (Priority: P2)

Como administrador, quiero consultar puntuaciones acumuladas, premios disponibles/canjeados, canjes y estadísticas del jugador para evaluar elegibilidad, recompensas y comportamiento.

**Why this priority**: Eleva la consulta de "quién es y dónde jugó" a "cómo rinde y qué ha obtenido". Depende de US1/US2 y es P2 porque el valor base ya se entregó, pero es crítico para gestionar premios y soporte avanzado.

**Independent Test**: Desde el detalle del jugador → pestañas "Puntuaciones" → ver total y desglose por tipo de transacción del ledger; "Premios" → ver premios elegibles y obtenidos; "Canjes" → ver canjes `Requested→Approved→Delivered` con estado y coste; "Estadísticas" → ver métricas agregadas (partidas jugadas, promedio, tasa de aciertos, mejor racha). Verificar coherencia entre `PointTransaction` y premios/canjes.

**Acceptance Scenarios**:

1. **Given** un jugador con movimientos de puntos, **When** abre "Puntuaciones", **Then** ve puntuación total reconstruida desde `PointTransaction` (no balance mutado directamente) y desglose por tipo (`ANSWER_CORRECT`, `ROUND_BONUS`, `PENALTY`, `REWARD_REDEMPTION`, `CONSOLATION`, etc.) con fecha y referencia de juego/premio.
2. **Given** el mismo jugador, **When** abre "Premios", **Then** ve premios asociados (premios del catálogo donde es elegible o que ha visto) y estado de elegibilidad; **When** abre "Canjes", **Then** ve historial paginado de canjes con `RewardName`, `Cost`, `Status` (`Requested`/`Approved`/`Rejected`/`Delivered`/`Cancelled`), `RequestedAt` y `Reason` cuando aplica, con filtros por estado y fecha.
3. **Given** un jugador con al menos 20 partidas, **When** abre "Estadísticas", **Then** ve métricas calculadas server-side (partidas totales, victorias/top-3, promedio de puntos por partida, tasa de aciertos, rachas, tiempo medio por pregunta) sin cálculo en cliente, con skeleton mientras carga.
4. **Given** un premio `Consolation` canjeado, **When** se consulta en "Canjes", **Then** se distingue con marca `consolation:true` y no se cuenta como premio normal, consistente con la regla `ConsolationEligibility`.
5. **Given** un GAME_MANAGER consulta puntuaciones/premios, **When** accede, **Then** tiene acceso; **Given** un REWARD_MANAGER consulta estadísticas de jugador, **When** accede, **Then** ve vista limitada o denegada según matriz de permisos (definida en FR-009) y por API recibe 403 sin fuga.

---

### Edge Cases

- ¿Qué ocurre si el jugador no existe o el `sub` no tiene `GamePlayer` asociado? Mostrar estado "Sin participaciones" con perfil básico (datos de OroIdentityServer) y secciones vacías, sin error 500.
- ¿Qué ocurre si el jugador tiene historial muy grande (≥500 partidas)? El sistema pagina server-side (`page`/`pageSize`) y no carga colecciones completas; filtros por fecha/estado deben seguir respondiendo <2s.
- ¿Qué ocurre si OroIdentityServer está no disponible o la sesión expiró mientras se consulta? La petición BFF retorna 401, el UI muestra "Sesión expirada — re-autenticar" y preserva filtros/búsqueda sin pérdida de estado local.
- ¿Qué ocurre si dos operadores consultan el mismo jugador simultáneamente y uno actualiza datos en otro contexto? La vista es de lectura; no hay conflicto de escritura, pero se debe propagar `CorrelationId` y no cachear de forma stale más allá de la ventana de consulta.
- ¿Qué ocurre si un jugador tiene premios/canjes con estado inconsistente (ej. `Approved` sin `PointTransaction`)? El UI muestra el estado reportado por API y un indicador de auditoría; no intenta recalcular elegibilidad localmente (Constitución V — Server Truth).
- ¿Qué ocurre si se filtra por rango de fechas con `Desde > Hasta`? Validación por campo con mensaje accionable y sin petición al servidor.
- ¿Qué ocurre si GAME_MANAGER intenta acceder a estadísticas restringidas a ADMIN? 403 `Forbidden` con mensaje `Insufficient permissions` sin exponer datos.

## Requirements *(mandatory)*

### Functional Requirements

**Consulta de perfil y estado**

- **FR-001**: El sistema MUST permitir consultar el perfil del jugador (identidad `sub` de OroIdentityServer, nombre, email, tenant, tipo y valor de identificación cuando estén disponibles, fecha de creación) en modo solo lectura.
- **FR-002**: El sistema MUST permitir consultar el estado del jugador (estado derivado del dominio: disponibilidad, última actividad, sesión activa `UserSession` si existe, estado de participación actual) sin permitir edición desde Admin Players.
- **FR-003**: El sistema MUST permitir buscar y listar jugadores con paginación server-side y búsqueda por nombre/email/`sub` e identificador, sin cargar colecciones completas.

**Historial, participaciones y resultados**

- **FR-004**: El sistema MUST permitir consultar el historial de partidas de un jugador paginado, con datos por partida: juego, categoría, fecha, estado del juego (`DRAFT`/`READY`/`WAITING_FOR_PLAYERS`/`IN_PROGRESS`/`ROUND_IN_PROGRESS`/`ROUND_COMPLETED`/`FINISHED`/`CANCELLED`/`FORCED_FINISHED`), rondas y tiempo, ordenable y filtrable por texto, estado y rango de fechas.
- **FR-005**: El sistema MUST permitir consultar las participaciones del jugador (juegos donde participó, fecha de unión, estado de participación, rol) paginadas y filtrables por estado y fecha, derivadas de `GamePlayer` y `Game`.
- **FR-006**: El sistema MUST permitir consultar los resultados por participación (puntuación total, puntos asegurados, posición en ranking, bonificaciones/penalizaciones, tiempo) derivados del ledger `PointTransaction` y del leaderboard, sin recalcular en cliente.

**Puntuaciones, premios, canjes y estadísticas**

- **FR-007**: El sistema MUST permitir consultar las puntuaciones del jugador reconstruidas desde `PointTransaction` (no balance mutado), con total acumulado y desglose por tipo (`ANSWER_CORRECT`, `ANSWER_INCORRECT`, `ROUND_BONUS`, `LEVEL_BONUS`, `GAME_BONUS`, `PENALTY`, `WITHDRAWAL`, `REWARD_REDEMPTION`, `CONSOLATION`, `ADJUSTMENT`), fecha y referencia (juego/premio), paginado y filtrable por tipo y fecha.
- **FR-008**: El sistema MUST permitir consultar los premios vinculados al jugador (premios del catálogo donde es elegible o que ha obtenido) y los canjes (`RewardRedemption`) con ciclo `REQUESTED → APPROVED → REJECTED → DELIVERED → CANCELLED`, mostrando `RewardName`, `RewardType`, `Cost`, `Status`, `RequestedAt`/`ApprovedAt`/`DeliveredAt`, `Reason` e `IsConsolation`, con filtros por estado, tipo de premio y fecha.
- **FR-009**: El sistema MUST permitir consultar estadísticas agregadas del jugador calculadas server-side (partidas totales, partidas ganadas/top-3, promedio de puntos, tasa de aciertos, mejor racha, tiempo medio por pregunta, distribución por dificultad/categoría) sin cálculo en cliente.
- **FR-010**: El sistema MUST distinguir premios `Consolation` de premios normales: un canje `IsConsolation:true` solo es visible como consolación y no se cuenta como premio normal, consistente con `ConsolationEligibility`.

**Autorización, validación y presentación**

- **FR-011**: El sistema MUST aplicar autorización por rol vía OroIdentityServer (OIDC `authorization_code` + `refresh_token`): `ADMIN` tiene acceso completo a todas las secciones; `GAME_MANAGER` tiene acceso a perfil/estado/historial/participaciones/resultados/puntuaciones/estadísticas; `REWARD_MANAGER` tiene acceso limitado a premios/canjes y lectura básica de perfil; cualquier rol no autorizado recibe `403 Forbidden` por API y `Access Denied` en UI sin fuga de datos. `PLAYER` no tiene acceso a Admin Players.
- **FR-012**: El sistema MUST validar en tres niveles: API (contrato — paginación, filtros, tipos), Aplicación (requisitos — existencia de jugador, coherencia de filtros, paginación) y Dominio (invariantes — `PlayerNotFound`, `GamePlayerNotFound` mapeados a 404/200 vacío según política, sin exponer detalles internos). Los invariantes MUST NOT depender solo de UI.
- **FR-013**: El sistema MUST mostrar estados de carga (`Loading` con skeleton), vacío (`Empty` sin resultados), error (`Error` con retry) y listo (`Ready`) por sección, con paginación, sin cargar colecciones completas, y MUST propagar `CorrelationId` con errores `ProblemDetails` RFC 7807 sin fuga.
- **FR-014**: El sistema MUST consumir exclusivamente la API/BFF (`QuizArena.Api` vía `QuizArena.Admin` BFF YARP) para todos los datos de jugador/partidas/puntuaciones/premios/canjes; MUST NOT acceder directamente a SQL Server/Oracle/`identitydb`.
- **FR-015**: El sistema MUST reutilizar el shell de navegación, tema `administration`, tokens y componentes del Design System (SPEC-016) sin valores hardcodeados, residir en `src/Admin/QuizArena.Admin` (Blazor Auto `net10.0`) y `src/Admin/QuizArena.Admin.Client`, y MUST exigir sesión válida vía OroIdentityServer con manejo de `must_change_password` y expiración antes de consultar.
- **FR-016**: El sistema MUST registrar auditoría de consultas administrativas cuando sea requerido por política (actor `sub`, timestamp UTC, `PlayerId` consultado, filtros aplicados, `CorrelationId`), sin mutar historial del jugador.

### Key Entities *(include if feature involves data)*

- **Player (Participant)**: Identidad externa `sub` de OroIdentityServer vinculada a `GamePlayer`. Atributos: `PlayerId` (`sub`), `DisplayName`, `Email`, `TenantId`, `IdentificationType/Value`, `CreatedAt`, `LastActiveAt`. Relación 1:N con `GameParticipation`, `PointTransaction`, `RewardRedemption`.
- **PlayerProfile**: Vista agregada de perfil + estado. Incluye datos de identidad (OroIdentityServer) y estado derivado (`GamePlayer.Status`, `UserSession`, última actividad). Solo lectura en Admin Players.
- **GameParticipation**: Participación de un jugador en un juego. Atributos: `ParticipationId`, `GameId`, `PlayerId`, `JoinedAt`, `State` (`JOINED`/`WITHDRAWN`/`FINISHED`/`KICKED`), `GameCategory`, `GameStatus`. Deriva el historial.
- **GameHistoryEntry**: Entrada de historial por juego. Atributos: `GameId`, `GameName`, `Category`, `Status`, `StartAt`/`FinishedAt`, `RoundCount`, `ResultSummary`. Paginado.
- **ScoreLedger**: Reconstrucción desde `PointTransaction`. Atributos: `TransactionId`, `PlayerId`, `GameId`, `Type` (`ANSWER_CORRECT` etc.), `Points`, `Timestamp`, `ReferenceId`. Balance = suma del ledger.
- **Reward / RewardRedemption (Canje)**: Catálogo y canjes del jugador. `RewardRedemption` con `RedemptionId`, `RewardId`, `RewardType`, `Cost`, `Status` (`Requested→Approved→Delivered` etc.), `RequestedAt`, `Reason?`, `IsConsolation`. Consolación independiente.
- **PlayerStatistics**: Métricas agregadas server-side: `TotalGames`, `Wins`, `Top3`, `AverageScore`, `AccuracyRate`, `BestStreak`, `AverageTimePerQuestion`, `DistributionByDifficulty/Category`.
- **Result**: Resultado por participación: `PlayerId`, `GameId`, `TotalScore`, `SecuredScore`, `Rank`, `Bonuses`, `Penalties`, `Duration`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Un ADMIN localiza a un jugador por nombre/email y abre su perfil completo en menos de 30 segundos en el 90% de los intentos, con datos consistentes entre OroIdentityServer y dominio de juego.
- **SC-002**: El 100% de las búsquedas de jugadores con texto parcial retornan resultados relevantes paginados en <2s percibidos con skeleton, sin cargar colecciones completas.
- **SC-003**: El 100% de las vistas de historial/participaciones paginan correctamente (≥200 participaciones) y filtran por estado y rango de fechas en <2s, con orden correcto y sin duplicados.
- **SC-004**: El 100% de las consultas de puntuaciones muestran el total reconstruido desde `PointTransaction` coincidente con el ledger server-side, con desglose por tipo y sin cálculo en cliente.
- **SC-005**: El 95% de los operadores completan la tarea "buscar jugador → ver historial → ver resultado de una participación → ver puntuaciones → ver canjes" en menos de 2 minutos sin ayuda externa.
- **SC-006**: El 100% de los canjes del jugador se muestran con estado correcto (`Requested`/`Approved`/`Rejected`/`Delivered`/`Cancelled`) y `Consolation` se distingue (`IsConsolation:true`) sin contarse como premio normal.
- **SC-007**: La autorización se respeta en el 100% de los casos: `GAME_MANAGER` accede a perfil/historial/estadísticas, `REWARD_MANAGER` ve solo premios/canjes + perfil básico, y cualquier usuario no autorizado recibe `Access Denied` en UI y `403` por API sin fuga.
- **SC-008**: El 100% de los errores de API se presentan como `ProblemDetails` RFC 7807 sin fuga interna, con `CorrelationId` propagado y estados `Loading`/`Empty`/`Error` visibles por sección.
- **SC-009**: La UI de jugadores cumple WCAG 2.2 AA en tema `administration` (contraste, foco visible, navegación teclado, `aria-live` en errores) y es utilizable entre 375 y 1536px sin scroll horizontal y con objetivos táctiles ≥44px, con tokens del Design System sin literales.

## Assumptions

- **Reutiliza SPEC-017/016/009**: La app Blazor `net10.0` Auto, shell de 10 secciones, BFF YARP, OIDC y agregados `Game`/`GamePlayer`/`PointTransaction`/`Reward`/`RewardRedemption` ya existen (009-reward-redemption con ledger, SPEC-016 Design System). 024 extiende solo lectura de participantes, sin crear nueva app ni duplicar autenticación.
- **Solo lectura en v1**: Admin Players es consulta y auditoría; no edita perfil, no ajusta puntos, no gestiona premios/canjes desde esta vista (esas acciones viven en 023 y en gestión de juego). Cualquier edición sería feature separada.
- **Fuente de verdad**: Perfil base proviene de OroIdentityServer (`sub` + claims `name`/`email`/`tenant_id`); historial/participaciones/resultados/puntuaciones/premios/canjes/estadísticas provienen de `oroclash-api` (SQL Server primario, abstracción Oracle, `rowversion` donde aplica). Admin nunca toca DB directamente.
- **Matriz de permisos v1**: `ADMIN` → todo; `GAME_MANAGER` → perfil/estado/historial/participaciones/resultados/puntuaciones/estadísticas (sin gestión de premios); `REWARD_MANAGER` → perfil básico + premios/canjes (coherente con 023 donde `RewardManagerOrAdmin` gestiona premios); `PLAYER` → 403 en `/admin/players`. Si la política final difiere, se ajusta en Plan sin cambiar el scope de consulta.
- **Paginación y filtros**: Listados paginados server-side (`page`/`pageSize`, `search`, `status`, `from`/`to`, `type` donde aplica); búsqueda por nombre/email/`sub` es case-insensitive y parcial; rango de fechas valida `Desde <= Hasta`.
- **Consolación independiente**: `Consolation` es tipo independiente (Constitución C) y su elegibilidad se evalúa server-side; en Admin Players solo se visualiza con `IsConsolation:true`.
- **Idioma**: Español para etiquetas, coherente con SPEC-017/020, sin i18n en v1.
- **Sin acceso directo a datos**: Todo conteo/validación vía BFF; no lectura directa a SQL Server, Oracle ni `identitydb` (Constitución H).
