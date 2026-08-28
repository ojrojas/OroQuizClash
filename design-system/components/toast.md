# Component: Toast

## Anatomy
- Slots: `icon (variant)`, `title`, `message`, `action (optional)`, `dismiss` — stacked corner region.

## Variants & Sizes
- Variants: `success` | `error` | `info` | `warning`
- Sizes: single `md`; max 3 stacked, oldest collapses.

## States (Addendum 2 §9)
- Global: appear (fade+slide), visible (auto-dismiss 5s, error 8s), dismissed
- Uses: save confirmations (Admin inline feedback), PlayerAnswerAccepted ack, withdrawal accepted, connection lost (error + retry action).

## Props (conceptual)
- Toast { variant, title, message?, action?: {label,onPress}, duration? }

## Tokens Used
- `--color-card`, `--color-border`, `--color-success/destructive/info/warning`, `--space-3/4`, `--radius-lg`, `--elevation-3`, `--motion-fade`, `--typography-label-m`/`body-m`

## A11y
- `role=status aria-live=polite` (success/info) or `role=alert` (error); dismissible by keyboard; never color-only (icon + text); does not steal focus from game input.

## Responsive
- 375: bottom full-width (above safe area); 768+: top-right stack; never covers Timer/QuestionCard critical area on game screen.

## Motion
- preset: fade 200 + slide-in 300; reduced: fade 200.
