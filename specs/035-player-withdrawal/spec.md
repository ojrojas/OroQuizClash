# Feature Specification: Player Withdrawal

**Feature Branch**: `035-player-withdrawal`

**Created**: 2026-08-29

**Status**: Ready for Review

**Input**: User description: "035 — Player Withdrawal Tecnología Angular 22 Objetivo Permitir al jugador retirarse voluntariamente conservando los puntos asegurados. Descripción La interfaz deberá mostrar claramente: Current Points Secured Points Potential Points Antes de confirmar: "If you continue and answer incorrectly, you may lose your accumulated points." "Withdraw now and secure X points?" El retiro deberá requerir confirmación. Una vez confirmado: PlayerWithdrawn El jugador no podrá continuar participando en la partida."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Visualizar puntuaciones antes de retirarse (Priority: P1)

Como jugador considerando retirarse, quiero ver claramente mis `Current Points`, `Secured Points` y `Potential Points` en el diálogo de confirmación, para decidir con información completa.

**Why this priority**: Es el contexto requerido antes de confirmar ("La interfaz deberá mostrar claramente: Current/Secured/Potential"). Sin estas 3 métricas el jugador no entiende qué arriesga. Entrega valor independiente como proyección autoritativa antes de `Withdraw`.

**Independent Test**: Con `Game` en `ROUND_IN_PROGRESS` `Score` `Current 400 Secured 200 Potential 100`, abrir `Withdrawal Action` → verificar diálogo muestra `Current Points 400 pts`, `Secured Points 200 pts · checkpoint 2`, `Potential Points 100 pts` coincidentes con `GET /players/me` ledger, sin cálculo cliente.

**Acceptance Scenarios**:

1. **Given** jugador `ACTIVE` con `Current 400`, `Secured 200 checkpoint 2`, `Potential 100`, **When** pulsa `Withdrawal Action`, **Then** ve diálogo con las 3 métricas `Current`/`Secured`/`Potential` con formato `"{n} pts"` y `Secured` con `checkpoint 2` si aplica.
2. **Given** `Potential Points` no configurado, **When** abre diálogo, **Then** ve `Potential` "—" sin romper layout.
3. **Given** `Secured` `checkpoint null`, **When** ve diálogo, **Then** ve `Secured 200 pts` sin "checkpoint".
4. **Given** lector de pantalla, **When** navega diálogo, **Then** anuncia "Current Points 400 puntos, Secured 200 checkpoint 2, Potential 100 puntos" con `aria-live polite`.

---

### User Story 2 — Confirmación con warnings de riesgo (Priority: P1)

Como jugador, quiero ver warnings explícitos antes de confirmar: _"If you continue and answer incorrectly, you may lose your accumulated points."_ y _"Withdraw now and secure X points?"_ y que el retiro requiera confirmación explícita, para evitar retiros accidentales y entender el riesgo de continuar.

**Why this priority**: Cubre "Antes de confirmar" con mensajes de riesgo y requisito de confirmación. Sin confirmación + warnings el retiro es propenso a error y el jugador no entiende que `LossPolicy` puede hacer perder puntos.

**Independent Test**: Abrir diálogo de retiro → verificar dos textos de warning exactos: "If you continue and answer incorrectly, you may lose your accumulated points." y "Withdraw now and secure 200 points?" (X = `SecuredPoints`), y que el botón `Confirmar` está deshabilitado hasta interacción explícita o requiere 2 pasos (abrir → confirmar), y `Cancelar` cierra sin llamar `POST /withdraw`.

**Acceptance Scenarios**:

1. **Given** diálogo de retiro abierto con `Secured 200`, **When** lo ve, **Then** muestra warning 1: "If you continue and answer incorrectly, you may lose your accumulated points." con `role="alert"` `aria-live assertive` y warning 2: "Withdraw now and secure 200 points?" dinámico con `Secured` valor.
2. **Given** diálogo abierto, **When** pulsa `Cancelar` o `Escape` o click fuera, **Then** cierra sin llamar `POST /withdraw` y vuelve a pantalla de juego con `canAnswer=true` aún.
3. **Given** diálogo abierto, **When** pulsa `Confirmar` (target ≥44px), **Then** envía `POST /api/games/{id}/withdraw` con `X-Idempotency-Key` `sessionStorage` per `gameId` + `Authorization Bearer`, con `aria-label` "Confirmar retiro".
4. **Given** intenta retiro sin confirmación (ej. `Enter` sin foco en `Confirmar`), **When** pulsa, **Then** no se envía y muestra validación local sin llamada.

---

### User Story 3 — Retiro confirmado PlayerWithdrawn terminal (Priority: P1)

Como jugador que confirma retiro, quiero que mi estado pase a `PlayerWithdrawn` (`WITHDRAWN`) y que no pueda continuar participando (respuestas bloqueadas `canAnswer=false`, `isTerminal=true`), conservando solo `Secured Points` según política `KEEP_SECURED_SCORE`.

**Why this priority**: Cubre "Una vez confirmado: PlayerWithdrawn — El jugador no podrá continuar". Es el efecto terminal autoritativo (V). Sin bloqueo el jugador retirado podría seguir respondiendo.

**Independent Test**: Con `Current 400 Secured 200`, abrir diálogo → `Confirmar` → verificar `POST /withdraw` 200, `GameSession.status WITHDRAWN` `RowVersion++`, `PlayerGameStore.status.isTerminal true` `canAnswer false`, `QuestionComponent` bloqueado `aria-disabled`, `Score` `Current` cae a `Secured` 200 si `KEEP_SECURED_SCORE`, e intento posterior `POST /answers` rechazado 403 `PlayerNotActive`.

**Acceptance Scenarios**:

1. **Given** `Secured 200` `Current 400` `KEEP_SECURED_SCORE`, **When** confirma retiro, **Then** recibe `WITHDRAWN` y ve `Player Status WITHDRAWN` `isTerminal true` `canAnswer false` y `Current Points` 200 (solo asegurados) tras `hydrate`.
2. **Given** ya `WITHDRAWN`, **When** intenta `POST /answers` o `POST /withdraw` de nuevo, **Then** servidor responde `403 PlayerAlreadyWithdrawn` o idempotente sin nuevo `PointTransaction` ledger, UI muestra `ErrorState` con `CorrelationId` sin duplicar.
3. **Given** `WITHDRAWN`, **When** recarga `/player/game/:id`, **Then** ve estado `WITHDRAWN` persistente vía `hydrate` `GET /players/me` y `Question` bloqueada `aria-disabled`.
4. **Given** `WITHDRAWN`, **When** abre `/player/game/:id/result`, **Then** ve `YOU WALKED AWAY` `Secured 200 · checkpoint 2` (034).

---

### User Story 4 — Responsive, accesible y premium del flujo de retiro (Priority: P2)

Como jugador en móvil/desktop, quiero que el flujo de retiro sea accesible por teclado, con foco visible, `data-theme="player"` sin literales, y responsive sin scroll.

**Why this priority**: Completa la experiencia con WCAG y premium (SPEC-016). Sin ello el diálogo no es usable en móvil ni pasa auditoría.

**Independent Test**: Abrir diálogo en 375px → 3 métricas + 2 warnings apilados sin scroll horizontal, en ≥768px centrado `max-width 400px`; verificar `data-theme="player"` 0 literales `var(--space-*)`, `axe` 0 violations `role="dialog"` `aria-modal`, `Tab` navega `Cancelar`/`Confirmar` 100%, `Escape` cierra, `prefers-reduced-motion` sin `scale`.

**Acceptance Scenarios**:

1. **Given** viewport 375px, **When** abre diálogo, **Then** 3 métricas + warnings en 1 col gap `var(--space-3)` targets ≥44px sin scroll.
2. **Given** `data-theme="player"`, **When** inspecciona CSS, **Then** 0 literales hardcodeados para color/spacing/typography/radius.
3. **Given** `axe`, **When** corre, **Then** 0 violations: `role="dialog"` `aria-modal` `aria-label` "Confirmar retiro", foco `outline:2px`, `aria-live` warnings.
4. **Given** `prefers-reduced-motion: reduce`, **When** abre diálogo, **Then** `scale` deshabilitado.

---

### Edge Cases

- ¿Qué pasa si el jugador confirma retiro dos veces rápido (doble clic `Confirmar`)? Solo primer `POST /withdraw` prevalece; segundo con misma `X-Idempotency-Key` retorna mismo `GameSession` sin duplicar `WITHDRAWAL` ledger (idempotente).
- ¿Qué ocurre si `Secured Points` es 0 (`LOSE_ALL` sin checkpoint)? Diálogo muestra `Current 100 pts`, `Secured 0 pts`, `Potential 100 pts` y warning "Withdraw now and secure 0 points?" sin romper layout.
- ¿Cómo maneja retiro si `Game` ya es `FINISHED`/`CANCELLED`? `POST /withdraw` rechazado 400 `InvalidGameState` con `ProblemDetails` + `CorrelationId`, diálogo muestra `ErrorState` sin `PlayerWithdrawn`.
- ¿Qué pasa si `Potential Points` no está configurado y es "—"? Diálogo muestra `Potential —` sin NaN.
- ¿Qué ocurre si token expira mientras confirma retiro? Interceptor 401 → `silentRenew`; si falla, redirect OIDC sin `PlayerWithdrawn` parcial.
- ¿Cómo se comporta si cliente modifica `Secured Points` en DevTools a 999? Siguiente `GET /players/me` sobrescribe con `Secured` autoritativo 200 (V) antes de mostrar confirmación.
- ¿Qué pasa si jugador `ELIMINATED` intenta retirarse? Rechazado 403 `PlayerAlreadyEliminated` con `CorrelationId`, diálogo no permite confirmar (`Withdrawal Action` deshabilitado si `isTerminal`).
- ¿Qué ocurre si `GameSession` `RowVersion` de A y B es igual y ambos se retiran simultáneamente? Cada `Withdraw` es per `GamePlayerId` `RowVersion` per `GamePlayer`, no global `Game`, sin conflicto.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: La interfaz de retiro DEBE mostrar `Current Points` (`Score.totalPoints` autoritativo), `Secured Points` (`SecuredPoints.securedPoints` + `checkpointRoundNumber`), y `Potential Points` (`PotentialReward` o `PointsPerRound` o "—") del servidor `GET /api/games/{id}/players/me` sin cálculo cliente (D/V).
- **FR-002**: Antes de confirmar, la interfaz DEBE mostrar warning 1: `"If you continue and answer incorrectly, you may lose your accumulated points."` con `role="alert"` `aria-live assertive` y warning 2 dinámico: `"Withdraw now and secure X points?"` donde X = `SecuredPoints.securedPoints` (D).
- **FR-003**: El retiro DEBE requerir confirmación explícita en 2 pasos: paso 1 `Withdrawal Action` botón `min-height:44px` abre diálogo `role="dialog"` `aria-modal`; paso 2 `Confirmar` (≥44px) envía `POST /api/games/{id}/withdraw` con `X-Idempotency-Key` `sessionStorage` per `gameId` + `Authorization Bearer`; `Cancelar`/`Escape`/click fuera cierra sin llamada (F).
- **FR-004**: Una vez confirmado `200`, el sistema DEBE poner `GameSession.status=WITHDRAWN` `PlayerStatus WITHDRAWN` `isTerminal true` `canAnswer false` `RowVersion++` per `GamePlayerId` (F) y `Score` `CurrentPoints` = `SecuredPoints` si `KEEP_SECURED_SCORE` (o según `WithdrawalPolicy`) (C).
- **FR-005**: `PlayerWithdrawn` DEBE bloquear cualquier `POST /answers` o `POST /withdraw` posterior con `403 PlayerAlreadyWithdrawn` o `409` idempotente sin nuevo `PointTransaction` ledger (F/D).
- **FR-006**: El diálogo DEBE ser accesible `role="dialog"` `aria-modal="true"` `aria-label="Confirmar retiro"` con foco `outline:2px solid var(--color-primary)` y navegable `Tab`/`Shift+Tab`/`Escape`/`Enter`, targets ≥44px.
- **FR-007**: El diálogo DEBE ser responsive 375–1536 sin scroll horizontal, `max-width 400px` centrado, gap `var(--space-3)` `data-theme="player"` sin literales, `prefers-reduced-motion` reduce sin `scale`.
- **FR-008**: El sistema DEBE propagar `X-Correlation-Id` por `POST /withdraw` + `GET /players/me` y mostrar `CorrelationId/TraceId` en `ErrorState` si falla (I).
- **FR-009**: Seguridad delegada (VI/H): `POST /withdraw` DEBE requerir JWT válido `jwks_uri`, `sub=PlayerId`, `must_change_password` gating; sin JWT → 401 OIDC; `PlayerId` de `sub` no del body; payload nunca incluye privados de otros.

### Key Entities *(include if feature involves data)*

- **WithdrawalAction**: Acción de dominio `Game.WithdrawPlayer(playerId)` que valida `!IsTerminal` + `!IsWithdrawn` + `!IsEliminated` + `IsActive`, calcula `deduction` per `WithdrawalPolicy` (`LOSE_ALL`/`KEEP_SECURED_SCORE` etc.), muta `GamePlayer.Status → WITHDRAWN` y `Score.CurrentPoints` y genera `PointTransaction` `WITHDRAWAL` + `Idempotency` (F/C).
- **Score / SecuredPoints / PotentialPoints**: Proyecciones autoritativas per `sub` `Score.totalPoints` `SecuredPoints.securedPoints` `PotentialReward` de `GET /players/me` (D).
- **GameSession**: `GamePlayerId` `PlayerId=sub` `GameId` `Status` `CurrentRoundNumber` `RowVersion` per `GamePlayerId` (F) — `Withdraw` usa `RowVersion` per `GamePlayer`.
- **PointTransaction (WITHDRAWAL)**: Entrada ledger `WITHDRAWAL` con `Points` `-deduction` `ResultingBalance` `CreatedAt` per `playerId+gameId` (D).
- **GameStatus / PlayerGameStatus**: `GameStatus` `IN_PROGRESS/ROUND_IN_PROGRESS/FINISHED` y `PlayerStatus` `ACTIVE/WITHDRAWN/ELIMINATED/WINNER` + `IsTerminal` `canAnswer` (A) — bloquea `Withdrawal Action` si `isTerminal`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% de aperturas de diálogo de retiro muestran `Current` `Secured` `Potential` coincidentes con `GET /players/me` ledger (0% cálculo cliente).
- **SC-002**: 100% de diálogos muestran warnings exactos: "If you continue..." y "Withdraw now and secure X points?" con X = `SecuredPoints` dinámico.
- **SC-003**: 100% de retiros requieren confirmación explícita 2 pasos; 0% de `Cancelar`/`Escape` envían `POST /withdraw`.
- **SC-004**: 100% de confirmaciones `POST /withdraw` con misma `X-Idempotency-Key` son idempotentes sin duplicar `WITHDRAWAL` ledger (segundo retorna mismo `GameSession`).
- **SC-005**: 100% de jugadores `WITHDRAWN` tienen `isTerminal true` `canAnswer false` y no pueden `POST /answers` (403) tras retiro.
- **SC-006**: 100% de jugadores `WITHDRAWN` conservan `Secured Points` según `WithdrawalPolicy` (`KEEP_SECURED_SCORE` → `Current=Secured`).
- **SC-007**: Responsive 375–1536 sin scroll horizontal para diálogo de retiro 100% y WCAG 2.2 AA `axe` 0 violations (`role="dialog"` `aria-modal` `aria-live`).
- **SC-008**: 100% de `POST /withdraw` incluyen `X-Correlation-Id` + `Authorization Bearer` y errores muestran `CorrelationId/TraceId`; 100% requieren JWT válido (sin JWT → 401).

## Assumptions

- Se extiende `QuizArena.Player` Angular 22 SPA existente (SPEC-027/029/032/033/034) con `WithdrawalComponent` `app-withdrawal` + `PlayerGameStore` `withdraw()` ya esbozado en 029 `withdraw()` `rxMethod` `POST /withdraw` `X-Idempotency-Key` `sessionStorage` per `gameId` + `GameComponent` `Withdrawal Action` botón `min-height:44px` + diálogo confirmación modal `role="dialog"` ya en 029 `GameComponent` con `showWithdrawConfirm` boolean; se reutiliza sin duplicar.
- `oroclash-api` ya expone `WithdrawPlayer` `POST /api/games/{id}/withdraw` idempotente `WITHDRAWAL` ledger `PlayerAlreadyWithdrawn` `RowVersion` per `GamePlayerId` (SPEC-008 C); no se crean nuevos agregados; `Current/Secured/Potential` ya en `GetMyPlayerState` (032).
- `WithdrawalPolicy` es `KEEP_SECURED_SCORE` por defecto si no configurado (alternativas `LOSE_ALL`/`KEEP_CURRENT_SCORE`/`KEEP_CHECKPOINT_SCORE` no hardcodeadas, solo proyección `Secured`).
- `Potential Points` en diálogo es `PotentialReward` de 029/032 (`Potential Reward` próximo premio o `PointsPerRound` o "—").
- `PlayerWithdrawn` es terminal: `isTerminal true` `canAnswer false` bloquea `QuestionComponent` `aria-disabled` y `Withdrawal Action` deshabilitado si `isTerminal`.
- Design System 016 ya en `angular.json` `design-system/tokens/design-tokens.css` `data-theme="player"`; se reutiliza sin literales para diálogo 3 métricas + 2 warnings.
- Tokens nunca en `localStorage`; `authInterceptor` Bearer solo `apiUrl`; `must_change_password` gating ya aplica (VI/H).
- Layout existente `GameComponent` `Withdrawal Action` botón en footer competitivo junto a `ScorePanel` 5 métricas + `Leaderboard`; diálogo modal centrado `position:fixed` `inset:0` `background:rgba(0,0,0,0.5)`.

## Dependencies

- SPEC-007 `Scoring System` (`Score` `SecuredPoints` `PointTransaction` `WITHDRAWAL` D, `WithdrawalPolicy` C).
- SPEC-008 `Player Withdrawal` base (`WithdrawPlayer` `PlayerAlreadyWithdrawn` `RowVersion` per `GamePlayerId` C).
- SPEC-012 `Realtime Game Events` (`GameHub` `ScoreUpdated`/`GameFinished` → `hydrate` G).
- SPEC-016 `UI/UX Design System` (`design-system/tokens/design-tokens.css` `data-theme="player"` WCAG).
- SPEC-027 `Player Application` (`QuizArena.Player` Angular 22 SPA, `PlayerGameStore`, `GamesApi`, `GameRealtimeService`).
- SPEC-029 `Player Game` (`GameComponent` `Withdrawal Action` botón + diálogo `showWithdrawConfirm` + `PlayerGameStore` `withdraw()`).
- SPEC-032 `Player Scoring` (`Current/Secured/Potential` 5 métricas `TotalPoints`).
- SPEC-033 `Player Multiplayer` (`Private State` per `sub` `UNIQUE` F).
- BuildingBlocks (`Kernel.Domain` `AggregateRoot/Result`, `CQRS ISender/IEndpoint`, `ServiceDefaults` OTel).
- OroIdentityServer `oroidentityserver:latest` `jwks_uri` PKCE `must_change_password`.

## Out of Scope

- Cálculo de `deduction` por `WithdrawalPolicy` detallado más allá de mostrar `Secured` `X` en warning (SPEC-007/008 ya autoritativo).
- Ledger detallado `WITHDRAWAL` histórico más allá de `Secured` final (SPEC-007 audit).
- `Consolation`/`Rewards` más allá de `Available Rewards` no en retiro (SPEC-009/010).
- Creación de juegos/preguntas/categorías (SPEC-001/003/005).
- Administración (Admin Blazor SPEC-017) y reporting (SPEC-015).
- Matchmaking, chat, amigos, invitaciones.
- Juego offline (sin conexión no hay retiro).
- Filtros de lobby (SPEC-028).
- Notificaciones push más allá de `GameHub` existente.

## References

- `draft/constitution.md` §I-VI, §A-J, §C `WithdrawalPolicy` `KEEP_SECURED_SCORE`, §D `WITHDRAWAL` ledger, §F `RowVersion` per `GamePlayerId` `Idempotency` `X-Idempotency-Key`, §G `GameHub`.
- `draft/game-concept.md` §Withdrawal §Scoring §Game/Round Lifecycle.
- `draft/oroidentityserver-specification.md` (OIDC PKCE `X-Correlation-Id`).
- `design-system/MASTER.md` + `overrides/player.md` + `tokens/design-tokens.css` (`data-theme="player"` WCAG).
- `src/Player/QuizArena.Player` (`app.routes.ts` `/game/:id`, `stores/player-game.store.ts` `withdraw()` `Score/SecuredPoints`, `features/game/withdrawal.component.ts` + `game.component.ts` `Withdrawal Action` `showWithdrawConfirm`, `features/shared/games.api.ts` `withdraw()` `getMyState`, `core/realtime/game-realtime.service.ts`).
- `src/OroQuizClash.Application/Features/Games/` (`WithdrawPlayer` `POST /withdraw` `X-Idempotency-Key` `PlayerAlreadyWithdrawn`, `GetMyPlayerState` `IEndpoint`).
- `OroQuizClash.AppHost/AppHost.cs` (`quizarena-player` → `oroclash-api`).
- `specs/008-player-withdrawal/` `specs/029-player-game/` (previos).
