import { Component, input, output } from '@angular/core';

@Component({
  selector: 'app-error-state',
  standalone: true,
  template: `
    <div class="error" role="alert" aria-live="assertive">
      <h3>{{ title() }}</h3>
      <p>{{ message() }}</p>
      @if (correlationId()) {
        <small>CorrelationId: {{ correlationId() }}</small>
      }
      @if (traceId()) {
        <small>TraceId: {{ traceId() }}</small>
      }
      <button (click)="retry.emit()" style="min-height:44px; min-width:44px;">Reintentar</button>
    </div>
  `,
  styles: [`
    .error { padding:16px; border:1px solid var(--color-error, red); border-radius:8px; background: var(--color-error-bg, #fee); }
    button { margin-top:12px; padding:8px 16px; }
    small { display:block; font-family: monospace; }
  `]
})
export class ErrorStateComponent {
  title = input<string>('Error');
  message = input<string>('Ha ocurrido un error');
  correlationId = input<string | null>(null);
  traceId = input<string | null>(null);
  retry = output<void>();
}
