using FCCCodeDesktop.Tools;
using FCCCodeDesktop.Tools.Unity;

return await UnityResourceLockingFixture.RunAsync();

internal static class UnityResourceLockingFixture
{
    private const string ProcessPrefix = "unity:process:";
    private const string ProjectPrefix = "unity:project:";

    public static async Task<int> RunAsync()
    {
        var failures = new List<string>();

        void Check(bool condition, string message)
        {
            if (!condition)
            {
                failures.Add(message);
            }
        }

        var provider = new UnityResourceLockProvider();
        var firstProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondProjectId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var thirdProjectId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        var firstRoot = Path.Combine(Path.GetTempPath(), "FCC P10-004", "مشروع Unity A");
        var secondRoot = Path.Combine(Path.GetTempPath(), "FCC P10-004", "Unity B");
        var thirdRoot = Path.Combine(Path.GetTempPath(), "FCC P10-004", "Unity C");

        var first = CreateInvocation(firstProjectId, firstRoot);
        var sameLogicalProjectDifferentRoot = CreateInvocation(firstProjectId, secondRoot);
        var samePhysicalProjectDifferentIdentity = CreateInvocation(secondProjectId, firstRoot);
        var samePhysicalProjectDifferentCasing = CreateInvocation(
            thirdProjectId,
            firstRoot.ToUpperInvariant() + Path.DirectorySeparatorChar);
        var independent = CreateInvocation(thirdProjectId, thirdRoot);

        var firstKeys = provider.GetResourceLockKeys(first).ToArray();
        var sameLogicalKeys = provider.GetResourceLockKeys(sameLogicalProjectDifferentRoot).ToArray();
        var samePhysicalKeys = provider.GetResourceLockKeys(samePhysicalProjectDifferentIdentity).ToArray();
        var sameCasingKeys = provider.GetResourceLockKeys(samePhysicalProjectDifferentCasing).ToArray();
        var independentKeys = provider.GetResourceLockKeys(independent).ToArray();

        Check(firstKeys.Length == 2, "Unity invocation must declare exactly two resource locks.");
        Check(firstKeys.Select(static key => key.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 2,
            "Unity process and project lock keys must be distinct.");

        var firstProcess = FindKey(firstKeys, ProcessPrefix);
        var firstProject = FindKey(firstKeys, ProjectPrefix);
        var sameLogicalProcess = FindKey(sameLogicalKeys, ProcessPrefix);
        var sameLogicalProject = FindKey(sameLogicalKeys, ProjectPrefix);
        var samePhysicalProcess = FindKey(samePhysicalKeys, ProcessPrefix);
        var samePhysicalProject = FindKey(samePhysicalKeys, ProjectPrefix);
        var sameCasingProject = FindKey(sameCasingKeys, ProjectPrefix);
        var independentProcess = FindKey(independentKeys, ProcessPrefix);
        var independentProject = FindKey(independentKeys, ProjectPrefix);

        Check(string.Equals(firstProcess.Value, sameLogicalProcess.Value, StringComparison.Ordinal),
            "The same logical project identity must reuse one Unity process lock.");
        Check(!string.Equals(firstProject.Value, sameLogicalProject.Value, StringComparison.Ordinal),
            "Different physical project roots must not share a project lock.");
        Check(!string.Equals(firstProcess.Value, samePhysicalProcess.Value, StringComparison.Ordinal),
            "Different logical project identities must not share a process lock.");
        Check(string.Equals(firstProject.Value, samePhysicalProject.Value, StringComparison.Ordinal),
            "The same physical project root must share one project lock across project identities.");
        Check(string.Equals(firstProject.Value, sameCasingProject.Value, StringComparison.Ordinal),
            "Windows path casing and a trailing separator must not create a second physical-project lock.");
        Check(!firstProject.Value.Contains(firstRoot, StringComparison.OrdinalIgnoreCase),
            "Physical project lock keys must not disclose the raw project path.");
        Check(!string.Equals(firstProcess.Value, independentProcess.Value, StringComparison.Ordinal) &&
              !string.Equals(firstProject.Value, independentProject.Value, StringComparison.Ordinal),
            "Independent Unity projects must not share process or project locks.");

        IExternalToolResourceLockProvider genericProvider = provider;
        ExpectThrows<ArgumentException>(
            () => genericProvider.GetResourceLockKeys(new ForeignInvocation(first.Project)),
            failures,
            "Unity lock provider must reject non-Unity invocations.");

        var manager = new ToolResourceLockManager();
        var coordinator = new UnityResourceLockCoordinator(manager, provider);

        await using (var firstLease = await coordinator.AcquireAsync(first))
        {
            Check(firstLease.Keys.Count == 2, "Acquired Unity lease must expose both held lock keys.");

            using (var cancellation = new CancellationTokenSource())
            {
                var blockedByProcess = coordinator
                    .AcquireAsync(sameLogicalProjectDifferentRoot, cancellation.Token)
                    .AsTask();
                Check(!blockedByProcess.IsCompleted,
                    "A second invocation for the same logical project must wait on the Unity process lock.");
                cancellation.Cancel();
                await ExpectCancelledAsync(
                    blockedByProcess,
                    failures,
                    "Cancelling a process-lock waiter must propagate cancellation.");
            }

            using (var cancellation = new CancellationTokenSource())
            {
                var blockedByProject = coordinator
                    .AcquireAsync(samePhysicalProjectDifferentIdentity, cancellation.Token)
                    .AsTask();
                Check(!blockedByProject.IsCompleted,
                    "A second identity targeting the same physical project must wait on the project lock.");
                cancellation.Cancel();
                await ExpectCancelledAsync(
                    blockedByProject,
                    failures,
                    "Cancelling a project-lock waiter must propagate cancellation.");
            }

            using var recoveryTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var recoveryInvocation = CreateInvocation(secondProjectId, secondRoot);
            await using var recoveryLease = await coordinator.AcquireAsync(recoveryInvocation, recoveryTimeout.Token);
            Check(recoveryLease.Keys.Count == 2,
                "Cancelled partial acquisition must release any earlier-acquired process lock.");

            using var independentTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await using var independentLease = await coordinator.AcquireAsync(independent, independentTimeout.Token);
            Check(independentLease.Keys.Count == 2,
                "Independent Unity projects must be able to acquire locks concurrently.");
        }

        using (var reacquireTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
        {
            await using var reacquiredLease = await coordinator.AcquireAsync(first, reacquireTimeout.Token);
            Check(reacquiredLease.Keys.Count == 2,
                "Disposing a Unity lease must release both process and project locks for reuse.");
        }

        if (failures.Count == 0)
        {
            Console.WriteLine("P10-004 Unity resource locking fixture: PASS");
            return 0;
        }

        Console.Error.WriteLine("P10-004 Unity resource locking fixture: FAIL");
        foreach (var failure in failures)
        {
            Console.Error.WriteLine(" - " + failure);
        }

        return 1;
    }

    private static UnityCliInvocation CreateInvocation(Guid projectId, string projectRoot)
    {
        var project = new ProjectContext(projectId, projectRoot);
        var editorPath = Path.Combine(Path.GetTempPath(), "Unity", "Editor", "Unity.exe");
        var logPath = Path.Combine(Path.GetTempPath(), "FCC P10-004", projectId.ToString("N") + ".log");
        var request = new UnityCliCommandRequest(
            project,
            editorPath,
            logPath,
            new UnityOpenProjectAction());
        var processRequest = new UnityCliCommandBuilder().Build(
            new ToolIdentity("unity", "Unity Editor"),
            request);

        return processRequest.Invocation as UnityCliInvocation
            ?? throw new InvalidOperationException("Unity command builder did not emit a Unity CLI invocation.");
    }

    private static ToolResourceLockKey FindKey(
        IEnumerable<ToolResourceLockKey> keys,
        string prefix)
    {
        return keys.Single(key => key.Value.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static void ExpectThrows<TException>(
        Action action,
        ICollection<string> failures,
        string message)
        where TException : Exception
    {
        try
        {
            action();
            failures.Add(message);
        }
        catch (TException)
        {
        }
    }

    private static async Task ExpectCancelledAsync(
        Task<ToolResourceLockLease> task,
        ICollection<string> failures,
        string message)
    {
        try
        {
            await task;
            failures.Add(message);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private sealed record ForeignInvocation : StructuredToolInvocation
    {
        public ForeignInvocation(ProjectContext project)
            : base(project, "fixture.foreign")
        {
        }
    }
}
