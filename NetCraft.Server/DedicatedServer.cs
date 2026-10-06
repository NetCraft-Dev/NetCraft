using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Threading;
using NetCraft.DataFixer;
using NetCraft.Game.Commands;
using NetCraft.Game.DFU;
using NetCraft.Game.Network;
using NetCraft.Game.Server.Rcon;
using NetCraft.Game.Server.Rcon.Thread;
using NetCraft.Game.Util.Monitoring.Jmx;
using NetCraft.Network.Protocol.Configuration;
using NetCraft.Network.Protocol.Common;
using NetCraft.Network.Protocol.Handshake;
using NetCraft.Network.Protocol.Login;
using NetCraft.Network.Protocol.Status;
using NetCraft.Game.World.Clock;
using NetCraft.Game.World.Entity;
using NetCraft.Game.World.Level;
using NetCraft.Game.World.Level.LevelGen;
using NetCraft.Game.World.Level.LevelGen.Dimension;
using NetCraft.Game.World.Level.Timers;
using NetCraft.Game.World.Phys.Collision;
using NetCraft.Logging;
using NetCraft.Network;
using NetCraft.Network.Protocol;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using NetCraft.Storage.Paletted;
using NetCraft.Util.Random;
using NetCraft.Util.Thread;
//注册表与关卡定义各有一个 DimensionType 前者是标记接口 这里固定指 Game 层的真实类型
using GameDimensionType = NetCraft.Game.World.Level.LevelGen.Dimension.DimensionType;

namespace NetCraft.Game.Server;

//DedicatedServer 专用服务端对应原版 net.minecraft.server.dedicated.DedicatedServer
//继承 MinecraftServer 接入 ServerSettings 与 LevelStorageAccess 持有世界存储引用
//阶段 11.35 接入构造与配置字段阶段 11.46 接入 PersistentServerLevel 与 tick 间隔控制
//阶段 11.47 接入玩家 Connection 列表与 level.Tick 调度对齐原版 tickChildren 调用顺序
//阶段 11.63 接入 ConnectionAcceptor 端口监听与 PlayerList 玩家管理实现 ServerHandshake/Login/Configuration 三阶段 Context 路由
//每 AutoSaveIntervalTicks tick 自动刷盘 Stop 时强制刷盘避免数据丢失
public sealed class DedicatedServer : MinecraftServer, ServerHandshakeContext, ServerLoginContext, ServerConfigurationContext, IDisposable
{
    //AutoSaveIntervalTicks 自动刷盘间隔对应原版 autosave.period 默认 6000 tick 约 5 分钟
    public const int AutoSaveIntervalTicks = 6000;

    //RandomTickRadius 随机刻作用的区块半径 对应原版实体 ticking 区块范围
    private const int RandomTickRadius = 8;

    private readonly ServerSettings _settings;
    private readonly LevelStorageAccess _levelAccess;
    private readonly NetCraft.DataFixer.DataFixer _dataFixer;
    private readonly PersistentServerLevel _overworld;
    //_overworldGenerator 主世界生成器 出生点气候搜索要用它取 spawn_target 与气候采样器
    private readonly ChunkGenerator? _overworldGenerator;
    //_levels 全部维度世界 键是维度标识 主世界一定在其中且与 _overworld 是同一个实例
    private readonly Dictionary<ResourceKey<Level>, PersistentServerLevel> _levels = new();
    private readonly List<Connection> _connections = new();
    private readonly PlayerList _playerList;
    //_opList 管理员名单 权限等级判定与 /op /deop 读写来源
    private readonly OpList _opList;
    //_banList 玩家封禁名单 登录校验与 /ban /pardon /banlist 读写来源
    private readonly BanList _banList;
    //_ipBanList IP 封禁名单 登录校验与 /ban-ip /pardon-ip 读写来源
    private readonly IpBanList _ipBanList;
    //_dataStorage 主世界 data 目录的 SavedData 存储 持 world_clocks.dat 等维度级数据
    private readonly SavedDataStorage _dataStorage;
    //_entityStorages 实体独立落盘存储 按维度各一份 挂各维度目录下的 entities
    private readonly Dictionary<ResourceKey<Level>, EntityStorage> _entityStorages = new();
    //_levelData 世界元数据 持 level.dat 读写 autosave 与 Stop 时落盘
    private readonly LevelData _levelData;
    //_playerData 玩家数据存档 玩家退出与刷盘时写 playerdata/<uuid>.dat
    private readonly PlayerDataStorage _playerData;
    //_worldGenSettings 世界生成设置 种子固化在存档 data/minecraft/world_gen_settings.dat
    private readonly WorldGenSettingsData _worldGenSettings;
    //_weatherData 天气状态 雨雷计时与目标状态 存 data/minecraft/weather.dat
    private readonly WeatherData _weatherData;
    //_chunkTickets 区块票存档 每个维度一份 存 data/minecraft/chunk_tickets*.dat
    //读档先入停用区 出生点区块准备完之后才激活 关服前重新停用并落盘
    private readonly Dictionary<ResourceKey<Level>, TicketStorage> _chunkTickets = new();
    private readonly BlockEntityManager _blockEntities = new();
    //_entityTracker 实体追踪器 按玩家视距下发实体进出与移动同步包
    private readonly EntityTracker _entityTracker = new();
    //_debugPlayers 假玩家管理器 供 /debug join 造指令驱动的假客户端
    private readonly DebugPlayerManager _debugPlayers;
    //_tickRandom 随机刻随机源 与区块生成用的随机源分开避免相互干扰
    private readonly RandomSource _tickRandom = RandomSource.Create();
    //_randomTickChunks 本刻要跑随机刻的区块 按玩家位置每刻重算 免得遍历全部已加载区块
    private readonly HashSet<long> _randomTickChunks = new();
    //_autoSaveTask 上次自动刷盘的后台写盘任务 未完成时跳过下次自动刷盘
    private Task? _autoSaveTask;
    private readonly ServerStatus _serverStatus;
    private readonly ReloadableServerResources? _rsr;
    private ConnectionAcceptor? _acceptor;
    private bool _disposed;
    //_registryAccess 世界数据包(ItemStack 等)编解码需要的注册表集合 首次进入 Configuration 时惰性构建
    private RegistryAccess? _registryAccess;
    //_commandStorage 命令存储 /data ... storage 的目标 首次用到时挂上 SavedData 存储
    private CommandStorage? _commandStorage;
    //_statistics 运行指标上报 仅 enable-jmx-monitoring 开启时非空
    private MinecraftServerStatistics? _statistics;
    //_rconConsoleSource RCON 命令执行者与输出缓冲
    private readonly RconConsoleSource _rconConsoleSource;
    //_rconThread RCON 监听线程 仅 enable-rcon 且密码已配置时非空
    private RconThread? _rconThread;
    //_queryThreadGs4 GS4 查询线程 仅 enable-query 且端口有效时非空
    private QueryThreadGs4? _queryThreadGs4;

    //RegistryAccessForConnection 惰性构建注册表访问集合
    //必须等 BootstrapClass.BootStrap 冻结注册表之后才可构建 服务端构造时已满足
    private RegistryAccess RegistryAccessForConnection
        => _registryAccess ??= BuiltInRegistries.CreateRegistryAccess();

    //Settings 服务端配置 server.properties 加载结果
    public override ServerSettings Settings => _settings;

    //LevelAccess 世界存储访问入口
    public LevelStorageAccess LevelAccess => _levelAccess;

    //Overworld 主世界 PersistentServerLevel 接入 RegionFileStorage
    public override PersistentServerLevel Overworld => _overworld;

    //Levels 全部维度世界供 tick 遍历与诊断
    public IEnumerable<PersistentServerLevel> Levels => _levels.Values;

    //GetLevel 按维度标识取世界 该维度没建时返回 null
    public override PersistentServerLevel? GetLevel(ResourceKey<Level> key)
        => _levels.TryGetValue(key, out var level) ? level : null;

    //CreateLevel 按维度建一个持久化世界
    //高度与生成器优先取关卡定义 无定义时退回传入的默认值（只有主世界有生成器兜底）
    private PersistentServerLevel CreateLevel(ResourceKey<Level> key, int defaultMinSectionY, int defaultSectionsCount,
        PalettedContainerFactory? factory, RegistryAccess? registryAccess, ChunkGenerator? fallbackGenerator,
        RandomSource? random, long worldSeed)
    {
        var stem = WorldPresets.Get(key.Identifier);
        var dimensionType = stem is null
            ? null
            : BuiltInRegistries.DIMENSION_TYPE.GetValue(stem.TypeId) as GameDimensionType;
        var minSectionY = dimensionType?.MinSectionY ?? defaultMinSectionY;
        var sectionsCount = dimensionType?.SectionCount ?? defaultSectionsCount;
        var chunkGenerator = stem?.Generator ?? fallbackGenerator;
        //存档未命中时走 ChunkStatusProcessor 流水线 random 保证生成可重现
        Func<ChunkPos, ChunkAccess?>? generator = null;
        NetCraft.Game.World.Level.LevelGen.Structure.StructureFeatureManager? structures = null;
        PersistentServerLevel? created = null;
        if (chunkGenerator is not null)
        {
            var pf = factory ?? PalettedContainerFactory.Default;
            //邻块只取已加载区块 未加载的邻居返回 null 装饰退化为只看中心区块
            //生成期不能触发新加载 否则邻块链会一路递归下去
            var built = ChunkGenerationHelper.CreateGenerator(
                chunkGenerator, minSectionY, sectionsCount, random ?? RandomSource.Create(), pf,
                worldSeed, (x, z) => created?.ChunkSource.GetLoadedChunk(x, z),
                _settings.GenerateStructures);
            generator = built.Generator;
            structures = built.Structures;
        }
        var regionStorage = _levelAccess.CreateRegionStorage(key, _dataFixer, DataFixTypes.Chunk);
        created = new PersistentServerLevel(
            regionStorage,
            minSectionY, sectionsCount,
            factory,
            key.Identifier,
            SharedConstants.WorldDataVersion,
            registryAccess,
            _settings.ViewDistance,
            generator,
            _settings.SimulationDistance);
        //结构桥在关卡建好之后挂上 区块落盘打包与读档还原都经它
        if (structures is not null)
        {
            created.StructureDataBridge = new ServerStructureDataBridge(structures,
                NetCraft.Game.World.Level.LevelGen.Structure.StructurePieceSerializationContext.FromManager(
                    Bootstrap.GameBootstrap.StructureTemplates));
        }
        return created;
    }

    //CreateExtraLevels 按关卡定义建主世界之外的维度
    //没有关卡定义就不建 数据包缺失时宁可退化成单世界也不要用错生成器造出假维度
    private void CreateExtraLevels(PalettedContainerFactory? factory, RegistryAccess? registryAccess,
        RandomSource? random, long worldSeed)
    {
        foreach (var key in new[] { LevelKeys.NETHER, LevelKeys.END })
        {
            if (WorldPresets.Get(key.Identifier) is null) continue;
            //无关卡定义时的默认高度取下界与末地共用的 0 起 8 段
            _levels[key] = CreateLevel(key, 0, 8, factory, registryAccess, null, random, worldSeed);
        }
    }

    //WireLevel 给一个维度挂上 Game 层副作用出口
    //方块更新/光照/方块实体/实体碰撞/实体死亡这些回调每个维度都要各挂一份 少挂一个该维度就断链
    private void WireLevel(PersistentServerLevel level)
    {
        //光照变化经玩家集合同步给客户端 区块包之外的光照变化只能靠这条链路下发
        level.LightUpdateSink = BroadcastLightUpdate;
        //方块更新链的副作用出口 方块实体移除与方块销毁都从这里回调
        level.BlockUpdateSink = new ServerBlockUpdateSink(level, _playerList, _blockEntities);
        //方块实体随区块落盘与卸载清理都经这条桥 Game 层负责按 id 编解码
        level.BlockEntityBridge = new ServerBlockEntityBridge(level, _blockEntities);
        //玩家不在实体管理器里 压力板那类实体进入判定要额外算上玩家包围盒
        level.ExtraEntityBoxes = PlayerBoxes;
        //实体移动的形状碰撞 关卡层问不了方块行为 用碰撞视图按实体口径取方块碰撞形状
        CollisionGetter collisionView = new LevelCollisionGetter(level, level.MinSectionY, level.SectionsCount);
        level.CollisionShapeProvider = (entity, box) => collisionView.GetBlockCollisions(entity, box).ToList();
        //实体死亡回调 由关卡在实体加入时挂接 广播死亡事件后把实体移出世界
        level.EntityDeathCallback = OnEntityDied;
    }

    //CreateEntityStorage 按维度建实体落盘存储 主世界落在世界根目录 其余维度落在各自维度目录
    private EntityStorage CreateEntityStorage(ResourceKey<Level> key)
        => new(
            new SimpleRegionStorage(
                new RegionStorageInfo(_settings.LevelName, key, "entities"),
                Path.Combine(_levelAccess.GetDimensionPath(key), "entities"),
                _dataFixer,
                syncWrites: true,
                DataFixTypes.EntityChunk),
            DefaultThreadPoolExecutor.Instance,
            RegistryAccessForConnection,
            EntityPersister.Load,
            EntityPersister.Save);

    //DataFixer 存档升级器由 GameDataFixers.BuildV1_21Fixer 构建传入
    public NetCraft.DataFixer.DataFixer DataFixer => _dataFixer;

    //Connections 已接入的玩家连接列表只读视图供外部诊断
    public override IReadOnlyList<Connection> Connections => _connections;

    //PlayerList 在线玩家集合管理
    public override PlayerList PlayerList => _playerList;

    //ClockManager 世界时钟管理器 推进/修改各 WorldClock 状态并随存档持久化
    public override ServerClockManager ClockManager { get; }

    //BlockEntities 方块实体集合 服务端每帧 tick 并可按需下发 ClientboundBlockEntityData
    public override BlockEntityManager BlockEntities => _blockEntities;

    //CommandStorage 命令存储 对应原版 MinecraftServer.getCommandStorage
    public override CommandStorage CommandStorage => _commandStorage ??= new CommandStorage(_dataStorage);

    //Stopwatches 调试计时器集合 对应原版 MinecraftServer.getStopwatches
    public override Stopwatches Stopwatches { get; } = new();

    //EntityTracker 实体追踪器 供诊断与测试直接驱动
    public override EntityTracker EntityTracker => _entityTracker;

    //DebugPlayers 假玩家管理器 /debug join 与 /debug player 的操作入口
    public override DebugPlayerManager DebugPlayers => _debugPlayers;

    //ServerStatus 服务器状态响应 StatusRequest 用
    public ServerStatus ServerStatus => _serverStatus;

    //ServerResources 服务端可重载资源集合持有 ResourceManager 与 Tags
    //为后续 /reload 命令重载 Tags/Recipes/Advancements 等数据驱动内容铺路
    public ReloadableServerResources? ServerResources => _rsr;

    //Commands 命令管理器 持命令树 进世界时下发 收到上行命令包时执行
    public override CommandManager Commands { get; }

    //DefaultGameType 服务端默认游戏模式存档存在时以 level.dat 为准否则由 settings.gamemode 解析
    //玩家加入时 PlayerList.PlaceNewPlayer 应用此模式 defaultgamemode 命令可改
    private GameType _defaultGameType;
    public override GameType DefaultGameType => _defaultGameType;

    //WorldSeed 世界种子用于 LoginPacket SpawnInfo 同步 未传默认 0
    public override long WorldSeed { get; }

    //LevelData 世界元数据 供外部读取种子外的时间出生点等状态
    public override LevelData LevelData => _levelData;

    //PlayerData 玩家数据存档 入服读取与退出/刷盘时写入
    public override PlayerDataStorage PlayerData => _playerData;

    //OpList 管理员名单 权限等级来源 列表变更即落盘 ops.json
    public override OpList OpList => _opList;

    //BanList 玩家封禁名单 列表变更即落盘 banned-players.json
    public override BanList BanList => _banList;

    //IpBanList IP 封禁名单 列表变更即落盘 banned-ips.json
    public override IpBanList IpBanList => _ipBanList;

    //WhiteList 白名单名单 列表变更即落盘 whitelist.json
    public override WhiteList WhiteList { get; } = new(AppPaths.WhitelistPath);

    //IsWhiteListEnabled 白名单是否启用 对应原版 PlayerList.isUsingWhitelist
    //构造时取 server.properties 的 white-list 之后由 whitelist on/off 命令改
    public override bool IsWhiteListEnabled { get; set; }

    //GameRules 游戏规则存档 供 /gamerule 命令接入时读写
    public override GameRuleMapData GameRules { get; }

    //SpawnPos 世界出生点 从 level.dat 恢复新世界默认 (0,64,0)
    //配置阶段出生点预载与 SetDefaultSpawnPosition 均以此为准
    private Vec3 _spawnPos = new(0, 64, 0);
    public override Vec3 SpawnPos => _spawnPos;

    //SetDefaultGameType defaultgamemode 命令改默认模式 只影响之后加入的玩家
    public override void SetDefaultGameType(GameType gameType) => _defaultGameType = gameType;

    //SetSpawnPos spawnpoint/setworldspawn 改写世界出生点 立即回写 level.dat
    public override void SetSpawnPos(Vec3 pos)
    {
        _spawnPos = pos;
        _levelData.SpawnX = (int)Math.Floor(pos.X);
        _levelData.SpawnY = (int)Math.Floor(pos.Y);
        _levelData.SpawnZ = (int)Math.Floor(pos.Z);
        SaveLevelData();
    }

    public DedicatedServer(
        Thread serverThread,
        ServerSettings settings,
        LevelStorageAccess levelAccess,
        NetCraft.DataFixer.DataFixer? dataFixer = null,
        RegistryAccess? registryAccess = null,
        PalettedContainerFactory? factory = null,
        int minSectionY = -4,
        int sectionsCount = 24,
        ChunkGenerator? chunkGenerator = null,
        RandomSource? random = null,
        ReloadableServerResources? rsr = null,
        long? worldSeed = null,
        LevelData? levelData = null,
        OpList? opList = null,
        BanList? banList = null,
        IpBanList? ipBanList = null)
        : base(serverThread)
    {
        //_settings 必须先赋值 CommandManager 构造要读 nc-debug-commands 决定是否注册 /debug
        _settings = settings;
        Commands = new CommandManager(this);
        //函数库与函数管理器对应原版 resources.managers.getFunctionLibrary 与 new ServerFunctionManager
        //编译权限按 function-permission-level 对应原版 getFunctionCompilationPermissions
        var functionLibrary = new ServerFunctionLibrary(
            LevelBasedPermissionSet.ForLevel((NetCraft.Registry.PermissionLevel)Math.Clamp(_settings.FunctionPermissionLevel, 0, 4)),
            Commands.Dispatcher);
        Functions = new ServerFunctionManager(this, functionLibrary);
        _levelAccess = levelAccess;
        _dataFixer = dataFixer ?? GameDataFixers.BuildV1_21Fixer();
        _rsr = rsr;
        //level.dat 存在则恢复存档元数据否则按 server.properties 初始化新世界
        _levelData = levelData ?? new LevelData
        {
            LevelName = settings.LevelName,
            GameTypeId = (GameType.ByName(settings.Gamemode) ?? GameType.Survival).Id,
            DifficultyName = settings.Difficulty,
            //新世界出生点还没定 记未初始化 启动时按原版 setInitialSpawn 搜索
            Initialized = false,
        };
        //按关卡定义建主世界 高度与生成器从维度配置推 无配置时退回构造参数与传入的生成器
        //出生点气候搜索要用主世界生成器 取值口径与 CreateLevel 内保持一致
        _overworldGenerator = WorldPresets.Get(LevelKeys.OVERWORLD.Identifier)?.Generator ?? chunkGenerator;
        _overworld = CreateLevel(LevelKeys.OVERWORLD, minSectionY, sectionsCount, factory, registryAccess,
            chunkGenerator, random, worldSeed ?? 0);
        _levels[LevelKeys.OVERWORLD] = _overworld;
        _playerList = new PlayerList(this, settings.MaxPlayers);
        //刻速率状态变化经玩家集合同步给客户端 对应原版 tickRateManager 取 server.getPlayerList() 广播
        TickRate.Players = _playerList;
        //主世界之外的维度按关卡定义建 数据包没定义就不建 免得用错生成器造出假维度
        CreateExtraLevels(factory, registryAccess, random, worldSeed ?? 0);
        //方块更新链的 Game 层副作用出口每个维度各挂一份 少挂一个该维度的更新链就断
        foreach (var level in _levels.Values) WireLevel(level);
        //管理员名单挂程序根目录 ops.json 对齐原版服务端根目录位置
        _opList = opList ?? new OpList(AppPaths.OpsPath);
        //封禁名单同样挂程序根目录 banned-players.json 与 banned-ips.json
        _banList = banList ?? new BanList(AppPaths.BannedPlayersPath);
        _ipBanList = ipBanList ?? new IpBanList(AppPaths.BannedIpsPath);
        IsWhiteListEnabled = settings.WhiteList;
        //假玩家管理器只持有服务端引用 构造期不做实际操作
        _debugPlayers = new DebugPlayerManager(this);
        //旧档恢复世界游戏时间与出生点新世界保持默认 0 与 (0,64,0)
        _overworld.GameTime = _levelData.GameTime;
        _spawnPos = new Vec3(_levelData.SpawnX, _levelData.SpawnY, _levelData.SpawnZ);
        //SavedDataStorage 挂主世界 data 目录 对应原版 overworld.getDataStorage
        _dataStorage = new SavedDataStorage(
            Path.Combine(levelAccess.GetDimensionPath(LevelKeys.OVERWORLD), "data"), _dataFixer);
        _dataStorage.SetRegistryAccess(RegistryAccessForConnection);
        //玩家数据存档挂世界根目录下的 playerdata 对应原版 PlayerDataStorage
        _playerData = new PlayerDataStorage(levelAccess.WorldDir);
        //实体独立落盘存储按维度各一份 对应原版每个 ServerLevel 各自的 entities 目录
        //loader/saver 由 Game 层 EntityPersister 提供 避免 Storage 层依赖具体实体类型
        foreach (var (key, level) in _levels)
        {
            var storage = CreateEntityStorage(key);
            _entityStorages[key] = storage;
            level.AttachEntityStorage(storage);
        }
        ClockManager = _dataStorage.ComputeIfAbsent(ServerClockManager.Type);
        ClockManager.Init(this);
        //世界生成设置与游戏规则从存档恢复新世界时固化种子
        _worldGenSettings = _dataStorage.ComputeIfAbsent(WorldGenSettingsData.Type);
        GameRules = _dataStorage.ComputeIfAbsent(GameRuleMapData.Type);
        //计划事件队列存档对应原版 MinecraftServer 构造里的 computeIfAbsent(TimerQueue.TYPE)
        ScheduledEvents = _dataStorage.ComputeIfAbsent(TimerQueueTypes.Instance);
        //世界边界存档挂主世界 data 目录 对应原版 ServerLevel.getWorldBorder 的 computeIfAbsent
        //装载后把存档参数灌进运行时字段 之后以运行时状态为准
        _overworld.WorldBorder = _dataStorage.ComputeIfAbsent(WorldBorder.Type);
        _overworld.WorldBorder.ApplyInitialSettings(_overworld.GameTime);
        //边界改动经监听器转成网络包广播 对应原版 PlayerList.addWorldborderListener
        _overworld.WorldBorder.AddListener(new ServerWorldBorderListener(_playerList));
        //天气状态存档 服务器级一份 对应原版 MinecraftServer 里 computeIfAbsent(WeatherData.TYPE)
        _weatherData = _dataStorage.ComputeIfAbsent(WeatherData.Type);
        //区块票存档 每个维度各一份对应原版 level.getDataStorage().computeIfAbsent(TicketStorage.TYPE)
        //共用一份时每次接入新维度都会覆盖等级回调 只剩最后一个维度收得到票变化 别维度的票等级从此不收敛
        //读档只填停用区 等出生点区块准备完再激活 免得旧票在世界就绪前把区块拉起来
        foreach (var (key, level) in _levels)
        {
            var tickets = _dataStorage.ComputeIfAbsent(TicketStorage.TypeFor(key.Identifier));
            _chunkTickets[key] = tickets;
            level.ChunkSource.AttachTicketStorage(tickets);
        }
        //读档时正在下雨则雨量直接置满 对应原版 ServerLevel 构造里的 prepareWeather
        if (_overworld.CanHaveWeather() && _weatherData.Raining)
        {
            _overworld.RainLevel = 1.0f;
            if (_weatherData.Thundering) _overworld.ThunderLevel = 1.0f;
        }
        if (levelData is null)
        {
            _worldGenSettings.Seed = worldSeed ?? 0;
            _worldGenSettings.SetDirty();
        }
        //主世界默认时钟 对应原版 overworld 维度类型的 default_clock
        _overworld.DefaultClock = WorldClocks.OverworldHolder;
        _serverStatus = BuildServerStatus();
        //RCON 命令源与输出缓冲 对应原版 DedicatedServer 构造里的 rconConsoleSource
        _rconConsoleSource = new RconConsoleSource(this);
        //gamemode 由 server.properties 每次启动覆盖并写回存档对齐原版 setGameType 语义
        //difficulty 相反以存档为准对应原版 forceDifficulty 空实现
        _defaultGameType = GameType.ByName(settings.Gamemode) ?? GameType.Survival;
        _levelData.GameTypeId = DefaultGameType.Id;
        WorldSeed = worldSeed ?? 0;
    }

    //BuildServerStatus 构造 StatusRequest 响应数据
    private ServerStatus BuildServerStatus()
    {
        return new ServerStatus
        {
            Description = _settings.Motd,
            Players = new ServerStatus.PlayersData
            {
                Max = _settings.MaxPlayers,
                Online = 0,
            },
            Version = ServerStatus.VersionData.Current(),
        };
    }

    //StartNetwork 启动 TCP 监听线程接受新连接
    public void StartNetwork()
    {
        _acceptor = new ConnectionAcceptor(IPAddress.Any, _settings.ServerPort, OnNewConnection);
        _acceptor.Start();
        Log.Info($"Network listening on port {_settings.ServerPort}");
    }

    //RunRconCommand 执行一条 RCON 命令并返回输出对应原版 DedicatedServer.runCommand
    //先清缓冲 再投到主循环同步执行 最后取缓冲内容发回客户端
    public string RunRconCommand(string command)
    {
        _rconConsoleSource.PrepareForCommand();
        ExecuteBlocking(_rconConsoleSource.CreateCommandSourceStack(), command);
        return _rconConsoleSource.GetCommandResponse();
    }

    //ShouldRconBroadcast RCON 执行结果是否广播给 op 对应原版 shouldRconBroadcast
    //NC 命令层还没有 informAdmins 通道 先挂配置项供命令层后续接入
    public bool ShouldRconBroadcast => _settings.BroadcastRconToOps;

    //InitServer 启动阶段初始化对应原版 DedicatedServer.initServer 收尾
    //新世界先按原版 setInitialSpawn 搜出生点 再把出生点周边区块加载完才宣告就绪
    //对应原版 prepareLevels 的 LOAD_INITIAL_CHUNKS 阶段: 先加载后 Done
    public void InitServer()
    {
        if (!_levelData.Initialized) SearchInitialSpawn();
        StartNetwork();
        //函数库按数据包装载对应原版 reloadableServerResources.loadResources 里的函数库重载段
        //挂进资源监听列表之后 /reload 也会带着函数库一起重载
        _rsr?.AttachFunctionLibrary(Functions.Library);
        //指标上报默认关闭 与原版 enable-jmx-monitoring 一致
        if (_settings.EnableJmxMonitoring)
        {
            _statistics = MinecraftServerStatistics.Register(this);
            Log.Info("JMX monitoring enabled");
        }
        //GS4 查询监听顺序与原版 initServer 一致 query 在前 rcon 在后
        //创建失败(端口未配置或被占用)只告警不阻断开服 对应原版返回 null 的分支
        if (_settings.EnableQuery)
        {
            Log.Info("Starting GS4 status listener");
            _queryThreadGs4 = QueryThreadGs4.Create(this);
        }
        if (_settings.EnableRcon)
        {
            Log.Info("Starting remote control listener");
            _rconThread = RconThread.Create(this);
        }
        //启动即固化 level.dat 与 saveddata 对应原版 initServer 末尾 saveEverything
        //新世界种子立刻落盘防止窗口期内崩溃重启换种子
        SaveLevelData();
        _dataStorage.ScheduleSave();
        //旧档的票等世界就绪之后才允许驱动加载 对应原版 prepareLevels 里的 activateAllDeactivatedTickets
        //每个维度各激活自己那份 票表按维度隔离后不能只激活一份
        foreach (var tickets in _chunkTickets.Values) tickets.ActivateAllDeactivatedTickets();
        //原版 26.2 的 prepareLevels 不再预加载出生点 激活完存档票就宣告就绪
        //实测: 全新世界准备阶段只加载出生点那一格且 2 tick 后就卸掉 读旧档 0 格
        //出生点那片 3x3 强加载是玩家登录时 PrepareSpawnTask 挂 PLAYER_SPAWN 票才有的 见 PrepareSpawnChunks
        Log.Info($"Done ({StartWatch.Elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture)}s)! NetCraft server ready");
    }

    //SearchInitialSpawn 新世界出生点搜索 对应原版 MinecraftServer.setInitialSpawn
    //先按气候目标定候选落点 再从候选起逐区块找能站人的地表 命中即替换出生点 全未命中保留默认值
    private void SearchInitialSpawn()
    {
        var watch = Stopwatch.StartNew();
        var suggestion = ResolveSpawnSuggestion();
        var found = SpawnFinder.Find(_overworld, suggestion, _overworld.MinSectionY << 4);
        if (found is { } pos)
        {
            _spawnPos = new Vec3(pos.X, pos.Y, pos.Z);
            _levelData.SpawnX = pos.X;
            _levelData.SpawnY = pos.Y;
            _levelData.SpawnZ = pos.Z;
            Log.Info($"Spawn search finished ({pos.X},{pos.Y},{pos.Z}) in {watch.ElapsedMilliseconds}ms");
        }
        else Log.Warning("Spawn search found nothing, keeping the default spawn");
        //标记已初始化 下次启动直接沿用 level.dat 里的出生点
        _levelData.Initialized = true;
    }

    //ResolveSpawnSuggestion 出生点候选落点 对应原版 setInitialSpawn 里的 findSpawnPosition
    //按噪声设置的 spawn_target 做气候径向搜索 数据包没给目标时退回当前出生点
    private BlockPos ResolveSpawnSuggestion()
    {
        if (_overworldGenerator is NoiseBasedChunkGenerator noiseGen && noiseGen.FindSpawnPosition() is { } candidate)
        {
            Log.Info($"Spawn climate search candidate ({candidate.X},{candidate.Z})");
            return candidate;
        }
        Log.Warning("Spawn climate search unavailable, falling back to the current spawn as candidate");
        return new BlockPos((int)SpawnPos.X, (int)SpawnPos.Y, (int)SpawnPos.Z);
    }

    //KeepAliveSpawnTickets 续出生点预载票 对应原版 PrepareSpawnTask.Ready.keepAlive
    //PLAYER_SPAWN 只有 20 tick 超时 新世界的 7x7 生成往往更久 不续的话票过期区块被回收
    //与 PrepareSpawnChunks 出的是同一张票同一半径 同类型同等级会被 AddTicket 当成同一张只做续期
    public void KeepAliveSpawnTickets()
    {
        var spawnChunk = new ChunkPos((int)Math.Floor(SpawnPos.X / 16), (int)Math.Floor(SpawnPos.Z / 16));
        Overworld.ChunkSource.TicketStorage?.AddTicketWithRadius(
            NetCraft.Storage.TicketType.PlayerSpawn, spawnChunk, PrepareChunkRadius);
    }

    //OnEntityDied 实体血量归零后的收尾 对应原版 LivingEntity.die 与 remove(KILLED)
    //广播死亡事件 3 让客户端播死亡动画 再把实体移出关卡由追踪器下发移除包
    private void OnEntityDied(NetCraft.Registry.Entity entity)
    {
        Log.Info($"Entity died {entity.Id} entityId={entity.EntityId}");
        _playerList.BroadcastAll(
            new NetCraft.Game.Network.Protocol.Game.ClientboundEntityEventPacket(entity.EntityId, 3));
        _overworld.RemoveEntity(entity);
    }

    //BroadcastLightUpdate 把光照变化按区块打包成增量光照包广播 对应原版 ChunkMap.onLightUpdate
    //方块包只带状态不带光照 缺这一步客户端会一直用旧亮度 方块看着发白或发暗像幽灵方块
    private void BroadcastLightUpdate(ChunkPos pos, IReadOnlyList<int> skySections, IReadOnlyList<int> blockSections)
    {
        var engine = _overworld.ChunkSource.LightEngine;
        var count = engine.GetLightSectionCount();
        _playerList.BroadcastAll(new NetCraft.Game.Network.Protocol.Game.ClientboundLightUpdatePacket(
            pos, engine,
            NetCraft.Game.Network.Protocol.Game.ClientboundLightUpdatePacketData.CreateFilter(count, skySections),
            NetCraft.Game.Network.Protocol.Game.ClientboundLightUpdatePacketData.CreateFilter(count, blockSections)));
    }

    //PlayerBoxes 在线玩家的包围盒 玩家尺寸按原版 0.6x1.8 脚部为原点
    //压力板那类实体进入判定要看玩家 玩家不在实体管理器里只能这样补进来
    private IEnumerable<AABB> PlayerBoxes()
    {
        foreach (var player in _playerList.Players)
        {
            var pos = player.Position;
            yield return new AABB(pos.X - 0.3, pos.Y, pos.Z - 0.3, pos.X + 0.3, pos.Y + 1.8, pos.Z + 0.3);
        }
    }

    //CollectRandomTickChunks 收集该维度要跑随机刻的区块
    //原版遍历的是模拟等级 <= 31 的区块 也就是实体 ticking 范围 不是按玩家位置画方框
    //这里按已加载区块逐个问区块源: 模拟距离之外只加载不推进 正好对上弱加载带的语义
    //该维度没有玩家时模拟表为空 全部落空等于不跑随机刻
    private HashSet<long> CollectRandomTickChunks(PersistentServerLevel level)
    {
        _randomTickChunks.Clear();
        foreach (var chunk in level.ChunkSource.LoadedChunks)
        {
            var packed = chunk.Pos.Pack();
            if (level.ChunkSource.InEntityTickingRange(packed)) _randomTickChunks.Add(packed);
        }
        return _randomTickChunks;
    }

    //TickWeather 推进天气状态机并广播变化 对应原版 ServerLevel.advanceWeatherCycle
    //广播走主世界玩家集合 对应原版 broadcastAll(packet, dimension)
    private void TickWeather()
    {
        var level = _overworld;
        if (!level.CanHaveWeather()) return;
        var wasRaining = level.IsRaining;
        //advance_weather 规则只停计时推进 雨量仍按当前目标渐变 与原版一致
        if (GameRules.GetBool(NetCraft.Game.World.Level.GameRules.AdvanceWeather))
            WeatherCycle.AdvanceCycle(_weatherData, _tickRandom);
        WeatherCycle.AdvanceLevel(level, _weatherData);
        if (level.ORainLevel != level.RainLevel)
            _playerList.BroadcastAll(new NetCraft.Game.Network.Protocol.Game.ClientboundGameEventPacket(
                NetCraft.Game.Network.Protocol.Game.GameEventType.RainLevelChange, level.RainLevel));
        if (level.OThunderLevel != level.ThunderLevel)
            _playerList.BroadcastAll(new NetCraft.Game.Network.Protocol.Game.ClientboundGameEventPacket(
                NetCraft.Game.Network.Protocol.Game.GameEventType.ThunderLevelChange, level.ThunderLevel));
        if (wasRaining == level.IsRaining) return;
        //雨状态翻转时先发起止事件 再补一次雨雷等级 对应原版三段广播
        _playerList.BroadcastAll(new NetCraft.Game.Network.Protocol.Game.ClientboundGameEventPacket(
            wasRaining
                ? NetCraft.Game.Network.Protocol.Game.GameEventType.StopRaining
                : NetCraft.Game.Network.Protocol.Game.GameEventType.StartRaining,
            0.0f));
        _playerList.BroadcastAll(new NetCraft.Game.Network.Protocol.Game.ClientboundGameEventPacket(
            NetCraft.Game.Network.Protocol.Game.GameEventType.RainLevelChange, level.RainLevel));
        _playerList.BroadcastAll(new NetCraft.Game.Network.Protocol.Game.ClientboundGameEventPacket(
            NetCraft.Game.Network.Protocol.Game.GameEventType.ThunderLevelChange, level.ThunderLevel));
    }

    //SetWeatherParameters 直接设定天气参数 对应原版 MinecraftServer.setWeatherParameters
    //雷暴计时原版写的也是 rainTime 这里照抄保持一致
    public override void SetWeatherParameters(int clearTime, int rainTime, bool raining, bool thundering)
    {
        _weatherData.SetClearWeatherTime(clearTime);
        _weatherData.SetRainTime(rainTime);
        _weatherData.SetThunderTime(rainTime);
        _weatherData.SetRaining(raining);
        _weatherData.SetThundering(thundering);
    }

    //OnNewConnection 新连接回调装初始握手监听器
    private void OnNewConnection(Connection conn)
    {
        var handshake = new ServerHandshakePacketListenerImpl(conn, this);
        conn.SetListenerForServerboundHandshake(handshake);
        lock (_connections) _connections.Add(conn);
        Log.Debug($"New connection added, current connections {_connections.Count}");
    }

    //AddPlayer 把 Connection 加入调度列表对应原版 PlayerList.addPlayer
    //低层 API 测试用直接添加 Connection 不创建 ServerPlayer 不同于 PlayerList.PlaceNewPlayer
    public void AddPlayer(Connection connection)
    {
        lock (_connections) _connections.Add(connection);
    }

    //RemovePlayer 移除 Connection 返回是否成功
    public bool RemovePlayer(Connection connection)
    {
        lock (_connections) return _connections.Remove(connection);
    }

    //--- ServerHandshakeContext 实现 ---

    //TransitionToStatus 切换连接到 Status 阶段挂 ServerStatusPacketListenerImpl
    public void TransitionToStatus(Connection connection)
    {
        Log.Debug("TransitionToStatus");
        connection.SetListenerForServerboundStatus(new ServerStatusPacketListenerImpl(connection, _serverStatus));
    }

    //TransitionToLogin 切换连接到 Login 阶段挂 ServerLoginPacketListenerImpl
    public void TransitionToLogin(Connection connection)
    {
        Log.Debug("TransitionToLogin");
        connection.SetListenerForServerboundLogin(new ServerLoginPacketListenerImpl(connection, this));
    }

    //--- ServerLoginContext 实现 ---

    //TransitionToConfiguration 切换连接到 Configuration 阶段挂 ServerConfigurationPacketListenerImpl
    //原版流程 select_known_packs → 客户端回复 → registry_data(29 个) → finish_configuration
    //registry_data 挪到 HandleSelectKnownPacks 后发因 empty contents 依赖客户端本地已确认的 vanilla 资源
    public void TransitionToConfiguration(Connection connection, GameProfile profile)
    {
        Log.Debug($"TransitionToConfiguration profile={profile.Name}");
        //世界数据包编解码要按注册表解析(ItemStack 的物品 id 等) 进入该阶段前装好
        connection.RegistryAccess = RegistryAccessForConnection;
        connection.SetListenerForServerboundConfiguration(
            new ServerConfigurationPacketListenerImpl(connection, profile, this));
        try
        {
            //version 必须与客户端 SharedConstants.getCurrentVersion().id() 精确匹配否则客户端不选中该 pack
            //KnownPack.Vanilla 用的是 NetCraft 自己版本号(26.2-netcraft)不能用于协商
            var core = new NetCraft.Network.Protocol.Configuration.KnownPack("minecraft", "core", "26.2");
            connection.Send(new ClientboundSelectKnownPacks(new List<NetCraft.Network.Protocol.Configuration.KnownPack> { core }));
            Log.Debug($"TransitionToConfiguration sent SelectKnownPacks(minecraft:core) profile={profile.Name}");
        }
        catch (Exception e)
        {
            Log.Warning($"Configuration phase send failed {profile.Name} {e.Message}");
        }
    }

    //--- ServerConfigurationContext 实现 ---

    //SendSynchronizedRegistries 发送全部 SYNCHRONIZED_REGISTRIES 对齐原版 packRegistries
    //客户端没收到的注册表保持空表 nonEmpty 校验会断连(如 cat_variant)
    //biome 带服务端 contents(控制 id 顺序)其余 28 个只发 id 省略 contents 客户端从本地 vanilla 资源加载
    public void SendSynchronizedRegistries(Connection connection)
    {
        if (!BuiltInRegistries.BIOME.IsEmpty)
        {
            var registryId = BuiltInRegistries.BIOME.Key.Identifier;
            var entries = BiomeRegistrySynchronization.PackBiomes(BuiltInRegistries.BIOME.EntrySet);
            connection.Send(new ClientboundRegistryDataPacket(registryId, entries));
            Log.Debug($"Sent biome registry_data id={registryId} entries={BuiltInRegistries.BIOME.Size}");
        }
        foreach (var (registry, ids) in SynchronizedRegistryData.All)
        {
            connection.Send(new ClientboundRegistryDataPacket(
                Identifier.Parse(registry), BiomeRegistrySynchronization.PackEmptyEntries(ids)));
            Log.Debug($"Sent registry_data id={registry} entries={ids.Length} (empty)");
        }
        //合并动态注册表(7 个)与静态注册表+biome 客户端解析元素 JSON 引用这些 tag 缺则报 Unbound/parse 失败
        var tags = new Dictionary<Identifier, Dictionary<Identifier, int[]>>();
        foreach (var (registry, tag, ids) in SynchronizedTagData.All.Concat(StaticRegistryTagData.All))
        {
            var registryId = Identifier.Parse(registry);
            if (!tags.TryGetValue(registryId, out var perRegistry))
                tags[registryId] = perRegistry = new Dictionary<Identifier, int[]>();
            perRegistry[Identifier.Parse("minecraft:" + tag)] = ids;
        }
        connection.Send(new ClientboundUpdateTagsPacket(tags));
        Log.Debug($"Sent update_tags registries={tags.Count} tags={tags.Values.Sum(p => p.Count)}");
    }

    //PrepareChunkRadius 出生点预载半径 对应原版 PrepareSpawnTask.PREPARE_CHUNK_RADIUS
    public const int PrepareChunkRadius = 3;

    //PrepareSpawnChunks 提交出生点周围区块加载 对应原版 PrepareSpawnTask 的 PLAYER_SPAWN ticket
    //原版是 addTicketAndLoadWithRadius(PLAYER_SPAWN, spawnChunk, 3) 即票等级 33-3=30 铺满 7x7
    //以前是直接给持有器写死 BorderLevel 不放票: 加载跟踪器下一拍就会按"这里没有票"把等级改回不加载档
    //配置阶段玩家还没进世界没有加载票 这批区块于是被回收 进世界看到的是空洞
    public Task[] PrepareSpawnChunks()
    {
        var chunkSource = Overworld.ChunkSource;
        var spawnChunk = new ChunkPos((int)Math.Floor(SpawnPos.X / 16), (int)Math.Floor(SpawnPos.Z / 16));
        chunkSource.TicketStorage?.AddTicketWithRadius(
            NetCraft.Storage.TicketType.PlayerSpawn, spawnChunk, PrepareChunkRadius);
        var tasks = new List<Task>((PrepareChunkRadius * 2 + 1) * (PrepareChunkRadius * 2 + 1));
        for (var dx = -PrepareChunkRadius; dx <= PrepareChunkRadius; dx++)
        {
            for (var dz = -PrepareChunkRadius; dz <= PrepareChunkRadius; dz++)
            {
                tasks.Add(chunkSource.GetChunkFuture(
                    spawnChunk.X + dx, spawnChunk.Z + dz, ChunkStatus.FULL));
            }
        }
        Log.Debug($"Spawn preload submitted center=({spawnChunk.X},{spawnChunk.Z}) radius={PrepareChunkRadius} total {tasks.Count} chunks");
        return tasks.ToArray();
    }

    //TransitionToGame 切换到 Play 阶段挂 ServerGamePacketListenerImpl 并触发 PlayerList.PlaceNewPlayer
    public void TransitionToGame(Connection connection, GameProfile profile)
    {
        Log.Debug($"TransitionToGame profile={profile.Name}");
        connection.RegistryAccess = RegistryAccessForConnection;
        var gameListener = new ServerGamePacketListenerImpl(connection, profile);
        connection.SetListenerForServerboundGame(gameListener);
        //封禁检查在进世界之前 命中就此断开不再建 ServerPlayer 对应原版 canPlayerLogin
        //必须放在 SetListenerForServerboundGame 之后 此刻出站协议才是 Play 断连包才发得出去
        if (!_playerList.CanPlayerLogin(connection, profile)) return;
        var player = _playerList.PlaceNewPlayer(connection, profile);
        //关联玩家与监听器 让心跳等客户端回包能回落到玩家状态
        if (player is not null)
        {
            gameListener.Player = player;
            //反向关联 命令层传送要经监听器走等待客户端确认的流程
            player.Listener = gameListener;
        }
        //注入玩家列表 方块变更需要向在线玩家广播
        gameListener.Players = _playerList;
        gameListener.BlockEntities = _blockEntities;
        //注入命令管理器 上行命令包在此执行
        gameListener.Commands = Commands;
    }

    //Tick 专用服务端帧逻辑对齐原版 MinecraftServer.tickChildren 调用顺序
    //1. tick 所有玩家 Connection 处理入站包队列与断连检测
    //2. 清理已断开连接调 HandleDisconnection 并从 PlayerList 移除
    //3. tick 主世界 ServerLevel 推进 ChunkSource 异步调度与实体调度
    //4. tick 方块实体并推进随机刻
    //5. 周期刷盘
    protected override void Tick()
    {
        //tickChildren 的 commandFunctions 段 tick/load 标签函数 管理器内部按 runsNormally 过滤
        Functions.Tick();
        //计划事件按主世界游戏时间触发 对应原版 scheduled_events 语义
        //到点的回调自己往队列里排函数或新事件
        var scheduledStart = TickStageProfiler.Now();
        if (TickRate.RunsNormally) ScheduledEvents.Tick(this, _overworld.GameTime);
        TickStageProfiler.Record(TickStage.Console, scheduledStart);
        var stageStart = TickStageProfiler.Now();
        List<Connection> snapshot;
        lock (_connections) snapshot = _connections.ToList();
        for (int i = 0; i < snapshot.Count; i++)
            snapshot[i].Tick();
        //清理已断开的连接读循环检测到流结束会调 Disconnect 标记 _disposed 此处统一回收
        lock (_connections)
        {
            for (int i = _connections.Count - 1; i >= 0; i--)
            {
                var conn = _connections[i];
                if (!conn.IsConnected)
                {
                    Log.Debug($"Disconnect cleanup triggered reason={conn.DisconnectionDetails?.Reason ?? "none"} current connections {_connections.Count}");
                    conn.HandleDisconnection();
                    _connections.RemoveAt(i);
                    //从 PlayerList 移除关联的 ServerPlayer 避免泄漏
                    //移除前先落盘玩家数据 否则重进服丢背包与位置
                    foreach (var player in _playerList.Players)
                    {
                        if (ReferenceEquals(player.Connection, conn))
                        {
                            _playerData.Save(player);
                            //玩家离开要撤掉他出的票 否则视距内的区块会一直挂在加载列表
                            if (player.Level is PersistentServerLevel leftLevel)
                                leftLevel.ChunkSource.RemovePlayerTickets(player);
                            _playerList.RemovePlayer(player);
                            break;
                        }
                    }
                    Log.Debug($"Connection removed, current connections {_connections.Count}");
                }
            }
        }
        TickStageProfiler.Record(TickStage.Connections, stageStart);

        //冻结只停世界推进 其余照跑 对应原版 tickChildren 只把时钟与游戏测试包在 runs 里
        //第 3 轮修正: 方块实体也要跟着停
        //原版 Level.tickBlockEntities 开头取 runsNormally 逐个 ticker 过滤 冻结时不推进
        //放在 runs 外会让 /tick step 1 之后每拍白送半格 活塞一步就位
        var runsNormally = TickRate.RunsNormally;
        if (runsNormally)
        {
            //gameTime 在关卡 tick 开始处递增 对应原版 Level.tick
            _overworld.GameTime++;
        }
        //世界时钟推进 rate 累积满一进位 原版 clockManager.tick 在 runs 里
        if (runsNormally) ClockManager.Tick();
        //世界边界插值推进 对应原版 ServerLevel.tick 开头 runs 内的 world border 段
        if (runsNormally) _overworld.WorldBorder.Tick();
        //天气状态机 对应原版 ServerLevel.tick 里 world border 之后的 weather 段
        if (runsNormally) TickWeather();
        //每 20 tick 向在线玩家同步游戏时间与时钟状态 原版 forceGameTimeSynchronization 在 runs 外
        if (TickCount > 0 && TickCount % 20 == 0 && _playerList.Players.Count > 0)
            _playerList.BroadcastAll(ClockManager.CreateFullSyncPacket());
        //每 300 tick 刷新 ping 在线人数 原版状态重建在 runs 外
        if (TickCount > 0 && TickCount % 300 == 0 && _serverStatus.Players is not null)
            _serverStatus.Players.Online = _playerList.Players.Count;
        TickStageProfiler.Record(TickStage.Clock, stageStart);
        //方块调度刻/关卡推进/随机刻/方块事件/实体进入方块 每个维度各跑一遍
        //对应原版 tickChildren 遍历 getAllLevels 逐个 level.tick
        foreach (var level in _levels.Values)
        {
            //handlingTick 覆盖原版从 tickPending 到 runBlockEvents 这一段 活塞收回降级判定要用
            if (runsNormally) level.IsHandlingTick = true;
            //方块调度刻在区块推进之前跑 对应原版 tickPending 阶段
            stageStart = TickStageProfiler.Now();
            if (runsNormally) level.TickBlockTicks();
            TickStageProfiler.Record(TickStage.BlockTicks, stageStart);
            //关卡 tick 每拍都跑 冻结由实体管理器逐实体过滤 区块调度与实体管理不受冻结影响
            stageStart = TickStageProfiler.Now();
            level.Tick(runsNormally);
            TickStageProfiler.Record(TickStage.LevelTick, stageStart);
            //本拍之前积压的方块变化在这里统一下发 对应原版 chunkSource.tick 里的 broadcastChangedChunks
            //它必须排在 runBlockEvents 之前 方块事件引起的方块更新要留到下一拍才发给客户端
            //活塞收回就是靠这个先后: 客户端重放搬运时前方那格还得是原方块
            //原版 ServerLevel.tick 传进去的 tickChunks 恒为 true 这一冲刷不受冻结影响
            //放进 runsNormally 会让 /tick freeze 期间放方块破坏方块都不下发 客户端看着像点了没反应
            stageStart = TickStageProfiler.Now();
            level.FlushBlockUpdates();
            TickStageProfiler.Record(TickStage.FlushBlocks, stageStart);
            //随机刻对应原版 chunkSource.tick 里的方块随机刻部分 只对玩家附近区块抽样
            stageStart = TickStageProfiler.Now();
            if (runsNormally)
                ServerBlockTicks.RandomTick(level, _tickRandom,
                    ServerBlockTicks.DefaultRandomTickSpeed, CollectRandomTickChunks(level));
            TickStageProfiler.Record(TickStage.RandomTick, stageStart);
            //方块事件在区块推进之后跑 对应原版 blockEvents 阶段
            stageStart = TickStageProfiler.Now();
            if (runsNormally) level.RunBlockEvents();
            if (runsNormally) level.IsHandlingTick = false;
            TickStageProfiler.Record(TickStage.BlockEvents, stageStart);
            //本拍所有方块变化都落定后再统一推进一轮光照
            //写方块只标记脏点 活塞搬运这类一拍几十次 setBlock 到这一步才跑一轮传播 与原版一致
            //原版光照传播跑在专属线程上与游戏刻无关 广播则和方块变化同在 broadcastChanges 里
            //这里同样不受冻结影响 否则冻结期间光照脏点只积压不消化 一解冻就要补一大轮
            stageStart = TickStageProfiler.Now();
            level.TickLight();
            TickStageProfiler.Record(TickStage.Light, stageStart);
            //实体进入方块效果 对应原版 Entity.checkInsideBlocks 在实体移动之后
            stageStart = TickStageProfiler.Now();
            if (runsNormally) level.DispatchEntityInside();
            TickStageProfiler.Record(TickStage.EntityInside, stageStart);
        }
        //方块实体随世界一起推进 冻结时停住 对应原版 Level.tickBlockEntities 的 runsNormally 过滤
        stageStart = TickStageProfiler.Now();
        if (runsNormally) _blockEntities.Tick();
        TickStageProfiler.Record(TickStage.BlockEntities, stageStart);
        stageStart = TickStageProfiler.Now();
        foreach (var player in _playerList.Players)
            player.Tick();
        TickStageProfiler.Record(TickStage.Players, stageStart);
        //假玩家的逐步行走先推进 本刻位移才能被紧随其后的实体追踪同步给其他玩家
        stageStart = TickStageProfiler.Now();
        _debugPlayers.Tick();
        TickStageProfiler.Record(TickStage.DebugPlayers, stageStart);
        //挂机踢出按秒检查 配置为 0 表示不启用 对应原版 player-idle-timeout
        stageStart = TickStageProfiler.Now();
        if (_settings.PlayerIdleTimeout > 0 && TickCount > 0 && TickCount % 20 == 0)
            KickIdlePlayers(_settings.PlayerIdleTimeout);
        TickStageProfiler.Record(TickStage.Players, stageStart);
        //实体追踪在实体与世界推进之后计算本帧的实体同步包
        stageStart = TickStageProfiler.Now();
        _entityTracker.Tick(_overworld, _playerList.Players);
        TickStageProfiler.Record(TickStage.EntityTracking, stageStart);
        stageStart = TickStageProfiler.Now();
        if (IsSavingEnabled && TickCount > 0 && TickCount % AutoSaveIntervalTicks == 0)
        {
            //快照在主线程完成 NBT 序列化与 region 写盘移到后台线程 避免阻塞主循环网络卡死
            //上次写盘未完成时跳过本次防止快照堆积
            if (_autoSaveTask is null || _autoSaveTask.IsCompleted)
            {
                try
                {
                    //快照在主线程完成 NBT 序列化 各维度的 region 写盘并行转后台避免阻塞主循环网络卡死
                    var writes = new List<Task>();
                    var total = 0;
                    foreach (var level in _levels.Values)
                    {
                        var snapshots = level.SnapshotAllChunks();
                        total += snapshots.Count;
                        writes.Add(Task.Run(async () =>
                        {
                            try { await level.WriteSnapshotsAsync(snapshots); }
                            catch (Exception e) { Log.Error($"Background chunk write failed {e}"); }
                        }));
                    }
                    _autoSaveTask = Task.WhenAll(writes);
                    Log.Info($"Autosave triggered at tick {TickCount}, {total} chunks handed to background write");
                }
                catch (Exception e)
                {
                    Log.Error($"Autosave snapshot failed at tick {TickCount} {e}");
                }
            }
            else
            {
                Log.Warning($"Autosave skipped at tick {TickCount}, previous write still running");
            }
            //世界元数据与 saveddata 随自动刷盘落盘对应原版 saveEverything 的 level.dat 部分
            SaveLevelData();
            _dataStorage.ScheduleSave();
            //在线玩家数据一并落盘 对应原版 saveEverything 的 saveAllPlayerData
            SaveAllPlayerData();
        }
        TickStageProfiler.Record(TickStage.AutoSave, stageStart);
    }

    //SaveLevelData 把世界游戏时间写回 level.dat 同步写盘失败不中断主循环
    private void SaveLevelData()
    {
        try
        {
            _levelData.GameTime = _overworld.GameTime;
            _levelData.Save(_levelAccess.WorldDir);
        }
        catch (Exception e)
        {
            Log.Error($"level.dat write failed: {e.Message}");
        }
    }

    //SaveAllPlayerData 落盘所有在线玩家数据 对应原版 saveAllPlayerData
    private void SaveAllPlayerData()
    {
        foreach (var player in _playerList.Players)
            _playerData.Save(player);
    }

    //IsSavingEnabled 自动刷盘开关 由 save-off/save-on 控制
    //关掉后周期性自动刷盘停摆 只剩 /save-all 与关服强刷
    private bool _isSavingEnabled = true;
    public override bool IsSavingEnabled => _isSavingEnabled;

    //SetSavingEnabled 开关自动刷盘
    public override void SetSavingEnabled(bool enabled) => _isSavingEnabled = enabled;

    //SaveAllNow 立即全量刷盘 对应原版 saveEverything 供 /save-all 调用
    //同样要走世界门: 命令可能从 GUI 线程下达 与主循环并发取快照一样会存下半成品
    public override void SaveAllNow()
    {
        SaveLevelData();
        _dataStorage.ScheduleSave();
        SaveAllPlayerData();
        lock (WorldGate)
        {
            foreach (var level in _levels.Values)
                level.SaveAllChunksAsync().GetAwaiter().GetResult();
        }
    }

    //KickIdlePlayers 踢出挂机超过指定分钟的玩家 对应原版 MinecraftServer.tickChildren 的 idle timeout 分支
    //文案对齐原版 multiplayer.disconnect.idling 玩家看到的断线原因与官方一致
    private void KickIdlePlayers(int minutes)
    {
        var limit = minutes * 60000L;
        var now = Environment.TickCount64;
        //先取副本 断开连接会改动在线列表
        foreach (var player in _playerList.Players.ToList())
            if (now - player.LastActiveMillis > limit)
                player.Disconnect("You have been idle for too long!");
    }

    //Stop 触发主循环退出并强制刷盘避免数据丢失
    //先停监听断开玩家再刷盘对齐原版 stopServer 顺序防止保存期间新包进入
    public override void Stop()
    {
        _acceptor?.Stop();
        //先停 RCON 与查询线程对应原版 stopServer 开头的两个 stop
        //线程可能阻塞在同步命令等待上 Stop 里的 join+打断负责把它们放出来
        _rconThread?.Stop();
        _queryThreadGs4?.Stop();
        //先落盘玩家数据再踢人 断连后连接清理会把玩家移出列表
        //文案对齐原版 multiplayer.disconnect.server_shutdown 客户端显示 Server closed
        SaveAllPlayerData();
        foreach (var player in _playerList.Players)
            player.Disconnect("Server closed");
        //发包已交后台写线程 刷盘前先等踢人包真正落到流上 否则关服客户端看不到原因
        List<Connection> pending;
        lock (_connections) pending = _connections.ToList();
        foreach (var conn in pending) conn.Flush();
        if (Running)
        {
            //整段刷盘与主循环互斥: 主循环正在搬运活塞时取快照会存下中间态
            //那种半成品是"方块已经换成移动活塞 但方块实体还没登记"
            //读回来那格再也没有东西推它 表现就是卡死的移动活塞或无头活塞
            //原版 stopServer 提交到 server 线程执行天然串行 这里用世界门补上
            //命令触发的关服本来就在主循环线程里 重入同一把门即可
            lock (WorldGate)
            {
                try
                {
                    //等后台写盘任务结束后再兜底全量保存并 fsync
                    _autoSaveTask?.GetAwaiter().GetResult();
                    //关服先停用票再落盘 对应原版 ServerChunkCache.close 里的 deactivateTicketsOnClosing
                    //票仍在内存里会随刷盘写进 chunk_tickets.dat 下次开服再激活
                    foreach (var tickets in _chunkTickets.Values)
                    {
                        tickets.DeactivateTicketsOnClosing();
                        tickets.SetDirty();
                    }
                    foreach (var level in _levels.Values)
                    {
                        level.SaveAllChunksAsync().GetAwaiter().GetResult();
                        level.SynchronizeAsync(flush: true).GetAwaiter().GetResult();
                    }
                    _dataStorage.SaveAndJoin();
                    SaveLevelData();
                    Log.Info("DedicatedServer shutdown flush finished");
                }
                catch (Exception e)
                {
                    Log.Error($"DedicatedServer shutdown flush failed {e.Message}");
                }
            }
        }
        base.Stop();
    }

    //RunStatus 阻塞直到 Shutdown 信号给外部 EXE 调用
    public void RunStatus()
    {
        Log.Info($"DedicatedServer port {_settings.ServerPort} level {_settings.LevelName} waiting for shutdown signal");
        WaitForShutdown();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _statistics?.Dispose();
        _acceptor?.Dispose();
        _dataStorage.Dispose();
        foreach (var storage in _entityStorages.Values) storage.Dispose();
        _levelAccess.Dispose();
        _disposed = true;
    }
}
