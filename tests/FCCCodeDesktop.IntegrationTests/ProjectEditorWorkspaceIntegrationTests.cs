using FCCCodeDesktop.Application.Projects;
using FCCCodeDesktop.Files;
using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.IntegrationTests;

public sealed class ProjectEditorWorkspaceIntegrationTests
{
    [Fact]
    public async Task RealFileServicePreservesUnicodePathEncodingNewLinesConflictAndReload()
    {
        using var fixture = new TemporaryDirectory("fccd-p06-editor-lifecycle");
        var root = fixture.GetPath("مشروع editor with spaces");
        Directory.CreateDirectory(root);
        var filePath = Path.Combine(root, "notes ünicode.txt");
        var service = new FileSystemProjectFileService();

        _ = await service.WriteTextAsync(
            new ProjectTextFileWriteRequest(
                root,
                filePath,
                "alpha\nbeta\n",
                ProjectTextEncoding.Utf16BigEndian),
            CancellationToken.None);

        var editor = new ProjectEditorWorkspace(service);
        editor.SetActiveProject(root);
        var document = await editor.OpenAsync(root, filePath, CancellationToken.None);

        Assert.Equal("notes ünicode.txt", document.RelativePath);
        Assert.Equal(ProjectTextEncoding.Utf16BigEndian, document.Encoding);
        Assert.Equal(ProjectNewLineStyle.Lf, document.NewLineStyle);
        Assert.Equal("alpha\r\nbeta\r\n", document.Text);
        Assert.False(document.IsDirty);

        document.Text = "alpha\r\nbeta changed\r\n";
        await editor.SaveAsync(document, CancellationToken.None);

        var saved = await service.ReadTextAsync(root, filePath, CancellationToken.None);
        Assert.Equal("alpha\nbeta changed\n", saved.Text);
        Assert.Equal(ProjectTextEncoding.Utf16BigEndian, saved.Encoding);
        Assert.Equal(ProjectNewLineStyle.Lf, saved.NewLineStyle);
        Assert.False(document.IsDirty);
        Assert.Equal(saved.Version, document.Version);

        _ = await service.WriteTextAsync(
            new ProjectTextFileWriteRequest(
                root,
                filePath,
                "external owner\n",
                saved.Encoding,
                saved.Version),
            CancellationToken.None);
        document.Text = "agent edit\r\n";

        _ = await Assert.ThrowsAsync<ProjectFileConflictException>(
            () => editor.SaveAsync(document, CancellationToken.None));
        Assert.True(document.IsDirty);
        Assert.True(document.HasConflict);
        Assert.Equal(
            "external owner\n",
            (await service.ReadTextAsync(root, filePath, CancellationToken.None)).Text);

        await editor.ReloadAsync(document, discardUnsavedChanges: true, CancellationToken.None);

        Assert.Equal("external owner\r\n", document.Text);
        Assert.Equal(ProjectTextEncoding.Utf16BigEndian, document.Encoding);
        Assert.Equal(ProjectNewLineStyle.Lf, document.NewLineStyle);
        Assert.False(document.IsDirty);
        Assert.False(document.HasConflict);
    }
}
