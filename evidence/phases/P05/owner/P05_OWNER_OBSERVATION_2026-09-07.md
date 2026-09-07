# P05 Owner Real-Target Observation — 2026-09-07

Status: `EXACT_SHA_CONFIRMED`

This record captures sanitized owner-observed behavior from a real Windows FCC Code Desktop run with the installed `fcc-claude`/provider environment. The owner subsequently confirmed the exact local repository SHA used for the successful manual run:

```text
TESTED_REPO_SHA: 60b6ef491e9dde3ca195b377a2ce07442452a6ce
TESTED_TREE_SHA: 221ebaf238346bdcc91cdc2fe2a1524637e312f1
```

The canonical machine-readable PASS evidence is now recorded at `evidence/phases/P05/owner/P05_PHASE_EXIT_REAL_TARGET.json`, with reconciliation review at `evidence/phases/P05/owner/P05_EXIT_OWNER_RECONCILIATION_2026-09-07.md`.

## Sanitized observations

The owner directly observed all of the following in the application:

- a genuine provider-backed task executed from the FCC Code Desktop conversation surface;
- structured runtime/tool activity was visible during execution;
- Stop transitioned the active task into stopping/cancellation behavior;
- Retry created a new run for the same logical task and incremented the attempt;
- the application closed and reopened successfully;
- the same session resumed with prior conversation/task state intact.

No prompt text, provider response content, credentials, environment variables, or user project contents are recorded here.

## Applicability boundary

The tested SHA was canonical main at the time of the manual run and had exact-main Windows CI, Workspace Search, and Large Workspace Safeguards all successful. Subsequent reconciliation-base history resolves to the same Git tree SHA, with no changed files between the tested product tree and that base. Therefore no source/configuration/packaging bytes changed before this evidence reconciliation.

This observation supports P05 exit reconciliation only. It does not close P04, P08, any later phase, or the final release.
