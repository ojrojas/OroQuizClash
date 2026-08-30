import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { GamesApi } from '../shared/games.api';

@Component({
  selector: 'app-leaderboard',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="multiplayer-public" data-theme="player">
      <div class="players" role="list" aria-label="Jugadores">
        @for (p of players(); track p.playerId) {
          <div role="listitem" [attr.aria-label]="p.displayName + ' ' + p.status">
            <span>{{ p.displayName }}</span>
            <span class="badge">{{ p.status }}</span>
          </div>
        }
      </div>
      <div class="players-remaining" role="status" aria-live="polite" [attr.aria-label]="'Players Remaining ' + playersRemaining()">
        Players Remaining: {{ playersRemaining() }}
      </div>
      <div class="leaderboard" role="list" aria-label="Leaderboard" aria-live="polite">
        @for (entry of leaderboard(); track entry.playerId; let i=$index) {
          <div role="listitem" [attr.aria-posinset]="i+1" [attr.aria-setsize]="leaderboard().length" [attr.aria-label]="entry.displayName + ' nivel ' + entry.level + ' ' + entry.totalPoints + ' puntos, puesto ' + (i+1)">
            <span class="position">{{ i+1 }}.</span>
            <span class="name">{{ entry.displayName }}</span>
            <span class="level">{{ entry.level }}</span>
            <span class="points">{{ entry.totalPoints }} pts</span>
          </div>
        }
      </div>
      <div class="current-round" role="status" aria-live="polite" [attr.aria-label]="'Current Round ' + currentRoundNumber() + ' de ' + maxRounds()">
        Ronda {{ currentRoundNumber() }}/{{ maxRounds() }}
      </div>
    </div>
  `,
  styles: [`
    .multiplayer-public { display:flex; flex-direction:column; gap:var(--space-3,12px); }
    .players { display:flex; flex-wrap:wrap; gap:var(--space-2,8px); }
    .leaderboard { display:grid; grid-template-columns:1fr; gap:var(--space-2,8px); }
    @media (min-width:768px){ .leaderboard { grid-template-columns:repeat(4,1fr); } }
    .players-remaining, .current-round { padding:var(--space-3,12px); border:1px solid var(--color-border,#DBEAFE); background:var(--color-surface,#FFF); border-radius:var(--radius-md,8px); min-height:44px; min-width:44px; display:flex; align-items:center; }
    .badge { background:var(--color-primary,#2563EB); color:white; padding:2px 8px; border-radius:12px; font-size:0.75rem; margin-left:4px; }
    @media (prefers-reduced-motion: reduce){ *{ animation:none; transition:none; } }
  `]
})
export class LeaderboardComponent {
  private api = inject(GamesApi);
  players = signal<any[]>([]);
  leaderboard = signal<any[]>([]);
  currentRoundNumber = signal(1);
  maxRounds = signal(10);
  playersRemaining = computed(() => this.players().filter((p: any) => p.status === 'ACTIVE' || p.isActive).length);

  hydrate(gameId: string) {
    this.api.getLeaderboard(gameId).subscribe({
      next: (res: any) => {
        const entries = res.entries ?? res.Players ?? [];
        this.leaderboard.set(entries.map((e: any) => ({
          playerId: e.playerId ?? e.PlayerId,
          displayName: e.displayName ?? e.DisplayName ?? 'Player',
          totalPoints: e.totalPoints ?? e.Points ?? 0,
          level: e.level ?? e.CurrentLevel ?? 'Basic',
          status: e.status ?? e.Status ?? 'ACTIVE',
        })));
        this.players.set(entries.map((e: any) => ({
          playerId: e.playerId ?? e.PlayerId,
          displayName: e.displayName ?? e.DisplayName ?? 'Player',
          status: e.status ?? e.Status ?? 'ACTIVE',
        })));
      },
      error: () => {}
    });
  }
}
