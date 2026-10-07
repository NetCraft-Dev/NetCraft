using NetCraft.Codec;
using NetCraft.Game.World.Level.LevelGen.BlockPredicates;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//BlockPredicateFilter block predicate filter, maps to vanilla BlockPredicateFilter
//Keep the position only when the predicate holds there
public sealed class BlockPredicateFilter : PlacementFilter
{
    public static readonly Codec<BlockPredicateFilter> Codec =
        new SingleFieldPlacementCodec<BlockPredicateFilter, BlockPredicate>(
            BlockPredicate.Codec.FieldOf("predicate"),
            predicate => new BlockPredicateFilter(predicate),
            filter => filter.Predicate);

    public BlockPredicate Predicate { get; }

    private BlockPredicateFilter(BlockPredicate predicate) => Predicate = predicate;

    //ForPredicate construction entry, maps to vanilla forPredicate
    public static BlockPredicateFilter ForPredicate(BlockPredicate predicate) => new(predicate);

    protected override bool ShouldPlace(PlacementContext context, RandomSource random, BlockPos origin)
        => Predicate.Test(context.Level, origin);

    public override PlacementModifierType Type => BlockPredicateFilterType.Instance;
}

//BlockPredicateFilterType, maps to vanilla PlacementModifierType.BLOCK_PREDICATE_FILTER
public sealed class BlockPredicateFilterType : PlacementModifierType<BlockPredicateFilter>
{
    public static readonly BlockPredicateFilterType Instance = Register(
        Identifier.WithDefaultNamespace("block_predicate_filter"), new BlockPredicateFilterType());

    private BlockPredicateFilterType()
        : base(Identifier.WithDefaultNamespace("block_predicate_filter"), BlockPredicateFilter.Codec) { }
}
