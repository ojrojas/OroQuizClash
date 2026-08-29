# Feature Specification: Player Rewards

**Feature Branch**: `036-player-rewards`

**Created**: 2026-08-29

**Status**: Ready for Review

**Input**: User description: "036 — Player Rewards Tecnología Angular 22 Objetivo Permitir al jugador consultar y canjear los puntos obtenidos. Descripción La aplicación deberá proporcionar: Points Wallet Rewards Catalog Reward Detail Redeem Confirmation Redemption History Consolation Reward El jugador deberá visualizar: Available Points Required Points Remaining Points Reward Status El canje deberá ser procesado por el backend y nunca directamente por Angular."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Consultar Points Wallet y Rewards Catalog (Priority: P1)

Como jugador autenticado quiero abrir mi Points Wallet para ver mis puntos disponibles y explorar el Rewards Catalog para entender qué recompensas puedo canjear, decidido si realizar un canje.

**Why this priority**: Es el punto de entrada de todo el flujo de recompensas. Sin visualizar Available Points, Required Points, Remaining Points y Reward Status el jugador no puede tomar decisiones. Entrega valor independiente aun sin canjear.

**Independent Test**: Navegar a la sección Rewards / Wallet y verificar que se muestran Available Points y lista de recompensas con Required Points y estado (canjeable / insuficiente) coherentes con el saldo del jugador.

**Acceptance Scenarios**:

1. **Given** jugador con 1200 Available Points, **When** abre Points Wallet / Rewards Catalog, **Then** ve `Available Points = 1200` y cada recompensa muestra `Required Points` y `Reward Status` (por ejemplo "Canjeable" si 1200 >= Required, "Puntos insuficientes" si no).
2. **Given** jugador con 0 puntos, **When** abre el catálogo, **Then** todas las recompensas muestran `Reward Status = Puntos insuficientes` y `Remaining Points = Required - Available` calculado como diferencia positiva.
3. **Given** catálogo con recompensas disponibles y no disponibles, **When** el jugador filtra o recorre la lista, **Then** cada tarjeta muestra `Required Points` y `Reward Status` sin necesidad de abrir detalle.

---

### User Story 2 - Consultar Reward Detail y canjear con confirmación (Priority: P1)

Como jugador quiero ver el detalle de una recompensa (Required Points, descripción, estado) y confirmar el canje para que el sistema descuente mis puntos y registre la redención.

**Why this priority**: Es la acción de valor core — transformar puntos en recompensa. Requiere procesamiento por backend (nunca cálculo directo en Angular) y confirmación explícita para evitar canjes accidentales.

**Independent Test**: Seleccionar una recompensa canjeable, abrir Reward Detail, presionar Redeem, confirmar en diálogo y verificar que el backend procesa el canje y retorna confirmación.

**Acceptance Scenarios**:

1. **Given** recompensa con `Required Points = 800` y jugador con `Available Points = 1200` y estado `Canjeable`, **When** abre Reward Detail, **Then** ve `Available Points 1200`, `Required Points 800`, `Remaining Points 400` (1200-800) y botón `Canjear` habilitado.
2. **Given** Reward Detail de recompensa canjeable, **When** el jugador presiona `Canjear` y confirma `Confirmar canje` en diálogo de confirmación (2 pasos), **Then** el sistema envía solicitud al backend, descuenta puntos y muestra pantalla `Confirmation` con `Reward Status = Canjeada`, `Remaining Points` actualizado y referencia de canje.
3. **Given** recompensa con `Required Points = 1500` y jugador con `800` puntos, **When** abre Reward Detail, **Then** ve `Reward Status = Puntos insuficientes`, `Remaining Points = 700` faltantes y botón `Canjear` deshabilitado con mensaje explicativo.
4. **Given** jugador confirma canje pero el backend responde `Puntos insuficientes` por canje concurrente, **When** se recibe la respuesta, **Then** se muestra error `No tienes puntos suficientes` y `Available Points` se refresca al valor autoritativo del servidor.

---

### User Story 3 - Consultar Redemption History (Priority: P2)

Como jugador quiero consultar mi historial de canjes para auditar qué recompensas he obtenido, cuándo y cuántos puntos consumí.

**Why this priority**: Provee trazabilidad y confianza. No bloquea el canje pero es necesario para la experiencia completa y soporte.

**Independent Test**: Abrir Redemption History y verificar listado cronológico de canjes con fecha, recompensa, puntos consumidos y estado.

**Acceptance Scenarios**:

1. **Given** jugador con 3 canjes previos, **When** abre Redemption History, **Then** ve lista ordenada por fecha descendente con cada entrada: nombre recompensa, `Required Points` consumidos, `Remaining Points` tras canje (o `Available Points` al momento), `Reward Status` (Canjeada / En proceso / Rechazada) y fecha.
2. **Given** jugador sin canjes previos, **When** abre Redemption History, **Then** ve estado vacío con mensaje "Aún no has canjeado recompensas" y CTA a Rewards Catalog.

---

### User Story 4 - Recibir Consolation Reward (Priority: P2)

Como jugador que no alcanzó el umbral para recompensas estándar quiero recibir automáticamente una recompensa de consolación si aplico, para mantener motivación y equidad.

**Why this priority**: Cierra el ciclo de recompensas para jugadores con bajo puntaje y reduce frustración. Depende del motor de reglas del backend.

**Independent Test**: Simular fin de partida donde el jugador no califica para recompensa estándar pero sí para consolación, y verificar que aparece Consolation Reward acreditada en Wallet/History.

**Acceptance Scenarios**:

1. **Given** jugador finaliza partida con puntaje por debajo del umbral estándar pero dentro del rango de consolación definido por configuración, **When** el backend evalúa elegibilidad, **Then** se acredita Consolation Reward, se muestra en Points Wallet (como crédito o recompensa) y en Redemption History con `Reward Status = Consolation` y motivo.
2. **Given** jugador que sí califica para recompensa estándar, **When** se evalúa consolación, **Then** no se otorga consolación adicional (exclusión mutua o regla de prioridad definida por backend).

---

### Edge Cases

- ¿Qué pasa cuando dos canjes concurrentes del mismo jugador solicitan la misma recompensa y solo hay saldo para uno? El backend debe procesar uno exitosamente y rechazar el segundo con `Puntos insuficientes` o `Conflicto de concurrencia`; Angular nunca decide localmente.
- ¿Cómo maneja el sistema un intento de canje con saldo manipulado en cliente (DevTools/alteración de Available Points)? El backend recalcula saldo autoritativo desde ledger de `PointTransaction`; la UI solo refleja lo que el servidor confirma.
- ¿Qué ocurre si la recompensa es despublicada o agotada entre que el jugador abre Reward Detail y confirma? El backend retorna `Recompensa no disponible` y la UI actualiza el catálogo.
- ¿Qué pasa si el jugador pierde autenticación durante el canje? Se redirige a login y se preserva el intento de canje para reintento tras re-autenticar, sin duplicar transacciones.
- ¿Cómo se muestra `Remaining Points` cuando el jugador tiene exactamente los puntos requeridos? Debe ser `0` y estado `Canjeable`, sin valores negativos.
- ¿Qué ocurre con `Consolation Reward` si el jugador abandona la partida (withdraw/eliminated)? La elegibilidad se evalúa solo sobre partidas finalizadas y no retiradas, según reglas de dominio.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema MUST mostrar al jugador autenticado su `Available Points` (saldo de puntos disponibles) obtenido desde el backend como valor autoritativo, nunca calculado en cliente.
- **FR-002**: El sistema MUST presentar un `Rewards Catalog` con todas las recompensas publicadas, cada una mostrando `Required Points` y `Reward Status` (al menos: Canjeable / Puntos insuficientes / No disponible / Agotada) derivado de `Available Points` vs `Required Points` y disponibilidad de la recompensa.
- **FR-003**: El sistema MUST proporcionar una vista `Reward Detail` que muestre al menos: nombre, descripción, `Required Points`, `Available Points` del jugador, `Remaining Points` (diferencia entre Available y Required, `max(0, Available - Required)` si canjeable o faltante si no canjeable según regla de visualización) y `Reward Status` actualizado.
- **FR-004**: El sistema MUST habilitar la acción `Redeem` solo cuando `Reward Status = Canjeable` (Available >= Required y recompensa disponible); en caso contrario el botón debe estar deshabilitado con explicación.
- **FR-005**: La acción `Redeem` MUST requerir confirmación explícita en 2 pasos (seleccionar Canjear → diálogo de confirmación con resumen de `Required Points` / `Remaining Points` → Confirmar) para evitar canjes accidentales.
- **FR-006**: El canje MUST ser procesado exclusivamente por el backend (descuento de puntos, validación de saldo, registro de `RewardRedemption` y creación de `PointTransaction` ledger). Angular MUST nunca descontar puntos directamente ni crear transacciones localmente; solo refleja el resultado del servidor.
- **FR-007**: Tras un canje exitoso el sistema MUST mostrar una vista `Confirmation` con: recompensa canjeada, puntos consumidos (`Required Points`), `Remaining Points` / saldo actualizado, fecha y referencia/ID de canje, y `Reward Status = Canjeada`.
- **FR-008**: El sistema MUST proveer `Redemption History` paginado/ordenado por fecha descendente con cada registro: recompensa, puntos consumidos, fecha, `Reward Status` y referencia.
- **FR-009**: El sistema MUST reflejar en toda la UI el saldo autoritativo retornado por el backend después de cada canje; cualquier discrepancia entre valor mostrado en cliente y servidor MUST resolverse a favor del servidor.
- **FR-010**: El sistema MUST manejar el `Consolation Reward` como recompensa otorgada por el backend según reglas configurables de elegibilidad al finalizar una partida; el jugador debe visualizarla en Wallet y en History con `Reward Status = Consolation` y sin poder canjearla manualmente como una recompensa estándar.
- **FR-011**: El sistema MUST impedir canjes duplicados idempotentes: reintentos de la misma solicitud de canje (mismo jugador + misma recompensa + misma clave de idempotencia si aplica) no deben generar múltiples `PointTransaction` ni múltiples redenciones.
- **FR-012**: El sistema MUST exigir autenticación para todas las operaciones de recompensas (ver Wallet, ver Catalog, ver Detail, canjear, ver History); solicitudes sin autenticación MUST retornar error de autenticación y redirigir a login.
- **FR-013**: El sistema MUST comunicar errores de canje de forma accionable: `Puntos insuficientes`, `Recompensa no disponible / agotada`, `Conflicto de concurrencia`, `No autenticado`, con mensaje claro y sin exponer detalles sensibles del servidor.

### Key Entities *(include if feature involves data)*

- **PlayerPoints / Points Wallet**: Saldo de puntos del jugador. Atributos: `playerId`, `availablePoints`, `totalEarned`, `totalRedeemed`, `lastUpdated`. Fuente autoritativa backend via ledger `PointTransaction`.
- **Reward**: Recompensa canjeable definida por configuración. Atributos: `rewardId`, `name`, `description`, `requiredPoints`, `status` (Publicada/No publicada/Agotada), `stock` opcional, `category`.
- **RewardRedemption / Redemption**: Registro de un canje. Atributos: `redemptionId`, `playerId`, `rewardId`, `requiredPoints`, `remainingPoints` (o saldo resultante), `status` (Canjeada / Rechazada / En proceso / Consolation), `redeemedAt`, `reference`.
- **Redemption History**: Colección ordenada de `RewardRedemption` por jugador.
- **Consolation Reward**: Subtipo de `Reward`/`Redemption` otorgada automáticamente por reglas de consolación (elegibilidad, umbral). Atributos adicionales: `eligibilityReason`, `sourceGameId` si aplica.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: El 100% de las visualizaciones de `Available Points` en Wallet, Catalog y Reward Detail coinciden con el saldo autoritativo del backend (0% cálculo de saldo en cliente) en pruebas de contrato.
- **SC-002**: El jugador puede completar el flujo completo Wallet → Catalog → Detail → Redeem → Confirmation en menos de 90 segundos en condiciones normales de red.
- **SC-003**: El 100% de los intentos de canje se procesan en backend con validación de saldo y ledger; 0 canjes son aceptados basándose únicamente en estado del cliente (verificado por pruebas de manipulación DevTools).
- **SC-004**: La tasa de canjes accidentales (canje sin confirmación explícita de 2 pasos) es 0% en pruebas de usabilidad; el botón Canjear requiere confirmación y no se dispara con un solo clic.
- **SC-005**: El 100% de los reintentos idempotentes del mismo canje no generan transacciones duplicadas (ledger contiene una sola `PointTransaction` por clave de idempotencia) en pruebas de concurrencia.
- **SC-006**: Redemption History muestra el listado correcto y ordenado para el 100% de los jugadores con canjes previos y estado vacío accionable para el 100% sin canjes.
- **SC-007**: Consolation Reward se acredita correctamente para el 100% de los jugadores elegibles según reglas configurables y aparece en Wallet/History con estado diferenciado, sin duplicar recompensas estándar.
- **SC-008**: El 95% de los errores de canje (puntos insuficientes, no disponible, no autenticado) muestran mensaje accionable en menos de 1 segundo tras la respuesta del servidor.

## Assumptions

- El flujo de recompensas se extiende sobre la SPA `QuizArena.Player` Angular 22 existente (standalone, `signal()`/`computed()`, `@ngrx/signals`) y reutiliza la infraestructura de autenticación OIDC PKCE con `OroIdentityServer` ya vigente en el sistema (constitución VI, H).
- `Available Points` proviene del `Score` / `PointTransaction` ledger existente (SPEC-008/032) y no requiere nuevo agregado de puntuación; este feature añade lectura específica para Rewards y escritura de `RewardRedemption`.
- `Rewards Catalog` y `Reward Detail` usan datos de recompensas ya administrables por el Admin (SPEC-023 Admin Rewards); si no existen recompensas publicadas, el catálogo muestra estado vacío con CTA.
- `Required Points` lo define la configuración de la recompensa en backend; `Remaining Points` es una proyección de lectura `max(0, Available - Required)` para canjeables o `Required - Available` como faltante cuando no alcanza, calculada en servidor o derivada de valores autoritativos sin lógica de negocio en cliente.
- `Reward Status` incluye al menos: `Canjeable` (Available >= Required y disponible), `Puntos insuficientes` (Available < Required), `No disponible`/`Agotada`; el catálogo puede mostrar estados adicionales según configuración.
- El procesamiento de canje implementa validación de saldo y concurrencia (`RowVersion` / control optimista) e idempotencia por clave (`X-Idempotency-Key` o equivalente) en backend, siguiendo patrón ya usado en `WithdrawPlayer`/`SubmitAnswer`.
- `Consolation Reward` se resuelve en backend al finalizar partida mediante reglas configurables (umbrales de `GameConfiguration`); no es canjeable manualmente y se otorga una sola vez por elegibilidad.
- Diseño visual sigue `design-system/tokens` con `data-theme="player"` y WCAG 2.2 AA (targets >=44px, `role="dialog"` para confirmación, `aria-live` para mensajes de estado) coherente con SPEC-016/027/035.

## Dependencies

- SPEC-027 Player Application, SPEC-029 Player Game, SPEC-032 Player Scoring, SPEC-035 Player Withdrawal (wallet y ledger base).
- BuildingBlocks: `AggregateRoot`, `IBusinessRule`, `Result`, `IRepository`, `IUnitOfWork`, `ICommand`/`IQuery`/`ISender`, `IEndpoint`, `AppDbContextBase` + Outbox.
- OroIdentityServer (OAuth2/OIDC) para autenticación/autorización de operaciones de recompensas.
- Domain `Game`/`Player`/`PointTransaction`/`Reward`/`RewardRedemption` para veredicto de elegibilidad de recompensas y de consolación.

## Out of Scope

- Creación/administración de recompensas (corresponde a Admin Rewards SPEC-023).
- Definición de reglas de puntuación por respuesta (SPEC-032).
- Proceso de entrega física/logística de premios externos; solo se registra el canje y la redención.
- Gamificación adicional (niveles, badges, leaderboards de recompensas) fuera de `Available/Required/Remaining/Status`.

## References

- Constitución v1.1.0 (I-VI, A-J) – en especial V Server Truth, VI OroIdentityServer, D Ledger, F Concurrency/Idempotency.
- SPEC-016 UI/UX Design System, SPEC-027/029/032/033/035 Player flows previos.
- `.specify/memory/constitution.md` governance constraints.

