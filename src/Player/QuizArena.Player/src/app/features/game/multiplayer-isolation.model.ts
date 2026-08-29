export interface PrivateGameState {
  game: { gameId: string; name: string; status: string; configuration: any };
  gameSession: { gameSessionId: string; playerId: string; gameId: string; status: string; currentRoundNumber: number; rowVersion: string };
}

export interface PrivateAnswerState {
  answer: { answerId: string | null; selectedOptionId: string | null; state: string; isCorrect: boolean | null } | null;
}

export interface PrivateScoreState {
  score: { playerId: string; gameId: string; totalPoints: number; roundPoints?: number; correctAnswers: number; currentLevel: string };
  securedPoints: { playerId: string; gameId: string; securedPoints: number; checkpointRoundNumber: number | null; policy: string };
}

export interface PrivateTimer {
  timer: { timeLimitSeconds: number; expiresAt: string; remainingSeconds: number; state: string; serverNow: string };
}

export interface PrivateSession {
  gameSession: { gameSessionId: string; playerId: string; gameId: string; status: string; currentRoundNumber: number; rowVersion: string };
}

export function isPrivateForSub(payload: any, sub: string): boolean {
  if (!payload) return false;
  const playerId = payload.gameSession?.playerId ?? payload.player?.playerId ?? payload.score?.playerId ?? payload.answer?.playerId;
  return playerId === sub;
}

export function assertNoLeak(privatePayload: any, otherSub: string): boolean {
  const json = JSON.stringify(privatePayload);
  // Should not contain otherSub's playerId or answer details
  return !json.includes(otherSub);
}
