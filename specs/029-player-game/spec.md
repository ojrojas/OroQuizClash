# Feature Specification: Player Game

**Feature Branch**: `029-player-game`

**Created**: 2026-08-28

**Status**: Ready for Review

**Input**: User description: "029 — Player Game Objetivo Definir la pantalla principal de juego. Descripción Esta será la experiencia visual principal de QuizArena. Deberá mostrar: Current Round Current Level Question Four Answers Timer Current Score Secured Points Potential Reward Player Status Withdrawal Action La experiencia deberá ser: Cinematic Immersive Premium Competitive Responsive Accessible La pantalla deberá utilizar el Design System generado en SPEC-016."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Visualizar pantalla principal de juego (Priority: P1)

Como jugador activo en partida, quiero ver en una sola pantalla cinematic todos los elementos del juego (ronda, nivel, pregunta, respuestas, timer, puntajes, recompensa, estado) para tener contexto completo sin navegar.

**Why this priority**: Es la experiencia visual principal de QuizArena; sin ella no hay juego jugable. Entrega valor independiente como proyección de solo lectura del estado autoritativo.

**Independent Test**: Con `Game` en `ROUND_IN_PROGRESS` con `CurrentRound=3`, `Level=Intermediate`, `Question` con 4 opciones, `Timer RUNNING` `remaining 12s`, `Score 250 + Secured 100 + Potential 100`, `Status ACTIVE`, abrir `/player/game/:id` → verificar que los 10 elementos (Current Round, Current Level, Question, Four Answers, Timer, Current Score, Secured Points, Potential Reward, Player Status, Withdrawal Action) están visibles con datos correctos y coinciden con `GET /api/games/{id}/players/me` y `X-Correlation-Id` visible en error.

**Acceptance Scenarios**:

1. **Given** jugador autenticado en partida `ROUND_IN_PROGRESS`, **When** abre pantalla de juego, **Then** ve Current Round (ej. "Ronda 3/10"), Current Level, Question text, Four Answers (A-D), Timer cuenta regresiva, Current Score, Secured Points, Potential Reward, Player Status, Withdrawal Action en layout cinematic `data-theme="player"` sin scroll horizontal.
2. **Given** pantalla de juego cargada, **When** el servidor cambia `Round` (RoundCompleted → nueva ronda), **Then** al rehidratar ve Current Round/Level/Question/Timer actualizados sin datos obsoletos (server truth).
3. **Given** sin partida activa (`WAITING_FOR_PLAYERS`), **When** abre URL de juego, **Then** ve estado Empty/Waiting con mensaje amigable y CTA volver al lobby (no error 500 ni datos de otra partida).
4. **Given** partida en estado terminal `WITHDRAWN/ELIMINATED/FINISHED`, **When** abre pantalla, **Then** ve Player Status terminal con bloqueo de respuestas y muestra Secured/Score final (isTerminal bloquea `canAnswer`).

---

### User Story 2 — Responder y ver progresión de nivel/premio (Priority: P1)

Como jugador, quiero seleccionar una de las Four Answers y ver inmediatamente el impacto en Current Score / Secured Points / Potential Reward y avance de Current Level.

**Why this priority**: Convierte la visualización en interacción competitiva; núcleo del loop de juego junto a US1.

**Independent Test**: Seleccionar opción y enviar `SubmitAnswer` → verificar `Answer.isCorrect` solo tras `EVALUATED`, `Current Score` incrementado según `PointsPerRound` + `Level Bonus`, `Secured Points` según política `KEEP_SECURED_SCORE`, `Potential Reward` muestra próximo umbral, `Current Level` avanza si aplica (ex. `Intermediate → Advanced`). Reintento misma opción con misma `X-Idempotency-Key` retorna mismo resultado sin duplicar ledger.

**Acceptance Scenarios**:

1. **Given** `canAnswer=true` y `Timer RUNNING`, **When** selecciona una Answer y pulsa Enviar, **Then** se envía `POST /api/games/{id}/answers` con `selectedOptionId` + `X-Idempotency-Key`, recibe `EVALUATED` con `isCorrect` y ve Current Score actualizado en <1s percibido.
2. **Given** `Answer.state==EVALUATED` con `isCorrect=true`, **When** revisa pantalla, **Then** ve Potential Reward con próximo premio/Recompensa alcanzable (ej. "Próximo: Pack Oro 500 pts") derivado de ledger.
3. **Given** respuesta ya enviada `state!=PENDING`, **When** intenta re-enviar, **Then** sistema bloquea acción localmente (`canAnswer=false`) y servidor rechaza con `409 QuestionAlreadyAnswered` idempotente.

---

### User Story 3 — Gestión de tiempo y estado (Priority: P2)

Como jugador, quiero ver el Timer autoritativo y mi Player Status actualizado para saber si puedo responder o debo retirarme.

**Why this priority**: Timer y status determinan si la acción es válida; sin ellos el jugador actúa a ciegas.

**Independent Test**: `Timer` cuenta 30→0 sin saltos >1s (corrección contra `expiresAt` server), `Player Status` `ACTIVE` → `WITHDRAWN` tras Withdrawal bloquea respuestas; verificar `submittedAt <= expiresAt` decide `EVALUATED` vs `EXPIRED` server-side.

**Acceptance Scenarios**:

1. **Given** `Timer RUNNING` con `expiresAt` en 30s, **When** observa countdown, **Then** ve `remainingSeconds` decrementando 1/s sin saltos >1s, color cambia a warning <10s, `aria-live="polite"`.
2. **Given** `Timer EXPIRED` sin envío, **When** intenta enviar, **Then** recibe `400 AnswerWindowExpired` y ve estado `EXPIRED` con mensaje y CorrelationId.
3. **Given** `Player Status ACTIVE`, **When** cambia a `EXPIRED` por timeout, **Then** pantalla muestra `State: EXPIRED` y bloquea `Withdrawal Action` si ya terminal.

---

### User Story 4 — Retirarse de la partida (Priority: P2)

Como jugador, quiero ejecutar `Withdrawal Action` para abandonar la partida conservando puntaje según política, con confirmación y sin afectar a otros jugadores.

**Why this priority**: Completa la gestión de sesión y es la única mutación de estado además de Answer; debe ser explícita y terminal.

**Independent Test**: Pulsar `Withdrawal Action` → confirmar modal → `POST /api/games/{id}/withdraw` → `PlayerStatus WITHDRAWN`, `SecuredPoints` según `KEEP_SECURED_SCORE` (ej. 200), `canAnswer=false`, otros jugadores siguen `ACTIVE` sin interferencia; segundo retiro idempotente no duplica `WITHDRAWAL` ledger.

**Acceptance Scenarios**:

1. **Given** `canAnswer=true` `Status ACTIVE`, **When** pulsa Withdrawal Action y confirma, **Then** ve `Player Status WITHDRAWN` y Secured Points final en <1s, no puede responder más (403 `PlayerNotActive` si intenta).
2. **Given** ya `WITHDRAWN`, **When** pulsa Withdrawal de nuevo, **Then** recibe mensaje idempotente sin nuevo ledger.
3. **Given** jugador no autenticado o suplantando `playerId` distinto a `sub`, **When** intenta Withdraw, **Then** recibe `403 PlayerIdentityMismatch` auditado (Constitución H).

---

### Edge Cases

- ¿Qué pasa si una pregunta tiene menos de 4 opciones por error de datos? Sistema muestra `ErrorState` "Pregunta inválida" con `CorrelationId` y no permite enviar (Constraint B).
- ¿Qué ocurre si `Potential Reward` no está configurado? Muestra placeholder "—" sin romper layout premium (Reward opcional).
- ¿Cómo se comporta la pantalla si `Timer` llega a 0 mientras selecciona respuesta? Bloquea envío localmente y servidor decide `EXPIRED` si `submittedAt > expiresAt` (V).
- ¿Qué pasa si `Secured Points` supera `Current Score` por corrección? Muestra ambos valores tal cual ledger; no recalcula cliente (D).
- ¿Qué ocurre con 100 jugadores simultáneos en mismo juego? Cada instancia mantiene contexto aislado (SignalStore per GameSession) sin compartir `Answer/Score` (F).
- ¿Cómo maneja la pantalla `Current Level` si `Difficulty` es `CategorySpecific`? Muestra nombre de nivel resuelto por `IDifficultyProgressionStrategy` (C).
- ¿Qué pasa si token expira a mitad de partida? Interceptor 401 → `silentRenew` / `refresh_token`; si falla redirige a OIDC `connect/authorize`.
- ¿Qué ocurre en móvil 375px con 10 elementos + 4 respuestas + timer? Layout apilado por áreas (Header: Round/Level/Timer, Center: Question+Answers, Footer: Score/Secured/Potential/Status/Withdraw) sin scroll horizontal, targets ≥44px, `aria-live` para cambios (SPEC-016).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: La pantalla DEBE mostrar `Current Round` como "Ronda `currentRoundNumber`/`maxRounds`" (ej. "Ronda 3/10") derivado de `GameSession.currentRoundNumber` y `GameConfiguration.maxRounds`, obtenido de `GET /api/games/{id}/players/me` (`gameSession`, `game`).
- **FR-002**: La pantalla DEBE mostrar `Current Level` (ej. Basic/Intermediate/Advanced) derivado de `Score.currentLevel` o `Round.level` (Difficulty 1..5 mapeado a `DifficultyLevel` enumeration), consistente con `IDifficultyProgressionStrategy`.
- **FR-003**: La pantalla DEBE mostrar `Question` text y `Four Answers` como 4 opciones con `optionId/text` de `Question.answerOptions` (exactamente 4, invariante B); `isCorrect` nunca expuesto antes de `EVALUATED`.
- **FR-004**: La pantalla DEBE mostrar `Timer` como cuenta regresiva derivada de `Round.expiresAt` (server ISO UTC) con `remainingSeconds = max(0,floor((expiresAt - now)/1000))`, actualizado por `interval(1000)` + corrección contra `serverNow`/`QuestionAvailable` `expiresAt`, estados `RUNNING/STOPPED/EXPIRED` con `aria-live` y color warning <10s; decisión de expiración solo server (`submittedAt <= expiresAt`).
- **FR-005**: La pantalla DEBE mostrar `Current Score` (`Score.totalPoints`) y `Secured Points` (`SecuredPoints.securedPoints` + `checkpointRoundNumber` si aplica) derivados de `PointTransaction` ledger, nunca calculados cliente-side, con formato "500 pts · 200 asegurados".
- **FR-006**: La pantalla DEBE mostrar `Potential Reward` como próximo premio alcanzable (Reward.Name si `GameConfiguration.RewardRules` define `RewardId` y ledger `points >= threshold` próximo) o "—" si no configurado; solo proyección.
- **FR-007**: La pantalla DEBE mostrar `Player Status` (`ACTIVE`/`WITHDRAWN`/`ELIMINATED`/`WINNER`/`EXPIRED`) derivado de `GameSession.status` + `Game.status` `isTerminal`, con bloqueo de interacción cuando `isTerminal=true` o `canAnswer=false`.
- **FR-008**: La pantalla DEBE exponer `Withdrawal Action` (botón "Retirarse") solo si `!isTerminal && status.canAnswer` (o `ACTIVE`) y al activar DEBE pedir confirmación modal y luego invocar `POST /api/games/{id}/withdraw` con `X-Idempotency-Key` `sessionStorage` per `gameId` + `Authorization: Bearer`; idempotente por `UNIQUE` ledger.
- **FR-009**: Selección de `Four Answers` DEBE ser por `radiogroup` accesible (role `radio`, `aria-checked`, teclado `Tab`/`Space`/`Enter`) y envío DEBE usar `selectedOptionId` + `X-Idempotency-Key` UUID per `roundId` (sessionStorage) vía `POST /api/games/{id}/answers`; reintento misma key retorna mismo `Answer` sin duplicar ledger (F).
- **FR-010**: La pantalla DEBE consumir estado autoritativo solo vía `GET /api/games/{id}/players/me` rehydrate en cada evento `QuestionAvailable`/`ScoreUpdated`/`RoundCompleted`/`GameFinished`/`Reconnected` (Server Truth V, SignalR no fuente de verdad, `GameRealtimeService` `withAutomaticReconnect` → `hydrate`).
- **FR-011**: La pantalla NO DEBE confiar en payload de evento para `Score/SecuredPoints/isCorrect`; solo dispara `hydrate` y corrige `Timer.serverNow` (V).
- **FR-012**: La pantalla DEBE propagar `X-Correlation-Id` por request y mostrar `CorrelationId/TraceId` en `ErrorState` para errores 400/403/404/409/429 (RFC 7807).
- **FR-013**: La pantalla DEBE cumplir `Design System SPEC-016`: usar `design-system/tokens/design-tokens.css` vía CSS variables `data-theme="player"`, sin literales, tokens para spacing/typography/color; experiencia `Cinematic Immersive Premium Competitive` con layout por áreas (Header Round/Level/Timer cinematic, Center Question+Answers premium, Footer Score/Secured/Potential/Status/Withdraw competitive).
- **FR-014**: La pantalla DEBE ser `Responsive` 375–1536 sin scroll horizontal, targets ≥44px, layout apilado en móvil (Header→Question→Answers→Score→Withdraw) y `Accessible` WCAG 2.2 AA (contraste tokens, foco visible `outline:2px`, `aria-live="polite"` Timer/Score/Status `assertive` para `EXPIRED/ELIMINATED`, teclado, `axe` pass) (SPEC-016).
- **FR-015**: La pantalla DEBE respetar seguridad delegada: todas las acciones requieren JWT válido `jwks_uri`, `PlayerId=sub` (no body), `must_change_password` gating redirect a `/auth/change-password` (VI/H).
- **FR-016**: La pantalla DEBE mostrar estados `Loading` (skeleton cinematic), `Empty` (no round), `Error` (ProblemDetails + Retry + CorrelationId), `Expired` (time 0), `Terminal` (WITHDRAWN etc.) con `audit` append-only (I).

### Key Entities *(include if feature involves data)*

- **Game / GameSession**: `GameId`, `Name`, `Status` (9 estados), `Configuration` (`MaxRounds`, `TimeLimitPerQuestion`, `PointsPerRound`, `WithdrawalPolicy`, `LossPolicy`, `RewardRules`), `GameSessionId` (`GamePlayerId`), `UserId` (`sub`), `Status` (`ACTIVE/WITHDRAWN/ELIMINATED/WINNER`), `CurrentRoundNumber`, `RowVersion`.
- **Round**: `RoundId`, `GameId`, `RoundNumber` 1..Max, `Level` Difficulty 1..5, `Status` `WAITING/IN_PROGRESS/COMPLETED`, `QuestionId`, `StartedAt`, `ExpiresAt` (StartedAt+TimeLimit), `Version`.
- **Question / AnswerOption**: `QuestionId`, `CategoryId`, `Text`, `AnswerOptions[4]` (`OptionId`, `Text`), `Difficulty`; invariante 4 opciones 1 correcta server-side (B). `isCorrect` solo tras `EVALUATED`.
- **Answer**: `AnswerId`, `PlayerId`, `GameId`, `RoundId`, `QuestionId`, `SelectedOptionId`, `SubmittedAt` (server), `State` `PENDING/SUBMITTED/EVALUATED/EXPIRED`, `IsCorrect` bool|null (solo EVALUATED), `IdempotencyKey` UUID per `playerId+roundId`.
- **Score / SecuredPoints / PointTransaction**: `Score` `TotalPoints/CorrectAnswers/CurrentLevel` derivado ledger, `SecuredPoints` `securedPoints/checkpointRoundNumber/policy`, `PointTransaction` `Type` `ANSWER_CORRECT/INCORRECT/ROUND_BONUS/LEVEL_BONUS/GAME_BONUS/WITHDRAWAL` etc. (D).
- **Potential Reward**: Proyección `Reward` `RewardId/Name/PointsRequired` si `RewardRules` define premio próximo.
- **Timer**: `TimeLimitSeconds`, `ExpiresAt` ISO UTC, `RemainingSeconds` computed, `State RUNNING/STOPPED/EXPIRED`, `ServerNow` para drift.
- **PlayerGameStatus**: `GameStatus`, `PlayerStatus`, `IsTerminal` (WITHDRAWN/ELIMINATED/WINNER/FINISHED), `CanAnswer` (`!isTerminal && round IN_PROGRESS && answer PENDING`).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% de accesos a `/player/game/:id` muestran los 10 elementos (Current Round, Current Level, Question, Four Answers, Timer, Current Score, Secured Points, Potential Reward, Player Status, Withdrawal Action) sin faltantes y coincidentes con `GET /players/me` (verificado por store hydrate).
- **SC-002**: 100% de preguntas muestran exactamente 4 opciones y 1 correcta solo tras `EVALUATED` (0% `isCorrect` leak antes); `Four Answers` accesibles por teclado `Tab/Space/Enter` 100% funcional.
- **SC-003**: 95% de envíos `SubmitAnswer` dentro de `expiresAt` completan `EVALUATED` con `Current Score` actualizado en <1s percibido; reintento misma `X-Idempotency-Key` 100% idempotente sin duplicar ledger.
- **SC-004**: Timer drift <1s en 95% de mediciones: `remainingSeconds` decrementa 1/s sin saltos >1s con corrección cada `QuestionAvailable`/`hydrate` contra `serverNow` (medido por `interval` + computed).
- **SC-005**: 100% de `Secured Points` y `Potential Reward` coinciden con ledger `PointTransaction` (reconstruible `sum(points)` = `totalPoints`); `checkpointRoundNumber` correcto per política `KEEP_SECURED_SCORE`.
- **SC-006**: 100% de `Withdrawal Action` con confirmación completan en <1s y `PlayerStatus→WITHDRAWN` bloquea `canAnswer=false`; segundo retiro idempotente 100% sin nuevo ledger, otros jugadores siguen `ACTIVE`.
- **SC-007**: Experiencia percibida como Cinematic/Immersive/Premium/Competitive por 80% de usuarios en test cualitativo (contraste, animación timer, layout por áreas, tokens `data-theme="player"`).
- **SC-008**: Responsive 375–1536 sin scroll horizontal y targets ≥44px en 100% de vistas; WCAG 2.2 AA pass 100% (axe/Lighthouse contrast, foco, aria-live) con `design-system/tokens`.
- **SC-009**: 100% de requests incluyen `X-Correlation-Id` y errores muestran `CorrelationId/TraceId`; 100% requieren JWT válido, sin JWT → `401` redirect OIDC; accesibilidad teclado 100%.

## Assumptions

- Se extiende `QuizArena.Player` Angular 22 SPA existente (SPEC-027) con `PlayerGameStore` `signalStore` 10 elementos scoped por `gameId` (`providers: [PlayerGameStore]`), `GamesApi` (`getMyState`, `submitAnswer`, `withdraw`, `getGame`), `GameRealtimeService` SignalR `withAutomaticReconnect` y `hydrate` (Server Truth V, R4 027), interceptores `correlation-id`, `auth secureRoutes`, `error` RFC 7807.
- `oroclash-api` ya expone slices: `GetMyPlayerState` `GET /players/me` (10 elementos), `GetGame`/`GetCurrentRound`/`GetCurrentQuestion`, `SubmitAnswer` `POST /answers` `X-Idempotency-Key` + `AnswerWindowExpired` + `QuestionAlreadyAnswered`, `WithdrawPlayer` `POST /withdraw`, `GetPlayerScore/Secured` ledger (D). No se crean nuevos agregados; `Potential Reward` es proyección de `Reward` si `RewardRules` lo define.
- `Available Games` y lobby previo son SPEC-028 (`GET /games?status=WAITING_FOR_PLAYERS` paginado); esta pantalla es `/player/game/:gameId` tras `JoinGame` (GameSession ACTIVE).
- `Current Level` mapeado de `Difficulty 1..5` (Basic..Expert) vía `IDifficultyProgressionStrategy`; `Current Round` texto "Ronda 3/10" de `currentRoundNumber/maxRounds`.
- `Timer` usa `Round.expiresAt` server ISO UTC; cliente `remainingSeconds = max(0,floor((expiresAt - Date.now())/1000))` con `interval(1000)` + corrección `serverNow` en cada `hydrate`/`QuestionAvailable` (R5 027).
- `Potential Reward` placeholder "—" si no configurado; no bloquea pantalla premium.
- Design System 016 ya genera `design-system/tokens/design-tokens.css` + `overrides/player.md` con `data-theme="player"` en `angular.json` styles y `app.component.ts`; se reutiliza sin literales para cinematic (gradientes, spacing, typography, color premium).
- `Withdrawal Action` requiere confirmación modal (`Confirmar retiro? Perderás puntos no asegurados según KEEP_SECURED_SCORE`) antes de `POST /withdraw`.
- Tokens nunca en `localStorage` (XSS); `authInterceptor` adjunta Bearer solo a `apiUrl`; `must_change_password` gating via `MustChangePasswordGuard` redirect a `identity-api /auth/change-password`.
- Layout responsive: cinematic Header (Round/Level/Timer con efecto premium), Center Question+Four Answers immersive, Footer Score/Secured/Potential/Status/Withdraw competitive, sin scroll, targets ≥44px.

## Dependencies

- SPEC-001 `Game Configuration` (MinRounds≥5, MaxRounds, Difficulty, TimeLimit, Points, Withdrawal/Loss/RewardRules).
- SPEC-004 `Game Lifecycle` (State Machine `WAITING_FOR_PLAYERS→IN_PROGRESS→ROUND_IN_PROGRESS`, `GamePlayer` lifecycle `ACTIVE→WITHDRAWN/ELIMINATED`).
- SPEC-005 `Round Engine` (Current Round/Level, `StartRound` con `PreviousQuestionIds`).
- SPEC-006 `Answer Evaluation` (`SubmitAnswer` idempotente, `AnswerWindowExpired`, `QuestionAlreadyAnswered`, `IsCorrect` server-side).
- SPEC-007 `Scoring System` (`PointTransaction` ledger, `SecuredPoints` checkpoint, `Potential Reward` proyección).
- SPEC-008 `Player Withdrawal` (explicit `WithdrawPlayer` domain action, `RowVersion`, `WITHDRAWAL` ledger).
- SPEC-012 `Realtime Game Events` (`GameHub` `QuestionAvailable/ScoreUpdated/RoundCompleted/GameFinished` + rehydrate, `withAutomaticReconnect`).
- SPEC-016 `UI/UX Design System` (`design-system/MASTER.md`, `tokens/design-tokens.css`, `overrides/player.md`, WCAG 2.2 AA, 375-1536, `data-theme="player"`, cinematic/immersive/premium).
- SPEC-027 `Player Application` (`QuizArena.Player` Angular 22 SPA, `PlayerGameStore`, `GamesApi`, `GameRealtimeService`, `app.routes.ts` `/game/:id`).
- SPEC-028 `Player Lobby` (`Available Games` 8 campos paginado, `JoinGame` idempotente, previo a esta pantalla).
- BuildingBlocks (`Kernel.Domain` `AggregateRoot/IBusinessRule/Result`, `CQRS ISender/IEndpoint`, `ServiceDefaults` OTel/health, `Kernel.Infrastructure AppDbContextBase EfRepository`).
- OroIdentityServer `oroidentityserver:latest` discovery `/.well-known/openid-configuration`, PKCE `authorization_code`+`refresh_token`, `jwks_uri`, `must_change_password`.

## Out of Scope

- Creación de juegos (Admin `POST /api/games` SPEC-001).
- Selección de pregunta / banco de preguntas (SPEC-003/005, `IQuestionSelectionStrategy`).
- Lógica de scoring detallada (SPEC-007 LevelBonus, GameBonus ya en ledger, no en cliente).
- Rewards redemption (`POST /rewards/{id}/redeem` SPEC-009) más allá de `Potential Reward` proyección.
- Consolation eligibility (SPEC-010) más allá de proyección.
- Global leaderboards (SPEC-011) más allá de `Current Score` individual.
- Administración (Admin Blazor SPEC-017) y reporting (SPEC-015).
- Matchmaking, invitaciones, chat, amigos.
- Notificaciones push más allá de `GameHub` existente.
- Juego offline (sin conexión no hay pantalla autoritativa).
- Filtros de lobby avanzados (ya en SPEC-028).

## References

- `draft/constitution.md` §I-VI, §A-J (Domain First, Authoritative Server Truth V, OroIdentityServer VI/H, Validation I).
- `draft/game-concept.md` §Game/Round Lifecycle A, §Scoring D, §Withdrawal C.
- `draft/oroidentityserver-specification.md` (OIDC PKCE discovery, `X-Correlation-Id`).
- `design-system/MASTER.md` + `design-system/overrides/player.md` + `design-system/tokens/design-tokens.css` (Cinematic/Immersive/Premium, `data-theme="player"`, WCAG 375-1536).
- `src/Player/QuizArena.Player` (`app.routes.ts` `/game/:id`, `stores/player-game.store.ts` 10 elementos, `features/game/` `question.component` `timer.component` `score-panel.component` `game.component`, `features/shared/games.api.ts` `getMyState/submitAnswer/withdraw`, `core/realtime/game-realtime.service.ts` `withAutomaticReconnect`, `core/interceptors/` `app.config.ts` PKCE).
- `src/OroQuizClash.Application/Features/Games/` (`GetMyPlayerState`, `SubmitAnswer`, `WithdrawPlayer`, `GetGame`, `GetPlayerScore`, `IEndpoint`).
- `OroQuizClash.AppHost/AppHost.cs` (`quizarena-player` → `oroclash-api` `identity-api`).
- `specs/028-player-lobby/` (previo: Available Games, Join).
