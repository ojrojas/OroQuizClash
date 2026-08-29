export type ResultState = 'won' | 'walked' | 'over' | 'finished' | 'playing';

export interface ResultDisplay {
  state: ResultState;
  finalScore: number;
  finalPosition: number | null;
  totalPlayers: number;
  prize: { name: string; pointsRequired: number } | null;
  consolation: { name: string } | null;
  availableRewards: Array<{ rewardId: string; name: string; pointsRequired: number }>;
  isTerminal: boolean;
}

export function mapResultState(playerStatus: string, gameStatus: string, rank: number | null): ResultState {
  if (playerStatus === 'WINNER' && gameStatus === 'FINISHED' && rank === 1) return 'won';
  if (playerStatus === 'WITHDRAWN') return 'walked';
  if (playerStatus === 'ELIMINATED') return 'over';
  if (gameStatus === 'FINISHED' && playerStatus === 'FINISHED' && rank != null && rank >= 2) return 'finished';
  if (gameStatus === 'FINISHED' && rank != null && rank >= 2) return 'finished';
  if (playerStatus === 'FINISHED' && gameStatus === 'FINISHED') return 'finished';
  return 'playing';
}
