# Screen: Categories

**Route**: `/admin/categories` | **Theme**: administration | **Roles**: ADMIN, GAME_MANAGER

## Layout

- Toolbar: search Input, status Select (All/Draft/Published), Button primary "New category"
- Table dense: Name, Question count, Difficulty mix, Status Badge, Updated, Actions (edit/publish/archive)
- Pagination footer (25/50/100)

## Components

Table (dense), Input, Select, Button, Badge, Modal (archive confirm), Toast

## States

- Loading: skeleton rows
- Ready: data
- Empty: illustration + CTA "Create your first category" (SPEC-002)
- Error: retry banner
- Publish action: inline confirm → Badge Draft→Published transition fade

## Tokens Used

`--color-card`, `--color-border`, `--color-success` (Published), `--color-muted` (Draft), `--space-3/4`, `--radius-md`

## A11y

Sortable columns `aria-sort`; row actions keyboard; status Badge never color-only (label text).

## Realtime Note

None required (CRUD); optimistic UI with rollback Toast on API error.

## Responsive

375: card list (name + status + actions); 1024+: dense table + persistent toolbar.
