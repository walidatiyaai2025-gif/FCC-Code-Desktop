# FCC Code Desktop — Canonical Task Ledger

This file is the authoritative inventory of mandatory v1 work.

## State rules

Allowed states:

`PENDING | CLAIMED | IN_PROGRESS | BLOCKED | IMPLEMENTED | VERIFIED | CLOSED`

- `IMPLEMENTED` means code exists but all closure evidence is not complete.
- `VERIFIED` means required evidence for the task passes on its candidate head.
- `CLOSED` means integrated into the canonical product baseline with no unresolved task-local regression.
- Release requires every mandatory task below to be `CLOSED` and the final acceptance matrix to pass on the exact release head.

Current verified implementation completion: **0%**.

Documentation/governance closure does not count as verified implementation completion.

## Sequential phase rule

Only tasks belonging to the phase in `CURRENT_PHASE.md` may be actively implemented.

A later phase may not open until every mandatory task in the current phase is `CLOSED` and the phase exit gate in `docs/EXECUTION_PLAN.md` is `PASS` with evidence stored under `evidence/phases/PXX/CLOSURE.md`.

Multiple workers may claim non-overlapping tasks only inside the same current phase.

---

## P00 — Constitution and contract de-risking

| ID | Task | State |
|---|---|---|
| FCCD-P00-001 | Establish repository constitution/source-of-truth docs | CLOSED |
| FCCD-P00-002 | Probe installed FCC/`fcc-claude` discovery/version/health behavior | CLOSED |
| FCCD-P00-003 | Probe real structured streaming contract | CLOSED |
| FCCD-P00-004 | Probe session ID/resume behavior | CLOSED |
| FCCD-P00-005 | Probe interrupt/cancel/error/rate-limit behavior | CLOSED |
| FCCD-P00-006 | Determine primary runtime adapter contract from evidence | CLOSED |
| FCCD-P00-007 | Prove CLI fallback contract | CLOSED |
| FCCD-P00-008 | Probe Unity current project/version/CLI/test/build contracts on target environment | CLOSED |
| FCCD-P00-009 | Probe Blender current CLI/background/Python/render/export contracts on target environment | CLOSED |
| FCCD-P00-010 | Record supported version/compatibility baseline | CLOSED |

Target reconciliation for `FCCD-P00-002` and `FCCD-P00-007` is complete. The owner Windows target exposes `fcc-claude` 2.1.251 and healthy FCC loopback behavior. `FCCD-P00-007` is CLOSED from provider-backed target execution at tested source SHA `8e59cd94ff0b13d56725686296c452b832c5b016`: launch and prompt transmission succeeded in normal, spaced, and Unicode/Arabic working directories; stdout/stderr were observable; terminal completion was classified successfully; graceful cancellation was exercised; and owned-process cleanup passed. See `docs/contracts/FCC_CLI_CONTRACT.md`, `evidence/phases/P00/cli-fallback/fcc-cli-fallback-target-closure.json`, and `evidence/phases/P00/cli-fallback/P00_007_TARGET_VALIDATION_2026-09-02.md`.

Target reconciliation for `FCCD-P00-003`, `FCCD-P00-004`, and `FCCD-P00-005` is complete. `FCCD-P00-004` is CLOSED from authoritative Windows target evidence at tested source SHA `8affdae59922f945576cc45fbd49d4fb68634b66`. `FCCD-P00-005` is CLOSED from authoritative exact-head Windows target evidence at tested source SHA `015ffd8c0e2a6e725e33ed153441ff51e7952556`: the provider-backed baseline classified `SUCCESS`; cancellation classified `INTERRUPTED`; graceful interruption, hardened descendant observation, residual cleanup by previously observed PID/identity, zero remaining owned processes, and persisted secret scan passed. The target recorded `RATE_LIMIT = NOT_OBSERVED_ON_TARGET`; no artificial 429 traffic was generated. `PG-002-P00-RATE-LIMIT-CLOSURE` is RESOLVED by `docs/contracts/FCC_RATE_LIMIT_CLOSURE_POLICY.md`, which accepts this explicit non-observation plus verified SELF_TEST_ONLY classifier mechanics as the safe P00-005 closure boundary without claiming an actual provider 429. See `docs/contracts/FCC_FAILURE_CONTRACT.md`, `evidence/phases/P00/failure/fcc-failure-target-exact-head.json`, and `evidence/phases/P00/failure/P00_005_TARGET_RERUN_2026-09-02.md`.

Closure evidence for `FCCD-P00-008`: the reusable probe infrastructure and deterministic 20/20 self-test were integrated and then executed on the owner's Windows target. Unity Hub, Editors `6000.5.8f1`/`2022.3.75f1`, disposable project creation, exact version selection, compile positive/negative/recovery, EditMode/PlayMode tests, `-executeMethod`, Windows x64 build artifacts, same-project locking, cancellation, and cleanup passed. See `docs/contracts/UNITY_AUTOMATION_CONTRACT.md`, `evidence/phases/P00/target/unity-contract.json`, and `evidence/phases/P00/unity/TARGET_VALIDATION_2026-09-02.md`.

Closure evidence for `FCCD-P00-009`: the current integrated Blender probe passed on the owner's authoritative Windows target using Blender `5.2.0` at tested source SHA `e6932783b30ab0bdbb596c7959e03143753bff9a`. Discovery/version, background/factory-startup execution, Python automation, `.blend` save validation, PNG render validation, OBJ export validation, controlled nonzero Python failure, owned cancellation, cleanup, Unicode/Arabic/space-containing fixture paths, and 29/29 deterministic self-tests passed. The sanitized target evidence was integrated by PR #40. `FCCD-P00-009` is CLOSED.

Closure evidence for `FCCD-P00-006` and `FCCD-P00-010`: the complete reconciled runtime/compatibility evidence set was integrated by PR #41, then the exact-head non-provider P00 pre-closure gate passed on candidate SHA `49840a7c9c7c9300dbeb3f2ec7077acb2f8bebe9`. The gate verified required evidence ancestry, 6/6 contract-probe self-tests, target evidence secret sanity, zero open plan gaps, zero known P00 blockers, `p00TargetValidationComplete=true`, and a clean exact-head worktree. Both tasks are CLOSED. See `docs/contracts/P00_RUNTIME_AND_COMPATIBILITY_BASELINE.md` and `evidence/phases/P00/CLOSURE.md`.

## P01 — Solution foundation / CI

| ID | Task | State |
|---|---|---|
| FCCD-P01-001 | Create .NET 10 solution/projects with clean boundaries | CLOSED |
| FCCD-P01-002 | Configure nullable/analyzers/style/quality policy | CLOSED |
| FCCD-P01-003 | Dependency pinning/lock strategy | CLOSED |
| FCCD-P01-004 | Unit/integration test infrastructure | CLOSED |
| FCCD-P01-005 | Windows CI Release build/test pipeline | CLOSED |
| FCCD-P01-006 | Build metadata/version service | CLOSED |

Closure evidence for `FCCD-P01-001` through `FCCD-P01-006` is recorded in `evidence/phases/P01/INTEGRATED_TASK_RECONCILIATION_2026-09-03.md`. This closes the six task rows only from implementation + task-specific/cloud validation + normal canonical integration + exact-current-main non-regression CI. P01 itself remains IN_PROGRESS and its exit gate remains NOT_RUN.

## P02 — Premium design system and shell

| ID | Task | State |
|---|---|---|
| FCCD-P02-001 | Define design tokens and typography | PENDING |
| FCCD-P02-002 | Dark/light semantic themes | PENDING |
| FCCD-P02-003 | Premium title/app chrome | PENDING |
| FCCD-P02-004 | Main resizable workspace layout | PENDING |
| FCCD-P02-005 | Navigation/projects/sessions/tasks surfaces | PENDING |
| FCCD-P02-006 | Bottom tool panel framework | PENDING |
| FCCD-P02-007 | Command palette/keyboard framework | PENDING |
| FCCD-P02-008 | Common empty/loading/error/status components | PENDING |
| FCCD-P02-009 | DPI/resolution layout foundations | PENDING |

## P03 — Persistence/state model

| ID | Task | State |
|---|---|---|
| FCCD-P03-001 | SQLite bootstrap and schema migrations | PENDING |
| FCCD-P03-002 | Project/session/message persistence | PENDING |
| FCCD-P03-003 | Task/agent/tool/process event journal | PENDING |
| FCCD-P03-004 | Queue persistence | PENDING |
| FCCD-P03-005 | Settings persistence | PENDING |
| FCCD-P03-006 | Database integrity/backup rotation | PENDING |
| FCCD-P03-007 | Migration/recovery tests | PENDING |

## P04 — FCC/Claude runtime

| ID | Task | State |
|---|---|---|
| FCCD-P04-001 | FCC/`fcc-claude` environment discovery | PENDING |
| FCCD-P04-002 | `IAgentRuntime` domain contract | PENDING |
| FCCD-P04-003 | Primary FCC/Claude structured runtime adapter | PENDING |
| FCCD-P04-004 | CLI fallback runtime adapter | PENDING |
| FCCD-P04-005 | Runtime event normalization | PENDING |
| FCCD-P04-006 | Runtime health/version compatibility service | PENDING |
| FCCD-P04-007 | Start/stop/retry supervision | PENDING |
| FCCD-P04-008 | Runtime contract suite | PENDING |

## P05 — Conversation/session/task UX

| ID | Task | State |
|---|---|---|
| FCCD-P05-001 | Streaming chat rendering | PENDING |
| FCCD-P05-002 | Structured tool activity timeline | PENDING |
| FCCD-P05-003 | Composer/attachments/context | PENDING |
| FCCD-P05-004 | Session create/history/resume | PENDING |
| FCCD-P05-005 | Explicit task state machine | PENDING |
| FCCD-P05-006 | Stop/cancel/retry UX | PENDING |
| FCCD-P05-007 | Markdown/code/diff content rendering | PENDING |
| FCCD-P05-008 | Conversation virtualization/performance | PENDING |

## P06 — Projects/files/editor/search

| ID | Task | State |
|---|---|---|
| FCCD-P06-001 | Add/open/recent project workflows | PENDING |
| FCCD-P06-002 | Project technology/tool detection framework | PENDING |
| FCCD-P06-003 | Lazy file explorer | PENDING |
| FCCD-P06-004 | Safe file service | PENDING |
| FCCD-P06-005 | Locally bundled code editor | PENDING |
| FCCD-P06-006 | Editor tabs/save/reload/dirty state | PENDING |
| FCCD-P06-007 | Workspace content/file/regex search | PENDING |
| FCCD-P06-008 | Large file/tree safeguards | PENDING |

## P07 — Changes and Git

| ID | Task | State |
|---|---|---|
| FCCD-P07-001 | `IGitService` and repository detection | PENDING |
| FCCD-P07-002 | Status/changed-files surface | PENDING |
| FCCD-P07-003 | Diff viewer | PENDING |
| FCCD-P07-004 | Stage/unstage | PENDING |
| FCCD-P07-005 | Branch create/checkout | PENDING |
| FCCD-P07-006 | Fetch/pull | PENDING |
| FCCD-P07-007 | Commit/push | PENDING |
| FCCD-P07-008 | History | PENDING |
| FCCD-P07-009 | Dirty/pre-existing-change provenance | PENDING |
| FCCD-P07-010 | Destructive-operation safeguards | PENDING |
| FCCD-P07-011 | Git integration tests/conflict scenarios | PENDING |

## P08 — Terminal/process supervision

| ID | Task | State |
|---|---|---|
| FCCD-P08-001 | Process supervisor with owned process-tree tracking | PENDING |
| FCCD-P08-002 | Graceful→forced cancellation escalation | PENDING |
| FCCD-P08-003 | Bounded streaming log pipeline | PENDING |
| FCCD-P08-004 | ConPTY terminal host | PENDING |
| FCCD-P08-005 | PowerShell/CMD profiles | PENDING |
| FCCD-P08-006 | Optional Git Bash/WSL detection | PENDING |
| FCCD-P08-007 | Interactive terminal UX | PENDING |
| FCCD-P08-008 | Process/terminal safety tests | PENDING |

## P09 — External Tool Gateway

| ID | Task | State |
|---|---|---|
| FCCD-P09-001 | `IExternalToolAdapter` contract | PENDING |
| FCCD-P09-002 | Tool discovery/capability registry | PENDING |
| FCCD-P09-003 | Structured invocation/result contracts | PENDING |
| FCCD-P09-004 | Tool resource locking | PENDING |
| FCCD-P09-005 | Artifact manifest/validation framework | PENDING |
| FCCD-P09-006 | Tool diagnostics/health framework | PENDING |
| FCCD-P09-007 | CLI/process generic adapter primitives | PENDING |
| FCCD-P09-008 | Optional protocol adapter seam (DAP/MCP/etc.) without core coupling | PENDING |

## P10 — Unity first-class adapter

| ID | Task | State |
|---|---|---|
| FCCD-P10-001 | Unity project/version detector | PENDING |
| FCCD-P10-002 | Unity install/Hub editor resolver | PENDING |
| FCCD-P10-003 | Strongly typed Unity CLI command builder | PENDING |
| FCCD-P10-004 | Unity process/project resource locking | PENDING |
| FCCD-P10-005 | Dedicated Unity log capture/parser | PENDING |
| FCCD-P10-006 | Compile validation | PENDING |
| FCCD-P10-007 | EditMode test integration | PENDING |
| FCCD-P10-008 | PlayMode test integration | PENDING |
| FCCD-P10-009 | Project-owned Editor automation invocation | PENDING |
| FCCD-P10-010 | Build target execution/artifact validation | PENDING |
| FCCD-P10-011 | Unity structured UI events | PENDING |
| FCCD-P10-012 | Unity cancellation/recovery | PENDING |
| FCCD-P10-013 | Unity contract fixture/suite | PENDING |

## P11 — Blender first-class adapter

| ID | Task | State |
|---|---|---|
| FCCD-P11-001 | Blender install/version resolver | PENDING |
| FCCD-P11-002 | Ordered strongly typed Blender CLI builder | PENDING |
| FCCD-P11-003 | Background/headless runner | PENDING |
| FCCD-P11-004 | Task-correlated Blender Python runner | PENDING |
| FCCD-P11-005 | Scene/mesh/material automation fixture | PENDING |
| FCCD-P11-006 | Import/export automation | PENDING |
| FCCD-P11-007 | Render/preview automation | PENDING |
| FCCD-P11-008 | Console/log/debug parser | PENDING |
| FCCD-P11-009 | `.blend`/export/render artifact validator | PENDING |
| FCCD-P11-010 | Asset checkpoint/backup safeguard | PENDING |
| FCCD-P11-011 | Blender resource locking | PENDING |
| FCCD-P11-012 | Blender structured UI events/artifact preview | PENDING |
| FCCD-P11-013 | Blender cancellation/recovery | PENDING |
| FCCD-P11-014 | Blender contract fixture/suite | PENDING |

## P12 — Unity↔Blender AI asset pipeline

| ID | Task | State |
|---|---|---|
| FCCD-P12-001 | Cross-tool orchestration use case | PENDING |
| FCCD-P12-002 | Approved artifact handoff/manifest | PENDING |
| FCCD-P12-003 | Unity import verification of Blender output | PENDING |
| FCCD-P12-004 | Broken/missing artifact negative tests | PENDING |
| FCCD-P12-005 | End-to-end AI 3D fixture acceptance | PENDING |

## P13 — Permissions and safety

| ID | Task | State |
|---|---|---|
| FCCD-P13-001 | Permission profile model/mapping | PENDING |
| FCCD-P13-002 | Permission request UX | PENDING |
| FCCD-P13-003 | Full-access high-risk warning flow | PENDING |
| FCCD-P13-004 | File/Git/tool side-effect classification | PENDING |
| FCCD-P13-005 | Workspace checkpoint policy | PENDING |
| FCCD-P13-006 | Unsafe path/argument guards | PENDING |

## P14 — Global queue / cooldown / throttling

| ID | Task | State |
|---|---|---|
| FCCD-P14-001 | Durable global execution coordinator | PENDING |
| FCCD-P14-002 | Enforce concurrency=1 | PENDING |
| FCCD-P14-003 | Enforce default 15s inter-run cooldown | PENDING |
| FCCD-P14-004 | Queue inspect/reorder/cancel UI | PENDING |
| FCCD-P14-005 | Rate-limit detection/classification | PENDING |
| FCCD-P14-006 | Bounded backoff/retry policy | PENDING |
| FCCD-P14-007 | Restart recovery without duplicate launch | PENDING |
| FCCD-P14-008 | Concurrency/rate-limit stress tests | PENDING |

## P15 — Recovery / backups

| ID | Task | State |
|---|---|---|
| FCCD-P15-001 | Durable recovery journal | PENDING |
| FCCD-P15-002 | Startup reconciliation engine | PENDING |
| FCCD-P15-003 | Interrupted agent-run recovery | PENDING |
| FCCD-P15-004 | Interrupted file/Git mutation recovery | PENDING |
| FCCD-P15-005 | Interrupted Unity operation recovery | PENDING |
| FCCD-P15-006 | Interrupted Blender operation recovery | PENDING |
| FCCD-P15-007 | Crash/reboot fault-injection suite | PENDING |
| FCCD-P15-008 | Automatic DB backup retention/recovery | PENDING |

## P16 — Diagnostics/security/performance

| ID | Task | State |
|---|---|---|
| FCCD-P16-001 | Structured logger/correlation system | PENDING |
| FCCD-P16-002 | Secret redaction at sink boundary | PENDING |
| FCCD-P16-003 | Health/diagnostics center | PENDING |
| FCCD-P16-004 | Sanitized diagnostic ZIP | PENDING |
| FCCD-P16-005 | No-telemetry verification | PENDING |
| FCCD-P16-006 | Large repo/search performance tests | PENDING |
| FCCD-P16-007 | Long chat/log/output memory tests | PENDING |
| FCCD-P16-008 | Unity/Blender high-output performance tests | PENDING |
| FCCD-P16-009 | Dependency/security review | PENDING |

## P17 — Premium UX closure

| ID | Task | State |
|---|---|---|
| FCCD-P17-001 | Complete all component visual states | PENDING |
| FCCD-P17-002 | Keyboard/focus/accessibility pass | PENDING |
| FCCD-P17-003 | 1366×768 acceptance | PENDING |
| FCCD-P17-004 | 1920×1080 acceptance | PENDING |
| FCCD-P17-005 | 4K/high-DPI acceptance | PENDING |
| FCCD-P17-006 | Dark/light visual parity | PENDING |
| FCCD-P17-007 | Unity UX polish | PENDING |
| FCCD-P17-008 | Blender UX/artifact preview polish | PENDING |
| FCCD-P17-009 | Performance/perceived-latency polish | PENDING |

## P18 — Branding / setup

| ID | Task | State |
|---|---|---|
| FCCD-P18-001 | Original premium AI-assisted visual identity | PENDING |
| FCCD-P18-002 | Production `.ico` multi-size asset | PENDING |
| FCCD-P18-003 | Asset provenance record | PENDING |
| FCCD-P18-004 | Installer/bootstrapper architecture | PENDING |
| FCCD-P18-005 | Premium branded setup UI | PENDING |
| FCCD-P18-006 | Install/start-menu/taskbar/version metadata | PENDING |
| FCCD-P18-007 | First-run environment check | PENDING |

## P19 — Upgrade/uninstall lifecycle

| ID | Task | State |
|---|---|---|
| FCCD-P19-001 | In-place upgrade path | PENDING |
| FCCD-P19-002 | Data-preserving migration/backup rollback behavior | PENDING |
| FCCD-P19-003 | Uninstall app-only default | PENDING |
| FCCD-P19-004 | Optional product-data removal scoped safely | PENDING |
| FCCD-P19-005 | Installer lifecycle automation tests | PENDING |

## P20 — Full regression / exact-head CI

| ID | Task | State |
|---|---|---|
| FCCD-P20-001 | All non-environment automated suites green | PENDING |
| FCCD-P20-002 | FCC runtime contract suite green | PENDING |
| FCCD-P20-003 | Unity contract suite green | PENDING |
| FCCD-P20-004 | Blender contract suite green | PENDING |
| FCCD-P20-005 | Unity↔Blender E2E suite green | PENDING |
| FCCD-P20-006 | UI automation/accessibility suite green | PENDING |
| FCCD-P20-007 | Freeze exact release candidate SHA | PENDING |
| FCCD-P20-008 | Rerun all required gates on exact SHA | PENDING |

## P21 — Clean-machine / provenance

| ID | Task | State |
|---|---|---|
| FCCD-P21-001 | Build production setup from exact candidate | PENDING |
| FCCD-P21-002 | Clean Windows install/launch test | PENDING |
| FCCD-P21-003 | Primary FCC+Git acceptance machine | PENDING |
| FCCD-P21-004 | Unity environment acceptance | PENDING |
| FCCD-P21-005 | Blender environment acceptance | PENDING |
| FCCD-P21-006 | Upgrade/uninstall acceptance | PENDING |
| FCCD-P21-007 | Final visual screenshot evidence | PENDING |
| FCCD-P21-008 | Checksums/release manifest/provenance | PENDING |
| FCCD-P21-009 | Diagnostics bundle final redaction verification | PENDING |

## P22 — v1.0.0 closure

| ID | Task | State |
|---|---|---|
| FCCD-P22-001 | Reconcile every acceptance row to PASS | PENDING |
| FCCD-P22-002 | Reconcile ledger to zero unresolved mandatory work | PENDING |
| FCCD-P22-003 | Confirm no known release blocker | PENDING |
| FCCD-P22-004 | Tag exact candidate `v1.0.0` | PENDING |
| FCCD-P22-005 | Publish final production artifacts/release notes | PENDING |
| FCCD-P22-006 | Set final status `VERIFIED_FINAL_COMPLETE` | PENDING |

---

## Current next action

`CURRENT_PHASE = P01` and P01 remains `IN_PROGRESS`.

`FCCD-P01-001` through `FCCD-P01-006` are CLOSED from validated canonical integration. The durable task evidence map is `evidence/phases/P01/INTEGRATED_TASK_RECONCILIATION_2026-09-03.md`. The permanent Windows CI baseline is green on exact current `main` SHA `416651579fb8ee42442d961b469b16266810138a` (run `33719564337`).

The next legitimate current-phase action is the complete P01 exit gate: verify a fresh/exact checkout can restore, format, Release-build and run the full unit/integration and P01 policy baseline using the documented commands; record exact-head `evidence/phases/P01/CLOSURE.md`; keep P01 open if any check fails. Do not begin P02 until that closure is integrated and `PHASE_EXIT_GATE=PASS`. `VERIFIED_FINAL_COMPLETE` remains false.
