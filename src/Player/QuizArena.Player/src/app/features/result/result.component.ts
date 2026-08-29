import { Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { PlayerGameStore } from '../../stores/player-game.store';
import { LoadingSkeletonComponent } from '../../shared/ui/loading-skeleton.component';

@Component({
  selector: 'app-result',
  standalone: true,
  imports: [LoadingSkeletonComponent],
  providers: [PlayerGameStore],
  template: `
    <div class="result" data-theme="player" aria-live="polite">
      @if (store.ui().isLoading) {
        <app-loading-skeleton />
      } @else {
        <h2>Resultado</h2>
        <p>Estado: {{ store.status().playerStatus }} / {{ store.status().gameStatus }}</p>
        <p>Score final: {{ store.score().totalPoints }} pts</p>
        <p>Asegurados: {{ store.securedPoints().securedPoints }} (checkpoint {{ store.securedPoints().checkpointRoundNumber }})</p>
        @if (store.status().isTerminal) {
          <p aria-live="assertive">Participación terminada - no puedes responder más</p>
        }
        <small>CorrelationId: {{ store.ui().error?.correlationId }}</small>
      }
    </div>
  `
})
export class ResultComponent implements OnInit {
  store = inject(PlayerGameStore);
  private route = inject(ActivatedRoute);
  ngOnInit() {
    const gameId = this.route.snapshot.paramMap.get('gameId')!;
    this.store.hydrateFor(gameId);
  }
}
