# Screen: Admin Shell (QuizArena.Admin — Blazor .NET 11)

**Theme**: `[data-theme="administration"]` — light, dense, professional Command Center (overrides/admin.md)
**Roles**: ADMIN, GAME_MANAGER (SPEC-010 RBAC); auth via OroIdentityServer `/Account/*` redirect (not redesigned)

## Layout

- **Sidebar** 240px: fixed@1024+, collapsible to 64px icon rail; drawer overlay@375–768. Sections: Dashboard, Games, Categories, Questions, Live, Reports, Audit, Players.
- **Topbar** 56px: breadcrumb, global search, role badge, user menu, theme locked light.
- **Content**: 12-col grid (gutter 32@1024/1440, 24@768, 16@375), max-width 1440 centered, page header (title + primary action right).
- **Filters**: persistent left rail or top bar within list pages — never hidden behind hero.

## Components

Drawer (nav), Table (dense), Card (stat), Button, Input/Select (filters), Badge (status), Tabs, Toast (inline feedback), Modal (confirm only).

## States

- Loading: skeleton shell (sidebar + table rows shimmer)
- Ready: content
- Empty: per-page CTA
- Error: global banner (retry) + per-widget error card
- Session expired → redirect OroIdentityServer login (toast before redirect)

## Tokens Used

`--color-background #F8FAFC`, `--color-primary #1E40AF`, `--space-2..6` dense, `--radius-md/lg`, `--elevation-1`, `--typography-font-heading: Fira Code`

## A11y

Landmarks: `nav` (sidebar), `banner` (topbar), `main`; skip-link first; focus ring `var(--color-ring)` 3:1; logical tab order sidebar→filters→content→pagination; contrast AA light (12.1:1 body).

## Responsive

375: drawer nav + card lists; 768: drawer + tables; 1024: docked sidebar collapsible; 1440: fixed sidebar + 12-col.

## Realtime Note

Shell subscribes GLOBAL events only (`GameStarted`, `GameFinished`) for Live badge count; no PLAYER-SPECIFIC events rendered in Admin shell (privacy §11).
