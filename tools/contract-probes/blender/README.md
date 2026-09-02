# FCCD-P00-009 Blender contract probe

This P00-only probe discovers Blender and validates real background Python automation with an owned disposable Unicode-path fixture. It creates a cube/material/camera/light, saves a `.blend`, renders a PNG, exports OBJ, validates every artifact independently of exit code, observes a controlled Python failure, and tests owned-process cancellation.

Discovery precedence is explicit `--blender`, `BLENDER_PATH`, `PATH`, then common Windows `Program Files` / `LocalAppData\Programs` locations. A directory name never establishes the Blender version: a discovered executable must return a parseable version from `blender --version`.

Run deterministic self-tests:

```powershell
node tools/contract-probes/blender/self-test.mjs
```

Run the Blender lane directly for diagnosis:

```powershell
node tools/contract-probes/blender/probe.mjs --mode all --json evidence/phases/P00/target/blender-contract.json
```

The authoritative target-machine entry point remains the unified one-command runner:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\contract-probes\run-target-validation.ps1
```

Use `--blender <path>` for a nonstandard install when invoking the Blender lane directly. All Blender mutation occurs below a newly generated disposable child directory, including when `--fixture-root` is supplied. `--keep-fixture` is diagnostic opt-in. The probe never opens or modifies a user `.blend` file and never kills processes by name.

A real PASS requires all of the following: zero exit from the positive background run, a structured JSON result matching the expected generated paths, a structurally valid Blender header, the complete PNG magic signature, OBJ vertices plus a face, a deterministic missing-script nonzero failure, and cancellation with explicit owned root-PID exit verification. Exit code zero alone is never sufficient.
