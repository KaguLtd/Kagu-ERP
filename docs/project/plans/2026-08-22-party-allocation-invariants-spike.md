# MP-03 Party Allocation Invariants Technical Spike

## Goal

Implement the smallest policy-independent domain slice for validating a same-currency payment allocation across one or more open items without claiming an authoritative settlement or posting workflow.

- Master phase: MP-03
- Risk: R4 — financial correctness and tenant/company isolation
- Status: Validating — local evidence complete; remote CI pending
- Requirements: `PARTY-INV-001`, `PARTY-INV-002`, `PARTY-INV-003`; same-currency technical subset of `PARTY-INV-005`

## Definition of Ready

Conditionally satisfied for a reversible domain-only spike. Named business, finance, security and legal owners remain `atanmadı` under `DEC-MP01-019`; therefore production acceptance, accounting policy, FX policy and persistent settlement remain blocked.

Read contracts:

- `MASTER_PLAN.md`
- `docs/modules/03-party-current-accounts.md`
- `docs/modules/09-accounting-general-ledger.md`
- `docs/00-foundation/04-data-architecture.md`
- `docs/00-foundation/07-cross-cutting-workflows.md`
- `docs/quality/01-testing-and-quality-strategy.md`

## Scope

Included:

- Independent `Parties.Domain` module.
- Immutable payment capacity with tenant, company, party account, payment, currency and usable amount.
- Immutable open-item capacity with tenant, company, party account, open item, currency and remaining amount.
- Positive decimal allocation lines and multi-open-item plans.
- Payment total, open-item capacity, duplicate item, scope and same-currency validation.
- Boundary, metamorphic and immutability checks.

Excluded:

- Database schema, migrations, locks and authoritative remaining-balance computation.
- Posted settlement, unallocation, audit, outbox, GL posting and reconciliation.
- FX rate, functional-currency and rounding snapshots.
- Automatic allocation policy, advances, write-offs, API, web and Android.

The capacity values are caller-supplied validation snapshots. They are not an authoritative balance and do not provide production concurrency control.

## Milestones

- [x] Add requirement traceability and module-local rules.
- [x] Implement immutable allocation capacity and plan values.
- [x] Prove amount, scope, currency, duplicate, ordering and immutability invariants.
- [x] Pass solution build and repository verification.
- [ ] Record local and remote evidence.

## Test Plan

- Reject zero/negative amounts and capacity overflow at exact decimal boundaries.
- Reject tenant, company and party-account scope mismatch.
- Reject cross-currency plans until an approved rate and rounding snapshot exists.
- Reject duplicate open items in the same plan.
- Prove line ordering does not change totals.
- Prove caller collection mutation cannot change a validated plan.

## Risks and Deferred Decisions

- Caller snapshots can become stale; persistence must later lock or compare authoritative append-only projections.
- Cross-currency allocation remains blocked on versioned FX and rounding policy.
- Posted allocation and unallocation require append-only events, audit and GL integration in a later approved slice.

## Evidence

Local verification on 22 August 2026:

- `dotnet restore KaguERP.slnx` passed and generated locked dependency state for `Parties.Domain` and its unit-check consumer.
- `dotnet build KaguERP.slnx --no-restore` passed with 0 warnings and 0 errors.
- Domain harness passed 15 checks; architecture harness passed for 10 source projects.
- `dotnet format KaguERP.slnx --no-restore --verify-no-changes` and `git diff --check` passed.
- `scripts/verify.ps1` passed end to end: locked restore, Release build, domain and architecture checks, web lint/typecheck/test/build, PostgreSQL migration and tenant/company RLS checks, Keycloak scope smoke, isolated restore/outbox/auth smoke, and Android lint/unit/instrumentation build.

Remote CI evidence will be recorded after the stacked draft pull request is created. This spike does not satisfy the MP-03 entry or exit gate and does not validate production concurrency, posted allocation or FX behavior.
