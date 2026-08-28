# Screen: Player Home

**Route**: `/` | **Theme**: player dark cinematic

## Layout

- Hero: display heading (Russo One) + subtext + primary CTA "Play now" (accent)
- Quick stats row: Secured points, Games played, Best streak (Cards)
- Featured games carousel (Cards, pause on hover/focus/reduced-motion, arrows + dots keyboard)
- Bottom nav (mobile): Home / Lobby / Rewards / Profile

## Components

Button, Card, Badge, Tabs (game filters)

## States

Loading (skeleton hero+cards), Ready, Empty (no games scheduled → CTA lobby), Error (retry banner)

## Tokens Used

`--typography-display-size`, `--color-accent` CTA, `--space-8/12` spacious, `--radius-2xl`, `--elevation-3`

## Realtime

GLOBAL `GameStarted` → "Live now" Badge on featured card.

## Responsive

375: stacked, bottom nav; 768: centered 640; 1024/1440: hero 2-col (text + featured), no bottom nav (top links).
