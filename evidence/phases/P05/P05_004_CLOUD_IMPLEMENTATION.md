# FCCD-P05-004 — Cloud implementation evidence

**Task:** `FCCD-P05-004 — Session create/history/resume`  
**Phase:** P05  
**Evidence class:** `SELF_TEST_ONLY / CLOUD`  
**Status:** implementation candidate; canonical closure requires exact PR-head Windows CI, normal merge, exact post-merge main CI, and integrated reconciliation

## Implemented cloud scope

- production `SessionWorkspaceState` over the existing P03 `IConversationStateStore` contract;
- project-scoped durable session history and session creation;
- exact session resume with cross-project rejection;
- durable `RuntimeSessionId` binding seam without runtime/provider execution;
- serialized durable user/assistant message append;
- persisted message projection into the P05 conversation surface;
- session history/create/refresh/resume WPF surface with semantic theme resources;
- production LocalApplicationData SQLite bootstrap and explicit project-activation seam;
- composer persistence-before-presentation when a durable session is active;
- permanent static, negative/recovery and executable Windows/WPF + temporary SQLite validation;
- permanent canonical Windows CI registration and negative CI-policy enforcement.

## Safety and ownership

This evidence does not claim real FCC/provider execution, a real provider session resume, an owner-machine check, a P04 exit-gate result, or P05 phase closure. It does not synthesize a project at startup and does not implement P06 project-opening UX.

P05-005 retains ownership of explicit task lifecycle/runtime-dispatch state. P05-006 retains stop/cancel/retry UX. P05-007 and P05-008 retain content-rendering and performance/virtualization closure respectively.

The existing `OWNER-P04-008-REAL-TARGET` entry remains `QUEUED`, unresolved, genuine, and release-blocking.

## Required candidate validation

The implementation candidate must pass the canonical Windows CI baseline, including:

```powershell
.\tools\ui\validate-session-workspace.ps1 -RunFixtures -RequireRuntime
```

The executable fixture must prove create/history/runtime-ID binding/message persistence, state/store recreation, durable resume, cross-project fail-closed behavior, conversation restore, production WPF composition, and semantic dark/light behavior using a temporary local SQLite database only.

Exact candidate/PR/run/merge provenance is intentionally deferred to the integrated reconciliation artifact after those facts exist. No PASS is claimed in advance.
