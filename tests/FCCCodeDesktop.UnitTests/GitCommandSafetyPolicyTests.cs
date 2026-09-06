using FCCCodeDesktop.Git;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class GitCommandSafetyPolicyTests
{
    [Fact]
    public void CurrentSafeMutationShapesAreAllowed()
    {
        string[][] commands =
        [
            ["add", "--", ":(literal)src/app.cs"],
            ["restore", "--staged", "--", ":(literal)src/app.cs"],
            ["rm", "--cached", "--force", "--ignore-unmatch", "--", ":(literal)new file.txt"],
            ["switch", "main"],
            ["switch", "--create", "feature/تجربة"],
            ["fetch", "--no-tags", "--no-recurse-submodules", "origin"],
            ["fetch", "--no-tags", "--no-recurse-submodules", "origin", "main"],
            ["merge", "--ff-only", "--no-edit", "FETCH_HEAD"],
            ["-c", "commit.gpgSign=false", "commit", "--no-verify", "--no-gpg-sign", "--cleanup=verbatim", "--message", "safe commit"],
            ["push", "--porcelain", "--no-verify", "origin", "HEAD:refs/heads/main"],
            ["symbolic-ref", "--quiet", "--short", "HEAD"],
            ["remote", "get-url", "origin"],
            ["remote", "get-url", "--all", "origin"],
            ["rev-parse", "--verify", "HEAD"],
            ["show-ref", "--verify", "--quiet", "refs/heads/main"],
            ["check-ref-format", "--branch", "feature/تجربة"],
            ["diff", "--cached", "--name-only", "-z", "--no-ext-diff", "--"],
            ["var", "GIT_AUTHOR_IDENT"],
            ["merge-base", "--is-ancestor", "HEAD", "FETCH_HEAD"],
        ];

        foreach (var command in commands)
        {
            var decision = GitCommandSafetyPolicy.Evaluate(command);
            Assert.True(decision.IsAllowed, $"Expected safe command shape to be allowed; rule={decision.Rule}.");
            GitCommandSafetyPolicy.EnsureAllowed(command);
        }
    }

    [Fact]
    public void DestructiveAndBroadMutationShapesAreBlocked()
    {
        string[][] commands =
        [
            ["reset", "--hard", "HEAD"],
            ["clean", "-fdx"],
            ["checkout", "--force", "main"],
            ["switch", "--discard-changes", "main"],
            ["switch", "--force", "main"],
            ["restore", "--worktree", "--", ":(literal)src/app.cs"],
            ["restore", "--source=HEAD", "--", ":(literal)src/app.cs"],
            ["rm", "--force", "--", ":(literal)src/app.cs"],
            ["add", "-A"],
            ["add", "--all"],
            ["fetch", "--force", "origin"],
            ["fetch", "--no-tags", "--no-recurse-submodules", "origin", "+main:main"],
            ["merge", "FETCH_HEAD"],
            ["merge", "--no-edit", "FETCH_HEAD"],
            ["commit", "--amend", "--no-edit"],
            ["-c", "core.hooksPath=/tmp/other", "commit", "--message", "x"],
            ["push", "--force", "origin", "HEAD:refs/heads/main"],
            ["push", "--force-with-lease", "origin", "HEAD:refs/heads/main"],
            ["push", "origin", "+HEAD:refs/heads/main"],
            ["push", "origin", ":refs/heads/main"],
            ["pull", "--rebase"],
            ["rebase", "main"],
            ["stash", "push"],
            ["update-ref", "-d", "refs/heads/main"],
            ["branch", "-D", "main"],
            ["symbolic-ref", "HEAD", "refs/heads/other"],
            ["remote", "remove", "origin"],
        ];

        foreach (var command in commands)
        {
            var decision = GitCommandSafetyPolicy.Evaluate(command);
            Assert.False(decision.IsAllowed);
            Assert.Throws<InvalidOperationException>(() => GitCommandSafetyPolicy.EnsureAllowed(command));
        }
    }

    [Fact]
    public void BlockedCommandExceptionDoesNotEchoArguments()
    {
        const string sensitiveMarker = "do-not-echo-this-value";
        var exception = Assert.Throws<InvalidOperationException>(() =>
            GitCommandSafetyPolicy.EnsureAllowed(["reset", "--hard", sensitiveMarker]));

        Assert.DoesNotContain(sensitiveMarker, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IndexOnlyForcedCachedRemovalIsContextuallyAllowedButWorktreeRemovalIsNot()
    {
        var cached = GitCommandSafetyPolicy.Evaluate(
            ["rm", "--cached", "--force", "--ignore-unmatch", "--", ":(literal)new-file.txt"]);
        var workTree = GitCommandSafetyPolicy.Evaluate(
            ["rm", "--force", "--ignore-unmatch", "--", ":(literal)new-file.txt"]);

        Assert.True(cached.IsAllowed);
        Assert.False(workTree.IsAllowed);
    }

    [Fact]
    public void PushMustBeCurrentHeadToNamedBranchWithoutForceOrDeleteSemantics()
    {
        Assert.True(GitCommandSafetyPolicy.Evaluate(
            ["push", "--porcelain", "--no-verify", "origin", "HEAD:refs/heads/feature/test"]).IsAllowed);
        Assert.False(GitCommandSafetyPolicy.Evaluate(
            ["push", "--porcelain", "--no-verify", "origin", "+HEAD:refs/heads/feature/test"]).IsAllowed);
        Assert.False(GitCommandSafetyPolicy.Evaluate(
            ["push", "--porcelain", "--no-verify", "origin", ":refs/heads/feature/test"]).IsAllowed);
    }
}
