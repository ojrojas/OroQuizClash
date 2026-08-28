# Typography Scale — Fluid Clamp

**Families:**
- Admin heading `Fira Code`, body `Fira Sans`
- Player heading `Russo One`, body `Chakra Petch`
- Fallback `system-ui, sans-serif`

**Import:**
```css
@import url('https://fonts.googleapis.com/css2?family=Fira+Code:wght@400;500;600;700&family=Fira+Sans:wght@300;400;500;600;700&display=swap');
@import url('https://fonts.googleapis.com/css2?family=Chakra+Petch:wght@300;400;500;600;700&family=Russo+One&display=swap');
```

| Level | Size (clamp) | Weight | Line | Tracking | Usage |
|-------|--------------|--------|------|----------|-------|
| display | `clamp(32px,5vw,48px)` | 700 | 1.1 | -0.02em | Player hero score |
| heading/l | `clamp(24px,3vw,32px)` | 600 | 1.2 | 0 | Page titles |
| title/m | `clamp(18px,2vw,20px)` | 600 | 1.4 | 0 | Card titles |
| body/m | `16px` | 400 | 1.6 | 0 | Body ≤65ch |
| label/m | `14px` | 500 | 1.4 | 0 | Labels/buttons |
| caption | `12px` | 400 | 1.4 | 0 | Caption |

**CSS:** `var(--typography-display-size)` etc. in `design-tokens.css`. Never hardcode `font-family` — use `var(--typography-font-heading)`. Responsive via `clamp()` preserves 375→1440 without scroll.
