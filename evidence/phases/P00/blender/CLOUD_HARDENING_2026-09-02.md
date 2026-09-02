# FCCD-P00-009 cloud hardening audit — 2026-09-02

**Evidence class:** `SELF_TEST_ONLY`  
**Target state:** `TARGET_UNVERIFIED`  
**Task closure claim:** none

## Live-state baseline

The Blender audit began from canonical `main` SHA `5f6093a2cb5b25774dfa0a753ce4f79942d4ce9f`. During publication, `main` advanced to `400f56ad4e23dae85a53a73a26da9258f792e653` via runtime-compatibility documentation reconciliation outside this worker's authorized write scope. The Blender branch was rebuilt directly on `400f56ad4e23dae85a53a73a26da9258f792e653` using the same tested Blender blobs. Existing target evidence was not replaced or promoted and continues to record `BLOCKED_BLENDER_NOT_FOUND`.

## Cloud-actionable defects hardened

- required complete Blender header shape rather than a seven-byte prefix;
- required the complete eight-byte PNG signature;
- required real OBJ vertices plus a face rather than any `o`/`v`/`f` token;
- required structured JSON fields and exact generated output paths;
- changed controlled negative classification so null/spawn-failure exits cannot masquerade as the expected nonzero Python failure;
- added explicit owned root-PID cleanup verification to cancellation;
- added deterministic unrelated-process survival and no-kill-by-name regression coverage;
- expanded exact spaces/Arabic/Unicode argument preservation coverage across every fixture argument;
- expanded redaction coverage for secret assignments embedded in persisted path/log/error strings;
- made `blender --version` observation explicit in successful discovery semantics.

## Deterministic verification

Run:

```text
node --check tools/contract-probes/blender/lib.mjs
node --check tools/contract-probes/blender/probe.mjs
node --check tools/contract-probes/blender/self-test.mjs
node tools/contract-probes/blender/self-test.mjs
```

Observed cloud result: `SELF_TEST_VERIFIED 27/27`. The tested `lib.mjs`, `probe.mjs`, and `self-test.mjs` Git blob SHAs exactly match the blobs published on the branch. `git diff --check` and the kill-by-name scan passed. This evidence does not claim any real Blender execution.

## Remaining target requirement

Run the canonical unified Windows target command on a clean exact-head checkout after Blender is installed/discoverable:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\contract-probes\run-target-validation.ps1
```

Real Blender success, discovered version, artifacts, negative behavior, and Windows cancellation remain target-observation requirements.
