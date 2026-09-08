# Unity Contract Suite

## Purpose

`FCCD-P10-013` is the aggregate executable acceptance boundary for the P10 Unity first-class adapter. It does not replace the individual P10 task validators; it composes them so the complete deterministic cloud contract is exercised in one exact-head gate.

## Included validators

The suite executes, in order:

1. Unity project/version detection;
2. installed Editor/Hub resolution;
3. strongly typed Unity CLI construction;
4. same-project/process resource locking;
5. dedicated Unity log capture/parsing;
6. compile validation;
7. EditMode test validation;
8. PlayMode test validation;
9. project-owned Editor automation validation;
10. build target/artifact validation;
11. structured Unity UI events;
12. cancellation/recovery.

Before execution, the suite verifies that all required production contract source files and validator scripts are present exactly once. It requires Windows and exact .NET SDK `10.0.400`. Every delegated validator remains responsible for its locked restore, Release warnings-as-errors, deterministic positive/negative fixtures, bounded evidence handling, and fail-closed semantics.

## Safety properties

The aggregate gate must not:

- invoke arbitrary shell strings;
- weaken individual validator failures;
- convert stale/malformed/mismatched evidence into success;
- fabricate a real Unity Editor/provider result;
- bypass same-project locking;
- authorize kill-by-name process cleanup;
- record credentials, tokens, prompts, raw environment variables, or local execution paths as acceptance evidence.

Any delegated non-success fails the aggregate suite. The phase can advance only after this gate, all individual P10 tasks, canonical evidence, and exact-main CI satisfy repository governance.
