# Responsive Specification (Addendum 2 §7)

## Breakpoints

**Normative (must support):** `375`, `768`, `1024`, `1440`
**Extensions (when valuable):** `360`, `640`, `1280`, `1536`
Tokens: `--breakpoint-*` (see `design-tokens.css` custom media). Mobile-first `min-width` queries.

## Grid & Gutters (adaptive, not scaled)

| BP | Columns | Gutter | Margin |
|----|---------|--------|--------|
| 375 | 4 | 16px | 16px |
| 768 | 8 | 24px | 24px |
| 1024 | 12 | 32px | 32px |
| 1440 | 12 | 32px | auto (max-width 1440 centered) |

## Core Rule

Layouts **adapt**, never merely scale. 0 horizontal scroll 320–1536px. Typography fluid via `clamp()`.

## Component Adaptation Table

| Component | 375 | 768 | 1024 | 1440 |
|-----------|-----|-----|------|------|
| Table | card list | card list | dense table | table + persistent filters |
| Sidebar (Admin) | drawer overlay | drawer | collapsible 240px | fixed 240px |
| QuestionCard | stacked 1-col | centered 640 | 2-col (Q+options) | 2-col + leaderboard rail |
| AnswerOption | 1-col (56px tall) | 1-col | 2×2 grid | 2×2 grid |
| Timer | sticky top bar | top pill | corner widget | corner large |
| Leaderboard | collapsible peek | collapsible | right rail | right rail 4-col |
| Select listbox | bottom sheet | popover | popover | popover |
| Modal | full-screen sheet | centered | centered | centered |
| Drawer | full-screen/overlay | overlay | docked (Admin) | docked |
| Tabs | scroll + fade edges | scroll/fit | fit | fit |
| Forms | stacked labels | stacked | label-left dense (Admin) | label-left dense |

## Game Screen Preservation (Addendum 2 §5)

At ALL breakpoints these remain visible without scrolling away context: question hierarchy, 4 options, progression, level, points, secured points, potential reward, countdown, player status, optional leaderboard, withdraw, feedback. See `screens/game-screen.md`.

## Validation

- 0 horizontal scroll 320–1536 (manual + automated at SPEC-017+)
- Touch targets ≥44px at 375
- No content hidden behind fixed navbars (safe-area padding bottom nav)
