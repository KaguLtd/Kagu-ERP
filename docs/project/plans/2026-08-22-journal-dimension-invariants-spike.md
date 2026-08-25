# MP-03 Journal Dimension Invariants Technical Spike

## Goal

Require journal-line dimension assignments explicitly from a versioned posting-rule requirement snapshot and reject missing dimensions without silently creating defaults.

- Master phase/backlog: MP-03 / item 14
- Risk: R4 — financial classification, tenant/company isolation and reproducibility
- Status: Completed locally — awaiting user-directed commit/push
- Requirement: `ACC-INV-007`

## Definition of Ready

Conditionally satisfied for a reversible domain-only spike. `DEC-MP01-002`, `DEC-MP01-008` and named owners remain open or `atanmadı`; therefore actual branch, project, department and cost-center policies are not selected.

## Scope

Included:

- Immutable dimension ID/value ID assignments on journal-line drafts.
- Immutable tenant/company/posting-rule-version requirement snapshots.
- Per-line required-dimension completeness validation.
- Duplicate assignment/requirement, scope, rule-version and collection immutability checks.
- Empty requirement sets for posting rules that require no dimensions.

Excluded:

- Real dimension catalog, codes, hierarchy, effective dates and user scope.
- Validation that a dimension value is active or belongs to a master-data hierarchy.
- Rule selection, silent/default dimension assignment and correction policy.
- Persistence, concurrency, posting, audit, outbox, API and clients.

Both requirements and dimension values are caller-supplied snapshots. A validated draft is not posted and production must later validate authoritative master-data versions in the same transaction.

## Milestones

- [x] Record scope and requirement traceability.
- [x] Add immutable line assignments and requirement snapshot.
- [x] Reject missing, duplicate and mismatched dimension contexts.
- [x] Prove no input mutation or ordering changes the validated result.
- [x] Pass full local repository verification.

## Evidence

- Debug and Release builds completed with zero warnings and zero errors.
- Domain invariant harness passed all 24 checks; architecture harness passed all 10 project checks.
- Web lint, typecheck, component tests and production build passed.
- Real PostgreSQL migration/RLS, isolated restore/outbox/auth and Keycloak token-scope smoke checks passed.
- Android lint, repository/ViewModel unit tests and instrumentation build passed.
- `git diff --check` passed before the full verification run.
- No real posting-rule/dimension policy or master data was inferred, and the MP-03 gate did not advance.
- No commit, push or PR was created, per user instruction.

## Deferred Decisions

- The actual required dimensions per posting rule need approved business/accounting policy.
- Dimension-value activity, hierarchy and user scope need ORG/IAM authoritative snapshots.
- Per user instruction, this local slice will not be committed, pushed or opened as a PR until explicitly requested.
