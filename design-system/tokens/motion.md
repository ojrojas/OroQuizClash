# Motion Tokens — Purposeful, Tokenized

**Addendum 2 §6:** Motion communicates state, never merely decorates; `prefers-reduced-motion` supported; never blocks `TimeLimit`.

| Preset | Duration | Easing | CSS | Use |
|--------|----------|--------|-----|-----|
| fade | 200ms | ease-out `cubic-bezier(0,0,0.2,1)` | `var(--motion-fade)` | modal/drawer/toast |
| slide | 300ms | ease-out | `var(--motion-slide)` | drawer/filter |
| scale | 200ms | spring `0.34,1.56,0.64,1` | `var(--motion-scale)` | button press |
| timer-pulse | 500ms infinite | ease-in-out | `var(--motion-timer-pulse)` | countdown warning/critical |
| roundTransition | 500ms | ease-in-out | `var(--motion-round-transition)` | Q→Evaluating→Correct |

**Tokens:** `duration 100/200/300/500/800`, `easing easeOut/easeInOut/spring`, presets above.

**Reduced-motion fallback:**
```css
@media (prefers-reduced-motion: reduce) {
  * { animation-duration: var(--motion-duration-200) !important; }
  .timer-pulse { animation: none; opacity: 1; } /* color+icon+text still signals warning/critical */
}
```
- fade/slide/scale → `fade 200ms`
- timer-pulse → `opacity` without scale (no solo-color loss)

**GSAP tier:** Standard 7 (scroll/stagger) for Correct celebration ≤600ms.

**Performance:** `transform`/`opacity` only — no layout-triggering.
