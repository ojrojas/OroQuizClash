# Component: Table

## Anatomy
- Slots: `toolbar (filters + actions)`, `header (sortable)`, `body rows`, `footer (pagination)` — sticky header, row hover highlight.

## Variants & Sizes
- Variants: `dense` (Admin default, row 36px) | `comfortable` (row 48px, reports/audit)
- Density toggle persisted per user.

## States (Addendum 2 §9)
- Global: Loading (skeleton rows shimmer), Ready, Empty (illustration + CTA "Create first item"), Error (retry banner), Disabled rows
- Visual: zebra optional, hover `var(--color-muted)`, selected row `var(--color-primary)` 8% tint.

## Props (conceptual)
- Table { columns: ColumnDef[], rows, loading?: bool, empty?: slot, sortable?: bool, pagination?: { page, size, total }, density }

## Tokens Used
- `--color-card`, `--color-border`, `--color-muted`, `--space-2/3/4`, `--radius-md`, `--typography-label-m`/`body-m`, `--motion-fade`

## A11y
- Semantic `<table>` with `<th scope>`; sortable → `aria-sort`; row actions keyboard reachable; pagination `nav aria-label`; caption for screen readers.

## Responsive
- 375: card list (each row → card); 768: cards; 1024+: dense table + persistent filters; 0 horizontal scroll (column priority/ellipsis).

## Motion
- preset: fade 200 (row load/filter), row highlight 150; reduced: fade 200.
