# Contracts: UI for Player Game (029)

**Branch**: `029-player-game` | **Date**: 2026-08-28
Design System: `design-system/tokens/design-tokens.css` `data-theme="player"` + `overrides/player.md` WCAG 375-1536.

## Layout (Cinematic 3 áreas)

**Route**: `/player/game/:gameId` (and `/game/:gameId` alias) `canActivate: [authGuard, mustChangePasswordGuard]` `providers: [PlayerGameStore]` scoped per `gameId`.

```html
<!-- game.component.html cinematic grid -->
<div class="game-cinematic" data-theme="player" style="display:grid; grid-template-areas:'header' 'center' 'footer'; gap:var(--space-4);">
  <header style="grid-area:header; display:flex; justify-content:space-between; align-items:center; background: var(--player-gradient-premium); padding: var(--space-4);">
    <div>Current Round: "Ronda {{store.gameSession().currentRoundNumber}}/{{store.game().configuration.maxRounds}}"</div>
    <div>Current Level: {{store.score().currentLevel}} ({{store.round()?.level}})</div>
    <app-timer /> <!-- RUNNING/STOPPED/EXPIRED aria-live, warning <10s -->
  </header>

  <main style="grid-area:center;">
    <h2 aria-live="polite">{{store.question()?.text}}</h2>
    <div role="radiogroup" aria-label="Opciones de respuesta" style="display:grid; grid-template-columns:1fr 1fr; gap:var(--space-3);">
      @for (opt of store.question()?.answerOptions; track opt.optionId) {
        <button role="radio" [attr.aria-checked]="selected===opt.optionId" (click)="selected=opt.optionId" style="min-height:44px; text-align:left; padding:var(--space-3);">{{opt.text}}</button>
      }
    </div>
    <button (click)="submit()" [disabled]="!store.canAnswer() || !selected" style="min-height:44px;">Enviar respuesta</button>
    @if (store.answer()?.state === 'EVALUATED') { <div aria-live="assertive">{{store.answer()?.isCorrect ? '¡Correcto!' : 'Incorrecto'}}</div> }
    @if (store.answer()?.state === 'EXPIRED') { <div aria-live="assertive">Tiempo expirado</div> }
  </main>

  <footer style="grid-area:footer; display:flex; gap:var(--space-3); flex-wrap:wrap; align-items:center;">
    <span>Current Score: {{store.score().totalPoints}} pts</span>
    <span>· {{store.securedPoints().securedPoints}} asegurados</span>
    @if (store.securedPoints().checkpointRoundNumber) { <span>checkpoint ronda {{store.securedPoints().checkpointRoundNumber}}</span> }
    <span class="badge">{{store.securedPoints().policy}}</span>
    <span>Potential Reward: {{potentialReward() ?? '—'}}</span>
    <span>Player Status: {{store.status().playerStatus}}</span>
    <button (click)="openWithdraw()" [disabled]="store.isTerminal()" style="min-height:44px;" aria-label="Retirarse">Withdrawal Action</button>
  </footer>
</div>

<!-- states -->
@if (store.ui().isLoading) { <app-loading-skeleton /> }
@if (store.status().isTerminal) { <div aria-live="assertive">Participación terminada</div> }
@if (store.ui().error) { <app-error-state [message]="store.ui().error!.detail" [correlationId]="store.ui().error!.correlationId" /> }
```

## Responsive

- **≥1024px**: 2 columnas Answers, Header flex row.
- **375px**: single column Answers, Header stacked, Footer chips stacked, no horizontal scroll, targets ≥44px.
- `axe` AA contrast via tokens, focus `outline:2px solid var(--color-primary)`, `aria-live="polite"` Timer/Score/Status `assertive` for EXPIRED/terminal.

## Withdrawal Modal

```html
@if (showWithdrawConfirm) {
  <div role="dialog" aria-modal="true" aria-label="Confirmar retiro">
    <p>¿Confirmar retiro? Perderás puntos no asegurados según {{store.securedPoints().policy}}</p>
    <button (click)="confirmWithdraw()" style="min-height:44px;">Confirmar</button>
    <button (click)="showWithdrawConfirm=false" style="min-height:44px;">Cancelar</button>
  </div>
}
```

## Interceptors / Realtime

- `GameRealtimeService` `withAutomaticReconnect [0,2000,5000,10000,30000]` `QuestionAvailable/ScoreUpdated/RoundCompleted/GameFinished/Reconnected` → `store.hydrateFor(gameId)` (never trust event payload for Score/isCorrect).
- `interval(1000)` → `_now` `computed remainingSeconds` + `serverNow` correction on hydrate.
