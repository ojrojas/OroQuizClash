# Iconography — Lucide/Phosphor Vector

**Grid:** 24px, stroke 1.5px (consistent per layer), 2px only for emphasis.
**Sizes (tokens):** sm 16, md 20, lg 24, xl 32 → `var(--icon-size-lg)` etc. in `design-tokens.css` as `--icon-*`.
**Family:** Lucide primary, Phosphor fallback — vector-only SVG, never emoji (§13).
**Consistency:** Same stroke/filled vs outline per hierarchy level; alignment to text baseline.

**A11y:**
- Decorative beside visible text → `aria-hidden="true"` (web) / native equivalent
- Meaningful without visible text → `aria-label` text alternative
- Control (button) → `accessible name + state` (`aria-pressed`, `aria-expanded`)

**Contrast:** Meaningful icons and control boundaries ≥3:1 against adjacent colors (non-text), decorative must not carry information.

**Examples:**
- `check` (success), `alert-triangle` (warning), `clock` (timer), `trophy` (reward), `users` (players) — choose semantics from use, not glyph.

**Validation:** `Lucide` imported via `@phosphor-icons/react` or `lucide-react`; no raster PNG.
