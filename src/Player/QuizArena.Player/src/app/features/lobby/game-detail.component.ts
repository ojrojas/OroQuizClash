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
        <p aria-live="polite" style="opacity:0.6; font-size:0.85rem;">Cargando detalle del juego...</p>
      } @else if (error) {
        <app-error-state [message]="error.detail" [correlationId]="error.correlationId" [traceId]="error.traceId" (retry)="load()" />
      } @else if (game) {
        <h2>{{ game.name }}</h2>
        <div style="display:flex; gap:8px; flex-wrap:wrap; margin:8px 0;">
          <span class="badge" style="background:var(--color-primary,#2563EB); color:white; padding:4px 8px; border-radius:12px;">{{ game.status }}</span>
          <span class="badge" style="background:var(--color-warning,#F59E0B); color:#111; padding:4px 8px; border-radius:12px;">{{ game.categoryName ?? game.categoryId }}</span>
        </div>
        <dl style="display:grid; grid-template-columns:140px 1fr; gap:6px 12px; background:var(--color-surface,#1e293b); padding:12px; border-radius:8px;">
          <dt>Game Name</dt><dd>{{ game.name }}</dd>
          <dt>Category</dt><dd>{{ game.categoryName ?? game.categoryId }}</dd>
          <dt>Difficulty</dt><dd>{{ game.difficulty ?? game.initialDifficulty ?? game.initialDifficulty }}</dd>
          <dt>Number of Rounds</dt><dd>{{ game.minRounds }}-{{ game.maxRounds }}</dd>
          <dt>Players</dt><dd>{{ game.playerCount ?? game.players?.current ?? game.playersCurrent ?? 0 }}/{{ game.maxPlayers ?? game.players?.max ?? game.playersMax ?? 10 }} · {{ (game.players?.length ?? game.playerCount ?? 0) }} inscritos</dd>
          <dt>Start Time</dt><dd>{{ game.startTime ?? game.startedAt ?? game.createdAt | date:'short' }}</dd>
          <dt>Prize</dt><dd>{{ game.prize ?? game.rewardRules?.type ?? '—' }}</dd>
          <dt>Status</dt><dd>{{ game.status }}</dd>
          <dt>TimeLimit</dt><dd>{{ game.timeLimitPerQuestionSeconds ?? game.timeLimit ?? game.configuration?.timeLimitPerQuestionSeconds }}s</dd>
          <dt>PointsPerRound</dt><dd>{{ game.pointsPerRound ?? game.configuration?.pointsPerRound }}</dd>
          <dt>WithdrawalPolicy</dt><dd>{{ game.withdrawalPolicy ?? game.configuration?.withdrawalPolicy }}</dd>
          <dt>LossPolicy</dt><dd>{{ game.lossPolicy ?? game.configuration?.lossPolicy }}</dd>
          <dt>Players List</dt><dd>
            @if (game.players?.length) {
              @for (p of game.players; track p.userId) {
                <span style="display:inline-block; background:var(--color-muted,#334155); padding:2px 6px; border-radius:6px; margin:2px;">{{ p.displayName }} ({{ p.status }})</span>
              }
            } @else {
              {{ game.playersList?.length ?? game.playerCount ?? 0 }} jugadores
            }
          </dd>
        </dl>
        <div style="display:flex; gap:8px; margin-top:12px; flex-wrap:wrap;">
          <button (click)="close()" style="min-height:44px; min-width:44px;">Cerrar</button>
          <button (click)="join()" [disabled]="game.status !== 'WAITING_FOR_PLAYERS'" style="min-height:44px; min-width:44px; background:var(--color-success,#22c55e); color:white; border:none; border-radius:6px; padding:8px 12px;">Unirse a la partida</button>
          <button (click)="load()" style="min-height:44px; min-width:44px;" aria-label="Recargar">↻ Recargar</button>
        </div>
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
  private loadTimeout: any = null;

  ngOnInit() {
    this.load();
  }

  load() {
    const id = this.route.snapshot.paramMap.get('gameId') ?? this.route.snapshot.paramMap.get('id');
    if (!id) { this.error = { detail: 'GameId requerido', correlationId: '', traceId: '' }; this.isLoading = false; return; }
    this.isLoading = true;
    this.error = null;
    // Timeout de 8s para detectar consulta demorada (backend optimizado con AsNoTracking)
    clearTimeout(this.loadTimeout);
    this.loadTimeout = setTimeout(() => {
      if (this.isLoading) console.warn('[GameDetail] consulta demorada >8s para', id);
    }, 8000);
    this.api.getGame(id).subscribe({
      next: (g) => {
        clearTimeout(this.loadTimeout);
        // Normalizar respuesta detallada (GameDetailResponse) para compatibilidad con template
        const anyG: any = g as any;
        // Mapear campos planos que espera el template
        anyG.categoryName = anyG.categoryName ?? anyG.CategoryName ?? anyG.categoryId;
        anyG.initialDifficulty = anyG.initialDifficulty ?? anyG.InitialDifficulty ?? anyG.difficulty;
        anyG.timeLimitPerQuestionSeconds = anyG.timeLimitPerQuestionSeconds ?? anyG.TimeLimitPerQuestionSeconds ?? anyG.configuration?.timeLimitPerQuestionSeconds;
        anyG.pointsPerRound = anyG.pointsPerRound ?? anyG.PointsPerRound ?? anyG.configuration?.pointsPerRound;
        anyG.withdrawalPolicy = anyG.withdrawalPolicy ?? anyG.WithdrawalPolicy ?? anyG.configuration?.withdrawalPolicy;
        anyG.lossPolicy = anyG.lossPolicy ?? anyG.LossPolicy ?? anyG.configuration?.lossPolicy;
        // Players list: backend retorna Players[] con UserId/DisplayName
        if (anyG.players && Array.isArray(anyG.players) && anyG.players.length > 0) {
          // ya está bien
        } else if (anyG.Players && Array.isArray(anyG.Players)) {
          anyG.players = anyG.Players;
        }
        this.game = anyG;
        this.isLoading = false;
      },
      error: (err) => { clearTimeout(this.loadTimeout); this.error = err; this.isLoading = false; }
    });
  }

  join() {
    const id = this.route.snapshot.paramMap.get('gameId') ?? this.route.snapshot.paramMap.get('id');
    if (!id) return;
    const key = (() => { try { return sessionStorage.getItem(`idemp-join-${id}`) ?? crypto.randomUUID(); } catch { return `join-${Date.now()}`; } })();
    try { sessionStorage.setItem(`idemp-join-${id}`, key); } catch {}
    this.api.joinGame(id, key).subscribe({
      next: () => this.router.navigate(['/player/game', id]),
      error: (err: any) => {
        if (err?.status === 409 || err?.code === 'AlreadyJoined') this.router.navigate(['/player/game', id]);
        else this.error = err;
      }
    });
  }

  close() {
    this.router.navigate(['/player/lobby']);
  }
}
