export type LadderStateKind = 'loading' | 'empty' | 'ready' | 'error' | 'terminal';

export interface RewardRule {
  rewardId?: string;
  roundThreshold: number;
  name: string;
  pointsRequired: number;
}

export interface SecuredPoints {
  playerId: string;
  gameId: string;
  securedPoints: number;
  checkpointRoundNumber: number | null;
  policy: string;
}

export interface RoundLite {
  roundId?: string | null;
  roundNumber: number;
  level: string;
  difficulty?: number | null;
  status?: string | null;
}

export interface LadderRow {
  roundNumber: number;
  level: string;
  difficulty: number | null;
  state: 'completed' | 'current' | 'upcoming';
  isSecured: boolean;
  isFinal: boolean;
  currentReward: string | null;
  nextRewardFlag: boolean;
  securedFlag: boolean;
  isCurrentReward: boolean;
  ariaLabel: string;
}

export interface LadderState {
  gameId: string | null;
  maxRounds: number;
  currentRoundNumber: number | null;
  ladder: LadderRow[];
  secured: SecuredPoints | null;
  rewardRules: RewardRule[];
  status: LadderStateKind;
  correlationId?: string;
  errorDetail?: string;
  _animatingRound: number | null;
  previousRoundNumber: number | null;
}

const LEVEL_FALLBACK: string[] = ['Basic', 'Elementary', 'Intermediate', 'Advanced', 'Expert'];

function fallbackLevel(roundNumber: number): string {
  // Linear mapping 1..N to 5 levels: 1-2 Basic, 3-4 Elementary etc. For N !=10, cyclic
  const idx = Math.min(4, Math.floor(((roundNumber - 1) / Math.max(1, 10)) * 5));
  // simpler cyclic for arbitrary N
  return LEVEL_FALLBACK[(roundNumber - 1) % LEVEL_FALLBACK.length] ?? 'Basic';
}

function difficultyFromLevel(level: string): number | null {
  const i = LEVEL_FALLBACK.indexOf(level);
  return i >= 0 ? i + 1 : null;
}

export function buildLadder(
  maxRounds: number,
  rounds: RoundLite[],
  rewardRules: RewardRule[] = [],
  secured: SecuredPoints | null = null,
  current: number | null = null,
  pointsPerRound?: number
): LadderRow[] {
  const n = Math.max(5, Math.min(15, maxRounds || 10));
  const map = new Map<number, RoundLite>();
  for (const r of rounds) map.set(r.roundNumber, r);
  const checkpoint = secured?.checkpointRoundNumber ?? 0;
  const hasSecured = (secured?.securedPoints ?? 0) > 0;

  return Array.from({ length: n }, (_, idx) => {
    const roundNumber = idx + 1;
    const r = map.get(roundNumber);
    const level = r?.level ?? fallbackLevel(roundNumber);
    const difficulty = r?.difficulty ?? difficultyFromLevel(level);
    let state: LadderRow['state'] = 'upcoming';
    if (current == null) state = 'upcoming';
    else if (roundNumber < current) state = 'completed';
    else if (roundNumber === current) state = 'current';
    else state = 'upcoming';

    const isFinal = roundNumber === n;
    const isSecured = hasSecured && roundNumber <= checkpoint;
    const securedFlag = secured?.checkpointRoundNumber === roundNumber;
    const nextRewardFlag = current != null && roundNumber === current + 1;
    const isCurrentReward = roundNumber === current;

    // currentReward display
    let currentReward: string | null = null;
    const rule = rewardRules.find(x => x.roundThreshold === roundNumber);
    if (rule) currentReward = `${rule.pointsRequired} pts`;
    else if (pointsPerRound != null && pointsPerRound > 0) currentReward = `${pointsPerRound * roundNumber} pts`;
    else if (pointsPerRound == null) currentReward = `${100 * roundNumber} pts`;
    else currentReward = null;

    // placeholder logic: if no rule and no pointsPerRound, keep null -> component shows "—"
    const securedText = isSecured ? 'asegurado' : '';
    const ariaLabel = `Ronda ${roundNumber} de ${n}, nivel ${level}${currentReward ? `, recompensa ${currentReward}` : ''}${securedText ? `, ${securedText}` : ''}${isFinal ? ', recompensa final' : ''}${state === 'current' ? ', nivel actual' : ''}`;

    return {
      roundNumber,
      level,
      difficulty,
      state,
      isSecured,
      isFinal,
      currentReward,
      nextRewardFlag,
      securedFlag,
      isCurrentReward,
      ariaLabel,
    };
  });
}
