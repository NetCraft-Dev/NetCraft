using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//HeightRangePlacement 高度区间放置对应原版 HeightRangePlacement
//按高度提供者采样新的 y
public sealed class HeightRangePlacement : PlacementModifier
{
    public static readonly Codec<HeightRangePlacement> Codec =
        new SingleFieldPlacementCodec<HeightRangePlacement, HeightProvider>(
            HeightProvider.Codec.FieldOf("height"),
            height => new HeightRangePlacement(height),
            placement => placement.Height);

    public HeightProvider Height { get; }

    private HeightRangePlacement(HeightProvider height) => Height = height;

    //Of 构造入口对应原版 of
    public static HeightRangePlacement Of(HeightProvider height) => new(height);

    public static HeightRangePlacement Uniform(VerticalAnchor minInclusive, VerticalAnchor maxInclusive)
        => new(UniformHeight.Of(minInclusive, maxInclusive));

    public override IEnumerable<BlockPos> GetPositions(PlacementContext context, RandomSource random, BlockPos origin)
        => new[] { new BlockPos(origin.X, Height.Sample(random, context), origin.Z) };

    public override PlacementModifierType Type => HeightRangePlacementType.Instance;
}

//HeightRangePlacementType 对应原版 PlacementModifierType.HEIGHT_RANGE
public sealed class HeightRangePlacementType : PlacementModifierType<HeightRangePlacement>
{
    public static readonly HeightRangePlacementType Instance = Register(
        Identifier.WithDefaultNamespace("height_range"), new HeightRangePlacementType());

    private HeightRangePlacementType()
        : base(Identifier.WithDefaultNamespace("height_range"), HeightRangePlacement.Codec) { }
}
