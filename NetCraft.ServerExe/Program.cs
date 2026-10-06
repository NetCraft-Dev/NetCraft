using System.Runtime.CompilerServices;
using NetCraft;
using NetCraft.Config;
using NetCraft.Game;
using NetCraft.Logging;
using NetCraft.ModLoader;

namespace NetCraft.ServerExe;

//Program standalone server entry point
//Only handles the process entry and mod bootstrap; the real server implementation stays in the NetCraft.Server class library
public static class Program
{
    //Main process entry
    //First thing is registering the kernel assembly resolution callback: kernel assemblies were moved out of deps.json,
    //so at runtime the callback fetches their bytes from the kernel directory and the main library's embedded resources. This must run before any kernel type is resolved.
    //The JIT resolves every type appearing in a method body, so the two layers below are marked no-inline,
    //otherwise ServerMain gets resolved early and bypasses the rewrite, bringing up an un-injected NetCraft.Server.
    public static int Main(string[] args)
    {
        EmbeddedAssemblyLoader.Initialize();
        BootMods(args);
        return Launch(args);
    }

    //BootMods mod bootstrap; this layer only touches the loader, not server types
    //--debug must open the log output here: injection runs before kernel Initialize, and one step late drops the whole run of debug records
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void BootMods(string[] args)
    {
        if (Array.IndexOf(args, "--debug") >= 0)
            DebugMode.IsEnabled = true;

        if (DebugMode.IsEnabled)
        {
            Log.SetConsoleLevel(LogLevel.Debug);
            Log.SetFileLevel(LogLevel.Debug);
            Log.SetVerbose(true);
        }

        ModBootstrap.Run(ModEnvironment.Server);
    }

    //Launch server startup flow; only at this layer is resolving NetCraft.Server allowed
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Launch(string[] args)
    {
        ServerMain.Run(args);
        return 0;
    }
}
