using System.Diagnostics;
using System.Runtime.InteropServices;
using NetCraft.Logging;

namespace NetCraft.ModLoader;

//ProfilerRelaunch: when runtime injection is needed, restarts the process with the native injection layer
//The ReJIT switch reads environment variables only at process start; setting them later has no effect, so a restart is required
//The restarted process shares this console and this process stays alive as its parent, which keeps the terminal continuous:
//the child's output lands on the same console, no shell prompt comes back in the middle, and an interrupt is handed over
//instead of killing the parent and orphaning the child
internal static class ProfilerRelaunch
{
    //ReloadFlag: set on the child process by this class to guard against repeated restarts when the environment variable was set but did not take effect
    private const string ReloadFlag = "NC_PROFILER_ATTACHED";

    //ProfilerGuid: must match the CLSID in the native library
    private const string ProfilerGuid = "{7A2E4C1B-9D3F-4E58-A6B0-1C5D8F2A3E70}";

    //Console bits needed to hand an interrupt to the child and keep escape sequences working
    private const uint CtrlCEvent = 0;
    private const uint EnableVirtualTerminalProcessing = 0x0004;
    private const uint StdOutputHandle = 0xFFFFFFF5;

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

    //LibraryNames: the native library's file name on each platform
    private static readonly string[] LibraryNames =
    {
        "lead_hook_native.dll",
        "liblead_hook_native.so",
        "lead_hook_native.dylib",
    };

    //Attached: the current process is already running with the native injection layer
    //Counts as attached when the user has attached another profiler, so we do not compete
    public static bool Attached =>
        Environment.GetEnvironmentVariable(ReloadFlag) == "1" ||
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CORECLR_PROFILER_PATH"));

    //Relaunch: restarts itself with the native injection layer, returns false if it did not restart
    //The parent forwards the child's exit code after waiting; when it returns true the caller should not continue
    public static bool Relaunch()
    {
        var library = FindLibrary();
        if (library is null)
        {
            Log.Warning("Runtime injection needs the native layer but no lead_hook_native library was found next to the program");
            return false;
        }

        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
        {
            Log.Warning("Cannot locate the running executable, restart with the native layer is not possible");
            return false;
        }

        var startInfo = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = false };
        //Entry 0 is the executable itself; the rest of the command line is forwarded as-is, otherwise a restart would drop arguments like --debug
        var args = Environment.GetCommandLineArgs();
        for (var i = 1; i < args.Length; i++)
            startInfo.ArgumentList.Add(args[i]);
        startInfo.Environment["CORECLR_ENABLE_PROFILING"] = "1";
        startInfo.Environment["CORECLR_PROFILER"] = ProfilerGuid;
        startInfo.Environment["CORECLR_PROFILER_PATH"] = library;
        startInfo.Environment[ReloadFlag] = "1";

        EnableVirtualTerminal();

        Log.Info("Restarting with the native injection layer to enable runtime injection rules");
        using var child = Process.Start(startInfo);
        if (child is null)
        {
            Log.Warning("The restarted process did not start");
            return false;
        }

        //The interrupt key belongs to this console, so the child does not get it on its own
        //Cancelling the default keeps this process alive and the event is raised on the child's console instead
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

        child.WaitForExit();
        Environment.Exit(child.ExitCode);
        return true;
    }

    //EnableVirtualTerminal turns on escape sequence handling for the console the child shares
    //The setting is per console, so enabling it here also covers the child's output
    private static void EnableVirtualTerminal()
    {
        if (!OperatingSystem.IsWindows()) return;
        var handle = GetStdHandle(StdOutputHandle);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1)) return;
        if (!GetConsoleMode(handle, out var mode)) return;
        _ = SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
    }

    //FindLibrary: looks for the native library in the program directory and the directory containing Lead.Hook
    //NC_PROFILER_PATH can point at a copy elsewhere, used in development to point at the Rust build output
    private static string? FindLibrary()
    {
        var explicitPath = Environment.GetEnvironmentVariable("NC_PROFILER_PATH");
        if (!string.IsNullOrEmpty(explicitPath) && File.Exists(explicitPath))
            return explicitPath;

        var directories = new List<string> { AppPaths.BaseDirectory };
        //Lead.Hook has no on-disk location when loaded from a stream, so only the program directory works then
        var hookDirectory = Path.GetDirectoryName(typeof(Lead.Hook.HookEngine).Assembly.Location);
        if (!string.IsNullOrEmpty(hookDirectory))
            directories.Add(hookDirectory);

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
}
