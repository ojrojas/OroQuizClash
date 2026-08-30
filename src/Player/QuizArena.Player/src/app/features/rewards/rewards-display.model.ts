export interface RewardsDisplay {
  availablePoints: number | null;
  requiredPoints: number;
  remainingPoints: number | null;
  remainingDisplay: string;
  rewardStatus: 'Canjeable' | 'Puntos insuficientes' | 'Agotada' | 'No disponible';
}

export function formatPoints(n: number): string {
  return `${n} pts`;
}

export function deriveRewardStatus(available: number | null, required: number, isAvailable: boolean, stock: number): 'Canjeable' | 'Puntos insuficientes' | 'Agotada' | 'No disponible' {
  if (!isAvailable && stock === 0) return 'Agotada';
  if (!isAvailable) return 'No disponible';
  if (available == null) return 'No disponible';
  if (available >= required) return 'Canjeable';
  return 'Puntos insuficientes';
}

export function formatRemaining(available: number | null, required: number, isAvailable: boolean): string {
  if (available == null) return '—';
  const diff = available - required;
  if (isAvailable && available >= required) return `${diff} pts`;
  return `Te faltan ${Math.abs(diff)} pts`;
}
