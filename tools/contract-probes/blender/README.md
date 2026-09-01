# FCCD-P00-009 Blender contract probe

This P00-only probe discovers Blender and validates real background Python automation with an owned disposable Unicode-path fixture. It creates a cube/material/camera/light, saves a `.blend`, renders a PNG, exports OBJ, validates every artifact independently of exit code, observes a controlled Python failure, and tests owned-process cancellation.

Run deterministic self-tests:

```powershell
node tools/contract-probes/blender/self-test.mjs
```

Run target validation:

```powershell
node tools/contract-probes/blender/probe.mjs --mode all --json evidence/phases/P00/target/blender-contract.json
```

Use `--blender <path>` for a nonstandard install. All mutations occur below an owned temporary fixture unless `--fixture-root` is supplied. `--keep-fixture` is diagnostic opt-in. The probe never modifies a user `.blend` file and never kills processes by name.
