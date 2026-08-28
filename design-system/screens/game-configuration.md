# Screen: Game Configuration

**Route**: `/admin/games/new` + `/admin/games/{id}/edit` | **Theme**: administration | **Roles**: ADMIN, GAME_MANAGER

## Layout

- Page header: title + status Badge (Draft/Published/Live/Finished)
- Form Card, 12 fields (SPEC-001): Name, Description, Category (Select), Difficulty (Select), Rounds, QuestionsPerRound, TimeLimit (sec), MinPlayers, MaxPlayers, EntryFee?, RewardPool?, Publish toggle
- 2-col form@1024 (label-left dense), stacked@375
- Sticky footer actions: Save draft (secondary), Publish (primary), Delete (destructive ghost, confirm Modal)

## Components

Input, Select, Card, Button, Modal (delete confirm), Badge, Toast (save feedback inline)

## States

- Loading: skeleton form (edit mode fetch)
- Ready: editable
- Error: inline per-field `aria-describedby` + top summary on submit fail
- Disabled: Live game → fields locked except publish stop (Badge `Live`)
- Success: Toast "Game saved" + stay (no disruptive redirect)

## Validation

Inline on blur + on submit; TimeLimit ≥ 5s; Rounds 1–10; MinPlayers ≤ MaxPlayers; error icon+text (no solo-color).

## Tokens Used

`--color-card`, `--color-border`, `--color-destructive`, `--space-3/4/6`, `--radius-md/lg`, `--typography-body-m`

## Realtime Note

If game becomes Live while editing → banner "Game is live, fields locked" via GLOBAL `GameStarted`.

## Responsive

375: stacked full-width, sticky bottom action bar; 1024/1440: 2-col fields, sticky footer.
