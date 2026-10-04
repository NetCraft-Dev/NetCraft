using NetCraft.Registry;

namespace NetCraft.Storage;

//TicketType 区块票类型对应原版 net.minecraft.server.level.TicketType
//timeout 决定票多久过期 flags 决定这张票参不参与加载/模拟/落盘/保持维度活跃
public sealed class TicketType : NetCraft.Registry.TicketType
{
    //NoTimeout 无超时哨兵对应原版 NO_TIMEOUT
    public const long NoTimeout = 0;

    //FlagPersist 落盘保留对应原版 FLAG_PERSIST
    public const int FlagPersist = 1;
    //FlagLoading 参与加载追踪对应原版 FLAG_LOADING
    public const int FlagLoading = 2;
    //FlagSimulation 参与模拟追踪对应原版 FLAG_SIMULATION
    public const int FlagSimulation = 4;
    //FlagKeepDimensionActive 让维度保持活跃对应原版 FLAG_KEEP_DIMENSION_ACTIVE
    public const int FlagKeepDimensionActive = 8;
    //FlagCanExpireIfUnloaded 区块未就绪也能被超时清理对应原版 FLAG_CAN_EXPIRE_IF_UNLOADED
    public const int FlagCanExpireIfUnloaded = 16;

    //_byName 内置类型按注册名索引 读盘时按名还原
    private static readonly Dictionary<Identifier, TicketType> _byName = new();
    private static readonly List<TicketType> _all = new();
    private static bool _registered;

    //PlayerSpawn 玩家出生点预载票 20 tick 超时对应原版 PLAYER_SPAWN
    public static readonly TicketType PlayerSpawn = Register("player_spawn", 20, FlagLoading);
    //SpawnSearch 出生点搜索票 1 tick 超时对应原版 SPAWN_SEARCH
    public static readonly TicketType SpawnSearch = Register("spawn_search", 1, FlagLoading);
    //Dragon 末影龙相关票对应原版 DRAGON
    public static readonly TicketType Dragon = Register("dragon", NoTimeout, FlagLoading | FlagSimulation);
    //PlayerLoading 玩家加载票对应原版 PLAYER_LOADING
    public static readonly TicketType PlayerLoading = Register("player_loading", NoTimeout, FlagLoading);
    //PlayerSimulation 玩家模拟票对应原版 PLAYER_SIMULATION
    public static readonly TicketType PlayerSimulation =
        Register("player_simulation", NoTimeout, FlagSimulation | FlagKeepDimensionActive);
    //Forced 强制加载票 /forceload 写入 两个会落盘的类型之一
    public static readonly TicketType Forced =
        Register("forced", NoTimeout, FlagPersist | FlagLoading | FlagSimulation | FlagKeepDimensionActive);
    //Portal 传送门票 300 tick 超时同样落盘
    public static readonly TicketType Portal =
        Register("portal", 300, FlagPersist | FlagLoading | FlagSimulation | FlagKeepDimensionActive);
    //EnderPearl 末影珍珠票 40 tick 超时对应原版 ENDER_PEARL
    public static readonly TicketType EnderPearl =
        Register("ender_pearl", 40, FlagLoading | FlagSimulation | FlagKeepDimensionActive);
    //Unknown 兜底票 区块未就绪也能超时清理对应原版 UNKNOWN
    public static readonly TicketType Unknown =
        Register("unknown", 1, FlagLoading | FlagCanExpireIfUnloaded);

    //Name 注册名落盘时写它 对应原版用注册表名序列化
    public Identifier Name { get; }
    //Timeout 票存活 tick 数 0 表示不过期
    public long Timeout { get; }
    //Flags 行为位掩码
    public int Flags { get; }

    private TicketType(Identifier name, long timeout, int flags)
    {
        Name = name;
        Timeout = timeout;
        Flags = flags;
    }

    private static TicketType Register(string name, long timeout, int flags)
    {
        var type = new TicketType(Identifier.WithDefaultNamespace(name), timeout, flags);
        _byName[type.Name] = type;
        _all.Add(type);
        return type;
    }

    //All 内置票类型全集 供启动时整批写进 TICKET_TYPE 注册表
    public static IReadOnlyList<TicketType> All => _all;

    //Bootstrap 把九种内置类型登记进 TICKET_TYPE 注册表 对应原版类加载时的 register
    //必须在注册表冻结之前调用 由 GameBootstrap 在数据加载前阶段触发
    public static void Bootstrap()
    {
        if (_registered) return;
        _registered = true;
        foreach (var type in _all)
            BuiltInRegistries.TICKET_TYPE.Register(
                ResourceKey<NetCraft.Registry.TicketType>.Create(Registries.TICKET_TYPE, type.Name),
                type, RegistrationInfo.BuiltIn);
    }

    //ByName 按注册名查类型 读盘的票要靠它还原
    public static TicketType? ByName(Identifier name) => _byName.GetValueOrDefault(name);

    //Persist 是否落盘对应原版 persist
    public bool Persist => (Flags & FlagPersist) != 0;
    //DoesLoad 是否参与加载追踪对应原版 doesLoad
    public bool DoesLoad => (Flags & FlagLoading) != 0;
    //DoesSimulate 是否参与模拟追踪对应原版 doesSimulate
    public bool DoesSimulate => (Flags & FlagSimulation) != 0;
    //ShouldKeepDimensionActive 是否让维度保持活跃对应原版 shouldKeepDimensionActive
    public bool ShouldKeepDimensionActive => (Flags & FlagKeepDimensionActive) != 0;
    //CanExpireIfUnloaded 区块未就绪也能超时清理对应原版 canExpireIfUnloaded
    public bool CanExpireIfUnloaded => (Flags & FlagCanExpireIfUnloaded) != 0;
    //HasTimeout 是否有超时对应原版 hasTimeout
    public bool HasTimeout => Timeout != NoTimeout;

    public override string ToString() => Name.ToString();
}
