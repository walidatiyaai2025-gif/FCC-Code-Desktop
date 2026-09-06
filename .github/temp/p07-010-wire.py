from pathlib import Path
import subprocess

EXPECTED_BASE = "ef311e6821e990eb11f83f1049fbcb5f8b567237"
BRANCH = "worker-b/fccd-p07-010-destructive-operation-safeguards"
SERVICES = [
    "src/FCCCodeDesktop.Git/GitCliIndexService.cs",
    "src/FCCCodeDesktop.Git/GitCliBranchService.cs",
    "src/FCCCodeDesktop.Git/GitCliRemoteService.cs",
    "src/FCCCodeDesktop.Git/GitCliCommitPushService.cs",
]
TEMP_FILES = [
    ".github/temp/p07-010-wire.py",
    ".github/workflows/temp-p07-010-wire.yml",
]
NEEDLE = "    {\n        var startInfo = new ProcessStartInfo(_gitExecutable)\n"
REPLACEMENT = "    {\n        GitCommandSafetyPolicy.EnsureAllowed(arguments);\n\n        var startInfo = new ProcessStartInfo(_gitExecutable)\n"


def run(*args: str) -> str:
    return subprocess.check_output(args, text=True).strip()


parent = run("git", "rev-parse", "HEAD^")
if parent != EXPECTED_BASE:
    raise SystemExit(f"Unexpected patch parent {parent}; expected {EXPECTED_BASE}.")

for service in SERVICES:
    path = Path(service)
    text = path.read_text(encoding="utf-8")
    count = text.count(NEEDLE)
    if count != 1:
        raise SystemExit(f"Guarded insertion expected exactly one process boundary in {service}; found {count}.")
    path.write_text(text.replace(NEEDLE, REPLACEMENT, 1), encoding="utf-8")

for temporary in TEMP_FILES:
    path = Path(temporary)
    if not path.exists():
        raise SystemExit(f"Expected temporary helper is missing: {temporary}")
    path.unlink()

changed = set(run("git", "diff", "--name-only").splitlines())
expected = set(SERVICES + TEMP_FILES)
if changed != expected:
    raise SystemExit(f"Unexpected wiring diff. expected={sorted(expected)} actual={sorted(changed)}")

subprocess.check_call(["git", "config", "user.name", "github-actions[bot]"])
subprocess.check_call(["git", "config", "user.email", "41898282+github-actions[bot]@users.noreply.github.com"])
subprocess.check_call(["git", "add", "-A"])
subprocess.check_call(["git", "commit", "-m", "P07-010: wire fail-closed destructive Git safeguards"])
subprocess.check_call(["git", "push", "origin", f"HEAD:{BRANCH}"])
