import { Component, input, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PlayerRoundsStore } from '../../stores/player-rounds.store';
import { ErrorStateComponent } from '../../shared/ui/error-state.component';

@Component({
  selector: 'app-player-rounds',
  standalone: true,
  imports: [CommonModule, ErrorStateComponent],
  template: `
    <section class="player-rounds" data-theme="player" role="region" aria-label="Progresión de rondas" [attr.data-status]="store.status()">
      @if (store.status() === 'loading') {
        <div class="ladder-skeleton" aria-busy="true" aria-live="polite">
          <div class="skeleton-row" *ngFor="let i of [1,2,3,4,5]" style="height:44px; background:var(--color-surface-muted, #eee); border-radius:var(--radius-md, 8px); margin:var(--space-2,8px) 0;"></div>
          <span class="sr-only">Cargando progresión…</span>
        </div>
      } @else if (store.status() === 'empty') {
        <div role="status" class="empty-state" style="text-align:center; padding:var(--space-4,16px);">
          Aún no inicia — {{ store.maxRounds() }} rondas por jugar
        </div>
        @if (store.ladder().length) {
          <ol class="ladder" role="list" aria-label="Progresión de rondas">
            @for (row of store.ladder(); track row.roundNumber) {
              <li role="listitem" class="ladder-row upcoming" [attr.aria-label]="row.ariaLabel">
                <span class="round-label">Round {{ row.roundNumber }}</span>
                <span class="level">{{ row.level }}</span>
                <span class="difficulty-indicator" aria-hidden="true">{{ row.difficulty }}</span>
                <span class="badge" aria-label="Sin recompensa configurada">{{ row.currentReward ?? '—' }}</span>
              </li>
            }
          </ol>
        }
      } @else if (store.status() === 'error') {
        <app-error-state [message]="store.errorDetail() ?? 'Error al cargar progresión'" [correlationId]="store.correlationId()" [traceId]="store.correlationId()" (retry)="retry()"></app-error-state>
      } @else {
        <ol class="ladder" role="list" aria-label="Progresión de rondas">
          @for (row of store.ladder(); track row.roundNumber) {
            <li role="listitem"
                class="ladder-row"
                [class.completed]="row.state==='completed'"
                [class.current]="row.state==='current'"
                [class.upcoming]="row.state==='upcoming'"
                [class.secured]="row.isSecured"
                [class.final]="row.isFinal"
                [class.animating]="store._animatingRound()===row.roundNumber"
                [attr.aria-current]="row.state==='current' ? 'step' : null"
                [attr.aria-label]="row.ariaLabel">
              <span class="round-label">Round {{ row.roundNumber }}</span>
              <span class="level">{{ row.level }}</span>
              <span class="difficulty-indicator" aria-hidden="true">{{ row.difficulty !== null ? '· ' + row.difficulty : '' }}</span>
              @if (row.isCurrentReward) {
                <span class="badge current-reward" aria-label="Recompensa actual">{{ row.currentReward ?? '—' }}</span>
              }
              @if (row.nextRewardFlag) {
                <span class="badge next-reward" aria-label="Próxima recompensa">{{ row.currentReward ?? '—' }} · próximo</span>
              }
              @if (row.securedFlag) {
                <span class="badge secured-reward" aria-label="Asegurado">
                  <svg class="shield" aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><path d="M12 2l7 4v6c0 5-3.5 7.5-7 8-3.5-.5-7-3-7-8V6l7-4z"/></svg>
                  Asegurado {{ store.secured()?.securedPoints }} pts
                </span>
              }
              @if (row.isFinal) {
                <span class="badge final-reward" aria-label="Recompensa final">
                  <svg class="crown" aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="currentColor"><path d="M3 5l4 10h10l4-10-6 3-4-4-4 4-6-3z"/></svg>
                  Final {{ row.currentReward ?? '—' }}
                </span>
              }
              @if (row.state==='completed') { <span aria-hidden="true" class="check">✓</span> }
            </li>
          }
        </ol>
        <div aria-live="polite" class="sr-only">{{ store.announcement() }}</div>
        @if (store.secured()?.checkpointRoundNumber) {
          <div class="secured-summary" aria-live="polite">Asegurado: {{ store.secured()?.securedPoints }} pts en ronda {{ store.secured()?.checkpointRoundNumber }}</div>
        } @else {
          <div class="secured-summary muted" aria-live="polite">Sin monto asegurado</div>
        }
      }
    </section>
  `,
  styleUrls: ['./player-rounds.component.css']
})
export class PlayerRoundsComponent {
  gameId = input.required<string>();
  store = inject(PlayerRoundsStore);

  retry() {
    const id = this.gameId();
    if (id) this.store.hydrateLadder(id);
  }
}
