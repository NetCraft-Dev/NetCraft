using NetCraft.Config;
using NetCraft.Logging;
using NetCraft.Network.Chat;
using NetCraft.Util;

namespace NetCraft;

//NetCraft kernel main entry (public API of the aggregate library).
//Maps to the entry responsibilities of vanilla net.minecraft.server.Main + net.minecraft.Bootstrap.
//Note: this class library is not an exe; callers create their own Main function and call Initialize.
public static class NetCraftKernel
{
    private static int _initialized;

    //Initialize the NetCraft kernel. This includes:
    //  <item>initializing the embedded assembly loader (loads sub-library dlls from embedded resources)</item>
    //  <item>parsing launch arguments (kernel-recognized ones are consumed, unhandled ones are broadcast via the LaunchOptions.UnhandledArgument event)</item>
    //  <item>printing startup version/protocol info (in Debug)</item>
    //Idempotent: multiple calls take effect only once.
    //args command-line arguments; before calling, subscribers should already be subscribed to LaunchOptions.UnhandledArgument
    public static void Initialize(string[]? args = null)
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            //Log.Debug("Initialize exit");
            return;
        }
        Log.Debug($"Initialize entry args={(args == null ? "null" : string.Join(",", args))}");

        // 0. Log output level is decided by debug mode; both outputs change together
        //    Defaults to Info only; without --debug, verbose Debug logs should not be emitted
        //    Loader already sets DebugMode from --debug; this adds a fallback so hosts passing args directly work too
        if (args is { Length: > 0 } && Array.IndexOf(args, "--debug") >= 0)
            DebugMode.IsEnabled = true;
        var detailLevel = DebugMode.IsEnabled ? LogLevel.Debug : LogLevel.Info;
        Log.SetConsoleLevel(detailLevel);
        Log.SetFileLevel(detailLevel);
        //Under --debug the format also switches to the detailed tier: timestamps with milliseconds plus file and line number
        Log.SetVerbose(DebugMode.IsEnabled);

        // 1. First initialize the embedded resource loader so later type resolution can find sub-libraries
        EmbeddedAssemblyLoader.Initialize();

        //Set the log source to the kernel
        Log.SetClassSource(typeof(NetCraftKernel));

        //Extract language files to the root lang/ and install the table once for the default language code
        //Placed here to be as early as possible, so mod loading and kernel startup logs have words to pick from
        //The real language code is installed again after ServerMain/ClientMain read the config
        NcLanguageFiles.Extract();
        NcLanguage.Load(Language.Default);

        //Print the startup ASCII-art banner
        PrintBanner();

        // 2. Parse launch arguments; kernel-recognized ones are consumed, unhandled ones are broadcast via the event
        //    Subscribers must subscribe to UnhandledArgument before calling Initialize
        if (args is { Length: > 0 })
        {
            LaunchOptions.Parse(args);
        }

        // 3. When debug mode is on, print startup info (triggered by the runtime --debug flag; also works in Release builds)
        if (DebugMode.IsEnabled)
            PrintStartupInfo();
        //Log.Debug("Initialize exit");
    }

    //Get the kernel version.
    public static string Version => SharedConstants.Version;

    //Get the current protocol version number.
    public static int ProtocolVersion => SharedConstants.ProtocolVersion;

    //PrintStartupInfo prints kernel startup details, maps to vanilla Main output: version/protocol/world data version/embedded sub-libraries
    //Triggered at runtime by DebugMode.IsEnabled; Release builds can also enable it via --debug
    private static void PrintStartupInfo()
    {
        //Log.Debug("PrintStartupInfo entry");
        Log.Info($"Initializing kernel v{SharedConstants.Version}");
        Log.Info($"Protocol version: {SharedConstants.ProtocolVersion} (min {SharedConstants.ProtocolVersionLowerBound})");
        Log.Info($"World data version: {SharedConstants.WorldDataVersion}");
        Log.Info($"Target TPS: {SharedConstants.TicksPerSecond}");

        var embedded = EmbeddedAssemblyLoader.ListEmbeddedAssemblies();
        if (embedded.Count > 0)
        {
            Log.Info($"Embedded sub-libraries ({embedded.Count}):");
            foreach (var name in embedded)
                Log.Info($"  - {name}");
        }
        //Log.Debug("PrintStartupInfo exit");
    }

    //Print the startup ASCII-art banner
    private static void PrintBanner()
    {
        ConsoleAnsiArtist.PrintRainbowText("NetCraft");
    }
}
