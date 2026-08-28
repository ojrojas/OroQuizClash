# Screen: Admin Dashboard

**Route**: `/admin` | **Theme**: administration | **Roles**: ADMIN, GAME_MANAGER

## Layout

- KPI row (4 stat Cards): Active games, Players online, Questions bank size, Pending reviews
- Charts row (2 Cards): Games per day (line), Reward payout trend (bar)
- Recent activity Table (dense, 5 rows) + Live games shortcut Badge `Live` pulse

## Components

Card (stat), Table (dense), Badge, Button (ghost link "View all"), Tabs (period 7d/30d/90d)

## States

- Loading: skeleton KPI + chart shimmer
- Ready: data
- Empty (new install): onboarding CTA card "Create your first game"
- Error: per-widget error card with retry (partial failure tolerated)

## Tokens Used

`--color-card`, `--color-primary`, `--space-4/6`, `--radius-lg`, `--elevation-1`, `--typography-display-size` (KPI numbers)

## Realtime Note

GLOBAL `GameStarted`/`GameFinished` update KPIs live (no reload); PLAYER-SPECIFIC never shown here.

## Responsive

375: KPI 2×2 grid, charts stacked; 768: KPI 4-across; 1024/1440: 12-col with charts side-by-side.
