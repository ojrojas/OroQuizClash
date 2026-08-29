import { Component, input } from '@angular/core';

@Component({
  selector: 'app-loading-skeleton',
  standalone: true,
  template: `
    <div class="skeleton" role="status" aria-live="polite" aria-label="Cargando">
      @for (i of rowsArray; track i) {
        <div class="skeleton-line" [style.width.%]="90 - i*5"></div>
      }
    </div>
  `,
  styles: [`
    .skeleton { display:flex; flex-direction:column; gap:12px; padding:16px; }
    .skeleton-line { height:16px; background: var(--color-skeleton, #e0e0e0); border-radius:4px; animation: pulse 1.5s infinite; }
    @keyframes pulse { 0% { opacity:.6 } 50% { opacity:1 } 100% { opacity:.6 } }
  `]
})
export class LoadingSkeletonComponent {
  rows = input<number>(3);
  get rowsArray() { return Array.from({ length: this.rows() }, (_, i) => i); }
}
