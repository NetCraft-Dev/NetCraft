using NetCraft.Codec;

namespace NetCraft.Game.World.Level.LevelGen;

//NoiseRouter 噪声路由表对应原版 net.minecraft.world.level.levelgen.NoiseRouter
//持有地形/气候/洞穴/veins 等 15 个 DensityFunction 描述世界各维度密度
//NoiseBasedChunkGenerator 通过此表查找具体密度函数用于生成决策
public sealed class NoiseRouter
{
    public DensityFunction Barrier { get; }
    public DensityFunction FluidLevelFloodedness { get; }
    public DensityFunction FluidLevelSpread { get; }
    public DensityFunction Lava { get; }
    public DensityFunction Temperature { get; }
    public DensityFunction Vegetation { get; }
    public DensityFunction Continents { get; }
    public DensityFunction Erosion { get; }
    public DensityFunction Depth { get; }
    public DensityFunction Ridges { get; }
    //PreliminarySurfaceLevel 初步地表高度字段 原版 26.2 起取代旧的 initial_density_without_jaggedness
    //地表规则靠它算 minSurfaceLevel 判断是否在初步地表之上
    public DensityFunction PreliminarySurfaceLevel { get; }
    public DensityFunction FinalDensity { get; }
    public DensityFunction VeinToggle { get; }
    public DensityFunction VeinRidged { get; }
    public DensityFunction VeinGap { get; }

    public NoiseRouter(
        DensityFunction barrier,
        DensityFunction fluidLevelFloodedness,
        DensityFunction fluidLevelSpread,
        DensityFunction lava,
        DensityFunction temperature,
        DensityFunction vegetation,
        DensityFunction continents,
        DensityFunction erosion,
        DensityFunction depth,
        DensityFunction ridges,
        DensityFunction preliminarySurfaceLevel,
        DensityFunction finalDensity,
        DensityFunction veinToggle,
        DensityFunction veinRidged,
        DensityFunction veinGap)
    {
        Barrier = barrier;
        FluidLevelFloodedness = fluidLevelFloodedness;
        FluidLevelSpread = fluidLevelSpread;
        Lava = lava;
        Temperature = temperature;
        Vegetation = vegetation;
        Continents = continents;
        Erosion = erosion;
        Depth = depth;
        Ridges = ridges;
        PreliminarySurfaceLevel = preliminarySurfaceLevel;
        FinalDensity = finalDensity;
        VeinToggle = veinToggle;
        VeinRidged = veinRidged;
        VeinGap = veinGap;
    }

    //Legacy14Args 兼容旧 14 参数构造函数对应旧简化版签名
    //VeinGap 默认 Constant.Zero 不影响已有调用方新代码应用 15 参数构造
    public NoiseRouter(
        DensityFunction barrier,
        DensityFunction fluidLevelFloodedness,
        DensityFunction fluidLevelSpread,
        DensityFunction lava,
        DensityFunction temperature,
        DensityFunction vegetation,
        DensityFunction continents,
        DensityFunction erosion,
        DensityFunction depth,
        DensityFunction ridges,
        DensityFunction initialDensityWithoutJaggedness,
        DensityFunction finalDensity,
        DensityFunction veinToggle,
        DensityFunction veinRidged)
        : this(barrier, fluidLevelFloodedness, fluidLevelSpread, lava,
            temperature, vegetation, continents, erosion, depth, ridges,
            initialDensityWithoutJaggedness, finalDensity, veinToggle, veinRidged,
            Constant.Zero)
    {
    }

    //MapAll 对所有 15 个字段递归应用 visitor 替换节点对应原版 mapAll
    public NoiseRouter MapAll(Visitor visitor)
        => new(
            Barrier.MapAll(visitor),
            FluidLevelFloodedness.MapAll(visitor),
            FluidLevelSpread.MapAll(visitor),
            Lava.MapAll(visitor),
            Temperature.MapAll(visitor),
            Vegetation.MapAll(visitor),
            Continents.MapAll(visitor),
            Erosion.MapAll(visitor),
            Depth.MapAll(visitor),
            Ridges.MapAll(visitor),
            PreliminarySurfaceLevel.MapAll(visitor),
            FinalDensity.MapAll(visitor),
            VeinToggle.MapAll(visitor),
            VeinRidged.MapAll(visitor),
            VeinGap.MapAll(visitor));

    //Empty 全部为 Constant.Zero 的空路由器对应原版 NoiseRouter.EMPTY
    public static readonly NoiseRouter Empty = new(
        Constant.Zero, Constant.Zero, Constant.Zero, Constant.Zero,
        Constant.Zero, Constant.Zero, Constant.Zero, Constant.Zero,
        Constant.Zero, Constant.Zero, Constant.Zero, Constant.Zero,
        Constant.Zero, Constant.Zero, Constant.Zero);
}
