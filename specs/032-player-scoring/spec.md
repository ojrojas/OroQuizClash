# Feature Specification: Player Scoring

**Feature Branch**: `032-player-scoring`

**Created**: 2026-08-29

**Status**: Ready for Review

**Input**: User description: "032 — Player Scoring Tecnología Angular 22 Objetivo Mostrar la evolución de puntos del jugador. Descripción La aplicación deberá mostrar: Current Points Secured Points Potential Points Round Points Total Points Los cambios de puntuación deberán actualizarse mediante los mecanismos realtime definidos en SPEC-012. El cliente nunca deberá ser la autoridad para calcular o modificar la puntuación."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Visualizar cinco puntuaciones autoritativas (Priority: P1)

Como jugador activo en partida, quiero ver en la pantalla de juego mis cinco métricas de puntuación (Current Points, Secured Points, Potential Points, Round Points, Total Points) derivadas del servidor, para entender mi progreso sin ambigüedad.

**Why this priority**: Es el núcleo de la feature ("mostrar la evolución de puntos"). Sin visualización autoritativa no hay feedback del juego. Entrega valor independiente como proyección de solo lectura del ledger `PointTransaction`.

**Independent Test**: Con `Game` en `ROUND_IN_PROGRESS` con `PointTransaction` ledger (ej. Current 350, Secured 200 checkpoint ronda 3, Potential 100, Round 50, Total 850), abrir `/player/game/:id` → verificar 5 valores coinciden con `GET /api/games/{id}/players/me` `Score`+`SecuredPoints` (Server Truth V), sin cálculo cliente.

**Acceptance Scenarios**:

1. **Given** jugador autenticado con score ledger, **When** abre pantalla de juego, **Then** ve `Current Points` (ej. 350), `Secured Points` (200 · checkpoint 3), `Potential Points` (100), `Round Points` (50), `Total Points` (850) con formato `"{n} pts"` y `aria-label` descriptivo.
2. **Given** partida sin apuestas (0 transacciones), **When** abre pantalla, **Then** ve 0 en las cinco métricas con placeholder "—" solo si `Potential Points` no configurado, sin error 500.
3. **Given** lector de pantalla, **When** navega puntuaciones, **Then** cada métrica anuncia "Current Points 350 puntos, Secured 200 checkpoint ronda 3" con `aria-live="polite"` para cambios.
4. **Given** inspección de payload `GET /players/me`, **When** revisa `Score` y `SecuredPoints`, **Then** no hay campo calculable cliente; `Total Points` es suma `PointTransaction` server-side (D).

---

### User Story 2 — Evolución en tiempo real vía SPEC-012 (Priority: P1)

Como jugador, quiero que mis puntuaciones se actualicen automáticamente cuando el servidor emite eventos `ScoreUpdated`/`RoundCompleted`/`AnswerEvaluated` sin recargar la página, porque el juego es competitivo en tiempo real.

**Why this priority**: Requisito explícito "actualizarse mediante mecanismos realtime definidos en SPEC-012". Sin realtime el jugador ve datos obsoletos y pierde confianza. Server Truth V prohíbe polling como autoridad.

**Independent Test**: Con `Game` activo, enviar `AnswerEvaluated` correcta desde otro cliente → servidor emite `ScoreUpdated` → verificar que `Current Points` y `Round Points` se incrementan en <1s sin `GET` manual, solo vía `hydrate` disparado por evento. Cliente nunca incrementa localmente antes del evento.

**Acceptance Scenarios**:

1. **Given** `Score` 350 y `RoundPoints` 50, **When** servidor emite `ScoreUpdated` con +100 (ANSWER_CORRECT), **Then** UI muestra 450 y 150 tras `hydrate` `GET /players/me`, con animación `pulse` `prefers-reduced-motion` reduce sin duplicar ledger.
2. **Given** `RoundCompleted` con bonus, **When** servidor emite `RoundCompleted` + `ScoreUpdated`, **Then** `Secured Points` se actualiza (ej. 200→250) y `Round Points` resetea según política, sin cálculo cliente.
3. **Given** pérdida de conexión SignalR `withAutomaticReconnect [0,2000,5000,10000,30000]`, **When** reconecta (`Reconnected`), **Then** dispara `hydrate` y sincroniza las cinco métricas sin requerir acción usuario.
4. **Given** evento `ScoreUpdated` recibido, **When** cliente intenta modificar `Current Points` localmente (ej. `+100` cliente), **Then** el cambio es descartado en siguiente `hydrate` (server truth).

---

### User Story 3 — Distinguir políticas y estados de puntuación (Priority: P2)

Como jugador, quiero distinguir qué puntos están asegurados vs en riesgo y cuál es mi potencial máximo, para decidir si arriesgar o retirarme.

**Why this priority**: Convierte puntuación en decisión estratégica (riesgo/recompensa). Depende de `LossPolicy`/`WithdrawalPolicy` de SPEC-007 y `SecuredPoints.checkpointRoundNumber` (SPEC-008).

**Independent Test**: Configurar `LossPolicy=LOSE_UNSECURED_POINTS` con `Secured 200`, `Round 80` → verificar UI muestra `Secured` con badge `asegurado` y `Round Points` con label `en riesgo`; `Potential Points` muestra `100` derivado de `GameConfiguration.PointsPerRound`.

**Acceptance Scenarios**:

1. **Given** `Secured 200 checkpoint 3` y `RoundPoints 80`, **When** ve puntuaciones, **Then** `Secured` muestra "200 · checkpoint 3" y `Round Points` "80 en juego" con `aria-label` diferenciador.
2. **Given** `GameConfiguration.RewardRules` con `Potential Points` 100, **When** no hay recompensa configurada, **Then** `Potential Points` muestra "—" sin romper layout.
3. **Given** política `KEEP_SECURED_SCORE` tras `Withdrawal`, **When** jugador se retira, **Then** `Total Points` refleja `Secured` final y `Current Points` cae a valor asegurado, sin cálculo cliente.

---

### User Story 4 — Responsive, accesible y premium (Priority: P2)

Como jugador en móvil/desktop, quiero que las cinco puntuaciones sean legibles, accesibles por teclado y con estilo `data-theme="player"` cinematic sin literales.

**Why this priority**: Completa la experiencia (SPEC-016) y asegura que puntuaciones no oculten `Question`/`Timer` en 375px.

**Independent Test**: Abrir en 375px → 5 métricas apiladas sin scroll horizontal, en ≥768px en footer competitivo con `Score/Secured/Potential`; verificar `data-theme="player"` 0 literales `var(--space-*)`, `axe` 0 violations, `Tab` navega métricas.

**Acceptance Scenarios**:

1. **Given** viewport 375px, **When** ve footer competitivo, **Then** 5 métricas en grid 1 col gap `var(--space-3)` targets ≥44px sin scroll.
2. **Given** `data-theme="player"`, **When** inspecciona CSS, **Then** 0 literales hardcodeados para color/spacing/typography/radius.
3. **Given** `prefers-reduced-motion: reduce`, **When** cambia `Score`, **Then** animación `pulse` deshabilitada.

---

### Edge Cases

- ¿Qué pasa si el ledger tiene 0 transacciones y `Total Points` aún no inicializado? Muestra 0 en las cinco métricas sin NaN ni error.
- ¿Qué ocurre si `ScoreUpdated` llega mientras `Evaluating` está activo? Puntuaciones se actualizan tras `hydrate` sin bloquear `QuestionComponent`; no duplica puntos.
- ¿Cómo maneja `Secured Points` si `checkpointRoundNumber` es null (sin checkpoint)? Muestra solo "200 pts" sin "checkpoint".
- ¿Qué pasa si `PointsPerRound` no está configurado (Potential Points null)? Muestra "—" con `aria-label` "Potential no disponible".
- ¿Qué ocurre si `Round Points` supera `Current Points` por corrección administrativa? Muestra valores tal cual ledger; no recalcula cliente (D).
- ¿Cómo se comporta con 100 jugadores simultáneos en mismo juego? Cada `Score` es aislado per `GameSession` (F) sin fuga entre jugadores.
- ¿Qué pasa si token expira mientras se recibe `ScoreUpdated`? Interceptor 401 → `silentRenew`; si falla, redirige OIDC sin perder puntuación (hydrate tras reconnect).
- ¿Qué ocurre si cliente modifica `Current Points` en DevTools? Siguiente `GET /players/me` sobrescribe con valor autoritativo (V).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema DEBE mostrar las cinco métricas derivadas del servidor: `Current Points` (`Score.CurrentPoints`), `Secured Points` (`SecuredPoints.securedPoints` + `checkpointRoundNumber`), `Potential Points` (`GameConfiguration.PointsPerRound` * dificultad o `RewardRules` próximo umbral), `Round Points` (`Score.RoundPoints`), `Total Points` (`sum(PointTransaction)` o `Score.TotalPoints`), obtenidas de `GET /api/games/{id}/players/me` (D).
- **FR-002**: El sistema DEBE obtener puntuaciones exclusivamente vía `GET /players/me` rehydrate; NUNCA calcular `Current/Secured/Potential/Round/Total` cliente-side ni aceptar valores del body (V).
- **FR-003**: El sistema DEBE actualizar las cinco métricas automáticamente al recibir eventos SPEC-012 `ScoreUpdated`/`RoundCompleted`/`RoundStarted`/`GameFinished`/`Reconnected` vía `GameRealtimeService` `withAutomaticReconnect [0,2000,5000,10000,30000]` disparando `hydrate` (no confiar en payload del evento) (G).
- **FR-004**: El sistema DEBE propagar `X-Correlation-Id` por `GET /players/me` y mostrar `CorrelationId/TraceId` en `ErrorState` si falla (I).
- **FR-005**: El sistema DEBE mostrar `Secured Points` con formato `"{secured} pts · checkpoint {n}"` si `checkpointRoundNumber != null`, o `"{secured} pts"` sin checkpoint, y badge `asegurado` cuando `isSecured=true` (per `isSecured` de ladder 030).
- **FR-006**: El sistema DEBE mostrar `Potential Points` como próximo premio alcanzable o `PointsPerRound` current; si no configurado debe mostrar "—" sin romper layout (compatible con 029 `potentialReward`).
- **FR-007**: El sistema DEBE distinguir visualmente `Round Points` ("en juego") vs `Secured Points` ("asegurado") con tokens `data-theme="player"` sin literales (SPEC-016).
- **FR-008**: El sistema DEBE ser responsive 375–1536 sin scroll horizontal, footer competitivo con 5 métricas (o 4 si `Potential` es "—"), gap `var(--space-3)` targets ≥44px, integrado con `GameComponent` grid `280px 1fr` (030) + center `Question` (031).
- **FR-009**: El sistema DEBE ser accesible `aria-live="polite"` para cambios de puntuación, `aria-label` por métrica, foco visible `outline:2px solid var(--color-primary)`, teclado `Tab` navega métricas.
- **FR-010**: El sistema DEBE respetar `prefers-reduced-motion: reduce` deshabilitando `pulse`/`scale` en animación de puntuación.
- **FR-011**: Seguridad delegada (VI/H): `GET /players/me` DEBE requerir JWT válido `jwks_uri`, `sub=PlayerId`, `must_change_password` gating; sin JWT → 401 redirect OIDC; `PlayerId` de `sub` no del body.
- **FR-012**: El sistema DEBE mostrar estados `Loading` (skeleton), `Error` (ProblemDetails `Retry` + `CorrelationId`), `Empty` (sin score) sin exponer `PointTransaction` detalle sensible.

### Key Entities *(include if feature involves data)*

- **Score**: Proyección de `PlayerScore` (`GameId`, `PlayerId`, `CurrentPoints`, `RoundPoints`, `CorrectAnswers`, `CurrentLevel`) derivado de `PointTransaction` ledger (D). No modificable cliente.
- **SecuredPoints**: `PlayerId`, `GameId`, `SecuredPoints` (int), `CheckpointRoundNumber` (int|null), `Policy` (`KEEP_SECURED_SCORE` etc.) (D). Protegido por `LossPolicy`.
- **PointTransaction**: Entrada inmutable ledger (`TransactionId`, `PlayerId`, `GameId`, `RoundId`, `QuestionId`, `Type` `ANSWER_CORRECT/INCORRECT/ROUND_BONUS/...`, `Points`, `ResultingBalance`, `CreatedAt`, `Reason`) (D). Appendix-only server calcula `Total Points`.
- **PotentialPoints**: Proyección derivada de `GameConfiguration.PointsPerRound` + `Difficulty` + `RewardRules` próximo umbral; solo visualización, no ledger.
- **TotalPoints**: Suma `PointTransaction` lifetime o `Score.TotalPoints` autoritativo; cliente no suma local.
- **Timer/PlayerGameStatus**: Reutilizados de 029 para `canAnswer/isTerminal` que bloquean visualización en terminal (F).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% de accesos a `/player/game/:id` muestran las cinco métricas (Current/Secured/Potential/Round/Total) coincidentes con `GET /players/me` ledger (0% cálculo cliente).
- **SC-002**: 100% de cambios de puntuación se reflejan en UI en <1s tras `ScoreUpdated`/`RoundCompleted` vía `hydrate` (realtime SPEC-012) sin recarga manual.
- **SC-003**: 0% de mutaciones de puntuación originadas en cliente son aceptadas; auditoría muestra `PointTransaction` solo server-side (V/D).
- **SC-004**: `Secured Points` distingue correctamente checkpoint en 100% de casos (`checkpoint null` → sin badge, `checkpoint 3` → "checkpoint 3").
- **SC-005**: `Potential Points` muestra "—" cuando no configurado en 100% sin romper layout 375-1536.
- **SC-006**: Responsive 375–1536 sin scroll horizontal y targets ≥44px en 100% de vistas con `data-theme="player"` 0 literales.
- **SC-007**: WCAG 2.2 AA pass 100% (`axe` 0 violations) para bloque de puntuaciones (`aria-live`, foco, contraste).
- **SC-008**: `prefers-reduced-motion: reduce` deshabilita animaciones de puntuación en 100%.

## Assumptions

- Se extiende `QuizArena.Player` Angular 22 SPA existente (SPEC-027/029/030/031) con `PlayerGameStore` 10 elementos (`Score`/`SecuredPoints`/`Timer`/`Status`) + `GameRealtimeService` `withAutomaticReconnect` ya `hydrate`; `GamesApi.getMyState` ya retorna `Score` y `SecuredPoints` (029).
- `oroclash-api` ya expone `GetMyPlayerState` con `Score`/`SecuredPoints`/`PointTransaction` ledger (SPEC-007 D); no se crean nuevos agregados; `Total Points` es `Score.TotalPoints` o suma ledger server-side.
- `Potential Points` deriva de `GameConfiguration.PointsPerRound` (SPEC-001) y `RewardRules` (SPEC-007); si no configurado, es proyección opcional.
- `Round Points` es `Score.RoundPoints` (o `Score.CurrentPoints - SecuredPoints` según implementación 029) reseteado en `RoundCompleted` per `LossPolicy`; visualización es solo lectura.
- Puntuaciones son per `GameSession` aisladas (F) – un jugador no ve score de otro.
- Design System 016 ya en `angular.json` `design-system/tokens/design-tokens.css` `data-theme="player"`; se reutiliza sin literales.
- Tokens nunca en `localStorage`; `authInterceptor` adjunta Bearer solo a `apiUrl`; `must_change_password` gating ya aplica (VI/H).
- Layout existente `GameComponent` grid `280px 1fr` con ladder sidebar 030 y center question 031; bloque de puntuaciones vive en footer competitivo junto a `ScorePanelComponent` (029).

## Dependencies

- SPEC-007 `Scoring System` (`Current/Secured/Round/Potential/Total`, `PointTransaction`, `LossPolicy`/`WithdrawalPolicy`/`SecurePoints`).
- SPEC-012 `Realtime Game Events` (`GameHub` `ScoreUpdated`/`RoundCompleted`/`Reconnected` + `hydrate`).
- SPEC-016 `UI/UX Design System` (`design-system/tokens/design-tokens.css` `data-theme="player"` WCAG 375-1536).
- SPEC-027 `Player Application` (`QuizArena.Player` Angular 22 SPA, `PlayerGameStore`, `GamesApi`, `GameRealtimeService`).
- SPEC-029 `Player Game` (`GameComponent` 10 elementos, `ScorePanelComponent`, `Timer`, `Withdrawal`).
- SPEC-030 `Player Rounds` (`PlayerRoundsStore` ladder `Secured`).
- SPEC-031 `Player Answering` (`AnswerInteractionStore` `SubmitAnswer` `isCorrect` server truth, reutiliza `getMyState`).
- BuildingBlocks (`Kernel.Domain` `AggregateRoot/Result`, `CQRS ISender/IEndpoint`, `ServiceDefaults` OTel).
- OroIdentityServer `oroidentityserver:latest` `jwks_uri` PKCE `must_change_password`.

## Out of Scope

- Cálculo o modificación de puntuación (SPEC-007 domain `AwardPoints/RemovePoints/SecurePoints/ConsumePoints` ya autoritativo).
- Ledger detallado `PointTransaction` por tipo histórico más allá de `Total Points`/`Round Points` visual (SPEC-007 audit).
- Withdrawal/rewards/consolation/leaderboards más allá de `Total Points` visual (SPEC-008/009/010/011).
- Creación de juegos/preguntas (SPEC-001/003/005).
- Administración (Admin Blazor SPEC-017) y reporting (SPEC-015).
- Matchmaking, chat, amigos, invitaciones.
- Juego offline (sin conexión no hay puntuación autoritativa).
- Filtros de lobby (SPEC-028).

## References

- `draft/constitution.md` §I-VI, §A-J (Domain First, Server Truth V, OroIdentityServer VI/H, Validation I).
- `draft/game-concept.md` §Scoring D, §Withdrawal C, §Game/Round Lifecycle A.
- `draft/oroidentityserver-specification.md` (OIDC PKCE `X-Correlation-Id`).
- `design-system/MASTER.md` + `overrides/player.md` + `tokens/design-tokens.css` (`data-theme="player"` WCAG).
- `src/Player/QuizArena.Player` (`stores/player-game.store.ts` `Score/SecuredPoints`, `features/game/score-panel.component.ts` `game.component.ts` `timer.component.ts`, `features/shared/games.api.ts` `getMyState`, `core/realtime/game-realtime.service.ts`).
- `src/OroQuizClash.Application/Features/Games/` (`GetMyPlayerState`, `GetPlayerScore`, `IEndpoint`).
- `OroQuizClash.AppHost/AppHost.cs` (`quizarena-player` → `oroclash-api`).
- `specs/007-scoring-system/` `specs/029-player-game/` `specs/030-player-rounds/` `specs/031-player-answering/` (previos).
