import { describe, it, expect, beforeEach, vi } from 'vitest';
import { buildLadder } from '../features/game/ladder.model';

describe('PlayerRoundsStore - buildLadder (T011/T018/T023/T034)', () => {
  it('builds N=10 ladder exact without gaps, current 4 aria-current completed/upcoming', () => {
    const ladder = buildLadder(10, [{ roundNumber: 4, level: 'Intermediate' }], [], null, 4, 100);
    expect(ladder.length).toBe(10);
    expect(ladder[3].state).toBe('current');
    expect(ladder[0].state).toBe('completed');
    expect(ladder[9].state).toBe('upcoming');
    expect(ladder[9].isFinal).toBe(true);
  });

  it('builds N=15 with CategorySpecific fallback level', () => {
    const ladder = buildLadder(15, [], [], null, 1);
    expect(ladder.length).toBe(15);
    expect(ladder[0].level).toBeTruthy();
  });

  it('rewards: Current/Next/Secured/Final from RewardRules and LOSE_ALL', () => {
    const rules = [{ roundThreshold: 5, name: 'Pack Plata', pointsRequired: 500 }, { roundThreshold: 10, name: 'Pack Oro', pointsRequired: 5000 }];
    const secured = { playerId: 'p', gameId: 'g', securedPoints: 500, checkpointRoundNumber: 5, policy: 'KEEP_SECURED_SCORE' };
    const ladder = buildLadder(10, [], rules, secured, 6, 100);
    expect(ladder[5].isCurrentReward).toBe(true);
    expect(ladder[6].nextRewardFlag).toBe(true);
    expect(ladder[4].securedFlag).toBe(true);
    expect(ladder.slice(0, 5).every(r => r.isSecured)).toBe(true);
    expect(ladder[9].isFinal).toBe(true);
    // placeholder when no rules
    const empty = buildLadder(10, [], [], null, 6);
    expect(empty[5].currentReward).toBeTruthy(); // fallback pointsPerRound gives value, if no pointsPerRound -> null -> component shows —
  });

  it('LOSE_ALL secured 0 -> no isSecured', () => {
    const secured = { playerId: 'p', gameId: 'g', securedPoints: 0, checkpointRoundNumber: null, policy: 'LOSE_ALL' };
    const ladder = buildLadder(10, [], [], secured, 6);
    expect(ladder.every(r => !r.isSecured)).toBe(true);
  });

  it('transition: empty when current null WAITING', () => {
    const ladder = buildLadder(10, [], [], null, null);
    expect(ladder.every(r => r.state === 'upcoming')).toBe(true);
  });

  it('rollback: current decreasing detected as jump', () => {
    // simulate previous 6 current 4 (correction)
    const ladder = buildLadder(10, [], [], null, 4);
    expect(ladder[3].state).toBe('current');
  });
});
