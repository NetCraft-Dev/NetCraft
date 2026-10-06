using NetCraft.Logging;

namespace NetCraft;

//ServerSettings server server.properties config
//Simplified version of vanilla net.minecraft.server.dedicated.Settings
//Inherits Settings<ServerSettings> and aligns with vanilla's self type pattern via a generic self-reference
//Keeps only core fields: port/world name/difficulty/max-players, etc.
public sealed class ServerSettings : Settings<ServerSettings>
{
    //Server listen port, default 25565
    public int ServerPort => GetInt("server-port", 25565);

    //Max players, default 20
    public int MaxPlayers => GetInt("max-players", 20);

    //Level name, default world
    public string LevelName => GetOrDefault("level-name", "world");

    //Game mode survival/creative/adventure/spectator
    public string Gamemode => GetOrDefault("gamemode", "survival");

    //Difficulty peaceful/easy/normal/hard
    public string Difficulty => GetOrDefault("difficulty", "easy");

    //Whether online-mode authentication is enabled, default true
    public bool OnlineMode => GetBool("online-mode", true);

    //Whether PVP is allowed, default true
    public bool AllowPvp => GetBool("pvp", true);

    //View distance in chunks, default 10
    public int ViewDistance => GetInt("view-distance", 10);

    //Simulation distance in chunks, default 10; maps to vanilla simulation-distance
    //Determines the range that actually participates in ticks; when view distance is larger, the ring in between is only loaded, not ticked (weak loading)
    public int SimulationDistance => GetInt("simulation-distance", 10);

    //Whether flight is allowed, default false
    public bool AllowFlight => GetBool("allow-flight", false);

    //Whether to spawn animals, default true
    public bool SpawnAnimals => GetBool("spawn-animals", true);

    //Whether to spawn monsters, default true
    public bool SpawnMonsters => GetBool("spawn-monsters", true);

    //Whether to spawn NPCs, default true
    public bool SpawnNpcs => GetBool("spawn-npcs", true);

    //Whether the whitelist is enabled, default false
    public bool WhiteList => GetBool("white-list", false);

    //Whether to generate structures, default true
    public bool GenerateStructures => GetBool("generate-structures", true);

    //Whether the Nether is allowed, default true
    public bool AllowNether => GetBool("allow-nether", true);

    //Level seed; an empty string means random generation
    public string LevelSeed => GetOrDefault("level-seed", string.Empty);

    //level-type default/flat/large_biomes/amplified
    public string LevelType => GetOrDefault("level-type", "default");

    //Max world size in chunks, default 29999984
    public int MaxWorldSize => GetInt("max-world-size", 29999984);

    //Server description (motd), default A Minecraft Server
    public string Motd => GetOrDefault("motd", "A Minecraft Server");

    //Whether RCON remote management is enabled, default false
    public bool EnableRcon => GetBool("enable-rcon", false);

    //Whether the Query protocol is enabled, default false
    public bool EnableQuery => GetBool("enable-query", false);

    //RconPort RCON listen port, maps to vanilla rcon.port, default 25575
    public int RconPort => GetInt("rcon.port", 25575);

    //RconPassword RCON password; an empty string means unconfigured, maps to vanilla rcon.password
    public string RconPassword => GetOrDefault("rcon.password", string.Empty);

    //BroadcastRconToOps whether RCON results are broadcast to ops, maps to vanilla broadcast-rcon-to-ops
    public bool BroadcastRconToOps => GetBool("broadcast-rcon-to-ops", true);

    //QueryPort GS4 query protocol listen port, maps to vanilla query.port, default 25565
    public int QueryPort => GetInt("query.port", 25565);

    //ServerIp bind address; an empty string means all interfaces, maps to vanilla server-ip
    public string ServerIp => GetOrDefault("server-ip", string.Empty);

    //FunctionPermissionLevel function compile permission level, maps to vanilla function-permission-level, default 2
    public int FunctionPermissionLevel => GetInt("function-permission-level", 2);

    //Whether to report server runtime metrics, default false, maps to vanilla enable-jmx-monitoring
    public bool EnableJmxMonitoring => GetBool("enable-jmx-monitoring", false);

    //op-permission-level permission level granted by default when running /op, default 4
    public int OpPermissionLevel => GetInt("op-permission-level", 4);

    //PlayerIdleTimeout minutes before an idle player is kicked; 0 means never, maps to vanilla player-idle-timeout
    public int PlayerIdleTimeout => GetInt("player-idle-timeout", 0);

    //NcDebugCommands whether to register the /debug command tree; an NC-specific switch, disabled by default, maps to nc-debug-commands
    //debug can directly change world blocks and spawn entities, so it should not be available by default in production
    public bool NcDebugCommands => GetBool("nc-debug-commands", false);

    //NcLanguage language code for the server panel and logs, maps to nc-language
    //The vanilla server is not localized; this is NC-specific, at the same level as nc-debug-commands, not mixed with vanilla config names
    public string NcLanguage => GetOrDefault("nc-language", "en_us");

    //SetGamemode rewrites the default game mode and writes it to disk; used by the defaultgamemode command
    public void SetGamemode(string name) => Set("gamemode", name);

    //SetDifficulty rewrites the difficulty and writes it to disk; used by the difficulty command
    public void SetDifficulty(string name) => Set("difficulty", name);

    //SetPlayerIdleTimeout rewrites the idle kick minutes and writes it to disk; used by the setidletimeout command
    public void SetPlayerIdleTimeout(int minutes) => SetInt("player-idle-timeout", minutes);

    //SetWhiteList rewrites the whitelist switch and writes it to disk; used by the whitelist command
    public void SetWhiteList(bool enabled) => SetBool("white-list", enabled);

    //SaveCurrent immediately writes the current config back to server.properties
    public void SaveCurrent() => Save(AppPaths.ServerPropertiesPath);

    //Load and apply defaults; if the file does not exist, generate a default server.properties
    public static ServerSettings LoadOrGenerate(string path)
    {
        var settings = new ServerSettings();
        if (File.Exists(path))
        {
            settings.Load(path);
            //Fill in keys missing from old files; otherwise config options added later would never appear on existing saves
            Log.Info(settings.EnsureDefaults(path)
                ? $"Loaded server settings {path} and filled in missing defaults"
                : $"Loaded server settings {path}");
        }
        else
        {
            Log.Info($"server.properties missing, generating default config at {path}");
            settings.SaveDefault(path);
        }
        return settings;
    }

    //DefaultEntries supported config options and their default values
    //Shared between generating the default file and filling old files, so the two lists do not diverge and miss keys
    private IEnumerable<(string Key, string Value)> DefaultEntries()
    {
        yield return ("server-port", ServerPort.ToString());
        yield return ("max-players", MaxPlayers.ToString());
        yield return ("level-name", LevelName);
        yield return ("gamemode", Gamemode);
        yield return ("difficulty", Difficulty);
        yield return ("online-mode", OnlineMode ? "true" : "false");
        yield return ("pvp", AllowPvp ? "true" : "false");
        yield return ("view-distance", ViewDistance.ToString());
        yield return ("simulation-distance", SimulationDistance.ToString());
        yield return ("allow-flight", AllowFlight ? "true" : "false");
        yield return ("spawn-animals", SpawnAnimals ? "true" : "false");
        yield return ("spawn-monsters", SpawnMonsters ? "true" : "false");
        yield return ("spawn-npcs", SpawnNpcs ? "true" : "false");
        yield return ("white-list", WhiteList ? "true" : "false");
        yield return ("generate-structures", GenerateStructures ? "true" : "false");
        yield return ("allow-nether", AllowNether ? "true" : "false");
        yield return ("level-seed", LevelSeed);
        yield return ("level-type", LevelType);
        yield return ("max-world-size", MaxWorldSize.ToString());
        yield return ("motd", Motd);
        yield return ("enable-rcon", EnableRcon ? "true" : "false");
        yield return ("enable-query", EnableQuery ? "true" : "false");
        yield return ("rcon.port", RconPort.ToString());
        yield return ("rcon.password", RconPassword);
        yield return ("broadcast-rcon-to-ops", BroadcastRconToOps ? "true" : "false");
        yield return ("query.port", QueryPort.ToString());
        yield return ("server-ip", ServerIp);
        yield return ("function-permission-level", FunctionPermissionLevel.ToString());
        yield return ("op-permission-level", OpPermissionLevel.ToString());
        yield return ("player-idle-timeout", PlayerIdleTimeout.ToString());
        yield return ("nc-debug-commands", NcDebugCommands ? "true" : "false");
        yield return ("nc-language", NcLanguage);
    }

    //Generate a default server.properties at the given path
    public void SaveDefault(string path)
    {
        foreach (var (key, value) in DefaultEntries()) Set(key, value);
        Save(path);
    }

    //EnsureDefaults fills missing config options in the file with defaults and writes to disk
    //Only adds, never overwrites; values the user already wrote are preserved as is. Returns whether anything was added; if nothing was, the file is not rewritten
    public bool EnsureDefaults(string path)
    {
        var added = false;
        foreach (var (key, value) in DefaultEntries())
        {
            if (Properties.ContainsKey(key)) continue;
            Set(key, value);
            added = true;
        }
        if (added) Save(path);
        return added;
    }
}
