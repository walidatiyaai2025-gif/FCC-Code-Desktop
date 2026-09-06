using System.Text;
using FCCCodeDesktop.Application.Projects;
using FCCCodeDesktop.Files;
using FCCCodeDesktop.Testing;
using Xunit;

namespace FCCCodeDesktop.IntegrationTests;

public sealed class ProjectFileServiceTests
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];
    private static readonly byte[] Utf16BigEndianBom = [0xFE, 0xFF];
    private static readonly byte[] InvalidUtf8Bytes = [0xC3, 0x28];

    [Fact]
    public async Task ReadsUtf8BomMetadataAndMixedNewLinesWithoutChangingSource()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-safe-file-read");
        var root = workspace.GetPath("مشروع safe files");
        Directory.CreateDirectory(root);
        var filePath = Path.Combine(root, "notes ütf8.txt");
        var originalBytes = Combine(Utf8Bom, Encoding.UTF8.GetBytes("alpha\r\nbeta\ngamma\r"));
        await File.WriteAllBytesAsync(filePath, originalBytes, CancellationToken.None);

        var snapshot = await new FileSystemProjectFileService()
            .ReadTextAsync(root, filePath, CancellationToken.None);

        Assert.Equal("notes ütf8.txt", snapshot.RelativePath);
        Assert.Equal("alpha\r\nbeta\ngamma\r", snapshot.Text);
        Assert.Equal(ProjectTextEncoding.Utf8WithBom, snapshot.Encoding);
        Assert.Equal(ProjectNewLineStyle.Mixed, snapshot.NewLineStyle);
        Assert.True(snapshot.EndsWithNewLine);
        Assert.Equal(originalBytes.LongLength, snapshot.Version.Length);
        Assert.Equal(64, snapshot.Version.Sha256.Length);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(filePath, CancellationToken.None));
    }

    [Fact]
    public async Task AtomicSavePreservesRequestedEncodingAndRequiresObservedVersion()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-safe-file-save");
        var root = workspace.GetPath("project with spaces");
        Directory.CreateDirectory(root);
        var filePath = Path.Combine(root, "document.txt");
        var service = new FileSystemProjectFileService();

        var created = await service.WriteTextAsync(
            new ProjectTextFileWriteRequest(
                root,
                filePath,
                "first\r\nline\r\n",
                ProjectTextEncoding.Utf16BigEndian),
            CancellationToken.None);
        var firstSnapshot = await service.ReadTextAsync(root, filePath, CancellationToken.None);

        Assert.Equal(ProjectTextEncoding.Utf16BigEndian, firstSnapshot.Encoding);
        Assert.Equal(ProjectNewLineStyle.CrLf, firstSnapshot.NewLineStyle);
        Assert.True(firstSnapshot.EndsWithNewLine);
        Assert.Equal(created.Version, firstSnapshot.Version);

        var saved = await service.WriteTextAsync(
            new ProjectTextFileWriteRequest(
                root,
                filePath,
                "second\r\nline\r\n",
                firstSnapshot.Encoding,
                firstSnapshot.Version),
            CancellationToken.None);
        var savedBytes = await File.ReadAllBytesAsync(filePath, CancellationToken.None);
        var savedSnapshot = await service.ReadTextAsync(root, filePath, CancellationToken.None);

        Assert.True(savedBytes.AsSpan().StartsWith(Utf16BigEndianBom));
        Assert.Equal("second\r\nline\r\n", savedSnapshot.Text);
        Assert.Equal(ProjectTextEncoding.Utf16BigEndian, savedSnapshot.Encoding);
        Assert.Equal(saved.Version, savedSnapshot.Version);
        Assert.Empty(Directory.EnumerateFiles(root, "*.fccd-*.tmp", SearchOption.TopDirectoryOnly));

        _ = await Assert.ThrowsAsync<ProjectFileConflictException>(() =>
            service.WriteTextAsync(
                new ProjectTextFileWriteRequest(
                    root,
                    filePath,
                    "unsafe overwrite",
                    ProjectTextEncoding.Utf8),
                CancellationToken.None));
        Assert.Equal("second\r\nline\r\n", (await service.ReadTextAsync(root, filePath, CancellationToken.None)).Text);
    }

    [Fact]
    public async Task RejectsStaleVersionWithoutOverwritingExternalWork()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-safe-file-conflict");
        var root = workspace.GetPath("project");
        Directory.CreateDirectory(root);
        var filePath = Path.Combine(root, "shared.txt");
        await File.WriteAllTextAsync(filePath, "initial", new UTF8Encoding(false), CancellationToken.None);
        var service = new FileSystemProjectFileService();
        var snapshot = await service.ReadTextAsync(root, filePath, CancellationToken.None);

        await File.WriteAllTextAsync(filePath, "external-owner-work", new UTF8Encoding(false), CancellationToken.None);

        var failure = await Assert.ThrowsAsync<ProjectFileConflictException>(() =>
            service.WriteTextAsync(
                new ProjectTextFileWriteRequest(
                    root,
                    filePath,
                    "agent-change",
                    snapshot.Encoding,
                    snapshot.Version),
                CancellationToken.None));

        Assert.Contains("changed", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("external-owner-work", await File.ReadAllTextAsync(filePath, CancellationToken.None));
        Assert.Empty(Directory.EnumerateFiles(root, "*.fccd-*.tmp", SearchOption.TopDirectoryOnly));
    }

    [Fact]
    public async Task RejectsOutsideRootDirectoryTargetsInvalidEncodingAndOversizedFiles()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-safe-file-negative");
        var root = workspace.GetPath("project");
        var outside = workspace.GetPath("outside");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        var service = new FileSystemProjectFileService(maximumFileBytes: 8);
        var outsidePath = Path.Combine(outside, "owner.txt");
        await File.WriteAllTextAsync(outsidePath, "owner-data", CancellationToken.None);

        var outsideFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReadTextAsync(root, outsidePath, CancellationToken.None));
        Assert.Contains("outside", outsideFailure.Message, StringComparison.OrdinalIgnoreCase);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.WriteTextAsync(
                new ProjectTextFileWriteRequest(root, root, "bad", ProjectTextEncoding.Utf8),
                CancellationToken.None));

        var invalidTextPath = Path.Combine(root, "binary.dat");
        await File.WriteAllBytesAsync(invalidTextPath, InvalidUtf8Bytes, CancellationToken.None);
        _ = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.ReadTextAsync(root, invalidTextPath, CancellationToken.None));

        var largePath = Path.Combine(root, "large.txt");
        await File.WriteAllTextAsync(largePath, "123456789", CancellationToken.None);
        _ = await Assert.ThrowsAsync<IOException>(() =>
            service.ReadTextAsync(root, largePath, CancellationToken.None));

        var oversizedWritePath = Path.Combine(root, "oversized-write.txt");
        _ = await Assert.ThrowsAsync<IOException>(() =>
            service.WriteTextAsync(
                new ProjectTextFileWriteRequest(
                    root,
                    oversizedWritePath,
                    "123456789",
                    ProjectTextEncoding.Utf8),
                CancellationToken.None));
        Assert.False(File.Exists(oversizedWritePath));

        Assert.Equal("owner-data", await File.ReadAllTextAsync(outsidePath, CancellationToken.None));
    }

    [Fact]
    public async Task RelativePathsCancellationAndConfigurationFailExplicitly()
    {
        using var workspace = new TemporaryDirectory("fccd-p06-safe-file-relative");
        var root = workspace.GetPath("project");
        var subdirectory = Path.Combine(root, "src");
        Directory.CreateDirectory(subdirectory);
        var service = new FileSystemProjectFileService();

        var created = await service.WriteTextAsync(
            new ProjectTextFileWriteRequest(
                root,
                Path.Combine("src", "relative.txt"),
                "hello\n",
                ProjectTextEncoding.Utf8),
            CancellationToken.None);
        Assert.Equal("src/relative.txt", created.RelativePath);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ReadTextAsync(root, created.FullPath, cancellation.Token));

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FileSystemProjectFileService(0));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FileSystemProjectFileService(FileSystemProjectFileService.MaximumSupportedFileBytes + 1));
    }

    private static byte[] Combine(byte[] prefix, byte[] payload)
    {
        var result = new byte[prefix.Length + payload.Length];
        prefix.CopyTo(result, 0);
        payload.CopyTo(result, prefix.Length);
        return result;
    }
}
