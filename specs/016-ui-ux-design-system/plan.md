# Implementation Plan: UI/UX Design System

**Branch**: `016-ui-ux-design-system` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/016-ui-ux-design-system/spec.md` — dual-experience Design System (Administration Blazor .NET 11 + Player Angular 22) sharing tokens, persisted as `design-system/MASTER.md`.

## Summary

Definir el sistema de diseño visual compartido por dos aplicaciones web: **Administration Experience** (Blazor Web App .NET 11 — enterprise SaaS densa, productiva, orientada a datos) y **Player Experience** (Angular 22 — cinemática premium de concurso, tensión/progresión sin copia de branding). Se entregan design tokens agnósticos (color primitive/semantic, typography, spacing 4/8, radius, elevation, motion, breakpoints 375/768/1024/1440, iconography), tipografía, componentes conceptuales, estados globales + de juego (Addendum 2 §9), a11y WCAG 2.2 AA, responsive y motion con `prefers-reduced-motion`, todo validado con **UI/UX Pro Max Skill** y persistido como `design-system/MASTER.md` + `components/screens/tokens/overrides` + overrides por página `design-system/pages/`. Ambas apps consumen `QuizArena.Api` (BuildingBlocks modular monolith); ningún token se hardcodea.

Enfoque técnico: generar el Design System con `ui-ux-pro-max --design-system --persist` usando el prompt canónico del Addendum 2 §12, sintetizar dos expresiones sobre un MASTER único (Admin light SaaS vs Player dark cinematic), usar estructura de tres capas `primitive→semantic→component` (design-system skill), y documentar contratos `design-tokens.json` (Style Dictionary compatible) + componente specs. Sin código de implementación Blazor/Angular en esta fase — solo artefacto de diseño + validación.

## Technical Context

**Language/Version**: Markdown + JSON (design tokens) + CSS variables; handoff a `C# 12 / .NET 11` (Blazor Web App Interactive Server) y `TypeScript / Angular 22.x` (standalone, signals) — ambos consumen `QuizArena.Api` .NET 10 (global.json `10.0.400`, multi-target `net10.0`/`net11.0` per constitution-addendum §19)

**Primary Dependencies**: `ui-ux-pro-max` skill (`--design-system`, 79 styles, 192 palettes, 74 font pairings, 17 GSAP presets, 22 stacks) + `design-system` skill (three-layer tokens, `generate-tokens.cjs`, `validate-tokens.cjs`); `BuildingBlocks.ServiceDefaults` (OTel/health/resilience no usado en tokens pero referencia arquitectura); `Style Dictionary` compatible `design-tokens.json`; `Lucide/Phosphor` icons (vector, `aria-hidden`/`aria-label`); `Google Fonts` (Fira Sans/Fira Code para Admin, Russo One/Chakra Petch para Player — a refinar con Pro Max)

**Storage**: Sistema de archivos — `design-system/MASTER.md` (fuente de verdad), `design-system/tokens/design-tokens.json` + `design-tokens.css`, `design-system/components/*.md`, `design-system/screens/*.md`, `design-system/overrides/` y `design-system/pages/*.md`; versionado en git; sin DB ni migración

**Testing**: Visual/contract: `validate-tokens.cjs --dir src/` (0 literales hardcodeados), `axe` automated kontrast (≥4.5:1, ≥3:1 large, focus ≥3:1), keyboard traversal, NVDA/VoiceOver, `prefers-reduced-motion` manual, responsive snapshot 375/768/1024/1440, anti-pattern audit (Addendum 2 §13), Visual Quality Gate checklist (Addendum 2 §12); no xUnit para tokens pero sí Architecture tests que prohíben `Blazor→DB`/`Angular→DB`

**Target Platform**: Web — Blazor Web App (Server) en `src/Admin/QuizArena.Admin` (.NET 11) y Angular 22 SPA en `src/Player/QuizArena.Player`; ambas SPA/SSR consumen `QuizArena.Api` vía REST + SignalR (solo presentación, principio V)

**Project Type**: `design-system` — documentación agnóstica + contratos; no backend vertical slice en esta fase

**Performance Goals**: Handoff <30 min por componente (SC-009); motion ≤500 ms micro / ≤800 ms ronda, reduced-motion fade ≤200 ms; 0 scroll horizontal 320–1536px; generación de `design-tokens.css` <1 s; build de docs <2 s

**Constraints**: Constitución I/II/III/VI/H/J (frontend presentation-only, BuildingBlocks obligatorio, identidad delegada OroIdentityServer, validación 3 niveles, RFC 7807); Addendum 2 §1-§15 normativo (Design System First, Pro Max obligatorio, 375/768/1024/1440, estados §9, realtime `Backend→Event→Client→UI`, anti-patterns prohibidos, Source of Truth `MASTER.md`); WCAG 2.2 AA; sin copiar branding de shows; motion nunca bloquea `TimeLimit`; light Admin vs dark cinematic Player compartiendo primitivos

**Scale/Scope**: 1 MASTER + 2 overrides (Admin/Player) + ~15 componentes conceptuales (Button, Input, Select, Table, Card, Modal, Drawer, Badge, Tabs, Progress, Timer, QuestionCard, AnswerOption, Leaderboard, Toast) + 6 pantallas (player-home, game-lobby, game-screen, game-results, rewards, admin-dashboard, game-configuration, categories, question-bank, live-games, reports) + 8+ breakpoints; roadmap SPEC-017..036 consume este sistema

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Principle / Addendum | Status | Evidence / Mitigación |
|------|----------------------|--------|------------------------|
| I. Domain First | Reglas en Domain, no en UI | ✅ PASS | Design System no contiene reglas de dominio (Game lifecycle, scoring, withdrawal). Solo tokens/componentes de presentación; dominio permanece en `OroQuizClash.Domain`. |
| II. Clean Architecture | `Web→Application→Domain←Infrastructure` | ✅ PASS | Ambas apps (`QuizArena.Admin`, `QuizArena.Player`) consumen `QuizArena.Api`; nunca `Blazor→DB` ni `Angular→DB` (FR-023). BuildingBlocks dependency inversion intacto. |
| III. BuildingBlocks No Reinvention | Reuso plataforma | ✅ PASS | No se reimplementa BuildingBlocks; Design System usa `ui-ux-pro-max` + `design-system` skills como ayuda, no duplica `Result`/`Specification`/`IEndpoint`. |
| V. Authoritative Domain Engine | Server truth, client presentation-only | ✅ PASS | FR-025: UI sigue `Backend State → Realtime Event → Client State → UI`; cliente no infiere `IsCorrect`/puntos/tiempo; timer usa `warning/critical` solo visual, validación en servidor. |
| VI. OroIdentityServer | Identidad delegada | ✅ PASS | No se diseña login propio; se reutiliza `/Account/*` y `/auth/*` de OroIdentityServer; claim `must_change_password` gating solo redirección visual. |
| E. Persistence | SQL Server + abstracciones | ✅ PASS | Design System no persiste en DB; artefactos en `design-system/` (filesystem). Backend sigue con `AppDbContextBase`/`IRepository` sin cambios. |
| J. API & Frontend | Frontend presentation-only, REST + SignalR | ✅ PASS | Admin/Player consumen `QuizArena.Api` vía REST + SignalR groups (GLOBAL vs player-specific). DTOs en boundary, paginación, `IEndpoint`. |
| Addendum §19 Multi-targeting | `net10.0`/`net11.0` | ✅ PASS | Blazor Admin `.NET 11` convive con backend `net10.0`; compatibilidad documentada; framework-specific aislado. |
| UI/UX Addendum 2 §1-§15 | UI first-class, Pro Max, Design System First, responsive 375/768/1024/1440, a11y, estados, anti-patterns, MASTER.md | ✅ PASS | Este plan existe precisamente para cumplir §1-3 antes de implementar superficies mayores; Pro Max genera/valida `MASTER.md` (§2); responsive normativo cubierto (FR-016/017); estados §9 en FR-011; Visual Quality Gate §12 y anti-patterns §13 en FR-027/028. |
| Simplicity | Evitar complejidad innecesaria | ✅ PASS | Un MASTER compartido + 2 overrides es más simple que dos sistemas independientes; three-layer tokens evita duplicación; sin WebGL/Canvas innecesario. |

**Gate Result: PASS — no violations. No complexity-tracking entries required. Re-check post-Phase 1 confirms same.**

## Project Structure

### Documentation (this feature)

```text
specs/016-ui-ux-design-system/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── design-tokens.json       # Style Dictionary compatible token contract (primitive→semantic→component)
│   ├── design-tokens.css        # CSS variables generated from tokens
│   ├── components.md            # Component spec contract (anatomy/variants/states/a11y/responsive/motion)
│   └── master-structure.md      # design-system/MASTER.md + overrides structure contract
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root) — per Addendum 2 §1/§18

```text
QuizArena/
├── src/
│   ├── BuildingBlocks/                                   # EXISTING — platform (no modificar)
│   │   ├── BuildingBlocks.Kernel.Domain/
│   │   ├── BuildingBlocks.CQRS/
│   │   ├── BuildingBlocks.EventBus/
│   │   ├── BuildingBlocks.EventBus.RabbitMQ/
│   │   ├── BuildingBlocks.Kernel.Infrastructure/
│   │   └── BuildingBlocks.ServiceDefaults/
│   ├── Backend/
│   │   ├── QuizArena.Api/                                # EXISTING — consume design tokens at runtime via CSS; no direct DB from UI
│   │   ├── QuizArena.Application/
│   │   ├── QuizArena.Domain/
│   │   └── QuizArena.Infrastructure/
│   ├── Admin/
│   │   └── QuizArena.Admin/                              # NEW — Blazor Web App .NET 11 Interactive Server (SPEC-017+); consumes design-system tokens via CSS
│   └── Player/
│       └── QuizArena.Player/                             # NEW — Angular 22 SPA (SPEC-027+); consumes design-system tokens via CSS
├── tests/
│   ├── OroQuizClash.Domain.Tests/
│   ├── OroQuizClash.Application.Tests/
│   ├── OroQuizClash.Api.Tests/
│   └── OroQuizClash.Architecture.Tests/                  # EXTEND — forbid Blazor/Angular → EF Core, enforce token usage
├── specs/
│   └── 016-ui-ux-design-system/                          # THIS FEATURE — docs + contracts
└── design-system/                                        # NEW — Source of truth (Addendum 2 §14)
    ├── MASTER.md                                         # Global Source of Truth (generated by ui-ux-pro-max --persist)
    ├── tokens/
    │   ├── design-tokens.json                            # Three-layer tokens (primitive→semantic→component)
    │   └── design-tokens.css                             # CSS variables (primitive → semantic → component)
    ├── components/
    │   ├── button.md
    │   ├── question-card.md
    │   └── ... (15 components)
    ├── screens/
    │   ├── admin-dashboard.md
    │   ├── game-screen.md
    │   └── ...
    ├── overrides/
    │   ├── admin.md                                      # ADMIN OVERRIDES (Blazor — dense/data)
    │   └── player.md                                     # PLAYER OVERRIDES (Angular — cinematic)
    └── pages/                                            # Page-specific overrides (player-home, game-lobby, etc.)
        ├── player-home.md
        ├── game-screen.md
        └── ...
```

**Structure Decision**: Design-system-first (Addendum 2 §3). Sin nuevos proyectos backend en esta fase; se crean `design-system/` y carpetas `src/Admin`/`src/Player` como placeholders para SPEC-017+ (este plan solo documenta su consumo de tokens, no los implementa). Backend modular monolith existente no cambia. El artefacto principal es documentación agnóstica versionada en `design-system/`.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
