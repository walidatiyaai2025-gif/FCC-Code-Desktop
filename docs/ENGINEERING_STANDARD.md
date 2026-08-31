# FCC Code Desktop — Engineering Standard

## 1. Quality policy

Production quality is mandatory from the first public release. No subsystem may be accepted on appearance alone; correctness, maintainability, diagnostics, recovery and tests are part of feature completion.

## 2. Baseline engineering rules

- C# nullable reference types enabled.
- Warnings treated seriously; release CI should use warnings-as-errors for project-owned code unless a documented exception exists.
- Async I/O end-to-end; no blocking `.Result`/`.Wait()` on UI paths.
- CancellationToken propagation for cancellable long operations.
- Dependency injection at composition root.
- Interfaces around external systems.
- No business/runtime orchestration in WPF code-behind.
- No catch-all exception swallowing.
- No global mutable singleton state except explicit, testable process-wide coordinators.
- Strong types for IDs/states where practical.
- Structured logging with correlation IDs.
- Deterministic cleanup via `IDisposable`/`IAsyncDisposable` where appropriate.
- Culture/encoding/path behavior tested for Windows realities including spaces and non-ASCII paths.

## 3. Code organization

A feature must have clear ownership boundaries. UI models do not invoke `Process.Start` directly. External tools do not write SQLite directly. Git does not control chat state. Runtime payload parsing does not live in views.

Favor small cohesive services over giant manager classes.

## 4. Error handling

Classify failures rather than return generic exceptions where product behavior differs:

- dependency missing,
- incompatible version,
- process launch failure,
- process non-zero exit,
- timeout/hang,
- cancelled,
- rate limited,
- authentication/provider failure,
- malformed runtime response,
- artifact validation failure,
- permission denied,
- database/integrity issue,
- Git conflict/dirty-state condition,
- external resource locked.

User-facing errors must be actionable and diagnostics must retain deeper technical context after redaction.

## 5. Testing pyramid

### Unit

Domain state machines, parsers, validators, command builders, redaction, migration logic, queue scheduling.

### Integration

SQLite, filesystem, Git repositories, process supervision, terminal primitives, runtime event normalization.

### Runtime contract

Real/controlled `fcc-claude` behavior required by the application: launch, streaming, session identity/resume, cancellation, errors, unavailable runtime, version change behavior.

### Tool contract

Unity and Blender adapters against supported installed versions/environments. Commands must be tested as real ordered argument arrays, not only mocked strings.

### Recovery

Kill/restart at meaningful lifecycle points and verify reconciliation.

### UI automation

Critical user journeys and state rendering.

### Installer

Install, launch, repair/upgrade, uninstall, retained data and clean-machine behavior.

## 6. Test-data discipline

Automated tests must use disposable temp projects/repos and never mutate the developer's real repositories. Unity/Blender contract fixtures must be dedicated fixtures.

## 7. Process safety

Always launch executables with structured argument lists/APIs when available. Avoid `cmd /c`/string shell concatenation except when the task explicitly requires a shell; quote/escape correctly and test hostile/space-containing paths.

Track owned process IDs. Never kill unrelated processes by executable name.

## 8. File safety

- Atomic writes where practical.
- Preserve encoding/newline semantics when editing user files.
- Checkpoint valuable binary assets before replacement.
- Validate path containment for operations intended to stay inside a workspace.
- Do not follow unsafe traversal/symlink/reparse paths blindly.
- Do not overwrite pre-existing user work merely to recover agent state.

## 9. Git quality

Use disposable repositories in Git tests. Test dirty trees, untracked files, conflicts, detached HEAD and no-remote cases. Destructive operations require separate policy gates.

## 10. Database quality

- SQLite schema versioned from v1.
- Forward migrations tested from each released schema.
- Transactions around lifecycle transitions.
- Busy/lock handling.
- Integrity checks and backup strategy.
- Corruption recovery behavior tested.
- No giant streaming logs stored as database blobs when file-backed storage is more appropriate.

## 11. Logging and secret redaction

Redaction happens before durable sinks.

Patterns/fields include at minimum API keys, bearer tokens, Authorization headers, provider/FCC credentials and known secret environment variables.

Tests must deliberately inject fake secrets and verify they are absent from logs and exported diagnostic bundles.

## 12. Performance budgets

Set measurable budgets during implementation and record accepted values. At minimum test:

- application cold/warm launch,
- project tree load for large repository,
- search cancellation,
- conversation virtualization,
- continuous high-volume process output,
- large diff rendering,
- database growth over long sessions,
- Unity log ingestion,
- Blender log/render task ingestion.

No synchronous bulk work on UI thread.

## 13. Memory and output management

Use bounded queues/buffers for streaming output. Spill large logs to disk. UI views display virtualized windows and can request more historical output.

## 14. Security

- Least privilege by default.
- High-risk permission mode visibly opt-in.
- Safe path handling.
- No arbitrary web content with unrestricted bridge privileges.
- WebView2 surfaces use narrow host APIs.
- Diagnostic export sanitization.
- Dependency vulnerability review before release.
- No telemetry/network call added without documented product requirement.

## 15. External tool adapters

Every adapter requires:

1. discovery tests,
2. version/capability model,
3. command/API builder tests,
4. lifecycle/cancellation behavior,
5. log/error parser tests,
6. artifact validation,
7. missing-tool behavior,
8. incompatible-version behavior,
9. resource-lock behavior,
10. diagnostics output.

### Unity

Do not assume one install path or one Unity version. Verify project version and installed editor. Prevent unsafe simultaneous editor automation for the same project. Parse compiler/test/build output into structured results.

### Blender

Respect CLI argument ordering. Use supported background/Python surfaces for deterministic automation. Generated scripts must have task correlation/provenance. Validate `.blend`, export and render outputs; process exit code alone is insufficient.

## 16. AI-generated code quality

AI authorship never excuses duplication, dead code, placeholder implementation or weak tests. Before task closure, perform self-review/refactor and remove scaffolding not intended for production.

Do not add TODO/FIXME for mandatory v1 work and then close the task. Add the work to the ledger and complete it.

## 17. Code review checklist

Every material PR/reconciliation checks:

- scope matches claimed task,
- architecture boundaries preserved,
- tests cover success/failure/cancel paths,
- no unsafe destructive behavior,
- no secrets/log leakage,
- no UI-thread blocking,
- accessibility/UX states covered where visible,
- documentation/ledger updated,
- no unrelated regression or scope creep.

## 18. CI requirements

CI should eventually gate at least:

- restore with locked dependencies,
- build Release,
- analyzers/format/style policy,
- unit tests,
- non-environment integration tests,
- packaging smoke test,
- artifact checksums/SBOM or dependency manifest as defined by release policy.

Environment-dependent Unity/Blender/FCC contract suites may run on designated Windows runners but remain mandatory release evidence.

## 19. Completion rule

Implementation without tests/evidence is `IMPLEMENTED`, never `CLOSED`.