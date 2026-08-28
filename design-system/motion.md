# Motion Specification (Addendum 2 §6)

## Principles

1. Motion communicates **state**, never merely decorates
2. Never blocks the `TimeLimit` window — gameplay continues through any animation
3. `prefers-reduced-motion: reduce` fully supported without information loss
4. `transform`/`opacity` only — no layout-triggering properties (performance)
5. Durations: micro 100–500ms; round transitions ≤800ms

## Presets (tokenized)

| Preset | Tokens | Duration | Easing | Use |
|--------|--------|----------|--------|-----|
| fade | `--motion-fade` | 200ms | ease-out | modal/drawer/toast/appear |
| slide | `--motion-slide` | 300ms | ease-out | drawer/select/tabs indicator |
| scale | `--motion-scale` | 200ms | spring (0.34,1.56,0.64,1) | button press/answer select |
| timer-pulse | `--motion-timer-pulse` | 500ms ∞ | ease-in-out | countdown warning/critical |
| roundTransition | duration 500 + ease-in-out | 500ms | ease-in-out | Question→Evaluating→result |

Duration tokens: `--motion-duration-100/200/300/500/800`. Easing: `--motion-ease-out`, `--motion-ease-in-out`, `--motion-spring`.

## Reduced-Motion Fallback (normative)

```css
@media (prefers-reduced-motion: reduce) {
  * { animation-duration: var(--motion-duration-200) !important; }
}
```

| Preset | Normal | Reduced |
|--------|--------|---------|
| fade | 200 | keep 200 |
| slide | 300 | fade 200 |
| scale | 200 spring | fade 100 |
| timer-pulse | 500 ∞ | none — static color+icon+text (no info loss) |
| roundTransition | 500 | fade 200 |
| celebration | ≤600 confetti-lite | success banner fade only |
| carousel auto-advance | 6s | paused by default |

## Game Motion Rules

- `AnswerSelected` feedback instant (≤100ms) — never delays lock
- `Evaluating` suspense pulse ≤800ms, cancelled immediately on server result
- Celebration ≤600ms, non-blocking, once per round
- Live row updates (Admin): bg tint fade 800ms marks realtime change
- No toasts during `QuestionActive` (distraction) except connection loss

## GSAP Tier

Standard 7 (scroll/stagger) available for Player celebration/hero; must degrade per reduced table.
