# MP-03 Control-Account Reconciliation Report Technical Spike

## Goal

Establish a policy-independent reporting slice and exact subledger-to-GL control-account reconciliation oracle that cannot compare different scope, currency, as-of, generation or dimension contexts.

- Master phase/backlog: MP-03 / item 18
- Risk: R4 — misleading financial totals, mixed data cuts and hidden control-account differences
- Status: Completed locally — awaiting user-directed commit/push
- Requirements: `RPT-INV-001`, `RPT-INV-003`, `RPT-INV-006`, `RPT-CTRL-001`, `RPT-CTRL-002`

## Definition of Ready

Conditionally satisfied for a reversible domain-only spike. Real chart/control-account mapping, report ownership, currency policy, dimension catalog, materialization and approved report definitions remain open or `atanmadı`; therefore the slice validates caller-supplied immutable snapshots and does not query or publish business data.

## Scope

Included:

- Independent `Reporting.Domain` project with no source-module, infrastructure, API or database dependency.
- Versioned report slice with tenant/company, effective as-of date, UTC data cutoff/generated timestamps, projection generation and canonical dimension selection.
- Immutable subledger/GL balance snapshots with exact decimal opening/debit/credit/closing cross-foot.
- Exact zero/difference reconciliation for matching control-account snapshots.
- Fail-closed rejection for scope, currency, as-of, generation, definition-version, dimension, account or ledger-side mismatch.
- Boundary, deterministic dimension, arithmetic and architecture checks.

Excluded:

- Real Party statement/aging query, bucket policy, calendar, disputes and multi-currency conversion.
- Account mapping, posted ledger persistence, materialized views, caching, API, permission filtering, export or UI.
- Tolerance, suspense classification, issue resolution workflow and period closing.
- Source-document drill-down authorization; the immutable slice is only the context that later drill-down must preserve.

## Milestones

- [x] Record report requirements and deferred business decisions.
- [x] Add Reporting.Domain and architecture coverage.
- [x] Add versioned immutable report/dimension slice.
- [x] Add exact subledger and GL balance snapshots.
- [x] Add zero/difference control-account reconciliation oracle.
- [x] Prove boundary, mismatch, arithmetic and immutability behavior.
- [x] Pass full local repository verification.

## Verification Evidence

- Locked restore completed after registering Reporting.Domain and updating project lock state.
- Debug and Release builds passed with zero warnings and zero errors.
- Domain quality harness passed all 47 checks. New checks cover report scope, definition version, effective as-of, UTC data cutoff/generation, projection generation, deterministic immutable dimensions, exact decimal balance cross-foot and zero/non-zero reconciliation results.
- Negative checks reject mismatched tenant, company, report definition, as-of date, data cutoff, projection generation, currency, dimension selection, control account and ledger side.
- Architecture harness passed for all 12 source projects and proves Reporting.Domain is an independent domain assembly without source-module or infrastructure references.
- Web lint, TypeScript typecheck, Vitest (2 tests) and production build passed.
- Real PostgreSQL migration idempotency and tenant/company RLS checks passed; Keycloak permission-scope smoke, isolated restore/migration/scope/outbox/auth smoke and Android lint/unit/instrumentation build gates passed.
- Full `scripts/verify.ps1` completed successfully, including formatting and diff checks.
- No real account mapping, sign convention, aging policy, query, authorization, export, caching or posting behavior was inferred.
- No commit, push or PR was created, per user instruction.

## Deferred Decisions

- Real control-account mapping and sign convention require approved Accounting/Party policy and are caller inputs here.
- Aging bucket/calendar and report currency behavior require approved company policy.
- Projection writer, as-of token acquisition, query authorization and export manifest belong to later Application/Infrastructure slices.
- No commit, push or PR will be created until explicitly requested by the user.
