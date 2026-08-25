# MP-03 Treasury Payment Economic Event Technical Spike

## Goal

Establish the Treasury-owned immutable payment economic-event boundary with explicit source identity and same-currency rate evidence, without coupling Treasury to Party allocation or Accounting internals.

- Master phase/backlog: MP-03 / item 16
- Risk: R4 — cash-event identity, currency reproducibility and duplicate financial effect
- Status: Completed locally — awaiting user-directed commit/push
- Requirements: `BNK-PAY-001`, `BNK-PAY-002`; same-currency technical subset of `BNK-INV-002`

## Definition of Ready

Conditionally satisfied for a reversible domain-only spike. `DEC-MP01-004`, `DEC-MP01-005`, `DEC-MP01-006`, `DEC-MP01-008`, `DEC-MP01-010`, `DEC-MP01-012` and named owners remain open or `atanmadı`; therefore real company currency, FX, payment lifecycle, approvals, bank settlement and posting are not selected.

## Scope

Included:

- Independent `Treasury.Domain` module with no references to Party, Accounting, provider or infrastructure projects.
- Immutable canonical payment source identity scoped by tenant/company/source/purpose.
- Explicit same-currency transaction/functional amount and identity-rate snapshot with source/type/date/version evidence.
- Payment, party-account and treasury-account identities, incoming/outgoing direction, effective date and UTC recorded timestamp.
- In-memory duplicate payment/source-intent rejection and immutable deterministic collection behavior.
- Boundary, scope, amount, rate, duplicate and architecture checks.

Excluded:

- Posted payment persistence, state machine, authorization, maker-checker, audit, outbox and API.
- Cross-currency conversion, rounding, override, realized FX and functional/reporting currency policy.
- Party allocation mutation/reference, GL journal generation or bank reconciliation.
- Provider/bank account master data, submission, settlement, return and fee events.

The payment object is a validated technical draft, not proof of posting or bank settlement. Party allocation will later consume a published immutable payment snapshot through an application contract; neither domain module references the other.

## Milestones

- [x] Record requirement traceability, module boundary and deferred decisions.
- [x] Add Treasury.Domain project and repository architecture coverage.
- [x] Add canonical source identity and same-currency rate snapshot.
- [x] Add scoped immutable payment economic-event draft and duplicate set.
- [x] Prove boundary, amount, scope, uniqueness, ordering and immutability behavior.
- [x] Pass full local repository verification.

## Verification Evidence

- Locked restore and Debug/Release builds passed with zero warnings and zero errors; the Treasury lock file and solution registration are current.
- Domain quality harness passed all 41 checks, including payment scope, positive decimal amount, exact same-currency functional amount, identity-rate evidence, source uniqueness, deterministic ordering and collection immutability.
- Architecture harness passed for all 11 source projects and proves Treasury.Domain has no Party, Accounting, provider or infrastructure dependency.
- Web lint, TypeScript typecheck, Vitest (2 tests) and production build passed.
- Real PostgreSQL migration idempotency and tenant/company RLS checks passed; Keycloak permission-scope smoke, isolated restore/migration/scope/outbox/auth smoke and Android lint/unit/instrumentation build gates passed.
- Windows Application Control rejected newly generated standalone quality/migrator DLLs with `0x800711C7`. The security policy was not weakened: Treasury and Accounting checks, plus the same migrator source and embedded SQL, execute through the established allowed quality harnesses while the standalone module and migrator artifacts still build independently.
- Full `scripts/verify.ps1` completed successfully after the stopped local Docker Desktop service was restarted.
- No real FX, payment lifecycle, bank settlement, allocation or GL-posting policy was inferred or implemented.
- No commit, push or PR was created, per user instruction.

## Deferred Decisions

- Payment approval/lifecycle, bank state and return behavior require approved Treasury/workflow policy.
- Cross-currency conversion requires approved functional-currency, rate, rounding and override policy.
- Persistent idempotency requires PostgreSQL unique constraints and transaction-level concurrency tests.
- Allocation/GL integration requires published contracts and application orchestration in the same authoritative transaction boundary.
- Per user instruction, this local slice will not be committed, pushed or opened as a PR until explicitly requested.
