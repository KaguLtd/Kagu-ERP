# MP-03 Party Report Web Drill-Down Technical Spike

## Goal

Create an accessible, server-authority-preserving Party statement/aging report surface that keeps one as-of context through source lineage drill-down and never calculates money with JavaScript numbers.

- Master phase/backlog: MP-03 / item 19
- Risk: R3 — client-side authorization assumptions, mixed report cuts and financial precision loss
- Status: Completed locally — awaiting user-directed commit/push
- Requirements: `WEB-RPT-001`, `WEB-RPT-002`, `WEB-RPT-003`, `WEB-RPT-004`, `RPT-INV-001`, `RPT-INV-002`

## Definition of Ready

Conditionally satisfied for a reversible presentation/contract spike. The real report endpoint, persistence, permission assignment and OpenAPI generation are not ready because MP-03 business decisions remain open or `atanmadı`. The page therefore consumes a strict future-facing contract and is verified with synthetic component fixtures; production code does not return embedded financial data or grant authority.

## Scope

Included:

- Lazy `/reports/party-account` route with URL-based company, party, as-of and focused-line context.
- Same-origin, credentialed, abortable TanStack Query adapter with Zod response validation and scope-aware query key.
- Visible report definition, as-of, data-through, generated-at, projection generation and stale state.
- Exact decimal-string money display without JavaScript numeric conversion or client-side financial recomputation.
- Server-driven visible/redacted amount handling and safe forbidden/error/empty/loading states.
- Accessible statement/aging/control summaries and same-context source-lineage links.
- Component tests for successful, stale, redacted, forbidden and missing-context behavior.

Excluded:

- Real API endpoint/query, database projection, permission seeding, OpenAPI generation and end-to-end browser/database flow.
- Export, mutation, approval, reconciliation or posting actions.
- Client role tables, token storage, tenant headers or synthetic production fallback data.

## Milestones

- [x] Record web report contract and authorization boundaries.
- [x] Add strict decimal-string report DTO and same-origin query adapter.
- [x] Add lazy report route, visible as-of/stale context and responsive layout.
- [x] Add server-driven redaction/forbidden/error/empty states.
- [x] Add same-context source-lineage drill-down links.
- [x] Pass component, accessibility-query, lint, typecheck and build gates.
- [x] Pass full local repository verification.

## Verification Evidence

- Web lint and TypeScript strict typecheck passed.
- Six Vitest component tests passed across the web workspace, including success, stale/redacted, forbidden and missing-context cases.
- Vite production build passed; the Party report remains a lazy-loaded chunk.
- Exact money presentation operates on decimal strings and drill-down links preserve company, party, as-of and focus context.
- Full `scripts/verify.ps1` passed: .NET build with zero warnings/errors, 51 domain checks, API safety contracts, 12-project architecture checks, PostgreSQL tenant/company RLS, Keycloak scope mapping, isolated restore/migration/outbox/auth smoke checks, and Android lint/unit/instrumentation assembly gates.
- No real report API, persistence, permission assignment, generated OpenAPI client or Playwright flow was introduced; these remain deferred behind the recorded MP-03 decisions.
- No commit, push or PR was created.

## Deferred Decisions

- The future API must authorize company/party scope and omit unauthorized financial fields before serialization.
- Permission codes, report owner, real data mapping and generated OpenAPI client require approved Application/API work.
- Playwright with real authentication/database is deferred until the endpoint and seeded permission fixture exist.
- No commit, push or PR will be created until explicitly requested by the user.
