# Feature Specification: Player Lobby

**Feature Branch**: `028-player-lobby`

**Created**: 2026-08-28

**Status**: Ready for Review

**Input**: User description: "028 — Player Lobby Objetivo Permitir al jugador descubrir, seleccionar y entrar a una partida. Descripción El lobby deberá mostrar: Available Games Game Name Category Difficulty Number of Rounds Players Start Time Prize Status El jugador podrá: Join Game Leave Lobby View Game Information"

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Descubrir partidas disponibles en el lobby (Priority: P1)

Como jugador autenticado, quiero ver la lista de partidas disponibles con su información clave para decidir a cuál unirme.

**Why this priority**: Es el punto de entrada del jugador al juego; sin descubrir partidas no hay participación. Entrega valor independiente como MVP de solo lectura.

**Independent Test**: Con 3 juegos en estado `WAITING_FOR_PLAYERS` y 2 en `IN_PROGRESS`/`FINISHED`, al abrir el lobby el jugador ve solo los disponibles (Available Games) con columnas Game Name, Category, Difficulty, Number of Rounds (Min/Max), Players (actual/max), Start Time, Prize, Status. Verificar que juegos no disponibles no aparecen y que cada fila muestra los 8 campos correctos.

**Acceptance Scenarios**:

1. **Given** jugador autenticado en `/player/lobby` y existen juegos disponibles, **When** abre el lobby, **Then** ve lista paginada de Available Games con 8 columnas (Game Name, Category, Difficulty, Number of Rounds, Players, Start Time, Prize, Status) ordenada por Start Time descendente.
2. **Given** no hay juegos disponibles, **When** abre el lobby, **Then** ve estado Empty "No hay partidas disponibles" con CTA para refrescar (no error 500).
3. **Given** juegos con distintos Status, **When** consulta el lobby, **Then** solo ve aquellos con Status `WAITING_FOR_PLAYERS` y `Category` publicada (filtrado server-side), verificado por API `GET /api/games?status=WAITING_FOR_PLAYERS`.
4. **Given** lobby con más de 20 juegos, **When** pagina, **Then** ve paginación y mantiene filtros sin pérdida de datos.

---

### User Story 2 — Unirse a una partida desde el lobby (Priority: P1)

Como jugador en el lobby, quiero unirme a una partida disponible para participar.

**Why this priority**: Convierte el descubrimiento en participación efectiva; núcleo del flujo lobby→juego.

**Independent Test**: Seleccionar un juego disponible y ejecutar `Join Game` → verificar que el jugador aparece en `Players` count incrementado, su `GameSession` pasa a `ACTIVE`, y es redirigido a `/player/game/:gameId`; intentar unirse de nuevo es idempotente. Verificar que un segundo jugador puede unirse al mismo juego sin interferir score/estado del primero.

**Acceptance Scenarios**:

1. **Given** jugador en lobby con juego disponible con cupo, **When** pulsa `Join Game`, **Then** se crea `GameSession` `ACTIVE` via `POST /api/games/{id}/players` y es redirigido a la sala del juego con confirmación visual.
2. **Given** jugador ya unido al juego, **When** pulsa `Join Game` de nuevo (doble clic o reload con `X-Idempotency-Key`), **Then** el servidor retorna 200 idempotente sin duplicar `GamePlayer` ni incrementar Players dos veces.
3. **Given** juego lleno (`Players == MaxPlayers`) o ya no en `WAITING_FOR_PLAYERS`, **When** intenta `Join Game`, **Then** recibe error `400 GameFull` o `400 GameNotWaitingForPlayers` con mensaje amigable y CTA volver al lobby (RFC 7807).
4. **Given** jugador no autenticado, **When** intenta `Join Game`, **Then** es redirigido a login OIDC y tras autenticar puede reintentar (Constitución VI).

---

### User Story 3 — Ver información detallada de la partida (Priority: P2)

Como jugador, quiero ver los detalles completos de una partida antes de unirme para evaluar si me conviene.

**Why this priority**: Mejora decisión informada y reduce abandonos; complementa US1 sin bloquear MVP.

**Independent Test**: Desde el lobby pulsar `View Game Information` en una fila → ver vista detalle con los 8 campos + reglas extendidas (TimeLimitPerQuestion, PointsPerRound, Withdrawal/LossPolicy, Prize breakdown, jugadores inscritos). Verificar que la información coincide con `GET /api/games/{id}`.

**Acceptance Scenarios**:

1. **Given** jugador en lobby, **When** pulsa `View Game Information` en un juego, **Then** ve modal/página detalle con Game Name, Category, Difficulty, Number of Rounds, Players (lista nombres), Start Time, Prize, Status + configuración extendida.
2. **Given** detalle abierto, **When** el juego cambia de estado (ej. pasa a `IN_PROGRESS` por otro evento), **Then** al refrescar ve `Status` actualizado sin mostrar datos obsoletos (server truth).
3. **Given** juego no existe (ID manipulado), **When** solicita detalle, **Then** recibe `404 GameNotFound` con estado Error y `CorrelationId` visible para soporte.

---

### User Story 4 — Salir del lobby sin participar (Priority: P2)

Como jugador, quiero salir del lobby para volver al inicio sin haber elegido partida, sin efectos colaterales.

**Why this priority**: Completa experiencia de navegación y accesibilidad; acción reversible y sin estado.

**Independent Test**: Desde el lobby pulsar `Leave Lobby` → verificar navegación a `/` o página anterior sin crear `GameSession`, sin llamada de escritura a API, y sin pérdida de sesión OIDC.

**Acceptance Scenarios**:

1. **Given** jugador en lobby sin haberse unido, **When** pulsa `Leave Lobby`, **Then** es navegado fuera del lobby (ej. `/`) sin mutar estado del jugador en ningún juego.
2. **Given** jugador que ya se unió a un juego y volvió al lobby, **When** pulsa `Leave Lobby`, **Then** sale del lobby pero mantiene su `GameSession` `ACTIVE` en el juego previo (no hace Withdraw automático).

---

### Edge Cases

- ¿Qué pasa si el lobby carga mientras un juego cambia de `WAITING_FOR_PLAYERS` a `IN_PROGRESS` entre el `GET /api/games` y el `POST /api/games/{id}/players`? Sistema retorna `400 InvalidGameState` y sugiere refrescar lista.
- ¿Cómo maneja el sistema 100 juegos disponibles concurrentemente con paginación y filtros? Usa paginación server-side `page/pageSize` + Specification sin traer todo a memoria.
- ¿Qué ocurre si `Prize` es nulo o no configurado? Muestra placeholder "Sin premio" sin romper layout (compatible con `Reward` opcional).
- ¿Qué ocurre si `Category` fue despublicada tras listar? La fila debe desaparecer al refrescar; `View Information` muestra `CategoryNotReady` si se intenta acceder.
- ¿Cómo se comporta `Start Time` con zonas horarias? Server envía ISO 8601 UTC; cliente lo muestra en zona local con formato relativo.
- ¿Qué pasa si el token expira mientras el jugador navega el lobby? Interceptor 401 dispara `silentRenew`/`refresh_token`; si falla, redirige a OIDC `connect/authorize` (VI).
- ¿Qué pasa si el jugador intenta `Join Game` en dos pestañas simultáneas? Idempotencia por `X-Idempotency-Key` + `rowversion` evita duplicados (F).
- ¿Qué ocurre con accesibilidad si la tabla tiene 8 columnas en móvil 375px? Diseño responsive: tarjetas apiladas con mismos 8 campos, no scroll horizontal, targets ≥44px, `aria-live` para lista (SPEC-016).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El lobby DEBE mostrar solo juegos en estado `WAITING_FOR_PLAYERS` (Available Games) obtenidos vía `GET /api/games?status=WAITING_FOR_PLAYERS` con paginación server-side (`page`, `pageSize`, default 20).
- **FR-002**: Cada fila de Available Games DEBE mostrar 8 campos: Game Name, Category (nombre), Difficulty (nivel 1..5), Number of Rounds (MinRounds/MaxRounds o `Min-Max`), Players (actual/max), Start Time (ISO UTC → local), Prize (si existe `Reward`/`Consolation`, si no "—"), Status (`WAITING_FOR_PLAYERS`).
- **FR-003**: El lobby DEBE permitir `View Game Information` para cualquier juego listado, mostrando detalle obtenido de `GET /api/games/{id}` con los 8 campos + extendidos (TimeLimitPerQuestion, PointsPerRound, WithdrawalPolicy, LossPolicy, jugadores inscritos count).
- **FR-004**: El lobby DEBE exponer acción `Join Game` por fila solo si `Players < MaxPlayers` y `Status==WAITING_FOR_PLAYERS`; al activar DEBE invocar `POST /api/games/{id}/players` con `X-Idempotency-Key` (UUID, `sessionStorage` por `gameId`) y `Authorization: Bearer` (OIDC PKCE, Constitution VI/H).
- **FR-005**: `Join Game` DEBE ser idempotente: reintento con misma `X-Idempotency-Key` retorna mismo `GameSession` sin duplicar `GamePlayer` (verificado por `UNIQUE (GameId,UserId)`).
- **FR-006**: Si `Join Game` falla por `GameFull`, `GameNotWaitingForPlayers`, `AlreadyJoined`, el sistema DEBE mapear a RFC 7807 `ProblemDetails` (400/409) con mensaje amigable y `CorrelationId/TraceId` visible; no debe crear `GameSession` fantasma.
- **FR-007**: El lobby DEBE exponer acción `Leave Lobby` que navega fuera sin invocar API de escritura ni mutar `GameSession`/`Game`; no DEBE hacer Withdraw automático.
- **FR-008**: `Leave Lobby` DEBE ser accesible por teclado (Tab/Enter) y lector (aria-label) y funcionar sin autenticación adicional más allá de la sesión existente.
- **FR-009**: El lobby DEBE respetar seguridad delegada: todas las consultas y `Join Game` requieren JWT válido contra OroIdentityServer `jwks_uri`; sin JWT → `401` y redirect a OIDC discovery; `PlayerId` es `sub` del token (no body param).
- **FR-010**: El lobby NO DEBE confiar en estado cliente para decidir cupo o status: validación autoritativa server-side con `RowVersion` y `GameStatus.IsValidTransition`; cliente solo proyecta.
- **FR-011**: El lobby DEBE soportar paginación, orden por `Start Time` (o `CreatedAt`) y estar preparado para filtros futuros (Category, Difficulty) sin cambiar contrato base.
- **FR-012**: El lobby DEBE mostrar estados de UI Loading (skeleton), Empty (no disponibles), Error (ProblemDetails con Retry + CorrelationId) y Ready, cumpliendo WCAG 2.2 AA (contraste, foco visible, `aria-live="polite"` para lista, responsive 375-1536 sin scroll horizontal, targets ≥44px, SPEC-016).
- **FR-013**: `View Game Information` DEBE ser de solo lectura y no exponer `Answer`/`Score` de otros jugadores; filtra información sensible si aplica.
- **FR-014**: El lobby DEBE propagar `X-Correlation-Id` por request y mostrar `CorrelationId/TraceId` en errores para auditoría/observabilidad (Constitución I).
- **FR-015**: El lobby NO DEBE exponer lógica autoritativa de selección de pregunta, scoring o timer; solo consume proyecciones de `oroclash-api` (Constitución V).

### Key Entities *(include if feature involves data)*

- **Game (proyección lobby)**: `GameId`, `Name` (Game Name), `CategoryId/CategoryName`, `Difficulty` (InitialDifficulty 1..5), `Configuration` (`MinRounds/MaxRounds` → Number of Rounds, `TimeLimitPerQuestion`, `PointsPerRound`, políticas), `Players` count (`Players.Count/MaxPlayers`), `StartTime` (`CreatedAt` o `StartedAt`), `Prize` (derivado de `Reward`/`RewardRules` si existe), `Status` (`WAITING_FOR_PLAYERS` para Available Games), `RowVersion`.
- **Category**: `CategoryId`, `Name`, `IsPublished`; usada para mostrar Category y filtrar juegos disponibles.
- **GamePlayer / GameSession**: `GamePlayerId`, `GameId`, `UserId` (`sub` JWT), `Status` (`ACTIVE`), `JoinedAt`; creado por `Join Game`, `UNIQUE (GameId,UserId)`.
- **Reward (Prize)**: `RewardId`, `Name`, `PointsRequired`; opcional, si el juego define premio se resuelve para columna Prize.
- **ProblemDetails**: `type/title/status/detail/code/correlationId/traceId` (RFC 7807) para errores de lobby.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% de juegos mostrados en lobby tienen `Status==WAITING_FOR_PLAYERS` y `Category` publicada; 0% de juegos en `FINISHED/CANCELLED` aparecen como disponibles (verificado por `GET /api/games?status=...` + DB `Status` index).
- **SC-002**: 100% de filas muestran los 8 campos (Game Name, Category, Difficulty, Number of Rounds, Players, Start Time, Prize, Status) sin valores nulos inesperados; `Prize` nulo muestra placeholder sin romper layout.
- **SC-003**: 95% de intentos `Join Game` con cupo válido completan en <1s percibido (desde clic hasta redirección) y `Players` count se incrementa atomáticamente sin duplicados bajo doble clic (idempotencia).
- **SC-004**: 100% de reintentos `Join Game` con misma `X-Idempotency-Key` retornan mismo `GameSession` sin crear segundo `GamePlayer` (verificado por `UNIQUE (GameId,UserId)` + count).
- **SC-005**: 100% de intentos `Join Game` en juego lleno o no en `WAITING_FOR_PLAYERS` son rechazados con mensaje amigable y no crean `GameSession` (400/409).
- **SC-006**: 100% de acciones `Leave Lobby` completan sin llamada de escritura y sin mutar `GameSession`; navegación <500ms sin pérdida de sesión OIDC.
- **SC-007**: 100% de `View Game Information` muestran datos consistentes con `GET /api/games/{id}` (8 campos + extendidos) y `Start Time` convertido correctamente a zona local.
- **SC-008**: Lobby cumple WCAG 2.2 AA (contraste, foco visible, teclado, `aria-live`, targets ≥44px) y responsive 375-1536 sin scroll horizontal (verificado por axe/Lighthouse en `design-system` tokens).
- **SC-009**: 100% de requests de lobby incluyen `X-Correlation-Id` y errores muestran `CorrelationId/TraceId`; 100% requieren JWT válido, sin JWT → `401` redirect OIDC.

## Assumptions

- Se reutiliza `oroclash-api` existente: `GET /api/games` (paginado, `GameFilterSpecification` con `Status==WAITING_FOR_PLAYERS`), `GET /api/games/{id}` y `POST /api/games/{id}/players` (`JoinGame` slice, Vertical Slice CQRS `IEndpoint`, `ISender`, `Result→HTTP` ProblemDetails). No se crean nuevos agregados; `Prize` es proyección de `Reward`/`GameConfiguration.RewardRules` si existe.
- Autenticación 100% delegada a OroIdentityServer (Constitución VI/H): Angular 22 SPA `angular-auth-oidc-client` PKCE `authorization_code` + `refresh_token` (research R1 SPEC-027) o BFF YARP (SPEC-017) si el equipo lo elige; `sub` es `PlayerId`; validación `jwks_uri` en `oroclash-api`.
- `Available Games` se define como `Status==WAITING_FOR_PLAYERS` (State Machine A). Juegos en otros estados no son joinables desde Player Lobby (Admin los gestiona).
- `Number of Rounds` se muestra como `MinRounds-MaxRounds` (ej. "5-10") o `MinRounds` si no hay rango distinto; `Players` como "3/10".
- `Start Time` es `Game.CreatedAt` o `Game.StartedAt` si ya existe; si el dominio no tiene `ScheduledAt` explícito, se usa `CreatedAt` ordenado descendente.
- `Prize` opcional: si `GameConfiguration.RewardRules` define `RewardId`, se resuelve nombre del `Reward`; si no, "—" sin bloquear lobby.
- `Leave Lobby` es navegación `Router.navigate(['/'])` o `location.back()` sin API; no equivale a `WithdrawPlayer` (explicit domain action, terminal).
- Paginación default `page=1 pageSize=20`, orden `StartTime desc`; cliente puede pedir `pageSize` mayor pero server limita a 50 (query-pattern index).
- Tokens nunca en `localStorage` (XSS); interceptor `authInterceptor` adjunta `Authorization: Bearer` solo a `apiUrl` (secureRoutes), `correlationIdInterceptor` genera UUID.
- Design System `design-system/tokens/design-tokens.css` + `overrides/player.md` (`data-theme="player"`) provee WCAG AA y responsive.

## Dependencies

- SPEC-001 `Game Configuration` (MinRounds≥5, Difficulty, Category, MaxPlayers).
- SPEC-002 `Categories` (Category publicada, ≥5 preguntas válidas).
- SPEC-004 `Game Lifecycle` (State Machine `WAITING_FOR_PLAYERS→IN_PROGRESS`, `GamePlayer` lifecycle).
- SPEC-006 `Answer Evaluation` / SPEC-007 `Scoring` no bloqueantes (lobby es pre-juego).
- SPEC-012 `Realtime Game Events` (no requerido para lobby, pero `JoinGame` dispara `PlayerJoinedDomainEvent` vía Outbox si se publica).
- SPEC-016 `UI/UX Design System` (`design-system/MASTER.md`, tokens, WCAG, 375-1536, 44px).
- SPEC-017 `Admin Application` BFF YARP patrón si se elige BFF para Player.
- SPEC-027 `Player Application` (`QuizArena.Player` Angular 22 SPA, `PlayerGameStore`, `GamesApi`, OIDC PKCE, AppHost `quizarena-player`).
- BuildingBlocks (`Kernel.Domain` `AggregateRoot/Entity/IBusinessRule/Result`, `CQRS ISender/IEndpoint`, `ServiceDefaults` OTel/health, `Kernel.Infrastructure AppDbContextBase EfRepository Specification Outbox`).
- OroIdentityServer `oroidentityserver:latest` discovery `/.well-known/openid-configuration`, `jwks_uri`, `authorization_code`+PKCE, `refresh_token`.

## Out of Scope

- Creación de juegos (lo hace Admin/Game Manager vía `POST /api/games` SPEC-001).
- Lógica de selección de pregunta, evaluación, scoring, timer, withdrawal, finish, rewards, consolation (SPEC-005-010).
- Matchmaking automático, invitaciones, amigos, chat.
- Global leaderboards (SPEC-011) más allá del conteo Players del lobby.
- Notificaciones push/SignalR en lobby (Server-driven notifications `RoundStarted` etc. son para juego activo, no para Available Games).
- Administración de categorías/premios (SPEC-020/023).
- Soporte offline (sin conexión no hay lobby).
- Filtros avanzados por Category/Difficulty/Prize más allá de paginación/orden base (futuro).
- `Leave Lobby` no hace Leave Game/Withdraw; eso es `POST /api/games/{id}/withdraw` (SPEC-008).

## References

- `draft/constitution.md` §I-VI, §A-J (Domain First, Authoritative Server Truth, OroIdentityServer).
- `draft/game-concept.md` §Game Lifecycle A, §C Configurable Rules.
- `draft/oroidentityserver-specification.md` (OIDC discovery, PKCE, `jwks_uri`).
- `design-system/MASTER.md` + `design-system/overrides/player.md` + `design-system/tokens/design-tokens.css` (WCAG, responsive, `data-theme="player"`).
- `src/Player/QuizArena.Player` (Angular 22 SPA, `app.routes.ts` `/lobby`, `stores/player-game.store.ts`, `features/shared/games.api.ts` `getGames`/`joinGame`, `core/interceptors/`).
- `src/OroQuizClash.Application/Features/Games/` (`GetGame`, `JoinGame`, `GetGames` vía `GameFilterSpecification`, `IEndpoint`).
- `OroQuizClash.AppHost/AppHost.cs` (`quizarena-player` AddNpmApp/AddContainer → `oroclash-api`).
- `specs/027-player-application/` (dependencia directa: lobby es vista previa a Player Game Session).
