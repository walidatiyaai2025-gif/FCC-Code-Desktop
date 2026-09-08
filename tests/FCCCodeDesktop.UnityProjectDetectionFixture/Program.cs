using FCCCodeDesktop.Tools.Unity;

namespace FCCCodeDesktop.UnityProjectDetectionFixture;

internal static class Program
{
    private static async Task<int> Main()
    {
        var fixtureRoot = Path.Combine(
            Path.GetTempPath(),
            $"fccd-p10-001 Unity مشروع عربي {Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(fixtureRoot);
            var detector = new UnityProjectDetector();

            await ValidateCompleteProjectAsync(detector, fixtureRoot).ConfigureAwait(false);
            await ValidateRevisionFallbackAsync(detector, fixtureRoot).ConfigureAwait(false);
            await ValidateIncompleteProjectAsync(detector, fixtureRoot).ConfigureAwait(false);
            await ValidateMissingVersionAsync(detector, fixtureRoot).ConfigureAwait(false);
            await ValidateNonUnityDirectoryAsync(detector, fixtureRoot).ConfigureAwait(false);
            await ValidateMissingRootAsync(detector, fixtureRoot).ConfigureAwait(false);
            await ValidateCancellationAsync(detector, fixtureRoot).ConfigureAwait(false);
            ValidateInvalidInput(detector);

            Console.WriteLine("P10-001 Unity project/version detector fixture: PASS");
            return 0;
        }
        finally
        {
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
        }
    }

    private static async Task ValidateCompleteProjectAsync(
        UnityProjectDetector detector,
        string fixtureRoot)
    {
        var projectRoot = Path.Combine(fixtureRoot, "مشروع كامل with spaces");
        await CreateUnityProjectAsync(
            projectRoot,
            "m_EditorVersion: 6000.5.8f1\r\nm_EditorVersionWithRevision: 6000.5.8f1 (abcdef123456)\r\n")
            .ConfigureAwait(false);
        var sentinelPath = Path.Combine(projectRoot, "Assets", "owner-sentinel.txt");
        var sentinel = "لا تعدل هذا الملف owner bytes";
        await File.WriteAllTextAsync(sentinelPath, sentinel).ConfigureAwait(false);

        var result = await detector.DetectAsync(projectRoot).ConfigureAwait(false);

        AssertEqual(UnityProjectDetectionStatus.ValidUnityProject, result.Status, "valid status");
        AssertTrue(result.IsValid, "valid result flag");
        AssertEqual(Path.GetFullPath(projectRoot), result.RootPath, "normalized root");
        AssertTrue(result.Markers.HasCompleteProjectLayout, "complete marker layout");
        AssertNotNull(result.Version, "valid version metadata");
        AssertEqual("6000.5.8f1", result.Version!.RequiredEditorVersion, "required editor version");
        AssertEqual("abcdef123456", result.Version.Revision, "revision");
        AssertEqual(sentinel, await File.ReadAllTextAsync(sentinelPath).ConfigureAwait(false), "owner bytes preserved");
    }

    private static async Task ValidateRevisionFallbackAsync(
        UnityProjectDetector detector,
        string fixtureRoot)
    {
        var projectRoot = Path.Combine(fixtureRoot, "revision fallback");
        await CreateUnityProjectAsync(
            projectRoot,
            "m_EditorVersionWithRevision: 2022.3.75f1 (fedcba654321)\n")
            .ConfigureAwait(false);

        var result = await detector.DetectAsync(projectRoot).ConfigureAwait(false);

        AssertEqual(UnityProjectDetectionStatus.ValidUnityProject, result.Status, "revision fallback status");
        AssertNotNull(result.Version, "revision fallback metadata");
        AssertEqual("2022.3.75f1", result.Version!.RequiredEditorVersion, "revision fallback version");
        AssertEqual("fedcba654321", result.Version.Revision, "revision fallback revision");
    }

    private static async Task ValidateIncompleteProjectAsync(
        UnityProjectDetector detector,
        string fixtureRoot)
    {
        var projectRoot = Path.Combine(fixtureRoot, "incomplete project");
        Directory.CreateDirectory(Path.Combine(projectRoot, "Assets"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "ProjectSettings"));
        await File.WriteAllTextAsync(
            Path.Combine(projectRoot, "ProjectSettings", "ProjectVersion.txt"),
            "m_EditorVersion: 6000.5.8f1\n").ConfigureAwait(false);

        var result = await detector.DetectAsync(projectRoot).ConfigureAwait(false);

        AssertEqual(UnityProjectDetectionStatus.UnityProjectIncomplete, result.Status, "incomplete status");
        AssertFalse(result.IsValid, "incomplete validity");
        AssertFalse(result.Markers.PackagesDirectoryExists, "missing Packages marker");
        AssertNotNull(result.Version, "incomplete project still reports parsed version");
    }

    private static async Task ValidateMissingVersionAsync(
        UnityProjectDetector detector,
        string fixtureRoot)
    {
        var projectRoot = Path.Combine(fixtureRoot, "missing version value");
        await CreateUnityProjectAsync(projectRoot, "m_SomeOtherSetting: value\n").ConfigureAwait(false);

        var result = await detector.DetectAsync(projectRoot).ConfigureAwait(false);

        AssertEqual(UnityProjectDetectionStatus.ProjectVersionMissing, result.Status, "missing version status");
        AssertTrue(result.Markers.HasCompleteProjectLayout, "missing-version layout is otherwise complete");
        AssertNull(result.Version, "missing version metadata");
    }

    private static async Task ValidateNonUnityDirectoryAsync(
        UnityProjectDetector detector,
        string fixtureRoot)
    {
        var projectRoot = Path.Combine(fixtureRoot, "ordinary repository");
        Directory.CreateDirectory(projectRoot);
        await File.WriteAllTextAsync(Path.Combine(projectRoot, "README.md"), "ordinary repo").ConfigureAwait(false);

        var result = await detector.DetectAsync(projectRoot).ConfigureAwait(false);

        AssertEqual(UnityProjectDetectionStatus.NotUnityProject, result.Status, "non-Unity status");
        AssertFalse(result.Markers.HasAnyUnityMarker, "non-Unity markers");
    }

    private static async Task ValidateMissingRootAsync(
        UnityProjectDetector detector,
        string fixtureRoot)
    {
        var projectRoot = Path.Combine(fixtureRoot, "does not exist");
        var result = await detector.DetectAsync(projectRoot).ConfigureAwait(false);

        AssertEqual(UnityProjectDetectionStatus.ProjectRootNotFound, result.Status, "missing-root status");
        AssertFalse(Directory.Exists(projectRoot), "detector must not create missing root");
    }

    private static async Task ValidateCancellationAsync(
        UnityProjectDetector detector,
        string fixtureRoot)
    {
        var projectRoot = Path.Combine(fixtureRoot, "cancelled project");
        await CreateUnityProjectAsync(projectRoot, "m_EditorVersion: 6000.5.8f1\n").ConfigureAwait(false);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            await detector.DetectAsync(projectRoot, cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        throw new InvalidOperationException("Pre-cancelled Unity project detection did not cancel.");
    }

    private static void ValidateInvalidInput(UnityProjectDetector detector)
    {
        AssertThrows<ArgumentException>(() => detector.DetectAsync(" "), "blank project root");
        AssertThrows<ArgumentException>(() => detector.DetectAsync(" project "), "ambiguous padded project root");
        AssertThrows<ArgumentException>(() => detector.DetectAsync("bad\0root"), "NUL project root");
    }

    private static async Task CreateUnityProjectAsync(string projectRoot, string projectVersionText)
    {
        Directory.CreateDirectory(Path.Combine(projectRoot, "Assets"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "Packages"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "ProjectSettings"));
        await File.WriteAllTextAsync(
            Path.Combine(projectRoot, "ProjectSettings", "ProjectVersion.txt"),
            projectVersionText).ConfigureAwait(false);
    }

    private static void AssertTrue(bool condition, string label)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Assertion failed: {label}.");
        }
    }

    private static void AssertFalse(bool condition, string label) => AssertTrue(!condition, label);

    private static void AssertNull(object? value, string label)
    {
        if (value is not null)
        {
            throw new InvalidOperationException($"Assertion failed: {label} expected null.");
        }
    }

    private static void AssertNotNull(object? value, string label)
    {
        if (value is null)
        {
            throw new InvalidOperationException($"Assertion failed: {label} expected non-null.");
        }
    }

    private static void AssertEqual<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"Assertion failed: {label}. Expected '{expected}', actual '{actual}'.");
        }
    }

    private static void AssertThrows<TException>(Func<Task> action, string label)
        where TException : Exception
    {
        try
        {
            action().GetAwaiter().GetResult();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Assertion failed: {label} did not throw {typeof(TException).Name}.");
    }
}
