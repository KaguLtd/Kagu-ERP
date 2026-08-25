# MP-03 Party Allocation Ledger Technical Spike

## Goal

Link immutable allocation impacts explicitly to a payment and derive payment used/remaining capacity from allocation/unallocation history without mutating the payment or original allocation.

- Master phase/backlog: MP-03 / item 16
- Risk: R4 — payment capacity, append-only correction and duplicate financial effect
- Status: Completed locally — awaiting user-directed commit/push
- Requirement: `PARTY-INV-004`; reinforces `PARTY-INV-001/002/003`

## Definition of Ready

Conditionally satisfied for a reversible domain-only spike. `DEC-MP01-004`, `DEC-MP01-008`, `DEC-MP01-009`, `DEC-MP01-010`, `DEC-MP01-012` and named owners remain open or `atanmadı`; therefore real Treasury payment posting, allocation approval, FX, GL and bank-reconciliation behavior are not selected.

## Scope

Included:

- Explicit payment identity on allocation and unallocation impact snapshots.
- Exact unallocation linkage to the original allocation with matching payment, due line, currency and amount.
- Tenant/company/party-account/payment/currency scoped payment-allocation history.
- Effective-date plus recorded-time as-of derivation of allocated and remaining usable payment amounts.
- Duplicate event/reversal rejection, capacity enforcement, deterministic immutable ordering and boundary checks.

Excluded:

- Authoritative Payment economic-event persistence or direct Treasury module/database access.
- Cross-currency allocation, realized FX, rounding residual, write-off and advance application policy.
- Journal generation, posting, database idempotency/concurrency, audit, outbox, API and clients.
- Bank settlement/reconciliation state and payment cancellation/reversal.

Payment capacity and impact events remain caller-supplied immutable snapshots. They do not prove that a payment or allocation was posted and cannot protect production concurrency without PostgreSQL transaction-level enforcement.

## Milestones

- [x] Record requirement traceability, scope and deferred decisions.
- [x] Add explicit payment linkage to allocation/unallocation impacts.
- [x] Enforce exact append-only unallocation counter-event identity.
- [x] Derive used and remaining payment capacity at an explicit as-of cutoff.
- [x] Prove scope, capacity, duplicate, ordering and immutability behavior.
- [x] Pass full local repository verification.

## Evidence

- Debug and Release builds completed with zero warnings and zero errors.
- Domain invariant harness passed all 38 checks; architecture harness passed all 10 project checks.
- Allocation/unallocation tests cover two due lines, late-recorded events, effective-date cutoffs, exact capacity restoration and immutable deterministic history.
- Missing/wrong payment identity, tenant/company/party/currency mismatch, invalid event kind, duplicate events, capacity overflow and decimal overflow are rejected.
- Unallocation tests reject missing originals, wrong due-line context, amount mismatch, earlier counter-events, duplicate counters and chained unallocation.
- Payment used/remaining amounts are derived only from considered immutable impacts; payment usable amount and original allocation are never mutated.
- Web lint, typecheck, component tests and production build passed.
- Real PostgreSQL migration/RLS, isolated restore/outbox/auth and Keycloak token-scope smoke checks passed.
- Android lint, repository/ViewModel unit tests and instrumentation build passed.
- `dotnet format --verify-no-changes` and `git diff --check` passed.
- No Treasury persistence, bank settlement, FX, approval, GL posting or database concurrency policy was inferred, and the MP-03 gate did not advance.
- No commit, push or PR was created, per user instruction.

## Deferred Decisions

- Payment lifecycle, cancellation/reversal and bank settlement belong to Treasury and require approved policy.
- Allocation approval, partial-unallocation and write-off rules require approved accounting/workflow policy.
- Cross-currency application requires approved functional-currency, rate and rounding snapshots.
- Idempotent journal persistence requires PostgreSQL unique constraints, locks, audit and outbox in one transaction.
- Per user instruction, this local slice will not be committed, pushed or opened as a PR until explicitly requested.
