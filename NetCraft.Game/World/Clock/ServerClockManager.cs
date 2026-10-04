using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.Server;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Storage;

namespace NetCraft.Game.World.Clock;

//ServerClockManager 服务端时钟管理器对应原版 net.minecraft.world.clock.ServerClockManager
//按 WorldClock 注册表为每个时钟维护 ClockInstance 状态机 rate 累积 partialTick 满一进位
//持久化为 overworld data 目录的 world_clocks.dat 修改时钟后向全服广播 SetTime 包
public sealed class ServerClockManager : SavedData, ClockManager
{
    //TypeId 存档标识 对应原版 SavedDataType 的 minecraft:world_clocks
    //存 data/minecraft/world_clocks.dat 带命名空间子目录
    private const string TypeId = "minecraft:world_clocks";

    //Type SavedData 工厂 tag 为磁盘读入的注册名到 ClockState 的 map 空则全新创建
    public static readonly SavedDataType<ServerClockManager> Type = new ClockManagerType();

    private readonly Dictionary<Holder<WorldClock>, ClockInstance> _clocks = new();
    private readonly Dictionary<Holder<WorldClock>, ClockState> _packedStates = new();
    private MinecraftServer _server = null!;

    public override string Id => TypeId;

    //ClockManagerType 工厂实现 存档 tag 先收着 Init 时按注册表展开
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

    //Init 遍历注册表建实例并从存档状态恢复 对应原版 init
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

    //Tick 每服务端 tick 推进所有时钟 对应原版 tick 受 ADVANCE_TIME 规则控制此规则未实现恒放行
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

    //MoveToTimeMarker 跳到下一次该时间标记 未注册该标记返回 false
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

    //ModifyClock 修改后广播该时钟新状态并标脏
    private void ModifyClock(Holder<WorldClock> clock, Action<ClockInstance> action)
    {
        var instance = GetInstance(clock);
        action(instance);
        var update = instance.PackNetworkState(ClockIdOf(clock));
        _server.PlayerList.BroadcastAll(new ClientboundSetTimePacket(_server.Overworld.GameTime, new[] { update }));
        SetDirty();
    }

    public long GetTotalTicks(Holder<WorldClock> definition) => GetInstance(definition).TotalTicks;

    //CreateFullSyncPacket 玩家加入时发送全量时钟状态
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

    //CommandTimeMarkersForClock 列出该时钟下命令可见的时间标记 id
    public IEnumerable<ResourceKey<ClockTimeMarker>> CommandTimeMarkersForClock(Holder<WorldClock> clock)
        => GetInstance(clock).TimeMarkers
            .Where(e => e.Value.ShowInCommands)
            .Select(e => e.Key);

    private static int ClockIdOf(Holder<WorldClock> clock)
        => BuiltInRegistries.WORLD_CLOCK.GetId(clock.Value);

    private ClockInstance GetInstance(Holder<WorldClock> definition)
        => _clocks.TryGetValue(definition, out var instance)
            ? instance
            : throw new InvalidOperationException($"时钟未初始化: {definition.RegisteredName}");

    //Save 写为 注册名 -> ClockState 的 map 对应原版 PackedClockStates.CODEC
    public override CompoundTag Save(CompoundTag tag)
    {
        foreach (var (holder, instance) in _clocks)
            tag.Put(holder.RegisteredName, instance.PackState().Save(new CompoundTag()));
        return tag;
    }

    //ClockInstance 单时钟运行时状态对应原版 ServerClockManager.ClockInstance
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

        //PackNetworkState 暂停时网络侧 rate 归零客户端据此停走
        public ClientboundSetTimePacket.ClockUpdate PackNetworkState(int clockId)
            => new(clockId, TotalTicks, PartialTick, Paused ? 0f : Rate);
    }
}
