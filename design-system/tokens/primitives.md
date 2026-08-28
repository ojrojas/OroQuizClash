# Primitives — Raw Values

**Layer**: primitive — never used directly in components; reference via semantic.
**Source**: `design-system/tokens/design-tokens.json` (three-layer)

## Color Primitive (50–900)

| Hue | 50 | 100 | 200 | 500 | 600 | 800 | 900 |
|-----|----|-----|-----|-----|-----|-----|-----|
| blue | #EFF6FF | #DBEAFE | #BFDBFE | #3B82F6 | **#2563EB** | #1E40AF | #1E3A8A |
| violet | — | — | — | #8B5CF6 | **#7C3AED** | — | — |
| amber | — | — | — | **#F59E0B** | #D97706 | — | — |
| neutral | #F8FAFC | #E9EEF6 | #DBEAFE | #64748B | — | — | #0F172A |
| red | — | — | — | — | **#DC2626** | — | — |
| green | — | — | — | — | **#16A34A** | — | — |

## Spacing (4px base)

`1=4`, `2=8`, `3=12`, `4=16`, `6=24`, `8=32`, `12=48`, `16=64` → `var(--space-4)` etc.

## Radius

`sm 4`, `md 8`, `lg 12`, `xl 16`, `2xl 24` → Admin `md/lg`, Player `xl/2xl`

## Elevation (shadow+blur)

0 none, 1 `0 1px 2px rgba(15,23,42,0.08)`, 2 `0 4px 8px rgba(15,23,42,0.12)`, 3 `0 8px 16px rgba(15,23,42,0.16)`, 4 `0 16px 32px rgba(15,23,42,0.20)`

## Breakpoints

`360, 375* , 640, 768*, 1024*, 1280, 1440*, 1536` — normative * (Addendum 2 §7). Use `var(--breakpoint-375)` or `@media (min-width: 375px)`.

## Icon Grid

24px grid, stroke 1.5px, sizes 16/20/24/32.

## Usage Rule

Primitive → Semantic (`var(--color-blue-600)` → `var(--color-primary)`) → Component (`var(--component-button-bg-primary)`). Never hex in `components/` or `screens/`.
