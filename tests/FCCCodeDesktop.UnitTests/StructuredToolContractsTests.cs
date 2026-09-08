using FCCCodeDesktop.Tools;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class StructuredToolContractsTests
{
    [Fact]
    public void StructuredInvocationPreservesOrderedHostileArgumentsAndSnapshotsInputs()
    {
        var context = CreateProjectContext();
        var arguments = new List<string>
        {
            "plain",
            "value with spaces",
            "\"quoted\"",
            "a&b|c;d",
            @"C:\path with spaces\tail\",
            string.Empty,
            "مرحبا-世界",
        };
        var environment = new Dictionary<string, string>
        {
            ["FCCD_MODE"] = "fixture",
            ["UNICODE_VALUE"] = "قيمة",
        };
        var workingDirectory = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), "fccd invocation مساحة"));

        var invocation = new FixtureInvocation(
            context,
            "fixture.execute",
            arguments,
            workingDirectory,
            environment);

        arguments[0] = "mutated";
        environment["FCCD_MODE"] = "mutated";

        Assert.Equal("fixture.execute", invocation.Operation);
        Assert.Equal(workingDirectory, invocation.WorkingDirectory);
        Assert.Equal(
            new[]
            {
                "plain",
                "value with spaces",
                "\"quoted\"",
                "a&b|c;d",
                @"C:\path with spaces\tail\",
                string.Empty,
                "مرحبا-世界",
            },
            invocation.Arguments);
        Assert.Equal("fixture", invocation.Environment["FCCD_MODE"]);
        Assert.Equal("قيمة", invocation.Environment["UNICODE_VALUE"]);
        Assert.Same(context, invocation.Project);
    }

    [Fact]
    public void StructuredInvocationDefaultsWorkingDirectoryAndEmptyCollectionsWithoutTouchingDisk()
    {
        var context = CreateProjectContext();
        var invocation = new FixtureInvocation(context, "fixture.inspect");

        Assert.Equal(context.RootPath, invocation.WorkingDirectory);
        Assert.Empty(invocation.Arguments);
        Assert.Empty(invocation.Environment);
        Assert.False(Directory.Exists(context.RootPath));
    }

    [Fact]
    public void StructuredInvocationRejectsInvalidOperationArgumentsAndWorkingDirectory()
    {
        var context = CreateProjectContext();
        var nulArgument = new List<string> { "ok", "bad\0arg" };
        var nullArgument = new List<string> { "ok", null! };

        Assert.Throws<ArgumentException>(() => new FixtureInvocation(context, " "));
        Assert.Throws<ArgumentException>(() => new FixtureInvocation(context, " fixture.execute"));
        Assert.Throws<ArgumentException>(() => new FixtureInvocation(context, "fixture\0execute"));
        Assert.Throws<ArgumentException>(() => new FixtureInvocation(context, "fixture.execute", nulArgument));
        Assert.Throws<ArgumentException>(() => new FixtureInvocation(context, "fixture.execute", nullArgument));
        Assert.Throws<ArgumentException>(() => new FixtureInvocation(context, "fixture.execute", workingDirectory: "."));
    }

    [Fact]
    public void StructuredInvocationRejectsUnsafeOrAmbiguousEnvironmentEntries()
    {
        var context = CreateProjectContext();
        var leadingWhitespace = new List<KeyValuePair<string, string>>
        {
            new(" BAD", "value"),
        };
        var equalsInName = new List<KeyValuePair<string, string>>
        {
            new("BAD=NAME", "value"),
        };
        var nulInValue = new List<KeyValuePair<string, string>>
        {
            new("BAD", "value\0tail"),
        };
        var duplicateIgnoringCase = new List<KeyValuePair<string, string>>
        {
            new("FCCD_MODE", "one"),
            new("fccd_mode", "two"),
        };

        Assert.Throws<ArgumentException>(() => new FixtureInvocation(
            context,
            "fixture.execute",
            environment: leadingWhitespace));
        Assert.Throws<ArgumentException>(() => new FixtureInvocation(
            context,
            "fixture.execute",
            environment: equalsInName));
        Assert.Throws<ArgumentException>(() => new FixtureInvocation(
            context,
            "fixture.execute",
            environment: nulInValue));
        Assert.Throws<ArgumentException>(() => new FixtureInvocation(
            context,
            "fixture.execute",
            environment: duplicateIgnoringCase));
    }

    [Fact]
    public void ToolResultRequiresTerminalStatusAndCarriesTypedCompletionEvent()
    {
        var result = new FixtureResult(ToolResultStatus.Succeeded, "completed");
        var toolEvent = new ToolResultEvent(result);

        Assert.Equal(ToolResultStatus.Succeeded, result.Status);
        Assert.Equal("completed", result.Summary);
        Assert.Same(result, toolEvent.Result);
        Assert.IsAssignableFrom<ToolEvent>(toolEvent);

        Assert.Throws<ArgumentOutOfRangeException>(() => new FixtureResult(ToolResultStatus.Unknown));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FixtureResult((ToolResultStatus)999));
        Assert.Throws<ArgumentException>(() => new FixtureResult(ToolResultStatus.Failed, " "));
        Assert.Throws<ArgumentNullException>(() => new ToolResultEvent(null!));
    }

    private static ProjectContext CreateProjectContext()
    {
        var rootPath = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), $"fccd-p09-003-{Guid.NewGuid():N}", "project مساحة"));
        return new ProjectContext(Guid.NewGuid(), rootPath);
    }

    private sealed record FixtureInvocation : StructuredToolInvocation
    {
        public FixtureInvocation(
            ProjectContext project,
            string operation,
            IEnumerable<string>? arguments = null,
            string? workingDirectory = null,
            IEnumerable<KeyValuePair<string, string>>? environment = null)
            : base(project, operation, arguments, workingDirectory, environment)
        {
        }
    }

    private sealed record FixtureResult : ToolResult
    {
        public FixtureResult(ToolResultStatus status, string? summary = null)
            : base(status, summary)
        {
        }
    }
}
