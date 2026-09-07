using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FCCCodeDesktop.Application.Terminal;
using FCCCodeDesktop.Terminal;

namespace FCCCodeDesktop.App.Terminal;

public partial class InteractiveTerminalSurface : UserControl, IAsyncDisposable
{
    private const int MaximumTranscriptCharacters = 250_000;
    private const int MaximumPendingOutputCharacters = 65_536;
    private const int MaximumCoalescedRunCharacters = 8_192;

    private readonly IConPtyTerminalHost _terminalHost;
    private readonly IOptionalShellDetector _optionalShellDetector;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly object _outputSync = new();
    private readonly Queue<string> _pendingOutput = new();
    private readonly LinkedList<RenderedRun> _renderedRuns = new();
    private readonly Paragraph _terminalParagraph;
    private IConPtyTerminalSession? _session;
    private CancellationTokenSource? _sessionCancellation;
    private CancellationTokenSource? _resizeCancellation;
    private Task? _outputPump;
    private int _pendingOutputCharacters;
    private int _outputFlushScheduled;
    private int _transcriptCharacters;
    private string _ansiCarry = string.Empty;
    private TerminalStyle _terminalStyle;
    private bool _pendingOutputWasTrimmed;
    private bool _initialized;
    private bool _disposeStarted;
    private bool _disposed;

    public InteractiveTerminalSurface()
        : this(new WindowsConPtyTerminalHost(), new WindowsOptionalShellDetector())
    {
    }

    internal InteractiveTerminalSurface(
        IConPtyTerminalHost terminalHost,
        IOptionalShellDetector optionalShellDetector)
    {
        _terminalHost = terminalHost ?? throw new ArgumentNullException(nameof(terminalHost));
        _optionalShellDetector = optionalShellDetector ?? throw new ArgumentNullException(nameof(optionalShellDetector));
        InitializeComponent();
        _terminalParagraph = new Paragraph { Margin = new Thickness(0) };
        TerminalOutput.Document.Blocks.Clear();
        TerminalOutput.Document.Blocks.Add(_terminalParagraph);
        Loaded += OnLoaded;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed || _disposeStarted)
        {
            return;
        }

        _disposeStarted = true;
        Loaded -= OnLoaded;
        CancelPendingResize();
        ClearPendingOutput();
        try
        {
            await CloseSessionAsync("Closed").ConfigureAwait(true);
            _disposed = true;
            _lifecycleGate.Dispose();
            GC.SuppressFinalize(this);
        }
        catch
        {
            _disposeStarted = false;
            throw;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized || _disposed)
        {
            return;
        }

        _initialized = true;
        await PopulateShellsAsync().ConfigureAwait(true);
    }

    private async Task PopulateShellsAsync()
    {
        var choices = new List<TerminalShellChoice>();
        var systemDirectory = Environment.SystemDirectory;
        var powerShell = Path.Combine(systemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        if (File.Exists(powerShell))
        {
            choices.Add(new TerminalShellChoice("PowerShell", powerShell, ["-NoLogo"]));
        }

        var comSpec = Environment.GetEnvironmentVariable("ComSpec");
        var commandPrompt = !string.IsNullOrWhiteSpace(comSpec) && Path.IsPathFullyQualified(comSpec)
            ? Path.GetFullPath(comSpec)
            : Path.Combine(systemDirectory, "cmd.exe");
        if (File.Exists(commandPrompt))
        {
            choices.Add(new TerminalShellChoice("Command Prompt", commandPrompt, []));
        }

        try
        {
            var optional = await _optionalShellDetector.DetectAsync().ConfigureAwait(true);
            foreach (var installation in optional.Installations)
            {
                switch (installation.Kind)
                {
                    case OptionalShellKind.GitBash:
                        choices.Add(new TerminalShellChoice("Git Bash", installation.ExecutablePath, ["--login", "-i"]));
                        break;
                    case OptionalShellKind.Wsl:
                        choices.Add(new TerminalShellChoice("WSL", installation.ExecutablePath, []));
                        break;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = $"Optional shell discovery unavailable: {exception.Message}";
        }

        ShellSelector.ItemsSource = choices;
        if (choices.Count > 0)
        {
            ShellSelector.SelectedIndex = 0;
            StatusText.Text = "Ready";
        }
        else
        {
            StartButton.IsEnabled = false;
            StatusText.Text = "No supported shell executable was found.";
        }
    }

    private async void OnStartClicked(object sender, RoutedEventArgs e) =>
        await StartSelectedSessionAsync().ConfigureAwait(true);

    private async void OnCloseClicked(object sender, RoutedEventArgs e) =>
        await CloseSessionAsync("Closed").ConfigureAwait(true);

    private void OnShellSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_session is not null)
        {
            StatusText.Text = "Shell selection changed. Press Start to restart with the selected shell.";
        }
    }

    private async Task StartSelectedSessionAsync()
    {
        if (_disposed || ShellSelector.SelectedItem is not TerminalShellChoice choice)
        {
            return;
        }

        await _lifecycleGate.WaitAsync().ConfigureAwait(true);
        try
        {
            await CloseSessionCoreAsync().ConfigureAwait(true);
            SetBusyState(true, "Starting…");
            ResetPresentation();

            var request = new ConPtyLaunchRequest(
                choice.ExecutablePath,
                choice.Arguments,
                ResolveWorkingDirectory(),
                MeasureTerminalSize());
            var cancellation = new CancellationTokenSource();
            IConPtyTerminalSession session;
            try
            {
                session = await _terminalHost.StartAsync(request, cancellation.Token).ConfigureAwait(true);
            }
            catch
            {
                cancellation.Dispose();
                throw;
            }

            _sessionCancellation = cancellation;
            _session = session;
            _outputPump = PumpOutputAsync(session, cancellation.Token);
            CloseButton.IsEnabled = true;
            StartButton.IsEnabled = true;
            ShellSelector.IsEnabled = true;
            StatusText.Text = $"Running {choice.DisplayName} · PID {session.ProcessId}";
            TerminalOutput.Focus();
            _ = ObserveCompletionAsync(session);
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or InvalidOperationException
                                           or PlatformNotSupportedException)
        {
            SetBusyState(false, $"Terminal failed: {exception.Message}");
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task ObserveCompletionAsync(IConPtyTerminalSession session)
    {
        try
        {
            var exitCode = await session.Completion.ConfigureAwait(true);
            if (ReferenceEquals(_session, session))
            {
                StatusText.Text = $"Exited with code {exitCode}";
                CloseButton.IsEnabled = true;
            }
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            if (ReferenceEquals(_session, session))
            {
                StatusText.Text = $"Terminal ended: {exception.Message}";
            }
        }
    }

    private async Task PumpOutputAsync(IConPtyTerminalSession session, CancellationToken cancellationToken)
    {
        var bytes = new byte[4096];
        var decoder = Encoding.UTF8.GetDecoder();
        var characters = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
        try
        {
            while (true)
            {
                var read = await session.Output.ReadAsync(bytes.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                decoder.Convert(
                    bytes,
                    0,
                    read,
                    characters,
                    0,
                    characters.Length,
                    flush: false,
                    out _,
                    out var charactersUsed,
                    out _);
                if (charactersUsed > 0)
                {
                    QueueTerminalOutput(new string(characters, 0, charactersUsed));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (IOException exception)
        {
            await Dispatcher.InvokeAsync(
                () => StatusText.Text = $"Terminal stream ended: {exception.Message}",
                DispatcherPriority.Background);
        }
    }

    private void QueueTerminalOutput(string text)
    {
        if (string.IsNullOrEmpty(text) || _disposed)
        {
            return;
        }

        lock (_outputSync)
        {
            _pendingOutput.Enqueue(text);
            _pendingOutputCharacters += text.Length;
            while (_pendingOutputCharacters > MaximumPendingOutputCharacters && _pendingOutput.Count > 1)
            {
                var dropped = _pendingOutput.Dequeue();
                _pendingOutputCharacters -= dropped.Length;
                _pendingOutputWasTrimmed = true;
            }
        }

        ScheduleOutputFlush();
    }

    private void ScheduleOutputFlush()
    {
        if (Interlocked.CompareExchange(ref _outputFlushScheduled, 1, 0) != 0)
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(FlushPendingOutput));
    }

    private void FlushPendingOutput()
    {
        try
        {
            List<string> chunks;
            bool wasTrimmed;
            lock (_outputSync)
            {
                chunks = new List<string>(_pendingOutput.Count);
                while (_pendingOutput.TryDequeue(out var chunk))
                {
                    chunks.Add(chunk);
                }

                _pendingOutputCharacters = 0;
                wasTrimmed = _pendingOutputWasTrimmed;
                _pendingOutputWasTrimmed = false;
            }

            if (_disposed)
            {
                return;
            }

            if (wasTrimmed)
            {
                _ansiCarry = string.Empty;
                _terminalStyle = default;
                AppendStyledText("[terminal output coalesced to protect UI responsiveness]\n", default);
            }

            foreach (var chunk in chunks)
            {
                AppendAnsiText(chunk);
            }

            TerminalOutput.CaretPosition = TerminalOutput.Document.ContentEnd;
            TerminalOutput.ScrollToEnd();
        }
        finally
        {
            Interlocked.Exchange(ref _outputFlushScheduled, 0);
            bool hasPending;
            lock (_outputSync)
            {
                hasPending = _pendingOutput.Count > 0;
            }

            if (hasPending && !_disposed)
            {
                ScheduleOutputFlush();
            }
        }
    }

    private void AppendAnsiText(string text)
    {
        var input = (_ansiCarry + text)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        _ansiCarry = string.Empty;
        var plainStart = 0;
        var index = 0;
        while (index < input.Length)
        {
            if (input[index] != '\u001b')
            {
                index++;
                continue;
            }

            if (index > plainStart)
            {
                AppendStyledText(input[plainStart..index], _terminalStyle);
            }

            if (!TryConsumeEscapeSequence(input, index, out var nextIndex))
            {
                _ansiCarry = input[index..];
                return;
            }

            index = nextIndex;
            plainStart = index;
        }

        if (plainStart < input.Length)
        {
            AppendStyledText(input[plainStart..], _terminalStyle);
        }
    }

    private bool TryConsumeEscapeSequence(string input, int start, out int nextIndex)
    {
        nextIndex = start;
        if (start + 1 >= input.Length)
        {
            return false;
        }

        var introducer = input[start + 1];
        if (introducer == '[')
        {
            for (var index = start + 2; index < input.Length; index++)
            {
                var final = input[index];
                if (final is < '@' or > '~')
                {
                    continue;
                }

                if (final == 'm')
                {
                    ApplySgr(input.AsSpan(start + 2, index - start - 2));
                }

                nextIndex = index + 1;
                return true;
            }

            return false;
        }

        if (introducer == ']')
        {
            for (var index = start + 2; index < input.Length; index++)
            {
                if (input[index] == '\a')
                {
                    nextIndex = index + 1;
                    return true;
                }

                if (input[index] == '\u001b' && index + 1 < input.Length && input[index + 1] == '\\')
                {
                    nextIndex = index + 2;
                    return true;
                }
            }

            return false;
        }

        nextIndex = start + 2;
        return true;
    }

    private void ApplySgr(ReadOnlySpan<char> parameters)
    {
        var values = ParseSgr(parameters);
        for (var index = 0; index < values.Count; index++)
        {
            var code = values[index];
            switch (code)
            {
                case 0:
                    _terminalStyle = default;
                    break;
                case 1:
                    _terminalStyle = _terminalStyle with { Bold = true };
                    break;
                case 22:
                    _terminalStyle = _terminalStyle with { Bold = false };
                    break;
                case >= 30 and <= 37:
                    _terminalStyle = _terminalStyle with { Foreground = StandardAnsiColor(code - 30, bright: false) };
                    break;
                case 39:
                    _terminalStyle = _terminalStyle with { Foreground = null };
                    break;
                case >= 40 and <= 47:
                    _terminalStyle = _terminalStyle with { Background = StandardAnsiColor(code - 40, bright: false) };
                    break;
                case 49:
                    _terminalStyle = _terminalStyle with { Background = null };
                    break;
                case >= 90 and <= 97:
                    _terminalStyle = _terminalStyle with { Foreground = StandardAnsiColor(code - 90, bright: true) };
                    break;
                case >= 100 and <= 107:
                    _terminalStyle = _terminalStyle with { Background = StandardAnsiColor(code - 100, bright: true) };
                    break;
                case 38:
                case 48:
                    if (TryReadExtendedColor(values, ref index, out var color))
                    {
                        _terminalStyle = code == 38
                            ? _terminalStyle with { Foreground = color }
                            : _terminalStyle with { Background = color };
                    }

                    break;
            }
        }
    }

    private static List<int> ParseSgr(ReadOnlySpan<char> parameters)
    {
        var values = new List<int>();
        if (parameters.IsEmpty)
        {
            values.Add(0);
            return values;
        }

        foreach (var part in parameters.ToString().Split(';'))
        {
            values.Add(int.TryParse(part, out var value) ? value : 0);
        }

        return values;
    }

    private static bool TryReadExtendedColor(IReadOnlyList<int> values, ref int index, out Color color)
    {
        color = default;
        if (index + 2 < values.Count && values[index + 1] == 5)
        {
            color = Ansi256Color(Math.Clamp(values[index + 2], 0, 255));
            index += 2;
            return true;
        }

        if (index + 4 < values.Count && values[index + 1] == 2)
        {
            color = Color.FromRgb(
                (byte)Math.Clamp(values[index + 2], 0, 255),
                (byte)Math.Clamp(values[index + 3], 0, 255),
                (byte)Math.Clamp(values[index + 4], 0, 255));
            index += 4;
            return true;
        }

        return false;
    }

    private static Color Ansi256Color(int value)
    {
        if (value < 16)
        {
            return StandardAnsiColor(value % 8, value >= 8);
        }

        if (value < 232)
        {
            var cube = value - 16;
            var red = cube / 36;
            var green = (cube / 6) % 6;
            var blue = cube % 6;
            return Color.FromRgb(CubeComponent(red), CubeComponent(green), CubeComponent(blue));
        }

        var gray = (byte)(8 + ((value - 232) * 10));
        return Color.FromRgb(gray, gray, gray);
    }

    private static byte CubeComponent(int value) => value == 0 ? (byte)0 : (byte)(55 + (value * 40));

    private static Color StandardAnsiColor(int index, bool bright)
    {
        var normal = new[]
        {
            Color.FromRgb(0, 0, 0),
            Color.FromRgb(205, 49, 49),
            Color.FromRgb(13, 188, 121),
            Color.FromRgb(229, 229, 16),
            Color.FromRgb(36, 114, 200),
            Color.FromRgb(188, 63, 188),
            Color.FromRgb(17, 168, 205),
            Color.FromRgb(229, 229, 229),
        };
        var intense = new[]
        {
            Color.FromRgb(102, 102, 102),
            Color.FromRgb(241, 76, 76),
            Color.FromRgb(35, 209, 139),
            Color.FromRgb(245, 245, 67),
            Color.FromRgb(59, 142, 234),
            Color.FromRgb(214, 112, 214),
            Color.FromRgb(41, 184, 219),
            Color.FromRgb(255, 255, 255),
        };
        return (bright ? intense : normal)[Math.Clamp(index, 0, 7)];
    }

    private void AppendStyledText(string text, TerminalStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (text.Length > MaximumTranscriptCharacters)
        {
            text = text[^MaximumTranscriptCharacters..];
        }

        var last = _renderedRuns.Last?.Value;
        if (last is not null
            && last.Style.Equals(style)
            && last.Length + text.Length <= MaximumCoalescedRunCharacters)
        {
            last.Run.Text += text;
            last.Length += text.Length;
        }
        else
        {
            var run = new Run(text)
            {
                FontWeight = style.Bold ? FontWeights.Bold : FontWeights.Normal,
            };
            if (style.Foreground is Color foreground)
            {
                run.Foreground = new SolidColorBrush(foreground);
            }

            if (style.Background is Color background)
            {
                run.Background = new SolidColorBrush(background);
            }

            _terminalParagraph.Inlines.Add(run);
            _renderedRuns.AddLast(new RenderedRun(run, style, text.Length));
        }

        _transcriptCharacters += text.Length;
        TrimTranscript();
    }

    private void TrimTranscript()
    {
        var overflow = _transcriptCharacters - MaximumTranscriptCharacters;
        while (overflow > 0 && _renderedRuns.First is { } node)
        {
            var rendered = node.Value;
            if (rendered.Length <= overflow)
            {
                overflow -= rendered.Length;
                _transcriptCharacters -= rendered.Length;
                _terminalParagraph.Inlines.Remove(rendered.Run);
                _renderedRuns.RemoveFirst();
                continue;
            }

            rendered.Run.Text = rendered.Run.Text[overflow..];
            rendered.Length -= overflow;
            _transcriptCharacters -= overflow;
            overflow = 0;
        }
    }

    private async void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (_session is null || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        e.Handled = true;
        await SendInputAsync(e.Text).ConfigureAwait(true);
    }

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            if (e.Key == Key.C)
            {
                if (!string.IsNullOrEmpty(TerminalOutput.Selection.Text))
                {
                    return;
                }

                e.Handled = true;
                await SendInputAsync("\u0003").ConfigureAwait(true);
                return;
            }

            if (e.Key == Key.V)
            {
                e.Handled = true;
                if (Clipboard.ContainsText())
                {
                    await SendInputAsync(Clipboard.GetText()).ConfigureAwait(true);
                }

                return;
            }
        }

        var input = e.Key switch
        {
            Key.Enter => "\r",
            Key.Back => "\b",
            Key.Tab => "\t",
            Key.Escape => "\u001b",
            Key.Up => "\u001b[A",
            Key.Down => "\u001b[B",
            Key.Right => "\u001b[C",
            Key.Left => "\u001b[D",
            Key.Home => "\u001b[H",
            Key.End => "\u001b[F",
            Key.Delete => "\u001b[3~",
            Key.PageUp => "\u001b[5~",
            Key.PageDown => "\u001b[6~",
            _ => null,
        };
        if (input is null)
        {
            return;
        }

        e.Handled = true;
        await SendInputAsync(input).ConfigureAwait(true);
    }

    private async Task SendInputAsync(string text)
    {
        var session = _session;
        var cancellation = _sessionCancellation;
        if (session is null || cancellation is null || cancellation.IsCancellationRequested)
        {
            return;
        }

        try
        {
            var payload = Encoding.UTF8.GetBytes(text);
            await session.Input.WriteAsync(payload.AsMemory(), cancellation.Token).ConfigureAwait(true);
            await session.Input.FlushAsync(cancellation.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (IOException exception)
        {
            StatusText.Text = $"Terminal input failed: {exception.Message}";
        }
    }

    private void OnSurfaceSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_session is null || _disposed)
        {
            return;
        }

        CancelPendingResize();
        _resizeCancellation = new CancellationTokenSource();
        _ = ResizeAfterLayoutSettlesAsync(_resizeCancellation.Token);
    }

    private async Task ResizeAfterLayoutSettlesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(75, cancellationToken).ConfigureAwait(true);
            var session = _session;
            if (session is null)
            {
                return;
            }

            var size = MeasureTerminalSize();
            if (size.Columns != session.Size.Columns || size.Rows != session.Size.Rows)
            {
                await session.ResizeAsync(size, cancellationToken).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (InvalidOperationException exception)
        {
            StatusText.Text = $"Resize unavailable: {exception.Message}";
        }
    }

    private void CancelPendingResize()
    {
        _resizeCancellation?.Cancel();
        _resizeCancellation?.Dispose();
        _resizeCancellation = null;
    }

    private TerminalSize MeasureTerminalSize()
    {
        var width = Math.Max(160d, TerminalOutput.ActualWidth - 20d);
        var height = Math.Max(90d, TerminalOutput.ActualHeight - 20d);
        return new TerminalSize(
            Math.Clamp((int)Math.Floor(width / 8.0d), 20, 300),
            Math.Clamp((int)Math.Floor(height / 17.0d), 5, 120));
    }

    private async Task CloseSessionAsync(string status)
    {
        await _lifecycleGate.WaitAsync().ConfigureAwait(true);
        try
        {
            await CloseSessionCoreAsync().ConfigureAwait(true);
            SetBusyState(false, status);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task CloseSessionCoreAsync()
    {
        CancelPendingResize();
        var session = _session;
        var cancellation = _sessionCancellation;
        var outputPump = _outputPump;
        _session = null;
        _sessionCancellation = null;
        _outputPump = null;

        cancellation?.Cancel();
        if (session is not null)
        {
            await session.DisposeAsync().ConfigureAwait(true);
        }

        if (outputPump is not null)
        {
            try
            {
                await outputPump.ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
            }
        }

        cancellation?.Dispose();
        CloseButton.IsEnabled = false;
    }

    private void ResetPresentation()
    {
        ClearPendingOutput();
        _ansiCarry = string.Empty;
        _terminalStyle = default;
        _transcriptCharacters = 0;
        _renderedRuns.Clear();
        _terminalParagraph.Inlines.Clear();
    }

    private void ClearPendingOutput()
    {
        lock (_outputSync)
        {
            _pendingOutput.Clear();
            _pendingOutputCharacters = 0;
            _pendingOutputWasTrimmed = false;
        }
    }

    private void SetBusyState(bool busy, string status)
    {
        StartButton.IsEnabled = !busy && ShellSelector.Items.Count > 0;
        ShellSelector.IsEnabled = !busy;
        CloseButton.IsEnabled = _session is not null;
        StatusText.Text = status;
    }

    private static string ResolveWorkingDirectory()
    {
        var current = Environment.CurrentDirectory;
        if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
        {
            return Path.GetFullPath(current);
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile) && Directory.Exists(profile))
        {
            return Path.GetFullPath(profile);
        }

        return Path.GetPathRoot(Environment.SystemDirectory)
            ?? throw new InvalidOperationException("A terminal working directory could not be resolved.");
    }

    private sealed record TerminalShellChoice(
        string DisplayName,
        string ExecutablePath,
        IReadOnlyList<string> Arguments);

    private readonly record struct TerminalStyle(Color? Foreground, Color? Background, bool Bold);

    private sealed class RenderedRun(Run run, TerminalStyle style, int length)
    {
        public Run Run { get; } = run;

        public TerminalStyle Style { get; } = style;

        public int Length { get; set; } = length;
    }
}
