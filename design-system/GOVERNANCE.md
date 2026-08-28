# Design System Governance

## Authority

- Source of truth: `design-system/MASTER.md` + `design-system/tokens/design-tokens.json` (v1.0.0)
- Changes require PR review; breaking changes require ADR (see below)
- Constitution Addendum 2 overrides this document where in conflict

## When to Add a Token

1. Value is used in ≥2 components/screens, OR
2. Value needs per-theme divergence (admin vs player)
Add to **semantic** layer with theme override; new raw values go to **primitive** first.
Never add component-specific hex directly — route through semantic.

## When to Add a Component

1. Used in ≥2 screens, OR required by a spec user story
2. Create `design-system/components/<name>.md` from `contracts/components.md` template (anatomy/variants/states/props/tokens/a11y/responsive/motion)
3. Register in MASTER §9 catalog table
4. Both apps must be able to consume same API (theme-only differences)

## Versioning (semver on `design-tokens.json` `version`)

- **MAJOR**: removed/renamed tokens, changed semantics (requires ADR + migration notes)
- **MINOR**: new tokens/components, new theme overrides
- **PATCH**: doc fixes, contrast pair additions, value tweaks within same role

## CI Checks (required at SPEC-017+ pipelines)

```bash
node .opencode/skills/design-system/scripts/validate-tokens.cjs --dir design-system/
node .opencode/skills/design-system/scripts/validate-tokens.cjs --dir src/Admin/
node .opencode/skills/design-system/scripts/validate-tokens.cjs --dir src/Player/
```
0 literals outside token catalog; axe-core AA per theme; architecture test `DesignSystem_NoDirectDb`.

## ADR Triggers

New font family, new color role, breakpoint change, motion preset addition, component API break → `docs/adr/ADR-0XX-*.md`.

## Handoff Checklist — Blazor Admin (SPEC-017+)

1. Reference `design-tokens.css`; set `<html data-theme="administration">`
2. Map tokens to CSS isolation or site theme; no raw hex in `.razor`/`.css`
3. Components per `design-system/components/*.md` + `screens/admin-*.md`
4. Validate: `validate-tokens --dir src/Admin`, axe AA light, keyboard pass

## Handoff Checklist — Angular Player (SPEC-027+)

1. Reference `design-tokens.css`; set `data-theme="player"` on app shell
2. Signals-based state per `screens/realtime-private-session.md`; no direct DB/EF
3. Components per `design-system/components/*.md` + `screens/*.md` + `pages/*.md` overrides
4. Validate: `validate-tokens --dir src/Player`, axe AA dark, 375/768/1024/1440, reduced-motion

## Review Cadence

- Every feature touching UI: PR must cite consumed tokens/components
- Quarterly: re-run Pro Max searches for drift; bump version
