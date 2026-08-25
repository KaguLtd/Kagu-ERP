# MP-03 Party Due Schedule and Open Item Technical Spike

## Goal

Model immutable installment schedules and derive open-item remaining amounts from append-only allocation/write-off facts at an explicit effective-date and recorded-time cutoff.

- Master phase/backlog: MP-03 / item 15
- Risk: R4 — receivable/payable completeness, as-of correctness and tenant/company isolation
- Status: Completed locally — awaiting user-directed commit/push
- Requirements: `PARTY-DUE-001`, `PARTY-DUE-002`, `PARTY-OI-001`, `PARTY-OI-002`

## Definition of Ready

Conditionally satisfied for a reversible domain-only spike. `DEC-MP01-004`, `DEC-MP01-006`, `DEC-MP01-009` and named owners remain open or `atanmadı`; therefore real payment terms, FX, rounding residual, auto-allocation, write-off approval and persistent balance policy are not selected.

## Scope

Included:

- Immutable due-schedule lines scoped by tenant, company, party account and source event.
- Explicit original currency/amount, due date, payment-term snapshot version and control-account identity.
- Exact schedule total validation with duplicate rejection and deterministic immutable ordering.
- Append-only allocation, unallocation, write-off and write-off-reversal impact snapshots.
- Exact counter-event linkage and effective-date plus recorded-time as-of derivation.
- Scope, currency, capacity, bitemporal ordering and immutability checks.

Excluded:

- Party/person/address/contact master data and actual invoice/payment persistence.
- Cross-currency allocation, functional amount, rate/rounding and residual treatment.
- Dispute/block status, credit notes, advances, payment terms calculation or aging bucket policy.
- Authorization, approval, concurrency locks, database constraints, audit, outbox, API and clients.

All events and balances are caller-supplied immutable snapshots. They are not authoritative posted facts and do not provide production concurrency protection. Remaining amount is calculated and never accepted as caller input.

## Milestones

- [x] Record requirement IDs, scope and deferred decisions.
- [x] Add immutable due-schedule line and exact-total schedule.
- [x] Add append-only open-item impact and counter-event contracts.
- [x] Derive allocation, write-off and remaining amounts at an explicit as-of cutoff.
- [x] Prove boundary, scope, linkage, capacity, ordering and immutability behavior.
- [x] Pass full local repository verification.

## Evidence

- Debug and Release builds completed with zero warnings and zero errors.
- Domain invariant harness passed all 35 checks; architecture harness passed all 10 project checks.
- Due-schedule tests cover required identities, positive decimal amounts, payment-term version, exact total, overflow, duplicate rejection, tenant/company/party/source/currency scope and immutable deterministic ordering.
- Open-item tests prove effective-date plus recorded-time cutoff behavior, allocation/unallocation and write-off/reversal pairs, exact counter-event linkage, capacity, overflow, scope isolation and immutable history copies.
- Remaining amount is only derived from original amount and considered append-only impacts; no mutable caller-supplied balance is accepted.
- Web lint, typecheck, component tests and production build passed.
- Real PostgreSQL migration/RLS, isolated restore/outbox/auth and Keycloak token-scope smoke checks passed.
- Android lint, repository/ViewModel unit tests and instrumentation build passed.
- `dotnet format --verify-no-changes` and `git diff --check` passed; new financial code contains no `float` or `double` usage.
- No payment-term, FX, rounding, dispute, write-off approval or concurrency policy was inferred, and the MP-03 gate did not advance.
- No commit, push or PR was created, per user instruction.

## Deferred Decisions

- Payment-term calculation, holiday rules and due-date adjustment require approved business policy.
- FX, functional amounts and rounding residual require approved currency/rate/rounding policy.
- Partial unallocation/write-off reversal, dispute state and approval thresholds require approved accounting/workflow policy.
- Persistent capacity enforcement requires PostgreSQL locking/constraints and concurrency tests.
- Per user instruction, this local slice will not be committed, pushed or opened as a PR until explicitly requested.
