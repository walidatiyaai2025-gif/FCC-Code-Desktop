# P10 Unity Build Target Execution and Artifact Validation

**Task:** `FCCD-P10-010 — Build target execution/artifact validation`  
**Phase:** P10 — Unity first-class adapter  
**Status:** implementation candidate; closure requires exact-head CI, normal integration, canonical reconciliation, and exact-main verification.

## Purpose

FCC Code Desktop exposes Unity build execution as a typed product boundary rather than a terminal snippet. Build success is fail-closed: process exit code, a project-owned success marker, or the existence of an old output cannot independently establish PASS.

## External target evidence

The real Unity build surface was already genuinely exercised by integrated P00 target evidence. `docs/contracts/UNITY_AUTOMATION_CONTRACT.md` records a real Windows target run using `BuildPipeline.BuildPlayer`, `-buildTarget StandaloneWindows64`, structured result evidence, and non-empty executable validation. The authoritative P00 evidence is:

- `evidence/phases/P00/target/unity-contract.json`
- `evidence/phases/P00/unity/TARGET_VALIDATION_2026-09-02.md`

P10-010 productizes that proven surface. GitHub-hosted CI validates repository-owned planning/result/artifact mechanics without pretending that hosted runners contain Unity.

## Typed invocation

`UnityBuildTargetCommandBuilder` composes the P10-009 `UnityEditorAutomationCommandBuilder`. The project-owned static method remains a typed `unity.execute-method` process invocation, while the build wrapper owns these discrete argv values:

```text
-buildTarget <typed target>
-fccBuildOutput <fully-qualified output>
-fccBuildArtifactKind file|directory
```

P10-009 continues to own the operation GUID and structured-result path:

```text
-fccAutomationOperationId <guid-D>
-fccAutomationResult <fully-qualified-result.json>
```

Rules:

- build target is an enum, never an arbitrary shell string;
- build output is fully qualified and identifies a concrete child artifact;
- target/output-kind combinations are constrained (`StandaloneWindows64`, `StandaloneLinux64`, and `Android` are files; `StandaloneOSX`, `WebGL`, and `iOS` are directories);
- Windows x64 output must be `.exe`; Android output must be `.apk` or `.aab`;
- caller arguments cannot override build-owned or P10-009 automation-owned switches;
- all arguments remain discrete argv values; no shell command string is constructed;
- operation correlation continues to bind the product operation ID into the P09 process contract.

## Structured build-result contract

The build method writes the same strict P10-009 schema-v1 terminal result at the product-owned result path. For a successful build it reports `status: succeeded`; a `BuildPipeline` failure must report `status: failed`. P10-009 validation therefore supplies bounded, fresh, stable, operation/method-correlated structured evidence before artifact validation begins.

A zero Unity exit cannot override a missing/stale/malformed/mismatched/project-failed structured result.

## Output artifact validation

`UnityBuildTargetValidator` composes:

1. `UnityEditorAutomationValidator` for process + fresh structured result evidence;
2. P09 `ToolArtifactValidator` for traversal/reparse/type/readability/non-empty validation of the expected output.

A build passes only when all of these are true:

```text
controlled Unity process succeeds
AND fresh matching structured build result reports succeeded
AND exact expected artifact exists
AND artifact type matches the typed target
AND artifact is non-empty
AND output freshness is demonstrable
```

For file outputs, the pre-run byte length/SHA-256 are captured when a safe file already exists; an unchanged file is stale and cannot pass. For directory outputs, a pre-existing directory is intentionally `Indeterminate` because the current P09 validator does not hash complete directory trees. Product callers must therefore use a fresh operation-owned directory for directory targets rather than treating old contents as new build evidence.

Valid file output evidence includes byte length and SHA-256. Local artifact paths remain internal validation details rather than required UI disclosure.

## Deterministic cloud fixture

`tests/FCCCodeDesktop.UnityBuildTargetFixture` proves on hosted Windows without Unity:

- typed target/output argv and operation correlation;
- product-owned switch injection rejection;
- target/artifact-kind and extension constraints;
- fresh non-empty Windows executable success;
- missing and empty artifact rejection;
- stale byte-identical file rejection;
- pre-existing directory freshness rejection;
- project/build-reported failure overriding an output artifact;
- process failure overriding a success-looking result/artifact;
- cancellation remaining distinct.

Runner:

```powershell
.\tools\unity\validate-unity-build-target.ps1
```

Dedicated CI:

```text
P10-010 Unity Build Target Validation
```

## Owner-last classification

No new owner-only item is required for P10-010 because real Unity Windows x64 build execution and artifact validation already passed in the integrated P00 target evidence. This task does not claim P10 phase-exit PASS, P21 environment acceptance, final visual acceptance, or final release acceptance.

`OWNER-P04-008-REAL-TARGET` remains the sole unresolved release-blocking owner obligation until genuinely reconciled under the canonical queue.
