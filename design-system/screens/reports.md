# Screen: Reports

**Route**: `/admin/reports` | **Theme**: administration | **Roles**: ADMIN, GAME_MANAGER

## Layout

- Toolbar: period Select (7d/30d/90d/custom), category Select, Export Button (CSV)
- KPI row: Games played, Avg players/game, Total rewards paid, Completion rate
- Charts: Games over time (line), Rewards by category (bar), Difficulty distribution (donut)
- Table comfortable: per-game summary (Game, Date, Players, Winner count, Rewards paid) + pagination

## Components

Card (stat), Select, Button, Table (comfortable, density toggle), Tabs (Overview/Games/Rewards), Toast (export ready)

## States

- Loading: skeleton KPI + chart shimmer + table skeleton
- Ready: data
- Empty (period without data): "No data for this period" + adjust-period CTA
- Error: per-widget retry; export failure Toast error

## Tokens Used

`--color-card`, `--color-primary`, `--color-accent` (rewards), `--space-4/6`, `--radius-lg`, `--elevation-1`

## A11y

Charts have data-table fallback (screen reader); export button `aria-busy` while generating.

## Realtime Note

None (batch data, SPEC-015); manual refresh + period change refetch.

## Responsive

375: KPI 2×2, charts stacked, table→cards; 1024/1440: 12-col charts side-by-side + table.
