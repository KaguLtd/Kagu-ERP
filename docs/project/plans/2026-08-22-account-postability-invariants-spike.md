# MP-03 Account Postability Invariants Technical Spike

## Goal

Validate every journal-draft account against an immutable, versioned chart snapshot without selecting or hard-coding a real KKTC chart of accounts.

- Master phase/backlog: MP-03 / item 14
- Risk: R4 — financial correctness and tenant/company isolation
- Status: Completed locally — awaiting user-directed commit/push
- Requirement: `ACC-INV-006`

## Definition of Ready

Conditionally satisfied for a reversible domain-only spike. `DEC-MP01-007`, `DEC-MP01-008` and named accounting ownership remain open or `atanmadı`; therefore the real chart source, codes, mappings, effective dates and posting policies remain blocked.

Read contracts:

- `MASTER_PLAN.md`
- `docs/modules/09-accounting-general-ledger.md`
- `docs/00-foundation/04-data-architecture.md`
- `docs/00-foundation/07-cross-cutting-workflows.md`
- `docs/quality/01-testing-and-quality-strategy.md`

## Scope

Included:

- Immutable account snapshots carrying tenant, company, account, chart-version, kind, active state and snapshot version.
- Journal-to-account validation for tenant/company/chart version.
- Fail-closed rejection of missing, inactive and summary/non-posting accounts.
- Duplicate account, invalid identity/version/kind and input-collection immutability checks.

Excluded:

- Account codes, names, hierarchy construction and a real KKTC chart fixture.
- Official source import, checksum, publish/approval and effective-date selection.
- Posting-rule mapping, dimensions, control-account policy and account-balance logic.
- Database schema, migration, concurrency, posting, audit, outbox, API and clients.

The account inputs are caller-supplied validation snapshots. They are not authoritative account records and the validated result is not a posted journal.

## Milestones

- [x] Record task boundaries and requirement traceability.
- [x] Implement immutable account snapshot and journal-account validation.
- [x] Prove identity, scope, chart-version, active/posting-kind and completeness boundaries.
- [x] Prove caller ordering/mutation cannot change a validated result.
- [x] Pass full local repository verification.

## Risks and Deferred Decisions

- Stale snapshots cannot protect a production transaction; persistence must later compare authoritative versions in the posting transaction.
- Chart hierarchy and postability are sourced facts, not inferred from account-code formatting.
- Official chart import and company mapping require an approved source and accounting owner.
- Per user instruction, this local slice will not be committed, pushed or opened as a PR until explicitly requested.

## Evidence

Local verification on 22 August 2026:

- Debug and Release solution builds passed with 0 warnings and 0 errors.
- Domain harness passed 21 checks; architecture harness passed for 10 source projects.
- Web lint, typecheck, tests and production build passed.
- PostgreSQL migration/RLS, Keycloak tenant/company/permission, isolated restore/outbox/auth smoke checks passed after Docker Desktop was restarted and all Compose services reported healthy.
- Android lint, unit tests and instrumentation APK build passed.
- Formatting and `git diff --check` passed.

No commit, push, PR or remote CI was created, per user instruction. This locally completed technical spike is not authoritative account master data, does not post a journal and does not advance the MP-03 business gate.
