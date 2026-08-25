# MP-03 Golden Accounting Cycle Harness Technical Spike

## Goal

Prove that the existing reversible MP-03 domain contracts can describe one exact, scoped customer collection cycle from source receivable through payment allocation to Party reporting and GL control-account reconciliation without inventing a posting, authorization or company policy.

- Master phase/gate: MP-03 / backlog item 20 and exit-gate assessment
- Risk: R3 — false end-to-end confidence, mixed scope/cutoff and manually plugged financial totals
- Status: Completed locally — awaiting user-directed commit/push
- Requirements: `ACC-INV-001`, `ACC-INV-005`, `PARTY-DUE-001`, `PARTY-OI-001`, `PARTY-INV-003`, `BNK-PAY-001`, `RPT-INV-001`, `RPT-CTRL-001`, `RPT-CTRL-002`, `RPT-PARTY-001`, `RPT-PARTY-002`
- Owner: Product/accounting decisions remain `atanmadı`; technical evidence is owned by the repository verification harness.
- Start/target date: 2026-08-24 / 2026-08-24

## Definition of Ready

Conditionally satisfied for a synthetic, side-effect-free verification harness. The implemented domain contracts are locally verified, but real posting rules, permissions, persistence and approved accounting ownership are not ready. The harness therefore proves arithmetic and context composition only; it cannot advance MP-03 to business implementation or production readiness.

## Scope

### Included

- A deterministic 100 GBP receivable and 60 GBP incoming-payment/allocation fixture.
- Exact derivation of 40 GBP open-item, statement and aging closing balances.
- Balanced invoice and collection journal drafts tied to their own canonical source identities.
- Same-slice Party subledger and GL control-account reconciliation with zero difference.
- A cross-scope negative assertion proving the golden reconciliation fails closed for another company.
- Full local repository verification after the harness is added.

### Excluded

- Real API/database transaction, posting engine, permission checks, optimistic/pessimistic concurrency or production idempotency.
- Migration changes, seeded company policy, account codes, tax, FX, approval or legal assertions.
- Browser-to-database E2E and user acceptance evidence.

## Invariants and Safety Boundaries

- Money remains `decimal`; no tolerance or balancing plug is allowed.
- Payment, allocation, open item, journal and report are separate immutable facts/snapshots.
- Every composed fact uses the same tenant, company, party account, currency, effective as-of and UTC cutoff where applicable.
- The harness uses synthetic IDs and carries no personal or production data.
- No database or external state is mutated by the golden check.

## Milestones

| No | Vertical slice | Verification | Status |
|---:|---|---|---|
| 1 | Compose receivable, payment and allocation facts | Exact 100 − 60 = 40 assertions | completed |
| 2 | Compose statement, aging and GL control snapshots | Exact cross-foot and zero reconciliation difference | completed |
| 3 | Prove scope mismatch fails closed | Other-company reconciliation is rejected | completed |
| 4 | Run repository gates and assess MP-03 | Full `scripts/verify.ps1` result and explicit deferrals | completed |

## Test Plan

- Unit/property: one named golden-cycle check using existing invariant constructors.
- DB integration/security/migration/restore: existing full verification gates must remain green; they do not convert this synthetic harness into a persisted E2E.
- Golden accounting cycle: source receivable → due/open item → payment → allocation → Party statement/aging → balanced journals → subledger/GL control report.
- Concurrency: deferred until authoritative persistence and posting transaction exist.

## Risks and Decisions

| Date | Risk/decision | Impact | Decision/owner |
|---|---|---|---|
| 2026-08-24 | Manually assembled snapshots could be mistaken for production E2E. | MP-03 gate could be overstated. | Label as technical harness; keep API/DB/concurrency/UAT deferred and phase proposed. |
| 2026-08-24 | Posting/account policy owners are unassigned. | Account selection and business posting cannot be approved. | Use synthetic account IDs and only invariant-safe balanced drafts; revisit near development completion. |

## Completion Evidence

- [x] Exact golden-cycle acceptance assertions pass: 100 GBP receivable − 60 GBP collection/allocation = 40 GBP open item, statement closing and aging total.
- [x] Another-company GL snapshot is rejected with `REPORT_RECONCILIATION_COMPANY_MISMATCH`.
- [x] Full local repository verification passes: zero-warning .NET build, 52 domain checks, API safety contracts, 12-project architecture checks, six web tests/build, PostgreSQL RLS, Keycloak scope mapping, isolated restore/migration/outbox/auth smoke checks and Android gates.
- [x] MP-03 remains proposed until persisted E2E, concurrency, authorization and business-owner evidence exist.
- [x] No commit, push or PR was created.

## Progress Log

- 2026-08-24: Added one named golden-cycle harness to the existing domain verification executable. It composes, but does not merge, receivable, due/open-item, payment, allocation, journal, Party report and control-account facts.
- 2026-08-24: Verified exact decimal cross-foot and zero-difference reconciliation without tolerance or plug entries.
- 2026-08-24: Re-ran the complete repository verification successfully. The evidence is technical composition only; real transactional E2E, concurrency and authorization remain explicitly deferred.
