# Component: AnswerOption

## Anatomy
- Slots: `key hint (A/B/C/D or 1–4)`, `label text`, `state icon (check/cross, only post-Evaluating)` — large touch target row.

## Variants & Sizes
- Single variant; size `lg` min-height 44px (56px@375 for thumb reach).

## States (Addendum 2 §9 — game)
- `default` → `hover` (border primary) → `selected` (`aria-pressed=true`, border+bg primary tint)
- `disabled` (pre-present/timeout), `locked` (after AnswerLocked — opacity .8, no interaction)
- `evaluating` (spinner on selected)
- `correct` (success border+icon+text) — **NEVER rendered before server `PlayerAnswerEvaluated`** (FR, server truth V)
- `incorrect` (destructive border+icon+text)
- Visual: tokens only; correct/incorrect include icon+text, never color-only (FR-015).

## Props (conceptual)
- AnswerOption { id, label, keyHint, selected: bool, locked: bool, result?: 'correct'|'incorrect' /* only after Evaluating */, onSelect }

## Tokens Used
- `--color-card`, `--color-border`, `--color-primary`, `--color-success-text`, `--color-destructive-text`, `--space-3/4`, `--radius-lg`, `--motion-scale`, `--typography-body-m`

## A11y
- `role=button` (or radio in group `role=radiogroup aria-label="Answers"`); `aria-pressed` when selected; keyboard Enter/Space + shortcuts 1–4; focus ring `var(--color-ring)`; result announced `aria-live=polite`.

## Responsive
- 375: 1-col stacked; 768: 1-col 640; 1024+: 2×2 grid beside QuestionCard.

## Motion
- preset: scale 200 spring (select), result reveal fade 200; reduced: fade 100/200.
