# AGENTS.md — FCC Code Desktop Constitution

This file is binding for every AI agent, coding worker, reviewer, maintainer, automation, and future continuation working in this repository.

## 1. Supreme objective

Deliver **FCC Code Desktop v1.0.0 Production** as a complete, premium, reliable Windows desktop product for the user's local `fcc-claude` environment.

The owner is primarily a supervisor. AI workers are expected to research, decide, implement, verify, document, and converge the product autonomously.

The objective is not to maximize activity, commits, screenshots, or apparent percentage. The objective is an installable, operational, polished product whose exact release commit passes all mandatory acceptance gates.

---

## 2. Source-of-truth rule

The repository is the only durable project memory.

Do not rely on:

- previous chat history,
- an agent's memory,
- local scratch notes,
- old prompts,
- uncommitted plans,
- verbal assumptions.

Before doing material work, read at minimum:

1. `AGENTS.md`
2. `PROJECT_CONTROL.md`
3. `docs/PRODUCT_SPEC.md`
4. `docs/ARCHITECTURE.md`
5. `docs/UI_UX_STANDARD.md`
6. `docs/ENGINEERING_STANDARD.md`
7. `docs/RELEASE_POLICY.md`
8. `docs/ACCEPTANCE_MATRIX.md`
9. `docs/TASK_LEDGER.md`
10. `docs/DECISIONS.md`

If a material decision is made during implementation, write it to the repository before treating it as durable project knowledge.

---

## 3. Product doctrine

FCC Code Desktop is never to be treated as an MVP, prototype, demo, mock, experiment, or throwaway shell.

From the start, code and UX must be written as production software.

Required characteristics:

- Premium UI/UX
- Strong architecture
- Testability
- Reliability and recovery
- Security and secret hygiene
- Performance on real repositories
- Accessibility-aware interaction
- Professional setup and branding
- Versioned persistence and upgrade path
- Diagnostics that make failures actionable

A temporary shortcut is permitted only inside a clearly isolated development harness and must never leak into production code or release artifacts.

---

## 4. Autonomous decision rule

Do **not** ask the owner to choose routine technical or product-implementation details.

Examples that workers must resolve autonomously:

- package/library selection,
- class and project structure,
- layout implementation,
- retry algorithms,
- state-machine design,
- installer technology details,
- test organization,
- error-state copy,
- minor UX decisions,
- code style,
- internal naming,
- serialization formats,
- migration implementation.

Use this decision order:

1. Project constitution and product requirements
2. User safety and data integrity
3. Proven platform best practice
4. Maintainability and testability
5. Reliability and compatibility
6. Performance
7. Premium UX consistency
8. Minimal unnecessary complexity

Document consequential decisions in `docs/DECISIONS.md`.

Ask for user intervention only when progress genuinely requires something the AI cannot determine or obtain, such as credentials, account permissions, paid-service authorization, legal ownership approval, external hardware access, or an irreversible business decision with materially different outcomes.

---

## 5. No-surprise engineering rule

Before implementing a dependency-sensitive subsystem, verify the real external contract first.

Examples:

- Verify actual `fcc-claude` behavior before building the runtime adapter.
- Verify Claude/FCC streaming and session semantics before freezing persistence contracts.
- Verify ConPTY behavior before building the terminal UI around assumptions.
- Verify installer upgrade/uninstall behavior before release packaging is considered complete.

Prefer contract tests and compatibility probes over undocumented assumptions.

External integrations must be behind project-owned interfaces so upstream changes do not force UI or domain rewrites.

---

## 6. No partial public release

Development builds are allowed. Public incomplete versions are not.

Do not create or present a release/tag/installer as a finished product while mandatory v1 scope is incomplete.

Forbidden as finished releases:

- `v0.x` presented to the owner as the product,
- `beta` or `preview` standing in for the requested complete product,
- installer that opens a partially implemented shell,
- release with placeholder branding,
- release with known broken primary workflow,
- release whose clean-machine setup was not validated,
- release whose exact commit was not tested.

The first public product release target is `v1.0.0`.

---

## 7. Exact-head verification

Never declare a feature or release verified using evidence from a different commit than the candidate being declared.

For release closure:

1. Freeze candidate commit SHA.
2. Build that exact SHA.
3. Run all required automated checks on that exact SHA.
4. Produce installer from that exact SHA.
5. Run clean-machine / acceptance checks against that installer.
6. Record evidence.
7. Only then create the final release/tag.

Any code change after verification invalidates affected verification and requires re-run.

---

## 8. Definition of complete

A task is complete only when all of the following are true:

- Implementation exists.
- Relevant tests exist or are updated.
- Tests pass.
- Error paths are handled.
- UI states are complete when applicable.
- Accessibility/keyboard behavior is considered when applicable.
- Logging/diagnostics are adequate.
- Documentation/state ledger is updated.
- No known regression is left behind.

"Code written" is not completion.

---

## 9. UI/UX enforcement

Do not ship default-looking WPF or installer UI.

Every visible surface must account for:

- information hierarchy,
- alignment and spacing,
- typography,
- focus states,
- hover/pressed/disabled states,
- loading states,
- empty states,
- errors,
- offline/runtime unavailable,
- blocked permission,
- rate limit,
- task cancellation,
- crash recovery,
- long text,
- narrow window,
- DPI scaling,
- dark/light theme.

Placeholder visuals, emoji used as final product icons, inconsistent icon families, arbitrary colors, default message boxes for core workflows, and debug text are release blockers.

See `docs/UI_UX_STANDARD.md`.

---

## 10. Branding and AI-generated assets

All product artwork should be produced or commissioned through an AI-assisted workflow where feasible, while maintaining professional visual quality and provenance.

The release must include an original professional application icon suitable for:

- Windows executable,
- installer,
- Start menu,
- taskbar,
- About screen,
- repository/release presentation.

Do not copy protected logos, Claude branding, Anthropic logos, or third-party product identity as FCC Code Desktop's identity.

Record asset origin/provenance and license status before release.

---

## 11. Runtime architecture rule

The UI/domain must never directly depend on brittle FCC/Claude implementation details.

Use a project-owned runtime abstraction, conceptually:

```text
IAgentRuntime
  ├── primary FCC/Claude adapter
  └── CLI/compatibility fallback adapter
```

Runtime state must be explicit and observable.

No infinite ambiguous `Working...` state.

Long-running tasks require lifecycle tracking, cancellation, failure classification, and recovery behavior.

---

## 12. Serial execution invariant

Default global coding-agent concurrency is **1**.

One active agent run may execute at a time unless the architecture is explicitly changed through an ADR after evidence proves safe parallel operation.

Default inter-run cooldown: **15 seconds**.

Queued conversations/tasks must remain queued until the previous active run is terminal and the cooldown has elapsed.

Rate-limit handling must stop new work from being launched into an already throttled provider.

This is a product invariant, not a cosmetic preference.

---

## 13. Data and destructive-operation safety

Never silently execute destructive user-workspace operations.

High-risk operations require explicit product-level safeguards, including where applicable:

- `git reset --hard`
- `git clean -fd`
- force push
- branch deletion
- destructive checkout
- bulk deletion
- history rewrite
- replacing local user data

Do not erase dirty working-tree changes just to make tests or checkout easier.

Use checkpoints and Git state awareness.

---

## 14. Secrets and privacy

Never log or export plaintext secrets.

At minimum redact:

- API keys
- bearer tokens
- authorization headers
- provider credentials
- FCC secrets
- environment secrets

Diagnostics bundles must be sanitized before export.

The application is local-first and must not introduce telemetry, tracking, cloud accounts, or remote data storage by default.

---

## 15. Multi-worker coordination

If multiple AI workers are active, duplicate work is forbidden.

Before claiming a task:

1. Fetch live `main`.
2. Read `docs/TASK_LEDGER.md`.
3. Inspect open PRs/branches/issues as relevant.
4. Build a claim map.
5. Select one unclaimed legitimate task.
6. Record the claim or use a dedicated branch/PR that clearly identifies ownership.

Workers must not independently implement the same subsystem without an explicit reconciliation reason.

Recommended branch format:

```text
worker/<task-id>-<short-description>
```

One focused task should produce one focused branch/PR unless the canonical ledger explicitly defines a convergence branch.

---

## 16. Continuation after interruption

When resuming after any interruption:

1. Fetch live repository state.
2. Read canonical documents.
3. Inspect recent commits, open PRs and current task ledger.
4. Reconcile what actually landed versus what was only planned.
5. Continue the next legitimate incomplete item.
6. Never restart already verified work merely because previous chat context is missing.

The repository must contain enough information to perform these steps without the old conversation.

---

## 17. Task ledger discipline

`docs/TASK_LEDGER.md` is the canonical inventory.

Allowed states:

- `PENDING`
- `CLAIMED`
- `IN_PROGRESS`
- `BLOCKED`
- `IMPLEMENTED`
- `VERIFIED`
- `CLOSED`

Do not mark `CLOSED` without verification evidence.

If new required work is discovered, add it to the ledger rather than hiding it inside a comment or chat.

---

## 18. Quality cannot be traded for apparent progress

Never:

- remove tests to get green CI,
- weaken assertions to hide a defect,
- disable analyzer rules without rationale,
- suppress exceptions globally,
- convert runtime failures into fake success,
- mark acceptance rows PASS without evidence,
- reduce v1 scope silently,
- claim an estimated completion percentage as verified completion.

If evidence reveals more work, update the ledger and continue.

---

## 19. Release closure authority

Only `docs/RELEASE_POLICY.md` and `docs/ACCEPTANCE_MATRIX.md` define whether a release is eligible.

Final status string is reserved:

```text
VERIFIED_FINAL_COMPLETE
```

It may be used only when there is zero legitimate required `PENDING`, `CLAIMED`, `IN_PROGRESS`, `BLOCKED`, or merely `IMPLEMENTED` release work remaining, all mandatory gates are verified on the exact release head, and the production installer has passed clean-machine acceptance.
