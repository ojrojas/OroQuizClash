import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: `
    <div data-theme="player" class="app-shell">
      <header class="app-header">
        <h1>QuizArena Player</h1>
      </header>
      <main>
        <router-outlet />
      </main>
    </div>
  `,
  styles: [`
    .app-shell { min-height: 100vh; display:flex; flex-direction:column; }
    .app-header { padding: var(--space-4, 1rem); background: var(--color-primary); color: white; }
  `]
})
export class AppComponent {}
