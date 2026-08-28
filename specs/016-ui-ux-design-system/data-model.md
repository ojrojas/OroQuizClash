# Data Model: UI/UX Design System

**Branch**: `016-ui-ux-design-system` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)
**Source of truth**: `design-system/MASTER.md` (filesystem) + `design-system/tokens/design-tokens.json` (serializable) — no DB.

## Overview

Este SPEC no introduce agregados de dominio transaccional (no `Game`/`PointTransaction`). Modela la **fundación agnóstica de diseño** como entidades conceptuales versionadas en `design-system/`. Todas son value-like, inmutables y auditables vía SDD/ADR. Su "persistencia" es archivo + git, no `IRepository` (excepto constraints de validación de tokens en CI).

```
Addendum 2 §14: design-system/
├── MASTER.md
├── tokens/        ← DesignToken, ColorPalette, TypographyScale, Breakpoint, MotionToken
├── components/    ← ComponenteConceptual
├── screens/
├── overrides/     ← Theme (Admin/Player)
└── pages/         ← Page override
```

## Entities

### 1. DesignToken

Unidad atómica de decisión visual. Primitive vs Semantic vs Component (three-layer).

| Field | Type | Validation | Notes |
|-------|------|------------|-------|
| `name` | `string` | pattern `^[a-z]+\.[a-z0-9\.-]+$` ej. `color.primary.500` | único, kebab+dot, estable |
| `value` | `string` | hex `#RRGGBB` o `rgba/hsla` o `rem/px/ms` | no vacío; color valida hex |
| `type` | `enum` | `color | typography | spacing | radius | elevation | border | opacity | zIndex | breakpoint | motion | iconography` | |
| `layer` | `enum` | `primitive | semantic | component` | primitive=raw, semantic=purpose, component=component-specific |
| `theme` | `enum?` | `base | administration | player | null` | `null`=base; override solo en `overrides/*.md` |
| `description` | `string` | 10–200 chars | uso |
| `usage` | `string` | lista `usar / no usar` | guía |
| `version` | `string` | semver `1.0.0` | increment per SDD |

**Relationships**: Token `semantic` referencia `primitive` vía `var(--color-blue-600)`; `component` referencia `semantic`.

**States**: N/A (inmutable; nueva versión = nueva entrada).

**Invariants**:
- Cualquier color/espaciado/radio/sombra en UI MUST provenir de un token (FR-002).
- `value` debe pasar contraste AA si es `semantic` de texto/fondo (axe).

**Example JSON** (primitive→semantic→component):
```json
{ "name": "color.blue.600", "value": "#2563EB", "type": "color", "layer": "primitive", "description": "quiz blue" },
{ "name": "color.primary.500", "value": "var(--color-blue-600)", "type": "color", "layer": "semantic" },
{ "name": "component.button.bg.primary", "value": "var(--color-primary-500)", "type": "color", "layer": "component" }
```

---

### 2. Theme / Expresión Visual (Addendum 2 §10)

Overrides semánticos sobre tokens base. Dos instancias canónicas.

| Field | Type | Validation |
|-------|------|------------|
| `name` | `enum` | `Administration | Player` |
| `surface` | `string` | `light` (Admin) / `dark cinematic` (Player) |
| `background` / `foreground` | `token ref` | `color.background` → `#F8FAFC` vs `#0F172A` |
| `primary` | `token ref` | `color.primary.500` → `#1E40AF` Admin vs `#2563EB` Player |
| `accent` | `token ref` | `amber-600` vs `amber-500` |
| `radius` | `token ref` | `radius.md` Admin vs `radius.xl` Player |
| `elevation` | `token ref` | `elevation.1` vs `elevation.3` |
| `motionIntensity` | `enum` | `subtle | standard | cinematic` |
| `density` | `enum` | `dense (8–32)` Admin vs `spacious (24–64)` Player |
| `metaphor` | `string` | `Command Center` vs `Game Show` |

**Invariants**: Misma API de tokens, distinta aplicación; Administración = operational UX, Player = emotional UX; ambas validadas AA independiente.

**Storage**: `design-system/overrides/admin.md` + `player.md` (markdown con frontmatter).

---

### 3. ComponenteConceptual

Patrón reutilizable agnóstico a framework (Blazor/Angular). API conceptual idéntica, theme diferente.

| Field | Type | Validation |
|-------|------|------------|
| `name` | `string` | ej. `Button`, `QuestionCard` |
| `anatomy` | `string[]` | slots (ej. `label, icon, loader`) |
| `variants` | `enum[]` | `primary | secondary | ghost | destructive` (+ component-specific) |
| `sizes` | `enum[]` | `sm | md | lg` |
| `states` | `enum[]` | Globales `Loading, Ready, Empty, Error, Disabled, Active, Selected, Success, Failure, Processing, Completed` + juego `QuestionActive...Consolation` per §9 |
| `props` | `object` | props conceptuales (ej. `AnswerOption { id, label, selected, disabled, correct? }` — `correct` nunca expuesto antes de `Evaluating`) |
| `tokensUsed` | `string[]` | lista de `DesignToken.name` |
| `a11y` | `object` | `{ role, aria, keyboard, focus, announcement }` |
| `responsive` | `object` | `{ 375: behavior, 768: ..., 1024: ..., 1440: ... }` |
| `motion` | `object` | `{ preset, duration, easing, reducedFallback }` |

**Relationships**: Consume `DesignToken`, referencia `Breakpoint`, `MotionToken`, `IconographySet`.

**Invariants**:
- Cada componente documenta todos los estados relevantes (§9).
- `AnswerOption.correct` nunca se expone en `QuestionActive/AnswerSelected/AnswerLocked` — solo tras `Evaluating` (server truth V).

**Example**: `QuestionCard` → `{ anatomy: [header, question, timer, options(4), progress], variants: [default], states: [QuestionActive, AnswerSelected, AnswerLocked, Evaluating, Correct, Incorrect, Timeout], tokensUsed: [typography.heading.l, spacing.4, color.surface, motion.timer-pulse] }`.

**Storage**: `design-system/components/<kebab-name>.md` (ej. `button.md`, `question-card.md`).

---

### 4. ColorPalette

| Field | Type | Validation |
|-------|------|------------|
| `name` | `string` | ej. `quiz-arena` |
| `primitive` | `map<string, string>` | `50:#EFF6FF ... 900:#0F172A` per hue |
| `semantic` | `map<string, string>` | `primary→var(--blue-600)` etc. |
| `contrastPairs` | `array` | `[{ fg: "color.foreground", bg: "color.background", ratio: 12.1, level: "AA" }]` |
| `stateVariants` | `map` | `hover: darken 5%, active: darken 10%, focus: ring, disabled: opacity 0.5` |
| `forcedColorsFallback` | `string` | `CanvasText` border 1px |

**Invariants**: Todo par `fg/bg` usado en producción MUST tener `ratio ≥4.5:1` (normal) o `≥3:1` (large) documentado; light y dark testeados independiente.

---

### 5. TypographyScale

| Field | Type | Validation |
|-------|------|------------|
| `family` | `enum` | `Fira Code | Fira Sans | Russo One | Chakra Petch` + fallback |
| `level` | `enum` | `display | heading | title | body | label | caption` (+ `l/m/s`) |
| `size` | `string` | `clamp()` ej. `clamp(18px, 2vw, 20px)` o `px` para label |
| `weight` | `enum` | `400 | 500 | 600 | 700` |
| `lineHeight` | `number` | `1.1–1.6` per level |
| `tracking` | `string` | ej. `-0.02em` |
| `usage` | `string` | `page title | card title | ...` |

**Invariants**: Nunca usar `font-family` hardcodeado en componente; siempre `var(--typography-font-heading)`.

---

### 6. Breakpoint & Layout (Addendum 2 §7)

| Field | Type | Validation |
|-------|------|------------|
| `name` | `enum` | `375 | 768 | 1024 | 1440` (normativos) + `360 | 640 | 1280 | 1536` extensión |
| `minWidth` | `number` | `375` etc. |
| `grid` | `enum` | `4 | 8 | 12` cols |
| `gutter` | `string` | `16px@375, 24@768, 32@1024/1440` |
| `behavior` | `map<component, string>` | `QuestionCard: stacked@375 → 2col@1024` |

**Invariants**: Layouts adaptan, no solo escalan; 0 scroll horizontal 320–1536px.

---

### 7. MotionToken (Addendum 2 §6)

| Field | Type | Validation |
|-------|------|------------|
| `name` | `string` | ej. `duration.200` |
| `duration` | `number` | `100 | 200 | 300 | 500` ms (micro ≤500, ronda ≤800) |
| `easing` | `enum` | `ease-out | ease-in-out | spring` |
| `preset` | `enum` | `fade | slide | scale | timer-pulse | round-transition` |
| `reducedMotionFallback` | `string` | `fade 200ms` o `none` |

**Invariants**: Nunca bloquea ventana de respuesta `TimeLimit`; respeta `prefers-reduced-motion`.

---

### 8. IconographySet (Addendum 2 §8)

| Field | Type | Validation |
|-------|------|------------|
| `grid` | `number` | `24` px |
| `stroke` | `string` | `1.5px` o `2px` consistente por capa |
| `sizes` | `number[]` | `16 | 20 | 24 | 32` (tokens) |
| `style` | `enum` | `outline | filled` por jerarquía |
| `a11y` | `enum` | `decorative → aria-hidden=true | meaningful → aria-label | control → accessible name + state` |
| `family` | `enum` | `Lucide | Phosphor` (vector-only), nunca emoji |

**Invariants**: Sizing y stroke consistentes; contraste ≥3:1 para meaningful icons.

---

### 9. DesignSystem Source of Truth (Addendum 2 §14)

Contenedor de todo.

| Field | Type | Validation |
|-------|------|------------|
| `master` | `string` | path `design-system/MASTER.md` |
| `tokens` | `string` | `design-system/tokens/design-tokens.json` + `.css` |
| `components` | `string[]` | `design-system/components/*.md` (15) |
| `screens` | `string[]` | `design-system/screens/*.md` |
| `overrides` | `string[]` | `design-system/overrides/{admin,player}.md` |
| `pages` | `string[]` | `design-system/pages/*.md` (11 pages) |
| `version` | `string` | semver, bump per SDD/ADR |
| `validatedBy` | `string` | `ui-ux-pro-max` report hash |

**Invariants**: Toda decisión visual mayor → SDD → ADR → version bump; `MASTER.md` es único global.

---

### 10. Visual Quality Gate (Addendum 2 §12)

Checklist de aceptación (no entidad persistida, pero artefacto).

| Field | Type |
|-------|------|
| `functionalCorrectness` | `bool` |
| `visualConsistency` | `bool` (0 literals, token compliance) |
| `responsive` | `bool` (375/768/1024/1440) |
| `accessibility` | `bool` (axe AA, keyboard, screen reader) |
| `interactionFeedback` | `bool` (hover/focus/active) |
| `animation` | `bool` (motion tokens) |
| `loadingStates` | `bool` |
| `errorStates` | `bool` |
| `emptyStates` | `bool` |
| `reducedMotion` | `bool` |

**Invariants**: Feature UI no se considera completa sin todos true (Definition of Done §15).

---

## Relationships Diagram

```
DesignToken (primitive)
      ↓ var()
DesignToken (semantic) ← Theme (Admin/Player override)
      ↓ var()
DesignToken (component) → ComponenteConceptual → Breakpoint / MotionToken / IconographySet
      ↓
ColorPalette + TypographyScale ──┐
                                ↓
                    DesignSystem Source of Truth (MASTER.md)
                                ↓
                    Visual Quality Gate (checklist)
```

## Validation Rules Summary

- Todo token reference MUST resolver a un primitive existente.
- Todo componente MUST listar `tokensUsed` y todos MUST existir.
- Todo color semantic usado para texto MUST tener `contrastPairs` entry con ratio.
- Ningún `.md` en `design-system/` puede contener hex literal fuera de `tokens/` (enforce con `validate-tokens.cjs`).
- Breakpoints normativos 375/768/1024/1440 MUST estar definidos; 0 scroll horizontal en esos.
- Estados por componente MUST cubrir §9 globales + juego cuando aplique.

## No DB, No Migration

Este SPEC no genera `DbContext`, `IRepository`, `Specification` ni migración. Los archivos en `design-system/` son la "base de datos" y su índice es `MASTER.md`. La validación es estática (`axe`, `validate-tokens`, snapshot) no `EF Core`.
