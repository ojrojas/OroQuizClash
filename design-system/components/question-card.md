# Component: QuestionCard

## Anatomy
- Slots: `category label`, `round indicator`, `question text` (hierarchy order), `media (optional image)` — vertical stack centered.

## Variants & Sizes
- Single variant; sizes adapt: text `--typography-title-m`@375 → `--typography-heading-l`@1024.

## States (Addendum 2 §9 — game)
- `QuestionActive` (default, entrance roundTransition 500)
- `AnswerSelected` (dim non-selected slightly? NO — keep all readable; highlight via AnswerOption)
- `AnswerLocked` (all options locked, card shows "Locked" subtle badge)
- `Evaluating` (suspense border pulse ≤800ms)
- `Correct` / `Incorrect` / `Timeout` (feedback banner inside card footer)
- `RoundCompleted` (score summary overlay before next)
- Visual: border/bg via `--color-card`, feedback via `--color-success-text`/`--color-destructive` + icon + text.

## Props (conceptual)
- QuestionCard { category, round, totalRounds, text, media?, state, feedback? }

## Tokens Used
- `--color-card`, `--color-border`, `--color-success-text`, `--color-destructive-text`, `--space-4/6`, `--radius-xl`, `--elevation-3`, `--motion-round-transition`, `--typography-heading-l`

## A11y
- `role=region aria-label="Question 3 of 5"`; feedback `aria-live=polite`; question text is heading level 2; media has alt text.

## Responsive
- 375: 1-col stacked full width; 768: centered 640; 1024+: left col of 2-col game layout (options beside/below per grid).

## Motion
- preset: roundTransition 500 ease-in-out (entrance), evaluating pulse 800; reduced: fade 200.
