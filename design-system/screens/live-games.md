# Screen: Live Games

**Route**: `/admin/live` | **Theme**: administration | **Roles**: ADMIN, GAME_MANAGER

## Layout

- Toolbar: status Tabs (Live/Upcoming/Finished), search Input, auto-refresh indicator Badge `Live` pulse
- Table comfortable: Game, Category, Players (active/total), Round x/y, Status Badge, Started at, Actions (view detail, stop)
- Row expand → mini leaderboard snapshot (read-only, aggregated — no private player answers shown §11)
- Pagination + sticky header

## Components

Table (comfortable, density toggle), Tabs, Input, Badge, Button, Drawer (game detail), Modal (stop confirm), Toast

## States

- Loading: skeleton rows
- Ready: live data
- Empty: "No live games right now" + link to schedule
- Error: reconnect banner (SignalR reconnecting → Badge `Reconnecting`)
- Stop action: destructive confirm Modal with impact text

## Tokens Used

`--color-card`, `--color-success` (Live), `--color-warning` (Reconnecting), `--space-3/4`, `--radius-md`

## Realtime Note

GLOBAL events only: `GameStarted`, `RoundStarted`, `RoundCompleted`, `GameFinished` update rows in place (fade 200). PLAYER-SPECIFIC events NOT consumed (private session isolation FR-025/026).

## Responsive

375: card list (game + status + players); 1024+: comfortable table, sticky header, density toggle persisted.
