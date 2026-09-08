using System.Collections.ObjectModel;
using FCCCodeDesktop.Tools;

namespace FCCCodeDesktop.Tools.Unity;

/// <summary>
/// Unity build targets exposed through the first-class P10 build boundary.
/// Values map to documented Unity -buildTarget names rather than arbitrary caller strings.
/// </summary>
public enum UnityBuildTarget
{
    StandaloneWindows64 = 1,
    StandaloneLinux64 = 2,
    StandaloneOSX = 3,
    WebGL = 4,
    Android = 5,
    iOS = 6,
}

/// <summary>
/// Immutable plan for one project-owned Unity build operation.
/// The build target, result path and expected output artifact are product-owned contract values.
/// </summary>
public sealed record UnityBuildTargetInvocationPlan
{
    internal UnityBuildTargetInvocationPlan(
        UnityEditorAutomationInvocationPlan automationPlan,
        UnityBuildTarget target,
        string outputArtifactPath,
        ToolArtifactKind outputArtifactKind)
    {
        AutomationPlan = automationPlan ?? throw new ArgumentNullException(nameof(automationPlan));
        if (!Enum.IsDefined(target))
        {
            throw new ArgumentOutOfRangeException(nameof(target), target, "A supported Unity build target is required.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(outputArtifactPath);
        if (!Enum.IsDefined(outputArtifactKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outputArtifactKind),
                outputArtifactKind,
                "A supported output artifact kind is required.");
        }

        Target = target;
        OutputArtifactPath = outputArtifactPath;
        OutputArtifactKind = outputArtifactKind;
    }

    public UnityEditorAutomationInvocationPlan AutomationPlan { get; }

    public ToolProcessRequest ProcessRequest => AutomationPlan.ProcessRequest;

    public Guid OperationId => AutomationPlan.OperationId;

    public string MethodName => AutomationPlan.MethodName;

    public string ResultFilePath => AutomationPlan.ResultFilePath;

    public UnityBuildTarget Target { get; }

    public string OutputArtifactPath { get; }

    public ToolArtifactKind OutputArtifactKind { get; }
}

public interface IUnityBuildTargetCommandBuilder
{
    UnityBuildTargetInvocationPlan Build(
        ToolIdentity tool,
        ProjectContext project,
        string editorExecutablePath,
        string logFilePath,
        string methodName,
        Guid operationId,
        string resultFilePath,
        UnityBuildTarget target,
        string outputArtifactPath,
        ToolArtifactKind outputArtifactKind,
        IEnumerable<string>? additionalArguments = null,
        IEnumerable<KeyValuePair<string, string>>? environment = null,
        ToolProcessCorrelation? correlation = null);
}

/// <summary>
/// Builds the P10 first-class Unity build contract on top of the project-owned Editor automation boundary.
/// No shell string is constructed; build-specific switches are injected as discrete argv values and cannot be overridden.
/// </summary>
public sealed class UnityBuildTargetCommandBuilder : IUnityBuildTargetCommandBuilder
{
    public const string BuildTargetSwitch = "-buildTarget";
    public const string BuildOutputSwitch = "-fccBuildOutput";
    public const string BuildArtifactKindSwitch = "-fccBuildArtifactKind";

    private static readonly string[] BuildOwnedSwitches =
    {
        BuildTargetSwitch,
        BuildOutputSwitch,
        BuildArtifactKindSwitch,
    };

    private readonly IUnityEditorAutomationCommandBuilder _automationCommandBuilder;

    public UnityBuildTargetCommandBuilder()
        : this(new UnityEditorAutomationCommandBuilder())
    {
    }

    public UnityBuildTargetCommandBuilder(IUnityEditorAutomationCommandBuilder automationCommandBuilder)
    {
        _automationCommandBuilder = automationCommandBuilder ?? throw new ArgumentNullException(nameof(automationCommandBuilder));
    }

    public UnityBuildTargetInvocationPlan Build(
        ToolIdentity tool,
        ProjectContext project,
        string editorExecutablePath,
        string logFilePath,
        string methodName,
        Guid operationId,
        string resultFilePath,
        UnityBuildTarget target,
        string outputArtifactPath,
        ToolArtifactKind outputArtifactKind,
        IEnumerable<string>? additionalArguments = null,
        IEnumerable<KeyValuePair<string, string>>? environment = null,
        ToolProcessCorrelation? correlation = null)
    {
        if (!Enum.IsDefined(target))
        {
            throw new ArgumentOutOfRangeException(nameof(target), target, "A supported Unity build target is required.");
        }

        if (!Enum.IsDefined(outputArtifactKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outputArtifactKind),
                outputArtifactKind,
                "A supported output artifact kind is required.");
        }

        var normalizedOutputPath = NormalizeOutputPath(outputArtifactPath);
        ValidateTargetArtifactShape(target, normalizedOutputPath, outputArtifactKind);
        var callerArguments = SnapshotAdditionalArguments(additionalArguments);

        var arguments = new List<string>(6 + callerArguments.Count)
        {
            BuildTargetSwitch,
            GetCliTarget(target),
            BuildOutputSwitch,
            normalizedOutputPath,
            BuildArtifactKindSwitch,
            outputArtifactKind == ToolArtifactKind.File ? "file" : "directory",
        };
        arguments.AddRange(callerArguments);

        var automationPlan = _automationCommandBuilder.Build(
            tool,
            project,
            editorExecutablePath,
            logFilePath,
            methodName,
            operationId,
            resultFilePath,
            arguments,
            environment,
            correlation);

        return new UnityBuildTargetInvocationPlan(
            automationPlan,
            target,
            normalizedOutputPath,
            outputArtifactKind);
    }

    public static string GetCliTarget(UnityBuildTarget target) => target switch
    {
        UnityBuildTarget.StandaloneWindows64 => "StandaloneWindows64",
        UnityBuildTarget.StandaloneLinux64 => "StandaloneLinux64",
        UnityBuildTarget.StandaloneOSX => "StandaloneOSX",
        UnityBuildTarget.WebGL => "WebGL",
        UnityBuildTarget.Android => "Android",
        UnityBuildTarget.iOS => "iOS",
        _ => throw new ArgumentOutOfRangeException(nameof(target), target, "A supported Unity build target is required."),
    };

    private static ReadOnlyCollection<string> SnapshotAdditionalArguments(IEnumerable<string>? additionalArguments)
    {
        var snapshot = new List<string>();
        foreach (var argument in additionalArguments ?? Array.Empty<string>())
        {
            if (argument is null)
            {
                throw new ArgumentException("Unity build arguments must not contain null values.", nameof(additionalArguments));
            }

            if (argument.Contains('\0'))
            {
                throw new ArgumentException("Unity build arguments must not contain NUL characters.", nameof(additionalArguments));
            }

            if (BuildOwnedSwitches.Any(owned => IsOwnedSwitch(argument, owned)))
            {
                throw new ArgumentException(
                    $"Unity build argument '{argument}' attempts to override a product-owned switch.",
                    nameof(additionalArguments));
            }

            snapshot.Add(argument);
        }

        return snapshot.AsReadOnly();
    }

    private static bool IsOwnedSwitch(string argument, string ownedSwitch) =>
        string.Equals(argument, ownedSwitch, StringComparison.OrdinalIgnoreCase) ||
        argument.StartsWith(ownedSwitch + "=", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeOutputPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!string.Equals(path, path.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Unity build output path must not contain leading or trailing whitespace.", nameof(path));
        }

        if (path.Contains('\0'))
        {
            throw new ArgumentException("Unity build output path must not contain NUL characters.", nameof(path));
        }

        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("Unity build output path must be fully qualified.", nameof(path));
        }

        var fullPath = Path.GetFullPath(path);
        var fileName = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("Unity build output path must identify a concrete artifact beneath a parent directory.", nameof(path));
        }

        return fullPath;
    }

    private static void ValidateTargetArtifactShape(
        UnityBuildTarget target,
        string outputPath,
        ToolArtifactKind outputArtifactKind)
    {
        var expectedKind = target switch
        {
            UnityBuildTarget.StandaloneWindows64 or UnityBuildTarget.StandaloneLinux64 or UnityBuildTarget.Android => ToolArtifactKind.File,
            UnityBuildTarget.StandaloneOSX or UnityBuildTarget.WebGL or UnityBuildTarget.iOS => ToolArtifactKind.Directory,
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "A supported Unity build target is required."),
        };

        if (outputArtifactKind != expectedKind)
        {
            throw new ArgumentException(
                $"Unity build target '{GetCliTarget(target)}' requires output artifact kind '{expectedKind}'.",
                nameof(outputArtifactKind));
        }

        if (target == UnityBuildTarget.StandaloneWindows64 &&
            !string.Equals(Path.GetExtension(outputPath), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("StandaloneWindows64 output must use an .exe path.", nameof(outputPath));
        }

        if (target == UnityBuildTarget.Android)
        {
            var extension = Path.GetExtension(outputPath);
            if (!string.Equals(extension, ".apk", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".aab", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Android output must use an .apk or .aab path.", nameof(outputPath));
            }
        }
    }
}
