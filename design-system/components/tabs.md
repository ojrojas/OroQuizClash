# Component: Tabs

## Anatomy
- Slots: `tablist`, `tab (icon + label)`, `indicator`, `tabpanel` — indicator slides under active tab.

## Variants & Sizes
- Variants: `underline` (default) | `pills` (Player filters)
- Sizes: `md 40px` | `lg 48px` (touch)

## States (Addendum 2 §9)
- Global: default, hover, active (indicator + `var(--color-primary)`), focus (ring), disabled
- Visual: inactive `var(--color-muted-foreground)`, active `var(--color-primary)` + 2px indicator.

## Props (conceptual)
- Tabs { tabs: {id,label,icon?}[], activeId, onChange, variant }

## Tokens Used
- `--color-primary`, `--color-muted-foreground`, `--color-border`, `--space-3/4`, `--typography-label-m`, `--motion-slide`

## A11y
- `role=tablist/tab/tabpanel`; `aria-selected`; keyboard: Left/Right arrows move + activate (roving tabindex), Home/End; panel labelled by tab (`aria-labelledby`); focus ring `var(--color-ring)`.

## Responsive
- 375: horizontal scroll with fade edges (no wrap); 768+: fit or scroll; scrollable tabs reachable by keyboard.

## Motion
- preset: slide 200 (indicator), panel fade 200; reduced: fade 200 (indicator jumps).
