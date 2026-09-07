# FCCD-P08-005 — Integrated Reconciliation Evidence

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

## Reconciliation consistency

The canonical reconciliation also removes the stale P08-006 narrative reference that still listed P08-005 as PENDING; the P08 inventory and narrative now consistently leave only P08-003, P08-004, P08-007, and P08-008 pending.

## Scope / owner-last boundary

This evidence closes only `FCCD-P08-005`.

It does not close P08, does not authorize P09 or P14, does not take ownership of P08-003/P08-004/P08-007/P08-008, and does not create any target/manual obligation. The canonical owner queue remains unchanged with exactly `OWNER-P04-008-REAL-TARGET` and `OWNER-P05-EXIT-REAL-TARGET`, both release-blocking. `VERIFIED_FINAL_COMPLETE` remains false.
