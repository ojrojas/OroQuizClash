import { Component, inject, OnInit, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { GamesApi } from '../shared/games.api';
import { LoadingSkeletonComponent } from '../../shared/ui/loading-skeleton.component';
import { ErrorStateComponent } from '../../shared/ui/error-state.component';

@Component({
  selector: 'app-game-detail',
  standalone: true,
  imports: [CommonModule, LoadingSkeletonComponent, ErrorStateComponent],
  template: `
    <div class="detail" data-theme="player" style="padding:16px;">
      @if (isLoading) {
        <app-loading-skeleton />
      } @else if (error) {
        <app-error-state [message]="error.detail" [correlationId]="error.correlationId" [traceId]="error.traceId" (retry)="load()" />
      } @else if (game) {
        <h2>{{ game.name }}</h2>
        <dl>
          <dt>Game Name</dt><dd>{{ game.name }}</dd>
          <dt>Category</dt><dd>{{ game.categoryName ?? game.categoryId }}</dd>
          <dt>Difficulty</dt><dd>{{ game.difficulty ?? game.initialDifficulty }}</dd>
          <dt>Number of Rounds</dt><dd>{{ game.minRounds }}-{{ game.maxRounds }}</dd>
          <dt>Players</dt><dd>{{ game.players?.current ?? 0 }}/{{ game.players?.max ?? game.maxPlayers ?? 10 }}</dd>
          <dt>Start Time</dt><dd>{{ game.startTime ?? game.createdAt | date:'short' }}</dd>
          <dt>Prize</dt><dd>{{ game.prize ?? '—' }}</dd>
          <dt>Status</dt><dd>{{ game.status }}</dd>
          <dt>TimeLimit</dt><dd>{{ game.timeLimitPerQuestionSeconds ?? game.configuration?.timeLimitPerQuestionSeconds }}s</dd>
          <dt>PointsPerRound</dt><dd>{{ game.pointsPerRound ?? game.configuration?.pointsPerRound }}</dd>
          <dt>WithdrawalPolicy</dt><dd>{{ game.withdrawalPolicy ?? game.configuration?.withdrawalPolicy }}</dd>
          <dt>LossPolicy</dt><dd>{{ game.lossPolicy ?? game.configuration?.lossPolicy }}</dd>
          <dt>Players List</dt><dd>{{ game.playersList?.length ?? 0 }} jugadores</dd>
        </dl>
        <button (click)="close()" style="min-height:44px; min-width:44px;">Cerrar</button>
      }
    </div>
  `
})
export class GameDetailComponent implements OnInit {
  private api = inject(GamesApi);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  game: any = null;
  isLoading = true;
  error: any = null;

  ngOnInit() {
    this.load();
  }

  load() {
    const id = this.route.snapshot.paramMap.get('gameId') ?? this.route.snapshot.paramMap.get('id');
    if (!id) { this.error = { detail: 'GameId requerido', correlationId: '', traceId: '' }; this.isLoading = false; return; }
    this.isLoading = true;
    this.api.getGame(id).subscribe({
      next: (g) => { this.game = g; this.isLoading = false; },
      error: (err) => { this.error = err; this.isLoading = false; }
    });
  }

  close() {
    this.router.navigate(['/lobby']);
  }
}
