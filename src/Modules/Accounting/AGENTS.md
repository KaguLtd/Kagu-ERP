# Accounting module rules

- Before changing this module, read `docs/modules/09-accounting-general-ledger.md`, `docs/00-foundation/04-data-architecture.md`, `docs/00-foundation/07-cross-cutting-workflows.md` and the active MP-03 task plan.
- Never use `float` or `double` for money, quantity, rates or percentages. Use `decimal` and keep rounding/scale policy explicit and versioned.
- Do not hard-code account codes, currency policy, exchange-rate sources, tax rules, period behavior or approval thresholds while their MP-01 decisions are open.
- A validated draft is not a posted journal. Posting requires authorization, period/account validation, idempotency, persistence, audit and outbox in one PostgreSQL transaction.
- Booked or posted financial records are append-only. Corrections create linked reversal/correction records; they do not mutate or delete the original.
- Domain projects cannot reference Application, Infrastructure, API, database or provider types.
- Every invariant change requires named requirement IDs and boundary/property checks.
