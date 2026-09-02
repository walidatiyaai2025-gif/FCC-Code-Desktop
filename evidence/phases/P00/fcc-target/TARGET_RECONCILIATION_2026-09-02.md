# FCC target reconciliation — target run 2026-09-02

> Historical evidence record. The classifications immediately below describe what the 2026-09-02 target run established at that repository state. Later changes to an evidence-producing probe can supersede a historical task classification under the exact-head rule. The current canonical task state is controlled by `CURRENT_PHASE.md`, `docs/TASK_LEDGER.md`, and the applicable contract documents.

## Historical target-run classification

- `FCCD-P00-002`: CLOSED — executable/version/help plus live loopback health verified.
- `FCCD-P00-003`: CLOSED — real newline-delimited structured init/retry events, raw framing, session IDs, and failure payloads captured.
- `FCCD-P00-004`: BLOCKED — session IDs and resume syntax observed, but successful continuation could not run while the provider returned 503.
- `FCCD-P00-005`: VERIFIED AT THIS TARGET SNAPSHOT — real provider 503 retries, timeout, cancellation, and then-observed owned cleanup were captured; natural rate limit was not observed and artificial load was not generated.
- `FCCD-P00-007`: BLOCKED — launch/path/output/cancellation are observed, but successful fallback prompt completion is blocked by provider 503.

## Current supersession for FCCD-P00-005

`FCCD-P00-005` is **currently BLOCKED**, not currently VERIFIED.

After the target run above, PR #9 (`P00: track late-spawned owned descendants`, merge `01e5ff6783396dd881a711c385021e601788cb6a`) strengthened the failure probe's ownership evidence by refreshing descendants immediately before cancellation/timeout escalation and requiring late-spawned owned descendants to be observed and cleaned. Because that change modified the evidence-producing probe after this historical Windows run, the exact-head rule requires a new authoritative Windows target rerun before the task can regain VERIFIED status.

Current closure also remains subject to `PG-002-P00-RATE-LIMIT-CLOSURE` unless a natural target rate-limit event is observed. No artificial load should be generated to manufacture HTTP/provider 429 evidence. Until planning authority resolves that gap or a natural event is captured, `RATE_LIMIT = NOT_OBSERVED_ON_TARGET` remains the truthful boundary.

This supersession does not invalidate the historical facts captured here; it only prevents the pre-hardening run from being treated as exact-head closure evidence for the current probe implementation.

## Evidence files

- `evidence/phases/P00/target/fcc-discovery-health.json`
- `evidence/phases/P00/target/fcc-discovery-cli.json`
- `evidence/phases/P00/target/fcc-stream-session-failure.json`

All persisted evidence was scanned for credential-shaped values. The files contain sanitized runtime/config metadata and no plaintext provider credentials.
