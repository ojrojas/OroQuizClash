# Research: Player Rounds (030)

**Branch**: `030-player-rounds` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Summary

0 `NEEDS CLARIFICATION` en spec; investigación consolida 5 decisiones para ladder vertical Round 1..N con 6 estados visuales (Current Level premium `aria-current`, Previous `completed`, Current/Next/Secured/Final rewards derivados de ledger) y transición sincronizada con servidor (evento → `hydrate` `GET /players/me` autoritativo, nunca payload del evento) con animación <400ms `prefers-reduced-motion` y `aria-live`. Todo alineado a constitución v1.1.0 + BuildingBlocks net10.0 y patrones de SPEC-027 (Player App) / 028 (Lobby) / 029 (Game) + SPEC-016 Design System `data-theme="player"`.

## Decisions

### 1. Ladder vertical con `PlayerRoundsStore` dedicado (`LadderRow[]` N=MaxRounds) y `role="list"` + `aria-current`

- **Decision**: Crear `stores/player-rounds.store.ts` `signalStore` con `state: { gameId: string|null, ladder: LadderRow[], currentRoundNumber: number|null, secured: SecuredPoints|null, rewardRules: RewardRule[], status: 'loading'|'empty'|'ready'|'error'|'terminal', previousTransition: number|null, _animating: boolean }` + `computed`: `currentLevel` (`ladder.find(r=>r.roundNumber===currentRoundNumber)`), `previousLevels` (`filter < current`), `nextReward` (`rewardRules.find(r=>r.roundThreshold===currentRoundNumber+1)`), `securedReward` (`securedPoints`), `finalReward` (`rewardRules.find(r=>r.roundThreshold===maxRounds)`), `finalRow` (`ladder[N-1]`). `withMethods`: `hydrateLadder(gameId)` `rxMethod` → `GamesApi.getMyState(gameId)` `switchMap` `tapResponse` → `patchState({ladder: buildLadder(maxRounds, rounds, rewardRules, secured, current), currentRoundNumber, status})` + `_animating` trigger <400ms si `previousTransition !== current`. `bindRealtimeLadder()` conecta `GameRealtimeService` eventos `RoundCompleted`/`QuestionAvailable`/`ScoreUpdated`/`GameFinished`/`Reconnected` → `hydrateLadder` (no muta ladder directo). `buildLadder(maxRounds, roundsFromServer, rewardRules, secured, current)` mapea 1..N filas `LadderRow {roundNumber, level, state:'completed'|'current'|'upcoming', isSecured: roundNumber<=checkpoint, isFinal: roundNumber===maxRounds, currentReward: rewardRules[roundNumber]?.points??pointsPerRound*roundNumber??null, nextFlag, securedFlag}`. `PlayerRoundsComponent` ladder vertical `role="list"` `aria-label="Progresión de rondas"` cada fila `role="listitem"` `aria-current="step"` solo Current Level, escudo `aria-label="Asegurado"` si `isSecured`, corona `aria-label="Recompensa final"` si `isFinal`, `aria-live="polite"` anuncia "Avanzaste a ronda X". CSS `display:flex flex-direction:column gap:var(--space-2) max-height:60vh overflow-y:auto` scrolleable interna si N=15, sin scroll horizontal. Responsive: sidebar sticky ≥1024px, panel apilado 375px. Usa `design-system/tokens/design-tokens.css` `data-theme="player"` sin literales.

- **Rationale**: FR-001..FR-007 (6 estados visuales), FR-011 (hydrate solo fuente), SPEC-016 `data-theme="player"` + FR-014 WCAG 375-1536, separation of concerns vs `PlayerGameStore` 10 elementos (029) — ladder tiene ciclo propio `LadderRow[]` N y 4 rewards derived.

- **Alternatives**: Extender `PlayerGameStore` con `ladder` field (rechazado — mezcla 10 elementos game + LadderRow N + 4 rewards + transition, viola SRP y testeabilidad; 030 debe ser independiente testeable); hardcodear N=10 (rechazado — `MaxRounds` dinámico 5–15 FR-001); CSS Grid 2 cols para ladder (rechazado — ladder es vertical 1 col, Grid innecesario).

- **Accessibility**: `role="list"`/`listitem`, `aria-current="step"` Current, `aria-label` per fila "Ronda 4 de 10, nivel Intermediate, recompensa 600 pts, asegurado", `aria-live="polite"` Current/Next/Secured, `prefers-reduced-motion`, targets ≥44px, foco `outline:2px solid var(--color-primary)`.

### 2. Recompensas derivadas de ledger `PointTransaction` + `RewardRules` (Current/Next/Secured/Final) sin cálculo cliente

- **Decision**: `Current Reward` = `RewardRules.find(r=>r.roundThreshold===currentRoundNumber)?.points` ?? `game.configuration.pointsPerRound * currentRoundNumber` (fallback proyección) ?? `null → "—"`. `Next Reward` = `RewardRules[current+1]` ?? `null → "—"` con estilo `upcoming` muted + flecha. `Secured Reward` = `SecuredPoints.securedPoints` + `checkpointRoundNumber` (derivado `PointTransaction` ledger `sum` `KEEP_SECURED_SCORE`, 0 si `LOSE_ALL` per SPEC-007/008) con icono escudo/lock `var(--color-success)` y filas ≤ checkpoint overlay `background: var(--color-success-subtle)`. `Final Reward` = `RewardRules[maxRounds]` ?? `gameBonus` ?? `null → "—"` con tratamiento premium `gradient: var(--player-gradient-final)` + corona SVG, siempre en fila N. Todos valores vienen de `GET /players/me` (`game.configuration.rewardRules`, `securedPoints`, `ledger` proyectado si necesario) — cliente nunca calcula ledger.

- **Rationale**: FR-005..FR-007 + SC-003/004 (100% ledger-reconstructable) + constitución D (Ledger) + C (RewardRules configurable). Placeholder "—" FR-005.

- **Alternatives**: Calcular Secured cliente-side `roundNumber*points` (rechazado — viola D, debe ser `PointTransaction` sum); mostrar solo Current Reward (rechazado — spec exige 4 rewards).

### 3. Transición sincronizada con servidor (evento → hydrate autoritativo, nunca payload del evento)

- **Decision**: `PlayerRoundsStore.bindRealtimeLadder()` suscribe `GameRealtimeService.on<RoundCompleted|QuestionAvailable|ScoreUpdated|GameFinished|Reconnected>` → `hydrateLadder(gameId)` `switchMap` `GamesApi.getMyState` `tapResponse` → `patchState` Current Level solo tras 200. Antes de hydrate, ladder no muta (mantiene `currentRoundNumber` previo). Si hydrate falla (500/429 con `X-Correlation-Id`), `patchState({status:'error', error: {detail, correlationId}})` y NO avanza Current; muestra `ErrorState` con Retry + `CorrelationId/TraceId` y `exponential backoff` (1s,2s,4s) opcional. Reconexión `withAutomaticReconnect` → `hydrateLadder` inmediato; si servidor avanzó 2 rondas offline, `currentRoundNumber` salta directo a 5 sin animar 3→4 intermedios falsos (detecta `previousTransition` diff >1 → animación directa sin secuelas). Evento `RoundCompleted` payload se ignora para rewards/level (FR-009).

- **Rationale**: FR-008..FR-011 + SC-005/007 + constitución V (Server Truth) + G (SignalR no fuente). Patrón ya probado en 029 `GameRealtimeService` `withAutomaticReconnect` → `hydrate`.

- **Alternatives**: Avanzar Current al recibir `RoundCompleted` sin hydrate (rechazado — viola V, payload cliente-trust); usar `setTimeout` para animar antes de hydrate (rechazado — desincroniza si hydrate falla).

### 4. Animación premium <400ms con `prefers-reduced-motion` y `aria-live`

- **Decision**: Transición CSS: `.ladder-row.current { transform: scale(1.02); border-color: var(--color-primary); box-shadow: var(--shadow-premium); transition: all 300ms ease-out; }` + `.ladder-row.completed { opacity:0.7; }` + check `::after` animado 200ms. Flag `_animating` en store true 350ms luego false (driven by `effect` que setTimeout 350ms). `@media (prefers-reduced-motion: reduce) { .ladder-row { transition: none; transform: none; } }` → instantáneo. `aria-live="polite"` `div` fuera de ladder anuncia "Avanzaste a ronda 5. Recompensa actual 600 puntos. Asegurado en ronda 5." Cambios sin `aria-live` excesivo (solo Current/Secured/Final).

- **Rationale**: FR-008 + SC-006 + WCAG 2.5.1 + SPEC-016 Cinematic premium (corona gradiente, Current Scale) sin ruido.

- **Alternatives**: JS animation library (Framer Motion) (rechazado — overkill, literales, bundle); sin `prefers-reduced-motion` (rechazado — WCAG fails).

### 5. Estados Loading/Empty/Error/Terminal + X-Correlation-Id + responsive sin literales

- **Decision**: Reusar `correlationIdInterceptor` (`X-Correlation-Id: crypto.randomUUID()` per hydrate) + `errorInterceptor` RFC7807 ya en 027/029. Estados: `Loading` → skeleton ladder 5 filas `aria-busy` `aria-live="polite"`; `Empty` (`WAITING_FOR_PLAYERS` o `currentRoundNumber==null`) → "Aún no inicia — N rondas por jugar" `role="status"` sin Current; `Error` → `app-error-state` `detail` + `CorrelationId/TraceId` + Retry CTA → `hydrateLadder`; `Terminal` (`WITHDRAWN/ELIMINATED/FINISHED` `isTerminal`) → bloquea transición (`_animating=false`), muestra Secured/Final finales con overlay. Todos con `data-theme="player"` tokens `var(--space-*)` `var(--color-*)` `var(--font-*)` 0 literales. Tests Vitest: ladder N exacto, `aria-current`, `isSecured` escudo, `isFinal` corona, transición solo tras hydrate mock `getMyState` 200, error mantiene previous, reconnect jump, axe 0 violations.

- **Rationale**: FR-010..FR-016 + SC-007/008 + constitución I (Validation/Errors) + H (X-Correlation-Id) + J (DTO boundary).

- **Alternatives**: `X-Correlation-Id` solo en lobby (rechazado — todo request auditarse); inline styles ladder (rechazado — viola SPEC-016 tokens).

## Resolved Clarifications

| Topic | Resolution |
|-------|------------|
| N filas hardcodeado vs dinámico | N=maxRounds dinámico 5–15 FR-001, buildLadder 1..N sin hardcode. |
| Difficulty CategorySpecific | Mostrar `Level` resuelto `IDifficultyProgressionStrategy` ("Geografía — Hard") sin hardcodear 1..5. |
| Reward sin config | Placeholder "—" `aria-label="Sin recompensa"` FR-005. |
| Secured con LOSE_ALL | 0 asegurado FR-006 (política prevalece sobre RewardRules). |
| Transición sin hydrate | Nunca avanza FR-009/010. |
| Reconexión salto 2 rondas | Hydrate directo a current autoritativo sin animar intermedios falsos (edge case 030). |
| Cinematic definition | Ladder vertical premium: Current scale+glow token, Final gradiente+corona, spacing `var(--space-*)`, validado 80% SC-009. |

## References

- `draft/constitution.md` §I–VI, §A–J, §V Server Truth, §C Configurable Rules, §D Ledger, §H `sub=PlayerId`.
- `draft/game-concept.md` §Game/Round Lifecycle A, §Scoring D, §Withdrawal C.
- `draft/oroidentityserver-specification.md` OIDC PKCE `X-Correlation-Id`.
- `design-system/MASTER.md` + `overrides/player.md` + `tokens/design-tokens.css` Cinematic `data-theme="player"` WCAG 375-1536.
- `src/Player/QuizArena.Player` `stores/player-game.store.ts` + futuro `player-rounds.store.ts` `features/game/player-rounds.component.ts` `features/shared/games.api.ts` `getMyState` `core/realtime/game-realtime.service.ts` `withAutomaticReconnect` `core/interceptors/` (SPEC-027/029).
- `src/OroQuizClash.Application/Features/Games/` `GetMyPlayerState` `GetGame` `IEndpoint` `GameClaims` `X-Idempotency-Key`.
- `OroQuizClash.AppHost/AppHost.cs` `quizarena-player` container `node:22-alpine`.
- `specs/027-player-application/` + `028-player-lobby/` + `029-player-game/` (previos).
