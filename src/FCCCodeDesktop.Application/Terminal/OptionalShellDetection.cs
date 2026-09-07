namespace FCCCodeDesktop.Application.Terminal;

public enum OptionalShellKind
{
    GitBash = 0,
    Wsl = 1,
}

public enum OptionalShellDetectionSource
{
    StandardInstall = 0,
    PathDerivedGitInstall = 1,
    WindowsSystem = 2,
}

public sealed record OptionalShellInstallation
{
    public OptionalShellInstallation(
        OptionalShellKind kind,
        string executablePath,
        OptionalShellDetectionSource source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (!Path.IsPathFullyQualified(executablePath))
        {
            throw new ArgumentException(
                "Optional shell executable paths must be fully qualified.",
                nameof(executablePath));
        }

        Kind = kind;
        ExecutablePath = Path.GetFullPath(executablePath);
        Source = source;
    }

    public OptionalShellKind Kind { get; }

    public string ExecutablePath { get; }

    public OptionalShellDetectionSource Source { get; }
}

public sealed class OptionalShellDetectionResult
{
    private readonly IReadOnlyList<OptionalShellInstallation> _installations;

    public OptionalShellDetectionResult(IEnumerable<OptionalShellInstallation> installations)
    {
        ArgumentNullException.ThrowIfNull(installations);
        var snapshot = installations.ToArray();
        if (snapshot.Any(static installation => installation is null))
        {
            throw new ArgumentException(
                "Optional shell detection results cannot contain null installations.",
                nameof(installations));
        }

        _installations = Array.AsReadOnly(snapshot);
    }

    public IReadOnlyList<OptionalShellInstallation> Installations => _installations;

    public bool IsDetected(OptionalShellKind kind) =>
        _installations.Any(installation => installation.Kind == kind);

    public OptionalShellInstallation? Find(OptionalShellKind kind) =>
        _installations.FirstOrDefault(installation => installation.Kind == kind);
}

public interface IOptionalShellDetector
{
    ValueTask<OptionalShellDetectionResult> DetectAsync(
        CancellationToken cancellationToken = default);
}
