using NetCraft.Registry;

namespace NetCraft.Game.World.Level.Material;

//Fluids the five built-in fluids, maps to vanilla net.minecraft.world.level.material.Fluids
//Registration order follows vanilla empty flowing_water water flowing_lava lava; registry ids and network sync depend on it
//Static fields initialise in textual order, so Empty must come before the other four
public static class Fluids
{
    //Empty empty fluid, the same instance as Fluid.Empty; only the empty state resolves to a registry identity
    public static readonly Fluid Empty = Register(FluidIds.Empty, Fluid.Empty);

    public static readonly FlowingFluid FlowingWater = Register(FluidIds.FlowingWater, new WaterFluid.Flowing());

    public static readonly FlowingFluid Water = Register(FluidIds.Water, new WaterFluid.Source());

    public static readonly FlowingFluid FlowingLava = Register(FluidIds.FlowingLava, new LavaFluid.Flowing());

    public static readonly FlowingFluid Lava = Register(FluidIds.Lava, new LavaFluid.Source());

    private static T Register<T>(ResourceKey<Fluid> key, T fluid) where T : Fluid
    {
        BuiltInRegistries.FLUID.Register(key, fluid, RegistrationInfo.BuiltIn);
        return fluid;
    }
}
