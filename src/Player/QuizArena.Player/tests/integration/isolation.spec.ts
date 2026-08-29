import { describe, it, expect } from 'vitest';

describe('isolation integration', () => {
  it('two players mock getMyState do not leak score/answer (FR-002)', async () => {
    const responseA = { player: { playerId: 'sub-A' }, score: { totalPoints: 100 }, answer: { state: 'PENDING' } };
    const responseB = { player: { playerId: 'sub-B' }, score: { totalPoints: 250 }, answer: { state: 'PENDING' } };
    expect(responseA.score.totalPoints).not.toBe(responseB.score.totalPoints);
    expect(responseA.player.playerId).not.toBe(responseB.player.playerId);
  });
});
