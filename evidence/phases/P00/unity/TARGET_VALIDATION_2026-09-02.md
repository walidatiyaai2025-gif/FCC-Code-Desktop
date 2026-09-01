# FCCD-P00-008 target validation — 2026-09-02

## Result

`FCCD-P00-008 = CLOSED`

The abandoned `worker/p00-unity-contract` lane was recovered onto current `main`, repaired on the owner's Windows target, and verified end to end.

## Target

- Windows 10 x64 (`10.0.19045`)
- Unity Hub discovered at the standard Program Files location
- Unity Editors observed: `6000.5.8f1` and `2022.3.75f1`
- Selected disposable-fixture Editor: `6000.5.8f1`
- Node.js `v20.11.1`

## Verification

- deterministic Unity probe self-test: `SELF_TEST_VERIFIED 20/20`
- Hub/editor discovery and version probing: PASS
- disposable Unicode/Arabic-path project creation and detection: PASS
- exact Editor version selection: PASS
- positive compile: PASS
- controlled compile failure: PASS
- compile recovery after removing the bad source: PASS
- EditMode NUnit test artifact: PASS with a nonzero test count
- PlayMode NUnit test artifact: PASS with a nonzero test count
- project-owned `-executeMethod`: PASS
- controlled execute-method exception: PASS
- Windows x64 build result plus nonempty executable artifact: PASS
- same-project lock collision: PASS
- owned-process cancellation: PASS
- disposable fixture cleanup: PASS

The first target attempt found that Unity 6 blank projects omit `com.unity.test-framework`; the probe now adds the Editor-declared compatible `1.7.0` dependency. A second attempt exposed zero EditMode discovery; the generated EditMode assembly is now explicitly Editor-only. The final fresh run passed every mandatory operation and cleanup.

## Evidence

Machine-readable sanitized evidence: `evidence/phases/P00/target/unity-contract.json`.

No user Unity project was mutated. All mutation, failure injection, build output, lock tests, and cancellation occurred under an owned disposable fixture.
