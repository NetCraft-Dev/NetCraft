namespace NetCraft.Registry;

//FluidIds 流体注册键 对应原版 net.minecraft.world.level.material.FluidIds
public static class FluidIds
{
    public static readonly ResourceKey<Fluid> Empty = Key("empty");
    public static readonly ResourceKey<Fluid> FlowingWater = Key("flowing_water");
    public static readonly ResourceKey<Fluid> Water = Key("water");
    public static readonly ResourceKey<Fluid> FlowingLava = Key("flowing_lava");
    public static readonly ResourceKey<Fluid> Lava = Key("lava");

    private static ResourceKey<Fluid> Key(string name)
        => ResourceKey<Fluid>.Create(Registries.FLUID, Identifier.WithDefaultNamespace(name));
}
