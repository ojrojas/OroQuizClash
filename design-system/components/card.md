# Component: Card

## Anatomy
- Slots: `header (title + actions)`, `body`, `footer (actions)` — visual map: vertical stack, `var(--space-4)` gaps.

## Variants & Sizes
- Variants: `default` | `interactive` (hover lift) | `stat` (KPI, Admin)
- Sizes: `md` (default padding `var(--space-6)`) | `compact` (`var(--space-4)`, Admin dense)

## States (Addendum 2 §9)
- Global: Loading (skeleton shimmer), Ready, Empty (CTA), Error (border `var(--color-destructive)`), Disabled (`opacity 0.5`)
- Visual: `var(--component-card-bg)`, hover `var(--elevation-2)` + `translateY(-2px)` (interactive only)

## Props (conceptual)
- Card { title?: string, interactive?: bool, loading?: bool, children }

## Tokens Used
- `--color-card`, `--color-card-foreground`, `--color-border`, `--space-4/6`, `--radius-lg` (Admin `md`) / `--radius-xl` (Player), `--elevation-1/2`, `--typography-title-m`

## A11y
- `role=article` or semantic `<section>` with heading; interactive card → `role=button`/link semantics + keyboard Enter; focus ring `var(--color-ring)`.

## Responsive
- 375: full width stacked; 768: grid 2col; 1024/1440: grid 3–4col (dashboards).

## Motion
- preset: fade 200 ease-out; interactive hover elevation 200; reduced: fade 200 (no translate).
