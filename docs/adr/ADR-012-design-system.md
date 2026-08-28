# ADR-012: Design System (UI/UX)

**Status**: Accepted
**Date**: 2026-08-28
**Deciders**: Architecture Team

## Context
SPEC-016 exige un Design System compartido como fuente de verdad agnóstica para dos expresiones de UI: `QuizArena.Admin` (Blazor Web App .NET 11, SaaS operativo light) y `QuizArena.Player` (Angular 22, concurso cinemático dark). Constitution Addendum 2 (§1–15) impone: Design System First (ningún UI mayor antes del MASTER), generación vía `ui-ux-pro-max`, breakpoints normativos 375/768/1024/1440, WCAG 2.2 AA, estados §9, motion §6 con reduced-motion, anti-patterns §13, realtime `Backend→Event→Client→UI` §11, y Visual Quality Gate §12. Riesgo sin ADR: drift Admin↔Player, literales hardcodeados, plagio visual de concursos TV, y UI que accede directo a DB.

## Decision

### 1. MASTER compartido + overrides por app
- `design-system/MASTER.md` fuente de verdad global (16 secciones per `contracts/master-structure.md`), generado con `ui-ux-pro-max --design-system --persist` usando el prompt canónico Addendum 2 §12
- `design-system/overrides/admin.md` (Command Center light) y `overrides/player.md` (Game Show dark) — misma primitiva, semántica distinta por tema
- 11 page overrides en `design-system/pages/` (solo desviaciones): player-home, game-lobby, game-screen, game-results, rewards, admin-dashboard, game-configuration, categories, question-bank, live-games, reports
- **Rationale**: Addendum 2 §3/§10/§14; un solo origen evita drift y duplicación; overrides capturan divergencia legítima sin romper el catálogo compartido

### 2. Paleta y tipografía
- Paleta: quiz blue `#2563EB` (primary) + gold `#F59E0B` (accent) sobre neutros slate; Admin primary `#1E40AF` + accent contenido `#D97706`; Player dark `#0F172A` con accent luminoso
- Tipografía: Admin `Fira Sans`/`Fira Code` (precisión datos); Player `Russo One`/`Chakra Petch` (gaming/esports); escala fluida `clamp()`
- Tokens de texto AA: `--color-success-text`, `--color-accent-text`, `--color-destructive-text` por tema (fixes T024/T030: green-600/amber-600/red-600 fallan 4.5:1 como texto pequeño en uno de los temas)
- **Rationale**: Pro Max (192 paletas/74 pairings) + auditoría WCAG computada archivada en `research.md` Addenda T024/T030/T034; paleta neon del estilo "vibrant block" rechazada explícitamente (§13 neon excess)

### 3. Tokens three-layer (primitive→semantic→component)
- `design-system/tokens/design-tokens.json` v1.0.0 (Style Dictionary compatible) + `design-tokens.css` con `:root` + `[data-theme="administration"]` + `[data-theme="player"]`
- Theming solo en capa semántica; primitivos compartidos; componentes referencian `var(--*)` nunca hex
- `contrastPairs` AA archivados en el JSON (11 pares verificados)
- **Rationale**: design-system skill + FR-001/002/027; trazable en repo (no Figma-only), validable por script

### 4. Validación automatizada (CI gates)
- `node .opencode/skills/design-system/scripts/validate-tokens.cjs --dir design-system/ | src/Admin | src/Player` → 0 literales fuera del catálogo
- `axe-core` AA por tema × breakpoint en fases de implementación (SPEC-017/027)
- Architecture test `DesignSystemNoDirectDbTests`: `src/Admin/**` y `src/Player/**` no referencian EF Core/DbContext/ADO.NET/Dapper/ConnectionStrings (filesystem scan, funciona desde fase placeholder)
- **Rationale**: SC-003/011, FR-023; gates ejecutables sin UI corriendo durante fase de diseño

### 5. Catálogo de 15 componentes + estados §9
- Button, Input, Select, Table, Card, Modal, Drawer, Badge, Tabs, Progress, Timer, QuestionCard, AnswerOption, Leaderboard, Toast — misma API ambas apps, solo theme difiere
- `design-system/states.md`: matriz global (Loading…Completed) + game (QuestionActive…Consolation); `correct` nunca antes de `PlayerAnswerEvaluated` (server truth)
- **Rationale**: FR-010..013; Addendum 2 §9/§11

### 6. Responsive, motion y a11y normativos
- Breakpoints 375/768/1024/1440 (ext 360/640/1280/1536); adaptar no escalar; 0 scroll horizontal 320–1536; game-screen preserva 12 elementos §5 en todos los breakpoints
- Motion tokenizado (fade/slide/scale/timer-pulse/roundTransition), nunca bloquea TimeLimit, reduced-motion → fade ≤200ms sin pérdida de info
- AA: 4.5:1/3:1/focus 3:1 por tema independiente, no solo-color (timer = color+icon+text+aria-live), forced-colors CanvasText, touch ≥44px
- **Rationale**: Addendum 2 §5–8; FR-014..018

### 7. Originalidad (no plagio)
- Inspiración en principios de interacción de concursos (tensión/progresión/riesgo/recompensa) sin copiar identidad/marca/assets/sonidos/layouts de ningún show
- **Rationale**: FR-024

## Consequences
- Ningún feature UI (SPEC-017..036) puede iniciar sin consumir MASTER/tokens; PRs deben citar tokens/componentes consumidos
- Cambios de token: semver en `design-tokens.json`; breaking changes requieren ADR (GOVERNANCE.md)
- Handoff medido: dry-run T039 implementó Button+QuestionCard en ambos temas en 38s con 0 variables sin resolver (<30 min/componente SC-009)
- Runtime axe/SUS/keyboard se re-validan en SPEC-017 (Admin) y SPEC-027 (Player); fase de diseño archiva evidencia computada
- `validate-tokens` + `DesignSystemNoDirectDbTests` entran al pipeline CI

## Alternatives
- Design systems separados por app: rechazado — drift garantizado, duplicación, viola §10 (mismo sistema, dos expresiones)
- Tokens solo en Figma: rechazado — no trazable en repo ni validable por script (research SPEC-016)
- Tailwind defaults / Bootstrap: rechazado — anti-pattern §13 (generic library appearance)
- Paleta neon del estilo Pro Max "vibrant block": rechazada — exceso neon §13; sustituida por blue+gold del prompt canónico
- Textos de estado con green-600/amber-600/red-600 en ambos temas: rechazada — fallan 4.5:1 en un tema; creados tokens `*-text` por tema
- Reflection-based architecture test: rechazado — Admin/Player aún no compilan; filesystem scan cubre fase placeholder y futura
