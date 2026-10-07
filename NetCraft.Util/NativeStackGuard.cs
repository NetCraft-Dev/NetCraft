using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NetCraft.Logging;

namespace NetCraft.Util;

//NativeStackGuard connects the managed crash path to the native layer
//
//Three things live here. The first gets the layer into the process at all: the runtime reads the profiler variables
//only while it starts, so a process that is already running cannot be attached afterwards and a restart is the only
//way in. The second is the callback used when the stack is about to run out, which the layer hands over on a thread
//that still has room to work. The third is the entry the injected early check calls, which has to be public because
//the call is written into method bodies of other assemblies
public static class NativeStackGuard
{
    //LibraryName file the native layer builds into, without extension
    private const string LibraryName = "netcraft_native";

    //The native library's file name on each platform; a cdylib has no prefix on Windows
    private static readonly string[] LibraryNames =
    {
        "netcraft_native.dll",
        "libnetcraft_native.so",
        "libnetcraft_native.dylib",
    };

    //ProfilerGuid must match the CLSID the native library reports
    private const string ProfilerGuid = "{2B5F8D34-6C1A-4E27-9B3D-52E8714AC618}";

    //AttachedFlag marks a process that has already been restarted once for the native layer
    //Without it a run where the layer fails to load would restart endlessly
    private const string AttachedFlag = "NC_NATIVE_ATTACHED";

    //EnsureStack is the entry the injected call points at
    //
    //It is deliberately not the framework helper. That check fires on every call, so once the stack is short it keeps
    //firing while the exception is unwound and handled, and the process ends up dying on the way to reporting the
    //problem. This one goes quiet after it trips and only arms again once the stack has grown back, which leaves room
    //for the unwind, the catch block and anything that logs along the way
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void EnsureStack()
    {
        if (Tripped()) throw new InsufficientExecutionStackException();
    }

    [DllImport(LibraryName, EntryPoint = "ncn_stack_guard", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool Tripped();

    //Console bits needed to hand an interrupt to the child and keep escape sequences working
    private const uint CtrlCEvent = 0;
    private const uint StdOutputHandle = 0xFFFFFFF5;
    private const uint EnableVirtualTerminalProcessing = 0x0004;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(uint handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr handle, out uint mode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr handle, uint mode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GenerateConsoleCtrlEvent(uint ctrlEvent, uint processGroupId);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void StackOverflowCallback(IntPtr faultingAddress);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ncn_register_stack_overflow_callback(IntPtr callback, byte[] reportDirectory, UIntPtr length);

    //The delegate is kept alive for the process lifetime: the native side holds its address and would otherwise end up
    //calling a collected stub
    private static StackOverflowCallback? _callback;
    private static bool _installed;

    /// <summary>
    /// RelaunchIfNeeded restarts this process once so the native layer is loaded from its first instruction
    /// Returns without doing anything when the layer is already in, when the platform cannot carry it, or when another
    /// profiler has claimed the process; in those cases this process simply goes on without the early stack check
    /// When it does restart, this never returns: the parent waits for the child and exits with the child's code
    /// </summary>
    public static void RelaunchIfNeeded()
    {
        //Restarted once already: if the layer is still missing, restarting again would loop forever
        if (Environment.GetEnvironmentVariable(AttachedFlag) == "1") return;

        //Another profiler already holds this process. It is not ours to displace, so the check stays off and the server
        //carries on; this is the case when the hook layer has been attached for runtime injection
        var existing = Environment.GetEnvironmentVariable("CORECLR_PROFILER_PATH");
        if (!string.IsNullOrEmpty(existing))
        {
            Log.Warning($"Another profiler already holds this process ({existing}), the early stack check stays off");
            return;
        }

        var library = FindNativeLibrary();
        if (library is null)
        {
            Log.Warning("The native layer was not found next to the program, the early stack check stays off");
            return;
        }

        var executable = Environment.ProcessPath;
        if (string.IsNullOrEmpty(executable))
        {
            Log.Warning("The running executable could not be located, restarting with the native layer is not possible");
            return;
        }

        var startInfo = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = false };

        //Entry zero is the executable itself; the rest of the command line is forwarded or a restart would drop flags
        var arguments = Environment.GetCommandLineArgs();
        for (var i = 1; i < arguments.Length; i++) startInfo.ArgumentList.Add(arguments[i]);

        startInfo.Environment["CORECLR_ENABLE_PROFILING"] = "1";
        startInfo.Environment["CORECLR_PROFILER"] = ProfilerGuid;
        startInfo.Environment["CORECLR_PROFILER_PATH"] = library;
        startInfo.Environment[AttachedFlag] = "1";

        //The rewrite list is left alone: the layer decides on its own when the variable is absent, and that default
        //covers every assembly except the framework ones, mods included

        EnableVirtualTerminal();

        Log.Info("Restarting with the native layer so stack depth is checked before the stack runs out");
        using var child = Process.Start(startInfo);
        if (child is null)
        {
            Log.Warning("The restarted process did not start");
            return;
        }

        ForwardInterrupt(child);

        child.WaitForExit();
        Environment.Exit(child.ExitCode);
    }

    /// <summary>
    /// Register takes the stack overflow callback from the native layer
    /// Returns whether the layer was present and accepted it; a missing layer only costs the richer report
    /// </summary>
    public static bool Register()
    {
        if (_installed) return true;

        try
        {
            _callback = OnStackOverflow;
            var pointer = Marshal.GetFunctionPointerForDelegate(_callback);
            var directory = Encoding.UTF8.GetBytes(CrashHandler.CrashReportsDir);
            var result = ncn_register_stack_overflow_callback(pointer, directory, (UIntPtr)directory.Length);
            if (result != 0)
            {
                Log.Warning($"The native layer refused the stack overflow callback, code {result}");
                _callback = null;
                return false;
            }

            _installed = true;
            Log.Info("The native layer is in place and will report a stack overflow");
            return true;
        }
        catch (DllNotFoundException)
        {
            //Not loaded, which is the normal case when profiling was never enabled for this run
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            Log.Warning("The native layer is present but has no stack overflow entry point");
            return false;
        }
    }

    //Installed whether the native layer took the callback
    public static bool Installed => _installed;

    //ForwardInterrupt hands the interrupt key to the child, since the console belongs to this process
    //Cancelling the default keeps this process alive and raises the event on the child's console instead
    private static void ForwardInterrupt(Process child)
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            if (child.HasExited) return;
            //On Unix the interrupt already reaches the whole foreground process group, so only Windows needs the hop
            if (!OperatingSystem.IsWindows()) return;
            try
            {
                AttachConsole((uint)child.Id);
                GenerateConsoleCtrlEvent(CtrlCEvent, 0);
                FreeConsole();
            }
            catch (Exception exception)
            {
                Log.Warning($"Forwarding the interrupt to the restarted process failed: {exception.GetType().Name}");
            }
        };
    }

    //EnableVirtualTerminal turns on escape sequence handling for the console the child shares
    //The setting belongs to the console rather than the process, so enabling it here also covers the child's output
    private static void EnableVirtualTerminal()
    {
        if (!OperatingSystem.IsWindows()) return;
        var handle = GetStdHandle(StdOutputHandle);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1)) return;
        if (!GetConsoleMode(handle, out var mode)) return;
        _ = SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
    }

    //FindNativeLibrary looks beside the program first, then under the per-platform release layout
    //The host resolves runtimes/<rid>/native on its own for ordinary native calls, but the profiler path is handed to
    //the runtime by name, so the entry for this platform has to be picked out here
    private static string? FindNativeLibrary()
    {
        var explicitPath = Environment.GetEnvironmentVariable("NC_NATIVE_PATH");
        if (!string.IsNullOrEmpty(explicitPath) && File.Exists(explicitPath))
            return explicitPath;

        var directories = new List<string> { AppContext.BaseDirectory };
        var runtimeIdentifier = RuntimeInformation.RuntimeIdentifier;
        if (!string.IsNullOrEmpty(runtimeIdentifier))
        {
            directories.Add(Path.Combine(AppContext.BaseDirectory, "runtimes", runtimeIdentifier, "native"));
        }

        foreach (var directory in directories)
        {
            foreach (var name in LibraryNames)
            {
                var candidate = Path.Combine(directory, name);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    //OnStackOverflow runs on the thread the native layer starts, so a stack is available here
    //The thread that overflowed is still stuck where it failed, so its stack cannot be described; what this can carry
    //is the address it failed at and the fact that a stack overflow was the cause
    private static void OnStackOverflow(IntPtr faultingAddress)
    {
        try
        {
            var address = $"{faultingAddress.ToInt64():X16}";
            var report = new CrashReport("A stack overflow ended the process",
                new StackOverflowException($"The stack was exhausted at address 0x{address}"));
            CrashHandler.Save(report, new[]
            {
                "A stack overflow cannot be caught by the runtime, so this report comes from the native layer",
                $"The thread that overflowed is stopped at 0x{address} and its stack cannot be read",
            });
        }
        catch (Exception exception)
        {
            //The process is already on its way out; all this branch can do is say that reporting failed
            Log.Critical($"Writing the stack overflow report failed: {exception.GetType().Name}: {exception.Message}");
        }
    }
}
