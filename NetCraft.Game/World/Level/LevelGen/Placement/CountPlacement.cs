using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//CountPlacement count placement, maps to vanilla CountPlacement
//Samples a count from an int provider and places that many at the same position
public sealed class CountPlacement : RepeatingPlacement
{
    public static readonly Codec<CountPlacement> Codec =
        new SingleFieldPlacementCodec<CountPlacement, IntProvider>(
            IntProviders.NonNegativeCodec.FieldOf("count"),
            count => new CountPlacement(count),
            placement => placement.CountProvider);

    //CountProvider count provider; the property name avoids the base class Count method
    public IntProvider CountProvider { get; }

    private CountPlacement(IntProvider count) => CountProvider = count;

    //Of construction entry, maps to vanilla of
    public static CountPlacement Of(IntProvider count) => new(count);

    public static CountPlacement Of(int count) => new(ConstantInt.Of(count));

    protected override int Count(RandomSource random, BlockPos origin) => CountProvider.Sample(random);

    public override PlacementModifierType Type => CountPlacementType.Instance;
}

//CountPlacementType, maps to vanilla PlacementModifierType.COUNT
public sealed class CountPlacementType : PlacementModifierType<CountPlacement>
{
    public static readonly CountPlacementType Instance = Register(
        Identifier.WithDefaultNamespace("count"), new CountPlacementType());

    private CountPlacementType()
        : base(Identifier.WithDefaultNamespace("count"), CountPlacement.Codec) { }
}
