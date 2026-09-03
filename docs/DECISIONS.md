# FCC Code Desktop — Decision Log

This file records durable decisions. New workers must not reopen a decided item merely from preference; supersede it only with stronger evidence and a new explicit ADR.

---

## ADR-001 — Repository is permanent project memory

**Status:** Accepted  
**Date:** 2026-08-31

All durable requirements, decisions, work state and release evidence belong in this repository. Chat history is non-authoritative.

**Reason:** The project must be resumable after arbitrary interruption without depending on the owner's memory.

---

## ADR-002 — First public release is complete v1.0.0

**Status:** Accepted

No incomplete MVP/beta/preview is presented as the product. Internal builds are allowed but are not releases. First public product target is `v1.0.0` and requires all mandatory acceptance gates.

**Reason:** Owner explicitly requires a complete, working, premium product without discovering basic missing pieces after installation.

---

## ADR-003 — AI executes routine decisions autonomously

**Status:** Accepted

The owner supervises rather than making routine engineering choices. AI workers determine the technically strongest implementation consistent with the constitution, verify it and document material decisions.

**Reason:** The project must progress without repeated human micro-decisions.

---

## ADR-004 — Windows .NET 10 WPF baseline

**Status:** Accepted, subject only to evidence from P00/P01 showing a blocking technical contradiction.

Desktop baseline: C#/.NET 10/WPF with MVVM, dependency injection and modular boundaries.

**Reason:** Strong native Windows integration, mature process/OS APIs, straightforward packaging and suitability for the requested local desktop product.

---

## ADR-005 — External runtime isolation

**Status:** Accepted

The UI/domain never binds directly to unstable FCC/Claude internals. Agent operations pass through project-owned `IAgentRuntime` abstractions with a primary structured adapter and a compatibility fallback.

**Reason:** Upstream FCC/Claude changes must not force a UI/domain rewrite.

---

## ADR-006 — External Developer Tool Gateway

**Status:** Accepted

Build/test/debug/content applications integrate through `IExternalToolAdapter`-style project-owned contracts for discovery, capabilities, invocation, streaming results, cancellation, locks and artifact validation.

**Reason:** FCC Code Desktop is intended as a Codex-replacement workbench, not a chat-only frontend.

---

## ADR-007 — Unity is first-class v1 scope

**Status:** Accepted

Unity detection, CLI/batch automation, logs, compile/test/build validation, project locking and recovery are mandatory before v1.0.0 release.

**Reason:** AI must be able to debug and validate Unity projects without routine manual owner intervention.

---

## ADR-008 — Blender is first-class v1 scope

**Status:** Accepted

Blender detection, background execution, Python-driven 3D automation, import/export, rendering, artifact validation, logs, resource locking and recovery are mandatory before v1.0.0 release.

**Reason:** AI must be able to create and modify 3D content as part of end-to-end game/application workflows, not just edit code.

---

## ADR-009 — Unity↔Blender AI pipeline is a mandatory acceptance workflow

**Status:** Accepted

A v1 acceptance fixture must prove an agent/tool workflow can generate/modify a 3D asset via Blender, validate/export it, place it in an approved Unity fixture, then have Unity recognize/validate the result.

**Reason:** Independent tool integrations are insufficient if the intended end-to-end production workflow fails.

---

## ADR-010 — Default global agent concurrency is one

**Status:** Accepted

```text
GLOBAL_AGENT_CONCURRENCY = 1
DEFAULT_INTER_RUN_COOLDOWN_SECONDS = 15
```

Other sessions queue. Provider rate limiting pauses new starts. Same-project/tool resource locks may impose stricter limits.

**Reason:** Prevent duplicate/concurrent agent requests and upstream `too many requests` failures while keeping task execution deterministic.

---

## ADR-011 — Local-first persistence

**Status:** Accepted

SQLite with versioned migrations and local file-backed logs/artifacts. No FCC Code Desktop account/cloud/telemetry by default.

**Reason:** User's runtime is local and the desktop should retain state independently without introducing unnecessary hosted dependencies.

---

## ADR-012 — Crash/reboot recovery is architecture, not polish

**Status:** Accepted

Task/event/process journaling and startup reconciliation are built into v1. A process exit cannot be assumed to mean task failure/success without reconciliation.

**Reason:** Long AI/Unity/Blender operations make interruption inevitable in real use.

---

## ADR-013 — Process exit code alone is not success

**Status:** Accepted

Tool operations declare expected outputs/evidence. A successful exit with missing/corrupt/wrong artifacts is failure.

**Reason:** Automation must validate outcomes, especially for builds, tests, Unity outputs, Blender assets/renders/exports.

---

## ADR-014 — Professional setup and original icon are release gates

**Status:** Accepted

Premium branded setup, original production icon, product metadata, upgrade and uninstall are mandatory in first public release.

**Reason:** Packaging is part of product quality, not post-development work.

---

## ADR-015 — No silent destructive source/asset operation

**Status:** Accepted

Dangerous Git/file/asset operations require safeguards; pre-existing user work is never discarded merely to simplify agent execution.

**Reason:** Autonomous AI requires stricter data-integrity boundaries than a manual terminal workflow.

---

## ADR-016 — Evidence-based closure only

**Status:** Accepted

Completion state is driven by exact-head tests and acceptance evidence. File counts, screenshots, code volume and estimates do not establish verified completion.

**Reason:** Prevent false progress and release surprises.
## ADR-017 — Primary runtime uses observed structured `fcc-claude` process contract

**Status:** Accepted  
**Date:** 2026-09-02

P04 will implement the primary `IAgentRuntime` adapter as an owned `fcc-claude` process using the target-observed noninteractive `--print --output-format stream-json --verbose` surface. Newline-delimited JSON is normalized into project-owned events; unknown event types remain preserved. FCC loopback health is a separate readiness signal and cannot stand in for provider readiness.

The plain print/single-result CLI path remains the compatibility fallback. Successful completion and resume behavior remain P00 blockers while the configured upstream emits HTTP 503 retries. See `docs/contracts/P00_RUNTIME_AND_COMPATIBILITY_BASELINE.md`.

---

## ADR-018 — P02 design tokens are theme-neutral WPF resources

**Status:** Accepted  
**Date:** 2026-09-03

P02 establishes application-wide WPF resource dictionaries before shell views multiply. `FCCD-P02-001` owns theme-neutral spacing, inset, radius, stroke/focus geometry, control/icon density, and named typography roles. `FCCD-P02-002` owns semantic dark/light color and brush values and must compose with these resources rather than replacing their contract.

The interface typography baseline is Windows-native `Segoe UI`; code/monospace typography uses Windows-native `Consolas`. No bundled or remotely loaded font is introduced by P02-001. Named `TextBlock` styles are the required consumption surface for display, section, body, metadata, compact-status, and code text.

**Reason:** A stable theme-neutral token/typography contract prevents per-view visual drift, keeps P02-001 and P02-002 non-overlapping, avoids font packaging/licensing/runtime availability risk, and lets later dark/light themes change appearance without architectural replacement.

See `docs/design/DESIGN_TOKENS_AND_TYPOGRAPHY.md`.

---

## ADR-019 — Dark/light appearance uses one semantic brush contract

**Status:** Accepted  
**Date:** 2026-09-03

Dark and light appearance are separate WPF resource dictionaries with identical semantic keys. Views and controls consume `FccBrush*` resources via `DynamicResource`; raw palette colors remain backing values. The dark theme is the default application composition, while `ThemeService` performs runtime dictionary replacement without introducing settings persistence into P02.

A requested theme is loaded and identity-validated before the active theme is removed. Unsupported or failed switches preserve the existing theme. Relative `App.xaml` theme URIs and assembly-qualified WPF component URIs normalize to the same theme identity.

The semantic contract includes background/surface hierarchy, text hierarchy, borders/dividers, accent states, focus/selection/interaction states, and success/warning/error/info pairs. Deterministic validation enforces identical key sets and accessibility-oriented contrast thresholds in both themes.

**Reason:** Semantic brushes keep feature UI independent of raw palette values, make runtime appearance switching architectural rather than per-screen styling, and prevent dark/light drift while preserving the P02-001 geometry/typography boundary.

See `docs/design/SEMANTIC_THEMES.md`.

---
