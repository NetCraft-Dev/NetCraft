using NetCraft.Logging;

namespace NetCraft;

//ServerSettings 服务端 server.properties 配置
//对应原版 net.minecraft.server.dedicated.Settings 简化版
//继承 Settings<ServerSettings> 用泛型自引用对齐原版 self type 模式
//仅保留核心字段端口/世界名/难度/max-players 等
public sealed class ServerSettings : Settings<ServerSettings>
{
    //服务器监听端口默认 25565
    public int ServerPort => GetInt("server-port", 25565);

    //最大玩家数默认 20
    public int MaxPlayers => GetInt("max-players", 20);

    //世界名称默认 world
    public string LevelName => GetOrDefault("level-name", "world");

    //游戏模式 survival/creative/adventure/spectator
    public string Gamemode => GetOrDefault("gamemode", "survival");

    //难度 peaceful/easy/normal/hard
    public string Difficulty => GetOrDefault("difficulty", "easy");

    //是否开启正版验证默认 true
    public bool OnlineMode => GetBool("online-mode", true);

    //是否允许 PVP 默认 true
    public bool AllowPvp => GetBool("pvp", true);

    //视野距离单位 chunk 默认 10
    public int ViewDistance => GetInt("view-distance", 10);

    //模拟距离单位 chunk 默认 10 对应原版 simulation-distance
    //决定"真正参与 tick 的范围" 视距比它大的时候 中间那一圈就是只加载不 tick 的弱加载
    public int SimulationDistance => GetInt("simulation-distance", 10);

    //是否允许飞行默认 false
    public bool AllowFlight => GetBool("allow-flight", false);

    //是否生成动物默认 true
    public bool SpawnAnimals => GetBool("spawn-animals", true);

    //是否生成怪物默认 true
    public bool SpawnMonsters => GetBool("spawn-monsters", true);

    //是否生成 NPC 默认 true
    public bool SpawnNpcs => GetBool("spawn-npcs", true);

    //是否启用白名单默认 false
    public bool WhiteList => GetBool("white-list", false);

    //是否生成结构默认 true
    public bool GenerateStructures => GetBool("generate-structures", true);

    //是否允许下界默认 true
    public bool AllowNether => GetBool("allow-nether", true);

    //世界种子空字符串表示随机生成
    public string LevelSeed => GetOrDefault("level-seed", string.Empty);

    //level-type default/flat/large_biomes/amplified
    public string LevelType => GetOrDefault("level-type", "default");

    //最大世界大小单位 chunk 默认 29999984
    public int MaxWorldSize => GetInt("max-world-size", 29999984);

    //服务器描述 motd 默认 A Minecraft Server
    public string Motd => GetOrDefault("motd", "A Minecraft Server");

    //是否启用 RCON 远程管理默认 false
    public bool EnableRcon => GetBool("enable-rcon", false);

    //是否启用 Query 协议默认 false
    public bool EnableQuery => GetBool("enable-query", false);

    //op-permission-level 执行 /op 时默认授予的权限等级 默认 4
    public int OpPermissionLevel => GetInt("op-permission-level", 4);

    //PlayerIdleTimeout 玩家挂机踢出分钟数 0 表示不踢 对应原版 player-idle-timeout
    public int PlayerIdleTimeout => GetInt("player-idle-timeout", 0);

    //NcDebugCommands 是否注册 /debug 命令树 nc 专属开关 默认禁用 对应 nc-debug-commands
    //debug 能直接改世界方块与造实体 生产环境不该默认可用
    public bool NcDebugCommands => GetBool("nc-debug-commands", false);

    //NcLanguage 服务端面板与日志的语言码 对应 nc-language
    //原版服务端不做本地化 这一项是 nc 专属 与 nc-debug-commands 同级 不与原版配置混名
    public string NcLanguage => GetOrDefault("nc-language", "en_us");

    //SetGamemode 改写默认游戏模式并落盘 defaultgamemode 命令用
    public void SetGamemode(string name) => Set("gamemode", name);

    //SetDifficulty 改写难度并落盘 difficulty 命令用
    public void SetDifficulty(string name) => Set("difficulty", name);

    //SetPlayerIdleTimeout 改写挂机踢出分钟数并落盘 setidletimeout 命令用
    public void SetPlayerIdleTimeout(int minutes) => SetInt("player-idle-timeout", minutes);

    //SetWhiteList 改写白名单开关并落盘 whitelist 命令用
    public void SetWhiteList(bool enabled) => SetBool("white-list", enabled);

    //SaveCurrent 把当前配置立刻写回 server.properties
    public void SaveCurrent() => Save(AppPaths.ServerPropertiesPath);

    //加载并应用默认值若文件不存在生成默认 server.properties
    public static ServerSettings LoadOrGenerate(string path)
    {
        var settings = new ServerSettings();
        if (File.Exists(path))
        {
            settings.Load(path);
            //补齐老文件缺的键 否则后加入的配置项在已有存档上永远不会出现
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

    //DefaultEntries 受支持的配置项与各自默认值
    //生成默认文件与补齐老文件共用这一份 免得两处清单各写各的又漏键
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
        yield return ("op-permission-level", OpPermissionLevel.ToString());
        yield return ("player-idle-timeout", PlayerIdleTimeout.ToString());
        yield return ("nc-debug-commands", NcDebugCommands ? "true" : "false");
        yield return ("nc-language", NcLanguage);
    }

    //生成默认 server.properties 写入路径
    public void SaveDefault(string path)
    {
        foreach (var (key, value) in DefaultEntries()) Set(key, value);
        Save(path);
    }

    //EnsureDefaults 把文件里缺失的配置项按默认值补上并落盘
    //只补不覆盖 用户已经写过的值原样保留 返回是否真的补过 没补就不用重写文件
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
