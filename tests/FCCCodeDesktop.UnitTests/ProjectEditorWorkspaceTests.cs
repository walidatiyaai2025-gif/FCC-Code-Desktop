using FCCCodeDesktop.Application.Projects;

namespace FCCCodeDesktop.UnitTests;

public sealed class ProjectEditorWorkspaceTests
{
    [Fact]
    public async Task OpenAsync_ReusesExistingTabForSameProjectFile()
    {
        var fixture = EditorFixture.Create("alpha.txt", "hello\nworld\n", ProjectNewLineStyle.Lf);
        var workspace = new ProjectEditorWorkspace(fixture.Service);

        var first = await workspace.OpenAsync(fixture.Root, fixture.Path);
        var second = await workspace.OpenAsync(fixture.Root, fixture.Path);

        Assert.Same(first, second);
        Assert.Single(workspace.Documents);
        Assert.Same(first, workspace.SelectedDocument);
        Assert.False(first.IsDirty);
    }

    [Fact]
    public async Task OpenAsync_NormalizesStableLineEndingsForEditorWithoutDirtying()
    {
        var fixture = EditorFixture.Create("alpha.txt", "one\ntwo\n", ProjectNewLineStyle.Lf);
        var workspace = new ProjectEditorWorkspace(fixture.Service);

        var document = await workspace.OpenAsync(fixture.Root, fixture.Path);

        Assert.Equal("one\r\ntwo\r\n", document.Text);
        Assert.Equal(ProjectNewLineStyle.Lf, document.NewLineStyle);
        Assert.False(document.IsDirty);
        Assert.True(document.EndsWithNewLine);
    }

    [Fact]
    public async Task SaveAsync_UsesObservedVersionEncodingAndOriginalNewLineStyle()
    {
        var fixture = EditorFixture.Create("alpha.txt", "one\ntwo\n", ProjectNewLineStyle.Lf);
        var workspace = new ProjectEditorWorkspace(fixture.Service);
        var document = await workspace.OpenAsync(fixture.Root, fixture.Path);
        document.Text = "one\r\ntwo changed\r\n";

        await workspace.SaveAsync(document);

        var write = Assert.IsType<ProjectTextFileWriteRequest>(fixture.Service.LastWriteRequest);
        Assert.Equal(fixture.OriginalVersion, write.ExpectedVersion);
        Assert.Equal(ProjectTextEncoding.Utf8, write.Encoding);
        Assert.Equal("one\ntwo changed\n", write.Text);
        Assert.False(document.IsDirty);
        Assert.False(document.HasConflict);
        Assert.NotEqual(fixture.OriginalVersion, document.Version);
        Assert.True(document.EndsWithNewLine);
    }

    [Fact]
    public async Task SaveAsync_ExternalConflictRetainsDirtyBuffer()
    {
        var fixture = EditorFixture.Create("alpha.txt", "before", ProjectNewLineStyle.None);
        fixture.Service.ConflictOnWrite = true;
        var workspace = new ProjectEditorWorkspace(fixture.Service);
        var document = await workspace.OpenAsync(fixture.Root, fixture.Path);
        document.Text = "after";

        await Assert.ThrowsAsync<ProjectFileConflictException>(() => workspace.SaveAsync(document));

        Assert.True(document.IsDirty);
        Assert.True(document.HasConflict);
        Assert.Equal("after", document.Text);
        Assert.True(workspace.HasError);
    }

    [Fact]
    public async Task ReloadAsync_DirtyBufferRequiresExplicitDiscard()
    {
        var fixture = EditorFixture.Create("alpha.txt", "before", ProjectNewLineStyle.None);
        var workspace = new ProjectEditorWorkspace(fixture.Service);
        var document = await workspace.OpenAsync(fixture.Root, fixture.Path);
        document.Text = "unsaved";
        fixture.Service.ReplaceSnapshot("disk changed", ProjectNewLineStyle.None);

        await Assert.ThrowsAsync<ProjectEditorDirtyException>(
            () => workspace.ReloadAsync(document, discardUnsavedChanges: false));
        Assert.Equal("unsaved", document.Text);

        await workspace.ReloadAsync(document, discardUnsavedChanges: true);

        Assert.Equal("disk changed", document.Text);
        Assert.False(document.IsDirty);
        Assert.False(document.HasConflict);
    }

    [Fact]
    public async Task Close_DirtyBufferRequiresExplicitDiscard()
    {
        var fixture = EditorFixture.Create("alpha.txt", "before", ProjectNewLineStyle.None);
        var workspace = new ProjectEditorWorkspace(fixture.Service);
        var document = await workspace.OpenAsync(fixture.Root, fixture.Path);
        document.Text = "unsaved";

        Assert.Throws<ProjectEditorDirtyException>(() => workspace.Close(document, discardUnsavedChanges: false));
        Assert.Single(workspace.Documents);

        workspace.Close(document, discardUnsavedChanges: true);
        Assert.Empty(workspace.Documents);
        Assert.Null(workspace.SelectedDocument);
    }

    [Fact]
    public async Task OpenAsync_BinaryAndOversizedFilesFailBeforeRead()
    {
        var binary = EditorFixture.Create("binary.dat", "ignored", ProjectNewLineStyle.None);
        binary.Service.ContentKind = ProjectFileContentKind.Binary;
        var workspace = new ProjectEditorWorkspace(binary.Service);

        await Assert.ThrowsAsync<ProjectEditorOpenException>(() => workspace.OpenAsync(binary.Root, binary.Path));
        Assert.Equal(0, binary.Service.ReadCount);

        var large = EditorFixture.Create("large.txt", "ignored", ProjectNewLineStyle.None);
        large.Service.ContentKind = ProjectFileContentKind.TooLarge;
        workspace = new ProjectEditorWorkspace(large.Service);

        await Assert.ThrowsAsync<ProjectEditorOpenException>(() => workspace.OpenAsync(large.Root, large.Path));
        Assert.Equal(0, large.Service.ReadCount);
    }

    [Fact]
    public async Task SetActiveProject_DoesNotRetargetExistingTabs()
    {
        var first = EditorFixture.Create("one.txt", "one", ProjectNewLineStyle.None);
        var workspace = new ProjectEditorWorkspace(first.Service);
        workspace.SetActiveProject(first.Root);
        var document = await workspace.OpenAsync(first.Root, first.Path);

        var otherRoot = Path.Combine(Path.GetTempPath(), "fcc-editor-other-project");
        workspace.SetActiveProject(otherRoot);
        document.Text = "edited";
        await workspace.SaveAsync(document);

        var write = Assert.IsType<ProjectTextFileWriteRequest>(first.Service.LastWriteRequest);
        Assert.Equal(first.Root, write.ProjectRootPath);
        Assert.Equal(first.Path, write.FilePath);
    }

    [Theory]
    [InlineData(ProjectNewLineStyle.CrLf, "a\r\nb\r\n")]
    [InlineData(ProjectNewLineStyle.Lf, "a\nb\n")]
    [InlineData(ProjectNewLineStyle.Cr, "a\rb\r")]
    public void NormalizeForSave_PreservesEstablishedSingleNewLinePolicy(
        ProjectNewLineStyle style,
        string expected)
    {
        var actual = ProjectEditorTextPolicy.NormalizeForSave("a\r\nb\r\n", style);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NormalizeForEditor_MixedLineEndingsRemainExact()
    {
        const string source = "a\r\nb\nc\rd";
        Assert.Equal(source, ProjectEditorTextPolicy.NormalizeForEditor(source, ProjectNewLineStyle.Mixed));
    }

    private sealed class EditorFixture
    {
        private EditorFixture(string root, string path, ProjectFileVersion version, FakeProjectFileService service)
        {
            Root = root;
            Path = path;
            OriginalVersion = version;
            Service = service;
        }

        public string Root { get; }
        public string Path { get; }
        public ProjectFileVersion OriginalVersion { get; }
        public FakeProjectFileService Service { get; }

        public static EditorFixture Create(string fileName, string text, ProjectNewLineStyle style)
        {
            var root = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "fcc-editor-project"));
            var path = System.IO.Path.Combine(root, fileName);
            var version = new ProjectFileVersion(text.Length, 100, "original");
            var snapshot = new ProjectTextFileSnapshot(
                root,
                path,
                fileName,
                text,
                ProjectTextEncoding.Utf8,
                style,
                text.EndsWith('\n') || text.EndsWith('\r'),
                version);
            return new EditorFixture(root, path, version, new FakeProjectFileService(snapshot));
        }
    }

    private sealed class FakeProjectFileService(ProjectTextFileSnapshot snapshot) : IProjectFileService
    {
        private ProjectTextFileSnapshot _snapshot = snapshot;
        private long _versionCounter = 200;

        public ProjectFileContentKind ContentKind { get; set; } = ProjectFileContentKind.Text;
        public bool ConflictOnWrite { get; set; }
        public int ReadCount { get; private set; }
        public ProjectTextFileWriteRequest? LastWriteRequest { get; private set; }

        public Task<ProjectFileInspection> InspectAsync(
            string projectRootPath,
            string filePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ProjectFileInspection(
                projectRootPath,
                filePath,
                _snapshot.RelativePath,
                _snapshot.Version.Length,
                ContentKind,
                ContentKind == ProjectFileContentKind.Text ? _snapshot.Text : null,
                false,
                240,
                ContentKind == ProjectFileContentKind.Text ? _snapshot.Encoding : null));
        }

        public Task<ProjectTextFileSnapshot> ReadTextAsync(
            string projectRootPath,
            string filePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadCount++;
            return Task.FromResult(_snapshot);
        }

        public Task<ProjectFileWriteResult> WriteTextAsync(
            ProjectTextFileWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastWriteRequest = request;
            if (ConflictOnWrite)
            {
                throw new ProjectFileConflictException("The file changed after it was opened.");
            }

            var version = new ProjectFileVersion(request.Text.Length, _versionCounter++, $"saved-{_versionCounter}");
            _snapshot = _snapshot with
            {
                Text = request.Text,
                Encoding = request.Encoding,
                Version = version,
            };
            return Task.FromResult(new ProjectFileWriteResult(request.FilePath, _snapshot.RelativePath, version));
        }

        public void ReplaceSnapshot(string text, ProjectNewLineStyle style)
        {
            var version = new ProjectFileVersion(text.Length, _versionCounter++, $"disk-{_versionCounter}");
            _snapshot = _snapshot with
            {
                Text = text,
                NewLineStyle = style,
                EndsWithNewLine = text.EndsWith('\n') || text.EndsWith('\r'),
                Version = version,
            };
        }
    }
}
