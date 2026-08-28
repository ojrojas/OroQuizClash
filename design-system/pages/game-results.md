# Page Override: game-results

> Overrides `design-system/MASTER.md` for `/results/:gameId`. Only deviations listed.

- Outcome hero uses `--elevation-4` + accent glow for `Winner` only; `Eliminated`/`Consolation` use neutral calm treatment (no red dominance).
- Celebration animation runs once on mount ≤600ms; never loops.
- Your-rank row pinned visible even if outside top 10 (deviation from Leaderboard top-10 default).
- Claim CTA appears only on `PlayerRewardAvailable`; before that, pending Badge with estimated time.
