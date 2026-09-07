using FCCCodeDesktop.Application.Terminal;
using FCCCodeDesktop.Terminal;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class OptionalShellDetectionTests
{
    [Fact]
    public async Task DetectAsyncFindsStandardGitBashAndWslDeterministically()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var programFiles = Path.Combine(root, "Program Files");
            var gitRoot = Path.Combine(programFiles, "Git");
            var bashPath = Path.Combine(gitRoot, "bin", "bash.exe");
            Touch(Path.Combine(gitRoot, "cmd", "git.exe"));
            Touch(bashPath);

            var windowsDirectory = Path.Combine(root, "Windows");
            var wslPath = Path.Combine(windowsDirectory, "System32", "wsl.exe");
            Touch(wslPath);

            var detector = new WindowsOptionalShellDetector(
                new OptionalShellDetectionEnvironment(
                    programFiles,
                    programFiles,
                    null,
                    windowsDirectory,
                    Path.Combine(gitRoot, "cmd")));

            var result = await detector.DetectAsync();

            Assert.Collection(
                result.Installations,
                gitBash =>
                {
                    Assert.Equal(OptionalShellKind.GitBash, gitBash.Kind);
                    Assert.Equal(Path.GetFullPath(bashPath), gitBash.ExecutablePath);
                    Assert.Equal(OptionalShellDetectionSource.StandardInstall, gitBash.Source);
                },
                wsl =>
                {
                    Assert.Equal(OptionalShellKind.Wsl, wsl.Kind);
                    Assert.Equal(Path.GetFullPath(wslPath), wsl.ExecutablePath);
                    Assert.Equal(OptionalShellDetectionSource.WindowsSystem, wsl.Source);
                });

            Assert.True(result.IsDetected(OptionalShellKind.GitBash));
            Assert.True(result.IsDetected(OptionalShellKind.Wsl));
            Assert.Equal(Path.GetFullPath(bashPath), result.Find(OptionalShellKind.GitBash)?.ExecutablePath);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public async Task DetectAsyncFindsPortableGitInstallationFromPathWithoutGenericBashFalsePositive()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var gitRoot = Path.Combine(root, "PortableGit");
            var bashPath = Path.Combine(gitRoot, "bin", "bash.exe");
            Touch(Path.Combine(gitRoot, "cmd", "git.exe"));
            Touch(bashPath);

            var unrelatedBin = Path.Combine(root, "Unrelated", "bin");
            Touch(Path.Combine(unrelatedBin, "bash.exe"));

            var pathValue = string.Join(
                Path.PathSeparator,
                unrelatedBin,
                Path.Combine(gitRoot, "cmd"));
            var detector = new WindowsOptionalShellDetector(
                new OptionalShellDetectionEnvironment(null, null, null, null, pathValue));

            var result = await detector.DetectAsync();

            var installation = Assert.Single(result.Installations);
            Assert.Equal(OptionalShellKind.GitBash, installation.Kind);
            Assert.Equal(Path.GetFullPath(bashPath), installation.ExecutablePath);
            Assert.Equal(OptionalShellDetectionSource.PathDerivedGitInstall, installation.Source);
            Assert.False(result.IsDetected(OptionalShellKind.Wsl));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public async Task DetectAsyncReturnsEmptyForMissingCandidates()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var detector = new WindowsOptionalShellDetector(
                new OptionalShellDetectionEnvironment(
                    Path.Combine(root, "ProgramFiles"),
                    null,
                    Path.Combine(root, "LocalAppData"),
                    Path.Combine(root, "Windows"),
                    null));

            var result = await detector.DetectAsync();

            Assert.Empty(result.Installations);
            Assert.Null(result.Find(OptionalShellKind.GitBash));
            Assert.Null(result.Find(OptionalShellKind.Wsl));
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    [Fact]
    public async Task DetectAsyncHonorsPreCancelledToken()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var detector = new WindowsOptionalShellDetector(
            new OptionalShellDetectionEnvironment(null, null, null, null, null));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => detector.DetectAsync(cancellation.Token).AsTask());
    }

    private static string CreateTemporaryRoot()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "fcc-p08-006-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Touch(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, []);
    }

    private static void DeleteTemporaryRoot(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(path, recursive: true);
    }
}
