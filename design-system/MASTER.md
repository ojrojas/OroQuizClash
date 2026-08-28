# QuizArena Design System — MASTER

> **LOGIC:** When building a specific page, first check `design-system/pages/[page-name].md`.
> If that file exists, its rules **override** this Master file.
> If not, strictly follow the rules below.
> Hierachical retrieval: `MASTER.md` → `pages/<name>.md` → `overrides/<admin|player>.md`

---

**Project:** QuizArena (OroQuizClash)
**Generated:** 2026-08-28 08:26:34 — enriched 2026-08-28 for SPEC-016 (Addendum 2 §14)
**Category:** Premium Multiplayer Trivia — Gaming / Education / Rewards
**Validated by:** ui-ux-pro-max skill (192 palettes, 74 pairings, 17 GSAP presets, 79 styles) — `python3 .opencode/skills/ui-ux-pro-max/scripts/search.py "premium multiplayer trivia game platform gaming education rewards" --design-system --persist -p "QuizArena" --output-dir "."`
**Source of Truth:** `design-system/MASTER.md` + `design-system/tokens/design-tokens.json` + `design-system/tokens/design-tokens.css` (three-layer primitive→semantic→component)
**Constitution:** v1.1.0 + constitution-addendum.md v1.0.0 (BuildingBlocks, `net10.0`/`net11.0`) + constitution-addendum2.md §1-15 (UI first-class, Pro Max, 375/768/1024/1440, states, anti-patterns)
**Architecture:** `QuizArena.Admin` (Blazor Web App .NET 11 Interactive Server) + `QuizArena.Player` (Angular 22) → `QuizArena.Api` (Modular Monolith BuildingBlocks) — never `Blazor→DB` nor `Angular→DB`
**Canonical Prompt (Addendum 2 §12):** QuizArena is a premium multiplayer trivia game platform. Two apps sharing one design system: Player Angular 22 cinematic immersive premium game-show (private screen per player, countdown, progression, points, withdraw, leaderboard). Administration Blazor .NET 11 professional enterprise dashboard (games, categories, questions, difficulty, scoring, rewards, players, live games, reports, audit). Avoid generic Bootstrap/templates, glass/neon excess, AI purple gradients, emoji as icons.

---

## 1. Visual Direction & Mood

**Mood:** Exciting, Premium, Trustworthy, Competitive, Immersive, Accessible
**Avoid:** Generic SaaS, Generic Bootstrap, AI purple gradients, Excessive glassmorphism, Excessive neon, Clutter (Addendum 2 §14)
**Flow:** `Product Requirements → UX Analysis → UI/UX Pro Max Design System → Visual Direction → IA → Interaction → Component → Screen → Implementation → UX Review` (Addendum 2 §3 — Design System First: no major UI before MASTER)

**Master Metaphor:** Shared primitive palette (quiz blue + gold) with two **related** expressions on one MASTER (not two independent systems):
- **ADMIN** = `Command Center` — professional, dense, data-oriented, productivity
- **PLAYER** = `Game Show` — cinematic, immersive, high emotional feedback, large typography, countdown/progression/score/reward/celebration

---

## 2. Pattern & Style

**Pattern (gaming base):** `Hero + Testimonials + CTA` — conversion social proof before CTA, testimonials photo/name/role, CTA sticky header + post-testimonials, pause on focus/hover/reduced-motion, announce slide position. Sections: Hero > Problem > Solution > Testimonials > CTA.
**Pattern (admin synthesis):** `Enterprise Gateway` — path selection, mega menu, trust signals, video/mission hero, solutions by industry/role, client logos, Contact Sales (admin-dashboard adapts: filters/pagination not marketing hero).

**Style (gaming):** `Vibrant & Block-based` — Bold, energetic, playful, block layout, geometric, high contrast, duotone, modern. Best for gaming/entertainment. Effects: 48px+ gaps, animated patterns, bold hover color shift, scroll-snap, 32px+ type, 200–300ms.
**Style (admin):** `Data-Dense Dashboard` — Multiple charts/widgets, KPI cards, minimal padding, grid, space-efficient. Best for BI/financial/operational dashboards. Effects: tooltips hover, chart zoom, row highlight, filter animations, spinners.

**Mode Support:** Light supported | Dark supported — contrast independently verified (not inferred).

---

## 3. Color Palette (Primitive → Semantic)

> Primitive hex only here; elsewhere use `var(--color-primary)` etc. — enforced by `validate-tokens.cjs`.

| Role | Hex | CSS Variable | Notes |
|------|-----|--------------|-------|
| Primary | `#2563EB` | `--color-primary` | Quiz blue — `gold leaderboard` (Pro Max) |
| On Primary | `#FFFFFF` | `--color-on-primary` | 12:1 on primary |
| Secondary | `#7C3AED` | `--color-secondary` | Limited accent |
| On Secondary | `#FFFFFF` | `--color-on-secondary` | |
| Accent/CTA | `#F59E0B` | `--color-accent` | Gold — Player luminous, Admin `#D97706` muted |
| On Accent | `#0F172A` | `--color-on-accent` | 8.2:1 on dark |
| Background | `#EFF6FF` | `--color-background` | Admin light `#F8FAFC`, Player dark `#0F172A` via `[data-theme]` |
| Foreground | `#0F172A` | `--color-foreground` | Admin `#1E3A8A`, Player `#F8FAFC` |
| Card | `#FFFFFF` | `--color-card` | Player `#1E293B` |
| Card Foreground | `#0F172A` | `--color-card-foreground` | Player `#F1F5F9` |
| Muted | `#F1F5FD` | `--color-muted` | Player `#334155` |
| Muted Foreground | `#475569` | `--color-muted-foreground` | |
| Border | `#E4ECFC` | `--color-border` | Player `#334155` |
| Destructive | `#DC2626` | `--color-destructive` | |
| On Destructive | `#FFFFFF` | `--color-on-destructive` | |
| Ring | `#2563EB` | `--color-ring` | Focus 3:1 |
| Success | `#16A34A` | `--color-success` | |
| Warning | `#D97706` | `--color-warning` | |
| Info | `#2563EB` | `--color-info` | |

**Primitive layer (excerpt):** `blue 50 #EFF6FF … 900 #1E3A8A`, `amber 500 #F59E0B 600 #D97706`, `neutral 50 #F8FAFC 900 #0F172A`, `red 600 #DC2626`, `green 600 #16A34A`.
**Semantic mapping:** `primary → blue-600` (Admin override `blue-800 #1E40AF`), `accent → amber-500` (Admin `#D97706`), `background → #F8FAFC` Admin / `#0F172A` Player.
**State variants:** hover darken 5%, active darken 10%, focus ring, disabled opacity 0.5 — all via tokens.
**Forced-colors fallback:** `1px solid CanvasText` borders in `design-tokens.css` `@media (forced-colors: active)`.
**Contrast verified (AA, Addendum 2 §8):** Admin `#1E3A8A` on `#F8FAFC` 12.1:1, Player `#F8FAFC` on `#0F172A` 15.8:1, Accent `#F59E0B` on `#0F172A` 8.2:1, Ring `#2563EB` on `#F8FAFC` 4.6:1 — light+dark independently, focus ≥3:1.

---

## 4. Typography

**Admin (Command Center — precise/data):**
- Heading: `Fira Code` (monospace, dashboard precision)
- Body: `Fira Sans`
- Google Fonts: https://fonts.googleapis.com/css2?family=Fira+Code:wght@400;500;600;700&family=Fira+Sans:wght@300;400;500;600;700&display=swap

**Player (Game Show — bold/esports):**
- Heading: `Russo One` (gaming bold)
- Body: `Chakra Petch` (action/esports)
- Mood: gaming, bold, action, esports, competitive, energetic
- Google Fonts: https://fonts.googleapis.com/css2?family=Chakra+Petch:wght@300;400;500;600;700&family=Russo+One&display=swap

**CSS Import:**
```css
@import url('https://fonts.googleapis.com/css2?family=Fira+Code:wght@400;500;600;700&family=Fira+Sans:wght@300;400;500;600;700&display=swap');
@import url('https://fonts.googleapis.com/css2?family=Chakra+Petch:wght@300;400;500;600;700&family=Russo+One&display=swap');
```

**Scale (clamp fluid, Addendum 2 §7):**
| Level | Size | Weight | Line | Tracking | Use |
|-------|------|--------|------|----------|-----|
| display | `clamp(32px,5vw,48px)` | 700 | 1.1 | -0.02em | Player hero score |
| heading/l | `clamp(24px,3vw,32px)` | 600 | 1.2 | 0 | Page titles |
| title/m | `clamp(18px,2vw,20px)` | 600 | 1.4 | 0 | Card titles |
| body/m | `16px` | 400 | 1.6 | 0 | Body (≤65ch) |
| label/m | `14px` | 500 | 1.4 | 0 | Labels/buttons |
| caption | `12px` | 400 | 1.4 | 0 | Caption |

Never hardcode `font-family` in components — use `var(--typography-font-heading)`.

---

## 5. Spacing / Radius / Elevation / Shadows

**Spacing (4/8 base):** `xs 4px`, `sm 8px`, `md 16px`, `lg 24px`, `xl 32px`, `2xl 48px`, `3xl 64px` — Admin dense `8–32`, Player spacious `24–64` (Pro Max `--density` dial 8 vs 4).

| Token | Value | Usage |
|-------|-------|-------|
| `--space-xs` | `4px` | Tight gaps |
| `--space-sm` | `8px` | Icon gaps |
| `--space-md` | `16px` | Standard padding |
| `--space-lg` | `24px` | Section padding |
| `--space-xl` | `32px` | Large gaps |
| `--space-2xl` | `48px` | Section margins |
| `--space-3xl` | `64px` | Hero padding |

**Radius:** `sm 4`, `md 8`, `lg 12`, `xl 16`, `2xl 24` — Admin `md/lg`, Player `lg/xl/2xl`.
**Elevation:** `0 none`, `1 0 1px 2px rgba(15,23,42,0.08)`, `2 0 4px 8px rgba(15,23,42,0.12)`, `3 0 8px 16px rgba(15,23,42,0.16)`, `4 0 16px 32px rgba(15,23,42,0.20)` — Admin max 2, Player 3–4 cinematic (no excessive glass §13).

| Level | Value | Usage |
|-------|-------|-------|
| `--shadow-sm` | `0 1px 2px rgba(0,0,0,0.05)` | Subtle lift |
| `--shadow-md` | `0 4px 6px rgba(0,0,0,0.1)` | Cards, buttons |
| `--shadow-lg` | `0 10px 15px rgba(0,0,0,0.1)` | Modals, dropdowns |
| `--shadow-xl` | `0 20px 25px rgba(0,0,0,0.15)` | Hero, featured |

---

## 6. Motion

**Presets (tokenized, Addendum 2 §6 — communicate state, not decoration):**

| Preset | Duration | Easing | Use | Reduced fallback |
|--------|----------|--------|-----|------------------|
| fade | 200ms | ease-out | modal/drawer/toast | keep fade 200 |
| slide | 300ms | ease-out | drawer/table filter | fade 200 |
| scale | 200ms | spring (0.34,1.56,0.64,1) | button press | fade 100 |
| timer-pulse | 500ms loop | ease-in-out | countdown warning/critical | opacity without scale |
| roundTransition | 500ms | ease-in-out | Question→Evaluating→Correct | fade 200 |

**Rules:** Durations micro ≤500ms, ronda ≤800ms; motion never blocks `TimeLimit` window; `prefers-reduced-motion: reduce` → `fade ≤200ms` or `none`; `transform`/`opacity` only (no layout shift); GSAP tier standard 7 (scroll/stagger) for celebration ≤600ms.

---

## 7. Breakpoints & Responsive (Addendum 2 §7)

**Normative (must support):** `375px`, `768px`, `1024px`, `1440px` — plus extensions `360`, `640`, `1280`, `1536` when value.
**Grid:** 4 (375), 8 (768), 12 (1024/1440) cols.
**Gutters:** `16px@375`, `24@768`, `32@1024/1440` — adaptive, not scaled.
**Rule:** Layouts adapt, not just scale; 0 horizontal scroll 320–1536px; game-screen preserves (§5): question hierarchy, 4 options, progression, level, points, secured points, potential reward, countdown, player status, optional leaderboard, withdraw, feedback.

| Component | 375 | 768 | 1024 | 1440 |
|-----------|-----|-----|------|------|
| QuestionCard | stacked 1 col, options 1 col | 1 col + sticky timer | 2 col (Q + options) | 2 col + leaderboard side |
| Table (Admin) | cards | cards | dense table | table + persistent filters |
| Sidebar | drawer overlay | drawer | collapsible 240px | fixed 240px |
| Timer | top sticky full | top | corner | corner large |

---

## 8. Iconography (Addendum 2 §8)

**Family:** `Lucide` primary / `Phosphor` fallback — vector-only SVG, never emoji (§13).
**Grid:** `24px`, stroke `1.5–2px` consistent per layer, sizes tokens `16/20/24/32`.
**A11y:** decorative `aria-hidden="true"` beside text; meaningful `aria-label`; control `accessible name + state` (selected/pressed/expanded). Contrast meaningful icons ≥3:1 (non-text).
**Prohibited:** Emoji as structural icons.

---

## 9. Component Specs (15 MVP)

Each `design-system/components/*.md` follows `contracts/components.md` template: anatomy → variants/sizes → states → props → tokens → a11y → responsive → motion.

| Component | Variants | Key Uses |
|-----------|----------|----------|
| Button | primary/secondary/ghost/destructive; sm/md/lg | All CTAs |
| Input/Select | default/error/disabled | Forms |
| Table | dense/comfortable | Admin lists |
| Card/Modal/Drawer | — | Containers |
| Badge/Tabs/Progress | — | Status |
| Timer | default/warning/critical (10s/5s) | Game |
| QuestionCard + AnswerOption | — | Game |
| Leaderboard | — | Game/Admin |
| Toast | success/error/info | Feedback |

See `design-system/components/*.md` + `contracts/components.md`.

---

## 10. States (Addendum 2 §9)

**Global (all interactive screens):** `Loading`, `Ready`, `Empty`, `Error`, `Disabled`, `Active`, `Selected`, `Success`, `Failure`, `Processing`, `Completed`
**Game-specific:** `QuestionActive`, `AnswerSelected`, `AnswerLocked`, `Evaluating`, `Correct`, `Incorrect`, `Timeout`, `RoundCompleted`, `WithdrawConfirmation`, `Withdrawn`, `Winner`, `Eliminated`, `Consolation`
**Treatment:** skeleton/shimmer Loading, Empty + CTA, Error + recovery, Disabled `aria-disabled` + cursor, Focus ring `var(--color-ring)`, `AnswerOption.correct` never exposed before `Evaluating` (server truth V).

---

## 11. Accessibility Rules (Addendum 2 §8)

- Contrast ≥4.5:1 normal, ≥3:1 large, focus ≥3:1 (light+dark independently, not inferred)
- Keyboard reachable, logical order, no trap; landmarks/headings; labels + `aria-describedby`
- No solo-color: timer warning/critical = color+icon+text+`aria-live`
- Touch targets ≥44px
- `forced-colors: CanvasText` fallback borders
- `prefers-reduced-motion` supported without breakage
- Pro Max pre-delivery checklist incorporated

---

## 12. Realtime UI (Addendum 2 §11)

```
Backend State → Realtime Event → Client State → UI
```
- Client never infers authoritative state from animation/timer.
- **GLOBAL:** `GameStarted`, `RoundStarted`, `RoundCompleted`, `GameFinished`
- **PLAYER-SPECIFIC:** `PlayerQuestionPresented`, `PlayerAnswerAccepted`, `PlayerAnswerEvaluated`, `PlayerScoreUpdated`, `PlayerWithdrawalAccepted`, `PlayerEliminated`, `PlayerRewardAvailable` — targeting via `PlayerId`/`ConnectionId` SignalR groups (SPEC-012).
- Per §4/§6: each player private session `Session A/B/C → Angular Screen A/B/C` on same `GameId`; public (leaderboard/round/players remaining) vs private (my answer/score/secured/timer/withdraw/reward).

---

## 13. Application Overrides (Addendum 2 §10)

**ADMIN OVERRIDES** (`design-system/overrides/admin.md` — Blazor .NET 11 Command Center):
- Light `background #F8FAFC`, `primary #1E40AF`, `accent #D97706` contained, elevation 1–2, radius `md/lg`, density dense `8–32`, professional Fira Sans/Code, tables/forms/filters/dashboards/CRUD, inline feedback.

**PLAYER OVERRIDES** (`design-system/overrides/player.md` — Angular 22 Game Show):
- Dark cinematic `background #0F172A`, `primary #2563EB`, accent luminous `#F59E0B`, elevation 2–4, radius `lg/xl/2xl`, density spacious `24–64`, Russo One/Chakra, centered layout, progression/timer/score/reward/celebration.

Shared primitives, different semantics — `design-tokens.css` `[data-theme="administration"]` vs `[data-theme="player"]`.

---

## 14. Style Guidelines & Anti-Patterns (Addendum 2 §13)

**Style:** Vibrant & Block-based + Data-Dense Dashboard synthesis.

**Prohibited unless ADR justified:**
- ❌ Generic Bootstrap-like UI
- ❌ Default library appearance
- ❌ Unstyled forms
- ❌ Random gradients / AI purple gradients
- ❌ Excessive glassmorphism / neon
- ❌ Emoji as primary icons
- ❌ Unnecessary animations
- ❌ Inconsistent spacing/typography
- ❌ Hidden loading states
- ❌ Missing error states
- ❌ Mobile = desktop compressed
- ❌ Muted colors / Low energy (base anti-patterns)

---

## 15. Quality Gate (Addendum 2 §12)

Feature complete only when ALL true (Definition of Done §15):
- [ ] Functional correctness
- [ ] Visual consistency (0 literals — `validate-tokens.cjs`)
- [ ] Responsive (375/768/1024/1440)
- [ ] Accessibility (axe AA, keyboard, screen reader, forced-colors)
- [ ] Interaction feedback (hover/focus/active)
- [ ] Animation (motion tokens, reduced-motion)
- [ ] Loading / Error / Empty / Reduced-motion

---

## 16. References

- Constitution v1.1.0 ` .specify/memory/constitution.md`
- constitution-addendum.md v1.0.0 (BuildingBlocks, `net10.0`/`net11.0`)
- constitution-addendum2.md §1-15 (UI first-class, Pro Max, Design System First, Player/Cinematic, Motion, Responsive 375/768/1024/1440, A11y, States, Separation, Realtime, Gate, Anti-patterns, MASTER.md, Done)
- `draft/oroidentityserver-specification.md` (OroIdentityServer — `/Account/*` not redesigned, only redirect + `must_change_password`)
- `draft/libraries/buildingblocks.md` / `design-system` skill three-layer tokens
- ui-ux-pro-max (79 styles, 192 palettes, 74 pairings, 17 GSAP presets) — prompt canónico §12
- `specs/016-ui-ux-design-system/{spec,plan,research,data-model,contracts,quickstart}.md`
- Roadmap SPEC-016→SPEC-017..036 (Admin + Player apps consume this MASTER)

---

## Component Specs (Pro Max Base)

### Buttons

```css
/* Primary Button */
.btn-primary {
  background: var(--color-accent);
  color: var(--color-on-accent);
  padding: var(--space-3) var(--space-6);
  border-radius: var(--radius-lg);
  font-weight: 600;
  transition: all var(--motion-duration-200) var(--motion-ease-out);
  cursor: pointer;
}
.btn-primary:hover { opacity: 0.9; transform: translateY(-1px); box-shadow: var(--elevation-2); }
.btn-primary:focus { box-shadow: 0 0 0 2px var(--color-ring); outline: none; }
```

### Cards / Inputs / Modals — use tokens, see `design-tokens.css` — never hardcode `#2563EB` outside tokens.

---

## Pre-Delivery Checklist

- [ ] No emojis as icons (SVG: Lucide/Phosphor)
- [ ] `cursor-pointer` on all clickable
- [ ] Hover 150–300ms smooth
- [ ] Contrast 4.5:1 both themes
- [ ] Focus visible for keyboard
- [ ] `prefers-reduced-motion` respected
- [ ] Responsive 375/768/1024/1440 + no horizontal scroll
- [ ] 4/8 spacing rhythm, `var(--color-primary)` not `#2563EB`
- [ ] 0 literals via `validate-tokens.cjs`

