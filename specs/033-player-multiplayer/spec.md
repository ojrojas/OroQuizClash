# Feature Specification: Player Multiplayer

**Feature Branch**: `033-player-multiplayer`

**Created**: 2026-08-29

**Status**: Ready for Review

**Input**: User description: "033 — Player Multiplayer Tecnología Angular 22 Objetivo Proporcionar la experiencia multiplayer sin comprometer el estado privado de cada participante. Descripción Cada jugador deberá tener: Private Game State Private Answer State Private Score State Private Timer Private Session Podrá visualizar información pública: Players Players Remaining Leaderboard Current Round pero nunca deberá recibir información privada de otros jugadores que pueda comprometer la competencia. Ejemplo: GAME SERVER │ ├── Player A → Angular A ├── Player B → Angular B ├── Player C → Angular C └── Player D → Angular D"

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Estado privado aislado por jugador (Priority: P1)

Como jugador en partida multiplayer (A, B, C, D), quiero que mi `Private Game State` + `Private Answer State` + `Private Score State` + `Private Timer` + `Private Session` sean visibles solo para mí, sin que otros jugadores vean mis respuestas, puntuación detallada, temporizador restante ni `isCorrect`, para garantizar competencia justa.

**Why this priority**: Es el núcleo de la feature ("sin comprometer el estado privado"). Sin aislamiento, un jugador podría espiar `Answer Selected`/`isCorrect`/`Timer` de otro y copiar o bloquear. Entrega valor independiente como aislamiento per `GameSession` (F).

**Independent Test**: Con 4 jugadores A-D en `ROUND_IN_PROGRESS` mismo `GameId`, abrir `/player/game/:id` como A y como B (dos browsers con JWT diferentes `sub=A` vs `sub=B`) → verificar que `GET /api/games/{id}/players/me` de A retorna `Answer.selectedOptionId` de A y `Score 100` de A, mientras B retorna `Answer` de B y `Score 250` de B; inspeccionar que payload de A no contiene `Answer` de B ni `Timer` de B; `GET /players/me` con `sub=A` no expone `isCorrect` de B.

**Acceptance Scenarios**:

1. **Given** partida con jugadores A y B en `ROUND_IN_PROGRESS`, **When** A abre `GET /players/me` con JWT `sub=A`, **Then** ve su `Private Game State` (`GameStatus` genérico), `Private Answer State` (`Answer` solo suya `PENDING/EVALUATED`), `Private Score State` (`Score` solo suya), `Private Timer` (`Timer` con `expiresAt` propio), `Private Session` (`GameSession` suya), y no ve `Answer/Score/Timer/Session` de B.
2. **Given** A responde opción B y B responde opción C, **When** ambos consultan `GET /players/me` simultáneamente, **Then** cada uno ve solo su `SelectedOptionId` y `isCorrect` solo si `EVALUATED` suya; `isCorrect` de B no aparece en payload de A (filtrado SPEC-006).
3. **Given** jugador no autenticado, **When** intenta `GET /players/me`, **Then** recibe 401 redirect OIDC, no datos privados.
4. **Given** jugador intenta suplantar `playerId` en body (ej. `POST /answers` con `playerId=B` mientras JWT `sub=A`), **When** envía, **Then** servidor ignora body y usa `sub=A` (GameClaims.GetSub), rechaza 403 si intenta acceder a `GameSession` de otro (H).

---

### User Story 2 — Visualizar información pública sin fuga (Priority: P1)

Como jugador, quiero ver información pública agregada `Players` + `Players Remaining` + `Leaderboard` (ranking anonimizado o con alias) + `Current Round` sin recibir detalles privados de otros, para tener contexto competitivo sin comprometer privacidad.

**Why this priority**: Complementa el aislamiento: el jugador necesita ver cuántos quedan y ranking, pero nunca `Answer Selected`/`isCorrect`/`Timer` de otros. Sin vista pública el juego se siente solitario; con fuga se rompe fairness.

**Independent Test**: Con 4 jugadores, abrir como A → verificar que `GET /api/games/{id}/leaderboard` retorna `Players` con `displayName` + `currentLevel` + `totalPoints` anonimizado o sin `SelectedOptionId/isCorrect`, y `Players Remaining` count, y `Current Round` 3/10; inspeccionar que no hay `Answer` de otros en `leaderboard` ni en `GET /players/me` de A.

**Acceptance Scenarios**:

1. **Given** 4 jugadores `ACTIVE` en `Game` `IN_PROGRESS`, **When** A abre `GET /leaderboard` y `GET /game/{id}/players` público, **Then** ve lista `Players` con `playerId, displayName, status ACTIVE` y `Players Remaining` 4, y `Current Round` 3/10, sin ver `Answer/Score` privado de otros.
2. **Given** `Leaderboard` ordenado por `TotalPoints`, **When** consulta, **Then** ve ranking con `totalPoints` y `level` públicos, pero no `SelectedOptionId`, `isCorrect`, `Timer`, `SecuredPoints` detallado de otros.
3. **Given** jugador B se retira (`WITHDRAWN`), **When** A refresca `Players Remaining`, **Then** ve 3 restantes y `Leaderboard` actualiza sin exponer motivo privado de B.
4. **Given** lector de pantalla, **When** navega `Leaderboard`, **Then** anuncia "Jugador 2, nivel Intermediate, 450 puntos, puesto 1" con `aria-live polite` sin revelar datos privados.

---

### User Story 3 — Sesiones privadas y timers por jugador (Priority: P2)

Como jugador, quiero que mi `Private Session` (`GameSession` per `playerId+gameId` con `RowVersion`) y mi `Private Timer` (`expiresAt` per `Round` per jugador) sean independientes, para que mi desconexión o tiempo no afecte a otros.

**Why this priority**: `Private Session` y `Private Timer` son parte del estado privado; compartir `RowVersion` o `expiresAt` entre jugadores causaría conflictos de concurrencia y ventaja temporal.

**Independent Test**: Con A y B en misma ronda, verificar que `GET /players/me` de A retorna `GameSession.currentRoundNumber 2` y `Timer expiresAt 12:00:30Z` mientras B retorna `currentRoundNumber 2` pero `Timer` puede diferir si `StartRound` con `TimeLimit` distinto; modificar `GameSession` de A (ej. `Withdraw`) no afecta `GameSession` de B; `RowVersion` por sesión aislado.

**Acceptance Scenarios**:

1. **Given** A y B en misma `Game`, **When** ambos llaman `GET /players/me`, **Then** cada uno recibe `GameSession` con su propio `GameSessionId` y `RowVersion` distintos, y `Timer` con `expiresAt` per `Round` (posiblemente mismo para ambos si misma ronda, pero no compartido).
2. **Given** A hace `POST /withdraw` y su `GameSession` pasa a `WITHDRAWN` `RowVersion++`, **When** B consulta su `GameSession`, **Then** sigue `ACTIVE` sin interferencia (F).
3. **Given** A pierde conexión y reconecta `Reconnected` → `hydrate`, **When** B sigue activo, **Then** `Private Session` de B no se resetea.

---

### User Story 4 — Concurrencia multiplayer sin interferencia (Priority: P2)

Como sistema, quiero soportar múltiples instancias `Angular A/B/C/D` conectadas al mismo `GAME SERVER` con `GameHub` `ScoreUpdated`/`RoundCompleted` por jugador, sin mezclar estados, para escalar a `MaxPlayers` 10 sin fuga.

**Why this priority**: Valida aislamiento bajo carga y `SignalR` `withAutomaticReconnect` per `gameId+playerId`; sin ello se mezclarían `Answer/Score` en memoria `signalStore` per `GameSession`.

**Independent Test**: Simular 4 browsers concurrentes A-D cada uno con `PlayerGameStore` scoped `providers: [PlayerGameStore]` per `GameComponent`, enviar `SubmitAnswer` simultáneo desde A y B → verificar que `Answer` de A no aparece en store de B y `Score` de A no contamina B; `Leaderboard` público sí se actualiza para todos vía `LeaderboardUpdated` pero sin detalles privados.

**Acceptance Scenarios**:

1. **Given** 4 store instancias A-D aisladas, **When** A envía `POST /answers` y B envía otro, **Then** cada store `answer().selectedOptionId` corresponde a su propio envío, no al otro.
2. **Given** `GameHub` emite `ScoreUpdated` para A, **When** B recibe evento, **Then** B hace `hydrate` y ve su propio `Score` (no el de A), mientras `Leaderboard` público se actualiza con `totalPoints` genérico.
3. **Given** viewport 375px con 4 jugadores, **When** ve `Players Remaining` 4 y `Leaderboard` 4 filas, **Then** layout no muestra datos privados de otros en `QuestionComponent` o `ScorePanelComponent`.

---

### Edge Cases

- ¿Qué pasa si un jugador inspecciona `GET /players/me` de otro interceptando JWT? Servidor valida `sub=PlayerId` desde JWT `jwks_uri`, no body, y rechaza 403 `PlayerIdentityMismatch` auditado (H).
- ¿Qué ocurre si `Leaderboard` intenta mostrar `isCorrect` de otro jugador? Contrato `GET /leaderboard` no incluye `IsCorrect`/`SelectedOptionId`/`Timer`; cualquier intento de exponerlo es rechazado por review de arquitectura (Domain ↛ Angular).
- ¿Cómo maneja `Private Timer` si `Round` es `ROUND_IN_PROGRESS` para A pero `ROUND_COMPLETED` para B por desfase de `hydrate`? Cada `GET /players/me` retorna `Timer` con `expiresAt` per `GameRound` actual; cliente corrige con `serverNow` pero no comparte.
- ¿Qué pasa si dos jugadores envían `Answer` al mismo `Round` simultáneamente? Cada `Answer` tiene `UNIQUE (GameId,RoundId,PlayerId)` aislado; `RowVersion` por `Game` protege transición pero `Answer` per jugador no colisiona.
- ¿Qué ocurre si `GameSession` `RowVersion` de A y B es igual inicialmente y ambos hacen `Withdraw`? Cada `Withdraw` es per `GamePlayer` (`GamePlayerId` RowVersion), no `Game` global; no conflitto.
- ¿Cómo se comporta `Players Remaining` si un jugador es `ELIMINATED` por `AnswerIncorrect` `LOSE_ALL`? `Players Remaining` cuenta solo `IsActive` (`ACTIVE` no `WITHDRAWN/ELIMINATED`), visible público.
- ¿Qué pasa si `SignalR` reenvía `ScoreUpdated` con payload `totalPoints` de otro jugador? Cliente ignora payload y hace `hydrate` `GET /players/me` (V), no confía en evento.
- ¿Qué ocurre si `GameComponent` es `providers: [PlayerGameStore]` compartido por error entre jugadores? Debe ser `providers: [PlayerGameStore]` per instancia `GameComponent` (Scoped), no `providedIn: 'root'` compartido; review verifica `isolation.spec.ts`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema DEBE aislar `Private Game State` por `playerId+gameId` vía `GET /api/games/{id}/players/me` que retorna `Game` genérico + `GameSession` privado (`GamePlayerId`, `UserId=sub`, `Status`, `CurrentRoundNumber`, `RowVersion`) solo del requester (H/F).
- **FR-002**: El sistema DEBE aislar `Private Answer State` por `playerId+roundId` vía `GET /players/me` `Answer` con `SelectedOptionId`/`State`/`IsCorrect` solo si `answer.PlayerId==sub` y `state==EVALUATED` sino `IsCorrect=null`; nunca exponer `Answer` de otro jugador (V/B).
- **FR-003**: El sistema DEBE aislar `Private Score State` por `playerId` vía `GET /players/me` `Score`+`SecuredPoints`+`PointTransaction` ledger solo del requester; `Leaderboard` público solo expone `totalPoints`/`level` sin `Answer/SelectedOptionId/isCorrect/Timer` (D).
- **FR-004**: El sistema DEBE aislar `Private Timer` por `playerId+roundId` vía `GET /players/me` `Timer` con `expiresAt` per `GameRound` + `serverNow` corrección; cada jugador tiene `Timer` independiente aunque misma ronda (F).
- **FR-005**: El sistema DEBE aislar `Private Session` por `playerId+gameId` (`GameSession` `GamePlayerId` `RowVersion`) — operaciones `Withdraw`/`Answer` usan `sub` no body y no mutan sesión de otro (H/F).
- **FR-006**: El sistema DEBE exponer `Players` público vía `GET /api/games/{id}/players` (o `GET /games/{id}` con `Players` lista) con `playerId/displayName/status` sin `Answer/Score` privado; `Players Remaining` es count `IsActive` (`ACTIVE` count) (A).
- **FR-007**: El sistema DEBE exponer `Leaderboard` público vía `GET /api/games/{id}/leaderboard` con ranking `playerId/displayName/totalPoints/level/position` sin `SelectedOptionId/isCorrect/Timer/Secured` detallado (D).
- **FR-008**: El sistema DEBE exponer `Current Round` público vía `GET /api/games/{id}/rounds/current` o `GET /players/me` `Round` con `RoundNumber/Level/Status` genérico sin `Question` privada de otro (A/C).
- **FR-009**: El sistema DEBE hacer que `QuizArena.Player` Angular 22 mantenga `PlayerGameStore` scoped `providers: [PlayerGameStore]` per `GameComponent` instancia, no `providedIn: 'root'` compartido, para aislar `Answer/Score/Timer` en memoria (F).
- **FR-010**: El sistema DEBE actualizar `Private State` solo vía `hydrate` `GET /players/me` disparado por `GameRealtimeService` eventos `ScoreUpdated/RoundCompleted/Reconnected` (Server Truth V, SignalR no fuente veredicto) (G).
- **FR-011**: El sistema DEBE propagar `X-Correlation-Id` por `GET /players/me` + `GET /leaderboard` y mostrar `CorrelationId/TraceId` en `ErrorState` (I).
- **FR-012**: El sistema DEBE cumplir `Design System` SPEC-016 `data-theme="player"` sin literales para bloque `Players/Leaderboard/Current Round` y `Responsive` 375–1536 sin scroll, `Accessible` `aria-live` (SPEC-016).
- **FR-013**: Seguridad delegada (VI/H): todas las APIs multiplayer DEBEN requerir JWT válido `jwks_uri`, `sub=PlayerId`, `must_change_password` gating; sin JWT → 401 OIDC; `PlayerId` de `sub` no del body; payload nunca incluye privados de otros.

### Key Entities *(include if feature involves data)*

- **Private Game State**: Proyección `Game` genérico (`GameId`, `Name`, `Status`, `Configuration` sin `Reward` sensible) + `GameSession` privado (`GamePlayerId`, `UserId=sub`, `Status ACTIVE/WITHDRAWN/ELIMINATED/WINNER`, `CurrentRoundNumber`, `RowVersion`) per `playerId+gameId`.
- **Private Answer State**: `Answer` (`AnswerId`, `PlayerId=sub`, `GameId`, `RoundId`, `QuestionId`, `SelectedOptionId`, `SubmittedAt` server, `State PENDING/EVALUATED/EXPIRED`, `IsCorrect` bool|null solo `EVALUATED`, `IdempotencyKey` per `playerId+roundId`) — aislado per `UNIQUE (GameId,RoundId,PlayerId)`.
- **Private Score State**: `Score` (`PlayerId=sub`, `GameId`, `TotalPoints`, `RoundPoints`, `CorrectAnswers`, `CurrentLevel`) + `SecuredPoints` (`securedPoints`, `checkpointRoundNumber`, `policy`) + `PointTransaction` ledger per `playerId` — no expuesto en `Leaderboard` detallado.
- **Private Timer**: `Timer` (`TimeLimitSeconds`, `ExpiresAt` per `GameRound`, `RemainingSeconds` computed, `State RUNNING/STOPPED/EXPIRED`, `ServerNow`) per `playerId+roundId` con corrección `serverNow`.
- **Private Session**: `GameSession` (`GamePlayerId`, `PlayerId=sub`, `GameId`, `Status`, `CurrentRoundNumber`, `RowVersion` base64) — `Withdrawal` usa `RowVersion` per `GamePlayer`, no global.
- **Players / Players Remaining**: Lista pública `Players[]` con `playerId/displayName/status IsActive` + `PlayersRemaining` count `IsActive` (`ACTIVE` count) per `Game`.
- **Leaderboard**: Ranking público `LeaderboardEntry[]` con `playerId/displayName/totalPoints/level/position` sin `SelectedOptionId/isCorrect/Timer/Secured`.
- **Current Round**: `Round` público (`RoundId`, `GameId`, `RoundNumber`, `Level`, `Status`, `QuestionId` sin `AnswerOption` detallada para otros) per `Game`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% de `GET /players/me` con JWT `sub=A` no exponen `Answer/Score/Timer/Session` de B (0% leak privado, verificado por contrato con 2 JWTs en paralelo).
- **SC-002**: 100% de `Leaderboard` y `Players Remaining` muestran solo datos públicos (`totalPoints/level/displayName/status`) sin `SelectedOptionId/isCorrect/Timer` de otros (0% fuga).
- **SC-003**: 100% de stores `PlayerGameStore` scoped per `GameComponent` mantienen `Answer/Score/Timer` aislados (`isolation.spec.ts` con 4 instancias A-D sin contaminación).
- **SC-004**: 100% de `Private Timer` y `Private Session` `RowVersion` son per `playerId` sin interferencia (Withdraw de A no afecta `GameSession` de B).
- **SC-005**: 100% de envíos `POST /answers` con `sub=A` no mutan `Answer/Score` de B (`QuestionAlreadyAnswered` per `playerId` aislado, ledger no duplica cross-player).
- **SC-006**: 100% de cambios de `Score` se reflejan en <1s vía `ScoreUpdated→hydrate` sin confiar en payload del evento (Server Truth V).
- **SC-007**: Responsive 375–1536 sin scroll horizontal para bloque `Players/Leaderboard/Current Round` 100% y WCAG 2.2 AA `axe` 0 violations (`role="list"` `aria-live`).
- **SC-008**: 100% de requests incluyen `X-Correlation-Id` y errores muestran `CorrelationId/TraceId`; 100% requieren JWT válido (sin JWT → 401).

## Assumptions

- Se extiende `QuizArena.Player` Angular 22 SPA existente (SPEC-027/029/030/031/032) con `PlayerGameStore` 10 elementos scoped `providers: [PlayerGameStore]` per `GameComponent` ya en 029 (no `providedIn: 'root'` global); `GamesApi.getMyState` ya retorna privado per `sub`.
- `oroclash-api` ya expone `GetMyPlayerState` `GET /players/me` privado per `sub`, `GetLeaderboard` `GET /leaderboard` público sin privados, `GetGamePlayers` `GET /players` público lista, `SubmitAnswer` `POST /answers` per `playerId+roundId` `UNIQUE`, `GameHub` `ScoreUpdated` per `gameId` (no por `playerId` pero cliente hace `hydrate` privado).
- `Game` tiene `MaxPlayers` 10 default, `MaxRounds` 5–15, `PointsPerRound` 100, `WithdrawalPolicy`/`LossPolicy` ya en 007.
- `SignalR` `withAutomaticReconnect [0,2000,5000,10000,30000]` ya en `GameRealtimeService`; cada Angular instancia A-D tiene su propia `HubConnection` con `gameId+accessTokenFactory` per `sub`.
- `Players Remaining` es `Players.count(IsActive)` donde `IsActive = Status==ACTIVE`; `Leaderboard` ordenado por `TotalPoints` descendente.
- Design System 016 ya en `angular.json` `design-system/tokens/design-tokens.css` `data-theme="player"`; se reutiliza sin literales.
- Tokens nunca en `localStorage`; `authInterceptor` Bearer solo `apiUrl`; `must_change_password` gating ya aplica (VI/H).
- Layout existente `GameComponent` grid `280px 1fr` con ladder sidebar 030 y center question 031 y footer scoring 032; bloque multiplayer `Players/Leaderboard/Current Round` vive en header/sidebar sin ocultar `ScorePanel`.

## Dependencies

- SPEC-007 `Scoring System` (`Score`/`SecuredPoints`/`PointTransaction` ledger D).
- SPEC-011 `Multiplayer` base (`GamePlayer`, `Players`, `Leaderboard` inicial) — esta feature lo privatiza.
- SPEC-012 `Realtime Game Events` (`GameHub` `ScoreUpdated/LeaderboardUpdated/RoundCompleted` + `hydrate`).
- SPEC-016 `UI/UX Design System` (`design-system/tokens/design-tokens.css` `data-theme="player"` WCAG).
- SPEC-027 `Player Application` (`QuizArena.Player` Angular 22 SPA, `PlayerGameStore` scoped, `GamesApi`, `GameRealtimeService`).
- SPEC-029 `Player Game` (`GameComponent` 10 elementos, `ScorePanel` 5 métricas).
- SPEC-030 `Player Rounds` (`PlayerRoundsStore` ladder `Secured`).
- SPEC-031 `Player Answering` (`AnswerInteractionStore` `SubmitAnswer` `isCorrect` filtrado).
- SPEC-032 `Player Scoring` (`ScorePanel` 5 métricas, `TotalPoints` autoritativo).
- BuildingBlocks (`Kernel.Domain` `AggregateRoot/Result`, `CQRS ISender/IEndpoint`, `ServiceDefaults` OTel).
- OroIdentityServer `oroidentityserver:latest` `jwks_uri` PKCE `must_change_password`.

## Out of Scope

- Creación de juegos/preguntas/categorías (SPEC-001/003/005).
- Lógica de scoring detallada (SPEC-007 `AwardPoints` etc. ya autoritativo) más allá de aislamiento.
- Withdrawal/rewards/consolation detallado más allá de `Private Session` `RowVersion` (SPEC-008/009/010).
- Administración (Admin Blazor SPEC-017) y reporting (SPEC-015).
- Matchmaking avanzado, invitaciones, chat, amigos.
- Juego offline (sin conexión no hay multiplayer).
- Filtros de lobby avanzados (SPEC-028).
- Notificaciones push más allá de `GameHub` existente.

## References

- `draft/constitution.md` §I-VI, §A-J, §D Ledger, §F Concurrency `UNIQUE (GameId,RoundId,PlayerId)` `RowVersion`, §G Realtime, §H `sub=PlayerId`, §V Server Truth (multiplayer aislado).
- `draft/game-concept.md` §Multiplayer §Scoring §Game/Round Lifecycle.
- `draft/oroidentityserver-specification.md` (OIDC PKCE `X-Correlation-Id`).
- `design-system/MASTER.md` + `overrides/player.md` + `tokens/design-tokens.css` (`data-theme="player"` WCAG).
- `src/Player/QuizArena.Player` (`stores/player-game.store.ts` scoped, `features/game/score-panel.component.ts` `game.component.ts`, `features/shared/games.api.ts` `getMyState` `getLeaderboard`, `core/realtime/game-realtime.service.ts` `GameHub`).
- `src/OroQuizClash.Application/Features/Games/` (`GetMyPlayerState` privado `sub`, `GetLeaderboard` público, `GetGamePlayers` público, `SubmitAnswer` per `playerId`, `IEndpoint`).
- `OroQuizClash.AppHost/AppHost.cs` (`quizarena-player` → `oroclash-api`).
- `specs/011-multiplayer/` `specs/029-player-game/` `specs/032-player-scoring/` (previos).
