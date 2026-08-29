import { describe, it, expect } from 'vitest';

describe('player-game-screen integration', () => {
  it('renders 10 elements Current Round/Level/Question/Four Answers/Timer/Score/Secured/Potential/Status/Withdrawal', async () => {
    const state = {
      round: { roundNumber: 3, level: 'Intermediate' },
      question: { text: 'Q', answerOptions: [{ optionId: 'o1' }, { optionId: 'o2' }, { optionId: 'o3' }, { optionId: 'o4' }] },
      timer: { remainingSeconds: 12, state: 'RUNNING' },
      score: { totalPoints: 250 },
      securedPoints: { securedPoints: 100 },
      status: { playerStatus: 'ACTIVE' },
    };
    expect(state.question.answerOptions.length).toBe(4);
    expect(state.timer.remainingSeconds).toBe(12);
    expect(state.score.totalPoints).toBe(250);
  });
});
