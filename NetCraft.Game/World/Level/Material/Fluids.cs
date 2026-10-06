using NetCraft.Registry;

namespace NetCraft.Game.World.Level.Material;

//Fluids 五个内置流体 对应原版 net.minecraft.world.level.material.Fluids
//注册顺序照原版 empty flowing_water water flowing_lava lava 注册表 id 与网络同步都依赖它
//静态字段按文本序初始化 所以 Empty 必须排在其余四个之前
public static class Fluids
{
    //Empty 空流体 与 Fluid.Empty 是同一个实例 空状态才能查到注册表身份
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
