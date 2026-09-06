using FCCCodeDesktop.Application.Projects;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class ProjectEditorWorkspaceTestsConcurrency
{
    [Fact]
    public async Task ConcurrentOpenSameFileIsSerializedAndReusesSingleTab()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "fcc-editor-concurrency"));
        var path = Path.Combine(root, "alpha.txt");
        var version = new ProjectFileVersion(5, 100, "initial");
        var snapshot = new ProjectTextFileSnapshot(
            root,
            path,
            "alpha.txt",
            "hello",
            ProjectTextEncoding.Utf8,
            ProjectNewLineStyle.None,
            false,
            version);
        var service = new BlockingProjectFileService(snapshot);
        var workspace = new ProjectEditorWorkspace(service);

        var firstOpen = workspace.OpenAsync(root, path, CancellationToken.None);
        await service.InspectionStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var secondOpen = workspace.OpenAsync(root, path, CancellationToken.None);
        Assert.False(secondOpen.IsCompleted);
        Assert.True(workspace.IsBusy);

        service.ReleaseInspection.TrySetResult(true);
        var opened = await Task.WhenAll(firstOpen, secondOpen);

        Assert.Same(opened[0], opened[1]);
        Assert.Single(workspace.Documents);
        Assert.Equal(1, service.InspectCount);
        Assert.Equal(1, service.ReadCount);
        Assert.False(workspace.IsBusy);
    }

    private sealed class BlockingProjectFileService(ProjectTextFileSnapshot snapshot) : IProjectFileService
    {
        public TaskCompletionSource<bool> InspectionStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> ReleaseInspection { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int InspectCount { get; private set; }
        public int ReadCount { get; private set; }

        public async Task<ProjectFileInspection> InspectAsync(
            string projectRootPath,
            string filePath,
            CancellationToken cancellationToken = default)
        {
            InspectCount++;
            InspectionStarted.TrySetResult(true);
            await ReleaseInspection.Task.WaitAsync(cancellationToken);
            return new ProjectFileInspection(
                projectRootPath,
                filePath,
                snapshot.RelativePath,
                snapshot.Version.Length,
                ProjectFileContentKind.Text,
                snapshot.Text,
                false,
                240,
                snapshot.Encoding);
        }

        public Task<ProjectTextFileSnapshot> ReadTextAsync(
            string projectRootPath,
            string filePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadCount++;
            return Task.FromResult(snapshot);
        }

        public Task<ProjectFileWriteResult> WriteTextAsync(
            ProjectTextFileWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
