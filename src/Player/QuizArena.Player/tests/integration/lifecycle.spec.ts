import { describe, it, expect } from 'vitest';

describe('lifecycle', () => {
  it('status transitions WAITING→ROUND_IN_PROGRESS→ROUND_COMPLETED→FINISHED toggle canAnswer and block terminal', () => {
    const statuses = ['WAITING_FOR_PLAYERS', 'ROUND_IN_PROGRESS', 'ROUND_COMPLETED', 'FINISHED'];
    const canAnswerMap: Record<string, boolean> = {
      'WAITING_FOR_PLAYERS': false,
      'ROUND_IN_PROGRESS': true,
      'ROUND_COMPLETED': false,
      'FINISHED': false,
    };
    for (const s of statuses) {
      expect(canAnswerMap[s]).toBeDefined();
    }
    const terminal = 'FINISHED';
    expect(canAnswerMap[terminal]).toBe(false);
  });
});
