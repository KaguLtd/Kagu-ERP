# MP-03 Journal Reservation, Audit and Outbox Atomicity Technical Spike

## Goal

Prove on real PostgreSQL that a validated journal-draft reservation, its audit evidence and its outbox intent can share one caller-owned transaction and cannot survive partially.

- Master phase/gate: MP-03 / backlog item 20 transactional atomicity evidence
- Risk: R4 — partial financial intent, missing audit and orphan external event
- Status: Completed locally — awaiting user-directed commit/push
- Requirements: `ACC-INV-005`, posting pipeline step 7, transactional outbox and audit requirements
- Definition of Ready: Conditional pass. This test composes existing policy-independent facts; it does not create a posted journal.

## Scope

### Included

- Transaction-bound audit append overload using a supplied connection and transaction.
- Integration composition of journal-source reservation + audit event + outbox event.
- Commit proof that all three facts exist exactly once.
- Rollback proof that none of the three facts survives.
- Existing scope, RLS, idempotency and hash-conflict guarantees remain active.

### Excluded

- Journal header/line persistence and posted accounting status.
- API endpoint, permission code, period/account decision and business posting rule.
- Production orchestration service; this is transaction-bound composition evidence in the integration harness.

## Milestones

| No | Vertical slice | Verification | Status |
|---:|---|---|---|
| 1 | Add transaction-bound audit append | Existing standalone audit behavior remains green | completed |
| 2 | Compose reservation/audit/outbox commit | Three exact rows in one scope | completed |
| 3 | Inject caller rollback | Zero rows for all three facts | completed |
| 4 | Run full repository gates | Full verification | completed |

## Completion Evidence

- [x] Commit persists reservation, audit and outbox exactly once.
- [x] Rollback persists none of them.
- [x] Full repository verification passes: zero-warning .NET build, 52 domain checks, 13-project architecture checks, six web tests/build, current/empty/restored PostgreSQL, Keycloak and Android gates.
- [x] Result is not represented as a posted journal.
- [x] No commit, push or PR was created.

## Progress Log

- 2026-08-24: Added a transaction-bound audit append overload without changing the standalone writer's own-transaction behavior.
- 2026-08-24: Initial integration run exposed that actor-aware and actor-free DB-context helpers had been called ambiguously; audit RLS rejected the insert and the transaction rolled back. Calls were corrected to use the explicit actor-aware helper.
- 2026-08-24: Real PostgreSQL commit and rollback assertions passed for reservation, audit and outbox as one unit; full repository verification passed.
