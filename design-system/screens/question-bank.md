# Screen: Question Bank

**Route**: `/admin/questions` | **Theme**: administration | **Roles**: ADMIN, GAME_MANAGER

## Layout

- Toolbar: search Input, Category Select, Difficulty Select, status filter Tabs (All/Draft/Published/Archived)
- Table dense: Question (truncated 60ch), Category, Difficulty Badge, Options count (must be 4), Status, Updated, Actions
- Side Drawer (right 400px): question editor — prompt textarea, 4 option Inputs, correct radio, explanation, difficulty Select, Save/Publish (SPEC-003)

## Components

Table (dense), Input, Select, Tabs, Drawer, Button, Badge, Modal (delete confirm), Toast

## States

- Loading: skeleton rows + drawer skeleton
- Ready: data
- Empty: CTA "Create your first question"
- Error: inline drawer field errors `aria-describedby`
- Validation: exactly 4 options, exactly 1 correct, prompt ≥ 10 chars — inline icon+text

## Tokens Used

`--color-card`, `--color-border`, `--color-primary`, `--space-3/4`, `--radius-md`, `--elevation-3` (drawer)

## A11y

Correct-option radio group labelled; drawer focus trap + Esc; table keyboard nav; contrast AA.

## Realtime Note

None (CRUD); if question used by Live game → lock Badge "In use" (read-only drawer).

## Responsive

375: card list + full-screen drawer sheet; 1024+: table + docked right drawer.
