# Reporting module rules

- Read `docs/modules/14-reporting-dashboard.md`, relevant source-module contracts and the active task plan before changing this module.
- Reporting is read-only: it cannot mutate source modules or make a cache/materialized view the business authority.
- Every financial result carries tenant/company scope, effective as-of, UTC data cutoff, report-definition version and projection generation.
- Cross-foot and reconciliation comparisons must use the same currency, dimensions and data cut; mismatches fail closed.
- Use `decimal` for money and never introduce silent tolerance, rounding, currency conversion or sign policy.
- Reporting Domain cannot reference source modules, Infrastructure, API, database, export or UI types.
- Every invariant change requires named requirement IDs and boundary/property checks.
