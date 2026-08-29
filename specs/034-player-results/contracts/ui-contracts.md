# UI Contracts: Player Results (034)

**Branch**: `034-player-results` | **Date**: 2026-08-29 | **Spec**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)

4 pantallas finales en `ResultComponent` `route /player/game/:gameId/result` standalone `app-result` con `PlayerGameStore` per `sub` + `Leaderboard` `Rank`.

## 1. ResultComponent

**Selector**: `app-result`
**Route**: `/player/game/:gameId/result` `canActivate: [authGuard, mustChangePasswordGuard]`
**Store**: `PlayerGameStore` `providers: [PlayerGameStore]` + `Leaderboard` via `GamesApi.getLeaderboard()` + `getMyState`.

### Template (`signal` + control flow, `data-theme="player"`)

```html
<div class="result" data-theme="player">
  @if (isLoading()) {
    <app-loading-skeleton [rows]="3" aria-live="polite" aria-busy="true" />
  } @else if (error()) {
    <app-error-state [message]="error()!.detail" [correlationId]="error()!.correlationId" [traceId]="error()!.traceId" (retry)="hydrate()" />
  } @else if (resultState() === 'playing') {
    <app-error-state [message]="'Partida aún en curso'" [correlationId]="correlationId() ?? ''" (retry)="navigateToGame()" />
  } @else if (resultState() === 'won') {
    <div class="you-won" role="status" aria-live="assertive" aria-label="Felicidades, YOU WON, puesto 1">
      <h1>YOU WON</h1>
      <div class="final-score" role="status" aria-live="assertive">Final Score {{ finalScore() }} pts</div>
      @if (prize()) { <div class="prize" role="status" aria-live="polite">Prize {{ prize()!.name }}</div> }
    </div>
  } @else if (resultState() === 'walked') {
    <div class="you-walked-away" role="status" aria-live="assertive" aria-label="YOU WALKED AWAY">
      <h1>YOU WALKED AWAY</h1>
      <div class="secured" role="status" aria-live="polite">Secured Points {{ formatSecured() }}</div>
      <div class="available-rewards" role="list" aria-label="Available Rewards" aria-live="polite">
        @for (r of availableRewards(); track r.rewardId) {
          <div role="listitem">{{ r.name }} {{ r.pointsRequired }} pts</div>
        } @empty {
          <span>Sin recompensas disponibles</span>
        }
      </div>
    </div>
  } @else if (resultState() === 'over') {
    <div class="game-over" role="status" aria-live="assertive" aria-label="GAME OVER">
      <h1>GAME OVER</h1>
      <div class="final-score" role="status" aria-live="assertive">Final Score {{ finalScore() }} pts</div>
      @if (consolation()) { <div class="consolation" role="status" aria-live="polite">Consolation Reward {{ consolation()!.name }}</div> } @else { <span>Sin consolación</span> }
    </div>
  } @else if (resultState() === 'finished') {
    <div class="game-finished" role="status" aria-live="assertive" [attr.aria-label]="'GAME FINISHED puesto ' + finalPosition() + ' de ' + totalPlayers()">
      <h1>GAME FINISHED</h1>
      <div class="final-position" role="status" aria-live="polite">Final Position {{ finalPosition() }} de {{ totalPlayers() }}</div>
      <div class="final-score" role="status" aria-live="assertive">Final Score {{ finalScore() }} pts</div>
      @if (reward()) { <div class="reward" role="status" aria-live="polite">Reward {{ reward()!.name }}</div> } @else { <span>Sin recompensa</span> }
    </div>
  }
  <button (click)="goLobby()" aria-label="Volver al lobby" style="min-height:44px; min-width:44px;">Volver al lobby</button>
</div>
```

- `resultState()` computed per `PlayerStatus` + `GameStatus` + `Rank` per `sub` (won/walked/over/finished/playing).
- `finalScore = store.score().totalPoints` autoritativo ledger.
- `finalPosition = leaderboard().find(e=>e.playerId==sub)?.position`.
- `prize/reward/consolation/availableRewards` per `RewardRules`/`ConsolationPolicy` filtrable.
- `playing` → `ErrorState` "Partida aún en curso" + `retry` `navigateToGame()` `router.navigate(['/player/game', gameId])`.

### CSS (tokens `data-theme="player"`, responsive, `prefers-reduced-motion`)

```css
.result { display:flex; flex-direction:column; gap:var(--space-4); max-width:600px; margin:auto; min-height:100vh; padding:var(--space-4); }
.you-won { background:var(--color-success); color:var(--color-success-contrast); border-radius:var(--radius-lg); padding:var(--space-6); text-align:center; animation: confetti 600ms ease; }
.you-walked-away { background:var(--color-warning); color:var(--color-warning-contrast); border-radius:var(--radius-lg); padding:var(--space-6); text-align:center; }
.game-over { background:var(--color-destructive); color:var(--color-on-destructive); border-radius:var(--radius-lg); padding:var(--space-6); text-align:center; }
.game-finished { background:var(--color-accent); color:var(--color-on-accent); border-radius:var(--radius-lg); padding:var(--space-6); text-align:center; }
.final-score { font-size:var(--font-size-lg); font-weight:700; }
@media (prefers-reduced-motion: reduce) { .you-won, .pulse { animation:none; } }
@keyframes confetti { 0%{transform:scale(0.9);opacity:0}100%{transform:scale(1);opacity:1} }
```

- 0 literales hardcodeados; todos `var(--*)` de `design-system/tokens/design-tokens.css`.
- 1 col 375px, 2 col ≥768px si `Available Rewards` lista, sin scroll horizontal, `gap var(--space-3)`, `min-height 44px`.

### Interaction Details

- `ngOnInit` `hydrate()` `GET /players/me` per `sub` + `GET /leaderboard` Rank per `sub` → `computed resultState`.
- `GameRealtimeService GameFinished` → `hydrate()` per `sub`.
- `goLobby()` `router.navigate(['/player/lobby'])`.
- `navigateToGame()` `router.navigate(['/player/game', gameId])` si `playing`.

## 2. Integration en app.routes.ts

`app.routes.ts` `path: 'player/game/:gameId/result'` `component: ResultComponent` `canActivate: [authGuard, mustChangePasswordGuard]` ya placeholder en 027, ahora extendido con 4 pantallas.

## 3. States & A11y

- `loading` skeleton `aria-busy` `aria-live polite`.
- `ErrorState` con `CorrelationId` + `Retry` per `GET /players/me`/`GET /leaderboard`.
- `playing` `ErrorState` "Partida aún en curso" + `Retry` navega a `/player/game/:id`.
- `you-won` `aria-live assertive` `confetti` `prefers-reduced-motion` none.
- `you-walked-away` `Secured` `availableRewards` `role="list"` `aria-live polite`.
- `game-over` `Final Score` + `Consolation` "Sin consolación" si null.
- `game-finished` `Final Position` `aria-label` "Puesto X de N" `Final Score` + `Reward` "Sin recompensa" si null.
- `role="status"` por pantalla + `h1` título + `aria-label` descriptivo.
```

## References

- SPEC-029 `contracts/ui-contracts.md` (GameComponent 10 elementos) + SPEC-032 scoring + SPEC-033 multiplayer
- `design-system/MASTER.md` + `overrides/player.md` + `tokens/design-tokens.css` (data-theme="player", cinematic premium, WCAG)
- `src/Player/QuizArena.Player/features/result/result.component.ts` placeholder + `stores/player-game.store.ts` `Score/SecuredPoints/Game/Leaderboard` + `features/shared/games.api.ts` `getMyState/getLeaderboard`
