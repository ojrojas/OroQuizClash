# UI Contracts: Player Rounds (030)

**Branch**: `030-player-rounds` | **Date**: 2026-08-28 | **Spec**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)

Ladder vertical embebida en `GameComponent` (`/player/game/:gameId`) como panel/sidebar cinematic, reutilizando `GameComponent` 3 áreas ya en 029 (Header Round/Level/Timer, Center Question+Four Answers, Footer Score/Secured/Potential/Status/Withdraw) — ladder es 4º área o sidebar.

## 1. PlayerRoundsComponent

**Selector**: `app-player-rounds`
**Inputs**: `gameId: string` (required), `store?: PlayerRoundsStore` (inject scoped)
**Outputs**: ninguno (solo lectura; no muta server)
**Store**: `PlayerRoundsStore` (`LadderState`) scoped `providers: [PlayerRoundsStore]` en `GameComponent` o `PlayerRoundsComponent`.

### Template (signal + control flow)

```html
<section class="player-rounds" data-theme="player" role="region" aria-label="Progresión de rondas"
         [attr.data-status]="store.status()">
  <!-- Loading -->
  @if (store.status() === 'loading') {
    <div class="ladder-skeleton" aria-busy="true" aria-live="polite">Cargando progresión…</div>
  }
  <!-- Empty -->
  @else if (store.status() === 'empty') {
    <div role="status">Aún no inicia — {{ store.maxRounds() }} rondas por jugar</div>
  }
  <!-- Error -->
  @else if (store.status() === 'error') {
    <app-error-state [detail]="store.errorDetail()" [correlationId]="store.correlationId()" (retry)="store.hydrateLadder(gameId())" />
  }
  <!-- Ready / Terminal -->
  @else {
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
          <span class="difficulty-indicator" aria-hidden="true">{{ row.difficulty }}</span>
          @if (row.isCurrentReward) {
            <span class="badge current-reward" aria-label="Recompensa actual">{{ row.currentReward ?? '—' }}</span>
          }
          @if (row.nextRewardFlag) {
            <span class="badge next-reward" aria-label="Próxima recompensa">{{ row.currentReward ?? '—' }} · próximo</span>
          }
          @if (row.securedFlag) {
            <span class="badge secured-reward" aria-label="Asegurado"><svg class="shield" aria-hidden="true"></svg> Asegurado {{ store.secured()?.securedPoints }} pts</span>
          }
          @if (row.isFinal) {
            <span class="badge final-reward" aria-label="Recompensa final"><svg class="crown" aria-hidden="true"></svg> Final {{ row.currentReward ?? '—' }}</span>
          }
          @if (row.state==='completed') { <span aria-hidden="true">✓</span> }
        </li>
      }
    </ol>
    <div aria-live="polite" class="sr-only">{{ store.announcement() }}</div>
    <!-- Secured summary -->
    @if (store.secured()?.checkpointRoundNumber) {
      <div class="secured-summary" aria-live="polite">Asegurado: {{ store.secured()?.securedPoints }} pts en ronda {{ store.secured()?.checkpointRoundNumber }}</div>
    } @else {
      <div class="secured-summary muted">Sin monto asegurado</div>
    }
  }
</section>
```

- `Current Level` único `aria-current="step"` con clase `.current` premium `border-color: var(--color-primary)` `box-shadow: var(--shadow-premium)` `scale(1.02)` transición 300ms.
- `Previous Levels` `.completed` `opacity:0.7` check ✓.
- `Next Reward` muted `opacity:0.6` + flecha/próximo.
- `Secured Reward` escudo `var(--color-success)` + filas ≤ checkpoint `background: var(--color-success-subtle)`.
- `Final Reward` fila N siempre visible `gradient: var(--player-gradient-final)` + corona, aunque `current` < N.
- Placeholder "—" `aria-label="Sin recompensa configurada"` si no regla.

### CSS (tokens sin literales)

```css
.player-rounds { display:flex; flex-direction:column; gap:var(--space-3); padding:var(--space-4); background:var(--color-surface); }
.ladder { display:flex; flex-direction:column; gap:var(--space-2); max-height:60vh; overflow-y:auto; overflow-x:hidden; list-style:none; padding:0; margin:0; }
.ladder-row { display:flex; align-items:center; gap:var(--space-2); padding:var(--space-3) var(--space-4); border-radius:var(--radius-md); border:1px solid var(--color-border); background:var(--color-surface); min-height:44px; }
.ladder-row.current { border-color:var(--color-primary); box-shadow:var(--shadow-premium); transform:scale(1.02); transition:all 300ms ease-out; }
.ladder-row.completed { opacity:0.7; }
.ladder-row.secured { background:var(--color-success-subtle); }
.ladder-row.final { background:var(--player-gradient-final); color:var(--color-final-contrast); }
.ladder-row.animating { animation: ladderPulse 300ms ease-out; }
@keyframes ladderPulse { 0%{box-shadow:var(--shadow-premium);} 100%{box-shadow:none;} }
@media (prefers-reduced-motion: reduce) { .ladder-row, .ladder-row.current { transition:none; transform:none; animation:none; } }
.badge { font-size:var(--font-size-sm); padding:var(--space-1) var(--space-2); border-radius:var(--radius-sm); }
.sr-only { position:absolute; left:-10000px; width:1px; height:1px; overflow:hidden; }
```

- 0 literales hardcodeados; todos `var(--*)` de `design-system/tokens/design-tokens.css`.

### Interaction

- Solo lectura: click no muta ladder; fila Current puede tener tooltip "Nivel actual".
- Teclado `Tab`/`Shift+Tab` recorre filas (native `li`), foco `outline:2px solid var(--color-primary)`.
- Reconexión salto: si `previousRoundNumber` diff >1, no animar intermedios falsos (direct hydrate).

## 2. Integration en GameComponent (029)

```html
<!-- src/app/features/game/game.component.ts (extend 029) -->
<div class="game-layout" data-theme="player">
  <header class="game-header"> <!-- Round/Level/Timer ya en 029 -->
    <span>{{ store.currentRoundLabel() }}</span> <span>{{ store.currentLevel() }}</span> <app-timer [timer]="store.timer()" />
  </header>
  <div class="game-body">
    <app-player-rounds [gameId]="gameId()" class="game-sidebar" /> <!-- NEW: ladder sidebar -->
    <main class="game-center"> <!-- Question + Four Answers ya en 029 -->
      <app-question [question]="store.question()" [selected]="selectedOptionId()" (select)="selectedOptionId.set($event)" />
      <app-score-panel [score]="store.score()" [secured]="store.securedPoints()" [potential]="store.potentialReward()" />
    </main>
  </div>
  <footer class="game-footer">
    <app-withdrawal [canWithdraw]="store.canWithdraw()" (withdraw)="store.withdraw()" />
  </footer>
</div>
```

- Desktop ≥1024px: `game-body { display:grid; grid-template-columns: 280px 1fr; gap:var(--space-4); }` sidebar sticky `position:sticky top:var(--space-4)`.
- Mobile 375px: `game-body { display:flex; flex-direction:column; }` ladder scrolleable interna `max-height:40vh` sin scroll horizontal.

## 3. States

- `loading`: skeleton ladder 5 filas pulsando `aria-live="polite"`.
- `empty`: `WAITING_FOR_PLAYERS` `currentRoundNumber==null` → texto sin Current `aria-current` ausente.
- `error`: `ProblemDetails detail` + `CorrelationId/TraceId` + Retry CTA → `hydrateLadder()`.
- `terminal`: `isTerminal` bloquea `_animating`, muestra Secured/Final finales.
- `ready`: N filas con estados + `announcement` "Avanzaste a ronda 5".

## 4. Accessibility

- `role="region"` + `aria-label="Progresión de rondas"` section.
- `role="list"` `ol` + `role="listitem"` `li` + `aria-current="step"` único Current.
- `aria-label` per fila "Ronda 4 de 10, nivel Intermediate, recompensa 600 puntos, asegurado".
- `aria-live="polite"` Current/Next/Secured/Final cambios; `aria-live="assertive"` no aplica ladder (terminal handled en game status).
- Targets ≥44px, foco visible, contraste tokens AA, axe 0 violations.

## 5. No mutation

Ladder no tiene `Outputs`; withdraw/answer ya en 029. Ladder solo proyecta.

## References

- SPEC-029 `contracts/ui-contracts.md` (Cinematic 3 áreas Header/Center/Footer design-system).
- `design-system/MASTER.md` + `overrides/player.md` + `tokens/design-tokens.css` (data-theme="player", cinematic premium, WCAG 375-1536, prefers-reduced-motion).
- `src/Player/QuizArena.Player/features/game/player-rounds.component.ts` + `stores/player-rounds.store.ts` + `core/realtime/game-realtime.service.ts`.
