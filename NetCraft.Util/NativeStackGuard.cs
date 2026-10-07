using System.Runtime.InteropServices;
using System.Text;
using NetCraft.Logging;

namespace NetCraft.Util;

//NativeStackGuard connects the managed crash path to the native layer
//A stack overflow is the one failure the managed side cannot report on its own: the runtime ends the process before any
//managed handler runs. The native layer catches the overflow first, hands it to the callback below on a thread that
//still has a stack, and that is what makes a crash report possible for this case at all
internal static class NativeStackGuard
{
    //LibraryName file the native layer builds into, without extension
    private const string LibraryName = "netcraft_native";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void StackOverflowCallback(IntPtr faultingAddress);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int ncn_register_stack_overflow_callback(IntPtr callback, byte[] reportDirectory, UIntPtr length);

    //The delegate is kept alive for the process lifetime: the native side holds its address and would otherwise end up
    //calling a collected stub
    private static StackOverflowCallback? _callback;
    private static bool _installed;

    //Install registers the callback and returns whether the native layer took it
    //False means the layer is not in this process, which is the normal case when profiling was never enabled; the
    //server keeps running and only the stack overflow report is unavailable
    public static bool Install()
    {
        if (_installed) return true;

        try
        {
            _callback = OnStackOverflow;
            var pointer = Marshal.GetFunctionPointerForDelegate(_callback);
            var directory = Encoding.UTF8.GetBytes(CrashReportsDirectory);
            var result = ncn_register_stack_overflow_callback(pointer, directory, (UIntPtr)directory.Length);
            if (result != 0)
            {
                Log.Warning($"The native layer refused the stack overflow callback, code {result}");
                _callback = null;
                return false;
            }

            _installed = true;
            Log.Info("Stack overflow reporting is handled by the native layer");
            return true;
        }
        catch (DllNotFoundException)
        {
            //Not loaded, which is expected unless the process was started with the profiler variables set
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

    //CrashReportsDirectory the directory the native side writes into when the managed callback cannot run
    private static string CrashReportsDirectory => CrashHandler.CrashReportsDir;

    //OnStackOverflow runs on the thread the native layer starts, so a stack is available here
    //The thread that overflowed is still stuck at the point of failure, so the report cannot describe its stack; what
    //it can carry is the faulting address and the fact that a stack overflow was the cause
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
