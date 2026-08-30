import { Component, input } from '@angular/core';

@Component({
  selector: 'app-consolation-badge',
  standalone: true,
  template: `
    @if (isConsolation()) {
      <span class="badge" style="background:var(--color-info,#3B82F6); color:white; border-radius:var(--radius-full,999px); padding:var(--space-1,4px) var(--space-2,8px); font-size:var(--text-xs,12px);" aria-label="Recompensa de consolación">Consolation</span>
    }
  `,
  styles: [`
    .badge { display:inline-block; font-weight:600; }
  `]
})
export class ConsolationBadgeComponent {
  isConsolation = input<boolean>(false);
}
