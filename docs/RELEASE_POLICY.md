# FCC Code Desktop — Release Policy

## 1. First public release policy

The first public product release is **v1.0.0 Production**.

Internal development builds may exist at any time, but they must be labeled clearly as internal/non-release artifacts and must not be presented to the owner as a usable finished version.

There is no acceptance path based on "good enough for now".

---

## 2. Release identity

Required production artifacts include at minimum:

```text
FCCCodeDesktop-Setup-1.0.0.exe
checksums.txt
release-manifest.json
```

The release manifest must record:

- product version,
- exact Git commit SHA,
- build timestamp,
- target architecture,
- installer hash,
- application executable hash,
- dependency/runtime manifest reference,
- FCC/Claude compatibility evidence version,
- Unity contract evidence version,
- Blender contract evidence version,
- acceptance evidence reference.

---

## 3. Exact-head rule

A release candidate is identified by one exact Git SHA.

All release evidence must apply to that SHA. Any source/config/packaging change after verification invalidates the relevant evidence and requires re-verification.

Do not tag first and hope CI passes later.

---

## 4. Required release gates

The release candidate must have PASS evidence for:

1. Release build
2. Static analysis/quality checks
3. Unit tests
4. Integration tests
5. Runtime contract tests
6. FCC unavailable/recovery scenarios
7. Sessions/resume
8. Queue/cooldown/rate-limit behavior
9. Files/editor/search
10. Diff/Git safety
11. Terminal/process supervision
12. Unity adapter contracts
13. Blender adapter contracts
14. Unity↔Blender workflow acceptance
15. Crash/restart/reboot recovery
16. SQLite migration/backup/recovery
17. Security/redaction
18. Performance baselines
19. UI automation
20. Accessibility/keyboard checks
21. Visual acceptance at required resolutions/scales/themes
22. Installer install/launch
23. Repair/upgrade path
24. Uninstall + data retention choice
25. Clean-machine acceptance
26. Artifact provenance/licensing
27. Diagnostic bundle sanitization

The detailed matrix lives in `docs/ACCEPTANCE_MATRIX.md`.

---

## 5. Installer requirement

The setup executable is part of product acceptance, not a post-release packaging task.

It must be branded and production-quality from v1.0.0:

- professional original icon,
- coherent visual identity,
- version metadata,
- sensible installation defaults,
- Start menu integration,
- uninstall entry,
- upgrade detection,
- repair behavior if selected by installer architecture,
- actionable error UI,
- no placeholder artwork,
- no raw MSI/WiX developer surfaces exposed unnecessarily,
- no requirement for Visual Studio or source checkout.

Signing support should be architected in; if a trusted code-signing certificate is unavailable at release time, that external constraint must be explicit in release evidence rather than hidden.

---

## 6. Clean-machine definition

A clean-machine acceptance environment must not rely on the developer workstation's SDK/tool residue.

At minimum verify the desktop product can install/launch without:

- Visual Studio,
- .NET SDK,
- repository source checkout,
- Node installation merely for the app,
- Python installation merely for the app.

External project tools such as FCC, Git, Unity or Blender may be required only for the features that use them. Their absence must degrade those capabilities clearly without breaking unrelated application functionality.

A second scenario must validate the intended primary environment with working FCC/`fcc-claude` and Git. Unity/Blender acceptance scenarios use machines/runners with their supported tool versions installed.

---

## 7. Upgrade policy

Starting at v1.0.0:

- user projects/history/settings survive in-place upgrades,
- database migrations are forward-only and tested,
- migration failure preserves recoverable backup,
- installer detects existing installation,
- running app/process conflicts are handled explicitly,
- a failed upgrade must not silently destroy previous user data.

---

## 8. Uninstall policy

Default uninstall removes application binaries but preserves local user project metadata/session history unless user explicitly chooses to remove product data.

Uninstall must not delete source repositories, Unity projects, Blender assets or unrelated FCC configuration.

---

## 9. Branding/provenance gate

Before release, every bundled visual/third-party artifact must have known origin/license status.

AI-generated visual identity must be original and must not imitate third-party protected product identity closely enough to create confusion.

No placeholder assets allowed.

---

## 10. Known defects policy

A known defect blocks v1.0.0 when it affects:

- primary agent workflow,
- data integrity,
- destructive-operation safety,
- crash/recovery correctness,
- installer/upgrade,
- queue/rate-limit safety,
- secret/privacy handling,
- Unity or Blender mandatory acceptance,
- supported-resolution usability,
- any mandatory acceptance row.

Low-severity cosmetic defects may be waived only by a documented release decision that does not contradict `docs/UI_UX_STANDARD.md` and does not make the product visibly unfinished.

---

## 11. Release sequence

```text
freeze candidate SHA
→ run automated suite
→ run FCC/Unity/Blender contract suites
→ build production installer from exact SHA
→ run installer/upgrade/uninstall tests
→ run clean-machine acceptance
→ run final visual/accessibility review
→ verify provenance/checksums/diagnostics
→ reconcile TASK_LEDGER: zero mandatory unresolved work
→ tag v1.0.0
→ publish release
```

---

## 12. Forbidden release shortcuts

Do not:

- publish installer just to let the owner discover missing features,
- call a development snapshot `v1.0.0`,
- accept failing tests as known issues,
- hide missing Unity/Blender integration behind "coming soon",
- skip clean-machine testing because it works on the build machine,
- rebuild after verification without rerunning applicable gates,
- manually patch release binaries after build,
- mark failed/untested acceptance items PASS.

---

## 13. Final status

Only after all gates pass and the ledger contains no legitimate unresolved mandatory work may project status be set to:

```text
VERIFIED_FINAL_COMPLETE
```
