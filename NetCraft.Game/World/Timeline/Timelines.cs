using NetCraft.Game.World.Clock;
using NetCraft.Registry;

namespace NetCraft.Game.World.Timeline;

//Timelines built-in timelines, maps to vanilla net.minecraft.world.timeline.Timelines
//Registration order follows lexicographic order to match SynchronizedRegistryData.Timeline and keep network ids aligned
//Vanilla's EnvironmentAttribute tracks are client-side environment sampling; this port keeps only periods and time markers
//The trailing Timeline namespace segment shadows the same-named type, so all references use the fully qualified name, following the Block/Entity convention
public static class Timelines
{
    public static readonly ResourceKey<NetCraft.Registry.Timeline> OVERWORLD_DAY = Key("day");
    public static readonly ResourceKey<NetCraft.Registry.Timeline> MOON = Key("moon");
    public static readonly ResourceKey<NetCraft.Registry.Timeline> VILLAGER_SCHEDULE = Key("villager_schedule");
    public static readonly ResourceKey<NetCraft.Registry.Timeline> EARLY_GAME = Key("early_game");

    //MoonPhaseCount moon phase period, 8 moon phases, maps to vanilla MoonPhase.COUNT
    private const int MoonPhaseCount = 8;

    public static void Bootstrap()
    {
        var overworldClock = WorldClocks.OverworldHolder
            ?? throw new InvalidOperationException("WorldClocks.Bootstrap must run before Timelines.Bootstrap");
        //day main day-night cycle, 24000 tick, four visible time markers for time set suggestions
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
        //early_game start decision line, no period
        Registry<NetCraft.Registry.Timeline>.RegisterForHolder(BuiltInRegistries.TIMELINE, EARLY_GAME, new NetCraft.Registry.Timeline(overworldClock, null));
        //moon moon phase cycle, 192000 tick
        Registry<NetCraft.Registry.Timeline>.RegisterForHolder(BuiltInRegistries.TIMELINE, MOON, new NetCraft.Registry.Timeline(overworldClock, 24000 * MoonPhaseCount));
        //villager_schedule villager schedule cycle, 24000 tick
        Registry<NetCraft.Registry.Timeline>.RegisterForHolder(BuiltInRegistries.TIMELINE, VILLAGER_SCHEDULE, new NetCraft.Registry.Timeline(overworldClock, 24000));
    }

    private static ResourceKey<NetCraft.Registry.Timeline> Key(string id)
        => ResourceKey<NetCraft.Registry.Timeline>.Create(Registries.TIMELINE, Identifier.WithDefaultNamespace(id));
}
