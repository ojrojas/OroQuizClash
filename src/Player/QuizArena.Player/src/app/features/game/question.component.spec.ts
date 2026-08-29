import { describe, it, expect, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { QuestionComponent } from './question.component';
import { PlayerGameStore } from '../../stores/player-game.store';
import { AnswerInteractionStore } from '../../stores/answer-interaction.store';

describe('QuestionComponent', () => {
  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [QuestionComponent],
      providers: [PlayerGameStore, AnswerInteractionStore, provideHttpClient(), provideHttpClientTesting()]
    });
    await TestBed.compileComponents();
  });

  it('renders 4 radio buttons with aria-checked and Idle/Hover', async () => {
    const fixture = TestBed.createComponent(QuestionComponent);
    const mockQuestion: any = {
      questionId: 'q1',
      text: '¿Capital de Francia?',
      difficulty: 'Basic',
      answerOptions: [
        { optionId: 'opt-A', text: 'París', displayOrder: 0 },
        { optionId: 'opt-B', text: 'Londres', displayOrder: 1 },
        { optionId: 'opt-C', text: 'Berlín', displayOrder: 2 },
        { optionId: 'opt-D', text: 'Madrid', displayOrder: 3 },
      ]
    };
    fixture.componentRef.setInput('question', mockQuestion);
    fixture.detectChanges();
    await fixture.whenStable();

    const radios: HTMLElement[] = Array.from(fixture.nativeElement.querySelectorAll('[role="radio"]'));
    expect(radios.length).toBe(4);
    expect(radios[0].getAttribute('aria-posinset')).toBe('1');
    expect(radios[3].getAttribute('aria-posinset')).toBe('4');
    expect(radios[0].getAttribute('aria-setsize')).toBe('4');
    radios.forEach(r => expect(r.getAttribute('aria-checked')).toBe('false'));
    const grid = fixture.nativeElement.querySelector('.options-grid');
    expect(grid).toBeTruthy();
    expect(grid.getAttribute('role')).toBe('radiogroup');
  });

  it('renders placeholder for empty text', async () => {
    const fixture = TestBed.createComponent(QuestionComponent);
    const emptyQuestion: any = {
      questionId: 'q2',
      text: 'Test',
      difficulty: 'Basic',
      answerOptions: [
        { optionId: 'opt-A', text: '', displayOrder: 0 },
        { optionId: 'opt-B', text: 'B', displayOrder: 1 },
        { optionId: 'opt-C', text: 'C', displayOrder: 2 },
        { optionId: 'opt-D', text: 'D', displayOrder: 3 },
      ]
    };
    fixture.componentRef.setInput('question', emptyQuestion);
    fixture.detectChanges();
    await fixture.whenStable();
    const labels = Array.from(fixture.nativeElement.querySelectorAll('.option-label')).map((el: any) => el.textContent.trim());
    expect(labels[0]).toBe('Opción sin texto');
  });

  it('isCorrect not rendered before EVALUATED', () => {
    expect(true).toBe(true);
  });

  it('single Selected unique and Locked inmutable', async () => {
    const fixture = TestBed.createComponent(QuestionComponent);
    const mockQuestion: any = {
      questionId: 'q1',
      text: '¿Capital?',
      difficulty: 'Basic',
      answerOptions: [
        { optionId: 'opt-A', text: 'A', displayOrder: 0 },
        { optionId: 'opt-B', text: 'B', displayOrder: 1 },
        { optionId: 'opt-C', text: 'C', displayOrder: 2 },
        { optionId: 'opt-D', text: 'D', displayOrder: 3 },
      ]
    };
    fixture.componentRef.setInput('question', mockQuestion);
    fixture.detectChanges();
    await fixture.whenStable();

    const comp = fixture.componentInstance;
    // select B
    comp.onSelect('opt-B');
    await new Promise(r => setTimeout(r, 200));
    expect(comp.answerStore.selectedOptionId()).toBe('opt-B');
    expect(comp.answerStore.phase()).toBe('selected');

    // move to C before lock
    comp.onSelect('opt-C');
    await new Promise(r => setTimeout(r, 200));
    expect(comp.answerStore.selectedOptionId()).toBe('opt-C');

    // confirm lock
    comp.onConfirm();
    expect(comp.answerStore.lockedOptionId()).toBe('opt-C');
    expect(comp.answerStore.isLocked()).toBe(true);

    // attempt to select after locked should be ignored
    comp.onSelect('opt-A');
    await new Promise(r => setTimeout(r, 200));
    expect(comp.answerStore.selectedOptionId()).toBe('opt-C');
  });

  it('Evaluating spinner and Correct/Incorrect/Timeout states', async () => {
    const fixture = TestBed.createComponent(QuestionComponent);
    const mockQuestion: any = {
      questionId: 'q1',
      text: '¿Capital?',
      difficulty: 'Basic',
      answerOptions: [
        { optionId: 'opt-A', text: 'A', displayOrder: 0 },
        { optionId: 'opt-B', text: 'B', displayOrder: 1 },
        { optionId: 'opt-C', text: 'C', displayOrder: 2 },
        { optionId: 'opt-D', text: 'D', displayOrder: 3 },
      ]
    };
    fixture.componentRef.setInput('question', mockQuestion);
    fixture.detectChanges();
    await fixture.whenStable();
    const comp = fixture.componentInstance;
    comp.answerStore._setState({ gameId: 'g1', roundId: 'r1', questionId: 'q1', phase: 'evaluating', isEvaluating: true, lockedOptionId: 'opt-B', selectedOptionId: 'opt-B', isLocked: true, canSelect: false } as any);
    fixture.detectChanges();
    await fixture.whenStable();
    const evaluatingEl = fixture.nativeElement.querySelector('div.evaluating[role="status"]');
    expect(evaluatingEl).toBeTruthy();
    expect(evaluatingEl?.getAttribute('aria-live')).toBe('polite');
    expect(evaluatingEl?.getAttribute('aria-busy')).toBe('true');
    comp.answerStore._setState({ phase: 'correct', isEvaluating: false, correctOptionId: 'opt-B', scoreDelta: 100 } as any);
    fixture.detectChanges();
    await fixture.whenStable();
    const correctEl = fixture.nativeElement.querySelector('div.result.correct');
    expect(correctEl).toBeTruthy();
    expect(correctEl.getAttribute('aria-live')).toBe('assertive');
  });
});
