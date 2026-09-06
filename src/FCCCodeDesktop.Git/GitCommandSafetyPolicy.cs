namespace FCCCodeDesktop.Git;

public enum GitCommandSafetyStatus
{
    Allowed = 0,
    Blocked = 1,
}

public sealed record GitCommandSafetyDecision(
    GitCommandSafetyStatus Status,
    string Rule)
{
    public bool IsAllowed => Status == GitCommandSafetyStatus.Allowed;
}

/// <summary>
/// Fail-closed policy for Git command shapes reachable from mutation adapters. The policy
/// deliberately permits only the bounded command forms owned by P07 and rejects destructive
/// work-tree/ref/history rewrites before a Git process is started.
/// </summary>
public static class GitCommandSafetyPolicy
{
    private const string AllowedCommitConfig = "commit.gpgSign=false";
    private const string LiteralPathspecPrefix = ":(literal)";

    public static GitCommandSafetyDecision Evaluate(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Count == 0)
        {
            return Blocked("empty-command");
        }

        var commandIndex = 0;
        while (commandIndex < arguments.Count && string.Equals(arguments[commandIndex], "-c", StringComparison.Ordinal))
        {
            if (commandIndex + 1 >= arguments.Count
                || !string.Equals(arguments[commandIndex + 1], AllowedCommitConfig, StringComparison.Ordinal))
            {
                return Blocked("unsupported-global-config");
            }

            commandIndex += 2;
        }

        if (commandIndex >= arguments.Count)
        {
            return Blocked("missing-command");
        }

        var command = arguments[commandIndex];
        var commandArguments = arguments.Skip(commandIndex + 1).ToArray();
        return command switch
        {
            "check-ref-format" => Allowed("read-only-ref-validation"),
            "show-ref" => Allowed("read-only-ref-query"),
            "rev-parse" => Allowed("read-only-revision-query"),
            "diff" => Allowed("read-only-diff-query"),
            "var" => Allowed("read-only-variable-query"),
            "merge-base" => Allowed("read-only-ancestry-query"),
            "symbolic-ref" => EvaluateSymbolicRef(commandArguments),
            "remote" => EvaluateRemote(commandArguments),
            "add" => EvaluateAdd(commandArguments),
            "restore" => EvaluateRestore(commandArguments),
            "rm" => EvaluateRm(commandArguments),
            "switch" => EvaluateSwitch(commandArguments),
            "fetch" => EvaluateFetch(commandArguments),
            "merge" => EvaluateMerge(commandArguments),
            "commit" => EvaluateCommit(commandArguments),
            "push" => EvaluatePush(commandArguments),
            _ => Blocked("unsupported-or-destructive-command"),
        };
    }

    public static void EnsureAllowed(IReadOnlyList<string> arguments)
    {
        var decision = Evaluate(arguments);
        if (!decision.IsAllowed)
        {
            throw new InvalidOperationException(
                $"Git command blocked by destructive-operation safety policy ({decision.Rule}).");
        }
    }

    private static GitCommandSafetyDecision EvaluateSymbolicRef(IReadOnlyList<string> arguments) =>
        arguments.Count == 3
        && string.Equals(arguments[0], "--quiet", StringComparison.Ordinal)
        && string.Equals(arguments[1], "--short", StringComparison.Ordinal)
        && string.Equals(arguments[2], "HEAD", StringComparison.Ordinal)
            ? Allowed("read-only-current-branch-query")
            : Blocked("symbolic-ref-mutation-or-unsupported-shape");

    private static GitCommandSafetyDecision EvaluateRemote(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 2
            && string.Equals(arguments[0], "get-url", StringComparison.Ordinal)
            && IsSafeAtom(arguments[1]))
        {
            return Allowed("read-only-remote-url-query");
        }

        if (arguments.Count == 3
            && string.Equals(arguments[0], "get-url", StringComparison.Ordinal)
            && string.Equals(arguments[1], "--all", StringComparison.Ordinal)
            && IsSafeAtom(arguments[2]))
        {
            return Allowed("read-only-remote-url-query");
        }

        return Blocked("remote-mutation-or-unsupported-shape");
    }

    private static GitCommandSafetyDecision EvaluateAdd(IReadOnlyList<string> arguments) =>
        arguments.Count >= 2
        && string.Equals(arguments[0], "--", StringComparison.Ordinal)
        && AllLiteralPathspecs(arguments, 1)
            ? Allowed("explicit-index-stage")
            : Blocked("broad-or-unsupported-index-stage");

    private static GitCommandSafetyDecision EvaluateRestore(IReadOnlyList<string> arguments) =>
        arguments.Count >= 3
        && string.Equals(arguments[0], "--staged", StringComparison.Ordinal)
        && string.Equals(arguments[1], "--", StringComparison.Ordinal)
        && AllLiteralPathspecs(arguments, 2)
            ? Allowed("index-only-restore")
            : Blocked("worktree-restore-or-unsupported-shape");

    private static GitCommandSafetyDecision EvaluateRm(IReadOnlyList<string> arguments) =>
        arguments.Count >= 5
        && string.Equals(arguments[0], "--cached", StringComparison.Ordinal)
        && string.Equals(arguments[1], "--force", StringComparison.Ordinal)
        && string.Equals(arguments[2], "--ignore-unmatch", StringComparison.Ordinal)
        && string.Equals(arguments[3], "--", StringComparison.Ordinal)
        && AllLiteralPathspecs(arguments, 4)
            ? Allowed("unborn-index-only-remove")
            : Blocked("worktree-remove-or-unsupported-shape");

    private static GitCommandSafetyDecision EvaluateSwitch(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 1 && IsSafeAtom(arguments[0]))
        {
            return Allowed("safe-existing-branch-switch");
        }

        if (arguments.Count == 2
            && string.Equals(arguments[0], "--create", StringComparison.Ordinal)
            && IsSafeAtom(arguments[1]))
        {
            return Allowed("safe-branch-create-switch");
        }

        return Blocked("forced-or-discarding-branch-switch");
    }

    private static GitCommandSafetyDecision EvaluateFetch(IReadOnlyList<string> arguments)
    {
        if (arguments.Count is not (3 or 4)
            || !string.Equals(arguments[0], "--no-tags", StringComparison.Ordinal)
            || !string.Equals(arguments[1], "--no-recurse-submodules", StringComparison.Ordinal)
            || !IsSafeFetchAtom(arguments[2]))
        {
            return Blocked("forced-pruning-or-unsupported-fetch");
        }

        if (arguments.Count == 4 && !IsSafeFetchAtom(arguments[3]))
        {
            return Blocked("unsafe-fetch-refspec");
        }

        return Allowed("bounded-fetch");
    }

    private static GitCommandSafetyDecision EvaluateMerge(IReadOnlyList<string> arguments) =>
        arguments.Count == 3
        && string.Equals(arguments[0], "--ff-only", StringComparison.Ordinal)
        && string.Equals(arguments[1], "--no-edit", StringComparison.Ordinal)
        && string.Equals(arguments[2], "FETCH_HEAD", StringComparison.Ordinal)
            ? Allowed("fast-forward-only-fetch-head-merge")
            : Blocked("non-fast-forward-or-unsupported-merge");

    private static GitCommandSafetyDecision EvaluateCommit(IReadOnlyList<string> arguments) =>
        arguments.Count == 5
        && string.Equals(arguments[0], "--no-verify", StringComparison.Ordinal)
        && string.Equals(arguments[1], "--no-gpg-sign", StringComparison.Ordinal)
        && string.Equals(arguments[2], "--cleanup=verbatim", StringComparison.Ordinal)
        && string.Equals(arguments[3], "--message", StringComparison.Ordinal)
            ? Allowed("new-staged-index-commit")
            : Blocked("history-rewrite-or-unsupported-commit");

    private static GitCommandSafetyDecision EvaluatePush(IReadOnlyList<string> arguments)
    {
        if (arguments.Count != 4
            || !string.Equals(arguments[0], "--porcelain", StringComparison.Ordinal)
            || !string.Equals(arguments[1], "--no-verify", StringComparison.Ordinal)
            || !IsSafeAtom(arguments[2]))
        {
            return Blocked("force-delete-or-unsupported-push");
        }

        const string prefix = "HEAD:refs/heads/";
        var refspec = arguments[3];
        if (!refspec.StartsWith(prefix, StringComparison.Ordinal)
            || !IsSafeAtom(refspec[prefix.Length..]))
        {
            return Blocked("unsafe-push-refspec");
        }

        return Allowed("non-force-current-head-push");
    }

    private static bool AllLiteralPathspecs(IReadOnlyList<string> arguments, int startIndex)
    {
        if (startIndex >= arguments.Count)
        {
            return false;
        }

        for (var index = startIndex; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (!argument.StartsWith(LiteralPathspecPrefix, StringComparison.Ordinal)
                || argument.Length == LiteralPathspecPrefix.Length)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSafeFetchAtom(string value) =>
        IsSafeAtom(value)
        && value[0] is not '+' and not ':';

    private static bool IsSafeAtom(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value[0] == '-')
        {
            return false;
        }

        foreach (var character in value)
        {
            if (char.IsControl(character) || char.IsWhiteSpace(character))
            {
                return false;
            }
        }

        return true;
    }

    private static GitCommandSafetyDecision Allowed(string rule) =>
        new(GitCommandSafetyStatus.Allowed, rule);

    private static GitCommandSafetyDecision Blocked(string rule) =>
        new(GitCommandSafetyStatus.Blocked, rule);
}
