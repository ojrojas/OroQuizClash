# Component: Modal

## Anatomy
- Slots: `overlay`, `header (title + close)`, `body`, `footer (actions)` — overlay `rgba` + `backdrop-filter: blur(4px)` (subtle, no glass excess §13).

## Variants & Sizes
- Variants: `default` | `confirm` (WithdrawConfirmation, destructive confirm) | `form`
- Sizes: `sm 400px` | `md 500px` | `lg 640px`; 375 → full-screen sheet.

## States (Addendum 2 §9)
- Global: open (fade+scale), Loading (body spinner `aria-busy`), Error (inline), Disabled actions
- Game: WithdrawConfirmation uses `confirm` variant with explicit risk text (secured points vs potential reward).

## Props (conceptual)
- Modal { open: bool, title: string, onClose, variant, size, children, footer? }

## Tokens Used
- `--color-card`, `--color-border`, `--radius-xl` (16px), `--space-6/8`, `--elevation-4`, `--motion-fade`, `--motion-scale`, `--typography-heading-l`

## A11y
- `role=dialog aria-modal=true`, labelled by title (`aria-labelledby`); **focus trap** inside; **return focus** to trigger on close; Esc closes; focus ring `var(--color-ring)`.

## Responsive
- 375: full-screen bottom sheet; 768+: centered, `max-width 90%`.

## Motion
- preset: fade+scale 300 ease-out; reduced: fade 200 (no scale).
