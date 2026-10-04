using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//SurfaceRelativeThresholdFilter 相对地表高度阈值过滤对应原版 SurfaceRelativeThresholdFilter
//位置 y 落在高度图相对区间内才保留
public sealed class SurfaceRelativeThresholdFilter : PlacementFilter
{
    public static readonly Codec<SurfaceRelativeThresholdFilter> Codec =
        RecordCodecBuilder.Of3<SurfaceRelativeThresholdFilter, Heightmap.Types, int, int>(
            HeightmapTypesCodec.Instance.FieldOf("heightmap")
                .ForGetter<SurfaceRelativeThresholdFilter, Heightmap.Types>(filter => filter.Heightmap),
            //原版默认下界是 ChunkSkyLightSources.NEGATIVE_INFINITY 即 int 最小值
            Codecs.Int.OptionalFieldOf("min_inclusive", int.MinValue)
                .ForGetter<SurfaceRelativeThresholdFilter, int>(filter => filter.MinInclusive),
            Codecs.Int.OptionalFieldOf("max_inclusive", int.MaxValue)
                .ForGetter<SurfaceRelativeThresholdFilter, int>(filter => filter.MaxInclusive),
            (heightmap, minInclusive, maxInclusive) =>
                new SurfaceRelativeThresholdFilter(heightmap, minInclusive, maxInclusive));

    public Heightmap.Types Heightmap { get; }
    public int MinInclusive { get; }
    public int MaxInclusive { get; }

    private SurfaceRelativeThresholdFilter(Heightmap.Types heightmap, int minInclusive, int maxInclusive)
    {
        Heightmap = heightmap;
        MinInclusive = minInclusive;
        MaxInclusive = maxInclusive;
    }

    //Of 构造入口对应原版 of
    public static SurfaceRelativeThresholdFilter Of(Heightmap.Types heightmap, int minInclusive, int maxInclusive)
        => new(heightmap, minInclusive, maxInclusive);

    protected override bool ShouldPlace(PlacementContext context, RandomSource random, BlockPos origin)
    {
        long surfaceY = context.Level.GetHeight(Heightmap, origin.X, origin.Z);
        return surfaceY + MinInclusive <= origin.Y && origin.Y <= surfaceY + MaxInclusive;
    }

    public override PlacementModifierType Type => SurfaceRelativeThresholdFilterType.Instance;
}

//SurfaceRelativeThresholdFilterType 对应原版 PlacementModifierType.SURFACE_RELATIVE_THRESHOLD_FILTER
public sealed class SurfaceRelativeThresholdFilterType : PlacementModifierType<SurfaceRelativeThresholdFilter>
{
    public static readonly SurfaceRelativeThresholdFilterType Instance = Register(
        Identifier.WithDefaultNamespace("surface_relative_threshold_filter"), new SurfaceRelativeThresholdFilterType());

    private SurfaceRelativeThresholdFilterType()
        : base(Identifier.WithDefaultNamespace("surface_relative_threshold_filter"), SurfaceRelativeThresholdFilter.Codec) { }
}
