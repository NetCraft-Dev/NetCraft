using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//SurfaceWaterDepthFilter 地表水深过滤对应原版 SurfaceWaterDepthFilter
//水面与海底高差不超过上限才保留
public sealed class SurfaceWaterDepthFilter : PlacementFilter
{
    public static readonly Codec<SurfaceWaterDepthFilter> Codec =
        new SingleFieldPlacementCodec<SurfaceWaterDepthFilter, int>(
            Codecs.Int.FieldOf("max_water_depth"),
            maxWaterDepth => new SurfaceWaterDepthFilter(maxWaterDepth),
            filter => filter.MaxWaterDepth);

    public int MaxWaterDepth { get; }

    private SurfaceWaterDepthFilter(int maxWaterDepth) => MaxWaterDepth = maxWaterDepth;

    //ForMaxDepth 构造入口对应原版 forMaxDepth
    public static SurfaceWaterDepthFilter ForMaxDepth(int maxWaterDepth) => new(maxWaterDepth);

    protected override bool ShouldPlace(PlacementContext context, RandomSource random, BlockPos origin)
    {
        var yOceanFloor = context.Level.GetHeight(Heightmap.Types.OceanFloor, origin.X, origin.Z);
        var ySurfaceFloor = context.Level.GetHeight(Heightmap.Types.WorldSurface, origin.X, origin.Z);
        return ySurfaceFloor - yOceanFloor <= MaxWaterDepth;
    }

    public override PlacementModifierType Type => SurfaceWaterDepthFilterType.Instance;
}

//SurfaceWaterDepthFilterType 对应原版 PlacementModifierType.SURFACE_WATER_DEPTH_FILTER
public sealed class SurfaceWaterDepthFilterType : PlacementModifierType<SurfaceWaterDepthFilter>
{
    public static readonly SurfaceWaterDepthFilterType Instance = Register(
        Identifier.WithDefaultNamespace("surface_water_depth_filter"), new SurfaceWaterDepthFilterType());

    private SurfaceWaterDepthFilterType()
        : base(Identifier.WithDefaultNamespace("surface_water_depth_filter"), SurfaceWaterDepthFilter.Codec) { }
}
