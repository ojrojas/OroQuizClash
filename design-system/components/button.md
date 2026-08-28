# Component: Button

## Anatomy

Slots: `label`, `icon (optional)`, `loader (spinner)` — icon + label baseline aligned.

## Variants & Sizes

- Variants: `primary` (accent-gold Player / blue Admin), `secondary` (outline), `ghost` (text), `destructive` (red)
- Sizes: `sm` (28px), `md` (36px), `lg` (44px touch-min)

## States

- Global: `default`/`hover` (`elevation 2`, darken 5%)/`active` (`elevation 0`, darken 10%)/`focus` (ring 2px `#2563EB` 3:1)/`disabled` (`opacity 0.5`, `aria-disabled`, no tap)/`loading` (spinner, `aria-busy`)
- Visual check: all states via tokens `var(--component-button-bg-primary)` etc.

## Props (conceptual)

`Button { variant, size, label: string, icon?: string, disabled?: bool, loading?: bool, onPress }` — same API Admin/Player, theme differs.

## Tokens Used

`--color-primary`, `--color-accent`, `--color-destructive`, `--space-3`/`--space-6`, `--radius-lg`, `--elevation-1/2`, `--typography-label-m`, `--motion-scale`

## A11y

`role=button`, `aria-disabled`, focus visible, keyboard `Enter`/`Space`, touch ≥44px, disabled not focusable.

## Responsive

Full width on 375 (stack), inline on 1024+.

## Motion

`scale 200 spring` on press; reduced → `fade 100`.

## CSS (token-only)

```css
.btn-primary { background: var(--color-accent); color: var(--color-on-accent); padding: var(--space-3) var(--space-6); border-radius: var(--radius-lg); box-shadow: var(--elevation-1); font: var(--typography-label-m); transition: var(--motion-scale); }
.btn-primary:hover { box-shadow: var(--elevation-2); }
.btn-primary:focus { box-shadow: 0 0 0 2px var(--color-ring); outline: none; }
```
