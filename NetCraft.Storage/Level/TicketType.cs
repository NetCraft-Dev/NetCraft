using NetCraft.Registry;

namespace NetCraft.Storage;

//TicketType, chunk ticket type, maps to vanilla net.minecraft.server.level.TicketType
//timeout decides how long a ticket lasts; flags decide whether it takes part in loading/simulation/persistence/keeping the dimension active
public sealed class TicketType : NetCraft.Registry.TicketType
{
    //NoTimeout, no-timeout sentinel, maps to vanilla NO_TIMEOUT
    public const long NoTimeout = 0;

    //FlagPersist, kept on disk, maps to vanilla FLAG_PERSIST
    public const int FlagPersist = 1;
    //FlagLoading, takes part in loading tracking, maps to vanilla FLAG_LOADING
    public const int FlagLoading = 2;
    //FlagSimulation, takes part in simulation tracking, maps to vanilla FLAG_SIMULATION
    public const int FlagSimulation = 4;
    //FlagKeepDimensionActive, keeps the dimension active, maps to vanilla FLAG_KEEP_DIMENSION_ACTIVE
    public const int FlagKeepDimensionActive = 8;
    //FlagCanExpireIfUnloaded, can expire on timeout even if the chunk is not ready, maps to vanilla FLAG_CAN_EXPIRE_IF_UNLOADED
    public const int FlagCanExpireIfUnloaded = 16;

    //_byName, built-in types indexed by registry name, restored by name on load
    private static readonly Dictionary<Identifier, TicketType> _byName = new();
    private static readonly List<TicketType> _all = new();
    private static bool _registered;

    //PlayerSpawn, player spawn preload ticket, 20 tick timeout, maps to vanilla PLAYER_SPAWN
    public static readonly TicketType PlayerSpawn = Register("player_spawn", 20, FlagLoading);
    //SpawnSearch, spawn search ticket, 1 tick timeout, maps to vanilla SPAWN_SEARCH
    public static readonly TicketType SpawnSearch = Register("spawn_search", 1, FlagLoading);
    //Dragon, ender dragon ticket, maps to vanilla DRAGON
    public static readonly TicketType Dragon = Register("dragon", NoTimeout, FlagLoading | FlagSimulation);
    //PlayerLoading, player loading ticket, maps to vanilla PLAYER_LOADING
    public static readonly TicketType PlayerLoading = Register("player_loading", NoTimeout, FlagLoading);
    //PlayerSimulation, player simulation ticket, maps to vanilla PLAYER_SIMULATION
    public static readonly TicketType PlayerSimulation =
        Register("player_simulation", NoTimeout, FlagSimulation | FlagKeepDimensionActive);
    //Forced, force-load ticket written by /forceload, one of the two types that persist
    public static readonly TicketType Forced =
        Register("forced", NoTimeout, FlagPersist | FlagLoading | FlagSimulation | FlagKeepDimensionActive);
    //Portal, portal ticket, 300 tick timeout, also persists
    public static readonly TicketType Portal =
        Register("portal", 300, FlagPersist | FlagLoading | FlagSimulation | FlagKeepDimensionActive);
    //EnderPearl, ender pearl ticket, 40 tick timeout, maps to vanilla ENDER_PEARL
    public static readonly TicketType EnderPearl =
        Register("ender_pearl", 40, FlagLoading | FlagSimulation | FlagKeepDimensionActive);
    //Unknown, fallback ticket, can expire on timeout even if the chunk is not ready, maps to vanilla UNKNOWN
    public static readonly TicketType Unknown =
        Register("unknown", 1, FlagLoading | FlagCanExpireIfUnloaded);

    //Name, the registry name written on save, matching how vanilla serializes by registry name
    public Identifier Name { get; }
    //Timeout, ticket lifetime in ticks; 0 means it never expires
    public long Timeout { get; }
    //Flags, behavior bitmask
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

    //All, the full set of built-in ticket types, written into the TICKET_TYPE registry in one batch at startup
    public static IReadOnlyList<TicketType> All => _all;

    //Bootstrap registers the nine built-in types into the TICKET_TYPE registry, maps to the register at class load in vanilla
    //Must be called before the registry freezes; GameBootstrap triggers it in the pre-data-load stage
    public static void Bootstrap()
    {
        if (_registered) return;
        _registered = true;
        foreach (var type in _all)
            BuiltInRegistries.TICKET_TYPE.Register(
                ResourceKey<NetCraft.Registry.TicketType>.Create(Registries.TICKET_TYPE, type.Name),
                type, RegistrationInfo.BuiltIn);
    }

    //ByName looks up a type by registry name; tickets read from disk are restored through it
    public static TicketType? ByName(Identifier name) => _byName.GetValueOrDefault(name);

    //Persist, whether it is kept on disk, maps to vanilla persist
    public bool Persist => (Flags & FlagPersist) != 0;
    //DoesLoad, whether it takes part in loading tracking, maps to vanilla doesLoad
    public bool DoesLoad => (Flags & FlagLoading) != 0;
    //DoesSimulate, whether it takes part in simulation tracking, maps to vanilla doesSimulate
    public bool DoesSimulate => (Flags & FlagSimulation) != 0;
    //ShouldKeepDimensionActive, whether it keeps the dimension active, maps to vanilla shouldKeepDimensionActive
    public bool ShouldKeepDimensionActive => (Flags & FlagKeepDimensionActive) != 0;
    //CanExpireIfUnloaded, can expire on timeout even if the chunk is not ready, maps to vanilla canExpireIfUnloaded
    public bool CanExpireIfUnloaded => (Flags & FlagCanExpireIfUnloaded) != 0;
    //HasTimeout, whether it has a timeout, maps to vanilla hasTimeout
    public bool HasTimeout => Timeout != NoTimeout;

    public override string ToString() => Name.ToString();
}
