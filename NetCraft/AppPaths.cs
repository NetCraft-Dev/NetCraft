using System.IO;
using NetCraft.Logging;

namespace NetCraft;

//AppPaths centralizes the program root path management
//Based on AppContext.BaseDirectory to avoid depending on the runtime working directory
//Removes the ambiguity of relative paths being interpreted as cwd and unifies the base for subdirectories like assets/worlds/logs
public static class AppPaths
{
    //OverrideRoot; when an explicit --output-dir is passed it is non-empty and overrides BaseDirectory for the whole root
    //SetOverride is called once at the top level by ServerMain/ClientMain; afterwards everything reads from AppPaths
    private static string? _overrideRoot;

    //BaseDirectory absolute program root path; _overrideRoot takes precedence, otherwise AppContext.BaseDirectory
    public static string BaseDirectory => _overrideRoot ?? AppContext.BaseDirectory;

    //AssetsDir assets/ subdirectory of the resource root, for AssetsExtractor extraction and business loading
    public static string AssetsDir => Path.Combine(BaseDirectory, "assets");

    //DataDir data/ subdirectory of the data root, for AssetsExtractor to extract jar entries under data/
    //Server data-driven sources: advancements/recipes/tags/functions, etc.
    public static string DataDir => Path.Combine(BaseDirectory, "data");

    //DatapacksDir datapacks/ subdirectory of external data packs, for ResourceManager to scan and load *.zip
    public static string DatapacksDir => Path.Combine(BaseDirectory, "datapacks");

    //WorldsDir worlds/ subdirectory of the world save root, for LevelStorage
    public static string WorldsDir => Path.Combine(BaseDirectory, "worlds");

    //LogsDir logs/ subdirectory of the log root; the log directory has one owner and this is it
    public static string LogsDir => Path.Combine(BaseDirectory, "logs");

    //CrashReportsDir crash-reports/ subdirectory of the crash report root, aligning with vanilla
    public static string CrashReportsDir => Path.Combine(BaseDirectory, "crash-reports");

    //TracesDir traces/ subdirectory, where /debug trace writes its captures
    public static string TracesDir => Path.Combine(BaseDirectory, "traces");

    //OptionsPath client config file path assets/options.txt
    public static string OptionsPath => Path.Combine(AssetsDir, "options.txt");

    //ServerPropertiesPath server config file path root/server.properties
    public static string ServerPropertiesPath => Path.Combine(BaseDirectory, "server.properties");

    //OpsPath operator list path root/ops.json, matching the vanilla server root location
    public static string OpsPath => Path.Combine(BaseDirectory, "ops.json");

    //WhitelistPath whitelist path root/whitelist.json
    public static string WhitelistPath => Path.Combine(BaseDirectory, "whitelist.json");

    //BannedPlayersPath player ban list path root/banned-players.json
    public static string BannedPlayersPath => Path.Combine(BaseDirectory, "banned-players.json");

    //BannedIpsPath IP ban list path root/banned-ips.json
    public static string BannedIpsPath => Path.Combine(BaseDirectory, "banned-ips.json");

    //SetOverride sets --output-dir to override the whole root
    //optionValue when null or whitespace it is ignored and AppContext.BaseDirectory is used
    public static void SetOverride(string? optionValue)
    {
        if (!string.IsNullOrWhiteSpace(optionValue))
            _overrideRoot = Path.GetFullPath(optionValue);
        //The log directory follows the program root and is set unconditionally, including the no-override case
        //Log only knows AppDomain.BaseDirectory, which is the host's own directory, so without this a tool like ncm
        //launching the server would leave the logs inside the tool's folder instead of the run directory
        Log.SetLogDirectory(LogsDir);
    }
}
