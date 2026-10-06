namespace NetCraft.Game.World.Clock;

//ClockTimeMarkers built-in clock time marker keys, maps to vanilla net.minecraft.world.clock.ClockTimeMarkers
//The key namespace lives in clock_time_marker, a non-synchronized registry only used for ResourceKey naming
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
