# Project technology detection

`FCCD-P06-002 — Project technology/tool detection framework` adds a read-only, bounded project-marker scan that runs after a project is opened and can be manually rescanned from the Projects surface.

## Architectural boundary

- The Application layer owns `IProjectTechnologyDetectionService` and the result contracts.
- `FCCCodeDesktop.Files` owns the concrete file-system scan.
- `FCCCodeDesktop.App` consumes only the Application contract and displays the detections.
- The detector never starts a process, invokes a toolchain, probes PATH, writes source files, changes Git state, or persists project source content.
- Installed-tool discovery remains the responsibility of later external-tool phases; P06-002 only infers the expected toolchain from project markers.

## Bounded traversal

Default limits are:

- maximum depth: `3`;
- maximum file-system entries examined: `4096`.

Both limits are validated and the scan reports whether the entry cap was reached. Traversal is performed off the WPF UI thread and accepts a cancellation token.

The scanner skips common generated/high-volume directories and reparse points, including `.git`, `.hg`, `.svn`, `.idea`, `.vs`, `.vscode`, `bin`, `obj`, `build`, `dist`, `node_modules`, Unity `Library`/`Temp`/`Logs`, Rust `target`, PHP `vendor`, and Python `__pycache__`.

Unreadable child paths are skipped and counted rather than crashing the whole scan. A missing root is an explicit error.

## Marker rules

The current framework recognizes these project technologies/toolchain expectations:

| Technology | Representative markers | Toolchain label |
|---|---|---|
| .NET | `.sln`, `.slnx`, `.csproj`, `.fsproj`, `.vbproj` | .NET SDK |
| Node.js | `package.json`, `pnpm-workspace.yaml` | Node.js package tooling |
| Python | `pyproject.toml`, `requirements.txt`, `setup.py`, `Pipfile` | Python |
| Unity | `ProjectSettings/ProjectVersion.txt` | Unity Editor |
| Blender | `*.blend` | Blender |
| Java/JVM | `pom.xml`, `build.gradle`, `build.gradle.kts` | JDK build tooling |
| Rust | `Cargo.toml` | Rust toolchain |
| Go | `go.mod` | Go toolchain |
| PHP | `composer.json` | PHP / Composer |
| C/C++ | `CMakeLists.txt`, `*.vcxproj` | CMake / C++ toolchain |

Multiple technologies may be returned for a monorepo. Duplicate markers for the same technology collapse deterministically to the strongest/lexicographically earliest evidence marker.

## User experience

Opening a project automatically performs the bounded marker scan. The Projects workspace displays:

- detected technology names;
- expected toolchain labels;
- the scan entry count, depth cap, entry cap, skipped-path count, and whether the entry cap was reached;
- a `Rescan markers` action for source trees that changed after opening.

A technology scan is advisory project metadata. It does not change project identity, session identity, source files, Git state, or tool execution state.

## Validation

Permanent validation is `tools/projects/validate-project-technology-detection.ps1` and the dedicated Windows CI step `Validate P06-002 project technology detection`.

The executable integration suite covers mixed technologies, Unicode/space-containing roots, generated-directory exclusion, source non-mutation, bounded entry limits, representative additional toolchains, missing roots, cancellation, and rejection of unbounded configuration.
