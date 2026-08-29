export interface WithdrawalDisplay {
  currentPoints: number;
  securedPoints: number;
  checkpointRoundNumber: number | null;
  potentialPoints: number | null;
  potentialDisplay: string;
}

export function formatPoints(n: number): string {
  return `${n} pts`;
}

export function formatSecured(secured: number, checkpoint: number | null): string {
  if (checkpoint != null) return `${secured} pts · checkpoint ${checkpoint}`;
  return `${secured} pts`;
}
