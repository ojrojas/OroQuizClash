# Screen: Rewards

**Route**: `/rewards` | **Theme**: player dark cinematic

## Layout

- Balance hero: available balance (display, accent-text), lifetime earned, pending
- Withdraw Card: amount Input, method Select, "Withdraw" Button → confirm Modal (fee + arrival estimate)
- History Table/List: date, game, amount, status Badge (Pending/Paid/Failed)

## Components

Card, Input, Select, Button, Modal, Badge, Table (cards@375), Toast

## States

Loading (skeleton), Ready, Empty history (CTA play), Withdraw pending (Badge + disabled re-submit), Error (inline + retry), Success (Toast + history update)

## Tokens Used

`--typography-display-size` (balance), `--color-accent-text`, `--color-success-text` (Paid), `--color-warning` (Pending), `--radius-xl`, `--space-6/8`

## Realtime

`PlayerRewardAvailable` updates balance/history live; withdrawal status via API polling/event.

## A11y

Amount input labelled with currency; confirm Modal focus trap; status Badges text+icon (no solo-color).

## Responsive

375: stacked, history as cards; 1024+: balance+withdraw left, history right.
