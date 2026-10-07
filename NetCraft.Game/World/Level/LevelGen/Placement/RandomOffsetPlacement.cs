using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//RandomOffsetPlacement random offset, maps to vanilla RandomOffsetPlacement
//Offsets xz and y using two int providers
public sealed class RandomOffsetPlacement : PlacementModifier
{
    //SpreadCodec offset range validation, maps to vanilla IntProviders.codec(-16, 16)
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

    //Of construction entry, maps to vanilla of
    public static RandomOffsetPlacement Of(IntProvider xzSpread, IntProvider ySpread) => new(xzSpread, ySpread);

    //OfTriangle triangular distribution construction, maps to vanilla ofTriangle
    public static RandomOffsetPlacement OfTriangle(int xzRange, int yRange)
        => new(TrapezoidInt.Triangle(xzRange), TrapezoidInt.Triangle(yRange));

    //Vertical vertical offset only, maps to vanilla vertical
    public static RandomOffsetPlacement Vertical(IntProvider ySpread) => new(ConstantInt.Of(0), ySpread);

    //Horizontal horizontal offset only, maps to vanilla horizontal
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

//RandomOffsetPlacementType, maps to vanilla PlacementModifierType.RANDOM_OFFSET
public sealed class RandomOffsetPlacementType : PlacementModifierType<RandomOffsetPlacement>
{
    public static readonly RandomOffsetPlacementType Instance = Register(
        Identifier.WithDefaultNamespace("random_offset"), new RandomOffsetPlacementType());

    private RandomOffsetPlacementType()
        : base(Identifier.WithDefaultNamespace("random_offset"), RandomOffsetPlacement.Codec) { }
}
