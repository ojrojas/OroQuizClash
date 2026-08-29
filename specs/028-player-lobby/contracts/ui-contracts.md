# Contracts: UI for Player Lobby (028)

**Branch**: `028-player-lobby` | **Date**: 2026-08-28
Design System: `design-system/tokens/design-tokens.css` `data-theme="player"` + `overrides/player.md` SPEC-016.

## Layout

- **Route**: `/player/lobby` (or `/lobby` per `app.routes.ts` SPEC-027) `canActivate: [authGuard, mustChangePasswordGuard]`.
- **Header**: `Available Games` title + `Refresh` button (44px) + `Leave Lobby` link/button (aria-label="Salir del lobby").

## Available Games Table (≥1024px)

```html
<table aria-live="polite" aria-label="Available Games">
  <thead><tr>
    <th>Game Name</th><th>Category</th><th>Difficulty</th><th>Number of Rounds</th><th>Players</th><th>Start Time</th><th>Prize</th><th>Status</th><th>Actions</th>
  </tr></thead>
  <tbody>
    <tr *ngFor="let g of games">
      <td>{{g.name}}</td><td>{{g.categoryName}}</td><td>{{g.difficultyName}}</td><td>{{g.numberOfRoundsDisplay}}</td><td>{{g.players.display}}</td><td>{{g.startTime | date:'short'}}</td><td>{{g.prize}}</td><td><span class="badge">{{g.status}}</span></td>
      <td>
        <button (click)="view(g.gameId)" aria-label="Ver información {{g.name}}" style="min-height:44px">View</button>
        <button (click)="join(g.gameId)" [disabled]="g.players.current>=g.players.max" aria-label="Unirse a {{g.name}}" style="min-height:44px">Join Game</button>
      </td>
    </tr>
  </tbody>
</table>
<paginator [totalCount]="totalCount" [page]="page" [pageSize]="pageSize" (pageChange)="load($event)" />
```

## Cards (≤768px, 375px)

Same 8 fields stacked:
```html
<div class="cards" aria-live="polite">
  @for (g of games; track g.gameId) {
    <article class="card">
      <h3>{{g.name}}</h3>
      <dl><dt>Category</dt><dd>{{g.categoryName}}</dd> ... 8 fields</dl>
      <button (click)="view(g.gameId)">View Game Information</button>
      <button (click)="join(g.gameId)">Join Game</button>
    </article>
  }
</div>
```

## States

- **Loading**: `<app-loading-skeleton [rows]="3" />` `role="status"` `aria-live="polite"`.
- **Empty**: `<app-empty-state message="No hay partidas disponibles" />` + `Refresh` button.
- **Error**: `<app-error-state [message]="detail" [correlationId]="correlationId" [traceId]="traceId" (retry)="load()" />` `role="alert"` `aria-live="assertive"`.
- **Ready**: table/cards above.

## Actions

- **View Game Information**: `router.navigate(['/player/lobby', g.gameId])` or `dialog.open(GameDetailComponent, { data: g.gameId })` → `GET /api/games/{id}` detail with extended config + playersList. No Answer/Score.
- **Join Game**: `sessionStorage.setItem('idemp-join-{gameId}', crypto.randomUUID())` if absent; `POST /api/games/{id}/players` with `X-Idempotency-Key`; on 200 `router.navigate(['/player/game', gameId])`; on 400/409 show `ProblemDetails.detail` + `CorrelationId` in `ErrorState` with `Volver al lobby`.
- **Leave Lobby**: `(click)="router.navigate(['/'])"` – no fetch.

## A11y / Tokens

- `data-theme="player"` on shell, styles via `design-tokens.css` no literals, `styles` already in `angular.json`.
- `aria-live="polite"` for list, `assertive` for Error, `role="radiogroup"` not needed (lobby has no radio). Keyboard `Tab`→ rows/buttons `Enter`→ action, focus visible `outline:2px solid var(--color-primary)`.
- Targets ≥44px, responsive `375–1536` no horizontal scroll, contrast AA verified by `design-system`.
- `X-Correlation-Id` per request via interceptor; display `CorrelationId/TraceId` in ErrorState small monospace.
