# Player Overrides — Game Show (Angular 22)

**Applies to**: `QuizArena.Player` (Angular 22 SPA, standalone, signals)
**Base**: `design-system/MASTER.md` + `design-system/tokens/design-tokens.json`
**Theme selector**: `[data-theme="player"]`
**Metaphor**: Game Show — cinematic, immersive, high emotional feedback, large typography, countdown/progression/score/reward/celebration
**Inspiration**: Premium televised knowledge competitions — interaction principles (tension/progression/risk/reward) but **no copy** of identity/branding/assets/sounds/layouts (§4)

## Semantic Overrides

| Token | MASTER (base) | Player Override | Rationale |
|-------|---------------|-----------------|-----------|
| `color.primary` | `#2563EB` | `#2563EB` (keep quiz blue) | Vibrant gaming |
| `color.accent` | `#F59E0B` muted admin | `#F59E0B` luminous | Gold leaderboard, progression |
| `color.background` | `#F8FAFC` | `#0F172A` dark cinematic | Immersive |
| `color.foreground` | `#1E3A8A` | `#F8FAFC` | 15.8:1 |
| `color.card` | `#FFFFFF` | `#1E293B` | Depth |
| `color.cardForeground` | `#1E3A8A` | `#F1F5F9` | |
| `color.muted` | `#E9EEF6` | `#334155` | |
| `color.border` | `#DBEAFE` | `#334155` | Visible in dark |
| `typography.heading` | `Fira Code` | `Russo One` display + `Chakra Petch` body | Gaming bold, esports |
| `radius.card` | `8px` | `16–24` (`xl/2xl`) | Enveloping |
| `elevation.card` | `1` | `3–4` | Depth cinematic (no excessive glass) |
| `density` | mid | spacious `24–64` (Pro Max --density 4) | Focus on question, not data |
| `motion` | 150–300 | 200–500 + `timer-pulse` 500 loop | Tension |

## Layout — Cinematic Centered

- Single column centered (375: stacked, 1024: 2-col Question + Options + leaderboard side)
- 0 horizontal scroll 320–1536 preserves (§5): question hierarchy, 4 options, progression, level, points, secured points, potential reward, countdown, player status, optional leaderboard, withdraw, feedback
- No mega menu — simple nav (home/lobby/game/results/rewards per SPEC-027)

## Components Emphasis

- QuestionCard, AnswerOption (4, states default/hover/selected/disabled/locked/correct/incorrect), Timer (warning <10s → amber, critical <5s → red + icon + text + pulse), Progress, Leaderboard, Badge, Toast
- Celebration micro-animation ≤600ms on Correct (fade+scale, not blocking next round)
- WithdrawConfirmation modal distinct

## States (Game §9)

- `QuestionActive` → `AnswerSelected` → `AnswerLocked` → `Evaluating` → `Correct`/`Incorrect`/`Timeout` → `RoundCompleted`
- `Winner`/`Eliminated`/`Consolation`/`Withdrawn` at game end
- `correct` never exposed before `Evaluating` (server truth V)

## Accessibility

- Same AA but on dark: `#F8FAFC` on `#0F172A` 15.8:1
- Timer warning/critical: color+icon+text+`aria-live=polite` (no solo-color)
- Touch 44px critical (mobile primary)

## Realtime

- Private session per player (`Session A/B/C → Screen A/B/C` on same `GameId`), public vs private split, GLOBAL vs PLAYER-SPECIFIC events (§6/§11)

## Anti-Patterns

- Prohibited: AI purple gradients, excessive neon/glass, random gradients, emoji, decorative competing with question

## Tokens Used

- `var(--color-primary)` → `#2563EB` in this theme (same primitive, different semantics)
- `var(--radius-xl)` `var(--elevation-3)` etc.
- Consumes `design-tokens.css` `[data-theme="player"]`
