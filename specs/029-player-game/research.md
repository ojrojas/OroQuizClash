# Research: Player Game (029)

**Branch**: `029-player-game` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Summary
0 `NEEDS CLARIFICATION` en spec; investigación consolida 5 decisiones para pantalla principal de juego cinematic con 10 elementos (Current Round/Level/Question/Four Answers/Timer/Score/Secured/Potential/Status/Withdrawal) sobre `PlayerGameStore` 10 elementos ya en SPEC-027, Timer con `interval(1000)` + corrección `serverNow`, Four Answers `radiogroup` accesible, Withdrawal modal confirmación idempotente, y `design-system/tokens` `data-theme="player"` para experiencia Cinematic/Immersive/Premium/Competitive Responsive WCAG 2.2 AA. Todo alineado a constitución v1.1.0 + BuildingBlocks net10.0 y patrones de SPEC-027 (Player App) y SPEC-028 (Lobby).

## Decisions

### 1. Layout cinematic 3 áreas con CSS Grid y `design-system/tokens`

- **Decision**: Pantalla `game.component.ts` con `display:grid; grid-template-areas: "header" "center" "footer";` Header (Current Round + Current Level + Timer) cinematic con gradiente token (`--player-gradient-premium`), Center (Question `h2` + Four Answers `radiogroup` 2x2 grid immersive), Footer (Current Score + Secured Points + Potential Reward + Player Status + Withdrawal Action competitive). Usa `design-system/tokens/design-tokens.css` (ya en `angular.json` styles) + `data-theme="player"` sin literales (`var(--space-4)`, `var(--color-primary)`, `var(--font-display)`). Responsive 375-1536: `Header` apilado 375px, `Center` Answers 1 columna 375px / 2 columnas ≥768px, `Footer` chips apilados 375px, sin scroll horizontal.
- **Rationale**: FR-013 (Cinematic/Immersive/Premium/Competitive) + FR-014 (Responsive 375-1536, targets ≥44px, WCAG) + SPEC-016 `design-system/MASTER.md` `overrides/player.md` (tokens centralizados). Grid por áreas evita flex anidado y mantiene premium spacing.
- **Alternatives**: Flex column única (rechazado — no transmite jerarquía cinematic, difícil premium spacing); Tailwind arbitrario (rechazado — viola Design System, no pasa axe/Lighthouse con tokens).
- **Accessibility**: `aria-live="polite"` para Timer/Score/Status, `aria-live="assertive"` para `EXPIRED/ELIMINATED/WITHDRAWN`, `role="status"` para Round/Level, targets ≥44px, foco `outline:2px solid var(--color-primary)`.

### 2. Timer autoritativo con `interval(1000)` + corrección `serverNow`/`expiresAt`

- **Decision**: `Timer` deriva de `Round.expiresAt` (server ISO UTC) + `_now` signal actualizado por `interval(1000)` → `remainingSeconds = max(0,floor((expiresAt - _now)/1000))` `computed` en `PlayerGameStore` (ya en 027 `withComputed remainingSeconds/isExpired`). Corrección drift: en cada `hydrate` (`GET /players/me` devuelve `timer.serverNow`) y cada `QuestionAvailable` evento `expiresAt` se hace `patchState({ _now: new Date(serverNow).getTime(), timer: {expiresAt} })` y `startTimerTick()` si `RUNNING`. Decisión expiración solo server: `submittedAt <= expiresAt` decide `EVALUATED` vs `400 AnswerWindowExpired` (V). Visual warning color `var(--color-warning)` cuando `remainingSeconds <10`.
- **Rationale**: FR-004 + SC-004 drift <1s 95% + constitución V (server truth) + research R5 027.
- **Alternatives**: `setTimeout` por segundo sin corrección (rechazado — drift acumula >1s sin `serverNow`); confiar en `remainingSeconds` del payload de evento sin rehydrate (rechazado — viola V, payload no es fuente de verdad).

### 3. Four Answers como `radiogroup` accesible con `selectedOptionId` signal y `X-Idempotency-Key`

- **Decision**: `question.component.ts` muestra 4 opciones con `role="radiogroup"` `aria-label="Opciones de respuesta"` y cada botón `role="radio"` `aria-checked="selectedOptionId()===opt.optionId"` `aria-selected` `tabIndex 0` `Space/Enter` selecciona, `selectedOptionId` es `signal<string|null>` local, `Submit` habilitado solo si `store.canAnswer() && selectedOptionId()!=null` (`canAnswer` = `!isTerminal && round IN_PROGRESS && answer PENDING`). Al enviar: `sessionStorage.getItem(`idemp-${roundId}`) ?? crypto.randomUUID()` → `store.submitAnswer(selectedOptionId)` `rxMethod` → `GamesApi.submitAnswer(gameId,{roundId,questionId,selectedOptionId,idempotencyKey})` `POST /api/games/{id}/answers` con header `X-Idempotency-Key` + `tapResponse` → `patchState({answer, timer:STOPPED})` + `hydrate` opcional. `isCorrect` nunca renderizado antes de `EVALUATED` (FR-003); tras `EVALUATED` se muestra `¡Correcto!/Incorrecto` con `aria-live="assertive"`.
- **Rationale**: FR-003 + FR-009 + WCAG `radiogroup` + constitución F idempotencia.
- **Alternatives**: `<input type="radio">` nativo (rechazado — difícil premium styling sin perder a11y, botones con `role=radio` + design tokens es más cinematic); enviar sin `X-Idempotency-Key` (rechazado — doble envío duplica ledger bajo race).

### 4. Withdrawal Action con confirmación modal idempotente

- **Decision**: `withdrawal.component.ts` (o integrado en `game.component.ts`) muestra botón "Retirarse" solo si `!isTerminal && status.canAnswer` (`ACTIVE`). Al pulsar abre modal `Confirmar retiro? Perderás puntos no asegurados según KEEP_SECURED_SCORE` con `Confirm`/`Cancelar` (ambos 44px, `aria-modal=true`). `Confirm` → `sessionStorage idemp-withdraw-{gameId} ?? crypto.randomUUID()` → `store.withdraw()` `rxMethod` → `GamesApi.withdraw(gameId)` `POST /api/games/{id}/withdraw` con `X-Idempotency-Key` → `tapResponse` → `patchState({gameSession: WITHDRAWN, status: isTerminal:true, canAnswer:false})`. Segundo retiro misma key → 200 mismo `GameSession` sin nuevo `PointTransaction` ledger `WITHDRAWAL` (F). Otros jugadores no afectados (aislamiento por `GameSession` scoped).
- **Rationale**: FR-008 + SC-006 + constitución C `WithdrawalPolicy` + F idempotencia + H audit.
- **Alternatives**: Withdraw sin confirmación (rechazado — riesgo UX, retiro es terminal per SPEC-008); DELETE `/players/me` (rechazado — no es `WithdrawPlayer` domain action, viola C).

### 5. Observabilidad con `X-Correlation-Id` y estados Loading/Empty/Error/Expired/Terminal

- **Decision**: Reusar interceptores 027 `correlationIdInterceptor` (`X-Correlation-Id: crypto.randomUUID()` per request), `authInterceptor` (`Authorization: Bearer` solo `apiUrl`), `errorInterceptor` (RFC7807 `ProblemDetails` mapping, 401 silentRenew, 429 `RetryAfter`). Estados: `Loading` → `app-loading-skeleton` cinematic `aria-live="polite"`, `Empty` ("No hay ronda activa" CTA lobby), `Error` (`app-error-state` `detail` + `CorrelationId/TraceId` + Retry → `hydrate`), `Expired` (Timer 0 `aria-live="assertive"` "Tiempo expirado"), `Terminal` (`WITHDRAWN/ELIMINATED/WINNER` `aria-live="assertive"` bloquea `canAnswer`). OTel `BuildingBlocks.ServiceDefaults` ya provee logs con `CorrelationId/TraceId/GameId/PlayerId/RoundId`.
- **Rationale**: FR-012 + FR-016 + SC-009 + constitución I (Validation/Errors/Observability).
- **Alternatives**: `X-Correlation-Id` solo en Join (rechazado — todo request debe auditarse).

## Resolved Clarifications

| Topic | Resolution |
|-------|------------|
| Current Round display | "Ronda 3/10" de `currentRoundNumber/maxRounds`. |
| Timer correction | `interval(1000)` + `serverNow` en cada `hydrate`/`QuestionAvailable`. |
| Potential Reward placeholder | "—" si `RewardRules` no define premio. |
| Withdraw vs Leave | Withdraw es `POST /withdraw` terminal domain action con confirmación; Leave es navegación 028. |
| Cinematic definition | Layout 3 áreas con gradiente token + spacing premium `design-system`, validado cualitativo 80%. |

## References

- `draft/constitution.md` §I-VI, §A-J, §V Server Truth `submittedAt <= expiresAt`, §H `sub`=`PlayerId`.
- `draft/game-concept.md` §Game/Round Lifecycle A, §Scoring D, §Withdrawal C.
- `draft/oroidentityserver-specification.md` OIDC PKCE `X-Correlation-Id`.
- `design-system/MASTER.md` + `overrides/player.md` + `tokens/design-tokens.css` Cinematic/Immersive/Premium `data-theme="player"` WCAG 375-1536.
- `src/Player/QuizArena.Player` `stores/player-game.store.ts` 10 elementos `remainingSeconds` `withComputed` `rxMethod hydrate/submitAnswer/withdraw/bindRealtime`, `features/game/` `game/question/timer/score-panel/withdrawal`, `features/shared/games.api.ts` `getMyState/submitAnswer/withdraw`, `core/realtime/game-realtime.service.ts` `withAutomaticReconnect`, `core/interceptors/` (SPEC-027+028).
- `src/OroQuizClash.Application/Features/Games/` `GetMyPlayerState` `SubmitAnswer` `WithdrawPlayer` `GetGame` `IEndpoint` `GameClaims` `X-Idempotency-Key`.
- `OroQuizClash.AppHost/AppHost.cs` `quizarena-player` container `node:22-alpine`.
- `specs/027-player-application/` + `028-player-lobby/` (previos: Available Games, Join).
