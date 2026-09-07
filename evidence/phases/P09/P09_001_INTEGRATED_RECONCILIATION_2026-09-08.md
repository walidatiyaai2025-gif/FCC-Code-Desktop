# FCCD-P09-001 — Integrated Reconciliation Evidence

Date: 2026-09-08
Task: `FCCD-P09-001 — IExternalToolAdapter contract`
Classification: CLOUD / HOSTED-WINDOWS / INTEGRATED
Canonical task result: CLOSED after normal implementation integration and exact-main permanent validation.

## Live-state selection and recovery

The slot that performed this reconciliation carried a future `FCCD-P16-004` scheduling hint, but live canonical state remained `CURRENT_PHASE=P09`. `FCCD-P09-001` already had legitimate normally merged implementation in canonical `main` and was still `PENDING` only because exact-main post-merge CI and durable reconciliation had not yet completed. Under the worker recovery protocol, that integration-pending P09 task therefore took priority over any new current-phase claim and all P16 implementation remained prohibited.

Pre-reconciliation canonical implementation main:

`44dbee3107d4fd3a410629fb72fec5978440683b`

At recovery time there were no open pull requests, and no competing P09 reconciliation work was found.

## Implementation

- Implementation PR: #214 — `P09-001: add external tool adapter contract`.
- Branch: `worker/fccd-p09-001-external-tool-adapter-contract`.
- Exact accepted implementation candidate: `1f33b646deecdb471741ddc406e998c8a5f97da5`.
- Initial implementation commit: `651af35c1a1721f6bca1695bf9145f064f36e2e8`.
- Test-determinism repair commit: `1f33b646deecdb471741ddc406e998c8a5f97da5` made the nonexistent project-root fixture GUID-scoped so the no-filesystem-I/O assertion cannot inherit stale temp-directory state.
- Production scope:
  - `src/FCCCodeDesktop.Tools/ExternalToolContracts.cs`
  - `tests/FCCCodeDesktop.UnitTests/ExternalToolAdapterContractTests.cs`
  - `tests/FCCCodeDesktop.UnitTests/FCCCodeDesktop.UnitTests.csproj`
- Contract behavior:
  - project-owned `IExternalToolAdapter` boundary exposes immutable `Identity`, project-scoped `DiscoverAsync`, `GetCapabilitiesAsync`, and cancellation-aware streamed `ExecuteAsync`;
  - `ToolIdentity` validates stable trimmed adapter identity/display values;
  - `ProjectContext` validates a non-empty project ID and fully-qualified project root without touching the filesystem;
  - nominal `ToolDiscoveryResult`, `ToolCapabilitySet`, `ToolInvocation`, and `ToolEvent` base contracts provide stable extension seams for later P09 work without coupling the gateway to persistence, Unity, Blender, or UI concerns;
  - structured execution uses `IAsyncEnumerable<ToolEvent>` and carries cancellation explicitly.

The task deliberately does not implement P09-002 registry/discovery orchestration, P09-003 richer invocation/result semantics, resource locking, artifact validation, diagnostics/health, CLI/process primitives, or optional protocol adapters.

## Unit/contract coverage

`ExternalToolAdapterContractTests` verifies:

- adapter identity validation;
- project identity and fully-qualified path validation;
- no filesystem creation while constructing `ProjectContext`;
- a concrete fixture can implement the complete adapter contract;
- discovery and capability calls are project-scoped;
- structured execution streams tool events;
- invocation retains exactly one project context;
- cancellation propagates through discovery, capability resolution, and streamed execution;
- null invocation project context is rejected.

The test project explicitly references `FCCCodeDesktop.Tools`, so the permanent Windows baseline compiles and executes this contract suite as part of the normal unit lane.

## Exact implementation-head validation

On exact implementation candidate `1f33b646deecdb471741ddc406e998c8a5f97da5`:

- Windows CI run `34169157466` — SUCCESS.
- P06-007 Workspace Search run `34169157495` — SUCCESS.
- P06-008 Large Workspace Safeguards run `34169157524` — SUCCESS.

The Windows baseline includes locked restore, format verification, Release build, the full unit/integration test lanes, build metadata, dependency/quality/test-infrastructure policy, owner-last governance validation, FCC runtime contract validators, design-system/UI validators, and inherited P05/P06 regression validators.

## Normal implementation integration

PR #214 was promoted from Draft only after the exact implementation-head gates above completed SUCCESS and `main` remained at its expected base. It was merged using the normal merge method with expected-head protection.

Accepted implementation merge / pre-reconciliation canonical main:

`44dbee3107d4fd3a410629fb72fec5978440683b`

The merge preserves tested candidate `1f33b646deecdb471741ddc406e998c8a5f97da5` as its second parent; no squash, rebase, force-push, or fabricated evidence is claimed.

## Exact implementation-main validation

On exact canonical implementation main `44dbee3107d4fd3a410629fb72fec5978440683b`:

- Windows CI run `34169744778` — SUCCESS.
- P06-007 Workspace Search run `34169744791` — SUCCESS.
- P06-008 Large Workspace Safeguards run `34169744771` — SUCCESS.

No task-local regression remained after normal implementation integration.

## Reconciliation boundary

This reconciliation closes only `FCCD-P09-001`.

After this task is canonically reconciled:

- P09 remains `IN_PROGRESS`;
- `PHASE_EXIT_GATE=NOT_RUN`;
- `FCCD-P09-002` through `FCCD-P09-008` remain PENDING unless separately integrated by other legitimate workers;
- P10 and later phase implementation remain prohibited until P09 is truthfully closed;
- no P09 owner/manual/REAL_TARGET obligation is created by this task;
- `OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner item;
- P05 remains `PASS_INTEGRATED`;
- `VERIFIED_FINAL_COMPLETE=false`.

The reconciliation PR itself must pass its exact-head CI, be normally merged, and the resulting exact canonical `main` must remain green before this CLOSED state is treated as the durable final task endpoint.
