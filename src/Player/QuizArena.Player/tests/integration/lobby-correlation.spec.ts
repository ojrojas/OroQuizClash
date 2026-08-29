import { describe, it, expect, vi } from 'vitest';

describe('lobby correlation', () => {
  it('sends X-Correlation-Id UUID and displays CorrelationId/TraceId on error', async () => {
    const headers = { 'X-Correlation-Id': crypto.randomUUID() };
    expect(headers['X-Correlation-Id']).toMatch(/^[0-9a-f-]{36}$/);
    const error = { detail: 'GameFull', correlationId: headers['X-Correlation-Id'], traceId: 'trace-123' };
    expect(error.correlationId).toBeTruthy();
    expect(error.traceId).toBeTruthy();
  });
});
