# MP-03 Journal Currency Reproducibility Technical Spike

## Goal

Make each journal-line transaction-to-functional-currency conversion reproducible from immutable, versioned exchange-rate and rounding-policy snapshots.

- Master phase/backlog: MP-03 / item 14
- Risk: R4 — financial amount reproducibility, tenant/company isolation and rounding
- Status: Completed locally — awaiting user-directed commit/push
- Requirement: `ACC-INV-008`

## Definition of Ready

Conditionally satisfied for a reversible domain-only spike. `DEC-MP01-004`, `DEC-MP01-005`, `DEC-MP01-006`, `DEC-MP01-008` and named owners remain open or `atanmadı`; therefore the real company currencies, rate provider/type, approval rules, decimal scale and rounding policy are not selected.

## Scope

Included:

- Immutable tenant/company-scoped exchange-rate snapshots with explicit transaction/functional currencies, rate type, source, date, numerator, denominator and version.
- Immutable tenant/company-scoped rounding-policy snapshots with explicit policy ID, version, scale and midpoint mode.
- Per-line transaction amount, unrounded functional amount, stored rounded functional amount and rounding difference.
- Validation that journal functional currency, tenant/company scope and stored line amount match the supplied conversion snapshot.
- Boundary, mismatch, deterministic recomputation and immutability checks.

Excluded:

- Real functional/reporting currency selection or ISO reference-data approval.
- Live/manual rate acquisition, approval workflow, overrides, triangulation or rate precedence.
- A production rounding/minor-unit policy, residual posting account or balancing rule.
- Persistence, concurrency, posting, audit, outbox, API and clients.

All policy values are explicit caller-supplied snapshots. The model does not infer a default rate, currency, scale or midpoint behavior. A validated currency set remains a draft and is not a posted journal.

## Milestones

- [x] Record scope, decision blocks and requirement traceability.
- [x] Add immutable rate and rounding snapshots.
- [x] Add reproducible per-line transaction/functional conversion results.
- [x] Reject missing, mismatched and non-reproducible currency contexts.
- [x] Prove deterministic decimal calculations and immutable inputs.
- [x] Pass full local repository verification.

## Evidence

- Debug and Release builds completed with zero warnings and zero errors.
- Domain invariant harness passed all 27 checks; architecture harness passed all 10 project checks.
- Decimal conversion tests cover numerator/denominator direction, debit/credit side preservation, midpoint modes, rounding difference, zero-rounding rejection and overflow.
- Journal validation rejects absent snapshots and tenant, company, functional-currency or stored-functional-amount mismatches.
- Web lint, typecheck, component tests and production build passed.
- Real PostgreSQL migration/RLS, isolated restore/outbox/auth and Keycloak token-scope smoke checks passed.
- Android lint, repository/ViewModel unit tests and instrumentation build passed.
- `dotnet format --verify-no-changes` and `git diff --check` passed.
- The first full verification attempt was stopped only by sandbox-denied NuGet network access (`NU1301`); the permitted rerun passed completely.
- No real currency, rate-source, approval, rounding or residual-account policy was inferred, and the MP-03 gate did not advance.
- No commit, push or PR was created, per user instruction.

## Deferred Decisions

- Company functional/reporting currency and allowed transaction currencies require approved company policy.
- Rate provider/type, approval and override behavior require approved treasury/accounting policy.
- Scale, midpoint behavior and residual-account treatment require approved accounting policy.
- Per user instruction, this local slice will not be committed, pushed or opened as a PR until explicitly requested.
