import { Component, input } from '@angular/core';

@Component({
  selector: 'app-empty-state',
  standalone: true,
  template: `
    <div class="empty" role="status">
      <p>{{ message() }}</p>
      <ng-content />
    </div>
  `,
  styles: [`
    .empty { text-align:center; padding:32px; color: var(--color-muted); }
    .empty p { font-size: var(--font-size-lg); }
  `]
})
export class EmptyStateComponent {
  message = input<string>('No hay datos disponibles');
}
