using System.Collections.ObjectModel;
using FCCCodeDesktop.Tools;

namespace FCCCodeDesktop.Tools.Unity;

/// <summary>
/// Immutable process plan for one project-owned Unity Editor automation entry point.
/// The result artifact and operation identity are part of the typed contract rather than ad-hoc caller arguments.
/// </summary>
public sealed record UnityEditorAutomationInvocationPlan
{
    internal UnityEditorAutomationInvocationPlan(
        ToolProcessRequest processRequest,
        Guid operationId,
        string methodName,
        string resultFilePath)
    {
        ProcessRequest = processRequest ?? throw new ArgumentNullException(nameof(processRequest));
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("Unity automation operation identity cannot be empty.", nameof(operationId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        ArgumentException.ThrowIfNullOrWhiteSpace(resultFilePath);

        OperationId = operationId;
        MethodName = methodName;
        ResultFilePath = resultFilePath;
    }

    public ToolProcessRequest ProcessRequest { get; }

    public Guid OperationId { get; }

    public string MethodName { get; }

    public string ResultFilePath { get; }
}

public interface IUnityEditorAutomationCommandBuilder
{
    UnityEditorAutomationInvocationPlan Build(
        ToolIdentity tool,
        ProjectContext project,
        string editorExecutablePath,
        string logFilePath,
        string methodName,
        Guid operationId,
        string resultFilePath,
        IEnumerable<string>? additionalArguments = null,
        IEnumerable<KeyValuePair<string, string>>? environment = null,
        ToolProcessCorrelation? correlation = null);
}

/// <summary>
/// Builds the P10 project-owned Editor automation contract on top of the strongly typed Unity CLI boundary.
/// Product-owned correlation/result switches are injected as discrete argv values and callers cannot override them.
/// </summary>
public sealed class UnityEditorAutomationCommandBuilder : IUnityEditorAutomationCommandBuilder
{
    public const string OperationIdSwitch = "-fccAutomationOperationId";
    public const string ResultFileSwitch = "-fccAutomationResult";

    private static readonly string[] AutomationOwnedSwitches =
    {
        OperationIdSwitch,
        ResultFileSwitch,
    };

    private readonly IUnityCliCommandBuilder _cliCommandBuilder;

    public UnityEditorAutomationCommandBuilder()
        : this(new UnityCliCommandBuilder())
    {
    }

    public UnityEditorAutomationCommandBuilder(IUnityCliCommandBuilder cliCommandBuilder)
    {
        _cliCommandBuilder = cliCommandBuilder ?? throw new ArgumentNullException(nameof(cliCommandBuilder));
    }

    public UnityEditorAutomationInvocationPlan Build(
        ToolIdentity tool,
        ProjectContext project,
        string editorExecutablePath,
        string logFilePath,
        string methodName,
        Guid operationId,
        string resultFilePath,
        IEnumerable<string>? additionalArguments = null,
        IEnumerable<KeyValuePair<string, string>>? environment = null,
        ToolProcessCorrelation? correlation = null)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(project);
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("Unity automation operation identity cannot be empty.", nameof(operationId));
        }

        // Reuse the P10-003 method-name validator rather than maintaining a second C# identifier grammar.
        var methodContract = new UnityExecuteMethodAction(methodName);
        var normalizedResultPath = NormalizeResultPath(resultFilePath);
        var callerArguments = SnapshotAdditionalArguments(additionalArguments);

        var arguments = new List<string>(4 + callerArguments.Count)
        {
            OperationIdSwitch,
            operationId.ToString("D"),
            ResultFileSwitch,
            normalizedResultPath,
        };
        arguments.AddRange(callerArguments);

        var action = new UnityExecuteMethodAction(methodContract.MethodName, arguments);
        var request = new UnityCliCommandRequest(
            project,
            editorExecutablePath,
            logFilePath,
            action,
            useNoGraphics: true,
            exitAfterOperation: true,
            environment: environment);

        var effectiveCorrelation = CreateCorrelation(operationId, correlation);
        var processRequest = _cliCommandBuilder.Build(tool, request, effectiveCorrelation);
        if (processRequest.Invocation is not UnityCliInvocation invocation ||
            !string.Equals(invocation.Operation, "unity.execute-method", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Unity Editor automation requires the typed unity.execute-method invocation contract.");
        }

        return new UnityEditorAutomationInvocationPlan(
            processRequest,
            operationId,
            methodContract.MethodName,
            normalizedResultPath);
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
                    "Unity Editor automation arguments must not contain null values.",
                    nameof(additionalArguments));
            }

            if (argument.Contains('\0'))
            {
                throw new ArgumentException(
                    "Unity Editor automation arguments must not contain NUL characters.",
                    nameof(additionalArguments));
            }

            if (AutomationOwnedSwitches.Any(owned => IsOwnedSwitch(argument, owned)))
            {
                throw new ArgumentException(
                    $"Unity Editor automation argument '{argument}' attempts to override a product-owned switch.",
                    nameof(additionalArguments));
            }

            snapshot.Add(argument);
        }

        return snapshot.AsReadOnly();
    }

    private static ToolProcessCorrelation CreateCorrelation(
        Guid operationId,
        ToolProcessCorrelation? correlation)
    {
        if (correlation?.OperationId is Guid existingOperationId && existingOperationId != operationId)
        {
            throw new ArgumentException(
                "Unity Editor automation correlation operation identity must match the invocation operation identity.",
                nameof(correlation));
        }

        return new ToolProcessCorrelation(
            correlation?.TaskId,
            correlation?.AgentRunId,
            correlation?.ToolRunId,
            correlation?.ProcessRunId,
            operationId);
    }

    private static bool IsOwnedSwitch(string argument, string ownedSwitch) =>
        string.Equals(argument, ownedSwitch, StringComparison.OrdinalIgnoreCase) ||
        argument.StartsWith(ownedSwitch + "=", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeResultPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!string.Equals(path, path.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Unity Editor automation result path must not contain leading or trailing whitespace.",
                nameof(path));
        }

        if (path.Contains('\0'))
        {
            throw new ArgumentException(
                "Unity Editor automation result path must not contain NUL characters.",
                nameof(path));
        }

        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException(
                "Unity Editor automation result path must be fully qualified.",
                nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        if (!string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Unity Editor automation result path must use a .json extension.",
                nameof(path));
        }

        return fullPath;
    }
}
