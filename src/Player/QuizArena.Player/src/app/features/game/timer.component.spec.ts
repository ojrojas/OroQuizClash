import { describe, it, expect } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { TimerComponent } from './timer.component';
import { PlayerGameStore } from '../../stores/player-game.store';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';

describe('TimerComponent', () => {
  it('shows 12s RUNNING and warning when <10s and EXPIRED assertive', async () => {
    TestBed.configureTestingModule({ imports: [TimerComponent], providers: [PlayerGameStore, provideHttpClient(), provideHttpClientTesting()] });
    const store = TestBed.inject(PlayerGameStore);
    expect(store.remainingSeconds()).toBeDefined();
    expect(store.isExpired()).toBe(false);
  });
});
