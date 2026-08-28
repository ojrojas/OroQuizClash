# Screen: Game Lobby

**Route**: `/lobby/:gameId` | **Theme**: player dark cinematic

## Layout

- Game header: name, category Badge, difficulty Badge, reward pool (accent-text)
- Players joined list (avatars + names, count x/MaxPlayers)
- Countdown to start (Timer default variant, large)
- Rules summary Card (rounds, questions/round, time limit, withdraw policy)
- CTA: "Ready" (primary) → waiting state; Leave (ghost)

## Components

Card, Badge, Button, Timer, Progress (players filling), Toast

## States

- Loading (skeleton), Ready (waiting room), Full (join disabled + message), Started (auto-transition to game screen via GLOBAL `GameStarted`), Error (retry), Cancelled (info + exit CTA)

## Tokens Used

`--color-card`, `--color-accent-text` (reward pool), `--radius-xl`, `--space-6/8`, `--motion-fade`

## Realtime

GLOBAL `GameStarted` → route transition; player join/leave list updates (public info).

## A11y

Countdown `aria-live=off` until <10s then polite; player list `aria-label="Players joined: 3 of 8"`.

## Responsive

375: stacked centered; 1024+: centered card max-width 640 with side rules panel.
