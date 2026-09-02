# FCC Code Desktop — Windows CI

## Purpose

`FCCD-P01-005` establishes one canonical Windows Release build/test baseline that is shared by GitHub Actions and local Windows validation. The workflow is intentionally thin; executable policy lives in `tools/ci/run-windows-ci.ps1` so CI and developer verification cannot silently diverge.

## Trigger and runner contract

`.github/workflows/windows-ci.yml` runs for:

- pushes to `main`;
- pull requests targeting `main`.

The canonical job runs on GitHub-hosted `windows-2025`, installs the repository-pinned .NET SDK `10.0.400`, and uses read-only repository contents permission. Superseded runs for the same workflow/ref may be cancelled by GitHub concurrency control; a cancellation is not a PASS and the replacement run must complete successfully.

No FCC provider credentials, Unity installation, Blender installation, owner workstation, or manual evidence is required for this P01 CI lane.

## Canonical Release baseline

From a Windows checkout, run the same entrypoint used by GitHub Actions:

```powershell
pwsh -NoProfile -File .\tools\ci\run-windows-ci.ps1
```

It requires Windows, PowerShell 7, and exact .NET SDK `10.0.400`, then executes:

1. locked solution restore;
2. formatting verification with no restore;
3. Release build with no restore;
4. the complete unit + integration test lane with no rebuild;
5. dependency-policy validation;
6. quality-policy validation;
7. test-infrastructure validation.

Any non-zero stage fails the CI baseline. The runner does not regenerate lock files, bypass locked restore, downgrade Release to Debug, skip failed tests, or use `continue-on-error` semantics.

## CI policy validation

Run:

```powershell
pwsh -NoProfile -File .\tools\ci\validate-windows-ci.ps1 -RequireDotNet
```

The validator checks the permanent workflow and executable runner contract. Deterministic in-memory negative fixtures prove rejection of:

- a non-Windows runner;
- SDK drift away from `10.0.400`;
- repository write permission;
- unlocked restore;
- non-Release build configuration;
- an incomplete unit-only test lane;
- weakened quality validation.

The negative fixtures do not mutate repository files or owner data.

## Acceptance boundary

P01-005 integration is cloud-completable when an exact candidate head passes the CI policy validator and the real `Windows CI / Windows Release` GitHub-hosted job, then the normal merge commit's `main` push run passes the same baseline. Phase/task closure bookkeeping and the P01 exit gate remain controller responsibilities.
