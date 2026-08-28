# Component: Select

## Anatomy
- Slots: `label`, `trigger (value + chevron)`, `listbox (options)`, `inline error` — trigger styled as Input.

## Variants & Sizes
- Variants: `default` | `error` | `disabled` | `multi` (chips)
- Sizes: `sm 32px` | `md 36px` | `lg 44px`

## States (Addendum 2 §9)
- Global: closed, open (listbox elevated), focus (ring), hover option (`var(--color-muted)`), selected (check icon), disabled, error
- Visual: listbox `var(--elevation-3)`, `var(--radius-md)`.

## Props (conceptual)
- Select { id, label, options: {value,label}[], value, onChange, error?, disabled?, multi? }

## Tokens Used
- `--color-card`, `--color-border`, `--color-primary`, `--color-muted`, `--space-2/3`, `--radius-md`, `--elevation-3`, `--motion-slide`, `--typography-body-m`

## A11y
- `role=combobox` + `aria-expanded` + `aria-controls`; options `role=option aria-selected`; keyboard: arrows navigate, Enter selects, Esc closes, Home/End; typeahead; focus ring `var(--color-ring)`.

## Responsive
- 375: full-screen bottom-sheet listbox (native-like); 768+: dropdown popover; 0 options clipped.

## Motion
- preset: slide 300 ease-out (open), option highlight 150; reduced: fade 200.
