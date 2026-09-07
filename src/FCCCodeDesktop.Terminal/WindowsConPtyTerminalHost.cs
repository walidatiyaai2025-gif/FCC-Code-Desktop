using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FCCCodeDesktop.Application.Terminal;
using Microsoft.Win32.SafeHandles;

namespace FCCCodeDesktop.Terminal;

internal sealed class ConPtyException : InvalidOperationException
{
    internal ConPtyException(string operation, int nativeCode, string message)
        : base($"ConPTY operation '{operation}' failed with native code 0x{nativeCode:X8}: {message}")
    {
        Operation = operation;
        NativeCode = nativeCode;
    }

    internal string Operation { get; }

    internal int NativeCode { get; }
}

public sealed partial class WindowsConPtyTerminalHost : IConPtyTerminalHost
{
    private const uint ExtendedStartupInfoPresent = 0x00080000;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint CreateSuspended = 0x00000004;
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;
    private const int JobObjectExtendedLimitInformationClass = 9;
    private static readonly nuint ProcThreadAttributePseudoConsole = 0x00020016;

    public Task<IConPtyTerminalSession> StartAsync(
        ConPtyLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            throw new PlatformNotSupportedException(
                "Windows ConPTY requires Windows 10 version 1809 (build 17763) or newer.");
        }

        if (!File.Exists(request.ExecutablePath))
        {
            throw new FileNotFoundException("The ConPTY executable does not exist.", request.ExecutablePath);
        }

        if (!Directory.Exists(request.WorkingDirectory))
        {
            throw new DirectoryNotFoundException(
                $"The ConPTY working directory does not exist: {request.WorkingDirectory}");
        }

        var session = StartCore(request);
        if (cancellationToken.IsCancellationRequested)
        {
            session.DisposeAsync().AsTask().GetAwaiter().GetResult();
            cancellationToken.ThrowIfCancellationRequested();
        }

        return Task.FromResult<IConPtyTerminalSession>(session);
    }

    private static WindowsConPtyTerminalSession StartCore(ConPtyLaunchRequest request)
    {
        IntPtr pseudoInputRead = IntPtr.Zero;
        IntPtr hostInputWrite = IntPtr.Zero;
        IntPtr hostOutputRead = IntPtr.Zero;
        IntPtr pseudoOutputWrite = IntPtr.Zero;
        IntPtr attributeList = IntPtr.Zero;
        IntPtr commandLineBuffer = IntPtr.Zero;
        SafePseudoConsoleHandle? pseudoConsole = null;
        SafeKernelHandle? jobHandle = null;
        SafeKernelHandle? processHandle = null;
        SafeKernelHandle? threadHandle = null;
        Process? process = null;
        FileStream? input = null;
        FileStream? output = null;
        var processAssignedToJob = false;

        try
        {
            EnsureWin32(
                NativeMethods.CreatePipe(out pseudoInputRead, out hostInputWrite, IntPtr.Zero, 0),
                "CreatePipe(input)");
            EnsureWin32(
                NativeMethods.CreatePipe(out hostOutputRead, out pseudoOutputWrite, IntPtr.Zero, 0),
                "CreatePipe(output)");

            var createPseudoConsoleResult = NativeMethods.CreatePseudoConsole(
                ToCoord(request.InitialSize),
                pseudoInputRead,
                pseudoOutputWrite,
                0,
                out var pseudoConsoleRaw);
            EnsureHResult(createPseudoConsoleResult, "CreatePseudoConsole");
            pseudoConsole = new SafePseudoConsoleHandle(pseudoConsoleRaw);

            nuint attributeListSize = 0;
            _ = NativeMethods.InitializeProcThreadAttributeList(
                IntPtr.Zero,
                1,
                0,
                ref attributeListSize);
            if (attributeListSize == 0)
            {
                ThrowLastWin32("InitializeProcThreadAttributeList(size)");
            }

            attributeList = Marshal.AllocHGlobal(checked((nint)attributeListSize));
            EnsureWin32(
                NativeMethods.InitializeProcThreadAttributeList(
                    attributeList,
                    1,
                    0,
                    ref attributeListSize),
                "InitializeProcThreadAttributeList");

            EnsureWin32(
                NativeMethods.UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    ProcThreadAttributePseudoConsole,
                    pseudoConsole.DangerousGetHandle(),
                    (nuint)IntPtr.Size,
                    IntPtr.Zero,
                    IntPtr.Zero),
                "UpdateProcThreadAttribute(PSEUDOCONSOLE)");

            var startupInfo = new StartupInfoEx
            {
                StartupInfo = new StartupInfo
                {
                    Cb = Marshal.SizeOf<StartupInfoEx>(),
                },
                AttributeList = attributeList,
            };

            jobHandle = CreateKillOnCloseJob();
            commandLineBuffer = Marshal.StringToHGlobalUni(
                BuildCommandLine(request.ExecutablePath, request.Arguments));

            EnsureWin32(
                NativeMethods.CreateProcess(
                    request.ExecutablePath,
                    commandLineBuffer,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    ExtendedStartupInfoPresent | CreateUnicodeEnvironment | CreateSuspended,
                    IntPtr.Zero,
                    request.WorkingDirectory,
                    ref startupInfo,
                    out var processInformation),
                "CreateProcessW");

            processHandle = new SafeKernelHandle(processInformation.ProcessHandle);
            threadHandle = new SafeKernelHandle(processInformation.ThreadHandle);

            EnsureWin32(
                NativeMethods.AssignProcessToJobObject(
                    jobHandle.DangerousGetHandle(),
                    processHandle.DangerousGetHandle()),
                "AssignProcessToJobObject");
            processAssignedToJob = true;

            process = Process.GetProcessById(checked((int)processInformation.ProcessId));

            var resumeResult = NativeMethods.ResumeThread(threadHandle.DangerousGetHandle());
            if (resumeResult == uint.MaxValue)
            {
                ThrowLastWin32("ResumeThread");
            }

            // This host deliberately creates the child suspended so it can be put in
            // the kill-on-close Job Object before any user code executes. Keep the
            // PTY-side handles alive until the child has actually been resumed; only
            // then can its console initialization attach without a broken-pipe race.
            CloseRawHandle(ref pseudoInputRead);
            CloseRawHandle(ref pseudoOutputWrite);

            threadHandle.Dispose();
            threadHandle = null;

            input = new FileStream(
                new SafeFileHandle(hostInputWrite, ownsHandle: true),
                FileAccess.Write,
                bufferSize: 4096,
                isAsync: false);
            hostInputWrite = IntPtr.Zero;

            output = new FileStream(
                new SafeFileHandle(hostOutputRead, ownsHandle: true),
                FileAccess.Read,
                bufferSize: 4096,
                isAsync: false);
            hostOutputRead = IntPtr.Zero;

            var session = new WindowsConPtyTerminalSession(
                process,
                processHandle,
                jobHandle,
                pseudoConsole,
                input,
                output,
                request.InitialSize,
                OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100));

            process = null;
            processHandle = null;
            jobHandle = null;
            pseudoConsole = null;
            input = null;
            output = null;
            return session;
        }
        catch
        {
            if (processHandle is not null && !processAssignedToJob && !processHandle.IsInvalid)
            {
                _ = NativeMethods.TerminateProcess(processHandle.DangerousGetHandle(), uint.MaxValue);
            }

            output?.Dispose();
            input?.Dispose();
            process?.Dispose();
            threadHandle?.Dispose();
            processHandle?.Dispose();
            jobHandle?.Dispose();
            pseudoConsole?.Dispose();
            throw;
        }
        finally
        {
            CloseRawHandle(ref pseudoInputRead);
            CloseRawHandle(ref pseudoOutputWrite);
            CloseRawHandle(ref hostInputWrite);
            CloseRawHandle(ref hostOutputRead);

            if (attributeList != IntPtr.Zero)
            {
                NativeMethods.DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }

            if (commandLineBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(commandLineBuffer);
            }
        }
    }

    private static SafeKernelHandle CreateKillOnCloseJob()
    {
        var rawJob = NativeMethods.CreateJobObject(IntPtr.Zero, null);
        if (rawJob == IntPtr.Zero)
        {
            ThrowLastWin32("CreateJobObjectW");
        }

        var job = new SafeKernelHandle(rawJob);
        try
        {
            var information = new JobObjectExtendedLimitInformation
            {
                BasicLimitInformation = new JobObjectBasicLimitInformation
                {
                    LimitFlags = JobObjectLimitKillOnJobClose,
                },
            };

            EnsureWin32(
                NativeMethods.SetInformationJobObject(
                    job.DangerousGetHandle(),
                    JobObjectExtendedLimitInformationClass,
                    ref information,
                    (uint)Marshal.SizeOf<JobObjectExtendedLimitInformation>()),
                "SetInformationJobObject(KILL_ON_JOB_CLOSE)");
            return job;
        }
        catch
        {
            job.Dispose();
            throw;
        }
    }

    private static string BuildCommandLine(string executablePath, IReadOnlyList<string> arguments)
    {
        var builder = new StringBuilder(QuoteWindowsArgument(executablePath));
        foreach (var argument in arguments)
        {
            builder.Append(' ');
            builder.Append(QuoteWindowsArgument(argument));
        }

        return builder.ToString();
    }

    private static string QuoteWindowsArgument(string argument)
    {
        if (argument.Length == 0)
        {
            return "\"\"";
        }

        var requiresQuoting = false;
        foreach (var character in argument)
        {
            if (char.IsWhiteSpace(character) || character == '"')
            {
                requiresQuoting = true;
                break;
            }
        }

        if (!requiresQuoting)
        {
            return argument;
        }

        var result = new StringBuilder(argument.Length + 2);
        result.Append('"');
        var pendingBackslashes = 0;
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                pendingBackslashes++;
                continue;
            }

            if (character == '"')
            {
                result.Append('\\', (pendingBackslashes * 2) + 1);
                result.Append('"');
                pendingBackslashes = 0;
                continue;
            }

            result.Append('\\', pendingBackslashes);
            pendingBackslashes = 0;
            result.Append(character);
        }

        result.Append('\\', pendingBackslashes * 2);
        result.Append('"');
        return result.ToString();
    }

    internal static Coord ToCoord(TerminalSize size) =>
        new(checked((short)size.Columns), checked((short)size.Rows));

    private static void CloseRawHandle(ref IntPtr handle)
    {
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            handle = IntPtr.Zero;
            return;
        }

        _ = NativeMethods.CloseHandle(handle);
        handle = IntPtr.Zero;
    }

    internal static void EnsureHResult(int hresult, string operation)
    {
        if (hresult >= 0)
        {
            return;
        }

        var exception = Marshal.GetExceptionForHR(hresult);
        throw new ConPtyException(operation, hresult, exception?.Message ?? "Unknown HRESULT failure.");
    }

    private static void EnsureWin32(bool succeeded, string operation)
    {
        if (!succeeded)
        {
            ThrowLastWin32(operation);
        }
    }

    private static void ThrowLastWin32(string operation)
    {
        var error = Marshal.GetLastPInvokeError();
        throw new ConPtyException(operation, error, new Win32Exception(error).Message);
    }

    internal sealed class WindowsConPtyTerminalSession : IConPtyTerminalSession
    {
        private readonly object _stateGate = new();
        private readonly Process _process;
        private readonly SafeKernelHandle _processHandle;
        private readonly SafeKernelHandle _jobHandle;
        private readonly SafePseudoConsoleHandle _pseudoConsole;
        private readonly FileStream _input;
        private readonly FileStream _output;
        private readonly bool _releasePseudoConsoleOnCompletion;
        private readonly Task<int> _completion;
        private TerminalSize _size;
        private int _disposeStarted;

        internal WindowsConPtyTerminalSession(
            Process process,
            SafeKernelHandle processHandle,
            SafeKernelHandle jobHandle,
            SafePseudoConsoleHandle pseudoConsole,
            FileStream input,
            FileStream output,
            TerminalSize initialSize,
            bool releasePseudoConsoleOnCompletion)
        {
            _process = process;
            _processHandle = processHandle;
            _jobHandle = jobHandle;
            _pseudoConsole = pseudoConsole;
            _input = input;
            _output = output;
            _size = initialSize;
            _releasePseudoConsoleOnCompletion = releasePseudoConsoleOnCompletion;
            _completion = ObserveCompletionAsync();
        }

        public int ProcessId => _process.Id;

        public Stream Input => _input;

        public Stream Output => _output;

        public TerminalSize Size
        {
            get
            {
                lock (_stateGate)
                {
                    return _size;
                }
            }
        }

        public Task<int> Completion => _completion;

        public ValueTask ResizeAsync(
            TerminalSize size,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(size);
            cancellationToken.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposeStarted) != 0, this);

            lock (_stateGate)
            {
                if (_completion.IsCompleted || _pseudoConsole.IsClosed || _pseudoConsole.IsInvalid)
                {
                    throw new InvalidOperationException("The ConPTY session has already completed.");
                }

                var result = NativeMethods.ResizePseudoConsole(
                    _pseudoConsole.DangerousGetHandle(),
                    ToCoord(size));
                EnsureHResult(result, "ResizePseudoConsole");
                _size = size;
            }

            return ValueTask.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
            {
                return;
            }

            try
            {
                _input.Dispose();
                _jobHandle.Dispose();

                try
                {
                    await _completion.ConfigureAwait(false);
                }
                catch (ConPtyException)
                {
                    // Native completion diagnostics remain observable from Completion.
                }
            }
            finally
            {
                _output.Dispose();
                _pseudoConsole.Dispose();
                _process.Dispose();
                _processHandle.Dispose();
                _jobHandle.Dispose();
            }
        }

        private async Task<int> ObserveCompletionAsync()
        {
            try
            {
                await _process.WaitForExitAsync().ConfigureAwait(false);
                if (!NativeMethods.GetExitCodeProcess(
                        _processHandle.DangerousGetHandle(),
                        out var exitCode))
                {
                    ThrowLastWin32("GetExitCodeProcess");
                }

                if (_releasePseudoConsoleOnCompletion &&
                    !_pseudoConsole.IsClosed &&
                    !_pseudoConsole.IsInvalid)
                {
                    EnsureHResult(
                        NativeMethods.ReleasePseudoConsole(_pseudoConsole.DangerousGetHandle()),
                        "ReleasePseudoConsole");
                }

                return unchecked((int)exitCode);
            }
            finally
            {
                _input.Dispose();
                if (!_releasePseudoConsoleOnCompletion)
                {
                    _pseudoConsole.Dispose();
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct Coord
    {
        internal Coord(short x, short y)
        {
            X = x;
            Y = y;
        }

        internal readonly short X;
        internal readonly short Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public int Cb;
        public IntPtr Reserved;
        public IntPtr Desktop;
        public IntPtr Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public int Flags;
        public short ShowWindow;
        public short Reserved2Size;
        public IntPtr Reserved2;
        public IntPtr StandardInput;
        public IntPtr StandardOutput;
        public IntPtr StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public IntPtr AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr ProcessHandle;
        public IntPtr ThreadHandle;
        public uint ProcessId;
        public uint ThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
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
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }

    internal sealed class SafeKernelHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal SafeKernelHandle(IntPtr handle)
            : base(ownsHandle: true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle() => NativeMethods.CloseHandle(handle);
    }

    internal sealed class SafePseudoConsoleHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal SafePseudoConsoleHandle(IntPtr handle)
            : base(ownsHandle: true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            NativeMethods.ClosePseudoConsole(handle);
            return true;
        }
    }

    private static partial class NativeMethods
    {
        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool CreatePipe(
            out IntPtr readPipe,
            out IntPtr writePipe,
            IntPtr pipeAttributes,
            uint size);

        [LibraryImport("kernel32.dll")]
        internal static partial int CreatePseudoConsole(
            Coord size,
            IntPtr input,
            IntPtr output,
            uint flags,
            out IntPtr pseudoConsole);

        [LibraryImport("kernel32.dll")]
        internal static partial int ResizePseudoConsole(IntPtr pseudoConsole, Coord size);

        [LibraryImport("kernel32.dll")]
        internal static partial int ReleasePseudoConsole(IntPtr pseudoConsole);

        [LibraryImport("kernel32.dll")]
        internal static partial void ClosePseudoConsole(IntPtr pseudoConsole);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool InitializeProcThreadAttributeList(
            IntPtr attributeList,
            int attributeCount,
            uint flags,
            ref nuint size);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool UpdateProcThreadAttribute(
            IntPtr attributeList,
            uint flags,
            nuint attribute,
            IntPtr value,
            nuint size,
            IntPtr previousValue,
            IntPtr returnSize);

        [LibraryImport("kernel32.dll")]
        internal static partial void DeleteProcThreadAttributeList(IntPtr attributeList);

        [LibraryImport(
            "kernel32.dll",
            EntryPoint = "CreateProcessW",
            SetLastError = true,
            StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool CreateProcess(
            string? applicationName,
            IntPtr commandLine,
            IntPtr processAttributes,
            IntPtr threadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
            uint creationFlags,
            IntPtr environment,
            string? currentDirectory,
            ref StartupInfoEx startupInfo,
            out ProcessInformation processInformation);

        [LibraryImport(
            "kernel32.dll",
            EntryPoint = "CreateJobObjectW",
            SetLastError = true,
            StringMarshalling = StringMarshalling.Utf16)]
        internal static partial IntPtr CreateJobObject(IntPtr jobAttributes, string? name);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetInformationJobObject(
            IntPtr job,
            int informationClass,
            ref JobObjectExtendedLimitInformation information,
            uint informationLength);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool AssignProcessToJobObject(IntPtr job, IntPtr process);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        internal static partial uint ResumeThread(IntPtr thread);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetExitCodeProcess(IntPtr process, out uint exitCode);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool TerminateProcess(IntPtr process, uint exitCode);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool CloseHandle(IntPtr handle);
    }
}
