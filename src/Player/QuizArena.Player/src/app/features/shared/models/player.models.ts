export interface Player {
  playerId: string;
  displayName: string;
  email: string;
  tenantId?: string;
  roles: string[];
  mustChangePassword: boolean;
}

export interface Game {
  gameId: string;
  name: string;
  status: string;
  categoryId: string;
  categoryName: string;
  configuration: GameConfiguration;
  maxPlayers: number;
  minPlayers: number;
}

export interface GameConfiguration {
  categoryId: string;
  minRounds: number;
  maxRounds: number;
  initialDifficulty: string;
  progressionStrategy: string;
  timeLimitPerQuestionSeconds: number;
  pointsPerRound: number;
  withdrawalPolicy: string;
  lossPolicy: string;
}

export interface GameSession {
  gameSessionId: string;
  playerId: string;
  gameId: string;
  status: string;
  joinedAt: string;
  currentRoundNumber: number;
  version: string;
}

export interface Round {
  roundId: string;
  gameId: string;
  roundNumber: number;
  level: string;
  status: string;
  questionId: string;
  startedAt: string;
  expiresAt: string;
  version: string;
}

export interface Question {
  questionId: string;
  categoryId: string;
  text: string;
  answerOptions: AnswerOption[];
  difficulty: string;
}

export interface AnswerOption {
  optionId: string;
  text: string;
}

export interface Answer {
  answerId: string | null;
  playerId: string;
  gameId: string;
  roundId: string;
  questionId: string;
  selectedOptionId: string | null;
  submittedAt: string | null;
  state: string;
  isCorrect: boolean | null;
  idempotencyKey: string;
}

export interface Score {
  playerId: string;
  gameId: string;
  totalPoints: number;
  correctAnswers: number;
  currentLevel: string;
  transactions?: PointTransaction[];
}

export interface PointTransaction {
  transactionId: string;
  type: string;
  points: number;
  roundNumber?: number;
  createdAt: string;
}

export interface SecuredPoints {
  playerId: string;
  gameId: string;
  securedPoints: number;
  checkpointRoundNumber: number | null;
  policy: string;
}

export interface Timer {
  timeLimitSeconds: number;
  expiresAt: string;
  remainingSeconds: number;
  state: 'RUNNING' | 'STOPPED' | 'EXPIRED';
  serverNow: string;
}

export interface PlayerGameStatus {
  gameStatus: string;
  playerStatus: string;
  isTerminal: boolean;
  canAnswer: boolean;
}

export interface PlayerGameState {
  player: Player;
  game: Game;
  gameSession: GameSession;
  round: Round | null;
  question: Question | null;
  answer: Answer | null;
  score: Score;
  securedPoints: SecuredPoints;
  timer: Timer;
  status: PlayerGameStatus;
}
