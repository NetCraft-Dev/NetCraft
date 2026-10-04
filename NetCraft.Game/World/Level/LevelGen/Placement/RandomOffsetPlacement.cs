using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//RandomOffsetPlacement 随机偏移对应原版 RandomOffsetPlacement
//按两个整数提供者分别偏移 xz 与 y
public sealed class RandomOffsetPlacement : PlacementModifier
{
    //SpreadCodec 偏移范围校验对应原版 IntProviders.codec(-16, 16)
    private static readonly Codec<IntProvider> SpreadCodec = new RangeValidatedIntProviderCodec(IntProviders.Codec, -16, 16);

    public static readonly Codec<RandomOffsetPlacement> Codec =
        RecordCodecBuilder.Of2<RandomOffsetPlacement, IntProvider, IntProvider>(
            SpreadCodec.FieldOf("xz_spread")
                .ForGetter<RandomOffsetPlacement, IntProvider>(placement => placement.XzSpread),
            SpreadCodec.FieldOf("y_spread")
                .ForGetter<RandomOffsetPlacement, IntProvider>(placement => placement.YSpread),
            (xzSpread, ySpread) => new RandomOffsetPlacement(xzSpread, ySpread));

    public IntProvider XzSpread { get; }
    public IntProvider YSpread { get; }

    private RandomOffsetPlacement(IntProvider xzSpread, IntProvider ySpread)
    {
        XzSpread = xzSpread;
        YSpread = ySpread;
    }

    //Of 构造入口对应原版 of
    public static RandomOffsetPlacement Of(IntProvider xzSpread, IntProvider ySpread) => new(xzSpread, ySpread);

    //OfTriangle 三角分布构造对应原版 ofTriangle
    public static RandomOffsetPlacement OfTriangle(int xzRange, int yRange)
        => new(TrapezoidInt.Triangle(xzRange), TrapezoidInt.Triangle(yRange));

    //Vertical 只竖直偏移对应原版 vertical
    public static RandomOffsetPlacement Vertical(IntProvider ySpread) => new(ConstantInt.Of(0), ySpread);

    //Horizontal 只水平偏移对应原版 horizontal
    public static RandomOffsetPlacement Horizontal(IntProvider xzSpread) => new(xzSpread, ConstantInt.Of(0));

    public override IEnumerable<BlockPos> GetPositions(PlacementContext context, RandomSource random, BlockPos origin)
        => new[]
        {
            new BlockPos(
                origin.X + XzSpread.Sample(random),
                origin.Y + YSpread.Sample(random),
                origin.Z + XzSpread.Sample(random))
        };

    public override PlacementModifierType Type => RandomOffsetPlacementType.Instance;
}

//RandomOffsetPlacementType 对应原版 PlacementModifierType.RANDOM_OFFSET
public sealed class RandomOffsetPlacementType : PlacementModifierType<RandomOffsetPlacement>
{
    public static readonly RandomOffsetPlacementType Instance = Register(
        Identifier.WithDefaultNamespace("random_offset"), new RandomOffsetPlacementType());

    private RandomOffsetPlacementType()
        : base(Identifier.WithDefaultNamespace("random_offset"), RandomOffsetPlacement.Codec) { }
}
