# FCC Code Desktop — Final Owner Acceptance Guide

This guide defines the **single owner-facing acceptance workflow**. It does not pre-classify unfinished future cloud work as owner-only and it does not claim that any owner action has been executed.

## One workflow

The owner runs only the consolidated master runner:

```powershell
pwsh -NoProfile -File .\tools\final-acceptance\run-final-owner-acceptance.ps1 -CandidateSha (git rev-parse HEAD) -Resume
```

The runner reads `docs/FINAL_OWNER_ACCEPTANCE_QUEUE.md` and executes every item whose canonical queue state is `QUEUED`. Individual task runners remain tracked implementation details; the owner does not need to discover or invoke them separately.

## What the master runner guarantees

Before any genuine owner command is invoked, it:

- requires the authoritative Windows environment;
- pins the invocation to one exact 40-character candidate SHA and re-verifies HEAD before and after every item;
- requires the repository identity to match the supplied repository root;
- rejects source/configuration/packaging worktree changes outside declared evidence roots;
- validates queue structure, classifications, tracked commands, cloud-complete evidence, and canonical evidence paths;
- verifies common machine prerequisites such as Git, PowerShell 7, exact .NET SDK requirements, exact candidate provenance, and clean-worktree requirements;
- delegates environment-specific checks to the tracked item runner that owns them rather than guessing that FCC/provider, Unity, Blender, installer, clean-machine, manual-visual, or hardware prerequisites exist.

For each legal `QUEUED` item it captures child-runner output, redacts secret-shaped console material before displaying it, requires the tracked command to return success, requires canonical evidence to exist, rejects empty/oversized evidence, rejects secret-shaped evidence, and—when evidence is JSON—requires matching `evidenceClassification`, exact `testedRepoSha`, and `overallStatus=PASS`.

## FCC, Unity, Blender, installer and manual acceptance

`docs/OWNER_LAST_EXECUTION_POLICY.md` forbids adding future owner items before their cloud prerequisites are integrated and green. Therefore the current queue is authoritative, not a forecast.

When a future requirement becomes genuinely owner/environment-bound and is lawfully appended to the queue, the same master command automatically executes its tracked runner. This is how genuine FCC/provider target checks, supported Unity/Blender target checks, installer install/repair/upgrade/uninstall lifecycle checks, clean-machine checks, and manual visual/accessibility checks are consolidated into one owner workflow without inventing premature owner tasks.

## Resume behavior

`-Resume` is intentionally conservative. A `QUEUED` item is skipped only when its existing evidence is machine-readable JSON, contains no detected plaintext secret, matches the exact candidate SHA and classification, and reports `overallStatus=PASS`. Stale, failed, malformed, non-machine-readable, or missing evidence is not used as a resume PASS.

A successful execution never edits the queue to `PASS_INTEGRATED`. It ends with reconciliation required. A convergence worker must review and integrate genuine evidence before any source task, phase gate, acceptance row, or queue state changes.

## Machine-readable result

The default summary path is:

```text
evidence/final-owner/FINAL_OWNER_ACCEPTANCE_SUMMARY.json
```

Every canonical queue item appears exactly once with `PASS`, `FAIL`, or `MISSING`. `PASS_INTEGRATED` historical items appear as `PASS` without rerunning owner actions. The summary always records `ownerExecutionClaimed=false`; the repository—not the runner—owns later reconciliation.

## Cloud validation only

Cloud CI uses `-SelfTestOnly`. That mode validates queue orchestration, exact-candidate guards, prerequisite plumbing, evidence parser rejection of stale/FAIL evidence, and secret redaction/detection. It never invokes genuine owner target commands and never produces real acceptance evidence.
