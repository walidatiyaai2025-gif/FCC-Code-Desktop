# P00 Target Machine Evidence

- Repo SHA: `e6932783b30ab0bdbb596c7959e03143753bff9a`
- Captured UTC: `2026-09-02T12:05:48.9923740Z`
- Overall status: **BLOCKED**
- Live provider-backed prompt authorized: `True`
- P00-005 integrated exact-head evidence: **PASS** - `evidence/phases/P00/failure/fcc-failure-target-exact-head.json`
- PG-002 policy: **RESOLVED** / `NOT_OBSERVED_ON_TARGET` - actual 429 observed: `False`
- P00-009 closure support: **READY_FOR_CLOSURE_RECONCILIATION** - `evidence/phases/P00/target/blender-contract.json`

## Contract summary

- fccDiscoveryCli: **BLOCKED** / `BLOCKED` - BLOCKED_NO_SAFE_PROMPT_INVOCATION_INFERRED - authoritative target execution: `True` - target behavior observed: `True` - evidence: `evidence/phases/P00/target/fcc-discovery-cli.json`
- fccStreamingSessionFailure: **BLOCKED** / `BLOCKED` - streaming=BLOCKED_NO_SAFE_INVOCATION;session=BLOCKED_INITIAL_RUN_MISSING;failure=BLOCKED_NO_SAFE_INVOCATION;rateLimit=NOT_OBSERVED_ON_TARGET - authoritative target execution: `True` - target behavior observed: `False` - evidence: `evidence/phases/P00/target/fcc-stream-session-failure.json`
- unity: **PASS** / `PASS` - PASS - authoritative target execution: `True` - target behavior observed: `True` - evidence: `evidence/phases/P00/target/unity-contract.json`
- blender: **PASS** / `PASS` - PASS - authoritative target execution: `True` - target behavior observed: `True` - evidence: `evidence/phases/P00/target/blender-contract.json`

## Steps

- fcc-pr1-self-test: **PASS** (exit 0)
- target-evidence-summary-self-test: **PASS** (exit 0)
- fcc-discovery-cli-target: **BLOCKED** (exit 2) - C:\Users\Waleed\Documents\Codex\2026-08-31\files-pasted-by-the-user-you\work\FCC-Code-Desktop-P00-FINAL-20260902-150153\evidence\phases\P00\target\fcc-discovery-cli.json
- fcc-stream-session-failure-self-test: **PASS** (exit 0)
- fcc-stream-session-failure-target: **BLOCKED** (exit 2) - C:\Users\Waleed\Documents\Codex\2026-08-31\files-pasted-by-the-user-you\work\FCC-Code-Desktop-P00-FINAL-20260902-150153\evidence\phases\P00\target\fcc-stream-session-failure.json
- unity-contract-self-test: **PASS** (exit 0)
- unity-contract-target: **PASS** (exit 0) - C:\Users\Waleed\Documents\Codex\2026-08-31\files-pasted-by-the-user-you\work\FCC-Code-Desktop-P00-FINAL-20260902-150153\evidence\phases\P00\target\unity-contract.json
- blender-contract-self-test: **PASS** (exit 0)
- blender-contract-target: **PASS** (exit 0) - C:\Users\Waleed\Documents\Codex\2026-08-31\files-pasted-by-the-user-you\work\FCC-Code-Desktop-P00-FINAL-20260902-150153\evidence\phases\P00\target\blender-contract.json
- target-evidence-summary: **BLOCKED** (exit 2) - C:\Users\Waleed\Documents\Codex\2026-08-31\files-pasted-by-the-user-you\work\FCC-Code-Desktop-P00-FINAL-20260902-150153\evidence\phases\P00\target\P00_TARGET_CONTRACT_SUMMARY.json - schemaVersion=2; fail-closed contract and P00 readiness metadata

Raw tool-specific JSON referenced above is produced by repository probes that redact credential-shaped values before persistence. The compact contract summary copies controlled classifications and bounded version/path metadata only; it does not copy raw provider output.
