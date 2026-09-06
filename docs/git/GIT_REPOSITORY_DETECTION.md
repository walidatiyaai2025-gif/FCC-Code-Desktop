# P07 Git Repository Detection Contract

`FCCD-P07-001` establishes the read-only Git boundary used by the later P07 change-review operations.

## Ownership

- `FCCCodeDesktop.Application.Git.IGitService` owns the application-facing repository-detection contract and result model.
- `FCCCodeDesktop.Git.GitCliService` owns the concrete Git CLI probe.
- This task does **not** own status/changed-files, diffs, stage/unstage, branch mutation, fetch/pull, commit/push, history, or destructive Git operations. Those remain in later P07 tasks.

## Detection behavior

`DetectRepositoryAsync` accepts an existing directory, normalizes the probe path, and executes bounded read-only `git rev-parse` probes with shell execution disabled. It distinguishes:

- normal work trees, including calls made from nested directories;
- bare repositories;
- ordinary directories that are not Git repositories;
- Git executable unavailability;
- probe failures that must not be misreported as "not a repository".

Repository results expose the normalized probe path, repository root, Git directory, and repository kind.

## Safety boundary

Repository detection is intentionally side-effect free:

- only `rev-parse` is invoked;
- `UseShellExecute=false` and `ArgumentList` are used instead of constructing shell command strings;
- `GIT_TERMINAL_PROMPT=0` prevents interactive credential prompting;
- `GIT_OPTIONAL_LOCKS=0` prevents optional repository lock creation during the read-only probe;
- `safe.directory` is never overridden, so Git ownership protections remain authoritative;
- no hooks are invoked and no repository configuration is changed;
- no add, checkout, reset, clean, stage, commit, fetch, pull, push, or branch mutation belongs to this task;
- a five-second default probe timeout is enforced, with a hard configurable maximum of thirty seconds;
- caller cancellation terminates the owned Git process tree and propagates cancellation.

The service does not surface raw Git stderr in its public result model, avoiding accidental propagation of environment-specific or sensitive diagnostic text.

## Automated coverage

The permanent unit-test baseline covers:

- ordinary non-repository directories;
- nested work-tree detection through paths containing spaces and Arabic text;
- bare repository detection;
- no filesystem mutation during a work-tree probe;
- missing Git executable classification;
- missing-directory failure;
- caller cancellation;
- probe-timeout configuration bounds.

The tests initialize disposable repositories only inside test-owned temporary directories. They do not modify the repository under test or the user's working tree.
