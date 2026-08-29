import { computed, inject } from '@angular/core';
import { signalStore, withState, withComputed, withMethods, patchState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap, tap, debounceTime } from 'rxjs';
import { tapResponse } from '@ngrx/operators';
import { GamesApi } from '../features/shared/games.api';
import { AnswerInteractionState } from '../features/game/answer-interaction.model';
import { ProblemDetails } from '../core/interceptors/error.interceptor';

const initialState: AnswerInteractionState = {
  gameId: null,
  roundId: null,
  questionId: null,
  selectedOptionId: null,
  lockedOptionId: null,
  phase: 'idle',
  isEvaluating: false,
  isLocked: false,
  canSelect: true,
  errorDetail: null,
  correlationId: null,
  scoreDelta: null,
  correctOptionId: null,
};

export const AnswerInteractionStore = signalStore(
  withState<AnswerInteractionState>(initialState),
  withComputed(({ isLocked, isEvaluating, phase, selectedOptionId, lockedOptionId, canSelect }) => ({
    isLockedComputed: computed(() => isLocked()),
    canSelectComputed: computed(() => canSelect() && !isLocked() && !isEvaluating() && phase() !== 'correct' && phase() !== 'incorrect' && phase() !== 'timeout'),
    hasSelection: computed(() => !!selectedOptionId()),
  })),
  withMethods((store) => {
    const api = inject(GamesApi);
    return {
    selectOption: rxMethod<string>(pipe(
      debounceTime(150),
      tap((optionId: string) => {
        if (store.isLocked() || store.isEvaluating() || !store.canSelect()) return;
        if (store.phase() === 'correct' || store.phase() === 'incorrect' || store.phase() === 'timeout' || store.phase() === 'evaluating') return;
        patchState(store, { selectedOptionId: optionId, phase: 'selected' as const, errorDetail: null });
      })
    )),

    confirmLock: rxMethod<void>(pipe(
      tap(() => {
        if (store.isLocked() || store.isEvaluating()) return;
        const selected = store.selectedOptionId();
        if (!selected) {
          patchState(store, { errorDetail: 'Selecciona una opción' });
          return;
        }
        patchState(store, { lockedOptionId: selected, phase: 'locked' as const, isLocked: true, canSelect: false, errorDetail: null });
      })
    )),

    submitAnswer: rxMethod<void>(pipe(
      switchMap(() => {
        const gameId = store.gameId();
        const roundId = store.roundId();
        const questionId = store.questionId();
        const locked = store.lockedOptionId();
        if (!gameId || !roundId || !questionId || !locked) {
          patchState(store, { errorDetail: 'Selecciona una opción' });
          return [] as any;
        }
        const key = sessionStorage.getItem(`idemp-${roundId}`) ?? crypto.randomUUID();
        sessionStorage.setItem(`idemp-${roundId}`, key);
        patchState(store, { isEvaluating: true, phase: 'evaluating' as const, errorDetail: null, correlationId: null });
        return api.submitAnswer(gameId, { roundId, questionId, selectedOptionId: locked, idempotencyKey: key }).pipe(
          tapResponse({
            next: (answer: any) => {
              const isCorrect = answer.isCorrect;
              const state = answer.state;
              if (state === 'EVALUATED') {
                patchState(store, { phase: isCorrect ? 'correct' as const : 'incorrect' as const, isEvaluating: false, canSelect: false, scoreDelta: answer.scoreDelta ?? null, correctOptionId: answer.correctOptionId ?? null });
                // if isCorrect is false, try to derive correctOptionId from answer if available
                if (isCorrect === false && answer.correctOptionId) {
                  patchState(store, { correctOptionId: answer.correctOptionId });
                }
              } else if (state === 'EXPIRED') {
                patchState(store, { phase: 'timeout' as const, isEvaluating: false, canSelect: false });
              } else if (state === 'SUBMITTED') {
                patchState(store, { phase: 'evaluating' as const, isEvaluating: true });
              } else {
                patchState(store, { isEvaluating: false });
              }
            },
            error: (err: any) => {
              const problem: ProblemDetails = err;
              const code = (problem as any)?.code ?? problem?.title;
              if (code === 'AnswerWindowExpired' || problem?.status === 400 && code === 'AnswerWindowExpired') {
                patchState(store, { phase: 'timeout' as const, isEvaluating: false, canSelect: false, errorDetail: null, correlationId: problem?.correlationId ?? null });
              } else if (problem?.status === 409 || code === 'QuestionAlreadyAnswered') {
                // saturate to locked/evaluating based on state; keep locked
                patchState(store, { isEvaluating: false, errorDetail: null, correlationId: problem?.correlationId ?? null });
              } else {
                patchState(store, { errorDetail: problem?.detail ?? problem?.title ?? 'Error al enviar respuesta', correlationId: problem?.correlationId ?? problem?.traceId ?? null, isEvaluating: false });
              }
            },
          })
        );
      })
    )),

    hydrateAnswer: rxMethod<string>(pipe(
      switchMap((gameId: string) => {
        patchState(store, { gameId, errorDetail: null });
        return api.getMyState(gameId).pipe(
          tapResponse({
            next: (state: any) => {
              const round = state.round;
              const question = state.question;
              const answer = state.answer;
              const timer = state.timer;
              const status = state.status;

              const roundId = round?.roundId ?? round?.RoundId ?? null;
              const questionId = question?.questionId ?? question?.QuestionId ?? null;
              const selectedOptionId = answer?.selectedOptionId ?? answer?.SelectedOptionId ?? null;
              const aState: string | null = answer?.state ?? answer?.State ?? 'PENDING';
              const isCorrect: boolean | null = answer?.isCorrect ?? answer?.IsCorrect ?? null;
              const correctOptionId: string | null = answer?.correctOptionId ?? null;

              // Validate 4 options invariant
              const options = question?.answerOptions ?? question?.AnswerOptions ?? [];
              if (question && options.length !== 4) {
                patchState(store, {
                  roundId, questionId,
                  canSelect: false,
                  errorDetail: 'Pregunta inválida (se requieren 4 opciones)',
                  correlationId: crypto.randomUUID(),
                  phase: 'idle' as const,
                  selectedOptionId: null,
                  lockedOptionId: null,
                  isLocked: false,
                });
                return;
              }

              // Timer expired handling
              if (timer?.state === 'EXPIRED' || aState === 'EXPIRED') {
                patchState(store, {
                  roundId, questionId,
                  selectedOptionId,
                  lockedOptionId: selectedOptionId,
                  isLocked: !!selectedOptionId,
                  phase: 'timeout' as const,
                  isEvaluating: false,
                  canSelect: false,
                  correctOptionId: null,
                });
                return;
              }

              if (aState === 'EVALUATED') {
                const phase = isCorrect ? 'correct' as const : 'incorrect' as const;
                patchState(store, {
                  roundId, questionId,
                  selectedOptionId,
                  lockedOptionId: selectedOptionId,
                  isLocked: true,
                  phase,
                  isEvaluating: false,
                  canSelect: false,
                  correctOptionId: correctOptionId ?? null,
                });
                return;
              }

              if (aState === 'SUBMITTED' || (aState === 'PENDING' && selectedOptionId && status?.canAnswer === false)) {
                // submitted but not yet evaluated -> evaluating
                patchState(store, {
                  roundId, questionId,
                  selectedOptionId,
                  lockedOptionId: selectedOptionId,
                  isLocked: true,
                  phase: 'evaluating' as const,
                  isEvaluating: true,
                  canSelect: false,
                });
                return;
              }

              if (selectedOptionId) {
                // hydrate locked from previous submit (PENDING+selected -> locked)
                const canAnswer = status?.canAnswer;
                if (canAnswer === false && aState !== 'PENDING') {
                  patchState(store, {
                    roundId, questionId,
                    selectedOptionId,
                    lockedOptionId: selectedOptionId,
                    isLocked: true,
                    phase: 'locked' as const,
                    isEvaluating: false,
                    canSelect: false,
                  });
                } else {
                  patchState(store, {
                    roundId, questionId,
                    selectedOptionId,
                    lockedOptionId: selectedOptionId,
                    isLocked: true,
                    phase: 'locked' as const,
                    isEvaluating: false,
                    canSelect: false,
                  });
                }
                return;
              }

              // idle: no answer yet
              patchState(store, {
                roundId, questionId,
                selectedOptionId: null,
                lockedOptionId: null,
                isLocked: false,
                phase: 'idle' as const,
                isEvaluating: false,
                canSelect: status?.canAnswer ?? true,
                errorDetail: null,
                correlationId: null,
                correctOptionId: null,
                scoreDelta: null,
              });
            },
            error: (err: ProblemDetails) => {
              patchState(store, { errorDetail: err?.detail ?? err?.title ?? 'Error al cargar pregunta', correlationId: err?.correlationId ?? err?.traceId ?? null });
            },
          })
        );
      })
    )),

    resetForNewQuestion(questionId: string, roundId: string) {
      patchState(store, {
        questionId, roundId,
        selectedOptionId: null,
        lockedOptionId: null,
        phase: 'idle' as const,
        isEvaluating: false,
        isLocked: false,
        canSelect: true,
        errorDetail: null,
        correlationId: null,
        correctOptionId: null,
        scoreDelta: null,
      });
    },

    clearError() {
      patchState(store, { errorDetail: null, correlationId: null });
    },

    setCanSelect(can: boolean) {
      patchState(store, { canSelect: can });
    },

    // for tests
    _setState(patch: Partial<AnswerInteractionState>) {
      patchState(store, patch as any);
    }
    };
  })
);
