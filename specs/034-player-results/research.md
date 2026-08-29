# Research: Player Results (034)

**Branch**: `034-player-results` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Summary

0 `NEEDS CLARIFICATION` en spec; investigación consolida 5 decisiones para 4 pantallas finales autoritativas `YOU WON` (`WINNER` Rank1 `Final Score`+`Prize`), `YOU WALKED AWAY` (`WITHDRAWN` `Secured`·checkpoint + `Available Rewards`), `GAME OVER` (`ELIMINATED` `Final Score`+`Consolation`), `GAME FINISHED` (`FINISHED` 2..N `Final Position`+`Final Score`+`Reward`) en `QuizArena.Player` Angular 22 SPA (SPEC-027/029/032/033) como proyección de sólo lectura de `GetMyPlayerState` per `sub` + `GetLeaderboard` `Rank` (Server Truth V), `ResultComponent` `route /player/game/:gameId/result` con redirect si `!IsTerminal`, `data-theme="player"` tokens 4 gradientes `prefers-reduced-motion`.

## Decisions

### 1. Cuatro pantallas per `ResultState` `WINNER/WITHDRAWN/ELIMINATED/FINISHED` con `Leaderboard Rank`

- **Decision**: `ResultComponent` `selector app-result` standalone `route /player/game/:gameId/result` `canActivate: [authGuard, mustChangePasswordGuard]` computa `resultState` via `PlayerGameStore` `computed resultState = status().playerStatus` + `GameStatus` `IsTerminal` + `Leaderboard Rank`: `WINNER` si `PlayerStatus==WINNER` && `GameStatus==FINISHED` && `Rank==1` → `YOU WON`; `WITHDRAWN` si `PlayerStatus==WITHDRAWN` → `YOU WALKED AWAY`; `ELIMINATED` si `PlayerStatus==ELIMINATED` → `GAME OVER`; `FINISHED` si `GameStatus==FINISHED` && `PlayerStatus==FINISHED` `Rank` 2..N → `GAME FINISHED`. Si `GameStatus` no terminal (`!IsTerminal` && `PlayerStatus==ACTIVE` && `RoundStatus==IN_PROGRESS`) redirige `router.navigate(['/player/game', gameId])` con `ErrorState` "Partida aún en curso". Template `@if resultState()==='won' YOU WON @else if ==='walked' YOU WALKED AWAY @else if ==='over' GAME OVER @else if ==='finished' GAME FINISHED` cada con `role="status"` `aria-live="assertive"`.

- **Rationale**: FR-001/011 + SC-001..004 + SC-006 + Constitución A (Game Lifecycle 4 terminales) + V (Server Truth `Rank`).

- **Alternatives**: Single pantalla genérica `Result` con `if` en TS sin `Rank` (rechazado — no distingue `YOU WON` Rank1 vs `GAME FINISHED` 2..N, viola SC-004); 4 rutas separadas `/result/won` etc. (rechazado — duplica routing, no escala, `Leaderboard Rank` es dinámico).

- **Accessibility**: `aria-live="assertive"` título, `role="status"` por pantalla, `Tab` + `Enter` en CTA "Volver al lobby".

### 2. `Final Score` ledger + `Final Position` `Leaderboard Rank` autoritativo per `sub`

- **Decision**: `Final Score` es `Score.totalPoints` `sum(PointTransaction)` server-side vía `GET /players/me` `score.totalPoints` (D/V); `Final Position` es `Leaderboard Rank` 1..N vía `GET /leaderboard` `LeaderboardEntry` `Rank` per `sub` (`LeaderboardBuilder.Build(game)` orden `totalPoints` desc). `ResultComponent` `finalScore = store.score().totalPoints`, `finalPosition = leaderboard().find(e=>e.playerId==sub)?.position ?? null`, `totalPlayers = leaderboard().length`. Nunca `Final Score = Current+Secured` cliente.

- **Rationale**: FR-002/007/009 + SC-005 + Constitución D (Ledger) + V (Server Truth).

- **Alternatives**: Calcular `Final Position` cliente-side ordenando `Players` `totalPoints` (rechazado — viola V, `Rank` incluye `CorrectAnswers` + `AchievedAt` tie-break SPEC-011).

### 3. `Prize`/`Consolation`/`Available Rewards` filtrable per `Secured`/`totalPoints`

- **Decision**: `Prize` en `YOU WON` es `Reward` `RewardId` si `totalPoints >= pointsRequired` de `GameConfiguration.RewardRules` o `RewardRedemption` `Status=DELIVERED` per `sub` (SPEC-009 C); `Consolation Reward` en `GAME OVER` es `Reward` `CONSOLATION` si `ConsolationPolicy` `FixedPoints`/`ParticipationBased`/`RewardBased` otorga (SPEC-010 C) per `sub`; `Available Rewards` en `YOU WALKED AWAY` es `GET /api/rewards` lista filtrable `reward.pointsRequired <= securedPoints.securedPoints` (no `CurrentPoints`). Si null → ocultar bloque o "Sin recompensa/consolación" `aria-live polite`.

- **Rationale**: FR-003/006/008 + SC-001..004 + Constitución C (Configurable `Reward`/`Consolation`).

- **Alternatives**: Mostrar `Prize` cliente-side si `totalPoints>500` (rechazado — viola C, `RewardRules` es configurable).

### 4. `Secured Points` · checkpoint para `YOU WALKED AWAY`

- **Decision**: `Secured Points` es `SecuredPoints.securedPoints` + `checkpointRoundNumber` per `sub` (D). Display `formatSecured(secured, checkpoint)` → `"{n} pts · checkpoint {m}"` si `checkpoint != null` else `"{n} pts"` (032). `YOU WALKED AWAY` `Secured` con badge `asegurado` `var(--color-warning)` y `Available Rewards` list `role="list"`.

- **Rationale**: FR-004/005 + SC-002 + Constitución D (Ledger `SecuredPoints`).

- **Alternatives**: Mostrar `Secured` sin checkpoint (rechazado — SC-002 requiere `checkpoint {m}`).

### 5. `data-theme="player"` 4 gradientes + `prefers-reduced-motion` + `X-Correlation-Id`

- **Decision**: `ResultComponent` 4 pantallas con `display:flex; flex-direction:column; gap:var(--space-4); max-width:600px; margin:auto; min-height:100vh;` `YOU WON` `background: var(--color-success-gradient)` `var(--color-success)` `confetti` `animation: pulse 600ms` `YOU WALKED AWAY` `var(--color-warning)` `GAME OVER` `var(--color-destructive)` `GAME FINISHED` `var(--color-accent)` tokens `var(--space-*)` `var(--color-*)` sin literales; `@media (prefers-reduced-motion: reduce) { *{animation:none;}}`. `correlationIdInterceptor` `X-Correlation-Id` per `GET /players/me` + `GET /leaderboard`, `errorInterceptor` RFC7807, `authInterceptor` `secureRoutes=[apiUrl]`, `must_change_password` gating.

- **Rationale**: FR-010/012 + SC-007/008/009 + Constitución H/I + SPEC-016.

- **Alternatives**: Sin `X-Correlation-Id` en `Result` (rechazado — trazabilidad OTel).

## Resolved Clarifications

| Topic | Resolution |
|-------|------------|
| `ResultState` detección | `PlayerStatus WINNER` + `Rank 1` → `YOU WON`; `WITHDRAWN` → `YOU WALKED AWAY`; `ELIMINATED` → `GAME OVER`; `FINISHED` + `FINISHED` 2..N → `GAME FINISHED`; `!IsTerminal` → redirect `/player/game/:id` |
| `Final Score` cálculo | `Score.totalPoints` `sum(PointTransaction)` per `sub` via `GET /players/me`; no `Current+Secured` cliente |
| `Prize` null | Ocultar bloque sin error; "Sin premio" no necesario si no hay premio (pero `YOU WON` con `Prize` null muestra "Sin premio" opcional) |
| `Secured checkpoint null` | Sin badge "checkpoint", solo `"{n} pts"` |
| `Consolation` null | `GAME OVER` "Sin consolación" |
| `Available Rewards` filtro | `reward.pointsRequired <= securedPoints.securedPoints` (no `CurrentPoints`) |
| `Final Position` null | Si `Leaderboard` no tiene `Rank` per `sub` (ej. no en game), mostrar "—" |

## References

- `draft/constitution.md` §I–VI, §A-J, §V Server Truth `Rank` + `Final Score` ledger, §C `Withdrawal`/`Consolation`/`Reward`, §G `GameFinished` → `hydrate`.
- `draft/game-concept.md` §Scoring §Withdrawal §Game/Round Lifecycle §Reward §Consolation.
- `design-system/MASTER.md` + `overrides/player.md` + `tokens/design-tokens.css` Cinematic `data-theme="player"` WCAG 375-1536.
- `src/Player/QuizArena.Player` `app.routes.ts` `/game/:gameId/result` `stores/player-game.store.ts` `Score/SecuredPoints` `features/result/result.component.ts` placeholder `features/shared/games.api.ts` `getMyState/getLeaderboard` `core/realtime/game-realtime.service.ts` `GameFinished` (SPEC-027/032/033).
- `src/OroQuizClash.Application/Features/Games/` `GetMyPlayerState` per `sub` + `GetLeaderboard` `Rank` + `GetGame` `GameStatus` `IEndpoint`.
- `OroQuizClash.AppHost/AppHost.cs` `quizarena-player` container `node:22-alpine`.
- `specs/007-scoring-system/` `specs/011-multiplayer/` `specs/033-player-multiplayer/` (previos).
