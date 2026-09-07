using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FCCCodeDesktop.Runtime;

/// <summary>
/// Windows process lifecycle supervisor backed by a private Job Object per launched tree.
/// </summary>
public sealed class ProcessSupervisor : IProcessSupervisor
{
    public const int MaximumArgumentCount = 512;
    public const int MaximumArgumentCharacters = 32_000;
    public const int MaximumEnvironmentEntries = 256;
    public const int MaximumEnvironmentCharacters = 32_000;

    private const int MaximumFailureMessageCharacters = 2_048;
    private readonly ConcurrentDictionary<Guid, SupervisedProcess> _active = new();
    private int _disposed;

    public IReadOnlyList<OwnedProcessSnapshot> GetActiveProcesses() =>
        _active.Values
            .OrderBy(static item => item.StartedUtc)
            .Select(static item => item.CreateSnapshot())
            .ToArray();

    public Task<ProcessLaunchResult> StartAsync(
        ProcessLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        ValidateRequest(request);

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(
                new ProcessLaunchResult(
                    ProcessLaunchStatus.UnsupportedPlatform,
                    null,
                    "Owned Windows process-tree supervision requires Windows."));
        }

        if (!Directory.Exists(request.WorkingDirectory))
        {
            return Task.FromResult(
                new ProcessLaunchResult(
                    ProcessLaunchStatus.InvalidWorkingDirectory,
                    null,
                    "The requested process working directory does not exist."));
        }

        WindowsJobObject? job = null;
        Process? process = null;
        try
        {
            job = WindowsJobObject.Create();
            process = new Process
            {
                StartInfo = BuildStartInfo(request),
                EnableRaisingEvents = false,
            };

            if (!process.Start())
            {
                process.Dispose();
                job.Dispose();
                return Task.FromResult(
                    new ProcessLaunchResult(
                        ProcessLaunchStatus.StartFailed,
                        null,
                        "The process API returned without starting the requested executable."));
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                job.Assign(process);
            }
            catch
            {
                TryKillUnassignedTree(process);
                throw;
            }

            var ownershipId = Guid.NewGuid();
            var owned = new SupervisedProcess(
                ownershipId,
                process,
                job,
                DateTimeOffset.UtcNow,
                RemoveActive);
            process = null;
            job = null;

            if (!_active.TryAdd(ownershipId, owned))
            {
                owned.DisposeAsync().AsTask().GetAwaiter().GetResult();
                throw new InvalidOperationException("Failed to register a newly owned process tree.");
            }

            owned.BeginObservation();
            return Task.FromResult(
                new ProcessLaunchResult(ProcessLaunchStatus.Started, owned));
        }
        catch (OperationCanceledException)
        {
            if (process is not null)
            {
                TryKillUnassignedTree(process);
            }

            process?.Dispose();
            job?.Dispose();
            throw;
        }
        catch (Win32Exception exception)
        {
            process?.Dispose();
            job?.Dispose();
            return Task.FromResult(
                new ProcessLaunchResult(
                    ClassifyStartFailure(exception.NativeErrorCode),
                    null,
                    BoundFailureMessage(exception.Message)));
        }
        catch
        {
            process?.Dispose();
            job?.Dispose();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        var active = _active.Values.ToArray();
        foreach (var process in active)
        {
            await process.DisposeAsync().ConfigureAwait(false);
        }

        _active.Clear();
    }

    private static void ValidateRequest(ProcessLaunchRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);
        ArgumentNullException.ThrowIfNull(request.Arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkingDirectory);

        if (request.Arguments.Count > MaximumArgumentCount)
        {
            throw new ArgumentException(
                $"Process argument count exceeds the {MaximumArgumentCount}-argument safety limit.",
                nameof(request));
        }

        var argumentCharacters = 0;
        foreach (var argument in request.Arguments)
        {
            ArgumentNullException.ThrowIfNull(argument);
            argumentCharacters = checked(argumentCharacters + argument.Length);
            if (argumentCharacters > MaximumArgumentCharacters)
            {
                throw new ArgumentException(
                    $"Process arguments exceed the {MaximumArgumentCharacters}-character safety limit.",
                    nameof(request));
            }
        }

        if (request.Environment is null)
        {
            return;
        }

        if (request.Environment.Count > MaximumEnvironmentEntries)
        {
            throw new ArgumentException(
                $"Process environment overrides exceed the {MaximumEnvironmentEntries}-entry safety limit.",
                nameof(request));
        }

        var environmentCharacters = 0;
        foreach (var pair in request.Environment)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pair.Key);
            environmentCharacters = checked(
                environmentCharacters + pair.Key.Length + (pair.Value?.Length ?? 0));
            if (environmentCharacters > MaximumEnvironmentCharacters)
            {
                throw new ArgumentException(
                    $"Process environment overrides exceed the {MaximumEnvironmentCharacters}-character safety limit.",
                    nameof(request));
            }
        }
    }

    private static ProcessStartInfo BuildStartInfo(ProcessLaunchRequest request)
    {
        var startInfo = new ProcessStartInfo(request.FileName)
        {
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (request.Environment is not null)
        {
            foreach (var pair in request.Environment)
            {
                if (pair.Value is null)
                {
                    startInfo.Environment.Remove(pair.Key);
                }
                else
                {
                    startInfo.Environment[pair.Key] = pair.Value;
                }
            }
        }

        return startInfo;
    }

    private static ProcessLaunchStatus ClassifyStartFailure(int nativeErrorCode) =>
        nativeErrorCode switch
        {
            2 or 3 => ProcessLaunchStatus.ExecutableNotFound,
            5 => ProcessLaunchStatus.AccessDenied,
            _ => ProcessLaunchStatus.StartFailed,
        };

    private static string BoundFailureMessage(string message) =>
        message.Length <= MaximumFailureMessageCharacters
            ? message
            : message[..MaximumFailureMessageCharacters];

    private static void TryKillUnassignedTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between the state check and the cleanup request.
        }
        catch (Win32Exception)
        {
            // The original start/assignment failure remains authoritative.
        }
    }

    private void RemoveActive(Guid ownershipId) => _active.TryRemove(ownershipId, out _);

    private sealed class SupervisedProcess : ISupervisedProcess
    {
        private static readonly TimeSpan TreePollInterval = TimeSpan.FromMilliseconds(50);

        private readonly Process _root;
        private readonly WindowsJobObject _job;
        private readonly Action<Guid> _removeActive;
        private readonly TaskCompletionSource<OwnedProcessExit> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _observationStarted;
        private int _forcedTerminationRequested;
        private int _disposed;

        public SupervisedProcess(
            Guid ownershipId,
            Process root,
            WindowsJobObject job,
            DateTimeOffset startedUtc,
            Action<Guid> removeActive)
        {
            OwnershipId = ownershipId;
            _root = root;
            _job = job;
            StartedUtc = startedUtc;
            _removeActive = removeActive;
            RootProcessId = root.Id;
        }

        public Guid OwnershipId { get; }

        public int RootProcessId { get; }

        public DateTimeOffset StartedUtc { get; }

        public Task<OwnedProcessExit> Completion => _completion.Task;

        public void BeginObservation()
        {
            if (Interlocked.Exchange(ref _observationStarted, 1) == 0)
            {
                _ = ObserveTreeExitAsync();
            }
        }

        public OwnedProcessSnapshot CreateSnapshot()
        {
            bool rootHasExited;
            try
            {
                rootHasExited = _root.HasExited;
            }
            catch (InvalidOperationException)
            {
                rootHasExited = true;
            }

            return new OwnedProcessSnapshot(
                OwnershipId,
                RootProcessId,
                StartedUtc,
                rootHasExited);
        }

        public async ValueTask TerminateOwnedTreeAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_completion.Task.IsCompleted)
            {
                return;
            }

            Interlocked.Exchange(ref _forcedTerminationRequested, 1);
            try
            {
                _job.Terminate();
            }
            catch (Win32Exception) when (_completion.Task.IsCompleted || _job.GetActiveProcessCount() == 0)
            {
                // The tree completed concurrently with the termination request.
            }

            await _completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            try
            {
                if (!_completion.Task.IsCompleted)
                {
                    await TerminateOwnedTreeAsync(CancellationToken.None).ConfigureAwait(false);
                }
                else
                {
                    await _completion.Task.ConfigureAwait(false);
                }
            }
            finally
            {
                _job.Dispose();
                _root.Dispose();
                _removeActive(OwnershipId);
            }
        }

        private async Task ObserveTreeExitAsync()
        {
            try
            {
                await _root.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                var rootExitCode = _root.ExitCode;

                while (_job.GetActiveProcessCount() != 0)
                {
                    await Task.Delay(TreePollInterval, CancellationToken.None).ConfigureAwait(false);
                }

                _completion.TrySetResult(
                    new OwnedProcessExit(
                        OwnershipId,
                        RootProcessId,
                        rootExitCode,
                        StartedUtc,
                        DateTimeOffset.UtcNow,
                        Volatile.Read(ref _forcedTerminationRequested) != 0));
            }
            catch (Exception exception)
            {
                _completion.TrySetException(exception);
            }
            finally
            {
                _removeActive(OwnershipId);
            }
        }
    }

    private sealed class WindowsJobObject : IDisposable
    {
        private const uint JobObjectLimitKillOnJobClose = 0x00002000;
        private const int JobObjectBasicAccountingInformation = 1;
        private const int JobObjectExtendedLimitInformation = 9;

        private readonly SafeFileHandle _handle;

        private WindowsJobObject(SafeFileHandle handle)
        {
            _handle = handle;
        }

        public static WindowsJobObject Create()
        {
            var handle = NativeMethods.CreateJobObjectW(IntPtr.Zero, null);
            if (handle.IsInvalid)
            {
                handle.Dispose();
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create an owned process job object.");
            }

            var job = new WindowsJobObject(handle);
            try
            {
                job.EnableKillOnClose();
                return job;
            }
            catch
            {
                job.Dispose();
                throw;
            }
        }

        public void Assign(Process process)
        {
            if (!NativeMethods.AssignProcessToJobObject(_handle, process.Handle))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to assign the started process to its owned job object.");
            }
        }

        public uint GetActiveProcessCount()
        {
            var size = Marshal.SizeOf<BasicAccountingInformation>();
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                if (!NativeMethods.QueryInformationJobObject(
                    _handle,
                    JobObjectBasicAccountingInformation,
                    buffer,
                    (uint)size,
                    out _))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to query the owned process job object.");
                }

                return Marshal.PtrToStructure<BasicAccountingInformation>(buffer).ActiveProcesses;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public void Terminate()
        {
            if (!NativeMethods.TerminateJobObject(_handle, 1))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to terminate the owned process job object.");
            }
        }

        public void Dispose() => _handle.Dispose();

        private void EnableKillOnClose()
        {
            var information = new ExtendedLimitInformation
            {
                BasicLimitInformation = new BasicLimitInformation
                {
                    LimitFlags = JobObjectLimitKillOnJobClose,
                },
            };

            var size = Marshal.SizeOf<ExtendedLimitInformation>();
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(information, buffer, fDeleteOld: false);
                if (!NativeMethods.SetInformationJobObject(
                    _handle,
                    JobObjectExtendedLimitInformation,
                    buffer,
                    (uint)size))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to configure the owned process job object.");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BasicLimitInformation
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public nuint MinimumWorkingSetSize;
            public nuint MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public nuint Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ExtendedLimitInformation
        {
            public BasicLimitInformation BasicLimitInformation;
            public IoCounters IoInfo;
            public nuint ProcessMemoryLimit;
            public nuint JobMemoryLimit;
            public nuint PeakProcessMemoryUsed;
            public nuint PeakJobMemoryUsed;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BasicAccountingInformation
        {
            public long TotalUserTime;
            public long TotalKernelTime;
            public long ThisPeriodTotalUserTime;
            public long ThisPeriodTotalKernelTime;
            public uint TotalPageFaultCount;
            public uint TotalProcesses;
            public uint ActiveProcesses;
            public uint TotalTerminatedProcesses;
        }

        private static class NativeMethods
        {
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern SafeFileHandle CreateJobObjectW(IntPtr jobAttributes, string? name);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetInformationJobObject(
                SafeFileHandle job,
                int informationClass,
                IntPtr information,
                uint informationLength);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool QueryInformationJobObject(
                SafeFileHandle job,
                int informationClass,
                IntPtr information,
                uint informationLength,
                out uint returnLength);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool TerminateJobObject(SafeFileHandle job, uint exitCode);
        }
    }
}
