# Research: Player Answering (031)

**Branch**: `031-player-answering` | **Date**: 2026-08-29 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Summary

0 `NEEDS CLARIFICATION` en spec; investigación consolida 5 decisiones para selector de exactamente 4 opciones con 8 estados visuales (`Idle→Hover→Selected→Locked→Evaluating→Correct/Incorrect/Timeout`), selección única con `Locked` inmutable (debounce 150ms), y veredicto autoritativo backend (`POST /answers` `X-Idempotency-Key` per `roundId` `sessionStorage`, `isCorrect` solo tras `EVALUATED`, `Timeout` `submittedAt<=expiresAt`). Todo alineado a constitución v1.1.0 + BuildingBlocks net10.0 y patrones de SPEC-027 (Player App) / 029 (Game) / 030 (Rounds) + SPEC-016 Design System `data-theme="player"` y SPEC-003/006 (4/1 invariante, `SubmitAnswer` idempotente).

## Decisions

### 1. Selector 4 opciones con 8 estados y `AnswerInteractionStore` dedicado

- **Decision**: Crear `stores/answer-interaction.store.ts` `signalStore` con `state: { selectedOptionId: string|null, lockedOptionId: string|null, phase: 'idle'|'selected'|'locked'|'evaluating'|'correct'|'incorrect'|'timeout', isEvaluating: boolean, canSelect: boolean, errorDetail?: string, correlationId?: string }` + `computed`: `canSelect = canAnswer && !isLocked && !isEvaluating && !isTerminal` (derivado de `PlayerGameStore.status.canAnswer`), `isLocked = lockedOptionId != null`, `isSelected = selectedOptionId != null`, `displayPhase` para componente. `withMethods`: `selectOption(optionId)` (mueve `Selected` único, debounce 150ms, ignora si `isLocked` o `isEvaluating`), `confirmLock()` → `patchState({lockedOptionId: selectedOptionId, phase:'locked'})` (valida `selected!=null` sino muestra validación local sin llamada), `submitAnswer(gameId, roundId, questionId)` `rxMethod` → `sessionStorage idemp-{roundId} ?? crypto.randomUUID()` → `GamesApi.submitAnswer(gameId,{roundId,questionId,selectedOptionId: lockedOptionId, idempotencyKey})` `switchMap` `tapResponse` → `phase:'evaluating'` luego tras `200 EVALUATED` → `phase: isCorrect?'correct':'incorrect'`; error `400 AnswerWindowExpired` → `phase:'timeout'`; error `500` → `ErrorState` con `correlationId` + `Retry` reusa misma key; error `409 QuestionAlreadyAnswered` → satura a `Locked/Evaluating` según `Answer.state` sin nuevo ledger. `hydrateAnswer()` via `GamesApi.getMyState` `answer.selectedOptionId` + `state` restaura `Locked/Evaluating/Correct/Incorrect/Timeout` tras recarga. `QuestionComponent` 4 botones `*ngFor answerOptions` con `role="radio"` `aria-checked="selectedOptionId===opt.optionId"` `aria-posinset` `aria-setsize="4"` `aria-disabled="isLocked||isEvaluating||phase terminal"`.

- **Rationale**: FR-002..FR-006 (8 estados, single selection, Locked inmutable, `X-Idempotency-Key` idempotencia, backend authoritative `isCorrect` solo tras `EVALUATED`), Constitución V (Server Truth) + F (Idempotency) + B (4/1).

- **Alternatives**: Extender `PlayerGameStore` con `selectedOptionId` field directo (rechazado — mezcla `AnswerInteractionState` con `Score/Timer/Rounds` 10 elementos, viola SRP y testeabilidad; 031 debe ser testeable aislado per `Round`); hardcodear `Locked` auto al seleccionar sin Confirmar (rechazado — evita errores táctiles, UX requiere confirmación explícita 44px); sin debounce 150ms (rechazado — doble clic rápido crea race `Selected` before `Locked`).

- **Accessibility**: `role="radiogroup"` `aria-label="Opciones de respuesta"` + `role="radio"` `aria-checked` `aria-posinset 1..4` `aria-setsize 4` `aria-disabled` en `Locked/Evaluating/Correct/Incorrect/Timeout`, `Tab`/`Shift+Tab` recorre opciones, `Space/Enter` selecciona, foco `outline:2px solid var(--color-primary)`, `aria-live="polite"` `Evaluating` spinner "Evaluando…" + `aria-live="assertive"` `Correct/Incorrect/Timeout`.

### 2. Veredicto backend autoritativo `Evaluating` → `Correct/Incorrect/Timeout` con `submittedAt<=expiresAt`

- **Decision**: `Evaluating` permanece hasta respuesta `POST /answers` 200 con `Answer.state===EVALUATED` (o `EXPIRED`) y `isCorrect` (o `null` si `Timeout`). No se calcula cliente. `Timer` visual `remainingSeconds` con `serverNow` corrección (029) pero decisión `Timeout` solo server: si cliente envía fuera de `TimeLimit`, server responde `400 AnswerWindowExpired` → UI `Timeout` (`var(--color-warning)` "Tiempo agotado" `aria-live="assertive"`). Tras `Correct` `Incorrect` se muestra además la opción correcta autoritativa como `Correct` secondary si `Locked Incorrect` (resaltando `Question` post-EVALUATED con `isCorrect` ya expuesto para `PLAYER`). Reintento misma `X-Idempotency-Key` → mismo `Answer` sin duplicar `PointTransaction` ledger `COUNT` (verificado `UNIQUE IdempotencyKey`).

- **Rationale**: FR-006..FR-008 + SC-005/007 + Constitución V (Server Truth) + F (Idempotency) + SPEC-006 `SubmitAnswer` idempotente. Patrón ya en 029 `submitAnswer`.

- **Alternatives**: Calcular `Correct` cliente-side comparando `optionId` (rechazado — viola V, cliente no confiable, `isCorrect` filtrado); confiar en payload `ScoreUpdated`/`QuestionAvailable` evento para `isCorrect` sin `POST /answers` (rechazado — evento nunca fuente veredicto).

### 3. `isCorrect` filtrado server-side para `PLAYER` antes de `EVALUATED` + Exactly 4 invariante

- **Decision**: Backend `GET /api/games/{id}/questions/current` y `GET /players/me` `question.answerOptions` filtran `isCorrect` para rol `PLAYER` hasta `Answer.state===EVALUATED` (SPEC-006/014). Cliente nunca ve `isCorrect` en DevTools pre-EVALUATED. Selector valida exactamente 4 opciones (`answerOptions.length===4`); si 3/5 muestra `ErrorState` "Pregunta inválida (se requieren 4 opciones)" con `CorrelationId` y bloquea `canSelect=false` (invariante B `CHECK exactly one correct` + publish ≥5 por categoría). Opción con `text` vacío renderiza placeholder "Opción sin texto" con `aria-label` fallback, no rompe grid 2x2.

- **Rationale**: FR-001 + FR-013 + Constitución B (Question 4/1) + H (no leak). DB constraint `CHECK exactly one correct` + `UNIQUE (GameId,RoundId,PlayerId)`.

- **Alternatives**: Exponer `isCorrect` siempre y ocultarlo con CSS (rechazado — leak via DevTools); permitir <4 opciones con grid adaptativo (rechazado — viola invariante B).

### 4. Responsive 1 col 375 / 2x2 ≥768 con Design System `data-theme="player"` tokens y `prefers-reduced-motion`

- **Decision**: `QuestionComponent` template `div class="options-grid"` con `display:grid; grid-template-columns:1fr; gap:var(--space-3);` `@media (min-width:768px) {grid-template-columns:1fr 1fr;}` 4 cards `min-height:44px` `min-width:44px` `padding:var(--space-3) var(--space-4)` `border:1px solid var(--color-border)` `border-radius:var(--radius-md)` . Estados con tokens: `Idle` `background var(--color-surface)`, `Hover` `border-color var(--color-primary)` `box-shadow var(--shadow-hover)` `transform scale(1.01)`, `Selected` `background var(--color-primary-subtle)` `border-color var(--color-primary)` `check var(--color-primary)`, `Locked` `opacity 0.9` `aria-disabled`, `Evaluating` `pulse var(--color-primary)` `spinner var(--color-primary)`, `Correct` `background var(--color-success) color var(--color-success-contrast) border var(--color-success)`, `Incorrect` `background var(--color-error) color var(--color-error-contrast)`, `Timeout` `background var(--color-warning)`. `@media (prefers-reduced-motion: reduce) { * {transition:none; transform:none; animation:none;}}` + Confirmar button `min-height:44px` `data-theme="player"` sin literales.

- **Rationale**: FR-011/012 + SC-008/009 + SPEC-016 `data-theme="player"` cinematic premium + WCAG AA. 1 col móvil sin scroll horizontal, 2x2 desktop aprovecha ancho.

- **Alternatives**: Flex column única siempre (rechazado — desperdicia ancho ≥768px); Tailwind arbitrario literales (rechazado — viola Design System, no pasa axe).

### 5. `X-Correlation-Id` + `X-Idempotency-Key` + debounce y ErrorState con Retry

- **Decision**: `correlationIdInterceptor` (`X-Correlation-Id: crypto.randomUUID()` per `POST /answers` hydrate) + `authInterceptor` `secureRoutes=[apiUrl]` + `errorInterceptor` RFC7807 ya en 027/029. `submitAnswer` debounce 150ms en `selectOption` y deshabilita botón Enviar durante `Evaluating` (`isEvaluating=true`). Error 500 → `ErrorState` `detail` + `CorrelationId/TraceId` + `Retry` que reusa misma `X-Idempotency-Key` (`sessionStorage idemp-{roundId}`) con backoff 1s; error 409 `QuestionAlreadyAnswered` → satura a `Locked` según `Answer.state` sin nuevo ledger; error `AnswerWindowExpired` 400 → `Timeout` terminal `canAnswer=false`. Validación Confirmar sin selección → mensaje local "Selecciona una opción" sin llamada.

- **Rationale**: FR-005/009/014 + SC-006/010 + Constitución F/I (Idempotency, ProblemDetails). Reuso interceptores evita duplicar.

- **Alternatives**: Nuevo `sessionStorage` per pregunta sin per `RoundId` (rechazado — `RoundId` es clave idempotencia per `UNIQUE`); `localStorage` (rechazado — persistencia cross-game, violación H).

## Resolved Clarifications

| Topic | Resolution |
|-------|------------|
| Auto-lock vs Confirmar explícito | Confirmar explícito botón 44px debounce 150ms (evita errores táctiles). |
| Evaluating timeout | 5s server, 3s cliente muestra ErrorState Retry misma key. |
| Texto vacío opción | Placeholder "Opción sin texto" fallback. |
| isCorrect pre-EVALUATED | Nunca expuesto, filtrado server `PLAYER`. |
| Timeout vs Evaluating race | `Timeout` prioridad server `submittedAt<=expiresAt`. |
| Debounce double-click | 150ms primera Selected prevalece. |

## References

- `draft/constitution.md` §I–VI, §A–J, §V Server Truth `submittedAt<=expiresAt`, §B 4/1, §F Idempotency, §H `sub=PlayerId`.
- `draft/game-concept.md` §Question Invariants B (4/1), §Answer Evaluation, §Scoring D.
- `draft/oroidentityserver-specification.md` OIDC PKCE `X-Correlation-Id`.
- `design-system/MASTER.md` + `overrides/player.md` + `tokens/design-tokens.css` Cinematic `data-theme="player"` WCAG 375-1536.
- `src/Player/QuizArena.Player` `stores/player-game.store.ts` + `player-rounds.store.ts` + `features/game/question.component.ts` `answer-option.component.ts` `features/shared/games.api.ts` `submitAnswer` `core/realtime/game-realtime.service.ts` `withAutomaticReconnect` `core/interceptors/` (SPEC-027/029/030).
- `src/OroQuizClash.Application/Features/Games/` `SubmitAnswer` `GetMyPlayerState`/`GetCurrentQuestion` `IEndpoint` `GameClaims` `X-Idempotency-Key` `AnswerWindowExpired`.
- `OroQuizClash.AppHost/AppHost.cs` `quizarena-player` container `node:22-alpine`.
- `specs/027-player-application/` + `029-player-game/` + `030-player-rounds/` (previos).
