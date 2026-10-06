using System.Runtime.CompilerServices;
using NetCraft.Util;

namespace NetCraft;

//Module initializer.
//.NET-only: runs automatically on assembly load (before any type is first accessed).
//Used to register the embedded resource resolution callback before any type in the main library is referenced, avoiding a chicken-and-egg problem:
//  - GetType(NetCraftKernel) needs NetCraft.Config (referenced)
//  - Resolving NetCraft.Config needs the Resolving callback
//  - The Resolving callback is registered in Initialize(), but GetType happens before Initialize
internal static class ModuleInitialization
{
    //UtilAssemblyName the assembly where the crash handler lives
    private const string UtilAssemblyName = "NetCraft.Util";

    [ModuleInitializer]
    public static void Initialize()
    {
        EmbeddedAssemblyLoader.Initialize();
        //The crash handler lives in NetCraft.Util; at this moment the kernel directory may not be set yet, and resolving directly would throw and crash the main library load
        //Instead, install it once that assembly is actually loaded; this layer only uses the BCL and touches no other kernel assembly
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == UtilAssemblyName))
            InstallCrashHandler();
    }

    //OnAssemblyLoad installs the crash handler when the kernel sub-library comes up
    private static void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs e)
    {
        if (e.LoadedAssembly.GetName().Name != UtilAssemblyName) return;
        InstallCrashHandler();
    }

    //InstallCrashHandler installs the global managed exception fallback; idempotent
    //Give the report directory as a delegate; the program root may later be changed by --output-dir, and reports must move with it
    private static void InstallCrashHandler()
    {
        AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
        CrashHandler.SetRootProvider(() => AppPaths.BaseDirectory);
        CrashHandler.Install();
    }
}
