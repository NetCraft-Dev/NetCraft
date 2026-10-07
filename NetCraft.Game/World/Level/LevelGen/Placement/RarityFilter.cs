using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//RarityFilter rarity filter, maps to vanilla RarityFilter
//Places once every chance attempts on average
public sealed class RarityFilter : PlacementFilter
{
    //PositiveInt positive-number validation, maps to vanilla ExtraCodecs.POSITIVE_INT
    private static readonly Codec<int> PositiveInt = Codecs.Int.ComapFlatMap(
        value => value > 0
            ? DataResult<int>.Success(value)
            : DataResult<int>.Error(() => $"chance must be positive: {value}"),
        value => value);

    public static readonly Codec<RarityFilter> Codec =
        new SingleFieldPlacementCodec<RarityFilter, int>(
            PositiveInt.FieldOf("chance"),
            chance => new RarityFilter(chance),
            filter => filter.Chance);

    public int Chance { get; }

    private RarityFilter(int chance) => Chance = chance;

    //OnAverageOnceEvery construction entry, maps to vanilla onAverageOnceEvery
    public static RarityFilter OnAverageOnceEvery(int chance) => new(chance);

    protected override bool ShouldPlace(PlacementContext context, RandomSource random, BlockPos origin)
        => random.NextFloat() < 1.0f / Chance;

    public override PlacementModifierType Type => RarityFilterType.Instance;
}

//RarityFilterType, maps to vanilla PlacementModifierType.RARITY_FILTER
public sealed class RarityFilterType : PlacementModifierType<RarityFilter>
{
    public static readonly RarityFilterType Instance = Register(
        Identifier.WithDefaultNamespace("rarity_filter"), new RarityFilterType());

    private RarityFilterType()
        : base(Identifier.WithDefaultNamespace("rarity_filter"), RarityFilter.Codec) { }
}
