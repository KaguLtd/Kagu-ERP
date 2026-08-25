# MP-03 Journal Reversal Invariants Technical Spike

## Goal

Create an immutable, exact opposite journal draft linked to its original journal identity without mutating or deleting the original financial record.

- Master phase/backlog: MP-03 / item 14
- Risk: R4 — posted-record immutability, correction traceability and duplicate financial effect
- Status: Completed locally — awaiting user-directed commit/push
- Requirement: `ACC-INV-003`

## Definition of Ready

Conditionally satisfied for a reversible domain-only spike. `DEC-MP01-003`, `DEC-MP01-008`, `DEC-MP01-012` and named owners remain open or `atanmadı`; therefore real reversal permissions, approvals, effective-date/correction-period selection and persistent posted state are not selected.

## Scope

Included:

- An immutable reversal draft explicitly linked to a non-empty original journal ID.
- Exact debit/credit inversion while preserving account, source-line, dimensions and currency calculation snapshots.
- Caller-supplied reversal posting-rule version, source type, posting purpose, effective date and UTC recorded timestamp.
- In-memory duplicate reversal-intent rejection for the same tenant, company and original journal ID.
- Linkage, exact inverse, scope, deterministic ordering and input immutability checks.

Excluded:

- Posted-journal persistence, authorization, maker-checker, period/reopen workflow or correction-period selection.
- Database uniqueness, concurrency, audit, outbox, API and clients.
- Reversal cancellation, reversal-of-reversal, partial reversal, correction journal content or repost.
- Tax, legal filing, exchange revaluation or external declaration impact decisions.

The source object is an immutable caller-supplied validated journal snapshot, not proof that a journal was posted. The produced result remains a draft and cannot be represented as a production reversal until persistence, authorization, period, audit and idempotency gates are implemented atomically.

## Milestones

- [x] Record scope, requirement traceability and policy blocks.
- [x] Add original-journal-linked immutable reversal draft.
- [x] Reverse every line and currency snapshot exactly without changing dimensions.
- [x] Reject missing context and duplicate reversal intents.
- [x] Prove source immutability and deterministic results.
- [x] Pass full local repository verification.

## Evidence

- Debug and Release builds completed with zero warnings and zero errors.
- Domain invariant harness passed all 30 checks; architecture harness passed all 10 project checks.
- Exact inverse tests prove debit/credit and transaction-currency sides reverse while account, source-line, dimensions, rate, rounding and original journal content remain unchanged.
- Missing original identity, rule/source/purpose context, non-UTC recorded time, empty/null sets and same-company duplicate reversal intents are rejected.
- Reversal intents are isolated by tenant/company/original journal identity, copied into an immutable collection and deterministically ordered.
- Web lint, typecheck, component tests and production build passed.
- Real PostgreSQL migration/RLS, isolated restore/outbox/auth and Keycloak token-scope smoke checks passed.
- Android lint, repository/ViewModel unit tests and instrumentation build passed.
- `dotnet format --verify-no-changes` and `git diff --check` passed.
- No permission, approval, period, correction-date or posted-persistence policy was inferred, and the MP-03 gate did not advance.
- No commit, push or PR was created, per user instruction.

## Deferred Decisions

- Who may reverse, which approvals apply and whether maker-checker is mandatory require approved IAM/workflow policy.
- Reversal effective date, closed-period handling and correction-period disclosures require approved accounting policy.
- Persistent single-active-reversal enforcement requires a PostgreSQL unique constraint and transaction-level concurrency tests.
- Per user instruction, this local slice will not be committed, pushed or opened as a PR until explicitly requested.
