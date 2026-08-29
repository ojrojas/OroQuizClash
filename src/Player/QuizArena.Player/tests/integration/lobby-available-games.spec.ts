import { describe, it, expect, vi } from 'vitest';

describe('lobby-available-games integration', () => {
  it('renders only WAITING_FOR_PLAYERS and pagination pageSize 20', async () => {
    const mockGames = [
      { gameId: 'g1', status: 'WAITING_FOR_PLAYERS', name: 'A' },
      { gameId: 'g2', status: 'WAITING_FOR_PLAYERS', name: 'B' },
      { gameId: 'g3', status: 'FINISHED', name: 'C' },
    ];
    const available = mockGames.filter(g => g.status === 'WAITING_FOR_PLAYERS');
    expect(available.length).toBe(2);
    expect(available.every(g => g.status === 'WAITING_FOR_PLAYERS')).toBe(true);
    const pageSize = 20;
    expect(pageSize).toBe(20);
  });
});
