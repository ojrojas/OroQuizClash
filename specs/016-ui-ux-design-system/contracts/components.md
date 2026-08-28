# Contracts: Components & States

**Branch**: `016-ui-ux-design-system` | **Spec**: [spec.md](../spec.md) | **Tokens**: [design-tokens.json](design-tokens.json)

Agreements for the conceptual component catalog. Same API in Administration (Blazor) and Player (Angular 22); only `theme` values differ (FR-010, Addendum 2 §10).

## Component Spec Template (per `design-system/components/*.md`)

```markdown
# Component: <Name> — e.g., Button, QuestionCard

## Anatomy
- Slots: [label, icon, loader] — visual map

## Variants & Sizes
- Variants: primary | secondary | ghost | destructive
- Sizes: sm | md | lg

## States (Addendum 2 §9)
- Global: Loading, Ready, Empty, Error, Disabled, Active, Selected, Success, Failure, Processing, Completed
- Game (when applicable): QuestionActive, AnswerSelected, AnswerLocked, Evaluating, Correct, Incorrect, Timeout, RoundCompleted, WithdrawConfirmation, Withdrawn, Winner, Eliminated, Consolation
- Visual for each: `var(--component-button-bg-primary)` etc.

## Props (conceptual)
- e.g., AnswerOption { id: string, label: string, selected: bool, disabled: bool, correct?: never-before-Evaluating }

## Tokens Used
- List: `color.primary.500`, `spacing.4`, `typography.label.m` …

## A11y
- role, aria-*, keyboard (Enter/Space), focus ring `var(--color-ring)`, announcement

## Responsive
- 375: stacked, 768: ..., 1024: ..., 1440: ...

## Motion
- preset: fade 200 ease-out, reduced: fade 200
```

## Catalog (MVP 15) — contracts

| Component | Variants | Key States | A11y | Responsive 375→1440 | Motion |
|-----------|----------|------------|------|----------------------|--------|
| **Button** | primary/secondary/ghost/destructive; sm/md/lg | default/hover/active/focus/disabled/loading | `role=button`, `aria-disabled`, focus ring 3:1 | full | scale 200 spring |
| **Input** | default/error/disabled | focus/error/disabled | `label`+`aria-describedby`, inline error | 100% width | fade |
| **Select** | — | open/closed/focus | `combobox`, `aria-expanded` | drawer on 375 | slide 300 |
| **Table** | dense/comfortable | loading/empty/error | `table`, sortable `aria-sort` | cards@375 → table@1024 | fade |
| **Card** | — | — | `article` | stacked | fade |
| **Modal** | — | open | `aria-modal`, focus trap, return focus | full@375 → centered@768 | fade+scale 300 |
| **Drawer** | left/right | open | `aria-modal` | overlay@375–768, docked@1024 | slide 300 |
| **Badge** | neutral/success/warning/error | — | `status` | — | fade |
| **Tabs** | — | active | `tablist/tab` | scroll@375 | slide |
| **Progress** | — | — | `progressbar aria-valuenow` | — | scale |
| **Timer** | default/warning/critical | warning <10s, critical <5s | `timer` + `aria-live=polite`, no solo-color (color+icon+text) | sticky@375 | timer-pulse 500 |
| **QuestionCard** | — | QuestionActive→AnswerSelected→AnswerLocked→Evaluating→Correct/Incorrect/Timeout | `region` + `aria-live` | 1col@375 → 2col@1024 | roundTransition 500 |
| **AnswerOption** | default/selected/locked/correct/incorrect | default/hover/selected/disabled/locked/evaluating | `button`, `aria-pressed` when selected, `correct` never before Evaluating | 1col@375 | scale 200 |
| **Leaderboard** | — | — | `table` + `aria-label` | collapsible@375 | slide |
| **Toast** | success/error/info | — | `status aria-live=polite` | bottom@375 | fade 200 |

## Iconography Contract

- **Family**: `Lucide` (primary) / `Phosphor` fallback — vector-only, `@phosphor-icons/react` style consistent.
- **Grid 24px**, stroke 1.5–2px, sizes tokens `16/20/24/32`.
- **A11y**: decorative `aria-hidden="true"` beside text; meaningful `aria-label`; control `accessible name + state`.
- **Prohibited**: emoji as icon (`Addendum 2 §13`).
- **Contrast**: meaningful icons ≥3:1 (non-text).

## Responsive Contract (Addendum 2 §7)

- Breakpoints normative: `375, 768, 1024, 1440` (plus `360,640,1280,1536` extensions).
- Gutters `16@375, 24@768, 32@1024/1440`.
- 0 scroll horizontal 320–1536px.
- Game screen preserves (Addendum 2 §5): question hierarchy, 4 options, progression, level, points, secured points, potential reward, countdown, player status, optional leaderboard, withdraw action, feedback.

## Motion Contract (Addendum 2 §6)

- Presets tokenized: `fade 200 ease-out`, `slide 300`, `scale 200 spring`, `timer-pulse 500 ease-in-out infinite`, `roundTransition 500`.
- **Reduced-motion**: `prefers-reduced-motion: reduce` → `fade ≤200ms` or `none`; never loss of info.
- Never blocks `TimeLimit` window.
- Performance: `transform`/`opacity` only (no layout-triggering).

## Realtime Contract (Addendum 2 §11)

```
Backend State → Realtime Event → Client State → UI
```
- Client never infers authoritative state from animation/timer.
- Events: GLOBAL `GameStarted, RoundStarted, RoundCompleted, GameFinished` vs PLAYER-SPECIFIC `PlayerQuestionPresented, PlayerAnswerAccepted, PlayerAnswerEvaluated, PlayerScoreUpdated, PlayerWithdrawalAccepted, PlayerEliminated, PlayerRewardAvailable` — targeting per `PlayerId`/`ConnectionId` (SignalR groups).
- Per Addendum 2 §4/§6: each player private session `Session A/B/C → Angular Screen A/B/C` on same `GameId`; public (leaderboard/round/players remaining) vs private (my answer/score/secured/timer/withdraw/reward).

## Visual Quality Gate Contract (Addendum 2 §12)

Feature complete only when checklist true:
- [ ] Functional correctness
- [ ] Visual consistency (0 literals — `validate-tokens.cjs`)
- [ ] Responsive (375/768/1024/1440)
- [ ] Accessibility (axe AA, keyboard, screen reader, forced-colors, no solo-color)
- [ ] Interaction feedback (hover/focus/active)
- [ ] Animation (motion tokens, reduced-motion)
- [ ] Loading / Error / Empty states
- [ ] Reduced-motion behavior

## Anti-Patterns Prohibited (Addendum 2 §13)

Require ADR to justify:
`Generic Bootstrap-like UI, Default library appearance, Unstyled forms, Random gradients, Excessive glassmorphism/neon, Emoji as icons, Unnecessary animations, Inconsistent spacing/typography, Hidden loading states, Missing error states, Mobile=desktop compressed`.

## File Mapping

```
design-system/components/button.md          ← this contract per component
design-system/components/question-card.md
...
design-system/screens/game-screen.md        ← per-screen assembly of components
```

Each `components/*.md` MUST import `design-tokens.css` and use `var(--color-primary)` exclusively (never `#2563EB` literal — see design-system skill token compliance).
