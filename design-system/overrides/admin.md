# Administration Overrides — Command Center (Blazor .NET 11)

**Applies to**: `QuizArena.Admin` (Blazor Web App .NET 11 Interactive Server)
**Base**: `design-system/MASTER.md` + `design-system/tokens/design-tokens.json`
**Theme selector**: `[data-theme="administration"]`
**Metaphor**: Command Center — professional, dense, data-oriented, productivity

## Semantic Overrides

| Token | MASTER (base) | Administration Override | Rationale |
|-------|---------------|-------------------------|-----------|
| `color.primary` | `#2563EB` (quiz blue) | `#1E40AF` (blue-800 enterprise) | Conservative, trustworthy for SaaS |
| `color.accent` | `#F59E0B` luminous | `#D97706` contained | CTA without cinematic glare |
| `color.background` | `#EFF6FF` | `#F8FAFC` (neutral-50) | Light, low fatigue 30min sessions |
| `color.card` | `#FFFFFF` | `#FFFFFF` | — |
| `typography.heading` | `Russo One` / `Fira Sans` | `Fira Code` monospace + `Fira Sans` body | Data precision |
| `radius.card` | `12px` | `8px` (`md/lg`) | Sobrio |
| `elevation.card` | `1` | `1` (max 2) | Sutil, no compite con datos |
| `density` | mid 16–64 | dense `8–32` (Pro Max --density 8) | Max data visibility |
| `motion` | 200–500ms | 150–300ms (tooltip/hover/row highlight) | Productivo, no celebrado |

## Layout — Data-Dense Dashboard

- Grid 12 cols (1024/1440), 8 (768), 4 (375)
- Gutters 32 (1024/1440), 24 (768), 16 (375)
- Sidebar 240px collapsible (1024+ fixed, 768 drawer, 375 overlay)
- Tables: dense, `comfortable/compact` toggle, sticky header, pagination, filters persistent (not hero)
- KPI cards minimal padding, KPI + chart zoom on click, tooltips hover

## Components Emphasis

- Tables, KPI Cards, Filters, Forms (12-field game config), KPI charts
- Inline feedback (not modal) for validation; modal only for confirmation
- Navigation: mega menu / lateral

## Accessibility

- Contrast same AA but on light surfaces; focus ring `#2563EB` 3:1
- Touch 44px still required even though desktop primary (768+)

## Anti-Patterns Avoided

- No glassmorphism/neon, no random gradients, no AI purple, no emoji, no missing error states (per §13)

## Tokens Used

- `var(--color-primary)` → `#1E40AF` in this theme
- `var(--elevation-1)`
- `var(--radius-md)` etc.
- Consumes `design-tokens.css` `[data-theme="administration"]`
