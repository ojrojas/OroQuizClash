import { computed, inject } from '@angular/core';
import { signalStore, withState, withComputed, withMethods, withProps, patchState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap, tap } from 'rxjs';
import { tapResponse } from '@ngrx/operators';
import { GamesApi } from '../shared/games.api';
import { ProblemDetails } from '../../core/interceptors/error.interceptor';

export interface GameSummary {
  gameId: string;
  name: string;
  categoryId: string;
  categoryName: string;
  difficulty: number;
  difficultyName: string;
  minRounds: number;
  maxRounds: number;
  numberOfRoundsDisplay: string;
  players: { current: number; max: number; display: string };
  startTime: string;
  prize: string;
  status: string;
  version: string;
}

type LobbyState = {
  games: GameSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
  isLoading: boolean;
  error: ProblemDetails | null;
};

const initialState: LobbyState = {
  games: [],
  totalCount: 0,
  page: 1,
  pageSize: 20,
  isLoading: false,
  error: null,
};

export const LobbyStore = signalStore(
  withState(initialState),

  withProps(() => ({
    _api: inject(GamesApi),
  })),

  withComputed(({ games }) => ({
    isEmpty: computed(() => games().length === 0),
    totalPages: computed(() => Math.ceil(initialState.totalCount / initialState.pageSize)),
  })),

  withMethods((store) => ({
    load: rxMethod<{ page?: number; pageSize?: number }>(pipe(
      tap(() => patchState(store, { isLoading: true, error: null })),
      switchMap(({ page, pageSize }) => {
        const p = page ?? store.page();
        const ps = pageSize ?? store.pageSize();
        return store._api.getGames({ status: 'WAITING_FOR_PLAYERS', page: p, pageSize: ps });
      }),
      tapResponse({
        next: (res: any) => {
          const games: GameSummary[] = (res.items ?? []).map((g: any) => ({
            gameId: g.gameId ?? g.id,
            name: g.name,
            categoryId: g.categoryId,
            categoryName: g.categoryName ?? g.category ?? '—',
            difficulty: g.difficulty ?? g.initialDifficulty ?? 1,
            difficultyName: g.difficultyName ?? String(g.difficulty ?? 1),
            minRounds: g.minRounds ?? 5,
            maxRounds: g.maxRounds ?? 10,
            numberOfRoundsDisplay: g.minRounds === g.maxRounds ? String(g.minRounds) : `${g.minRounds}-${g.maxRounds}`,
            players: { current: g.players?.current ?? g.currentPlayers ?? 0, max: g.players?.max ?? g.maxPlayers ?? 10, display: `${g.players?.current ?? 0}/${g.players?.max ?? 10}` },
            startTime: g.startTime ?? g.createdAt,
            prize: g.prize ?? '—',
            status: g.status,
            version: g.version ?? '',
          }));
          patchState(store, { games, totalCount: res.totalCount ?? games.length, page: res.page ?? store.page(), isLoading: false });
        },
        error: (err: ProblemDetails) => patchState(store, { error: err, isLoading: false }),
      })
    )),

    clearError() { patchState(store, { error: null }); },
  }))
);
