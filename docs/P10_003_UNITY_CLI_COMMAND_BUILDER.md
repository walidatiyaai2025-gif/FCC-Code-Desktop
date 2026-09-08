# P10-003 — Strongly Typed Unity CLI Command Builder

## Scope

This task owns command construction only. It does not launch Unity, classify compile/test/build results, acquire project locks, parse logs, validate build artifacts, or implement cancellation/recovery.

The builder consumes a validated project context, a fully qualified `Unity.exe` path, a fully qualified dedicated log path, and one typed action. It produces the existing P09 `ToolProcessRequest` / `StructuredToolInvocation` boundary with ordered discrete argv values and no shell command concatenation.

## Canonical argv baseline

Common switches are emitted in deterministic order:

```text
-batchmode
[-nographics]
-projectPath <project-root>
-logFile <dedicated-log>
-timestamps
<typed action switches>
[-quit]
```

Supported typed actions in P10-003:

- open project / batch baseline;
- project-owned `-executeMethod <Type.Method>` with extra values preserved as discrete argv;
- Unity Test Framework `-runTests -testPlatform EditMode|PlayMode -testResults <file>`.

Future P10 tasks own execution semantics and may extend the typed builder where their task-specific switches require it. They must not bypass the discrete argv boundary.

## Safety invariants

- No filesystem probing or process launch occurs during build.
- Editor, log, and test-result paths must be fully qualified; the editor executable must resolve to `Unity.exe`.
- `executeMethod` must be a dot-qualified C# identifier path.
- Additional execute-method arguments preserve spaces, quotes, empty values, shell metacharacters, Unicode, and Arabic as individual argv elements.
- Additional arguments cannot override builder-owned Unity switches such as `-projectPath`, `-logFile`, `-executeMethod`, `-runTests`, `-testPlatform`, or `-quit`.
- Environment overlays are snapshotted and then validated by the existing P09 structured invocation contract, including case-insensitive duplicate-key rejection.

## Deterministic validation

`tools/unity/validate-unity-cli-command-builder.ps1` restores/builds/runs `tests/FCCCodeDesktop.UnityCliCommandFixture` on Windows with SDK `10.0.400` and warnings as errors.

The fixture covers canonical ordering, hostile/discrete argv preservation, Unicode/Arabic paths and values, typed EditMode/PlayMode commands, reserved-switch injection rejection, method/path validation, immutable input snapshots, duplicate environment rejection, and optional `-nographics` / `-quit` behavior.
