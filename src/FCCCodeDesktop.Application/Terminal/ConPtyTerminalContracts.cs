namespace FCCCodeDesktop.Application.Terminal;

public sealed record TerminalSize
{
    public const int MaximumDimension = short.MaxValue;

    public TerminalSize(int columns, int rows)
    {
        if (columns is < 1 or > MaximumDimension)
        {
            throw new ArgumentOutOfRangeException(
                nameof(columns),
                columns,
                $"Terminal columns must be between 1 and {MaximumDimension}.");
        }

        if (rows is < 1 or > MaximumDimension)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rows),
                rows,
                $"Terminal rows must be between 1 and {MaximumDimension}.");
        }

        Columns = columns;
        Rows = rows;
    }

    public int Columns { get; }

    public int Rows { get; }
}

public sealed class ConPtyLaunchRequest
{
    private readonly IReadOnlyList<string> _arguments;

    public ConPtyLaunchRequest(
        string executablePath,
        IEnumerable<string>? arguments,
        string workingDirectory,
        TerminalSize initialSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(initialSize);

        if (!Path.IsPathFullyQualified(executablePath))
        {
            throw new ArgumentException("The ConPTY executable path must be fully qualified.", nameof(executablePath));
        }

        if (!Path.IsPathFullyQualified(workingDirectory))
        {
            throw new ArgumentException("The ConPTY working directory must be fully qualified.", nameof(workingDirectory));
        }

        var materializedArguments = arguments?.ToArray() ?? [];
        foreach (var argument in materializedArguments)
        {
            if (argument is null)
            {
                throw new ArgumentException("ConPTY arguments cannot contain null values.", nameof(arguments));
            }
        }

        ExecutablePath = Path.GetFullPath(executablePath);
        WorkingDirectory = Path.GetFullPath(workingDirectory);
        InitialSize = initialSize;
        _arguments = Array.AsReadOnly(materializedArguments);
    }

    public string ExecutablePath { get; }

    public IReadOnlyList<string> Arguments => _arguments;

    public string WorkingDirectory { get; }

    public TerminalSize InitialSize { get; }
}

public interface IConPtyTerminalHost
{
    Task<IConPtyTerminalSession> StartAsync(
        ConPtyLaunchRequest request,
        CancellationToken cancellationToken = default);
}

public interface IConPtyTerminalSession : IAsyncDisposable
{
    int ProcessId { get; }

    Stream Input { get; }

    Stream Output { get; }

    TerminalSize Size { get; }

    Task<int> Completion { get; }

    ValueTask ResizeAsync(
        TerminalSize size,
        CancellationToken cancellationToken = default);
}
