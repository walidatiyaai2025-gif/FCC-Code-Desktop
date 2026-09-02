using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class TemporaryDirectoryTests
{
    [Fact]
    public void DisposeRemovesWorkspaceWithUnicodeChild()
    {
        string workspacePath;

        using (var workspace = new TemporaryDirectory("fccd unit"))
        {
            workspacePath = workspace.Path;
            Assert.True(Directory.Exists(workspacePath));

            var filePath = workspace.GetPath(Path.Combine("nested space", "ملف.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, "FCC Code Desktop", System.Text.Encoding.UTF8);

            Assert.Equal("FCC Code Desktop", File.ReadAllText(filePath, System.Text.Encoding.UTF8));
        }

        Assert.False(Directory.Exists(workspacePath));
    }

    [Fact]
    public void GetPathRejectsTraversalOutsideWorkspace()
    {
        using var workspace = new TemporaryDirectory("fccd-unit-negative");

        Assert.Throws<ArgumentException>(() => workspace.GetPath(Path.Combine("..", "owner-data.txt")));
    }

    [Fact]
    public void PathThrowsAfterDispose()
    {
        var workspace = new TemporaryDirectory("fccd-unit-dispose");
        workspace.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = workspace.Path);
    }
}
