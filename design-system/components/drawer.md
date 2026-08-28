# Component: Drawer

## Anatomy
- Slots: `overlay`, `header (title + close)`, `body (nav/filters/content)` — slides from edge.

## Variants & Sizes
- Variants: `left` (Admin nav) | `right` (filters/details)
- Sizes: `240px` (nav) | `320px` (filters) | `400px` (details); 375–768 → overlay full-height.

## States (Addendum 2 §9)
- Global: open/closed, Loading (skeleton body), Empty
- Admin: nav drawer collapsible 1024+ (docked), overlay <1024.

## Props (conceptual)
- Drawer { open: bool, side: left|right, onClose, title?, children }

## Tokens Used
- `--color-card`, `--color-border`, `--space-4/6`, `--elevation-3`, `--motion-slide`, `--typography-title-m`

## A11y
- `role=dialog aria-modal=true` when overlay; focus trap + return focus; Esc closes; nav variant → `role=navigation` + landmark when docked; focus ring `var(--color-ring)`.

## Responsive
- 375–768: overlay slide-over; 1024+: docked/collapsible (Admin sidebar 240px fixed).

## Motion
- preset: slide 300 ease-out; reduced: fade 200.
