import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PlayerGameStore } from '../../stores/player-game.store';

@Component({
  selector: 'app-question',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="question" aria-live="polite">
      @if (store.question(); as q) {
        <h3>{{ q.text }}</h3>
        <p>Dificultad: {{ q.difficulty }}</p>
        <div role="radiogroup" aria-label="Opciones de respuesta">
          @for (opt of q.answerOptions; track opt.optionId) {
            <button
              role="radio"
              [attr.aria-checked]="selectedOptionId() === opt.optionId"
              [attr.aria-selected]="selectedOptionId() === opt.optionId"
              (click)="selectedOptionId.set(opt.optionId)"
              [disabled]="!store.canAnswer()"
              style="min-height:44px; min-width:44px; display:block; margin:8px 0; width:100%; text-align:left; padding:12px;"
              [style.outline]="selectedOptionId() === opt.optionId ? '2px solid var(--color-primary)' : 'none'"
            >
              {{ opt.text }}
            </button>
          }
        </div>
        <button
          (click)="submit()"
          [disabled]="!store.canAnswer() || !selectedOptionId()"
          style="min-height:44px; min-width:44px;"
          aria-label="Enviar respuesta"
        >
          Enviar respuesta
        </button>
        @if (store.answer(); as a) {
          @if (a.state === 'EVALUATED') {
            <div role="status" aria-live="assertive">
              {{ a.isCorrect ? '¡Correcto!' : 'Incorrecto' }}
            </div>
          }
          @if (a.state === 'EXPIRED') {
            <div role="status" aria-live="assertive">Tiempo expirado</div>
          }
        }
      } @else {
        <p>Esperando pregunta...</p>
      }
    </div>
  `
})
export class QuestionComponent {
  store = inject(PlayerGameStore);
  selectedOptionId = signal<string | null>(null);

  submit() {
    const id = this.selectedOptionId();
    if (!id) return;
    this.store.submitAnswer(id);
  }
}
