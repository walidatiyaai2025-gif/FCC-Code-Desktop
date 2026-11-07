using System.Diagnostics;

namespace FCCCodeDesktop.Runtime;

/// <summary>
/// A private kill-on-close Windows job for a process the caller has just started.
/// Interactive transports retain direct pipe access while preserving owned-tree cleanup.
/// Attach before sending requests that can launch tools or other child processes.
/// </summary>
public sealed class OwnedProcessTreeLease : IDisposable
{
    private readonly ProcessSupervisor.WindowsJobObject _job;

    public OwnedProcessTreeLease(Process ownedProcess)
    {
        ArgumentNullException.ThrowIfNull(ownedProcess);
        if (!OperatingSystem.IsWindows()) { throw new PlatformNotSupportedException("Owned process jobs require Windows."); }
        _job = ProcessSupervisor.WindowsJobObject.Create();
        try { _job.Assign(ownedProcess); }
        catch { _job.Dispose(); throw; }
    }

    public void Terminate()
    {
        if (_job.GetActiveProcessCount() != 0) { _job.Terminate(); }
    }

    public async Task WaitForExitAsync(CancellationToken cancellationToken)
    {
        while (_job.GetActiveProcessCount() != 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose() => _job.Dispose();
}
