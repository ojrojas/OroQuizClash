# UI Contracts: Player Multiplayer (033)

**Branch**: `033-player-multiplayer` | **Date**: 2026-08-29 | **Spec**: [spec.md](../spec.md) | **Plan**: [plan.md](../plan.md)

Bloque multiplayer en `GameComponent` header/sidebar junto a ladder Round 1..N (030) y center `Question` (031) y footer scoring 032, reutiliza `PlayerGameStore` scoped + `LeaderboardComponent`.

## 1. Isolation Contract: 5 privados per Store scoped

**Store**: `PlayerGameStore` scoped `providers: [PlayerGameStore]` per `GameComponent` instancia (no `providedIn: 'root'`).

- `GameComponent` `providers: [PlayerGameStore, PlayerRoundsStore]` ya en 029 scoped per `GameComponent`.
- Test `isolation.spec.ts` crea 4 `TestBed` configuraciones A-D con `provideHttpClientTesting` mock `getMyState` per `sub` con `score 100 vs 250` y `answer opt-A vs opt-C`, verifica `storeA.answer().selectedOptionId !== storeB.answer().selectedOptionId` sin contaminación.

```ts
// isolation.spec.ts pseudo
const storeA = TestBed.inject(PlayerGameStore); // with GamesApi mock returning sub=A data
const storeB = TestBed.inject(PlayerGameStore); // separate TestBed per browser, but concept
expect(storeA.answer().selectedOptionId).toBe('opt-A');
expect(storeB.answer().selectedOptionId).toBe('opt-C');
```

- `GameRealtimeService` per `gameId` con `accessTokenFactory` per `sub` cada instancia tiene `HubConnection` con `?gameId` + `Authorization: Bearer` per `sub`.

## 2. Public Components: Players / Players Remaining / Leaderboard / Current Round

**Selector**: `app-leaderboard` (opcional) o directo en `GameComponent` header.

**Store**: `PlayerGameStore` para `Current Round` público + `Leaderboard` público via `GamesApi.getLeaderboard()` separado o `getMyState` genérico + `getPlayers`.

### Template (`signal` + control flow, `data-theme="player"`)

```html
<div class="multiplayer-public" data-theme="player">
  <div class="players" role="list" aria-label="Jugadores">
    @for (p of players(); track p.playerId) {
      <div role="listitem" [attr.aria-label]="p.displayName + ' ' + p.status">
        <span>{{ p.displayName }}</span>
        <span class="badge">{{ p.status }}</span>
      </div>
    }
  </div>
  <div class="players-remaining" role="status" aria-live="polite" aria-label="Players Remaining {{ playersRemaining() }}">
    Players Remaining: {{ playersRemaining() }}
  </div>
  <div class="leaderboard" role="list" aria-label="Leaderboard" aria-live="polite">
    @for (entry of leaderboard(); track entry.playerId; let i=$index) {
      <div role="listitem" [attr.aria-posinset]="i+1" [attr.aria-setsize]="leaderboard().length">
        <span class="position">{{ i+1 }}.</span>
        <span class="name">{{ entry.displayName }}</span>
        <span class="level">{{ entry.level }}</span>
        <span class="points">{{ entry.totalPoints }} pts</span>
      </div>
    }
  </div>
  <div class="current-round" role="status" aria-live="polite" [attr.aria-label]="'Current Round ' + currentRoundNumber() + ' de ' + maxRounds()">
    Ronda {{ currentRoundNumber() }}/{{ maxRounds() }}
  </div>
</div>
```

- `Players` `role="list"` con `displayName` + `status` sin `Answer/Score` privado.
- `Players Remaining` `role="status"` `aria-live polite` count `IsActive`.
- `Leaderboard` `role="list"` con `totalPoints/level` sin `SelectedOptionId/isCorrect/Timer/Secured`.
- `Current Round` `role="status"` `aria-live polite` "Ronda 3/10".

### CSS (tokens `data-theme="player"`, responsive, `prefers-reduced-motion`)

```css
.multiplayer-public { display:flex; flex-direction:column; gap:var(--space-3); }
.players { display:flex; flex-wrap:wrap; gap:var(--space-2); }
.leaderboard { display:grid; grid-template-columns:1fr; gap:var(--space-2); }
@media (min-width:768px) { .leaderboard { grid-template-columns:repeat(4,1fr); } }
.metric { min-height:44px; min-width:44px; padding:var(--space-3) var(--space-4); border:1px solid var(--color-border); background:var(--color-surface); border-radius:var(--radius-md); }
@media (prefers-reduced-motion: reduce) { * { animation:none; } }
```

- 0 literales hardcodeados; todos `var(--*)` de `design-system/tokens/design-tokens.css`.
- 1 col 375px, 4 col ≥768px, sin scroll horizontal, `gap var(--space-3)`, `min-height 44px`.

### Interaction Details

- `Players`/`PlayersRemaining`/`Leaderboard`/`CurrentRound` públicos via `GamesApi.getPlayers/getLeaderboard/getGame` o `getMyState` genérico (`Game` + `Players` lista).
- `hydrate()` (llamado en `GameComponent ngOnInit` + `GameRealtimeService ScoreUpdated/LeaderboardUpdated/Reconnected`): `GET /players/me` privado per `sub` + `GET /leaderboard` público (si `LeaderboardComponent` separado) → patch `players/leaderboard/currentRound`.
- Error 401 → `silentRenew`; 500 → `ErrorState` `Retry` per `GET /players/me` / `GET /leaderboard`.

## 3. Private Display: 5 privados no en Leaderboard

- `ScorePanelComponent` 5 métricas `Current/Secured/Potential/Round/Total` `aria-live polite` solo del requester per `GET /players/me` privado (032), nunca `Answer` de otro en `Leaderboard`.
- `QuestionComponent` `Private Answer` `SelectedOptionId/isCorrect` solo per `sub` (031), no en `Leaderboard`.
- `TimerComponent` `Private Timer` per `sub` (029), no en `Leaderboard`.

## 4. Integration en GameComponent (029/030/031/032)

`GameComponent` `grid 280px 1fr` (030) + center `QuestionComponent` (031) + footer `ScorePanelComponent` (032) + header/sidebar `Leaderboard/Players/CurrentRound` públicos: `game.component.ts` `providers: [PlayerGameStore, PlayerRoundsStore]` `ngOnInit` → `store.hydrateFor(gameId)` privado per `sub` + `leaderboardStore.hydrateLeaderboard(gameId)` público.

## 5. States & A11y

- `loading` skeletons `Players/Leaderboard/CurrentRound` `aria-busy`.
- `ErrorState` con `CorrelationId` + `Retry`.
- `aria-live="polite"` por `Players Remaining`/`Leaderboard`/`Current Round` + `Current/Total Points` etc.
- `role="list"` `aria-posinset`/`aria-setsize` para `Leaderboard`.
- `prefers-reduced-motion: reduce` deshabilita animaciones.

## References

- SPEC-011 `Multiplayer` base + SPEC-029 `contracts/ui-contracts.md` (GameComponent 10 elementos) + SPEC-032 scoring
- `design-system/MASTER.md` + `overrides/player.md` + `tokens/design-tokens.css` (data-theme="player", cinematic premium, WCAG)
- `src/Player/QuizArena.Player/features/game/leaderboard.component.ts` `stores/player-game.store.ts` scoped `features/shared/games.api.ts` `getMyState/getLeaderboard/getPlayers`
