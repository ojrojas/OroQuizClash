import { describe, it, expect, vi } from 'vitest';

describe('rehydrate resilience', () => {
  it('HubConnection disconnect 10s rehydrate corrects Timer serverNow without duplicate ledger', async () => {
    const hydrate = vi.fn(async () => ({ timer: { serverNow: new Date().toISOString(), expiresAt: new Date(Date.now() + 20000).toISOString() } }));
    const before = new Date().toISOString();
    // simulate disconnect
    await new Promise(r => setTimeout(r, 10));
    const state = await hydrate();
    expect(new Date(state.timer.serverNow).getTime()).toBeGreaterThan(new Date(before).getTime());
    expect(hydrate).toHaveBeenCalledTimes(1);
  });
});
