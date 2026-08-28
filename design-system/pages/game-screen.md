# Page Override: game-screen

> Overrides `design-system/MASTER.md` for `/game/:sessionId`. Only deviations listed.

- All 12 Addendum-2-§5 elements mandatory at every breakpoint — overrides any MASTER layout simplification.
- Timer sticky top full-width bar@375 (deviation from corner widget) to preserve visibility with keyboard open.
- Withdraw action pinned bottom-fixed@375, inline@1024+ — always reachable, never behind scroll.
- Celebration on Correct: confetti-lite (CSS particles ≤20, GPU transform) ≤600ms; reduced-motion → success banner fade only.
- Leaderboard hidden by default during active question@375 (collapsible peek) to protect question focus; optional per FR.
- No toasts during `QuestionActive` (except connection loss) — feedback deferred to `RoundCompleted` to avoid distraction.
