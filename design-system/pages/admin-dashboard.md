# Page Override: admin-dashboard

> Overrides `design-system/MASTER.md` for `/admin`. Only deviations listed.

- KPI numbers use `--typography-display-size` with `Fira Code` (tabular-nums) — deviation from body scale for data scanning.
- Charts row fixed height 280px@1024+ (no aspect-ratio scaling) to keep axis labels legible.
- Live badge pulse dot: 1s loop allowed here (status, not decoration); reduced-motion → static dot.
- No page-level hero/banner (Enterprise Gateway pattern applies to shell, not dashboard).
