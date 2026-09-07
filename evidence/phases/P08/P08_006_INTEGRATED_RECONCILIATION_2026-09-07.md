# P08-006 — Integrated reconciliation

**Task:** `FCCD-P08-006 — Optional Git Bash/WSL detection`  
**Canonical status:** `CLOSED`  
**Current phase after reconciliation:** `P08 — Terminal/process supervision` / `IN_PROGRESS`  
**Phase exit gate:** `NOT_RUN`

## Implementation

PR #197 implemented an Application-owned typed optional-shell detection contract and a Windows read-only detector. The exact implementation candidate was `72f313ab27a54a0188ee31faea69f00aaeb0f0c9`. Detection is intentionally conservative: Git Bash is discovered only through standard Git-for-Windows install roots plus PATH entries that can be related back to a Git root, and WSL availability is represented only by the presence of `%SystemRoot%/System32/wsl.exe`. No detector path launches a shell, starts/enumerates WSL distributions, writes files, mutates environment variables, writes the registry, or uses the network.

Exact implementation-head gates completed SUCCESS:

- Windows CI #474 / run `34083435776`
- P06-007 Workspace Search #203 / run `34083435742`
- P06-008 Large Workspace Safeguards #187 / run `34083435765`
- P08-006 Optional Shell Detection #6 / run `34083435787`

PR #197 was normally merged as `e43ef53eb8b7826e8541875576a5fa097c5c81a4`, preserving exact candidate ancestry.

## Exact accepted-main verification

The exact resulting main `e43ef53eb8b7826e8541875576a5fa097c5c81a4` passed the complete applicable gate set:

- Windows CI #476 / run `34084399929` — SUCCESS
- P06-007 Workspace Search #205 / run `34084399920` — SUCCESS
- P06-008 Large Workspace Safeguards #189 / run `34084399959` — SUCCESS
- P08-006 Optional Shell Detection #7 / run `34084399934` — SUCCESS

This is the accepted canonical integration baseline for P08-006.

## Closure boundary

P08-006 is therefore CLOSED. P08 remains `IN_PROGRESS`; P08-003, P08-004, P08-005, P08-007, and P08-008 remain PENDING and `PHASE_EXIT_GATE=NOT_RUN`. P09, P14, and later phases remain prohibited.

No new owner-only evidence is required by this task. The existing release-blocking owner queue remains unchanged:

- `OWNER-P04-008-REAL-TARGET`
- `OWNER-P05-EXIT-REAL-TARGET`

`KNOWN_RELEASE_BLOCKERS=2`, `P04=NOT_RUN`, `P05=NOT_RUN`, and `VERIFIED_FINAL_COMPLETE=false` remain unchanged.
