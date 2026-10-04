namespace NetCraft.Game.World.Clock;

//WorldClocks 内置世界时钟对应原版 net.minecraft.world.clock.WorldClocks
//注册顺序按字典序与 SynchronizedRegistryData.WorldClock 一致保证网络 id 对齐
public static class WorldClocks
{
    public static readonly ResourceKey<WorldClock> OVERWORLD = Key("overworld");
    public static readonly ResourceKey<WorldClock> THE_END = Key("the_end");

    //OverworldHolder 主世界时钟注册表引用 Timelines 注册内置时间线时引用
    public static Reference<WorldClock>? OverworldHolder { get; private set; }

    public static void Bootstrap()
    {
        OverworldHolder = Registry<WorldClock>.RegisterForHolder(BuiltInRegistries.WORLD_CLOCK, OVERWORLD, new WorldClock());
        Registry<WorldClock>.RegisterForHolder(BuiltInRegistries.WORLD_CLOCK, THE_END, new WorldClock());
    }

    private static ResourceKey<WorldClock> Key(string id)
        => ResourceKey<WorldClock>.Create(Registries.WORLD_CLOCK, Identifier.WithDefaultNamespace(id));
}
