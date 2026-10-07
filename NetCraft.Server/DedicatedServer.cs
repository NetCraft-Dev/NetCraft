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
using NetCraft.Server.Diagnostics;
using NetCraft.Storage;
using NetCraft.Storage.Paletted;
using NetCraft.Util.Random;
using NetCraft.Util.Thread;
//The registry and level definitions each have a DimensionType, the former is a marker interface, this fixes the reference to the real Game-layer type
using GameDimensionType = NetCraft.Game.World.Level.LevelGen.Dimension.DimensionType;

namespace NetCraft.Game.Server;

//DedicatedServer, the dedicated server, maps to vanilla net.minecraft.server.dedicated.DedicatedServer
//Extends MinecraftServer, hooks in ServerSettings and LevelStorageAccess and holds the world storage reference
//Stage 11.35 added construction and config fields, stage 11.46 added PersistentServerLevel and tick interval control
//Stage 11.47 added the player Connection list and level.Tick scheduling, aligned with vanilla tickChildren call order
//Stage 11.63 added ConnectionAcceptor port listening and PlayerList player management, implementing the ServerHandshake/Login/Configuration three-phase Context routing
//Autosaves every AutoSaveIntervalTicks and forces a flush on Stop to avoid data loss
public sealed class DedicatedServer : MinecraftServer, ServerHandshakeContext, ServerLoginContext, ServerConfigurationContext, IDisposable
{
    //AutoSaveIntervalTicks autosave interval, maps to vanilla autosave.period, default 6000 ticks about 5 minutes
    public const int AutoSaveIntervalTicks = 6000;

    //RandomTickRadius the chunk radius random ticks act on, maps to the vanilla entity-ticking chunk range
    private const int RandomTickRadius = 8;

    private readonly ServerSettings _settings;
    private readonly LevelStorageAccess _levelAccess;
    private readonly NetCraft.DataFixer.DataFixer _dataFixer;
    private readonly PersistentServerLevel _overworld;
    //_overworldGenerator the overworld generator, the spawn climate search uses it for spawn_target and the climate sampler
    private readonly ChunkGenerator? _overworldGenerator;
    //_levels all dimension worlds, keyed by dimension identifier, the overworld is always among them and is the same instance as _overworld
    private readonly Dictionary<ResourceKey<Level>, PersistentServerLevel> _levels = new();
    private readonly List<Connection> _connections = new();
    private readonly PlayerList _playerList;
    //_opList the operator list, source of permission level checks and /op /deop reads and writes
    private readonly OpList _opList;
    //_banList the player ban list, source of login checks and /ban /pardon /banlist reads and writes
    private readonly BanList _banList;
    //_ipBanList the IP ban list, source of login checks and /ban-ip /pardon-ip reads and writes
    private readonly IpBanList _ipBanList;
    //_dataStorage the SavedData storage for the overworld data directory, holds dimension-level data such as world_clocks.dat
    private readonly SavedDataStorage _dataStorage;
    //_entityStorages separate entity persistence storage, one per dimension, attached under each dimension's entities directory
    private readonly Dictionary<ResourceKey<Level>, EntityStorage> _entityStorages = new();
    //_levelData the world metadata, holds level.dat for reading and writes it on autosave and Stop
    private readonly LevelData _levelData;
    //_playerData the player data storage, writes playerdata/<uuid>.dat on player exit and flush
    private readonly PlayerDataStorage _playerData;
    //_worldGenSettings world generation settings, the seed is fixed into the save at data/minecraft/world_gen_settings.dat
    private readonly WorldGenSettingsData _worldGenSettings;
    //_weatherData weather state, rain and thunder timers and target state, stored at data/minecraft/weather.dat
    private readonly WeatherData _weatherData;
    //_chunkTickets chunk ticket saves, one per dimension, stored at data/minecraft/chunk_tickets*.dat
    //Loads go into the deactivated pool first and are activated after the spawn chunks are prepared, and are deactivated and flushed again before shutdown
    private readonly Dictionary<ResourceKey<Level>, TicketStorage> _chunkTickets = new();
    private readonly BlockEntityManager _blockEntities = new();
    //_entityTracker the entity tracker, sends entity enter/leave and movement sync packets by player view distance
    private readonly EntityTracker _entityTracker = new();
    //_debugPlayers the fake player manager, used by /debug join to create command-driven fake clients
    private readonly DebugPlayerManager _debugPlayers;
    //_tickRandom the random tick source, separated from the chunk generation source to avoid interference
    private readonly RandomSource _tickRandom = RandomSource.Create();
    //_randomTickChunks chunks to random-tick this tick, recomputed per tick from player positions to avoid iterating all loaded chunks
    private readonly HashSet<long> _randomTickChunks = new();
    //_autoSaveTask the background write task from the last autosave, the next autosave is skipped while it is unfinished
    private Task? _autoSaveTask;
    private readonly ServerStatus _serverStatus;
    private readonly ReloadableServerResources? _rsr;
    private ConnectionAcceptor? _acceptor;
    private bool _disposed;
    //_registryAccess the registry set needed to codec world data packets (ItemStack and the like), built lazily on first entry into Configuration
    private RegistryAccess? _registryAccess;
    //_commandStorage the command storage, target of /data ... storage, attached to SavedData storage on first use
    private CommandStorage? _commandStorage;
    //_statistics runtime metrics reporting, non-null only with enable-jmx-monitoring on
    private MinecraftServerStatistics? _statistics;
    //_rconConsoleSource the RCON command executor and output buffer
    private readonly RconConsoleSource _rconConsoleSource;
    //_rconThread the RCON listener thread, non-null only with enable-rcon on and a password configured
    private RconThread? _rconThread;
    //_queryThreadGs4 the GS4 query thread, non-null only with enable-query on and a valid port
    private QueryThreadGs4? _queryThreadGs4;

    //RegistryAccessForConnection builds the registry access set lazily
    //It can only be built after BootstrapClass.BootStrap freezes the registries, already satisfied at server construction
    private RegistryAccess RegistryAccessForConnection
        => _registryAccess ??= BuiltInRegistries.CreateRegistryAccess();

    //Settings the server config, the server.properties load result
    public override ServerSettings Settings => _settings;

    //LevelAccess the world storage access entry point
    public LevelStorageAccess LevelAccess => _levelAccess;

    //Overworld the overworld PersistentServerLevel hooked into RegionFileStorage
    public override PersistentServerLevel Overworld => _overworld;

    //Levels all dimension worlds for tick iteration and diagnostics
    public IEnumerable<PersistentServerLevel> Levels => _levels.Values;

    //GetLevel gets a world by dimension identifier, null when that dimension was not created
    public override PersistentServerLevel? GetLevel(ResourceKey<Level> key)
        => _levels.TryGetValue(key, out var level) ? level : null;

    //CreateLevel creates a persistent world for a dimension
    //Height and generator prefer the level definition, falling back to the passed defaults without one (only the overworld has a generator fallback)
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
        //On a save miss it goes through the ChunkStatusProcessor pipeline, random makes generation reproducible
        Func<ChunkPos, ChunkAccess?>? generator = null;
        NetCraft.Game.World.Level.LevelGen.Structure.StructureFeatureManager? structures = null;
        PersistentServerLevel? created = null;
        if (chunkGenerator is not null)
        {
            var pf = factory ?? PalettedContainerFactory.Default;
            //Neighbor lookups only take loaded chunks, an unloaded neighbor returns null and decoration degrades to the center chunk only
            //Generation must not trigger new loads, otherwise the neighbor chain recurses endlessly
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
        //The structure bridge is attached after the level is built, chunk persistence packing and load restoration both go through it
        if (structures is not null)
        {
            created.StructureDataBridge = new ServerStructureDataBridge(structures,
                NetCraft.Game.World.Level.LevelGen.Structure.StructurePieceSerializationContext.FromManager(
                    Bootstrap.GameBootstrap.StructureTemplates));
        }
        return created;
    }

    //CreateExtraLevels creates dimensions beyond the overworld from the level definitions
    //None are created without a level definition, a missing data pack degrades to a single world rather than using the wrong generator to fabricate fake dimensions
    private void CreateExtraLevels(PalettedContainerFactory? factory, RegistryAccess? registryAccess,
        RandomSource? random, long worldSeed)
    {
        foreach (var key in new[] { LevelKeys.NETHER, LevelKeys.END })
        {
            if (WorldPresets.Get(key.Identifier) is null) continue;
            //The default height without a level definition takes the value shared by the nether and the end, 0 with 8 sections
            _levels[key] = CreateLevel(key, 0, 8, factory, registryAccess, null, random, worldSeed);
        }
    }

    //WireLevel attaches the Game layer side-effect outlets to a dimension
    //Block updates/light/block entities/entity collision/entity death each need one attachment per dimension, missing one breaks that dimension's chain
    private void WireLevel(PersistentServerLevel level)
    {
        //Light changes are synced to clients through the player set, light changes outside chunk packets can only be sent via this chain
        level.LightUpdateSink = BroadcastLightUpdate;
        //Side-effect outlet of the block update chain, block entity removal and block destruction both call back from here
        level.BlockUpdateSink = new ServerBlockUpdateSink(level, _playerList, _blockEntities);
        //Block entities persist with chunks and are cleaned up on unload through this bridge, the Game layer handles coding by id
        level.BlockEntityBridge = new ServerBlockEntityBridge(level, _blockEntities);
        //Players are not in the entity manager, entities like pressure plates need the player bounding boxes added for enter detection
        level.ExtraEntityBoxes = PlayerBoxes;
        //Shape collision for entity movement, the level layer cannot ask block behavior, a collision view fetches block collision shapes per entity
        CollisionGetter collisionView = new LevelCollisionGetter(level, level.MinSectionY, level.SectionsCount);
        level.CollisionShapeProvider = (entity, box) => collisionView.GetBlockCollisions(entity, box).ToList();
        //Entity death callback, attached by the level when an entity is added, broadcasts the death event then removes the entity from the world
        level.EntityDeathCallback = OnEntityDied;
    }

    //CreateEntityStorage builds entity persistence storage per dimension, the overworld lands in the world root and other dimensions in their own directories
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

    //DataFixer the save upgrader, built and passed by GameDataFixers.BuildV1_21Fixer
    public NetCraft.DataFixer.DataFixer DataFixer => _dataFixer;

    //Connections a readonly view of connected player connections for external diagnostics
    public override IReadOnlyList<Connection> Connections => _connections;

    //PlayerList online player set management
    public override PlayerList PlayerList => _playerList;

    //ClockManager the world clock manager, advances and modifies each WorldClock state and persists it with the save
    public override ServerClockManager ClockManager { get; }

    //BlockEntities the block entity collection, the server ticks it every frame and can send ClientboundBlockEntityData on demand
    public override BlockEntityManager BlockEntities => _blockEntities;

    //CommandStorage command storage, maps to vanilla MinecraftServer.getCommandStorage
    public override CommandStorage CommandStorage => _commandStorage ??= new CommandStorage(_dataStorage);

    //Stopwatches the debug stopwatch collection, maps to vanilla MinecraftServer.getStopwatches
    public override Stopwatches Stopwatches { get; } = new();

    //EntityTracker the entity tracker, driven directly by diagnostics and tests
    public override EntityTracker EntityTracker => _entityTracker;

    //DebugPlayers the fake player manager, the operation entry for /debug join and /debug player
    public override DebugPlayerManager DebugPlayers => _debugPlayers;

    //Trace the runtime trace capture control, the operation entry for /debug trace
    //Holds no session until a capture starts, so constructing it costs nothing
    public override ServerTraceControl Trace { get; } = new RuntimeTraceRecorder();

    //ServerStatus the server status, used to respond to StatusRequest
    public ServerStatus ServerStatus => _serverStatus;

    //ServerResources the server reloadable resource set holding the ResourceManager and Tags
    //Paves the way for later /reload commands to reload data-driven content such as Tags/Recipes/Advancements
    public ReloadableServerResources? ServerResources => _rsr;

    //Commands the command manager, holds the command tree, sends it on world entry and executes on incoming command packets
    public override CommandManager Commands { get; }

    //DefaultGameType the server default game mode, level.dat wins when the save exists, otherwise it is parsed from settings.gamemode
    //PlayerList.PlaceNewPlayer applies it on join and the defaultgamemode command can change it
    private GameType _defaultGameType;
    public override GameType DefaultGameType => _defaultGameType;

    //WorldSeed the world seed used for LoginPacket SpawnInfo sync, defaults to 0 when not passed
    public override long WorldSeed { get; }

    //LevelData the world metadata, for external reads of state such as time and spawn besides the seed
    public override LevelData LevelData => _levelData;

    //PlayerData the player data storage, read on join and written on exit/flush
    public override PlayerDataStorage PlayerData => _playerData;

    //OpList the operator list, source of permission levels, writes ops.json on change
    public override OpList OpList => _opList;

    //BanList the player ban list, writes banned-players.json on change
    public override BanList BanList => _banList;

    //IpBanList the IP ban list, writes banned-ips.json on change
    public override IpBanList IpBanList => _ipBanList;

    //WhiteList the whitelist, writes whitelist.json on change
    public override WhiteList WhiteList { get; } = new(AppPaths.WhitelistPath);

    //IsWhiteListEnabled whether the whitelist is enabled, maps to vanilla PlayerList.isUsingWhitelist
    //Taken from white-list in server.properties at construction and changed later by the whitelist on/off command
    public override bool IsWhiteListEnabled { get; set; }

    //GameRules the game rule save, read and written by the /gamerule command
    public override GameRuleMapData GameRules { get; }

    //SpawnPos the world spawn, restored from level.dat and defaulting to (0,64,0) for a new world
    //The configuration-phase spawn preload and SetDefaultSpawnPosition both use it
    private Vec3 _spawnPos = new(0, 64, 0);
    public override Vec3 SpawnPos => _spawnPos;

    //SetDefaultGameType the defaultgamemode command changes the default mode, affecting only players who join afterwards
    public override void SetDefaultGameType(GameType gameType) => _defaultGameType = gameType;

    //SetSpawnPos spawnpoint/setworldspawn rewrite the world spawn and write level.dat back immediately
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
        //_settings must be assigned first, the CommandManager constructor reads nc-debug-commands to decide whether to register /debug
        _settings = settings;
        Commands = new CommandManager(this);
        //The function library and function manager map to vanilla resources.managers.getFunctionLibrary and new ServerFunctionManager
        //Compile permission follows function-permission-level, maps to vanilla getFunctionCompilationPermissions
        var functionLibrary = new ServerFunctionLibrary(
            LevelBasedPermissionSet.ForLevel((NetCraft.Registry.PermissionLevel)Math.Clamp(_settings.FunctionPermissionLevel, 0, 4)),
            Commands.Dispatcher);
        Functions = new ServerFunctionManager(this, functionLibrary);
        _levelAccess = levelAccess;
        _dataFixer = dataFixer ?? GameDataFixers.BuildV1_21Fixer();
        _rsr = rsr;
        //Restore save metadata when level.dat exists, otherwise initialize a new world from server.properties
        _levelData = levelData ?? new LevelData
        {
            LevelName = settings.LevelName,
            GameTypeId = (GameType.ByName(settings.Gamemode) ?? GameType.Survival).Id,
            DifficultyName = settings.Difficulty,
            //A new world's spawn is not yet decided, mark it uninitialized, startup searches following vanilla setInitialSpawn
            Initialized = false,
        };
        //Build the overworld from the level definition, height and generator derive from the dimension config and fall back to the constructor args and passed generator without one
        //The spawn climate search needs the overworld generator and uses the same source as CreateLevel
        _overworldGenerator = WorldPresets.Get(LevelKeys.OVERWORLD.Identifier)?.Generator ?? chunkGenerator;
        _overworld = CreateLevel(LevelKeys.OVERWORLD, minSectionY, sectionsCount, factory, registryAccess,
            chunkGenerator, random, worldSeed ?? 0);
        _levels[LevelKeys.OVERWORLD] = _overworld;
        _playerList = new PlayerList(this, settings.MaxPlayers);
        //Tick rate state changes are synced to clients through the player set, maps to vanilla tickRateManager using server.getPlayerList() to broadcast
        TickRate.Players = _playerList;
        //Dimensions beyond the overworld are built from the level definitions, none without a data pack definition to avoid fabricating fake dimensions with the wrong generator
        CreateExtraLevels(factory, registryAccess, random, worldSeed ?? 0);
        //The Game layer side-effect outlet of the block update chain is attached per dimension, missing one breaks that dimension's chain
        foreach (var level in _levels.Values) WireLevel(level);
        //The operator list is attached to ops.json in the program root, matching the vanilla server root location
        _opList = opList ?? new OpList(AppPaths.OpsPath);
        //The ban lists are likewise attached to banned-players.json and banned-ips.json in the program root
        _banList = banList ?? new BanList(AppPaths.BannedPlayersPath);
        _ipBanList = ipBanList ?? new IpBanList(AppPaths.BannedIpsPath);
        IsWhiteListEnabled = settings.WhiteList;
        //The fake player manager only holds a server reference and performs no actions during construction
        _debugPlayers = new DebugPlayerManager(this);
        //An old save restores world game time and spawn, a new world keeps the defaults 0 and (0,64,0)
        _overworld.GameTime = _levelData.GameTime;
        _spawnPos = new Vec3(_levelData.SpawnX, _levelData.SpawnY, _levelData.SpawnZ);
        //SavedDataStorage is attached to the overworld data directory, maps to vanilla overworld.getDataStorage
        _dataStorage = new SavedDataStorage(
            Path.Combine(levelAccess.GetDimensionPath(LevelKeys.OVERWORLD), "data"), _dataFixer);
        _dataStorage.SetRegistryAccess(RegistryAccessForConnection);
        //Player data storage is attached to playerdata under the world root, maps to vanilla PlayerDataStorage
        _playerData = new PlayerDataStorage(levelAccess.WorldDir);
        //Separate entity persistence storage is created per dimension, maps to each vanilla ServerLevel's own entities directory
        //The loader/saver come from the Game layer EntityPersister, avoiding a Storage layer dependency on concrete entity types
        foreach (var (key, level) in _levels)
        {
            var storage = CreateEntityStorage(key);
            _entityStorages[key] = storage;
            level.AttachEntityStorage(storage);
        }
        ClockManager = _dataStorage.ComputeIfAbsent(ServerClockManager.Type);
        ClockManager.Init(this);
        //World generation settings and game rules are restored from the save, the seed is fixed for a new world
        _worldGenSettings = _dataStorage.ComputeIfAbsent(WorldGenSettingsData.Type);
        GameRules = _dataStorage.ComputeIfAbsent(GameRuleMapData.Type);
        //The scheduled event queue save maps to computeIfAbsent(TimerQueue.TYPE) in the vanilla MinecraftServer constructor
        ScheduledEvents = _dataStorage.ComputeIfAbsent(TimerQueueTypes.Instance);
        //The world border save is attached to the overworld data directory, maps to computeIfAbsent in vanilla ServerLevel.getWorldBorder
        //After loading, the save parameters are poured into runtime fields and the runtime state takes over
        _overworld.WorldBorder = _dataStorage.ComputeIfAbsent(WorldBorder.Type);
        _overworld.WorldBorder.ApplyInitialSettings(_overworld.GameTime);
        //Border changes are turned into broadcast packets by a listener, maps to vanilla PlayerList.addWorldborderListener
        _overworld.WorldBorder.AddListener(new ServerWorldBorderListener(_playerList));
        //The weather state save is server-wide, maps to computeIfAbsent(WeatherData.TYPE) in vanilla MinecraftServer
        _weatherData = _dataStorage.ComputeIfAbsent(WeatherData.Type);
        //Chunk ticket saves, one per dimension, maps to vanilla level.getDataStorage().computeIfAbsent(TicketStorage.TYPE)
        //Sharing one would overwrite the level callback on every new dimension, only the last dimension would receive ticket changes and other dimensions' ticket levels would stop converging
        //Only the deactivated pool is filled on load and activation waits until the spawn chunks are prepared, so old tickets do not pull chunks up before the world is ready
        foreach (var (key, level) in _levels)
        {
            var tickets = _dataStorage.ComputeIfAbsent(TicketStorage.TypeFor(key.Identifier));
            _chunkTickets[key] = tickets;
            level.ChunkSource.AttachTicketStorage(tickets);
        }
        //If it is raining on load the rain level is set full directly, maps to prepareWeather in the vanilla ServerLevel constructor
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
        //The overworld default clock, maps to default_clock of the vanilla overworld dimension type
        _overworld.DefaultClock = WorldClocks.OverworldHolder;
        _serverStatus = BuildServerStatus();
        //The RCON command source and output buffer, maps to rconConsoleSource in the vanilla DedicatedServer constructor
        _rconConsoleSource = new RconConsoleSource(this);
        //gamemode is overridden from server.properties on every startup and written back to the save, aligned with vanilla setGameType semantics
        //difficulty is the opposite and follows the save, maps to vanilla forceDifficulty being a no-op
        _defaultGameType = GameType.ByName(settings.Gamemode) ?? GameType.Survival;
        _levelData.GameTypeId = DefaultGameType.Id;
        WorldSeed = worldSeed ?? 0;
    }

    //BuildServerStatus builds the StatusRequest response data
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

    //StartNetwork starts the TCP listener thread to accept new connections
    public void StartNetwork()
    {
        _acceptor = new ConnectionAcceptor(IPAddress.Any, _settings.ServerPort, OnNewConnection);
        _acceptor.Start();
        Log.Info($"Network listening on port {_settings.ServerPort}");
    }

    //RunRconCommand executes one RCON command and returns the output, maps to vanilla DedicatedServer.runCommand
    //Clears the buffer, dispatches to the main loop for synchronous execution, then takes the buffer content back to the client
    public string RunRconCommand(string command)
    {
        _rconConsoleSource.PrepareForCommand();
        ExecuteBlocking(_rconConsoleSource.CreateCommandSourceStack(), command);
        return _rconConsoleSource.GetCommandResponse();
    }

    //ShouldRconBroadcast whether RCON results are broadcast to ops, maps to vanilla shouldRconBroadcast
    //The NC command layer has no informAdmins channel yet, the config entry is exposed for the command layer to hook in later
    public bool ShouldRconBroadcast => _settings.BroadcastRconToOps;

    //InitServer startup initialization, maps to the tail of vanilla DedicatedServer.initServer
    //A new world first searches the spawn following vanilla setInitialSpawn, then loads the chunks around the spawn before declaring ready
    //Maps to the LOAD_INITIAL_CHUNKS stage of vanilla prepareLevels: load first, then Done
    public void InitServer()
    {
        if (!_levelData.Initialized) SearchInitialSpawn();
        StartNetwork();
        //The function library is loaded by data pack, maps to the function library reload section of vanilla reloadableServerResources.loadResources
        //Once attached to the resource listener list, /reload carries the function library along
        _rsr?.AttachFunctionLibrary(Functions.Library);
        //Metrics reporting is off by default, same as vanilla enable-jmx-monitoring
        if (_settings.EnableJmxMonitoring)
        {
            _statistics = MinecraftServerStatistics.Register(this);
            Log.Info("JMX monitoring enabled");
        }
        //The GS4 query listener order matches vanilla initServer, query before rcon
        //A creation failure (port unconfigured or in use) only warns and does not block startup, maps to the vanilla null-return branch
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
        //Startup immediately fixes level.dat and saveddata, maps to saveEverything at the end of vanilla initServer
        //A new world seed is written to disk immediately to prevent a crash-restart within the window from changing it
        SaveLevelData();
        _dataStorage.ScheduleSave();
        //Old save tickets may drive loading only after the world is ready, maps to activateAllDeactivatedTickets in vanilla prepareLevels
        //Each dimension activates its own copy, with ticket tables isolated per dimension only one cannot be activated
        foreach (var tickets in _chunkTickets.Values) tickets.ActivateAllDeactivatedTickets();
        //Vanilla 26.2 prepareLevels no longer preloads the spawn, it declares ready after activating the save tickets
        //Measured: a fresh world's prepare stage loads only the single spawn chunk and unloads it after 2 ticks, an old save loads 0
        //The 3x3 strong load around the spawn only exists because PrepareSpawnTask adds a PLAYER_SPAWN ticket on player login, see PrepareSpawnChunks
        Log.Info($"Done ({StartWatch.Elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture)}s)! NetCraft server ready");
    }

    //SearchInitialSpawn new world spawn search, maps to vanilla MinecraftServer.setInitialSpawn
    //First finds a candidate from the climate target, then searches chunk by chunk from the candidate for standable ground, a hit replaces the spawn and an all-miss keeps the default
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
        //Mark initialized so the next startup reuses the spawn in level.dat
        _levelData.Initialized = true;
    }

    //ResolveSpawnSuggestion the candidate spawn position, maps to findSpawnPosition in vanilla setInitialSpawn
    //Does a radial climate search from the noise settings spawn_target, falling back to the current spawn when the data pack gives no target
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

    //KeepAliveSpawnTickets renews the spawn preload tickets, maps to vanilla PrepareSpawnTask.Ready.keepAlive
    //PLAYER_SPAWN times out after only 20 ticks and a new world's 7x7 generation often takes longer, without renewal the tickets expire and chunks are reclaimed
    //It is the same ticket and radius as the one from PrepareSpawnChunks, the same type and level is treated by AddTicket as the same ticket and only renewed
    public void KeepAliveSpawnTickets()
    {
        var spawnChunk = new ChunkPos((int)Math.Floor(SpawnPos.X / 16), (int)Math.Floor(SpawnPos.Z / 16));
        Overworld.ChunkSource.TicketStorage?.AddTicketWithRadius(
            NetCraft.Storage.TicketType.PlayerSpawn, spawnChunk, PrepareChunkRadius);
    }

    //OnEntityDied cleanup after an entity's health reaches zero, maps to vanilla LivingEntity.die and remove(KILLED)
    //Broadcasts death event 3 so the client plays the death animation, then removes the entity from the level and the tracker sends the removal packet
    private void OnEntityDied(NetCraft.Registry.Entity entity)
    {
        Log.Info($"Entity died {entity.Id} entityId={entity.EntityId}");
        _playerList.BroadcastAll(
            new NetCraft.Game.Network.Protocol.Game.ClientboundEntityEventPacket(entity.EntityId, 3));
        _overworld.RemoveEntity(entity);
    }

    //BroadcastLightUpdate packs light changes per chunk into incremental light packets and broadcasts them, maps to vanilla ChunkMap.onLightUpdate
    //Block packets carry state without light, missing this step makes the client keep the old brightness and blocks look washed out or dark like ghost blocks
    private void BroadcastLightUpdate(ChunkPos pos, IReadOnlyList<int> skySections, IReadOnlyList<int> blockSections)
    {
        var engine = _overworld.ChunkSource.LightEngine;
        var count = engine.GetLightSectionCount();
        _playerList.BroadcastAll(new NetCraft.Game.Network.Protocol.Game.ClientboundLightUpdatePacket(
            pos, engine,
            NetCraft.Game.Network.Protocol.Game.ClientboundLightUpdatePacketData.CreateFilter(count, skySections),
            NetCraft.Game.Network.Protocol.Game.ClientboundLightUpdatePacketData.CreateFilter(count, blockSections)));
    }

    //PlayerBoxes bounding boxes of online players, player size follows vanilla 0.6x1.8 with the feet at the origin
    //Entities like pressure plates need players for enter detection and players are not in the entity manager so they are added this way
    private IEnumerable<AABB> PlayerBoxes()
    {
        foreach (var player in _playerList.Players)
        {
            var pos = player.Position;
            yield return new AABB(pos.X - 0.3, pos.Y, pos.Z - 0.3, pos.X + 0.3, pos.Y + 1.8, pos.Z + 0.3);
        }
    }

    //CollectRandomTickChunks collects the chunks to random-tick in this dimension
    //Vanilla iterates chunks with simulation level <= 31, that is the entity-ticking range, not a box drawn from player positions
    //Here each loaded chunk asks the chunk source: beyond the simulation distance chunks load without ticking, matching the weak-load band semantics
    //With no players in this dimension the simulation table is empty and everything misses, meaning no random ticks
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

    //TickWeather advances the weather state machine and broadcasts changes, maps to vanilla ServerLevel.advanceWeatherCycle
    //Broadcasts go through the overworld player set, maps to vanilla broadcastAll(packet, dimension)
    private void TickWeather()
    {
        var level = _overworld;
        if (!level.CanHaveWeather()) return;
        var wasRaining = level.IsRaining;
        //The advance_weather rule only stops timer advancement, the rain level still fades toward the current target, same as vanilla
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
        //On a rain state flip it sends the start/stop event first then the rain and thunder levels again, maps to the vanilla three-part broadcast
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

    //SetWeatherParameters sets the weather parameters directly, maps to vanilla MinecraftServer.setWeatherParameters
    //Vanilla also writes rainTime for the thunder timer, copied here to stay consistent
    public override void SetWeatherParameters(int clearTime, int rainTime, bool raining, bool thundering)
    {
        _weatherData.SetClearWeatherTime(clearTime);
        _weatherData.SetRainTime(rainTime);
        _weatherData.SetThunderTime(rainTime);
        _weatherData.SetRaining(raining);
        _weatherData.SetThundering(thundering);
    }

    //OnNewConnection new connection callback, attaches the initial handshake listener
    private void OnNewConnection(Connection conn)
    {
        var handshake = new ServerHandshakePacketListenerImpl(conn, this);
        conn.SetListenerForServerboundHandshake(handshake);
        lock (_connections) _connections.Add(conn);
        Log.Debug($"New connection added, current connections {_connections.Count}");
    }

    //AddPlayer adds a Connection to the scheduling list, maps to vanilla PlayerList.addPlayer
    //For low-level API tests it adds the Connection directly without creating a ServerPlayer, unlike PlayerList.PlaceNewPlayer
    public void AddPlayer(Connection connection)
    {
        lock (_connections) _connections.Add(connection);
    }

    //RemovePlayer removes a Connection and returns whether it succeeded
    public bool RemovePlayer(Connection connection)
    {
        lock (_connections) return _connections.Remove(connection);
    }

    //--- ServerHandshakeContext implementation ---

    //TransitionToStatus switches the connection to the Status phase and attaches ServerStatusPacketListenerImpl
    public void TransitionToStatus(Connection connection)
    {
        Log.Debug("TransitionToStatus");
        connection.SetListenerForServerboundStatus(new ServerStatusPacketListenerImpl(connection, _serverStatus));
    }

    //TransitionToLogin switches the connection to the Login phase and attaches ServerLoginPacketListenerImpl
    public void TransitionToLogin(Connection connection)
    {
        Log.Debug("TransitionToLogin");
        connection.SetListenerForServerboundLogin(new ServerLoginPacketListenerImpl(connection, this));
    }

    //--- ServerLoginContext implementation ---

    //TransitionToConfiguration switches the connection to the Configuration phase and attaches ServerConfigurationPacketListenerImpl
    //The vanilla flow is select_known_packs → client reply → registry_data (29 of them) → finish_configuration
    //registry_data is moved to after HandleSelectKnownPacks because empty contents relies on the client having confirmed local vanilla resources
    public void TransitionToConfiguration(Connection connection, GameProfile profile)
    {
        Log.Debug($"TransitionToConfiguration profile={profile.Name}");
        //World data packet coding resolves against registries (ItemStack item ids and such), set up before entering this phase
        connection.RegistryAccess = RegistryAccessForConnection;
        connection.SetListenerForServerboundConfiguration(
            new ServerConfigurationPacketListenerImpl(connection, profile, this));
        try
        {
            //version must exactly match the client's SharedConstants.getCurrentVersion().id(), otherwise the client does not select this pack
            //KnownPack.Vanilla uses NetCraft's own version (26.2-netcraft) and cannot be used for negotiation
            var core = new NetCraft.Network.Protocol.Configuration.KnownPack("minecraft", "core", "26.2");
            connection.Send(new ClientboundSelectKnownPacks(new List<NetCraft.Network.Protocol.Configuration.KnownPack> { core }));
            Log.Debug($"TransitionToConfiguration sent SelectKnownPacks(minecraft:core) profile={profile.Name}");
        }
        catch (Exception e)
        {
            Log.Warning($"Configuration phase send failed {profile.Name} {e.Message}");
        }
    }

    //--- ServerConfigurationContext implementation ---

    //SendSynchronizedRegistries sends all SYNCHRONIZED_REGISTRIES, aligned with vanilla packRegistries
    //Registries the client did not receive stay empty and the nonEmpty check disconnects (such as cat_variant)
    //biome carries server contents (controlling id order), the other 28 send only ids and omit contents, the client loads them from local vanilla resources
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
        //Merges the dynamic registries (7) with the static registries+biome, the client resolves element JSON references to these tags and missing ones report Unbound/parse failures
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

    //PrepareChunkRadius the spawn preload radius, maps to vanilla PrepareSpawnTask.PREPARE_CHUNK_RADIUS
    public const int PrepareChunkRadius = 3;

    //PrepareSpawnChunks submits chunk loading around the spawn, maps to the PLAYER_SPAWN ticket of vanilla PrepareSpawnTask
    //Vanilla does addTicketAndLoadWithRadius(PLAYER_SPAWN, spawnChunk, 3), ticket level 33-3=30 filling a 7x7
    //Previously the holder's BorderLevel was hardcoded without a ticket: the next tick of the load tracker would see "no ticket here" and reset the level to unloaded
    //During configuration the player has not entered the world and there is no load ticket, so these chunks were reclaimed and the player saw holes on entry
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

    //TransitionToGame switches to the Play phase, attaches ServerGamePacketListenerImpl and triggers PlayerList.PlaceNewPlayer
    public void TransitionToGame(Connection connection, GameProfile profile)
    {
        Log.Debug($"TransitionToGame profile={profile.Name}");
        connection.RegistryAccess = RegistryAccessForConnection;
        var gameListener = new ServerGamePacketListenerImpl(connection, profile);
        connection.SetListenerForServerboundGame(gameListener);
        //The ban check happens before entering the world, a hit disconnects here without creating a ServerPlayer, maps to vanilla canPlayerLogin
        //It must come after SetListenerForServerboundGame, only then is the outbound protocol Play and the disconnect packet can be sent
        if (!_playerList.CanPlayerLogin(connection, profile)) return;
        var player = _playerList.PlaceNewPlayer(connection, profile);
        //Associates the player with the listener so client replies such as heartbeats fall back to player state
        if (player is not null)
        {
            gameListener.Player = player;
            //Reverse association, command layer teleports go through the listener into the client-confirmation flow
            player.Listener = gameListener;
        }
        //Inject the player list, block changes must broadcast to online players
        gameListener.Players = _playerList;
        gameListener.BlockEntities = _blockEntities;
        //Inject the command manager, incoming command packets execute here
        gameListener.Commands = Commands;
    }

    //Tick dedicated server frame logic, aligned with vanilla MinecraftServer.tickChildren call order
    //1. tick all player Connections, processing the inbound packet queue and disconnection detection
    //2. clean up disconnected connections, calling HandleDisconnection and removing from PlayerList
    //3. tick the overworld ServerLevel, advancing ChunkSource async scheduling and entity scheduling
    //4. tick block entities and advance random ticks
    //5. periodic autosave
    protected override void Tick()
    {
        //The commandFunctions section of tickChildren, ticks/loads tag functions, the manager filters by runsNormally internally
        Functions.Tick();
        //Scheduled events fire by overworld game time, maps to vanilla scheduled_events semantics
        //A due callback enqueues functions or new events itself
        var scheduledStart = TickStageProfiler.Now();
        if (TickRate.RunsNormally) ScheduledEvents.Tick(this, _overworld.GameTime);
        TickStageProfiler.Record(TickStage.Console, scheduledStart);
        var stageStart = TickStageProfiler.Now();
        List<Connection> snapshot;
        lock (_connections) snapshot = _connections.ToList();
        for (int i = 0; i < snapshot.Count; i++)
            snapshot[i].Tick();
        //Clean up disconnected connections, the read loop calls Disconnect when it sees end of stream and marks _disposed, collected here
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
                    //Remove the associated ServerPlayer from the PlayerList to avoid a leak
                    //Write player data to disk before removal, otherwise rejoin loses the inventory and position
                    foreach (var player in _playerList.Players)
                    {
                        if (ReferenceEquals(player.Connection, conn))
                        {
                            _playerData.Save(player);
                            //A leaving player's tickets must be withdrawn, otherwise chunks within view distance stay on the load list forever
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

        //Freezing only stops world advancement, everything else keeps running, maps to vanilla tickChildren wrapping only the clock and game tests in runs
        //Round 3 fix: block entities must stop too
        //Vanilla Level.tickBlockEntities reads runsNormally at the start and filters per ticker, so it does not advance while frozen
        //Outside runs it would give away half a step per tick after /tick step 1 and the piston snaps into place
        var runsNormally = TickRate.RunsNormally;
        if (runsNormally)
        {
            //gameTime increments at the start of the level tick, maps to vanilla Level.tick
            _overworld.GameTime++;
        }
        //World clock advancement, rate accumulates to a carry, vanilla clockManager.tick is inside runs
        if (runsNormally) ClockManager.Tick();
        //World border interpolation advance, maps to the world border section inside runs at the start of vanilla ServerLevel.tick
        if (runsNormally) _overworld.WorldBorder.Tick();
        //Weather state machine, maps to the weather section after the world border in vanilla ServerLevel.tick
        if (runsNormally) TickWeather();
        //Sync game time and clock state to online players every 20 ticks, vanilla forceGameTimeSynchronization is outside runs
        if (TickCount > 0 && TickCount % 20 == 0 && _playerList.Players.Count > 0)
            _playerList.BroadcastAll(ClockManager.CreateFullSyncPacket());
        //Refresh the ping online count every 300 ticks, the vanilla status rebuild is outside runs
        if (TickCount > 0 && TickCount % 300 == 0 && _serverStatus.Players is not null)
            _serverStatus.Players.Online = _playerList.Players.Count;
        TickStageProfiler.Record(TickStage.Clock, stageStart);
        //Block scheduled ticks/level advance/random ticks/block events/entity enter block, each runs once per dimension
        //Maps to vanilla tickChildren iterating getAllLevels and calling level.tick for each
        foreach (var level in _levels.Values)
        {
            //handlingTick covers the span from tickPending to runBlockEvents in vanilla, used by the piston retraction downgrade check
            if (runsNormally) level.IsHandlingTick = true;
            //Block scheduled ticks run before chunk advancement, maps to the vanilla tickPending phase
            stageStart = TickStageProfiler.Now();
            if (runsNormally) level.TickBlockTicks();
            TickStageProfiler.Record(TickStage.BlockTicks, stageStart);
            //Fluid scheduled ticks follow right after block ticks, maps to the paired BlockTicks and FluidTicks calls in vanilla tickPending
            //Water and lava flow entirely on this tick, missing it means they only move at the instant of placement
            stageStart = TickStageProfiler.Now();
            if (runsNormally) level.TickFluidTicks();
            TickStageProfiler.Record(TickStage.FluidTicks, stageStart);
            //The level tick runs every frame, freezing is filtered per entity by the entity manager, chunk scheduling and entity management are unaffected by freezing
            stageStart = TickStageProfiler.Now();
            level.Tick(runsNormally);
            TickStageProfiler.Record(TickStage.LevelTick, stageStart);
            //Block changes backlogged before this frame are dispatched here, maps to broadcastChangedChunks in vanilla chunkSource.tick
            //It must come before runBlockEvents, block updates caused by block events wait for the next frame to be sent to clients
            //Piston retraction relies on this ordering: when the client replays the move the cell ahead must still be the original block
            //The tickChunks passed by vanilla ServerLevel.tick is always true, this flush is unaffected by freezing
            //Putting it inside runsNormally would prevent placing and breaking blocks from dispatching during /tick freeze, making the client look unresponsive
            stageStart = TickStageProfiler.Now();
            level.FlushBlockUpdates();
            TickStageProfiler.Record(TickStage.FlushBlocks, stageStart);
            //Random ticks correspond to the block random tick part of vanilla chunkSource.tick, sampling only chunks near players
            stageStart = TickStageProfiler.Now();
            if (runsNormally)
                ServerBlockTicks.RandomTick(level, _tickRandom,
                    ServerBlockTicks.DefaultRandomTickSpeed, CollectRandomTickChunks(level));
            TickStageProfiler.Record(TickStage.RandomTick, stageStart);
            //Block events run after chunk advancement, maps to the vanilla blockEvents phase
            stageStart = TickStageProfiler.Now();
            if (runsNormally) level.RunBlockEvents();
            if (runsNormally) level.IsHandlingTick = false;
            TickStageProfiler.Record(TickStage.BlockEvents, stageStart);
            //After all block changes this frame settle, one round of light advancement is run
            //Writing a block only marks dirty points, dozens of setBlock calls per frame from piston movement run one propagation round here, same as vanilla
            //Vanilla light propagation runs on a dedicated thread independent of game ticks while broadcasting stays with block changes in broadcastChanges
            //This is likewise unaffected by freezing, otherwise light dirty points only pile up while frozen and one large catch-up round is needed after unfreezing
            stageStart = TickStageProfiler.Now();
            level.TickLight();
            TickStageProfiler.Record(TickStage.Light, stageStart);
            //Entity enter-block effects, maps to vanilla Entity.checkInsideBlocks after entity movement
            stageStart = TickStageProfiler.Now();
            if (runsNormally) level.DispatchEntityInside();
            TickStageProfiler.Record(TickStage.EntityInside, stageStart);
        }
        //Block entities advance with the world and stop while frozen, maps to the runsNormally filter of vanilla Level.tickBlockEntities
        stageStart = TickStageProfiler.Now();
        if (runsNormally) _blockEntities.Tick();
        TickStageProfiler.Record(TickStage.BlockEntities, stageStart);
        stageStart = TickStageProfiler.Now();
        foreach (var player in _playerList.Players)
            player.Tick();
        TickStageProfiler.Record(TickStage.Players, stageStart);
        //Fake players' step-by-step walking advances first so this frame's movement can be synced to other players by the entity tracker right after
        stageStart = TickStageProfiler.Now();
        _debugPlayers.Tick();
        TickStageProfiler.Record(TickStage.DebugPlayers, stageStart);
        //Idle kicks are checked per second, 0 means disabled, maps to vanilla player-idle-timeout
        stageStart = TickStageProfiler.Now();
        if (_settings.PlayerIdleTimeout > 0 && TickCount > 0 && TickCount % 20 == 0)
            KickIdlePlayers(_settings.PlayerIdleTimeout);
        TickStageProfiler.Record(TickStage.Players, stageStart);
        //Entity tracking computes this frame's entity sync packets after entity and world advancement
        stageStart = TickStageProfiler.Now();
        _entityTracker.Tick(_overworld, _playerList.Players);
        TickStageProfiler.Record(TickStage.EntityTracking, stageStart);
        stageStart = TickStageProfiler.Now();
        if (IsSavingEnabled && TickCount > 0 && TickCount % AutoSaveIntervalTicks == 0)
        {
            //The snapshot completes NBT serialization on the main thread and moves region writes to a background thread, avoiding main loop blockage and network freezes
            //This round is skipped when the last write is unfinished to prevent snapshot pileup
            if (_autoSaveTask is null || _autoSaveTask.IsCompleted)
            {
                try
                {
                    //The snapshot completes NBT serialization on the main thread and each dimension's region writes go to the background in parallel to avoid main loop blockage and network freezes
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
            //World metadata and saveddata are written with the autosave, maps to the level.dat part of vanilla saveEverything
            SaveLevelData();
            _dataStorage.ScheduleSave();
            //Online player data is written too, maps to saveAllPlayerData in vanilla saveEverything
            SaveAllPlayerData();
        }
        TickStageProfiler.Record(TickStage.AutoSave, stageStart);
    }

    //SaveLevelData writes the world game time back to level.dat, a synchronous write failure does not interrupt the main loop
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

    //SaveAllPlayerData writes all online player data to disk, maps to vanilla saveAllPlayerData
    private void SaveAllPlayerData()
    {
        foreach (var player in _playerList.Players)
            _playerData.Save(player);
    }

    //IsSavingEnabled the autosave toggle, controlled by save-off/save-on
    //Turning it off stops the periodic autosave, leaving only /save-all and the shutdown force flush
    private bool _isSavingEnabled = true;
    public override bool IsSavingEnabled => _isSavingEnabled;

    //SetSavingEnabled toggles autosave
    public override void SetSavingEnabled(bool enabled) => _isSavingEnabled = enabled;

    //SaveAllNow flushes everything immediately, maps to vanilla saveEverything, called by /save-all
    //It also goes through the world gate: the command may come from the GUI thread and taking a snapshot concurrently with the main loop stores half-finished state
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

    //KickIdlePlayers kicks players idle for more than the given minutes, maps to the idle timeout branch of vanilla MinecraftServer.tickChildren
    //The text matches vanilla multiplayer.disconnect.idling so the disconnect reason players see is the same as official
    private void KickIdlePlayers(int minutes)
    {
        var limit = minutes * 60000L;
        var now = Environment.TickCount64;
        //Take a copy first, disconnecting mutates the online list
        foreach (var player in _playerList.Players.ToList())
            if (now - player.LastActiveMillis > limit)
                player.Disconnect("You have been idle for too long!");
    }

    //Stop triggers main loop exit and forces a flush to avoid data loss
    //Stops listeners and disconnects players before flushing, aligned with vanilla stopServer order to prevent new packets during saving
    public override void Stop()
    {
        _acceptor?.Stop();
        //First stop the RCON and query threads, maps to the two stops at the start of vanilla stopServer
        //The threads may be blocked on a synchronous command wait, the join+interrupt in Stop releases them
        _rconThread?.Stop();
        _queryThreadGs4?.Stop();
        //Write player data before kicking, connection cleanup after disconnect removes players from the list
        //The text matches vanilla multiplayer.disconnect.server_shutdown, the client shows Server closed
        SaveAllPlayerData();
        foreach (var player in _playerList.Players)
            player.Disconnect("Server closed");
        //The packets are handed to the background write thread, wait for the kick packet to actually reach the stream before flushing, otherwise the client sees no reason on shutdown
        List<Connection> pending;
        lock (_connections) pending = _connections.ToList();
        foreach (var conn in pending) conn.Flush();
        if (Running)
        {
            //The whole flush is mutually exclusive with the main loop: taking a snapshot while the main loop moves a piston stores an intermediate state
            //That half-finished state is "the block is already a moving piston but the block entity is not registered yet"
            //Read back, nothing ever pushes that cell again, showing up as a stuck moving piston or a headless piston
            //Vanilla stopServer submits to the server thread and is naturally serial, the world gate covers that here
            //A command-triggered shutdown is already on the main loop thread and simply re-enters the same gate
            lock (WorldGate)
            {
                try
                {
                    //Wait for the background write task to finish, then do a full fallback save and fsync
                    _autoSaveTask?.GetAwaiter().GetResult();
                    //Shutdown deactivates tickets before flushing, maps to deactivateTicketsOnClosing in vanilla ServerChunkCache.close
                    //Tickets still in memory would be written into chunk_tickets.dat on flush and activated on the next startup
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

    //RunStatus blocks until the shutdown signal, for the external EXE to call
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
