using FCCCodeDesktop.Application.Terminal;

namespace FCCCodeDesktop.Terminal;

public sealed record OptionalShellDetectionEnvironment(
    string? ProgramFilesPath,
    string? ProgramFilesX86Path,
    string? LocalApplicationDataPath,
    string? WindowsDirectoryPath,
    string? PathEnvironmentVariable)
{
    public static OptionalShellDetectionEnvironment CaptureCurrent() =>
        new(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetEnvironmentVariable("SystemRoot"),
            Environment.GetEnvironmentVariable("PATH"));
}

/// <summary>
/// Detects optional local shell executables without launching them, touching remotes, or mutating
/// process/global environment state. Detection proves executable presence only; launch usability is
/// intentionally validated by later shell-profile/process-safety work.
/// </summary>
public sealed class WindowsOptionalShellDetector : IOptionalShellDetector
{
    private readonly OptionalShellDetectionEnvironment _environment;

    public WindowsOptionalShellDetector(OptionalShellDetectionEnvironment? environment = null)
    {
        _environment = environment ?? OptionalShellDetectionEnvironment.CaptureCurrent();
    }

    public ValueTask<OptionalShellDetectionResult> DetectAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var installations = new List<OptionalShellInstallation>();
        var seenExecutablePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in EnumerateStandardGitRoots())
        {
            cancellationToken.ThrowIfCancellationRequested();
            TryAddGitBash(
                root,
                OptionalShellDetectionSource.StandardInstall,
                installations,
                seenExecutablePaths);
        }

        foreach (var root in EnumeratePathDerivedGitRoots())
        {
            cancellationToken.ThrowIfCancellationRequested();
            TryAddGitBash(
                root,
                OptionalShellDetectionSource.PathDerivedGitInstall,
                installations,
                seenExecutablePaths);
        }

        cancellationToken.ThrowIfCancellationRequested();
        TryAddWsl(installations, seenExecutablePaths);

        installations.Sort(static (left, right) =>
        {
            var kindComparison = left.Kind.CompareTo(right.Kind);
            if (kindComparison != 0)
            {
                return kindComparison;
            }

            var sourceComparison = left.Source.CompareTo(right.Source);
            return sourceComparison != 0
                ? sourceComparison
                : StringComparer.OrdinalIgnoreCase.Compare(left.ExecutablePath, right.ExecutablePath);
        });

        return ValueTask.FromResult(new OptionalShellDetectionResult(installations));
    }

    private List<string> EnumerateStandardGitRoots()
    {
        var roots = new List<string>(3);

        var programFiles = TryNormalizeDirectory(_environment.ProgramFilesPath);
        if (programFiles is not null)
        {
            roots.Add(Path.Combine(programFiles, "Git"));
        }

        var programFilesX86 = TryNormalizeDirectory(_environment.ProgramFilesX86Path);
        if (programFilesX86 is not null)
        {
            roots.Add(Path.Combine(programFilesX86, "Git"));
        }

        var localApplicationData = TryNormalizeDirectory(_environment.LocalApplicationDataPath);
        if (localApplicationData is not null)
        {
            roots.Add(Path.Combine(localApplicationData, "Programs", "Git"));
        }

        return roots;
    }

    private List<string> EnumeratePathDerivedGitRoots()
    {
        var roots = new List<string>();
        if (string.IsNullOrWhiteSpace(_environment.PathEnvironmentVariable))
        {
            return roots;
        }

        foreach (var rawEntry in _environment.PathEnvironmentVariable.Split(
                     Path.PathSeparator,
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var entry = TryNormalizeDirectory(rawEntry.Trim('"'));
            if (entry is null)
            {
                continue;
            }

            roots.Add(entry);

            var trimmedEntry = entry.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var leaf = Path.GetFileName(trimmedEntry);
            var parent = Directory.GetParent(entry)?.FullName;
            if (parent is not null &&
                (string.Equals(leaf, "cmd", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(leaf, "bin", StringComparison.OrdinalIgnoreCase)))
            {
                roots.Add(parent);
            }

            if (parent is not null &&
                string.Equals(leaf, "bin", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Path.GetFileName(parent), "usr", StringComparison.OrdinalIgnoreCase))
            {
                var grandParent = Directory.GetParent(parent)?.FullName;
                if (grandParent is not null)
                {
                    roots.Add(grandParent);
                }
            }
        }

        return roots;
    }

    private static void TryAddGitBash(
        string root,
        OptionalShellDetectionSource source,
        List<OptionalShellInstallation> installations,
        HashSet<string> seenExecutablePaths)
    {
        var normalizedRoot = TryNormalizeDirectory(root);
        if (normalizedRoot is null)
        {
            return;
        }

        var gitCmd = Path.Combine(normalizedRoot, "cmd", "git.exe");
        var gitBin = Path.Combine(normalizedRoot, "bin", "git.exe");
        if (!File.Exists(gitCmd) && !File.Exists(gitBin))
        {
            return;
        }

        var bash = Path.Combine(normalizedRoot, "bin", "bash.exe");
        if (!File.Exists(bash))
        {
            bash = Path.Combine(normalizedRoot, "usr", "bin", "bash.exe");
            if (!File.Exists(bash))
            {
                return;
            }
        }

        var normalizedBash = Path.GetFullPath(bash);
        if (seenExecutablePaths.Add(normalizedBash))
        {
            installations.Add(
                new OptionalShellInstallation(OptionalShellKind.GitBash, normalizedBash, source));
        }
    }

    private void TryAddWsl(
        List<OptionalShellInstallation> installations,
        HashSet<string> seenExecutablePaths)
    {
        var windowsDirectory = TryNormalizeDirectory(_environment.WindowsDirectoryPath);
        if (windowsDirectory is null)
        {
            return;
        }

        var executable = Path.Combine(windowsDirectory, "System32", "wsl.exe");
        if (!File.Exists(executable))
        {
            return;
        }

        var normalizedExecutable = Path.GetFullPath(executable);
        if (seenExecutablePaths.Add(normalizedExecutable))
        {
            installations.Add(
                new OptionalShellInstallation(
                    OptionalShellKind.Wsl,
                    normalizedExecutable,
                    OptionalShellDetectionSource.WindowsSystem));
        }
    }

    private static string? TryNormalizeDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
