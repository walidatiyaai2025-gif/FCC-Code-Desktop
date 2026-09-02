# Blender Automation Contract — P00 probe baseline

**Task:** `FCCD-P00-009`  
**Status:** reusable probe integrated and cloud-hardened; target Blender execution remains required because canonical target evidence currently records Blender as not installed/discoverable.

The P00 probe discovers an explicit `--blender` path, `BLENDER_PATH`, `PATH`, and common Windows Program Files/LocalAppData installations. Discovery precedence is explicit path, environment, PATH, then standard locations. The executable version is observed only by invoking `blender --version`; directory names are never treated as version evidence. A present executable whose version cannot be observed is not a successful discovery contract.

Real mutation is confined to a newly generated disposable child directory containing spaces, Arabic, and Unicode characters. Supplying `--fixture-root` changes only the parent under which that owned child is created. The probe never opens a pre-existing `.blend`. Blender is launched with a structured argument array and `shell: false`:

```text
--background
--factory-startup
--python <fixture.py>
-- <structured fixture arguments>
```

The fixture creates a cube, material, camera, and light; saves a `.blend`; renders a small PNG; exports OBJ; and writes structured JSON. Positive success requires independent validation of every required output:

- `.blend` begins with a structurally valid 12-byte Blender file header (`BLENDER`, pointer-size marker, endianness marker, three version digits);
- PNG contains the complete eight-byte PNG signature and is nonempty;
- OBJ contains at least three vertex records and a face record, rather than merely an object name or arbitrary text;
- structured JSON parses, reports `success: true`, contains Blender/object/output fields, and matches the generated scene/render/export paths;
- the positive background process exits zero.

Exit code zero alone is insufficient.

The negative lane invokes a deliberately missing Python script. Only a real integer nonzero process exit counts as the expected controlled failure; spawn failure/null exit does not. Artifact validators also have deterministic malformed/missing negative coverage.

The cancellation lane launches an owned sleeping background process and terminates only the process identity it created. On Windows the probe uses `taskkill.exe /PID <owned-pid> /T /F`; on non-Windows self-test hosts it uses the owned detached process group. There is no kill-by-name path. The cancellation result must additionally verify that the owned root PID is gone, and deterministic self-tests prove an unrelated process remains alive.

Persisted probe objects, stdout/stderr, errors, and strings are passed through recursive redaction. Deterministic tests cover secret-shaped object keys, Bearer/Authorization values, and secret assignments embedded in path/log/error strings.

Deterministic self-tests cover discovery failure, exact spaces/Arabic/Unicode argument preservation across every structured fixture argument, disposable-root containment, fixture content, `.blend`/PNG/OBJ positive and malformed negative validation, structured JSON positive/malformed/incomplete cases, strict nonzero failure classification, cancellation cleanup verification/unrelated-process preservation, no kill-by-name, and redaction mechanics.

Machine-readable target-host evidence remains `evidence/phases/P00/target/blender-contract.json`. The currently integrated target evidence records `BLOCKED_BLENDER_NOT_FOUND` / `TARGET_UNVERIFIED`; no synthetic artifact is represented as target success.

The authoritative local Windows rerun remains one command:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\contract-probes\run-target-validation.ps1
```

`FCCD-P00-009` remains open/blocked until that current integrated probe passes with a real Blender executable on the authoritative Windows target and the resulting sanitized evidence is reconciled by the convergence lane.
