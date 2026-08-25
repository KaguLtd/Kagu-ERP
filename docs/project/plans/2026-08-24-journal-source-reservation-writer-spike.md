# MP-03 Journal Source Reservation Writer Technical Spike

## Goal

Provide an Accounting-owned PostgreSQL adapter that reserves a validated journal draft inside the caller's existing transaction, returns the existing reservation for an exact retry, and fails closed when the same source identity is reused with different validated content.

- Master phase/gate: MP-03 / backlog item 20 application-concurrency evidence
- Risk: R4 — financial idempotency, transaction ownership and cross-company access
- Status: Completed locally — awaiting user-directed commit/push
- Requirements: `ACC-INV-005`, API idempotency conflict semantics, transaction/outbox atomicity boundary
- Definition of Ready: Conditional pass for a transaction-bound adapter. Real journal persistence, permission codes and posting policy remain deferred.

## Scope

### Included

- Accounting Infrastructure project with no cross-module dependency.
- Version-pinned deterministic SHA-256 fingerprint of every validated journal-draft field, line, dimension and currency snapshot.
- Caller-owned `NpgsqlConnection` + `NpgsqlTransaction`; the adapter cannot commit independently.
- Execution-scope validation before SQL.
- Same source/same fingerprint returns the original reservation; same source/different fingerprint throws a typed conflict.
- PostgreSQL integration tests for exact retry, semantic line-order normalization, changed-content conflict, scope denial and rollback.

### Excluded

- Journal/header/line insertion, authorization permission selection, HTTP endpoint and response caching.
- Audit/outbox composition; the caller transaction boundary is prepared but not yet populated with those facts.
- Business account, period, tax, approval or reversal policy.

## Safety Boundaries

- The adapter accepts only `ValidatedJournalDraft`; it does not validate or invent business posting policy.
- It never opens, commits or rolls back the supplied transaction.
- Fingerprint algorithm is explicitly V1 and fails closed if future code changes without a migration/compatibility decision.
- Reservation remains non-financial and cannot be queried as a posted journal.

## Milestones

| No | Vertical slice | Verification | Status |
|---:|---|---|---|
| 1 | Add Accounting Infrastructure boundary | Architecture checks | completed |
| 2 | Add deterministic V1 fingerprint and writer | Exact retry/conflict tests | completed |
| 3 | Prove transaction and scope behavior | Rollback and negative-scope tests | completed |
| 4 | Run full repository gates | Full verification | completed |

## Completion Evidence

- [x] Same validated content is idempotent and returns the first reservation ID.
- [x] Reordered equivalent lines and decimal scale variants fingerprint identically; changed financial content raises `JournalSourceReservationConflictException`.
- [x] Scope mismatch fails before SQL and caller rollback leaves no reservation.
- [x] Full repository verification passes: zero-warning .NET build, 52 domain checks, 13-project architecture checks, six web tests/build, current/empty/restored PostgreSQL checks, Keycloak and Android gates.
- [x] No commit, push or PR was created.

## Progress Log

- 2026-08-24: Added the Accounting Infrastructure project and a V1 length-prefixed canonical fingerprint covering journal context, rule, dates, currency, normalized lines, dimensions and currency snapshots.
- 2026-08-24: Added transaction-bound reservation behavior and real PostgreSQL checks for exact retry, reordered lines, changed-content conflict, scope denial and rollback.
- 2026-08-24: Windows Application Control blocked a newly generated module DLL at runtime with `0x800711C7`. Security policy was not weakened; Accounting source is linked into the established allowed architecture harness while the standalone project still builds independently.
- 2026-08-24: Full repository verification passed. Real journal/audit/outbox persistence and HTTP idempotency remain deferred.
