using FCCCodeDesktop.Tools;
using FCCCodeDesktop.Tools.Unity;

var failures = new List<string>();
Run("Open project canonical argv", OpenProjectCanonicalArgv, failures);
Run("ExecuteMethod hostile argv preserved", ExecuteMethodHostileArgvPreserved, failures);
Run("Typed EditMode and PlayMode commands", TypedTestCommands, failures);
Run("Builder-owned switch injection rejected", ReservedSwitchInjectionRejected, failures);
Run("Invalid executeMethod names rejected", InvalidExecuteMethodRejected, failures);
Run("Request snapshots mutable inputs", RequestSnapshotsMutableInputs, failures);
Run("Invalid executable and result paths rejected", InvalidPathsRejected, failures);
Run("Duplicate environment keys fail closed", DuplicateEnvironmentKeysRejected, failures);
Run("Optional nographics and quit switches", OptionalSwitches, failures);

if (failures.Count > 0)
{
    Console.Error.WriteLine($"Unity CLI command fixture failed: {failures.Count} case(s).");
    foreach (var failure in failures)
    {
        Console.Error.WriteLine(failure);
    }

    return 1;
}

Console.WriteLine("P10-003 Unity CLI command fixture: PASS.");
return 0;

static void OpenProjectCanonicalArgv()
{
    var context = CreateProjectContext("Open Project عربي");
    var logPath = Path.Combine(context.RootPath, "Logs", "unity editor.log");
    var editorPath = Path.Combine(Path.GetTempPath(), "Unity Hub", "2022.3.75f1", "Editor", "Unity.exe");
    var tool = new ToolIdentity("unity", "Unity Editor");
    var request = new UnityCliCommandRequest(
        context,
        editorPath,
        logPath,
        new UnityOpenProjectAction());

    var processRequest = new UnityCliCommandBuilder().Build(tool, request);
    var invocation = RequireUnityInvocation(processRequest);

    Equal(Path.GetFullPath(editorPath), processRequest.ExecutablePath, "resolved executable path");
    Equal("unity.open-project", invocation.Operation, "operation name");
    Equal(context.RootPath, invocation.WorkingDirectory, "working directory");
    SequenceEqual(
        new[]
        {
            "-batchmode",
            "-nographics",
            "-projectPath",
            context.RootPath,
            "-logFile",
            Path.GetFullPath(logPath),
            "-timestamps",
            "-quit",
        },
        invocation.Arguments,
        "open-project argv");
}

static void ExecuteMethodHostileArgvPreserved()
{
    var context = CreateProjectContext("Execute Method مسافة");
    var logPath = Path.Combine(context.RootPath, "Logs", "execute.log");
    var editorPath = Path.Combine(Path.GetTempPath(), "Unity", "Editor", "Unity.exe");
    var extras = new List<string>
    {
        "-fixtureOutput",
        Path.Combine(context.RootPath, "Artifacts", "result with spaces.json"),
        "",
        "value with spaces",
        "quote-\"value\"",
        "semi;colon&pipe|literal",
        "عربي-✓",
    };
    var environment = new List<KeyValuePair<string, string>>
    {
        new("UNITY_FIXTURE_MODE", "cli-builder"),
        new("UNICODE_VALUE", "قيمة ✓"),
    };

    var action = new UnityExecuteMethodAction("Company.Tools.Automation.Run", extras);
    var request = new UnityCliCommandRequest(
        context,
        editorPath,
        logPath,
        action,
        environment: environment);
    var processRequest = new UnityCliCommandBuilder().Build(
        new ToolIdentity("unity", "Unity Editor"),
        request,
        new ToolProcessCorrelation(operationId: Guid.NewGuid()));
    var invocation = RequireUnityInvocation(processRequest);

    Equal("unity.execute-method", invocation.Operation, "executeMethod operation name");
    var methodIndex = IndexOf(invocation.Arguments, "-executeMethod");
    Equal("Company.Tools.Automation.Run", invocation.Arguments[methodIndex + 1], "executeMethod value");

    var tail = invocation.Arguments.Skip(methodIndex + 2).Take(extras.Count).ToArray();
    SequenceEqual(extras, tail, "hostile extra argv values");
    True(invocation.Arguments[^1] == "-quit", "quit must remain final builder-owned switch");
    Equal("cli-builder", invocation.Environment["UNITY_FIXTURE_MODE"], "environment value");
    Equal("قيمة ✓", invocation.Environment["UNICODE_VALUE"], "Unicode environment value");
}

static void TypedTestCommands()
{
    foreach (var platform in new[] { UnityTestPlatform.EditMode, UnityTestPlatform.PlayMode })
    {
        var context = CreateProjectContext($"Tests {platform}");
        var resultsPath = Path.Combine(context.RootPath, "TestResults", $"{platform}.xml");
        var request = new UnityCliCommandRequest(
            context,
            Path.Combine(Path.GetTempPath(), "Editors", "6000.5.8f1", "Editor", "Unity.exe"),
            Path.Combine(context.RootPath, "Logs", $"{platform}.log"),
            new UnityRunTestsAction(platform, resultsPath));

        var invocation = RequireUnityInvocation(
            new UnityCliCommandBuilder().Build(new ToolIdentity("unity", "Unity Editor"), request));

        Equal(
            platform == UnityTestPlatform.EditMode
                ? "unity.run-tests.editmode"
                : "unity.run-tests.playmode",
            invocation.Operation,
            "typed test operation");
        var runTestsIndex = IndexOf(invocation.Arguments, "-runTests");
        Equal("-testPlatform", invocation.Arguments[runTestsIndex + 1], "test platform switch");
        Equal(platform.ToString(), invocation.Arguments[runTestsIndex + 2], "test platform value");
        Equal("-testResults", invocation.Arguments[runTestsIndex + 3], "test results switch");
        Equal(Path.GetFullPath(resultsPath), invocation.Arguments[runTestsIndex + 4], "test results path");
    }
}

static void ReservedSwitchInjectionRejected()
{
    foreach (var argument in new[]
    {
        "-projectPath",
        "-PROJECTPATH=C:\\Other",
        "-logFile",
        "-executeMethod",
        "-runTests",
        "-testPlatform=PlayMode",
        "-quit",
    })
    {
        Throws<ArgumentException>(
            () => _ = new UnityExecuteMethodAction("Fixture.Runner.Execute", new[] { argument }),
            $"reserved switch '{argument}'");
    }
}

static void InvalidExecuteMethodRejected()
{
    foreach (var methodName in new[]
    {
        "RunOnly",
        " Namespace.Type.Run",
        "Namespace.Type.Run ",
        "Namespace.Type.Run()",
        "Namespace.Type.Run;Delete",
        "Namespace..Run",
    })
    {
        Throws<ArgumentException>(
            () => _ = new UnityExecuteMethodAction(methodName),
            $"invalid method '{methodName}'");
    }
}

static void RequestSnapshotsMutableInputs()
{
    var context = CreateProjectContext("Snapshot");
    var extras = new List<string> { "-fixtureArg", "before" };
    var environment = new List<KeyValuePair<string, string>>
    {
        new("SNAPSHOT", "before"),
    };
    var action = new UnityExecuteMethodAction("Fixture.Runner.Execute", extras);
    var request = new UnityCliCommandRequest(
        context,
        Path.Combine(Path.GetTempPath(), "Unity", "Editor", "Unity.exe"),
        Path.Combine(context.RootPath, "Logs", "snapshot.log"),
        action,
        environment: environment);

    extras[1] = "after";
    extras.Add("unexpected");
    environment[0] = new KeyValuePair<string, string>("SNAPSHOT", "after");
    environment.Add(new KeyValuePair<string, string>("EXTRA", "unexpected"));

    var invocation = RequireUnityInvocation(
        new UnityCliCommandBuilder().Build(new ToolIdentity("unity", "Unity Editor"), request));

    True(invocation.Arguments.Contains("before", StringComparer.Ordinal), "snapshotted extra argument missing");
    True(!invocation.Arguments.Contains("after", StringComparer.Ordinal), "mutable extra argument leaked");
    Equal("before", invocation.Environment["SNAPSHOT"], "snapshotted environment value");
    True(!invocation.Environment.ContainsKey("EXTRA"), "mutable environment addition leaked");
}

static void InvalidPathsRejected()
{
    var context = CreateProjectContext("Path validation");
    Throws<ArgumentException>(
        () => _ = new UnityCliCommandRequest(
            context,
            "Unity.exe",
            Path.Combine(context.RootPath, "Logs", "unity.log"),
            new UnityOpenProjectAction()),
        "relative editor executable");

    Throws<ArgumentException>(
        () => _ = new UnityCliCommandRequest(
            context,
            Path.Combine(Path.GetTempPath(), "not-unity.exe"),
            Path.Combine(context.RootPath, "Logs", "unity.log"),
            new UnityOpenProjectAction()),
        "wrong editor executable name");

    Throws<ArgumentException>(
        () => _ = new UnityRunTestsAction(UnityTestPlatform.EditMode, "results.xml"),
        "relative test results path");
}

static void DuplicateEnvironmentKeysRejected()
{
    var context = CreateProjectContext("Duplicate environment");
    var request = new UnityCliCommandRequest(
        context,
        Path.Combine(Path.GetTempPath(), "Unity", "Editor", "Unity.exe"),
        Path.Combine(context.RootPath, "Logs", "unity.log"),
        new UnityOpenProjectAction(),
        environment: new[]
        {
            new KeyValuePair<string, string>("UNITY_KEY", "one"),
            new KeyValuePair<string, string>("unity_key", "two"),
        });

    Throws<ArgumentException>(
        () => _ = new UnityCliCommandBuilder().Build(new ToolIdentity("unity", "Unity Editor"), request),
        "case-insensitive duplicate environment keys");
}

static void OptionalSwitches()
{
    var context = CreateProjectContext("Optional switches");
    var request = new UnityCliCommandRequest(
        context,
        Path.Combine(Path.GetTempPath(), "Unity", "Editor", "Unity.exe"),
        Path.Combine(context.RootPath, "Logs", "unity.log"),
        new UnityOpenProjectAction(),
        useNoGraphics: false,
        exitAfterOperation: false);

    var invocation = RequireUnityInvocation(
        new UnityCliCommandBuilder().Build(new ToolIdentity("unity", "Unity Editor"), request));
    True(!invocation.Arguments.Contains("-nographics", StringComparer.Ordinal), "nographics should be optional");
    True(!invocation.Arguments.Contains("-quit", StringComparer.Ordinal), "quit should be optional");
}

static ProjectContext CreateProjectContext(string suffix)
{
    var root = Path.Combine(Path.GetTempPath(), $"FCC P10-003 {suffix} {Guid.NewGuid():N}");
    return new ProjectContext(Guid.NewGuid(), root);
}

static UnityCliInvocation RequireUnityInvocation(ToolProcessRequest request)
{
    if (request.Invocation is not UnityCliInvocation invocation)
    {
        throw new InvalidOperationException("Expected UnityCliInvocation.");
    }

    return invocation;
}

static int IndexOf(IReadOnlyList<string> values, string expected)
{
    for (var index = 0; index < values.Count; index++)
    {
        if (string.Equals(values[index], expected, StringComparison.Ordinal))
        {
            return index;
        }
    }

    throw new InvalidOperationException($"Expected argv value '{expected}' was not found.");
}

static void SequenceEqual(
    IEnumerable<string> expected,
    IEnumerable<string> actual,
    string description)
{
    var expectedArray = expected.ToArray();
    var actualArray = actual.ToArray();
    if (!expectedArray.SequenceEqual(actualArray, StringComparer.Ordinal))
    {
        throw new InvalidOperationException(
            $"{description}: expected [{string.Join(" | ", expectedArray)}], actual [{string.Join(" | ", actualArray)}].");
    }
}

static void Equal<T>(T expected, T actual, string description)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{description}: expected '{expected}', actual '{actual}'.");
    }
}

static void True(bool condition, string description)
{
    if (!condition)
    {
        throw new InvalidOperationException(description);
    }
}

static void Throws<TException>(Action action, string description)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}: {description}.");
}

static void Run(string name, Action test, ICollection<string> failures)
{
    try
    {
        test();
        Console.WriteLine($"PASS: {name}");
    }
    catch (Exception exception)
    {
        failures.Add($"FAIL: {name}: {exception.GetType().Name}: {exception.Message}");
    }
}
