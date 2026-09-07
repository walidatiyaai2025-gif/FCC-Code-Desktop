# P08-007 — Integrated reconciliation

**Task:** `FCCD-P08-007 — Interactive terminal UX`  
**Canonical status:** `CLOSED`  
**Current phase after reconciliation:** `P08 — Terminal/process supervision` / `IN_PROGRESS`  
**Phase exit gate:** `NOT_RUN`

## Implementation

PR #206 implemented the production interactive terminal surface by composing the existing typed ConPTY host into the existing Bottom Tool Panel terminal seam while preserving the P02 shell contract. The exact implementation candidate was `bebe178af5f36f97427d101c5b2e3ab2cd0ff075`.

Implemented behavior includes validated PowerShell/CMD and read-only detected Git Bash/WSL choices; UTF-8 input/output; Ctrl+C selected-text copy versus ETX interrupt; Ctrl+V paste; navigation escape sequences; debounced ConPTY resize; explicit Start/Close lifecycle; bounded/coalesced high-output rendering; ANSI SGR color presentation; and window-close cancellation until async terminal disposal completes.

Exact implementation-head gates completed SUCCESS:

- Windows CI #554 / run `34161412445`
- P06-007 Workspace Search #283 / run `34161412447`
- P06-008 Large Workspace Safeguards #267 / run `34161412450`
- P08-007 Interactive Terminal UX #3 / run `34161412438`

PR #206 was normally merged as `53df4d66026b0e793d7c7797a016ed654d8c3663`.

## Exact accepted-main verification

The exact resulting main `53df4d66026b0e793d7c7797a016ed654d8c3663` passed the complete applicable gate set:

- Windows CI #555 / run `34161963855` — SUCCESS
- P06-007 Workspace Search #284 / run `34161963822` — SUCCESS
- P06-008 Large Workspace Safeguards #268 / run `34161963867` — SUCCESS
- P08-007 Interactive Terminal UX #4 / run `34161963864` — SUCCESS

This is the accepted implementation integration baseline for P08-007.

## Recovered defects

The task remained open while repairable defects existed. The branch recovered:

- the inherited P02 bottom-tool-panel validator failure without weakening the canonical P02 composition token;
- missing deterministic async terminal disposal from the application window-close path;
- ANSI stripping, replacing it with bounded SGR color rendering;
- whole-transcript-per-chunk UI rebuilding, replacing it with bounded/coalesced dispatcher presentation;
- analyzer `CA1859` by correcting the private helper type rather than suppressing the analyzer;
- the WPF runtime fixture STA-entrypoint defect.

The one-off staging helper `tools/reconciliation/repair-p08-007-candidate.py` is removed during final reconciliation because it encoded superseded pre-repair assumptions and is not a production or permanent validation tool.

## Closure boundary

P08-007 is CLOSED only after this reconciliation is normally integrated and its reconciliation head/resulting main remain green. No owner-only evidence is required or added. P08 remains `IN_PROGRESS`; only `FCCD-P08-008 — Process/terminal safety tests` remains PENDING and `PHASE_EXIT_GATE=NOT_RUN`. P09 and later phases remain prohibited until P08-008 and the formal P08 phase exit/closure process are complete. `VERIFIED_FINAL_COMPLETE=false`.
