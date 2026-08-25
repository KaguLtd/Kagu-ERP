# MP-03 Party Statement and Aging Report Technical Spike

## Goal

Build a bitemporal, immutable Party statement and explicit calendar-day aging oracle that preserves one report slice and exact-cross-foots open-item totals without querying Party tables or selecting company policy defaults.

- Master phase/backlog: MP-03 / item 18
- Risk: R4 — mixed as-of statements, incorrect aging classification and report totals that do not cross-foot
- Status: Completed locally — awaiting user-directed commit/push
- Requirements: `RPT-INV-001`, `RPT-INV-002`, `RPT-PARTY-001`, `RPT-PARTY-002`; report projection of `PARTY-OI-001/002`

## Definition of Ready

Conditionally satisfied for a reversible domain-only spike. Real tenant bucket policy, business-day calendar, dispute presentation, credit balances, FX and report authorization remain open or `atanmadı`. This slice accepts immutable, normalized caller snapshots and an explicit calendar-day policy version; it does not choose defaults or read source modules.

## Scope

Included:

- Open-item statement event snapshots with source, due-line, optional payment, effective date and UTC recorded timestamp.
- Deterministic bitemporal inclusion and immutable running exposure balance in one financial report slice.
- Open-item aging snapshots tied to the same effective as-of and data cutoff.
- Explicit versioned calendar-day bucket policy with full, contiguous integer-day coverage and no overlap.
- Exact bucket/aging totals and same-slice statement-to-aging cross-foot.
- Scope, currency, account, date-cut, duplicate, arithmetic, ordering and immutability checks.

Excluded:

- Database queries/projections, source-module references, API, permissions, pagination, export and UI.
- Default 0–30/31–60/61–90/90+ selection, business-day calendar and tenant policy assignment.
- Multi-currency conversion, functional currency, rounding, credit balance/netting and unapplied advances.
- Dispute/block workflow, write-off approval or GL/control-account mapping selection.

The statement amount is a normalized exposure effect: positive increases the selected receivable/payable exposure and negative reduces it. Real source-to-sign mapping is an upstream versioned report contract, not inferred here.

## Milestones

- [x] Record requirements, normalized sign meaning and deferred policy boundaries.
- [x] Add immutable bitemporal Party statement snapshots and running balance.
- [x] Add explicit versioned calendar-day aging policy.
- [x] Add scoped open-item aging classification and exact bucket totals.
- [x] Add same-slice statement-to-aging cross-foot.
- [x] Prove boundary, scope, cutoff, ordering, arithmetic and immutability behavior.
- [x] Pass full local repository verification.

## Verification Evidence

- Debug and Release builds passed with zero warnings and zero errors; locked restore remained current.
- Domain quality harness passed all 51 checks. New checks cover normalized event-kind signs, source/payment/due-line evidence, effective/recorded bitemporal cuts, explicit sequence ordering, running exposure, duplicate rejection and immutable output.
- Calendar-day aging tests cover versioned full-range bucket policies, gap/overlap and integer-boundary rejection, future/current/overdue classification, disputed/blocked evidence, exact bucket totals and immutable item ordering.
- Same-slice statement closing exposure and aging remaining total cross-foot exactly; mismatched account, report slice or decimal total fails closed.
- Architecture harness passed for all 12 source projects; Reporting.Domain still has no Party, Accounting, Infrastructure or API reference.
- Web lint, TypeScript typecheck, Vitest (2 tests) and production build passed.
- Real PostgreSQL migration idempotency and tenant/company RLS checks passed; Keycloak permission-scope smoke, isolated restore/migration/scope/outbox/auth smoke and Android lint/unit/instrumentation build gates passed.
- Full `scripts/verify.ps1` completed successfully, including formatting and diff checks.
- No tenant bucket default, business-day calendar, credit/advance/netting, FX, query, authorization, export or UI policy was inferred.
- No commit, push or PR was created, per user instruction.

## Deferred Decisions

- Tenant bucket definitions and calendar-day/business-day choice require approved company policy.
- Credit balances, advances, netting, disputes and FX require explicit report semantics and golden data.
- Query authorization and drill-down must preserve this slice in later Application/API work.
- No commit, push or PR will be created until explicitly requested by the user.
