# P06-007 — Workspace content/file/regex search

- Source task: `FCCD-P06-007`
- Evidence class: cloud/self-test
- Owner-last queue: unchanged; no owner-only evidence required

## Implemented

- application-layer workspace search contract with file-name, literal-content and regex modes;
- background filesystem search service with cancellation, bounded files/results/file-size, regex timeout, generated-directory exclusion, binary/encoding safety and reparse-point refusal;
- WPF dispatcher-safe search state with project-switch cancellation and stale-result suppression;
- workspace search surface with query/mode/case controls, Search/Cancel, Enter/Escape keyboard behavior, virtualized results and inline status/errors;
- permanent integration tests and static/negative validator;
- dedicated Windows CI validation for P06-007 to avoid colliding with the active P06-005 worker's canonical workflow edit.

## Required cloud validation

1. Canonical Windows Release baseline.
2. Full unit and integration suites through that baseline.
3. `tools/projects/validate-workspace-search.ps1 -RunFixtures -RequireRuntime`.

Closure is valid only after the exact branch head is green and the task evidence/governance reconciliation is merged to canonical `main`.
