# Component: Badge

## Anatomy
- Slots: `icon (optional)`, `label` — pill shape, inline-flex.

## Variants & Sizes
- Variants: `neutral` | `success` | `warning` | `error` | `info` | `accent` (reward/points)
- Sizes: `sm 20px` | `md 24px`

## States (Addendum 2 §9)
- Static by design; game uses: `Winner` (accent), `Eliminated` (error), `Consolation` (neutral), `Live` (success + pulse dot), `Withdrawn` (warning).
- Never color-only meaning — icon or text always present (FR-015).

## Props (conceptual)
- Badge { variant, label, icon?, pulse? }

## Tokens Used
- `--color-success/warning/destructive/info/accent` at 12% tint bg + full-color text/icon, `--space-1/2`, `--radius-2xl` (pill), `--typography-caption`/`label-m`

## A11y
- `role=status` when dynamic (live updates); text alternative for icon-only; contrast tint-bg vs text ≥4.5:1 verified both themes.

## Responsive
- Inline everywhere; wraps in narrow columns; truncates with tooltip at <80px.

## Motion
- preset: fade 200 (appear); `pulse` dot 1s for Live only (reduced: static dot).
