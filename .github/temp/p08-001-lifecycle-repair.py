from pathlib import Path

PROCESS_PATH = Path("src/FCCCodeDesktop.Runtime/ProcessSupervisor.cs")
WORKFLOW_PATH = Path(".github/workflows/temp-p08-001-lifecycle-repair.yml")
SCRIPT_PATH = Path(".github/temp/p08-001-lifecycle-repair.py")

text = PROCESS_PATH.read_text(encoding="utf-8")
old = '''                _completion.TrySetResult(
                    new OwnedProcessExit(
                        OwnershipId,
                        RootProcessId,
                        rootExitCode,
                        StartedUtc,
                        DateTimeOffset.UtcNow,
                        Volatile.Read(ref _forcedTerminationRequested) != 0));
            }
            catch (Exception exception)
            {
                _completion.TrySetException(exception);
            }
            finally
            {
                _removeActive(OwnershipId);
            }
'''
new = '''                var exit = new OwnedProcessExit(
                    OwnershipId,
                    RootProcessId,
                    rootExitCode,
                    StartedUtc,
                    DateTimeOffset.UtcNow,
                    Volatile.Read(ref _forcedTerminationRequested) != 0);

                _removeActive(OwnershipId);
                _completion.TrySetResult(exit);
            }
            catch (Exception exception)
            {
                _removeActive(OwnershipId);
                _completion.TrySetException(exception);
            }
'''
count = text.count(old)
if count != 1:
    raise SystemExit(f"expected exactly one lifecycle block; found {count}")

PROCESS_PATH.write_text(text.replace(old, new), encoding="utf-8", newline="\n")
WORKFLOW_PATH.unlink()
SCRIPT_PATH.unlink()
