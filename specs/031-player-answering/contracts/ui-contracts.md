# UI Contracts: Player Answering (031)

**Branch**: `031-player-answering` | **Date**: 2026-08-29 | **Spec**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)

Selector de 4 opciones embebido en `GameComponent` (`/player/game/:gameId`) junto a ladder Round 1..N (030), 10 elementos ya en 029. Reutiliza `QuestionComponent` base, extiende con 8 estados.

## 1. QuestionComponent

**Selector**: `app-question`
**Inputs**:
- `question: QuestionView|null` (`questionId`, `text`, `answerOptions[4]` `{optionId, text, displayOrder}`)
- `interaction: Signal<AnswerInteractionState>` (`selectedOptionId`, `lockedOptionId`, `phase`, `isEvaluating`, `canSelect`, `errorDetail`, `correlationId`)
- optional `disabled: boolean` (`!canSelect || isLocked || isEvaluating`)
**Outputs**:
- `select: EventEmitter<string>` (`optionId`) — al clic/`Space`/`Enter` en opción
- `confirm: EventEmitter<void>` — Confirmar/Lock + Submit

**Store**: `AnswerInteractionStore` (o `PlayerGameStore` extension) scoped `providers: [AnswerInteractionStore]` en `GameComponent` o `QuestionComponent`. Debounce 150ms en `select`.

### Template (`signal` + control flow, `role="radiogroup"`)

```html
<div class="question-answering" data-theme="player">
  @if (!question()) {
    <app-error-state [message]="'Pregunta inválida (se requieren 4 opciones)'" [correlationId]="interaction().correlationId ?? ''" />
  } @else {
    <h2 class="question-text" aria-live="polite">{{ question().text }}</h2>
    <div class="options-grid"
         role="radiogroup"
         aria-label="Opciones de respuesta"
         [attr.aria-busy]="interaction().isEvaluating ? 'true' : null">
      @for (opt of question().answerOptions; track opt.optionId; let i=$index) {
        @let optState = optionState(opt.optionId); <!-- Idle|Hover|Selected|Locked|Evaluating|Correct|Incorrect|Timeout -->
        <button type="button"
                class="answer-option"
                role="radio"
                [class.idle]="optState==='Idle'"
                [class.hover]="optState==='Hover'"
                [class.selected]="optState==='Selected'"
                [class.locked]="optState==='Locked'"
                [class.evaluating]="optState==='Evaluating'"
                [class.correct]="optState==='Correct'"
                [class.incorrect]="optState==='Incorrect'"
                [class.timeout]="optState==='Timeout'"
                [attr.aria-checked]="optState==='Selected' || optState==='Locked' || optState==='Correct' ? 'true' : 'false'"
                [attr.aria-posinset]="i+1"
                [attr.aria-setsize]="4"
                [attr.aria-disabled]="optState==='Locked' || optState==='Evaluating' || optState==='Correct' || optState==='Incorrect' || optState==='Timeout' ? 'true' : null"
                [disabled]="optState==='Locked' || optState==='Evaluating' || optState==='Correct' || optState==='Incorrect' || optState==='Timeout' || !interaction().canSelect"
                (click)="onSelect(opt.optionId)"
                (keydown.space)="onSelect(opt.optionId)"
                (keydown.enter)="onSelect(opt.optionId)">
          <span class="option-label">{{ opt.text || 'Opción sin texto' }}</span>
          @if (optState==='Selected') { <span aria-hidden="true" class="check">●</span> }
          @if (optState==='Locked') { <span aria-hidden="true" class="lock">🔒</span> }
          @if (optState==='Evaluating') { <span aria-hidden="true" class="spinner" aria-label="Evaluando"></span> }
          @if (optState==='Correct') { <span aria-hidden="true" class="icon correct">✓</span> }
          @if (optState==='Incorrect') { <span aria-hidden="true" class="icon incorrect">✗</span> }
          @if (optState==='Timeout') { <span aria-hidden="true" class="icon timeout">⏱</span> }
        </button>
      }
    </div>

    @if (interaction().isEvaluating) {
      <div class="evaluating" role="status" aria-live="polite" aria-busy="true">Evaluando…</div>
    }
    @if (interaction().phase==='correct') {
      <div class="result correct" role="status" aria-live="assertive" aria-atomic="true">¡Correcto! +{{ scoreDelta() }} pts</div>
    }
    @if (interaction().phase==='incorrect') {
      <div class="result incorrect" role="status" aria-live="assertive">Incorrecto — la correcta era {{ correctOptionText() }}</div>
    }
    @if (interaction().phase==='timeout') {
      <div class="result timeout" role="status" aria-live="assertive">Tiempo agotado</div>
    }
    @if (interaction().errorDetail) {
      <app-error-state [message]="interaction().errorDetail!" [correlationId]="interaction().correlationId ?? ''" (retry)="onRetry()"></app-error-state>
    }

    <button type="button"
            class="confirm-btn"
            [disabled]="!interaction().selectedOptionId || interaction().isLocked || interaction().isEvaluating"
            (click)="onConfirm()"
            aria-label="Confirmar respuesta"
            style="min-height:44px; min-width:44px;">
      Confirmar
    </button>
    @if (!interaction().selectedOptionId && showValidation) {
      <span role="alert" aria-live="assertive">Selecciona una opción</span>
    }
  }
</div>
```

- `Selected`: `aria-checked="true"` único, `Hover` via `:hover` + `:focus-visible` en `Idle`.
- `Locked`: `aria-disabled="true"` en no-seleccionadas, `selected` botón sigue `disabled` si ya `Locked`.
- `Evaluating`: `aria-busy` + spinner, otras `aria-disabled`.
- `Correct`/`Incorrect`/`Timeout`: terminales `aria-live="assertive"` `aria-atomic`, `Incorrect` además resalta secundaria `Correct` en `isCorrect` option (no `Locked`).
- Placeholder "Opción sin texto" si `opt.text` vacío (edge case 031).
- Validación Confirmar sin selección → mensaje local sin llamada; debounce 150ms coalesce double-click.

### CSS (tokens `data-theme="player"`, responsive, `prefers-reduced-motion`)

```css
.question-answering { display:flex; flex-direction:column; gap:var(--space-4); }
.question-text { font-size:var(--font-size-lg); font-weight:var(--font-weight-bold); }
.options-grid { display:grid; grid-template-columns:1fr; gap:var(--space-3); }
@media (min-width:768px) { .options-grid { grid-template-columns:1fr 1fr; } }
.answer-option { display:flex; align-items:center; gap:var(--space-2); padding:var(--space-3) var(--space-4); min-height:44px; min-width:44px; border-radius:var(--radius-md); border:1px solid var(--color-border); background:var(--color-surface); transition:all 200ms ease; }
.answer-option:hover, .answer-option.hover { border-color:var(--color-primary); box-shadow:var(--shadow-hover); transform:scale(1.01); }
.answer-option:focus-visible { outline:2px solid var(--color-primary); outline-offset:2px; }
.answer-option.selected { background:var(--color-primary-subtle); border-color:var(--color-primary); box-shadow:var(--shadow-selected); }
.answer-option.locked { opacity:0.9; cursor:not-allowed; }
.answer-option.evaluating { background:var(--color-primary-subtle); animation: pulse 600ms ease infinite; }
.answer-option.correct { background:var(--color-success); color:var(--color-success-contrast); border-color:var(--color-success); }
.answer-option.incorrect { background:var(--color-error); color:var(--color-error-contrast); border-color:var(--color-error); }
.answer-option.timeout { background:var(--color-warning); color:var(--color-warning-contrast); border-color:var(--color-warning); }
@media (prefers-reduced-motion: reduce) { .answer-option, .answer-option:hover { transition:none; transform:none; animation:none; } }
.confirm-btn { margin-top:var(--space-3); padding:var(--space-3) var(--space-4); border-radius:var(--radius-md); background:var(--color-primary); color:var(--color-primary-contrast); }
.confirm-btn:disabled { opacity:0.5; cursor:not-allowed; }
.spinner { width:16px; height:16px; border:2px solid var(--color-border); border-top-color:var(--color-primary); border-radius:50%; animation: spin 600ms linear infinite; }
@media (prefers-reduced-motion: reduce) { .spinner { animation:none; } }
@keyframes spin { to { transform:rotate(360deg); } }
@keyframes pulse { 0%,100% { opacity:1; } 50% { opacity:0.8; } }
```

- 0 literales hardcodeados; todos `var(--*)` de `design-system/tokens/design-tokens.css`.
- 1 col 375px, 2x2 ≥768px, sin scroll horizontal, pregunta larga scrolleable interna.

### Interaction Details

- `onSelect(optionId)`: `if (isLocked || isEvaluating || !canSelect) return;` debounce 150ms → `patchState({selectedOptionId: optionId, phase:'selected'})`.
- `onConfirm()`: `if (!selected) {showValidation=true; return;}` → `patchState({lockedOptionId: selected, phase:'locked', isLocked:true})` → `submitAnswer(gameId, roundId, questionId)` `POST /answers` con `X-Idempotency-Key` `sessionStorage idemp-{roundId}`.
- `hydrateAnswer()` (llamado en `GameComponent ngOnInit` + `GameRealtimeService QuestionAvailable`/`ScoreUpdated`): lee `GET /players/me` `answer.selectedOptionId` + `state` y `timer` → restaura `locked/phase` (`PENDING`+selected → `locked`, `SUBMITTED` → `evaluating`, `EVALUATED correct/incorrect`, `EXPIRED` → `timeout`).
- Error 500 → `ErrorState` `Retry` reusa misma `X-Idempotency-Key`; 409 `QuestionAlreadyAnswered` → satura a `Locked` sin nuevo ledger.

## 2. Integration en GameComponent (029/030)

`GameComponent` `grid 280px 1fr` (030) + center `QuestionComponent` ya en 029: `game.component.ts` `providers: [PlayerGameStore, PlayerRoundsStore, AnswerInteractionStore]` (o `PlayerGameStore` ext.) `ngOnInit` → `answerStore.hydrateAnswer(gameId)` + `select` bindings.

## 3. States & A11y

- `loading` skeletons 4 cards `aria-busy`; `ErrorState` pregunta inválida `<4` opciones con `CorrelationId`; `disabled` en `Locked/Evaluating/Correct/Incorrect/Timeout`.
- `role="radiogroup"` + `aria-label`, `aria-checked` único `Selected/Locked/Correct`, `aria-posinset`/`aria-setsize` 1..4, `aria-disabled` terminales, `aria-busy` Evaluating, `aria-live` `polite` Evaluating + `assertive` Correct/Incorrect/Timeout.

## References

- SPEC-029 `contracts/ui-contracts.md` (GameComponent 10 elementos) + SPEC-030 ladder (misma pantalla)
- `design-system/MASTER.md` + `overrides/player.md` + `tokens/design-tokens.css` (data-theme="player", cinematic premium, WCAG)
- `src/Player/QuizArena.Player/features/game/question.component.ts` + `answer-option.component.ts` + `stores/answer-interaction.store.ts` + `features/shared/games.api.ts` `submitAnswer`
