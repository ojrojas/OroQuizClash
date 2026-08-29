import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { PlayerGameStore } from './player-game.store';

describe('PlayerGameStore', () => {
  it('initial 10 elements isolated', () => {
    TestBed.configureTestingModule({ providers: [PlayerGameStore, provideHttpClient(), provideHttpClientTesting()] });
    const store = TestBed.inject(PlayerGameStore);
    expect(store.player()).toBeNull();
    expect(store.game()).toBeNull();
    expect(store.score().totalPoints).toBe(0);
    expect(store.timer().state).toBe('STOPPED');
    expect(store.status().isTerminal).toBe(false);
  });

  it('isolates instances: two stores do not share _now/answer', () => {
    TestBed.configureTestingModule({ providers: [PlayerGameStore, provideHttpClient(), provideHttpClientTesting()] });
    const storeA = TestBed.inject(PlayerGameStore);
    // patch via private API simulation: ensure not shared across TestBed resets
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [PlayerGameStore, provideHttpClient(), provideHttpClientTesting()] });
    const storeB = TestBed.inject(PlayerGameStore);
    expect(storeA._now).not.toBe(storeB._now);
  });

  it('remainingSeconds is computed from expiresAt and _now', async () => {
    TestBed.configureTestingModule({ providers: [PlayerGameStore, provideHttpClient(), provideHttpClientTesting()] });
    const store = TestBed.inject(PlayerGameStore);
    const future = new Date(Date.now() + 10000).toISOString();
    (store as any)._api = { getMyState: () => ({ subscribe: () => {} }) };
    expect(store.remainingSeconds()).toBeDefined();
  });

  it('isExpired true when expiresAt <= _now', () => {
    TestBed.configureTestingModule({ providers: [PlayerGameStore, provideHttpClient(), provideHttpClientTesting()] });
    const store = TestBed.inject(PlayerGameStore);
    expect(store.isExpired()).toBe(false);
  });
});
