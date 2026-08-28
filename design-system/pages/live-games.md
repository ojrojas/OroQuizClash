# Page Override: live-games

> Overrides `design-system/MASTER.md` for `/admin/live`. Only deviations listed.

- Default density `comfortable` (not dense) — live rows need scanning room; toggle still available.
- Row in-place updates on GLOBAL events use fade 200 highlight (bg tint → transparent 800ms) — deviation: motion marks realtime change.
- `Reconnecting` state: warning banner + rows frozen with `aria-busy` — no stale-data interaction.
- Expanded row leaderboard shows aggregates only (rank, name, score) — never individual answers (privacy §11).
- Stop-game confirm Modal includes live player count impact text.
