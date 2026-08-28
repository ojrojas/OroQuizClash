# Component: Leaderboard

## Anatomy
- Slots: `header (title + collapse toggle)`, `rows (rank, name, score)`, `you-row (highlighted, pinned)`, `footer (updated indicator)`.

## Variants & Sizes
- Variants: `full` (results/admin) | `mini` (game-screen rail) | `collapsible` (@375)
- Rows: top 10 + pinned you-row if outside top 10.

## States (Addendum 2 §9)
- Loading (skeleton rows), Ready, Empty (hidden until first score), Updating (row reorder fade)
- Game-screen: shows public aggregates only — never individual answers (privacy §11).

## Props (conceptual)
- Leaderboard { entries: {rank, name, score, isYou}[], collapsed?: bool, live?: bool }

## Tokens Used
- `--color-card`, `--color-border`, `--color-accent-text` (rank 1), `--color-primary` tint (you-row), `--space-2/3`, `--radius-lg`, `--typography-label-m` (tabular-nums scores)

## A11y
- Semantic `<table aria-label="Leaderboard">` or list with `aria-label`; you-row announced "Your position: 4th"; updates `aria-live=off` (too frequent) — on-demand refresh button for SR.

## Responsive
- 375: collapsible bottom sheet (peek top 3); 768: collapsible side; 1024/1440: fixed right rail 4-col.

## Motion
- preset: row reorder slide 300; collapse slide 300; reduced: fade 200 / instant.
