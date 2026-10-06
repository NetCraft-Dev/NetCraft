using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.Server;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.World.Clock;

//ServerClockManager server-side clock manager, maps to vanilla net.minecraft.world.clock.ServerClockManager
//Maintains a ClockInstance state machine per clock from the WorldClock registry; rate accumulates partialTick and carries on reaching one
//Persisted as world_clocks.dat in the overworld data directory; after changing a clock it broadcasts a SetTime packet to the whole server
public sealed class ServerClockManager : SavedData, ClockManager
{
    //TypeId save id, maps to the minecraft:world_clocks of vanilla SavedDataType
    //Stored in data/minecraft/world_clocks.dat under a namespaced subdirectory
    private const string TypeId = "minecraft:world_clocks";

    //Type SavedData factory; tag maps registry names read from disk to ClockState; empty means create fresh
    public static readonly SavedDataType<ServerClockManager> Type = new ClockManagerType();

    private readonly Dictionary<Holder<WorldClock>, ClockInstance> _clocks = new();
    private readonly Dictionary<Holder<WorldClock>, ClockState> _packedStates = new();
    private MinecraftServer _server = null!;

    public override string Id => TypeId;

    //ClockManagerType factory implementation; the save tag is kept and expanded against the registry in Init
    private sealed class ClockManagerType : SavedDataType<ServerClockManager>
    {
        public string Id => TypeId;

        public ServerClockManager Create(CompoundTag tag, RegistryAccess registryAccess)
        {
            var manager = new ServerClockManager();
            foreach (var (name, value) in tag)
            {
                if (value is not CompoundTag state)
                    continue;
                var holder = BuiltInRegistries.WORLD_CLOCK.Get(Identifier.Parse(name));
                if (holder is not null)
                    manager._packedStates[holder] = ClockState.Load(state);
            }
            return manager;
        }
    }

    //Init iterates the registry to build instances and restores from the saved state, maps to vanilla init
    public void Init(MinecraftServer server)
    {
        _server = server;
        foreach (var holder in BuiltInRegistries.WORLD_CLOCK.ListElements())
            _clocks[holder] = new ClockInstance();
        foreach (var holder in BuiltInRegistries.TIMELINE.ListElements())
            holder.Value.RegisterTimeMarkers(RegisterTimeMarker);
        foreach (var (holder, state) in _packedStates)
        {
            if (_clocks.TryGetValue(holder, out var instance))
                instance.LoadFrom(state);
        }
        _packedStates.Clear();
    }

    private void RegisterTimeMarker(ResourceKey<ClockTimeMarker> timeMarkerId, ClockTimeMarker timeMarker)
        => GetInstance(timeMarker.Clock).TimeMarkers[timeMarkerId] = timeMarker;

    //Tick advances all clocks every server tick, maps to vanilla tick; controlled by the ADVANCE_TIME rule which is unimplemented here and always passes
    public void Tick()
    {
        foreach (var instance in _clocks.Values)
            instance.Tick();
        SetDirty();
    }

    public void SetTotalTicks(Holder<WorldClock> clock, long totalTicks)
        => ModifyClock(clock, instance =>
        {
            instance.TotalTicks = totalTicks;
            instance.PartialTick = 0f;
        });

    //MoveToTimeMarker jumps to the next occurrence of the time marker; returns false when the marker is unregistered
    public bool MoveToTimeMarker(Holder<WorldClock> clock, ResourceKey<ClockTimeMarker> timeMarkerId)
    {
        var moved = false;
        ModifyClock(clock, instance =>
        {
            if (!instance.TimeMarkers.TryGetValue(timeMarkerId, out var timeMarker))
                return;
            instance.TotalTicks = timeMarker.ResolveTimeToMoveTo(instance.TotalTicks);
            instance.PartialTick = 0f;
            moved = true;
        });
        return moved;
    }

    public void AddTicks(Holder<WorldClock> clock, int ticks)
        => ModifyClock(clock, instance => instance.TotalTicks = Math.Max(instance.TotalTicks + ticks, 0L));

    public void SetPaused(Holder<WorldClock> clock, bool paused)
        => ModifyClock(clock, instance => instance.Paused = paused);

    public void SetRate(Holder<WorldClock> clock, float rate)
        => ModifyClock(clock, instance => instance.Rate = rate);

    //ModifyClock after a change broadcasts the clock's new state and marks it dirty
    private void ModifyClock(Holder<WorldClock> clock, Action<ClockInstance> action)
    {
        var instance = GetInstance(clock);
        action(instance);
        var update = instance.PackNetworkState(ClockIdOf(clock));
        _server.PlayerList.BroadcastAll(new ClientboundSetTimePacket(_server.Overworld.GameTime, new[] { update }));
        SetDirty();
    }

    public long GetTotalTicks(Holder<WorldClock> definition) => GetInstance(definition).TotalTicks;

    //CreateFullSyncPacket sends the full clock state when a player joins
    public ClientboundSetTimePacket CreateFullSyncPacket()
    {
        var updates = new List<ClientboundSetTimePacket.ClockUpdate>(_clocks.Count);
        foreach (var (holder, instance) in _clocks)
            updates.Add(instance.PackNetworkState(ClockIdOf(holder)));
        return new ClientboundSetTimePacket(_server.Overworld.GameTime, updates);
    }

    public bool IsAtTimeMarker(Holder<WorldClock> clock, ResourceKey<ClockTimeMarker> timeMarkerId)
    {
        var instance = GetInstance(clock);
        return instance.TimeMarkers.TryGetValue(timeMarkerId, out var timeMarker)
            && timeMarker.OccursAt(instance.TotalTicks);
    }

    //CommandTimeMarkersForClock lists the command-visible time marker ids for the clock
    public IEnumerable<ResourceKey<ClockTimeMarker>> CommandTimeMarkersForClock(Holder<WorldClock> clock)
        => GetInstance(clock).TimeMarkers
            .Where(e => e.Value.ShowInCommands)
            .Select(e => e.Key);

    private static int ClockIdOf(Holder<WorldClock> clock)
        => BuiltInRegistries.WORLD_CLOCK.GetId(clock.Value);

    private ClockInstance GetInstance(Holder<WorldClock> definition)
        => _clocks.TryGetValue(definition, out var instance)
            ? instance
            : throw new InvalidOperationException($"clock not initialized: {definition.RegisteredName}");

    //Save written as a registry name -> ClockState map, maps to vanilla PackedClockStates.CODEC
    public override CompoundTag Save(CompoundTag tag)
    {
        foreach (var (holder, instance) in _clocks)
            tag.Put(holder.RegisteredName, instance.PackState().Save(new CompoundTag()));
        return tag;
    }

    //ClockInstance single-clock runtime state, maps to vanilla ServerClockManager.ClockInstance
    private sealed class ClockInstance
    {
        public long TotalTicks;
        public float PartialTick;
        public bool Paused;
        public float Rate = ClockState.DefaultRate;
        public readonly Dictionary<ResourceKey<ClockTimeMarker>, ClockTimeMarker> TimeMarkers = new();

        public void LoadFrom(ClockState state)
        {
            TotalTicks = state.TotalTicks;
            PartialTick = state.PartialTick;
            Rate = state.Rate;
            Paused = state.Paused;
        }

        public void Tick()
        {
            if (Paused)
                return;
            PartialTick += Rate;
            var fullTicks = (int)MathF.Floor(PartialTick);
            PartialTick -= fullTicks;
            TotalTicks += fullTicks;
        }

        public ClockState PackState() => new(TotalTicks, PartialTick, Rate, Paused);

        //PackNetworkState when paused the network rate is zeroed so the client stops advancing
        public ClientboundSetTimePacket.ClockUpdate PackNetworkState(int clockId)
            => new(clockId, TotalTicks, PartialTick, Paused ? 0f : Rate);
    }
}
