namespace NetCraft.Game.World.Clock;

//ClockTimeMarkers 内置时钟时间标记 key 对应原版 net.minecraft.world.clock.ClockTimeMarkers
//key 空间挂在 clock_time_marker 非同步注册表 仅用于 ResourceKey 命名
public static class ClockTimeMarkers
{
    public static readonly ResourceKey<ClockTimeMarker> DAY = CreateKey("day");
    public static readonly ResourceKey<ClockTimeMarker> NOON = CreateKey("noon");
    public static readonly ResourceKey<ClockTimeMarker> NIGHT = CreateKey("night");
    public static readonly ResourceKey<ClockTimeMarker> MIDNIGHT = CreateKey("midnight");
    public static readonly ResourceKey<ClockTimeMarker> WAKE_UP_FROM_SLEEP = CreateKey("wake_up_from_sleep");
    public static readonly ResourceKey<ClockTimeMarker> ROLL_VILLAGE_SIEGE = CreateKey("roll_village_siege");

    public static ResourceKey<ClockTimeMarker> CreateKey(string name)
        => ResourceKey<ClockTimeMarker>.Create(Registries.CLOCK_TIME_MARKER, Identifier.WithDefaultNamespace(name));
}
