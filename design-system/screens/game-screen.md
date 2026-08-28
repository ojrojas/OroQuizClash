# Screen: Game Screen (Player — Angular 22)

**Route**: `/game/:sessionId` | **Theme**: `[data-theme="player"]` dark cinematic | **Private session** (FR-025/026)

## Required Elements (Addendum 2 §5 — all preserved at every breakpoint)

1. Question hierarchy (category → round → question text)
2. 4 AnswerOptions
3. Progression (round x/y + Progress bar)
4. Level/difficulty Badge
5. Points (current round potential)
6. Secured points (banked)
7. Potential reward (accent-text)
8. Countdown Timer (warning <10s, critical <5s)
9. Player status (avatar/name + connection)
10. Optional Leaderboard (collapsible@375)
11. Withdraw action (after round ≥1)
12. Feedback (Correct/Incorrect/Timeout)

## Layout

- 375: single column — Timer sticky top, Progress, QuestionCard, 4 options stacked, secured/potential row, Withdraw bottom-fixed
- 768: centered column max-width 640
- 1024/1440: 2-col — Question+Options center (8-col), Leaderboard right rail (4-col); Timer corner large

## State Machine (FR-013)

`QuestionActive → AnswerSelected → AnswerLocked → Evaluating → Correct | Incorrect | Timeout → RoundCompleted → (next round | WithdrawConfirmation → Withdrawn | game end → Winner | Eliminated | Consolation)`

- `AnswerLocked`: options disabled until server `PlayerAnswerEvaluated`
- `correct` styling NEVER before `Evaluating` (server truth V)
- Celebration on Correct ≤600ms (fade+scale), non-blocking

## Components

QuestionCard, AnswerOption, Timer, Progress, Leaderboard, Badge, Modal (WithdrawConfirmation), Toast

## Tokens Used

`--color-background #0F172A`, `--color-card #1E293B`, `--color-accent-text` (reward), `--radius-xl/2xl`, `--elevation-3`, `--typography-font-heading: Russo One`, `--motion-round-transition`

## Realtime (Addendum 2 §11)

`Backend State → Realtime Event → Client State → UI`. PLAYER-SPECIFIC: `PlayerQuestionPresented`, `PlayerAnswerAccepted`, `PlayerAnswerEvaluated`, `PlayerScoreUpdated`, `PlayerWithdrawalAccepted`, `PlayerEliminated`, `PlayerRewardAvailable`. GLOBAL: `RoundStarted`, `RoundCompleted`, `GameFinished`. Client never infers authoritative state from timer/animation — timeout only via server event.

## A11y

Timer warning/critical = color+icon+text+`aria-live=polite`; options keyboard 1–4 shortcuts + tab; focus ring 3:1 dark; touch ≥44px; reduced-motion → fade 200, timer pulse → static opacity.

## Anti-Plagiarism

Original identity: no copied layouts/assets/sounds/branding from any TV show (FR-024) — interaction principles only.
