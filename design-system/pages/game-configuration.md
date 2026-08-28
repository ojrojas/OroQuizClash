# Page Override: game-configuration

> Overrides `design-system/MASTER.md` for `/admin/games/*`. Only deviations listed.

- Form uses label-left layout@1024+ (160px label col) instead of MASTER stacked labels — dense data entry.
- Sticky action bar bottom with `--color-card` bg + `--elevation-2` top border; overrides default footer spacing.
- TimeLimit/Rounds numeric inputs use `Fira Code` for alignment.
- Live-locked state: all inputs `disabled` + info banner; Publish button becomes "Stop game" destructive — variant swap deviation.
- Delete requires typed-confirm Modal (type game name) — stronger than default confirm.
