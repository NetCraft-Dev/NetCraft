using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//RarityFilter 稀有度过滤对应原版 RarityFilter
//平均每 chance 次放一次
public sealed class RarityFilter : PlacementFilter
{
    //PositiveInt 正数校验对应原版 ExtraCodecs.POSITIVE_INT
    private static readonly Codec<int> PositiveInt = Codecs.Int.ComapFlatMap(
        value => value > 0
            ? DataResult<int>.Success(value)
            : DataResult<int>.Error(() => $"chance 必须为正数: {value}"),
        value => value);

    public static readonly Codec<RarityFilter> Codec =
        new SingleFieldPlacementCodec<RarityFilter, int>(
            PositiveInt.FieldOf("chance"),
            chance => new RarityFilter(chance),
            filter => filter.Chance);

    public int Chance { get; }

    private RarityFilter(int chance) => Chance = chance;

    //OnAverageOnceEvery 构造入口对应原版 onAverageOnceEvery
    public static RarityFilter OnAverageOnceEvery(int chance) => new(chance);

    protected override bool ShouldPlace(PlacementContext context, RandomSource random, BlockPos origin)
        => random.NextFloat() < 1.0f / Chance;

    public override PlacementModifierType Type => RarityFilterType.Instance;
}

//RarityFilterType 对应原版 PlacementModifierType.RARITY_FILTER
public sealed class RarityFilterType : PlacementModifierType<RarityFilter>
{
    public static readonly RarityFilterType Instance = Register(
        Identifier.WithDefaultNamespace("rarity_filter"), new RarityFilterType());

    private RarityFilterType()
        : base(Identifier.WithDefaultNamespace("rarity_filter"), RarityFilter.Codec) { }
}
