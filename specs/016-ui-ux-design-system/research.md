# Research: UI/UX Design System

**Branch**: `016-ui-ux-design-system` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

All NEEDS CLARIFICATION resolved in spec (0 markers). Research consolidates UI/UX Pro Max findings and architecture decisions for the dual-experience Design System.

## 1. Design System Generation — ui-ux-pro-max `--design-system`

**Decision**: Usar `ui-ux-pro-max` con prompt canónico del Addendum 2 §12 y persistir como `design-system/MASTER.md` + overrides, sintetizando dos expresiones sobre un MASTER único.

**Prompt canónico (Addendum 2 §12)**:
> QuizArena is a premium multiplayer trivia game platform. Two apps sharing one design system: 1) Player — Angular 22 cinematic immersive premium game-show (private screen per player, countdown, progression, points, withdraw, leaderboard). 2) Administration — Blazor .NET 11 professional enterprise dashboard (games, categories, questions, difficulty, scoring, rewards, players, live games, reports, audit). Visual inspiration: premium televised knowledge competitions, modern interactive game shows, cinematic competition — Do NOT copy identity/branding/assets. Player: cinematic/immersive/exciting/premium/competitive/dramatic but usable. Admin: professional/efficient/information-dense/modern SaaS/accessible. Prioritize a11y, responsive 375/768/1024/1440, keyboard, touch-friendly, high contrast, hierarchy, meaningful motion, reduced-motion, clear loading/error/empty, design tokens, reusable components.
> Avoid: generic Bootstrap/templates, glass/neon excess, random gradients, AI purple gradients, excessive animation, decorative competition with question, emoji as icons.

**Command** (three-layer + Master persistence):
```bash
python3 .opencode/skills/ui-ux-pro-max/scripts/search.py \
  "premium multiplayer trivia game platform gaming education rewards" \
  --design-system -f markdown --persist -p "QuizArena" --output-dir "."
# Genera design-system/quiz-arena/MASTER.md — mover a design-system/MASTER.md
python3 .opencode/skills/ui-ux-pro-max/scripts/search.py \
  "enterprise saas administration dashboard data dense productivity" \
  --design-system -f markdown --persist -p "QuizArena" --page "admin-dashboard" --output-dir "."
```

**Rationale**: Addendum 2 §2-3 obliga Pro Max como ayuda de inteligencia y Design System First antes de pantallas mayores. El skill aporta 192 palettes + 74 pairings + 17 GSAP presets con reasoning, evitando decisiones arbitrarias. Persistencia jerárquica (`MASTER.md` + `pages/*.md`) permite overrides por página sin drift (§14).

**Alternatives considered**:
- Diseñar tokens manualmente sin Pro Max — rechazado: sin trazabilidad ni validación de contraste/armonía; viola §2.
- Dos sistemas independientes (Admin y Player separados) — rechazado: duplica primitivos, diverge en 2 sprints; Addendum 2 §10 recomienda MASTER compartido + overrides.
- Usar solo librería (Blazor FluentUI / Angular Material default) — rechazado: anti-pattern §13 (apariencia por defecto), sin identidad premium.

**Artifacts**:
- `design-system/MASTER.md` — Global Source of Truth (spec §10.2)
- `design-system/tokens/design-tokens.json` + `design-tokens.css` — three-layer (primitive→semantic→component) per `design-system` skill
- `design-system/pages/*.md` — page overrides (player-home, game-lobby, game-screen, game-results, rewards, admin-dashboard, game-configuration, categories, question-bank, live-games, reports) per §12

---

## 2. Visual Direction — Dos expresiones sobre MASTER compartido

**Decision**: MASTER agnóstico + dos overrides: **Admin (Command Center)** light SaaS y **Player (Game Show)** dark cinematic, compartiendo primitivos.

| Aspect | MASTER (compartido) | ADMIN OVERRIDES (Blazor) | PLAYER OVERRIDES (Angular 22) |
|--------|---------------------|--------------------------|-------------------------------|
| Surface | `neutral.50–900` primitive | `background: #F8FAFC`, `card: #FFFFFF` (light, Figma #1) | `background: #0F172A`, `card: #1E293B` (dark cinematic, desaturado del MASTER) |
| Primary | `blue-600 #2563EB` (Pro Max primary gaming/enterprise) | `blue-800 #1E40AF` (enterprise gateway, conservador) | `blue-600 #2563EB` + gold acento `#F59E0B` (leaderboard premium) |
| Accent | `amber #F59E0B` / `#D97706` | `#D97706` contenido (CTA secundario) | `#F59E0B` luminoso (progression/score) |
| Typography | Base sans: Fira Sans / Chakra Petch | Heading `Fira Code` monospace (data precision) + `Fira Sans` body | Heading `Russo One` display (gaming bold) + `Chakra Petch` body (esports) |
| Radius | 4/8/12/16 | 8–12 (sobrio) | 12–24 (envolvente, cards grandes) |
| Elevation | 0–4 | 1–2 (sutil, no compite con datos) | 2–4 (profundidad cinemática, no glass excesivo) |
| Motion | `fade/slide/scale/timer-pulse` 100–500 ms | 150–300 ms (tooltips/hover) | 200–500 ms (tensión) + `timer-pulse` |
| Density | Mid (16–64px) | Dense (8–32px, `density 8`) | Spacious (24–64px, `density 4`) |

**Rationale**: El prompt genera dos sistemas cercanos (Vibrant & Block-based + Data-Dense Dashboard) que comparten azul `#2563EB` y ámbar `#F59E0B/#D97706`. La síntesis respeta Addendum 2 §10 (misma API de tokens, distinta aplicación) y evita anti-patterns §13 (no Bootstrap genérico, no glass/neón excesivo, no gradientes aleatorios AI purple).

**Alternatives considered**:
- Paleta gaming neón (magenta/cyan) — rechazado: fails WCAG AA en dark surfaces, excesivo neón §13.
- Paleta monocromática gris — rechazado: sin tensión/progression (§4), no premium.
- Tipografía genérica Inter — posible pero menos expresiva; Pro Max recomienda Fira Code/Chakra para dashboard/gaming con mejor reasoning.

**Output**: `design-system/overrides/admin.md` + `player.md` documentan mapeo de primitivos→semantic por tema; valores idénticos salvo `theme` override (spec FR-007/022).

---

## 3. Color System — Primitive → Semantic (WCAG AA)

**Decision**: Tres capas — Primitive (50–900), Semantic (primary/neutral/feedback/surface), Component (button-bg, etc.). Contraste ≥4.5:1 normal, ≥3:1 large/ non-text (focus 3:1). Validado con Pro Max palettes + axe.

Primitive (extracto):
```json
{ "blue": { "50": "#EFF6FF", "500": "#2563EB", "800": "#1E40AF" },
  "amber": { "500": "#F59E0B", "600": "#D97706" },
  "neutral": { "50": "#F8FAFC", "900": "#0F172A" },
  "red": { "600": "#DC2626" }, "green": { "600": "#16A34A" } }
```
Semantic mapping (Addendum 2 §10 shared):
- `primary` → `blue-600` (default) / `blue-800` Admin — `onPrimary: #FFF`
- `secondary` → `violet #7C3AED` (solo acento secundario, uso limitado)
- `accent` → `amber-500` Player / `amber-600` Admin — `onAccent: #0F172A/#000` (contrast 12:1)
- `background` → `#F8FAFC` Admin light / `#0F172A` Player dark
- `feedback` → `success #16A34A`, `warning #D97706`, `error #DC2626`, `info #2563EB`
- Estados `hover/active/focus/disabled` derivados por token (no hex), focus `ring #2563EB` + 3:1.

**Rationale**: Pro Max sugiere azul quiz + gold leaderboard (Vibrant) y blue data + amber highlights (Enterprise Gateway); fusión mantiene 4.5:1 en ambos temas (testeado light + dark independientemente, no asumido). `forced-colors` fallback: bordes 1px `CanvasText` en ambos.

**Alternatives**: Tailwind default slate — rechazado: sin identidad; Material indigo — similar pero sin gold premium.

---

## 4. Typography Scale — Fluida con `clamp()`

**Decision**: Dos pairings validados por Pro Max (74 pairings) pero unificados por jerarquía `display/heading/title/body/label/caption` + pesos 400/500/600/700 + `lineHeight`/`tracking` por nivel + `clamp()` responsive.

| Level | Size (clamp) | Weight | Line | Use |
|-------|--------------|--------|------|-----|
| `display` | `clamp(32px, 5vw, 48px)` | 700 | 1.1 | Player hero / score grande |
| `heading/l` | `clamp(24px, 3vw, 32px)` | 600 | 1.2 | Page titles |
| `title/m` | `clamp(18px, 2vw, 20px)` | 600 | 1.4 | Card titles |
| `body/m` | `16px` | 400 | 1.6 | Body (max 65ch) |
| `label/m` | `14px` | 500 | 1.4 | Labels/buttons |
| `caption` | `12px` | 400 | 1.4 | Caption |

- Admin: Heading `Fira Code`, Body `Fira Sans` (dashboard preciso) — alternativa si pairing es demasiado mono: `Inter` fallback documentado vía ADR.
- Player: Heading `Russo One`, Body `Chakra Petch` (gaming bold, esports) — Google Fonts import con `display=swap`.
- Tracking `-0.02em` en display, `0` en body.

**Rationale**: Pro Max recomienda Fira Code/Sans para dashboards y Russo One/Chakra para gaming con mood `dashboard, data` vs `gaming, bold, esports`. `clamp()` evita scroll horizontal en 375px y mantiene medida legible en 1440px (Addendum 2 §7). WCAG: tamaño no es excusa para contraste <4.5:1.

**Alternatives**: Inter único para ambos — menos expresivo; Space Grotesk — similar a Chakra pero menos soportado.

---

## 5. Spacing / Radius / Elevation / Iconography

**Decision**:
- **Spacing**: Base 4px — escala `4,8,12,16,24,32,48,64` (Admin dense 8–32px tier, Player spacious 24–64px) — Pro Max `--density` dial `8` Admin vs `4` Player; CSS `var(--space-4)`.
- **Radius**: `sm 4, md 8, lg 12, xl 16, 2xl 24` — Admin usa `md/lg`, Player `lg/xl/2xl`.
- **Elevation**: 0–4 (shadow + blur) — `0 none`, `1 0 1px 2px rgba`, `2 0 4px 8px`, `3 0 8px 16px`, `4 0 16px 32px` — Admin max 2, Player 3–4 cinematic (sin glassmorphism excesivo §13).
- **Iconography**: Grid 24px, stroke 1.5–2px, sizes `16/20/24/32` tokenizados, `Lucide`/`Phosphor` vector-only — consistente sizing, stroke, filled vs outline por nivel; `aria-hidden="true"` decorativos, `aria-label` standalone controls; nunca emoji (§13); contraste ≥3:1 para meaningful icons (Addendum 2 §8).

**Rationale**: 4/8 rhythm + section tiers 16/24/32/48 (Pro Max Layout rules) garantizan ritmo y vertical hierarchy; elevation y radius diferenciados comunican SaaS sobrio vs cinemático sin duplicar primitivos.

---

## 6. Motion Tokens — Con propósito, nunca decorativo

**Decision**: Duraciones `100/200/300/500` ms, easings `ease-out`, `ease-in-out`, `spring` sutil (GSAP presets 17), presets `fade`, `slide`, `scale`, `timer-pulse` — todos tokenizados y `prefers-reduced-motion` reduce a `fade 200ms` u omisión.

| Preset | Dur | Easing | Use | Reduced |
|--------|-----|--------|-----|---------|
| `fade` | 200 | ease-out | modal/drawer | `fade 200` (keep) |
| `slide` | 300 | ease-out | drawer/table filter | `fade 200` |
| `scale` | 200 | spring | button press | `fade 100` |
| `timer-pulse` | 500 loop | ease-in-out | countdown warning/critical | `opacity` sin scale |
| `round-transition` | 500 | ease-in-out | Question→Evaluating→Correct | `fade 200` |

Reglas (Addendum 2 §6): motion comunica estado, no decora; nunca bloquea capacidad de responder dentro de `TimeLimit`; timer `warning <10s` / `critical <5s` cambia color + ícono + texto además de pulso, para no solo-color (FR-015).

**GSAP snippet tier** (Pro Max `--motion 7` standard): scroll/stagger moderado para Player celebration (Correct) — ≤600 ms total, sin pin/splitText complejo (alto motion 8–10 reservado para marketing, no para juego).

**Alternatives**: Framer-only spring everywhere — rechazado: inconsistent, jank en low-end.

---

## 7. Responsive — 375/768/1024/1440 (Addendum 2 §7)

**Decision**: Mobile-first, adaptar no solo escalar. Breakpoints normativos `375, 768, 1024, 1440` + extensiones `360, 640, 1280, 1536` cuando aporten.

| Component | 375 (móvil) | 768 (tablet) | 1024 (desktop) | 1440 (large) |
|-----------|-------------|--------------|----------------|--------------|
| `QuestionCard` | apilada 1 col, opciones 1 col | 1 col + sticky timer | 2 col (pregunta + opciones) | 2 col + leaderboard lateral |
| `Table` (Admin) | cards | cards | table densa | table + filtros persistentes |
| Sidebar | drawer overlay | drawer | colapsable 240px | fija 240px |
| Timer | top sticky full | top | corner | corner large |

Gutters: `16px` en 375, `24px` en 768, `32px` en 1024/1440 — adaptativos. Todo verifica 0 scroll horizontal 320–1536px, preserva pregunta/opciones/timer/score/acción primaria en juego (§5).

**Alternatives**: Tailwind default 640/768/1024 — sin 375 específico del Addendum; Bootstrap 576 — rompe mobile peq.

---

## 8. Component Library — 15 conceptuales + pantallas

**Decision**: Catálogo único conceptual (API idéntica, theme diferente) para evitar drift; cada componente documenta anatomía/variantes/tamaños/estados/a11y/responsive/motion + tokens consumidos (FR-011).

MVP components: `Button` (primary/secondary/ghost/destructive), `Input`, `Select`, `Table`, `Card`, `Modal`, `Drawer`, `Badge`, `Tabs`, `Progress`, `Timer`, `QuestionCard`, `AnswerOption`, `Leaderboard`, `Toast`.

Estados por Addendum 2 §9: globales `Loading/Ready/Empty/Error/Disabled/Active/Selected/Success/Failure/Processing/Completed` + juego `QuestionActive/AnswerSelected/AnswerLocked/Evaluating/Correct/Incorrect/Timeout/RoundCompleted/WithdrawConfirmation/Withdrawn/Winner/Eliminated/Consolation`.

**Stack guidelines** (`--stack angular` / `--stack html-tailwind` para Blazor analog):
- Angular 22: standalone, signals, CSS variables `var(--color-primary)` no hardcoded; GSAP solo para celebration; no React domain.
- Blazor: `var(--color-primary)` en CSS isolation; FluentUI solo como adaptador si se elige, no como fuente de verdad.

**Alternatives**: Crear componentes separados por app — rechazado: duplica estados/a11y.

---

## 9. Accessibility — WCAG 2.2 AA + Pro Max checklist

**Decision**: Contraste 4.5:1 normal / 3:1 large + focus 3:1, keyboard sin trampa, landmarks, `aria-*` correcto, `forced-colors` bordes, touch ≥44px, no solo-color, `prefers-reduced-motion`, `prefers-contrast`. Incorporar Pro Max pre-delivery checklist §8/§3.

Validación: `axe` en light + dark independiente, keyboard tab + NVDA/VoiceOver en 8 flujos clave, 375/1440 + reduced-motion on/off.

**Alternatives**: Solo contraste light — rechazado: dark cinemático falla.

---

## 10. Persistencia — `design-system/MASTER.md` structure

**Decision**: `design-system/MASTER.md` Master + `components/` + `screens/` + `tokens/` + `overrides/` + `pages/` per Addendum 2 §14 y design-system skill §Persist. Versionado SDD: cambios visuales mayores → nueva SPEC → ADR.

```
design-system/
├── MASTER.md               # Global Source of Truth
├── tokens/
│   ├── design-tokens.json  # three-layer JSON
│   └── design-tokens.css   # CSS variables
├── components/*.md
├── screens/*.md
├── overrides/{admin.md,player.md}
└── pages/*.md              # page-specific overrides
```

Hierarchical retrieval: `MASTER.md` → `pages/<name>.md` override si existe.

**Alternatives**: Tokens solo en Figma — rechazado: no trazable en repo.

---

## Consolidated Decisions Summary

| # | Decisión | Fuente |
|---|----------|--------|
| 1 | Prompt canónico Addendum 2 §12 + `ui-ux-pro-max --persist` | Spec FR-019, Addendum 2 §2-3 |
| 2 | MASTER compartido + Admin light (Fira) vs Player dark (Russo/Chakra) + gold | Spec FR-007/022, Pro Max palettes |
| 3 | Three-layer tokens primitive→semantic→component + Style Dictionary | design-system skill |
| 4 | Color AA validado light+dark independiente | WCAG 2.2 AA, FR-014 |
| 5 | `clamp()` typography + Fira/Russo pairings | Pro Max 74 pairings |
| 6 | Spacing 4px, density 8 vs 4, motion 150–500ms + reduced fade | FR-005/006/018 |
| 7 | Breakpoints 375/768/1024/1440 + adaptive gutters | Addendum 2 §7 |
| 8 | 15 components + estados §9 + realtime Backend→Event→Client→UI | Addendum 2 §9/§11 |
| 9 | A11y AA + axe + keyboard + reduced-motion | Addendum 2 §8 |
| 10 | `design-system/MASTER.md` persistence | Addendum 2 §14 |

All clarifications resolved — no NEEDS CLARIFICATION remains. Ready for Phase 1 design.

---

## Addendum T024 — Admin Light Theme Contrast Audit (2026-08-28)

Computed with WCAG relative-luminance formula (Node) against `design-tokens.json` values.

| Pair | fg | bg | Ratio | Min | Result |
|------|----|----|-------|-----|--------|
| Admin body text | #1E3A8A | #F8FAFC | 9.90:1 | 4.5 | PASS |
| On-primary (white on blue-800) | #FFFFFF | #1E40AF | 8.72:1 | 4.5 | PASS |
| Primary text/link on bg | #1E40AF | #F8FAFC | 8.34:1 | 4.5 | PASS |
| Muted foreground on card | #475569 | #FFFFFF | 7.58:1 | 4.5 | PASS |
| Destructive text on card | #DC2626 | #FFFFFF | 4.83:1 | 4.5 | PASS |
| Focus ring (non-text) vs bg | #2563EB | #F8FAFC | 4.94:1 | 3.0 | PASS |
| Accent amber-600 small text on bg | #D97706 | #F8FAFC | 3.04:1 | 4.5 | **FAIL → fixed** |
| Success green-600 small text on card | #16A34A | #FFFFFF | 3.30:1 | 4.5 | **FAIL → fixed** |

**Fixes applied (tokens v1.0.0):**
- New semantic `successText`: admin `#15803D` green-700 (5.02:1 on #FFFFFF), player `#4ADE80` green-400 (10.25:1 on #0F172A)
- New semantic `accentText`: admin `#B45309` amber-700 (4.80:1 on #F8FAFC), player `#F59E0B` amber-500 (8.19:1 on #0F172A)
- `--color-success`/`--color-accent` remain for fills/icons/large text; small text MUST use `--color-success-text`/`--color-accent-text`
- Accent button bg `#D97706` with on-accent `#0F172A` label = 5.60:1 PASS (unchanged)

**Keyboard traversal (documented path, admin shell):** skip-link → sidebar nav (Dashboard…Players) → topbar search → page header action → filters (search → selects) → table (sortable headers → rows → row actions) → pagination. Focus ring `var(--color-ring)` 4.94:1 ≥ 3:1; no traps; Drawer/Modal focus trap + return focus; Esc closes overlays. Full axe run against rendered Blazor app deferred to SPEC-017+ (design phase has no running UI).

---

## Addendum T030 — Player Dark Theme Contrast Audit (2026-08-28)

| Pair | fg | bg | Ratio | Min | Result |
|------|----|----|-------|-----|--------|
| Player body | #F8FAFC | #0F172A | 17.06:1 | 4.5 | PASS |
| Player card fg | #F1F5F9 | #1E293B | 13.35:1 | 4.5 | PASS |
| Accent text (amber-500) | #F59E0B | #0F172A | 8.31:1 | 4.5 | PASS |
| Success text (green-400) | #4ADE80 | #0F172A | 10.25:1 | 4.5 | PASS |
| Muted fg (slate-400) | #94A3B8 | #1E293B | 5.71:1 | 4.5 | PASS |
| Timer warning text | #F59E0B | #0F172A | 8.31:1 | 4.5 | PASS |
| Timer critical text red-600 | #DC2626 | #0F172A | 3.70:1 | 4.5 | **FAIL → fixed** |
| Timer critical text red-400 | #F87171 | #0F172A | 6.45:1 | 4.5 | PASS |
| Focus ring (non-text) | #2563EB | #0F172A | 3.45:1 | 3.0 | PASS |
| On-accent (dark on amber-500) | #0F172A | #F59E0B | 8.31:1 | 4.5 | PASS |

**Fix applied:** new semantic `destructiveText` — admin `#DC2626` (4.83:1 on #FFFFFF), player `#F87171` red-400 (6.45:1 on #0F172A). Timer/AnswerOption/QuestionCard specs updated to `--color-destructive-text` for small text.

**Timer FR-015 compliance:** warning/critical states encoded as color + icon (clock→alert) + text label ("10s left"/"Hurry!") + `aria-live=polite` at critical — verified no solo-color dependence in `design-system/components/timer.md`.

---

## Addendum T034 — Cross-Theme Accessibility Audit (2026-08-28)

Design-phase audit (no rendered apps yet — full axe-core runs scheduled at SPEC-017 Admin / SPEC-027 Player implementation).

**Contrast (computed, WCAG relative luminance):** all pairs PASS after T024/T030 fixes — see `design-tokens.json` `contrastPairs` (9 admin pairs, 10 player pairs; light and dark verified independently).

**Static rule coverage verified in specs:**
- axe `color-contrast`: enforced via contrastPairs + text-token rule (success/accent/destructive small text)
- axe `label`, `aria-*` patterns: specified per component (forms label+describedby, combobox, tablist, dialog aria-modal, progressbar values, table th scope/aria-sort)
- axe `focus-visible`/`focus-order-semantics`: ring token 3:1 both themes + documented traversal (T024)
- axe `target-size` (WCAG 2.2): ≥44px specified (AnswerOption 56px@375)
- Landmarks/headings: specified in admin-shell.md + a11y.md

**Emulations documented:**
- `forced-colors: active` → CanvasText borders + Highlight focus implemented in `design-tokens.css`; state never conveyed by background alone
- `prefers-reduced-motion: reduce` → global 200ms cap + per-preset fallback table in `design-system/motion.md`; timer keeps color+icon+text (no info loss)

**Result:** 0 design-level failures outstanding. Residual risk (runtime-only axe rules: duplicate-id, dynamic aria wiring) explicitly deferred to implementation-phase axe runs.

---

## Addendum T036 — UI/UX Pro Max Validation Searches (2026-08-28)

**Search 1** — `search.py "vibrant block gaming" --domain style` → `vibrant-and-block-based` (active):
- Confirms Player style choice (bold, block layout, 48px+ gaps, 32px+ type, 200–300ms)
- Its default neon palette (Neon Green/Electric Purple) **intentionally NOT adopted** — replaced by quiz blue #2563EB + gold #F59E0B per canonical prompt (avoids §13 neon excess); deviation documented in MASTER §3
- Accessibility requirements listed by Pro Max (`contrast-text-4.5, keyboard, visible-focus, reduced-motion`) all covered in `design-system/a11y.md` + `motion.md`
- Implementation checklist gap noted: "7:1+ contrast" target — our body pairs exceed (9.90/17.06); accent small-text pairs meet 4.5 minimum (4.80/8.31) — accepted, large text exceeds 7:1

**Search 2** — `search.py "enterprise saas dashboard" --domain product` → `SaaS (General)`:
- Dashboard style recommendation `Data-Dense + Real-Time Monitoring` — matches Admin override (dense tables, live-games realtime)
- Palette focus `Trust blue + accent contrast` — matches admin primary #1E40AF + contained amber #D97706
- Secondary styles (Minimalism & Swiss) inform Admin restraint: no decorative motion, elevation ≤2

**Conclusion:** 0 corrections required; 2 confirmations archived. MASTER/overrides remain valid.
