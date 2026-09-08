# FCCD-P09-007 — Implementation Scope Evidence

Date: 2026-09-08
Task: `FCCD-P09-007 — CLI/process generic adapter primitives`
Classification: CLOUD / HOSTED-WINDOWS / IMPLEMENTATION
Source canonical main: `24df5bd6e3fe40c892a34087729827360c061947`

## Live-state selection

- Canonical current phase is P09 — External Tool Gateway.
- P09-001 through P09-006 are CLOSED.
- P09-007 and P09-008 were PENDING at selection time.
- There were no open pull requests and no `worker/fccd-p09-007*` branch/claim before this worker created its branch.
- The P17 slot hint is future-phase work and is therefore not legal while P09 remains active.

## Implementation boundary

P09-007 composes the P08-owned `IProcessSupervisor` and bounded process-output contracts into a provider-neutral Tool Gateway primitive. It does not duplicate process ownership, Job Object lifecycle, bounded output capture, or arbitrary shell execution.

The implementation preserves structured argv/environment/working-directory semantics, exposes typed start/output/result events, carries optional execution correlation, classifies launch and exit status, suppresses raw launch failure messages at the gateway boundary, and terminates only the owned process tree when caller cancellation is requested.

## Validation plan

Focused tests cover request validation, hostile/discrete argv, Unicode and environment forwarding, correlation propagation, stdout/stderr event mapping, success/nonzero exit classification, launch-failure secret suppression, cancellation cleanup, and pre-cancel no-launch behavior. The exact implementation candidate must also pass the repository's shared Windows CI, Workspace Search, and Large Workspace Safeguards before normal integration.

## Non-claims

This record is not task closure evidence. `FCCD-P09-007` remains PENDING until implementation is normally merged, exact-main CI succeeds, and canonical integrated reconciliation is completed. It adds no owner-only acceptance item and does not authorize P09-008, P10, P17, or later implementation.
