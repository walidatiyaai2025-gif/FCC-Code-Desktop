# FCC Code Desktop — v1.0.0 Acceptance Matrix

## Rules

- Every mandatory row must end in `PASS` with evidence from the exact release candidate.
- `NOT_RUN`, `PARTIAL`, `FLAKY`, `SKIPPED`, `ASSUMED`, or `PASS_ON_OLD_SHA` are not release passes.
- Evidence location must be linked/recorded before closure.
- If a row reveals new required work, add/expand the task ledger rather than weakening the row.

Status legend: `NOT_RUN | PASS | FAIL | BLOCKED`

---

## A. Build and engineering

| ID | Acceptance | Release status |
|---|---|---|
| AC-BLD-001 | Clean Release build from exact candidate SHA | NOT_RUN |
| AC-BLD-002 | Project-owned analyzer/quality checks pass | NOT_RUN |
| AC-BLD-003 | Unit suite passes | NOT_RUN |
| AC-BLD-004 | Integration suite passes | NOT_RUN |
| AC-BLD-005 | No mandatory-v1 TODO/FIXME left unresolved | NOT_RUN |
| AC-BLD-006 | Dependency/version manifest reproducible | NOT_RUN |

## B. Product shell / UI

| ID | Acceptance | Release status |
|---|---|---|
| AC-UI-001 | Premium main shell complete; no placeholder/default-looking core UI | NOT_RUN |
| AC-UI-002 | Dark mode complete | NOT_RUN |
| AC-UI-003 | Light mode complete | NOT_RUN |
| AC-UI-004 | 1366×768 @100% usable without critical clipping | NOT_RUN |
| AC-UI-005 | 1920×1080 validated | NOT_RUN |
| AC-UI-006 | High-DPI 125/150/200% critical flows validated | NOT_RUN |
| AC-UI-007 | Keyboard/focus navigation for primary workflows | NOT_RUN |
| AC-UI-008 | Empty/loading/error/offline/queued/rate-limit/recovery states styled and usable | NOT_RUN |
| AC-UI-009 | Long chat/log/diff surfaces remain responsive | NOT_RUN |

## C. FCC / agent runtime

| ID | Acceptance | Release status |
|---|---|---|
| AC-RT-001 | Detect working `fcc-claude` environment | NOT_RUN |
| AC-RT-002 | Clear behavior when `fcc-claude` absent | NOT_RUN |
| AC-RT-003 | Real prompt completes through FCC | NOT_RUN |
| AC-RT-004 | Streaming normalized into UI events | NOT_RUN |
| AC-RT-005 | Tool activity observed/normalized | NOT_RUN |
| AC-RT-006 | Session identity persisted | NOT_RUN |
| AC-RT-007 | Session resume succeeds after app restart | NOT_RUN |
| AC-RT-008 | Graceful interrupt/cancel | NOT_RUN |
| AC-RT-009 | Forced child-process cleanup affects owned tree only | NOT_RUN |
| AC-RT-010 | FCC stop/failure classified and surfaced | NOT_RUN |
| AC-RT-011 | FCC recovery/retry succeeds without duplicate run | NOT_RUN |
| AC-RT-012 | Runtime version change triggers compatibility check | NOT_RUN |
| AC-RT-013 | Fallback runtime path contract passes | NOT_RUN |

## D. Projects / files / search / editor

| ID | Acceptance | Release status |
|---|---|---|
| AC-PROJ-001 | Add/reopen project persists | NOT_RUN |
| AC-PROJ-002 | Git and non-Git folders supported | NOT_RUN |
| AC-FILE-001 | Large tree lazy-loads without UI freeze | NOT_RUN |
| AC-FILE-002 | Open/edit/save/reload text file correctly | NOT_RUN |
| AC-FILE-003 | Non-ASCII and space-containing paths | NOT_RUN |
| AC-FILE-004 | Workspace search + cancel | NOT_RUN |
| AC-FILE-005 | Large-file protection | NOT_RUN |
| AC-FILE-006 | User encoding/newline not unintentionally corrupted | NOT_RUN |

## E. Changes and Git

| ID | Acceptance | Release status |
|---|---|---|
| AC-GIT-001 | Status/diff correct with dirty tree | NOT_RUN |
| AC-GIT-002 | Stage/unstage | NOT_RUN |
| AC-GIT-003 | Branch create/checkout | NOT_RUN |
| AC-GIT-004 | Fetch/pull behavior and errors | NOT_RUN |
| AC-GIT-005 | Commit | NOT_RUN |
| AC-GIT-006 | Push | NOT_RUN |
| AC-GIT-007 | Conflict surfaced safely | NOT_RUN |
| AC-GIT-008 | Pre-existing user changes preserved | NOT_RUN |
| AC-GIT-009 | Destructive operations governed; no silent hard reset/clean/force push | NOT_RUN |

## F. Terminal / process supervision

| ID | Acceptance | Release status |
|---|---|---|
| AC-TERM-001 | PowerShell interactive session | NOT_RUN |
| AC-TERM-002 | CMD session | NOT_RUN |
| AC-TERM-003 | ANSI/UTF-8/resize/copy/paste/Ctrl+C | NOT_RUN |
| AC-TERM-004 | High-volume output without UI lock | NOT_RUN |
| AC-PROC-001 | Owned process lifecycle tracked | NOT_RUN |
| AC-PROC-002 | Cancellation escalation works | NOT_RUN |
| AC-PROC-003 | Unrelated same-name process never killed | NOT_RUN |

## G. Queue / rate limiting

| ID | Acceptance | Release status |
|---|---|---|
| AC-QUE-001 | Default global concurrency is exactly 1 | NOT_RUN |
| AC-QUE-002 | Second chat cannot start while first active | NOT_RUN |
| AC-QUE-003 | 15-second default cooldown enforced | NOT_RUN |
| AC-QUE-004 | Queue order persisted across restart | NOT_RUN |
| AC-QUE-005 | Cancel queued item | NOT_RUN |
| AC-QUE-006 | Rate-limit pauses new starts | NOT_RUN |
| AC-QUE-007 | Bounded backoff/retry state visible | NOT_RUN |
| AC-QUE-008 | No duplicate execution after recovery | NOT_RUN |

## H. Unity first-class adapter

| ID | Acceptance | Release status |
|---|---|---|
| AC-UNITY-001 | Unity project detected | NOT_RUN |
| AC-UNITY-002 | Required project Editor version parsed | NOT_RUN |
| AC-UNITY-003 | Compatible local Unity installation resolved | NOT_RUN |
| AC-UNITY-004 | Missing/incompatible Unity handled clearly | NOT_RUN |
| AC-UNITY-005 | Correct project launched deterministically | NOT_RUN |
| AC-UNITY-006 | Batch operation with dedicated log works | NOT_RUN |
| AC-UNITY-007 | Compile success and compile failure classified | NOT_RUN |
| AC-UNITY-008 | EditMode test execution where fixture supports it | NOT_RUN |
| AC-UNITY-009 | PlayMode test execution where fixture supports it | NOT_RUN |
| AC-UNITY-010 | Project-owned Editor automation entry point invoked | NOT_RUN |
| AC-UNITY-011 | Build operation/artifact validation | NOT_RUN |
| AC-UNITY-012 | Same-project unsafe concurrent automation prevented | NOT_RUN |
| AC-UNITY-013 | Cancellation/recovery | NOT_RUN |
| AC-UNITY-014 | Logs/errors appear as structured agent activity | NOT_RUN |

## I. Blender first-class adapter

| ID | Acceptance | Release status |
|---|---|---|
| AC-BLENDER-001 | Supported Blender installation/version detected | NOT_RUN |
| AC-BLENDER-002 | Missing/incompatible Blender handled clearly | NOT_RUN |
| AC-BLENDER-003 | Background Blender process launches with ordered arguments | NOT_RUN |
| AC-BLENDER-004 | Blender Python automation executes | NOT_RUN |
| AC-BLENDER-005 | Script creates/modifies a test scene/mesh | NOT_RUN |
| AC-BLENDER-006 | `.blend` output saved and validated | NOT_RUN |
| AC-BLENDER-007 | Game-pipeline export fixture produced and validated | NOT_RUN |
| AC-BLENDER-008 | Preview render produced and validated | NOT_RUN |
| AC-BLENDER-009 | Python exception parsed/classified | NOT_RUN |
| AC-BLENDER-010 | Console/log output streams into structured activity | NOT_RUN |
| AC-BLENDER-011 | Existing valuable asset checkpointed before replacement | NOT_RUN |
| AC-BLENDER-012 | Same-target unsafe concurrent automation prevented | NOT_RUN |
| AC-BLENDER-013 | Cancellation/recovery | NOT_RUN |

## J. Unity↔Blender end-to-end

| ID | Acceptance | Release status |
|---|---|---|
| AC-3D-001 | Agent-triggered Blender fixture asset generated | NOT_RUN |
| AC-3D-002 | Export artifact placed into approved Unity fixture path | NOT_RUN |
| AC-3D-003 | Unity imports/recognizes generated asset | NOT_RUN |
| AC-3D-004 | Unity compile/test/build validation succeeds with asset | NOT_RUN |
| AC-3D-005 | Broken Blender export causes detected failure, not false success | NOT_RUN |
| AC-3D-006 | Combined evidence returned to agent workflow | NOT_RUN |

## K. Persistence / recovery

| ID | Acceptance | Release status |
|---|---|---|
| AC-DATA-001 | SQLite initializes and migrations apply | NOT_RUN |
| AC-DATA-002 | Project/session/message/task persistence | NOT_RUN |
| AC-DATA-003 | Backup rotation | NOT_RUN |
| AC-DATA-004 | Corruption/integrity failure recovery path | NOT_RUN |
| AC-REC-001 | App killed during streaming; restart reconciles task | NOT_RUN |
| AC-REC-002 | App killed during file-changing task; changes preserved/reviewable | NOT_RUN |
| AC-REC-003 | App killed during Unity operation; recovery reconciles process/state | NOT_RUN |
| AC-REC-004 | App killed during Blender operation; recovery reconciles process/state | NOT_RUN |
| AC-REC-005 | Windows/reboot-like abandoned task recovery | NOT_RUN |

## L. Security / privacy

| ID | Acceptance | Release status |
|---|---|---|
| AC-SEC-001 | Fake API key absent from persistent logs | NOT_RUN |
| AC-SEC-002 | Fake bearer token absent from diagnostics bundle | NOT_RUN |
| AC-SEC-003 | No telemetry/network tracking introduced by product | NOT_RUN |
| AC-SEC-004 | Path/argument handling avoids unsafe shell concatenation | NOT_RUN |
| AC-SEC-005 | High-risk permission mode clearly opt-in | NOT_RUN |
| AC-SEC-006 | WebView bridge surface constrained | NOT_RUN |

## M. Diagnostics

| ID | Acceptance | Release status |
|---|---|---|
| AC-DIAG-001 | Health center reports app/DB/FCC/Git/runtime | NOT_RUN |
| AC-DIAG-002 | Unity health displayed when relevant | NOT_RUN |
| AC-DIAG-003 | Blender health displayed when relevant | NOT_RUN |
| AC-DIAG-004 | Diagnostic ZIP export succeeds | NOT_RUN |
| AC-DIAG-005 | Diagnostic ZIP sanitized | NOT_RUN |
| AC-DIAG-006 | Correlation IDs trace task→runtime→tool→process | NOT_RUN |

## N. Installer / lifecycle

| ID | Acceptance | Release status |
|---|---|---|
| AC-SETUP-001 | Premium branded setup executable | NOT_RUN |
| AC-SETUP-002 | Professional original icon all Windows surfaces | NOT_RUN |
| AC-SETUP-003 | Fresh install succeeds | NOT_RUN |
| AC-SETUP-004 | Launch immediately after install | NOT_RUN |
| AC-SETUP-005 | No dev SDK/source checkout required | NOT_RUN |
| AC-SETUP-006 | Upgrade preserves data | NOT_RUN |
| AC-SETUP-007 | Failed migration/upgrade preserves recovery path | NOT_RUN |
| AC-SETUP-008 | Uninstall app-only preserves data by default | NOT_RUN |
| AC-SETUP-009 | Optional remove-product-data behavior correctly scoped | NOT_RUN |
| AC-SETUP-010 | Source repos/FCC/Unity/Blender user data never removed by uninstall | NOT_RUN |

## O. Clean-machine / final provenance

| ID | Acceptance | Release status |
|---|---|---|
| AC-FINAL-001 | Clean Windows install scenario | NOT_RUN |
| AC-FINAL-002 | Primary FCC+Git machine scenario | NOT_RUN |
| AC-FINAL-003 | Unity tool-runner scenario | NOT_RUN |
| AC-FINAL-004 | Blender tool-runner scenario | NOT_RUN |
| AC-FINAL-005 | Exact candidate SHA recorded | NOT_RUN |
| AC-FINAL-006 | Installer/application checksums recorded | NOT_RUN |
| AC-FINAL-007 | Bundled asset/dependency provenance reviewed | NOT_RUN |
| AC-FINAL-008 | Task ledger has zero mandatory unresolved items | NOT_RUN |

---

## Final release rule

`v1.0.0` is eligible only when every mandatory row above is `PASS` on the exact release candidate, unless a row is explicitly replaced by a stronger documented acceptance requirement through a repository ADR. Rows may not simply be deleted to make release easier.