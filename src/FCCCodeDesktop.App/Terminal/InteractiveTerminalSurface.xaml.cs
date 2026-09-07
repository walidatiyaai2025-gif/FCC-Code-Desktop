using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using FCCCodeDesktop.Application.Terminal;
using FCCCodeDesktop.Terminal;

namespace FCCCodeDesktop.App.Terminal;

public partial class InteractiveTerminalSurface : UserControl, IAsyncDisposable
{
    private const int MaximumTranscriptCharacters = 250_000;
    private static readonly Regex AnsiEscape = new(
        "\\x1B(?:\\[[0-?]*[ -/]*[@-~]|\\][^\\x07]*(?:\\x07|\\x1B\\\\))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IConPtyTerminalHost _terminalHost;
    private readonly IOptionalShellDetector _optionalShellDetector;
    private readonly StringBuilder _transcript = new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private IConPtyTerminalSession? _session;
    private CancellationTokenSource? _sessionCancellation;
    private CancellationTokenSource? _resizeCancellation;
    private Task? _outputPump;
    private bool _initialized;
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
        Loaded += OnLoaded;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Loaded -= OnLoaded;
        _resizeCancellation?.Cancel();
        _resizeCancellation?.Dispose();
        _resizeCancellation = null;
        await CloseSessionAsync("Closed").ConfigureAwait(true);
        _lifecycleGate.Dispose();
        GC.SuppressFinalize(this);
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
            _transcript.Clear();
            TerminalOutput.Clear();

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
                if (charactersUsed == 0)
                {
                    continue;
                }

                var text = new string(characters, 0, charactersUsed);
                await Dispatcher.InvokeAsync(
                    () => AppendTerminalText(text),
                    DispatcherPriority.Background,
                    cancellationToken);
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

    private void AppendTerminalText(string text)
    {
        var normalized = AnsiEscape.Replace(text, string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        _transcript.Append(normalized);
        if (_transcript.Length > MaximumTranscriptCharacters)
        {
            _transcript.Remove(0, _transcript.Length - MaximumTranscriptCharacters);
        }

        TerminalOutput.Text = _transcript.ToString();
        TerminalOutput.CaretIndex = TerminalOutput.Text.Length;
        TerminalOutput.ScrollToEnd();
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
                if (!string.IsNullOrEmpty(TerminalOutput.SelectedText))
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

        _resizeCancellation?.Cancel();
        _resizeCancellation?.Dispose();
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
}
