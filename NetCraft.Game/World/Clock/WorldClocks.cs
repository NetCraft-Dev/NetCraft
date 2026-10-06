namespace NetCraft.Game.World.Clock;

//WorldClocks built-in world clocks, maps to vanilla net.minecraft.world.clock.WorldClocks
//Registration order follows lexicographic order to match SynchronizedRegistryData.WorldClock and keep network ids aligned
public static class WorldClocks
{
    public static readonly ResourceKey<WorldClock> OVERWORLD = Key("overworld");
    public static readonly ResourceKey<WorldClock> THE_END = Key("the_end");

    //OverworldHolder overworld clock registry reference; referenced when Timelines registers built-in timelines
    public static Reference<WorldClock>? OverworldHolder { get; private set; }

    public static void Bootstrap()
    {
        OverworldHolder = Registry<WorldClock>.RegisterForHolder(BuiltInRegistries.WORLD_CLOCK, OVERWORLD, new WorldClock());
        Registry<WorldClock>.RegisterForHolder(BuiltInRegistries.WORLD_CLOCK, THE_END, new WorldClock());
    }

    private static ResourceKey<WorldClock> Key(string id)
        => ResourceKey<WorldClock>.Create(Registries.WORLD_CLOCK, Identifier.WithDefaultNamespace(id));
}
