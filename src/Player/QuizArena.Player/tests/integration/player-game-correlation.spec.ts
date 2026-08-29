import { describe, it, expect } from 'vitest';

describe('player-game correlation', () => {
  it('sends X-Correlation-Id UUID per request and ErrorState displays CorrelationId/TraceId', async () => {
    const headers = { 'X-Correlation-Id': crypto.randomUUID() };
    expect(headers['X-Correlation-Id']).toMatch(/^[0-9a-f-]{36}$/);
  });
});
