using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace FCCCodeDesktop.Tools.Unity;

public enum UnityLogSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Exception = 3,
    Assert = 4,
}

/// <summary>
/// One complete Unity log line captured from the project-owned -logFile target.
/// </summary>
public sealed record UnityLogEntry
{
    public UnityLogEntry(
        long sequence,
        UnityLogSeverity severity,
        string text,
        bool isTruncated,
        long truncatedUtf8Bytes,
        bool hadEncodingErrors)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);
        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity), severity, "A concrete Unity log severity is required.");
        }

        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(truncatedUtf8Bytes);

        Sequence = sequence;
        Severity = severity;
        Text = text;
        IsTruncated = isTruncated;
        TruncatedUtf8Bytes = truncatedUtf8Bytes;
        HadEncodingErrors = hadEncodingErrors;
    }

    public long Sequence { get; }

    public UnityLogSeverity Severity { get; }

    public string Text { get; }

    public bool IsTruncated { get; }

    public long TruncatedUtf8Bytes { get; }

    public bool HadEncodingErrors { get; }
}

/// <summary>
/// Immutable continuation token for incremental Unity log capture.
/// Pending bytes are internal so callers cannot mutate decoder state.
/// </summary>
public sealed class UnityLogCursor
{
    private readonly byte[] _pendingLineBytes;

    internal UnityLogCursor(
        long byteOffset,
        long nextSequence,
        long generation,
        byte[] pendingLineBytes,
        bool pendingWasTruncated,
        long pendingDroppedUtf8Bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(byteOffset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nextSequence);
        ArgumentOutOfRangeException.ThrowIfNegative(generation);
        ArgumentNullException.ThrowIfNull(pendingLineBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(pendingDroppedUtf8Bytes);

        ByteOffset = byteOffset;
        NextSequence = nextSequence;
        Generation = generation;
        _pendingLineBytes = pendingLineBytes.ToArray();
        PendingWasTruncated = pendingWasTruncated;
        PendingDroppedUtf8Bytes = pendingDroppedUtf8Bytes;
    }

    public static UnityLogCursor Start { get; } = new(0, 1, 0, Array.Empty<byte>(), false, 0);

    public long ByteOffset { get; }

    public long NextSequence { get; }

    public long Generation { get; }

    public bool HasPendingLine => _pendingLineBytes.Length > 0 || PendingWasTruncated;

    internal bool PendingWasTruncated { get; }

    internal long PendingDroppedUtf8Bytes { get; }

    internal byte[] SnapshotPendingLineBytes() => _pendingLineBytes.ToArray();
}

public sealed record UnityLogCaptureOptions
{
    public UnityLogCaptureOptions(
        int maxLineUtf8Bytes = 64 * 1024,
        int maxEntriesPerRead = 2_000,
        int readBufferBytes = 16 * 1024)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLineUtf8Bytes, 256);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxLineUtf8Bytes, 4 * 1024 * 1024);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEntriesPerRead, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxEntriesPerRead, 100_000);
        ArgumentOutOfRangeException.ThrowIfLessThan(readBufferBytes, 256);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(readBufferBytes, 1024 * 1024);

        MaxLineUtf8Bytes = maxLineUtf8Bytes;
        MaxEntriesPerRead = maxEntriesPerRead;
        ReadBufferBytes = readBufferBytes;
    }

    public int MaxLineUtf8Bytes { get; }

    public int MaxEntriesPerRead { get; }

    public int ReadBufferBytes { get; }
}

public sealed record UnityLogReadBatch
{
    private readonly ReadOnlyCollection<UnityLogEntry> _entries;

    internal UnityLogReadBatch(
        IEnumerable<UnityLogEntry> entries,
        UnityLogCursor cursor,
        bool fileExists,
        bool wasReset,
        bool reachedEntryLimit,
        long consumedUtf8Bytes)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
        ArgumentOutOfRangeException.ThrowIfNegative(consumedUtf8Bytes);

        _entries = Array.AsReadOnly(entries.ToArray());
        FileExists = fileExists;
        WasReset = wasReset;
        ReachedEntryLimit = reachedEntryLimit;
        ConsumedUtf8Bytes = consumedUtf8Bytes;
    }

    public IReadOnlyList<UnityLogEntry> Entries => _entries;

    public UnityLogCursor Cursor { get; }

    public bool FileExists { get; }

    public bool WasReset { get; }

    public bool ReachedEntryLimit { get; }

    public long ConsumedUtf8Bytes { get; }
}

public interface IUnityLogCaptureReader
{
    ValueTask<UnityLogReadBatch> ReadNewAsync(
        string logFilePath,
        UnityLogCursor? cursor = null,
        bool flushFinalLine = false,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Classifies Unity log lines without deciding compile/test/build success. Those outcomes belong to later P10 layers.
/// </summary>
public static class UnityLogParser
{
    private static readonly string[] TimestampFormats =
    {
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss.fff",
        "HH:mm:ss",
        "HH:mm:ss.fff",
    };

    public static UnityLogSeverity Classify(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var content = StripTimestamp(line).TrimStart();

        if (content.StartsWith("Assertion failed", StringComparison.OrdinalIgnoreCase) ||
            content.StartsWith("Assert:", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("UnityEngine.Assertions.AssertionException", StringComparison.OrdinalIgnoreCase))
        {
            return UnityLogSeverity.Assert;
        }

        if (LooksLikeException(content))
        {
            return UnityLogSeverity.Exception;
        }

        if (content.StartsWith("Error", StringComparison.OrdinalIgnoreCase) ||
            content.StartsWith("[Error]", StringComparison.OrdinalIgnoreCase) ||
            content.Contains(" error CS", StringComparison.OrdinalIgnoreCase) ||
            content.StartsWith("error CS", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("fatal error", StringComparison.OrdinalIgnoreCase))
        {
            return UnityLogSeverity.Error;
        }

        if (content.StartsWith("Warning", StringComparison.OrdinalIgnoreCase) ||
            content.StartsWith("[Warning]", StringComparison.OrdinalIgnoreCase) ||
            content.Contains(" warning CS", StringComparison.OrdinalIgnoreCase) ||
            content.StartsWith("warning CS", StringComparison.OrdinalIgnoreCase))
        {
            return UnityLogSeverity.Warning;
        }

        return UnityLogSeverity.Info;
    }

    private static bool LooksLikeException(string content)
    {
        if (content.Contains("Exception:", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("Unhandled exception", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var colon = content.IndexOf(':');
        if (colon <= 0)
        {
            return false;
        }

        var typeName = content[..colon].Trim();
        return typeName.EndsWith("Exception", StringComparison.OrdinalIgnoreCase);
    }

    private static string StripTimestamp(string line)
    {
        if (line.Length < 3 || line[0] != '[')
        {
            return line;
        }

        var closingBracket = line.IndexOf(']', 1);
        if (closingBracket is < 2 or > 32)
        {
            return line;
        }

        var candidate = line[1..closingBracket];
        if (!DateTime.TryParseExact(
                candidate,
                TimestampFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out _))
        {
            return line;
        }

        return line[(closingBracket + 1)..];
    }
}

/// <summary>
/// Incrementally reads the Unity -logFile target while Unity may still hold it open.
/// Complete lines are emitted once, partial UTF-8 lines survive polling boundaries, and truncation by length resets safely.
/// </summary>
public sealed class UnityLogCaptureReader : IUnityLogCaptureReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly UTF8Encoding ReplacementUtf8 = new(false, false);
    private readonly UnityLogCaptureOptions _options;

    public UnityLogCaptureReader(UnityLogCaptureOptions? options = null)
    {
        _options = options ?? new UnityLogCaptureOptions();
    }

    public async ValueTask<UnityLogReadBatch> ReadNewAsync(
        string logFilePath,
        UnityLogCursor? cursor = null,
        bool flushFinalLine = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedPath = NormalizeLogPath(logFilePath);
        var current = cursor ?? UnityLogCursor.Start;

        if (!File.Exists(normalizedPath))
        {
            return new UnityLogReadBatch(
                Array.Empty<UnityLogEntry>(),
                current,
                fileExists: false,
                wasReset: false,
                reachedEntryLimit: false,
                consumedUtf8Bytes: 0);
        }

        await using var stream = new FileStream(
            normalizedPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            _options.ReadBufferBytes,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var wasReset = stream.Length < current.ByteOffset;
        var byteOffset = wasReset ? 0 : current.ByteOffset;
        var generation = wasReset ? checked(current.Generation + 1) : current.Generation;
        var nextSequence = current.NextSequence;
        var pendingSnapshot = wasReset ? Array.Empty<byte>() : current.SnapshotPendingLineBytes();
        var pending = new List<byte>(pendingSnapshot.Length);
        var pendingWasTruncated = false;
        var pendingDroppedUtf8Bytes = 0L;

        if (!wasReset)
        {
            pending.AddRange(pendingSnapshot);
            pendingWasTruncated = current.PendingWasTruncated;
            pendingDroppedUtf8Bytes = current.PendingDroppedUtf8Bytes;
        }

        stream.Seek(byteOffset, SeekOrigin.Begin);
        var initialOffset = byteOffset;
        var entries = new List<UnityLogEntry>();
        var buffer = new byte[_options.ReadBufferBytes];
        var reachedEntryLimit = false;

        while (!reachedEntryLimit)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            for (var index = 0; index < bytesRead; index++)
            {
                byteOffset = checked(byteOffset + 1);
                var value = buffer[index];
                if (value != (byte)'\n')
                {
                    if (RetainedContentByteCount(pending) < _options.MaxLineUtf8Bytes)
                    {
                        pending.Add(value);
                    }
                    else
                    {
                        pendingWasTruncated = true;
                        pendingDroppedUtf8Bytes = checked(pendingDroppedUtf8Bytes + 1);
                    }

                    continue;
                }

                entries.Add(CreateEntry(
                    nextSequence,
                    pending,
                    pendingWasTruncated,
                    pendingDroppedUtf8Bytes));
                nextSequence = checked(nextSequence + 1);
                pending.Clear();
                pendingWasTruncated = false;
                pendingDroppedUtf8Bytes = 0;

                if (entries.Count >= _options.MaxEntriesPerRead)
                {
                    reachedEntryLimit = true;
                    break;
                }
            }
        }

        if (!reachedEntryLimit && flushFinalLine && (pending.Count > 0 || pendingWasTruncated))
        {
            entries.Add(CreateEntry(
                nextSequence,
                pending,
                pendingWasTruncated,
                pendingDroppedUtf8Bytes));
            nextSequence = checked(nextSequence + 1);
            pending.Clear();
            pendingWasTruncated = false;
            pendingDroppedUtf8Bytes = 0;
        }

        var nextCursor = new UnityLogCursor(
            byteOffset,
            nextSequence,
            generation,
            pending.ToArray(),
            pendingWasTruncated,
            pendingDroppedUtf8Bytes);

        return new UnityLogReadBatch(
            entries,
            nextCursor,
            fileExists: true,
            wasReset,
            reachedEntryLimit,
            consumedUtf8Bytes: byteOffset - initialOffset);
    }

    private static int RetainedContentByteCount(List<byte> pending) =>
        pending.Count - (HasUtf8Bom(pending) ? 3 : 0);

    private static bool HasUtf8Bom(List<byte> pending) =>
        pending.Count >= 3 &&
        pending[0] == 0xEF &&
        pending[1] == 0xBB &&
        pending[2] == 0xBF;

    private static UnityLogEntry CreateEntry(
        long sequence,
        List<byte> pending,
        bool wasTruncated,
        long droppedUtf8Bytes)
    {
        var count = pending.Count;
        if (count > 0 && pending[count - 1] == (byte)'\r')
        {
            count--;
        }

        var start = HasUtf8Bom(pending) ? 3 : 0;
        var bytes = pending.GetRange(start, count - start).ToArray();
        string text;
        var hadEncodingErrors = false;
        try
        {
            text = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            hadEncodingErrors = true;
            text = ReplacementUtf8.GetString(bytes);
        }

        return new UnityLogEntry(
            sequence,
            UnityLogParser.Classify(text),
            text,
            wasTruncated,
            droppedUtf8Bytes,
            hadEncodingErrors);
    }

    private static string NormalizeLogPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!string.Equals(path, path.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Unity log path must not contain leading or trailing whitespace.",
                nameof(path));
        }

        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("Unity log path must be fully qualified.", nameof(path));
        }

        if (path.Contains('\0'))
        {
            throw new ArgumentException("Unity log path must not contain NUL characters.", nameof(path));
        }

        return Path.GetFullPath(path);
    }
}
