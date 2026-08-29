# UI Contracts: Player Withdrawal (035)

**Branch**: `035-player-withdrawal` | **Date**: 2026-08-29 | **Spec**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)

Flujo `Withdrawal Action` → diálogo modal 3 métricas + 2 warnings → `Confirmar` `POST /withdraw` `X-Idempotency-Key` → `PlayerWithdrawn` `isTerminal` en `GameComponent` footer + `ScorePanel`.

## 1. GameComponent Withdrawal Action + Dialog

**Store**: `PlayerGameStore` `withdraw()` `rxMethod` `X-Idempotency-Key` `idemp-withdraw-{gameId}` scoped `providers: [PlayerGameStore]` en `GameComponent`.

### Template (`signal` + control flow, `data-theme="player"`)

```html
<div class="game-cinematic" data-theme="player">
  <!-- Header: Current Round/Level/Timer + Leaderboard -->
  <!-- Sidebar: Ladder + Players -->
  <!-- Center: Question + Answers -->
  <footer class="footer-competitive">
    <app-score-panel />
    <div class="secured">Secured Points: {{ store.securedPoints().securedPoints }} @if (checkpoint) { checkpoint {{ checkpoint }} } </div>
    <button type="button"
            class="withdrawal-action"
            [disabled]="store.isTerminal() || !store.status().canAnswer"
            (click)="openWithdraw()"
            aria-label="Retirarse"
            style="min-height:44px; min-width:44px;">
      Withdrawal Action
    </button>
  </footer>

  @if (showWithdrawConfirm) {
    <div role="dialog" aria-modal="true" aria-label="Confirmar retiro"
         style="position:fixed; inset:0; background:rgba(0,0,0,0.5); display:flex; align-items:center; justify-content:center;"
         (click)="showWithdrawConfirm=false">
      <div class="dialog" role="document" style="background:var(--color-surface); padding:var(--space-6); border-radius:var(--radius-lg); max-width:400px; display:flex; flex-direction:column; gap:var(--space-3);"
           (click)="$event.stopPropagation()">
        <h2>Confirmar retiro</h2>
        <div class="metrics" role="group" aria-label="Puntuaciones">
          <div>Current Points {{ store.score().totalPoints }} pts</div>
          <div>Secured Points {{ formatSecured() }}</div>
          <div>Potential Points {{ store.potentialReward() }}</div>
        </div>
        <div role="alert" aria-live="assertive" class="warning">
          If you continue and answer incorrectly, you may lose your accumulated points.
        </div>
        <div role="alert" aria-live="assertive" class="withdraw-secure">
          Withdraw now and secure {{ store.securedPoints().securedPoints }} points?
        </div>
        <div class="actions" style="display:flex; gap:var(--space-3); justify-content:flex-end;">
          <button type="button" (click)="confirmWithdraw()" style="min-height:44px; min-width:44px;" aria-label="Confirmar retiro">Confirmar</button>
          <button type="button" (click)="showWithdrawConfirm=false" style="min-height:44px; min-width:44px;" aria-label="Cancelar">Cancelar</button>
        </div>
        @if (store.ui().error) {
          <app-error-state [message]="store.ui().error!.detail" [correlationId]="store.ui().error!.correlationId" [traceId]="store.ui().error!.traceId" (retry)="confirmWithdraw()" />
        }
      </div>
    </div>
  }
</div>
```

- `Withdrawal Action` `disabled` si `isTerminal` (WITHDRAWN/ELIMINATED/FINISHED) o `!canAnswer`.
- Diálogo `role="dialog"` `aria-modal="true"` `aria-label="Confirmar retiro"` con 3 métricas `Current/Secured/Potential` + 2 warnings `role="alert"` `aria-live assertive` exactos, `Confirmar` `aria-label="Confirmar retiro"` `min-height:44px`, `Cancelar` `Esc`/`Enter` cierra sin llamada, click fuera `backdrop` cierra.
- `formatSecured()` → `"{n} pts · checkpoint {m}"` o `"{n} pts"` si `checkpoint null`.
- `Potential` "—" si no configurado.

### CSS (tokens `data-theme="player"`, responsive, `prefers-reduced-motion`)

```css
.withdrawal-action { min-height:44px; min-width:44px; padding:var(--space-3) var(--space-4); border-radius:var(--radius-md); background:var(--color-destructive); color:var(--color-on-destructive); }
.dialog { max-width:400px; background:var(--color-surface); border:1px solid var(--color-border); border-radius:var(--radius-lg); padding:var(--space-6); gap:var(--space-3); }
.metrics { display:flex; flex-direction:column; gap:var(--space-2); }
.warning, .withdraw-secure { color:var(--color-destructive); font-weight:600; }
@media (prefers-reduced-motion: reduce) { .dialog { animation:none; } }
```

- 0 literales hardcodeados; todos `var(--*)` de `design-system/tokens/design-tokens.css`.
- 1 col 375px diálogo centrado `max-width:400px`, sin scroll horizontal, `gap var(--space-3)`, `min-height 44px`.

### Interaction Details

- `openWithdraw()` → `showWithdrawConfirm=true`.
- `confirmWithdraw()` → `sessionStorage idemp-withdraw-{gameId} ?? crypto.randomUUID()` → `store.withdraw()` `POST /withdraw` `X-Idempotency-Key` `X-Correlation-Id` `Authorization Bearer`.
- `store.withdraw` `rxMethod` → `patchState({gameSession, status: WITHDRAWN isTerminal true canAnswer false})` + `PointTransaction WITHDRAWAL` ledger `Current=Secured` si `KEEP_SECURED_SCORE`.
- Idempotente: segunda `confirmWithdraw` misma `X-Idempotency-Key` → mismo `GameSession` sin nuevo ledger; `PlayerAlreadyWithdrawn` 403 si distinto key pero ya WITHDRAWN.
- `isTerminal true` → `QuestionComponent` `aria-disabled` + `Withdrawal Action` `disabled`.

## 2. Integration en GameComponent (029/032/033)

`GameComponent` footer ya en 029 con `Withdrawal Action` `showWithdrawConfirm` boolean `withdraw()` + `ScorePanel` 5 métricas + `Timer` + `Leaderboard` (033).

## 3. States & A11y

- `Withdrawal Action` `disabled` si `isTerminal` o `!canAnswer` `aria-disabled`.
- Diálogo `role="dialog"` `aria-modal="true"` `aria-label="Confirmar retiro"` `role="alert"` warnings `aria-live assertive` `role="group"` métricas `aria-live polite`.
- `Confirmar`/`Cancelar` `min-height:44px` `min-width:44px` foco `outline:2px solid var(--color-primary)` `Tab`/`Escape`/`Enter`.
- `ErrorState` `ProblemDetails` `CorrelationId/TraceId` `Retry` reusa misma `X-Idempotency-Key`.
- `PlayerWithdrawn` `status WITHDRAWN` `isTerminal true` `canAnswer false` `aria-disabled` `WITHDRAWAL` ledger.

## References

- SPEC-029 `contracts/ui-contracts.md` (GameComponent Withdrawal Action `showWithdrawConfirm`) + SPEC-032 scoring
- `design-system/MASTER.md` + `overrides/player.md` + `tokens/design-tokens.css` (data-theme="player", cinematic premium, WCAG)
- `src/Player/QuizArena.Player/features/game/withdrawal.component.ts` `stores/player-game.store.ts` `withdraw()` `features/shared/games.api.ts` `withdraw()` `core/realtime/game-realtime.service.ts` `GameFinished`
