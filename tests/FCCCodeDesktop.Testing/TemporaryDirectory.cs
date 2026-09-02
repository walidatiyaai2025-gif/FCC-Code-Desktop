namespace FCCCodeDesktop.Testing;

public sealed class TemporaryDirectory : IDisposable
{
    private string? _path;

    public TemporaryDirectory(string prefix = "fccd-test")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        if (prefix.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("The temporary-directory prefix contains invalid file-name characters.", nameof(prefix));
        }

        _path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_path);
    }

    public string Path => _path ?? throw new ObjectDisposedException(nameof(TemporaryDirectory));

    public string GetPath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (System.IO.Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException("The path must be relative to the disposable workspace.", nameof(relativePath));
        }

        var root = Path;
        var candidate = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relativePath));
        var rootedPrefix = root.TrimEnd(
            System.IO.Path.DirectorySeparatorChar,
            System.IO.Path.AltDirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!candidate.StartsWith(rootedPrefix, comparison))
        {
            throw new ArgumentException("The path escapes the disposable workspace.", nameof(relativePath));
        }

        return candidate;
    }

    public void Dispose()
    {
        var path = Interlocked.Exchange(ref _path, null);
        if (path is null)
        {
            return;
        }

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        GC.SuppressFinalize(this);
    }
}
