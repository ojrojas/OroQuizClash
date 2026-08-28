# Component: Timer

## Anatomy
- Slots: `icon (clock/alert)`, `seconds text`, `ring/bar progress` — compact pill or corner widget.

## Variants & Sizes
- Variants: `default` (>10s, primary) | `warning` (≤10s, amber) | `critical` (≤5s, destructive)
- Sizes: `md` (game-screen corner) | `lg` (lobby countdown) | `sticky` full-width bar@375

## States (Addendum 2 §9)
- `default` → `warning` (color+icon+text change, pulse starts) → `critical` (faster pulse, `aria-live=polite` announces 5s) → `expired` (only on server `Timeout` event)
- **No solo-color** (FR-015): each state differs by color AND icon AND label text ("10s left" / "Hurry!").
- Client-side display only — authoritative timeout comes from server (never infer §11).

## Props (conceptual)
- Timer { seconds: number, total: number, state: default|warning|critical, size }

## Tokens Used
- `--color-primary`, `--color-warning`, `--color-destructive-text` (critical text), `--color-accent-text` (warning text on dark), `--space-2/3`, `--radius-2xl` (pill), `--motion-timer-pulse`, `--typography-label-m` (tabular-nums)

## A11y
- `role=timer aria-live=off` (default) → `aria-live=polite` at critical; label "Time remaining: 8 seconds"; pulse removed under reduced-motion (static color+icon+text remain).

## Responsive
- 375: sticky top full-width bar; 768: top-center pill; 1024/1440: corner large widget.

## Motion
- preset: timer-pulse 500 ease-in-out infinite (warning/critical); seconds tick no animation (tabular-nums prevents shift); reduced: none (opacity 1).
