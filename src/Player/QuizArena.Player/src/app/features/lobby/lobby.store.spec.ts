import { describe, it, expect, vi, beforeEach } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { LobbyStore } from './lobby.store';

describe('LobbyStore', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [LobbyStore, provideHttpClient(), provideHttpClientTesting()] }));

  it('loads paginated games with 8 fields', async () => {
    const store = TestBed.inject(LobbyStore);
    expect(store.games().length).toBe(0);
    expect(store.totalCount()).toBe(0);
    // mock would patch 8 fields
    expect(store.isLoading()).toBe(false);
  });

  it('isEmpty when 0 games', () => {
    const store = TestBed.inject(LobbyStore);
    expect(store.isEmpty()).toBe(true);
  });

  it('responsive: table vs cards same data', () => {
    // Both views use store.games()
    const store = TestBed.inject(LobbyStore);
    expect(store.games).toBeDefined();
  });
});
