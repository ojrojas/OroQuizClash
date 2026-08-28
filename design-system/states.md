# States Matrix (Addendum 2 §9)

Every interactive screen/component maps to these states. Missing states = spec bug.

## Global States (all interactive screens)

| State | Visual Treatment | A11y | Example |
|-------|------------------|------|---------|
| Loading | skeleton/shimmer (layout-stable) | `aria-busy=true` | Table rows, KPI cards |
| Ready | normal render | — | Data visible |
| Empty | illustration + primary CTA | heading + CTA focusable | "Create your first game" |
| Error | destructive-text icon + message + retry action | `role=alert` | API failure banner |
| Disabled | opacity 0.5 + not-allowed | `aria-disabled`, remains focusable for explanation | Live-game locked fields |
| Active | current view/action context | `aria-current` | Nav item, active round |
| Selected | primary tint + check | `aria-pressed`/`aria-selected` | AnswerOption, tab |
| Success | success-text + check icon + text | `role=status` | Save toast |
| Failure | destructive-text + cross icon + text | `role=alert` | Incorrect answer |
| Processing | spinner in trigger + disabled | `aria-busy` | Withdraw submit |
| Completed | success border + summary | announced once | Round completed |

## Game States (Player)

| State | Trigger (server event) | Visual | Notes |
|-------|------------------------|--------|-------|
| QuestionActive | `PlayerQuestionPresented` | full render, timer running | entrance roundTransition 500 |
| AnswerSelected | local (optimistic) | option primary tint `aria-pressed` | reverts if no `PlayerAnswerAccepted` in 2s |
| AnswerLocked | `PlayerAnswerAccepted` | all options locked, spinner on selected | inputs disabled |
| Evaluating | server processing | suspense pulse ≤800ms | no result colors yet |
| Correct | `PlayerAnswerEvaluated` | success icon+text+celebration ≤600ms | **never before this event** |
| Incorrect | `PlayerAnswerEvaluated` | destructive icon+text | reveal correct answer |
| Timeout | server timeout event | neutral "Time's up" + reveal | client never self-declares |
| RoundCompleted | `RoundCompleted` | score summary overlay | → next round |
| WithdrawConfirmation | user action | Modal risk text (secured vs potential) | focus trap |
| Withdrawn | `PlayerWithdrawalAccepted` | summary + exit CTA | final |
| Winner | `GameFinished` + rank 1 | accent celebration once | results hero |
| Eliminated | `PlayerEliminated` | calm neutral treatment | no red dominance |
| Consolation | `GameFinished` + tier | neutral + small reward | results hero |

## Component State Coverage

See each `design-system/components/*.md` "States" section — all 15 catalog components declare coverage of applicable global + game states.

## Rules

1. Loading never hides layout (skeleton keeps dimensions — no CLS)
2. Empty always offers next action
3. Error always offers recovery (retry/back/support)
4. Disabled explains why (tooltip/hint)
5. Game results only from server events (Addendum 2 §11)
