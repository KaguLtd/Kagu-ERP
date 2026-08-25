# Treasury module rules

- Read `docs/modules/07-banking-cash.md`, the data architecture, cross-cutting workflows and the active task plan before changing this module.
- Use `decimal` for all money and rate values; keep transaction and functional amounts explicit.
- Payment, allocation, bank settlement and reconciliation are separate facts and states.
- Posted bank/cash movements are append-only; correction creates linked counter-events.
- Do not hard-code bank/provider, currency, FX, transit-account, approval or reconciliation policy while MP-01 decisions are open.
- Treasury Domain cannot reference Party, Accounting, Infrastructure, API, database or provider types.
- Every invariant change requires named requirement IDs and boundary/property checks.
