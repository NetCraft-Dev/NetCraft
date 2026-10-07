using NetCraft.Codec;

namespace NetCraft.Game.World.Level.LevelGen;

//NoiseRouter noise router table, maps to vanilla net.minecraft.world.level.levelgen.NoiseRouter
//Holds 15 DensityFunctions for terrain/climate/caves/veins that describe density across the dimension
//NoiseBasedChunkGenerator looks up concrete density functions through this table for generation decisions
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
    //PreliminarySurfaceLevel preliminary surface height field, replaced the old initial_density_without_jaggedness in vanilla 26.2
    //Surface rules use it to compute minSurfaceLevel and decide whether a position is above the preliminary surface
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

    //Legacy14Args keeps the old 14-argument constructor, matching the old simplified signature
    //VeinGap defaults to Constant.Zero and does not affect existing callers; new code should use the 15-argument constructor
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

    //MapAll recursively applies the visitor to all 15 fields to replace nodes, maps to vanilla mapAll
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

    //Empty router with every field set to Constant.Zero, maps to vanilla NoiseRouter.EMPTY
    public static readonly NoiseRouter Empty = new(
        Constant.Zero, Constant.Zero, Constant.Zero, Constant.Zero,
        Constant.Zero, Constant.Zero, Constant.Zero, Constant.Zero,
        Constant.Zero, Constant.Zero, Constant.Zero, Constant.Zero,
        Constant.Zero, Constant.Zero, Constant.Zero);
}
