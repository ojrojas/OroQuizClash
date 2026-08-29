# Feature Specification: Player Rounds

**Feature Branch**: `030-player-rounds`

**Created**: 2026-08-28

**Status**: Ready for Review

**Input**: User description: "030 — Player Rounds Objetivo Representar visualmente la progresión del jugador durante las rondas. Descripción La aplicación deberá mostrar: Round 1 Round 2 Round 3 ... Round N y la progresión de dificultad. Deberá existir una representación visual de: Current Level Previous Levels Current Reward Next Reward Secured Reward Final Reward La transición de ronda deberá ser visualmente clara y sincronizada con el estado recibido del servidor."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Visualizar escalera de rondas Round 1..N con progresión de dificultad (Priority: P1)

Como jugador en partida, quiero ver una escalera/ladder vertical (o equivalente responsive) que liste Round 1 .. Round N con su nivel/dificultad asociada, donde se distingan visualmente Previous Levels (superados), Current Level (actual) y niveles futuros, para entender dónde estoy y qué dificultad viene.

**Why this priority**: Es la representación central pedida (Round 1..N + progresión de dificultad). Sin ella no hay conciencia de avance ni dificultad creciente. Entrega valor independiente como lectura del estado autoritativo de `GameSession.currentRoundNumber` + `Round.level`.

**Independent Test**: Con juego `MaxRounds=10`, `currentRoundNumber=4`, `Strategy=Linear` (Level 1..5 mapeado a rounds), abrir ladder → verificar 10 filas "Round 1" .. "Round 10" cada una con `RoundNumber`, `Level` (ej. Basic/Intermediate) y `Difficulty` indicator, Current Level (ronda 4) destacado con token premium, Previous 1-3 en estado muted/completed, futuros 5-10 en estado upcoming, coincidente con `GET /api/games/{id}/players/me` (`currentRoundNumber`, `rounds[]`, `game.maxRounds`) y `IDifficultyProgressionStrategy`.

**Acceptance Scenarios**:

1. **Given** juego con `MaxRounds=10` y `currentRoundNumber=4` (ROUND_IN_PROGRESS), **When** se renderiza la escalera, **Then** ve lista ordenada Round 1..10 con Current Level = Round 4 destacado (aria-current="step", color/borde premium), Previous Levels 1-3 con estilo completed/check, y 5-10 muted upcoming sin huecos ni duplicados.
2. **Given** `Strategy=Linear` con 5 niveles (Basic..Expert) mapeados a 10 rondas (ej. 1-2 Basic, 3-4 Elementary...), **When** observa cada fila, **Then** ve `Difficulty` (nivel textual + indicator visual) consistente con `Round.level` y progresión creciente (nivel nunca decrece hacia abajo salvo `Adaptive` configurado, pero siempre refleja `Round.level` autoritativo).
3. **Given** `MaxRounds` dinámico (5..15 según GameConfiguration), **When** cambia juego, **Then** ladder muestra exactamente N filas sin hardcodear 10 (derivado de `GameConfiguration.maxRounds`).
4. **Given** lector de pantalla, **When** navega la ladder, **Then** cada fila tiene `role="listitem"` y Current Level anuncia "Nivel actual, ronda 4 de 10, dificultad Intermediate" (`aria-current`, `aria-label`).

---

### User Story 2 — Visualizar recompensas: Current, Next, Secured y Final (Priority: P1)

Como jugador, quiero ver sobre la misma ladder/recompensas: Current Reward (premio de la ronda actual), Next Reward (próximo), Secured Reward (último checkpoint asegurado según política), y Final Reward (premio máximo), para decidir si arriesgar o retirarme.

**Why this priority**: Las 4 recompensas son requisito explícito y determinan la estrategia de withdraw. Sin ellas el jugador no percibe riesgo/recompensa.

**Independent Test**: Con `RewardRules` por umbral (ej. Round 5 = 500 pts asegurado, Round 10 = 5000 pts final) y ledger `SecuredPoints=500 (checkpointRound 5)`, `Current=700` (ronda 6), verificar ladder muestra: fila 6 con badge "Current Reward: 600 pts", fila 7 "Next: 800 pts", fila 5 con icono secured/lock "Asegurado", fila 10 con corona "Final Reward: 5000 pts". Valores coinciden con `RewardThreshold[]` + `PointTransaction` ledger (reconstruible).

**Acceptance Scenarios**:

1. **Given** jugador con `SecuredPoints=500` checkpoint `Round 5` (política `KEEP_SECURED_SCORE`), **When** ve ladder, **Then** Round 5 muestra indicador Secured Reward (icono escudo/check, color token success) y rondas 1-5 con overlay "asegurado", distinto de rondas no aseguradas.
2. **Given** `currentRoundNumber=6`, **When** ve ladder, **Then** Round 6 badge `Current Reward` con valor de `RewardRules[roundNumber]` o `PointsPerRound` acumulado, y Round 7 muestra `Next Reward` (siguiente umbral) con estilo muted upcoming y flecha/indicador "próximo".
3. **Given** `MaxRounds=10` con `Final Reward` configurado en `RewardRules[maxRounds]`, **When** ve ladder, **Then** Round 10 siempre visible con tratamiento premium (corona/gradiente, token `final`) y valor `Final Reward`, aunque no haya llegado.
4. **Given** `RewardRules` no configurado (sin premios por ronda), **When** ve ladder, **Then** muestra placeholder "—" en Current/Next/Final sin romper layout (misma fila, acceso `aria-label="Sin recompensa configurada"`).

---

### User Story 3 — Transición de ronda sincronizada con servidor y visualmente clara (Priority: P1)

Como jugador, quiero que el avance de ronda (RoundCompleted → nueva RoundStarted) se anime de forma clara (highlight, movimiento, feedback) y que ese cambio esté sincronizado con el estado autoritativo recibido del servidor (SignalR `RoundCompleted`/`QuestionAvailable` + rehydrate), para no ver salto desincronizado ni estado fake cliente-side.

**Why this priority**: Requisito explícito "transición visualmente clara y sincronizada con el estado recibido del servidor". Sin sincronía el cliente podría mostrar avance falso (violación Constitución V Server Truth).

**Independent Test**: Simular `Round 4 COMPLETED` → servidor emite `RoundCompleted {gameId, roundNumber=4}` + `QuestionAvailable {roundNumber=5, expiresAt}`. Verificar cliente dispara `hydrate GET /api/games/{id}/players/me` (no usa payload evento para Score/isCorrect), actualiza `currentRoundNumber` a 5 en <500ms percibido, anima transición (ej. fila 4 → completed con check animado, fila 5 entra con highlight + pulso), sin actualizar Current Reward fuera de `hydrate`.

**Acceptance Scenarios**:

1. **Given** `ROUND_IN_PROGRESS` ronda 4 con `Answer EVALUATED`, **When** servidor transita a `ROUND_COMPLETED` y emite `RoundCompleted`, **Then** cliente hace `hydrate` y solo tras respuesta exitosa anima ladder: Previous Levels incluye 4 con check, Current Level pasa a 5 con animación premium (no antes del `hydrate`).
2. **Given** evento `RoundCompleted` recibido pero `hydrate` falla (network 500 con `X-Correlation-Id`), **When** ocurre, **Then** no avanza Current Level (mantiene 4), muestra `ErrorState` con Retry + CorrelationId y reintenta `hydrate` con `exponential backoff` (no deja ladder en estado intermedio falso).
3. **Given** reconexión SignalR `Reconnected` o `withAutomaticReconnect`, **When** se reconecta, **Then** dispara `hydrate` y sincroniza ladder a `currentRoundNumber` autoritativo (si servidor avanzó 2 rondas offline, salta a la correcta sin animar rondas intermedias falsas).
4. **Given** animación en curso, **When** inspecciona `prefers-reduced-motion`, **Then** animación respeta `reduced-motion` (transición instantánea, sin parpadeo) y mantiene `aria-live="polite"` anunciando "Avanzaste a ronda 5".

---

### User Story 4 — Experiencia responsive, accesible y premium en tema Player (Priority: P2)

Como jugador en móvil/desktop, quiero que la ladder sea `Cinematic Immersive Premium` con tokens de SPEC-016, responsive y accesible, para que la progresión se perciba competitiva sin scroll horizontal.

**Why this priority**: Complementa las 3 anteriores con requisitos no funcionales de SPEC-016/029 (Design System, `data-theme="player"`, WCAG). Entrega valor como pulido independiente verificable por axe.

**Independent Test**: Abrir ladder en 375px, 768px, 1280px, 1536px → verificar sin scroll horizontal, targets ≥44px, `data-theme="player"` tokens sin literales, contraste AA, foco visible, `aria-live` para Current/Secured/Next cambios, y cinematic header/gradiente premium.

**Acceptance Scenarios**:

1. **Given** viewport 375px, **When** ve ladder vertical, **Then** filas apiladas sin scroll horizontal, scroll vertical interno si N>viewport con `max-height` y `overflow-y:auto`, targets ≥44px.
2. **Given** tokens `design-system/tokens/design-tokens.css` con `data-theme="player"`, **When** inspecciona CSS, **Then** 0 literales hardcodeados para color/spacing/typography/radius, usa CSS variables.
3. **Given** auditoría axe/Lighthouse, **When** corre, **Then** WCAG 2.2 AA pass (contraste tokens, foco `outline:2px`, `aria-current`, `aria-live`).
4. **Given** `prefers-reduced-motion: reduce`, **When** avanza ronda, **Then** transición sin movimiento excesivo.

---

### Edge Cases

- ¿Qué pasa si `MaxRounds` cambia tras `StartGame`? `GameConfiguration` es inmutable tras start (SPEC-001/004); ladder ignora mutación y mantiene N original snapshoteado.
- ¿Qué ocurre si `RewardRules` define checkpoint asegurado cada 5 rondas pero `LossPolicy=LOSE_ALL`? Secured Reward muestra 0 (política prevalece); UI refleja ledger no regla aislada.
- ¿Cómo se comporta si `currentRoundNumber` es `null` en `WAITING_FOR_PLAYERS`? Ladder muestra estado Empty "Aún no inicia — N rondas por jugar" sin Current.
- ¿Qué pasa si N=15 y viewport 375px? Ladder virtualizable/scrollable interna sin scroll horizontal; nunca renderiza N filas hardcodeadas.
- ¿Qué ocurre si `hydrate` trae `currentRoundNumber` que retrocede (corrección server)? Ladder sincroniza hacia atrás sin animar avance falso, anunciando corrección vía `aria-live`.
- ¿Qué pasa si `Secured Reward` no existe (aún sin checkpoint)? Muestra "Sin monto asegurado" con icono muted, no escudo.
- ¿Cómo maneja `Difficulty` `CategorySpecific`? Mostrar `Level` resuelto por `IDifficultyProgressionStrategy` (ej. "Geografía — Hard") sin hardcodear 1..5.
- ¿Qué ocurre con reconexión tardía que perdió 2 `RoundCompleted`? `hydrate` sincroniza al último `currentRoundNumber` sin animar rondas saltadas como secuenciales falsas.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema DEBE mostrar escalera `Round 1 .. Round N` donde `N = GameConfiguration.maxRounds` (≥5, inmutable tras `StartGame`) ordenada ascendente sin huecos, cada fila con `RoundNumber`, `Level/Difficulty` (texto + indicator visual 1..5 o `CategorySpecific`), y estado visual `completed/current/upcoming` derivado de `GameSession.currentRoundNumber` + `Round.status`.
- **FR-002**: La fila `Current Level` DEBE destacarse como `aria-current="step"` con token premium (`data-theme="player"` gradient/borde) y anunciarse "Nivel actual, ronda X de N", derivada de `currentRoundNumber` autoritativo (`GET /api/games/{id}/players/me`), nunca de payload de evento.
- **FR-003**: Las filas `Previous Levels` (roundNumber < current) DEBEN mostrar estado `completed` (check/icono muted, opacidad reducida, sin highlight) y ser distinguibles de `Current` y `upcoming`.
- **FR-004**: El sistema DEBE mapear `Difficulty` por fila desde `Round.level` (Enumeration 1..5 Basic..Expert o `CategorySpecific`) resuelta por `IDifficultyProgressionStrategy` (Linear/Progressive/Adaptive/CategorySpecific) y mostrar progresión creciente coherente con `Round.Difficulty` autoritativo (no calculada cliente).
- **FR-005**: El sistema DEBE mostrar `Current Reward` como valor/beneficio asociado a `currentRoundNumber` según `GameConfiguration.RewardRules` o `PointsPerRound` acumulado ledger (proyección), y `Next Reward` como umbral de `currentRoundNumber+1` si existe; si no configurado mostrar placeholder "—" sin romper layout.
- **FR-006**: El sistema DEBE mostrar `Secured Reward` como último checkpoint asegurado: `SecuredPoints.securedPoints` + `checkpointRoundNumber` según política `KEEP_SECURED_SCORE`/`KEEP_CHECKPOINT_SCORE` (o 0 si `LOSE_ALL`), con icono escudo/lock y resaltado de filas ≤ checkpoint como "asegurado" (overlay success token), derivado exclusivamente de ledger `PointTransaction` (`sum` reconstructable).
- **FR-007**: El sistema DEBE mostrar `Final Reward` siempre en fila `N` (Round N) con tratamiento premium (corona/gradiente, token `final`) y valor de `RewardRules[maxRounds]` o `GameBonus` si configurado, incluso antes de alcanzarlo.
- **FR-008**: La transición de ronda DEBE ser visualmente clara: animación premium (highlight/pulso/check) al pasar `Current` de `k` a `k+1` y `Previous` marcando `k` como completed, respetando `prefers-reduced-motion`, con duración corta (<400ms) y `aria-live="polite"` anunciando "Avanzaste a ronda X".
- **FR-009**: La transición DEBE estar sincronizada con servidor: el cambio de `Current Level` solo ocurre tras `hydrate` exitoso (`GET /api/games/{id}/players/me` o `getMyState`) disparado por eventos `RoundCompleted`/`QuestionAvailable`/`ScoreUpdated`/`GameFinished`/`Reconnected`; el payload del evento NO DEBE usarse como fuente para `Current Reward/Next/Secured/Score/isCorrect` (Constitución V).
- **FR-010**: Ante `hydrate` fallido o evento sin `hydrate`, el sistema NO DEBE avanzar Current Level (mantiene estado previo) y DEBE mostrar `ErrorState` con `CorrelationId/TraceId` (RFC 7807) y `Retry` con backoff; reconexión `withAutomaticReconnect` DEBE re-disparar `hydrate`.
- **FR-011**: El sistema DEBE consumir estado autoritativo solo vía `GET /api/games/{id}/players/me` (GameSession + Game + RewardRules + ledger Secured) y recalibrar `N` y `currentRoundNumber` en cada `hydrate`; SignalR (`GameRealtimeService`) solo dispara `hydrate`, no muta store directamente.
- **FR-012**: El sistema DEBE propagar `X-Correlation-Id` en `hydrate` y mostrarlo en estados `Loading/Empty/Error` (skeleton, waiting, ProblemDetails) según SPEC-029.
- **FR-013**: El sistema DEBE cumplir Design System SPEC-016: usar `design-system/tokens/design-tokens.css` vía `data-theme="player"`, sin literales, tokens para spacing/typography/color/radius/shadow; experiencia `Cinematic Immersive Premium Competitive` con ladder vertical premium.
- **FR-014**: El sistema DEBE ser `Responsive` 375–1536 sin scroll horizontal, con ladder scrolleable vertical interna si N excede viewport, y `Accessible` WCAG 2.2 AA (contraste tokens, foco `outline:2px`, `role="list"`/`listitem`, `aria-current`, `aria-live`, teclado Tab/Shift+Tab, axe pass) — SPEC-016.
- **FR-015**: Seguridad delegada (Constitución VI/H): `hydrate` DEBE requerir JWT válido `jwks_uri`, `PlayerId=sub`, `must_change_password` gating redirect; sin JWT → 401 redirect OIDC, sin exponer datos de otro jugador.
- **FR-016**: Estados vacíos/terminales: en `WAITING_FOR_PLAYERS`/sin ronda DEBE mostrar Empty "Aún no inicia"; en `WITHDRAWN/ELIMINATED/FINISHED` DEBE bloquear animación, mostrar Secured/Final final y `isTerminal` sin permitir avance (isTerminal bloquea transición).

### Key Entities *(include if feature involves data)*

- **Game / GameSession**: `GameId`, `MaxRounds` (N≥5 inmutable), `Status` (9 estados), `GameSession.currentRoundNumber` (1..N, autoritativo), `PlayerStatus` (ACTIVE/WITHDRAWN...), `RowVersion`. Fuente de N y Current.
- **Round / GameRound**: `RoundId`, `GameId`, `RoundNumber` 1..N, `Level/Difficulty` 1..5 o CategorySpecific, `Status` WAITING/IN_PROGRESS/COMPLETED, `StartedAt/ExpiresAt/CompletedAt`. Cada fila de la ladder corresponde a un `GameRound` (existente o futuro placeholder hasta N).
- **DifficultyLevel**: Enumeration 1..5 (Basic, Elementary, Intermediate, Advanced, Expert) o mapeo CategorySpecific; resuelta por `IDifficultyProgressionStrategy` (Linear/Progressive/Adaptive/CategorySpecific).
- **RewardRule / Reward**: `RewardId`, `RoundThreshold` (ej. Round 5→500, Round 10→5000), `Name`, `PointsRequired`; `RewardRules` en `GameConfiguration`. Mapea Current/Next/Final.
- **SecuredPoints**: `securedPoints` (int), `checkpointRoundNumber` (int|null), `policy` (KEEP_SECURED_SCORE etc.) derivado de `PointTransaction` ledger (D). Determina Secured Reward y filas aseguradas.
- **PointTransaction**: Ledger append-only `Type` ANSWER_CORRECT/INCORRECT/ROUND_BONUS/LEVEL_BONUS/GAME_BONUS/PENALTY/WITHDRAWAL etc., usado para reconstruir `Current/ Secured/ Next/Final` sin cálculo cliente.
- **LadderState (view model)**: `rounds: LadderRow[]` (size N) con `RoundNumber, Level, State completed/current/upcoming, CurrentReward, NextReward flag, IsSecured, IsFinal`, computado tras cada `hydrate`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% de juegos muestran ladder con exactamente `N = maxRounds` filas Round 1..N sin faltantes ni duplicados y `Current Level` con `aria-current="step"` coincidente con `currentRoundNumber` autoritativo (`GET /players/me`); verificado en `hydrate`.
- **SC-002**: `Previous Levels` (<current) renderizan como `completed` con check/icono muted en 100% de casos; futuros (>current) como `upcoming` muted; `Current Level` destacado premium en 100% sin confundir estados.
- **SC-003**: `Current Reward`, `Next Reward`, `Secured Reward`, `Final Reward` coinciden con ledger `PointTransaction` + `RewardRules` en 100% (reconstruible `sum` = total, checkpoint según política); `Final Reward` visible en fila N en 100% de partidas.
- **SC-004**: `Secured Reward` muestra escudo y resalta filas ≤ `checkpointRoundNumber` como aseguradas en 100% cuando `KEEP_SECURED_SCORE`; con `LOSE_ALL` muestra 0 asegurado en 100% (política respetada).
- **SC-005**: 100% de transiciones de ronda ocurren solo tras `hydrate` exitoso disparado por `RoundCompleted`/`QuestionAvailable`/`Reconnected`; 0% de avances usan payload evento como fuente de verdad (audit log `hydrate` vs evento).
- **SC-006**: Transición anima de forma clara (<400ms, pulso/highlight) y anuncia `aria-live` en 100%; respeta `prefers-reduced-motion` en 100% (sin animación excesiva).
- **SC-007**: Ante `hydrate` fallido, 0% de ladders avanzan falsamente; 100% muestran `ErrorState` con `CorrelationId` y `Retry` funcional; reconexión resincroniza a `currentRoundNumber` correcto en 100%.
- **SC-008**: Responsive 375–1536 sin scroll horizontal en 100% de viewports; axe/WCAG 2.2 AA pass 100% (contraste tokens, foco, list semantics); `data-theme="player"` 0 literales hardcodeados.
- **SC-009**: Experiencia percibida como Cinematic/Premium/Competitive por ≥80% en test cualitativo (contraste, gradiente final, animación clara sin ruido).
- **SC-010**: `Difficulty` por fila coincide con `Round.level` autoritativo (strategy Linear 1→2→3→4→5 clamp 1..5 verificado) en 100% de rondas; CategorySpecific muestra nombre resuelto sin hardcodear.

## Assumptions

- Se extiende `QuizArena.Player` Angular 22 SPA de SPEC-027/029 con `PlayerRoundsStore` (`signalStore` scoped por `gameId`, `ladder: Signal<LadderRow[]>`) + `PlayerRoundsComponent` (ladder vertical) integrado en `GameComponent` (`features/game/player-rounds.component.ts`) y opcional en `player/game` sidebar, reutilizando `GamesApi.getMyState`/`getGame` y `GameRealtimeService` (`withAutomaticReconnect` → `hydrate`) — Server Truth V.
- `oroclash-api` ya expone `GetMyPlayerState` `GET /api/games/{id}/players/me` con `gameSession {currentRoundNumber, securedPoints, checkpointRoundNumber}`, `game {maxRounds, configuration { RewardRules, PointsPerRound, WithdrawalPolicy, LossPolicy, DifficultyStrategy, TimeLimitPerQuestion}}`, `rounds[]` y `ledger` proyectado; no se crean nuevos agregados, `LadderRow` es proyección.
- `Available Games` y lobby son SPEC-028; esta ladder se muestra en `/player/game/:gameId` (SPEC-029) cuando `Game.status` IN_PROGRESS/ROUND_IN_PROGRESS/ROUND_COMPLETED/FINISHED.
- `MaxRounds` 5–15 típico (mínimo 5 per SPEC-005 FR-002); ejemplo 10 rondas con 5 niveles mapeados 1-2 Basic, 3-4 Elementary etc. para `Linear`; `Adaptive` puede no ser monotónico pero siempre refleja `Round.level` autoritativo.
- `Current Reward` deriva de `RewardRules[roundNumber]` si existe, si no de `PointsPerRound * roundNumber` acumulado; `Next Reward` = `RewardRules[current+1]`; `Secured Reward` de `SecuredPoints`; `Final Reward` = `RewardRules[maxRounds]` o `GameBonus`.
- `RewardRules` puede estar vacío → placeholders "—" sin romper layout premium.
- Design System 016 ya genera `design-system/tokens/design-tokens.css` + `overrides/player.md` con `data-theme="player"` en `angular.json` styles y `app.component.ts`; ladder usa CSS variables sin literales para cinematic (gradientes fila final, spacing ladder, color current/premium).
- Animación usa CSS transition/transform (no JS pesado) con `prefers-reduced-motion` media query; no bloquea `hydrate`.
- Tokens nunca en `localStorage`; `authInterceptor` adjunta Bearer solo a `apiUrl`; `MustChangePasswordGuard` ya aplica.
- Layout: ladder vertical `role="list"` con filas `role="listitem"` + fila final premium; en desktop puede ser sidebar sticky, en móvil scrolleable interna.

## Dependencies

- SPEC-001 `Game Configuration` (MaxRounds≥5, RewardRules, PointsPerRound, Withdrawal/Loss policies, DifficultyStrategy, TimeLimit) — define N y reglas de Secured/Final.
- SPEC-004 `Game Lifecycle` (State Machine 9 estados, GamePlayer lifecycle ACTIVE→WITHDRAWN, rowversion).
- SPEC-005 `Round Engine` (GameRound con RoundNumber 1..N, Difficulty 1..5, TimeLimit, Status, IQuestionSelectionStrategy, IDifficultyProgressionStrategy Linear/Progressive/Adaptive/CategorySpecific).
- SPEC-007 `Scoring System` (PointTransaction ledger, SecuredPoints checkpoint, Current/Next/Final reconstruibles).
- SPEC-008 `Player Withdrawal` (explicit WithdrawPlayer, checkpoint asegurado).
- SPEC-012 `Realtime Game Events` (GameHub RoundCompleted/QuestionAvailable/ScoreUpdated/GameFinished + hydrate, withAutomaticReconnect).
- SPEC-016 `UI/UX Design System` (tokens, data-theme="player", WCAG 2.2 AA, 375-1536, cinematic/immersive/premium).
- SPEC-027 `Player Application` (QuizArena.Player Angular 22, PlayerGameStore, GamesApi, GameRealtimeService, app.routes.ts).
- SPEC-028 `Player Lobby` (Available Games, JoinGame previo).
- SPEC-029 `Player Game` (pantalla principal con Current Round/Level/Question/Answers/Timer/Score/Secured/Potential/Status/Withdraw — esta ladder complementa esa pantalla).
- BuildingBlocks (`Kernel.Domain` AggregateRoot/IBusinessRule/Result, `CQRS ISender/IEndpoint`, `ServiceDefaults` OTel/health, `Kernel.Infrastructure AppDbContextBase`).
- OroIdentityServer `oroidentityserver:latest` discovery `/.well-known/openid-configuration`, PKCE `authorization_code`+`refresh_token`, `jwks_uri`, `must_change_password`.

## Out of Scope

- Creación/edición de GameConfiguration o RewardRules (Admin SPEC-019).
- Selección de pregunta / banco / IQuestionSelectionStrategy (SPEC-003/005).
- Lógica de scoring/ledger detallada más allá de proyección (SPEC-007).
- Rewards redemption (`POST /rewards/{id}/redeem` SPEC-009) más allá de visualizar Current/Next/Secured/Final.
- Consolation (SPEC-010), leaderboards globales (SPEC-011) más allá de ladder individual.
- Administración (Admin Blazor SPEC-017) y reporting (SPEC-015).
- Matchmaking, invitaciones, chat.
- Juego offline (sin conexión no hay ladder autoritativa).

## References

- `draft/constitution.md` §I-VI, §A-J (Domain First, Server Truth V, OroIdentityServer VI/H, Validation I, Audit I, OTel).
- `draft/game-concept.md` §Game/Round Lifecycle A, §Scoring D, §Withdrawal C, §Rewards.
- `draft/oroidentityserver-specification.md` (OIDC PKCE discovery, X-Correlation-Id).
- `design-system/MASTER.md` + `design-system/overrides/player.md` + `design-system/tokens/design-tokens.css` (Cinematic/Premium, data-theme="player", WCAG 375-1536).
- `src/Player/QuizArena.Player` (`app.routes.ts` `/game/:id`, `stores/player-game.store.ts` + futuro `player-rounds.store.ts`, `features/game/player-rounds.component.ts`, `features/shared/games.api.ts` `getMyState`, `core/realtime/game-realtime.service.ts` `withAutomaticReconnect`, `core/interceptors/`).
- `src/OroQuizClash.Application/Features/Games/` (`GetMyPlayerState`, `GetGame`, `GetPlayerScore`, `IEndpoint`).
- `OroQuizClash.AppHost/AppHost.cs` (`quizarena-player` → `oroclash-api` `identity-api`).
- `specs/029-player-game/` (pantalla principal donde se embebe la ladder).
