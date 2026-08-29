# UI Contracts: Player Scoring (032)

**Branch**: `032-player-scoring` | **Date**: 2026-08-29 | **Spec**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)

Bloque de 5 métricas en `GameComponent` footer competitivo junto a ladder Round 1..N (030) y center `Question` (031), reutiliza `ScorePanelComponent` base (029) y `PlayerGameStore` 10 elementos.

## 1. ScorePanelComponent

**Selector**: `app-score-panel`
**Store**: `PlayerGameStore` scoped `providers: [PlayerGameStore]` en `GameComponent`.

### Template (`signal` + control flow, `data-theme="player"`)

```html
<div class="score-panel" data-theme="player">
  @if (store.ui().isLoading) {
    <app-loading-skeleton [rows]="5" aria-live="polite" aria-busy="true" />
  } @else if (store.ui().error) {
    <app-error-state [message]="store.ui().error!.detail" [correlationId]="store.ui().error!.correlationId" [traceId]="store.ui().error!.traceId" (retry)="store.hydrateFor(gameId)" />
  } @else {
    <div class="scoring-grid" role="group" aria-label="Puntuaciones">
      <div class="metric current" role="status" aria-live="polite" [attr.aria-label]="'Current Points ' + store.score().totalPoints + ' puntos'">
        <span class="label">Current Points</span>
        <span class="value pulse" [class.pulse]="store.isScorePulse()">{{ store.score().totalPoints }} pts</span>
      </div>
      <div class="metric secured" role="status" aria-live="polite" [attr.aria-label]="'Secured Points ' + store.securedPoints().securedPoints + (store.securedPoints().checkpointRoundNumber ? ' checkpoint ronda ' + store.securedPoints().checkpointRoundNumber : '')">
        <span class="label">Secured Points</span>
        <span class="value">
          {{ store.securedPoints().securedPoints }} pts
          @if (store.securedPoints().checkpointRoundNumber) { <span class="badge">· checkpoint {{ store.securedPoints().checkpointRoundNumber }}</span> }
          @if (store.isSecured()) { <span class="badge asegurado">asegurado</span> }
        </span>
      </div>
      <div class="metric potential" role="status" aria-live="polite" [attr.aria-label]="store.potentialReward() === '—' ? 'Potential no disponible' : 'Potential Points ' + store.potentialReward()">
        <span class="label">Potential Points</span>
        <span class="value">{{ store.potentialReward() }}</span>
      </div>
      <div class="metric round" role="status" aria-live="polite" [attr.aria-label]="'Round Points ' + roundPoints() + ' en juego'">
        <span class="label">Round Points</span>
        <span class="value">{{ roundPoints() }} pts <small>en juego</small></span>
      </div>
      <div class="metric total" role="status" aria-live="polite" [attr.aria-label]="'Total Points ' + store.score().totalPoints + ' puntos'">
        <span class="label">Total Points</span>
        <span class="value total-bold">{{ store.score().totalPoints }} pts</span>
      </div>
    </div>
  }
</div>
```

- `Current Points` = `store.score().totalPoints` (o `currentPoints` si se expone).
- `Secured Points` = `store.securedPoints().securedPoints` + `checkpointRoundNumber` (null → sin badge, SC-004).
- `Potential Points` = `store.potentialReward()` (029 computed `Potential Reward` próximo premio o "—", SC-005).
- `Round Points` = `store.score().roundPoints ?? (store.score().totalPoints - store.securedPoints().securedPoints)` o `score().totalPoints` derivado; 0 si `RoundCompleted`.
- `Total Points` = `store.score().totalPoints` (server `sum(PointTransaction)`), no `Current+Secured` cliente.

### CSS (tokens `data-theme="player"`, responsive, `prefers-reduced-motion`)

```css
.score-panel { display:flex; flex-direction:column; gap:var(--space-3); }
.scoring-grid { display:grid; grid-template-columns:1fr; gap:var(--space-3); }
@media (min-width:768px) { .scoring-grid { grid-template-columns:repeat(5,1fr); } }
.metric { display:flex; flex-direction:column; gap:var(--space-1); padding:var(--space-3) var(--space-4); min-height:44px; min-width:44px; border-radius:var(--radius-md); border:1px solid var(--color-border); background:var(--color-surface); }
.metric.current .value { color:var(--color-primary); font-weight:700; }
.metric.secured .value { color:var(--color-success); }
.metric.round .value { color:var(--color-warning); }
.metric.total .value.total-bold { color:var(--color-primary); font-weight:700; font-size:var(--font-size-lg); }
.metric.potential .value { color:var(--color-accent); }
.badge.asegurado { background:var(--color-success-subtle); color:var(--color-success); padding:var(--space-1) var(--space-2); border-radius:var(--radius-sm); font-size:var(--font-size-sm); }
.pulse { animation: pulse 600ms ease; }
@media (prefers-reduced-motion: reduce) { .pulse { animation:none; } }
@keyframes pulse { 0%,100% { opacity:1; } 50% { opacity:0.8; } }
```

- 0 literales hardcodeados; todos `var(--*)` de `design-system/tokens/design-tokens.css`.
- 1 col 375px, 5 col ≥768px (o 4 col si `Potential` "—"), sin scroll horizontal, `gap var(--space-3)`, `min-height 44px`.

### Interaction Details

- `hydrate()` (llamado en `GameComponent ngOnInit` + `GameRealtimeService ScoreUpdated/RoundCompleted/Reconnected`): `GET /players/me` → patch `score/securedPoints/game`.
- `isScorePulse` computed `true` tras `ScoreUpdated` 600ms luego `false` (pulse).
- Error 401 → `silentRenew`; 500 → `ErrorState` `Retry`; 429 `Retry-After`.

## 2. Integration en GameComponent (029/030/031)

`GameComponent` `grid 280px 1fr` (030) + center `QuestionComponent` (031) + footer `ScorePanelComponent` ya en 029: `game.component.ts` `providers: [PlayerGameStore, PlayerRoundsStore]` `ngOnInit` → `store.hydrateFor(gameId)` + `store.bindRealtime(gameId)` ya incluye `ScoreUpdated`.

## 3. States & A11y

- `loading` skeletons 5 métricas `aria-busy`; `ErrorState` con `CorrelationId` + `Retry`; `Empty` 0 pts.
- `aria-live="polite"` por métrica + `Current/Total` `pulse` anim, `Secured` badge `asegurado`, `Round` "en juego", `Potential` "—" `aria-label` "Potential no disponible".
- `role="group" aria-label="Puntuaciones"` + métricas `role="status"`.
- `prefers-reduced-motion: reduce` deshabilita `pulse`.

## References

- SPEC-029 `contracts/ui-contracts.md` (GameComponent 10 elementos, ScorePanel) + SPEC-030 ladder
- `design-system/MASTER.md` + `overrides/player.md` + `tokens/design-tokens.css` (data-theme="player", cinematic premium, WCAG)
- `src/Player/QuizArena.Player/features/game/score-panel.component.ts` + `stores/player-game.store.ts` + `features/shared/games.api.ts` `getMyState`
