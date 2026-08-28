# Screen: Game Results

**Route**: `/results/:gameId` | **Theme**: player dark cinematic

## Layout

- Outcome hero: `Winner` (accent celebration ≤600ms) | `Eliminated` | `Consolation` | `Withdrawn`
- Final score Card: points earned, secured points, correct/total, best streak
- Leaderboard final (top 10, your rank highlighted)
- Reward status Card: `PlayerRewardAvailable` → "Claim" CTA or pending Badge
- Actions: Play again (primary), Home (ghost)

## Components

Card, Badge, Leaderboard, Button, Toast

## States

Loading (skeleton), Ready, Reward pending (Badge + spinner), Reward paid (success Badge), Error (retry)

## Tokens Used

`--typography-display-size` (outcome), `--color-accent-text` (reward), `--color-success-text` (paid), `--radius-2xl`, `--elevation-4` (hero)

## Realtime

GLOBAL `GameFinished` finalizes; `PlayerRewardAvailable` (PLAYER-SPECIFIC) reveals Claim.

## A11y

Outcome announced via `aria-live=assertive` once; celebration respects reduced-motion (fade only).

## Responsive

375: stacked; 1024+: centered 640 with leaderboard side.
