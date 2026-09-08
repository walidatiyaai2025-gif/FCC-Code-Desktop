# P10 Unity Editor Automation Contract

**Task:** `FCCD-P10-009 — Project-owned Editor automation invocation`  
**Phase:** P10 — Unity first-class adapter  
**Acceptance row:** `AC-UNITY-010`  
**Status:** implementation candidate; closure requires exact-head CI and normal integration.

## Purpose

FCC Code Desktop invokes project-owned static Unity Editor entry points through the typed Unity CLI boundary rather than through UI shell strings or arbitrary command concatenation. A process exit code is not sufficient evidence of automation success.

## External contract evidence

The real Unity `-executeMethod` behavior is not invented by P10. The P00 target-validation lane already exercised positive and negative `-executeMethod` behavior on the owner's Windows Unity environment and reconciled the evidence in:

- `docs/contracts/UNITY_AUTOMATION_CONTRACT.md`
- `evidence/phases/P00/target/unity-contract.json`
- `evidence/phases/P00/unity/TARGET_VALIDATION_2026-09-02.md`

That target run observed Unity Hub plus Editors `6000.5.8f1` and `2022.3.75f1` and passed project-owned Editor automation positive/negative behavior. P10-009 productizes that proven external surface; hosted CI validates the repository-owned invocation/result mechanics without pretending that GitHub-hosted runners contain Unity.

## Typed invocation

`UnityEditorAutomationCommandBuilder` wraps the P10-003 `UnityCliCommandBuilder` and always uses the existing typed `unity.execute-method` operation.

The ordered task-specific tail is:

```text
-executeMethod <Namespace.Type.StaticMethod>
-fccAutomationOperationId <guid-D>
-fccAutomationResult <fully-qualified-result.json>
<validated caller arguments...>
```

The surrounding P10-003 builder retains ownership of:

```text
-batchmode
-nographics
-projectPath <project>
-logFile <dedicated-log>
-timestamps
-quit
```

Rules:

- method names use the existing dot-qualified C# identifier validation;
- operation IDs must be non-empty GUIDs;
- result paths must be fully-qualified `.json` paths;
- caller arguments remain discrete argv values and cannot override product-owned automation switches;
- caller attempts to override builder-owned Unity switches remain rejected by P10-003;
- the operation ID is also injected into `ToolProcessCorrelation`; a conflicting supplied correlation fails closed;
- inputs are snapshotted before the process request is returned.

## Structured result schema

A project-owned method reports terminal evidence to the requested JSON file using schema version 1:

```json
{
  "schemaVersion": 1,
  "operationId": "00000000-0000-0000-0000-000000000001",
  "methodName": "Company.Tools.Automation.Run",
  "status": "succeeded",
  "message": "optional bounded diagnostic"
}
```

Required properties:

- `schemaVersion`: integer `1`;
- `operationId`: canonical GUID `D` text matching the requested operation;
- `methodName`: exact ordinal match for the requested static method;
- `status`: exactly `succeeded` or `failed`.

Optional `message` must be a JSON string without NUL. Retained diagnostic text is bounded to 4096 characters. Schema-v1 duplicate or unknown properties fail closed so result interpretation cannot silently drift.

## Success and failure semantics

`UnityEditorAutomationValidator` requires all of the following for `Succeeded`:

1. controlled process launch reached `Started`;
2. process terminal result is `Succeeded`, exit code is `0`, and forced termination was not requested;
3. a safe non-empty result artifact exists at the exact requested path;
4. the artifact is no larger than 256 KiB;
5. the artifact remains byte-stable between safe P09 artifact validation and parsing;
6. the artifact is not byte-identical to a pre-run result baseline;
7. JSON is valid schema v1;
8. operation ID and method name match the invocation;
9. project-owned status is `succeeded`.

Fail-closed classifications include:

- cancellation;
- process/launch/forced-termination failure;
- missing or empty result;
- unsafe/unreadable result path;
- oversized result;
- result mutation during validation;
- stale pre-run result reuse;
- malformed JSON;
- schema/operation/method mismatch;
- project-owned `failed` status.

A success artifact cannot override a failed controlled process. A zero process exit cannot override missing, stale, malformed, mismatched, or project-reported failure evidence.

## Security and data-integrity properties

- No shell command string is constructed.
- Result validation reuses the P09 reparse/traversal-safe `ToolArtifactValidator`.
- Local result paths are internal validation details rather than required UI output.
- SHA-256 and byte length are recorded for valid readable evidence.
- Parser input is bounded before allocation/parsing.
- JSON comments/trailing commas are rejected and parser depth is bounded.
- No credentials, provider responses, or fabricated Unity target observations are required by this task.

## Deterministic cloud fixture

`tests/FCCCodeDesktop.UnityEditorAutomationFixture` proves on hosted Windows without Unity:

- exact ordered typed argv and operation correlation;
- hostile/Unicode/space-containing discrete arguments;
- product-owned switch-injection rejection;
- fresh matching success evidence;
- stale result rejection;
- missing result rejection;
- malformed and oversized result rejection;
- operation-contract mismatch rejection;
- project-reported failure overriding zero exit;
- process failure overriding a success artifact;
- cancellation remaining distinct;
- invalid result path/empty operation identity rejection.

Runner:

```powershell
.\tools\unity\validate-unity-editor-automation.ps1
```

Dedicated CI:

```text
P10-009 Unity Editor Automation
```

## Owner-last classification

No new owner-only evidence is required for P10-009. The external Unity `-executeMethod` semantics were already genuinely observed in P00 target evidence. P10-009 closes only after its production implementation, deterministic fixtures, dedicated CI, normal merge, canonical reconciliation, and exact-main verification are green. This does not mark the broader P10 phase-exit gate or final Unity environment acceptance as PASS.
