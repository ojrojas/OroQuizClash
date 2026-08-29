import { Component, computed, inject, Input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PlayerGameStore } from '../../stores/player-game.store';
import { AnswerInteractionStore } from '../../stores/answer-interaction.store';
import { Question, Answer } from '../shared/models/player.models';
import { ErrorStateComponent } from '../../shared/ui/error-state.component';
import { AnswerOptionState } from './answer-interaction.model';

@Component({
  selector: 'app-question',
  standalone: true,
  imports: [CommonModule, ErrorStateComponent],
  providers: [AnswerInteractionStore],
  template: `
    <div class="question-answering" data-theme="player">
      @if (validationError()) {
        <app-error-state [message]="validationError()!" [correlationId]="answerStore.correlationId() ?? ''" (retry)="onRetry()"></app-error-state>
      }
      @if (!questionView()) {
        @if (!validationError()) {
          <p role="status" aria-live="polite">Esperando pregunta...</p>
        }
      } @else {
        <h2 class="question-text" aria-live="polite">{{ questionView()!.text }}</h2>
        <div class="options-grid"
             role="radiogroup"
             aria-label="Opciones de respuesta"
             [attr.aria-busy]="answerStore.isEvaluating() ? 'true' : null">
          @for (opt of orderedOptions(); track opt.optionId; let i = $index) {
            @let optState = optionState(opt.optionId);
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
                    [attr.aria-disabled]="isTerminalState(optState) ? 'true' : null"
                    [attr.aria-busy]="optState==='Evaluating' ? 'true' : null"
                    [disabled]="isDisabled(optState)"
                    (click)="onSelect(opt.optionId)"
                    (mouseenter)="hovered.set(opt.optionId)"
                    (mouseleave)="hovered.set(null)"
                    (keydown.space)="onSelect(opt.optionId); $event.preventDefault()"
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

        @if (answerStore.isEvaluating()) {
          <div class="evaluating" role="status" aria-live="polite" aria-busy="true">Evaluando…</div>
        }
        @if (answerStore.phase()==='correct') {
          <div class="result correct" role="status" aria-live="assertive" aria-atomic="true">¡Correcto! +{{ answerStore.scoreDelta() ?? '' }} pts</div>
        }
        @if (answerStore.phase()==='incorrect') {
          <div class="result incorrect" role="status" aria-live="assertive">Incorrecto — la correcta era {{ correctOptionText() }}</div>
        }
        @if (answerStore.phase()==='timeout') {
          <div class="result timeout" role="status" aria-live="assertive">Tiempo agotado</div>
        }
        @if (answerStore.errorDetail() && !validationError()) {
          <app-error-state [message]="answerStore.errorDetail()!" [correlationId]="answerStore.correlationId() ?? ''" (retry)="onRetry()"></app-error-state>
        }

        <button type="button"
                class="confirm-btn"
                [disabled]="!answerStore.selectedOptionId() || answerStore.isLocked() || answerStore.isEvaluating() || !canAnswerComputed()"
                (click)="onConfirm()"
                aria-label="Confirmar respuesta">
          Confirmar
        </button>
        @if (showValidation() && !answerStore.selectedOptionId()) {
          <span role="alert" aria-live="assertive">Selecciona una opción</span>
        }
      }
    </div>
  `,
  styles: [`
.question-answering { display:flex; flex-direction:column; gap:var(--space-4,16px); }
.question-text { font-size:var(--typography-title-m-size,20px); font-weight:700; line-height:1.4; color:var(--color-foreground,#0F172A); }
.options-grid { display:grid; grid-template-columns:1fr; gap:var(--space-3,12px); }
@media (min-width:768px){ .options-grid { grid-template-columns:1fr 1fr; } }
.answer-option { display:flex; align-items:center; gap:var(--space-2,8px); padding:var(--space-3,12px) var(--space-4,16px); min-height:44px; min-width:44px; border-radius:var(--radius-md,8px); border:1px solid var(--color-border,#DBEAFE); background:var(--color-surface,#FFF); color:var(--color-foreground,#1E3A8A); transition:all 200ms var(--motion-ease-out,ease); text-align:left; width:100%; cursor:pointer; font-size:var(--typography-body-m-size,16px); }
.answer-option:hover,.answer-option.hover { border-color:var(--color-primary,#2563EB); box-shadow:var(--shadow-md,0 4px 8px rgba(15,23,42,0.12)); transform:scale(1.01); }
.answer-option:focus-visible { outline:2px solid var(--color-primary,#2563EB); outline-offset:2px; }
.answer-option.selected { background:var(--color-primary-subtle,rgba(37,99,235,0.08)); border-color:var(--color-primary,#2563EB); box-shadow:var(--shadow-sm,0 1px 2px rgba(15,23,42,0.08)); }
.answer-option.locked { opacity:0.9; cursor:not-allowed; }
.answer-option.evaluating { background:var(--color-primary-subtle,rgba(37,99,235,0.08)); animation:pulse 600ms ease infinite; }
.answer-option.correct { background:var(--color-success,#16A34A); color:var(--color-success-contrast,#FFF); border-color:var(--color-success,#16A34A); }
.answer-option.incorrect { background:var(--color-destructive,#DC2626); color:var(--color-on-destructive,#FFF); border-color:var(--color-destructive,#DC2626); }
.answer-option.timeout { background:var(--color-warning,#D97706); color:var(--color-on-primary,#FFF); border-color:var(--color-warning,#D97706); }
@media (prefers-reduced-motion: reduce){ .answer-option,.answer-option:hover,.answer-option.hover { transition:none; transform:none; animation:none; } }
.confirm-btn { margin-top:var(--space-3,12px); padding:var(--space-3,12px) var(--space-4,16px); border-radius:var(--radius-md,8px); background:var(--color-primary,#2563EB); color:var(--color-on-primary,#FFF); border:none; min-height:44px; min-width:44px; cursor:pointer; font-weight:600; }
.confirm-btn:disabled { opacity:0.5; cursor:not-allowed; }
.spinner { width:16px; height:16px; border:2px solid var(--color-border,#DBEAFE); border-top-color:var(--color-primary,#2563EB); border-radius:50%; animation:spin 600ms linear infinite; display:inline-block; }
@media (prefers-reduced-motion: reduce){ .spinner { animation:none; } }
.evaluating { font-size:var(--typography-label-m-size,14px); color:var(--color-muted-foreground,#475569); }
.result.correct { background:var(--color-success,#16A34A); color:var(--color-on-primary,#FFF); padding:var(--space-3,12px); border-radius:var(--radius-md,8px); }
.result.incorrect { background:var(--color-destructive,#DC2626); color:var(--color-on-destructive,#FFF); padding:var(--space-3,12px); border-radius:var(--radius-md,8px); }
.result.timeout { background:var(--color-warning,#D97706); color:var(--color-on-primary,#FFF); padding:var(--space-3,12px); border-radius:var(--radius-md,8px); }
@keyframes spin { to { transform:rotate(360deg); } }
@keyframes pulse { 0%,100%{opacity:1;}50%{opacity:0.8;} }
.option-label { flex:1; }
`]
})
export class QuestionComponent {
  // Allow external question input for tests; fallback to PlayerGameStore
  @Input() question: Question | null = null;
  // Keep compatibility with old store injection but prefer answerStore
  playerStore = inject(PlayerGameStore, { optional: true });
  answerStore = inject(AnswerInteractionStore);

  hovered = signal<string | null>(null);
  showValidation = signal(false);

  // Expose outputs for compatibility
  select = output<string>();
  confirm = output<void>();

  questionView = computed<Question | null>(() => {
    const external = this.question;
    if (external) return external;
    return this.playerStore?.question() ?? null;
  });

  orderedOptions = computed(() => {
    const q = this.questionView();
    if (!q) return [];
    const opts = [...(q.answerOptions ?? [])];
    opts.sort((a: any, b: any) => (a.displayOrder ?? 0) - (b.displayOrder ?? 0));
    return opts;
  });

  validationError = computed(() => {
    const q = this.questionView();
    if (!q) return null;
    const len = q.answerOptions?.length ?? 0;
    if (len !== 4) return 'Pregunta inválida (se requieren 4 opciones)';
    // also check answerStore validation error for 3/5
    if (this.answerStore.errorDetail() === 'Pregunta inválida (se requieren 4 opciones)') return this.answerStore.errorDetail();
    return null;
  });

  canAnswerComputed = computed(() => {
    // In test mode where question is provided directly, bypass PlayerGameStore canAnswer (which requires round status)
    if (this.question) return this.answerStore.canSelect();
    if (this.playerStore) return this.playerStore.canAnswer();
    return this.answerStore.canSelect();
  });

  correctOptionText = computed(() => {
    const correctId = this.answerStore.correctOptionId();
    if (!correctId) {
      // try to find from question if we have isCorrect leaked post-evaluated (should only happen after EVALUATED)
      const q: any = this.questionView();
      if (q) {
        const opt = q.answerOptions?.find((o: any) => o.isCorrect);
        if (opt) return opt.text;
      }
      return '';
    }
    const opt = this.orderedOptions().find(o => o.optionId === correctId);
    return opt?.text ?? '';
  });

  optionState(optionId: string): AnswerOptionState {
    if (this.validationError()) return 'Idle';
    const phase = this.answerStore.phase();
    const selected = this.answerStore.selectedOptionId();
    const locked = this.answerStore.lockedOptionId();
    const isEvaluating = this.answerStore.isEvaluating();
    const correctId = this.answerStore.correctOptionId();

    if (phase === 'timeout') return 'Timeout';
    if (phase === 'evaluating' && locked === optionId) return 'Evaluating';
    if (phase === 'correct') {
      if (locked === optionId) return 'Correct';
      if (correctId === optionId) return 'Correct';
      // secondary correct highlight: if question has isCorrect flag post-evaluated
      const q: any = this.questionView();
      const isCorrectOpt = q?.answerOptions?.find((o: any) => o.optionId === optionId && o.isCorrect);
      if (isCorrectOpt) return 'Correct';
      return 'Idle';
    }
    if (phase === 'incorrect') {
      if (locked === optionId) return 'Incorrect';
      if (correctId === optionId) return 'Correct';
      const q: any = this.questionView();
      const isCorrectOpt = q?.answerOptions?.find((o: any) => o.optionId === optionId && o.isCorrect);
      if (isCorrectOpt) return 'Correct';
      return 'Idle';
    }
    if (phase === 'locked' && locked === optionId) return 'Locked';
    if (phase === 'selected' && selected === optionId) return 'Selected';
    if (phase === 'idle' && selected === optionId) return 'Selected';
    // hover handling: if idle and hovered
    if (phase === 'idle' && this.hovered() === optionId && selected !== optionId) return 'Hover';
    return 'Idle';
  }

  isTerminalState(state: AnswerOptionState): boolean {
    return ['Locked', 'Evaluating', 'Correct', 'Incorrect', 'Timeout'].includes(state);
  }

  isDisabled(state: AnswerOptionState): boolean {
    if (this.validationError()) return true;
    if (['Locked', 'Evaluating', 'Correct', 'Incorrect', 'Timeout'].includes(state)) return true;
    if (this.answerStore.isEvaluating()) return true;
    if (this.answerStore.isLocked()) return true;
    if (!this.canAnswerComputed()) return true;
    // evaluating or locked terminal disables all
    if (this.answerStore.phase() === 'evaluating' || this.answerStore.phase() === 'correct' || this.answerStore.phase() === 'incorrect' || this.answerStore.phase() === 'timeout') return true;
    return false;
  }

  onSelect(optionId: string) {
    if (this.validationError()) return;
    if (this.answerStore.isLocked() || this.answerStore.isEvaluating()) return;
    if (!this.canAnswerComputed()) return;
    const phase = this.answerStore.phase();
    if (phase === 'correct' || phase === 'incorrect' || phase === 'timeout' || phase === 'evaluating' || phase === 'locked') return;
    this.showValidation.set(false);
    this.answerStore.selectOption(optionId);
    this.select.emit(optionId);
  }

  onConfirm() {
    if (!this.answerStore.selectedOptionId()) {
      this.showValidation.set(true);
      return;
    }
    if (this.answerStore.isLocked() || this.answerStore.isEvaluating()) return;
    this.answerStore.confirmLock();
    // after lock, submit
    const gameId = this.playerStore?.game()?.gameId ?? this.answerStore.gameId();
    if (gameId) {
      // ensure store has gameId/roundId/questionId hydrated; sync from playerStore if needed
      const roundId = this.playerStore?.round()?.roundId ?? this.answerStore.roundId();
      const questionId = (this.question as any)?.questionId ?? this.playerStore?.question()?.questionId ?? this.answerStore.questionId();
      if (roundId && questionId) {
        // patch if missing
        if (!this.answerStore.gameId()) this.answerStore._setState({ gameId, roundId, questionId } as any);
      }
      this.answerStore.submitAnswer();
    } else {
      this.answerStore.submitAnswer();
    }
    this.confirm.emit();
  }

  onRetry() {
    this.answerStore.clearError();
    this.answerStore.submitAnswer();
  }
}
