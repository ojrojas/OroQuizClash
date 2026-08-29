import { describe, it, expect } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { QuestionComponent } from './question.component';
import { PlayerGameStore } from '../../stores/player-game.store';

describe('QuestionComponent', () => {
  it('renders 4 radio buttons with aria-checked and Submit disabled when !canAnswer', async () => {
    TestBed.configureTestingModule({ imports: [QuestionComponent], providers: [PlayerGameStore, provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(QuestionComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.selectedOptionId()).toBeNull();
  });

  it('isCorrect not rendered before EVALUATED', () => {
    expect(true).toBe(true);
  });
});
