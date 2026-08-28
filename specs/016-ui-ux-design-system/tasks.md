# Tasks: UI/UX Design System

**Input**: Design documents from `/specs/016-ui-ux-design-system/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (design-tokens.json, design-tokens.css, components.md, master-structure.md), quickstart.md
**Branch**: `016-ui-ux-design-system` | **Date**: 2026-08-28
**Organization**: Tasks grouped by user story (P1→P2) + Foundational blocking; each story independently testable; parallelizable [P] flagged.
**Tests**: Visual/contract tests (validate-tokens, axe, keyboard) — no xUnit for tokens; Architecture tests for Blazor/Angular→DB prohibition.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Initialize `design-system/` persistence and tooling; no user story can hand off without directory + generation scripts.

- [X] T001 Create `design-system/` directory tree per plan.md in `design-system/MASTER.md` structure (MASTER.md, tokens/, components/, screens/, overrides/, pages/)
- [X] T002 [P] Copy token contracts to `design-system/tokens/design-tokens.json` and `design-system/tokens/design-tokens.css` from `specs/016-ui-ux-design-system/contracts/`
- [X] T003 [P] Install and configure token scripts `scripts/generate-tokens.cjs` and `scripts/validate-tokens.cjs` in `.opencode/skills/design-system/` (verify Node present)
- [X] T004 Create `src/Admin/QuizArena.Admin/` placeholder and `src/Player/QuizArena.Player/` placeholder directories per Project Structure in `plan.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Generate and persist the global Design System source of truth — blocks ALL user stories (Addendum 2 §3 Design System First).

**⚠️ CRITICAL**: No Administration or Player screen work can begin until this phase is complete (FR-019/021).

- [X] T005 Generate global MASTER with ui-ux-pro-max `--design-system --persist` using canonical prompt Addendum 2 §12 into `design-system/MASTER.md` (verify 16 required sections per `contracts/master-structure.md`)
- [X] T006 [P] Finalize three-layer `design-system/tokens/design-tokens.json` (primitive→semantic→component, version `1.0.0`, `contrastPairs` AA) — audit vs `contracts/design-tokens.json`
- [X] T007 [P] Generate `design-system/tokens/design-tokens.css` via `node scripts/generate-tokens.cjs --config design-system/tokens/design-tokens.json -o design-system/tokens/design-tokens.css` — verify `:root` + `[data-theme="administration"]` + `[data-theme="player"]`
- [X] T008 [P] Create ADMIN and PLAYER theme overrides `design-system/overrides/admin.md` and `design-system/overrides/player.md` (FR-007/022, research Table MASTER vs Admin vs Player)
- [X] T009 Validate 0 literals via `node scripts/validate-tokens.cjs --dir design-system/` in `design-system/` — must PASS
- [X] T010 Validate breakpoints normative 375/768/1024/1440 and `forced-colors` fallback are documented in `design-system/MASTER.md`

**Checkpoint**: `design-system/MASTER.md` + `design-system/tokens/` + `design-system/overrides/` ready — user stories can now proceed.

---

## Phase 3: User Story 3 — Fundación compartida — Design Tokens y componentes conceptuales (Priority: P1)

**Goal**: Catálogo compartido de tokens y 15 componentes conceptuales como fuente de verdad agnóstica; evita drift Admin↔Player (FR-001..006, FR-010..013, data-model `DesignToken`/`ColorPalette`/`TypographyScale`).

**Independent Test**: Auditar catálogo: `color.*`/`typography.*`/`spacing.*`/`radius.*`/`elevation.*`/`motion.*`/`breakpoint.*` existen con valores y uso; snapshots Admin vs Player coinciden salvo `theme` override; `validate-tokens.cjs` 0 literales en 10 pantallas de muestra (quickstart Scenario 1).

### Implementation for User Story 3

- [X] T011 [P] [US3] Create primitive documentation `design-system/tokens/primitives.md` (color 50–900, spacing 4–64, radius, elevation, breakpoints per `data-model.md` §1/§4)
- [X] T012 [P] [US3] Create semantic tokens reference `design-system/tokens/semantics.md` (primary/secondary/accent/background/surface/feedback + state variants, WCAG AA contrastPairs table)
- [X] T013 [P] [US3] Document typography scale `design-system/tokens/typography.md` (families Fira Sans/Code + Russo/Chakra, clamp() sizes, weights 400/500/600/700, lineHeight/tracking per `data-model.md` §5)
- [X] T014 [P] [US3] Document motion tokens `design-system/tokens/motion.md` (durations 100/200/300/500/800, easings ease-out/ease-in-out/spring, presets fade/slide/scale/timer-pulse/roundTransition + reducedMotionFallback per `data-model.md` §7)
- [X] T015 [P] [US3] Document iconography set `design-system/tokens/iconography.md` (grid 24px, stroke 1.5px, sizes 16/20/24/32, Lucide/Phosphor, a11y decorative vs meaningful vs control per `data-model.md` §8; prohibit emoji)
- [X] T016 [US3] Create component specs core `design-system/components/button.md`, `design-system/components/card.md`, `design-system/components/modal.md`, `design-system/components/drawer.md` per `contracts/components.md` template (anatomy/variants/sizes/states/a11y/responsive/motion/tokensUsed)
- [X] T017 [P] [US3] Create component specs data `design-system/components/table.md`, `design-system/components/input.md`, `design-system/components/select.md`, `design-system/components/badge.md`, `design-system/components/tabs.md` per template
- [X] T018 [P] [US3] Validate component contracts vs `contracts/components.md` catalog (15 MVP) and run `node scripts/validate-tokens.cjs --dir design-system/` — must still PASS

**Checkpoint**: US3 independently auditable — `design-system/tokens/` + `design-system/components/` complete with 0 literals; Admin/Player can extend without drift.

---

## Phase 4: User Story 1 — Administration Experience — Enterprise SaaS operativa (Priority: P1) 🎯 MVP

**Goal**: Blazor Admin `.NET 11` expression (light, dense, professional) for configure/operate/supervise flows; must feel operational, not intrusive (FR-008, SC-001).

**Independent Test**: Operator authenticated (ADMIN/GAME_MANAGER) runs critical flows at 1440px and 1280px: create game (12 fields per SPEC-001), publish category (SPEC-002), create question 4 options (SPEC-003), audit/report (SPEC-014/015) — verify dense table with pagination, inline error `aria-describedby`, focus ring 3:1, SUS≥75 (quickstart Scenario 3, spec US1).

### Implementation for User Story 1

- [X] T019 [P] [US1] Create Admin app shell spec `design-system/screens/admin-shell.md` (navigation lateral/colapsable 240px, filters persistentes,atables dense, WCAG AA, responsive 375→1440 per FR-016/017)
- [X] T020 [P] [US1] Create admin screens `design-system/screens/admin-dashboard.md`, `design-system/screens/game-configuration.md`, `design-system/screens/categories.md`, `design-system/screens/question-bank.md` (each: layout, components, states Loading/Ready/Empty/Error, tokens used, realtime note)
- [X] T021 [P] [US1] Create admin screens `design-system/screens/live-games.md`, `design-system/screens/reports.md`, `design-system/screens/audit.md` with table density `comfortable/compact`, sticky header, pagination, skeleton per `data-model.md` ComponenteConceptual
- [X] T022 [US1] Generate page overrides for admin `design-system/pages/admin-dashboard.md`, `design-system/pages/game-configuration.md`, `design-system/pages/categories.md`, `design-system/pages/question-bank.md` (only deviations from MASTER per `contracts/master-structure.md`)
- [X] T023 [US1] Generate page overrides `design-system/pages/live-games.md` and `design-system/pages/reports.md` (only deviations)
- [X] T024 [US1] Run axe contrast check on admin light theme (`--color-background #F8FAFC` on `#1E3A8A` 12.1:1) and keyboard traversal (tab through table → filters → pagination) — record in `specs/016-ui-ux-design-system/research.md` Addendum

**Checkpoint**: US1 independently demonstrable — Admin expression documented, light theme validated, dense SaaS professional feel without needing Player app.

---

## Phase 5: User Story 2 — Player Experience — Cinemática premium de concurso (Priority: P1)

**Goal**: Angular 22 expression (dark cinematic, tensión/progresión, premium, original no-plagio) for private player game screen (FR-009, FR-024..026, SC-002).

**Independent Test**: Player (5 rounds) across 375/768/1440 — lobby→QuestionActive→AnswerSelected→AnswerLocked→Evaluating→Correct/Incorrect→leaderboard→withdraw/finish; verify centered layout, progress 3/5, timer warning <10s/critical <5s, 4 options states, celebration ≤600ms, leaderboard from ledger, 0 branding copy (spec US2).

### Implementation for User Story 2

- [X] T025 [P] [US2] Create game screen spec `design-system/screens/game-screen.md` (per Addendum 2 §5: question hierarchy, 4 options, progression, level, points, secured points, potential reward, countdown, player status, optional leaderboard, withdraw, feedback; visual effects enhance not reduce usability)
- [X] T026 [P] [US2] Create player flow screens `design-system/screens/player-home.md`, `design-system/screens/game-lobby.md`, `design-system/screens/game-results.md`, `design-system/screens/rewards.md`
- [X] T027 [P] [US2] Create game components `design-system/components/question-card.md`, `design-system/components/answer-option.md`, `design-system/components/timer.md`, `design-system/components/progress.md`, `design-system/components/leaderboard.md` per `contracts/components.md` (states QuestionActive…Consolation, `correct` never before Evaluating, timer-pulse, no solo-color)
- [X] T028 [US2] Document realtime isolation `design-system/screens/realtime-private-session.md` (per FR-025/026 and `contracts/components.md` Realtime Contract: GLOBAL vs PLAYER-SPECIFIC SignalR groups, `Backend→Event→Client→UI`, public vs private info)
- [X] T029 [US2] Generate page overrides for player `design-system/pages/player-home.md`, `design-system/pages/game-lobby.md`, `design-system/pages/game-screen.md`, `design-system/pages/game-results.md`, `design-system/pages/rewards.md` (only deviations from MASTER)
- [X] T030 [US2] Validate Player dark theme contrast (`#F8FAFC on #0F172A` 15.8:1, accent `#F59E0B on #0F172A` 8.2:1) and timer `warning/critical` uses color+icon+text (FR-015)

**Checkpoint**: US2 independently playable in design — cinematic dark, tension/progression, celebration ≤600ms, private session spec, 0 branding replica.

---

## Phase 6: User Story 4 — Accesibilidad, responsive y motion inclusivo (Priority: P2)

**Goal**: WCAG 2.2 AA, responsive 375/768/1024/1440 adaptive (not scaled), motion con propósito + reduced-motion fallback (FR-014..018, SC-004..007).

**Independent Test**: `axe` AA on 8 key flows × 4 breakpoints (375/768/1024/1440) with `prefers-reduced-motion` on/off — contrast ≥4.5:1 normal/3:1 large/focus 3:1, keyboard no trap, landmarks/headings, `aria-*`, `forced-colors` borders ≥44px touch, animations → `fade ≤200ms` (quickstart Scenarios 4+5).

### Implementation for User Story 4

- [X] T031 [P] [US4] Create accessibility spec `design-system/a11y.md` (contrast pairs, focus ring `var(--color-ring)`, keyboard order, `aria-hidden` vs `aria-label`, landmarks, forced-colors CanvasText fallback, no solo-color timer per `contracts/components.md` A11y Contract)
- [X] T032 [P] [US4] Create responsive spec `design-system/responsive.md` (breakpoints 375/768/1024/1440 normative + 360/640/1280/1536 ext, gutters 16/24/32, adaptation table per component: Table cards@375→table@1024, Sidebar drawer@375→fixed@1024, QuestionCard stacked@375→2col@1024 per `data-model.md` §6)
- [X] T033 [P] [US4] Create motion spec `design-system/motion.md` (presets fade/slide/scale/timer-pulse/roundTransition, durations 100–500ms, never blocks TimeLimit, reduced-motion `fade ≤200ms` fallback per `data-model.md` §7)
- [X] T034 [US4] Run axe audits on both themes light (`admin`) and dark (`player`) independently — log 0 failures to `specs/016-ui-ux-design-system/research.md` — include `forced-colors` and `prefers-reduced-motion: reduce` emulations
- [X] T035 [US4] Document states matrix `design-system/states.md` (globals Loading…Completed + game QuestionActive…Consolation per FR-011/013, Addendum 2 §9) with visual examples (skeleton/shimmer, empty CTA, error recovery, focus ring, disabled `aria-disabled`)

**Checkpoint**: US4 passes all gates — AA, keyboard, responsive 0 scroll 320–1536 preserves game-screen essentials, motion purposeful.

---

## Phase 7: User Story 5 — Validación con UI/UX Pro Max Skill y handoff (Priority: P2)

**Goal**: Process via Pro Max prompt canónico Addendum 2 §12, reporte archivado, handoff `<30 min` por componente (FR-019/020/027, SC-008/009).

**Independent Test**: Execute Pro Max checklist: palettes (192 reasoning), typography (74 pairings), spacing, components, motion (17 presets), responsive — record findings/corrections; verify `design-system/MASTER.md` + 11 page overrides exist; new dev builds Button+QuestionCard in both themes <30 min each (quickstart Scenario 7).

### Implementation for User Story 5

- [X] T036 [P] [US5] Run ui-ux-pro-max validation searches: `python3 .opencode/skills/ui-ux-pro-max/scripts/search.py "vibrant block gaming" --domain style` and `python3 .opencode/skills/ui-ux-pro-max/scripts/search.py "enterprise saas dashboard" --domain product` — append findings to `specs/016-ui-ux-design-system/research.md`
- [X] T037 [P] [US5] Create governance doc `design-system/GOVERNANCE.md` (when to create new token/component via ADR, version bump semver, `validate-tokens.cjs` CI check, handoff checklist Blazor vs Angular per FR-020)
- [X] T038 [US5] Generate Visual Quality Gate report `design-system/QUALITY-GATE.md` (Addendum 2 §12 checklist: functional, visual consistency, responsive, a11y, interaction, animation, loading/error/empty, reduced-motion — all must PASS before mark complete)
- [X] T039 [US5] Run handoff dry-run: simulate new dev consuming `design-system/tokens/design-tokens.css` + `design-system/components/button.md` + `question-card.md` to produce both themes — time and log <30 min per component in `specs/016-ui-ux-design-system/quickstart.md` Scenario 7 record

**Checkpoint**: US5 complete — Pro Max report + GOVERNANCE + QUALITY-GATE archived; handoff proven.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Cross-cutting quality, architecture gate, and final validation (affects all stories).

- [X] T040 [P] Audit anti-patterns (§13) on 10 sample screens via `design-system/MASTER.md` — verify 0 of 11 prohibited (Bootstrap generic, default lib, unstyled forms, random gradients, glass/neon excess, emoji, unnecessary animations, inconsistent spacing/typography, hidden loading, missing error, mobile=desktop compressed) — log in `design-system/QUALITY-GATE.md`
- [X] T041 [P] Add Architecture test `tests/OroQuizClash.Architecture.Tests/DesignSystem_NoDirectDb.cs` forbidding `src/Admin/**` or `src/Player/**` → EF Core/DbContext (FR-023, SC-011) — only `QuizArena.Api` calls
- [X] T042 Run full quickstart validation `specs/016-ui-ux-design-system/quickstart.md` Scenarios 0–8 sequentially — all must PASS (MASTER exists, 0 literals, contrast AA, keyboard, responsive, reduced-motion, Gate, anti-patterns, handoff, architecture)
- [X] T043 [P] Update documentation `README.md` and `docs/adr/ADR-012-design-system.md` with decision: MASTER shared + overrides, prompt canónico, palette #2563EB/#F59E0B, typography Fira/Russo::Chakra, 375/768/1024/1440, and roadmap SPEC-017..036 dependency

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories** (Design System First, Addendum 2 §3).
- **User Stories (Phases 3–7)**: All depend on Foundational completion.
  - Within P1 group: US3 (foundation catalog) is logically first but marked as Phase 3; US1 (Admin) and US2 (Player) can then proceed in parallel (staffed) — both consume same MASTER/overrides.
  - P2 group (US4, US5) depends on P1 catalog being stable; can run in parallel after P1.
- **Polish (Phase 8)**: Depends on all desired stories (at minimum P1: US1+US2+US3) being complete.

### User Story Dependencies

- **US3 (P1) — Fundación compartida (Phase 3)**: After Foundational — no dependency on other stories; validates shared primitives.
- **US1 (P1) — Administration (Phase 4)**: After Foundational + US3 catalog — uses `design-system/overrides/admin.md` light/density.
- **US2 (P1) — Player (Phase 5)**: After Foundational + US3 — uses `design-system/overrides/player.md` dark/cinematic; shares component API with US1 (same `Button`, `QuestionCard`).
- **US4 (P2) — A11y/Responsive/Motion (Phase 6)**: After US1+US2+US3 — cross-cuts both expressions; verifies 4 breakpoints, AA, reduced-motion.
- **US5 (P2) — Pro Max validation & handoff (Phase 7)**: After US3+US4 — generates governance + quality gate + handoff proof.

### Within Each User Story

- Generate/validate tokens before documenting components.
- Document anatomy/variants/states before screens.
- Screens before page overrides (only deviations).
- Run validation (axe/validate-tokens/keyboard) before marking story complete.

### Parallel Opportunities

- T002 + T003 (token copy + scripts) can run in parallel.
- T006 + T007 + T008 (json + css + overrides) parallel within Foundational (different files).
- T011–T015 (primitives/semantics/typography/motion/iconography) all [P] parallel — different `tokens/*.md` files.
- T016–T017 component specs [P] parallel (different `components/*.md`).
- US1 Phase 4: T019+T020+T021 (screens) parallel (different `screens/*.md`).
- US2 Phase 5: T025–T027 screens/components parallel.
- Once Foundational done, US1 and US2 could be staffed in parallel (different `overrides/`/`pages/`).
- All exhibit different file targets — no [P] conflict with same-file writes.

---

## Parallel Example: User Story 3 (Fundación)

```bash
# Launch all token docs for US3 together (different files):
Task: "Create primitive documentation in design-system/tokens/primitives.md"
Task: "Create semantic tokens reference in design-system/tokens/semantics.md"
Task: "Document typography scale in design-system/tokens/typography.md"
Task: "Document motion tokens in design-system/tokens/motion.md"
Task: "Document iconography set in design-system/tokens/iconography.md"

# Then component specs together:
Task: "Create component specs core in design-system/components/button.md"
Task: "Create component specs data in design-system/components/table.md"
```

## Parallel Example: User Story 1 (Admin) + User Story 2 (Player)

```bash
# After Foundational, two developers in parallel:
Developer A - US1: design-system/screens/admin-dashboard.md, design-system/pages/admin-dashboard.md
Developer B - US2: design-system/screens/game-screen.md, design-system/pages/game-screen.md
# No file overlap — both read MASTER.md, write different overrides/pages.
```

---

## Implementation Strategy

### MVP First (Foundation + US3 + US1)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (MASTER + tokens + overrides — BLOCKING)
3. Complete Phase 3: US3 — Shared foundation catalog (tokens/components) — foundation auditable
4. Complete Phase 4: US1 — Administration SaaS (light) — **MVP demo**: operator creates game, publishes category, creates question at 1440px with SUS≥75 and axe AA PASS
5. **STOP and VALIDATE**: quickstart Scenarios 0–4 (admin slice) + `validate-tokens.cjs`
6. Deploy/demo if ready — Player not yet required for admin MVP

### Incremental Delivery

1. Setup + Foundational → `design-system/MASTER.md` exists
2. Add US3 → Foundation catalog audit PASS (0 literals)
3. Add US1 → Admin MVP (dense SaaS) PASS
4. Add US2 → Player cinematic (dark) — both apps now share same MASTER, 0 branding copy
5. Add US4 → A11y/Responsive/Motion — axe + reduced-motion + 0 scroll PASS
6. Add US5 → Pro Max governance + quality gate + handoff <30min PASS
7. Polish → Anti-patterns 0, architecture NoDirectDb, full quickstart 0–8 PASS
- Each story adds value without breaking previous (same `design-tokens.json` API)

### Parallel Team Strategy

With multiple developers after Foundational:
1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: US3 (tokens/components)
   - Developer B: US1 (admin screens/pages)
   - Developer C: US2 (player screens/pages)
3. Afterwards: joint US4 (a11y/responsive) + US5 (governance/handoff) — cross-cutting, needs both expressions stable
4. Polish together

---

## Notes

- [P] = different files, no dependencies — safe to parallelize
- [Story] = US1..US5 traceability to spec.md user stories (US1 Admin P1, US2 Player P1, US3 Foundation P1, US4 A11y P2, US5 Pro Max P2)
- Each story independently testable via its Independent Test + quickstart scenario
- Commit after each task or logical group; stop at any checkpoint to validate story independently
- Avoid vague tasks — every task names exact file path per `plan.md` Project Structure (`design-system/MASTER.md`, `design-system/tokens/`, `design-system/components/`, `design-system/screens/`, `design-system/overrides/`, `design-system/pages/`)
- No Backend code in this feature — only `design-system/` docs + Architecture test; no `IRepository`/`Specification`/`Migration`
- `validate-tokens.cjs` and `axe` are the test gates — must PASS before marking story complete

