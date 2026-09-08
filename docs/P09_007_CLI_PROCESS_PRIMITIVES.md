# P09-007 — Generic CLI/process gateway primitives

## Scope

`FCCD-P09-007` provides the provider-neutral CLI/process execution primitive used by later first-class external-tool adapters. It deliberately composes the proven P08 owned-process supervisor and bounded-output pipeline instead of creating a second subprocess implementation.

The primitive:

- binds one `StructuredToolInvocation` to one fully-qualified resolved executable;
- preserves arguments as ordered discrete values and never builds a shell command string;
- preserves the invocation working directory and immutable environment overlay;
- carries optional task/agent/tool/process/operation correlation identities into bounded process output;
- streams stdout/stderr as structured `ToolProcessOutputEvent` values;
- exposes structured start and terminal result events;
- maps process launch failure classes into stable Tool Gateway classifications;
- does not copy raw OS/process launch failure messages into Tool Gateway result summaries;
- maps exit code zero to success and nonzero exit codes to failure without losing the exit code;
- on caller cancellation, terminates only the owned process tree, disposes the handle, and propagates cancellation.

## Reused safety boundary

Process ownership remains in `FCCCodeDesktop.Runtime.IProcessSupervisor`, established and verified by P08. That layer already owns private Windows Job Objects, `UseShellExecute=false`, `ProcessStartInfo.ArgumentList`, bounded stdout/stderr capture, and owned-tree cleanup. P09-007 does not expose arbitrary PID termination and does not introduce a generic shell execution API.

## Task boundary

This task does not implement:

- P09-008 DAP/MCP or other protocol seams;
- Unity or Blender-specific command builders/adapters;
- permission policy or side-effect classification from P13;
- final P09 fixture exit-gate closure;
- any owner-machine acceptance item.

## Validation

Focused tests cover typed executable/request validation, hostile/discrete argv and Unicode preservation, environment/correlation forwarding, structured stdout/stderr streaming, zero/nonzero exit classification, launch-failure secret suppression, cancellation-owned-tree cleanup, and pre-cancel no-launch behavior.

Shared Windows CI remains authoritative for Release build/analyzers and the existing cross-phase non-regression baseline.
