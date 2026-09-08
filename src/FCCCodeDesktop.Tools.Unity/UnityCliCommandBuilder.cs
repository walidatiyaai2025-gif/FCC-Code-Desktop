using System.Collections.ObjectModel;
using FCCCodeDesktop.Tools;

namespace FCCCodeDesktop.Tools.Unity;

/// <summary>
/// Supported Unity Test Framework platforms exposed by the typed CLI boundary.
/// </summary>
public enum UnityTestPlatform
{
    EditMode = 1,
    PlayMode = 2,
}

/// <summary>
/// Base type for Unity CLI actions. Concrete action records prevent illegal option combinations.
/// </summary>
public abstract record UnityCliAction
{
    protected UnityCliAction()
    {
    }
}

/// <summary>
/// Opens a project in batch mode without adding task-specific switches.
/// Later compile/build/recovery layers decide what constitutes success.
/// </summary>
public sealed record UnityOpenProjectAction : UnityCliAction;

/// <summary>
/// Invokes one project-owned static Unity Editor method while preserving extra arguments as discrete argv values.
/// </summary>
public sealed record UnityExecuteMethodAction : UnityCliAction
{
    private static readonly string[] ReservedSwitches =
    {
        "-batchmode",
        "-nographics",
        "-projectPath",
        "-logFile",
        "-timestamps",
        "-executeMethod",
        "-runTests",
        "-testPlatform",
        "-testResults",
        "-quit",
    };

    private readonly ReadOnlyCollection<string> _additionalArguments;

    public UnityExecuteMethodAction(
        string methodName,
        IEnumerable<string>? additionalArguments = null)
    {
        ValidateMethodName(methodName);
        MethodName = methodName;
        _additionalArguments = SnapshotAdditionalArguments(additionalArguments);
    }

    public string MethodName { get; }

    public IReadOnlyList<string> AdditionalArguments => _additionalArguments;

    private static void ValidateMethodName(string methodName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        if (!string.Equals(methodName, methodName.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Unity executeMethod name must not contain leading or trailing whitespace.",
                nameof(methodName));
        }

        if (methodName.Contains('\0'))
        {
            throw new ArgumentException(
                "Unity executeMethod name must not contain NUL characters.",
                nameof(methodName));
        }

        var segments = methodName.Split('.');
        if (segments.Length < 2 || segments.Any(static segment => !IsIdentifier(segment)))
        {
            throw new ArgumentException(
                "Unity executeMethod name must be a dot-qualified C# type and static method name.",
                nameof(methodName));
        }
    }

    private static bool IsIdentifier(string value)
    {
        if (value.Length == 0 || !(char.IsLetter(value[0]) || value[0] == '_'))
        {
            return false;
        }

        for (var index = 1; index < value.Length; index++)
        {
            if (!(char.IsLetterOrDigit(value[index]) || value[index] == '_'))
            {
                return false;
            }
        }

        return true;
    }

    private static ReadOnlyCollection<string> SnapshotAdditionalArguments(
        IEnumerable<string>? additionalArguments)
    {
        var snapshot = new List<string>();
        foreach (var argument in additionalArguments ?? Array.Empty<string>())
        {
            if (argument is null)
            {
                throw new ArgumentException(
                    "Unity additional arguments must not contain null values.",
                    nameof(additionalArguments));
            }

            if (argument.Contains('\0'))
            {
                throw new ArgumentException(
                    "Unity additional arguments must not contain NUL characters.",
                    nameof(additionalArguments));
            }

            if (ReservedSwitches.Any(reserved => IsReservedSwitch(argument, reserved)))
            {
                throw new ArgumentException(
                    $"Unity additional argument '{argument}' attempts to override a builder-owned switch.",
                    nameof(additionalArguments));
            }

            snapshot.Add(argument);
        }

        return snapshot.AsReadOnly();
    }

    private static bool IsReservedSwitch(string argument, string reservedSwitch) =>
        string.Equals(argument, reservedSwitch, StringComparison.OrdinalIgnoreCase) ||
        argument.StartsWith(reservedSwitch + "=", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Runs Unity Test Framework with a typed platform and an explicit structured result artifact.
/// </summary>
public sealed record UnityRunTestsAction : UnityCliAction
{
    public UnityRunTestsAction(UnityTestPlatform platform, string testResultsPath)
    {
        if (!Enum.IsDefined(platform))
        {
            throw new ArgumentOutOfRangeException(
                nameof(platform),
                platform,
                "A concrete Unity test platform is required.");
        }

        TestResultsPath = NormalizeFullyQualifiedPath(
            testResultsPath,
            nameof(testResultsPath),
            "Unity test result path");
        Platform = platform;
    }

    public UnityTestPlatform Platform { get; }

    public string TestResultsPath { get; }

    private static string NormalizeFullyQualifiedPath(
        string path,
        string parameterName,
        string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);
        if (!string.Equals(path, path.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"{displayName} must not contain leading or trailing whitespace.",
                parameterName);
        }

        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException($"{displayName} must be fully qualified.", parameterName);
        }

        if (path.Contains('\0'))
        {
            throw new ArgumentException($"{displayName} must not contain NUL characters.", parameterName);
        }

        return Path.GetFullPath(path);
    }
}

/// <summary>
/// Immutable request for building a Unity process command. Building is pure: no filesystem access and no process launch.
/// </summary>
public sealed record UnityCliCommandRequest
{
    private readonly ReadOnlyCollection<KeyValuePair<string, string>> _environment;

    public UnityCliCommandRequest(
        ProjectContext project,
        string editorExecutablePath,
        string logFilePath,
        UnityCliAction action,
        bool useNoGraphics = true,
        bool exitAfterOperation = true,
        IEnumerable<KeyValuePair<string, string>>? environment = null)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        Action = action ?? throw new ArgumentNullException(nameof(action));
        EditorExecutablePath = NormalizeEditorExecutable(editorExecutablePath);
        LogFilePath = NormalizeFullyQualifiedPath(logFilePath, nameof(logFilePath), "Unity log path");
        UseNoGraphics = useNoGraphics;
        ExitAfterOperation = exitAfterOperation;
        _environment = SnapshotEnvironment(environment);
    }

    public ProjectContext Project { get; }

    public string EditorExecutablePath { get; }

    public string LogFilePath { get; }

    public UnityCliAction Action { get; }

    public bool UseNoGraphics { get; }

    public bool ExitAfterOperation { get; }

    public IReadOnlyList<KeyValuePair<string, string>> Environment => _environment;

    private static string NormalizeEditorExecutable(string path)
    {
        var normalized = NormalizeFullyQualifiedPath(path, nameof(path), "Unity editor executable path");
        if (!string.Equals(Path.GetFileName(normalized), "Unity.exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Unity editor executable path must resolve to Unity.exe.",
                nameof(path));
        }

        return normalized;
    }

    private static string NormalizeFullyQualifiedPath(
        string path,
        string parameterName,
        string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);
        if (!string.Equals(path, path.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"{displayName} must not contain leading or trailing whitespace.",
                parameterName);
        }

        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException($"{displayName} must be fully qualified.", parameterName);
        }

        if (path.Contains('\0'))
        {
            throw new ArgumentException($"{displayName} must not contain NUL characters.", parameterName);
        }

        return Path.GetFullPath(path);
    }

    private static ReadOnlyCollection<KeyValuePair<string, string>> SnapshotEnvironment(
        IEnumerable<KeyValuePair<string, string>>? environment)
    {
        var snapshot = (environment ?? Array.Empty<KeyValuePair<string, string>>()).ToArray();
        return Array.AsReadOnly(snapshot);
    }
}

/// <summary>
/// Unity-specific structured invocation consumed by the provider-neutral P09 process runner.
/// </summary>
public sealed record UnityCliInvocation : StructuredToolInvocation
{
    internal UnityCliInvocation(
        ProjectContext project,
        string operation,
        IEnumerable<string> arguments,
        IEnumerable<KeyValuePair<string, string>> environment)
        : base(
            project,
            operation,
            arguments,
            workingDirectory: project.RootPath,
            environment: environment)
    {
    }
}

public interface IUnityCliCommandBuilder
{
    ToolProcessRequest Build(
        ToolIdentity tool,
        UnityCliCommandRequest request,
        ToolProcessCorrelation? correlation = null);
}

/// <summary>
/// Builds the documented Unity CLI baseline as ordered argv values without shell concatenation or manual quoting.
/// </summary>
public sealed class UnityCliCommandBuilder : IUnityCliCommandBuilder
{
    public ToolProcessRequest Build(
        ToolIdentity tool,
        UnityCliCommandRequest request,
        ToolProcessCorrelation? correlation = null)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(request);

        var arguments = new List<string>
        {
            "-batchmode",
        };

        if (request.UseNoGraphics)
        {
            arguments.Add("-nographics");
        }

        arguments.Add("-projectPath");
        arguments.Add(request.Project.RootPath);
        arguments.Add("-logFile");
        arguments.Add(request.LogFilePath);
        arguments.Add("-timestamps");

        var operation = AppendActionArguments(arguments, request.Action);

        if (request.ExitAfterOperation)
        {
            arguments.Add("-quit");
        }

        var invocation = new UnityCliInvocation(
            request.Project,
            operation,
            arguments,
            request.Environment);

        return new ToolProcessRequest(
            tool,
            invocation,
            request.EditorExecutablePath,
            correlation);
    }

    private static string AppendActionArguments(
        ICollection<string> arguments,
        UnityCliAction action)
    {
        switch (action)
        {
            case UnityOpenProjectAction:
                return "unity.open-project";

            case UnityExecuteMethodAction executeMethod:
                arguments.Add("-executeMethod");
                arguments.Add(executeMethod.MethodName);
                foreach (var argument in executeMethod.AdditionalArguments)
                {
                    arguments.Add(argument);
                }

                return "unity.execute-method";

            case UnityRunTestsAction runTests:
                arguments.Add("-runTests");
                arguments.Add("-testPlatform");
                arguments.Add(runTests.Platform.ToString());
                arguments.Add("-testResults");
                arguments.Add(runTests.TestResultsPath);
                return runTests.Platform == UnityTestPlatform.EditMode
                    ? "unity.run-tests.editmode"
                    : "unity.run-tests.playmode";

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(action),
                    action.GetType().FullName,
                    "Unsupported Unity CLI action type.");
        }
    }
}
