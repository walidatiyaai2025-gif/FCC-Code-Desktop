using FCCCodeDesktop.Application.Projects;

namespace FCCCodeDesktop.Files;

public sealed class FileSystemProjectTechnologyDetectionService : IProjectTechnologyDetectionService
{
    public const int DefaultMaximumDepth = 3;
    public const int DefaultMaximumEntries = 4096;
    public const int MaximumSupportedDepth = 8;
    public const int MaximumSupportedEntries = 100_000;

    private static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".hg",
        ".svn",
        ".idea",
        ".vs",
        ".vscode",
        "__pycache__",
        "bin",
        "build",
        "dist",
        "Library",
        "Logs",
        "node_modules",
        "obj",
        "target",
        "Temp",
        "vendor",
    };

    private readonly int _maximumDepth;
    private readonly int _maximumEntries;

    public FileSystemProjectTechnologyDetectionService(
        int maximumDepth = DefaultMaximumDepth,
        int maximumEntries = DefaultMaximumEntries)
    {
        if (maximumDepth < 0 || maximumDepth > MaximumSupportedDepth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumDepth),
                maximumDepth,
                $"Technology scan depth must be between 0 and {MaximumSupportedDepth}.");
        }

        if (maximumEntries < 1 || maximumEntries > MaximumSupportedEntries)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumEntries),
                maximumEntries,
                $"Technology scan entry limit must be between 1 and {MaximumSupportedEntries}.");
        }

        _maximumDepth = maximumDepth;
        _maximumEntries = maximumEntries;
    }

    public Task<ProjectTechnologyScanResult> DetectAsync(
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Project root path is required.", nameof(rootPath));
        }

        return Task.Run(() => DetectCore(rootPath, cancellationToken), cancellationToken);
    }

    private ProjectTechnologyScanResult DetectCore(string rootPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedRootPath = Path.GetFullPath(rootPath);
        if (!Directory.Exists(normalizedRootPath))
        {
            throw new DirectoryNotFoundException($"Project folder does not exist: {normalizedRootPath}");
        }

        var detections = new Dictionary<string, ProjectTechnologyDetection>(StringComparer.OrdinalIgnoreCase);
        var pendingDirectories = new Queue<(string Path, int Depth)>();
        pendingDirectories.Enqueue((normalizedRootPath, 0));
        var entriesExamined = 0;
        var skippedPaths = 0;
        var limitReached = false;

        while (pendingDirectories.Count > 0 && !limitReached)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remainingCapacity = _maximumEntries - entriesExamined;
            if (remainingCapacity <= 0)
            {
                limitReached = true;
                break;
            }

            var (directoryPath, depth) = pendingDirectories.Dequeue();
            string[] entries;
            var directoryWasTruncated = false;
            try
            {
                var boundedEntries = Directory
                    .EnumerateFileSystemEntries(directoryPath)
                    .Take(remainingCapacity + 1)
                    .ToArray();
                directoryWasTruncated = boundedEntries.Length > remainingCapacity;
                entries = directoryWasTruncated
                    ? boundedEntries[..remainingCapacity]
                    : boundedEntries;
                Array.Sort(entries, StringComparer.OrdinalIgnoreCase);
            }
            catch (UnauthorizedAccessException)
            {
                skippedPaths++;
                continue;
            }
            catch (IOException)
            {
                skippedPaths++;
                continue;
            }

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entriesExamined >= _maximumEntries)
                {
                    limitReached = true;
                    break;
                }

                entriesExamined++;
                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(entry);
                }
                catch (UnauthorizedAccessException)
                {
                    skippedPaths++;
                    continue;
                }
                catch (IOException)
                {
                    skippedPaths++;
                    continue;
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    skippedPaths++;
                    continue;
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    var directoryName = Path.GetFileName(entry);
                    if (IgnoredDirectoryNames.Contains(directoryName))
                    {
                        skippedPaths++;
                        continue;
                    }

                    if (depth < _maximumDepth)
                    {
                        pendingDirectories.Enqueue((entry, depth + 1));
                    }
                    else
                    {
                        skippedPaths++;
                    }

                    continue;
                }

                var markerPath = Path.GetRelativePath(normalizedRootPath, entry).Replace('\\', '/');
                foreach (var detection in DetectMarker(markerPath))
                {
                    AddOrUpgradeDetection(detections, detection);
                }
            }

            if (directoryWasTruncated)
            {
                limitReached = true;
            }
        }

        var orderedDetections = detections.Values
            .OrderBy(detection => detection.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(detection => detection.Id, StringComparer.Ordinal)
            .ToArray();

        return new ProjectTechnologyScanResult(
            normalizedRootPath,
            orderedDetections,
            entriesExamined,
            skippedPaths,
            _maximumDepth,
            _maximumEntries,
            limitReached);
    }

    private static IEnumerable<ProjectTechnologyDetection> DetectMarker(string markerPath)
    {
        var fileName = Path.GetFileName(markerPath);
        var extension = Path.GetExtension(fileName);

        if (markerPath.EndsWith("ProjectSettings/ProjectVersion.txt", StringComparison.OrdinalIgnoreCase))
        {
            yield return Create("unity", "Unity", "Unity Editor", markerPath, ProjectTechnologyConfidence.Definitive);
        }

        if (extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".fsproj", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".vbproj", StringComparison.OrdinalIgnoreCase))
        {
            yield return Create("dotnet", ".NET", ".NET SDK", markerPath, ProjectTechnologyConfidence.Strong);
        }

        if (fileName.Equals("package.json", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("pnpm-workspace.yaml", StringComparison.OrdinalIgnoreCase))
        {
            yield return Create("nodejs", "Node.js", "Node.js package tooling", markerPath, ProjectTechnologyConfidence.Strong);
        }

        if (fileName.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("requirements.txt", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("setup.py", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("Pipfile", StringComparison.OrdinalIgnoreCase))
        {
            yield return Create("python", "Python", "Python", markerPath, ProjectTechnologyConfidence.Strong);
        }

        if (extension.Equals(".blend", StringComparison.OrdinalIgnoreCase))
        {
            yield return Create("blender", "Blender", "Blender", markerPath, ProjectTechnologyConfidence.Definitive);
        }

        if (fileName.Equals("pom.xml", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("build.gradle", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("build.gradle.kts", StringComparison.OrdinalIgnoreCase))
        {
            yield return Create("java", "Java/JVM", "JDK build tooling", markerPath, ProjectTechnologyConfidence.Strong);
        }

        if (fileName.Equals("Cargo.toml", StringComparison.OrdinalIgnoreCase))
        {
            yield return Create("rust", "Rust", "Rust toolchain", markerPath, ProjectTechnologyConfidence.Strong);
        }

        if (fileName.Equals("go.mod", StringComparison.OrdinalIgnoreCase))
        {
            yield return Create("go", "Go", "Go toolchain", markerPath, ProjectTechnologyConfidence.Strong);
        }

        if (fileName.Equals("composer.json", StringComparison.OrdinalIgnoreCase))
        {
            yield return Create("php", "PHP", "PHP / Composer", markerPath, ProjectTechnologyConfidence.Strong);
        }

        if (fileName.Equals("CMakeLists.txt", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".vcxproj", StringComparison.OrdinalIgnoreCase))
        {
            yield return Create("cpp", "C/C++", "CMake / C++ toolchain", markerPath, ProjectTechnologyConfidence.Marker);
        }
    }

    private static ProjectTechnologyDetection Create(
        string id,
        string displayName,
        string toolchain,
        string markerPath,
        ProjectTechnologyConfidence confidence) =>
        new(id, displayName, toolchain, markerPath, confidence);

    private static void AddOrUpgradeDetection(
        Dictionary<string, ProjectTechnologyDetection> detections,
        ProjectTechnologyDetection candidate)
    {
        if (!detections.TryGetValue(candidate.Id, out var existing)
            || candidate.Confidence > existing.Confidence
            || (candidate.Confidence == existing.Confidence
                && string.Compare(candidate.MarkerPath, existing.MarkerPath, StringComparison.OrdinalIgnoreCase) < 0))
        {
            detections[candidate.Id] = candidate;
        }
    }
}
