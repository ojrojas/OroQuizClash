# Page Override: game-lobby

> Overrides `design-system/MASTER.md` for `/lobby/:gameId`. Only deviations listed.

- Countdown Timer uses `lg` size centered — deviation: timer as hero element pre-game.
- Players-joined list animates new avatars with fade+scale 200 (social presence), capped at 1 animation/s to avoid churn flicker.
- "Ready" CTA becomes disabled "Waiting…" with spinner after press — single-press guard.
- Auto-transition on `GameStarted`: 800ms countdown overlay then route change (reduced-motion: immediate).
