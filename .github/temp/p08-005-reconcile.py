from pathlib import Path

BASE = "9bcb6b94bbc5f185e8615c38b73e4f5496a0397e"


def replace_once(path: str, old: str, new: str) -> None:
    p = Path(path)
    text = p.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"guard failed: {path}: expected exactly one match, got {count}: {old!r}")
    p.write_text(text.replace(old, new, 1), encoding="utf-8", newline="\n")

replace_once(
    "CURRENT_PHASE.md",
    "- `FCCD-P08-005` — PowerShell/CMD profiles — PENDING.",
    "- `FCCD-P08-005` — PowerShell/CMD profiles — CLOSED.",
)

p08_005_current = """## P08-005 integration provenance

- Task: `FCCD-P08-005 — PowerShell/CMD profiles` — CLOSED.
- Implementation PR: #198 (`cloud/fccd-p08-005-shell-profiles-lane1`).
- Exact implementation candidate: `214e3fb7e9b4a985474f5a690d4e6b90da8651fe`.
- Exact implementation-head Windows CI: run `34086264659` / #483 — SUCCESS.
- Exact implementation-head P06-007 Workspace Search: run `34086264671` / #212 — SUCCESS.
- Exact implementation-head P06-008 Large Workspace Safeguards: run `34086264743` / #196 — SUCCESS.
- Normal implementation merge / accepted main: `9bcb6b94bbc5f185e8615c38b73e4f5496a0397e`.
- Exact accepted-main Windows CI: run `34086944769` / #484 — SUCCESS.
- Exact accepted-main P06-007 Workspace Search: run `34086944845` / #213 — SUCCESS.
- Exact accepted-main P06-008 Large Workspace Safeguards: run `34086944798` / #197 — SUCCESS.
- Integrated evidence: `evidence/phases/P08/P08_005_INTEGRATED_RECONCILIATION_2026-09-07.md`.
- Evidence is cloud/hosted-Windows immutable shell-profile contract evidence only. No ConPTY ownership, optional-shell discovery, process launch, P08 phase closure, P09/P14 authorization, new owner-only evidence, release eligibility, or `VERIFIED_FINAL_COMPLETE=true` is claimed.

"""
replace_once(
    "CURRENT_PHASE.md",
    "## P08-006 integration provenance\n",
    p08_005_current + "## P08-006 integration provenance\n",
)

old_pc = "`FCCD-P08-003`, `FCCD-P08-004`, `FCCD-P08-005`, `FCCD-P08-007`, and `FCCD-P08-008` remain PENDING."
new_pc = "`FCCD-P08-005 — PowerShell/CMD profiles` is CLOSED after implementation PR #198 and exact accepted-main Windows CI `34086944769`, Workspace Search `34086944845`, and Large Workspace Safeguards `34086944798` all completed SUCCESS on `9bcb6b94bbc5f185e8615c38b73e4f5496a0397e`. `FCCD-P08-003`, `FCCD-P08-004`, `FCCD-P08-007`, and `FCCD-P08-008` remain PENDING."
replace_once("PROJECT_CONTROL.md", old_pc, new_pc)

replace_once(
    "docs/TASK_LEDGER.md",
    "| FCCD-P08-005 | PowerShell/CMD profiles | PENDING |",
    "| FCCD-P08-005 | PowerShell/CMD profiles | CLOSED |",
)

ledger_entry = """`FCCD-P08-005` is CLOSED from the immutable PowerShell/CMD shell-profile contracts integrated in PR #198. Exact implementation candidate `214e3fb7e9b4a985474f5a690d4e6b90da8651fe` passed Windows CI `34086264659` / #483, P06-007 Workspace Search `34086264671` / #212, and P06-008 Large Workspace Safeguards `34086264743` / #196. PR #198 was normally merged as `9bcb6b94bbc5f185e8615c38b73e4f5496a0397e`; that exact canonical main passed Windows CI `34086944769` / #484, P06-007 Workspace Search `34086944845` / #213, and P06-008 Large Workspace Safeguards `34086944798` / #197. Coverage includes immutable validated shell-profile identity/display/executable fields, snapshotted argument lists that cannot be mutated by callers after construction, deterministic Windows PowerShell (`powershell.exe -NoLogo`) and Command Prompt (`cmd.exe`) defaults, and explicit Windows requirement metadata. The task defines launch contracts only and does not launch processes or take ConPTY/optional-shell/terminal-UX ownership. Task evidence: `evidence/phases/P08/P08_005_INTEGRATED_RECONCILIATION_2026-09-07.md`. No P08 phase closure, P09/P14 authorization, new owner-only obligation, release eligibility, or `VERIFIED_FINAL_COMPLETE=true` is claimed; P08 remains `IN_PROGRESS`, P08-003, P08-004, P08-007, and P08-008 remain PENDING, and the two existing owner-last release blockers remain unchanged.

"""
replace_once(
    "docs/TASK_LEDGER.md",
    "## P09 — External Tool Gateway\n",
    ledger_entry + "## P09 — External Tool Gateway\n",
)

evidence = """# FCCD-P08-005 — Integrated Reconciliation Evidence

Date: 2026-09-07
Task: `FCCD-P08-005 — PowerShell/CMD profiles`
Classification: CLOUD / HOSTED-WINDOWS / INTEGRATED
Canonical task result: CLOSED after normal integration and exact-main permanent validation.

## Implementation

- Implementation PR: #198 — `P08-005: add PowerShell and CMD shell profiles`
- Branch: `cloud/fccd-p08-005-shell-profiles-lane1`
- Exact accepted implementation candidate: `214e3fb7e9b4a985474f5a690d4e6b90da8651fe`
- Durable implementation scope:
  - `src/FCCCodeDesktop.Runtime/ShellProfiles.cs`
  - `tests/FCCCodeDesktop.UnitTests/ShellProfileTests.cs`
- Contract behavior:
  - immutable validated shell-profile identity/display/executable fields;
  - caller argument lists are snapshotted and exposed read-only;
  - deterministic Windows PowerShell profile uses `powershell.exe` with `-NoLogo`;
  - deterministic Command Prompt profile uses `cmd.exe` with no extra arguments;
  - both default profiles explicitly require Windows;
  - the contract describes launch metadata only and does not start a process.

## Exact implementation-head validation

On `214e3fb7e9b4a985474f5a690d4e6b90da8651fe`:

- Windows CI run `34086264659` / #483 — SUCCESS.
- P06-007 Workspace Search run `34086264671` / #212 — SUCCESS.
- P06-008 Large Workspace Safeguards run `34086264743` / #196 — SUCCESS.

## Normal integration

PR #198 was merged with the normal merge method. Accepted canonical implementation merge:

`9bcb6b94bbc5f185e8615c38b73e4f5496a0397e`

The merge preserves tested candidate `214e3fb7e9b4a985474f5a690d4e6b90da8651fe` as its second parent; no squash/rebase/force-push is claimed.

## Exact accepted-main validation

On exact canonical main `9bcb6b94bbc5f185e8615c38b73e4f5496a0397e`:

- Windows CI run `34086944769` / #484 — SUCCESS.
- P06-007 Workspace Search run `34086944845` / #213 — SUCCESS.
- P06-008 Large Workspace Safeguards run `34086944798` / #197 — SUCCESS.

No task-local cloud regression remains known after these permanent gates.

## Scope / owner-last boundary

This evidence closes only `FCCD-P08-005`.

It does not close P08, does not authorize P09 or P14, does not take ownership of P08-003/P08-004/P08-007/P08-008, and does not create any target/manual obligation. The canonical owner queue remains unchanged with exactly `OWNER-P04-008-REAL-TARGET` and `OWNER-P05-EXIT-REAL-TARGET`, both release-blocking. `VERIFIED_FINAL_COMPLETE` remains false.
"""
Path("evidence/phases/P08/P08_005_INTEGRATED_RECONCILIATION_2026-09-07.md").write_text(evidence, encoding="utf-8", newline="\n")
