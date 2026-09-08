using System.Security.Cryptography;
using System.Text;
using FCCCodeDesktop.Tools;
using Xunit;

namespace FCCCodeDesktop.UnitTests;

public sealed class ToolArtifactValidationTests
{
    [Fact]
    public void ManifestSnapshotsDefinitionsAndRejectsAmbiguousDeclarations()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"fccd-p09-005-{Guid.NewGuid():N}"));
        var definitions = new List<ToolArtifactDefinition>
        {
            new("primary", Path.Combine("output", "result.bin"), ToolArtifactKind.File),
        };

        var manifest = new ToolArtifactManifest(root, definitions);
        definitions.Clear();

        Assert.Single(manifest.Artifacts);
        Assert.Equal("primary", manifest.Artifacts[0].Id);

        Assert.Throws<ArgumentException>(() => new ToolArtifactDefinition("bad", "../escape.bin", ToolArtifactKind.File));
        Assert.Throws<ArgumentException>(() => new ToolArtifactDefinition("bad", Path.GetFullPath("escape.bin"), ToolArtifactKind.File));
        Assert.Throws<ArgumentException>(() => new ToolArtifactDefinition("bad", $"a{Path.DirectorySeparatorChar}{Path.DirectorySeparatorChar}b", ToolArtifactKind.File));
        Assert.Throws<ArgumentException>(() => new ToolArtifactDefinition("bad", "file.bin", ToolArtifactKind.File, expectedSha256: "1234"));
        Assert.Throws<ArgumentException>(() => new ToolArtifactDefinition("bad", "folder", ToolArtifactKind.Directory, expectedSha256: new string('a', 64)));

        Assert.Throws<ArgumentException>(() => new ToolArtifactManifest(
            root,
            new[]
            {
                new ToolArtifactDefinition("same", "one.bin", ToolArtifactKind.File),
                new ToolArtifactDefinition("SAME", "two.bin", ToolArtifactKind.File),
            }));

        Assert.Throws<ArgumentException>(() => new ToolArtifactManifest(
            root,
            new[]
            {
                new ToolArtifactDefinition("one", "same.bin", ToolArtifactKind.File),
                new ToolArtifactDefinition("two", "SAME.BIN", ToolArtifactKind.File),
            }));
    }

    [Fact]
    public async Task ValidatorProducesStableFileAndDirectoryEvidence()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var outputDirectory = Directory.CreateDirectory(Path.Combine(root, "output"));
            var filePath = Path.Combine(outputDirectory.FullName, "result.bin");
            var bytes = Encoding.UTF8.GetBytes("validated-artifact");
            await File.WriteAllBytesAsync(filePath, bytes);
            var expectedHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

            var manifest = new ToolArtifactManifest(
                root,
                new[]
                {
                    new ToolArtifactDefinition(
                        "file",
                        Path.Combine("output", "result.bin"),
                        ToolArtifactKind.File,
                        requireNonEmpty: true,
                        expectedSha256: expectedHash.ToUpperInvariant()),
                    new ToolArtifactDefinition(
                        "directory",
                        "output",
                        ToolArtifactKind.Directory,
                        requireNonEmpty: true),
                });

            var validator = new ToolArtifactValidator();
            var report = await validator.ValidateAsync(manifest);

            Assert.True(report.Succeeded);
            Assert.Equal(2, report.Entries.Count);

            var file = Assert.Single(report.Entries.Where(entry => entry.Definition.Id == "file"));
            Assert.Equal(ToolArtifactValidationStatus.Valid, file.Status);
            Assert.Equal(bytes.LongLength, file.Length);
            Assert.Equal(expectedHash, file.Sha256);
            Assert.Null(file.Diagnostic);

            var directory = Assert.Single(report.Entries.Where(entry => entry.Definition.Id == "directory"));
            Assert.Equal(ToolArtifactValidationStatus.Valid, directory.Status);
            Assert.Null(directory.Length);
            Assert.Null(directory.Sha256);

            var toolEvent = new ToolArtifactValidationEvent(report);
            Assert.Same(report, toolEvent.Report);
            Assert.IsAssignableFrom<ToolEvent>(toolEvent);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ValidatorFailsMissingEmptyTypeAndHashMismatchArtifactsIndependently()
    {
        var root = CreateTemporaryRoot();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "directory"));
            await File.WriteAllBytesAsync(Path.Combine(root, "empty.bin"), Array.Empty<byte>());
            await File.WriteAllTextAsync(Path.Combine(root, "hash.bin"), "actual");

            var manifest = new ToolArtifactManifest(
                root,
                new[]
                {
                    new ToolArtifactDefinition("missing", "missing.bin", ToolArtifactKind.File),
                    new ToolArtifactDefinition("empty", "empty.bin", ToolArtifactKind.File, requireNonEmpty: true),
                    new ToolArtifactDefinition("type", "directory", ToolArtifactKind.File),
                    new ToolArtifactDefinition("hash", "hash.bin", ToolArtifactKind.File, expectedSha256: new string('0', 64)),
                });

            var report = await new ToolArtifactValidator().ValidateAsync(manifest);

            Assert.False(report.Succeeded);
            Assert.Equal(ToolArtifactValidationStatus.Missing, GetStatus(report, "missing"));
            Assert.Equal(ToolArtifactValidationStatus.Empty, GetStatus(report, "empty"));
            Assert.Equal(ToolArtifactValidationStatus.TypeMismatch, GetStatus(report, "type"));
            Assert.Equal(ToolArtifactValidationStatus.HashMismatch, GetStatus(report, "hash"));

            var hashEntry = Assert.Single(report.Entries.Where(entry => entry.Definition.Id == "hash"));
            Assert.NotNull(hashEntry.Sha256);
            Assert.Equal(64, hashEntry.Sha256!.Length);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ValidatorHonorsPreCancellationWithoutTouchingTheFilesystem()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"fccd-p09-005-{Guid.NewGuid():N}"));
        var manifest = new ToolArtifactManifest(
            root,
            new[] { new ToolArtifactDefinition("missing", "missing.bin", ToolArtifactKind.File) });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await new ToolArtifactValidator().ValidateAsync(manifest, cancellation.Token);
        });

        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task EmptyManifestIsExplicitlyValidAndProducesNoFilesystemWork()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"fccd-p09-005-{Guid.NewGuid():N}"));
        var manifest = new ToolArtifactManifest(root, Array.Empty<ToolArtifactDefinition>());

        var report = await new ToolArtifactValidator().ValidateAsync(manifest);

        Assert.True(report.Succeeded);
        Assert.Empty(report.Entries);
        Assert.False(Directory.Exists(root));
    }

    private static ToolArtifactValidationStatus GetStatus(ToolArtifactValidationReport report, string id) =>
        Assert.Single(report.Entries.Where(entry => entry.Definition.Id == id)).Status;

    private static string CreateTemporaryRoot()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"fccd-p09-005-{Guid.NewGuid():N}"));
        Directory.CreateDirectory(root);
        return root;
    }
}
