export type AnswerOptionState = 'Idle' | 'Hover' | 'Selected' | 'Locked' | 'Evaluating' | 'Correct' | 'Incorrect' | 'Timeout';
export type AnswerPhase = 'idle' | 'selected' | 'locked' | 'evaluating' | 'correct' | 'incorrect' | 'timeout';

export interface AnswerInteractionState {
  gameId: string | null;
  roundId: string | null;
  questionId: string | null;
  selectedOptionId: string | null;
  lockedOptionId: string | null;
  phase: AnswerPhase;
  isEvaluating: boolean;
  isLocked: boolean;
  canSelect: boolean;
  errorDetail?: string | null;
  correlationId?: string | null;
  scoreDelta?: number | null;
  correctOptionId?: string | null;
}

export function mapOptionState(
  optionId: string,
  interaction: AnswerInteractionState,
  answerState: string | null,
  isCorrect: boolean | null,
  correctOptionId: string | null
): AnswerOptionState {
  const { phase, selectedOptionId, lockedOptionId, isEvaluating } = interaction;

  if (phase === 'timeout') return 'Timeout';
  if (phase === 'evaluating' && lockedOptionId === optionId) return 'Evaluating';
  if (phase === 'correct') {
    if (lockedOptionId === optionId) return 'Correct';
    if (correctOptionId === optionId) return 'Correct';
    return 'Idle';
  }
  if (phase === 'incorrect') {
    if (lockedOptionId === optionId) return 'Incorrect';
    if (correctOptionId === optionId) return 'Correct';
    return 'Idle';
  }
  if (phase === 'locked' && lockedOptionId === optionId) return 'Locked';
  if (phase === 'selected' && selectedOptionId === optionId) return 'Selected';
  if (phase === 'idle' && selectedOptionId === optionId) return 'Selected';
  // fallback for hydrate from server states
  if (answerState === 'EXPIRED') return 'Timeout';
  if (isEvaluating && lockedOptionId === optionId) return 'Evaluating';
  return 'Idle';
}

export function answerStateToPhase(state: string | null, isCorrect: boolean | null, isEvaluating: boolean): AnswerPhase {
  if (isEvaluating) return 'evaluating';
  if (state === 'EXPIRED') return 'timeout';
  if (state === 'EVALUATED' && isCorrect === true) return 'correct';
  if (state === 'EVALUATED' && isCorrect === false) return 'incorrect';
  if (state === 'SUBMITTED') return 'evaluating';
  if (state === 'LOCKED') return 'locked';
  return 'idle';
}
