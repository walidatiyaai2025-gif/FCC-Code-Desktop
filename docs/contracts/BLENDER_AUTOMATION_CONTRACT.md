# Blender Automation Contract — P00 probe baseline

**Task:** `FCCD-P00-009`  
**Status:** reusable probe integrated and self-tested; target Blender execution blocked because Blender is not installed on this Windows machine.

The P00 probe discovers an explicit `--blender` path, `BLENDER_PATH`, `PATH`, and common Windows Program Files/LocalAppData installations. It observes `blender --version` without inferring a version from directory names.

Real mutation is confined to a generated disposable directory containing spaces, Arabic, and Unicode characters. Blender is launched with a structured argument array and `shell: false`:

```text
--background
--factory-startup
--python <fixture.py>
-- <structured fixture arguments>
```

The fixture creates a cube, material, camera, and light; saves a `.blend`; renders a small PNG; exports OBJ; and writes structured JSON. Success requires independently validated nonempty artifacts: Blender header for `.blend`, PNG signature, OBJ geometry, and structured JSON. Exit code zero alone is insufficient.

The negative lane invokes a missing Python script and requires nonzero exit. The cancellation lane launches an owned sleeping background process and terminates only its PID/tree. User projects and existing `.blend` files are never opened or modified.

Deterministic self-tests cover discovery failure, Unicode argument preservation, redaction, disposable-root containment, fixture content, `.blend`/PNG/OBJ positive and negative artifact validation, background/factory-startup flags, and the save/render/export script surface.

Machine-readable target-host evidence is written to `evidence/phases/P00/target/blender-contract.json`. On this host it truthfully records `BLOCKED_BLENDER_NOT_FOUND`; no synthetic artifact is represented as target evidence.

`FCCD-P00-009` remains `BLOCKED` until the same integrated probe passes on an actual supported Blender installation.
