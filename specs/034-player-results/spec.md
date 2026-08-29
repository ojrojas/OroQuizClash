# Feature Specification: Player Results

**Feature Branch**: `034-player-results`

**Created**: 2026-08-29

**Status**: Ready for Review

**Input**: User description: "034 — Player Results Tecnología Angular 22 Objetivo Mostrar el resultado final de la participación del jugador. Descripción Debe contemplar: Victoria YOU WON Final Score Prize Retiro YOU WALKED AWAY Secured Points Available Rewards Eliminación GAME OVER Final Score Consolation Reward Juego finalizado GAME FINISHED Final Position Final Score Reward"

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Victoria YOU WON (Priority: P1)

Como jugador que gana la partida (primer puesto al `FINISHED`), quiero ver pantalla `YOU WON` con `Final Score` y `Prize` (recompensa ganada) con celebración premium, para sentir recompensa y entender mi logro.

**Why this priority**: Es el resultado más deseado ("Victoria") y cierra el loop competitivo. Sin `YOU WON` el ganador no percibe victoria. Entrega valor independiente como proyección final autoritativa.

**Independent Test**: Con `Game` `FINISHED` y `Player` `WINNER` `GamePlayer.Score.CurrentPoints 850` `Winner=true` `Reward` `Pack Oro`, abrir `/player/game/:id/result` → verificar `YOU WON` título, `Final Score 850 pts`, `Prize Pack Oro` con `aria-live="assertive"`, sin `YOU WALKED AWAY` ni `GAME OVER`.

**Acceptance Scenarios**:

1. **Given** partida `FINISHED` con jugador `WINNER` (posición 1), **When** abre resultado, **Then** ve `YOU WON` (título grande `data-theme="player"` gradiente), `Final Score 850 pts` y `Prize Pack Oro` (o lista de `RewardRedemption` si múltiples), con confetti/`pulse` `prefers-reduced-motion` reduce sin animación.
2. **Given** victoria con `GameBonus` + `LevelBonus`, **When** revisa `Final Score`, **Then** coincide con `GET /players/me` `Score.totalPoints` + ledger `sum(PointTransaction)` autoritativo (D).
3. **Given** lector de pantalla, **When** navega victoria, **Then** anuncia "Felicidades, YOU WON, puesto 1, 850 puntos, premio Pack Oro" con `aria-live="assertive"`.
4. **Given** sin `Prize` (no `RewardRules`), **When** gana, **Then** ve `YOU WON` + `Final Score` sin bloque `Prize` roto (placeholder "Sin premio" no muestra error).

---

### User Story 2 — Retiro YOU WALKED AWAY (Priority: P1)

Como jugador que se retira voluntariamente (`WITHDRAWN`), quiero ver `YOU WALKED AWAY` con `Secured Points` y `Available Rewards` según política `KEEP_SECURED_SCORE`, para entender qué conservé y qué puedo canjear.

**Why this priority**: Cubre "Retiro" explícito (withdrawal) — una de las 4 salidas. Sin `YOU WALKED AWAY` el jugador no entiende qué puntos aseguró.

**Independent Test**: Con `Player` `WITHDRAWN` `SecuredPoints 200 checkpoint 2` `Available Rewards [Pack Plata 300 pts]`, abrir `/player/game/:id/result` → verificar `YOU WALKED AWAY`, `Secured Points 200 pts · checkpoint 2`, `Available Rewards` lista con `Pack Plata` canjeable, sin `YOU WON` ni `GAME OVER`.

**Acceptance Scenarios**:

1. **Given** jugador `WITHDRAWN` con `KEEP_SECURED_SCORE` 200 pts, **When** abre resultado, **Then** ve `YOU WALKED AWAY` (título `var(--color-warning)`), `Secured Points 200 pts · checkpoint 2` y `Available Rewards` (rewards con `pointsRequired <= SecuredPoints`).
2. **Given** `WITHDRAWN` con `LOSE_ALL` 0 asegurados, **When** ve resultado, **Then** ve `YOU WALKED AWAY` + `Secured Points 0 pts` + `Available Rewards` vacía con "Sin recompensas disponibles" `aria-live polite`.
3. **Given** jugador intenta `POST /withdraw` ya `WITHDRAWN`, **When** abre resultado, **Then** idempotente sin nuevo ledger y `YOU WALKED AWAY` persistente.

---

### User Story 3 — Eliminación GAME OVER (Priority: P1)

Como jugador eliminado (`ELIMINATED` por `LOSE_ALL` o `FALLBACK_TO_CHECKPOINT`), quiero ver `GAME OVER` con `Final Score` y `Consolation Reward` si aplica, para entender por qué perdí y si hay consolación.

**Why this priority**: Cubre "Eliminación" — pérdida forzada por regla, distinta de retiro voluntario. Necesita `GAME OVER` terminal y `Consolation` si `Spec 010` aplica.

**Independent Test**: Con `Player` `ELIMINATED` `Final Score 120` `ConsolationReward` `Pack Consuelo` (si `ConsolationPolicy` cumple), abrir `/player/game/:id/result` → verificar `GAME OVER`, `Final Score 120 pts`, `Consolation Reward Pack Consuelo` o "Sin consolación" si no aplica.

**Acceptance Scenarios**:

1. **Given** jugador `ELIMINATED` por `AnswerIncorrect` `LOSE_ALL`, **When** abre resultado, **Then** ve `GAME OVER` (`var(--color-destructive)`), `Final Score 0 pts` y `Consolation Reward` si `ConsolationPolicy` `FixedPoints` otorga 50 pts, sino "Sin consolación".
2. **Given** `ELIMINATED` con `ConsolationPolicy ParticipationBased` 80 pts, **When** ve resultado, **Then** ve `GAME OVER` + `Consolation Reward 80 pts` con `aria-live assertive`.
3. **Given** sin `Consolation` elegible, **When** ve `GAME OVER`, **Then** no muestra bloque `Prize` roto, solo `Final Score`.

---

### User Story 4 — Juego finalizado GAME FINISHED para no ganadores (Priority: P2)

Como jugador que termina la partida sin ganar ni retirarse ni ser eliminado (`FINISHED` posición 2..N), quiero ver `GAME FINISHED` con `Final Position` (puesto), `Final Score` y `Reward` (si alcanzó umbral), para ver mi clasificación final.

**Why this priority**: Cubre "Juego finalizado" genérico — el resto de participantes (perdedores no eliminados ni retirados). Completa los 4 estados finales.

**Independent Test**: Con `Game` `FINISHED` y `Player` `FINISHED` posición 3 `Final Score 400` `Reward Pack Bronce` si `RewardRules` threshold 300 alcanzado, abrir `/player/game/:id/result` → verificar `GAME FINISHED`, `Final Position 3`, `Final Score 400 pts`, `Reward Pack Bronce` o "Sin recompensa" si no alcanzó.

**Acceptance Scenarios**:

1. **Given** partida `FINISHED` con 4 jugadores `Player` posición 3, **When** abre resultado, **Then** ve `GAME FINISHED` (`var(--color-accent)`), `Final Position 3` `aria-label` "Puesto 3 de 4", `Final Score 400 pts`, `Reward` si `totalPoints >= pointsRequired`.
2. **Given** posición 2 con `Reward` no alcanzado, **When** ve resultado, **Then** ve `GAME FINISHED` + `Final Position 2` + `Final Score` + "Sin recompensa" `aria-live polite`.
3. **Given** viewport 375px, **When** ve cualquier resultado (`YOU WON`/`YOU WALKED AWAY`/`GAME OVER`/`GAME FINISHED`), **Then** layout 1 col sin scroll horizontal, targets ≥44px, `data-theme="player"` 0 literales.

---

### Edge Cases

- ¿Qué pasa si `Game` aún no es `FINISHED`/`WITHDRAWN`/`ELIMINATED` y se intenta ver `/result`? Redirige a `/player/game/:id` (juego en curso) con mensaje "Partida aún en curso".
- ¿Qué ocurre si `Reward` no está configurado y `Prize` es null? Muestra solo `Final Score` y `Final Position` sin bloque `Prize` roto (placeholder no necesario).
- ¿Cómo maneja `Secured Points` si `checkpointRoundNumber` es null en `YOU WALKED AWAY`? Muestra "200 pts" sin "checkpoint".
- ¿Qué pasa si `Consolation Reward` no aplica y es null? `GAME OVER` muestra "Sin consolación" sin error.
- ¿Qué ocurre si jugador recarga `/result` tras `FINISHED` y `Leaderboard` cambió? `Final Position`/`Final Score` vienen de `GET /players/me` + `GET /leaderboard` autoritativo per `sub`, no cache cliente.
- ¿Cómo se comporta con 10 jugadores en `Leaderboard` y `Final Position` 10? `aria-posinset`/`aria-setsize` correctos, sin fuga de privados de otros (033).
- ¿Qué pasa si token expira en `/result`? Interceptor 401 → `silentRenew`; si falla, redirect OIDC sin perder `Final Score` (hydrate tras reconnect).
- ¿Qué ocurre si cliente modifica `Final Score` en DevTools? Siguiente `GET /players/me` sobrescribe con ledger autoritativo (V).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: El sistema DEBE detectar el estado final del jugador y mostrar exactamente una de las 4 pantallas: `YOU WON` si `PlayerStatus==WINNER` y `GameStatus==FINISHED` posición 1; `YOU WALKED AWAY` si `PlayerStatus==WITHDRAWN`; `GAME OVER` si `PlayerStatus==ELIMINATED`; `GAME FINISHED` si `GameStatus==FINISHED` y `PlayerStatus==FINISHED` posición 2..N (A).
- **FR-002**: El sistema DEBE mostrar `Final Score` autoritativo `Score.totalPoints` (o `score.totalPoints`) de `GET /api/games/{id}/players/me` ledger `sum(PointTransaction)` sin cálculo cliente (D/V).
- **FR-003**: El sistema DEBE mostrar `Prize` en `YOU WON` como `Reward` ganada (`RewardRedemption` o `Leaderboard` `Reward` si `totalPoints >= pointsRequired`) con nombre y puntos requeridos; si no hay `Prize`, ocultar bloque sin error.
- **FR-004**: El sistema DEBE mostrar `Secured Points` en `YOU WALKED AWAY` como `SecuredPoints.securedPoints` + `checkpointRoundNumber` (`"{n} pts · checkpoint {m}"` o `"{n} pts"` si null) per `sub` (D).
- **FR-005**: El sistema DEBE mostrar `Available Rewards` en `YOU WALKED AWAY` como lista `Reward` con `pointsRequired <= SecuredPoints` filtrable, `role="list"` `aria-live polite`; vacía → "Sin recompensas disponibles".
- **FR-006**: El sistema DEBE mostrar `Consolation Reward` en `GAME OVER` si `ConsolationPolicy` otorga (SPEC-010) `Reward` o `Points` `CONSOLATION`; si no aplica, mostrar "Sin consolación" (C).
- **FR-007**: El sistema DEBE mostrar `Final Position` en `GAME FINISHED` como `Leaderboard` `position` `1..N` de `GET /api/games/{id}/leaderboard` público (D) con `aria-label` "Puesto X de N".
- **FR-008**: El sistema DEBE mostrar `Reward` en `GAME FINISHED` si `totalPoints >= pointsRequired` de `RewardRules` próximo umbral, sino "Sin recompensa" (C).
- **FR-009**: El sistema DEBE obtener todos los datos de resultado vía `GET /players/me` privado per `sub` + `GET /leaderboard` público + `GET /players/me` `Game` genérico (V/G).
- **FR-010**: El sistema DEBE propagar `X-Correlation-Id` por `GET /players/me` + `GET /leaderboard` y mostrar `CorrelationId/TraceId` en `ErrorState` si falla (I).
- **FR-011**: El sistema DEBE hacer que `ResultComponent` sea `app-result` standalone `route /player/game/:gameId/result` con `canActivate: [authGuard, mustChangePasswordGuard]` y redirigir a `/player/game/:id` si `GameStatus` no es terminal (`FINISHED/WITHDRAWN/ELIMINATED`) con mensaje "Partida aún en curso".
- **FR-012**: El sistema DEBE cumplir `Design System` SPEC-016 `data-theme="player"` sin literales para las 4 pantallas (`YOU WON` gradiente `success`, `YOU WALKED AWAY` `warning`, `GAME OVER` `destructive`, `GAME FINISHED` `accent`) y `Responsive` 375–1536 sin scroll, `Accessible` `aria-live assertive` para títulos, `role="status"` `aria-label`, foco `outline:2px`, `prefers-reduced-motion` reduce (SPEC-016).
- **FR-013**: Seguridad delegada (VI/H): `GET /players/me` + `GET /leaderboard` DEBEN requerir JWT válido `jwks_uri`, `sub=PlayerId`, `must_change_password` gating; sin JWT → 401 OIDC; `PlayerId` de `sub` no del body.

### Key Entities *(include if feature involves data)*

- **ResultState**: Estado final `WINNER`/`WITHDRAWN`/`ELIMINATED`/`FINISHED` derivado de `GameStatus` + `GamePlayer.ParticipationStatus` + `Leaderboard` `position` 1..N (A).
- **FinalScore**: `Score.totalPoints` autoritativo `sum(PointTransaction)` per `sub` (D).
- **Prize / Reward**: `Reward` (`RewardId`, `Name`, `PointsRequired`, `Type`) o `RewardRedemption` (`RedemptionId`, `RewardId`, `PlayerId`, `Status REQUESTED→DELIVERED`) per `sub` si `totalPoints >= pointsRequired` (C).
- **SecuredPoints**: `SecuredPoints` (`securedPoints`, `checkpointRoundNumber`, `policy`) per `sub` para `YOU WALKED AWAY` (D).
- **AvailableRewards**: Lista `Reward[]` filtrable `pointsRequired <= SecuredPoints` para `YOU WALKED AWAY` (C).
- **ConsolationReward**: `Reward` o `Points` `CONSOLATION` per `sub` si `ConsolationPolicy` cumple (C) para `GAME OVER`.
- **FinalPosition**: `Leaderboard` `position` `1..N` per `sub` desde `GET /leaderboard` público `totalPoints` orden desc (D).
- **Leaderboard**: `LeaderboardEntry[]` público `playerId/displayName/totalPoints/level/position` sin privados (033).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% de jugadores `WINNER` ven `YOU WON` con `Final Score` coincidente con ledger `sum(PointTransaction)` y `Prize` si corresponde, sin `YOU WALKED AWAY`/`GAME OVER`.
- **SC-002**: 100% de jugadores `WITHDRAWN` ven `YOU WALKED AWAY` con `Secured Points` `"{n} pts · checkpoint {m}"` y `Available Rewards` filtrable `pointsRequired <= Secured`, sin `YOU WON`.
- **SC-003**: 100% de jugadores `ELIMINATED` ven `GAME OVER` con `Final Score` y `Consolation Reward` si aplica ("Sin consolación" si no), sin `YOU WON`.
- **SC-004**: 100% de jugadores `FINISHED` posición 2..N ven `GAME FINISHED` con `Final Position` `1..N` `aria-label` "Puesto X de N" y `Final Score` y `Reward` si alcanzó umbral.
- **SC-005**: 100% de `Final Score`/`Final Position`/`Prize` vienen de `GET /players/me` + `GET /leaderboard` autoritativo 0% cálculo cliente (V).
- **SC-006**: Acceso a `/player/game/:id/result` si `GameStatus` no terminal redirige a `/player/game/:id` 100% con mensaje "Partida aún en curso".
- **SC-007**: Responsive 375–1536 sin scroll horizontal para las 4 pantallas 100% y WCAG 2.2 AA `axe` 0 violations (`role="status"` `aria-live assertive/polte` `outline:2px`).
- **SC-008**: 100% de requests incluyen `X-Correlation-Id` y errores muestran `CorrelationId/TraceId`; 100% requieren JWT válido (sin JWT → 401).
- **SC-009**: `prefers-reduced-motion: reduce` deshabilita `pulse`/`confetti` en 100%.

## Assumptions

- Se extiende `QuizArena.Player` Angular 22 SPA existente (SPEC-027/029/030/031/032/033) con `ResultComponent` `app-result` standalone `route /player/game/:gameId/result` ya esbozado en 027 `app.routes.ts` placeholder (`result.component.ts` con "Resultado" genérico) + `PlayerGameStore` 10 elementos (`Score`/`SecuredPoints`/`Game`/`GameSession`) + `GamesApi.getMyState/getLeaderboard` + `GameRealtimeService` ya `hydrate`; se reutiliza `GetMyPlayerState` + `GetLeaderboard` ya privados/públicos.
- `oroclash-api` ya expone `GetMyPlayerState` `Score`/`SecuredPoints`/`GameSession`/`Game` + `GetLeaderboard` `LeaderboardEntry` con `Rank/Points/SecuredPoints/Status` + `GetGame` `GameStatus`; no se crean nuevos agregados; `Prize`/`Consolation` es `Reward` existente (SPEC-009/010) si `totalPoints >= pointsRequired` o `ConsolationPolicy` cumple; `Available Rewards` es `GET /api/rewards` filtrable (009).
- `GameStatus` terminal `FINISHED`/`WITHDRAWN`/`ELIMINATED` derivado de `Game.Status.IsTerminal` + `GamePlayer.ParticipationStatus` (`WINNER` vs `WITHDRAWN` vs `ELIMINATED` vs `FINISHED`); `Final Position` es `Leaderboard` `Rank` per `sub`.
- `Prize` en `YOU WON` es `Reward` con `Status=DELIVERED` o `Winner` threshold; `ConsolationReward` en `GAME OVER` es `Reward` `CONSOLATION` si `ConsolationPolicy` otorga; `Available Rewards` en `YOU WALKED AWAY` son `Reward` con `pointsRequired <= SecuredPoints` (no `CurrentPoints`).
- `ResultComponent` redirige a `/player/game/:id` si `GameStatus` no es terminal (`!IsTerminal` && `PlayerStatus==ACTIVE` && `RoundStatus==IN_PROGRESS`) con `ErrorState` "Partida aún en curso".
- Design System 016 ya en `angular.json` `design-system/tokens/design-tokens.css` `data-theme="player"`; se reutiliza sin literales para 4 pantallas con gradientes/estados.
- Tokens nunca en `localStorage`; `authInterceptor` Bearer solo `apiUrl`; `must_change_password` gating ya aplica (VI/H).
- Layout existente `GameComponent` grid `280px 1fr` no aplica a `ResultComponent` — `ResultComponent` es pantalla completa `min-height:100vh` centrada `max-width:600px` con `data-theme="player"`.

## Dependencies

- SPEC-007 `Scoring System` (`Score`/`PointTransaction` ledger `TotalPoints` D, `SecuredPoints` C).
- SPEC-008 `Player Withdrawal` (`WITHDRAWN` `SecuredPoints` `KEEP_SECURED_SCORE` C).
- SPEC-009 `Reward Redemption` (`Reward` `RewardRedemption` `Available Rewards` C).
- SPEC-010 `Consolation` (`ConsolationPolicy` `ConsolationReward` C).
- SPEC-011 `Multiplayer` base (`GamePlayer` `Leaderboard` `Rank` A).
- SPEC-012 `Realtime Game Events` (`GameFinished`/`ScoreUpdated` → `hydrate` G).
- SPEC-016 `UI/UX Design System` (`design-system/tokens/design-tokens.css` `data-theme="player"` WCAG).
- SPEC-027 `Player Application` (`QuizArena.Player` Angular 22 SPA, `PlayerGameStore`, `GamesApi`, `GameRealtimeService`, `app.routes.ts` `/game/:id`).
- SPEC-029 `Player Game` (`GameComponent` 10 elementos `ScorePanel`).
- SPEC-032 `Player Scoring` (`Score` 5 métricas `TotalPoints`).
- SPEC-033 `Player Multiplayer` (`Private State` per `sub` + `Leaderboard` público).
- BuildingBlocks (`Kernel.Domain` `AggregateRoot/Result`, `CQRS ISender/IEndpoint`, `ServiceDefaults` OTel).
- OroIdentityServer `oroidentityserver:latest` `jwks_uri` PKCE `must_change_password`.

## Out of Scope

- Cálculo de `Winner`/`Rank`/`Consolation` (SPEC-007/011 `LeaderboardBuilder` + `ConsolationPolicy` ya autoritativo) más allá de mostrar `Final Position`/`Prize`/`Consolation`.
- Ledger detallado `PointTransaction` histórico más allá de `Final Score` (SPEC-007 audit).
- Creación de juegos/preguntas/categorías (SPEC-001/003/005).
- Administración (Admin Blazor SPEC-017) y reporting (SPEC-015).
- Matchmaking, chat, amigos, invitaciones.
- Juego offline (sin conexión no hay resultado).
- Filtros de lobby (SPEC-028).
- Notificaciones push más allá de `GameHub` `GameFinished`.

## References

- `draft/constitution.md` §I-VI, §A-J, §D Ledger, §C Configurable `Withdrawal`/`Consolation`/`Reward`, §G Realtime `GameFinished`, §V Server Truth.
- `draft/game-concept.md` §Scoring §Withdrawal §Game/Round Lifecycle §Reward §Consolation.
- `draft/oroidentityserver-specification.md` (OIDC PKCE `X-Correlation-Id`).
- `design-system/MASTER.md` + `overrides/player.md` + `tokens/design-tokens.css` (`data-theme="player"` WCAG).
- `src/Player/QuizArena.Player` (`app.routes.ts` `/game/:id/result`, `stores/player-game.store.ts` `Score/SecuredPoints/Game`, `features/result/result.component.ts` placeholder, `features/shared/games.api.ts` `getMyState/getLeaderboard`, `core/realtime/game-realtime.service.ts` `GameFinished`).
- `src/OroQuizClash.Application/Features/Games/` (`GetMyPlayerState` `GetLeaderboard` `GetGame` `IEndpoint`).
- `OroQuizClash.AppHost/AppHost.cs` (`quizarena-player` → `oroclash-api`).
- `specs/007-scoring-system/` `specs/033-player-multiplayer/` (previos).
