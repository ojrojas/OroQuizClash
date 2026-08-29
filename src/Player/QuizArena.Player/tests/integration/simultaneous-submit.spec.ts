import { describe, it, expect, vi } from 'vitest';

describe('simultaneous submit', () => {
  it('N simultaneous submitAnswer with distinct idempotencyKeys each 200 EVALUATED', async () => {
    const api = { submitAnswer: vi.fn((_, dto) => Promise.resolve({ state: 'EVALUATED', isCorrect: true, idempotencyKey: dto.idempotencyKey })) };
    const keys = Array.from({ length: 5 }, () => crypto.randomUUID());
    const results = await Promise.all(keys.map(k => api.submitAnswer('g1', { roundId: 'r1', questionId: 'q1', selectedOptionId: 'o-1', idempotencyKey: k })));
    expect(results.every(r => r.state === 'EVALUATED')).toBe(true);
    expect(new Set(results.map(r => r.idempotencyKey)).size).toBe(5);
  });
});
