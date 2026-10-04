using NetCraft.Game.World.Clock;
using NetCraft.Registry;

namespace NetCraft.Game.World.Timeline;

//Timelines 内置时间线对应原版 net.minecraft.world.timeline.Timelines
//注册顺序按字典序与 SynchronizedRegistryData.Timeline 一致保证网络 id 对齐
//原版的 EnvironmentAttribute 轨道为客户端环境采样此移植仅保留周期与时间标记
//命名空间末段 Timeline 遮蔽同名类型所有引用用全限定名 对齐 Block/Entity 惯例
public static class Timelines
{
    public static readonly ResourceKey<NetCraft.Registry.Timeline> OVERWORLD_DAY = Key("day");
    public static readonly ResourceKey<NetCraft.Registry.Timeline> MOON = Key("moon");
    public static readonly ResourceKey<NetCraft.Registry.Timeline> VILLAGER_SCHEDULE = Key("villager_schedule");
    public static readonly ResourceKey<NetCraft.Registry.Timeline> EARLY_GAME = Key("early_game");

    //MoonPhaseCount 月相周期 8 个月相 对应原版 MoonPhase.COUNT
    private const int MoonPhaseCount = 8;

    public static void Bootstrap()
    {
        var overworldClock = WorldClocks.OverworldHolder
            ?? throw new InvalidOperationException("WorldClocks.Bootstrap 必须先于 Timelines.Bootstrap");
        //day 主昼夜周期 24000 tick 四个可见时间标记供 time set 建议
        Registry<NetCraft.Registry.Timeline>.RegisterForHolder(BuiltInRegistries.TIMELINE, OVERWORLD_DAY, new NetCraft.Registry.Timeline(
            overworldClock, 24000, new Dictionary<ResourceKey<ClockTimeMarker>, NetCraft.Registry.Timeline.TimeMarkerInfo>
            {
                [ClockTimeMarkers.DAY] = new(1000, true),
                [ClockTimeMarkers.NOON] = new(6000, true),
                [ClockTimeMarkers.NIGHT] = new(13000, true),
                [ClockTimeMarkers.MIDNIGHT] = new(18000, true),
                [ClockTimeMarkers.WAKE_UP_FROM_SLEEP] = new(0, false),
                [ClockTimeMarkers.ROLL_VILLAGE_SIEGE] = new(18000, false),
            }));
        //early_game 开局判定线 无周期
        Registry<NetCraft.Registry.Timeline>.RegisterForHolder(BuiltInRegistries.TIMELINE, EARLY_GAME, new NetCraft.Registry.Timeline(overworldClock, null));
        //moon 月相周期 192000 tick
        Registry<NetCraft.Registry.Timeline>.RegisterForHolder(BuiltInRegistries.TIMELINE, MOON, new NetCraft.Registry.Timeline(overworldClock, 24000 * MoonPhaseCount));
        //villager_schedule 村民作息周期 24000 tick
        Registry<NetCraft.Registry.Timeline>.RegisterForHolder(BuiltInRegistries.TIMELINE, VILLAGER_SCHEDULE, new NetCraft.Registry.Timeline(overworldClock, 24000));
    }

    private static ResourceKey<NetCraft.Registry.Timeline> Key(string id)
        => ResourceKey<NetCraft.Registry.Timeline>.Create(Registries.TIMELINE, Identifier.WithDefaultNamespace(id));
}
