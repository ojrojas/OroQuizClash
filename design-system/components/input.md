# Component: Input

## Anatomy
- Slots: `label`, `prefix/suffix icon`, `input`, `helper text`, `inline error` — label above, error below with icon.

## Variants & Sizes
- Variants: `default` | `error` | `disabled` | `password` (toggle visibility)
- Sizes: `sm 32px` | `md 36px` | `lg 44px` (touch-min Player)

## States (Addendum 2 §9)
- Global: default, hover (border darken), focus (ring 2px `var(--color-ring)`), error (border + message `var(--color-destructive)`), disabled (`opacity 0.5` + `aria-disabled`), loading (suffix spinner)
- Visual: border `var(--color-border)` → focus `var(--color-primary)`.

## Props (conceptual)
- Input { id, label, type, value, onChange, error?: string, helper?: string, disabled?, required? }

## Tokens Used
- `--color-card`, `--color-border`, `--color-primary`, `--color-destructive`, `--space-2/3`, `--radius-md`, `--typography-body-m`/`label-m`, `--motion-fade`

## A11y
- `<label for>` always; error linked via `aria-describedby` + `role=alert` on appear; `aria-invalid=true` on error; focus ring 3:1; never color-only error (icon + text).

## Responsive
- 100% width in forms; 375 stacked labels; 1024+ inline label-left option (Admin dense forms).

## Motion
- preset: fade 200 (error appear), border-color 150; reduced: fade 200.
