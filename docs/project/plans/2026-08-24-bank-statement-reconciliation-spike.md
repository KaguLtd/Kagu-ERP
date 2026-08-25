# MP-03 Bank Statement and Reconciliation Technical Spike

## Goal

Establish canonical imported-statement-line identity and immutable many-to-many reconciliation proposal boundaries without selecting a bank adapter, approval tolerance or accounting policy.

- Master phase/backlog: MP-03 / item 17
- Risk: R4 — duplicate bank evidence, cross-scope matching and over-reconciliation
- Status: Completed locally — awaiting user-directed commit/push
- Requirements: `BNK-INV-003`, `BNK-STMT-001`, `BNK-REC-001`, `BNK-REC-002`

## Definition of Ready

Conditionally satisfied for a reversible domain-only spike. Bank profiles, statement formats, currencies, tolerance, approval ownership, fees/FX and GL policy remain open or `atanmadı`; therefore this slice validates caller-supplied normalized evidence and proposals but does not import, approve, settle or post anything.

## Scope

Included:

- Canonical statement-line external identity scoped by tenant, company and treasury account.
- Immutable normalized line draft with signed decimal amount, currency, booking/value dates, UTC recorded time, raw-object SHA-256 and parser version.
- Duplicate statement-line identity and tenant-scoped line-ID rejection.
- Immutable internal movement capacity snapshot and many-to-many reconciliation match proposal.
- Same-scope/account/currency validation, pair uniqueness and aggregate capacity checks on both sides.
- Boundary, duplicate, ordering, immutability and architecture checks.

Excluded:

- File upload/parsing, malware scanning, encryption, provider adapters and bank-specific fingerprints.
- Statement opening/closing balance, sequence and file-level control totals.
- Match scoring, tolerance, approval, maker-checker, period locks and approved reconciliation lifecycle.
- Payment mutation, bank settlement, allocation, fees, FX, suspense events, persistence, API, outbox or GL posting.

## Milestones

- [x] Record requirement traceability and deferred policy boundaries.
- [x] Add canonical statement-line identity and immutable normalized line draft.
- [x] Add duplicate-safe deterministic statement-line set.
- [x] Add scoped internal-movement capacity and many-to-many reconciliation proposal.
- [x] Prove scope, currency, direction, amount, capacity, uniqueness, ordering and immutability behavior.
- [x] Pass full local repository verification.

## Verification Evidence

- Debug and Release builds passed with zero warnings and zero errors; locked restore remained current.
- Domain quality harness passed all 44 checks. New checks cover normalized identity, signed decimal amount, UTC/raw-hash/parser evidence, tenant-scoped deduplication, incoming/outgoing direction, cross-scope/currency/account rejection, many-to-many matching, pair uniqueness, aggregate capacity, deterministic ordering and collection immutability.
- Architecture harness passed for all 11 source projects; the new statement and reconciliation types remain inside independent Treasury.Domain.
- Web lint, TypeScript typecheck, Vitest (2 tests) and production build passed.
- Real PostgreSQL migration idempotency and tenant/company RLS checks passed; Keycloak permission-scope smoke, isolated restore/migration/scope/outbox/auth smoke and Android lint/unit/instrumentation build gates passed.
- Full `scripts/verify.ps1` completed successfully. Its formatting gate first identified two whitespace-only issues; both were corrected before the successful rerun.
- No provider adapter, external-key derivation, tolerance, score, approval, bank settlement, payment/allocation mutation, fee/FX event or GL policy was inferred.
- No commit, push or PR was created, per user instruction.

## Deferred Decisions

- External identity/fingerprint derivation belongs to a versioned provider/profile adapter.
- Tolerance, score threshold and automatic/manual approval require approved reconciliation policy.
- Approved-match correction must use an append-only counter-event under `BNK-INV-005`; this proposal slice does not authorize approval or mutation.
- Persistent deduplication and concurrent approval require PostgreSQL constraints and transaction-level tests.
- No commit, push or PR will be created until explicitly requested by the user.
