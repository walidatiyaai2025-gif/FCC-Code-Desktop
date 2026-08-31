# FCC Code Desktop — Architecture

## 1. Architectural objectives

The architecture must isolate volatile external integrations, preserve user data, support long-running/recoverable agent work, and permit premium desktop UX without coupling the UI to subprocess details.

Primary principles:

- modular monolith first,
- explicit interfaces around external systems,
- event-driven runtime state inside the process,
- durable journal for recovery,
- deterministic process ownership,
- no hidden global mutable state,
- no UI-thread blocking I/O,
- versioned persistence,
- dependency inversion around FCC, Git, terminal, Unity, Blender and future tools.

---

## 2. Solution layout

Target structure:

```text
FCCCodeDesktop.sln

src/
  FCCCodeDesktop.App
  FCCCodeDesktop.Core
  FCCCodeDesktop.Application
  FCCCodeDesktop.Infrastructure
  FCCCodeDesktop.Persistence
  FCCCodeDesktop.Runtime
  FCCCodeDesktop.Fcc
  FCCCodeDesktop.Files
  FCCCodeDesktop.Git
  FCCCodeDesktop.Terminal
  FCCCodeDesktop.Tools
  FCCCodeDesktop.Tools.Unity
  FCCCodeDesktop.Tools.Blender
  FCCCodeDesktop.Security
  FCCCodeDesktop.Diagnostics
  FCCCodeDesktop.Updater

bridge/
  FCCCodeDesktop.AgentBridge   # only if structured sidecar is proven necessary

tests/
  FCCCodeDesktop.UnitTests
  FCCCodeDesktop.IntegrationTests
  FCCCodeDesktop.RuntimeContractTests
  FCCCodeDesktop.ToolContractTests
  FCCCodeDesktop.RecoveryTests
  FCCCodeDesktop.UiTests
  FCCCodeDesktop.InstallerTests
```

Project count may be adjusted if evidence shows a cleaner boundary, but responsibilities must remain isolated.

---

## 3. Layering

### Core

Pure domain models/state machines/value objects. No WPF, SQLite, Git, FCC, Unity or Blender dependencies.

### Application

Use cases/orchestration: projects, sessions, task queue, permissions, recovery, tool routing.

### Infrastructure

Concrete OS/process/file/network utilities and cross-cutting implementation details.

### Adapters

FCC, Git, terminal, Unity, Blender and other external integrations implement project-owned interfaces.

### App

WPF composition root, views, view models, navigation and presentation state.

---

## 4. Runtime abstraction

```csharp
public interface IAgentRuntime
{
    Task<RuntimeProbeResult> ProbeAsync(CancellationToken ct);
    IAsyncEnumerable<AgentEvent> RunTurnAsync(AgentRunRequest request, CancellationToken ct);
    Task<ResumeResult> ResumeAsync(SessionResumeRequest request, CancellationToken ct);
    Task<InterruptResult> InterruptAsync(AgentRunId runId, CancellationToken ct);
}
```

Concrete implementation must be replaceable without changing chat/domain/UI layers.

Target adapters:

```text
IAgentRuntime
  ├── FccClaudeStructuredRuntime
  └── FccClaudeCliFallbackRuntime
```

Exact transport is chosen only after P00 contract probes establish real behavior.

---

## 5. Agent event model

All agent/runtime output is normalized into typed events.

Conceptual discriminated set:

```text
AgentEvent
  MessageDelta
  MessageCompleted
  RuntimeStateChanged
  ToolStarted
  ToolProgress
  ToolCompleted
  FileObserved
  FileMutationObserved
  CommandStarted
  CommandOutput
  CommandCompleted
  BuildResult
  TestResult
  UnityEvent
  BlenderEvent
  PermissionRequested
  RateLimitObserved
  Warning
  Error
  TurnCompleted
```

Raw provider/FCC payloads must not leak through the UI contract.

Unknown upstream events are retained safely in diagnostics but do not crash the client.

---

## 6. Task state machine

Canonical states:

```text
CREATED
QUEUED
STARTING
RUNNING
WAITING_PERMISSION
VERIFYING
COMPLETED
FAILED
CANCELLED
INTERRUPTED
RATE_LIMITED
BLOCKED
```

Transitions are validated centrally.

A task cannot become `COMPLETED` until the runtime signals terminal success and any task-specific verification step has completed.

---

## 7. Queue architecture

A single durable `AgentExecutionCoordinator` owns global execution.

Responsibilities:

- persist queue order,
- enforce concurrency=1 by default,
- enforce 15-second default cooldown,
- react to rate-limit state,
- recover queued/running state after restart,
- issue resource locks for external tools,
- prevent duplicate starts,
- expose queue events to UI.

Do not let individual chat tabs spawn autonomous agent processes directly.

---

## 8. Process supervision

Every product-owned child process is represented by a durable/observable process record containing:

- logical operation ID,
- executable identity/path,
- arguments (sanitized for persistence),
- working directory,
- process ID after launch,
- start time,
- stdout/stderr routing,
- cancellation state,
- exit code,
- expected artifacts,
- resource locks.

Cancellation escalation:

```text
graceful protocol interrupt
  ↓ timeout
Ctrl+C / tool-specific cancellation
  ↓ timeout
terminate process
  ↓ if required
kill owned process tree
```

Do not kill unrelated matching process names.

---

## 9. Tool Gateway

Core abstraction:

```csharp
public interface IExternalToolAdapter
{
    ToolIdentity Identity { get; }
    Task<ToolDiscoveryResult> DiscoverAsync(ProjectContext project, CancellationToken ct);
    Task<ToolCapabilitySet> GetCapabilitiesAsync(ProjectContext project, CancellationToken ct);
    IAsyncEnumerable<ToolEvent> ExecuteAsync(ToolInvocation invocation, CancellationToken ct);
}
```

Each adapter declares:

- compatible versions,
- detection evidence,
- capabilities,
- mutating vs non-mutating operations,
- resource-lock keys,
- expected outputs,
- cancellation semantics,
- validation rules.

Invocation arguments are structured data, not arbitrary concatenated shell strings.

---

## 10. Unity adapter

`FCCCodeDesktop.Tools.Unity` owns Unity-specific logic.

Key components:

```text
UnityProjectDetector
UnityInstallationResolver
UnityCommandBuilder
UnityProcessRunner
UnityLogParser
UnityTestResultParser
UnityArtifactValidator
UnityProjectLock
UnityHealthProbe
```

Project detection should inspect canonical Unity project markers such as `ProjectSettings/ProjectVersion.txt`.

Commands are built from validated arguments, including project path, batch mode, log destination, test/build mode and optional project-owned automation entry points.

The adapter must never assume a globally fixed Unity install path.

---

## 11. Blender adapter

`FCCCodeDesktop.Tools.Blender` owns Blender-specific logic.

Key components:

```text
BlenderInstallationResolver
BlenderCommandBuilder
BlenderPythonRunner
BlenderLogParser
BlenderArtifactManifest
BlenderArtifactValidator
BlenderResourceLock
BlenderHealthProbe
```

Automation uses Blender-supported CLI/Python surfaces. Headless automation should use background mode when appropriate.

Important implementation constraint: Blender command-line arguments are order-sensitive. Build invocations as ordered strongly typed arguments and test the final process contract.

Generated automation scripts must be stored in a controlled temp/work directory with traceability to the task and sanitized logs.

Before overwriting existing `.blend` or exported assets, apply the workspace safety/checkpoint policy.

---

## 12. Unity↔Blender orchestration

The Tool Gateway supports composition but does not couple adapters to each other.

Application-level orchestration owns workflows such as:

```text
Blender create/modify
→ validate output manifest
→ copy/export into project-approved destination
→ Unity import/compile/test
→ classify output
→ return combined evidence to agent
```

This prevents Unity code from importing Blender-specific dependencies and vice versa.

---

## 13. Persistence

SQLite baseline with migrations.

Logical tables/entities:

```text
Projects
ProjectToolDetections
Sessions
Messages
Tasks
TaskEvents
AgentRuns
ToolRuns
ProcessRuns
QueueEntries
FileEvents
GitCheckpoints
Settings
RecoveryJournal
DiagnosticsIndex
SchemaMigrations
```

Use transactional writes for state transitions that must survive a crash.

Large raw logs should be file-backed with indexed metadata rather than stored as giant database blobs.

---

## 14. Recovery journal

Before/after important transitions persist checkpoints:

- task accepted,
- queue claimed,
- runtime starting,
- session ID received,
- external process launched,
- mutating tool operation started,
- expected artifact declared,
- Git/workspace checkpoint,
- verification started,
- task terminal.

At startup, recovery scans non-terminal records and reconciles reality before allowing new work.

---

## 15. Git safety boundary

Git operations pass through `IGitService`.

The service classifies operations:

- read-only,
- normal mutating,
- destructive/high-risk.

High-risk operations cannot be invoked through an unclassified generic command path by product UX.

Pre-existing dirty work is captured before agent operations and must never be silently erased.

---

## 16. Terminal architecture

Use ConPTY-backed sessions for interactive Windows terminal behavior.

Terminal sessions are separate from agent-owned command processes but use shared process/logging primitives where sensible.

Closing a terminal tab must have explicit policy if child processes remain active.

---

## 17. Editor architecture

Editor component is a locally bundled surface; no runtime CDN requirement.

Editor communicates with host through a narrow bridge for:

- open content,
- save,
- dirty state,
- theme,
- diagnostics markers when available,
- navigation.

The file service remains authoritative for disk writes/safety.

---

## 18. Diagnostics and logging

Structured logs have categories and correlation IDs:

```text
ApplicationId
ProjectId
SessionId
TaskId
AgentRunId
ToolRunId
ProcessRunId
```

Mandatory redaction occurs before persistent sink/output, not merely at export time.

Diagnostic bundle generation performs a second sanitization pass.

---

## 19. Single-instance architecture

Use an OS-level single-instance mechanism.

A second launch communicates open-project/file intent to the existing instance and exits.

This prevents duplicate runtime supervisors and duplicate global queues.

---

## 20. Dependency/version policy

- Pin direct dependency versions.
- Use lock files where ecosystem supports them.
- Record external compatibility observations.
- Add automated dependency/security review in CI.
- Avoid prerelease runtime dependencies in production unless no stable option exists and an ADR explicitly accepts the risk.

---

## 21. Non-goals for architecture

Do not build:

- a microservice fleet for a local desktop app,
- a custom source-control implementation,
- a custom 3D engine,
- a custom Unity clone,
- a custom Blender clone,
- a second provider-management backend duplicating FCC.

Own the orchestration and UX; integrate stable external capabilities behind adapters.