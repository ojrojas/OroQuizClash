# Feature Specification: Admin Rewards

**Feature Branch**: `023-admin-rewards`

**Created**: 2026-08-28

**Status**: Draft

**Input**: User description: "023 — Admin Rewards Objetivo Administrar el catálogo de premios y su disponibilidad. Descripción Permitirá: Crear premios. Editar premios. Activar/desactivar premios. Definir costo en puntos. Definir disponibilidad. Definir inventario. Definir tipo de premio. Consultar canjes. Aprobar/rechazar operaciones cuando corresponda. Gestionar entrega. Tipos: Monetary, Physical, Digital, Voucher, Experience, Consolation"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Gestionar el catálogo de premios (Priority: P1)

Como administrador (ADMIN) o gestor de premios (REWARD_MANAGER) autenticado, quiero crear y editar premios definiendo nombre, descripción, tipo (Monetary, Physical, Digital, Voucher, Experience, Consolation), costo en puntos, disponibilidad (fechas), inventario (stock) y estado activo/inactivo, y poder activar/desactivar un premio existente.

**Why this priority**: Es el núcleo de 023 — sin catálogo no hay canjes ni entregas. El catálogo con 6 tipos + inventario + costo es la base para que los jugadores vean premios disponibles. Constituye el MVP estricto.

**Independent Test**: Login REWARD_MANAGER → /admin/rewards → "Crear premio" → completar nombre 3–100, tipo Physical, costo 500, stock 10, disponibilidad 2026-09-01 a 2026-12-31, activo → guardar → verificar premio aparece en listado con tipo/stock/costo y es visible para jugadores elegibles. Luego desactivar → verificar `Inactive` y no elegible.

**Acceptance Scenarios**:

1. **Given** un REWARD_MANAGER en creación, **When** completa nombre 3–100, descripción 0–500, tipo `Physical`, costo 100–100000, stock ≥0 (0 = ilimitado según política), disponibilidad `From`/`To` con `From < To` y guarda, **Then** el sistema crea el premio en `Active` (si disponible hoy) o `Inactive` y muestra confirmación con ID.
2. **Given** un intento con costo 0 o negativo o stock negativo, **When** guarda, **Then** el sistema rechaza con `InvalidRewardData` por campo `Cost`/`Stock` y no crea el premio.
3. **Given** un intento con tipo fuera de los 6 (`Monetary`/`Physical`/`Digital`/`Voucher`/`Experience`/`Consolation`) o nombre duplicado exacto (case-insensitive) entre no archivados, **When** guarda, **Then** el sistema rechaza con `InvalidRewardData` o `RewardAlreadyExists`.
4. **Given** un premio existente en `Active` con stock 5, **When** el ADMIN edita costo a 600 y guarda, **Then** el sistema persiste la edición y mantiene historial de cambios.
5. **Given** un premio en `Active`, **When** el REWARD_MANAGER ejecuta "Desactivar", **Then** transita a `Inactive` y deja de ser visible para nuevos canjes; "Activar" desde `Inactive` lo vuelve a `Active` si mantiene inventario y fechas válidas (si no, rechaza con `RewardUnavailable`).

---

### User Story 2 - Operar canjes y su ciclo de vida (Priority: P1)

Como operador de premios, quiero consultar los canjes (`RewardRedemption`) con filtros, y aprobar/rechazar operaciones pendientes, y gestionar la entrega (`Delivered`) de los aprobados, para operar el flujo `Requested → Approved/Rejected → Delivered/Cancelled` de forma auditada.

**Why this priority**: El objetivo incluye explícitamente Consultar canjes, Aprobar/rechazar y Gestionar entrega. Sin operar canjes, el catálogo es solo lectura. Co-prioritario con US1 para valor operacional.

**Independent Test**: Con un jugador que canjea un premio `Physical` (costo 500, tiene puntos) → aparece en listado de canjes `Requested` → aprobar → `Approved`; luego marcar `Delivered` → verificar entregado; otro canje con puntos insuficientes → `Rejected` o `InsufficientPoints`; filtrar canjes por estado/tipo/fecha.

**Acceptance Scenarios**:

1. **Given** un jugador con puntos suficientes canjea un premio `Active` con stock 5, **When** el REWARD_MANAGER abre `/admin/rewards` pestaña "Canjes" y filtra por `Requested`, **Then** ve el canje con `PlayerName`, `RewardName`, `Cost`, `RequestedAt`, `Status: Requested`.
2. **Given** un canje en `Requested`, **When** el REWARD_MANAGER ejecuta "Aprobar" y confirma, **Then** transita a `Approved`, se descuenta inventario (stock 5→4) y se audita `Approve` con `ActorId`/`CorrelationId`; si stock es 0 y tipo `Physical` con inventario limitado, aprobar es rechazado con `RewardOutOfStock`.
3. **Given** un canje en `Requested`, **When** ejecuta "Rechazar" con motivo, **Then** transita a `Rejected` con `Reason`, no descuenta stock ni puntos (puntos se retienen o devuelven según política de `009-reward-redemption`), y se audita `Reject`.
4. **Given** un canje en `Approved`, **When** ejecuta "Marcar entregado", **Then** transita a `Delivered` y se registra `DeliveredAt`/`DeliveredBy`; si intenta entregar un `Rejected`, el sistema rechaza con `InvalidRedemptionState`.
5. **Given** un canje en `Requested` que expira o es cancelado por el jugador, **When** el operador ejecuta "Cancelar", **Then** transita a `Cancelled` (terminal) y se libera inventario si se había reservado.
6. **Given** un GAME_MANAGER (sin permiso `Reward.Manage`) intenta aprobar un canje, **When** ejecuta, **Then** el sistema retorna 403 sin fuga y no muta el canje.

---

### User Story 3 - Controlar disponibilidad, inventario y tipos con coherencia (Priority: P2)

Como administrador, quiero definir disponibilidad (fechas, stock), inventario por tipo y consultar la elegibilidad de un premio (costo vs puntos del jugador, stock, fechas, tipo `Consolation` independiente) con feedback por campo, para que el catálogo sea coherente y no se canjeen premios no disponibles.

**Why this priority**: Eleva el catálogo de "crear premio" a "catálogo curado" (Constitución C: Rewards con lifecycle, D: ledger). Depende de US1/US2 y es P2 porque el valor base ya se entregó.

**Independent Test**: Crear premio `Voucher` con stock 0 (ilimitado) y disponibilidad sin fechas → verificar siempre `Active`; crear premio `Physical` con stock 2 → canjear 2 veces → stock 0 → tercer canje → `RewardOutOfStock`; crear premio `Consolation` → verificar que solo es elegible via regla de consolación, no como premio normal.

**Acceptance Scenarios**:

1. **Given** un premio `Digital` con `From` 2026-09-01 y `To` 2026-09-30, **When** la fecha actual es 2026-10-01, **Then** el premio muestra `Fuera de disponibilidad` y no es elegible para nuevos canjes (aunque siga `Active`).
2. **Given** un premio `Physical` con stock 2, **When** se aprueban 2 canjes, **Then** stock baja a 0 y el premio muestra `Sin stock`; un tercer intento de canje es rechazado con `RewardOutOfStock`.
3. **Given** un premio `Monetary` con costo 1000, **When** un jugador con 500 puntos intenta canjear, **Then** el canje es rechazado con `InsufficientPoints` sin crear registro en `Requested` o con `Rejected` inmediato según política.
4. **Given** un premio `Consolation` con tipo `Consolation`, **When** un jugador intenta canjearlo como premio normal, **Then** el sistema rechaza con `InvalidRewardType` (solo elegible via `Consolation` rule, Constitución C); si el jugador es elegible por consolación, el canje se crea con marca `consolation:true`.
5. **Given** un premio `Experience` con `Active` y sin stock limitado (stock null/0 = ilimitado según política), **When** se canjean 100 veces, **Then** todos los canjes son aprobables sin agotar inventario.

---

### Edge Cases

- ¿Qué ocurre si dos REWARD_MANAGER aprueban el mismo canje `Requested` simultáneamente con stock 1? Uno tiene éxito y el otro recibe `ConcurrencyConflict`/`InvalidRedemptionState` sin doble descuento de stock (transacción + `rowversion`).
- ¿Qué ocurre si se edita el costo de un premio `Active` mientras hay canjes `Requested` pendientes? Los canjes pendientes mantienen el costo original (inmutabilidad de instancia); los nuevos canjes usan el nuevo costo.
- ¿Qué ocurre si se desactiva un premio con canjes `Requested` pendientes? Los pendientes permanecen `Requested` pero nuevos canjes son rechazados con `RewardUnavailable`; el operador puede aprobar/rechazar los pendientes.
- ¿Qué ocurre si se intenta archivar/eliminar un premio con canjes `Approved` sin entregar? Rechazado con `RewardInUse` o se archiva con advertencia pero mantiene canjes históricos.
- ¿Qué ocurre si `Disponibilidad` tiene `From` > `To`? Rechazado con `InvalidRewardData` por campo `Availability`.
- ¿Qué ocurre si el ADMIN pierde sesión mientras aprueba un canje? La petición falla con 401, el canje permanece `Requested`, sin auditoría de éxito, y muestra "Sesión expirada — re-autenticar".
- ¿Qué ocurre con `Consolation` si el jugador ya recibió premio normal? `Consolation` es independiente (Constitución C) y no se trata como `Delivered` normal; su elegibilidad se evalúa via `ConsolationEligibility` rule.

## Requirements *(mandatory)*

### Functional Requirements

**Catálogo — creación y edición**

- **FR-001**: El sistema MUST permitir crear un premio con `Nombre` (3–100, requerido, único case-insensitive entre no archivados), `Descripción` (0–500, opcional), `Tipo` (requerido, uno de `Monetary`, `Physical`, `Digital`, `Voucher`, `Experience`, `Consolation`) y `Costo en puntos` (entero 1–100000, requerido) con validación por campo.
- **FR-002**: El sistema MUST permitir definir `Inventario` (`Stock` entero ≥0, donde 0 = ilimitado para tipos `Digital`/`Voucher`/`Consolation` según política, y stock limitado para `Physical`/`Monetary`; `Stock` 0 con `Physical` puede interpretarse como ilimitado solo si la política lo permite, documentado en UI) y `Disponibilidad` (`AvailableFrom`/`AvailableTo` opcionales, con `From < To` si ambos se definen) con validación por campo.
- **FR-003**: El sistema MUST permitir definir `Disponibilidad` temporal y `Inventario`, y calcular `Elegible` como `Status == Active AND (Stock == 0 → ilimitado o Stock >0) AND (hoy entre `From`/`To` si se definen)`.
- **FR-004**: El sistema MUST permitir `Editar` los 7 campos (nombre, descripción, tipo, costo, disponibilidad, inventario) mientras el premio está en `Active`/`Inactive`; toda edición MUST re-validar unicidad, costo, stock, fechas y tipo, y MUST preservar `RowVersion`.
- **FR-005**: El sistema MUST permitir `Activar` (`Inactive` → `Active`) y `Desactivar` (`Active` → `Inactive`) con validación de costo, stock y fechas; `Active` es elegible para nuevos canjes si además cumple disponibilidad y stock, `Inactive` no es elegible.

**Canjes — consulta y operación**

- **FR-006**: El sistema MUST permitir `Consultar canjes` (`RewardRedemption`) con paginación y filtros por estado (`Requested`/`Approved`/`Rejected`/`Delivered`/`Cancelled`), por tipo de premio, por jugador y por rango de fechas, sin cargar colecciones completas.
- **FR-007**: El sistema MUST permitir `Aprobar` (`Requested` → `Approved`) y `Rechazar` (`Requested` → `Rejected` con `Reason` requerido) con validación de `RowVersion` y auditoría; `Approve` MUST descontar `Stock` (si limitado) de forma transaccional y MUST ser rechazado con `RewardOutOfStock` si no hay stock.
- **FR-008**: El sistema MUST permitir `Gestionar entrega` (`Approved` → `Delivered` con `DeliveredAt`/`DeliveredBy`) y `Cancelar` (`Requested` → `Cancelled` o `Approved` → `Cancelled` con motivo) de forma transaccional; `Delivered`/`Rejected`/`Cancelled` son terminales.
- **FR-009**: El sistema MUST aplicar la máquina de estados de canje: `Requested → Approved → Delivered` y `Requested → Rejected`, `Requested`/`Approved` → `Cancelled`; toda transición inválida MUST ser rechazada con `InvalidRedemptionState` sin mutación parcial, protegida por `rowversion` y `IdempotencyKey`.

**Disponibilidad, inventario y tipos**

- **FR-010**: El sistema MUST validar `Costo` contra puntos elegibles del jugador (ledger `PointTransaction`) al intentar canjear; si insuficientes, el canje MUST ser rechazado con `InsufficientPoints` sin crear `Requested` o con `Rejected` inmediato según política de `009` (documentado en UI).
- **FR-011**: El sistema MUST distinguir `Consolation` como tipo independiente: un premio `Consolation` solo es canjeable si el jugador es elegible via `ConsolationEligibility` rule (Constitución C), no como premio normal; su canje se marca `IsConsolation` y no se cuenta como `Delivered` normal.
- **FR-012**: El sistema MUST mantener coherencia: al aprobar/rechazar/cancelar/entregar un canje, el `Stock` del premio y el `Score` del jugador (ledger) MUST actualizarse de forma transaccional; el costo de canjes `Requested` pendientes no descuenta puntos hasta `Approved` (o descontado y reembolsado en `Rejected` según política, documentado).

**Validación, autorización y auditoría**

- **FR-013**: El sistema MUST validar en tres niveles: API (contrato), Aplicación (requisitos — unicidad nombre, costo 1–100000, stock ≥0, fechas `From < To`, tipo en catálogo 6, `RowVersion`), y Dominio (invariantes — `RewardAlreadyExists`, `RewardInUse`, `RewardOutOfStock`, `InvalidRedemptionState`, `InsufficientPoints`, `ConcurrencyConflict`). Los invariantes MUST NOT depender solo de UI.
- **FR-014**: El sistema MUST mostrar errores por campo con códigos accionables (`RewardAlreadyExists`, `RewardOutOfStock`, `RewardInUse`, `InvalidRewardData`, `InvalidRedemptionState`, `InsufficientPoints`, `ConcurrencyConflict`) y MUST preservar borrador local en caso de 401 sin pérdida de datos hasta re-autenticar.
- **FR-015**: El sistema MUST restringir creación/edición/activación y aprobación/rechazo/entrega a roles `ADMIN` y `REWARD_MANAGER` (política `RewardManagerOrAdmin`); `GAME_MANAGER` y `PLAYER` MUST recibir `Access Denied` en UI y 403 por API sin fuga. `OroIdentityServer` es la única autoridad (Constitución VI).
- **FR-016**: El sistema MUST auditar de forma append-only cada creación, modificación, cambio de estado de premio y cada transición de canje (actor `sub`, timestamp UTC, `RewardId`/`RedemptionId`, estado anterior/nuevo, diff, `CorrelationId`) sin mutar historial; `Cancel`/`ForceFinish` no aplica aquí, pero `Approve`/`Reject`/`Deliver` sí.
- **FR-017**: El sistema MUST propagar `CorrelationId` y mapear `Result` → HTTP (`ProblemDetails` RFC 7807) sin exponer detalles internos.

**Integración y presentación**

- **FR-018**: El sistema MUST consumir exclusivamente la API/BFF (`QuizArena.Api` via `QuizArena.Admin` BFF) para todos los datos de premios/canjes; MUST NOT acceder directamente a SQL Server/Oracle/`identitydb`.
- **FR-019**: El sistema MUST reutilizar el shell de navegación, tema `administration`, tokens y componentes del Design System (SPEC-016) sin valores hardcodeados y MUST residir en `src/Admin/QuizArena.Admin` (Blazor Auto net10.0) y `src/Admin/QuizArena.Admin.Client`.
- **FR-020**: El sistema MUST exigir sesión válida via `OroIdentityServer` (OIDC `authorization_code` + `refresh_token`) y manejar `must_change_password` y expiración antes de permitir administrar premios.
- **FR-021**: El sistema MUST listar premios con paginación y filtros por tipo, estado, disponibilidad y búsqueda por nombre, y MUST listar canjes con paginación y filtros por estado/tipo/fecha, cada uno con skeleton y sin cargar colecciones completas.

### Key Entities *(include if feature involves data)*

- **Reward**: Agregado de catálogo. Atributos: `RewardId`, `Name` (3–100, único), `Description` (0–500), `Type` (`Monetary`/`Physical`/`Digital`/`Voucher`/`Experience`/`Consolation`), `Cost` (1–100000), `Stock` (≥0, 0 = ilimitado según política), `AvailableFrom`/`AvailableTo` (opcionales, `From < To`), `Status` (`Active`/`Inactive`/`Archived`), `RowVersion`. Invariante: `Active` requiere costo y tipo válidos, `Consolation` independiente.
- **Reward State Machine (Catálogo)**: Estados `Active` ↔ `Inactive` → `Archived` (terminal); `Active` elegible si `Stock` y fechas lo permiten. Protegida por `rowversion`.
- **RewardRedemption (Canje)**: Entidad de canje. Atributos: `RedemptionId`, `RewardId` (FK `Reward`), `PlayerId` (sub), `Cost` (snapshot), `Status` (`Requested`→`Approved`→`Delivered` y `Requested`→`Rejected`, `Requested`/`Approved`→`Cancelled`), `RequestedAt`, `ApprovedAt`/`RejectedAt`/`DeliveredAt`, `Reason?`, `IsConsolation` bool, `RowVersion`. Invariante: transiciones válidas, `Approved` descuenta stock/puntos.
- **Redemption State Machine**: `Requested → Approved → Delivered` y `Requested → Rejected`, `Requested`/`Approved` → `Cancelled`; terminales `Delivered`/`Rejected`/`Cancelled`.
- **Stock & Availability**: `Stock` (si limitado) y `AvailableFrom`/`To` determinan `Elegible` (`Active` + stock + fechas). `AvailableFrom`/`To` opcionales; sin fechas, siempre disponible si `Active` y stock.
- **Reward Audit Entry**: Registro append-only: `RewardId`/`RedemptionId`, `ActorId` (sub), `Timestamp`, `FromState`, `ToState`, `Action`, `Reason?`, `CorrelationId`, `Result`, `IdempotencyKey`.
- **RewardType**: Enumeración de dominio `Monetary`, `Physical`, `Digital`, `Voucher`, `Experience`, `Consolation` (este último con elegibilidad independiente).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Un REWARD_MANAGER completa la creación válida de un premio `Physical` (nombre, tipo, costo 500, stock 10, fechas) en menos de 2 minutos en el 90% de los intentos desde "Crear premio" hasta confirmación `Active`.
- **SC-002**: El 100% de los premios creados con tipo fuera de los 6, costo 0/negativo, stock negativo o fechas `From ≥ To` son rechazados con `InvalidRewardData` por campo sin creación.
- **SC-003**: El 100% de las transiciones `Active ↔ Inactive` y `→ Archived` se ejecutan con éxito cuando hay stock/fechas válidas; el 100% de los intentos con nombre duplicado son rechazados con `RewardAlreadyExists`.
- **SC-004**: El 100% de los canjes `Requested → Approved` con stock y puntos suficientes se ejecutan con éxito, descuentan stock de forma transaccional y generan auditoría; el 100% con stock 0 (`Physical` limitado) son rechazados con `RewardOutOfStock` sin mutación parcial.
- **SC-005**: El 100% de los canjes `Requested → Rejected` (con motivo) y `Approved → Delivered` se ejecutan con éxito y son auditados; el 100% de los intentos `Delivered` sobre `Rejected` son rechazados con `InvalidRedemptionState`.
- **SC-006**: La autorización se respeta en el 100% de los casos: `GAME_MANAGER` ve `Access Denied` en "Crear/Editar/Aprobar" y cualquier intento por API retorna 403 sin fuga; `ADMIN`/`REWARD_MANAGER` operan sin fricción.
- **SC-007**: El formulario y listados de premios/canjes cumplen WCAG 2.2 AA en tema `administration` (contraste, foco visible, navegación teclado, `aria-live` en errores) y son utilizables entre 375 y 1536px sin scroll horizontal y con objetivos táctiles ≥44px.
- **SC-008**: Concurrencia: dos REWARD_MANAGER aprueban el mismo canje `Requested` con stock 1 simultáneamente → uno tiene éxito y el otro recibe `ConcurrencyConflict`/`InvalidRedemptionState` sin doble descuento de stock en el 100% de las pruebas de colisión.
- **SC-009**: El listado de premios pagina correctamente (≥50 premios, 6 tipos) y filtra por tipo/estado/disponibilidad/búsqueda en <2s percibidos con skeleton, sin cargar colecciones completas; igual para canjes por estado/tipo/fecha.
- **SC-010**: El 90% de los operadores completa la tarea "crear premio Voucher 100pts stock 10 → jugador canjea → aprobar → entregar" sin ayuda externa en el primer intento, y los premios `Consolation` solo son canjeables via regla de consolación (no como premio normal).

## Assumptions

- **Reutiliza SPEC-017/009/016**: La app Blazor net10.0 Auto, shell de 10 secciones, BFF YARP, OIDC y `Reward`/`RewardRedemption` de dominio ya existen (009-reward-redemption con `PointTransaction` ledger, SPEC-016 Design System). 023 extiende la superficie administrativa de UI + operaciones de catálogo/canjes, sin crear nueva app ni duplicar autenticación.
- **Estados de catálogo**: `Active` (elegible si stock y fechas lo permiten), `Inactive` (no elegible para nuevos canjes), `Archived` (terminal, solo lectura, no elegible). `Active` no requiere stock >0 si el tipo es `Digital`/`Voucher`/`Consolation` con stock 0 = ilimitado según política.
- **Unicidad**: `Name` único case-insensitive entre premios no archivados para evitar confusión en selectores; duplicados entre archivados se permiten.
- **Tipos**: Catálogo cerrado inicial de 6 (`Monetary`, `Physical`, `Digital`, `Voucher`, `Experience`, `Consolation`) — no texto libre en MVP; valores fuera de catálogo rechazados. `Consolation` es independiente (Constitución C) y su elegibilidad se evalúa server-side.
- **Stock**: `Stock` 0 = ilimitado para `Digital`/`Voucher`/`Experience`/`Consolation` según política; para `Physical`/`Monetary` stock 0 significa sin stock si la política lo define así (documentado en UI con tooltip). Se valida que `Stock` ≥0 siempre.
- **Disponibilidad**: `AvailableFrom`/`AvailableTo` opcionales; sin fechas, el premio es siempre disponible si `Active` y stock. Con fechas, elegible solo si hoy entre `From`/`To`.
- **Costo**: Entero 1–100000 puntos; se descuenta del ledger `PointTransaction` al aprobar (o al solicitar y reembolsar en `Rejected` según política de 009, documentado en UI).
- **Canjes**: `Requested` (solicitado por jugador), `Approved` (aprobado por operador), `Rejected` (rechazado con motivo), `Delivered` (entregado), `Cancelled` (cancelado). `Approve` descuenta stock/puntos de forma transaccional; `Reject` no descuenta o reembolsa según política.
- **Idioma**: Español para etiquetas, coherente con SPEC-017/020, sin i18n en v1.
- **Sin acceso directo a datos**: Todo conteo/validación via BFF; no lectura directa a SQL Server, Oracle ni `identitydb` (Constitución H).
