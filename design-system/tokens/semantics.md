# Semantic Tokens — Purpose Aliases

Maps primitive to meaning. `var(--color-primary)` in code, not `#2563EB`.

## Color Semantic

| Semantic | Value (var) | On | Notes |
|----------|-------------|----|-------|
| primary | `var(--color-blue-600)` | `#FFFFFF` | Admin override `blue-800 #1E40AF` |
| secondary | `var(--color-violet-500)` | `#FFFFFF` | Limited |
| accent | `var(--color-amber-500)` | `#0F172A` | Admin `#D97706` |
| background | `#F8FAFC` | — | Player `#0F172A` via `[data-theme="player"]` |
| foreground | `#1E3A8A` | — | Player `#F8FAFC` |
| surface/card | `#FFFFFF` | — | Player `#1E293B` |
| muted | `#E9EEF6`/ `#334155` | `#475569` | |
| border | `#DBEAFE`/ `#334155` | — | `CanvasText` fallback for forced-colors |
| ring | `var(--color-blue-600)` | — | Focus 3:1 |
| destructive/success/warning/info | red/green/amber/blue 600 | — | Feedback fills/icons |
| successText | admin `#15803D` / player `#4ADE80` | — | **Small success text** (AA 5.02/10.25) |
| accentText | admin `#B45309` / player `#F59E0B` | — | **Small accent text** (AA 4.80/8.19) |

> Rule: `--color-success`/`--color-accent` for fills, icons, large text only. Small text MUST use `--color-success-text`/`--color-accent-text` (T024 audit fix).

**State variants:** `hover` darken 5% (token), `active` darken 10%, `focus` ring 2px, `disabled` opacity 0.5 + `aria-disabled`.

## Contrast Pairs (AA verified, light+dark independent)

| fg | bg | Ratio | Pass |
|----|----|-------|------|
| `#1E3A8A` | `#F8FAFC` | 12.1 | ✔ AA Admin |
| `#F8FAFC` | `#0F172A` | 15.8 | ✔ AA Player |
| `#F59E0B` | `#0F172A` | 8.2 | ✔ AA large+normal |
| `#2563EB` focus on `#F8FAFC` | — | 4.6 | ✔ 3:1 focus |
| Text `muted #475569` on `#FFFFFF` | — | 7.0 | ✔ |

All pairs in `design-tokens.json` `contrastPairs`; light and dark tested separately via axe.

## Other Semantics

- **Spacing**: `xs=var(--space-1) 4`, `sm=8`, `md=16`, `lg=24`, `xl=32`, `2xl=48`
- **Radius**: `sm 4` … `2xl 24`
- **Elevation**: `0–4`
- **Motion**: `fade 200 ease-out`, `slide 300`, `scale 200 spring`, `timer-pulse 500`
