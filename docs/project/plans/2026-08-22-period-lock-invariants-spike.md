# MP-03 Period Lock Invariants Technical Spike

## Goal

Implement a policy-independent Accounting Domain state-machine and fail-closed posting gate for scoped period locks without defining the company's fiscal calendar, permissions or reopen approval policy.

- Master phase/backlog: MP-03 / item 14
- Risk: R4 — financial integrity, closed-period posting and tenant/company isolation
- Status: Validating — local evidence complete; remote CI pending
- Requirements: `ACC-INV-004`, `ACC-PER-001`, `ACC-PER-002`, `ACC-PER-003`

## Definition of Ready

Conditionally satisfied for a reversible domain-only spike. `DEC-MP01-003`, `DEC-MP01-008`, `DEC-MP01-012` and named owners remain open or `atanmadı`; therefore real fiscal calendars, closing evidence, permissions, quorum, approved reopen and persistent posting remain blocked.

Read contracts:

- `MASTER_PLAN.md`
- `docs/modules/09-accounting-general-ledger.md`
- `docs/00-foundation/04-data-architecture.md`
- `docs/00-foundation/07-cross-cutting-workflows.md`
- `docs/quality/01-testing-and-quality-strategy.md`

## Scope

Included:

- Immutable company/period/scope/version lock snapshots.
- Forward close progression: `open → soft_close → review → hard_close`.
- Fail-closed rejection of skipped, backward and no-op transitions.
- Independent operational, inventory valuation, GL, tax and hard/legal scopes.
- Standard posting gate requiring explicit open GL and hard/legal snapshots.
- Tenant/company/period scope, duplicate scope, boundary and immutability checks.

Excluded:

- Fiscal-year/calendar creation and effective-date-to-period lookup.
- Database schema, migration, row locks and concurrency/version persistence.
- Close checklist, reconciliation evidence and balance-difference reports.
- Permission, maker-checker, quorum, delegation and approved reopen execution.
- Posting, journal persistence, audit, outbox, API, web and Android behavior.

The lock set is a caller-supplied validation snapshot. It is not an authoritative period record and cannot authorize a reopen.

## Milestones

- [x] Add period requirement traceability and task boundaries.
- [x] Implement immutable scoped lock snapshots and close transitions.
- [x] Implement fail-closed standard posting validation.
- [x] Pass boundary, scope, ordering and immutability checks.
- [ ] Pass local and remote repository gates.

## Test Plan

- Accept only the documented forward close sequence.
- Reject skipped, no-op and backward/reopen transitions.
- Reject missing, duplicate or mismatched scoped lock snapshots.
- Reject standard posting when GL or hard/legal scope is not open.
- Prove unrelated scopes remain independent and caller mutation cannot change a validated set.

## Risks and Deferred Decisions

- A stale caller snapshot cannot protect a production transaction; persistence must later lock or compare an authoritative version.
- Soft-close adjustment rights and backdate exceptions need approved permission/policy snapshots.
- Hard-close reopen remains unavailable until the workflow proves distinct approvals, reason, duration, audit and balance-difference evidence.

## Evidence

Local verification on 22 August 2026:

- Debug and Release solution builds passed with 0 warnings and 0 errors.
- Domain harness passed 18 checks; architecture harness passed for 10 source projects.
- `scripts/verify.ps1` passed end to end: locked restore, formatting, web lint/typecheck/test/build, PostgreSQL migration and tenant/company RLS, Keycloak scope smoke, isolated restore/outbox/auth smoke, and Android lint/unit/instrumentation build.
- `git diff --check` passed.

Remote CI evidence will be recorded after the stacked draft pull request is created. This spike does not authorize a reopen, define company period policy, or satisfy the MP-03 entry/exit gate.
