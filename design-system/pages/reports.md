# Page Override: reports

> Overrides `design-system/MASTER.md` for `/admin/reports`. Only deviations listed.

- Reward amounts always `Fira Code` tabular-nums + currency prefix; accent color `--color-accent` reserved for reward figures only.
- Charts provide data-table fallback toggle (a11y deviation: visible table on demand).
- Custom period picker uses Modal (not popover) at all breakpoints for consistency.
- Export runs async: button `aria-busy` + Toast on completion — no blocking spinner overlay.
