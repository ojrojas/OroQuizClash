# Screen: Audit

**Route**: `/admin/audit` | **Theme**: administration | **Roles**: ADMIN (read-only for GAME_MANAGER)

## Layout

- Toolbar: date range Input (from/to), actor Select, action Select (GameCreated/QuestionPublished/RewardPaid/WithdrawalProcessed…), search Input
- Table comfortable (immutable log): Timestamp, Actor, Action Badge, Entity, Details (truncated, expand row), IP
- Pagination (50/100) + sticky header + CSV export
- Row expand → full JSON detail in code block (`Fira Code`)

## Components

Table (comfortable), Input, Select, Button, Badge (action type), Toast (export)

## States

- Loading: skeleton rows
- Ready: data
- Empty: "No audit entries match filters" + clear-filters CTA
- Error: retry banner
- Immutable: no edit/delete actions anywhere (SPEC-014)

## Tokens Used

`--color-card`, `--color-border`, `--color-muted`, `--space-3/4`, `--radius-md`, `--typography-font-heading` (code detail)

## A11y

Expandable rows keyboard operable (`aria-expanded`); timestamp in local TZ with `time` element; filter labels explicit.

## Realtime Note

None (historical log); append-only stream, refresh button.

## Responsive

375: card list (actor + action + time), detail full-screen sheet; 1024+: comfortable table sticky header.
