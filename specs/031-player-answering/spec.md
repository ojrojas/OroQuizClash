# Feature Specification: Player Answering

**Feature Branch**: `031-player-answering`

**Created**: 2026-08-29

**Status**: Draft

**Input**: User description: "031 — Player Answering Tecnología Angular 22 Objetivo Definir la interacción del jugador con las cuatro respuestas. Descripción Cada pregunta deberá presentar exactamente cuatro opciones. Estados: Idle Hover Selected Locked Evaluating Correct Incorrect Timeout El jugador deberá poder seleccionar una única respuesta. Una vez bloqueada, la respuesta no podrá modificarse. El resultado deberá ser proporcionado por el backend."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Presentar exactamente cuatro opciones en estado Idle/Hover (Priority: P1)

Como jugador viendo una pregunta activa, quiero ver exactamente cuatro opciones de respuesta en estado Idle que reaccionen visualmente a Hover/Focus para entender que son interactivas antes de elegir.

**Why this priority**: Es la base visual solicitada ("cada pregunta deberá presentar exactamente cuatro opciones"). Sin 4 opciones no hay interacción posible. Entrega valor independiente como proyección de solo lectura de `Question.answerOptions[4]` y valida invariante B.

**Independent Test**: Con `Game` en `ROUND_IN_PROGRESS` con `Question` publicada (4 opciones A-D, 1 correcta server-side), abrir pregunta → verificar 4 botones/cards con texto correcto, orden por `displayOrder`, estado inicial `Idle`, hover con transformación premium y foco `outline:2px`. Inspeccionar que no se expone `isCorrect` en payload para `PLAYER` y que menos/más de 4 opciones muestra `ErrorState` sin permitir selección.

**Acceptance Scenarios**:

1. **Given** pregunta activa con 4 opciones válidas, **When** se renderiza, **Then** ve 4 opciones en estado `Idle` ordenadas, sin revelar `Correct/Incorrect`, con `role="radio"` `aria-checked="false"` dentro de `role="radiogroup"` `aria-label="Opciones de respuesta"`.
2. **Given** cursor sobre opción en `Idle`, **When** hace hover (o foco teclado Tab), **Then** ve estado `Hover` (borde/gradiente premium token, scale sutil) y `Hover` respeta `prefers-reduced-motion` sin animación excesiva.
3. **Given** `Question` corrupta con 3 o 5 opciones por error de datos, **When** intenta renderizar, **Then** muestra `ErrorState` "Pregunta inválida (se requieren 4 opciones)" con `CorrelationId` y bloquea selección.
4. **Given** lector de pantalla, **When** navega opciones, **Then** cada opción anuncia "Opción A, texto, sin seleccionar, 1 de 4" (`aria-posinset`/`aria-setsize`).

---

### User Story 2 — Seleccionar una única respuesta y bloquearla (Selected → Locked) (Priority: P1)

Como jugador, quiero seleccionar una única respuesta (clic/teclado) que pase a `Selected` y al confirmar quede `Locked` (no modificable), para evitar cambios accidentales y respetar la regla "una vez bloqueada, no podrá modificarse".

**Why this priority**: Núcleo de la interacción competitiva: single selection + immutabilidad. Sin bloqueo el jugador podría cambiar tras ver timeout o ventaja, rompiendo fairness y constitución F (idempotencia).

**Independent Test**: Seleccionar opción B → verifica `Selected` (check premium, `aria-checked="true"` solo B, otras `false`), pulsar `Confirmar` (o auto-lock según diseño) → estado `Locked` deshabilita otras opciones y bloquea cambio local; intentar seleccionar otra opción → ignorado; recargar página y rehidratar `GET /players/me` muestra misma `Selected` locked desde servidor (server truth).

**Acceptance Scenarios**:

1. **Given** 4 opciones `Idle`, **When** selecciona opción B (clic o `Space`/`Enter` con foco), **Then** B pasa a `Selected` (`aria-checked="true"`, `aria-selected`, visual check) y las otras permanecen `Idle`; solo una `Selected` a la vez (seleccionar C mueve `Selected` de B→C antes de lock).
2. **Given** opción en `Selected`, **When** confirma (botón Confirmar/Enviar o timeout de confirmación), **Then** pasa a `Locked` (`aria-disabled="true"` en otras, `Selected` botón `disabled`, `isLocked=true`), y cualquier intento posterior de seleccionar otra opción es ignorado localmente y rechazado por servidor `409 QuestionAlreadyAnswered` si se reenvía.
3. **Given** estado `Locked`, **When** recarga y rehidrata `GET /api/games/{id}/players/me` o `QuestionAvailable`, **Then** ve misma opción `Locked`/`Evaluating` según servidor, no puede desbloquear cliente-side.
4. **Given** sin selección y pulsa Confirmar, **When** intenta bloquear, **Then** permanece `Idle` y muestra mensaje validación "Selecciona una opción" sin llamar backend.

---

### User Story 3 — Evaluar y mostrar resultado autoritativo del backend (Evaluating → Correct/Incorrect/Timeout) (Priority: P1)

Como jugador con respuesta bloqueada, quiero que el sistema envíe la selección al backend y muestre `Evaluating` hasta recibir el resultado autoritativo (`Correct`/`Incorrect` o `Timeout`), porque el veredicto debe venir del servidor (constitución V).

**Why this priority**: Garantiza anti-cheating y regla "resultado proporcionado por backend". Sin evaluación server-side el cliente podría falsificar `Correct`.

**Independent Test**: Con opción `Locked` (B) y `hydrate` previo `roundStatus=IN_PROGRESS`, enviar `POST /answers` con `selectedOptionId` + `X-Idempotency-Key` → ver estado `Evaluating` (spinner `aria-live="polite"` "Evaluando…"), luego tras `hydrate` o polling `GET /players/me` recibir `Answer.state=EVALUATED isCorrect true/false` → mostrar `Correct` (verde + check animado) o `Incorrect` (rojo + cross + resaltar correcta server-side) o `Timeout` (si `submittedAt > expiresAt` o `Timer EXPIRED`). Reintento misma `X-Idempotency-Key` no duplica ledger.

**Acceptance Scenarios**:

1. **Given** `Locked` opción B dentro de `TimeLimit`, **When** envía `POST /api/games/{id}/answers` con `selectedOptionId` + `X-Idempotency-Key` UUID per `roundId` `sessionStorage`, **Then** ve `Evaluating` (`aria-busy`, spinner, otras opciones `aria-disabled`, botón Enviar deshabilitado) hasta respuesta 200 `EVALUATED`.
2. **Given** backend responde `EVALUATED isCorrect=true` (o `score` incrementado ledger), **When** recibe, **Then** opción bloqueada muestra `Correct` (token `success`, borde verde, icono check, `aria-live="assertive"` "¡Correcto! +X pts") y demás opciones en `Locked` muted salvo correcta si era otra.
3. **Given** backend responde `isCorrect=false`, **When** recibe, **Then** opción bloqueada muestra `Incorrect` (token `error`, borde rojo, cross, `aria-live="assertive"` "Incorrecto") y la opción correcta (de `Question` autoritativa post-EVALUATED) se resalta como `Correct` secondary para aprendizaje.
4. **Given** envío fuera de ventana (`Timer EXPIRED` o `submittedAt > expiresAt`), **When** backend rechaza `400 AnswerWindowExpired` o marca `EXPIRED`, **Then** ve `Timeout` (token `warning`, texto "Tiempo agotado", `aria-live="assertive"`, botón reintento deshabilitado, estado terminal impide nuevo envío).
5. **Given** reintento con misma `X-Idempotency-Key`, **When** reenvía, **Then** recibe mismo `Answer` sin duplicar `PointTransaction` ledger (idempotente, verificado por `COUNT`).

---

### User Story 4 — Accesibilidad, responsive y premium del selector de respuestas (Priority: P2)

Como jugador en móvil/desktop, quiero que el selector de 4 opciones sea accesible (teclado `Tab`/`Space`/`Enter`, `radiogroup`) y responsive premium (`data-theme="player"` tokens sin literales) para competir sin fricción.

**Why this priority**: Completa la experiencia con WCAG 2.2 AA y cinematic premium (SPEC-016). Sin ello el selector no es usable en móvil ni pasa auditoría.

**Independent Test**: Abrir selector en 375px (1 columna), 768px (2x2 grid), 1280px/1536px sin scroll horizontal, todos targets ≥44px, `data-theme="player"` sin literales (`var(--space-*)` `var(--color-*)`), contraste AA, foco `outline:2px`, `axe` 0 violations, `prefers-reduced-motion` sin hover scale.

**Acceptance Scenarios**:

1. **Given** viewport 375px, **When** ve 4 opciones, **Then** stack vertical 1 columna sin scroll horizontal, gap `var(--space-3)`; en ≥768px grid 2x2, en 375px `min-height 44px` por opción.
2. **Given** inspección CSS con `data-theme="player"`, **When** audita, **Then** 0 literales hardcodeados para color/spacing/typography/radius/shadow; usa `design-system/tokens/design-tokens.css`.
3. **Given** auditoría `axe`/`Lighthouse`, **When** corre, **Then** 0 violations: `role="radiogroup"` con `aria-label`, cada opción `role="radio"` `aria-checked`/`aria-posinset`/`aria-setsize`, `aria-disabled` en `Locked/Evaluating/Correct/Incorrect/Timeout`, foco visible.
4. **Given** `prefers-reduced-motion: reduce`, **When** hace hover/selected/evaluating, **Then** transición instantánea sin scale/pulse.

---

### Edge Cases

- ¿Qué pasa si el jugador hace doble clic rápido en dos opciones antes de lock? Solo primera `Selected` prevalece; segunda es ignorada por debounce 150ms y luego `Locked` bloquea.
- ¿Qué ocurre si la red falla durante `Evaluating` (`POST /answers` 500 con `X-Correlation-Id`)? Permanece `Evaluating` → tras timeout 3s muestra `ErrorState` con `CorrelationId` + `Retry`; `Retry` reusa misma `X-Idempotency-Key` sin duplicar.
- ¿Cómo se comporta si `Timer` expira mientras está en `Selected` antes de `Locked`? Bloqueo forzado a `Timeout` local y `POST /answers` será rechazado `AnswerWindowExpired`; UI muestra `Timeout` sin `Correct/Incorrect`.
- ¿Qué pasa si backend responde `Correct` pero la opción correcta no es la `Locked` (trampa)? Cliente resalta correcta autoritativa, `Locked Incorrect` en rojo + correcta en verde (no confía en cliente).
- ¿Qué ocurre si `Question` trae 4 opciones pero una con `text` vacío? Renderiza con placeholder "Opción sin texto" y `aria-label` fallback, no rompe grid.
- ¿Cómo maneja `Locked` tras recarga offline? `hydrate` con `GET /players/me` restaura `Locked/Evaluating/Correct` desde servidor; sin conexión muestra `ErrorState` con `Retry` sin permitir modificación.
- ¿Qué pasa si el jugador intenta inspeccionar `isCorrect` en DevTools antes de `EVALUATED`? Payload `AnswerOption` no incluye `isCorrect` para `PLAYER` (filtrado server-side SPEC-006/014).
- ¿Qué ocurre con `Timeout` y `Evaluating` simultáneos? `Timeout` tiene prioridad: si `expiresAt <= submittedAt` server decide `EXPIRED/Timeout` aunque cliente estuviera `Evaluating`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema DEBE presentar exactamente cuatro opciones por pregunta activa, ordenadas por `displayOrder`, cada una con `optionId` (UUID) y `text` (1..200 chars), derivadas de `Question.answerOptions[4]` con invariante "exactamente 4, exactamente 1 correcta server-side" (Constitución B, SPEC-003).
- **FR-002**: El sistema DEBE implementar 8 estados visuales por opción y transiciones entre ellos: `Idle` (default, sin selección) → `Hover` (mouse/focus) → `Selected` (una activa, `aria-checked="true"`) → `Locked` (confirmada, inmutable, `aria-disabled`) → `Evaluating` (spinner `aria-busy` tras `POST /answers`) → `Correct` / `Incorrect` / `Timeout` (terminales post-backend). Solo una `Selected` a la vez; `Locked` inmutable.
- **FR-003**: La selección DEBE ser única: seleccionar nueva opción en `Selected` mueve el estado `Selected` (no multi-select); confirmar/lock convierte `Selected` → `Locked`; tras `Locked` el sistema DEBE bloquear cualquier cambio de selección local y servidor DEBE rechazar `409 QuestionAlreadyAnswered` idempotente.
- **FR-004**: El bloqueo (`Locked`) DEBE ocurrir explícitamente al confirmar (botón Confirmar/Enviar o auto-lock configurable) y DEBE persistir tras recarga vía `hydrate` `GET /api/games/{id}/players/me` con `Answer.selectedOptionId` + `state LOCKED/EVALUATED`. `Locked` NO DEBE ser reversible cliente-side.
- **FR-005**: El envío DEBE usar `POST /api/games/{id}/answers` con `roundId`, `questionId`, `selectedOptionId`, `X-Idempotency-Key` UUID v4 per `playerId+roundId` en `sessionStorage` (`idemp-{roundId}`), `Authorization: Bearer` (OroIdentityServer JWT `sub=PlayerId`), `X-Correlation-Id`; reintento misma key DEBE ser idempotente (sin duplicar `PointTransaction` ledger, Constitución F) y retornar mismo `Answer`.
- **FR-006**: El resultado DEBE ser autoritativo backend: `Evaluating` permanece hasta `Answer.state==EVALUATED` o `EXPIRED/Timeout` desde servidor; `isCorrect` solo se expone tras `EVALUATED`; cliente NO DEBE calcular `Correct/Incorrect` ni confiar en payload de evento SignalR para veredicto (Constitución V).
- **FR-007**: El sistema DEBE mapear `Evaluating` → `Correct` si `isCorrect==true` (token `success` verde, check animado <300ms, `aria-live="assertive"`), o → `Incorrect` si `false` (token `error` rojo, cross, resaltando además la opción correcta autoritativa como `Correct` secondary), o → `Timeout` si `AnswerWindowExpired`/`EXPIRED` (token `warning`, texto "Tiempo agotado", `aria-live="assertive"`).
- **FR-008**: El sistema DEBE respetar ventana autoritativa `submittedAt <= expiresAt` (server timestamp): `Timeout` aplica si envío fuera de `TimeLimit` aunque cliente estuviera `Selected`/`Evaluating`; `Timer` cliente es visual con corrección `serverNow` pero decisión solo server.
- **FR-009**: El sistema DEBE propagar `X-Correlation-Id` en `POST /answers` y mostrar `CorrelationId/TraceId` en `ErrorState` (RFC 7807 `ProblemDetails`) con `Retry` que reusa misma `X-Idempotency-Key` (backoff opcional 1s/2s).
- **FR-010**: El selector DEBE ser accesible `role="radiogroup"` `aria-label="Opciones de respuesta"` con cada opción `role="radio"` `aria-checked` `aria-posinset 1..4` `aria-setsize 4` `aria-disabled` en `Locked/Evaluating/Correct/Incorrect/Timeout`, navegable `Tab`/`Shift+Tab` + activación `Space`/`Enter`, foco `outline:2px solid var(--color-primary)` visible.
- **FR-011**: El sistema DEBE cumplir Design System SPEC-016: usar `design-system/tokens/design-tokens.css` vía `data-theme="player"` sin literales hardcodeados para spacing/typography/color/radius/shadow; estados `Hover`/`Selected`/`Locked`/`Evaluating`/`Correct`/`Incorrect`/`Timeout` usan tokens (`--color-primary` `success` `error` `warning` etc.) y `prefers-reduced-motion` reduce animación.
- **FR-012**: El sistema DEBE ser responsive 375–1536 sin scroll horizontal: 1 columna 375px, grid 2x2 ≥768px, gap `var(--space-3)`, cada opción `min-height 44px` `min-width 44px`, scrolleable interna si pregunta larga, sin romper layout premium.
- **FR-013**: Seguridad delegada (Constitución VI/H): `POST /answers` DEBE requerir JWT válido `jwks_uri`, `sub=PlayerId`, `must_change_password` gating redirect a `/auth/change-password`; sin JWT → 401 redirect OIDC; `PlayerId` de `sub` no del body; payload nunca incluye `isCorrect` para `PLAYER` antes de `EVALUATED` (filtrado SPEC-006).
- **FR-014**: El sistema DEBE manejar estados `Evaluating`/`Error` sin duplicar envíos: durante `Evaluating` botón Enviar deshabilitado + debounce 150ms; error 500 → `ErrorState` con `CorrelationId` + `Retry`; error 409 `QuestionAlreadyAnswered` → satura a `Locked/Evaluating` según servidor sin nuevo `PointTransaction`.

### Key Entities *(include if feature involves data)*

- **Question**: `QuestionId`, `CategoryId`, `Text`, `AnswerOptions[4]` (`OptionId` UUID, `Text` 1..200, `DisplayOrder` 0..3, `IsCorrect` bool server-only), `Difficulty`, `Status=PUBLISHED`. Invariante B: exactamente 4, exactamente 1 correcta.
- **AnswerOption (view)**: `OptionId`, `Text`, `DisplayOrder`, `State` (`Idle/Hover/Selected/Locked/Evaluating/Correct/Incorrect/Timeout` client view), `IsSelected` bool, `IsLocked` bool, `AriaChecked` bool.
- **Answer (domain)**: `AnswerId` (`AnswerSubmissionId` strongly typed), `PlayerId` (`sub`), `GameId`, `RoundId`, `QuestionId`, `SelectedOptionId`, `SubmittedAt` (server), `State` (`PENDING/SUBMITTED/EVALUATED/EXPIRED`), `IsCorrect` bool|null (solo `EVALUATED`), `IdempotencyKey` UUID per `playerId+roundId`, `RowVersion`. `UNIQUE (GameId, RoundId, PlayerId)` + `UNIQUE IdempotencyKey`.
- **PlayerGameStatus**: `GameStatus`, `PlayerStatus`, `IsTerminal` (WITHDRAWN/ELIMINATED/FINISHED), `CanAnswer` (`!isTerminal && round IN_PROGRESS && answer PENDING`), usado para bloquear selector en terminal.
- **Timer**: `TimeLimitSeconds` 5..300, `ExpiresAt` ISO UTC, `RemainingSeconds` computed `max(0,floor((expiresAt - now)/1000))`, `State RUNNING/STOPPED/EXPIRED`, `ServerNow` para drift correction; decide `Timeout`.
- **AnswerInteractionState (view-model)**: `selectedOptionId: string|null`, `lockedOptionId: string|null`, `phase: 'idle'|'selected'|'locked'|'evaluating'|'correct'|'incorrect'|'timeout'`, `isEvaluating: boolean`, `canSelect: boolean (=canAnswer && !isLocked && !isEvaluating)`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% de preguntas activas muestran exactamente 4 opciones ordenadas con texto correcto y sin `isCorrect` leak antes de `EVALUATED` (0% exposición), verificado por contrato `GET /questions/current` filtrado para `PLAYER`.
- **SC-002**: El selector distingue los 8 estados (`Idle/Hover/Selected/Locked/Evaluating/Correct/Incorrect/Timeout`) en 100% de interacciones, con tokens correctos y `aria-checked/disabled` + `aria-live` por estado, sin confundir estados.
- **SC-003**: Selección única enforced en 100%: nunca más de una `Selected` simultánea; cambiar selección antes de `Locked` mueve `Selected` sin multi-select, verificado por inspección `aria-checked` único.
- **SC-004**: `Locked` es inmutable en 100%: tras `Locked` 0% de intentos locales modifican selección y servidor responde `409 QuestionAlreadyAnswered` idempotente sin nuevo `PointTransaction` ledger (verificado por `COUNT` no incrementa).
- **SC-005**: 100% de veredictos `Correct/Incorrect/Timeout` coinciden con backend `Answer.isCorrect` / `AnswerWindowExpired` autoritativo (cliente nunca calcula); `Evaluating` permanece hasta respuesta servidor en 100% de envíos.
- **SC-006**: Reintento con misma `X-Idempotency-Key` 100% idempotente sin duplicar `PointTransaction` ni `Answer` duplicado, verificado por doble `POST /answers` concurrente con misma key.
- **SC-007**: 95% de envíos dentro de `expiresAt` completan `EVALUATED` con `Correct/Incorrect` visible en <1s percibido; envíos fuera de ventana muestran `Timeout` en 100% sin `Correct`.
- **SC-008**: Responsive 375–1536 sin scroll horizontal en 100% de viewports, grid 1 col 375px / 2x2 ≥768px, targets ≥44px 100%, `data-theme="player"` 0 literales hardcodeados.
- **SC-009**: WCAG 2.2 AA pass 100% (`axe` 0 violations): `role="radiogroup"` `aria-checked` `aria-posinset/setsize` `aria-disabled` `aria-live` foco `outline:2px` contraste AA, teclado `Tab/Space/Enter` 100% funcional.
- **SC-010**: 100% de `POST /answers` incluyen `X-Correlation-Id` UUID y errores muestran `CorrelationId/TraceId` con `Retry`; JWT requerido 100% (sin JWT → 401 OIDC), sin exponer `isCorrect` pre-EVALUATED.

## Assumptions

- Se extiende `QuizArena.Player` Angular 22 SPA existente (SPEC-027/029/030) con `QuestionComponent`/`AnswerOptionComponent` y `PlayerGameStore`/`PlayerRoundsStore` ya con `GET /players/me` `Question` + `Answer` `Timer` `Status`. Se reutiliza `GamesApi.submitAnswer` con `X-Idempotency-Key` y `GameRealtimeService` ya `withAutomaticReconnect` → `hydrate`.
- La tecnología Angular 22 es requisito del usuario para el frontend Player; el backend permanece .NET 10 Clean Architecture/CQRS/BuildingBlocks (Constitución II-IV). El spec es agnóstico pero la implementación asumida es Angular 22 standalone `input()` `signal()` `computed()` `@if/@for`.
- `oroclash-api` ya expone slices `GetQuestion`/`GetCurrentQuestion` filtrado `isCorrect` per rol, `SubmitAnswer` `POST /answers` idempotente con `AnswerWindowExpired` + `QuestionAlreadyAnswered`, `GetMyPlayerState` con `question`/`answer`/`timer`/`status`; no se crean nuevos agregados, `AnswerInteractionState` es view-model.
- `Question` siempre tiene exactamente 4 opciones con 1 correcta (invariante B enforced por `Question.Create` + DB `CHECK exactly one correct` + publish requiere ≥5 preguntas por categoría).
- `Selected` → `Locked` ocurre al pulsar `Confirmar/Enviar` (botón explícito 44px) con debounce 150ms; no auto-lock al seleccionar para evitar errores táctiles. Confirmar sin selección muestra validación local sin llamada.
- `Evaluating` usa spinner `aria-busy` + deshabilita selector y botón Enviar; timeout de evaluación server 5s; tras 3s sin respuesta muestra `ErrorState` con `Retry` misma `X-Idempotency-Key`.
- `Timeout` es terminal por ronda: `isCorrect=null`, `Answer.state=EXPIRED`, `PointTransaction` no premia (según `LossPolicy`), bloquea re-envío `canAnswer=false`.
- Design System 016 ya genera `design-system/tokens/design-tokens.css` + `overrides/player.md` con `data-theme="player"` en `angular.json` styles y `app.component.ts`; selector usa CSS variables sin literales para cinematic (hover gradient, selected check, evaluating pulse, correct/incorrect/timeout tokens).
- Layout responsive: 1 columna 375px, 2x2 grid ≥768px (gap `var(--space-3)`), pregunta larga scrolleable, sin scroll horizontal, `prefers-reduced-motion` reduce animaciones.
- Tokens nunca en `localStorage`; `authInterceptor` adjunta Bearer solo a `apiUrl`; `MustChangePasswordGuard` ya aplica.
- No se implementa revelación de correcta antes de `EVALUATED`; la correcta secondary en `Incorrect` solo tras veredicto autoritativo.

## Dependencies

- SPEC-003 `Question Bank` (`Question` 4/1 invariante, `AnswerOption` con `DisplayOrder`, `PUBLISHED` requiere ≥5 preguntas por categoría, `IQuestionSelectionStrategy`).
- SPEC-005 `Round Engine` (`GameRound` con `RoundNumber` `Difficulty` `QuestionId` `TimeLimit` `Status` `StartRound` aleatoria, `PreviousQuestionIds`).
- SPEC-006 `Answer Evaluation` (`SubmitAnswer` idempotente `AnswerWindowExpired` `QuestionAlreadyAnswered` `IsCorrect` server-side, ledger `ANSWER_CORRECT/INCORRECT`).
- SPEC-007 `Scoring System` (`PointTransaction` ledger, `Score`/`SecuredPoints`, `Potential` proyección tras `Correct`).
- SPEC-012 `Realtime Game Events` (`GameHub` `QuestionAvailable`/`ScoreUpdated`/`RoundCompleted` + `hydrate` `withAutomaticReconnect`).
- SPEC-016 `UI/UX Design System` (`design-system/MASTER.md` + `tokens/design-tokens.css` `overrides/player.md` `data-theme="player"` WCAG 375-1536 cinematic).
- SPEC-027 `Player Application` (`QuizArena.Player` Angular 22 SPA, `PlayerGameStore` 10 elementos, `GamesApi`, `GameRealtimeService`, `app.routes.ts` `/game/:id`).
- SPEC-029 `Player Game` (`GameComponent` con `Current Round/Level/Question/Four Answers/Timer/Score/Secured/Potential/Status/Withdraw` y `question.component` base).
- SPEC-030 `Player Rounds` (`PlayerRoundsStore` `ladder` Round 1..N `Current Level` `Secured/Final` — selector vive en misma pantalla).
- BuildingBlocks (`Kernel.Domain` `AggregateRoot/IBusinessRule/Result`, `CQRS ISender/IEndpoint`, `ServiceDefaults` OTel/health, `Kernel.Infrastructure` `AppDbContextBase`).
- OroIdentityServer `oroidentityserver:latest` `/.well-known/openid-configuration` PKCE `authorization_code`+`refresh_token` `jwks_uri` `must_change_password`.

## Out of Scope

- Banco de preguntas / creación de `Question` / `IQuestionSelectionStrategy` (SPEC-003/005).
- Lógica de scoring/ledger detallada más allá de visualizar `Correct` (+pts) (SPEC-007).
- Withdrawal/rewards/consolation/leaderboards más allá de selector (SPEC-008/009/010/011).
- Ladder Round 1..N visual (SPEC-030) más allá de integrar selector en misma pantalla.
- Administración (Admin Blazor SPEC-017) y reporting (SPEC-015).
- Matchmaking, chat, multiplayer más allá de aislamiento per `GameSession`.
- Juego offline (sin conexión no hay envío autoritativo).
- Filtros de lobby (SPEC-028).

## References

- `draft/constitution.md` §I-VI, §A-J (Domain First, Authoritative Server Truth V, OroIdentityServer VI/H, Validation I).
- `draft/game-concept.md` §Question Invariants B (4 opciones 1 correcta), §Answer Evaluation, §Scoring D.
- `draft/oroidentityserver-specification.md` (OIDC PKCE discovery, `X-Correlation-Id`).
- `design-system/MASTER.md` + `design-system/overrides/player.md` + `design-system/tokens/design-tokens.css` (Cinematic premium, `data-theme="player"` WCAG 375-1536).
- `src/Player/QuizArena.Player` (`app.routes.ts` `/game/:id`, `stores/player-game.store.ts` `player-rounds.store.ts`, `features/game/question.component.ts`, `features/shared/games.api.ts` `getMyState/submitAnswer`, `core/realtime/game-realtime.service.ts` `withAutomaticReconnect`, `core/interceptors/`).
- `src/OroQuizClash.Application/Features/Games/` (`GetMyPlayerState`, `SubmitAnswer` `POST /answers` `X-Idempotency-Key` + `AnswerWindowExpired` `QuestionAlreadyAnswered`, `WithdrawPlayer`).
- `OroQuizClash.AppHost/AppHost.cs` (`quizarena-player` → `oroclash-api` `identity-api`).
- `specs/029-player-game/` + `specs/030-player-rounds/` (previos: pantalla principal + ladder).
