# Page Override: rewards

> Overrides `design-system/MASTER.md` for `/rewards`. Only deviations listed.

- Balance figures use Russo One display + `--color-accent-text` — money is the hero here.
- Withdraw confirm Modal shows fee breakdown table + arrival estimate; requires explicit amount > 0 (button disabled otherwise).
- History status Badges: Pending (warning), Paid (success-text), Failed (destructive) — all icon+text.
- Amount Input uses inputmode=decimal + currency suffix; inline validation min/max from API config.
