using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using FCCCodeDesktop.Application.Git;

namespace FCCCodeDesktop.Git;

/// <summary>
/// Executes bounded, read-only local Git history queries.
/// </summary>
public sealed class GitCliHistoryService : IGitHistoryService
{
    public static readonly TimeSpan DefaultOperationTimeout = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan MaximumOperationTimeout = TimeSpan.FromSeconds(60);

    public const int MaximumCommitCount = 100;
    public const int MaximumRelativePathCharacters = 4096;
    public const int DefaultMaximumOutputCharacters = 512 * 1024;
    public const int MaximumOutputCharacters = 2 * 1024 * 1024;

    private const int MaximumFailureMessageCharacters = 4096;
    private const int HistoryFieldCount = 7;
    private const string HistoryFormat = "%H%x00%h%x00%P%x00%an%x00%ae%x00%aI%x00%s";

    private readonly IGitService _gitService;
    private readonly string _gitExecutable;
    private readonly TimeSpan _operationTimeout;
    private readonly int _maximumOutputCharacters;

    public GitCliHistoryService(
        string gitExecutable = "git",
        TimeSpan? operationTimeout = null,
        int maximumOutputCharacters = DefaultMaximumOutputCharacters)
        : this(new GitCliService(gitExecutable), gitExecutable, operationTimeout, maximumOutputCharacters)
    {
    }

    public GitCliHistoryService(
        IGitService gitService,
        string gitExecutable = "git",
        TimeSpan? operationTimeout = null,
        int maximumOutputCharacters = DefaultMaximumOutputCharacters)
    {
        ArgumentNullException.ThrowIfNull(gitService);
        ArgumentException.ThrowIfNullOrWhiteSpace(gitExecutable);

        var resolvedTimeout = operationTimeout ?? DefaultOperationTimeout;
        if (resolvedTimeout <= TimeSpan.Zero || resolvedTimeout > MaximumOperationTimeout)
        {
            throw new ArgumentOutOfRangeException(
                nameof(operationTimeout),
                operationTimeout,
                $"Git history timeout must be greater than zero and no more than {MaximumOperationTimeout.TotalSeconds} seconds.");
        }

        if (maximumOutputCharacters <= 0 || maximumOutputCharacters > MaximumOutputCharacters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumOutputCharacters),
                maximumOutputCharacters,
                $"Git history output limit must be greater than zero and no more than {MaximumOutputCharacters} characters.");
        }

        _gitService = gitService;
        _gitExecutable = gitExecutable;
        _operationTimeout = resolvedTimeout;
        _maximumOutputCharacters = maximumOutputCharacters;
    }

    public async Task<GitHistoryResult> GetHistoryAsync(
        string path,
        GitHistoryQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        var resolvedQuery = query ?? new GitHistoryQuery();
        if (!TryNormalizeQuery(resolvedQuery, out var normalizedPath, out var validationFailure))
        {
            return EmptyResult(GitHistoryStatus.InvalidQuery, failureMessage: validationFailure);
        }

        var detection = await _gitService.DetectRepositoryAsync(path, cancellationToken).ConfigureAwait(false);
        switch (detection.Status)
        {
            case GitRepositoryDetectionStatus.NotRepository:
                return EmptyResult(GitHistoryStatus.NotRepository);
            case GitRepositoryDetectionStatus.GitUnavailable:
                return EmptyResult(GitHistoryStatus.GitUnavailable);
            case GitRepositoryDetectionStatus.ProbeFailed:
                return EmptyResult(GitHistoryStatus.QueryFailed);
            case GitRepositoryDetectionStatus.Repository:
                break;
            default:
                return EmptyResult(GitHistoryStatus.QueryFailed);
        }

        var repository = detection.Repository;
        if (repository is null)
        {
            return EmptyResult(GitHistoryStatus.QueryFailed);
        }

        var repositoryRoot = repository.RepositoryRootPath;
        var head = await ExecuteGitAsync(
            repositoryRoot,
            new List<string> { "rev-parse", "--verify", "--quiet", "HEAD^{commit}" },
            cancellationToken).ConfigureAwait(false);
        if (!head.Started)
        {
            return EmptyResult(GitHistoryStatus.GitUnavailable, repositoryRoot);
        }

        if (head.TimedOut)
        {
            return EmptyResult(
                GitHistoryStatus.QueryFailed,
                repositoryRoot,
                failureMessage: "Git HEAD verification timed out.");
        }

        if (head.ExitCode != 0)
        {
            return string.IsNullOrWhiteSpace(head.StandardError)
                ? EmptyResult(GitHistoryStatus.EmptyRepository, repositoryRoot)
                : EmptyResult(
                    GitHistoryStatus.QueryFailed,
                    repositoryRoot,
                    failureMessage: NormalizeFailureMessage(head.StandardError));
        }

        if (resolvedQuery.BeforeCommitSha is not null)
        {
            var cursorVerification = await ExecuteGitAsync(
                repositoryRoot,
                new List<string>
                {
                    "rev-parse",
                    "--verify",
                    "--quiet",
                    $"{resolvedQuery.BeforeCommitSha}^{{commit}}",
                },
                cancellationToken).ConfigureAwait(false);

            if (!cursorVerification.Started)
            {
                return EmptyResult(GitHistoryStatus.GitUnavailable, repositoryRoot);
            }

            if (cursorVerification.TimedOut)
            {
                return EmptyResult(
                    GitHistoryStatus.QueryFailed,
                    repositoryRoot,
                    failureMessage: "Git history cursor verification timed out.");
            }

            if (cursorVerification.ExitCode != 0)
            {
                return EmptyResult(
                    GitHistoryStatus.InvalidQuery,
                    repositoryRoot,
                    failureMessage: "The requested history cursor does not identify a commit in this repository.");
            }
        }

        var arguments = new List<string>
        {
            "log",
            "--no-patch",
            "--no-show-signature",
            "--no-decorate",
            "--encoding=UTF-8",
            "-z",
            $"--max-count={resolvedQuery.MaxCount + 1}",
            $"--format={HistoryFormat}",
        };

        if (resolvedQuery.BeforeCommitSha is not null)
        {
            arguments.Add($"{resolvedQuery.BeforeCommitSha}^");
        }

        if (normalizedPath is not null)
        {
            arguments.Add("--");
            arguments.Add($":(literal){normalizedPath}");
        }

        var history = await ExecuteGitAsync(
            repositoryRoot,
            arguments,
            cancellationToken,
            _maximumOutputCharacters).ConfigureAwait(false);
        if (!history.Started)
        {
            return EmptyResult(GitHistoryStatus.GitUnavailable, repositoryRoot);
        }

        if (history.TimedOut)
        {
            return EmptyResult(
                GitHistoryStatus.QueryFailed,
                repositoryRoot,
                failureMessage: "Git history query timed out.");
        }

        if (history.ExitCode != 0)
        {
            return EmptyResult(
                GitHistoryStatus.QueryFailed,
                repositoryRoot,
                failureMessage: NormalizeFailureMessage(history.StandardError));
        }

        if (history.StandardOutputTruncated)
        {
            return EmptyResult(
                GitHistoryStatus.TooLarge,
                repositoryRoot,
                failureMessage: "Git history output exceeded the configured materialization limit.");
        }

        if (!TryParseHistory(history.StandardOutput, out var commits))
        {
            return EmptyResult(
                GitHistoryStatus.QueryFailed,
                repositoryRoot,
                failureMessage: "Git history output did not match the expected structured record format.");
        }

        var visibleCommits = commits.Take(resolvedQuery.MaxCount).ToArray();
        var nextCursor = commits.Count > resolvedQuery.MaxCount && visibleCommits.Length > 0
            ? visibleCommits[^1].Sha
            : null;

        return new GitHistoryResult(
            GitHistoryStatus.Success,
            repositoryRoot,
            visibleCommits,
            nextCursor);
    }

    private static bool TryNormalizeQuery(
        GitHistoryQuery query,
        out string? normalizedPath,
        out string? failureMessage)
    {
        if (query.MaxCount <= 0 || query.MaxCount > MaximumCommitCount)
        {
            normalizedPath = null;
            failureMessage = $"History MaxCount must be between 1 and {MaximumCommitCount}.";
            return false;
        }

        if (query.BeforeCommitSha is not null && !IsFullObjectId(query.BeforeCommitSha))
        {
            normalizedPath = null;
            failureMessage = "History cursor must be a full 40- or 64-character hexadecimal object ID.";
            return false;
        }

        if (query.RelativePath is null)
        {
            normalizedPath = null;
            failureMessage = null;
            return true;
        }

        if (query.RelativePath.Length == 0
            || query.RelativePath.Length > MaximumRelativePathCharacters
            || Path.IsPathRooted(query.RelativePath))
        {
            normalizedPath = null;
            failureMessage = "History path filter must be a bounded repository-relative path.";
            return false;
        }

        var candidate = query.RelativePath.Replace('\\', '/');
        if (candidate.Length == 0 || candidate[0] == '/')
        {
            normalizedPath = null;
            failureMessage = "History path filter must be repository-relative.";
            return false;
        }

        var segments = candidate.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0
            || segments.Any(static segment => segment is "." or ".." || segment.Equals(".git", StringComparison.OrdinalIgnoreCase)))
        {
            normalizedPath = null;
            failureMessage = "History path filter contains a disallowed path segment.";
            return false;
        }

        normalizedPath = string.Join('/', segments);
        failureMessage = null;
        return true;
    }

    private static bool IsFullObjectId(string value)
    {
        if (value.Length is not (40 or 64))
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseHistory(string output, out IReadOnlyList<GitHistoryCommit> commits)
    {
        if (output.Length == 0)
        {
            commits = Array.Empty<GitHistoryCommit>();
            return true;
        }

        var fields = output.Split('\0', StringSplitOptions.None);
        var fieldCount = fields.Length;
        if (fieldCount > 0 && fields[^1].Length == 0)
        {
            fieldCount--;
        }

        if (fieldCount == 0 || fieldCount % HistoryFieldCount != 0)
        {
            commits = Array.Empty<GitHistoryCommit>();
            return false;
        }

        var parsed = new List<GitHistoryCommit>(fieldCount / HistoryFieldCount);
        for (var index = 0; index < fieldCount; index += HistoryFieldCount)
        {
            var sha = fields[index];
            var abbreviatedSha = fields[index + 1];
            var parents = fields[index + 2]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var authorName = fields[index + 3];
            var authorEmail = fields[index + 4];
            var authorDateText = fields[index + 5];
            var subject = fields[index + 6];

            if (!IsFullObjectId(sha)
                || string.IsNullOrWhiteSpace(abbreviatedSha)
                || !DateTimeOffset.TryParse(
                    authorDateText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces,
                    out var authorDate)
                || parents.Any(static parent => !IsFullObjectId(parent)))
            {
                commits = Array.Empty<GitHistoryCommit>();
                return false;
            }

            parsed.Add(new GitHistoryCommit(
                sha,
                abbreviatedSha,
                parents,
                authorName,
                authorEmail,
                authorDate,
                subject));
        }

        commits = parsed;
        return true;
    }

    private async Task<GitCommandResult> ExecuteGitAsync(
        string workingDirectory,
        List<string> arguments,
        CancellationToken cancellationToken,
        int? maxStandardOutputCharacters = null)
    {
        var startInfo = new ProcessStartInfo(_gitExecutable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        startInfo.Environment["GIT_PAGER"] = "cat";
        startInfo.Environment["PAGER"] = "cat";
        startInfo.Environment["GIT_EDITOR"] = "true";
        startInfo.Environment["GIT_SEQUENCE_EDITOR"] = "true";
        startInfo.Environment["GCM_INTERACTIVE"] = "Never";
        startInfo.Environment["LC_ALL"] = "C";
        startInfo.Environment["LANG"] = "C";

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                return GitCommandResult.NotStarted;
            }
        }
        catch (Win32Exception)
        {
            return GitCommandResult.NotStarted;
        }

        var standardOutputTask = ReadProcessOutputAsync(
            process.StandardOutput,
            maxStandardOutputCharacters);
        var standardErrorTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_operationTimeout);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            await TerminateProcessAsync(process).ConfigureAwait(false);
            var cancelledOutput = await standardOutputTask.ConfigureAwait(false);
            var cancelledError = await standardErrorTask.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new GitCommandResult(
                true,
                null,
                cancelledOutput.Text,
                cancelledError,
                TimedOut: true,
                cancelledOutput.WasTruncated);
        }

        var standardOutput = await standardOutputTask.ConfigureAwait(false);
        return new GitCommandResult(
            true,
            process.ExitCode,
            standardOutput.Text,
            await standardErrorTask.ConfigureAwait(false),
            TimedOut: false,
            standardOutput.WasTruncated);
    }

    private static async Task<BoundedTextResult> ReadProcessOutputAsync(
        StreamReader reader,
        int? maxCharacters)
    {
        if (maxCharacters is null)
        {
            return new BoundedTextResult(
                await reader.ReadToEndAsync(CancellationToken.None).ConfigureAwait(false),
                WasTruncated: false);
        }

        var builder = new StringBuilder(Math.Min(maxCharacters.Value, 64 * 1024));
        var buffer = new char[8192];
        var wasTruncated = false;

        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), CancellationToken.None).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            var remaining = maxCharacters.Value - builder.Length;
            if (remaining > 0)
            {
                builder.Append(buffer, 0, Math.Min(remaining, read));
            }

            if (read > remaining)
            {
                wasTruncated = true;
            }
        }

        return new BoundedTextResult(builder.ToString(), wasTruncated);
    }

    private static GitHistoryResult EmptyResult(
        GitHistoryStatus status,
        string? repositoryRoot = null,
        string? failureMessage = null) =>
        new(status, repositoryRoot, Array.Empty<GitHistoryCommit>(), null, failureMessage);

    private static string? NormalizeFailureMessage(string standardError)
    {
        var normalized = standardError.Trim();
        if (normalized.Length == 0)
        {
            return null;
        }

        return normalized.Length <= MaximumFailureMessageCharacters
            ? normalized
            : normalized[..MaximumFailureMessageCharacters];
    }

    private static async Task TerminateProcessAsync(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            return;
        }

        try
        {
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private sealed record BoundedTextResult(string Text, bool WasTruncated);

    private sealed record GitCommandResult(
        bool Started,
        int? ExitCode,
        string StandardOutput,
        string StandardError,
        bool TimedOut,
        bool StandardOutputTruncated)
    {
        public static GitCommandResult NotStarted { get; } =
            new(false, null, string.Empty, string.Empty, TimedOut: false, StandardOutputTruncated: false);
    }
}
