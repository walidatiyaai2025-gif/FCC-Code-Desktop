using FCCCodeDesktop.Runtime;

namespace FCCCodeDesktop.Fcc;

public interface IFccEnvironmentDiscovery
{
    Task<FccEnvironmentSnapshot> DiscoverAsync(CancellationToken cancellationToken = default);
    Task<FccLoopbackHealth> ProbeLoopbackHealthAsync(CancellationToken cancellationToken = default);
}

public enum FccServerState { NotStarted, Checking, Starting, Ready, Failed, Stopped }

public sealed record FccServerStatus(FccServerState State, string Message, bool OwnsServer = false);

/// <summary>Reuses an available loopback server or starts and owns one bounded, supervised process.</summary>
public sealed class FccServerLifecycle : IAsyncDisposable
{
    private readonly IFccEnvironmentDiscovery _discovery;
    private readonly IProcessSupervisor _supervisor;
    private readonly TimeSpan _startupTimeout;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private ISupervisedProcess? _ownedProcess;
    private bool _disposed;
    private FccServerStatus _status = new(FccServerState.NotStarted, "Preparing FCC…");

    public FccServerLifecycle(
        IFccEnvironmentDiscovery? discovery = null,
        IProcessSupervisor? supervisor = null,
        TimeSpan? startupTimeout = null)
    {
        _discovery = discovery ?? new FccEnvironmentDiscoveryService();
        _supervisor = supervisor ?? new ProcessSupervisor();
        _startupTimeout = startupTimeout ?? TimeSpan.FromSeconds(45);
        if (_startupTimeout <= TimeSpan.Zero || _startupTimeout > TimeSpan.FromMinutes(2))
        {
            throw new ArgumentOutOfRangeException(nameof(startupTimeout));
        }
    }

    public event EventHandler? StatusChanged;
    public FccServerStatus Status => Volatile.Read(ref _status);

    public async Task<FccEnvironmentSnapshot> EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            SetStatus(FccServerState.Checking, "Checking local FCC…");
            var snapshot = await _discovery.DiscoverAsync(linked.Token).ConfigureAwait(false);
            if (!snapshot.IsFccClaudeAvailable)
            {
                throw new InvalidOperationException("Install fcc-claude and make it available on PATH, then retry FCC startup.");
            }

            if (_ownedProcess?.Completion.IsCompleted == true)
            {
                await ReleaseOwnedProcessAsync().ConfigureAwait(false);
            }

            if (snapshot.LoopbackHealth.IsHealthy)
            {
                SetStatus(FccServerState.Ready, "Local FCC server ready", _ownedProcess is not null);
                return snapshot;
            }

            if (snapshot.LoopbackHealth.State == FccLoopbackHealthState.Unhealthy)
            {
                throw new InvalidOperationException("The local FCC endpoint responded with an error. Resolve its configuration and retry; another server will not be started over it.");
            }

            if (!snapshot.FccServer.IsFound)
            {
                throw new InvalidOperationException("Install fcc-server and make it available on PATH, then retry FCC startup.");
            }

            SetStatus(FccServerState.Starting, "Starting local FCC server…", _ownedProcess is not null);
            if (_ownedProcess is null)
            {
                var executable = snapshot.FccServer.ExecutablePath!;
                var arguments = Array.Empty<string>();
                IReadOnlyDictionary<string, string?>? environment = null;
                if (Path.GetExtension(executable).Equals(".cmd", StringComparison.OrdinalIgnoreCase)
                    || Path.GetExtension(executable).Equals(".bat", StringComparison.OrdinalIgnoreCase))
                {
                    environment = new Dictionary<string, string?> { ["FCCD_SERVER_EXECUTABLE"] = executable };
                    executable = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.System), "cmd.exe");
                    arguments = ["/D", "/S", "/C", "\"\"%FCCD_SERVER_EXECUTABLE%\"\""];
                }

                var launch = await _supervisor.StartAsync(new ProcessLaunchRequest(
                    executable, arguments, Path.GetDirectoryName(snapshot.FccServer.ExecutablePath!)!, environment), linked.Token).ConfigureAwait(false);
                if (!launch.IsStarted)
                {
                    throw new InvalidOperationException($"FCC server could not start ({launch.Status}). Check the installation and retry.");
                }
                _ownedProcess = launch.Process!;
            }

            using var startup = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
            startup.CancelAfter(_startupTimeout);
            try
            {
                while (true)
                {
                    startup.Token.ThrowIfCancellationRequested();
                    if (_ownedProcess.Completion.IsCompleted)
                    {
                        throw new InvalidOperationException("FCC server exited before becoming ready. Check its configuration and retry.");
                    }
                    var health = await _discovery.ProbeLoopbackHealthAsync(startup.Token).ConfigureAwait(false);
                    if (health.IsHealthy && !_ownedProcess.Completion.IsCompleted)
                    {
                        SetStatus(FccServerState.Ready, "Local FCC server ready", ownsServer: true);
                        return snapshot with { LoopbackHealth = health };
                    }
                    await Task.Delay(TimeSpan.FromMilliseconds(250), startup.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (!linked.IsCancellationRequested)
            {
                throw new InvalidOperationException("FCC server did not become ready within the startup deadline. Check its configuration and retry.");
            }
        }
        catch (OperationCanceledException)
        {
            await ReleaseOwnedProcessAsync().ConfigureAwait(false);
            SetStatus(FccServerState.Stopped, "FCC startup cancelled");
            throw;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            await ReleaseOwnedProcessAsync().ConfigureAwait(false);
            SetStatus(FccServerState.Failed, exception.Message);
            throw;
        }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync().ConfigureAwait(false);
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) { return; }
            _disposed = true;
            await ReleaseOwnedProcessAsync().ConfigureAwait(false);
            await _supervisor.DisposeAsync().ConfigureAwait(false);
            SetStatus(FccServerState.Stopped, "FCC stopped");
        }
        finally { _gate.Release(); }
    }

    private async ValueTask ReleaseOwnedProcessAsync()
    {
        if (_ownedProcess is not null)
        {
            await _ownedProcess.DisposeAsync().ConfigureAwait(false);
            _ownedProcess = null;
        }
    }

    private void SetStatus(FccServerState state, string message, bool ownsServer = false)
    {
        Volatile.Write(ref _status, new FccServerStatus(state, message, ownsServer));
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }
}
