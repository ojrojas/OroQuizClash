# Component: Progress

## Anatomy
- Slots: `track`, `fill`, `label (optional "Round 3/5")`, `markers (round dots, optional)`.

## Variants & Sizes
- Variants: `bar` (default) | `dots` (round markers, Player)
- Sizes: `sm 4px` | `md 8px` | `lg 12px`

## States (Addendum 2 §9)
- Indeterminate (loading, shimmer), determinate (fill %), completed (full + success tint)
- Game: fill per round progression; secured-points marker optional.

## Props (conceptual)
- Progress { value: number, max: number, variant, label?, markers?: number }

## Tokens Used
- `--color-muted` (track), `--color-primary` (fill), `--color-success` (completed), `--space-1/2`, `--radius-2xl` (pill), `--typography-caption`, `--motion-slide`

## A11y
- `role=progressbar aria-valuenow aria-valuemin aria-valuemax aria-label="Round 3 of 5"`; label visible or accessible name always.

## Responsive
- Full width in context; dots variant centers@375.

## Motion
- preset: fill width transition 300 ease-out (transform scaleX preferred); reduced: instant.
