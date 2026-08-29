export interface ScoringDisplayState {
  currentPoints: number;
  securedPoints: number;
  checkpointRoundNumber: number | null;
  potentialPoints: number | null;
  potentialDisplay: string;
  roundPoints: number;
  totalPoints: number;
  isLoading: boolean;
  errorDetail?: string | null;
  correlationId?: string | null;
}

export function formatPoints(n: number): string {
  return `${n} pts`;
}

export function formatSecured(secured: number, checkpoint: number | null): string {
  if (checkpoint != null) return `${secured} pts · checkpoint ${checkpoint}`;
  return `${secured} pts`;
}

export function formatPotential(potential: number | null, rewardName?: string | null, threshold?: number | null): string {
  if (potential == null) return '—';
  if (rewardName && threshold != null) return `Próximo: ${rewardName} ${threshold} pts`;
  return `${potential} pts`;
}
