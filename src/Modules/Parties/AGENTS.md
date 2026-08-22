# Parties module rules

- Read `docs/modules/03-party-current-accounts.md`, the data architecture, cross-cutting workflows and the active task plan before changing this module.
- Use `decimal` for money. Payment, allocation, unallocation, write-off and reconciliation are separate facts.
- Open-item remaining is derived from append-only facts. A validation capacity is only a snapshot and is never an authoritative mutable balance.
- A posted allocation is immutable; correction requires a linked unallocation counter-event.
- Do not implement cross-currency allocation without an approved, versioned rate, functional-currency and rounding snapshot.
- The Domain project must not reference another module, infrastructure, API, database or provider package.
- Every invariant needs a requirement ID and boundary/property-oriented tests.
