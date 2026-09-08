using FCCCodeDesktop.Tools.Unity;

var sandbox = Path.Combine(Path.GetTempPath(), $"fccd-unity-editor-resolution-{Guid.NewGuid():N}");
Directory.CreateDirectory(sandbox);

try
{
    await ValidateConfiguredRootPrecedenceAsync(sandbox);
    await ValidateDefaultHubFallbackAsync(sandbox);
    await ValidateDirectInstallationRootAsync(sandbox);
    await ValidateMissingExecutableAsync(sandbox);
    await ValidateSearchRootDeduplicationAsync(sandbox);
    await ValidateVersionValidationAsync(sandbox);
    await ValidateCancellationAsync(sandbox);

    Console.WriteLine("P10-002 Unity editor resolution fixture: PASS.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}
finally
{
    try
    {
        if (Directory.Exists(sandbox))
        {
            Directory.Delete(sandbox, recursive: true);
        }
    }
    catch (IOException)
    {
        // Fixture cleanup must not hide the validation outcome.
    }
    catch (UnauthorizedAccessException)
    {
        // Fixture cleanup must not hide the validation outcome.
    }
}

static async Task ValidateConfiguredRootPrecedenceAsync(string sandbox)
{
    const string requiredVersion = "6000.0.42f1";
    var configuredRoot = Path.Combine(sandbox, "configured");
    var programFiles = Path.Combine(sandbox, "Program Files");
    var defaultHubRoot = Path.Combine(programFiles, "Unity", "Hub", "Editor");

    var configuredExecutable = CreateFakeEditor(configuredRoot, requiredVersion);
    _ = CreateFakeEditor(defaultHubRoot, requiredVersion);

    var resolver = new UnityEditorResolver(
        [configuredRoot],
        includeDefaultHubRoot: true,
        programFilesDirectoryOverride: programFiles);

    var result = await resolver.ResolveAsync(requiredVersion);
    var installation = result.Installation
        ?? throw new InvalidOperationException("Configured resolution must include installation metadata.");

    Assert(result.IsResolved, "Configured exact editor should resolve.");
    Assert(result.Status == UnityEditorResolutionStatus.ExactMatch, "Configured resolution status must be ExactMatch.");
    Assert(installation.Source == UnityEditorInstallationSource.ConfiguredRoot, "Configured root must win over the default Hub root.");
    AssertPathEqual(configuredExecutable, installation.EditorExecutablePath, "Configured editor executable path mismatch.");
    Assert(result.SearchRoots.Count == 2, "Configured plus default Hub roots should both be recorded.");
}

static async Task ValidateDefaultHubFallbackAsync(string sandbox)
{
    const string requiredVersion = "2022.3.75f1";
    var configuredRoot = Path.Combine(sandbox, "configured-empty");
    var programFiles = Path.Combine(sandbox, "HubFallbackProgramFiles");
    var defaultHubRoot = Path.Combine(programFiles, "Unity", "Hub", "Editor");
    var expectedExecutable = CreateFakeEditor(defaultHubRoot, requiredVersion);

    var resolver = new UnityEditorResolver(
        [configuredRoot],
        includeDefaultHubRoot: true,
        programFilesDirectoryOverride: programFiles);

    var result = await resolver.ResolveAsync(requiredVersion);
    var installation = result.Installation
        ?? throw new InvalidOperationException("Default Hub resolution must include installation metadata.");

    Assert(result.IsResolved, "Default Unity Hub editor should resolve when configured roots do not match.");
    Assert(installation.Source == UnityEditorInstallationSource.UnityHubDefault, "Fallback must report UnityHubDefault provenance.");
    AssertPathEqual(expectedExecutable, installation.EditorExecutablePath, "Default Hub executable path mismatch.");
}

static async Task ValidateDirectInstallationRootAsync(string sandbox)
{
    const string requiredVersion = "2021.3.45f1";
    var versionRoot = Path.Combine(sandbox, "direct", requiredVersion);
    var editorDirectory = Path.Combine(versionRoot, "Editor");
    Directory.CreateDirectory(editorDirectory);
    var expectedExecutable = Path.Combine(editorDirectory, "Unity.exe");
    File.WriteAllText(expectedExecutable, string.Empty);

    var resolver = new UnityEditorResolver([versionRoot], includeDefaultHubRoot: false);
    var result = await resolver.ResolveAsync(requiredVersion);
    var installation = result.Installation
        ?? throw new InvalidOperationException("Direct-root resolution must include installation metadata.");

    Assert(result.IsResolved, "A configured root that points directly at the requested version must resolve.");
    AssertPathEqual(versionRoot, installation.InstallationRoot, "Direct installation root mismatch.");
    AssertPathEqual(expectedExecutable, installation.EditorExecutablePath, "Direct editor executable path mismatch.");
}

static async Task ValidateMissingExecutableAsync(string sandbox)
{
    const string requiredVersion = "6000.1.0b3";
    var root = Path.Combine(sandbox, "missing-executable");
    Directory.CreateDirectory(Path.Combine(root, requiredVersion, "Editor"));

    var resolver = new UnityEditorResolver([root], includeDefaultHubRoot: false);
    var result = await resolver.ResolveAsync(requiredVersion);

    Assert(!result.IsResolved, "A version directory without Unity.exe must not resolve.");
    Assert(result.Status == UnityEditorResolutionStatus.NotInstalled, "Missing executable must report NotInstalled.");
    Assert(result.Installation is null, "Missing executable must not produce installation metadata.");
}

static async Task ValidateSearchRootDeduplicationAsync(string sandbox)
{
    const string requiredVersion = "2020.3.48f1";
    var root = Path.Combine(sandbox, "duplicate-root");
    _ = CreateFakeEditor(root, requiredVersion);

    var resolver = new UnityEditorResolver([root, root], includeDefaultHubRoot: false);
    var result = await resolver.ResolveAsync(requiredVersion);

    Assert(result.IsResolved, "Duplicate configured roots must not prevent resolution.");
    Assert(result.SearchRoots.Count == 1, "Duplicate configured roots must be de-duplicated case-insensitively.");
}

static async Task ValidateVersionValidationAsync(string sandbox)
{
    var resolver = new UnityEditorResolver([sandbox], includeDefaultHubRoot: false);

    await AssertThrowsAsync<ArgumentException>(
        () => resolver.ResolveAsync("../6000.0.42f1"),
        "Path-like Unity versions must be rejected.");
    await AssertThrowsAsync<ArgumentException>(
        () => resolver.ResolveAsync("6000.0.42F1"),
        "Non-canonical channel casing must be rejected.");
    await AssertThrowsAsync<ArgumentException>(
        () => resolver.ResolveAsync("6000.0.42f1 "),
        "Unity versions with trailing whitespace must be rejected.");

    const string chinaRevisionVersion = "2022.3.45f1c1";
    _ = CreateFakeEditor(sandbox, chinaRevisionVersion);
    var chinaRevisionResult = await resolver.ResolveAsync(chinaRevisionVersion);
    Assert(chinaRevisionResult.IsResolved, "Canonical Unity China revision suffixes must remain resolvable.");
}

static async Task ValidateCancellationAsync(string sandbox)
{
    var resolver = new UnityEditorResolver([sandbox], includeDefaultHubRoot: false);
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    await AssertThrowsAsync<OperationCanceledException>(
        () => resolver.ResolveAsync("6000.0.42f1", cancellation.Token),
        "Pre-cancelled resolution must propagate cancellation.");
}

static string CreateFakeEditor(string searchRoot, string version)
{
    var editorDirectory = Path.Combine(searchRoot, version, "Editor");
    Directory.CreateDirectory(editorDirectory);
    var executablePath = Path.Combine(editorDirectory, "Unity.exe");
    File.WriteAllText(executablePath, string.Empty);
    return executablePath;
}

static async Task AssertThrowsAsync<TException>(Func<Task> action, string message)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static void AssertPathEqual(string expected, string actual, string message)
{
    var expectedFullPath = Path.GetFullPath(expected);
    var actualFullPath = Path.GetFullPath(actual);
    Assert(
        string.Equals(expectedFullPath, actualFullPath, StringComparison.OrdinalIgnoreCase),
        $"{message} Expected '{expectedFullPath}', actual '{actualFullPath}'.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
