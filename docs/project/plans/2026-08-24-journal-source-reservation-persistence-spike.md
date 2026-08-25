# MP-03 Journal Source Reservation Persistence Technical Spike

## Goal

Close the database-race portion of `ACC-INV-005` with a tenant/company-scoped, append-only PostgreSQL reservation for canonical journal source identity, while keeping real journal posting and business idempotency behavior out of scope.

- Master phase/gate: MP-03 / backlog item 20 concurrency evidence
- Risk: R4 — financial duplicate prevention and tenant isolation
- Status: Completed locally — awaiting user-directed commit/push
- Requirements: `ACC-INV-005`, `RPT-INV-001`, DATA tenant/company RLS and migration standards
- Owner: Business/accounting roles remain `atanmadı`; this is a policy-independent technical persistence contract.
- Start/target date: 2026-08-24 / 2026-08-24

## Definition of Ready

Conditionally satisfied. Canonical source identity and process-local duplicate rejection already exist and are verified. A PostgreSQL uniqueness reservation can be added without choosing accounts, posting rules, tax, approval or reversal policy. The table is not a journal entry and cannot be presented as a posted financial result.

## Scope

### Included

- Forward migration for `accounting.journal_source_reservation`.
- Unique key on tenant, company, canonical source type, source event and posting purpose.
- Canonical-text, SHA-256, timestamp and required-ID constraints.
- Tenant/company RLS with forced enforcement and non-owner runtime role.
- Runtime `SELECT`/`INSERT` only; no `UPDATE`/`DELETE`.
- Real PostgreSQL integration checks for authorized insert, parallel duplicate race, conflicting duplicate rejection, company separation, RLS denial and privilege boundaries.
- Empty/current/restored database migration verification through the existing repository gate.

### Excluded

- `journal_entry`/`journal_line`, posting transaction, rule selection, active/reversal semantics and API idempotency response.
- Application repository/writer and authorization permission code.
- Production deployment or destructive rollback.

## Invariants and Safety Boundaries

- Reservation is append-only and contains no mutable status.
- The source identity is unique per tenant/company/source type/source event/posting purpose.
- Tenant/company relationship is protected by a composite foreign key as well as RLS.
- A reservation hash identifies the validated intent content; a different hash cannot reuse the key.
- The table grants no authority to post, reverse or report a journal.
- Migration rollback is forward compensation only: stop writers, verify dependent references, then remove the unused table in a later approved migration. Production data is never automatically dropped.

## Milestones

| No | Vertical slice | Verification | Status |
|---:|---|---|---|
| 1 | Add forward schema migration | Migrator recognizes and applies the new migration | completed |
| 2 | Enforce scope and immutability | RLS plus runtime privilege checks | completed |
| 3 | Enforce concurrent uniqueness | Two app connections race; exactly one insert succeeds | completed |
| 4 | Verify migration/restore/regression | Full repository verification | completed |

## Test Plan

- DB integration: authorized insert/read, same-key parallel insert, different-content duplicate, company-key separation.
- Security: cross-company insert invisible/rejected; runtime has neither owner/BYPASSRLS nor update/delete rights.
- Migration/restore: existing normal and isolated restored-database paths apply/check all migrations.
- Domain/API/E2E: existing gates remain green; real posting E2E remains deferred.

## Risks and Decisions

| Date | Risk/decision | Impact | Decision/owner |
|---|---|---|---|
| 2026-08-24 | A reservation could be mistaken for a posted journal. | False financial state. | Name and document it as an idempotency reservation only; no posted state or balance columns. |
| 2026-08-24 | “Active journal” reversal semantics are not approved. | Partial unique index could encode an unsupported lifecycle. | Use permanent source-purpose reservation; reversal/correction must have its own explicit source/purpose contract later. |
| 2026-08-24 | Dropping a new financial table is destructive. | Potential audit/data loss. | No down migration; use reviewed forward compensation if the unused contract is superseded. |

## Completion Evidence

- [x] Migration and schema checks pass on the current, empty and restored PostgreSQL databases; the empty database applies five migrations and the second run applies zero.
- [x] Two parallel runtime connections race on the same canonical identity and store exactly one reservation.
- [x] Conflicting-content duplicate, cross-company RLS, and actual runtime `UPDATE`/`DELETE` attempts are rejected; the same source identity remains separate in another authorized company.
- [x] Full local repository verification passes: zero-warning .NET build, 52 domain checks, API safety contracts, 12-project architecture checks, six web tests/build, current/empty/restored PostgreSQL checks, Keycloak scope mapping and Android gates.
- [x] MP-03 remains proposed pending the real journal + audit + outbox transaction, API idempotency/authorization, business-owner approval and UAT.
- [x] No commit, push or PR was created.

## Progress Log

- 2026-08-24: Added forward migration `0005_accounting_journal_source_reservation` with composite company FK, canonical identity uniqueness, SHA-256 content hash, forced RLS and append-only runtime privileges.
- 2026-08-24: Added real PostgreSQL race, conflict, company-isolation, RLS and mutation-negative checks. The first local attempt exposed an existing index-name collision and rolled back atomically; the index was renamed before migration application. A test-only open-reader/commit ordering issue was then corrected.
- 2026-08-24: Added a generated-name, safely cleaned empty-database migration gate to the full verification script. Current, empty and restored database paths and the complete repository suite pass.
