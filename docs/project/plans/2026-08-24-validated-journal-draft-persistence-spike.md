# MP-03 Validated Journal Draft Persistence Technical Spike

## Goal

Persist a validated, non-posted journal header and its lines as an append-only snapshot bound to the existing source reservation, audit and outbox transaction.

- Master phase/gate: MP-03 / backlog item 20 persistence evidence
- Risk: R4 — financial precision, partial lines, duplicate source and false posted state
- Status: Completed
- Requirements: `ACC-INV-001`, `ACC-INV-005`, `ACC-INV-008`, DATA numeric/RLS/append-only rules
- Definition of Ready: Conditional pass for a non-posted technical snapshot; business posting gates remain open.

## Scope

- Header and line tables with `numeric(20,4)`, exact balance checks, separate line grain and immutable runtime privileges.
- Tenant/company composite foreign keys and forced RLS.
- Reservation ID and V1 draft hash linkage.
- Transaction-bound writer accepting only `ValidatedJournalDraft` and an existing reservation result.
- Integration commit/rollback evidence with reservation, header/lines, audit and outbox.
- Empty/current/restored database migration verification.

Excluded: posted state, account/period/rule authorization, reversal execution, API and production posting orchestration.

## Milestones

| No | Vertical slice | Verification | Status |
|---:|---|---|---|
| 1 | Add forward header/line migration | Empty/current DB migration | completed |
| 2 | Add transaction-bound draft writer | Exact header and line persistence | completed |
| 3 | Extend atomic commit/rollback chain | All facts or no facts | completed |
| 4 | Run full repository gates | Full verification | completed |

## Completion Evidence

- [x] Exact decimal header/lines persist once and remain non-posted. The writer rejects values that cannot fit `numeric(20,4)` without rounding.
- [x] Runtime role has only `SELECT`/`INSERT`; direct header update and line delete are rejected. Forced tenant/company RLS applies to both tables.
- [x] Commit persists one reservation, one header, two lines, one audit event and one outbox event; rollback leaves all five fact groups empty.
- [x] Current DB applied one migration then zero; empty DB applied six then zero. Full repository verification passed on 24 August 2026.
- [x] No commit, push or PR was created.

## Delivery Notes

- Migration: `0006_validated_journal_draft.sql` adds append-only, non-posted header and line snapshots linked to the source reservation.
- Application adapter: `PostgresValidatedJournalDraftWriter` participates in the caller-owned transaction and returns the original draft identity on an equivalent retry.
- Precision: debit, credit and totals are checked before SQL; PostgreSQL cannot silently round an accepted value into `numeric(20,4)`.
- Remaining boundary: no posting state, period/account authorization, API command or business-owner UAT was introduced. MP-03 therefore remains `proposed`.
