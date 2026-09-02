# Blender Automation Contract — P00 probe baseline

**Task:** `FCCD-P00-009`  
**Status:** CLOSED — authoritative Windows Blender 5.2.0 target contract passed and sanitized evidence is integrated.

The P00 probe discovers Blender using an explicit executable path when supplied; otherwise it may use `BLENDER_PATH`, `PATH`, and common Windows Program Files/LocalAppData installations. An explicit path is authoritative and does not silently fall through to another Blender installation when that explicit target is missing. The executable version is observed only by invoking Blender; directory names are never treated as version evidence.

Real mutation is confined to a newly generated disposable child directory containing spaces, Arabic, and Unicode characters. Supplying `--fixture-root` changes only the parent under which that owned child is created. The probe never opens or overwrites a pre-existing `.blend`.

Blender is launched with structured argument arrays and `shell: false`. The positive fixture uses background/factory-startup execution and a repository-owned Python automation script. The fixture creates a cube, material, camera, and light; saves an uncompressed `.blend`, renders a small PNG, exports OBJ, and writes structured JSON.

Positive success requires independent validation of every required output:

- `.blend` begins with either the validated legacy 12-byte Blender header or the validated modern Blender 5.2 17-byte header;
- PNG contains the complete eight-byte PNG signature and is nonempty;
- OBJ contains at least three vertex records and a face record rather than merely an object name or arbitrary text;
- structured JSON parses, reports `success: true`, contains Blender/object/output fields, and matches the generated scene/render/export paths;
- the positive background process exits zero.

Exit code zero alone is insufficient.

The negative lane invokes a deliberately missing Python script with an explicit `--python-exit-code 17`, so Blender/Python failure produces a real integer nonzero process exit. Spawn failure or a null exit does not count as the expected controlled failure. Artifact validators also have deterministic malformed/missing negative coverage.

The cancellation lane launches only an owned Blender process and terminates only the process identity/tree it created. There is no kill-by-name path. Cancellation success additionally requires verified root-process exit and cleanup, while deterministic tests prove an unrelated process remains alive.

Persisted probe objects, stdout/stderr, errors, and strings are recursively redacted. Deterministic coverage includes secret-shaped object keys, Bearer/Authorization values, and secret assignments embedded in path/log/error strings.

Deterministic self-tests pass 29/29 and cover discovery semantics, exact spaces/Arabic/Unicode argument preservation, disposable-root containment, legacy and modern Blender headers, fixture content, `.blend`/PNG/OBJ positive and malformed negative validation, structured JSON validation, explicit Python nonzero-exit handling, owned cancellation cleanup/unrelated-process preservation, no kill-by-name, and redaction mechanics.

Machine-readable authoritative evidence: `evidence/phases/P00/target/blender-contract.json`.

The integrated authoritative evidence records `overallStatus = PASS`, `evidenceState = VERIFIED_ON_AVAILABLE_BLENDER_HOST`, and Blender version `5.2.0`.

The authoritative Windows run passed real discovery/version, background execution, Python automation, `.blend` save validation, PNG render validation, OBJ export validation, controlled Python failure, cancellation, and cleanup.

Evidence-producing source SHA: `e6932783b30ab0bdbb596c7959e03143753bff9a`.

The sanitized target evidence was published and merged through PR #40.

`FCCD-P00-009` is therefore reconciled `CLOSED`. This is a P00 tested compatibility result for Blender `5.2.0`; it does not by itself declare a broader Blender support range.
